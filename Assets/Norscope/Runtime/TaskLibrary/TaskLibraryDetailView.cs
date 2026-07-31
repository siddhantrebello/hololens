using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Norscope.Localization;

namespace Norscope.TaskLibrary
{
    /// <summary>
    /// Data-only binder for the authored Task Library detail pane.
    /// All visual structure and layout remain in TaskLibraryPanel.prefab.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TaskLibraryDetailView : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text categoryText;
        [SerializeField] private TMP_Text durationText;
        [SerializeField] private TMP_Text difficultyText;
        [SerializeField] private TMP_Text stepCountText;
        [SerializeField] private GameObject offlineGroup;
        [SerializeField] private TMP_Text updatedText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private TMP_Text[] stepPreviewTexts;
        [SerializeField] private GameObject[] stepPreviewRows;
        [SerializeField] private GameObject extraStepsGroup;
        [SerializeField] private TMP_Text extraStepsText;
        [SerializeField] private Button startButton;
        [SerializeField] private Button detailsButton;

        public Button StartButton => startButton;
        public Button DetailsButton => detailsButton;

        public void Bind(TaskData data)
        {
            if (data == null) return;

            if (titleText != null) titleText.text = data.taskName;
            if (categoryText != null) categoryText.text = data.category;
            if (durationText != null) durationText.text = data.estimatedDuration;
            if (difficultyText != null)
                difficultyText.text = LocalizationService.Get(
                    "difficulty." + data.difficulty.ToString().ToLowerInvariant());

            int stepCount = data.steps != null ? data.steps.Count : 0;
            if (stepCountText != null)
            {
                stepCountText.text = LocalizationService.Get(
                    stepCount == 1 ? "task.guided_steps.one" :
                    "task.guided_steps.many", stepCount);
            }

            if (offlineGroup != null)
                offlineGroup.SetActive(data.offlineAvailable);
            if (updatedText != null)
            {
                updatedText.gameObject.SetActive(
                    !string.IsNullOrWhiteSpace(data.lastUpdated));
                updatedText.text = string.IsNullOrWhiteSpace(data.lastUpdated)
                    ? string.Empty
                    : LocalizationService.Get("task.updated", data.lastUpdated);
            }
            if (descriptionText != null)
                descriptionText.text = data.description;

            int previewCount = Mathf.Min(
                stepCount,
                Mathf.Min(stepPreviewTexts != null
                        ? stepPreviewTexts.Length
                        : 0,
                    stepPreviewRows != null ? stepPreviewRows.Length : 0));

            if (stepPreviewRows != null)
            {
                for (int i = 0; i < stepPreviewRows.Length; i++)
                {
                    if (stepPreviewRows[i] != null)
                        stepPreviewRows[i].SetActive(i < previewCount);
                }
            }
            for (int i = 0; i < previewCount; i++)
            {
                if (stepPreviewTexts[i] != null)
                    stepPreviewTexts[i].text = data.steps[i].stepTitle;
            }

            int extra = Mathf.Max(0, stepCount - previewCount);
            if (extraStepsGroup != null)
                extraStepsGroup.SetActive(extra > 0);
            if (extraStepsText != null)
            {
                extraStepsText.text = LocalizationService.Get(
                    extra == 1 ? "task.more_steps.one" :
                    "task.more_steps.many", extra);
            }
        }
    }
}
