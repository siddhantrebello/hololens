using System;
using UnityEngine;

namespace Norscope.ArGuidance
{
    public enum ArGuidanceAnimationHint
    {
        None = 0,
        Fade = 1,
        Ghost = 2
    }

    [Serializable]
    public sealed class ArTaskGuidanceEntry
    {
        public string hologramId = string.Empty;
        public bool visible = true;
        public ArHologramVisualState visualState = ArHologramVisualState.Default;
        public string labelOverride = string.Empty;
        public ArGuidanceAnimationHint animationHint = ArGuidanceAnimationHint.Fade;
        public int displayOrder;
    }

    [Serializable]
    public sealed class ArTaskGuidanceStage
    {
        [Tooltip("Must match TaskStep.guidanceKey for the task step.")]
        public string guidanceKey = string.Empty;
        public int displayOrder;
        public ArTaskGuidanceEntry[] entries = Array.Empty<ArTaskGuidanceEntry>();
    }

    /// <summary>
    /// Data-driven map from task guidance keys to hologram visibility/state.
    /// Does not hardcode step indexes in the renderer.
    /// </summary>
    [CreateAssetMenu(
        fileName = "ArTaskGuidanceMap",
        menuName = "Norscope/AR Guidance/Task Guidance Map")]
    public sealed class ArTaskGuidanceMap : ScriptableObject
    {
        public string taskId = "replace-battery";
        public ArTaskGuidanceStage[] stages = Array.Empty<ArTaskGuidanceStage>();

        public ArTaskGuidanceStage FindStage(string guidanceKey)
        {
            if (stages == null || string.IsNullOrEmpty(guidanceKey))
                return null;
            for (int i = 0; i < stages.Length; i++)
            {
                if (stages[i] != null && stages[i].guidanceKey == guidanceKey)
                    return stages[i];
            }

            return null;
        }

        public int StageCount => stages != null ? stages.Length : 0;

        public ArTaskGuidanceStage GetStageByDisplayOrder(int orderIndex)
        {
            if (stages == null || stages.Length == 0)
                return null;
            ArTaskGuidanceStage[] sorted = (ArTaskGuidanceStage[])stages.Clone();
            Array.Sort(sorted, (a, b) =>
            {
                int ao = a != null ? a.displayOrder : 0;
                int bo = b != null ? b.displayOrder : 0;
                return ao.CompareTo(bo);
            });
            orderIndex = Mathf.Clamp(orderIndex, 0, sorted.Length - 1);
            return sorted[orderIndex];
        }
    }
}
