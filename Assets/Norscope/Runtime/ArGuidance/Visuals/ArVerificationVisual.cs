using UnityEngine;
using TMPro;

namespace Norscope.ArGuidance
{
    /// <summary>Reusable verification mark + optional icon + dual-line label.</summary>
    public sealed class ArVerificationVisual : ArHologramVisualBase
    {
        [Header("Verification (metres)")]
        [SerializeField] private float checkSizeMeters = 0.040f;
        [SerializeField] private float lineThicknessMeters = 0.003f;
        [SerializeField] private float rippleMaxScale = 1.35f;
        [SerializeField] private bool greenRipple = true;
        [Tooltip("Longest edge of the battery icon quad, in metres.")]
        [SerializeField, Range(0.010f, 0.050f)] private float batteryIconSizeMeters = 0.022f;
        [Tooltip("Height-to-width ratio of the battery icon quad, preserved when resizing " +
                 "(the icon isn't square — this keeps its authored proportions).")]
        [SerializeField, Range(0.02f, 1f)] private float batteryIconAspectRatio = 0.08f;

        [SerializeField] private LineRenderer checkMark;
        [SerializeField] private LineRenderer rippleRing;
        [SerializeField] private Transform batteryIcon;
        [SerializeField] private TMP_Text label;

        private Vector3 _rippleBaseScale;

        protected override void Awake()
        {
            base.Awake();
            SetVisualState(ArHologramVisualState.Completed);
            if (rippleRing != null)
                _rippleBaseScale = rippleRing.transform.localScale;
            RebuildGeometry();
            ApplyLabel();
        }

        private void Update()
        {
            if (!AnimationEnabled || !greenRipple || rippleRing == null)
                return;
            float t = (Time.unscaledTime * 0.55f * AnimationSpeed) % 1f;
            float s = Mathf.Lerp(1f, rippleMaxScale, t);
            rippleRing.transform.localScale = _rippleBaseScale * s;
            Color c = CurrentColor;
            c.a *= 1f - t;
            ApplyLineColor(rippleRing, c);
        }

        protected override void ApplyAppearance()
        {
            base.ApplyAppearance();
            ApplyLineColor(checkMark, CurrentColor);
            ApplyLineColor(rippleRing, CurrentColor);
            if (label != null)
                label.color = CurrentColor;
        }

        private void ApplyLabel()
        {
            if (label == null)
                return;
            label.text = string.IsNullOrEmpty(LabelText)
                ? "Verify battery is detected and charging\nConfirm charging status"
                : LabelText;
            label.raycastTarget = false;
            label.transform.localPosition = LabelOffset;
        }

        protected override void OnLabelChanged() => ApplyLabel();

        public void RebuildGeometry()
        {
            float s = checkSizeMeters;
            if (checkMark != null)
            {
                checkMark.useWorldSpace = false;
                checkMark.loop = false;
                checkMark.widthMultiplier = lineThicknessMeters;
                checkMark.positionCount = 3;
                checkMark.SetPositions(new[]
                {
                    new Vector3(-s * 0.35f, 0f, 0f),
                    new Vector3(-s * 0.05f, 0f, -s * 0.25f),
                    new Vector3(s * 0.4f, 0f, s * 0.35f)
                });
            }

            if (rippleRing != null)
            {
                const int segments = 28;
                float r = s * 0.7f;
                rippleRing.useWorldSpace = false;
                rippleRing.loop = true;
                rippleRing.widthMultiplier = lineThicknessMeters * 0.6f;
                rippleRing.positionCount = segments;
                for (int i = 0; i < segments; i++)
                {
                    float a = (i / (float)segments) * Mathf.PI * 2f;
                    rippleRing.SetPosition(i, new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r));
                }
            }

            if (batteryIcon != null)
            {
                float w = batteryIconSizeMeters;
                float h = batteryIconSizeMeters * batteryIconAspectRatio;
                Vector3 current = batteryIcon.localScale;
                batteryIcon.localScale = new Vector3(w, h, current.z);
            }

            ApplyAppearance();
        }
    }
}
