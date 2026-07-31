using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Norscope;
using Norscope.Audio;
using Norscope.DesignSystem;
using Norscope.UI;

namespace Norscope.Navigation
{
    /// <summary>
    /// Home dashboard card button. Hover lift + cyan outline + press scale.
    /// Click opens the card's registered panel via PanelRegistry; panels without a
    /// prefab yet still just log (see PanelRegistry.Open).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HomePanelCard :
        MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerMoveHandler,
        IPointerDownHandler,
        IPointerUpHandler,
        ISelectHandler,
        IDeselectHandler
    {
        public enum PresentationMode
        {
            SharedTheme = 0,
            Authored = 1
        }

        [Header("Presentation ownership")]
        [Tooltip("SharedTheme preserves the legacy behaviour. Authored preserves the " +
                 "prefab's visual values and uses only the serialized interaction states below.")]
        [SerializeField] private PresentationMode presentationMode =
            PresentationMode.SharedTheme;

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

        [Header("Authored interaction states")]
        [SerializeField] private Color authoredHoverSurface =
            new Color(0.1098f, 0.1529f, 0.2118f, 1f);
        [SerializeField] private Color authoredHoverBorder =
            new Color(0.1176f, 0.7843f, 0.9608f, 0.35f);
        [SerializeField] private Color authoredFocusedBorder =
            new Color(0.1176f, 0.7843f, 0.9608f, 1f);
        [SerializeField] private Color authoredPressedSurface =
            new Color(0.0824f, 0.1176f, 0.1569f, 1f);
        [SerializeField] private Color authoredPressedBorder =
            new Color(0.1176f, 0.7843f, 0.9608f, 0.22f);
        [SerializeField, Range(0f, 1f)] private float authoredDisabledOpacity = 0.36f;
        [SerializeField] private float authoredHoverScale = 1.013f;
        [SerializeField] private float authoredPressedScale = 0.983f;
        [SerializeField] private float authoredHoverOffsetUiUnits = 1f;
        [SerializeField] private float authoredTransitionDuration = 0.2f;
        [SerializeField] private Graphic authoredFocusOutline;
        [SerializeField] private Graphic authoredFocusAccent;

        private Vector3 _baseLocalPos;
        private Vector3 _baseScale;
        private Color _baseSurface;
        private Color _baseBorder;
        private Color _baseIcon;
        private Color _baseTitle;
        private Color _baseSubtitle;
        private Coroutine _authoredTransition;
        private bool _hovered;
        private bool _pressed;
        private bool _focused;
        private bool _ready;
        private float _lastHoverSignalTime = -999f;

        public PanelType TargetPanel => targetPanel;
        public PresentationMode VisualPresentationMode => presentationMode;

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

            if (presentationMode == PresentationMode.SharedTheme)
            {
                ApplyThemeColors();
            }
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
            if (presentationMode == PresentationMode.SharedTheme)
            {
                ApplyThemeColors();
            }
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
            if (background != null) _baseSurface = background.color;
            if (icon != null) _baseIcon = icon.color;
            if (title != null) _baseTitle = title.color;
            if (subtitle != null) _baseSubtitle = subtitle.color;

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
            if (presentationMode == PresentationMode.SharedTheme)
            {
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

        private void OnDisable()
        {
            _hovered = false;
            _pressed = false;
            _focused = false;
            if (_authoredTransition != null)
            {
                StopCoroutine(_authoredTransition);
                _authoredTransition = null;
            }

            // Home closes when navigating to Ask AI / other panels. The card
            // GameObject is already inactive here — StartCoroutine would throw.
            if (presentationMode == PresentationMode.Authored)
                SnapAuthoredState(AuthoredState.Default);
            else
                ApplyLegacyIdle();
        }

        private void Update()
        {
            if (presentationMode != PresentationMode.Authored) return;
            if (!_hovered && !_pressed && !_focused) return;

            bool changed = false;
            if (_focused && !NorscopeUiPointerState.IsStillSelected(transform, button))
            {
                _focused = false;
                changed = true;
            }

            if ((_hovered || _pressed) &&
                !NorscopeUiPointerState.IsHoverFresh(_lastHoverSignalTime))
            {
                _hovered = false;
                _pressed = false;
                changed = true;
            }

            if (changed && !_pressed)
            {
                TransitionAuthored(_focused
                    ? AuthoredState.Focused
                    : (_hovered ? AuthoredState.Hovered : AuthoredState.Default));
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!_ready)
            {
                CacheBase();
            }

            if (presentationMode == PresentationMode.Authored)
            {
                if (!IsInteractable()) return;
                NoteHover();
                TransitionAuthored(AuthoredState.Hovered);
            }
            else
            {
                transform.localPosition = _baseLocalPos +
                    new Vector3(0f, hoverLiftMeters * 1000f, -2f);
                transform.localScale = _baseScale * hoverScale;
                if (border != null && theme != null)
                {
                    border.color = theme.primaryAccent;
                }
            }
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            if (presentationMode != PresentationMode.Authored || !IsInteractable())
                return;
            if (_hovered)
                NoteHover();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (presentationMode == PresentationMode.Authored)
            {
                _hovered = false;
                _pressed = false;
                _focused = false;
                NorscopeUiPointerState.ClearSelectionIfOurs(transform, button);
                TransitionAuthored(AuthoredState.Default);
            }
            else
            {
                ApplyLegacyIdle();
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (presentationMode != PresentationMode.Authored ||
                !IsInteractable()) return;
            NoteHover();
            _pressed = true;
            TransitionAuthored(AuthoredState.Pressed);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (presentationMode != PresentationMode.Authored) return;
            _pressed = false;
            TransitionAuthored(_focused
                ? AuthoredState.Focused
                : (_hovered ? AuthoredState.Hovered : AuthoredState.Default));
        }

        public void OnSelect(BaseEventData eventData)
        {
            if (presentationMode != PresentationMode.Authored ||
                !IsInteractable()) return;
            _focused = true;
            if (!_pressed) TransitionAuthored(AuthoredState.Focused);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            if (presentationMode != PresentationMode.Authored) return;
            _focused = false;
            if (!_pressed)
                TransitionAuthored(_hovered
                    ? AuthoredState.Hovered
                    : AuthoredState.Default);
        }

        private void NoteHover()
        {
            _hovered = true;
            _lastHoverSignalTime = Time.unscaledTime;
        }

        private void ApplyLegacyIdle()
        {
            transform.localPosition = _baseLocalPos;
            transform.localScale = _baseScale;
            if (border != null)
                border.color = _baseBorder;
        }

        private enum AuthoredState
        {
            Default,
            Hovered,
            Focused,
            Pressed,
            Disabled
        }

        private bool IsInteractable() =>
            button == null || button.IsInteractable();

        private void TransitionAuthored(AuthoredState requestedState)
        {
            AuthoredState state = IsInteractable()
                ? requestedState
                : AuthoredState.Disabled;
            if (_authoredTransition != null)
            {
                StopCoroutine(_authoredTransition);
                _authoredTransition = null;
            }

            if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
            {
                SnapAuthoredState(state);
                return;
            }

            _authoredTransition = StartCoroutine(AnimateAuthoredState(state));
        }

        private void SnapAuthoredState(AuthoredState state)
        {
            ResolveAuthoredTargets(
                state,
                out Color surfaceTarget,
                out Color borderTarget,
                out Vector3 scaleTarget,
                out Vector3 positionTarget,
                out float focusAlpha,
                out float contentOpacity);

            if (background != null)
                background.color = WithOpacity(surfaceTarget, contentOpacity);
            if (border != null)
                border.color = WithOpacity(borderTarget, contentOpacity);
            transform.localScale = scaleTarget;
            transform.localPosition = positionTarget;
            SetContentOpacity(contentOpacity);
            SetFocusOpacity(focusAlpha);
        }

        private void ResolveAuthoredTargets(
            AuthoredState state,
            out Color surfaceTarget,
            out Color borderTarget,
            out Vector3 scaleTarget,
            out Vector3 positionTarget,
            out float focusAlpha,
            out float contentOpacity)
        {
            surfaceTarget = _baseSurface;
            borderTarget = _baseBorder;
            scaleTarget = _baseScale;
            positionTarget = _baseLocalPos;
            focusAlpha = 0f;
            contentOpacity = 1f;

            switch (state)
            {
                case AuthoredState.Hovered:
                    surfaceTarget = authoredHoverSurface;
                    borderTarget = authoredHoverBorder;
                    scaleTarget = _baseScale * authoredHoverScale;
                    positionTarget = _baseLocalPos +
                        Vector3.up * authoredHoverOffsetUiUnits;
                    break;
                case AuthoredState.Focused:
                    borderTarget = authoredFocusedBorder;
                    scaleTarget = _baseScale * authoredHoverScale;
                    positionTarget = _baseLocalPos +
                        Vector3.up * authoredHoverOffsetUiUnits;
                    focusAlpha = 1f;
                    break;
                case AuthoredState.Pressed:
                    surfaceTarget = authoredPressedSurface;
                    borderTarget = authoredPressedBorder;
                    scaleTarget = _baseScale * authoredPressedScale;
                    break;
                case AuthoredState.Disabled:
                    contentOpacity = authoredDisabledOpacity;
                    break;
            }
        }

        private IEnumerator AnimateAuthoredState(AuthoredState state)
        {
            ResolveAuthoredTargets(
                state,
                out Color surfaceTarget,
                out Color borderTarget,
                out Vector3 scaleTarget,
                out Vector3 positionTarget,
                out float focusAlpha,
                out float contentOpacity);

            Color surfaceFrom = background != null ? background.color : Color.clear;
            Color borderFrom = border != null ? border.color : Color.clear;
            Vector3 scaleFrom = transform.localScale;
            Vector3 positionFrom = transform.localPosition;
            float elapsed = 0f;
            float duration = Mathf.Max(0f, authoredTransitionDuration);
            do
            {
                float t = duration <= 0f
                    ? 1f
                    : Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                if (background != null)
                    background.color = Color.Lerp(surfaceFrom,
                        WithOpacity(surfaceTarget, contentOpacity), eased);
                if (border != null)
                    border.color = Color.Lerp(borderFrom,
                        WithOpacity(borderTarget, contentOpacity), eased);
                transform.localScale =
                    Vector3.Lerp(scaleFrom, scaleTarget, eased);
                transform.localPosition =
                    Vector3.Lerp(positionFrom, positionTarget, eased);
                SetContentOpacity(contentOpacity);
                SetFocusOpacity(focusAlpha);
                if (t >= 1f) break;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            } while (true);
            _authoredTransition = null;
        }

        private void SetContentOpacity(float opacity)
        {
            if (icon != null) icon.color = WithOpacity(_baseIcon, opacity);
            if (title != null) title.color = WithOpacity(_baseTitle, opacity);
            if (subtitle != null)
                subtitle.color = WithOpacity(_baseSubtitle, opacity);
        }

        private void SetFocusOpacity(float opacity)
        {
            if (authoredFocusOutline != null)
            {
                Color color = authoredFocusOutline.color;
                color.a = 0.18f * opacity;
                authoredFocusOutline.color = color;
            }
            if (authoredFocusAccent != null)
            {
                Color color = authoredFocusAccent.color;
                color.a = 0.85f * opacity;
                authoredFocusAccent.color = color;
            }
        }

        private static Color WithOpacity(Color color, float opacity)
        {
            color.a *= Mathf.Clamp01(opacity);
            return color;
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
