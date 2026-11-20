using UnityEngine;
using TMPro;

namespace Norscope.ArGuidance
{
    public enum ArIconCueKind
    {
        Power = 0,
        Plug = 1
    }

    /// <summary>Reusable floating icon cue (power / plug) with optional ring, leader, breathing.</summary>
    public sealed class ArIconCueVisual : ArHologramVisualBase
    {
        [Header("Icon cue (metres)")]
        [SerializeField] private ArIconCueKind iconKind = ArIconCueKind.Power;
        [SerializeField] private float ringDiameterMeters = 0.042f;
        [SerializeField] private float leaderLineLengthMeters = 0.060f;
        [SerializeField] private float lineThicknessMeters = 0.002f;
        [SerializeField] private bool breathScale = true;
        [SerializeField] private float breathAmount = 0.03f;
        [SerializeField] private bool slowFloat;
        [SerializeField] private float floatAmplitudeMeters = 0.008f;

        [SerializeField] private LineRenderer iconLines;
        [SerializeField] private LineRenderer ring;
        [SerializeField] private LineRenderer leader;
        [SerializeField] private TMP_Text label;

        private Vector3 _baseScale;
        private Vector3 _basePos;
        private bool _cached;

        protected override void Awake()
        {
            base.Awake();
            CacheBases(force: true);
            RebuildGeometry();
            ApplyLabel();
        }

        private void OnEnable() => CacheBases(force: true);

        public override void NotifyPoseApplied() => CacheBases(force: true);

        private void CacheBases(bool force)
        {
            if (_cached && !force)
                return;
            _baseScale = transform.localScale;
            _basePos = transform.localPosition;
            _cached = true;
        }

        private void Update()
        {
            if (!_cached)
                CacheBases(force: true);

            if (!AnimationEnabled)
            {
                // Pose is owned by library/calibration — never snap position back to a
                // stale cache (Awake often runs before ApplyPoseFor).
                if (breathScale)
                    transform.localScale = _baseScale;
                return;
            }

            if (breathScale)
            {
                float s = 1f + breathAmount * Mathf.Sin(Time.unscaledTime * 1.1f * AnimationSpeed);
                transform.localScale = _baseScale * s;
            }

            if (slowFloat)
            {
                float y = floatAmplitudeMeters *
                          Mathf.Sin(Time.unscaledTime * 0.9f * AnimationSpeed);
                transform.localPosition = _basePos + Vector3.up * y;
            }
        }

        protected override void ApplyAppearance()
        {
            base.ApplyAppearance();
            ApplyLineColor(iconLines, CurrentColor);
            ApplyLineColor(ring, CurrentColor);
            SetLeaderActive(leader, LeaderLineEnabled);
            if (LeaderLineEnabled)
                ApplyLineColor(leader, CurrentColor);
            if (label != null)
                label.color = CurrentColor;
            // Prefer sprite icon when assigned.
            if (Icon != null && iconLines != null)
                iconLines.enabled = false;
            else if (iconLines != null)
                iconLines.enabled = true;
        }

        protected override void OnLabelChanged() => ApplyLabel();

        private void ApplyLabel()
        {
            if (label == null)
                return;
            label.text = LabelText ?? string.Empty;
            label.raycastTarget = false;
            label.transform.localPosition = LabelOffset;
        }

        public void RebuildGeometry()
        {
            float s = IconSizeMeters;
            if (iconLines != null)
            {
                iconLines.useWorldSpace = false;
                iconLines.widthMultiplier = lineThicknessMeters;
                if (iconKind == ArIconCueKind.Power)
                {
                    // Circle + stem
                    const int segs = 20;
                    iconLines.loop = false;
                    iconLines.positionCount = segs + 2;
                    float r = s * 0.42f;
                    for (int i = 0; i < segs; i++)
                    {
                        // leave a gap at top for stem
                        float t = 0.15f + (i / (float)(segs - 1)) * 0.7f;
                        float a = t * Mathf.PI * 2f - Mathf.PI * 0.5f;
                        iconLines.SetPosition(i, new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r));
                    }

                    iconLines.SetPosition(segs, new Vector3(0f, 0f, r * 0.2f));
                    iconLines.SetPosition(segs + 1, new Vector3(0f, 0f, r * 1.05f));
                }
                else
                {
                    // Simple plug silhouette (rectangle + prongs)
                    iconLines.loop = false;
                    iconLines.positionCount = 8;
                    float hw = s * 0.28f;
                    float hd = s * 0.18f;
                    iconLines.SetPosition(0, new Vector3(-hw, 0f, -hd));
                    iconLines.SetPosition(1, new Vector3(hw, 0f, -hd));
                    iconLines.SetPosition(2, new Vector3(hw, 0f, hd));
                    iconLines.SetPosition(3, new Vector3(-hw, 0f, hd));
                    iconLines.SetPosition(4, new Vector3(-hw, 0f, -hd));
                    iconLines.SetPosition(5, new Vector3(-hw * 0.35f, 0f, -hd));
                    iconLines.SetPosition(6, new Vector3(-hw * 0.35f, 0f, -hd - s * 0.25f));
                    iconLines.SetPosition(7, new Vector3(hw * 0.35f, 0f, -hd - s * 0.25f));
                }
            }

            if (ring != null)
            {
                float r = ringDiameterMeters * 0.5f;
                const int segments = 28;
                ring.useWorldSpace = false;
                ring.loop = true;
                ring.widthMultiplier = lineThicknessMeters * 0.75f;
                ring.positionCount = segments;
                for (int i = 0; i < segments; i++)
                {
                    float a = (i / (float)segments) * Mathf.PI * 2f;
                    ring.SetPosition(i, new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r));
                }
            }

            if (leader != null)
            {
                leader.useWorldSpace = false;
                leader.widthMultiplier = lineThicknessMeters * 0.75f;
                leader.positionCount = 2;
                leader.SetPosition(0, Vector3.zero);
                Vector3 dir = LeaderLineOffset.sqrMagnitude > 0.0001f
                    ? LeaderLineOffset.normalized
                    : Vector3.down;
                leader.SetPosition(1, dir * leaderLineLengthMeters);
            }

            ApplyAppearance();
        }
    }
}
