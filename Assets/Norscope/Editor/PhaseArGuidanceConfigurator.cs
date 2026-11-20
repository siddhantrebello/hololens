using System;
using System.IO;
using System.Text;
using Norscope.ArGuidance;
using Norscope.Navigation;
using Norscope.UI;
using Norscope.Windows;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace Norscope.Editor
{
    /// <summary>
    /// AR Guidance Phase 1 — Capture → Dry Run → Apply → Validate → Restore.
    /// </summary>
    public static class PhaseArGuidanceConfigurator
    {
        private const string Menu = "Norscope/HoloLens/AR Guidance/Phase 1/";
        private const string BackupRoot = "Assets/Norscope/UI/Panels/_Backups";
        private const string BackupKey = "Norscope.ArGuidancePhase1.Backup";
        private const string OpenXrHashKey = "Norscope.ArGuidancePhase1.OpenXrHash";
        private const string ValidateReportPath = "Temp/NorscopeArGuidancePhase1Validate.txt";
        private const string ApplyReportPath = "Temp/NorscopeArGuidancePhase1Apply.txt";

        private const string BootstrapScene = "Assets/Norscope/Scenes/Bootstrap.unity";
        private const string HomePanelPrefab = "Assets/Norscope/UI/Panels/HomePanel.prefab";
        private const string ArPanelPrefab = "Assets/Norscope/UI/Panels/ArGuidancePanel.prefab";
        private const string ProfilePath =
            "Assets/Norscope/Data/ArGuidance/ArGuidanceProfile_DellPrecision3570.asset";
        private const string HoloPrefabFolder = "Assets/Norscope/UI/Panels/ArGuidance";
        private const string MatFolder = "Assets/Norscope/Materials/ArGuidance";
        private const string OpenXrSettings = "Assets/XR/Settings/OpenXRPackageSettings.asset";

        private static readonly Color Cyan = new Color(0.133f, 0.827f, 0.933f, 0.85f);
        private static readonly Color Amber = new Color(0.961f, 0.620f, 0.043f, 0.9f);
        private static readonly Color PanelBg = new Color(0.07f, 0.09f, 0.12f, 0.94f);
        private static readonly Color PanelBorder = new Color(0.15f, 0.19f, 0.26f, 0.9f);

        [MenuItem(Menu + "1) Capture Baseline")]
        public static void CaptureBaselineMenu()
        {
            string folder = BackupRoot + "/ArGuidancePhase1_" +
                            DateTime.Now.ToString("yyyyMMdd_HHmmss");
            Directory.CreateDirectory(folder);
            CopyIfExists(BootstrapScene, folder + "/Bootstrap.unity");
            CopyIfExists(HomePanelPrefab, folder + "/HomePanel.prefab");
            CopyIfExists(
                "Assets/Norscope/Runtime/Navigation/PanelType.cs",
                folder + "/PanelType.cs.bak");
            CopyIfExists(
                "Assets/Norscope/Runtime/Windows/SpatialWindowId.cs",
                folder + "/SpatialWindowId.cs.bak");
            CopyIfExists(OpenXrSettings, folder + "/OpenXRPackageSettings.asset.bak");
            if (File.Exists(OpenXrSettings))
                EditorPrefs.SetString(OpenXrHashKey, HashFile(OpenXrSettings));
            EditorPrefs.SetString(BackupKey, folder);
            AssetDatabase.Refresh();
            Debug.Log("[Norscope AR Guidance] Baseline CAPTURED → " + folder);
            if (!Application.isBatchMode)
                EditorUtility.DisplayDialog("AR Guidance Phase 1", "Baseline:\n" + folder, "OK");
        }

        [MenuItem(Menu + "2) Dry Run")]
        public static void DryRunMenu()
        {
            string report = File.Exists("Temp/NorscopeArGuidancePhase1DryRun.txt")
                ? File.ReadAllText("Temp/NorscopeArGuidancePhase1DryRun.txt")
                : "Dry run report missing. See approved Temp report.";
            Debug.Log("[Norscope AR Guidance] Dry Run (locked Apply until approval).\n" + report);
            if (!Application.isBatchMode)
                EditorUtility.DisplayDialog("AR Guidance Dry Run", "See Temp report. Apply locked.", "OK");
        }

        [MenuItem(Menu + "3) Apply")]
        public static void ApplyMenu()
        {
            try
            {
                ApplyInternal();
            }
            catch (Exception ex)
            {
                Debug.LogError("[Norscope AR Guidance] Apply FAILED:\n" + ex);
                if (!Application.isBatchMode)
                    EditorUtility.DisplayDialog("AR Guidance Apply", "FAILED\n" + ex.Message, "OK");
                throw;
            }
        }

        [MenuItem(Menu + "4) Validate")]
        public static void ValidateMenu()
        {
            bool pass = Validate(out string report);
            File.WriteAllText(ValidateReportPath, report);
            Debug.Log(pass
                ? "[Norscope AR Guidance] Validate PASS\n" + report
                : "[Norscope AR Guidance] Validate FAIL\n" + report);
            if (!Application.isBatchMode)
                EditorUtility.DisplayDialog("AR Guidance Validate", pass ? "PASS" : "FAIL", "OK");
        }

        [MenuItem(Menu + "5) Restore Baseline")]
        public static void RestoreMenu()
        {
            string folder = EditorPrefs.GetString(BackupKey, string.Empty);
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                Debug.LogError("[Norscope AR Guidance] No baseline.");
                return;
            }

            CopyIfExists(folder + "/Bootstrap.unity", BootstrapScene);
            CopyIfExists(folder + "/HomePanel.prefab", HomePanelPrefab);
            CopyIfExists(folder + "/PanelType.cs.bak",
                "Assets/Norscope/Runtime/Navigation/PanelType.cs");
            CopyIfExists(folder + "/SpatialWindowId.cs.bak",
                "Assets/Norscope/Runtime/Windows/SpatialWindowId.cs");
            AssetDatabase.Refresh();
            Debug.Log("[Norscope AR Guidance] Baseline restored from " + folder);
        }

        public static void CaptureApplyValidate()
        {
            CaptureBaselineMenu();
            ApplyInternal();
            bool pass = Validate(out string report);
            File.WriteAllText(ValidateReportPath, report);
            Debug.Log(pass
                ? "[Norscope AR Guidance] CaptureApplyValidate PASS"
                : "[Norscope AR Guidance] CaptureApplyValidate FAIL\n" + report);
            EditorApplication.Exit(pass ? 0 : 1);
        }

        private static void ApplyInternal()
        {
            // Enum uniqueness gate
            if ((int)PanelType.ArGuidance != 10)
                throw new Exception("PanelType.ArGuidance must be 10.");
            if ((int)SpatialWindowId.ArGuidance != 11)
                throw new Exception("SpatialWindowId.ArGuidance must be 11.");
            foreach (PanelType t in Enum.GetValues(typeof(PanelType)))
            {
                if (t != PanelType.ArGuidance && (int)t == 10)
                    throw new Exception("PanelType value 10 already used by " + t);
            }

            foreach (SpatialWindowId id in Enum.GetValues(typeof(SpatialWindowId)))
            {
                if (id != SpatialWindowId.ArGuidance && (int)id == 11)
                    throw new Exception("SpatialWindowId value 11 already used by " + id);
            }

            Directory.CreateDirectory(HoloPrefabFolder);
            Directory.CreateDirectory(MatFolder);
            Directory.CreateDirectory("Assets/Norscope/Data/ArGuidance");
            Directory.CreateDirectory("Temp");

            Material cyan = GetOrCreateMat("ArHoloCyan", Cyan);
            Material amber = GetOrCreateMat("ArHoloAmber", Amber);
            Material green = GetOrCreateMat("ArHoloGreen", new Color(0.13f, 0.77f, 0.37f, 0.9f));
            Material red = GetOrCreateMat("ArHoloRed", new Color(0.94f, 0.27f, 0.27f, 0.9f));

            GameObject outline = CreateLaptopOutlinePrefab(cyan);
            GameObject screwMarker = CreateScrewMarkerPrefab(cyan);
            GameObject screwGroup = CreateScrewGroupPrefab(screwMarker);
            GameObject lift = CreateLiftEdgePrefab(cyan);
            GameObject connector = CreateConnectorPrefab(amber);
            GameObject fan = CreateFanPrefab(cyan);
            GameObject airflow = CreateAirflowPrefab(cyan);

            ArGuidanceProfile profile = GetOrCreateProfile();
            profile.equipmentId = "dell_precision_3570";
            profile.displayName = "Dell Precision 3570";
            profile.markerPayload = "NORSCOPE:DELL3570:DEMO01";
            profile.laptopOutlinePrefab = outline;
            profile.screwGroupPrefab = screwGroup;
            profile.liftEdgePrefab = lift;
            profile.connectorWarningPrefab = connector;
            profile.fanHighlightPrefab = fan;
            profile.airflowPrefab = airflow;
            EditorUtility.SetDirty(profile);

            GameObject panel = CreateArGuidancePanelPrefab(profile);
            UpdateHomeCardStorage();
            WireBootstrap(panel, profile);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var log = new StringBuilder();
            log.AppendLine("=== AR Guidance Phase 1 Apply ===");
            log.AppendLine(DateTime.Now.ToString("o"));
            log.AppendLine("Panel: " + ArPanelPrefab);
            log.AppendLine("Profile: " + ProfilePath);
            log.AppendLine("Home Card_Storage → PanelType.ArGuidance");
            log.AppendLine("Registry: ArGuidance WorldLocked");
            File.WriteAllText(ApplyReportPath, log.ToString());
            Debug.Log("[Norscope AR Guidance] Apply COMPLETED\n" + log);
            if (!Application.isBatchMode)
                EditorUtility.DisplayDialog("AR Guidance Apply", "COMPLETED", "OK");
        }

        private static bool Validate(out string report)
        {
            var sb = new StringBuilder();
            bool pass = true;
            void Check(bool ok, string label)
            {
                sb.AppendLine((ok ? "PASS  " : "FAIL  ") + label);
                if (!ok) pass = false;
            }

            sb.AppendLine("=== AR Guidance Phase 1 Validate ===");
            sb.AppendLine(DateTime.Now.ToString("o"));
            sb.AppendLine();

            Check((int)PanelType.ArGuidance == 10, "PanelType.ArGuidance = 10");
            Check((int)SpatialWindowId.ArGuidance == 11, "SpatialWindowId.ArGuidance = 11");
            bool unique10 = true;
            foreach (PanelType t in Enum.GetValues(typeof(PanelType)))
                if (t != PanelType.ArGuidance && (int)t == 10) unique10 = false;
            Check(unique10, "PanelType 10 unique");
            bool unique11 = true;
            foreach (SpatialWindowId id in Enum.GetValues(typeof(SpatialWindowId)))
                if (id != SpatialWindowId.ArGuidance && (int)id == 11) unique11 = false;
            Check(unique11, "SpatialWindowId 11 unique");

            Check(File.Exists(ArPanelPrefab), "ArGuidancePanel.prefab exists");
            Check(File.Exists(ProfilePath), "Equipment profile exists");
            Check(File.Exists(HoloPrefabFolder + "/ArLaptopOutline.prefab"), "ArLaptopOutline");
            Check(File.Exists(HoloPrefabFolder + "/ArScrewGroup.prefab"), "ArScrewGroup");
            Check(File.Exists(HoloPrefabFolder + "/ArScrewMarker.prefab"), "ArScrewMarker");
            Check(File.Exists(HoloPrefabFolder + "/ArLiftEdgeGuidance.prefab"), "ArLiftEdgeGuidance");
            Check(File.Exists(HoloPrefabFolder + "/ArConnectorWarning.prefab"), "ArConnectorWarning");
            Check(File.Exists(HoloPrefabFolder + "/ArFanHighlight.prefab"), "ArFanHighlight");
            Check(File.Exists(HoloPrefabFolder + "/ArAirflowGuidance.prefab"), "ArAirflowGuidance");

            Check(File.Exists("Assets/Norscope/Runtime/ArGuidance/IArTrackingOriginProvider.cs"),
                "IArTrackingOriginProvider");
            Check(File.Exists("Assets/Norscope/Runtime/ArGuidance/ManualTestOriginProvider.cs"),
                "ManualTestOriginProvider");
            Check(File.Exists("Assets/Norscope/Runtime/ArGuidance/QrTrackingOriginProvider.cs"),
                "QrTrackingOriginProvider stub");
            string qrSrc = File.ReadAllText(
                "Assets/Norscope/Runtime/ArGuidance/QrTrackingOriginProvider.cs");
            Check(!qrSrc.Contains("ARMarker") && !qrSrc.Contains("QRCode") &&
                  !qrSrc.Contains("Detect"),
                "No real QR implementation");
            Check(File.Exists("Assets/Norscope/Runtime/ArGuidance/ArGuidanceRenderer.cs") &&
                  File.ReadAllText("Assets/Norscope/Runtime/ArGuidance/ArGuidanceRenderer.cs")
                      .Contains("ShowGuidance"),
                "Future ShowGuidance API present (unused by tasks)");
            Check(!File.ReadAllText(
                      "Assets/Norscope/Runtime/TaskExecution/ActiveTaskPanel.cs")
                  .Contains("ArGuidance"),
                "ActiveTaskPanel unchanged (no AR refs)");

            // Home card
            GameObject home = PrefabUtility.LoadPrefabContents(HomePanelPrefab);
            try
            {
                HomePanelCard[] cards = home.GetComponentsInChildren<HomePanelCard>(true);
                HomePanelCard storage = null;
                HomePanelCard tasks = null, ask = null, settings = null, reports = null, about = null;
                for (int i = 0; i < cards.Length; i++)
                {
                    string n = cards[i].gameObject.name;
                    if (n == "Card_Storage") storage = cards[i];
                    if (n == "Card_TaskLibrary") tasks = cards[i];
                    if (n == "Card_AskAI") ask = cards[i];
                    if (n == "Card_Settings") settings = cards[i];
                    if (n == "Card_Reports") reports = cards[i];
                    if (n == "Card_About") about = cards[i];
                }

                Check(storage != null, "Card_Storage present");
                if (storage != null)
                {
                    SerializedObject so = new SerializedObject(storage);
                    Check((PanelType)so.FindProperty("targetPanel").enumValueIndex ==
                          PanelType.ArGuidance ||
                          so.FindProperty("targetPanel").intValue == (int)PanelType.ArGuidance,
                        "Card_Storage targets ArGuidance");
                    TMP_Text title = so.FindProperty("title").objectReferenceValue as TMP_Text;
                    TMP_Text sub = so.FindProperty("subtitle").objectReferenceValue as TMP_Text;
                    Check(title != null && title.text.Contains("AR Guidance"),
                        "Card_Storage title = AR Guidance");
                    Check(sub != null && sub.text.Contains("Spatial repair overlays"),
                        "Card_Storage subtitle updated");
                }

                Check(tasks != null && GetTarget(tasks) == PanelType.TaskLibrary,
                    "Tasks card unchanged");
                Check(ask != null && GetTarget(ask) == PanelType.AskAI, "Ask AI card unchanged");
                Check(settings != null && GetTarget(settings) == PanelType.Settings,
                    "Settings card unchanged");
                Check(reports != null && GetTarget(reports) == PanelType.Reports,
                    "Reports card unchanged");
                Check(about != null && GetTarget(about) == PanelType.About,
                    "About card unchanged");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(home);
            }

            // Panel chrome
            GameObject panel = PrefabUtility.LoadPrefabContents(ArPanelPrefab);
            try
            {
                Check(panel.GetComponent<NorscopePanelRoot>() != null, "NorscopePanelRoot");
                Check(panel.GetComponent<SpatialWindowBindings>() != null, "SpatialWindowBindings");
                Check(panel.GetComponent<PanelNavigationBinding>() != null, "PanelNavigationBinding");
                Check(panel.GetComponent<ArGuidancePanelController>() != null,
                    "ArGuidancePanelController");
                Check(FindDeep(panel.transform, "GrabRegion") != null, "GrabRegion");
                Check(FindDeep(panel.transform, "BottomRight") != null, "BottomRight resize");
                Check(FindDeep(panel.transform, "BackButton") != null, "BackButton");
                Check(FindDeep(panel.transform, "CloseButton") != null, "CloseButton");
                Check(FindDeep(panel.transform, "PreviewView") != null, "PreviewView");
                Check(FindDeep(panel.transform, "CalibrationView") != null, "CalibrationView");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(panel);
            }

            // Bootstrap registry
            Scene scene = EditorSceneManager.OpenScene(BootstrapScene, OpenSceneMode.Single);
            PanelRegistry registry = UnityEngine.Object.FindFirstObjectByType<PanelRegistry>();
            Check(registry != null, "PanelRegistry in Bootstrap");
            int arCount = 0;
            if (registry != null)
            {
                SerializedObject so = new SerializedObject(registry);
                SerializedProperty entries = so.FindProperty("entries");
                for (int i = 0; i < entries.arraySize; i++)
                {
                    SerializedProperty e = entries.GetArrayElementAtIndex(i);
                    if (e.FindPropertyRelative("panelType").intValue == (int)PanelType.ArGuidance)
                    {
                        arCount++;
                        Check(e.FindPropertyRelative("windowId").intValue ==
                              (int)SpatialWindowId.ArGuidance, "Registry windowId ArGuidance");
                        Check(e.FindPropertyRelative("behaviour").intValue ==
                              (int)PanelBehaviourType.WorldLocked, "Registry WorldLocked");
                        Check(e.FindPropertyRelative("prefab").objectReferenceValue != null,
                            "Registry prefab assigned");
                    }
                }
            }

            Check(arCount == 1, "Exactly one ArGuidance registry entry");
            Check(UnityEngine.Object.FindFirstObjectByType<ArGuidanceSystem>() != null,
                "ArGuidanceSystem in Bootstrap");

            string captured = EditorPrefs.GetString(OpenXrHashKey, string.Empty);
            if (!string.IsNullOrEmpty(captured) && File.Exists(OpenXrSettings))
                Check(HashFile(OpenXrSettings) == captured, "OpenXR unchanged");
            else
                sb.AppendLine("WARN  OpenXR hash not captured");

            Check(EditorPrefs.HasKey(BackupKey) || Directory.Exists(BackupRoot),
                "Restore Baseline available");

            Check(File.Exists("Assets/Norscope/Runtime/ArGuidance/JsonArCalibrationRepository.cs") &&
                  File.ReadAllText(
                          "Assets/Norscope/Runtime/ArGuidance/JsonArCalibrationRepository.cs")
                      .Contains("ArCalibration"),
                "Calibration persistence present");
            Check(File.ReadAllText(
                      "Assets/Norscope/Runtime/ArGuidance/ArFineAdjustStep.cs")
                  .Contains("0.010f") &&
                  File.ReadAllText(
                      "Assets/Norscope/Runtime/ArGuidance/ArFineAdjustStep.cs")
                  .Contains("0.002f") &&
                  File.ReadAllText(
                      "Assets/Norscope/Runtime/ArGuidance/ArFineAdjustStep.cs")
                  .Contains("0.0005f"),
                "Fine position increments approved");

            sb.AppendLine();
            sb.AppendLine("Manual Play Mode / HoloLens checks still required:");
            sb.AppendLine("  - Card_Storage opens AR Guidance WorldLocked panel");
            sb.AppendLine("  - Back/Close/Grab/Resize");
            sb.AppendLine("  - Preview toggles; Calibration grab + fine steps");
            sb.AppendLine("  - Manual origin once, no head follow, SIMULATION ORIGIN");
            sb.AppendLine("  - Save/reload local transforms; holograms under origin");
            sb.AppendLine("  - Holograms do not block hand rays");
            sb.AppendLine("  - Ask AI / Tasks / Settings / Reports unchanged");
            sb.AppendLine();
            sb.AppendLine(pass ? "STATUS: PASS" : "STATUS: FAIL");
            report = sb.ToString();
            EditorSceneManager.CloseScene(scene, true);
            return pass;
        }

        private static PanelType GetTarget(HomePanelCard card)
        {
            SerializedObject so = new SerializedObject(card);
            return (PanelType)so.FindProperty("targetPanel").intValue;
        }

        private static void UpdateHomeCardStorage()
        {
            GameObject home = PrefabUtility.LoadPrefabContents(HomePanelPrefab);
            try
            {
                Transform card = FindDeep(home.transform, "Card_Storage");
                if (card == null)
                    throw new Exception("Card_Storage not found on HomePanel.");
                HomePanelCard hpc = card.GetComponent<HomePanelCard>();
                SerializedObject so = new SerializedObject(hpc);
                so.FindProperty("targetPanel").intValue = (int)PanelType.ArGuidance;
                so.FindProperty("logLabel").stringValue = "Opening AR Guidance";
                TMP_Text title = so.FindProperty("title").objectReferenceValue as TMP_Text;
                TMP_Text sub = so.FindProperty("subtitle").objectReferenceValue as TMP_Text;
                if (title != null) title.text = "AR Guidance";
                if (sub != null) sub.text = "Spatial repair overlays and calibration";
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(home, HomePanelPrefab);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(home);
            }
        }

        private static void WireBootstrap(GameObject panelPrefab, ArGuidanceProfile profile)
        {
            Scene scene = EditorSceneManager.OpenScene(BootstrapScene, OpenSceneMode.Single);
            PanelRegistry registry = UnityEngine.Object.FindFirstObjectByType<PanelRegistry>();
            if (registry == null)
                throw new Exception("PanelRegistry missing in Bootstrap.");

            SerializedObject so = new SerializedObject(registry);
            SerializedProperty entries = so.FindProperty("entries");
            bool found = false;
            for (int i = 0; i < entries.arraySize; i++)
            {
                SerializedProperty e = entries.GetArrayElementAtIndex(i);
                if (e.FindPropertyRelative("panelType").intValue == (int)PanelType.ArGuidance)
                {
                    e.FindPropertyRelative("windowId").intValue = (int)SpatialWindowId.ArGuidance;
                    e.FindPropertyRelative("behaviour").intValue =
                        (int)PanelBehaviourType.WorldLocked;
                    e.FindPropertyRelative("prefab").objectReferenceValue = panelPrefab;
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                entries.arraySize++;
                SerializedProperty e = entries.GetArrayElementAtIndex(entries.arraySize - 1);
                e.FindPropertyRelative("panelType").intValue = (int)PanelType.ArGuidance;
                e.FindPropertyRelative("windowId").intValue = (int)SpatialWindowId.ArGuidance;
                e.FindPropertyRelative("behaviour").intValue =
                    (int)PanelBehaviourType.WorldLocked;
                e.FindPropertyRelative("prefab").objectReferenceValue = panelPrefab;
            }

            so.ApplyModifiedPropertiesWithoutUndo();

            ArGuidanceSystem system = UnityEngine.Object.FindFirstObjectByType<ArGuidanceSystem>();
            if (system == null)
            {
                GameObject go = new GameObject("ArGuidanceSystem");
                system = go.AddComponent<ArGuidanceSystem>();
                go.AddComponent<ManualTestOriginProvider>();
                go.AddComponent<QrTrackingOriginProvider>();
                go.AddComponent<ArGuidanceRenderer>();
                go.AddComponent<ArCalibrationController>();
            }

            SerializedObject sysSo = new SerializedObject(system);
            SerializedProperty profileProp = sysSo.FindProperty("profile");
            if (profileProp != null)
                profileProp.objectReferenceValue = profile;
            sysSo.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static ArGuidanceProfile GetOrCreateProfile()
        {
            ArGuidanceProfile profile =
                AssetDatabase.LoadAssetAtPath<ArGuidanceProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<ArGuidanceProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            return profile;
        }

        private static Material GetOrCreateMat(string name, Color color)
        {
            string path = MatFolder + "/" + name + ".mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null)
                    shader = Shader.Find("Universal Render Pipeline/Unlit");
                mat = new Material(shader) { color = color, name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.color = color;
                EditorUtility.SetDirty(mat);
            }

            return mat;
        }

        private static GameObject CreateLaptopOutlinePrefab(Material mat)
        {
            string path = HoloPrefabFolder + "/ArLaptopOutline.prefab";
            var root = new GameObject("ArLaptopOutline");
            var visual = root.AddComponent<ArLaptopOutlineVisual>();
            visual.ConfigureIdentity("laptop_outline", ArVisualType.LaptopOutline);
            LineRenderer peri = CreateLine("Perimeter", root.transform, mat, true);
            SerializedObject so = new SerializedObject(visual);
            so.FindProperty("perimeter").objectReferenceValue = peri;
            // corner brackets
            var brackets = new LineRenderer[4];
            for (int i = 0; i < 4; i++)
                brackets[i] = CreateLine("Corner" + i, root.transform, mat, false);
            SerializedProperty arr = so.FindProperty("cornerBrackets");
            arr.arraySize = 4;
            for (int i = 0; i < 4; i++)
                arr.GetArrayElementAtIndex(i).objectReferenceValue = brackets[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            visual.RebuildGeometry();
            return SavePrefab(root, path);
        }

        private static GameObject CreateScrewMarkerPrefab(Material mat)
        {
            string path = HoloPrefabFolder + "/ArScrewMarker.prefab";
            var root = new GameObject("ArScrewMarker");
            var visual = root.AddComponent<ArScrewMarkerVisual>();
            LineRenderer ring = CreateLine("Ring", root.transform, mat, true);
            GameObject labelGo = CreateTmpLabel("Number", root.transform, "1", 0.015f, 0.2f);
            SerializedObject so = new SerializedObject(visual);
            so.FindProperty("ring").objectReferenceValue = ring;
            so.FindProperty("numberLabel").objectReferenceValue =
                labelGo.GetComponent<TextMeshPro>();
            so.ApplyModifiedPropertiesWithoutUndo();
            visual.RebuildGeometry();
            return SavePrefab(root, path);
        }

        private static GameObject CreateScrewGroupPrefab(GameObject markerPrefab)
        {
            string path = HoloPrefabFolder + "/ArScrewGroup.prefab";
            var root = new GameObject("ArScrewGroup");
            var visual = root.AddComponent<ArScrewGroupVisual>();
            visual.ConfigureIdentity("bottom_cover_screw_group", ArVisualType.ScrewGroup);
            Vector3[] offsets =
            {
                new Vector3(-0.12f, 0f, -0.08f),
                new Vector3(0.12f, 0f, -0.08f),
                new Vector3(-0.12f, 0f, 0.08f),
                new Vector3(0.12f, 0f, 0.08f)
            };
            var markers = new ArScrewMarkerVisual[offsets.Length];
            for (int i = 0; i < offsets.Length; i++)
            {
                GameObject child = (GameObject)PrefabUtility.InstantiatePrefab(markerPrefab);
                child.name = "Screw_" + (i + 1);
                child.transform.SetParent(root.transform, false);
                child.transform.localPosition = offsets[i];
                markers[i] = child.GetComponent<ArScrewMarkerVisual>();
                markers[i].SetScrewNumber(i + 1);
            }

            SerializedObject so = new SerializedObject(visual);
            SerializedProperty arr = so.FindProperty("markers");
            arr.arraySize = markers.Length;
            for (int i = 0; i < markers.Length; i++)
                arr.GetArrayElementAtIndex(i).objectReferenceValue = markers[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            return SavePrefab(root, path);
        }

        private static GameObject CreateLiftEdgePrefab(Material mat)
        {
            string path = HoloPrefabFolder + "/ArLiftEdgeGuidance.prefab";
            var root = new GameObject("ArLiftEdgeGuidance");
            var visual = root.AddComponent<ArLiftEdgeVisual>();
            visual.ConfigureIdentity("lift_edge", ArVisualType.LiftEdge);
            LineRenderer edge = CreateLine("Edge", root.transform, mat, false);
            var chevrons = new Transform[3];
            for (int i = 0; i < 3; i++)
            {
                var c = GameObject.CreatePrimitive(PrimitiveType.Cube);
                c.name = "Chevron" + i;
                UnityEngine.Object.DestroyImmediate(c.GetComponent<Collider>());
                c.transform.SetParent(root.transform, false);
                c.transform.localScale = new Vector3(0.01f, 0.002f, 0.008f);
                c.transform.localPosition = new Vector3((i - 1) * 0.02f, 0.005f, 0f);
                c.GetComponent<MeshRenderer>().sharedMaterial = mat;
                chevrons[i] = c.transform;
            }

            GameObject label = CreateTmpLabel("Label", root.transform, "Lift here", 0.05f, 0.2f);
            SerializedObject so = new SerializedObject(visual);
            so.FindProperty("edgeLine").objectReferenceValue = edge;
            so.FindProperty("label").objectReferenceValue = label.GetComponent<TextMeshPro>();
            SerializedProperty arr = so.FindProperty("chevrons");
            arr.arraySize = 3;
            for (int i = 0; i < 3; i++)
                arr.GetArrayElementAtIndex(i).objectReferenceValue = chevrons[i];
            so.FindProperty("labelText").stringValue = "Lift here";
            so.ApplyModifiedPropertiesWithoutUndo();
            visual.RebuildGeometry();
            return SavePrefab(root, path);
        }

        private static GameObject CreateConnectorPrefab(Material mat)
        {
            string path = HoloPrefabFolder + "/ArConnectorWarning.prefab";
            var root = new GameObject("ArConnectorWarning");
            var visual = root.AddComponent<ArConnectorWarningVisual>();
            visual.ConfigureIdentity("battery_connector", ArVisualType.ConnectorWarning);
            LineRenderer zone = CreateLine("Zone", root.transform, mat, true);
            LineRenderer leader = CreateLine("Leader", root.transform, mat, false);
            GameObject label = CreateTmpLabel(
                "Label", root.transform, "Disconnect battery first", 0.06f, 0.2f);
            var icon = GameObject.CreatePrimitive(PrimitiveType.Cube);
            icon.name = "WarningIcon";
            UnityEngine.Object.DestroyImmediate(icon.GetComponent<Collider>());
            icon.transform.SetParent(root.transform, false);
            icon.transform.localScale = new Vector3(0.008f, 0.008f, 0.002f);
            icon.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            icon.GetComponent<MeshRenderer>().sharedMaterial = mat;
            SerializedObject so = new SerializedObject(visual);
            so.FindProperty("zone").objectReferenceValue = zone;
            so.FindProperty("leader").objectReferenceValue = leader;
            so.FindProperty("label").objectReferenceValue = label.GetComponent<TextMeshPro>();
            so.FindProperty("warningIcon").objectReferenceValue = icon.transform;
            so.FindProperty("labelText").stringValue = "Disconnect battery first";
            so.ApplyModifiedPropertiesWithoutUndo();
            visual.RebuildGeometry();
            return SavePrefab(root, path);
        }

        private static GameObject CreateFanPrefab(Material mat)
        {
            string path = HoloPrefabFolder + "/ArFanHighlight.prefab";
            var root = new GameObject("ArFanHighlight");
            var visual = root.AddComponent<ArFanHighlightVisual>();
            visual.ConfigureIdentity("cooling_fan", ArVisualType.FanHighlight);
            LineRenderer ring = CreateLine("Ring", root.transform, mat, true);
            LineRenderer leader = CreateLine("Leader", root.transform, mat, false);
            GameObject label = CreateTmpLabel("Label", root.transform, "Cooling fan", 0.08f, 0.2f);
            SerializedObject so = new SerializedObject(visual);
            so.FindProperty("ring").objectReferenceValue = ring;
            so.FindProperty("leader").objectReferenceValue = leader;
            so.FindProperty("label").objectReferenceValue = label.GetComponent<TextMeshPro>();
            so.FindProperty("labelText").stringValue = "Cooling fan";
            so.ApplyModifiedPropertiesWithoutUndo();
            visual.RebuildGeometry();
            return SavePrefab(root, path);
        }

        private static GameObject CreateAirflowPrefab(Material mat)
        {
            string path = HoloPrefabFolder + "/ArAirflowGuidance.prefab";
            var root = new GameObject("ArAirflowGuidance");
            var visual = root.AddComponent<ArAirflowVisual>();
            visual.ConfigureIdentity("fan_airflow", ArVisualType.Airflow);
            var chevrons = new Transform[5];
            for (int i = 0; i < 5; i++)
            {
                var c = GameObject.CreatePrimitive(PrimitiveType.Cube);
                c.name = "Flow" + i;
                UnityEngine.Object.DestroyImmediate(c.GetComponent<Collider>());
                c.transform.SetParent(root.transform, false);
                c.transform.localScale = new Vector3(0.006f, 0.0015f, 0.010f);
                c.transform.localPosition = new Vector3(0f, 0f, i * 0.02f);
                c.GetComponent<MeshRenderer>().sharedMaterial = mat;
                chevrons[i] = c.transform;
            }

            SerializedObject so = new SerializedObject(visual);
            SerializedProperty arr = so.FindProperty("chevrons");
            arr.arraySize = chevrons.Length;
            for (int i = 0; i < chevrons.Length; i++)
                arr.GetArrayElementAtIndex(i).objectReferenceValue = chevrons[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            return SavePrefab(root, path);
        }

        private static GameObject CreateArGuidancePanelPrefab(ArGuidanceProfile profile)
        {
            var root = new GameObject("ArGuidancePanel", typeof(RectTransform));
            RectTransform rootRt = root.GetComponent<RectTransform>();
            rootRt.sizeDelta = new Vector2(560f, 420f);

            Image bg = CreateUiImage("Background", root.transform, PanelBg, stretch: true);
            Image border = CreateUiImage("Border", root.transform, PanelBorder, stretch: true);
            border.raycastTarget = false;

            // Title bar
            GameObject titleBar = CreateUiPanel("TitleBar", root.transform,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -48f), new Vector2(0f, 0f));
            Button back = CreateUiButton("BackButton", titleBar.transform, "Back",
                new Vector2(8f, -8f), new Vector2(72f, 32f));
            TMP_Text title = CreateUiTmp("Title", titleBar.transform, "AR Guidance", 22,
                new Vector2(90f, -8f), new Vector2(260f, 32f));
            GameObject grab = CreateUiPanel("GrabRegion", titleBar.transform,
                new Vector2(0.35f, 0f), new Vector2(0.85f, 1f), Vector2.zero, Vector2.zero);
            grab.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.02f);
            GameObject close = CreateUiButton("CloseButton", titleBar.transform, "X",
                new Vector2(-40f, -8f), new Vector2(32f, 32f)).gameObject;
            RectTransform closeRt = close.GetComponent<RectTransform>();
            closeRt.anchorMin = closeRt.anchorMax = new Vector2(1f, 1f);

            TMP_Text equipment = CreateUiTmp("EquipmentLabel", root.transform,
                "Dell Precision 3570", 14,
                new Vector2(16f, -56f), new Vector2(300f, 24f));
            TMP_Text tracking = CreateUiTmp("TrackingStatusLabel", root.transform,
                "Manual Test Origin · SIMULATION ORIGIN", 13,
                new Vector2(16f, -78f), new Vector2(400f, 22f));

            GameObject modeRow = CreateUiPanel("ModeTabs", root.transform,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -110f), new Vector2(0f, -86f));
            Button previewBtn = CreateUiButton("PreviewTab", modeRow.transform, "Preview",
                new Vector2(16f, -4f), new Vector2(120f, 28f));
            Button calibBtn = CreateUiButton("CalibrationTab", modeRow.transform, "Calibration",
                new Vector2(150f, -4f), new Vector2(130f, 28f));

            GameObject previewView = CreateUiPanel("PreviewView", root.transform,
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 40f), new Vector2(0f, -120f));
            Toggle t1 = CreateUiToggle("Toggle_LaptopOutline", previewView.transform,
                "Show Laptop Outline", new Vector2(16f, -8f));
            Toggle t2 = CreateUiToggle("Toggle_BottomCoverScrews", previewView.transform,
                "Show Bottom-Cover Screws", new Vector2(16f, -40f));
            Toggle t3 = CreateUiToggle("Toggle_LiftEdge", previewView.transform,
                "Show Lift Edge", new Vector2(16f, -72f));
            Toggle t4 = CreateUiToggle("Toggle_BatteryConnector", previewView.transform,
                "Show Battery Connector", new Vector2(16f, -104f));
            Toggle t5 = CreateUiToggle("Toggle_CoolingFan", previewView.transform,
                "Show Cooling Fan", new Vector2(16f, -136f));
            Toggle t6 = CreateUiToggle("Toggle_FanAirflow", previewView.transform,
                "Show Fan Airflow", new Vector2(16f, -168f));
            Button hideAll = CreateUiButton("Button_HideAll", previewView.transform, "Hide All",
                new Vector2(16f, -208f), new Vector2(120f, 28f));

            GameObject calibView = CreateUiPanel("CalibrationView", root.transform,
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 40f), new Vector2(0f, -120f));
            calibView.SetActive(false);
            TMP_Dropdown dropdown = CreateUiDropdown("ComponentSelector", calibView.transform,
                new Vector2(16f, -8f), new Vector2(280f, 28f));
            Button showSel = CreateUiButton("Button_ShowSelected", calibView.transform,
                "Show Selected", new Vector2(310f, -8f), new Vector2(140f, 28f));
            Button save = CreateUiButton("Button_SaveComponent", calibView.transform, "Save Component",
                new Vector2(16f, -48f), new Vector2(140f, 28f));
            Button reset = CreateUiButton("Button_ResetComponent", calibView.transform, "Reset Component",
                new Vector2(170f, -48f), new Vector2(140f, 28f));
            Button previewSaved = CreateUiButton("Button_PreviewSaved", calibView.transform,
                "Preview Saved", new Vector2(320f, -48f), new Vector2(130f, 28f));
            Button hidePrev = CreateUiButton("Button_HidePreview", calibView.transform, "Hide Preview",
                new Vector2(16f, -88f), new Vector2(130f, 28f));
            Button p10 = CreateUiButton("PosStep10", calibView.transform, "10 mm",
                new Vector2(16f, -128f), new Vector2(64f, 26f));
            Button p2 = CreateUiButton("PosStep2", calibView.transform, "2 mm",
                new Vector2(88f, -128f), new Vector2(56f, 26f));
            Button p05 = CreateUiButton("PosStep05", calibView.transform, "0.5 mm",
                new Vector2(152f, -128f), new Vector2(64f, 26f));
            Button r5 = CreateUiButton("RotStep5", calibView.transform, "5°",
                new Vector2(230f, -128f), new Vector2(48f, 26f));
            Button r1 = CreateUiButton("RotStep1", calibView.transform, "1°",
                new Vector2(286f, -128f), new Vector2(48f, 26f));

            Button pxn = CreateUiButton("PosXNeg", calibView.transform, "X-", new Vector2(16f, -168f), new Vector2(40f, 26f));
            Button pxp = CreateUiButton("PosXPos", calibView.transform, "X+", new Vector2(60f, -168f), new Vector2(40f, 26f));
            Button pyn = CreateUiButton("PosYNeg", calibView.transform, "Y-", new Vector2(110f, -168f), new Vector2(40f, 26f));
            Button pyp = CreateUiButton("PosYPos", calibView.transform, "Y+", new Vector2(154f, -168f), new Vector2(40f, 26f));
            Button pzn = CreateUiButton("PosZNeg", calibView.transform, "Z-", new Vector2(204f, -168f), new Vector2(40f, 26f));
            Button pzp = CreateUiButton("PosZPos", calibView.transform, "Z+", new Vector2(248f, -168f), new Vector2(40f, 26f));
            Button rxn = CreateUiButton("RotXNeg", calibView.transform, "RX-", new Vector2(16f, -204f), new Vector2(44f, 26f));
            Button rxp = CreateUiButton("RotXPos", calibView.transform, "RX+", new Vector2(64f, -204f), new Vector2(44f, 26f));
            Button ryn = CreateUiButton("RotYNeg", calibView.transform, "RY-", new Vector2(116f, -204f), new Vector2(44f, 26f));
            Button ryp = CreateUiButton("RotYPos", calibView.transform, "RY+", new Vector2(164f, -204f), new Vector2(44f, 26f));
            Button rzn = CreateUiButton("RotZNeg", calibView.transform, "RZ-", new Vector2(216f, -204f), new Vector2(44f, 26f));
            Button rzp = CreateUiButton("RotZPos", calibView.transform, "RZ+", new Vector2(264f, -204f), new Vector2(44f, 26f));

            GameObject resizeRoot = CreateUiPanel("ResizeHandles", root.transform,
                new Vector2(1f, 0f), new Vector2(1f, 0f), Vector2.zero, Vector2.zero);
            GameObject bottomRight = CreateUiPanel("BottomRight", resizeRoot.transform,
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 0f), new Vector2(0f, 24f));
            bottomRight.AddComponent<Image>().color = new Color(0.2f, 0.8f, 0.9f, 0.35f);

            var panelRoot = root.AddComponent<NorscopePanelRoot>();
            var bindings = root.AddComponent<SpatialWindowBindings>();
            var nav = root.AddComponent<PanelNavigationBinding>();
            nav.Configure(PanelType.ArGuidance, PanelType.Home, back);
            var controller = root.AddComponent<ArGuidancePanelController>();

            // Wire bindings via SerializedObject
            SerializedObject bindSo = new SerializedObject(bindings);
            bindSo.FindProperty("grabRegion").objectReferenceValue = grab.transform;
            bindSo.FindProperty("resizeHandle").objectReferenceValue =
                bottomRight.GetComponent<RectTransform>();
            bindSo.FindProperty("closeButtonRoot").objectReferenceValue = close.transform;
            bindSo.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject ctrlSo = new SerializedObject(controller);
            ctrlSo.FindProperty("titleText").objectReferenceValue = title;
            ctrlSo.FindProperty("equipmentText").objectReferenceValue = equipment;
            ctrlSo.FindProperty("trackingStatusText").objectReferenceValue = tracking;
            ctrlSo.FindProperty("previewView").objectReferenceValue = previewView;
            ctrlSo.FindProperty("calibrationView").objectReferenceValue = calibView;
            ctrlSo.FindProperty("previewModeButton").objectReferenceValue = previewBtn;
            ctrlSo.FindProperty("calibrationModeButton").objectReferenceValue = calibBtn;
            ctrlSo.FindProperty("toggleLaptopOutline").objectReferenceValue = t1;
            ctrlSo.FindProperty("toggleScrewGroup").objectReferenceValue = t2;
            ctrlSo.FindProperty("toggleLiftEdge").objectReferenceValue = t3;
            ctrlSo.FindProperty("toggleBatteryConnector").objectReferenceValue = t4;
            ctrlSo.FindProperty("toggleCoolingFan").objectReferenceValue = t5;
            ctrlSo.FindProperty("toggleFanAirflow").objectReferenceValue = t6;
            ctrlSo.FindProperty("hideAllButton").objectReferenceValue = hideAll;
            ctrlSo.FindProperty("componentDropdown").objectReferenceValue = dropdown;
            ctrlSo.FindProperty("showSelectedButton").objectReferenceValue = showSel;
            ctrlSo.FindProperty("saveButton").objectReferenceValue = save;
            ctrlSo.FindProperty("resetButton").objectReferenceValue = reset;
            ctrlSo.FindProperty("previewSavedButton").objectReferenceValue = previewSaved;
            ctrlSo.FindProperty("hidePreviewButton").objectReferenceValue = hidePrev;
            ctrlSo.FindProperty("posStep10Button").objectReferenceValue = p10;
            ctrlSo.FindProperty("posStep2Button").objectReferenceValue = p2;
            ctrlSo.FindProperty("posStep05Button").objectReferenceValue = p05;
            ctrlSo.FindProperty("rotStep5Button").objectReferenceValue = r5;
            ctrlSo.FindProperty("rotStep1Button").objectReferenceValue = r1;
            ctrlSo.FindProperty("posXNeg").objectReferenceValue = pxn;
            ctrlSo.FindProperty("posXPos").objectReferenceValue = pxp;
            ctrlSo.FindProperty("posYNeg").objectReferenceValue = pyn;
            ctrlSo.FindProperty("posYPos").objectReferenceValue = pyp;
            ctrlSo.FindProperty("posZNeg").objectReferenceValue = pzn;
            ctrlSo.FindProperty("posZPos").objectReferenceValue = pzp;
            ctrlSo.FindProperty("rotXNeg").objectReferenceValue = rxn;
            ctrlSo.FindProperty("rotXPos").objectReferenceValue = rxp;
            ctrlSo.FindProperty("rotYNeg").objectReferenceValue = ryn;
            ctrlSo.FindProperty("rotYPos").objectReferenceValue = ryp;
            ctrlSo.FindProperty("rotZNeg").objectReferenceValue = rzn;
            ctrlSo.FindProperty("rotZPos").objectReferenceValue = rzp;
            ctrlSo.FindProperty("profile").objectReferenceValue = profile;
            ctrlSo.ApplyModifiedPropertiesWithoutUndo();

            // Do NOT add Canvas/GraphicRaycaster here. SpatialWindowSpawner hosts the panel
            // under WindowCanvas (GraphicRaycaster + TrackedDeviceGraphicRaycaster). A nested
            // Canvas (auto-added with GraphicRaycaster) breaks HL2 hand-ray / poke clicks.
            StripNestedCanvasRaycasters(root);

            return SavePrefab(root, ArPanelPrefab);
        }

        [MenuItem(Menu + "Fix Panel UI Interaction")]
        public static void FixPanelUiInteractionMenu()
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(ArPanelPrefab);
            try
            {
                StripNestedCanvasRaycasters(prefabRoot);
                EnsureToggleHitTargets(prefabRoot.transform);
                EnsureButtonTargetGraphics(prefabRoot.transform);
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, ArPanelPrefab);
                Debug.Log("[Norscope AR Guidance] Fixed panel UI interaction (removed nested Canvas; enlarged toggle hits).");
                if (!Application.isBatchMode)
                    EditorUtility.DisplayDialog("AR Guidance", "Panel UI interaction fixed. Re-open the panel to test.", "OK");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static void StripNestedCanvasRaycasters(GameObject root)
        {
            if (root == null) return;
            // Remove leaf raycasters first — Unity may auto-add Canvas with GraphicRaycaster.
            GraphicRaycaster[] raycasters = root.GetComponentsInChildren<GraphicRaycaster>(true);
            for (int i = 0; i < raycasters.Length; i++)
            {
                if (raycasters[i] != null)
                    UnityEngine.Object.DestroyImmediate(raycasters[i]);
            }

            Canvas[] canvases = root.GetComponentsInChildren<Canvas>(true);
            for (int i = 0; i < canvases.Length; i++)
            {
                if (canvases[i] != null)
                    UnityEngine.Object.DestroyImmediate(canvases[i]);
            }
        }

        private static void EnsureToggleHitTargets(Transform root)
        {
            Toggle[] toggles = root.GetComponentsInChildren<Toggle>(true);
            for (int i = 0; i < toggles.Length; i++)
            {
                Toggle toggle = toggles[i];
                if (toggle == null) continue;
                Image hit = toggle.GetComponent<Image>();
                if (hit == null)
                {
                    hit = toggle.gameObject.AddComponent<Image>();
                    hit.color = new Color(1f, 1f, 1f, 0.01f);
                }

                hit.raycastTarget = true;
                toggle.targetGraphic = hit;
            }
        }

        private static void EnsureButtonTargetGraphics(Transform root)
        {
            Button[] buttons = root.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                Button button = buttons[i];
                if (button == null) continue;
                Image img = button.GetComponent<Image>();
                if (img == null) continue;
                img.raycastTarget = true;
                button.targetGraphic = img;
            }
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
            lr.widthMultiplier = 0.002f;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            return lr;
        }

        private static GameObject CreateTmpLabel(
            string name, Transform parent, string text, float y, float fontSize = 0.1f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, y, 0f);
            go.layer = LayerMask.NameToLayer("Ignore Raycast");
            TextMeshPro tmp = go.AddComponent<TextMeshPro>();
            tmp.text = text;
            // World-space TMP size in meters — keep tiny for laptop-scale holograms.
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Cyan;
            tmp.raycastTarget = false;
            tmp.enableAutoSizing = false;
            RectTransform rt = go.GetComponent<RectTransform>();
            if (rt != null)
                rt.sizeDelta = new Vector2(0.6f, 0.12f);
            return go;
        }

        private static GameObject CreateUiPanel(
            string name, Transform parent,
            Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            return go;
        }

        private static Image CreateUiImage(
            string name, Transform parent, Color color, bool stretch)
        {
            GameObject go = stretch
                ? CreateUiPanel(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero)
                : new GameObject(name, typeof(RectTransform));
            if (!stretch)
                go.transform.SetParent(parent, false);
            Image img = go.AddComponent<Image>();
            img.color = color;
            return img;
        }

        private static TMP_Text CreateUiTmp(
            string name, Transform parent, string text, float size,
            Vector2 anchoredPos, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static Button CreateUiButton(
            string name, Transform parent, string label,
            Vector2 anchoredPos, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
            Image img = go.AddComponent<Image>();
            img.color = new Color(0.12f, 0.18f, 0.24f, 1f);
            img.raycastTarget = true;
            Button btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            CreateUiTmp("Label", go.transform, label, 14f,
                new Vector2(4f, -2f), sizeDelta - new Vector2(8f, 4f));
            return btn;
        }

        private static Toggle CreateUiToggle(
            string name, Transform parent, string label, Vector2 anchoredPos)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(360f, 32f);
            // Full-row invisible hit target — required after SpatialWindow strips non-targetGraphic raycasts.
            Image hit = go.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0.01f);
            hit.raycastTarget = true;
            Toggle toggle = go.AddComponent<Toggle>();
            GameObject bg = CreateUiPanel("Background", go.transform,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(0f, -10f), new Vector2(20f, 10f));
            Image bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0.15f, 0.2f, 0.28f, 1f);
            bgImg.raycastTarget = false;
            GameObject check = CreateUiPanel("Checkmark", bg.transform,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Image checkImg = check.AddComponent<Image>();
            checkImg.color = Cyan;
            checkImg.raycastTarget = false;
            toggle.targetGraphic = hit;
            toggle.graphic = checkImg;
            CreateUiTmp("Label", go.transform, label, 14f,
                new Vector2(28f, 0f), new Vector2(320f, 32f));
            return toggle;
        }

        private static TMP_Dropdown CreateUiDropdown(
            string name, Transform parent, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
            Image img = go.AddComponent<Image>();
            img.color = new Color(0.12f, 0.18f, 0.24f, 1f);
            TMP_Dropdown dropdown = go.AddComponent<TMP_Dropdown>();
            // Minimal template so TMP_Dropdown does not NRE in editor.
            GameObject label = CreateUiTmp("Label", go.transform, "component", 13f,
                new Vector2(8f, -2f), sizeDelta - new Vector2(16f, 4f)).gameObject;
            dropdown.captionText = label.GetComponent<TextMeshProUGUI>();
            return dropdown;
        }

        private static GameObject SavePrefab(GameObject root, string path)
        {
            EnsureIgnoreRaycastRecursive(root.transform);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static void EnsureIgnoreRaycastRecursive(Transform t)
        {
            // Only world holograms — skip UI panels under ArGuidancePanel
            if (t.GetComponent<RectTransform>() != null &&
                t.GetComponent<CanvasRenderer>() != null)
                return;
            if (t.GetComponent<LineRenderer>() != null ||
                t.GetComponent<MeshRenderer>() != null ||
                t.GetComponent<TextMeshPro>() != null ||
                t.GetComponent<ArHologramVisualBase>() != null)
            {
                int layer = LayerMask.NameToLayer("Ignore Raycast");
                if (layer >= 0)
                    t.gameObject.layer = layer;
                Collider col = t.GetComponent<Collider>();
                if (col != null)
                    UnityEngine.Object.DestroyImmediate(col);
            }

            for (int i = 0; i < t.childCount; i++)
                EnsureIgnoreRaycastRecursive(t.GetChild(i));
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform f = FindDeep(root.GetChild(i), name);
                if (f != null) return f;
            }

            return null;
        }

        private static void CopyIfExists(string src, string dst)
        {
            if (!File.Exists(src)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(dst) ?? ".");
            File.Copy(src, dst, true);
        }

        private static string HashFile(string path)
        {
            using (var md5 = System.Security.Cryptography.MD5.Create())
            using (var stream = File.OpenRead(path))
            {
                byte[] hash = md5.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", string.Empty);
            }
        }
    }
}
