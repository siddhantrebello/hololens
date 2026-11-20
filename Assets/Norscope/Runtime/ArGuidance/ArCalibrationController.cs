using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Norscope.Audio;

namespace Norscope.ArGuidance
{
    [DisallowMultipleComponent]
    public sealed class ArCalibrationController : MonoBehaviour
    {
        private const float PosEpsilonMeters = 0.00005f;
        private const float RotEpsilonDegrees = 0.05f;

        [SerializeField] private ArGuidanceRenderer guidanceRenderer;
        [SerializeField] private ArHologramLibrary library;
        [SerializeField] private string selectedComponentId = "h03_back_panel_outline";
        [SerializeField] private int positionStepIndex = 1; // 2 mm
        [SerializeField] private int rotationStepIndex = 1; // 1 deg
        [SerializeField] private bool lockHeight;
        [SerializeField] private bool lockRotation;

        /// <summary>Fires after every ShowSelected — error is null on success, or a human-
        /// readable message when the selected component's hologram couldn't be resolved.</summary>
        public event Action<string, string> SelectionResolved;

        private IArCalibrationRepository _repository;
        private XRGrabInteractable _activeGrab;
        private readonly ArCalibrationUndoStack _undo = new ArCalibrationUndoStack(20);
        private bool _undoBurstOpen;
        private Vector3 _grabStartPos;
        private Quaternion _grabStartRot;
        private Vector3 _grabStartScale;
        private bool _grabTracking;

        public string SelectedComponentId => selectedComponentId;
        public int PositionStepIndex => positionStepIndex;
        public int RotationStepIndex => rotationStepIndex;
        public bool LockHeight => lockHeight;
        public bool LockRotation => lockRotation;
        public float PositionStepMeters =>
            ArFineAdjustStep.PositionMeters[
                Mathf.Clamp(positionStepIndex, 0, ArFineAdjustStep.PositionMeters.Length - 1)];
        public float RotationStepDegrees =>
            ArFineAdjustStep.RotationDegrees[
                Mathf.Clamp(rotationStepIndex, 0, ArFineAdjustStep.RotationDegrees.Length - 1)];

        public void Bind(ArGuidanceRenderer renderer, IArCalibrationRepository repository, ArHologramLibrary hologramLibrary)
        {
            guidanceRenderer = renderer;
            _repository = repository;
            library = hologramLibrary;
            positionStepIndex = 1;
            rotationStepIndex = 1;
            _undo.Clear();
            _undoBurstOpen = false;
        }

        public void SelectComponent(string componentId)
        {
            ClearSelectionInteraction();
            selectedComponentId = componentId;
            _undo.Clear();
            _undoBurstOpen = false;
        }

        public void SetPositionStepIndex(int index)
        {
            positionStepIndex = Mathf.Clamp(index, 0, ArFineAdjustStep.PositionMeters.Length - 1);
        }

        public void SetRotationStepIndex(int index)
        {
            rotationStepIndex = Mathf.Clamp(index, 0, ArFineAdjustStep.RotationDegrees.Length - 1);
        }

        public void SetLockHeight(bool value) => lockHeight = value;
        public void SetLockRotation(bool value) => lockRotation = value;

        public Transform GetSelectedTransform()
        {
            ArHologramVisualBase visual = GetSelectedVisual();
            return visual != null ? visual.transform : null;
        }

        public ArHologramVisualBase GetSelectedVisual()
        {
            return guidanceRenderer != null ? guidanceRenderer.GetVisual(selectedComponentId) : null;
        }

        public void ShowSelected(bool enableManipulation)
        {
            if (guidanceRenderer == null)
                return;

            guidanceRenderer.HideAll();

            bool resolved = guidanceRenderer.EnsureResolved(selectedComponentId, _repository, library, out string error);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[AR Guidance][Calibration] EnsureResolved(\"" + selectedComponentId + "\") = " +
                      (resolved ? "OK" : "FAILED (" + error + ")"));
#endif
            if (!resolved)
            {
                SelectionResolved?.Invoke(selectedComponentId, error);
                return;
            }

            guidanceRenderer.SetVisible(selectedComponentId, true);
            ArHologramVisualBase visual = guidanceRenderer.GetVisual(selectedComponentId);
            if (visual == null)
            {
                SelectionResolved?.Invoke(selectedComponentId, "Hologram instance missing after resolution.");
                return;
            }

            // Task-guidance fade-outs can leave opacity at 0. Calibration show must restore
            // visibility appearance or the active GameObject is still fully transparent.
            visual.SetOpacity(0.85f);
            visual.SetVisualState(ArHologramVisualState.Selected);
            visual.SetManipulationEnabled(enableManipulation);
            if (enableManipulation)
                EnsureGrab(visual);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[AR Guidance][Calibration] Showing \"" + selectedComponentId + "\" -> instance=\"" +
                      visual.gameObject.name + "\" visibleCount=" + guidanceRenderer.CountVisible());
#endif

            SelectionResolved?.Invoke(selectedComponentId, null);
        }

        public void HidePreview()
        {
            ClearSelectionInteraction();
            guidanceRenderer?.HideAll();
        }

        public void PreviewSaved() => ApplySaved(showManipulation: false);

        public void RestoreSaved() => ApplySaved(showManipulation: true);

        private void ApplySaved(bool showManipulation)
        {
            if (_repository == null || guidanceRenderer == null)
                return;
            if (_repository.TryGetComponent(selectedComponentId, out ArComponentCalibration cal))
                guidanceRenderer.ApplyCalibration(cal);
            _undo.Clear();
            _undoBurstOpen = false;
            ShowSelected(showManipulation);
        }

        public void NudgePosition(Vector3 axisSign)
        {
            ArHologramVisualBase visual = GetSelectedVisual();
            if (visual == null)
                return;
            Vector3 axis = axisSign.normalized;
            if (lockHeight)
                axis.y = 0f;
            if (axis.sqrMagnitude < 0.0001f)
                return;
            visual.transform.localPosition += axis * PositionStepMeters;
            visual.NotifyPoseApplied();
        }

        public void NudgeRotation(Vector3 axisSign)
        {
            if (lockRotation)
                return;
            ArHologramVisualBase visual = GetSelectedVisual();
            if (visual == null)
                return;
            visual.transform.localRotation *=
                Quaternion.Euler(axisSign.normalized * RotationStepDegrees);
            visual.NotifyPoseApplied();
        }

        public void PushUndoIfNeeded()
        {
            if (_undoBurstOpen)
                return;
            ArHologramVisualBase visual = GetSelectedVisual();
            if (visual == null)
                return;
            _undo.Push(Capture(visual));
            _undoBurstOpen = true;
        }

        public void EndUndoBurst() => _undoBurstOpen = false;

        public void Undo()
        {
            if (!_undo.TryPop(out ArCalibrationUndoStack.Snapshot snap))
                return;
            if (snap.ComponentId != selectedComponentId)
                return;
            ArHologramVisualBase visual = GetSelectedVisual();
            if (visual == null)
                return;
            visual.transform.localPosition = snap.LocalPosition;
            visual.transform.localRotation = snap.LocalRotation;
            visual.transform.localScale = snap.LocalScale;
            visual.NotifyPoseApplied();
            _undoBurstOpen = false;
        }

        public void SaveSelected()
        {
            if (_repository == null || guidanceRenderer == null)
                return;
            ArHologramVisualBase visual = GetSelectedVisual();
            if (visual == null)
                return;

            var cal = new ArComponentCalibration
            {
                componentId = selectedComponentId,
                visualType = visual.VisualType,
                localPosition = visual.transform.localPosition,
                localRotation = visual.transform.localRotation,
                localScale = visual.transform.localScale
            };
            _repository.SaveComponent(cal);
            _undo.Clear();
            _undoBurstOpen = false;
            UIAudioManager.Play(UIAudioEvent.Success);
        }

        /// <summary>Legacy name — maps to ResetCurrent (marks dirty; does not auto-save).</summary>
        public void ResetSelected() => ResetCurrent();

        public void ResetCurrent()
        {
            if (guidanceRenderer == null)
                return;
            PushUndoIfNeeded();
            // Apply profile defaults in-memory only — do not persist until Save.
            ArComponentCalibration defaults =
                JsonArCalibrationRepository.CreateDefaultComponent(selectedComponentId);
            guidanceRenderer.ApplyCalibration(defaults);
            _undoBurstOpen = false;
            ShowSelected(enableManipulation: true);
        }

        /// <summary>True if the repository has any saved calibration for componentId — the
        /// "Never Calibrated" vs "Saved/Unsaved" distinction the Developer panel badge needs.</summary>
        public bool HasSavedCalibration(string componentId)
        {
            return _repository != null && _repository.TryGetComponent(componentId, out _);
        }

        public bool IsDirtyVersusSaved()
        {
            ArHologramVisualBase visual = GetSelectedVisual();
            if (visual == null || _repository == null)
                return false;
            // Never calibrated is not "unsaved edits" — otherwise the dropdown/Prev/Next
            // gate traps the user on the first component after a fresh install.
            if (!_repository.TryGetComponent(selectedComponentId, out ArComponentCalibration cal) ||
                cal == null)
                return false;
            if ((visual.transform.localPosition - cal.localPosition).sqrMagnitude >
                PosEpsilonMeters * PosEpsilonMeters)
                return true;
            float angle = Quaternion.Angle(visual.transform.localRotation, cal.localRotation);
            if (angle > RotEpsilonDegrees)
                return true;
            return false;
        }

        private ArCalibrationUndoStack.Snapshot Capture(ArHologramVisualBase visual)
        {
            return new ArCalibrationUndoStack.Snapshot(
                selectedComponentId,
                visual.transform.localPosition,
                visual.transform.localRotation,
                visual.transform.localScale);
        }

        private void EnsureGrab(ArHologramVisualBase visual)
        {
            ClearSelectionInteraction();
            int grabLayer = LayerMask.NameToLayer("Grab");
            if (grabLayer >= 0)
                visual.gameObject.layer = grabLayer;

            Collider col = visual.GetComponent<Collider>();
            if (col == null)
            {
                var box = visual.gameObject.AddComponent<BoxCollider>();
                box.size = new Vector3(0.08f, 0.02f, 0.08f);
                col = box;
            }

            col.enabled = true;
            col.isTrigger = false;

            Rigidbody rb = visual.GetComponent<Rigidbody>();
            if (rb == null)
                rb = visual.gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            _activeGrab = visual.GetComponent<XRGrabInteractable>();
            if (_activeGrab == null)
                _activeGrab = visual.gameObject.AddComponent<XRGrabInteractable>();

            // Locks apply to precision +/- only — grab stays unconstrained (documented).
            _activeGrab.selectEntered.AddListener(OnGrabEntered);
            _activeGrab.selectExited.AddListener(OnGrabExited);
        }

        private void OnGrabEntered(SelectEnterEventArgs args)
        {
            ArHologramVisualBase visual = GetSelectedVisual();
            if (visual == null)
                return;
            _grabStartPos = visual.transform.localPosition;
            _grabStartRot = visual.transform.localRotation;
            _grabStartScale = visual.transform.localScale;
            _grabTracking = true;
        }

        private void OnGrabExited(SelectExitEventArgs args)
        {
            if (!_grabTracking)
                return;
            _grabTracking = false;
            ArHologramVisualBase visual = GetSelectedVisual();
            if (visual == null)
                return;
            bool moved =
                (visual.transform.localPosition - _grabStartPos).sqrMagnitude >
                PosEpsilonMeters * PosEpsilonMeters ||
                Quaternion.Angle(visual.transform.localRotation, _grabStartRot) > RotEpsilonDegrees;
            if (!moved)
                return;
            visual.NotifyPoseApplied();
            _undo.Push(new ArCalibrationUndoStack.Snapshot(
                selectedComponentId, _grabStartPos, _grabStartRot, _grabStartScale));
            _undoBurstOpen = false;
        }

        private void ClearSelectionInteraction()
        {
            if (_activeGrab != null)
            {
                _activeGrab.selectEntered.RemoveListener(OnGrabEntered);
                _activeGrab.selectExited.RemoveListener(OnGrabExited);
                ArHologramVisualBase visual = _activeGrab.GetComponent<ArHologramVisualBase>();
                if (visual != null)
                    visual.RestoreDecorativeLayer();
            }

            _activeGrab = null;
            _grabTracking = false;
        }

        private void OnDisable()
        {
            ClearSelectionInteraction();
            _undoBurstOpen = false;
        }
    }
}
