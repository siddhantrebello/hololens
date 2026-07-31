using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Norscope.TaskExecution
{
    /// <summary>
    /// Binds one authored workflow-row presentation. It never creates, moves, or
    /// resizes UI and owns no task-navigation behavior.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ActiveTaskStepRowView : MonoBehaviour
    {
        [SerializeField] private Image surface;
        [SerializeField] private Image stateIndicator;
        [SerializeField] private TMP_Text numberText;
        [SerializeField] private TMP_Text titleText;

        [Header("Authored state colours")]
        [SerializeField] private Color completedSurface = new Color(0.082f, 0.118f, 0.149f, 1f);
        [SerializeField] private Color currentSurface = new Color(0.098f, 0.141f, 0.22f, 1f);
        [SerializeField] private Color upcomingSurface = new Color(0f, 0f, 0f, 0f);
        [SerializeField] private Color completedAccent = new Color(0.224f, 0.851f, 0.541f, 1f);
        [SerializeField] private Color currentAccent = new Color(0.118f, 0.784f, 0.961f, 1f);
        [SerializeField] private Color upcomingAccent = new Color(0.29f, 0.365f, 0.431f, 1f);
        [SerializeField] private Color activeText = new Color(0.953f, 0.969f, 0.984f, 1f);
        [SerializeField] private Color upcomingText = new Color(0.569f, 0.635f, 0.71f, 1f);

        public void Bind(int stepIndex, string stepTitle, int currentStepIndex)
        {
            bool completed = stepIndex < currentStepIndex;
            bool current = stepIndex == currentStepIndex;

            if (numberText != null) numberText.text = (stepIndex + 1).ToString();
            if (titleText != null) titleText.text = stepTitle ?? string.Empty;
            if (surface != null)
            {
                surface.color = completed
                    ? completedSurface
                    : current ? currentSurface : upcomingSurface;
            }
            if (stateIndicator != null)
            {
                stateIndicator.color = completed
                    ? completedAccent
                    : current ? currentAccent : upcomingAccent;
            }
            if (numberText != null)
                numberText.color = current || completed ? activeText : upcomingText;
            if (titleText != null)
                titleText.color = current || completed ? activeText : upcomingText;
        }
    }
}
