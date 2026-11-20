using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Norscope.UI
{
    [DisallowMultipleComponent]
    public sealed class NorscopeBadgeControl : MonoBehaviour
    {
        public enum BadgeKind
        {
            Ready,
            Warning,
            Error,
            Neutral
        }

        [SerializeField] private NorscopeTheme theme;
        [SerializeField] private BadgeKind kind = BadgeKind.Ready;
        [SerializeField] private Image background;
        [SerializeField] private TMP_Text label;
        [SerializeField] private RectTransform root;
        [SerializeField] private LayoutElement layoutElement;

        [SerializeField] private string text = "Ready";
        [SerializeField] private Vector2 size = new Vector2(88f, 24f);
        [SerializeField] private RectOffset padding;

        public string Text
        {
            get => text;
            set
            {
                text = value;
                Apply();
            }
        }

        public BadgeKind Kind
        {
            get => kind;
            set
            {
                kind = value;
                Apply();
            }
        }

        private void OnValidate() => Apply();
        private void Awake() => Apply();

        public void Apply()
        {
            if (padding == null)
            {
                padding = new RectOffset(10, 10, 4, 4);
            }

            if (root == null)
            {
                root = transform as RectTransform;
            }

            if (label != null)
            {
                label.text = text;
            }

            if (root != null)
            {
                root.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
                root.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);
            }

            if (layoutElement != null)
            {
                layoutElement.preferredWidth = size.x;
                layoutElement.preferredHeight = size.y;
            }

            var hl = GetComponent<HorizontalLayoutGroup>();
            if (hl != null)
            {
                hl.padding = padding;
            }

            if (theme == null || background == null || label == null)
            {
                return;
            }

            Color accent = kind switch
            {
                BadgeKind.Ready => theme.success,
                BadgeKind.Warning => theme.warning,
                BadgeKind.Error => theme.error,
                _ => theme.secondaryAccent
            };

            background.color = new Color(accent.r, accent.g, accent.b, 0.18f);
            label.color = accent;
            label.fontSize = theme.captionSize;
            if (theme.fonts != null && theme.fonts.captionFont != null)
            {
                label.font = theme.fonts.captionFont;
            }
        }
    }
}
