using System.Collections;
using Norscope.DesignSystem;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Norscope.Settings.Motion
{
    /// <summary>
    /// Visual-only button state: Disabled > Selected > Pressed > Hovered > Default.
    /// Does not add click listeners or play audio.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SettingsButtonVisualState : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler,
        IPointerDownHandler, IPointerUpHandler,
        ISelectHandler, IDeselectHandler
    {
        public enum Role
        {
            Navigation,
            PlusMinus,
            Generic
        }

        [SerializeField] private Role role = Role.Generic;
        [SerializeField] private Selectable selectable;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Graphic[] tintTargets;
        [SerializeField] private bool isSelected;
        [SerializeField] private float disabledOpacity = 0.35f;
        [SerializeField] private float pressDuration = 0.14f;
        [SerializeField] private float pressScale = 0.982f;
        [SerializeField] private Color selectedColor = new Color(0.18f, 0.48f, 0.96f, 1f);
        [SerializeField] private Color hoverColor = new Color(1f, 1f, 1f, 0.08f);
        [SerializeField] private Color defaultColor = new Color(1f, 1f, 1f, 0.04f);
        [SerializeField] private bool useBackgroundTint = true;

        private RectTransform _rect;
        private Vector3 _restScale;
        private Coroutine _pressRoutine;
        private bool _hovered;
        private bool _pressed;
        private bool _focused;
        private float _lastHoverSignalTime = -999f;
        private Color[] _baseColors;
        private bool _colorsCached;
        private int _pressToken;

        public bool IsSelected => isSelected;

        public void Configure(
            Role buttonRole,
            Selectable target,
            CanvasGroup group,
            Graphic[] graphics,
            float pressScaleAmount,
            float pressSeconds)
        {
            role = buttonRole;
            selectable = target;
            canvasGroup = group;
            tintTargets = graphics;
            pressScale = pressScaleAmount;
            pressDuration = pressSeconds;
            useBackgroundTint = buttonRole == Role.Navigation;
            if (buttonRole == Role.Navigation)
            {
                selectedColor = new Color(0.118f, 0.784f, 0.961f, 0.22f);
                hoverColor = new Color(0.176f, 0.231f, 0.286f, 0.40f);
            }

            CacheBaseColors();
            ApplyVisual();
        }

        public void SetSelected(bool selected)
        {
            isSelected = selected;
            ApplyVisual();
        }

        public void CaptureAuthoredColorsAsDefault()
        {
            CacheBaseColors(force: true);
            if (tintTargets != null && tintTargets.Length > 0 && tintTargets[0] != null)
                defaultColor = tintTargets[0].color;
            ApplyVisual();
        }

        private void Awake()
        {
            if (selectable == null)
                selectable = GetComponent<Selectable>();
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();
            _rect = transform as RectTransform;
            _restScale = transform.localScale;
            if (role == Role.PlusMinus)
            {
                pressDuration = 0.12f;
                pressScale = 0.90f;
            }
            else if (role == Role.Navigation)
            {
                pressDuration = 0.14f;
                pressScale = 0.982f;
            }

            CacheBaseColors();
            ApplyVisual();
        }

        private bool _wasInteractable = true;

        private void Update()
        {
            bool visualChanged = false;
            bool now = IsInteractable();
            if (now != _wasInteractable)
            {
                _wasInteractable = now;
                if (!now)
                {
                    _hovered = false;
                    _pressed = false;
                    _focused = false;
                    StopPress(true);
                }

                visualChanged = true;
            }

            if (_focused &&
                !NorscopeUiPointerState.IsStillSelected(transform, selectable))
            {
                _focused = false;
                visualChanged = true;
            }

            if ((_hovered || _pressed) &&
                !NorscopeUiPointerState.IsHoverFresh(_lastHoverSignalTime))
            {
                _hovered = false;
                _pressed = false;
                visualChanged = true;
            }

            if (visualChanged)
                ApplyVisual();
        }

        private void OnDisable()
        {
            StopPress(true);
            _hovered = false;
            _pressed = false;
            _focused = false;
            transform.localScale = _restScale;
            ApplyVisual();
        }

        private void OnDestroy()
        {
            StopPress(true);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            NoteHover();
            ApplyVisual();
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
            NorscopeUiPointerState.ClearSelectionIfOurs(transform, selectable);
            ApplyVisual();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!IsInteractable()) return;
            NoteHover();
            _pressed = true;
            ApplyVisual();
            PlayPressPulse();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _pressed = false;
            ApplyVisual();
        }

        public void OnSelect(BaseEventData eventData)
        {
            _focused = true;
            ApplyVisual();
        }

        public void OnDeselect(BaseEventData eventData)
        {
            _focused = false;
            ApplyVisual();
        }

        private void NoteHover()
        {
            _hovered = true;
            _lastHoverSignalTime = Time.unscaledTime;
        }

        private bool IsInteractable()
        {
            return selectable == null || selectable.IsInteractable();
        }

        private bool IsDisabled()
        {
            return !IsInteractable();
        }

        private void ApplyVisual()
        {
            // Priority: Disabled > Selected > Pressed > Hovered/focused > Default
            if (IsDisabled())
            {
                if (canvasGroup != null)
                    canvasGroup.alpha = disabledOpacity;
                ApplyTint(defaultColor);
                return;
            }

            if (canvasGroup != null)
                canvasGroup.alpha = 1f;

            if (!useBackgroundTint || tintTargets == null || tintTargets.Length == 0)
                return;

            if (isSelected)
            {
                // Hover must not remove selected; press may layer slightly.
                Color c = selectedColor;
                if (_pressed)
                    c = Color.Lerp(selectedColor, Color.white, 0.08f);
                ApplyTint(c);
                return;
            }

            if (_pressed)
            {
                ApplyTint(Color.Lerp(defaultColor, Color.white, 0.12f));
                return;
            }

            if (_hovered || _focused)
            {
                ApplyTint(hoverColor);
                return;
            }

            ApplyTint(defaultColor);
        }

        private void ApplyTint(Color color)
        {
            if (tintTargets == null) return;
            for (int i = 0; i < tintTargets.Length; i++)
            {
                if (tintTargets[i] != null)
                    tintTargets[i].color = color;
            }
        }

        private void CacheBaseColors(bool force = false)
        {
            if (_colorsCached && !force) return;
            if (tintTargets == null || tintTargets.Length == 0)
            {
                var img = GetComponent<Image>();
                if (img != null)
                    tintTargets = new Graphic[] { img };
            }

            if (tintTargets != null)
            {
                _baseColors = new Color[tintTargets.Length];
                for (int i = 0; i < tintTargets.Length; i++)
                    _baseColors[i] = tintTargets[i] != null ? tintTargets[i].color : Color.white;
            }

            _colorsCached = true;
        }

        private void PlayPressPulse()
        {
            if (!IsMotionEnabled())
                return;
            StopPress(false);
            _pressRoutine = StartCoroutine(PressRoutine(++_pressToken));
        }

        private IEnumerator PressRoutine(int token)
        {
            float half = Mathf.Max(0.01f, pressDuration * 0.5f);
            float elapsed = 0f;
            while (elapsed < half)
            {
                if (token != _pressToken) yield break;
                float t = elapsed / half;
                float s = Mathf.Lerp(1f, pressScale, t);
                transform.localScale = _restScale * s;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < half)
            {
                if (token != _pressToken) yield break;
                float t = elapsed / half;
                float s = Mathf.Lerp(pressScale, 1f, t);
                transform.localScale = _restScale * s;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            transform.localScale = _restScale;
            _pressRoutine = null;
        }

        private void StopPress(bool bump)
        {
            if (bump) _pressToken++;
            if (_pressRoutine != null)
            {
                StopCoroutine(_pressRoutine);
                _pressRoutine = null;
            }

            transform.localScale = _restScale;
        }

        private static bool IsMotionEnabled()
        {
            var provider = Norscope.DesignSystem.NorscopeDesignProvider.Instance;
            if (provider == null || provider.ActiveProfile == null)
                return true;
            return provider.ActiveProfile.features.enableMotion;
        }
    }
}
