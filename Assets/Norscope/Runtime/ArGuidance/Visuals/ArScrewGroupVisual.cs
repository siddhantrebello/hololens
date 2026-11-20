using UnityEngine;

namespace Norscope.ArGuidance
{
    /// <summary>
    /// Parent for a set of screw markers. Must live in its own script file — Unity cannot
    /// reliably serialize a second MonoBehaviour class that shares a .cs with
    /// <see cref="ArScrewMarkerVisual"/> (H19 prefab previously showed Missing Script).
    /// </summary>
    public sealed class ArScrewGroupVisual : ArHologramVisualBase
    {
        [SerializeField] private ArScrewMarkerVisual[] markers;

        public ArScrewMarkerVisual[] Markers => markers;

        protected override void Awake()
        {
            base.Awake();
            EnsureMarkers();
        }

        public override void SetVisible(bool visible)
        {
            base.SetVisible(visible);
            EnsureMarkers();
            // Markers self-hide in Awake (startVisible=false). Re-enable with the group
            // or they stay inactive under an active parent and the group looks empty.
            for (int i = 0; i < markers.Length; i++)
            {
                if (markers[i] != null)
                    markers[i].SetVisible(visible);
            }
        }

        public override void SetOpacity(float value)
        {
            base.SetOpacity(value);
            EnsureMarkers();
            for (int i = 0; i < markers.Length; i++)
            {
                if (markers[i] != null)
                    markers[i].SetOpacity(value);
            }
        }

        public override void SetVisualState(ArHologramVisualState state)
        {
            base.SetVisualState(state);
            EnsureMarkers();
            for (int i = 0; i < markers.Length; i++)
            {
                if (markers[i] != null)
                    markers[i].SetVisualState(state);
            }
        }

        private void EnsureMarkers()
        {
            if (markers == null || markers.Length == 0)
                markers = GetComponentsInChildren<ArScrewMarkerVisual>(true);
            if (markers == null)
                markers = System.Array.Empty<ArScrewMarkerVisual>();
        }
    }
}
