using UnityEngine;
using TMPro;

namespace Norscope.ArGuidance
{
    public enum ArDirectionSense
    {
        Up = 0,
        Down = 1
    }

    /// <summary>Reusable directional guidance: edge line + animated chevrons + label.</summary>
    public sealed class ArDirectionVisual : ArHologramVisualBase
    {
        [Header("Direction (metres)")]
        [SerializeField] private float edgeLengthMeters = 0.080f;
        [SerializeField] private float lineThicknessMeters = 0.002f;
        [SerializeField] private float chevronSizeMeters = 0.012f;
        [SerializeField] private float chevronTravelMeters = 0.020f;
        [SerializeField] private ArDirectionSense direction = ArDirectionSense.Up;

        [SerializeField] private LineRenderer edgeLine;
        [SerializeField] private Transform[] chevrons;
        [SerializeField] private TMP_Text label;

        private Vector3[] _chevronBases;
        private MaterialPropertyBlock _mpb;

        protected override void Awake()
        {
            base.Awake();
            RebuildGeometry();
            CacheChevrons();
            ApplyLabel();
        }

        private void Update()
        {
            if (!AnimationEnabled || chevrons == null || _chevronBases == null)
                return;
            float t = (Time.unscaledTime * 0.6f * AnimationSpeed) % 1f;
            Vector3 axis = direction == ArDirectionSense.Up ? Vector3.up : Vector3.down;
            for (int i = 0; i < chevrons.Length; i++)
            {
                if (chevrons[i] == null)
                    continue;
                float phase = (t + i * 0.25f) % 1f;
                chevrons[i].localPosition =
                    _chevronBases[i] + axis * (phase * chevronTravelMeters);
            }
        }

        protected override void ApplyAppearance()
        {
            ApplyLineColor(edgeLine, CurrentColor);
            if (label != null)
                label.color = CurrentColor;
            if (chevrons == null)
                return;
            for (int i = 0; i < chevrons.Length; i++)
            {
                if (chevrons[i] == null)
                    continue;
                MeshRenderer mr = chevrons[i].GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    if (_mpb == null)
                        _mpb = new MaterialPropertyBlock();
                    mr.GetPropertyBlock(_mpb);
                    _mpb.SetColor("_Color", CurrentColor);
                    _mpb.SetColor("_BaseColor", CurrentColor);
                    mr.SetPropertyBlock(_mpb);
                }
            }
        }

        private void ApplyLabel()
        {
            if (label == null)
                return;
            label.text = string.IsNullOrEmpty(LabelText) ? "Lift Here" : LabelText;
            label.raycastTarget = false;
            label.transform.localPosition = LabelOffset;
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

            if (chevrons != null)
            {
                for (int i = 0; i < chevrons.Length; i++)
                {
                    if (chevrons[i] != null)
                        chevrons[i].localScale = Vector3.one * chevronSizeMeters;
                }
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
}
