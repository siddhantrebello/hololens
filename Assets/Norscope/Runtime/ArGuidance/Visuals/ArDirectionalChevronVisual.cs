using UnityEngine;
using TMPro;

namespace Norscope.ArGuidance
{
    /// <summary>
    /// Lift-edge guidance: edge line + stacked directional chevrons with sequential fade travel.
    /// Animates chevron children only — never the hologram root.
    /// </summary>
    public sealed class ArDirectionalChevronVisual : ArHologramVisualBase
    {
        [Header("Edge (metres)")]
        [SerializeField] private float edgeLengthMeters = 0.075f;
        [SerializeField] private float lineThicknessMeters = 0.0018f;

        [Header("Chevrons (metres)")]
        [SerializeField] private float chevronWidthMeters = 0.018f;
        [SerializeField] private float chevronHeightMeters = 0.010f;
        [SerializeField] private float chevronTravelMeters = 0.015f;
        [SerializeField] private float loopSeconds = 1.0f;
        [SerializeField] private ArDirectionSense direction = ArDirectionSense.Up;

        [Header("Wiring")]
        [SerializeField] private LineRenderer edgeLine;
        [SerializeField] private LineRenderer[] chevronLines;
        [SerializeField] private Transform[] chevrons;
        [SerializeField] private TMP_Text label;
        [SerializeField] private SpriteRenderer fallbackIcon;

        private Vector3[] _chevronBases;
        private readonly Vector3[] _chevronPointScratch = new Vector3[3];

        protected override void Awake()
        {
            base.Awake();
            RebuildGeometry();
            CacheChevrons();
            ApplyLabel();
            SetFallbackIconActive(false);
        }

        private void Update()
        {
            if (!AnimationEnabled || chevrons == null || _chevronBases == null || chevronLines == null)
                return;

            float loop = Mathf.Max(0.05f, loopSeconds / Mathf.Max(0.01f, AnimationSpeed));
            float t = (Time.unscaledTime % loop) / loop;
            Vector3 axis = direction == ArDirectionSense.Up ? Vector3.up : Vector3.down;
            int count = Mathf.Min(chevrons.Length, chevronLines.Length);
            float stagger = count > 0 ? 1f / count : 1f;

            for (int i = 0; i < count; i++)
            {
                if (chevrons[i] == null)
                    continue;
                float phase = (t + i * stagger) % 1f;
                chevrons[i].localPosition = _chevronBases[i] + axis * (phase * chevronTravelMeters);

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
            ApplyLineColor(edgeLine, CurrentColor);
            if (label != null)
                label.color = CurrentColor;
            if (chevronLines == null)
                return;
            for (int i = 0; i < chevronLines.Length; i++)
                ApplyLineColor(chevronLines[i], CurrentColor);
        }

        protected override void OnLabelChanged() => ApplyLabel();

        private void ApplyLabel()
        {
            if (label == null)
                return;
            label.text = string.IsNullOrEmpty(LabelText) ? "LIFT HERE" : LabelText;
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
            if (edgeLine != null)
            {
                float half = edgeLengthMeters * 0.5f;
                edgeLine.useWorldSpace = false;
                edgeLine.loop = false;
                edgeLine.widthMultiplier = lineThicknessMeters;
                edgeLine.positionCount = 2;
                edgeLine.SetPosition(0, new Vector3(-half, 0f, 0f));
                edgeLine.SetPosition(1, new Vector3(half, 0f, 0f));
            }

            if (chevronLines != null)
            {
                float hw = chevronWidthMeters * 0.5f;
                float hh = chevronHeightMeters;
                bool up = direction == ArDirectionSense.Up;
                _chevronPointScratch[0] = new Vector3(-hw, 0f, 0f);
                _chevronPointScratch[1] = new Vector3(0f, up ? hh : -hh, 0f);
                _chevronPointScratch[2] = new Vector3(hw, 0f, 0f);

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
