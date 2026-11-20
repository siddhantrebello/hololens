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
    /// AR Guidance Phase 2 — Replace Battery hologram library.
    /// Builds reusable primitives + battery workflow prefabs. No QR / no task wiring.
    /// </summary>
    public static class PhaseArGuidanceBatteryLibraryConfigurator
    {
        private const string Menu = "Norscope/HoloLens/AR Guidance/Phase 2/";
        private const string PrefabFolder = "Assets/Norscope/UI/Panels/ArGuidance/Battery";
        private const string PrimitiveFolder = "Assets/Norscope/UI/Panels/ArGuidance/Primitives";
        private const string MatFolder = "Assets/Norscope/Materials/ArGuidance";
        private const string LibraryPath =
            "Assets/Norscope/Data/ArGuidance/ArHologramLibrary_ReplaceBattery.asset";
        private const string ProfilePath =
            "Assets/Norscope/Data/ArGuidance/ArGuidanceProfile_DellPrecision3570.asset";
        private const string ValidateReportPath =
            "Temp/NorscopeArGuidancePhase2BatteryValidate.txt";
        private const string ApplyReportPath =
            "Temp/NorscopeArGuidancePhase2BatteryApply.txt";

        private static readonly Color Cyan = ArGuidancePalette.Cyan;
        private static readonly Color Amber = ArGuidancePalette.Warning;
        private static readonly Color Red = ArGuidancePalette.Danger;
        private static readonly Color Green = ArGuidancePalette.Completed;

        [MenuItem(Menu + "1) Apply")]
        public static void ApplyMenu()
        {
            try { ApplyInternal(); }
            catch (Exception ex)
            {
                Debug.LogError("[Norscope AR Guidance P2] Apply FAILED:\n" + ex);
                if (!Application.isBatchMode)
                    EditorUtility.DisplayDialog("AR Guidance P2", "FAILED\n" + ex.Message, "OK");
            }
        }

        [MenuItem(Menu + "2) Validate")]
        public static void ValidateMenu()
        {
            bool pass = Validate(out string report);
            File.WriteAllText(ValidateReportPath, report);
            Debug.Log(pass
                ? "[Norscope AR Guidance P2] Validate PASS\n" + report
                : "[Norscope AR Guidance P2] Validate FAIL\n" + report);
            if (!Application.isBatchMode)
                EditorUtility.DisplayDialog("AR Guidance P2 Validate", pass ? "PASS" : "FAIL", "OK");
        }

        public static void CaptureApplyValidate()
        {
            ApplyInternal();
            bool pass = Validate(out string report);
            File.WriteAllText(ValidateReportPath, report);
            if (!pass)
                throw new Exception("Phase 2 Battery Library Validate FAIL\n" + report);
            Debug.Log("[Norscope AR Guidance P2] CaptureApplyValidate PASS");
        }

        private static void ApplyInternal()
        {
            Directory.CreateDirectory(PrefabFolder);
            Directory.CreateDirectory(PrimitiveFolder);
            Directory.CreateDirectory(MatFolder);
            Directory.CreateDirectory("Assets/Norscope/Data/ArGuidance");
            Directory.CreateDirectory("Temp");

            Material cyan = GetOrCreateMat("ArHoloCyan", Cyan);
            Material amber = GetOrCreateMat("ArHoloAmber", Amber);
            Material red = GetOrCreateMat("ArHoloRed", Red);
            Material green = GetOrCreateMat("ArHoloGreen", Green);
            GetOrCreateMat("ArHoloSelected", ArGuidancePalette.Selected);

            GameObject pOutline = CreateOutlinePrefab(
                PrimitiveFolder + "/ArOutline.prefab", "ArOutline",
                0.358f, 0.232f, 0.002f, 0.08f, Cyan, cyan, "Outline", false);
            GameObject pScrewMarker = CreateScrewMarkerPrefab(
                PrimitiveFolder + "/ArScrewMarker.prefab", cyan, 1);
            CreateScrewGroupPrefab(
                PrimitiveFolder + "/ArScrewGroup.prefab", pScrewMarker,
                "ArScrewGroup", "screw_group_template");
            CreateConnectorPrefab(PrimitiveFolder + "/ArConnector.prefab", amber, Amber);
            CreateWarningPrefab(PrimitiveFolder + "/ArWarning.prefab", red);
            CreateDirectionPrefab(
                PrimitiveFolder + "/ArDirection.prefab", cyan, ArDirectionSense.Up, "Lift Here");
            CreateVerificationPrefab(PrimitiveFolder + "/ArVerification.prefab", green);

            GameObject h02 = CreateIconCuePrefab(
                PrefabFolder + "/H02_DisconnectAC.prefab", "H02_DisconnectAC",
                "h02_disconnect_ac", ArIconCueKind.Plug, 0.025f, 0f, 0.060f,
                true, false, Amber, amber, "Disconnect AC Adapter", false);

            GameObject h03 = CreateOutlinePrefab(
                PrefabFolder + "/H03_BackPanelOutline.prefab", "H03_BackPanelOutline",
                0.358f, 0.232f, 0.002f, 0.06f, Cyan, cyan, string.Empty, true,
                cornerRadius: 0.006f);
            ConfigureIdentity(h03, "h03_back_panel_outline", ArVisualType.Outline);

            // H04 screws are 9 independent top-level hologram library entries (one per screw,
            // sharing this one marker prefab template) rather than a single group prefab with
            // 9 children — each screw gets its own Preview toggle and calibration entry with
            // no shared parent to worry about. See Entry(...) calls below and
            // ArGuidanceRenderer.TryEnsureLibraryEntryInstance, which numbers each instance at
            // resolve time via ArCalibrationIds.ParseScrewNumber since there's no longer a
            // group iterating its children to do that.
            GameObject h04Marker = CreateScrewMarkerPrefab(
                PrefabFolder + "/H04_ScrewMarker.prefab", cyan, 1);

            // H05 / H08 / H18 / H20 use the focused Visual Redesign builders so a library
            // rebuild cannot revert those industrial AR visuals. Poses below stay measured.
            GameObject h05 = PhaseArGuidanceHologramVisualRedesignConfigurator.RebuildH05LiftHere(cyan);
            GameObject outlineForH07 = CreateOutlinePrefab(
                PrefabFolder + "/H07_ReplaceBackCover_Outline.prefab", "Outline",
                0.358f, 0.232f, 0.002f, 0.05f, Cyan, cyan, string.Empty, true);
            GameObject h07 = CreateReplaceCoverComposite(
                PrefabFolder + "/H07_ReplaceBackCover.prefab", cyan, outlineForH07);

            GameObject h08 = PhaseArGuidanceHologramVisualRedesignConfigurator.RebuildH08PowerOn(cyan);

            GameObject h17 = CreateOutlinePrefab(
                PrefabFolder + "/H17_BatteryOutline.prefab", "H17_BatteryOutline",
                0.320f, 0.090f, 0.002f, 0f, Cyan, cyan, "Battery", true,
                cornerRadius: 0.006f);
            ConfigureIdentity(h17, "h17_battery_outline", ArVisualType.Outline);

            GameObject h18 =
                PhaseArGuidanceHologramVisualRedesignConfigurator.RebuildH18BatteryConnector(cyan, amber);

            // H19 screws are 4 independent top-level library entries (same pattern as H04).
            GameObject h19Marker = CreateScrewMarkerPrefab(
                PrefabFolder + "/H19_BatteryScrewMarker.prefab", cyan, 1);

            GameObject h20 =
                PhaseArGuidanceHologramVisualRedesignConfigurator.RebuildH20SwollenWarning(amber);

            GameObject h21 = CreateOutlinePrefab(
                PrefabFolder + "/H21_InstallNewBattery.prefab", "H21_InstallNewBattery",
                0.320f, 0.090f, 0.002f, 0f, Cyan, cyan, "Install New Battery", true,
                cornerRadius: 0.006f);
            ConfigureIdentity(h21, "h21_install_new_battery", ArVisualType.Outline);
            // Same orientation / motion as H17 — no ghost slide.
            SetBool(h21.GetComponent<ArOutlineVisual>(), "ghostSlideIn", false);

            GameObject h22 = CreateVerificationPrefab(
                PrefabFolder + "/H22_BatteryVerification.prefab", green);
            ConfigureIdentity(h22, "h22_battery_verification", ArVisualType.Verification);
            SetString(h22.GetComponent<ArVerificationVisual>(), "labelText",
                "Battery Detected\nCharging Normal");

            ArHologramLibrary library = GetOrCreateLibrary();
            library.equipmentId = "dell_precision_3570";
            library.workflowId = "replace-battery";
            Vector3[] h04Locals = ArCalibrationIds.DefaultH04ScrewLocals();
            Vector3[] h04Eulers = ArCalibrationIds.DefaultH04ScrewEulers();
            Vector3[] h19Locals = ArCalibrationIds.DefaultH19ScrewLocals();
            library.entries = new[]
            {
                Entry("h02_disconnect_ac", "H02 Disconnect AC", ArVisualType.IconCue, h02, true,
                    new Vector3(-0.132f, -0.039f, -0.002f), new Vector3(0f, 180f, -90f)),
                Entry("h03_back_panel_outline", "H03 Back Panel Outline", ArVisualType.Outline, h03, true,
                    new Vector3(0f, 0.120f, 0f), new Vector3(-90f, -90f, -90f)),
                Entry("h04_screw_01", "H04-01", ArVisualType.ScrewMarker, h04Marker, true, h04Locals[0], h04Eulers[0]),
                Entry("h04_screw_02", "H04-02", ArVisualType.ScrewMarker, h04Marker, true, h04Locals[1], h04Eulers[1]),
                Entry("h04_screw_03", "H04-03", ArVisualType.ScrewMarker, h04Marker, true, h04Locals[2], h04Eulers[2]),
                Entry("h04_screw_04", "H04-04", ArVisualType.ScrewMarker, h04Marker, true, h04Locals[3], h04Eulers[3]),
                Entry("h04_screw_05", "H04-05", ArVisualType.ScrewMarker, h04Marker, true, h04Locals[4], h04Eulers[4]),
                Entry("h04_screw_06", "H04-06", ArVisualType.ScrewMarker, h04Marker, true, h04Locals[5], h04Eulers[5]),
                Entry("h04_screw_07", "H04-07", ArVisualType.ScrewMarker, h04Marker, true, h04Locals[6], h04Eulers[6]),
                Entry("h04_screw_08", "H04-08", ArVisualType.ScrewMarker, h04Marker, true, h04Locals[7], h04Eulers[7]),
                Entry("h04_screw_09", "H04-09", ArVisualType.ScrewMarker, h04Marker, true, h04Locals[8], h04Eulers[8]),
                Entry("h05_lift_here", "H05 Lift Here", ArVisualType.Direction, h05, true,
                    new Vector3(-0.138f, 0.082f, -0.010f), new Vector3(-58f, 180f, -86.2f)),
                Entry("h07_replace_back_cover", "H07 Replace Back Cover", ArVisualType.Direction, h07, true,
                    new Vector3(-0.130f, 0.110f, 0f), new Vector3(0f, 180f, -90f)),
                Entry("h08_power_on", "H08 Power On", ArVisualType.IconCue, h08, true, new Vector3(0f, 0.08f, 0.14f)),
                Entry("h17_battery_outline", "H17 Battery Outline", ArVisualType.Outline, h17, false,
                    new Vector3(-0.056f, 0.128f, 0f), new Vector3(0f, -90f, -90f)),
                Entry("h18_battery_connector", "H18 Battery Connector", ArVisualType.Connector, h18, false,
                    new Vector3(0f, 0.146f, 0.002f), new Vector3(0f, -90f, -90f)),
                Entry("h19_screw_01", "H19-01", ArVisualType.ScrewMarker, h19Marker, false, h19Locals[0]),
                Entry("h19_screw_02", "H19-02", ArVisualType.ScrewMarker, h19Marker, false, h19Locals[1]),
                Entry("h19_screw_03", "H19-03", ArVisualType.ScrewMarker, h19Marker, false, h19Locals[2]),
                Entry("h19_screw_04", "H19-04", ArVisualType.ScrewMarker, h19Marker, false, h19Locals[3]),
                Entry("h20_swollen_battery_warning", "H20 Swollen Warning", ArVisualType.Warning, h20, false,
                    new Vector3(0.042f, 0.080f, 0f), new Vector3(0f, -90f, -90f)),
                Entry("h21_install_new_battery", "H21 Install New Battery", ArVisualType.Outline, h21, false,
                    new Vector3(0.044f, 0.128f, 0f), new Vector3(0f, -90f, -90f)),
                Entry("h22_battery_verification", "H22 Battery Verification", ArVisualType.Verification, h22, false,
                    new Vector3(0.044f, 0.080f, 0f), new Vector3(0f, -90f, -90f))
            };
            EditorUtility.SetDirty(library);

            ArGuidanceProfile profile = AssetDatabase.LoadAssetAtPath<ArGuidanceProfile>(ProfilePath);
            if (profile == null)
                throw new Exception("Equipment profile missing: " + ProfilePath);
            profile.hologramLibrary = library;
            EditorUtility.SetDirty(profile);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var log = new StringBuilder();
            log.AppendLine("=== AR Guidance Phase 2 Battery Library Apply ===");
            log.AppendLine(DateTime.Now.ToString("o"));
            log.AppendLine("Primitives OK. Library entries: " + library.entries.Length);
            log.AppendLine("QR untouched. Calibration repository untouched. Tasks untouched.");
            log.AppendLine("Primitive template kept: " + (pOutline != null));
            File.WriteAllText(ApplyReportPath, log.ToString());
            Debug.Log("[Norscope AR Guidance P2] Apply COMPLETED\n" + log);
            if (!Application.isBatchMode)
                EditorUtility.DisplayDialog("AR Guidance P2", "COMPLETED", "OK");
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

            sb.AppendLine("=== AR Guidance Phase 2 Battery Library Validate ===");
            sb.AppendLine(DateTime.Now.ToString("o"));
            sb.AppendLine();

            string[] primitives =
            {
                "ArOutline", "ArScrewMarker", "ArScrewGroup", "ArConnector",
                "ArWarning", "ArDirection", "ArVerification"
            };
            for (int i = 0; i < primitives.Length; i++)
                Check(File.Exists(PrimitiveFolder + "/" + primitives[i] + ".prefab"),
                    "Primitive " + primitives[i]);

            string[] holograms =
            {
                "H02_DisconnectAC", "H03_BackPanelOutline",
                "H04_ScrewMarker", "H05_LiftHere", "H07_ReplaceBackCover",
                "H08_PowerOn", "H17_BatteryOutline", "H18_BatteryConnector",
                "H19_BatteryScrewMarker", "H20_SwollenBatteryWarning",
                "H21_InstallNewBattery", "H22_BatteryVerification"
            };
            for (int i = 0; i < holograms.Length; i++)
                Check(File.Exists(PrefabFolder + "/" + holograms[i] + ".prefab"),
                    "Hologram " + holograms[i]);

            Check(File.Exists(MatFolder + "/ArHoloCyan.mat"), "Material Cyan");
            Check(File.Exists(MatFolder + "/ArHoloAmber.mat"), "Material Amber");
            Check(File.Exists(MatFolder + "/ArHoloRed.mat"), "Material Red");
            Check(File.Exists(MatFolder + "/ArHoloGreen.mat"), "Material Green");
            Check(File.Exists(MatFolder + "/ArHoloSelected.mat"), "Material Selected");
            Check(File.Exists(LibraryPath), "Hologram library asset");

            ArHologramLibrary lib = AssetDatabase.LoadAssetAtPath<ArHologramLibrary>(LibraryPath);
            Check(lib != null && lib.entries != null && lib.entries.Length == 23, "Library has 23 entries");
            if (lib?.entries != null)
            {
                for (int i = 0; i < lib.entries.Length; i++)
                {
                    ArHologramLibraryEntry e = lib.entries[i];
                    Check(e != null && e.prefab != null && !string.IsNullOrEmpty(e.hologramId),
                        "Entry wired: " + (e != null ? e.hologramId : "null"));
                    if (e?.prefab != null)
                    {
                        ArHologramVisualBase v = e.prefab.GetComponent<ArHologramVisualBase>() ??
                                                 e.prefab.GetComponentInChildren<ArHologramVisualBase>(true);
                        Check(v != null, "Editable visual on " + e.hologramId);
                    }
                }
            }

            ArGuidanceProfile profile = AssetDatabase.LoadAssetAtPath<ArGuidanceProfile>(ProfilePath);
            Check(profile != null && profile.hologramLibrary == lib, "Profile references hologram library");

            Check(File.Exists("Assets/Norscope/Runtime/ArGuidance/QrTrackingOriginProvider.cs"), "QR stub present");
            Check(File.Exists("Assets/Norscope/Runtime/ArGuidance/JsonArCalibrationRepository.cs"), "Calibration repo present");
            Check(File.Exists("Assets/Norscope/Runtime/TaskLibrary/TaskStep.cs"), "TaskStep present");
            Check(File.Exists("Assets/Norscope/Runtime/ArGuidance/Visuals/ArOutlineVisual.cs"), "ArOutlineVisual");
            Check(File.Exists("Assets/Norscope/Runtime/ArGuidance/Visuals/ArDirectionVisual.cs"), "ArDirectionVisual");
            Check(File.Exists("Assets/Norscope/Runtime/ArGuidance/Visuals/ArConnectorVisual.cs"), "ArConnectorVisual");
            Check(File.Exists("Assets/Norscope/Runtime/ArGuidance/Visuals/ArWarningVisual.cs"), "ArWarningVisual");
            Check(File.Exists("Assets/Norscope/Runtime/ArGuidance/Visuals/ArVerificationVisual.cs"),
                "ArVerificationVisual");
            Check(File.Exists("Assets/Norscope/Runtime/ArGuidance/Visuals/ArIconCueVisual.cs"), "ArIconCueVisual");
            Check(File.Exists("Assets/Norscope/Runtime/ArGuidance/Visuals/ArDirectionalChevronVisual.cs"),
                "ArDirectionalChevronVisual (H05 redesign)");
            Check(File.Exists("Assets/Norscope/Runtime/ArGuidance/Visuals/ArTargetRingVisual.cs"),
                "ArTargetRingVisual (H08 redesign)");
            Check(File.Exists("Assets/Norscope/Runtime/ArGuidance/Visuals/ArConnectorActionVisual.cs"),
                "ArConnectorActionVisual (H18 redesign)");
            Check(File.Exists("Assets/Norscope/Runtime/ArGuidance/Visuals/ArInspectionWarningVisual.cs"),
                "ArInspectionWarningVisual (H20 redesign)");

            GameObject h05Go = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "/H05_LiftHere.prefab");
            GameObject h08Go = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "/H08_PowerOn.prefab");
            GameObject h18Go = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "/H18_BatteryConnector.prefab");
            GameObject h20Go = AssetDatabase.LoadAssetAtPath<GameObject>(
                PrefabFolder + "/H20_SwollenBatteryWarning.prefab");
            Check(h05Go != null && h05Go.GetComponent<ArDirectionalChevronVisual>() != null,
                "H05 redesign component present after rebuild path");
            Check(h08Go != null && h08Go.GetComponent<ArTargetRingVisual>() != null,
                "H08 redesign component present after rebuild path");
            Check(h18Go != null && h18Go.GetComponent<ArConnectorActionVisual>() != null,
                "H18 redesign component present after rebuild path");
            Check(h20Go != null && h20Go.GetComponent<ArInspectionWarningVisual>() != null,
                "H20 redesign component present after rebuild path");

            sb.AppendLine();
            sb.AppendLine(pass ? "STATUS PASS" : "STATUS FAIL");
            sb.AppendLine("QR logic modified: NO");
            sb.AppendLine("Task logic modified: NO");
            sb.AppendLine("Calibration repository modified: NO");
            report = sb.ToString();
            return pass;
        }

        private static ArHologramLibraryEntry Entry(
            string id, string name, ArVisualType type, GameObject prefab, bool shared, Vector3 pos)
        {
            return Entry(id, name, type, prefab, shared, pos, Vector3.zero);
        }

        private static ArHologramLibraryEntry Entry(
            string id, string name, ArVisualType type, GameObject prefab, bool shared,
            Vector3 pos, Vector3 euler)
        {
            return new ArHologramLibraryEntry
            {
                hologramId = id,
                displayName = name,
                visualType = type,
                prefab = prefab,
                defaultLocalPosition = pos,
                defaultLocalEuler = euler,
                defaultLocalScale = Vector3.one,
                isShared = shared
            };
        }

        private static ArHologramLibrary GetOrCreateLibrary()
        {
            ArHologramLibrary lib = AssetDatabase.LoadAssetAtPath<ArHologramLibrary>(LibraryPath);
            if (lib != null) return lib;
            lib = ScriptableObject.CreateInstance<ArHologramLibrary>();
            AssetDatabase.CreateAsset(lib, LibraryPath);
            return lib;
        }

        private static Material GetOrCreateMat(string name, Color color)
        {
            string path = MatFolder + "/" + name + ".mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Sprites/Default"));
                mat.color = color;
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.color = color;
                EditorUtility.SetDirty(mat);
            }
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
            lr.widthMultiplier = 0.002f;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            return lr;
        }

        private static TMP_Text CreateWorldLabel(string name, Transform parent, string text, float y)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, y, 0f);
            go.layer = LayerMask.NameToLayer("Ignore Raycast");
            TextMeshPro tmp = go.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = 0.1f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Cyan;
            tmp.raycastTarget = false;
            tmp.enableAutoSizing = false;
            RectTransform rt = go.GetComponent<RectTransform>();
            if (rt != null) rt.sizeDelta = new Vector2(0.6f, 0.12f);
            return tmp;
        }

        private static GameObject CreateOutlinePrefab(
            string path, string name, float w, float d, float thickness, float fillOp,
            Color color, Material mat, string label, bool fadeIn, float cornerRadius = 0f)
        {
            var root = new GameObject(name);
            var visual = root.AddComponent<ArOutlineVisual>();
            LineRenderer peri = CreateLine("Perimeter", root.transform, mat, true);
            var brackets = new LineRenderer[4];
            for (int i = 0; i < 4; i++)
                brackets[i] = CreateLine("Corner" + i, root.transform, mat, false);

            var fill = GameObject.CreatePrimitive(PrimitiveType.Quad);
            fill.name = "Fill";
            UnityEngine.Object.DestroyImmediate(fill.GetComponent<Collider>());
            fill.transform.SetParent(root.transform, false);
            fill.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            fill.transform.localScale = new Vector3(w, d, 1f);
            MeshRenderer fillMr = fill.GetComponent<MeshRenderer>();
            fillMr.sharedMaterial = mat;
            fill.SetActive(fillOp > 0.001f);
            fillMr.enabled = fillOp > 0.001f;

            TMP_Text tmp = string.IsNullOrEmpty(label)
                ? null
                : CreateWorldLabel("Label", root.transform, label, 0.04f);

            SerializedObject so = new SerializedObject(visual);
            so.FindProperty("perimeter").objectReferenceValue = peri;
            SerializedProperty arr = so.FindProperty("cornerBrackets");
            arr.arraySize = 4;
            for (int i = 0; i < 4; i++)
                arr.GetArrayElementAtIndex(i).objectReferenceValue = brackets[i];
            so.FindProperty("fillRenderer").objectReferenceValue = fillMr;
            so.FindProperty("label").objectReferenceValue = tmp;
            so.FindProperty("widthMeters").floatValue = w;
            so.FindProperty("depthMeters").floatValue = d;
            so.FindProperty("lineThicknessMeters").floatValue = thickness;
            so.FindProperty("fillOpacity").floatValue = fillOp;
            so.FindProperty("fadeInOnEnable").boolValue = fadeIn;
            so.FindProperty("labelText").stringValue = label ?? string.Empty;
            so.FindProperty("defaultColor").colorValue = color;
            so.FindProperty("color").colorValue = color;
            so.FindProperty("opacity").floatValue = color.a;
            so.FindProperty("visualType").intValue = (int)ArVisualType.Outline;
            so.FindProperty("cornerRadiusMeters").floatValue = cornerRadius;
            so.ApplyModifiedPropertiesWithoutUndo();
            visual.RebuildGeometry();
            return SavePrefab(root, path);
        }

        private static GameObject CreateScrewMarkerPrefab(string path, Material mat, int number)
        {
            var root = new GameObject("ArScrewMarker");
            var visual = root.AddComponent<ArScrewMarkerVisual>();
            LineRenderer ring = CreateLine("Ring", root.transform, mat, true);
            LineRenderer leader = CreateLine("Leader", root.transform, mat, false);
            TMP_Text label = CreateWorldLabel("Number", root.transform, number.ToString(), 0.015f);
            SerializedObject so = new SerializedObject(visual);
            so.FindProperty("ring").objectReferenceValue = ring;
            so.FindProperty("leader").objectReferenceValue = leader;
            so.FindProperty("numberLabel").objectReferenceValue = label;
            so.FindProperty("screwNumber").intValue = number;
            so.FindProperty("ringOuterDiameterMeters").floatValue = 0.012f;
            so.FindProperty("lineThicknessMeters").floatValue = 0.0015f;
            so.FindProperty("numberHeightMeters").floatValue = 0.015f;
            so.FindProperty("leaderLineEnabled").boolValue = true;
            so.FindProperty("visualType").intValue = (int)ArVisualType.ScrewMarker;
            so.FindProperty("defaultColor").colorValue = Cyan;
            so.FindProperty("completedColor").colorValue = Green;
            so.ApplyModifiedPropertiesWithoutUndo();
            visual.RebuildGeometry();
            return SavePrefab(root, path);
        }

        public static GameObject CreateScrewGroupPrefab(
            string path, GameObject markerPrefab, string name, string id, Vector3[] offsets = null)
        {
            var root = new GameObject(name);
            var visual = root.AddComponent<ArScrewGroupVisual>();
            visual.ConfigureIdentity(id, ArVisualType.ScrewGroup);
            if (offsets == null)
            {
                offsets = new[]
                {
                    new Vector3(-0.15f, 0f, -0.10f),
                    new Vector3(0.15f, 0f, -0.10f),
                    new Vector3(-0.15f, 0f, 0.10f),
                    new Vector3(0.15f, 0f, 0.10f)
                };
            }

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
            so.FindProperty("visualType").intValue = (int)ArVisualType.ScrewGroup;
            so.ApplyModifiedPropertiesWithoutUndo();
            return SavePrefab(root, path);
        }

        private static GameObject CreateConnectorPrefab(string path, Material mat, Color color)
        {
            var root = new GameObject("ArConnector");
            var visual = root.AddComponent<ArConnectorVisual>();
            LineRenderer zone = CreateLine("Zone", root.transform, mat, true);
            LineRenderer leader = CreateLine("Leader", root.transform, mat, false);
            TMP_Text label = CreateWorldLabel("Label", root.transform, "Disconnect Battery First", 0.05f);
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
            so.FindProperty("label").objectReferenceValue = label;
            so.FindProperty("warningIcon").objectReferenceValue = icon.transform;
            so.FindProperty("targetZoneMeters").vector2Value = new Vector2(0.025f, 0.010f);
            so.FindProperty("leaderLineLengthMeters").floatValue = 0.040f;
            so.FindProperty("leaderLineEnabled").boolValue = true;
            so.FindProperty("defaultColor").colorValue = color;
            so.FindProperty("warningColor").colorValue = color;
            so.FindProperty("color").colorValue = color;
            so.FindProperty("visualType").intValue = (int)ArVisualType.Connector;
            so.ApplyModifiedPropertiesWithoutUndo();
            visual.RebuildGeometry();
            return SavePrefab(root, path);
        }

        private static GameObject CreateWarningPrefab(string path, Material mat)
        {
            var root = new GameObject("ArWarning");
            var visual = root.AddComponent<ArWarningVisual>();
            LineRenderer tri = CreateLine("Triangle", root.transform, mat, true);
            TMP_Text label = CreateWorldLabel("Label", root.transform,
                "Battery appears swollen.\nDo NOT puncture.", 0.06f);
            SerializedObject so = new SerializedObject(visual);
            so.FindProperty("triangle").objectReferenceValue = tri;
            so.FindProperty("label").objectReferenceValue = label;
            so.FindProperty("triangleSizeMeters").floatValue = 0.045f;
            so.FindProperty("defaultColor").colorValue = Red;
            so.FindProperty("dangerColor").colorValue = Red;
            so.FindProperty("color").colorValue = Red;
            so.FindProperty("visualType").intValue = (int)ArVisualType.Warning;
            so.ApplyModifiedPropertiesWithoutUndo();
            visual.RebuildGeometry();
            return SavePrefab(root, path);
        }

        private static GameObject CreateDirectionPrefab(
            string path, Material mat, ArDirectionSense sense, string labelText)
        {
            var root = new GameObject("ArDirection");
            var visual = root.AddComponent<ArDirectionVisual>();
            LineRenderer edge = CreateLine("Edge", root.transform, mat, false);
            var chevrons = new Transform[3];
            for (int i = 0; i < 3; i++)
            {
                var c = GameObject.CreatePrimitive(PrimitiveType.Cube);
                c.name = "Chevron" + i;
                UnityEngine.Object.DestroyImmediate(c.GetComponent<Collider>());
                c.transform.SetParent(root.transform, false);
                c.transform.localScale = new Vector3(0.012f, 0.002f, 0.008f);
                float y = sense == ArDirectionSense.Up ? 0.005f + i * 0.008f : -0.005f - i * 0.008f;
                c.transform.localPosition = new Vector3((i - 1) * 0.02f, y, 0f);
                c.GetComponent<MeshRenderer>().sharedMaterial = mat;
                chevrons[i] = c.transform;
            }

            TMP_Text label = CreateWorldLabel("Label", root.transform, labelText, 0.05f);
            SerializedObject so = new SerializedObject(visual);
            so.FindProperty("edgeLine").objectReferenceValue = edge;
            so.FindProperty("label").objectReferenceValue = label;
            SerializedProperty arr = so.FindProperty("chevrons");
            arr.arraySize = 3;
            for (int i = 0; i < 3; i++)
                arr.GetArrayElementAtIndex(i).objectReferenceValue = chevrons[i];
            so.FindProperty("direction").intValue = (int)sense;
            so.FindProperty("edgeLengthMeters").floatValue = 0.080f;
            so.FindProperty("chevronSizeMeters").floatValue = 0.012f;
            so.FindProperty("labelText").stringValue = labelText;
            so.FindProperty("defaultColor").colorValue = Cyan;
            so.FindProperty("visualType").intValue = (int)ArVisualType.Direction;
            so.ApplyModifiedPropertiesWithoutUndo();
            visual.RebuildGeometry();
            return SavePrefab(root, path);
        }

        private static GameObject CreateReplaceCoverComposite(string path, Material mat, GameObject outlinePrefab)
        {
            var root = new GameObject("H07_ReplaceBackCover");
            var visual = root.AddComponent<ArDirectionVisual>();
            visual.ConfigureIdentity("h07_replace_back_cover", ArVisualType.Direction);

            GameObject outline = (GameObject)PrefabUtility.InstantiatePrefab(outlinePrefab);
            outline.name = "BackPanelOutline";
            outline.transform.SetParent(root.transform, false);

            LineRenderer edge = CreateLine("Edge", root.transform, mat, false);
            var chevrons = new Transform[3];
            for (int i = 0; i < 3; i++)
            {
                var c = GameObject.CreatePrimitive(PrimitiveType.Cube);
                c.name = "Chevron" + i;
                UnityEngine.Object.DestroyImmediate(c.GetComponent<Collider>());
                c.transform.SetParent(root.transform, false);
                c.transform.localScale = new Vector3(0.012f, 0.002f, 0.008f);
                c.transform.localPosition = new Vector3((i - 1) * 0.025f, -0.008f - i * 0.01f, -0.02f);
                c.GetComponent<MeshRenderer>().sharedMaterial = mat;
                chevrons[i] = c.transform;
            }

            TMP_Text label = CreateWorldLabel("Label", root.transform, "Replace Back Cover", 0.05f);
            SerializedObject so = new SerializedObject(visual);
            so.FindProperty("edgeLine").objectReferenceValue = edge;
            so.FindProperty("label").objectReferenceValue = label;
            SerializedProperty arr = so.FindProperty("chevrons");
            arr.arraySize = 3;
            for (int i = 0; i < 3; i++)
                arr.GetArrayElementAtIndex(i).objectReferenceValue = chevrons[i];
            so.FindProperty("direction").intValue = (int)ArDirectionSense.Down;
            so.FindProperty("edgeLengthMeters").floatValue = 0.200f;
            so.FindProperty("labelText").stringValue = "Replace Back Cover";
            so.FindProperty("defaultColor").colorValue = Cyan;
            so.FindProperty("visualType").intValue = (int)ArVisualType.Direction;
            so.ApplyModifiedPropertiesWithoutUndo();
            visual.RebuildGeometry();
            return SavePrefab(root, path);
        }

        private static GameObject CreateVerificationPrefab(string path, Material mat)
        {
            var root = new GameObject("ArVerification");
            var visual = root.AddComponent<ArVerificationVisual>();
            LineRenderer check = CreateLine("Check", root.transform, mat, false);
            LineRenderer ripple = CreateLine("Ripple", root.transform, mat, true);
            var icon = GameObject.CreatePrimitive(PrimitiveType.Cube);
            icon.name = "BatteryIcon";
            UnityEngine.Object.DestroyImmediate(icon.GetComponent<Collider>());
            icon.transform.SetParent(root.transform, false);
            icon.transform.localScale = new Vector3(0.025f, 0.002f, 0.014f);
            icon.transform.localPosition = new Vector3(0f, 0f, -0.035f);
            icon.GetComponent<MeshRenderer>().sharedMaterial = mat;
            TMP_Text label = CreateWorldLabel("Label", root.transform,
                "Battery Detected\nCharging Normal", 0.06f);

            SerializedObject so = new SerializedObject(visual);
            so.FindProperty("checkMark").objectReferenceValue = check;
            so.FindProperty("rippleRing").objectReferenceValue = ripple;
            so.FindProperty("batteryIcon").objectReferenceValue = icon.transform;
            so.FindProperty("label").objectReferenceValue = label;
            so.FindProperty("defaultColor").colorValue = Green;
            so.FindProperty("completedColor").colorValue = Green;
            so.FindProperty("color").colorValue = Green;
            so.FindProperty("visualType").intValue = (int)ArVisualType.Verification;
            so.ApplyModifiedPropertiesWithoutUndo();
            visual.RebuildGeometry();
            return SavePrefab(root, path);
        }

        private static GameObject CreateIconCuePrefab(
            string path, string name, string id, ArIconCueKind kind,
            float iconSize, float ringDia, float leaderLen,
            bool leader, bool breath, Color color, Material mat,
            string labelText, bool slowFloat)
        {
            var root = new GameObject(name);
            var visual = root.AddComponent<ArIconCueVisual>();
            visual.ConfigureIdentity(id, ArVisualType.IconCue);
            LineRenderer icon = CreateLine("Icon", root.transform, mat, false);
            LineRenderer ring = ringDia > 0.001f ? CreateLine("Ring", root.transform, mat, true) : null;
            LineRenderer leaderLr = leader ? CreateLine("Leader", root.transform, mat, false) : null;
            TMP_Text label = CreateWorldLabel("Label", root.transform, labelText, 0.05f);

            SerializedObject so = new SerializedObject(visual);
            so.FindProperty("iconLines").objectReferenceValue = icon;
            so.FindProperty("ring").objectReferenceValue = ring;
            so.FindProperty("leader").objectReferenceValue = leaderLr;
            so.FindProperty("label").objectReferenceValue = label;
            so.FindProperty("iconKind").intValue = (int)kind;
            so.FindProperty("iconSizeMeters").floatValue = iconSize;
            so.FindProperty("ringDiameterMeters").floatValue = ringDia;
            so.FindProperty("leaderLineLengthMeters").floatValue = leaderLen;
            so.FindProperty("leaderLineEnabled").boolValue = leader;
            so.FindProperty("breathScale").boolValue = breath;
            so.FindProperty("breathAmount").floatValue = 0.03f;
            so.FindProperty("slowFloat").boolValue = slowFloat;
            so.FindProperty("labelText").stringValue = labelText;
            so.FindProperty("defaultColor").colorValue = color;
            so.FindProperty("color").colorValue = color;
            so.FindProperty("visualType").intValue = (int)ArVisualType.IconCue;
            so.ApplyModifiedPropertiesWithoutUndo();
            visual.RebuildGeometry();
            return SavePrefab(root, path);
        }

        private static void ConfigureIdentity(GameObject go, string id, ArVisualType type)
        {
            ArHologramVisualBase v = go.GetComponent<ArHologramVisualBase>();
            if (v == null) return;
            SerializedObject so = new SerializedObject(v);
            so.FindProperty("componentId").stringValue = id;
            so.FindProperty("visualType").intValue = (int)type;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(Component c, string prop, float value)
        {
            if (c == null) return;
            SerializedObject so = new SerializedObject(c);
            SerializedProperty p = so.FindProperty(prop);
            if (p != null) { p.floatValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
        }

        private static void SetBool(Component c, string prop, bool value)
        {
            if (c == null) return;
            SerializedObject so = new SerializedObject(c);
            SerializedProperty p = so.FindProperty(prop);
            if (p != null) { p.boolValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
        }

        private static void SetString(Component c, string prop, string value)
        {
            if (c == null) return;
            SerializedObject so = new SerializedObject(c);
            SerializedProperty p = so.FindProperty(prop);
            if (p != null) { p.stringValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
        }

        private static void SetColor(Component c, string prop, Color value)
        {
            if (c == null) return;
            SerializedObject so = new SerializedObject(c);
            SerializedProperty p = so.FindProperty(prop);
            if (p != null) { p.colorValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
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
            int layer = LayerMask.NameToLayer("Ignore Raycast");
            if (layer >= 0) t.gameObject.layer = layer;
            for (int i = 0; i < t.childCount; i++)
                EnsureIgnoreRaycastRecursive(t.GetChild(i));
        }
    }
}
