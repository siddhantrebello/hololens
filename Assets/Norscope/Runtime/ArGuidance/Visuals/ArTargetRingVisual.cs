using UnityEngine;
using TMPro;

namespace Norscope.ArGuidance
{
    /// <summary>
    /// Physical button target: dominant ring + secondary power glyph with press cue.
    /// Animates ring opacity and glyph child position only — never the hologram root.
    /// </summary>
    public sealed class ArTargetRingVisual : ArHologramVisualBase
    {
        [Header("Ring (metres)")]
        [SerializeField] private float ringDiameterMeters = 0.022f;
        [SerializeField] private float ringThicknessMeters = 0.0018f;
        [SerializeField, Range(0f, 0.35f)] private float ringPulseAmount = 0.18f;

        [Header("Power glyph (metres)")]
        [SerializeField] private float powerGlyphSizeMeters = 0.025f;
        [SerializeField] private float glyphLineThicknessMeters = 0.0015f;
        [SerializeField] private float pressTravelMeters = 0.0065f;
        [SerializeField] private float loopSeconds = 1.75f;
        [SerializeField] private Vector3 pressAxisLocal = new Vector3(0f, -1f, 0f);
        [SerializeField] private Vector3 glyphRestLocalPosition = new Vector3(0f, 0.028f, 0f);

        [Header("Wiring")]
        [SerializeField] private LineRenderer ring;
        [SerializeField] private LineRenderer powerGlyph;
        [SerializeField] private Transform powerGlyphRoot;
        [SerializeField] private TMP_Text label;
        [SerializeField] private SpriteRenderer fallbackIcon;

        private Vector3 _glyphRest;
        private bool _glyphCached;
        private readonly Vector3[] _ringScratch = new Vector3[28];

        protected override void Awake()
        {
            base.Awake();
            CacheGlyph(force: true);
            RebuildGeometry();
            ApplyLabel();
            SetFallbackIconActive(false);
        }

        private void OnEnable() => CacheGlyph(force: true);

        public override void NotifyPoseApplied()
        {
            // Root pose is owned by library/calibration — glyph rest is a child local offset.
            CacheGlyph(force: false);
        }

        private void CacheGlyph(bool force)
        {
            if (_glyphCached && !force)
                return;
            if (powerGlyphRoot != null)
                _glyphRest = powerGlyphRoot.localPosition;
            else
                _glyphRest = glyphRestLocalPosition;
            _glyphCached = true;
        }

        private void Update()
        {
            if (!_glyphCached)
                CacheGlyph(force: true);

            if (!AnimationEnabled)
            {
                if (powerGlyphRoot != null)
                    powerGlyphRoot.localPosition = _glyphRest;
                ApplyLineColor(ring, CurrentColor);
                ApplyLineColor(powerGlyph, CurrentColor);
                return;
            }

            float loop = Mathf.Max(0.05f, loopSeconds / Mathf.Max(0.01f, AnimationSpeed));
            float t = (Time.unscaledTime % loop) / loop;

            // Ring soft opacity pulse (dominant cue).
            float pulse = 1f - ringPulseAmount * (0.5f + 0.5f * Mathf.Sin(t * Mathf.PI * 2f));
            Color ringColor = CurrentColor;
            ringColor.a *= pulse;
            ApplyLineColor(ring, ringColor);

            // Glyph press toward button, hold, return. Never touches root.
            float press;
            if (t < 0.35f)
                press = EaseOutQuad(t / 0.35f);
            else if (t < 0.50f)
                press = 1f;
            else if (t < 0.85f)
                press = 1f - EaseOutQuad((t - 0.50f) / 0.35f);
            else
                press = 0f;

            Vector3 axis = pressAxisLocal.sqrMagnitude > 1e-6f
                ? pressAxisLocal.normalized
                : Vector3.down;
            if (powerGlyphRoot != null)
                powerGlyphRoot.localPosition = _glyphRest + axis * (press * pressTravelMeters);

            // Glyph slightly dimmer than ring so ring stays dominant.
            Color glyphColor = CurrentColor;
            glyphColor.a *= 0.85f;
            ApplyLineColor(powerGlyph, glyphColor);
        }

        protected override void ApplyAppearance()
        {
            ApplyLineColor(ring, CurrentColor);
            Color glyphColor = CurrentColor;
            glyphColor.a *= 0.85f;
            ApplyLineColor(powerGlyph, glyphColor);
            if (label != null)
                label.color = CurrentColor;
        }

        protected override void OnLabelChanged() => ApplyLabel();

        private void ApplyLabel()
        {
            if (label == null)
                return;
            label.text = string.IsNullOrEmpty(LabelText) ? "PRESS POWER" : LabelText;
            label.raycastTarget = false;
            label.transform.localPosition = LabelOffset;
        }

        public void SetFallbackIconActive(bool active)
        {
            if (fallbackIcon != null)
                fallbackIcon.enabled = active && fallbackIcon.sprite != null;
        }

        public void RebuildGeometry()
        {
            if (ring != null)
            {
                float r = ringDiameterMeters * 0.5f;
                const int segments = 28;
                ring.useWorldSpace = false;
                ring.loop = true;
                ring.widthMultiplier = ringThicknessMeters;
                ring.positionCount = segments;
                for (int i = 0; i < segments; i++)
                {
                    float a = (i / (float)segments) * Mathf.PI * 2f;
                    _ringScratch[i] = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                }

                ring.SetPositions(_ringScratch);
            }

            if (powerGlyph != null)
            {
                float s = powerGlyphSizeMeters;
                powerGlyph.useWorldSpace = false;
                powerGlyph.loop = false;
                powerGlyph.widthMultiplier = glyphLineThicknessMeters;
                const int segs = 20;
                powerGlyph.positionCount = segs + 2;
                float rad = s * 0.42f;
                for (int i = 0; i < segs; i++)
                {
                    float u = 0.15f + (i / (float)(segs - 1)) * 0.7f;
                    float a = u * Mathf.PI * 2f - Mathf.PI * 0.5f;
                    powerGlyph.SetPosition(i, new Vector3(Mathf.Cos(a) * rad, 0f, Mathf.Sin(a) * rad));
                }

                powerGlyph.SetPosition(segs, new Vector3(0f, 0f, rad * 0.2f));
                powerGlyph.SetPosition(segs + 1, new Vector3(0f, 0f, rad * 1.05f));
            }

            if (powerGlyphRoot != null && !Application.isPlaying)
            {
                powerGlyphRoot.localPosition = glyphRestLocalPosition;
                _glyphRest = glyphRestLocalPosition;
                _glyphCached = true;
            }

            ApplyAppearance();
        }

        private static float EaseOutQuad(float x)
        {
            float u = 1f - x;
            return 1f - u * u;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying)
                RebuildGeometry();
        }
#endif
    }
}
