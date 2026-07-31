using TMPro;
using UnityEngine;

namespace Norscope.TaskExecution
{
    /// <summary>
    /// Data-binds one authored completed-step row. It never creates or lays out UI.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TaskCompletionStepRowView : MonoBehaviour
    {
        [SerializeField] private TMP_Text numberText;
        [SerializeField] private TMP_Text titleText;

        public void Bind(int index, string title)
        {
            if (numberText != null) numberText.text = "\u2713";
            if (titleText != null) titleText.text = title ?? string.Empty;
        }
    }
}
