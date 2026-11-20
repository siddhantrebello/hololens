using UnityEngine;
using TMPro;

namespace Norscope.ArGuidance
{
    /// <summary>
    /// Instructional inspection warning: amber perimeter + caution triangle + dual labels.
    /// Pulses VisualPulseRoot child only — never the hologram root.
    /// </summary>
    public sealed class ArInspectionWarningVisual : ArHologramVisualBase
    {
        [Header("Perimeter (metres)")]
        [SerializeField] private Vector2 perimeterSizeMeters = new Vector2(0.200f, 0.080f);
        [SerializeField] private float outlineThicknessMeters = 0.0018f;
        [Tooltip("Quarter-circle radius on each corner. 0 = sharp rectangle.")]
        [SerializeField] private float cornerRadiusMeters = 0.008f;
        [SerializeField, Range(0f, 0.05f)] private float pulseScaleMax = 0.04f;
        [SerializeField] private float pulseHz = 0.55f;

        [Header("Warning triangle (metres)")]
        [SerializeField] private float warningIconSizeMeters = 0.027f;
        [SerializeField, Range(0f, 0.05f)] private float triangleBreathAmount = 0.03f;
        [SerializeField] private Vector3 triangleLocalPosition = new Vector3(0f, 0.055f, 0f);

        [Header("Labels")]
        [SerializeField] private string secondaryLabelText = "Do not continue if damaged";
        [SerializeField] private Vector3 secondaryLabelOffset = new Vector3(0f, 0.040f, 0f);

        [Header("Wiring")]
        [SerializeField] private Transform visualPulseRoot;
        [SerializeField] private LineRenderer perimeter;
        [SerializeField] private Transform warningTriangleRoot;
        [SerializeField] private LineRenderer warningTriangle;
        [SerializeField] private TMP_Text label;
        [SerializeField] private TMP_Text secondaryLabel;
        [SerializeField] private SpriteRenderer fallbackIcon;

        private const int CornerArcSegments = 6;

        private Vector3 _pulseBaseScale = Vector3.one;
        private Vector3 _triangleBaseScale = Vector3.one;
        private readonly Vector3[] _periScratch = new Vector3[4 * CornerArcSegments];
        private readonly Vector3[] _triScratch = new Vector3[3];

        protected override void Awake()
        {
            base.Awake();
            if (VisualState == ArHologramVisualState.Danger)
                SetVisualState(ArHologramVisualState.Warning);
            CacheBases();
            RebuildGeometry();
            ApplyLabels();
            SetFallbackIconActive(false);
        }

        private void CacheBases()
        {
            if (visualPulseRoot != null)
                _pulseBaseScale = visualPulseRoot.localScale;
            if (warningTriangleRoot != null)
                _triangleBaseScale = warningTriangleRoot.localScale;
        }

        private void Update()
        {
            if (!AnimationEnabled)
            {
                if (visualPulseRoot != null)
                    visualPulseRoot.localScale = _pulseBaseScale;
                if (warningTriangleRoot != null)
                    warningTriangleRoot.localScale = _triangleBaseScale;
                return;
            }

            float wave = Mathf.Sin(Time.unscaledTime * pulseHz * Mathf.PI * 2f * AnimationSpeed);

            // Perimeter expansion on VisualPulseRoot only.
            if (visualPulseRoot != null)
            {
                float s = 1f + pulseScaleMax * (0.5f + 0.5f * wave);
                visualPulseRoot.localScale = _pulseBaseScale * s;
            }

            // Gentle triangle breath on its own child — still not the hologram root.
            if (warningTriangleRoot != null)
            {
                float s = 1f + triangleBreathAmount * (0.5f + 0.5f * wave);
                warningTriangleRoot.localScale = _triangleBaseScale * s;
            }
        }

        protected override void ApplyAppearance()
        {
            ApplyLineColor(perimeter, CurrentColor);
            ApplyLineColor(warningTriangle, CurrentColor);
            if (label != null)
                label.color = CurrentColor;
            if (secondaryLabel != null)
            {
                Color c = CurrentColor;
                c.a *= 0.85f;
                secondaryLabel.color = c;
            }
        }

        public override void SetVisualState(ArHologramVisualState state)
        {
            // Instructional caution defaults to Warning/amber. Red only when explicitly Danger.
            if (state == ArHologramVisualState.Default)
                state = ArHologramVisualState.Warning;
            base.SetVisualState(state);
        }

        protected override void OnLabelChanged() => ApplyLabels();

        private void ApplyLabels()
        {
            if (label != null)
            {
                label.text = string.IsNullOrEmpty(LabelText) ? "CHECK FOR SWELLING" : LabelText;
                label.raycastTarget = false;
                label.transform.localPosition = LabelOffset;
            }

            if (secondaryLabel != null)
            {
                secondaryLabel.text = secondaryLabelText ?? string.Empty;
                secondaryLabel.raycastTarget = false;
                secondaryLabel.transform.localPosition = secondaryLabelOffset;
            }
        }

        public void SetFallbackIconActive(bool active)
        {
            if (fallbackIcon != null)
                fallbackIcon.enabled = active && fallbackIcon.sprite != null;
        }

        public void RebuildGeometry()
        {
            float hw = perimeterSizeMeters.x * 0.5f;
            float hd = perimeterSizeMeters.y * 0.5f;
            if (perimeter != null)
            {
                perimeter.useWorldSpace = false;
                perimeter.loop = true;
                perimeter.widthMultiplier = outlineThicknessMeters;

                float radius = Mathf.Clamp(cornerRadiusMeters, 0f, Mathf.Min(hw, hd));
                if (radius > 0.0001f)
                {
                    // Same rounded-corner traversal as ArOutlineVisual (bottom→right→top→left).
                    Vector3[] corners =
                    {
                        new Vector3(-hw, 0f, -hd),
                        new Vector3(hw, 0f, -hd),
                        new Vector3(hw, 0f, hd),
                        new Vector3(-hw, 0f, hd)
                    };
                    Vector3[] inwardX =
                    {
                        Vector3.right, Vector3.left, Vector3.left, Vector3.right
                    };
                    Vector3[] inwardZ =
                    {
                        Vector3.forward, Vector3.forward, Vector3.back, Vector3.back
                    };
                    bool[] reverse = { true, false, true, false };
                    for (int c = 0; c < 4; c++)
                    {
                        WriteCornerArc(
                            _periScratch, c * CornerArcSegments,
                            corners[c], inwardX[c], inwardZ[c], radius, reverse[c]);
                    }

                    perimeter.positionCount = 4 * CornerArcSegments;
                    perimeter.SetPositions(_periScratch);
                }
                else
                {
                    perimeter.positionCount = 4;
                    _periScratch[0] = new Vector3(-hw, 0f, -hd);
                    _periScratch[1] = new Vector3(hw, 0f, -hd);
                    _periScratch[2] = new Vector3(hw, 0f, hd);
                    _periScratch[3] = new Vector3(-hw, 0f, hd);
                    perimeter.SetPositions(_periScratch);
                }
            }

            if (warningTriangle != null)
            {
                float s = warningIconSizeMeters;
                warningTriangle.useWorldSpace = false;
                warningTriangle.loop = true;
                warningTriangle.widthMultiplier = outlineThicknessMeters;
                warningTriangle.positionCount = 3;
                _triScratch[0] = new Vector3(0f, 0f, s * 0.55f);
                _triScratch[1] = new Vector3(-s * 0.5f, 0f, -s * 0.35f);
                _triScratch[2] = new Vector3(s * 0.5f, 0f, -s * 0.35f);
                warningTriangle.SetPositions(_triScratch);
            }

            if (warningTriangleRoot != null && !Application.isPlaying)
                warningTriangleRoot.localPosition = triangleLocalPosition;

            CacheBases();
            ApplyAppearance();
        }

        private static void WriteCornerArc(
            Vector3[] into, int offset, Vector3 corner,
            Vector3 inwardX, Vector3 inwardZ, float radius, bool reverse)
        {
            Vector3 center = corner + inwardX * radius + inwardZ * radius;
            for (int i = 0; i < CornerArcSegments; i++)
            {
                float t = i / (float)(CornerArcSegments - 1);
                if (reverse)
                    t = 1f - t;
                float a = t * Mathf.PI * 0.5f;
                Vector3 dir = -inwardZ * Mathf.Cos(a) - inwardX * Mathf.Sin(a);
                into[offset + i] = center + dir * radius;
            }
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
