using UnityEngine;

namespace Norscope.ArGuidance
{
    [CreateAssetMenu(
        fileName = "ArGuidanceProfile",
        menuName = "Norscope/AR Guidance/Equipment Profile")]
    public sealed class ArGuidanceProfile : ScriptableObject
    {
        public string equipmentId = JsonArCalibrationRepository.DefaultEquipmentId;
        public string displayName = "Dell Precision 3570";
        public string markerPayload = JsonArCalibrationRepository.DefaultMarkerPayload;

        [Header("Hologram prefabs")]
        public GameObject laptopOutlinePrefab;
        public GameObject screwGroupPrefab;
        public GameObject liftEdgePrefab;
        public GameObject connectorWarningPrefab;
        public GameObject fanHighlightPrefab;
        public GameObject airflowPrefab;

        [Header("Phase 2 hologram library")]
        public ArHologramLibrary hologramLibrary;

        public GameObject GetPrefab(ArVisualType visualType)
        {
            switch (visualType)
            {
                case ArVisualType.LaptopOutline: return laptopOutlinePrefab;
                case ArVisualType.ScrewGroup: return screwGroupPrefab;
                case ArVisualType.LiftEdge: return liftEdgePrefab;
                case ArVisualType.ConnectorWarning: return connectorWarningPrefab;
                case ArVisualType.FanHighlight: return fanHighlightPrefab;
                case ArVisualType.Airflow: return airflowPrefab;
                default:
                    return GetLibraryPrefabByType(visualType);
            }
        }

        public GameObject GetLibraryPrefabByType(ArVisualType visualType)
        {
            if (hologramLibrary?.entries == null)
                return null;
            for (int i = 0; i < hologramLibrary.entries.Length; i++)
            {
                ArHologramLibraryEntry e = hologramLibrary.entries[i];
                if (e != null && e.visualType == visualType && e.prefab != null)
                    return e.prefab;
            }

            return null;
        }

        public static string[] Phase1ComponentIds => new[]
        {
            "laptop_outline",
            "bottom_cover_screw_group",
            "lift_edge",
            "battery_connector",
            "cooling_fan",
            "fan_airflow"
        };
    }
}
