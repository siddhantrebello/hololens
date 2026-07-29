using Norscope.Audio;
using Norscope.Navigation;
using Norscope.TaskLibrary;
using Norscope.Windows;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

        [Header("Task Navigation")]
        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button finishButton;

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
            WireButton(returnHomeButton, OnReturnHome);
            WireButton(generateReportButton, OnGenerateReport);
        }

        public void Bind(
            ActiveTaskSession session,
            PanelRegistry panelRegistry,
            SpatialWindowManager windowManager)
        {
            _session = session;
            _panelRegistry = panelRegistry;
            _windowManager = windowManager;
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
            stepCounterText.text = "Step " + (_session.CurrentStepIndex + 1) + " of " + _session.StepCount;
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
            previousButton.interactable = _session.CanGoPrevious;
            nextButton.gameObject.SetActive(!_session.IsOnLastStep);
            finishButton.gameObject.SetActive(_session.IsOnLastStep);
        }

        private void SetOptionalImage(Sprite image)
        {
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
                ShowCompletion();
            }
        }

        private void ShowCompletion()
        {
            taskView.SetActive(false);
            completionView.SetActive(true);
            completionTaskTitleText.text = _session.SelectedTask.DisplayName;
            completionMessageText.text = "All task steps have been completed.";
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
