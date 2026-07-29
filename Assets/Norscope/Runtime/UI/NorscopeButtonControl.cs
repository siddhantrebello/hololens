using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Norscope.UI
{
    /// <summary>
    /// Editable button control. Hierarchy is authored in the prefab; this only exposes
    /// Inspector fields and applies theme colors / ColorBlock for XRI UI modules.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NorscopeButtonControl : MonoBehaviour
    {
        public enum ButtonVariant
        {
            Primary,
            Secondary,
            Icon,
            Close
        }

        [SerializeField] private NorscopeTheme theme;
        [SerializeField] private ButtonVariant variant = ButtonVariant.Primary;
        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text label;
        [SerializeField] private RectTransform root;
        [SerializeField] private LayoutElement layoutElement;

        [Header("Content")]
        [SerializeField] private string text = "Button";
        [SerializeField] private Sprite icon;

        [Header("Size")]
        [SerializeField] private Vector2 size = new Vector2(160f, 40f);
        [SerializeField] private RectOffset padding;
        [SerializeField] private float spacing = 8f;

        [Header("State")]
        [SerializeField] private bool interactable = true;

        public string Text
        {
            get => text;
            set
            {
                text = value;
                ApplyContent();
            }
        }

        public Sprite Icon
        {
            get => icon;
            set
            {
                icon = value;
                ApplyContent();
            }
        }

        public Vector2 Size
        {
            get => size;
            set
            {
                size = value;
                ApplySize();
            }
        }

        private void OnValidate()
        {
            ApplyAll();
        }

        private void Awake()
        {
            ApplyAll();
        }

        public void ApplyAll()
        {
            if (padding == null)
            {
                padding = new RectOffset(12, 12, 8, 8);
            }

            if (root == null)
            {
                root = transform as RectTransform;
            }

            ApplyContent();
            ApplySize();
            ApplyTheme();
        }

        private void ApplyContent()
        {
            if (label != null)
            {
                label.text = text;
                label.gameObject.SetActive(variant != ButtonVariant.Icon && variant != ButtonVariant.Close || !string.IsNullOrEmpty(text));
                if (variant == ButtonVariant.Icon || variant == ButtonVariant.Close)
                {
                    label.gameObject.SetActive(!string.IsNullOrEmpty(text));
                }
            }

            if (iconImage != null)
            {
                iconImage.sprite = icon;
                iconImage.enabled = icon != null;
                iconImage.gameObject.SetActive(icon != null);
            }
        }

        private void ApplySize()
        {
            if (root != null)
            {
                root.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
                root.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);
            }

            if (layoutElement != null)
            {
                layoutElement.preferredWidth = size.x;
                layoutElement.preferredHeight = size.y;
                layoutElement.minWidth = size.x;
                layoutElement.minHeight = size.y;
            }

            var hl = GetComponent<HorizontalLayoutGroup>();
            if (hl != null)
            {
                hl.padding = padding;
                hl.spacing = spacing;
            }
        }

        private void ApplyTheme()
        {
            if (theme == null || button == null || background == null)
            {
                return;
            }

            Color fill;
            Color textColor = theme.primaryText;
            switch (variant)
            {
                case ButtonVariant.Primary:
                    fill = theme.primaryAccent;
                    textColor = new Color(0.043f, 0.059f, 0.090f, 1f);
                    break;
                case ButtonVariant.Secondary:
                    fill = theme.elevatedPanelBackground;
                    textColor = theme.primaryText;
                    break;
                case ButtonVariant.Close:
                case ButtonVariant.Icon:
                    fill = new Color(theme.elevatedPanelBackground.r, theme.elevatedPanelBackground.g, theme.elevatedPanelBackground.b, 0.85f);
                    textColor = theme.primaryText;
                    break;
                default:
                    fill = theme.primaryAccent;
                    break;
            }

            background.color = fill;
            if (label != null)
            {
                label.color = textColor;
                label.fontSize = theme.bodySize;
                if (theme.fonts != null && theme.fonts.bodyFont != null)
                {
                    label.font = theme.fonts.bodyFont;
                }
            }

            if (iconImage != null)
            {
                iconImage.color = textColor;
            }

            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(1f, 1f, 1f, theme.disabledOpacity);
            colors.fadeDuration = theme.transitionDuration;
            button.colors = colors;
            button.transition = Selectable.Transition.ColorTint;
            button.interactable = interactable;
        }
    }
}
