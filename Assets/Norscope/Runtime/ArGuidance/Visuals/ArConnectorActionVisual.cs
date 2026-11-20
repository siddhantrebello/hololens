using UnityEngine;
using TMPro;

namespace Norscope.ArGuidance
{
    /// <summary>
    /// Connector highlight + pull-direction chevrons for disconnect actions.
    /// Animates bracket opacity and chevron children only — never the hologram root.
    /// </summary>
    public sealed class ArConnectorActionVisual : ArHologramVisualBase
    {
        [Header("Connector target (metres)")]
        [SerializeField] private Vector2 targetZoneMeters = new Vector2(0.025f, 0.010f);
        [SerializeField] private float lineThicknessMeters = 0.0018f;
        [SerializeField] private bool pulseBracket = true;
        [SerializeField, Range(0f, 0.25f)] private float bracketPulseAmount = 0.12f;

        [Header("Pull chevrons (metres)")]
        [SerializeField] private float arrowSizeMeters = 0.010f;
        [SerializeField] private float arrowTravelMeters = 0.028f;
        [SerializeField] private float loopSeconds = 1.0f;
        [SerializeField] private Vector3 unplugDirectionLocal = new Vector3(0f, 0f, 1f);
        [SerializeField] private Color accentColor = ArGuidancePalette.Warning;

        [Header("Labels")]
        [SerializeField] private string secondaryLabelText = "Battery connector";
        [SerializeField] private Vector3 secondaryLabelOffset = new Vector3(0f, 0.028f, 0f);

        [Header("Wiring")]
        [SerializeField] private LineRenderer connectorBracket;
        [SerializeField] private LineRenderer[] chevronLines;
        [SerializeField] private Transform[] chevrons;
        [SerializeField] private TMP_Text label;
        [SerializeField] private TMP_Text secondaryLabel;

        private Vector3[] _chevronBases;
        private readonly Vector3[] _bracketScratch = new Vector3[4];
        private readonly Vector3[] _chevronPointScratch = new Vector3[3];

        protected override void Awake()
        {
            base.Awake();
            RebuildGeometry();
            CacheChevrons();
            ApplyLabels();
        }

        private void Update()
        {
            if (!AnimationEnabled)
            {
                ApplyLineColor(connectorBracket, CurrentColor);
                return;
            }

            if (pulseBracket && connectorBracket != null)
            {
                float pulse = 1f - bracketPulseAmount *
                              (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 1.5f * AnimationSpeed));
                Color bracket = CurrentColor;
                bracket.a *= pulse;
                ApplyLineColor(connectorBracket, bracket);
            }

            if (chevrons == null || _chevronBases == null || chevronLines == null)
                return;

            Vector3 dir = unplugDirectionLocal.sqrMagnitude > 1e-6f
                ? unplugDirectionLocal.normalized
                : Vector3.forward;
            float loop = Mathf.Max(0.05f, loopSeconds / Mathf.Max(0.01f, AnimationSpeed));
            float t = (Time.unscaledTime % loop) / loop;
            int count = Mathf.Min(chevrons.Length, chevronLines.Length);
            float stagger = count > 0 ? 1f / count : 1f;

            for (int i = 0; i < count; i++)
            {
                if (chevrons[i] == null)
                    continue;
                float phase = (t + i * stagger) % 1f;
                chevrons[i].localPosition = _chevronBases[i] + dir * (phase * arrowTravelMeters);

                if (chevronLines[i] != null)
                {
                    Color c = CurrentColor;
                    c.a *= 1f - phase;
                    ApplyLineColor(chevronLines[i], c);
                }
            }
        }

        protected override void ApplyAppearance()
        {
            ApplyLineColor(connectorBracket, CurrentColor);
            if (label != null)
                label.color = CurrentColor;
            if (secondaryLabel != null)
            {
                Color accent = accentColor;
                accent.a = Opacity;
                secondaryLabel.color = accent;
            }

            if (chevronLines == null)
                return;
            for (int i = 0; i < chevronLines.Length; i++)
                ApplyLineColor(chevronLines[i], CurrentColor);
        }

        protected override void OnLabelChanged() => ApplyLabels();

        private void ApplyLabels()
        {
            if (label != null)
            {
                label.text = string.IsNullOrEmpty(LabelText) ? "DISCONNECT" : LabelText;
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

        public void RebuildGeometry()
        {
            float hw = targetZoneMeters.x * 0.5f;
            float hh = targetZoneMeters.y * 0.5f;
            if (connectorBracket != null)
            {
                connectorBracket.useWorldSpace = false;
                connectorBracket.loop = true;
                connectorBracket.widthMultiplier = lineThicknessMeters;
                connectorBracket.positionCount = 4;
                _bracketScratch[0] = new Vector3(-hw, 0f, -hh);
                _bracketScratch[1] = new Vector3(hw, 0f, -hh);
                _bracketScratch[2] = new Vector3(hw, 0f, hh);
                _bracketScratch[3] = new Vector3(-hw, 0f, hh);
                connectorBracket.SetPositions(_bracketScratch);
            }

            if (chevronLines != null)
            {
                Vector3 dir = unplugDirectionLocal.sqrMagnitude > 1e-6f
                    ? unplugDirectionLocal.normalized
                    : Vector3.forward;
                Vector3 side = Vector3.Cross(dir, Vector3.up);
                if (side.sqrMagnitude < 1e-6f)
                    side = Vector3.Cross(dir, Vector3.right);
                side.Normalize();

                float halfW = arrowSizeMeters * 0.5f;
                _chevronPointScratch[0] = -side * halfW;
                _chevronPointScratch[1] = dir * arrowSizeMeters;
                _chevronPointScratch[2] = side * halfW;

                for (int i = 0; i < chevronLines.Length; i++)
                {
                    LineRenderer lr = chevronLines[i];
                    if (lr == null)
                        continue;
                    lr.useWorldSpace = false;
                    lr.loop = false;
                    lr.widthMultiplier = lineThicknessMeters;
                    lr.positionCount = 3;
                    lr.SetPositions(_chevronPointScratch);
                }
            }

            ApplyAppearance();
        }

        private void CacheChevrons()
        {
            if (chevrons == null)
            {
                _chevronBases = null;
                return;
            }

            _chevronBases = new Vector3[chevrons.Length];
            for (int i = 0; i < chevrons.Length; i++)
            {
                if (chevrons[i] != null)
                    _chevronBases[i] = chevrons[i].localPosition;
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
