using UnityEngine;
using UnityEngine.UI;

namespace Norscope.UI
{
    /// <summary>
    /// Simple card / frame / bar styling without rebuilding hierarchy.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NorscopeThemedFrame : MonoBehaviour
    {
        public enum FrameRole
        {
            Card,
            PanelFrame,
            TitleBar,
            GrabBar,
            ResizeHandle
        }

        [SerializeField] private NorscopeTheme theme;
        [SerializeField] private FrameRole role = FrameRole.Card;
        [SerializeField] private Image background;
        [SerializeField] private Image border;
        [SerializeField] private RectTransform root;
        [SerializeField] private LayoutElement layoutElement;

        [SerializeField] private Vector2 size = new Vector2(200f, 120f);
        [SerializeField] private RectOffset padding;
        [SerializeField] private float spacing = 8f;

        public Vector2 Size
        {
            get => size;
            set
            {
                size = value;
                Apply();
            }
        }

        private void OnValidate() => Apply();
        private void Awake() => Apply();

        public void Apply()
        {
            if (padding == null)
            {
                padding = new RectOffset(12, 12, 12, 12);
            }

            if (root == null)
            {
                root = transform as RectTransform;
            }

            if (root != null && role != FrameRole.TitleBar && role != FrameRole.GrabBar)
            {
                root.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
                root.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);
            }

            if (layoutElement != null)
            {
                layoutElement.preferredWidth = size.x;
                layoutElement.preferredHeight = size.y;
            }

            var vl = GetComponent<VerticalLayoutGroup>();
            if (vl != null)
            {
                vl.padding = padding;
                vl.spacing = spacing;
            }

            var hl = GetComponent<HorizontalLayoutGroup>();
            if (hl != null)
            {
                hl.padding = padding;
                hl.spacing = spacing;
            }

            if (theme == null || background == null)
            {
                return;
            }

            switch (role)
            {
                case FrameRole.Card:
                    background.color = theme.elevatedPanelBackground;
                    break;
                case FrameRole.PanelFrame:
                    background.color = theme.panelBackground;
                    break;
                case FrameRole.TitleBar:
                    background.color = new Color(theme.elevatedPanelBackground.r, theme.elevatedPanelBackground.g, theme.elevatedPanelBackground.b, 0.98f);
                    break;
                case FrameRole.GrabBar:
                    background.color = new Color(theme.border.r, theme.border.g, theme.border.b, 0.9f);
                    break;
                case FrameRole.ResizeHandle:
                    background.color = new Color(theme.primaryAccent.r, theme.primaryAccent.g, theme.primaryAccent.b, 0.55f);
                    break;
            }

            if (border != null)
            {
                border.color = theme.border;
            }
        }
    }
}
