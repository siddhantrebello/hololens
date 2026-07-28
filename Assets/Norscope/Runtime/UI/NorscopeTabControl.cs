using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Norscope.UI
{
    [DisallowMultipleComponent]
    public sealed class NorscopeTabControl : MonoBehaviour
    {
        [SerializeField] private NorscopeTheme theme;
        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [SerializeField] private TMP_Text label;
        [SerializeField] private RectTransform root;
        [SerializeField] private LayoutElement layoutElement;

        [SerializeField] private string text = "Tab";
        [SerializeField] private bool selected;
        [SerializeField] private Vector2 size = new Vector2(96f, 32f);
        [SerializeField] private RectOffset padding = new RectOffset(12, 12, 6, 6);

        public string Text
        {
            get => text;
            set
            {
                text = value;
                Apply();
            }
        }

        public bool Selected
        {
            get => selected;
            set
            {
                selected = value;
                Apply();
            }
        }

        private void OnValidate() => Apply();
        private void Awake() => Apply();

        public void Apply()
        {
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

            if (theme == null || background == null || label == null || button == null)
            {
                return;
            }

            background.color = selected
                ? new Color(theme.primaryAccent.r, theme.primaryAccent.g, theme.primaryAccent.b, 0.22f)
                : theme.elevatedPanelBackground;
            label.color = selected ? theme.primaryAccent : theme.secondaryText;
            label.fontSize = theme.bodySize;

            ColorBlock colors = button.colors;
            colors.fadeDuration = theme.transitionDuration;
            button.colors = colors;
        }
    }
}
