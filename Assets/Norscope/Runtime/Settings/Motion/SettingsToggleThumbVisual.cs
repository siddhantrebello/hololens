using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Norscope.Settings.Motion
{
    /// <summary>
    /// Visual-only toggle thumb/track. Does not write settings or play audio.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SettingsToggleThumbVisual : MonoBehaviour
    {
        [SerializeField] private RectTransform thumb;
        [SerializeField] private Graphic trackGraphic;
        [SerializeField] private float duration = 0.20f;
        [SerializeField] private float offX = -10f;
        [SerializeField] private float onX = 10f;
        [SerializeField] private Color offTrackColor = new Color(1f, 1f, 1f, 0.12f);
        [SerializeField] private Color onTrackColor = new Color(0.18f, 0.48f, 0.96f, 1f);

        private Coroutine _routine;
        private bool _isOn;
        private int _token;

        public void Configure(
            RectTransform thumbRect,
            Graphic track,
            float offAnchoredX,
            float onAnchoredX,
            Color offColor,
            Color onColor)
        {
            thumb = thumbRect;
            trackGraphic = track;
            offX = offAnchoredX;
            onX = onAnchoredX;
            offTrackColor = offColor;
            onTrackColor = onColor;
        }

        private void OnDisable()
        {
            StopActive();
            ApplyImmediate(_isOn);
        }

        private void OnDestroy()
        {
            StopActive();
        }

        /// <summary>Called by SettingsAudioController. Visual only.</summary>
        public void SetOn(bool on, bool animate = true)
        {
            _isOn = on;
            bool motion = animate && IsMotionEnabled() && isActiveAndEnabled;
            if (!motion)
            {
                StopActive();
                ApplyImmediate(on);
                return;
            }

            StopActive(false);
            _routine = StartCoroutine(AnimateRoutine(++_token, on));
        }

        public void SnapOn(bool on)
        {
            _isOn = on;
            StopActive();
            ApplyImmediate(on);
        }

        private IEnumerator AnimateRoutine(int token, bool on)
        {
            float fromX = thumb != null ? thumb.anchoredPosition.x : (on ? offX : onX);
            float toX = on ? onX : offX;
            Color fromC = trackGraphic != null ? trackGraphic.color : (on ? offTrackColor : onTrackColor);
            Color toC = on ? onTrackColor : offTrackColor;

            float elapsed = 0f;
            float dur = Mathf.Max(0.01f, duration);
            AnimationCurve curve = SettingsMotionEasing.FigmaEaseOut;
            while (elapsed < dur)
            {
                if (token != _token) yield break;
                float t = SettingsMotionEasing.Evaluate(curve, elapsed / dur);
                if (thumb != null)
                {
                    Vector2 p = thumb.anchoredPosition;
                    p.x = Mathf.LerpUnclamped(fromX, toX, t);
                    thumb.anchoredPosition = p;
                }

                if (trackGraphic != null)
                    trackGraphic.color = Color.LerpUnclamped(fromC, toC, t);

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (token == _token)
            {
                ApplyImmediate(on);
                _routine = null;
            }
        }

        private void ApplyImmediate(bool on)
        {
            if (thumb != null)
            {
                Vector2 p = thumb.anchoredPosition;
                p.x = on ? onX : offX;
                thumb.anchoredPosition = p;
            }

            if (trackGraphic != null)
                trackGraphic.color = on ? onTrackColor : offTrackColor;
        }

        private void StopActive(bool bump = true)
        {
            if (bump) _token++;
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }
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
