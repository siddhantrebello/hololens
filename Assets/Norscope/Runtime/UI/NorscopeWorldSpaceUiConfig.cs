using UnityEngine;

namespace Norscope.UI
{
    /// <summary>
    /// Converts Canvas pixel sizes to world metres for HL2 world-space UI.
    /// Default: 1000 px = 1 m (Canvas localScale = 0.001).
    /// </summary>
    [CreateAssetMenu(fileName = "NorscopeWorldSpaceUiConfig", menuName = "Norscope/UI/World Space Config", order = 11)]
    public sealed class NorscopeWorldSpaceUiConfig : ScriptableObject
    {
        [Min(1f)]
        [Tooltip("How many Canvas pixels equal one metre in world space.")]
        public float pixelsPerMeter = 1000f;

        public float CanvasScale => 1f / Mathf.Max(1f, pixelsPerMeter);

        public Vector2 MetersToPixels(Vector2 meters)
        {
            return meters * pixelsPerMeter;
        }

        public Vector2 PixelsToMeters(Vector2 pixels)
        {
            return pixels / Mathf.Max(1f, pixelsPerMeter);
        }

        public void ApplyToCanvas(RectTransform canvasRoot)
        {
            if (canvasRoot == null)
            {
                return;
            }

            canvasRoot.localScale = Vector3.one * CanvasScale;
        }
    }
}
