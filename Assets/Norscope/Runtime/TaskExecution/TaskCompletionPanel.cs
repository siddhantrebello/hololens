using Norscope.Audio;
using Norscope.Navigation;
using Norscope.Windows;
using Norscope.TaskLibrary;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Norscope.Localization;

namespace Norscope.TaskExecution
{
    /// <summary>
    /// Binds completion data and interactions to an authored prefab. It never creates UI
    /// or modifies visual layout.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TaskCompletionPanel : MonoBehaviour
    {
        [SerializeField] private TMP_Text taskTitleText;
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private Button returnHomeButton;
        [SerializeField] private Button generateReportButton;

        [Header("Authored completion summary")]
        [SerializeField] private TMP_Text durationText;
        [SerializeField] private TMP_Text completedAtText;
        [SerializeField] private TMP_Text verificationStatusText;
        [SerializeField] private TMP_Text evidenceStatusText;
        [SerializeField] private TMP_Text completedStepCountText;
        [SerializeField] private TaskCompletionStepRowView[] workflowRows;

        private string _taskName;
        private TaskDefinition _completedTask;
        private PanelRegistry _panelRegistry;
        private SpatialWindowManager _windowManager;

        private void Awake()
        {
            WireButton(returnHomeButton, OnReturnHome);
            WireButton(generateReportButton, OnGenerateReport);
        }

        private void OnEnable()
        {
            LocalizationService.LanguageChanged -= RefreshLocalizedPresentation;
            LocalizationService.LanguageChanged += RefreshLocalizedPresentation;
        }

        private void OnDisable()
        {
            LocalizationService.LanguageChanged -= RefreshLocalizedPresentation;
        }

        private void RefreshLocalizedPresentation()
        {
            if (!string.IsNullOrEmpty(_taskName))
                BindInternal(_completedTask, _taskName, _panelRegistry, _windowManager);
        }

        public void Bind(
            string completedTaskName,
            PanelRegistry panelRegistry,
            SpatialWindowManager windowManager)
        {
            BindInternal(null, completedTaskName, panelRegistry, windowManager);
        }

        public void Bind(
            TaskDefinition completedTask,
            PanelRegistry panelRegistry,
            SpatialWindowManager windowManager)
        {
            BindInternal(
                completedTask,
                completedTask != null ? completedTask.DisplayName : null,
                panelRegistry,
                windowManager);
        }

        private void BindInternal(
            TaskDefinition completedTask,
            string completedTaskName,
            PanelRegistry panelRegistry,
            SpatialWindowManager windowManager)
        {
            _taskName = string.IsNullOrWhiteSpace(completedTaskName)
                ? LocalizationService.Get("task.generic_name")
                : completedTaskName;
            _completedTask = completedTask;
            _panelRegistry = panelRegistry;
            _windowManager = windowManager;

            if (taskTitleText != null) taskTitleText.text = _taskName;
            if (messageText != null)
            {
                messageText.text =
                    LocalizationService.Get("task.completion.success");
            }

            if (durationText != null)
                durationText.text = completedTask != null
                    ? completedTask.EstimatedDuration
                    : LocalizationService.Get("status.not_recorded");
            if (completedAtText != null)
                completedAtText.text = DateTime.Now.ToString(
                    "dd MMM yyyy") + " \u00B7 " + DateTime.Now.ToString("HH:mm");
            if (verificationStatusText != null)
                verificationStatusText.text =
                    LocalizationService.Get("status.not_recorded");
            if (evidenceStatusText != null)
                evidenceStatusText.text =
                    LocalizationService.Get("status.not_captured");

            int stepCount = completedTask != null && completedTask.Steps != null
                ? completedTask.Steps.Count
                : 0;
            if (completedStepCountText != null)
                completedStepCountText.text = stepCount > 0
                    ? LocalizationService.Get("task.completed_steps", stepCount)
                    : LocalizationService.Get("task.workflow_completed");

            if (workflowRows != null)
            {
                for (int i = 0; i < workflowRows.Length; i++)
                {
                    TaskCompletionStepRowView row = workflowRows[i];
                    if (row == null) continue;
                    bool visible = completedTask != null && i < stepCount;
                    row.gameObject.SetActive(visible);
                    if (visible)
                        row.Bind(i, completedTask.Steps[i].stepTitle);
                }
            }
        }

        private static void WireButton(
            Button button,
            UnityEngine.Events.UnityAction action)
        {
            if (button == null) return;
            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }

        private void OnReturnHome()
        {
            UIAudioManager.Play(UIAudioEvent.ButtonClick);
            if (_panelRegistry == null || _windowManager == null) return;

            SpatialWindow home = _panelRegistry.Open(PanelType.Home);
            if (home == null || !home.IsOpen)
            {
                Debug.LogWarning(
                    "[TaskCompletion] Home could not open; completion panel remains visible.");
                return;
            }

            _windowManager.Close(SpatialWindowId.TaskCompletion);
        }

        private void OnGenerateReport()
        {
            UIAudioManager.Play(UIAudioEvent.ButtonClick);
            UIAudioManager.Play(UIAudioEvent.ReportGenerated);
            Debug.Log("[TaskCompletion] Generate Report placeholder: " + _taskName);
        }
    }
}
