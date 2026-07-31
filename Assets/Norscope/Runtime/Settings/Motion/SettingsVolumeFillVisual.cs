using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Norscope.Settings.Motion
{
    /// <summary>
    /// Visual-only volume fill. Does not write slider/mixer values.
    /// Controller remains the source of truth via SetNormalized.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SettingsVolumeFillVisual : MonoBehaviour
    {
        [SerializeField] private Image fillImage;
        [SerializeField] private float duration = 0.25f;
        [SerializeField] private bool useFillAmount = true;

        private Coroutine _routine;
        private float _current;
        private float _target;
        private int _token;

        public void Configure(Image image, float seconds = 0.25f)
        {
            fillImage = image;
            duration = seconds;
            if (fillImage != null && useFillAmount)
            {
                fillImage.type = Image.Type.Filled;
                fillImage.fillMethod = Image.FillMethod.Horizontal;
                fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            }
        }

        private void OnDisable()
        {
            StopActive();
            ApplyImmediate(_target);
        }

        private void OnDestroy()
        {
            StopActive();
        }

        /// <summary>Called by SettingsAudioController when volume changes. Visual only.</summary>
        public void SetNormalized(float normalized, bool animate = true)
        {
            _target = Mathf.Clamp01(normalized);
            bool motion = animate && IsMotionEnabled();
            if (!motion || !isActiveAndEnabled)
            {
                StopActive();
                ApplyImmediate(_target);
                return;
            }

            StopActive(false);
            _routine = StartCoroutine(AnimateRoutine(++_token, _current, _target));
        }

        public void SnapNormalized(float normalized)
        {
            _target = Mathf.Clamp01(normalized);
            StopActive();
            ApplyImmediate(_target);
        }

        private IEnumerator AnimateRoutine(int token, float from, float to)
        {
            float elapsed = 0f;
            float dur = Mathf.Max(0.01f, duration);
            AnimationCurve curve = SettingsMotionEasing.EaseOutQuad;
            while (elapsed < dur)
            {
                if (token != _token) yield break;
                float t = SettingsMotionEasing.Evaluate(curve, elapsed / dur);
                ApplyImmediate(Mathf.LerpUnclamped(from, to, t));
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (token == _token)
            {
                ApplyImmediate(to);
                _routine = null;
            }
        }

        private void ApplyImmediate(float value)
        {
            _current = value;
            if (fillImage == null) return;
            if (useFillAmount)
                fillImage.fillAmount = value;
            else
            {
                var rt = fillImage.rectTransform;
                Vector2 max = rt.anchorMax;
                max.x = value;
                rt.anchorMax = max;
            }
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
