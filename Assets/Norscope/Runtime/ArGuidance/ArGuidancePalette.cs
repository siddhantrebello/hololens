using UnityEngine;

namespace Norscope.ArGuidance
{
    /// <summary>NORSCOPE AR Guidance Phase 2 color language (Inspector-overridable per prefab).</summary>
    public static class ArGuidancePalette
    {
        public static readonly Color Cyan = new Color(0f, 216f / 255f, 1f, 0.85f);          // #00D8FF
        public static readonly Color Selected = new Color(57f / 255f, 242f / 255f, 1f, 1f);  // #39F2FF
        public static readonly Color Warning = new Color(1f, 194f / 255f, 71f / 255f, 0.9f); // #FFC247
        public static readonly Color Danger = new Color(1f, 77f / 255f, 77f / 255f, 0.95f);  // #FF4D4D
        public static readonly Color Completed = new Color(50f / 255f, 232f / 255f, 117f / 255f, 0.9f); // #32E875
    }
}
