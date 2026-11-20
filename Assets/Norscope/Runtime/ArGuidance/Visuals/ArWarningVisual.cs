using UnityEngine;
using TMPro;

namespace Norscope.ArGuidance
{
    /// <summary>Reusable danger / prerequisite warning triangle + label.</summary>
    public sealed class ArWarningVisual : ArHologramVisualBase
    {
        [Header("Warning (metres)")]
        [SerializeField] private float triangleSizeMeters = 0.045f;
        [SerializeField] private float lineThicknessMeters = 0.002f;
        [SerializeField] private bool slowPulse = true;
        [SerializeField] private float pulseAmount = 0.08f;

        [SerializeField] private LineRenderer triangle;
        [SerializeField] private TMP_Text label;

        private Vector3 _baseScale;

        protected override void Awake()
        {
            base.Awake();
            _baseScale = transform.localScale;
            SetVisualState(ArHologramVisualState.Danger);
            RebuildGeometry();
            ApplyLabel();
        }

        private void Update()
        {
            if (!AnimationEnabled || !slowPulse)
            {
                transform.localScale = _baseScale;
                return;
            }

            float s = 1f + pulseAmount * Mathf.Sin(Time.unscaledTime * 0.7f * AnimationSpeed);
            transform.localScale = _baseScale * s;
        }

        protected override void ApplyAppearance()
        {
            base.ApplyAppearance();
            ApplyLineColor(triangle, CurrentColor);
            if (label != null)
                label.color = CurrentColor;
        }

        protected override void OnLabelChanged() => ApplyLabel();

        private void ApplyLabel()
        {
            if (label == null)
                return;
            label.text = string.IsNullOrEmpty(LabelText)
                ? "Battery appears swollen.\nDo NOT puncture."
                : LabelText;
            label.raycastTarget = false;
            label.transform.localPosition = LabelOffset;
        }

        public void RebuildGeometry()
        {
            if (triangle == null)
                return;
            float s = triangleSizeMeters;
            triangle.useWorldSpace = false;
            triangle.loop = true;
            triangle.widthMultiplier = lineThicknessMeters;
            triangle.positionCount = 3;
            triangle.SetPositions(new[]
            {
                new Vector3(0f, 0f, s * 0.55f),
                new Vector3(-s * 0.5f, 0f, -s * 0.35f),
                new Vector3(s * 0.5f, 0f, -s * 0.35f)
            });
            ApplyAppearance();
        }
    }
}
