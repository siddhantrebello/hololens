using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Norscope.DesignSystem
{
    /// <summary>
    /// Presentation-only observer. It does not register click listeners and cannot invoke
    /// navigation, task, panel, window, input, or audio behavior.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NorscopeInteractionVisual :
        MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerMoveHandler,
        IPointerDownHandler,
        IPointerUpHandler,
        ISelectHandler,
        IDeselectHandler
    {
        [SerializeField] private NorscopeVisualRole visualRole;
        [SerializeField] private Selectable observedSelectable;
        [SerializeField] private NorscopeHighlightController highlightController;
        [SerializeField] private NorscopeDesignProfile fallbackProfile;

        private bool _hovered;
        private bool _pressed;
        private bool _focused;
        private bool _hasExplicitState;
        private NorscopeVisualState _explicitState;
        private NorscopeVisualState _currentState;
        private NorscopeDesignProvider _subscribedProvider;
        private float _lastHoverSignalTime = -999f;

        public NorscopeVisualRole VisualRole => visualRole;
        public Selectable ObservedSelectable => observedSelectable;
        public NorscopeHighlightController HighlightController => highlightController;

        public void SetReferences(
            NorscopeVisualRole role,
            Selectable selectable,
            NorscopeHighlightController controller,
            NorscopeDesignProfile profile)
        {
            visualRole = role;
            observedSelectable = selectable;
            highlightController = controller;
            fallbackProfile = profile;
        }

        private void OnEnable()
        {
            SubscribeToProvider();
            _hovered = false;
            _pressed = false;
            _focused = false;
            _currentState = ResolveState();
            ApplyState(_currentState, true);
        }

        private void OnDisable()
        {
            UnsubscribeFromProvider();
            _hovered = false;
            _pressed = false;
            _focused = false;
            _currentState = NorscopeVisualState.Idle;
            if (highlightController != null)
                highlightController.TransitionTo(null, 0f, null, false);
        }

        private void Update()
        {
            SubscribeToProvider();
            ReconcilePointerState();
            NorscopeVisualState state = ResolveState();
            if (state != _currentState)
            {
                NorscopeVisualState previous = _currentState;
                _currentState = state;
                ApplyState(previous, false);
            }

            if (highlightController != null)
                highlightController.Tick(Time.unscaledDeltaTime);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!IsInteractable()) return;
            NoteHover();
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            if (!IsInteractable()) return;
            if (_hovered || eventData != null && eventData.pointerEnter != null)
                NoteHover();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            _pressed = false;
            _focused = false;
            NorscopeUiPointerState.ClearSelectionIfOurs(transform, observedSelectable);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!IsInteractable()) return;
            NoteHover();
            _pressed = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _pressed = false;
        }

        public void OnSelect(BaseEventData eventData)
        {
            if (IsInteractable()) _focused = true;
        }

        public void OnDeselect(BaseEventData eventData)
        {
            _focused = false;
        }

        public void SetPresentationState(NorscopeVisualState state)
        {
            _explicitState = state;
            _hasExplicitState = state == NorscopeVisualState.Highlighted ||
                                state == NorscopeVisualState.Verified ||
                                state == NorscopeVisualState.Completed;
        }

        public void ClearPresentationState()
        {
            _hasExplicitState = false;
        }

        private void NoteHover()
        {
            _hovered = true;
            _lastHoverSignalTime = Time.unscaledTime;
        }

        private void ReconcilePointerState()
        {
            if (_focused &&
                !NorscopeUiPointerState.IsStillSelected(transform, observedSelectable))
            {
                _focused = false;
            }

            if ((_hovered || _pressed) &&
                !NorscopeUiPointerState.IsHoverFresh(_lastHoverSignalTime))
            {
                _hovered = false;
                _pressed = false;
            }
        }

        private NorscopeVisualState ResolveState()
        {
            if (!IsInteractable()) return NorscopeVisualState.Disabled;
            if (_hasExplicitState) return _explicitState;
            if (_pressed) return NorscopeVisualState.Pressed;
            if (_focused) return NorscopeVisualState.Selected;

            NorscopeDesignProfile profile = ResolveProfile();
            if (_hovered && profile != null && profile.features.enableHoverGlow)
                return NorscopeVisualState.Hovered;

            return NorscopeVisualState.Idle;
        }

        private void ApplyState(NorscopeVisualState previousState, bool immediate)
        {
            NorscopeDesignProfile profile = ResolveProfile();
            if (profile == null || highlightController == null)
            {
                if (highlightController != null)
                    highlightController.TransitionTo(null, 0f, null, false);
                return;
            }

            InteractionStateStyle style =
                profile.GetInteractionStyle(visualRole, _currentState);
            float duration = immediate ? 0f : ResolveDuration(profile, previousState, _currentState);
            AnimationCurve curve = ResolveCurve(profile, _currentState);
            highlightController.TransitionTo(
                style,
                duration,
                curve,
                profile.features.enableMotion && !immediate);
        }

        private NorscopeDesignProfile ResolveProfile()
        {
            NorscopeDesignProvider provider = NorscopeDesignProvider.Instance;
            return provider != null && provider.ActiveProfile != null
                ? provider.ActiveProfile
                : fallbackProfile;
        }

        private bool IsInteractable()
        {
            return observedSelectable == null || observedSelectable.IsInteractable();
        }

        private static float ResolveDuration(
            NorscopeDesignProfile profile,
            NorscopeVisualState previous,
            NorscopeVisualState current)
        {
            if (current == NorscopeVisualState.Pressed) return profile.motion.pressDuration;
            if (previous == NorscopeVisualState.Pressed) return profile.motion.releaseDuration;
            if (current == NorscopeVisualState.Hovered) return profile.motion.hoverInDuration;
            if (previous == NorscopeVisualState.Hovered) return profile.motion.hoverOutDuration;
            if (current == NorscopeVisualState.Verified)
                return profile.motion.verificationDuration;
            if (current == NorscopeVisualState.Completed)
                return profile.motion.completionDuration;
            return profile.motion.selectionDuration;
        }

        private static AnimationCurve ResolveCurve(
            NorscopeDesignProfile profile,
            NorscopeVisualState state)
        {
            if (state == NorscopeVisualState.Pressed) return profile.motion.pressEase;
            if (state == NorscopeVisualState.Hovered) return profile.motion.hoverEase;
            if (state == NorscopeVisualState.Verified)
                return profile.motion.verificationEase;
            return profile.motion.defaultEase;
        }

        private void SubscribeToProvider()
        {
            NorscopeDesignProvider provider = NorscopeDesignProvider.Instance;
            if (_subscribedProvider == provider) return;
            UnsubscribeFromProvider();
            _subscribedProvider = provider;
            if (_subscribedProvider != null)
                _subscribedProvider.ProfileChanged += OnProfileChanged;
        }

        private void UnsubscribeFromProvider()
        {
            if (_subscribedProvider != null)
            {
                _subscribedProvider.ProfileChanged -= OnProfileChanged;
                _subscribedProvider = null;
            }
        }

        private void OnProfileChanged(NorscopeDesignProfile profile)
        {
            ApplyState(_currentState, true);
        }
    }
}
