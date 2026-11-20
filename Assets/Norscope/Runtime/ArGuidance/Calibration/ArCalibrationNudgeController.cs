using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Norscope.Audio;

namespace Norscope.ArGuidance
{
    /// <summary>
    /// Calibration nudge UI: readouts, step highlights, locks, hold bindings, minimize.
    /// Does not own persistence — delegates to ArCalibrationController / SelectionController.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ArCalibrationNudgeController : MonoBehaviour
    {
        [Header("Deps")]
        [SerializeField] private ArCalibrationController calibration;
        [SerializeField] private ArCalibrationSelectionController selection;

        [Header("Readouts")]
        [SerializeField] private TMP_Text posXText;
        [SerializeField] private TMP_Text posYText;
        [SerializeField] private TMP_Text posZText;
        [SerializeField] private TMP_Text rotXText;
        [SerializeField] private TMP_Text rotYText;
        [SerializeField] private TMP_Text rotZText;
        [SerializeField] private TMP_Text dirtyLabel;

        [Header("Step buttons")]
        [SerializeField] private Button posStep10;
        [SerializeField] private Button posStep2;
        [SerializeField] private Button posStep05;
        [SerializeField] private Button rotStep5;
        [SerializeField] private Button rotStep1;
        [SerializeField] private Color stepNormal = new Color(0.12f, 0.18f, 0.24f, 1f);
        [SerializeField] private Color stepSelected = new Color(0f, 0.85f, 1f, 0.95f);

        [Header("Axis hold buttons")]
        [SerializeField] private ArPressAndHoldButton posXNeg;
        [SerializeField] private ArPressAndHoldButton posXPos;
        [SerializeField] private ArPressAndHoldButton posYNeg;
        [SerializeField] private ArPressAndHoldButton posYPos;
        [SerializeField] private ArPressAndHoldButton posZNeg;
        [SerializeField] private ArPressAndHoldButton posZPos;
        [SerializeField] private ArPressAndHoldButton rotXNeg;
        [SerializeField] private ArPressAndHoldButton rotXPos;
        [SerializeField] private ArPressAndHoldButton rotYNeg;
        [SerializeField] private ArPressAndHoldButton rotYPos;
        [SerializeField] private ArPressAndHoldButton rotZNeg;
        [SerializeField] private ArPressAndHoldButton rotZPos;

        [Header("Locks")]
        [SerializeField] private Toggle lockHeightToggle;
        [SerializeField] private Toggle lockRotationToggle;
        [SerializeField] private TMP_Text lockNoteText;

        [Header("Actions")]
        [SerializeField] private Button undoButton;
        [SerializeField] private Button restoreSavedButton;
        [SerializeField] private Button resetCurrentButton;
        [SerializeField] private Button saveButton;
        [SerializeField] private Button prevButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button minimizeButton;
        [SerializeField] private Button restorePanelButton;
        [SerializeField] private GameObject dirtyBanner;
        [SerializeField] private Button dirtySaveButton;
        [SerializeField] private Button dirtyDiscardButton;
        [SerializeField] private Button dirtyCancelButton;
        [SerializeField] private GameObject calibrationContentRoot;
        [SerializeField] private GameObject minimizedChrome;

        private bool _bound;
        private bool _minimized;

        public void Bind(ArCalibrationController cal, ArCalibrationSelectionController sel)
        {
            Unbind();
            calibration = cal;
            selection = sel;
            Wire();
            _bound = true;
            RefreshAll();
        }

        public void Unbind()
        {
            UnbindHolds();
            if (selection != null)
            {
                selection.DirtyChanged -= OnDirtyChanged;
                selection.SelectionChanged -= RefreshAll;
                selection.PendingNavChanged -= OnPendingNavChanged;
            }

            _bound = false;
        }

        private void OnDisable()
        {
            StopAllHolds();
        }

        private void OnDestroy() => Unbind();

        private void LateUpdate()
        {
            if (!_bound || calibration == null || _minimized)
                return;
            RefreshReadouts();
            // Keep dirty flag aligned after grab / undo (no silent save).
            if (selection != null &&
                selection.PendingNavigation == ArCalibrationSelectionController.PendingNav.None)
                selection.SetDirty(calibration.IsDirtyVersusSaved());
        }

        private void Wire()
        {
            WireStep(posStep10, 0, true);
            WireStep(posStep2, 1, true);
            WireStep(posStep05, 2, true);
            WireStep(rotStep5, 0, false);
            WireStep(rotStep1, 1, false);

            BindHold(posXNeg, () => NudgePos(Vector3.left));
            BindHold(posXPos, () => NudgePos(Vector3.right));
            BindHold(posYNeg, () => NudgePos(Vector3.down));
            BindHold(posYPos, () => NudgePos(Vector3.up));
            BindHold(posZNeg, () => NudgePos(Vector3.back));
            BindHold(posZPos, () => NudgePos(Vector3.forward));
            BindHold(rotXNeg, () => NudgeRot(Vector3.left));
            BindHold(rotXPos, () => NudgeRot(Vector3.right));
            BindHold(rotYNeg, () => NudgeRot(Vector3.down));
            BindHold(rotYPos, () => NudgeRot(Vector3.up));
            BindHold(rotZNeg, () => NudgeRot(Vector3.back));
            BindHold(rotZPos, () => NudgeRot(Vector3.forward));

            if (lockHeightToggle != null)
            {
                lockHeightToggle.onValueChanged.RemoveAllListeners();
                lockHeightToggle.onValueChanged.AddListener(on =>
                {
                    calibration?.SetLockHeight(on);
                    RefreshLockInteractable();
                });
            }

            if (lockRotationToggle != null)
            {
                lockRotationToggle.onValueChanged.RemoveAllListeners();
                lockRotationToggle.onValueChanged.AddListener(on =>
                {
                    calibration?.SetLockRotation(on);
                    RefreshLockInteractable();
                });
            }

            WireClick(undoButton, () =>
            {
                calibration?.Undo();
                selection?.SetDirty(calibration != null && calibration.IsDirtyVersusSaved());
                RefreshAll();
            });
            WireClick(restoreSavedButton, () =>
            {
                calibration?.RestoreSaved();
                selection?.ClearDirty();
                RefreshAll();
            });
            WireClick(resetCurrentButton, () =>
            {
                calibration?.ResetCurrent();
                selection?.SetDirty(true);
                RefreshAll();
            });
            WireClick(saveButton, () =>
            {
                calibration?.SaveSelected();
                selection?.ClearDirty();
                RefreshAll();
            });
            WireClick(prevButton, () =>
            {
                selection?.RequestPrevious();
                RefreshAll();
            });
            WireClick(nextButton, () =>
            {
                selection?.RequestNext();
                RefreshAll();
            });
            WireClick(minimizeButton, Minimize);
            WireClick(restorePanelButton, RestoreMinimized);
            WireClick(dirtySaveButton, () => selection?.ConfirmSaveAndContinue());
            WireClick(dirtyDiscardButton, () => selection?.ConfirmDiscardAndContinue());
            WireClick(dirtyCancelButton, () => selection?.CancelPendingNavigation());

            if (selection != null)
            {
                selection.DirtyChanged += OnDirtyChanged;
                selection.SelectionChanged += RefreshAll;
                selection.PendingNavChanged += OnPendingNavChanged;
            }

            if (lockNoteText != null)
                lockNoteText.text = "Locks apply to precision +/− only. Direct grab stays unconstrained.";
        }

        private void WireStep(Button button, int index, bool position)
        {
            if (button == null)
                return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                UIAudioManager.Play(UIAudioEvent.ButtonClick);
                if (position)
                    calibration?.SetPositionStepIndex(index);
                else
                    calibration?.SetRotationStepIndex(index);
                RefreshStepVisuals();
            });
        }

        private static void WireClick(Button button, System.Action action)
        {
            if (button == null || action == null)
                return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                UIAudioManager.Play(UIAudioEvent.ButtonClick);
                action();
            });
        }

        private void BindHold(ArPressAndHoldButton hold, System.Action action)
        {
            if (hold == null)
                return;
            hold.Bind(action, () => calibration?.EndUndoBurst());
        }

        private void UnbindHolds()
        {
            ArPressAndHoldButton[] holds =
            {
                posXNeg, posXPos, posYNeg, posYPos, posZNeg, posZPos,
                rotXNeg, rotXPos, rotYNeg, rotYPos, rotZNeg, rotZPos
            };
            for (int i = 0; i < holds.Length; i++)
                holds[i]?.Unbind();
        }

        private void StopAllHolds() => UnbindHolds();

        private void NudgePos(Vector3 axis)
        {
            if (calibration == null)
                return;
            calibration.PushUndoIfNeeded();
            calibration.NudgePosition(axis);
            selection?.SetDirty(true);
            RefreshReadouts();
        }

        private void NudgeRot(Vector3 axis)
        {
            if (calibration == null)
                return;
            calibration.PushUndoIfNeeded();
            calibration.NudgeRotation(axis);
            selection?.SetDirty(true);
            RefreshReadouts();
        }

        public void RefreshAll()
        {
            RefreshReadouts();
            RefreshStepVisuals();
            RefreshLockInteractable();
            OnDirtyChanged();
            OnPendingNavChanged();
            if (prevButton != null)
                prevButton.interactable = selection != null && selection.CanGoPrevious &&
                                          selection.PendingNavigation ==
                                          ArCalibrationSelectionController.PendingNav.None;
            if (nextButton != null)
                nextButton.interactable = selection != null && selection.CanGoNext &&
                                          selection.PendingNavigation ==
                                          ArCalibrationSelectionController.PendingNav.None;
        }

        private void RefreshReadouts()
        {
            if (calibration == null)
                return;
            Transform t = calibration.GetSelectedTransform();
            if (t == null)
                return;
            Vector3 p = t.localPosition;
            Vector3 e = t.localRotation.eulerAngles;
            e.x = NormalizeAngle(e.x);
            e.y = NormalizeAngle(e.y);
            e.z = NormalizeAngle(e.z);
            SetMm(posXText, p.x);
            SetMm(posYText, p.y);
            SetMm(posZText, p.z);
            SetDeg(rotXText, e.x);
            SetDeg(rotYText, e.y);
            SetDeg(rotZText, e.z);
        }

        private static float NormalizeAngle(float degrees)
        {
            float a = degrees % 360f;
            if (a > 180f)
                a -= 360f;
            if (a < -180f)
                a += 360f;
            return a;
        }

        private static void SetMm(TMP_Text text, float meters)
        {
            if (text != null)
                text.text = (meters * 1000f).ToString("0.0") + " mm";
        }

        private static void SetDeg(TMP_Text text, float degrees)
        {
            if (text != null)
                text.text = degrees.ToString("0.0") + "°";
        }

        private void RefreshStepVisuals()
        {
            if (calibration == null)
                return;
            PaintStep(posStep10, calibration.PositionStepIndex == 0);
            PaintStep(posStep2, calibration.PositionStepIndex == 1);
            PaintStep(posStep05, calibration.PositionStepIndex == 2);
            PaintStep(rotStep5, calibration.RotationStepIndex == 0);
            PaintStep(rotStep1, calibration.RotationStepIndex == 1);
        }

        private void PaintStep(Button button, bool selected)
        {
            if (button == null)
                return;
            Image img = button.targetGraphic as Image;
            if (img == null)
                img = button.GetComponent<Image>();
            if (img != null)
                img.color = selected ? stepSelected : stepNormal;
        }

        private void RefreshLockInteractable()
        {
            bool lockY = calibration != null && calibration.LockHeight;
            bool lockRot = calibration != null && calibration.LockRotation;
            SetInteractable(posYNeg, !lockY);
            SetInteractable(posYPos, !lockY);
            SetInteractable(rotXNeg, !lockRot);
            SetInteractable(rotXPos, !lockRot);
            SetInteractable(rotYNeg, !lockRot);
            SetInteractable(rotYPos, !lockRot);
            SetInteractable(rotZNeg, !lockRot);
            SetInteractable(rotZPos, !lockRot);
        }

        private static void SetInteractable(ArPressAndHoldButton hold, bool on)
        {
            if (hold == null)
                return;
            Button b = hold.GetComponent<Button>();
            if (b != null)
                b.interactable = on;
        }

        private void OnDirtyChanged()
        {
            bool dirty = selection != null && selection.IsDirty;
            if (dirtyLabel != null)
            {
                dirtyLabel.gameObject.SetActive(dirty);
                dirtyLabel.text = dirty ? "Unsaved changes" : string.Empty;
            }
        }

        private void OnPendingNavChanged()
        {
            bool pending = selection != null &&
                           selection.PendingNavigation !=
                           ArCalibrationSelectionController.PendingNav.None;
            if (dirtyBanner != null)
                dirtyBanner.SetActive(pending);
            if (prevButton != null)
                prevButton.interactable = !pending && selection != null && selection.CanGoPrevious;
            if (nextButton != null)
                nextButton.interactable = !pending && selection != null && selection.CanGoNext;
        }

        private void Minimize()
        {
            _minimized = true;
            StopAllHolds();
            if (calibrationContentRoot != null)
                calibrationContentRoot.SetActive(false);
            if (minimizedChrome != null)
                minimizedChrome.SetActive(true);
        }

        private void RestoreMinimized()
        {
            _minimized = false;
            if (minimizedChrome != null)
                minimizedChrome.SetActive(false);
            if (calibrationContentRoot != null)
                calibrationContentRoot.SetActive(true);
            RefreshAll();
        }

        public void ForceRestoreIfMinimized()
        {
            if (_minimized)
                RestoreMinimized();
        }
    }
}
