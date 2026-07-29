using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Norscope;
using Norscope.Audio;
using Norscope.UI;

namespace Norscope.Navigation
{
    /// <summary>
    /// Home dashboard card button. Hover lift + cyan outline + press scale.
    /// Click opens the card's registered panel via PanelRegistry; panels without a
    /// prefab yet still just log (see PanelRegistry.Open).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HomePanelCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private PanelType targetPanel = PanelType.None;
        [SerializeField] private string logLabel = "Opening";
        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [SerializeField] private Image border;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text subtitle;
        [SerializeField] private NorscopeTheme theme;
        [SerializeField] private float hoverLiftMeters = 0.004f;
        [SerializeField] private float hoverScale = 1.02f;

        private Vector3 _baseLocalPos;
        private Vector3 _baseScale;
        private Color _baseBorder;
        private bool _ready;

        public PanelType TargetPanel => targetPanel;

        public void Configure(
            PanelType panel,
            string titleText,
            string subtitleText,
            string openLogLabel,
            Sprite iconSprite,
            NorscopeTheme themeAsset,
            Image bg,
            Image borderImage,
            Image iconImage,
            TMP_Text titleTextComp,
            TMP_Text subtitleTextComp,
            Button buttonComp)
        {
            targetPanel = panel;
            logLabel = openLogLabel;
            theme = themeAsset;
            background = bg;
            border = borderImage;
            icon = iconImage;
            title = titleTextComp;
            subtitle = subtitleTextComp;
            button = buttonComp;

            if (title != null)
            {
                title.text = titleText;
            }

            if (subtitle != null)
            {
                subtitle.text = subtitleText;
            }

            if (icon != null)
            {
                icon.sprite = iconSprite;
                icon.enabled = iconSprite != null;
            }

            ApplyThemeColors();
            WireButton();
            CacheBase();
        }

        private void Awake()
        {
            if (button == null)
            {
                button = GetComponent<Button>();
            }

            WireButton();
            ApplyThemeColors();
            CacheBase();

            if (GetComponent<UiButtonPressScale>() == null)
            {
                gameObject.AddComponent<UiButtonPressScale>();
            }
        }

        private void CacheBase()
        {
            _baseLocalPos = transform.localPosition;
            _baseScale = transform.localScale;
            if (border != null)
            {
                _baseBorder = border.color;
            }

            _ready = true;
        }

        private void WireButton()
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveListener(OnClicked);
            button.onClick.AddListener(OnClicked);
            button.transition = Selectable.Transition.ColorTint;
            if (theme != null && background != null)
            {
                ColorBlock colors = button.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(1.05f, 1.05f, 1.05f, 1f);
                colors.pressedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
                colors.selectedColor = colors.highlightedColor;
                colors.disabledColor = new Color(0.7f, 0.7f, 0.7f, 0.5f);
                colors.colorMultiplier = 1f;
                colors.fadeDuration = theme.transitionDuration;
                button.colors = colors;
                button.targetGraphic = background;
                button.interactable = true;
            }
        }

        private void ApplyThemeColors()
        {
            if (theme == null)
            {
                return;
            }

            if (background != null)
            {
                background.color = theme.elevatedPanelBackground;
            }

            if (border != null)
            {
                border.color = theme.border;
                _baseBorder = border.color;
            }

            if (title != null)
            {
                title.color = theme.primaryText;
                title.fontSize = theme.headingSize;
            }

            if (subtitle != null)
            {
                subtitle.color = theme.secondaryText;
                subtitle.fontSize = theme.captionSize;
            }

            if (icon != null)
            {
                icon.color = theme.primaryAccent;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!_ready)
            {
                CacheBase();
            }

            transform.localPosition = _baseLocalPos + new Vector3(0f, hoverLiftMeters * 1000f, -2f);
            transform.localScale = _baseScale * hoverScale;
            if (border != null && theme != null)
            {
                border.color = theme.primaryAccent;
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            transform.localPosition = _baseLocalPos;
            transform.localScale = _baseScale;
            if (border != null)
            {
                border.color = _baseBorder;
            }
        }

        private void OnClicked()
        {
            UIAudioManager.Play(UIAudioEvent.ButtonClick);
            Debug.Log("[NAV] " + logLabel);

            // Open the child first; Home is hidden only after the open succeeds.
            PanelNavigationService navigation = PanelNavigationService.Instance;
            if (navigation != null)
            {
                navigation.Navigate(PanelType.Home, targetPanel);
            }
            else
            {
                Debug.LogWarning("[NAV] PanelNavigationService unavailable; Home remains visible.");
            }
        }
    }
}
