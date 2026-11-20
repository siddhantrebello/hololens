using UnityEngine;
using TMPro;

namespace Norscope.ArGuidance
{
    public sealed class ArLiftEdgeVisual : ArHologramVisualBase
    {
        [Header("Physical size (metres)")]
        [SerializeField] private float edgeLengthMeters = 0.080f;
        [SerializeField] private float lineThicknessMeters = 0.002f;
        [SerializeField] private float chevronHeightMeters = 0.012f;
        [SerializeField] private float chevronTravelMeters = 0.020f;

        [SerializeField] private LineRenderer edgeLine;
        [SerializeField] private Transform[] chevrons;
        [SerializeField] private TMP_Text label;

        private Vector3[] _chevronBases;

        protected override void Awake()
        {
            base.Awake();
            if (string.IsNullOrEmpty(LabelText))
                ConfigureIdentity(ComponentId, ArVisualType.LiftEdge);
            RebuildGeometry();
            CacheChevrons();
            if (label != null)
            {
                label.text = string.IsNullOrEmpty(LabelText) ? "Lift here" : LabelText;
                label.raycastTarget = false;
                label.transform.localPosition = LabelOffset;
            }
        }

        private void Update()
        {
            if (!AnimationEnabled || chevrons == null || _chevronBases == null)
                return;
            float t = (Time.unscaledTime * 0.6f * AnimationSpeed) % 1f;
            for (int i = 0; i < chevrons.Length; i++)
            {
                if (chevrons[i] == null)
                    continue;
                float phase = (t + i * 0.25f) % 1f;
                chevrons[i].localPosition =
                    _chevronBases[i] + Vector3.up * (phase * chevronTravelMeters);
            }
        }

        protected override void ApplyAppearance()
        {
            ApplyLineColor(edgeLine, CurrentColor);
            if (label != null)
                label.color = CurrentColor;
        }

        public void RebuildGeometry()
        {
            if (edgeLine != null)
            {
                float half = edgeLengthMeters * 0.5f;
                edgeLine.useWorldSpace = false;
                edgeLine.widthMultiplier = lineThicknessMeters;
                edgeLine.positionCount = 2;
                edgeLine.SetPosition(0, new Vector3(-half, 0f, 0f));
                edgeLine.SetPosition(1, new Vector3(half, 0f, 0f));
            }

            ApplyAppearance();
        }

        private void CacheChevrons()
        {
            if (chevrons == null)
                return;
            _chevronBases = new Vector3[chevrons.Length];
            for (int i = 0; i < chevrons.Length; i++)
            {
                if (chevrons[i] != null)
                    _chevronBases[i] = chevrons[i].localPosition;
            }
        }
    }

    public sealed class ArConnectorWarningVisual : ArHologramVisualBase
    {
        [Header("Physical size (metres)")]
        [SerializeField] private Vector2 targetZoneMeters = new Vector2(0.025f, 0.010f);
        [SerializeField] private float leaderLineLengthMeters = 0.040f;

        [SerializeField] private LineRenderer zone;
        [SerializeField] private LineRenderer leader;
        [SerializeField] private TMP_Text label;
        [SerializeField] private Transform warningIcon;

        protected override void Awake()
        {
            base.Awake();
            SetVisualState(ArHologramVisualState.Warning);
            RebuildGeometry();
            if (label != null)
            {
                label.text = string.IsNullOrEmpty(LabelText)
                    ? "Disconnect battery first"
                    : LabelText;
                label.raycastTarget = false;
                label.transform.localPosition = LabelOffset;
            }
        }

        private void Update()
        {
            if (!AnimationEnabled || VisualState != ArHologramVisualState.Warning)
                return;
            float pulse = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 1.5f * AnimationSpeed);
            Color c = CurrentColor;
            c.a *= pulse;
            ApplyLineColor(zone, c);
            ApplyLineColor(leader, c);
        }

        protected override void ApplyAppearance()
        {
            ApplyLineColor(zone, CurrentColor);
            ApplyLineColor(leader, CurrentColor);
            if (label != null)
                label.color = CurrentColor;
        }

        public void RebuildGeometry()
        {
            float hw = targetZoneMeters.x * 0.5f;
            float hh = targetZoneMeters.y * 0.5f;
            if (zone != null)
            {
                zone.useWorldSpace = false;
                zone.loop = true;
                zone.widthMultiplier = 0.0015f;
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
                leader.widthMultiplier = 0.0015f;
                leader.positionCount = 2;
                leader.SetPosition(0, Vector3.zero);
                leader.SetPosition(1, LeaderLineOffset.normalized * leaderLineLengthMeters);
            }

            ApplyAppearance();
        }
    }

    public sealed class ArFanHighlightVisual : ArHologramVisualBase
    {
        [Header("Physical size (metres)")]
        [SerializeField] private float outerRingDiameterMeters = 0.072f;
        [SerializeField] private float ringThicknessMeters = 0.002f;
        [SerializeField] private float leaderLineLengthMeters = 0.050f;

        [SerializeField] private LineRenderer ring;
        [SerializeField] private LineRenderer leader;
        [SerializeField] private TMP_Text label;

        protected override void Awake()
        {
            base.Awake();
            RebuildGeometry();
            if (label != null)
            {
                label.text = string.IsNullOrEmpty(LabelText) ? "Cooling fan" : LabelText;
                label.raycastTarget = false;
                label.transform.localPosition = LabelOffset;
            }
        }

        protected override void ApplyAppearance()
        {
            ApplyLineColor(ring, CurrentColor);
            ApplyLineColor(leader, CurrentColor);
            if (label != null)
                label.color = CurrentColor;
        }

        public void RebuildGeometry()
        {
            if (ring != null)
            {
                float r = outerRingDiameterMeters * 0.5f;
                const int segments = 32;
                ring.useWorldSpace = false;
                ring.loop = true;
                ring.widthMultiplier = ringThicknessMeters;
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
                leader.widthMultiplier = 0.0015f;
                leader.positionCount = 2;
                leader.SetPosition(0, new Vector3(0f, 0f, outerRingDiameterMeters * 0.5f));
                leader.SetPosition(1, new Vector3(0f, leaderLineLengthMeters, outerRingDiameterMeters * 0.5f));
            }

            ApplyAppearance();
        }
    }

    public sealed class ArAirflowVisual : ArHologramVisualBase
    {
        [Header("Physical size (metres)")]
        [SerializeField] private float pathLengthMeters = 0.120f;
        [SerializeField] private float pathWidthMeters = 0.020f;
        [SerializeField] private Transform[] chevrons;

        private Vector3[] _bases;

        protected override void Awake()
        {
            base.Awake();
            if (chevrons == null || chevrons.Length == 0)
                chevrons = GetComponentsInChildren<Transform>();
            Cache();
        }

        private void Update()
        {
            if (!AnimationEnabled || chevrons == null || _bases == null)
                return;
            float t = (Time.unscaledTime * 0.45f * AnimationSpeed) % 1f;
            for (int i = 0; i < chevrons.Length; i++)
            {
                if (chevrons[i] == null || chevrons[i] == transform)
                    continue;
                float phase = (t + i * 0.2f) % 1f;
                chevrons[i].localPosition =
                    _bases[i] + Vector3.forward * (phase * pathLengthMeters * 0.25f);
            }
        }

        private void Cache()
        {
            if (chevrons == null)
                return;
            _bases = new Vector3[chevrons.Length];
            for (int i = 0; i < chevrons.Length; i++)
            {
                if (chevrons[i] != null)
                    _bases[i] = chevrons[i].localPosition;
            }
        }
    }
}
