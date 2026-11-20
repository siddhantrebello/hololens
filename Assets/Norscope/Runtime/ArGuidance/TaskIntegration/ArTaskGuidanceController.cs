using System.Collections.Generic;
using Norscope.Audio;
using Norscope.TaskExecution;
using UnityEngine;

namespace Norscope.ArGuidance
{
    /// <summary>
    /// Bridges ActiveTaskSession (or Preview Guidance) to hologram visibility.
    /// Does not own calibration and does not parent holograms to UI/camera/hands.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ArTaskGuidanceController : MonoBehaviour
    {
        [SerializeField] private ActiveTaskSession taskSession;
        [SerializeField] private ArGuidanceSystem guidanceSystem;
        [SerializeField] private ArGuidanceRenderer guidanceRenderer;
        [SerializeField] private ArVisibilityController visibility;
        [SerializeField] private ArTaskGuidanceMap guidanceMap;

        private readonly List<ArHologramVisualBase> _activeVisuals = new List<ArHologramVisualBase>();
        private bool _previewMode;
        private int _previewStageIndex;
        private bool _bound;
        private bool _pausedForCalibration;
        private string _lastGuidanceKey = string.Empty;
        private IArTrackingOriginProvider _subscribedOrigin;

        public bool IsPreviewMode => _previewMode;
        public int PreviewStageIndex => _previewStageIndex;
        public ArTaskGuidanceMap Map => guidanceMap;
        public bool IsPausedForCalibration => _pausedForCalibration;

        /// <summary>
        /// While true, task/preview stage transitions do not show or hide holograms so
        /// Developer calibration can keep a single selected visual visible at the origin.
        /// </summary>
        public void SetCalibrationPause(bool paused)
        {
            if (_pausedForCalibration == paused)
                return;
            _pausedForCalibration = paused;
            if (paused)
            {
                if (_previewMode)
                {
                    _previewMode = false;
                    _lastGuidanceKey = string.Empty;
                }

                // Must be immediate — an animated Transition would finish AFTER calibration
                // ShowSelected and force every hologram back to opacity 0 / inactive.
                visibility?.CancelPending();
                if (guidanceRenderer != null)
                {
                    var all = new List<ArHologramVisualBase>();
                    guidanceRenderer.CollectAllVisuals(all);
                    visibility?.HideAllImmediate(all);
                    guidanceRenderer.HideAll();
                }

                _activeVisuals.Clear();
                return;
            }

            if (!_previewMode &&
                taskSession != null &&
                taskSession.SelectedTask != null &&
                !taskSession.IsComplete)
                OnStepChanged();
        }

        public void Configure(
            ActiveTaskSession session,
            ArGuidanceSystem system,
            ArTaskGuidanceMap map)
        {
            UnbindSession();
            taskSession = session;
            guidanceSystem = system;
            guidanceMap = map;
            if (guidanceSystem != null)
                guidanceRenderer = guidanceSystem.Renderer;
            if (visibility == null)
                visibility = GetComponent<ArVisibilityController>() ??
                             gameObject.AddComponent<ArVisibilityController>();
            BindSession();
            SubscribeOrigin();
        }

        private void OnEnable()
        {
            if (guidanceSystem == null)
                guidanceSystem = ArGuidanceSystem.Instance;
            if (guidanceRenderer == null && guidanceSystem != null)
                guidanceRenderer = guidanceSystem.Renderer;
            if (visibility == null)
                visibility = GetComponent<ArVisibilityController>() ??
                             gameObject.AddComponent<ArVisibilityController>();
            if (taskSession == null)
                taskSession = FindFirstObjectByType<ActiveTaskSession>();
            BindSession();
            SubscribeOrigin();
        }

        private void OnDisable()
        {
            UnsubscribeOrigin();
            UnbindSession();
            HideAllTaskGuidance(false);
        }

        private void SubscribeOrigin()
        {
            IArTrackingOriginProvider origin = guidanceSystem != null
                ? guidanceSystem.ActiveOrigin
                : null;
            if (origin == _subscribedOrigin)
                return;
            UnsubscribeOrigin();
            _subscribedOrigin = origin;
            if (_subscribedOrigin != null)
                _subscribedOrigin.TrackingStateChanged += OnOriginTrackingStateChanged;
        }

        private void UnsubscribeOrigin()
        {
            if (_subscribedOrigin == null)
                return;
            _subscribedOrigin.TrackingStateChanged -= OnOriginTrackingStateChanged;
            _subscribedOrigin = null;
        }

        private void OnOriginTrackingStateChanged(ArTrackingState state)
        {
            if (_pausedForCalibration)
                return;
            if (_previewMode || taskSession == null || taskSession.SelectedTask == null)
                return;

            // Searching after a forced rescan: hide until the QR locks again.
            // Lost with a prior pose: keep holograms; ProcessMarkers will realign on re-acquire.
            if (guidanceSystem != null &&
                guidanceSystem.ActiveOrigin != null &&
                guidanceSystem.ActiveOrigin.IsValid)
            {
                OnStepChanged();
                return;
            }

            HideAllTaskGuidance(false);
        }

        private void BindSession()
        {
            if (_bound || taskSession == null)
                return;
            taskSession.TaskBegan += OnTaskBegan;
            taskSession.StepChanged += OnStepChanged;
            taskSession.TaskFinished += OnTaskEnded;
            taskSession.TaskCancelled += OnTaskEnded;
            _bound = true;
        }

        private void UnbindSession()
        {
            if (!_bound || taskSession == null)
            {
                _bound = false;
                return;
            }

            taskSession.TaskBegan -= OnTaskBegan;
            taskSession.StepChanged -= OnStepChanged;
            taskSession.TaskFinished -= OnTaskEnded;
            taskSession.TaskCancelled -= OnTaskEnded;
            _bound = false;
        }

        private void OnTaskBegan()
        {
            ExitPreview();
            if (guidanceSystem != null && !guidanceSystem.IsSessionActive)
                guidanceSystem.BeginSession();
            // Scan QR at the start of every task; keep following if the marker moves.
            guidanceSystem?.RequestOriginRescan();
            SubscribeOrigin();
            // Clear Developer preview toggles (Phase 1 + library) before task stage.
            guidanceRenderer?.HideAll();
            _activeVisuals.Clear();
            _lastGuidanceKey = string.Empty;
            OnStepChanged();
        }

        private void OnTaskEnded()
        {
            // Cancel / finish: close every hologram. End session unless Developer is open.
            HideAllTaskGuidance(true);
            guidanceSystem?.HideAllHolograms();
            if (guidanceSystem != null && !IsDeveloperPanelOpen())
                guidanceSystem.EndSession();
        }

        private void OnStepChanged()
        {
            if (_pausedForCalibration)
                return;
            if (_previewMode)
                return;
            if (taskSession == null || guidanceMap == null || guidanceRenderer == null)
                return;
            if (taskSession.SelectedTask == null ||
                taskSession.SelectedTask.TaskId != guidanceMap.taskId)
            {
                HideAllTaskGuidance(true);
                return;
            }

            if (guidanceSystem != null && !guidanceSystem.IsSessionActive)
                guidanceSystem.BeginSession();

            if (guidanceSystem != null &&
                (guidanceSystem.ActiveOrigin == null || !guidanceSystem.ActiveOrigin.IsValid))
            {
                // Waiting for QR after rescan / panel close — keep holograms hidden.
                HideAllTaskGuidance(false);
                return;
            }

            string key = taskSession.CurrentGuidanceKey;
            ShowStageByKey(key, playAudio: true);
        }

        public void EnterPreview()
        {
            if (_pausedForCalibration)
                return;
            if (guidanceMap == null || guidanceMap.StageCount == 0)
                return;
            _previewMode = true;
            _previewStageIndex = 0;
            ShowStage(guidanceMap.GetStageByDisplayOrder(_previewStageIndex), playAudio: true);
        }

        public void ExitPreview()
        {
            if (!_previewMode)
                return;
            _previewMode = false;
            HideAllTaskGuidance(false);
            // If a task is still active, restore its stage.
            if (taskSession != null && taskSession.SelectedTask != null)
                OnStepChanged();
        }

        public void PreviewNext()
        {
            if (!_previewMode || guidanceMap == null || guidanceMap.StageCount == 0)
                return;
            _previewStageIndex = (_previewStageIndex + 1) % guidanceMap.StageCount;
            ShowStage(guidanceMap.GetStageByDisplayOrder(_previewStageIndex), playAudio: true);
        }

        public void PreviewPrevious()
        {
            if (!_previewMode || guidanceMap == null || guidanceMap.StageCount == 0)
                return;
            _previewStageIndex--;
            if (_previewStageIndex < 0)
                _previewStageIndex = guidanceMap.StageCount - 1;
            ShowStage(guidanceMap.GetStageByDisplayOrder(_previewStageIndex), playAudio: true);
        }

        public void RefreshFromSession() => OnStepChanged();

        private void ShowStageByKey(string guidanceKey, bool playAudio)
        {
            if (string.IsNullOrEmpty(guidanceKey) || guidanceMap == null)
            {
                HideAllTaskGuidance(playAudio);
                return;
            }

            if (guidanceKey == _lastGuidanceKey && _activeVisuals.Count > 0)
                return;

            ShowStage(guidanceMap.FindStage(guidanceKey), playAudio);
            _lastGuidanceKey = guidanceKey ?? string.Empty;
        }

        private void ShowStage(ArTaskGuidanceStage stage, bool playAudio)
        {
            if (_pausedForCalibration)
                return;
            if (guidanceRenderer == null || visibility == null)
                return;

            var hide = new List<ArHologramVisualBase>(_activeVisuals);
            var show = new List<ArVisibilityController.PendingShow>();
            _activeVisuals.Clear();

            if (stage?.entries != null)
            {
                // Sort by displayOrder
                ArTaskGuidanceEntry[] entries = (ArTaskGuidanceEntry[])stage.entries.Clone();
                System.Array.Sort(entries, (a, b) =>
                {
                    int ao = a != null ? a.displayOrder : 0;
                    int bo = b != null ? b.displayOrder : 0;
                    return ao.CompareTo(bo);
                });

                for (int i = 0; i < entries.Length; i++)
                {
                    ArTaskGuidanceEntry e = entries[i];
                    if (e == null || string.IsNullOrEmpty(e.hologramId))
                        continue;
                    ArHologramVisualBase visual = guidanceRenderer.GetVisual(e.hologramId);
                    if (visual == null)
                    {
                        Debug.LogWarning("[AR Task Guidance] Missing hologram: " + e.hologramId);
                        continue;
                    }

                    // Re-apply saved / library pose every show (survives session reuse).
                    IArCalibrationRepository repo = guidanceSystem != null
                        ? guidanceSystem.Repository
                        : null;
                    ArHologramLibrary library = guidanceSystem != null &&
                                               guidanceSystem.Profile != null
                        ? guidanceSystem.Profile.hologramLibrary
                        : null;
                    guidanceRenderer.ApplyPoseFor(e.hologramId, repo, library);

                    hide.Remove(visual);
                    float targetOpacity = ResolveTargetOpacity(e.visualState);
                    show.Add(new ArVisibilityController.PendingShow(
                        visual, e.visible, e.visualState, e.labelOverride, targetOpacity));
                    if (e.visible)
                        _activeVisuals.Add(visual);
                }
            }

            // Hide any library holograms not in the new stage (hard rule).
            HideUnmappedImmediate(hide);

            UIAudioEvent evt = UIAudioEvent.Notification;
            if (stage != null && StageHasDanger(stage))
                evt = UIAudioEvent.Warning;

            visibility.Transition(hide, show, playAudio, evt);
            _lastGuidanceKey = stage != null ? stage.guidanceKey : string.Empty;
        }

        private void HideUnmappedImmediate(List<ArHologramVisualBase> hide)
        {
            // Hide every spawned hologram not in the new stage (includes Phase 1 leftovers).
            if (guidanceRenderer == null || hide == null)
                return;
            guidanceRenderer.CollectAllVisuals(hide);
            for (int a = 0; a < _activeVisuals.Count; a++)
                hide.Remove(_activeVisuals[a]);
        }

        private static bool StageHasDanger(ArTaskGuidanceStage stage)
        {
            if (stage?.entries == null)
                return false;
            for (int i = 0; i < stage.entries.Length; i++)
            {
                if (stage.entries[i] != null &&
                    stage.entries[i].visualState == ArHologramVisualState.Danger)
                    return true;
            }

            return false;
        }

        private static float ResolveTargetOpacity(ArHologramVisualState state)
        {
            switch (state)
            {
                case ArHologramVisualState.Danger: return 0.95f;
                case ArHologramVisualState.Warning: return 0.90f;
                case ArHologramVisualState.Completed: return 0.90f;
                default: return 0.85f;
            }
        }

        public void HideAllTaskGuidance(bool playAudio)
        {
            _lastGuidanceKey = string.Empty;
            if (visibility == null)
            {
                _activeVisuals.Clear();
                return;
            }

            var hide = new List<ArHologramVisualBase>(_activeVisuals);
            HideUnmappedImmediate(hide);
            _activeVisuals.Clear();
            visibility.Transition(hide, new List<ArVisibilityController.PendingShow>(), playAudio,
                UIAudioEvent.PanelClose);
        }

        private static bool IsDeveloperPanelOpen()
        {
            ArGuidancePanelController panel = Object.FindFirstObjectByType<ArGuidancePanelController>();
            return panel != null && panel.isActiveAndEnabled;
        }
    }
}
