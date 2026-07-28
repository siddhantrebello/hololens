using UnityEngine;

namespace Norscope.UI
{
    /// <summary>
    /// Shared visual tokens for the Norscope Canvas/UGUI design system.
    /// Inspired by the MIT-licensed UI Toolkit design-token set (colors/spacing/radii),
    /// adapted for Fluent-style HL2 panels with a cyan accent.
    /// </summary>
    [CreateAssetMenu(fileName = "NorscopeTheme", menuName = "Norscope/UI/Theme", order = 10)]
    public sealed class NorscopeTheme : ScriptableObject
    {
        [Header("Colors")]
        public Color panelBackground = new Color(0.075f, 0.102f, 0.141f, 0.92f);       // #131A24 @ ~92%
        public Color elevatedPanelBackground = new Color(0.102f, 0.137f, 0.188f, 0.96f); // #1A2330
        public Color primaryAccent = new Color(0.133f, 0.827f, 0.933f, 1f);              // cyan
        public Color secondaryAccent = new Color(0.231f, 0.510f, 0.965f, 1f);             // blue
        public Color primaryText = new Color(0.949f, 0.957f, 0.969f, 1f);                 // #F2F4F7
        public Color secondaryText = new Color(0.631f, 0.655f, 0.702f, 1f);               // #A1A7B3
        public Color border = new Color(0.149f, 0.188f, 0.255f, 1f);                      // #263041
        public Color hover = new Color(0.122f, 0.161f, 0.216f, 1f);                       // #1F2937
        public Color pressed = new Color(0.090f, 0.120f, 0.165f, 1f);
        public Color disabled = new Color(0.404f, 0.439f, 0.522f, 1f);                    // #677085
        public Color success = new Color(0.133f, 0.773f, 0.369f, 1f);
        public Color warning = new Color(0.961f, 0.620f, 0.043f, 1f);
        public Color error = new Color(0.937f, 0.267f, 0.267f, 1f);

        [Header("Typography")]
        public TMPFontSlots fonts = new TMPFontSlots();
        public float titleSize = 26f;
        public float headingSize = 20f;
        public float bodySize = 14f;
        public float captionSize = 11f;

        [Header("Geometry")]
        public float smallCornerRadius = 6f;
        public float mediumCornerRadius = 8f;
        public float largeCornerRadius = 12f;
        public float thinBorderWidth = 1f;
        public float standardBorderWidth = 2f;

        [Header("Spacing")]
        public float spaceExtraSmall = 4f;
        public float spaceSmall = 8f;
        public float spaceMedium = 12f;
        public float spaceLarge = 16f;
        public float spaceExtraLarge = 24f;

        [Header("Interaction")]
        public float hoverScale = 1.02f;
        public float pressedScale = 0.96f;
        public float transitionDuration = 0.12f;
        public float disabledOpacity = 0.45f;

        [System.Serializable]
        public sealed class TMPFontSlots
        {
            public TMPro.TMP_FontAsset titleFont;
            public TMPro.TMP_FontAsset headingFont;
            public TMPro.TMP_FontAsset bodyFont;
            public TMPro.TMP_FontAsset captionFont;
        }
    }
}
