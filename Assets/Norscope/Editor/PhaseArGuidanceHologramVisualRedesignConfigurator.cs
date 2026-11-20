using System;
using System.IO;
using System.Text;
using Norscope.ArGuidance;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Norscope.Editor
{
    /// <summary>
    /// Focused visual redesign for H05 / H08 / H18 / H20 only.
    /// Capture → Dry Run → Apply → Validate → Restore.
    /// </summary>
    public static class PhaseArGuidanceHologramVisualRedesignConfigurator
    {
        private const string Menu = "Norscope/HoloLens/AR Guidance/Hologram Visual Redesign/";
        private const string BackupRoot = "Assets/Norscope/UI/Panels/_Backups";
        private const string BackupKey = "Norscope.ArGuidance.HologramVisualRedesign.Backup";
        private const string ApplyApprovedKey = "Norscope.ArGuidance.HologramVisualRedesign.ApplyApproved";
        private const string DryRunReportPath = "Temp/NorscopeHologramVisualRedesignDryRun.txt";
        private const string ApplyReportPath = "Temp/NorscopeHologramVisualRedesignApply.txt";
        private const string ValidateReportPath = "Temp/NorscopeHologramVisualRedesignValidate.txt";

        private const string PrefabFolder = "Assets/Norscope/UI/Panels/ArGuidance/Battery";
        private const string MatFolder = "Assets/Norscope/Materials/ArGuidance";
        private const string H05Path = PrefabFolder + "/H05_LiftHere.prefab";
        private const string H08Path = PrefabFolder + "/H08_PowerOn.prefab";
        private const string H18Path = PrefabFolder + "/H18_BatteryConnector.prefab";
        private const string H20Path = PrefabFolder + "/H20_SwollenBatteryWarning.prefab";
        private const string LibraryPath =
            "Assets/Norscope/Data/ArGuidance/ArHologramLibrary_ReplaceBattery.asset";
        private const string TaskMapPath =
            "Assets/Norscope/Data/ArGuidance/ArTaskGuidanceMap_ReplaceBattery.asset";

        private static readonly Vector3 H05Pos = new Vector3(-0.138f, 0.082f, -0.010f);
        private static readonly Vector3 H05Euler = new Vector3(-58f, 180f, -86.2f);
        private static readonly Vector3 H08Pos = new Vector3(0f, 0.08f, 0.14f);
        private static readonly Vector3 H08Euler = Vector3.zero;
        private static readonly Vector3 H18Pos = new Vector3(0f, 0.146f, 0.002f);
        private static readonly Vector3 H18Euler = new Vector3(0f, -90f, -90f);
        private static readonly Vector3 H20Pos = new Vector3(0.042f, 0.08f, 0f);
        private static readonly Vector3 H20Euler = new Vector3(0f, -90f, -90f);

        private static readonly string[] BaselineAssets =
        {
            H05Path, H08Path, H18Path, H20Path, LibraryPath, TaskMapPath,
            "Assets/Norscope/Runtime/ArGuidance/Visuals/ArDirectionalChevronVisual.cs",
            "Assets/Norscope/Runtime/ArGuidance/Visuals/ArTargetRingVisual.cs",
            "Assets/Norscope/Runtime/ArGuidance/Visuals/ArConnectorActionVisual.cs",
            "Assets/Norscope/Runtime/ArGuidance/Visuals/ArInspectionWarningVisual.cs",
            "Assets/Norscope/Editor/PhaseArGuidanceBatteryLibraryConfigurator.cs",
            "Assets/Norscope/Editor/PhaseArGuidanceBatteryTaskIntegrationConfigurator.cs"
        };

        [MenuItem(Menu + "1) Capture Baseline")]
        public static void CaptureBaselineMenu()
        {
            string folder = BackupRoot + "/HologramVisualRedesign_" +
                            DateTime.Now.ToString("yyyyMMdd_HHmmss");
            Directory.CreateDirectory(folder);
            var manifest = new StringBuilder();
            manifest.AppendLine("Hologram Visual Redesign baseline " + DateTime.Now.ToString("O"));
            for (int i = 0; i < BaselineAssets.Length; i++)
            {
                string path = BaselineAssets[i];
                if (!File.Exists(path))
                {
                    manifest.AppendLine("MISSING " + path);
                    continue;
                }

                string dest = BackupDestPath(folder, path);
                string destDir = Path.GetDirectoryName(dest);
                if (!string.IsNullOrEmpty(destDir))
                    Directory.CreateDirectory(destDir);
                FileUtil.CopyFileOrDirectory(path, dest);
                manifest.AppendLine("OK " + path);
            }

            File.WriteAllText(folder + "/manifest.txt", manifest.ToString(), Encoding.UTF8);
            EditorPrefs.SetString(BackupKey, folder);
            EditorPrefs.SetBool(ApplyApprovedKey, false);
            AssetDatabase.Refresh();
            Debug.Log("[Norscope Hologram Visual Redesign] Baseline CAPTURED -> " + folder);
        }

        [MenuItem(Menu + "2) Dry Run")]
        public static void DryRunMenu()
        {
            bool ready = BuildDryRun(out string report);
            Directory.CreateDirectory("Temp");
            File.WriteAllText(DryRunReportPath, report, Encoding.UTF8);
            Debug.Log((ready
                ? "[Norscope Hologram Visual Redesign] Dry Run READY\n"
                : "[Norscope Hologram Visual Redesign] Dry Run BLOCKED\n") + report);
        }

        [MenuItem(Menu + "3) Apply")]
        public static void ApplyMenu()
        {
            if (!EditorPrefs.GetBool(ApplyApprovedKey, false))
            {
                Debug.LogError("[Norscope Hologram Visual Redesign] Apply LOCKED — approve in chat first.");
                return;
            }

            try
            {
                string report = ExecuteApply();
                Directory.CreateDirectory("Temp");
                File.WriteAllText(ApplyReportPath, report, Encoding.UTF8);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[Norscope Hologram Visual Redesign] Apply COMPLETE\n" + report);
                ValidateMenu();
            }
            catch (Exception ex)
            {
                Debug.LogError("[Norscope Hologram Visual Redesign] Apply FAILED:\n" + ex);
                throw;
            }
        }

        [MenuItem(Menu + "4) Validate")]
        public static void ValidateMenu()
        {
            bool pass = BuildValidate(out string report);
            Directory.CreateDirectory("Temp");
            File.WriteAllText(ValidateReportPath, report, Encoding.UTF8);
            if (pass)
                Debug.Log("[Norscope Hologram Visual Redesign] Validate PASS\n" + report);
            else
                Debug.LogError("[Norscope Hologram Visual Redesign] Validate FAIL\n" + report);
        }

        [MenuItem(Menu + "5) Restore Baseline")]
        public static void RestoreBaselineMenu()
        {
            string folder = EditorPrefs.GetString(BackupKey, string.Empty);
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                Debug.LogError("[Norscope Hologram Visual Redesign] No baseline recorded.");
                return;
            }

            for (int i = 0; i < BaselineAssets.Length; i++)
            {
                string path = BaselineAssets[i];
                string dest = BackupDestPath(folder, path);
                if (File.Exists(dest) && File.Exists(path))
                    FileUtil.ReplaceFile(dest, path);
            }

            EditorPrefs.SetBool(ApplyApprovedKey, false);
            AssetDatabase.Refresh();
            Debug.Log("[Norscope Hologram Visual Redesign] Baseline restored from " + folder);
        }

        /// <summary>Chat unlock for Apply.</summary>
        public static void ApproveApplyFromChat()
        {
            EditorPrefs.SetBool(ApplyApprovedKey, true);
            Debug.Log("[Norscope Hologram Visual Redesign] ApplyApproved = true");
        }

        /// <summary>Batch: approve + capture if needed + apply + validate.</summary>
        public static void ApproveApplyValidateBatch()
        {
            if (string.IsNullOrEmpty(EditorPrefs.GetString(BackupKey, string.Empty)))
                CaptureBaselineMenu();
            ApproveApplyFromChat();
            ApplyMenu();
            if (!BuildValidate(out string report))
                throw new Exception("Hologram Visual Redesign Validate FAIL\n" + report);
            Debug.Log("[Norscope Hologram Visual Redesign] ApproveApplyValidateBatch PASS");
        }

        /// <summary>Rebuild H20 only (rounded perimeter) without touching library poses.</summary>
        public static void RebuildH20RoundedPerimeterBatch()
        {
            Material amber = LoadMat("ArHoloAmber", ArGuidancePalette.Warning);
            ArHologramLibrary library = AssetDatabase.LoadAssetAtPath<ArHologramLibrary>(LibraryPath);
            Vector3 pos = ReadPose(library, "h20_swollen_battery_warning", out Vector3 euler, H20Pos, H20Euler);
            GameObject h20 = RebuildH20SwollenWarning(amber);
            WriteEntryPrefabOnly(library, "h20_swollen_battery_warning", h20, pos, euler);
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var so = new SerializedObject(h20.GetComponent<ArInspectionWarningVisual>());
            float radius = so.FindProperty("cornerRadiusMeters").floatValue;
            int points = h20.transform.Find("VisualPulseRoot/Perimeter")
                .GetComponent<LineRenderer>().positionCount;
            Debug.Log(
                "[Norscope Hologram Visual Redesign] H20 rounded perimeter rebuilt. " +
                "cornerRadiusMeters=" + radius + " perimeterPoints=" + points +
                " pose preserved " + pos + " / " + euler);
        }

        private static string BackupDestPath(string folder, string assetPath)
        {
            string flattened = assetPath.Replace("Assets/Norscope/", "").Replace('/', '_');
            if (flattened.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                flattened += ".bak";
            return folder + "/" + flattened;
        }

        private static string ExecuteApply()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Hologram Visual Redesign Apply ===");
            sb.AppendLine(DateTime.Now.ToString("o"));

            ArHologramLibrary library = AssetDatabase.LoadAssetAtPath<ArHologramLibrary>(LibraryPath);
            if (library == null)
                throw new InvalidOperationException("Library missing: " + LibraryPath);

            // Snapshot library poses — Apply must not change them.
            Vector3 p05 = ReadPose(library, "h05_lift_here", out Vector3 e05, H05Pos, H05Euler);
            Vector3 p08 = ReadPose(library, "h08_power_on", out Vector3 e08, H08Pos, H08Euler);
            Vector3 p18 = ReadPose(library, "h18_battery_connector", out Vector3 e18, H18Pos, H18Euler);
            Vector3 p20 = ReadPose(library, "h20_swollen_battery_warning", out Vector3 e20, H20Pos, H20Euler);

            Material cyan = LoadMat("ArHoloCyan", ArGuidancePalette.Cyan);
            Material amber = LoadMat("ArHoloAmber", ArGuidancePalette.Warning);

            Sprite upIcon = FindExistingIcon(H05Path);
            Sprite powerIcon = FindExistingIcon(H08Path);
            Sprite warnIcon = FindExistingIcon(H20Path);

            GameObject h05 = BuildH05(H05Path, cyan, upIcon);
            GameObject h08 = BuildH08(H08Path, cyan, powerIcon);
            GameObject h18 = BuildH18(H18Path, cyan, amber);
            GameObject h20 = BuildH20(H20Path, amber, warnIcon);

            // Re-bind library prefab refs only — restore exact poses.
            WriteEntryPrefabOnly(library, "h05_lift_here", h05, p05, e05);
            WriteEntryPrefabOnly(library, "h08_power_on", h08, p08, e08);
            WriteEntryPrefabOnly(library, "h18_battery_connector", h18, p18, e18);
            WriteEntryPrefabOnly(library, "h20_swollen_battery_warning", h20, p20, e20);
            EditorUtility.SetDirty(library);

            sb.AppendLine("Rebuilt H05_LiftHere with ArDirectionalChevronVisual");
            sb.AppendLine("Rebuilt H08_PowerOn with ArTargetRingVisual");
            sb.AppendLine("Rebuilt H18_BatteryConnector with ArConnectorActionVisual");
            sb.AppendLine("Rebuilt H20_SwollenBatteryWarning with ArInspectionWarningVisual");
            sb.AppendLine("Library poses preserved for all four ids");
            sb.AppendLine("Task map untouched");
            return sb.ToString();
        }

        // ── Public builders used by Battery Library rebuild ───────────────────

        public static GameObject RebuildH05LiftHere(Material cyan, Sprite fallbackIcon = null)
        {
            return BuildH05(H05Path, cyan, fallbackIcon ?? FindExistingIcon(H05Path));
        }

        public static GameObject RebuildH08PowerOn(Material cyan, Sprite fallbackIcon = null)
        {
            return BuildH08(H08Path, cyan, fallbackIcon ?? FindExistingIcon(H08Path));
        }

        public static GameObject RebuildH18BatteryConnector(Material cyan, Material amber)
        {
            return BuildH18(H18Path, cyan, amber);
        }

        public static GameObject RebuildH20SwollenWarning(Material amber, Sprite fallbackIcon = null)
        {
            return BuildH20(H20Path, amber, fallbackIcon ?? FindExistingIcon(H20Path));
        }

        private static GameObject BuildH05(string path, Material mat, Sprite fallbackIcon)
        {
            var root = new GameObject("H05_LiftHere");
            var visual = root.AddComponent<ArDirectionalChevronVisual>();

            LineRenderer edge = CreateLine("Edge", root.transform, mat, false);
            var chevronLines = new LineRenderer[3];
            var chevrons = new Transform[3];
            for (int i = 0; i < 3; i++)
            {
                var go = new GameObject("Chevron" + i);
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = new Vector3(0f, 0.002f + i * 0.001f, 0f);
                chevronLines[i] = CreateLine("Line", go.transform, mat, false);
                chevrons[i] = go.transform;
            }

            TMP_Text label = CreateWorldLabel("Label", root.transform, "LIFT HERE", 0.032f, 0.09f);
            SpriteRenderer fallback = CreateFallbackIcon(root.transform, fallbackIcon);

            SerializedObject so = new SerializedObject(visual);
            so.FindProperty("componentId").stringValue = "h05_lift_here";
            so.FindProperty("visualType").intValue = (int)ArVisualType.Direction;
            so.FindProperty("edgeLine").objectReferenceValue = edge;
            so.FindProperty("label").objectReferenceValue = label;
            so.FindProperty("fallbackIcon").objectReferenceValue = fallback;
            // Keep base icon null so ApplyIcon does not re-enable the PNG fallback.
            so.FindProperty("icon").objectReferenceValue = null;
            so.FindProperty("iconRenderer").objectReferenceValue = null;
            SerializedProperty lines = so.FindProperty("chevronLines");
            SerializedProperty xforms = so.FindProperty("chevrons");
            lines.arraySize = 3;
            xforms.arraySize = 3;
            for (int i = 0; i < 3; i++)
            {
                lines.GetArrayElementAtIndex(i).objectReferenceValue = chevronLines[i];
                xforms.GetArrayElementAtIndex(i).objectReferenceValue = chevrons[i];
            }

            so.FindProperty("edgeLengthMeters").floatValue = 0.075f;
            so.FindProperty("lineThicknessMeters").floatValue = 0.0018f;
            so.FindProperty("chevronWidthMeters").floatValue = 0.018f;
            so.FindProperty("chevronHeightMeters").floatValue = 0.010f;
            so.FindProperty("chevronTravelMeters").floatValue = 0.015f;
            so.FindProperty("loopSeconds").floatValue = 1.0f;
            so.FindProperty("direction").intValue = (int)ArDirectionSense.Up;
            so.FindProperty("labelText").stringValue = "LIFT HERE";
            so.FindProperty("labelOffset").vector3Value = new Vector3(0f, 0.032f, 0f);
            so.FindProperty("animationEnabled").boolValue = true;
            so.FindProperty("animationSpeed").floatValue = 1f;
            so.FindProperty("defaultColor").colorValue = ArGuidancePalette.Cyan;
            so.FindProperty("color").colorValue = ArGuidancePalette.Cyan;
            so.FindProperty("opacity").floatValue = 0.85f;
            so.FindProperty("authoredVisualState").enumValueIndex = (int)ArHologramVisualState.Default;
            so.FindProperty("leaderLineEnabled").boolValue = false;
            so.FindProperty("startVisible").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            visual.RebuildGeometry();
            visual.SetFallbackIconActive(false);
            return SavePrefab(root, path);
        }

        private static GameObject BuildH08(string path, Material mat, Sprite fallbackIcon)
        {
            var root = new GameObject("H08_PowerOn");
            var visual = root.AddComponent<ArTargetRingVisual>();

            LineRenderer ring = CreateLine("TargetRing", root.transform, mat, true);
            var glyphGo = new GameObject("PowerGlyph");
            glyphGo.transform.SetParent(root.transform, false);
            glyphGo.transform.localPosition = new Vector3(0f, 0.028f, 0f);
            LineRenderer glyph = CreateLine("GlyphLine", glyphGo.transform, mat, false);
            TMP_Text label = CreateWorldLabel("Label", root.transform, "PRESS POWER", 0.040f, 0.09f);
            SpriteRenderer fallback = CreateFallbackIcon(root.transform, fallbackIcon);

            SerializedObject so = new SerializedObject(visual);
            so.FindProperty("componentId").stringValue = "h08_power_on";
            so.FindProperty("visualType").intValue = (int)ArVisualType.IconCue;
            so.FindProperty("ring").objectReferenceValue = ring;
            so.FindProperty("powerGlyph").objectReferenceValue = glyph;
            so.FindProperty("powerGlyphRoot").objectReferenceValue = glyphGo.transform;
            so.FindProperty("label").objectReferenceValue = label;
            so.FindProperty("fallbackIcon").objectReferenceValue = fallback;
            so.FindProperty("icon").objectReferenceValue = null;
            so.FindProperty("iconRenderer").objectReferenceValue = null;
            so.FindProperty("ringDiameterMeters").floatValue = 0.022f;
            so.FindProperty("ringThicknessMeters").floatValue = 0.0018f;
            so.FindProperty("powerGlyphSizeMeters").floatValue = 0.025f;
            so.FindProperty("pressTravelMeters").floatValue = 0.0065f;
            so.FindProperty("loopSeconds").floatValue = 1.75f;
            so.FindProperty("glyphRestLocalPosition").vector3Value = new Vector3(0f, 0.028f, 0f);
            so.FindProperty("pressAxisLocal").vector3Value = new Vector3(0f, -1f, 0f);
            so.FindProperty("labelText").stringValue = "PRESS POWER";
            so.FindProperty("labelOffset").vector3Value = new Vector3(0f, 0.040f, 0f);
            so.FindProperty("animationEnabled").boolValue = true;
            so.FindProperty("animationSpeed").floatValue = 1f;
            so.FindProperty("defaultColor").colorValue = ArGuidancePalette.Cyan;
            so.FindProperty("completedColor").colorValue = ArGuidancePalette.Completed;
            so.FindProperty("color").colorValue = ArGuidancePalette.Cyan;
            so.FindProperty("opacity").floatValue = 0.85f;
            so.FindProperty("authoredVisualState").enumValueIndex = (int)ArHologramVisualState.Default;
            so.FindProperty("leaderLineEnabled").boolValue = false;
            so.FindProperty("startVisible").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            visual.RebuildGeometry();
            visual.SetFallbackIconActive(false);
            return SavePrefab(root, path);
        }

        private static GameObject BuildH18(string path, Material cyanMat, Material amberMat)
        {
            var root = new GameObject("H18_BatteryConnector");
            var visual = root.AddComponent<ArConnectorActionVisual>();

            LineRenderer bracket = CreateLine("ConnectorBracket", root.transform, cyanMat, true);
            var chevronLines = new LineRenderer[3];
            var chevrons = new Transform[3];
            for (int i = 0; i < 3; i++)
            {
                var go = new GameObject("PullChevron" + i);
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = new Vector3(0f, 0f, 0.006f + i * 0.002f);
                chevronLines[i] = CreateLine("Line", go.transform, cyanMat, false);
                chevrons[i] = go.transform;
            }

            TMP_Text label = CreateWorldLabel("Label", root.transform, "DISCONNECT", 0.040f, 0.09f);
            TMP_Text secondary = CreateWorldLabel(
                "SecondaryLabel", root.transform, "Battery connector", 0.028f, 0.07f);
            secondary.color = ArGuidancePalette.Warning;

            SerializedObject so = new SerializedObject(visual);
            so.FindProperty("componentId").stringValue = "h18_battery_connector";
            so.FindProperty("visualType").intValue = (int)ArVisualType.Connector;
            so.FindProperty("connectorBracket").objectReferenceValue = bracket;
            so.FindProperty("label").objectReferenceValue = label;
            so.FindProperty("secondaryLabel").objectReferenceValue = secondary;
            SerializedProperty lines = so.FindProperty("chevronLines");
            SerializedProperty xforms = so.FindProperty("chevrons");
            lines.arraySize = 3;
            xforms.arraySize = 3;
            for (int i = 0; i < 3; i++)
            {
                lines.GetArrayElementAtIndex(i).objectReferenceValue = chevronLines[i];
                xforms.GetArrayElementAtIndex(i).objectReferenceValue = chevrons[i];
            }

            so.FindProperty("targetZoneMeters").vector2Value = new Vector2(0.025f, 0.010f);
            so.FindProperty("lineThicknessMeters").floatValue = 0.0018f;
            so.FindProperty("arrowSizeMeters").floatValue = 0.010f;
            so.FindProperty("arrowTravelMeters").floatValue = 0.028f;
            so.FindProperty("loopSeconds").floatValue = 1.0f;
            so.FindProperty("unplugDirectionLocal").vector3Value = new Vector3(0f, 0f, 1f);
            so.FindProperty("accentColor").colorValue = ArGuidancePalette.Warning;
            so.FindProperty("labelText").stringValue = "DISCONNECT";
            so.FindProperty("secondaryLabelText").stringValue = "Battery connector";
            so.FindProperty("labelOffset").vector3Value = new Vector3(0f, 0.040f, 0f);
            so.FindProperty("secondaryLabelOffset").vector3Value = new Vector3(0f, 0.028f, 0f);
            so.FindProperty("animationEnabled").boolValue = true;
            so.FindProperty("defaultColor").colorValue = ArGuidancePalette.Cyan;
            so.FindProperty("warningColor").colorValue = ArGuidancePalette.Warning;
            so.FindProperty("color").colorValue = ArGuidancePalette.Cyan;
            so.FindProperty("opacity").floatValue = 0.85f;
            so.FindProperty("authoredVisualState").enumValueIndex = (int)ArHologramVisualState.Default;
            so.FindProperty("leaderLineEnabled").boolValue = false;
            so.FindProperty("startVisible").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            visual.RebuildGeometry();
            if (amberMat != null)
                secondary.color = amberMat.color;
            return SavePrefab(root, path);
        }

        private static GameObject BuildH20(string path, Material amberMat, Sprite fallbackIcon)
        {
            var root = new GameObject("H20_SwollenBatteryWarning");
            var visual = root.AddComponent<ArInspectionWarningVisual>();

            var pulse = new GameObject("VisualPulseRoot");
            pulse.transform.SetParent(root.transform, false);
            LineRenderer peri = CreateLine("Perimeter", pulse.transform, amberMat, true);

            var triRoot = new GameObject("WarningTriangle");
            triRoot.transform.SetParent(root.transform, false);
            triRoot.transform.localPosition = new Vector3(0f, 0.055f, 0f);
            LineRenderer tri = CreateLine("TriangleLine", triRoot.transform, amberMat, true);

            TMP_Text label = CreateWorldLabel(
                "Label", root.transform, "CHECK FOR SWELLING", 0.055f, 0.085f);
            label.transform.localPosition = new Vector3(0f, 0.078f, 0f);
            TMP_Text secondary = CreateWorldLabel(
                "SecondaryLabel", root.transform, "Do not continue if damaged", 0.040f, 0.065f);
            SpriteRenderer fallback = CreateFallbackIcon(root.transform, fallbackIcon);

            SerializedObject so = new SerializedObject(visual);
            so.FindProperty("componentId").stringValue = "h20_swollen_battery_warning";
            so.FindProperty("visualType").intValue = (int)ArVisualType.Warning;
            so.FindProperty("visualPulseRoot").objectReferenceValue = pulse.transform;
            so.FindProperty("perimeter").objectReferenceValue = peri;
            so.FindProperty("warningTriangleRoot").objectReferenceValue = triRoot.transform;
            so.FindProperty("warningTriangle").objectReferenceValue = tri;
            so.FindProperty("label").objectReferenceValue = label;
            so.FindProperty("secondaryLabel").objectReferenceValue = secondary;
            so.FindProperty("fallbackIcon").objectReferenceValue = fallback;
            so.FindProperty("icon").objectReferenceValue = null;
            so.FindProperty("iconRenderer").objectReferenceValue = null;
            so.FindProperty("perimeterSizeMeters").vector2Value = new Vector2(0.200f, 0.080f);
            so.FindProperty("outlineThicknessMeters").floatValue = 0.0018f;
            so.FindProperty("cornerRadiusMeters").floatValue = 0.008f;
            so.FindProperty("pulseScaleMax").floatValue = 0.04f;
            so.FindProperty("warningIconSizeMeters").floatValue = 0.027f;
            so.FindProperty("triangleLocalPosition").vector3Value = new Vector3(0f, 0.055f, 0f);
            so.FindProperty("labelText").stringValue = "CHECK FOR SWELLING";
            so.FindProperty("secondaryLabelText").stringValue = "Do not continue if damaged";
            so.FindProperty("labelOffset").vector3Value = new Vector3(0f, 0.078f, 0f);
            so.FindProperty("secondaryLabelOffset").vector3Value = new Vector3(0f, 0.040f, 0f);
            so.FindProperty("animationEnabled").boolValue = true;
            so.FindProperty("defaultColor").colorValue = ArGuidancePalette.Warning;
            so.FindProperty("warningColor").colorValue = ArGuidancePalette.Warning;
            so.FindProperty("dangerColor").colorValue = ArGuidancePalette.Danger;
            so.FindProperty("color").colorValue = ArGuidancePalette.Warning;
            so.FindProperty("opacity").floatValue = 0.90f;
            so.FindProperty("authoredVisualState").enumValueIndex = (int)ArHologramVisualState.Warning;
            so.FindProperty("leaderLineEnabled").boolValue = false;
            so.FindProperty("startVisible").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            visual.RebuildGeometry();
            visual.SetFallbackIconActive(false);
            return SavePrefab(root, path);
        }

        private static bool BuildDryRun(out string report)
        {
            var sb = new StringBuilder();
            bool ready = true;
            void Check(bool ok, string label)
            {
                sb.AppendLine((ok ? "PASS  " : "FAIL  ") + label);
                if (!ok) ready = false;
            }

            ArHologramLibrary library = AssetDatabase.LoadAssetAtPath<ArHologramLibrary>(LibraryPath);
            sb.AppendLine("Hologram Visual Redesign Dry Run");
            sb.AppendLine(DateTime.Now.ToString("o"));
            Check(File.Exists(H05Path), "H05 prefab");
            Check(File.Exists(H08Path), "H08 prefab");
            Check(File.Exists(H18Path), "H18 prefab");
            Check(File.Exists(H20Path), "H20 prefab");
            Check(library != null, "Library present");
            Check(PoseMatches(library, "h05_lift_here", H05Pos, H05Euler), "H05 library pose locked");
            Check(PoseMatches(library, "h08_power_on", H08Pos, H08Euler), "H08 library pose locked");
            Check(PoseMatches(library, "h18_battery_connector", H18Pos, H18Euler), "H18 library pose locked");
            Check(PoseMatches(library, "h20_swollen_battery_warning", H20Pos, H20Euler), "H20 library pose locked");
            report = sb.ToString();
            return ready;
        }

        private static bool BuildValidate(out string report)
        {
            var sb = new StringBuilder();
            bool pass = true;
            void Check(bool ok, string label)
            {
                sb.AppendLine((ok ? "PASS  " : "FAIL  ") + label);
                if (!ok) pass = false;
            }

            sb.AppendLine("Hologram Visual Redesign Validate");
            sb.AppendLine(DateTime.Now.ToString("o"));

            ArHologramLibrary library = AssetDatabase.LoadAssetAtPath<ArHologramLibrary>(LibraryPath);
            GameObject h05 = AssetDatabase.LoadAssetAtPath<GameObject>(H05Path);
            GameObject h08 = AssetDatabase.LoadAssetAtPath<GameObject>(H08Path);
            GameObject h18 = AssetDatabase.LoadAssetAtPath<GameObject>(H18Path);
            GameObject h20 = AssetDatabase.LoadAssetAtPath<GameObject>(H20Path);

            Check(h05 != null && h05.GetComponent<ArDirectionalChevronVisual>() != null,
                "H05 has ArDirectionalChevronVisual");
            Check(h08 != null && h08.GetComponent<ArTargetRingVisual>() != null,
                "H08 has ArTargetRingVisual");
            Check(h18 != null && h18.GetComponent<ArConnectorActionVisual>() != null,
                "H18 has ArConnectorActionVisual");
            Check(h20 != null && h20.GetComponent<ArInspectionWarningVisual>() != null,
                "H20 has ArInspectionWarningVisual");

            Check(h05 != null && Approx(h05.transform.localPosition, Vector3.zero) &&
                  Approx(h05.transform.localEulerAngles, Vector3.zero),
                "H05 prefab root identity");
            Check(h08 != null && Approx(h08.transform.localPosition, Vector3.zero),
                "H08 prefab root identity");
            Check(h18 != null && Approx(h18.transform.localPosition, Vector3.zero),
                "H18 prefab root identity");
            Check(h20 != null && Approx(h20.transform.localPosition, Vector3.zero),
                "H20 prefab root identity");

            Check(PoseMatches(library, "h05_lift_here", H05Pos, H05Euler), "H05 library pose unchanged");
            Check(PoseMatches(library, "h08_power_on", H08Pos, H08Euler), "H08 library pose unchanged");
            Check(PoseMatches(library, "h18_battery_connector", H18Pos, H18Euler), "H18 library pose unchanged");
            Check(PoseMatches(library, "h20_swollen_battery_warning", H20Pos, H20Euler), "H20 library pose unchanged");

            Check(ComponentIdIs(h05, "h05_lift_here"), "H05 componentId intact");
            Check(ComponentIdIs(h08, "h08_power_on"), "H08 componentId intact");
            Check(ComponentIdIs(h18, "h18_battery_connector"), "H18 componentId intact");
            Check(ComponentIdIs(h20, "h20_swollen_battery_warning"), "H20 componentId intact");

            Check(NoEnabledColliders(h05), "H05 no enabled colliders");
            Check(NoEnabledColliders(h08), "H08 no enabled colliders");
            Check(NoEnabledColliders(h18), "H18 no enabled colliders");
            Check(NoEnabledColliders(h20), "H20 no enabled colliders");

            Check(LayerIsIgnoreRaycast(h05), "H05 Ignore Raycast");
            Check(LayerIsIgnoreRaycast(h08), "H08 Ignore Raycast");
            Check(LayerIsIgnoreRaycast(h18), "H18 Ignore Raycast");
            Check(LayerIsIgnoreRaycast(h20), "H20 Ignore Raycast");

            Check(h20 != null && h20.transform.Find("VisualPulseRoot") != null,
                "H20 VisualPulseRoot present");
            Check(h08 != null && h08.GetComponent<ArIconCueVisual>() == null,
                "H08 no longer uses ArIconCueVisual (root breath removed)");
            Check(File.Exists(TaskMapPath), "Task map file present (untouched by Apply)");

            Check(File.Exists("Assets/Norscope/Runtime/ArGuidance/Visuals/ArDirectionalChevronVisual.cs"),
                "ArDirectionalChevronVisual script");
            Check(File.Exists("Assets/Norscope/Runtime/ArGuidance/Visuals/ArTargetRingVisual.cs"),
                "ArTargetRingVisual script");
            Check(File.Exists("Assets/Norscope/Runtime/ArGuidance/Visuals/ArConnectorActionVisual.cs"),
                "ArConnectorActionVisual script");
            Check(File.Exists("Assets/Norscope/Runtime/ArGuidance/Visuals/ArInspectionWarningVisual.cs"),
                "ArInspectionWarningVisual script");

            // Field gates via SerializedObject
            if (h05 != null)
            {
                var v = h05.GetComponent<ArDirectionalChevronVisual>();
                var so = new SerializedObject(v);
                Check(Mathf.Abs(so.FindProperty("edgeLengthMeters").floatValue - 0.075f) < 1e-5f,
                    "H05 edge 75 mm");
                Check(Mathf.Abs(so.FindProperty("chevronTravelMeters").floatValue - 0.015f) < 1e-5f,
                    "H05 travel 15 mm");
                Check(so.FindProperty("animationEnabled").boolValue, "H05 animation on");
                Check(so.FindProperty("labelText").stringValue == "LIFT HERE", "H05 label");
            }

            if (h08 != null)
            {
                var v = h08.GetComponent<ArTargetRingVisual>();
                var so = new SerializedObject(v);
                Check(Mathf.Abs(so.FindProperty("ringDiameterMeters").floatValue - 0.022f) < 1e-5f,
                    "H08 ring 22 mm");
                Check(Mathf.Abs(so.FindProperty("pressTravelMeters").floatValue - 0.0065f) < 1e-5f,
                    "H08 press 6.5 mm");
                Check(so.FindProperty("labelText").stringValue == "PRESS POWER", "H08 label");
            }

            if (h18 != null)
            {
                var v = h18.GetComponent<ArConnectorActionVisual>();
                var so = new SerializedObject(v);
                Vector2 zone = so.FindProperty("targetZoneMeters").vector2Value;
                Check(Mathf.Abs(zone.x - 0.025f) < 1e-5f && Mathf.Abs(zone.y - 0.010f) < 1e-5f,
                    "H18 zone 25×10 mm");
                Check(Mathf.Abs(so.FindProperty("arrowTravelMeters").floatValue - 0.028f) < 1e-5f,
                    "H18 travel 28 mm");
                Check(so.FindProperty("labelText").stringValue == "DISCONNECT", "H18 label");
                Check(so.FindProperty("secondaryLabelText").stringValue == "Battery connector",
                    "H18 secondary");
            }

            if (h20 != null)
            {
                var v = h20.GetComponent<ArInspectionWarningVisual>();
                var so = new SerializedObject(v);
                Check(so.FindProperty("pulseScaleMax").floatValue <= 0.05f + 1e-5f, "H20 pulse ≤5%");
                Check(Mathf.Abs(so.FindProperty("pulseScaleMax").floatValue - 0.04f) < 1e-5f,
                    "H20 pulse +4%");
                Check(so.FindProperty("labelText").stringValue == "CHECK FOR SWELLING", "H20 label");
                Check(so.FindProperty("authoredVisualState").enumValueIndex ==
                      (int)ArHologramVisualState.Warning, "H20 authored Warning (not Danger)");
            }

            sb.AppendLine();
            sb.AppendLine(pass ? "STATUS PASS" : "STATUS FAIL");
            report = sb.ToString();
            return pass;
        }

        private static Vector3 ReadPose(
            ArHologramLibrary library, string id, out Vector3 euler,
            Vector3 fallbackPos, Vector3 fallbackEuler)
        {
            ArHologramLibraryEntry e = library != null ? library.Find(id) : null;
            if (e == null)
            {
                euler = fallbackEuler;
                return fallbackPos;
            }

            euler = e.defaultLocalEuler;
            return e.defaultLocalPosition;
        }

        private static void WriteEntryPrefabOnly(
            ArHologramLibrary library, string id, GameObject prefab, Vector3 pos, Vector3 euler)
        {
            ArHologramLibraryEntry e = library.Find(id);
            if (e == null)
                throw new InvalidOperationException("Missing library entry " + id);
            e.prefab = prefab;
            e.defaultLocalPosition = pos;
            e.defaultLocalEuler = euler;
            e.defaultLocalScale = Vector3.one;
        }

        private static bool PoseMatches(
            ArHologramLibrary library, string id, Vector3 pos, Vector3 euler)
        {
            ArHologramLibraryEntry e = library != null ? library.Find(id) : null;
            return e != null && Approx(e.defaultLocalPosition, pos) && Approx(e.defaultLocalEuler, euler);
        }

        private static bool ComponentIdIs(GameObject go, string id)
        {
            if (go == null) return false;
            ArHologramVisualBase v = go.GetComponent<ArHologramVisualBase>();
            return v != null && v.ComponentId == id;
        }

        private static bool Approx(Vector3 a, Vector3 b) => (a - b).sqrMagnitude < 1e-10f;

        private static bool NoEnabledColliders(GameObject go)
        {
            if (go == null) return false;
            Collider[] cols = go.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i] != null && cols[i].enabled)
                    return false;
            }

            return true;
        }

        private static bool LayerIsIgnoreRaycast(GameObject go)
        {
            if (go == null) return false;
            int layer = LayerMask.NameToLayer("Ignore Raycast");
            Transform[] all = go.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].gameObject.layer != layer)
                    return false;
            }

            return true;
        }

        private static Sprite FindExistingIcon(string prefabPath)
        {
            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (go == null) return null;
            ArHologramVisualBase v = go.GetComponent<ArHologramVisualBase>();
            if (v != null && v.Icon != null)
                return v.Icon;
            SpriteRenderer sr = go.GetComponentInChildren<SpriteRenderer>(true);
            return sr != null ? sr.sprite : null;
        }

        private static Material LoadMat(string name, Color color)
        {
            string path = MatFolder + "/" + name + ".mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            mat = new Material(Shader.Find("Sprites/Default")) { color = color };
            Directory.CreateDirectory(MatFolder);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static LineRenderer CreateLine(string name, Transform parent, Material mat, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.layer = LayerMask.NameToLayer("Ignore Raycast");
            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = mat;
            lr.useWorldSpace = false;
            lr.loop = loop;
            lr.widthMultiplier = 0.0018f;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            return lr;
        }

        private static TMP_Text CreateWorldLabel(
            string name, Transform parent, string text, float y, float fontSize)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, y, 0f);
            go.layer = LayerMask.NameToLayer("Ignore Raycast");
            TextMeshPro tmp = go.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = ArGuidancePalette.Cyan;
            tmp.raycastTarget = false;
            tmp.enableAutoSizing = false;
            RectTransform rt = go.GetComponent<RectTransform>();
            if (rt != null) rt.sizeDelta = new Vector2(0.55f, 0.10f);
            return tmp;
        }

        private static SpriteRenderer CreateFallbackIcon(Transform parent, Sprite sprite)
        {
            var go = new GameObject("IconSprite", typeof(SpriteRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            go.layer = LayerMask.NameToLayer("Ignore Raycast");
            SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.enabled = false;
            sr.color = ArGuidancePalette.Cyan;
            return sr;
        }

        private static GameObject SavePrefab(GameObject root, string path)
        {
            EnsureIgnoreRaycastRecursive(root.transform);
            StripEnabledColliders(root.transform);
            // Prefab root must stay identity — library owns measured pose.
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? PrefabFolder);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static void EnsureIgnoreRaycastRecursive(Transform t)
        {
            int layer = LayerMask.NameToLayer("Ignore Raycast");
            if (layer >= 0) t.gameObject.layer = layer;
            for (int i = 0; i < t.childCount; i++)
                EnsureIgnoreRaycastRecursive(t.GetChild(i));
        }

        private static void StripEnabledColliders(Transform t)
        {
            Collider[] cols = t.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i] != null)
                    UnityEngine.Object.DestroyImmediate(cols[i]);
            }
        }
    }
}
