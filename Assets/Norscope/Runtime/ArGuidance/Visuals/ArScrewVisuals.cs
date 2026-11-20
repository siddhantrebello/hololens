using UnityEngine;
using TMPro;

namespace Norscope.ArGuidance
{
    public sealed class ArScrewMarkerVisual : ArHologramVisualBase
    {
        [Header("Physical size (metres)")]
        [SerializeField] private float ringOuterDiameterMeters = 0.012f;
        [SerializeField] private float lineThicknessMeters = 0.0015f;
        [SerializeField] private float numberHeightMeters = 0.015f;
        [SerializeField] private int screwNumber = 1;

        [SerializeField] private LineRenderer ring;
        [SerializeField] private LineRenderer leader;
        [SerializeField] private TMP_Text numberLabel;
        [SerializeField] private float pulseSpeed = 2f;
        [SerializeField] private float leaderLineLengthMeters = 0.015f;

        private Vector3 _baseScale;

        protected override void Awake()
        {
            base.Awake();
            _baseScale = transform.localScale;
            RebuildGeometry();
            if (numberLabel != null)
            {
                numberLabel.text = screwNumber.ToString();
                numberLabel.raycastTarget = false;
            }
        }

        private void Update()
        {
            if (!AnimationEnabled || VisualState != ArHologramVisualState.Selected)
            {
                transform.localScale = _baseScale;
                return;
            }

            float s = 1f + 0.06f * Mathf.Sin(Time.unscaledTime * pulseSpeed * AnimationSpeed);
            transform.localScale = _baseScale * s;
        }

        protected override void ApplyAppearance()
        {
            ApplyLineColor(ring, CurrentColor);
            SetLeaderActive(leader, LeaderLineEnabled);
            if (LeaderLineEnabled)
                ApplyLineColor(leader, CurrentColor);
            if (numberLabel != null)
                numberLabel.color = CurrentColor;
        }

        public void RebuildGeometry()
        {
            if (ring == null)
                return;
            float r = ringOuterDiameterMeters * 0.5f;
            const int segments = 24;
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.widthMultiplier = lineThicknessMeters;
            ring.positionCount = segments;
            for (int i = 0; i < segments; i++)
            {
                float a = (i / (float)segments) * Mathf.PI * 2f;
                ring.SetPosition(i, new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r));
            }

            if (leader != null)
            {
                leader.useWorldSpace = false;
                leader.widthMultiplier = lineThicknessMeters * 0.75f;
                leader.positionCount = 2;
                leader.SetPosition(0, new Vector3(0f, 0f, r));
                leader.SetPosition(1, new Vector3(0f, leaderLineLengthMeters, r));
            }

            if (numberLabel != null)
                numberLabel.transform.localPosition = new Vector3(0f, numberHeightMeters, 0f);
            ApplyAppearance();
        }

        public void SetScrewNumber(int number)
        {
            screwNumber = number;
            if (numberLabel != null)
                numberLabel.text = screwNumber.ToString();
        }
    }
}
