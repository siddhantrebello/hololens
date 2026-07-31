using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Norscope;
using Norscope.Audio;
using Norscope.DesignSystem;

namespace Norscope.Navigation
{
    /// <summary>
    /// Permanent Home Launcher control. Pinch/press opens Home via PanelRegistry.
    /// Never closed, never duplicated, never grabbed/resized.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HomeLauncher : MonoBehaviour, IPointerEnterHandler,
        IPointerExitHandler, IPointerMoveHandler, IPointerDownHandler, IPointerUpHandler,
        ISelectHandler, IDeselectHandler
    {
        [SerializeField] private PanelRegistry registry;
        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [SerializeField] private Image outline;
        [SerializeField] private Image icon;
        [SerializeField] private Color normalOutline = new Color(0.149f, 0.188f, 0.255f, 0.85f);
        [SerializeField] private Color hoverOutline = new Color(0.133f, 0.827f, 0.933f, 0.95f);
        [SerializeField] private float hoverScale = 1.04f;

        [Header("Authored Launcher Presentation")]
        [SerializeField] private bool useAuthoredPresentation;
        [SerializeField] private RectTransform visualRoot;
        [SerializeField] private Image focusOutline;
        [SerializeField] private Image outerGlow;
        [SerializeField] private Image readyDot;
        [SerializeField] private CanvasGroup hoverLabel;
        [SerializeField] private Color authoredIdleSurface = new Color(0.094f, 0.145f, 0.235f, 1f);
        [SerializeField] private Color authoredHoverSurface = new Color(0.114f, 0.184f, 0.282f, 1f);
        [SerializeField] private Color authoredFocusSurface = new Color(0.106f, 0.176f, 0.275f, 1f);
        [SerializeField] private Color authoredPressedSurface = new Color(0.122f, 0.196f, 0.322f, 1f);
        [SerializeField] private Color authoredIdleBorder = new Color(0.118f, 0.784f, 0.961f, 0.13f);
        [SerializeField] private Color authoredHoverBorder = new Color(0.118f, 0.784f, 0.961f, 0.32f);
        [SerializeField] private Color authoredFocusBorder = new Color(0.118f, 0.784f, 0.961f, 0.78f);
        [SerializeField] private Color authoredPressedBorder = new Color(0.118f, 0.784f, 0.961f, 0.42f);
        [SerializeField] private float authoredHoverScale = 1.034f;
        [SerializeField] private float authoredFocusScale = 1.042f;
        [SerializeField] private float authoredPressedScale = 0.952f;
        [SerializeField] private float authoredHoverOffset = 1.5f;
        [SerializeField] private float authoredFocusOffset = 2f;
        [SerializeField] private float authoredTransitionDuration = 0.18f;

        private Vector3 _legacyBaseScale;
        private Vector3 _visualBaseScale;
        private Vector2 _visualBasePosition;
        private bool _hovered;
        private bool _focused;
        private bool _pressed;
        private float _lastHoverSignalTime = -999f;
        private float _stateBlend = 1f;
        private Color _startSurface;
        private Color _startBorder;
        private float _startScale = 1f;
        private float _startOffset;
        private float _startLabelAlpha;
        private Color _targetSurface;
        private Color _targetBorder;
        private float _targetScale = 1f;
        private float _targetOffset;
        private float _targetLabelAlpha;

        private void Awake()
        {
            _legacyBaseScale = transform.localScale;
            if (visualRoot != null)
            {
                _visualBaseScale = visualRoot.localScale;
                _visualBasePosition = visualRoot.anchoredPosition;
            }
            if (registry == null)
            {
                registry = FindFirstObjectByType<PanelRegistry>();
            }

            if (button == null)
            {
                button = GetComponentInChildren<Button>(true);
            }

            if (button != null)
            {
                button.onClick.RemoveListener(OnPressed);
                button.onClick.AddListener(OnPressed);
            }

            if (useAuthoredPresentation)
            {
                ApplyAuthoredStateImmediately();
            }
            else
            {
                ApplyOutline(false);
                if (GetComponent<UiButtonPressScale>() == null)
                {
                    gameObject.AddComponent<UiButtonPressScale>();
                }
            }
        }

        private void Update()
        {
            if (!useAuthoredPresentation)
                return;

            if (_hovered || _pressed || _focused)
            {
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

                if (changed)
                    BeginAuthoredTransition();
            }

            if (visualRoot == null || _stateBlend >= 1f)
                return;

            float duration = Mathf.Max(0.001f, authoredTransitionDuration);
            _stateBlend = Mathf.Min(1f, _stateBlend + Time.unscaledDeltaTime / duration);
            float t = 1f - Mathf.Pow(1f - _stateBlend, 3f);
            ApplyAuthoredVisual(
                Color.Lerp(_startSurface, _targetSurface, t),
                Color.Lerp(_startBorder, _targetBorder, t),
                Mathf.Lerp(_startScale, _targetScale, t),
                Mathf.Lerp(_startOffset, _targetOffset, t),
                Mathf.Lerp(_startLabelAlpha, _targetLabelAlpha, t));
        }

        private void OnDestroy()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(OnPressed);
            }
        }

        private void OnDisable()
        {
            _hovered = false;
            _pressed = false;
            _focused = false;
            if (useAuthoredPresentation) ApplyAuthoredStateImmediately();
            else
            {
                ApplyOutline(false);
                transform.localScale = _legacyBaseScale;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            NoteHover();
            if (useAuthoredPresentation) BeginAuthoredTransition();
            else
            {
                ApplyOutline(true);
                transform.localScale = _legacyBaseScale * hoverScale;
            }
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            if (_hovered)
                NoteHover();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            _pressed = false;
            _focused = false;
            NorscopeUiPointerState.ClearSelectionIfOurs(transform, button);
            if (useAuthoredPresentation) BeginAuthoredTransition();
            else
            {
                ApplyOutline(false);
                transform.localScale = _legacyBaseScale;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!useAuthoredPresentation) return;
            NoteHover();
            _pressed = true;
            BeginAuthoredTransition();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!useAuthoredPresentation) return;
            _pressed = false;
            BeginAuthoredTransition();
        }

        public void OnSelect(BaseEventData eventData)
        {
            if (!useAuthoredPresentation) return;
            _focused = true;
            BeginAuthoredTransition();
        }

        public void OnDeselect(BaseEventData eventData)
        {
            if (!useAuthoredPresentation) return;
            _focused = false;
            BeginAuthoredTransition();
        }

        private void NoteHover()
        {
            _hovered = true;
            _lastHoverSignalTime = Time.unscaledTime;
        }

        private void OnPressed()
        {
            UIAudioManager.Play(UIAudioEvent.HomeNavigate);

            PanelRegistry target = registry != null ? registry : PanelRegistry.Instance;
            if (target == null)
            {
                Debug.LogError("[NAV] HomeLauncher: PanelRegistry missing.");
                return;
            }

            target.Open(PanelType.Home);
        }

        private void ApplyOutline(bool hover)
        {
            if (outline != null)
            {
                outline.color = hover ? hoverOutline : normalOutline;
                outline.enabled = true;
            }
        }

        private void ApplyAuthoredStateImmediately()
        {
            ResolveAuthoredTarget();
            _stateBlend = 1f;
            ApplyAuthoredVisual(_targetSurface, _targetBorder, _targetScale,
                _targetOffset, _targetLabelAlpha);
        }

        private void BeginAuthoredTransition()
        {
            if (visualRoot == null) return;
            _startSurface = background != null ? background.color : authoredIdleSurface;
            _startBorder = outline != null ? outline.color : authoredIdleBorder;
            _startScale = visualRoot.localScale.x /
                          Mathf.Max(0.0001f, _visualBaseScale.x);
            _startOffset = visualRoot.anchoredPosition.y - _visualBasePosition.y;
            _startLabelAlpha = hoverLabel != null ? hoverLabel.alpha : 0f;
            ResolveAuthoredTarget();
            _stateBlend = 0f;
        }

        private void ResolveAuthoredTarget()
        {
            if (button != null && !button.interactable)
            {
                _targetSurface = authoredIdleSurface;
                _targetBorder = authoredIdleBorder;
                _targetScale = 1f;
                _targetOffset = 0f;
                _targetLabelAlpha = 0f;
                return;
            }

            if (_pressed)
            {
                _targetSurface = authoredPressedSurface;
                _targetBorder = authoredPressedBorder;
                _targetScale = authoredPressedScale;
                _targetOffset = 0f;
            }
            else if (_focused)
            {
                _targetSurface = authoredFocusSurface;
                _targetBorder = authoredFocusBorder;
                _targetScale = authoredFocusScale;
                _targetOffset = authoredFocusOffset;
            }
            else if (_hovered)
            {
                _targetSurface = authoredHoverSurface;
                _targetBorder = authoredHoverBorder;
                _targetScale = authoredHoverScale;
                _targetOffset = authoredHoverOffset;
            }
            else
            {
                _targetSurface = authoredIdleSurface;
                _targetBorder = authoredIdleBorder;
                _targetScale = 1f;
                _targetOffset = 0f;
            }

            _targetLabelAlpha = _hovered || _focused ? 1f : 0f;
        }

        private void ApplyAuthoredVisual(Color surface, Color border, float scale,
            float upwardOffset, float labelAlpha)
        {
            if (background != null) background.color = surface;
            if (outline != null) outline.color = border;
            visualRoot.localScale = _visualBaseScale * scale;
            visualRoot.anchoredPosition =
                _visualBasePosition + new Vector2(0f, upwardOffset);
            if (hoverLabel != null) hoverLabel.alpha = labelAlpha;
            if (focusOutline != null)
            {
                Color c = authoredFocusBorder;
                c.a = (_focused ? 0.17f : 0f) * labelAlpha;
                focusOutline.color = c;
            }
            if (outerGlow != null)
            {
                Color c = authoredFocusBorder;
                c.a = _focused ? 0.19f : _hovered ? 0.14f : 0f;
                outerGlow.color = c;
            }
            if (icon != null)
            {
                Color c = Color.white;
                c.a = _hovered || _focused || _pressed ? 0.97f : 0.85f;
                icon.color = c;
            }
            if (readyDot != null)
            {
                readyDot.color = new Color(0.224f, 0.851f, 0.541f, 1f);
            }
        }
    }
}
