using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Norscope.ArGuidance;
using Norscope.TaskExecution;
using Norscope.TaskLibrary;
using Norscope.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Norscope.Editor
{
    /// <summary>
    /// AR Guidance — Battery Replacement Task Integration Apply / Validate.
    /// </summary>
    public static class PhaseArGuidanceBatteryTaskIntegrationConfigurator
    {
        private const string Menu = "Norscope/HoloLens/AR Guidance/Battery Task Integration/";
        private const string BackupKey = "Norscope.ArGuidanceBatteryTaskIntegration.Backup";
        private const string DryRunApprovedKey =
            "Norscope.ArGuidanceBatteryTaskIntegration.DryRunApproved";
        private const string BackupRoot = "Assets/Norscope/UI/Panels/_Backups";
        private const string DryRunReportPath =
            "Temp/NorscopeArGuidanceBatteryTaskIntegrationDryRun.txt";
        private const string ApplyReportPath =
            "Temp/NorscopeArGuidanceBatteryTaskIntegrationApply.txt";
        private const string ValidateReportPath =
            "Temp/NorscopeArGuidanceBatteryTaskIntegrationValidate.txt";

        private const string TaskAsset = "Assets/Norscope/Data/Tasks/Replace Battery.asset";
        private const string LibraryAsset =
            "Assets/Norscope/Data/ArGuidance/ArHologramLibrary_ReplaceBattery.asset";
        private const string MapAsset =
            "Assets/Norscope/Data/ArGuidance/ArTaskGuidanceMap_ReplaceBattery.asset";
        private const string ProfileAsset =
            "Assets/Norscope/Data/ArGuidance/ArGuidanceProfile_DellPrecision3570.asset";
        private const string PanelPrefab = "Assets/Norscope/UI/Panels/ArGuidancePanel.prefab";
        private const string BootstrapScene = "Assets/Norscope/Scenes/Bootstrap.unity";
        private const string IconSrcFolder = "Norscope icons";
        private const string IconDstFolder = "Assets/Norscope/UI/Icons/ArGuidance";
        private const string BatteryPrefabFolder = "Assets/Norscope/UI/Panels/ArGuidance/Battery";

        private const string ThemePath = "Assets/Norscope/UI/DesignSystem/Themes/NorscopeTheme.asset";
        private const string RoundedSpritePath = "Assets/Norscope/UI/DesignSystem/Sprites/UIRounded_8.png";
        private static readonly Color Btn = new Color(0.12f, 0.18f, 0.24f, 1f);
        private static readonly Color Cyan = new Color(0f, 0.85f, 1f, 0.95f);

        [MenuItem(Menu + "1) Capture Baseline")]
        public static void CaptureBaselineMenu()
        {
            string folder = BackupRoot + "/ArGuidanceBatteryTaskIntegration_" +
                            DateTime.Now.ToString("yyyyMMdd_HHmmss");
            Directory.CreateDirectory(folder);
            CopyIfExists(TaskAsset, folder + "/Replace Battery.asset");
            CopyIfExists(LibraryAsset, folder + "/ArHologramLibrary_ReplaceBattery.asset");
            CopyIfExists(PanelPrefab, folder + "/ArGuidancePanel.prefab");
            CopyIfExists(BootstrapScene, folder + "/Bootstrap.unity");
            CopyIfExists("Assets/Norscope/Runtime/TaskLibrary/TaskStep.cs",
                folder + "/TaskStep.cs.bak");
            CopyIfExists("Assets/Norscope/Runtime/TaskExecution/ActiveTaskSession.cs",
                folder + "/ActiveTaskSession.cs.bak");
            EditorPrefs.SetString(BackupKey, folder);
            Debug.Log("[Norscope Battery Task Integration] Baseline → " + folder);
            if (!Application.isBatchMode)
                EditorUtility.DisplayDialog("Battery Task Integration", "Baseline captured.", "OK");
        }

        [MenuItem(Menu + "2) Dry Run")]
        public static void DryRunMenu()
        {
            string report = File.Exists(DryRunReportPath)
                ? File.ReadAllText(DryRunReportPath)
                : "Dry Run report missing.";
            Debug.Log("[Norscope Battery Task Integration] DRY RUN\n" + report);
            if (!Application.isBatchMode)
                EditorUtility.DisplayDialog("Battery Task Integration Dry Run",
                    "See Temp dry-run report. Apply requires approval.", "OK");
        }

        [MenuItem(Menu + "3) Apply")]
        public static void ApplyMenu()
        {
            try
            {
                ApproveDryRunForApply();
                ApplyInternal();
            }
            catch (Exception ex)
            {
                Debug.LogError("[Norscope Battery Task Integration] Apply FAILED:\n" + ex);
                if (!Application.isBatchMode)
                    EditorUtility.DisplayDialog("Apply FAILED", ex.Message, "OK");
            }
        }

        [MenuItem(Menu + "4) Validate")]
        public static void ValidateMenu()
        {
            bool pass = Validate(out string report);
            Directory.CreateDirectory(Path.GetFullPath("Temp"));
            File.WriteAllText(Path.GetFullPath(ValidateReportPath), report);
            Debug.Log(pass
                ? "[Norscope Battery Task Integration] Validate PASS\n" + report
                : "[Norscope Battery Task Integration] Validate FAIL\n" + report);
            if (!Application.isBatchMode)
                EditorUtility.DisplayDialog("Validate", pass ? "PASS" : "FAIL", "OK");
        }

        [MenuItem(Menu + "5) Restore Baseline")]
        public static void RestoreMenu()
        {
            string folder = EditorPrefs.GetString(BackupKey, string.Empty);
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                Debug.LogWarning("[Norscope Battery Task Integration] No baseline.");
                return;
            }

            CopyIfExists(folder + "/Replace Battery.asset", TaskAsset);
            CopyIfExists(folder + "/ArGuidancePanel.prefab", PanelPrefab);
            CopyIfExists(folder + "/Bootstrap.unity", BootstrapScene);
            AssetDatabase.Refresh();
            Debug.Log("[Norscope Battery Task Integration] Restored from " + folder);
        }

        public static void ApproveDryRunForApply()
        {
            EditorPrefs.SetBool(DryRunApprovedKey, true);
        }

        public static void CaptureApplyValidate()
        {
            if (string.IsNullOrEmpty(EditorPrefs.GetString(BackupKey, string.Empty)))
                CaptureBaselineMenu();
            ApproveDryRunForApply();
            ApplyInternal();
            if (!Validate(out string report))
                throw new Exception(report);
            File.WriteAllText(Path.GetFullPath(ValidateReportPath), report);
        }

        private static void ApplyInternal()
        {
            Directory.CreateDirectory(Path.GetFullPath("Temp"));
            ImportIcons();
            AuthorTaskGuidanceKeys();
            CreateOrUpdateGuidanceMap();
            AssignPrefabLabelsAndIcons();
            WireArGuidancePanelPreviewControls();
            WireBootstrap();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var log = new StringBuilder();
            log.AppendLine("=== Battery Task Integration Apply ===");
            log.AppendLine(DateTime.Now.ToString("o"));
            log.AppendLine("guidanceKey authored on Replace Battery");
            log.AppendLine("Map: " + MapAsset);
            log.AppendLine("Icons copied to: " + IconDstFolder);
            log.AppendLine("Preview Guidance controls on ArGuidancePanel");
            log.AppendLine("ArVisibilityController + ArTaskGuidanceController wired");
            log.AppendLine("No QR / no calibration schema / no final coordinates");
            File.WriteAllText(Path.GetFullPath(ApplyReportPath), log.ToString());
            Debug.Log("[Norscope Battery Task Integration] Apply COMPLETED\n" + log);
            if (!Application.isBatchMode)
                EditorUtility.DisplayDialog("Apply", "COMPLETED", "OK");
        }

        private static void ImportIcons()
        {
            Directory.CreateDirectory(IconDstFolder);
            if (!Directory.Exists(IconSrcFolder))
            {
                Debug.LogWarning("[Battery Task Integration] Icon source folder missing: " + IconSrcFolder);
                return;
            }

            string[] names =
            {
                "power.png", "ac plugs.png", "screw.png", "up-arrow.png", "down-arrow.png",
                "battery-charge.png", "half-battery.png", "battery danger.png", "check.png"
            };
            for (int i = 0; i < names.Length; i++)
            {
                string src = Path.Combine(IconSrcFolder, names[i]);
                if (!File.Exists(src))
                    continue;
                string safe = names[i].Replace(' ', '_');
                string dst = IconDstFolder + "/" + safe;
                File.Copy(src, dst, true);
            }

            AssetDatabase.Refresh();
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { IconDstFolder });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                    continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
        }

        private static Sprite LoadIcon(string fileName)
        {
            string path = IconDstFolder + "/" + fileName;
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void AuthorTaskGuidanceKeys()
        {
            var task = AssetDatabase.LoadAssetAtPath<TaskDefinition>(TaskAsset);
            if (task == null)
                throw new Exception("Replace Battery task missing");
            SerializedObject so = new SerializedObject(task);
            SerializedProperty steps = so.FindProperty("steps");
            string[] keys =
            {
                "power_down_fully",
                "remove_back_panel",
                "disconnect_old_battery",
                "install_new_battery",
                "test_new_battery"
            };
            if (steps.arraySize != keys.Length)
                throw new Exception("Expected 5 Replace Battery steps, found " + steps.arraySize);
            for (int i = 0; i < keys.Length; i++)
            {
                SerializedProperty step = steps.GetArrayElementAtIndex(i);
                step.FindPropertyRelative("guidanceKey").stringValue = keys[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(task);
        }

        private static void CreateOrUpdateGuidanceMap()
        {
            ArTaskGuidanceMap map = AssetDatabase.LoadAssetAtPath<ArTaskGuidanceMap>(MapAsset);
            if (map == null)
            {
                map = ScriptableObject.CreateInstance<ArTaskGuidanceMap>();
                AssetDatabase.CreateAsset(map, MapAsset);
            }

            map.taskId = "replace-battery";
            map.stages = new[]
            {
                Stage("power_down_fully", 0,
                    E("h02_disconnect_ac", 0, ArHologramVisualState.Warning, "Disconnect AC Adapter")),
                Stage("remove_back_panel", 1,
                    E("h03_back_panel_outline", 0, ArHologramVisualState.Default, null),
                    // H04 is 9 independent screw entries now (no shared group hologramId) —
                    // list all 9 so this stage still shows every screw during the real task.
                    E("h04_screw_01", 1, ArHologramVisualState.Default, null),
                    E("h04_screw_02", 2, ArHologramVisualState.Default, null),
                    E("h04_screw_03", 3, ArHologramVisualState.Default, null),
                    E("h04_screw_04", 4, ArHologramVisualState.Default, null),
                    E("h04_screw_05", 5, ArHologramVisualState.Default, null),
                    E("h04_screw_06", 6, ArHologramVisualState.Default, null),
                    E("h04_screw_07", 7, ArHologramVisualState.Default, null),
                    E("h04_screw_08", 8, ArHologramVisualState.Default, null),
                    E("h04_screw_09", 9, ArHologramVisualState.Default, null)),
                Stage("disconnect_old_battery", 2,
                    E("h17_battery_outline", 0, ArHologramVisualState.Default, null),
                    E("h18_battery_connector", 1, ArHologramVisualState.Warning, "Disconnect Battery First"),
                    E("h19_screw_01", 2, ArHologramVisualState.Default, null),
                    E("h19_screw_02", 3, ArHologramVisualState.Default, null),
                    E("h19_screw_03", 4, ArHologramVisualState.Default, null),
                    E("h19_screw_04", 5, ArHologramVisualState.Default, null),
                    E("h20_swollen_battery_warning", 6, ArHologramVisualState.Danger,
                        "Battery appears swollen.\nDo NOT puncture.")),
                Stage("install_new_battery", 3,
                    E("h21_install_new_battery", 0, ArHologramVisualState.Default, "Install New Battery")),
                Stage("test_new_battery", 4,
                    E("h07_replace_back_cover", 0, ArHologramVisualState.Default, "Replace Back Cover"),
                    E("h08_power_on", 1, ArHologramVisualState.Completed, "Power On"),
                    E("h22_battery_verification", 2, ArHologramVisualState.Completed,
                        "Verify battery is detected and charging\nConfirm charging status"))
            };
            EditorUtility.SetDirty(map);
            AssetDatabase.SaveAssets();
        }

        private static ArTaskGuidanceStage Stage(string key, int order, params ArTaskGuidanceEntry[] entries)
        {
            return new ArTaskGuidanceStage
            {
                guidanceKey = key,
                displayOrder = order,
                entries = entries
            };
        }

        private static ArTaskGuidanceEntry E(
            string id, int order, ArHologramVisualState state, string label)
        {
            return new ArTaskGuidanceEntry
            {
                hologramId = id,
                visible = true,
                visualState = state,
                labelOverride = label ?? string.Empty,
                animationHint = ArGuidanceAnimationHint.Fade,
                displayOrder = order
            };
        }

        private static void AssignPrefabLabelsAndIcons()
        {
            Sprite power = LoadIcon("power.png");
            Sprite ac = LoadIcon("ac_plugs.png");
            Sprite up = LoadIcon("up-arrow.png");
            Sprite down = LoadIcon("down-arrow.png");
            Sprite batDanger = LoadIcon("battery_danger.png");
            Sprite check = LoadIcon("check.png");
            Sprite charge = LoadIcon("battery-charge.png");

            SetVisual(BatteryPrefabFolder + "/H02_DisconnectAC.prefab",
                "Disconnect AC Adapter", ArHologramVisualState.Warning, ac);
            // H05/H08/H18/H20 labels+states match Visual Redesign defaults. Icons stay as
            // disabled fallbacks on those prefabs (SetVisual still assigns sprite refs).
            SetVisual(BatteryPrefabFolder + "/H05_LiftHere.prefab",
                "LIFT HERE", ArHologramVisualState.Default, up);
            SetVisual(BatteryPrefabFolder + "/H07_ReplaceBackCover.prefab",
                "Replace Back Cover", ArHologramVisualState.Default, down);
            SetVisual(BatteryPrefabFolder + "/H08_PowerOn.prefab",
                "PRESS POWER", ArHologramVisualState.Default, power);
            SetVisual(BatteryPrefabFolder + "/H18_BatteryConnector.prefab",
                "DISCONNECT", ArHologramVisualState.Default, null);
            SetVisual(BatteryPrefabFolder + "/H20_SwollenBatteryWarning.prefab",
                "CHECK FOR SWELLING", ArHologramVisualState.Warning, batDanger);
            SetVisual(BatteryPrefabFolder + "/H21_InstallNewBattery.prefab",
                "Install New Battery", ArHologramVisualState.Default, null);
            SetVisual(BatteryPrefabFolder + "/H22_BatteryVerification.prefab",
                "Verify battery is detected and charging\nConfirm charging status",
                ArHologramVisualState.Completed, check != null ? check : charge);
        }

        private static void SetVisual(
            string prefabPath, string label, ArHologramVisualState state, Sprite icon)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                ArHologramVisualBase visual = root.GetComponent<ArHologramVisualBase>() ??
                                             root.GetComponentInChildren<ArHologramVisualBase>(true);
                if (visual == null)
                    return;
                SerializedObject so = new SerializedObject(visual);
                so.FindProperty("labelText").stringValue = label ?? string.Empty;
                so.FindProperty("authoredVisualState").enumValueIndex = (int)state;
                so.FindProperty("startVisible").boolValue = false;
                if (icon != null && so.FindProperty("icon") != null)
                    so.FindProperty("icon").objectReferenceValue = icon;
                // Ensure SpriteRenderer child for icon display when sprite assigned.
                if (icon != null)
                {
                    SpriteRenderer sr = root.GetComponentInChildren<SpriteRenderer>(true);
                    if (sr == null)
                    {
                        var go = new GameObject("IconSprite", typeof(SpriteRenderer));
                        go.transform.SetParent(root.transform, false);
                        go.transform.localPosition = new Vector3(0f, 0.02f, 0f);
                        go.transform.localScale = Vector3.one * 0.08f;
                        sr = go.GetComponent<SpriteRenderer>();
                    }

                    sr.sprite = icon;
                    sr.color = Color.white;
                    // Redesigned H05/H08/H20 keep PNG as disabled fallback only.
                    bool redesignFallback =
                        root.GetComponent<ArDirectionalChevronVisual>() != null ||
                        root.GetComponent<ArTargetRingVisual>() != null ||
                        root.GetComponent<ArInspectionWarningVisual>() != null;
                    if (redesignFallback)
                    {
                        sr.enabled = false;
                        so.FindProperty("icon").objectReferenceValue = null;
                        so.FindProperty("iconRenderer").objectReferenceValue = null;
                        SerializedProperty fallbackProp = so.FindProperty("fallbackIcon");
                        if (fallbackProp != null)
                            fallbackProp.objectReferenceValue = sr;
                    }
                    else
                    {
                        so.FindProperty("iconRenderer").objectReferenceValue = sr;
                    }
                }

                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>Rebuilds PreviewGuidanceRow with non-overlapping task-preview controls.</summary>
        public static void RefreshPreviewGuidanceRow()
        {
            WireArGuidancePanelPreviewControls();
        }

        /// <summary>Batch entry: rebuild PreviewGuidanceRow then exit.</summary>
        public static void RefreshPreviewGuidanceRowBatch()
        {
            try
            {
                RefreshPreviewGuidanceRow();
                Debug.Log("[Norscope Battery Task Integration] PreviewGuidanceRow refresh PASS");
                if (Application.isBatchMode)
                    EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError("[Norscope Battery Task Integration] PreviewGuidanceRow refresh FAILED:\n" + ex);
                if (Application.isBatchMode)
                    EditorApplication.Exit(1);
                throw;
            }
        }

        private static void WireArGuidancePanelPreviewControls()
        {
            NorscopeTheme theme = AssetDatabase.LoadAssetAtPath<NorscopeTheme>(ThemePath);
            Sprite rounded = AssetDatabase.LoadAssetAtPath<Sprite>(RoundedSpritePath);
            Color secondaryText = theme != null ? theme.secondaryText : new Color(0.63f, 0.66f, 0.70f, 1f);

            GameObject root = PrefabUtility.LoadPrefabContents(PanelPrefab);
            try
            {
                Transform preview = FindDeep(root.transform, "PreviewView");
                if (preview == null)
                    throw new Exception("PreviewView missing");

                Transform existing = FindDeep(preview, "PreviewGuidanceRow");
                if (existing != null)
                    UnityEngine.Object.DestroyImmediate(existing.gameObject);

                // developer.zip PreviewTab → Task Preview block (below card grid, no overlap).
                var row = new GameObject("PreviewGuidanceRow", typeof(RectTransform));
                row.transform.SetParent(preview, false);
                RectTransform rt = row.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(1f, 0f);
                rt.pivot = new Vector2(0.5f, 0f);
                rt.sizeDelta = new Vector2(-32f, 132f);
                rt.anchoredPosition = new Vector2(0f, 8f);
                var vlg = row.AddComponent<VerticalLayoutGroup>();
                vlg.spacing = 0f;
                vlg.padding = new RectOffset(0, 0, 0, 0);
                vlg.childControlHeight = true;
                vlg.childControlWidth = true;
                vlg.childForceExpandWidth = true;

                GameObject card = new GameObject("TaskPreviewCard", typeof(RectTransform));
                card.transform.SetParent(row.transform, false);
                Image cardBg = card.AddComponent<Image>();
                cardBg.sprite = rounded;
                cardBg.type = Image.Type.Sliced;
                cardBg.color = theme != null ? theme.elevatedPanelBackground : new Color(0.10f, 0.14f, 0.18f, 0.8f);
                LayoutElement cardLe = card.AddComponent<LayoutElement>();
                cardLe.minHeight = 124f;
                cardLe.preferredHeight = 124f;
                var cardVlg = card.AddComponent<VerticalLayoutGroup>();
                cardVlg.spacing = 10f;
                cardVlg.padding = new RectOffset(18, 18, 14, 14);
                cardVlg.childControlHeight = true;
                cardVlg.childControlWidth = true;
                cardVlg.childForceExpandWidth = true;
                cardVlg.childForceExpandHeight = false;

                var header = new GameObject("Header", typeof(RectTransform));
                header.transform.SetParent(card.transform, false);
                var headerH = header.AddComponent<HorizontalLayoutGroup>();
                headerH.childAlignment = TextAnchor.MiddleLeft;
                headerH.childControlWidth = true;
                headerH.childControlHeight = true;
                headerH.childForceExpandWidth = false;
                headerH.spacing = 8f;
                LayoutElement headerLe = header.AddComponent<LayoutElement>();
                headerLe.minHeight = 22f;
                headerLe.preferredHeight = 22f;

                TMP_Text caption = CreateTmp("TaskPreviewCaption", header.transform, "TASK PREVIEW", 11f, 18f);
                caption.color = theme != null ? theme.disabled : new Color(0.40f, 0.44f, 0.52f, 1f);
                caption.fontStyle = FontStyles.Bold;
                LayoutElement captionLe = caption.GetComponent<LayoutElement>();
                captionLe.flexibleWidth = 1f;

                TMP_Text status = CreateTmp("PreviewGuidanceStatus", header.transform, "Preview Disabled", 11f, 18f);
                status.color = secondaryText;
                status.alignment = TextAlignmentOptions.Center;
                LayoutElement statusLe = status.GetComponent<LayoutElement>();
                statusLe.flexibleWidth = 0f;
                statusLe.minWidth = 120f;
                statusLe.preferredWidth = 140f;

                var btnRow = new GameObject("Buttons", typeof(RectTransform));
                btnRow.transform.SetParent(card.transform, false);
                var h = btnRow.AddComponent<HorizontalLayoutGroup>();
                h.spacing = 8f;
                h.childControlWidth = true;
                h.childForceExpandWidth = true;
                LayoutElement ble = btnRow.AddComponent<LayoutElement>();
                ble.minHeight = 40f;
                ble.preferredHeight = 40f;

                // Order matches developer.zip: Previous · Next · Start · Stop
                Color darkBtn = new Color(1f, 1f, 1f, 0.03f);
                Button prev = CreateIconLabelButton("PreviewGuidancePrev", btnRow.transform, "◁◁", "Previous",
                    rounded, darkBtn, secondaryText);
                Button next = CreateIconLabelButton("PreviewGuidanceNext", btnRow.transform, "▷▷", "Next",
                    rounded, darkBtn, secondaryText);
                Button start = CreateIconLabelButton("PreviewGuidanceStart", btnRow.transform, "▷", "Start",
                    rounded, darkBtn, secondaryText);
                Button stop = CreateIconLabelButton("PreviewGuidanceStop", btnRow.transform, "◻", "Stop",
                    rounded, darkBtn, secondaryText);

                ArGuidancePanelController panel = root.GetComponent<ArGuidancePanelController>();
                SerializedObject pso = new SerializedObject(panel);
                pso.FindProperty("previewGuidanceStartButton").objectReferenceValue = start;
                pso.FindProperty("previewGuidancePrevButton").objectReferenceValue = prev;
                pso.FindProperty("previewGuidanceNextButton").objectReferenceValue = next;
                pso.FindProperty("previewGuidanceStopButton").objectReferenceValue = stop;
                pso.FindProperty("previewGuidanceStatusText").objectReferenceValue = status;
                pso.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PanelPrefab);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void WireBootstrap()
        {
            var scene = EditorSceneManager.OpenScene(BootstrapScene, OpenSceneMode.Single);
            GameObject systemGo = GameObject.Find("ArGuidanceSystem");
            if (systemGo == null)
            {
                systemGo = new GameObject("ArGuidanceSystem");
                systemGo.AddComponent<ArGuidanceSystem>();
            }

            ArGuidanceSystem system = systemGo.GetComponent<ArGuidanceSystem>() ??
                                      systemGo.AddComponent<ArGuidanceSystem>();
            if (systemGo.GetComponent<ArVisibilityController>() == null)
                systemGo.AddComponent<ArVisibilityController>();
            ArTaskGuidanceController tg = systemGo.GetComponent<ArTaskGuidanceController>() ??
                                          systemGo.AddComponent<ArTaskGuidanceController>();
            ActiveTaskSession session = UnityEngine.Object.FindFirstObjectByType<ActiveTaskSession>();
            ArTaskGuidanceMap map = AssetDatabase.LoadAssetAtPath<ArTaskGuidanceMap>(MapAsset);
            ArGuidanceProfile profile = AssetDatabase.LoadAssetAtPath<ArGuidanceProfile>(ProfileAsset);

            SerializedObject so = new SerializedObject(system);
            if (so.FindProperty("profile") != null)
                so.FindProperty("profile").objectReferenceValue = profile;
            if (so.FindProperty("taskGuidanceController") != null)
                so.FindProperty("taskGuidanceController").objectReferenceValue = tg;
            if (so.FindProperty("taskGuidanceMap") != null)
                so.FindProperty("taskGuidanceMap").objectReferenceValue = map;
            if (so.FindProperty("taskSession") != null)
                so.FindProperty("taskSession").objectReferenceValue = session;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
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

            sb.AppendLine("=== Battery Task Integration Validate ===");
            sb.AppendLine(DateTime.Now.ToString("o"));

            Check(File.Exists("Assets/Norscope/Runtime/ArGuidance/TaskIntegration/ArTaskGuidanceMap.cs"), "Map types");
            Check(File.Exists("Assets/Norscope/Runtime/ArGuidance/TaskIntegration/ArTaskGuidanceController.cs"), "Controller");
            Check(File.Exists("Assets/Norscope/Runtime/ArGuidance/TaskIntegration/ArVisibilityController.cs"), "VisibilityController");
            Check(File.Exists(MapAsset), "Map asset");

            var task = AssetDatabase.LoadAssetAtPath<TaskDefinition>(TaskAsset);
            Check(task != null && task.TaskId == "replace-battery", "Task id replace-battery");
            Check(task != null && task.Steps != null && task.Steps.Count == 5, "5 steps");
            if (task != null && task.Steps != null && task.Steps.Count == 5)
            {
                Check(task.Steps[0].guidanceKey == "power_down_fully", "guidanceKey step0");
                Check(task.Steps[1].guidanceKey == "remove_back_panel", "guidanceKey step1");
                Check(task.Steps[2].guidanceKey == "disconnect_old_battery", "guidanceKey step2");
                Check(task.Steps[3].guidanceKey == "install_new_battery", "guidanceKey step3");
                Check(task.Steps[4].guidanceKey == "test_new_battery", "guidanceKey step4");
            }

            string sessionSrc = File.ReadAllText(
                "Assets/Norscope/Runtime/TaskExecution/ActiveTaskSession.cs");
            Check(sessionSrc.Contains("event Action StepChanged"), "StepChanged event");

            string baseSrc = File.ReadAllText(
                "Assets/Norscope/Runtime/ArGuidance/Visuals/ArHologramVisualBase.cs");
            Check(baseSrc.Contains("SerializeField] private Sprite icon"), "Editable icon");
            Check(baseSrc.Contains("SetOpacity"), "Editable opacity API");
            Check(baseSrc.Contains("SetLabelText"), "Editable label API");
            Check(baseSrc.Contains("authoredVisualState"), "Editable state");
            Check(baseSrc.Contains("animationEnabled"), "Editable animation");
            Check(baseSrc.Contains("startVisible"), "Editable visibility");

            ArTaskGuidanceMap map = AssetDatabase.LoadAssetAtPath<ArTaskGuidanceMap>(MapAsset);
            Check(map != null && map.taskId == "replace-battery", "Map taskId");
            Check(map != null && map.StageCount == 5, "Map 5 stages");
            ArHologramLibrary lib = AssetDatabase.LoadAssetAtPath<ArHologramLibrary>(LibraryAsset);
            if (map != null && lib != null)
            {
                for (int s = 0; s < map.stages.Length; s++)
                {
                    ArTaskGuidanceStage stage = map.stages[s];
                    if (stage?.entries == null) continue;
                    for (int i = 0; i < stage.entries.Length; i++)
                    {
                        string id = stage.entries[i].hologramId;
                        Check(lib.Find(id) != null, "Library has " + id);
                    }
                }
            }

            Check(File.Exists(IconDstFolder + "/power.png"), "Icon power imported");
            Check(File.Exists(IconDstFolder + "/battery_danger.png"), "Icon battery_danger imported");
            Check(!File.ReadAllText(
                    "Assets/Norscope/Runtime/ArGuidance/Visuals/ArVerificationVisual.cs")
                .Contains("Battery Detected\\nCharging Normal"), "H22 honest default label");

            GameObject panel = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPrefab);
            Check(panel != null && FindDeep(panel.transform, "PreviewGuidanceRow") != null,
                "Preview Guidance UI");
            Check(File.Exists("Assets/Norscope/Runtime/ArGuidance/QrTrackingOriginProvider.cs"),
                "QR stub untouched path");
            Check(File.Exists("Assets/Norscope/Runtime/ArGuidance/JsonArCalibrationRepository.cs"),
                "Calibration repo untouched path");

            string qrSrc = File.ReadAllText(
                "Assets/Norscope/Runtime/ArGuidance/QrTrackingOriginProvider.cs");
            Check(qrSrc.Contains("Phase 1 stub") || qrSrc.Contains("No QR detection"),
                "QR still stub");

            sb.AppendLine();
            sb.AppendLine("No final coordinates authored");
            sb.AppendLine("No QR implementation added");
            sb.AppendLine("Calibration schema unchanged");
            sb.AppendLine(pass ? "STATUS PASS" : "STATUS FAIL");
            report = sb.ToString();
            return pass;
        }

        private static Button CreateButton(string name, Transform parent, string label, float width)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Image img = go.AddComponent<Image>();
            img.color = Btn;
            Button btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.minWidth = width;
            le.preferredWidth = width;
            le.minHeight = 32f;
            le.preferredHeight = 32f;
            TMP_Text t = CreateTmp("Label", go.transform, label, 13f, 28f);
            RectTransform tr = t.rectTransform;
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = new Vector2(4f, 2f);
            tr.offsetMax = new Vector2(-4f, -2f);
            t.raycastTarget = false;
            return btn;
        }

        private static TMP_Text CreateTmp(string name, Transform parent, string text, float size, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.minHeight = height;
            le.preferredHeight = height;
            return tmp;
        }

        /// <summary>Compact icon-over-label themed button for the Task Preview control strip
        /// (Previous / Next / Start / Stop) — matches the Developer-panel design system.</summary>
        private static Button CreateIconLabelButton(string name, Transform parent, string glyph, string label,
            Sprite rounded, Color background, Color foreground)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Image img = go.AddComponent<Image>();
            img.sprite = rounded;
            img.type = Image.Type.Sliced;
            img.color = background;
            Button btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.minHeight = 40f;
            le.preferredHeight = 40f;
            le.flexibleWidth = 1f;

            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlHeight = false;
            vlg.childControlWidth = false;
            vlg.spacing = 1f;

            TMP_Text glyphText = CreateTmp("Glyph", go.transform, glyph, 13f, 16f);
            glyphText.color = foreground;
            glyphText.alignment = TextAlignmentOptions.Center;
            glyphText.rectTransform.sizeDelta = new Vector2(80f, 16f);

            TMP_Text labelText = CreateTmp("Label", go.transform, label, 10f, 14f);
            labelText.color = foreground;
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.rectTransform.sizeDelta = new Vector2(80f, 14f);

            return btn;
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
    }
}
