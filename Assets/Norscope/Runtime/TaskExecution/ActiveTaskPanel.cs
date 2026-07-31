using Norscope.Audio;
using Norscope.Navigation;
using Norscope.TaskLibrary;
using Norscope.Windows;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Norscope.Localization;

namespace Norscope.TaskExecution
{
    /// <summary>
    /// Binds session state to authored prefab references. It never creates UI, changes
    /// RectTransforms, or modifies LayoutGroups.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ActiveTaskPanel : MonoBehaviour
    {
        [Header("Views")]
        [SerializeField] private GameObject taskView;
        [SerializeField] private GameObject completionView;

        [Header("Task View Content")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text stepCounterText;
        [SerializeField] private Image progressFill;
        [SerializeField] private TMP_Text stepTitleText;
        [SerializeField] private TMP_Text instructionText;
        [SerializeField] private GameObject imageSection;
        [SerializeField] private Image referenceImage;
        [SerializeField] private GameObject warningSection;
        [SerializeField] private TMP_Text warningText;
        [SerializeField] private GameObject verificationSection;
        [SerializeField] private TMP_Text verificationText;

        [Header("Authored Figma Presentation")]
        [SerializeField] private TMP_Text headerTaskTitleText;
        [SerializeField] private TMP_Text headerStepCounterText;
        [SerializeField] private TMP_Text offlineStatusText;
        [SerializeField] private TMP_Text taskSubtitleText;
        [SerializeField] private TMP_Text remainingTimeText;
        [SerializeField] private TMP_Text verificationStatusText;
        [SerializeField] private ActiveTaskStepRowView[] workflowRows;
        [SerializeField] private Image[] progressMarkers;
        [SerializeField] private Color completedProgressMarkerColor =
            new Color(0.118f, 0.784f, 0.961f, 1f);
        [SerializeField] private Color upcomingProgressMarkerColor =
            new Color(0.176f, 0.231f, 0.286f, 1f);

        [Header("Task Navigation")]
        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button finishButton;
        [SerializeField] private Button cancelButton;

        [Header("Cancel Confirmation")]
        [SerializeField] private GameObject cancelConfirmationView;
        [SerializeField] private Button continueTaskButton;
        [SerializeField] private Button confirmCancelButton;
        [SerializeField] private TMP_Text cancelTaskTitleText;
        [SerializeField] private TMP_Text cancelProgressText;
        [SerializeField] private TMP_Text cancelDurationText;
        [SerializeField] private TMP_Text cancelProgressDetailText;
        [SerializeField] private Image cancelProgressFill;

        [Header("Completion View")]
        [SerializeField] private TMP_Text completionTaskTitleText;
        [SerializeField] private TMP_Text completionMessageText;
        [SerializeField] private Button returnHomeButton;
        [SerializeField] private Button generateReportButton;

        private ActiveTaskSession _session;
        private PanelRegistry _panelRegistry;
        private SpatialWindowManager _windowManager;

        private void Awake()
        {
            WireButton(previousButton, OnPrevious);
            WireButton(nextButton, OnNext);
            WireButton(finishButton, OnFinish);
            WireButton(cancelButton, OnCancelRequested);
            WireButton(continueTaskButton, OnContinueTask);
            WireButton(confirmCancelButton, OnConfirmCancel);
            WireButton(returnHomeButton, OnReturnHome);
            WireButton(generateReportButton, OnGenerateReport);
        }

        private void OnEnable()
        {
            LocalizationService.LanguageChanged -= Refresh;
            LocalizationService.LanguageChanged += Refresh;
        }

        private void OnDisable()
        {
            LocalizationService.LanguageChanged -= Refresh;
        }

        public void Bind(
            ActiveTaskSession session,
            PanelRegistry panelRegistry,
            SpatialWindowManager windowManager)
        {
            _session = session;
            _panelRegistry = panelRegistry;
            _windowManager = windowManager;
            SetCancelConfirmationVisible(false);
            Refresh();
        }

        private static void WireButton(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null) return;
            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }

        private void Refresh()
        {
            if (_session == null || _session.SelectedTask == null) return;
            if (_session.IsComplete)
            {
                ShowCompletion();
                return;
            }

            TaskStep step = _session.CurrentStep;
            if (step == null) return;

            taskView.SetActive(true);
            completionView.SetActive(false);
            titleText.text = _session.SelectedTask.DisplayName;
            if (headerTaskTitleText != null)
                headerTaskTitleText.text = _session.SelectedTask.DisplayName;
            if (headerStepCounterText != null)
                headerStepCounterText.text = LocalizationService.Get(
                    "task.step_of", _session.CurrentStepIndex + 1, _session.StepCount);
            if (offlineStatusText != null)
                offlineStatusText.text = _session.SelectedTask.OfflineAvailable
                    ? "●  " + LocalizationService.Get("status.offline_ready")
                    : "●  " + LocalizationService.Get("status.offline");
            if (taskSubtitleText != null)
                taskSubtitleText.text = LocalizationService.Get("task.guided_workflow");
            if (remainingTimeText != null)
                remainingTimeText.text = LocalizationService.Get(
                    "task.time_remaining", _session.SelectedTask.EstimatedDuration);
            stepCounterText.text = LocalizationService.Get(
                "task.step_of", _session.CurrentStepIndex + 1, _session.StepCount);
            stepTitleText.text = step.stepTitle;
            instructionText.text = step.instruction;

            if (progressFill != null)
            {
                progressFill.fillAmount = _session.StepCount > 0
                    ? (float)(_session.CurrentStepIndex + 1) / _session.StepCount
                    : 0f;
            }

            SetOptionalImage(step.image);
            SetOptionalText(warningSection, warningText, step.warning);
            SetOptionalText(verificationSection, verificationText, step.verification);
            if (verificationStatusText != null)
                verificationStatusText.text = string.IsNullOrWhiteSpace(step.verification)
                    ? LocalizationService.Get("task.verification.not_required")
                    : LocalizationService.Get("task.verification.pending");
            RefreshWorkflowPresentation();
            previousButton.interactable = _session.CanGoPrevious;
            nextButton.gameObject.SetActive(!_session.IsOnLastStep);
            finishButton.gameObject.SetActive(_session.IsOnLastStep);
        }

        private void RefreshWorkflowPresentation()
        {
            if (workflowRows != null)
            {
                for (int i = 0; i < workflowRows.Length; i++)
                {
                    ActiveTaskStepRowView row = workflowRows[i];
                    if (row == null) continue;
                    bool visible = i < _session.StepCount;
                    row.gameObject.SetActive(visible);
                    if (visible)
                    {
                        row.Bind(i, _session.SelectedTask.Steps[i].stepTitle,
                            _session.CurrentStepIndex);
                    }
                }
            }

            if (progressMarkers != null)
            {
                for (int i = 0; i < progressMarkers.Length; i++)
                {
                    Image marker = progressMarkers[i];
                    if (marker == null) continue;
                    marker.gameObject.SetActive(i < _session.StepCount);
                    marker.color = i <= _session.CurrentStepIndex
                        ? completedProgressMarkerColor
                        : upcomingProgressMarkerColor;
                }
            }
        }

        private void SetOptionalImage(Sprite image)
        {
            if (imageSection == null)
            {
                if (image != null)
                {
                    Debug.LogWarning(
                        "[ActiveTask] This step has an image, but the authored " +
                        "ImageSection reference is missing. The task remains usable.");
                }
                return;
            }

            bool visible = image != null;
            imageSection.SetActive(visible);
            if (visible && referenceImage != null) referenceImage.sprite = image;
        }

        private static void SetOptionalText(GameObject section, TMP_Text target, string value)
        {
            bool visible = !string.IsNullOrWhiteSpace(value);
            section.SetActive(visible);
            if (visible && target != null) target.text = value;
        }

        private void OnPrevious()
        {
            UIAudioManager.Play(UIAudioEvent.ButtonClick);
            if (_session != null && _session.Previous()) Refresh();
        }

        private void OnNext()
        {
            UIAudioManager.Play(UIAudioEvent.ButtonClick);
            if (_session != null && _session.Next()) Refresh();
        }

        private void OnFinish()
        {
            UIAudioManager.Play(UIAudioEvent.ButtonClick);
            if (_session != null && _session.Finish())
            {
                UIAudioManager.Play(UIAudioEvent.Success);
                if (!OpenTaskCompletionPanel())
                {
                    Debug.LogWarning(
                        "[ActiveTask] Separate completion panel could not open; " +
                        "using the existing completion view.");
                    ShowCompletion();
                }
            }
        }

        private bool OpenTaskCompletionPanel()
        {
            PanelNavigationService navigation = PanelNavigationService.Instance;
            if (navigation == null || _session == null || _session.SelectedTask == null)
            {
                return false;
            }

            SpatialWindow completionWindow = navigation.Navigate(
                PanelType.TaskExecution,
                PanelType.TaskCompletion);
            if (completionWindow == null) return false;

            TaskCompletionPanel completionPanel =
                completionWindow.GetComponentInChildren<TaskCompletionPanel>(true);
            if (completionPanel == null)
            {
                Debug.LogError(
                    "[ActiveTask] TaskCompletionPanel component is missing from its prefab.");
                navigation.Back(PanelType.TaskCompletion, PanelType.TaskExecution);
                return false;
            }

            completionPanel.Bind(
                _session.SelectedTask,
                _panelRegistry,
                _windowManager);
            return true;
        }

        private void OnCancelRequested()
        {
            UIAudioManager.Play(UIAudioEvent.ButtonClick);
            UIAudioManager.Play(UIAudioEvent.Warning);
            RefreshCancelSummary();
            SetCancelConfirmationVisible(true);
        }

        private void OnContinueTask()
        {
            UIAudioManager.Play(UIAudioEvent.ButtonClick);
            SetCancelConfirmationVisible(false);
        }

        private void OnConfirmCancel()
        {
            UIAudioManager.Play(UIAudioEvent.ButtonClick);
            if (_session == null) return;

            PanelNavigationService navigation = PanelNavigationService.Instance;
            if (navigation == null)
            {
                Debug.LogWarning(
                    "[ActiveTask] Cancel ignored: PanelNavigationService is unavailable.");
                return;
            }

            if (!navigation.Back(PanelType.TaskExecution, PanelType.TaskLibrary))
            {
                Debug.LogWarning(
                    "[ActiveTask] Cancel did not complete because Task Library could not reopen.");
                return;
            }

            _session.Cancel();
            SetCancelConfirmationVisible(false);
            Debug.Log("[ActiveTask] Task cancelled.");
        }

        private void SetCancelConfirmationVisible(bool visible)
        {
            if (cancelConfirmationView != null)
            {
                cancelConfirmationView.SetActive(visible);
            }
        }

        private void RefreshCancelSummary()
        {
            if (_session == null || _session.SelectedTask == null) return;

            int stepCount = Mathf.Max(1, _session.StepCount);
            int displayedStep = Mathf.Clamp(
                _session.CurrentStepIndex + 1, 1, stepCount);

            if (cancelTaskTitleText != null)
                cancelTaskTitleText.text = _session.SelectedTask.DisplayName;
            if (cancelProgressText != null)
                cancelProgressText.text = LocalizationService.Get(
                    "task.step_of", displayedStep, stepCount);
            if (cancelDurationText != null)
                cancelDurationText.text =
                    string.IsNullOrWhiteSpace(
                        _session.SelectedTask.EstimatedDuration)
                        ? LocalizationService.Get("task.in_progress")
                        : _session.SelectedTask.EstimatedDuration;
            if (cancelProgressDetailText != null)
                cancelProgressDetailText.text = LocalizationService.Get(
                    "task.steps_progress", displayedStep, stepCount);
            if (cancelProgressFill != null)
                cancelProgressFill.fillAmount = Mathf.Clamp01(
                    (float)_session.CurrentStepIndex / stepCount);
        }

        private void ShowCompletion()
        {
            SetCancelConfirmationVisible(false);
            taskView.SetActive(false);
            completionView.SetActive(true);
            completionTaskTitleText.text = _session.SelectedTask.DisplayName;
            completionMessageText.text =
                LocalizationService.Get("task.all_steps_completed");
        }

        private void OnReturnHome()
        {
            UIAudioManager.Play(UIAudioEvent.ButtonClick);
            if (_windowManager != null) _windowManager.Close(SpatialWindowId.TaskExecution);
            if (_panelRegistry != null) _panelRegistry.Open(PanelType.Home);
        }

        private void OnGenerateReport()
        {
            UIAudioManager.Play(UIAudioEvent.ButtonClick);
            UIAudioManager.Play(UIAudioEvent.ReportGenerated);
            string taskName = _session != null && _session.SelectedTask != null
                ? _session.SelectedTask.DisplayName
                : "Unknown task";
            Debug.Log("[ActiveTask] Generate Report placeholder: " + taskName);
        }
    }
}
