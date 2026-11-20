using UnityEngine;

namespace Norscope.ArGuidance
{
    public sealed class ArLaptopOutlineVisual : ArHologramVisualBase
    {
        [Header("Physical size (metres)")]
        [SerializeField] private float widthMeters = 0.358f;
        [SerializeField] private float depthMeters = 0.236f;
        [SerializeField] private float lineThicknessMeters = 0.002f;
        [SerializeField, Range(0f, 1f)] private float fillOpacity = 0.08f;

        [SerializeField] private LineRenderer perimeter;
        [SerializeField] private LineRenderer[] cornerBrackets;
        [SerializeField] private MeshRenderer fillRenderer;

        protected override void Awake()
        {
            base.Awake();
            RebuildGeometry();
        }

        protected override void ApplyAppearance()
        {
            Color c = CurrentColor;
            ApplyLineColor(perimeter, c);
            if (cornerBrackets != null)
            {
                for (int i = 0; i < cornerBrackets.Length; i++)
                    ApplyLineColor(cornerBrackets[i], c);
            }

            if (fillRenderer != null && fillRenderer.sharedMaterial != null)
            {
                Color fill = c;
                fill.a = fillOpacity * c.a;
                fillRenderer.sharedMaterial.color = fill;
            }
        }

        public void RebuildGeometry()
        {
            float hw = widthMeters * 0.5f;
            float hd = depthMeters * 0.5f;
            if (perimeter != null)
            {
                perimeter.useWorldSpace = false;
                perimeter.loop = true;
                perimeter.widthMultiplier = lineThicknessMeters;
                perimeter.positionCount = 4;
                perimeter.SetPositions(new[]
                {
                    new Vector3(-hw, 0f, -hd),
                    new Vector3(hw, 0f, -hd),
                    new Vector3(hw, 0f, hd),
                    new Vector3(-hw, 0f, hd)
                });
            }

            ApplyAppearance();
        }
    }
}
