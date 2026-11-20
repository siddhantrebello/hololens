using UnityEngine;

namespace Norscope.ArGuidance
{
    /// <summary>
    /// Stable calibration component IDs and dropdown display names.
    /// Screw groups remain for hierarchy/task visibility; leaves are calibrated.
    /// </summary>
    public static class ArCalibrationIds
    {
        public const string H04Group = "h04_bottom_cover_screws";
        public const string H19Group = "h19_battery_retaining_screws";

        public const int H04ScrewCount = 9;
        public const int H19ScrewCount = 4;

        /// <summary>Leaf-only calibration order (parents omitted).</summary>
        public static readonly string[] LeafCalibrationOrder =
        {
            "h02_disconnect_ac",
            "h03_back_panel_outline",
            "h04_screw_01", "h04_screw_02", "h04_screw_03",
            "h04_screw_04", "h04_screw_05", "h04_screw_06",
            "h04_screw_07", "h04_screw_08", "h04_screw_09",
            "h05_lift_here",
            "h07_replace_back_cover",
            "h08_power_on",
            "h17_battery_outline",
            "h18_battery_connector",
            "h19_screw_01", "h19_screw_02", "h19_screw_03", "h19_screw_04",
            "h20_swollen_battery_warning",
            "h21_install_new_battery",
            "h22_battery_verification"
        };

        public static bool IsScrewGroupParent(string componentId)
        {
            return componentId == H04Group ||
                   componentId == H19Group ||
                   componentId == "bottom_cover_screw_group";
        }

        /// <summary>
        /// Leaf IDs available for calibration against a hologram library.
        /// Prefer this over spawned-instance membership so the dropdown stays complete
        /// even when a leaf has not been instantiated yet (lazy resolve on select).
        /// </summary>
        public static string[] BuildLibraryLeafOrder(ArHologramLibrary library)
        {
            if (library?.entries == null || library.entries.Length == 0)
                return (string[])LeafCalibrationOrder.Clone();

            var ids = new System.Collections.Generic.List<string>(LeafCalibrationOrder.Length);
            for (int i = 0; i < LeafCalibrationOrder.Length; i++)
            {
                string id = LeafCalibrationOrder[i];
                if (IsCalibratableInLibrary(id, library))
                    ids.Add(id);
            }

            return ids.Count > 0 ? ids.ToArray() : (string[])LeafCalibrationOrder.Clone();
        }

        public static bool IsCalibratableInLibrary(string componentId, ArHologramLibrary library)
        {
            if (string.IsNullOrEmpty(componentId) || library == null)
                return false;

            return library.Find(componentId)?.prefab != null;
        }

        public static string ScrewComponentId(string groupId, int index1Based)
        {
            if (groupId == H04Group)
                return "h04_screw_" + index1Based.ToString("00");
            if (groupId == H19Group)
                return "h19_screw_" + index1Based.ToString("00");
            return groupId + "_" + index1Based.ToString("00");
        }

        /// <summary>Extracts the 1-based screw number from an "h04_screw_NN"/"h19_screw_NN" id,
        /// or 0 if componentId isn't a screw leaf id. Used to label an independently-resolved
        /// screw marker instance with its correct number (H04 screws each have their own
        /// top-level hologram library entry now, so nothing iterates siblings to number them).</summary>
        public static int ParseScrewNumber(string componentId)
        {
            if (string.IsNullOrEmpty(componentId))
                return 0;
            if (componentId.StartsWith("h04_screw_") &&
                int.TryParse(componentId.Substring("h04_screw_".Length), out int h04n))
                return h04n;
            if (componentId.StartsWith("h19_screw_") &&
                int.TryParse(componentId.Substring("h19_screw_".Length), out int h19n))
                return h19n;
            return 0;
        }

        public static string DisplayName(string componentId)
        {
            if (string.IsNullOrEmpty(componentId))
                return componentId;

            if (componentId.StartsWith("h04_screw_"))
            {
                if (int.TryParse(componentId.Substring("h04_screw_".Length), out int n))
                    return "H04-" + n.ToString("00");
            }

            if (componentId.StartsWith("h19_screw_"))
            {
                if (int.TryParse(componentId.Substring("h19_screw_".Length), out int n))
                    return "H19-" + n.ToString("00");
            }

            switch (componentId)
            {
                case "h02_disconnect_ac": return "H02 Disconnect AC";
                case "h03_back_panel_outline": return "H03 Back Panel Outline";
                case "h05_lift_here": return "H05 Lift Here";
                case "h07_replace_back_cover": return "H07 Replace Back Cover";
                case "h08_power_on": return "H08 Power On";
                case "h17_battery_outline": return "H17 Battery Outline";
                case "h18_battery_connector": return "H18 Battery Connector";
                case "h20_swollen_battery_warning": return "H20 Swollen Warning";
                case "h21_install_new_battery": return "H21 Install New Battery";
                case "h22_battery_verification": return "H22 Battery Verification";
                default: return componentId;
            }
        }

        /// <summary>
        /// Measured QR-local defaults for H04 (Dell Precision 3570), mm→m.
        /// Authored library defaults only — saved calibration still wins at runtime.
        /// </summary>
        public static Vector3[] DefaultH04ScrewLocals()
        {
            return new[]
            {
                new Vector3(-0.132f, 0.288f, -0.002f),   // 1
                new Vector3(-0.132f, 0.116f, -0.002f),   // 2
                new Vector3(-0.132f, -0.039f, -0.002f),  // 3
                new Vector3(0.012f, 0.294f, 0.004f),    // 4
                new Vector3(-0.002f, 0.186f, 0.004f),   // 5
                new Vector3(-0.002f, 0.052f, 0.004f),   // 6
                new Vector3(0.0069f, -0.0513f, -0.0011f), // 7
                new Vector3(0.092f, 0.288f, 0.004f),    // 8
                new Vector3(0.092f, -0.042f, 0.004f),   // 9
            };
        }

        /// <summary>Measured QR-local Euler defaults (degrees) for H04 screws 1–9.</summary>
        public static Vector3[] DefaultH04ScrewEulers()
        {
            return new[]
            {
                new Vector3(9f, -90f, -90f),   // 1
                new Vector3(-9f, -90f, -90f),  // 2
                new Vector3(-9f, -90f, -90f),  // 3
                new Vector3(-9f, -90f, -90f),  // 4
                new Vector3(-9f, -90f, -90f),  // 5
                new Vector3(-9f, -90f, -90f),  // 6
                new Vector3(-9f, -90f, -90f),  // 7
                new Vector3(-9f, -90f, -90f),  // 8
                new Vector3(-9f, -90f, -90f),  // 9
            };
        }

        /// <summary>
        /// Editable default locals for H19 (former group offset folded into children).
        /// </summary>
        public static Vector3[] DefaultH19ScrewLocals()
        {
            var groupBias = new Vector3(0.02f, 0.01f, 0.03f);
            return new[]
            {
                groupBias + new Vector3(-0.04f, 0f, -0.025f),
                groupBias + new Vector3(0.04f, 0f, -0.025f),
                groupBias + new Vector3(-0.04f, 0f, 0.025f),
                groupBias + new Vector3(0.04f, 0f, 0.025f)
            };
        }
    }
}
