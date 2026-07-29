using UnityEngine;
using UnityEngine.UI;

namespace Norscope.DesignSystem
{
    /// <summary>
    /// Interpolates only the decorative graphics explicitly assigned to it. It never touches
    /// the host control's target graphic, layout transform, material, or interaction state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NorscopeHighlightController : MonoBehaviour
    {
        [SerializeField] private Image surfaceGlow;
        [SerializeField] private Image edgeGraphic;
        [SerializeField] private Outline edgeOutline;
        [SerializeField] private CanvasGroup optionalLabel;
        [SerializeField] private Transform optionalVisualScaleTarget;

        private Color _surfaceFrom;
        private Color _surfaceTo;
        private Color _edgeFrom;
        private Color _edgeTo;
        private Vector2 _outlineFrom;
        private Vector2 _outlineTo;
        private float _labelFrom;
        private float _labelTo;
        private Vector3 _scaleFrom;
        private Vector3 _scaleTo;
        private float _elapsed;
        private float _duration;
        private AnimationCurve _curve;
        private bool _transitioning;

        public Image SurfaceGlow => surfaceGlow;
        public Image EdgeGraphic => edgeGraphic;
        public Outline EdgeOutline => edgeOutline;

        public void SetReferences(
            Image surface,
            Image edge,
            Outline outline,
            CanvasGroup label,
            Transform visualScaleTarget)
        {
            surfaceGlow = surface;
            edgeGraphic = edge;
            edgeOutline = outline;
            optionalLabel = label;
            optionalVisualScaleTarget = visualScaleTarget;
        }

        public void TransitionTo(
            InteractionStateStyle style,
            float duration,
            AnimationCurve easing,
            bool allowMotion)
        {
            if (style == null)
            {
                SetDecorativeObjectsVisible(false);
                return;
            }

            SetDecorativeObjectsVisible(true);
            Color surfaceTarget = WithAuthoredIntensity(
                style.surfaceColor,
                style.surfaceOpacity,
                style.luminanceStrength);
            Color edgeTarget = WithAuthoredIntensity(
                style.edgeColor,
                style.edgeOpacity,
                style.luminanceStrength);
            Vector2 outlineTarget = Vector2.one * style.outlineStrength;
            float labelTarget = style.edgeOpacity > 0f ? 1f : 0f;
            Vector3 scaleTarget = Vector3.one * style.visualScale;

            if (!allowMotion || duration <= 0f)
            {
                Apply(surfaceTarget, edgeTarget, outlineTarget, labelTarget, scaleTarget);
                _transitioning = false;
                return;
            }

            _surfaceFrom = surfaceGlow != null ? surfaceGlow.color : Color.clear;
            _surfaceTo = surfaceTarget;
            _edgeFrom = edgeOutline != null
                ? edgeOutline.effectColor
                : (edgeGraphic != null ? edgeGraphic.color : Color.clear);
            _edgeTo = edgeTarget;
            _outlineFrom = edgeOutline != null ? edgeOutline.effectDistance : Vector2.zero;
            _outlineTo = outlineTarget;
            _labelFrom = optionalLabel != null ? optionalLabel.alpha : 0f;
            _labelTo = labelTarget;
            _scaleFrom = optionalVisualScaleTarget != null
                ? optionalVisualScaleTarget.localScale
                : Vector3.one;
            _scaleTo = scaleTarget;
            _elapsed = 0f;
            _duration = duration;
            _curve = easing;
            _transitioning = true;
        }

        public void Tick(float deltaTime)
        {
            if (!_transitioning) return;
            _elapsed += Mathf.Max(0f, deltaTime);
            float normalized = _duration > 0f ? Mathf.Clamp01(_elapsed / _duration) : 1f;
            float eased = _curve != null ? _curve.Evaluate(normalized) : normalized;
            Apply(
                Color.LerpUnclamped(_surfaceFrom, _surfaceTo, eased),
                Color.LerpUnclamped(_edgeFrom, _edgeTo, eased),
                Vector2.LerpUnclamped(_outlineFrom, _outlineTo, eased),
                Mathf.LerpUnclamped(_labelFrom, _labelTo, eased),
                Vector3.LerpUnclamped(_scaleFrom, _scaleTo, eased));
            if (normalized >= 1f) _transitioning = false;
        }

        private void Apply(
            Color surface,
            Color edge,
            Vector2 outline,
            float labelAlpha,
            Vector3 visualScale)
        {
            if (surfaceGlow != null) surfaceGlow.color = surface;
            if (edgeOutline != null)
            {
                edgeOutline.effectColor = edge;
                edgeOutline.effectDistance = outline;
            }
            if (optionalLabel != null) optionalLabel.alpha = labelAlpha;
            if (optionalVisualScaleTarget != null)
            {
                optionalVisualScaleTarget.localScale = visualScale;
            }
        }

        private void SetDecorativeObjectsVisible(bool visible)
        {
            if (surfaceGlow != null) surfaceGlow.enabled = visible;
            if (edgeGraphic != null) edgeGraphic.enabled = visible;
        }

        private static Color WithAuthoredIntensity(
            Color source,
            float opacity,
            float intensity)
        {
            float safeIntensity = Mathf.Max(0f, intensity);
            return new Color(
                Mathf.Clamp01(source.r * safeIntensity),
                Mathf.Clamp01(source.g * safeIntensity),
                Mathf.Clamp01(source.b * safeIntensity),
                Mathf.Clamp01(source.a * opacity));
        }
    }
}
