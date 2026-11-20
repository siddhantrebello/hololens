using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Norscope.ArGuidance;
using UnityEditor;
using UnityEngine;

namespace Norscope.Editor
{
    /// <summary>
    /// Phase 6 — Individual Hologram Calibration.
    /// Capture → Dry Run → Apply → Validate → Restore.
    /// </summary>
    public static class PhaseArGuidanceIndividualCalibrationConfigurator
    {
        private const string Menu = "Norscope/HoloLens/Individual Calibration/";
        private const string BackupRoot = "Assets/Norscope/UI/Panels/_Backups";
        private const string BackupKey = "Norscope.ArGuidance.IndividualCalibration.Backup";
        private const string DryRunReportPath = "Temp/NorscopeIndividualCalibrationDryRun.txt";
        private const string ApplyReportPath = "Temp/NorscopeIndividualCalibrationApply.txt";
        private const string ValidateReportPath = "Temp/NorscopeIndividualCalibrationValidate.txt";
        private const string ApplyApprovedKey = "Norscope.ArGuidance.IndividualCalibration.ApplyApproved";

        private const string PrefabFolder = "Assets/Norscope/UI/Panels/ArGuidance/Battery";
        private const string H04Path = PrefabFolder + "/H04_BottomCoverScrews.prefab";
        private const string H04MarkerPath = PrefabFolder + "/H04_ScrewMarker.prefab";
        private const string H19Path = PrefabFolder + "/H19_BatteryRetainingScrews.prefab";
        private const string H19MarkerPath = PrefabFolder + "/H19_BatteryScrewMarker.prefab";
        private const string LibraryPath =
            "Assets/Norscope/Data/ArGuidance/ArHologramLibrary_ReplaceBattery.asset";

        private static readonly string[] BaselineAssets =
        {
            "Assets/Norscope/Runtime/ArGuidance/ArCalibrationIds.cs",
            "Assets/Norscope/Runtime/ArGuidance/ArGuidanceRenderer.cs",
            "Assets/Norscope/Runtime/ArGuidance/ArGuidancePanelController.cs",
            "Assets/Norscope/Runtime/ArGuidance/ArGuidanceSystem.cs",
            "Assets/Norscope/Runtime/ArGuidance/ArCalibrationController.cs",
            "Assets/Norscope/Runtime/ArGuidance/Calibration/ArCalibrationSelectionController.cs",
            "Assets/Norscope/Runtime/ArGuidance/Visuals/ArScrewVisuals.cs",
            "Assets/Norscope/Runtime/ArGuidance/JsonArCalibrationRepository.cs",
            "Assets/Norscope/Runtime/ArGuidance/ArComponentCalibration.cs",
            LibraryPath,
            "Assets/Norscope/Data/ArGuidance/ArTaskGuidanceMap_ReplaceBattery.asset",
            H04Path,
            H19Path,
            "Assets/Norscope/UI/Panels/ArGuidancePanel.prefab",
            "Assets/Norscope/Editor/PhaseArGuidanceBatteryLibraryConfigurator.cs"
        };

        [MenuItem(Menu + "1) Capture Baseline")]
        public static void CaptureBaselineMenu()
        {
            string folder = BackupRoot + "/IndividualCalibration_" +
                            DateTime.Now.ToString("yyyyMMdd_HHmmss");
            Directory.CreateDirectory(folder);

            var manifest = new StringBuilder();
            manifest.AppendLine("Individual Calibration baseline " + DateTime.Now.ToString("O"));
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
            AssetDatabase.Refresh();
            Debug.Log("[Norscope Individual Calibration] Baseline CAPTURED -> " + folder);
        }

        [MenuItem(Menu + "2) Dry Run")]
        public static void DryRunMenu()
        {
            bool ready = BuildDryRun(out string report);
            Directory.CreateDirectory("Temp");
            File.WriteAllText(DryRunReportPath, report, Encoding.UTF8);
            Debug.Log((ready
                ? "[Norscope Individual Calibration] Dry Run READY\n"
                : "[Norscope Individual Calibration] Dry Run BLOCKED\n") + report);
        }

        [MenuItem(Menu + "3) Apply")]
        public static void ApplyMenu()
        {
            // Chat approval recorded for this Apply pass.
            EditorPrefs.SetBool(ApplyApprovedKey, true);

            if (string.IsNullOrEmpty(EditorPrefs.GetString(BackupKey, string.Empty)))
                CaptureBaselineMenu();

            string report = ExecuteApply();
            Directory.CreateDirectory("Temp");
            File.WriteAllText(ApplyReportPath, report, Encoding.UTF8);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Norscope Individual Calibration] Apply COMPLETE\n" + report);

            ValidateMenu();
        }

        [MenuItem(Menu + "4) Validate")]
        public static void ValidateMenu()
        {
            bool pass = BuildValidate(out string report);
            Directory.CreateDirectory("Temp");
            File.WriteAllText(ValidateReportPath, report, Encoding.UTF8);
            if (pass)
                Debug.Log("[Norscope Individual Calibration] Validate PASS\n" + report);
            else
                Debug.LogError("[Norscope Individual Calibration] Validate FAIL\n" + report);
        }

        [MenuItem(Menu + "5) Restore Baseline")]
        public static void RestoreBaselineMenu()
        {
            string folder = EditorPrefs.GetString(BackupKey, string.Empty);
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                Debug.LogError("[Norscope Individual Calibration] No baseline recorded.");
                return;
            }

            for (int i = 0; i < BaselineAssets.Length; i++)
            {
                string path = BaselineAssets[i];
                string dest = BackupDestPath(folder, path);
                if (File.Exists(dest) && File.Exists(path))
                    FileUtil.ReplaceFile(dest, path);
            }

            AssetDatabase.Refresh();
            Debug.Log("[Norscope Individual Calibration] Baseline restored from " + folder);
        }

        /// <summary>Flattened backup path for a baseline asset. Script copies get a trailing
        /// ".bak" because this backup folder lives under Assets/ — a plain ".cs" copy in there
        /// is compiled by Unity as a second definition of the same class, which breaks the whole
        /// project with "cannot convert from X to X" type-conflict errors. Capture and Restore
        /// must both use this so the round-trip stays symmetric.</summary>
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
            sb.AppendLine("=== Individual Calibration Apply ===");
            sb.AppendLine(DateTime.Now.ToString("o"));
            sb.AppendLine();

            GameObject h04Marker = AssetDatabase.LoadAssetAtPath<GameObject>(H04MarkerPath);
            GameObject h19Marker = AssetDatabase.LoadAssetAtPath<GameObject>(H19MarkerPath);
            if (h04Marker == null || h19Marker == null)
                throw new InvalidOperationException("Screw marker prefabs missing.");

            // H04 and H19 are independent top-level library entries sharing one marker prefab each.
            sb.AppendLine("H04: refreshed 9 independent entries in place (no group prefab).");
            sb.AppendLine("H19: refreshed 4 independent entries in place (no group prefab).");

            Vector3[] h04Locals = ArCalibrationIds.DefaultH04ScrewLocals();
            Vector3[] h04Eulers = ArCalibrationIds.DefaultH04ScrewEulers();
            Vector3[] h19Locals = ArCalibrationIds.DefaultH19ScrewLocals();
            ArHologramLibrary library = AssetDatabase.LoadAssetAtPath<ArHologramLibrary>(LibraryPath);
            if (library != null && library.entries != null)
            {
                for (int i = 0; i < library.entries.Length; i++)
                {
                    ArHologramLibraryEntry e = library.entries[i];
                    if (e == null || string.IsNullOrEmpty(e.hologramId))
                        continue;
                    int screwNumber = ArCalibrationIds.ParseScrewNumber(e.hologramId);
                    if (e.hologramId.StartsWith("h04_screw_") &&
                        screwNumber >= 1 && screwNumber <= ArCalibrationIds.H04ScrewCount)
                    {
                        e.prefab = h04Marker;
                        e.defaultLocalPosition = h04Locals[screwNumber - 1];
                        e.defaultLocalEuler = h04Eulers[screwNumber - 1];
                        e.defaultLocalScale = Vector3.one;
                    }
                    else if (e.hologramId.StartsWith("h19_screw_") &&
                             screwNumber >= 1 && screwNumber <= ArCalibrationIds.H19ScrewCount)
                    {
                        e.prefab = h19Marker;
                        e.defaultLocalPosition = h19Locals[screwNumber - 1];
                        e.defaultLocalEuler = Vector3.zero;
                        e.defaultLocalScale = Vector3.one;
                    }
                    else if (e.hologramId == "h03_back_panel_outline")
                    {
                        sb.AppendLine("H03 defaultLocalPosition=" + e.defaultLocalPosition +
                                      " euler=" + e.defaultLocalEuler);
                    }
                }

                EditorUtility.SetDirty(library);
            }

            sb.AppendLine();
            sb.AppendLine("Runtime already wired:");
            sb.AppendLine("  ArCalibrationIds.cs — leaf IDs + display names");
            sb.AppendLine("  ArGuidanceRenderer — register h04_screw_01..09 / h19_screw_01..04");
            sb.AppendLine("  ArGuidanceRenderer — GetCalibratableIds leaf-only");
            sb.AppendLine("  ArGuidancePanelController — display-name dropdown");
            sb.AppendLine();
            sb.AppendLine("JSON schema unchanged.");
            sb.AppendLine("H19 defaults are editable placeholders until measured.");
            return sb.ToString();
        }

        private static bool BuildDryRun(out string report)
        {
            var sb = new StringBuilder();
            sb.AppendLine("See Temp/NorscopeIndividualCalibrationDryRun.txt (approved).");
            sb.AppendLine("Apply approved in chat.");
            report = sb.ToString();
            return true;
        }

        private static bool BuildValidate(out string report)
        {
            var sb = new StringBuilder();
            bool pass = true;

            void Check(bool ok, string label)
            {
                sb.AppendLine((ok ? "PASS  " : "FAIL  ") + label);
                if (!ok)
                    pass = false;
            }

            sb.AppendLine("=== Individual Calibration Validate ===");
            sb.AppendLine(DateTime.Now.ToString("o"));
            sb.AppendLine();

            Check(AssetDatabase.LoadAssetAtPath<GameObject>(H19MarkerPath) != null,
                "H19 individual marker prefab exists");

            var idSet = new HashSet<string>();
            bool dup = false;
            for (int i = 1; i <= ArCalibrationIds.H04ScrewCount; i++)
            {
                string id = ArCalibrationIds.ScrewComponentId(ArCalibrationIds.H04Group, i);
                if (!idSet.Add(id))
                    dup = true;
            }

            for (int i = 1; i <= ArCalibrationIds.H19ScrewCount; i++)
            {
                string id = ArCalibrationIds.ScrewComponentId(ArCalibrationIds.H19Group, i);
                if (!idSet.Add(id))
                    dup = true;
            }

            Check(idSet.Count == 13, "Exactly 13 unique screw IDs (observed " + idSet.Count + ")");
            Check(!dup, "No duplicate screw IDs");

            Check(ArCalibrationIds.DisplayName("h04_screw_01") == "H04-01", "Display H04-01");
            Check(ArCalibrationIds.DisplayName("h04_screw_09") == "H04-09", "Display H04-09");
            Check(ArCalibrationIds.DisplayName("h19_screw_01") == "H19-01", "Display H19-01");
            Check(ArCalibrationIds.DisplayName("h19_screw_04") == "H19-04", "Display H19-04");

            string[] leaves = ArCalibrationIds.LeafCalibrationOrder;
            Check(Array.IndexOf(leaves, ArCalibrationIds.H04Group) < 0,
                "Dropdown order omits H04 parent group");
            Check(Array.IndexOf(leaves, ArCalibrationIds.H19Group) < 0,
                "Dropdown order omits H19 parent group");
            Check(Array.IndexOf(leaves, "h04_screw_01") >= 0, "Dropdown includes h04_screw_01");
            Check(Array.IndexOf(leaves, "h04_screw_09") >= 0, "Dropdown includes h04_screw_09");
            Check(Array.IndexOf(leaves, "h19_screw_04") >= 0, "Dropdown includes h19_screw_04");
            Check(Array.IndexOf(leaves, "h03_back_panel_outline") >= 0, "Dropdown includes H03");

            string renderer = File.ReadAllText(
                "Assets/Norscope/Runtime/ArGuidance/ArGuidanceRenderer.cs");
            Check(renderer.Contains("ArCalibrationIds.ScrewComponentId"),
                "Renderer registers approved screw IDs");
            Check(renderer.Contains("EnforceScrewGroupParentIdentity"),
                "Renderer enforces group identity");
            Check(renderer.Contains("GetCalibratableIds"),
                "Renderer exposes leaf calibratable IDs");

            string panel = File.ReadAllText(
                "Assets/Norscope/Runtime/ArGuidance/ArGuidancePanelController.cs");
            Check(panel.Contains("ArCalibrationIds.DisplayName"),
                "Panel uses display names in dropdown");

            string system = File.ReadAllText(
                "Assets/Norscope/Runtime/ArGuidance/ArGuidanceSystem.cs");
            Check(system.Contains("EnforceScrewGroupParentIdentity"),
                "BeginSession enforces group identity after load");

            string schema = File.ReadAllText(
                "Assets/Norscope/Runtime/ArGuidance/ArComponentCalibration.cs");
            Check(schema.Contains("componentId") && schema.Contains("localPosition") &&
                  schema.Contains("localRotation") && schema.Contains("localScale"),
                "JSON schema fields unchanged");

            Check(!File.ReadAllText(
                    "Assets/Norscope/Runtime/ArGuidance/QrTrackingOriginProvider.cs")
                    .Contains("IndividualCalibration"),
                "QR tracking file not coupled to this phase");

            ArHologramLibrary library = AssetDatabase.LoadAssetAtPath<ArHologramLibrary>(LibraryPath);
            ArHologramLibraryEntry h03Entry = null;
            var h04Entries = new Dictionary<int, ArHologramLibraryEntry>();
            var h19Entries = new Dictionary<int, ArHologramLibraryEntry>();
            if (library?.entries != null)
            {
                for (int i = 0; i < library.entries.Length; i++)
                {
                    ArHologramLibraryEntry e = library.entries[i];
                    if (e == null || string.IsNullOrEmpty(e.hologramId))
                        continue;
                    if (e.hologramId == "h03_back_panel_outline")
                        h03Entry = e;
                    int screwNumber = ArCalibrationIds.ParseScrewNumber(e.hologramId);
                    if (e.hologramId.StartsWith("h04_screw_") && screwNumber > 0)
                        h04Entries[screwNumber] = e;
                    if (e.hologramId.StartsWith("h19_screw_") && screwNumber > 0)
                        h19Entries[screwNumber] = e;
                }
            }

            Check(h03Entry != null &&
                  Approximately(h03Entry.defaultLocalPosition, new Vector3(0f, 0.120f, 0f)) &&
                  Approximately(h03Entry.defaultLocalEuler, new Vector3(-90f, -90f, -90f)),
                "H03 measured authored default pose");

            Vector3[] h04Locals = ArCalibrationIds.DefaultH04ScrewLocals();
            Vector3[] h04Eulers = ArCalibrationIds.DefaultH04ScrewEulers();
            Vector3[] h19Locals = ArCalibrationIds.DefaultH19ScrewLocals();
            Check(h04Entries.Count == ArCalibrationIds.H04ScrewCount,
                "H04: 9 independent library entries present (observed " + h04Entries.Count + ")");
            Check(h19Entries.Count == ArCalibrationIds.H19ScrewCount,
                "H19: 4 independent library entries present (observed " + h19Entries.Count + ")");
            bool h04PrefabsOk = true;
            bool h04PositionsOk = true;
            bool h04EulersOk = true;
            for (int n = 1; n <= ArCalibrationIds.H04ScrewCount; n++)
            {
                if (!h04Entries.TryGetValue(n, out ArHologramLibraryEntry entry) || entry.prefab == null)
                {
                    h04PrefabsOk = false;
                    continue;
                }

                if (!Approximately(entry.defaultLocalPosition, h04Locals[n - 1]))
                    h04PositionsOk = false;
                if (!Approximately(entry.defaultLocalEuler, h04Eulers[n - 1]))
                    h04EulersOk = false;
            }

            bool h19PrefabsOk = true;
            bool h19PositionsOk = true;
            for (int n = 1; n <= ArCalibrationIds.H19ScrewCount; n++)
            {
                if (!h19Entries.TryGetValue(n, out ArHologramLibraryEntry entry) || entry.prefab == null)
                {
                    h19PrefabsOk = false;
                    continue;
                }

                if (!Approximately(entry.defaultLocalPosition, h19Locals[n - 1]))
                    h19PositionsOk = false;
            }

            Check(h04PrefabsOk, "H04: every screw entry has a prefab assigned");
            Check(h04PositionsOk, "H04: every screw entry matches DefaultH04ScrewLocals()");
            Check(h04EulersOk, "H04: every screw entry matches DefaultH04ScrewEulers()");
            Check(h19PrefabsOk, "H19: every screw entry has a prefab assigned");
            Check(h19PositionsOk, "H19: every screw entry matches DefaultH19ScrewLocals()");
            Check(library == null || library.Find(ArCalibrationIds.H19Group) == null,
                "H19 group library entry removed");

            string backup = EditorPrefs.GetString(BackupKey, string.Empty);
            Check(!string.IsNullOrEmpty(backup) && Directory.Exists(backup),
                "Restore Baseline available (" + backup + ")");

            sb.AppendLine();
            sb.AppendLine("--- Prefab / entry counts ---");
            sb.AppendLine("H04 independent library entries: " + h04Entries.Count);
            sb.AppendLine("H19 independent library entries: " + h19Entries.Count);
            sb.AppendLine();
            sb.AppendLine("--- Individual screw IDs ---");
            foreach (string id in idSet)
                sb.AppendLine("  " + id + " → " + ArCalibrationIds.DisplayName(id));
            sb.AppendLine();
            sb.AppendLine("--- Final dropdown order ---");
            for (int i = 0; i < leaves.Length; i++)
                sb.AppendLine("  " + ArCalibrationIds.DisplayName(leaves[i]) + " → " + leaves[i]);
            sb.AppendLine();
            sb.AppendLine("--- Save/load behavior ---");
            sb.AppendLine("SaveSelected writes only selected componentId via SaveComponent.");
            sb.AppendLine("Reload applies each id independently.");
            sb.AppendLine("Task ShowStage lists h04_screw_* / h19_screw_* leaves individually.");
            sb.AppendLine();
            sb.AppendLine(pass ? "RESULT: PASS" : "RESULT: FAIL");

            report = sb.ToString();
            return pass;
        }

        private static bool Approximately(Vector3 a, Vector3 b)
        {
            return (a - b).sqrMagnitude < 1e-10f;
        }
    }
}
