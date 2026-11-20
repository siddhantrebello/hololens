using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Norscope.Audio;

namespace Norscope.ArGuidance
{
    /// <summary>
    /// Reusable press-and-hold for calibration nudges. One immediate fire, then repeat.
    /// Cancels on release, exit, disable, and destroy. No coroutine accumulation.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class ArPressAndHoldButton : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public const float DefaultInitialDelaySeconds = 0.35f;
        public const float DefaultRepeatIntervalSeconds = 0.10f;

        [SerializeField] private float initialDelaySeconds = DefaultInitialDelaySeconds;
        [SerializeField] private float repeatIntervalSeconds = DefaultRepeatIntervalSeconds;
        [SerializeField] private bool playAudioOnHoldBegin = true;

        private Button _button;
        private Action _action;
        private Action _onRelease;
        private bool _holding;
        private bool _repeating;
        private float _holdStartTime;
        private float _nextRepeatTime;

        public void Bind(Action action, Action onRelease = null)
        {
            _action = action;
            _onRelease = onRelease;
            if (_button == null)
                _button = GetComponent<Button>();
        }

        public void Unbind()
        {
            StopHold();
            _action = null;
            _onRelease = null;
        }

        private void Awake()
        {
            _button = GetComponent<Button>();
        }

        private void OnDisable() => StopHold();

        private void OnDestroy() => StopHold();

        private void Update()
        {
            if (!_holding || _action == null)
                return;
            if (_button != null && !_button.interactable)
            {
                StopHold();
                return;
            }

            float now = Time.unscaledTime;
            if (!_repeating)
            {
                if (now - _holdStartTime < initialDelaySeconds)
                    return;
                _repeating = true;
                _nextRepeatTime = now;
            }

            if (now < _nextRepeatTime)
                return;

            _action.Invoke();
            _nextRepeatTime = now + repeatIntervalSeconds;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_action == null)
                return;
            if (_button != null && !_button.interactable)
                return;

            StopHold();
            _holding = true;
            _repeating = false;
            _holdStartTime = Time.unscaledTime;

            if (playAudioOnHoldBegin)
                UIAudioManager.Play(UIAudioEvent.ButtonClick);

            _action.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData) => StopHold();

        public void OnPointerExit(PointerEventData eventData) => StopHold();

        private void StopHold()
        {
            bool wasHolding = _holding;
            _holding = false;
            _repeating = false;
            if (wasHolding)
                _onRelease?.Invoke();
        }
    }
}
