using UnityEngine;
using TMPro;

namespace Norscope.ArGuidance
{
    /// <summary>Reusable connector target rectangle + optional leader + warning icon.</summary>
    public sealed class ArConnectorVisual : ArHologramVisualBase
    {
        [Header("Connector (metres)")]
        [SerializeField] private Vector2 targetZoneMeters = new Vector2(0.025f, 0.010f);
        [SerializeField] private float leaderLineLengthMeters = 0.040f;
        [SerializeField] private float lineThicknessMeters = 0.0015f;
        [SerializeField] private bool pulseWhenWarning = true;
        [Tooltip("Longest edge of the warning icon quad, in metres. warningIcon is a square " +
                 "Unity Quad primitive (1x1 native), so this is applied uniformly to X/Y — " +
                 "never to this hologram's root transform.")]
        [SerializeField, Range(0.010f, 0.050f)] private float warningIconSizeMeters = 0.022f;

        [SerializeField] private LineRenderer zone;
        [SerializeField] private LineRenderer leader;
        [SerializeField] private TMP_Text label;
        [SerializeField] private Transform warningIcon;

        protected override void Awake()
        {
            base.Awake();
            if (VisualState == ArHologramVisualState.Default)
                SetVisualState(ArHologramVisualState.Warning);
            RebuildGeometry();
            ApplyLabel();
        }

        private void Update()
        {
            if (!AnimationEnabled || !pulseWhenWarning)
                return;
            if (VisualState != ArHologramVisualState.Warning &&
                VisualState != ArHologramVisualState.Danger)
                return;

            float pulse = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 1.5f * AnimationSpeed);
            Color c = CurrentColor;
            c.a *= pulse;
            ApplyLineColor(zone, c);
            if (LeaderLineEnabled)
                ApplyLineColor(leader, c);
        }

        protected override void ApplyAppearance()
        {
            ApplyLineColor(zone, CurrentColor);
            SetLeaderActive(leader, LeaderLineEnabled);
            if (LeaderLineEnabled)
                ApplyLineColor(leader, CurrentColor);
            if (label != null)
                label.color = CurrentColor;
        }

        private void ApplyLabel()
        {
            if (label == null)
                return;
            label.text = string.IsNullOrEmpty(LabelText)
                ? "Disconnect Battery First"
                : LabelText;
            label.raycastTarget = false;
            label.transform.localPosition = LabelOffset;
        }

        public void RebuildGeometry()
        {
            float hw = targetZoneMeters.x * 0.5f;
            float hh = targetZoneMeters.y * 0.5f;
            if (zone != null)
            {
                zone.useWorldSpace = false;
                zone.loop = true;
                zone.widthMultiplier = lineThicknessMeters;
                zone.positionCount = 4;
                zone.SetPositions(new[]
                {
                    new Vector3(-hw, 0f, -hh),
                    new Vector3(hw, 0f, -hh),
                    new Vector3(hw, 0f, hh),
                    new Vector3(-hw, 0f, hh)
                });
            }

            if (leader != null)
            {
                leader.useWorldSpace = false;
                leader.widthMultiplier = lineThicknessMeters;
                leader.positionCount = 2;
                leader.SetPosition(0, Vector3.zero);
                Vector3 dir = LeaderLineOffset.sqrMagnitude > 0.0001f
                    ? LeaderLineOffset.normalized
                    : Vector3.up;
                leader.SetPosition(1, dir * leaderLineLengthMeters);
            }

            if (warningIcon != null)
            {
                float s = warningIconSizeMeters;
                Vector3 current = warningIcon.localScale;
                warningIcon.localScale = new Vector3(s, s, current.z);
            }

            ApplyAppearance();
        }
    }
}
