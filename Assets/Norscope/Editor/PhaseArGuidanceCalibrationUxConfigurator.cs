using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Norscope.ArGuidance;
using Norscope.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Norscope.Editor
{
    /// <summary>
    /// AR Guidance Calibration UX — Capture / Dry Run / Apply / Validate / Restore.
    /// Visual language matches the NORSCOPE Developer-panel design system
    /// (NorscopeTheme + the shared rounded-rect sprite) — same tokens used by
    /// Phase6TaskLibraryConfigurator, PhaseAskAiPanelConfigurator, etc. All
    /// calibration behavior (step sizes, hold-to-repeat, locks, undo, dirty-confirm
    /// navigation, minimize) is unchanged — this file only restyles the subtree.
    /// </summary>
    public static class PhaseArGuidanceCalibrationUxConfigurator
    {
        private const string Menu = "Norscope/HoloLens/AR Guidance/Calibration UX/";
        private const string BackupKey = "Norscope.ArGuidanceCalibrationUx.Backup";
        private const string DryRunApprovedKey = "Norscope.ArGuidanceCalibrationUx.DryRunApproved";
        private const string BackupRoot = "Assets/Norscope/UI/Panels/_Backups";
        private const string DryRunReportPath = "Temp/NorscopeArGuidanceCalibrationUxDryRun.txt";
        private const string ApplyReportPath = "Temp/NorscopeArGuidanceCalibrationUxApply.txt";
        private const string ValidateReportPath = "Temp/NorscopeArGuidanceCalibrationUxValidate.txt";
        private const string ApplyRequestFlag = "Temp/NorscopeArGuidanceCalibrationUxApply.flag";
        private const string ValidateRequestFlag = "Temp/NorscopeArGuidanceCalibrationUxValidate.flag";
        private const string CaptureApplyValidateFlag =
            "Temp/NorscopeArGuidanceCalibrationUxCaptureApplyValidate.flag";
        private const string FixDropdownFlag =
            "Temp/NorscopeArGuidanceCalibrationUxFixDropdown.flag";
        private const string PanelPrefab = "Assets/Norscope/UI/Panels/ArGuidancePanel.prefab";
        private const string ThemePath = "Assets/Norscope/UI/DesignSystem/Themes/NorscopeTheme.asset";
        private const string RoundedSpritePath = "Assets/Norscope/UI/DesignSystem/Sprites/UIRounded_8.png";

        // Fallbacks used only if the NorscopeTheme asset can't be loaded.
        private static readonly Color FallbackPanelBg = new Color(0.07f, 0.09f, 0.12f, 0.94f);
        private static readonly Color FallbackBtn = new Color(0.12f, 0.18f, 0.24f, 1f);
        private static readonly Color FallbackCyan = new Color(0f, 0.85f, 1f, 0.95f);

        // Axis accent colors — matches the Developer-panel mockup's X/Y/Z badge colors.
        private static readonly Color AxisXColor = new Color(1f, 0.42f, 0.48f, 1f);
        private static readonly Color AxisYColor = new Color(0.36f, 0.91f, 0.63f, 1f);
        private static readonly Color AxisZColor = new Color(0.38f, 0.67f, 1f, 1f);

        private static bool _quiet;

        [InitializeOnLoadMethod]
        private static void PollFlags()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
        }

        private static void OnEditorUpdate()
        {
            if (File.Exists(CaptureApplyValidateFlag))
            {
                File.Delete(CaptureApplyValidateFlag);
                EditorApplication.delayCall += () =>
                {
                    _quiet = true;
                    try
                    {
                        CaptureApplyValidate();
                        Debug.Log("[Norscope Calibration UX] CaptureApplyValidate PASS");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError("[Norscope Calibration UX] CaptureApplyValidate FAIL\n" + ex);
                    }
                    finally
                    {
                        _quiet = false;
                    }
                };
                return;
            }

            if (File.Exists(FixDropdownFlag))
            {
                File.Delete(FixDropdownFlag);
                EditorApplication.delayCall += () =>
                {
                    _quiet = true;
                    try
                    {
                        FixDropdownTemplateInternal();
                        bool pass = Validate(out string report);
                        Directory.CreateDirectory(Path.GetFullPath("Temp"));
                        File.WriteAllText(Path.GetFullPath(ValidateReportPath), report);
                        Debug.Log(pass
                            ? "[Norscope Calibration UX] FixDropdown + Validate PASS\n" + report
                            : "[Norscope Calibration UX] FixDropdown + Validate FAIL\n" + report);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError("[Norscope Calibration UX] FixDropdown FAIL\n" + ex);
                    }
                    finally
                    {
                        _quiet = false;
                    }
                };
                return;
            }

            if (File.Exists(ValidateRequestFlag))
            {
                File.Delete(ValidateRequestFlag);
                EditorApplication.delayCall += ValidateMenu;
                return;
            }

            if (File.Exists(ApplyRequestFlag))
            {
                File.Delete(ApplyRequestFlag);
                EditorApplication.delayCall += ApplyMenu;
            }
        }

        [MenuItem(Menu + "1) Capture Baseline")]
        public static void CaptureBaselineMenu()
        {
            string folder = BackupRoot + "/ArGuidanceCalibrationUx_" +
                            DateTime.Now.ToString("yyyyMMdd_HHmmss");
            Directory.CreateDirectory(folder);
            if (File.Exists(PanelPrefab))
                File.Copy(PanelPrefab, folder + "/ArGuidancePanel.prefab", true);
            EditorPrefs.SetString(BackupKey, folder);
            Debug.Log("[Norscope Calibration UX] Baseline → " + folder);
            Notify("Calibration UX", "Baseline captured.");
        }

        [MenuItem(Menu + "2) Dry Run")]
        public static void DryRunMenu()
        {
            Debug.Log("[Norscope Calibration UX] Dry Run already approved in chat. See Temp report.");
            Notify("Calibration UX Dry Run", "Approved Dry Run on record.");
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
                Debug.LogError("[Norscope Calibration UX] Apply FAILED:\n" + ex);
                Notify("Calibration UX Apply", "FAILED\n" + ex.Message);
            }
        }

        [MenuItem(Menu + "4) Validate")]
        public static void ValidateMenu()
        {
            bool pass = Validate(out string report);
            Directory.CreateDirectory(Path.GetFullPath("Temp"));
            File.WriteAllText(Path.GetFullPath(ValidateReportPath), report);
            Debug.Log(pass
                ? "[Norscope Calibration UX] Validate PASS\n" + report
                : "[Norscope Calibration UX] Validate FAIL\n" + report);
            Notify("Calibration UX Validate", pass ? "PASS" : "FAIL");
        }

        [MenuItem(Menu + "5) Restore Baseline")]
        public static void RestoreMenu()
        {
            string folder = EditorPrefs.GetString(BackupKey, string.Empty);
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                Debug.LogWarning("[Norscope Calibration UX] No baseline.");
                return;
            }

            string src = folder + "/ArGuidancePanel.prefab";
            if (File.Exists(src))
                File.Copy(src, PanelPrefab, true);
            AssetDatabase.Refresh();
            Debug.Log("[Norscope Calibration UX] Restored from " + folder);
        }

        private static void Notify(string title, string message)
        {
            if (_quiet || Application.isBatchMode)
                return;
            EditorUtility.DisplayDialog(title, message, "OK");
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
            Directory.CreateDirectory(Path.GetFullPath("Temp"));
            File.WriteAllText(Path.GetFullPath(ValidateReportPath), report);
        }

        /// <summary>Batch entry: rebuild calibration UX with Position|Rotation side-by-side.</summary>
        public static void ApplyPoseSideBySideBatch()
        {
            try
            {
                _quiet = true;
                ApproveDryRunForApply();
                ApplyInternal();
                if (!Validate(out string report))
                {
                    Debug.LogError("[Norscope Calibration UX] Validate FAIL\n" + report);
                    EditorApplication.Exit(1);
                    return;
                }

                Directory.CreateDirectory(Path.GetFullPath("Temp"));
                File.WriteAllText(Path.GetFullPath(ValidateReportPath), report);
                Debug.Log("[Norscope Calibration UX] Pose side-by-side Apply PASS");
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError("[Norscope Calibration UX] ApplyPoseSideBySideBatch FAILED:\n" + ex);
                EditorApplication.Exit(1);
            }
        }

        private static void ApplyInternal()
        {
            Directory.CreateDirectory(Path.GetFullPath("Temp"));
            NorscopeTheme theme = AssetDatabase.LoadAssetAtPath<NorscopeTheme>(ThemePath);
            Sprite rounded = AssetDatabase.LoadAssetAtPath<Sprite>(RoundedSpritePath);
            if (theme == null)
                Debug.LogWarning("[Norscope Calibration UX] NorscopeTheme asset not found at " +
                                  ThemePath + " — using fallback colors.");

            GameObject root = PrefabUtility.LoadPrefabContents(PanelPrefab);
            try
            {
                RectTransform rootRt = root.GetComponent<RectTransform>();
                rootRt.sizeDelta = new Vector2(720f, 560f);
                NorscopePanelRoot panelRoot = root.GetComponent<NorscopePanelRoot>();
                if (panelRoot != null)
                {
                    SerializedObject pr = new SerializedObject(panelRoot);
                    pr.FindProperty("sizePixels").vector2Value = new Vector2(720f, 560f);
                    pr.FindProperty("sizeMeters").vector2Value = new Vector2(0.72f, 0.56f);
                    pr.ApplyModifiedPropertiesWithoutUndo();
                }

                Transform calib = FindDeep(root.transform, "CalibrationView");
                if (calib == null)
                    throw new Exception("CalibrationView missing");

                // Clear old calibration children
                for (int i = calib.childCount - 1; i >= 0; i--)
                    UnityEngine.Object.DestroyImmediate(calib.GetChild(i).gameObject);

                RectTransform calibRt = calib.GetComponent<RectTransform>();
                // Bottom inset 34px (not 8) — clears the Developer-theme Footer's 26px band.
                Stretch(calibRt, new Vector2(8f, 34f), new Vector2(-8f, -120f));

                GameObject content = CreatePanel("CalibrationContent", calib, stretch: true);
                GameObject minimized = CreatePanel("MinimizedChrome", calib, false);
                RectTransform minRt = minimized.GetComponent<RectTransform>();
                minRt.anchorMin = new Vector2(0f, 1f);
                minRt.anchorMax = new Vector2(1f, 1f);
                minRt.pivot = new Vector2(0.5f, 1f);
                minRt.sizeDelta = new Vector2(0f, 52f);
                minRt.anchoredPosition = new Vector2(0f, -4f);
                minimized.SetActive(false);
                Image minBg = minimized.AddComponent<Image>();
                minBg.sprite = rounded;
                minBg.type = Image.Type.Sliced;
                minBg.color = Card(theme);
                HorizontalLayoutGroup minHl = minimized.AddComponent<HorizontalLayoutGroup>();
                minHl.padding = new RectOffset(14, 14, 8, 8);
                minHl.childAlignment = TextAnchor.MiddleLeft;
                minHl.childControlWidth = false;
                minHl.childControlHeight = true;
                CreateTmp("MinimizedLabel", minimized.transform, "Calibration minimized", 14f, 34f, theme, Secondary(theme));
                GameObject minSpacer = new GameObject("Spacer", typeof(RectTransform));
                minSpacer.transform.SetParent(minimized.transform, false);
                minSpacer.AddComponent<LayoutElement>().flexibleWidth = 1f;
                Button restorePanel = CreateThemedButton("RestorePanelButton", minimized.transform,
                    "Show Calibration", new Vector2(160f, 36f), ButtonStyle.Primary, theme, rounded);

                // Scroll
                ScrollRect scroll = content.AddComponent<ScrollRect>();
                scroll.horizontal = false;
                scroll.vertical = true;
                scroll.movementType = ScrollRect.MovementType.Clamped;
                Image contentBg = content.AddComponent<Image>();
                contentBg.color = new Color(0f, 0f, 0f, 0.01f);
                contentBg.raycastTarget = true;

                GameObject viewport = CreatePanel("Viewport", content.transform, true);
                Mask mask = viewport.AddComponent<Mask>();
                mask.showMaskGraphic = false;
                viewport.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.01f);
                GameObject body = CreatePanel("Body", viewport.transform, false);
                RectTransform bodyRt = body.GetComponent<RectTransform>();
                bodyRt.anchorMin = new Vector2(0f, 1f);
                bodyRt.anchorMax = new Vector2(1f, 1f);
                bodyRt.pivot = new Vector2(0.5f, 1f);
                bodyRt.anchoredPosition = Vector2.zero;
                bodyRt.sizeDelta = new Vector2(0f, 900f);
                VerticalLayoutGroup vlg = body.AddComponent<VerticalLayoutGroup>();
                // developer.zip CalibrationTab uses gap 16 between stacked sections.
                vlg.padding = new RectOffset(4, 4, 4, 8);
                vlg.spacing = 16f;
                vlg.childControlHeight = false;
                vlg.childControlWidth = true;
                vlg.childForceExpandHeight = false;
                vlg.childForceExpandWidth = true;
                ContentSizeFitter csf = body.AddComponent<ContentSizeFitter>();
                csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                scroll.viewport = viewport.GetComponent<RectTransform>();
                scroll.content = bodyRt;

                // Build sections
                TMP_Dropdown dropdown;
                Button showSelected, prev, next, save, restore, reset, undo, minimize;
                TMP_Text dirtyLabel;
                GameObject dirtyBanner;
                Button dirtySave, dirtyDiscard, dirtyCancel;
                Toggle lockH, lockR;
                TMP_Text lockNote;
                Button pos10, pos2, pos05, rot5, rot1;
                ArPressAndHoldButton pxn, pxp, pyn, pyp, pzn, pzp;
                ArPressAndHoldButton rxn, rxp, ryn, ryp, rzn, rzp;
                TMP_Text tx, ty, tz, trx, try_, trz;
                TMP_Text calibratingName, selectedComponentInfo;
                NorscopeBadgeControl calBadge;

                // ── Currently Calibrating card ──────────────────────────────
                GameObject calibCard = CreateCard(body.transform, "CurrentlyCalibratingCard", theme, rounded,
                    tinted: true, padding: new RectOffset(16, 16, 12, 12));
                CreateTmp("CalibratingCaption", calibCard.transform, "CURRENTLY CALIBRATING", 11f, 18f,
                    theme, Dim(theme)).fontStyle = FontStyles.Bold;
                GameObject calibRow = CreateLayoutRow(calibCard.transform, 34f);
                calibratingName = CreateTmp("CalibratingName", calibRow.transform, "—", 19f, 30f, theme, Primary(theme));
                calibratingName.fontStyle = FontStyles.Normal;
                LayoutElement calibNameLe = calibratingName.gameObject.GetComponent<LayoutElement>();
                if (calibNameLe != null) calibNameLe.flexibleWidth = 0f;
                dirtyLabel = CreateTmp("DirtyLabel", calibRow.transform, "", 12f, 30f, theme, Warning(theme));
                dirtyLabel.text = string.Empty;
                GameObject calibSpacer = new GameObject("Spacer", typeof(RectTransform));
                calibSpacer.transform.SetParent(calibRow.transform, false);
                calibSpacer.AddComponent<LayoutElement>().flexibleWidth = 1f;
                GameObject badgeGo = new GameObject("CalBadge", typeof(RectTransform));
                badgeGo.transform.SetParent(calibRow.transform, false);
                RectTransform badgeRt = badgeGo.GetComponent<RectTransform>();
                badgeRt.sizeDelta = new Vector2(130f, 26f);
                Image badgeBg = badgeGo.AddComponent<Image>();
                badgeBg.sprite = rounded;
                badgeBg.type = Image.Type.Sliced;
                GameObject badgeLabelGo = new GameObject("Label", typeof(RectTransform));
                badgeLabelGo.transform.SetParent(badgeGo.transform, false);
                Stretch(badgeLabelGo.GetComponent<RectTransform>(), new Vector2(10f, 4f), new Vector2(-10f, -4f));
                TextMeshProUGUI badgeLabel = badgeLabelGo.AddComponent<TextMeshProUGUI>();
                badgeLabel.alignment = TextAlignmentOptions.Center;
                badgeLabel.raycastTarget = false;
                badgeGo.AddComponent<LayoutElement>().preferredWidth = 130f;
                calBadge = badgeGo.AddComponent<NorscopeBadgeControl>();
                SerializedObject badgeSo = new SerializedObject(calBadge);
                badgeSo.FindProperty("theme").objectReferenceValue = theme;
                badgeSo.FindProperty("background").objectReferenceValue = badgeBg;
                badgeSo.FindProperty("label").objectReferenceValue = badgeLabel;
                badgeSo.FindProperty("root").objectReferenceValue = badgeRt;
                badgeSo.FindProperty("size").vector2Value = new Vector2(130f, 26f);
                badgeSo.ApplyModifiedPropertiesWithoutUndo();

                // ── Component selection (developer.zip: dropdown row, then nav) ─
                CreateSectionCaption(body.transform, "Component", theme);
                GameObject compRow = CreateLayoutRow(body.transform, 38f);
                HorizontalLayoutGroup compHl = compRow.GetComponent<HorizontalLayoutGroup>();
                compHl.childControlWidth = true;
                compHl.childForceExpandWidth = false;
                dropdown = CreateThemedDropdown("ComponentSelector", compRow.transform, 0f, theme, rounded);
                LayoutElement dropdownLe = dropdown.GetComponent<LayoutElement>();
                dropdownLe.flexibleWidth = 1f;
                dropdownLe.minWidth = 220f;
                dropdownLe.preferredWidth = -1f;
                dropdownLe.preferredHeight = 38f;
                GameObject navRow = CreateLayoutRow(body.transform, 38f);
                prev = CreateThemedButton("PrevComponent", navRow.transform, "Prev", new Vector2(90f, 36f),
                    ButtonStyle.Dark, theme, rounded);
                next = CreateThemedButton("NextComponent", navRow.transform, "Next", new Vector2(90f, 36f),
                    ButtonStyle.Dark, theme, rounded);
                showSelected = CreateThemedButton("Button_ShowSelected", navRow.transform, "Show", new Vector2(100f, 36f),
                    ButtonStyle.Ghost, theme, rounded);
                GameObject navSpacer = new GameObject("Spacer", typeof(RectTransform));
                navSpacer.transform.SetParent(navRow.transform, false);
                navSpacer.AddComponent<LayoutElement>().flexibleWidth = 1f;

                // ── Selected Component info card (replaces fake hologram preview —
                // the real hologram is already visible in-world via AR) ──────────
                GameObject infoCard = CreateCard(body.transform, "SelectedComponentCard", theme, rounded,
                    tinted: false, padding: new RectOffset(16, 16, 12, 12));
                CreateTmp("SelectedCaption", infoCard.transform, "SELECTED COMPONENT", 11f, 18f, theme, Dim(theme))
                    .fontStyle = FontStyles.Bold;
                selectedComponentInfo = CreateTmp("SelectedComponentName", infoCard.transform, "—", 17f, 26f, theme, Primary(theme));
                CreateTmp("TrackingSourceNote", infoCard.transform, "Tracking Source · QR Tracking", 12f, 20f, theme, Secondary(theme));

                // ── Step size chips (full width — do NOT pack into narrow pose columns) ─
                CreateSectionCaption(body.transform, "Position step", theme);
                GameObject posSteps = CreateLayoutRow(body.transform, 36f);
                posSteps.GetComponent<HorizontalLayoutGroup>().spacing = 8f;
                pos10 = CreateStepButton("PosStep10", posSteps.transform, "10 mm", theme, rounded);
                pos2 = CreateStepButton("PosStep2", posSteps.transform, "2 mm", theme, rounded);
                pos05 = CreateStepButton("PosStep05", posSteps.transform, "0.5 mm", theme, rounded);

                CreateSectionCaption(body.transform, "Rotation step", theme);
                GameObject rotSteps = CreateLayoutRow(body.transform, 36f);
                rot5 = CreateStepButton("RotStep5", rotSteps.transform, "5°", theme, rounded);
                rot1 = CreateStepButton("RotStep1", rotSteps.transform, "1°", theme, rounded);

                // ── Position + Rotation axes side by side (Figma-style) ───────
                GameObject poseColumns = CreatePanel("PoseColumns", body.transform, false);
                LayoutElement poseColsLe = poseColumns.AddComponent<LayoutElement>();
                // Caption + 3×40 axis rows + gaps — no step chips inside columns.
                poseColsLe.minHeight = 200f;
                poseColsLe.preferredHeight = 200f;
                HorizontalLayoutGroup poseHl = poseColumns.AddComponent<HorizontalLayoutGroup>();
                poseHl.spacing = 16f;
                poseHl.padding = new RectOffset(0, 0, 0, 0);
                poseHl.childAlignment = TextAnchor.UpperLeft;
                poseHl.childControlWidth = true;
                poseHl.childControlHeight = false;
                poseHl.childForceExpandWidth = true;
                poseHl.childForceExpandHeight = false;

                GameObject posCol = CreatePoseColumn(poseColumns.transform, "PositionColumn", theme);
                CreateSectionCaption(posCol.transform, "Position", theme);
                AxisRow(posCol.transform, "Pos", "X", AxisXColor, theme, rounded, out pxn, out tx, out pxp);
                AxisRow(posCol.transform, "Pos", "Y", AxisYColor, theme, rounded, out pyn, out ty, out pyp);
                AxisRow(posCol.transform, "Pos", "Z", AxisZColor, theme, rounded, out pzn, out tz, out pzp);

                GameObject rotCol = CreatePoseColumn(poseColumns.transform, "RotationColumn", theme);
                CreateSectionCaption(rotCol.transform, "Rotation", theme);
                AxisRow(rotCol.transform, "Rot", "X", AxisXColor, theme, rounded, out rxn, out trx, out rxp);
                AxisRow(rotCol.transform, "Rot", "Y", AxisYColor, theme, rounded, out ryn, out try_, out ryp);
                AxisRow(rotCol.transform, "Rot", "Z", AxisZColor, theme, rounded, out rzn, out trz, out rzp);

                // ── Locks ────────────────────────────────────────────────────
                CreateSectionCaption(body.transform, "Locks", theme);
                GameObject lockRow = CreateLayoutRow(body.transform, 32f);
                lockH = CreateThemedToggle("LockHeight", lockRow.transform, "Lock Height", theme);
                lockR = CreateThemedToggle("LockRotation", lockRow.transform, "Lock Rotation", theme);
                lockNote = CreateTmp("LockNote", body.transform,
                    "Locks apply to precision +/− only. Direct grab stays unconstrained.", 11f, 32f, theme, Dim(theme));

                // ── Actions ──────────────────────────────────────────────────
                CreateSectionCaption(body.transform, "Actions", theme);
                // developer.zip action row: Reset · Restore ····· Save Calibration
                GameObject act1 = CreateLayoutRow(body.transform, 40f);
                reset = CreateThemedButton("Button_ResetCurrent", act1.transform, "Reset", new Vector2(100f, 36f),
                    ButtonStyle.Amber, theme, rounded);
                restore = CreateThemedButton("Button_RestoreSaved", act1.transform, "Restore", new Vector2(110f, 36f),
                    ButtonStyle.Dark, theme, rounded);
                GameObject act1Spacer = new GameObject("Spacer", typeof(RectTransform));
                act1Spacer.transform.SetParent(act1.transform, false);
                act1Spacer.AddComponent<LayoutElement>().flexibleWidth = 1f;
                save = CreateThemedButton("Button_SaveComponent", act1.transform, "Save Calibration", new Vector2(180f, 36f),
                    ButtonStyle.Primary, theme, rounded);
                GameObject act2 = CreateLayoutRow(body.transform, 36f);
                undo = CreateThemedButton("Button_Undo", act2.transform, "Undo", new Vector2(90f, 34f),
                    ButtonStyle.Ghost, theme, rounded);
                minimize = CreateThemedButton("Button_Minimize", act2.transform, "Minimize Panel", new Vector2(150f, 34f),
                    ButtonStyle.Ghost, theme, rounded);
                GameObject act2Spacer = new GameObject("Spacer", typeof(RectTransform));
                act2Spacer.transform.SetParent(act2.transform, false);
                act2Spacer.AddComponent<LayoutElement>().flexibleWidth = 1f;

                // ── Dirty (pending-nav confirm) banner ──────────────────────
                dirtyBanner = CreateCard(body.transform, "DirtyBanner", theme, rounded, tinted: true,
                    padding: new RectOffset(14, 14, 8, 8), horizontal: true);
                CreateTmp("DirtyBannerLabel", dirtyBanner.transform, "Unsaved changes — save or discard to continue",
                    12f, 32f, theme, Warning(theme));
                dirtySave = CreateThemedButton("DirtySave", dirtyBanner.transform, "Save", new Vector2(80f, 32f),
                    ButtonStyle.Primary, theme, rounded);
                dirtyDiscard = CreateThemedButton("DirtyDiscard", dirtyBanner.transform, "Discard", new Vector2(90f, 32f),
                    ButtonStyle.Dark, theme, rounded);
                dirtyCancel = CreateThemedButton("DirtyCancel", dirtyBanner.transform, "Cancel", new Vector2(80f, 32f),
                    ButtonStyle.Ghost, theme, rounded);
                dirtyBanner.SetActive(false);

                // Controllers on root
                ArCalibrationSelectionController selection =
                    root.GetComponent<ArCalibrationSelectionController>() ??
                    root.AddComponent<ArCalibrationSelectionController>();
                ArCalibrationNudgeController nudge =
                    root.GetComponent<ArCalibrationNudgeController>() ??
                    root.AddComponent<ArCalibrationNudgeController>();

                SerializedObject nso = new SerializedObject(nudge);
                nso.FindProperty("posXText").objectReferenceValue = tx;
                nso.FindProperty("posYText").objectReferenceValue = ty;
                nso.FindProperty("posZText").objectReferenceValue = tz;
                nso.FindProperty("rotXText").objectReferenceValue = trx;
                nso.FindProperty("rotYText").objectReferenceValue = try_;
                nso.FindProperty("rotZText").objectReferenceValue = trz;
                nso.FindProperty("dirtyLabel").objectReferenceValue = dirtyLabel;
                nso.FindProperty("posStep10").objectReferenceValue = pos10;
                nso.FindProperty("posStep2").objectReferenceValue = pos2;
                nso.FindProperty("posStep05").objectReferenceValue = pos05;
                nso.FindProperty("rotStep5").objectReferenceValue = rot5;
                nso.FindProperty("rotStep1").objectReferenceValue = rot1;
                nso.FindProperty("stepNormal").colorValue = Elevated(theme);
                nso.FindProperty("stepSelected").colorValue = Accent(theme);
                nso.FindProperty("posXNeg").objectReferenceValue = pxn;
                nso.FindProperty("posXPos").objectReferenceValue = pxp;
                nso.FindProperty("posYNeg").objectReferenceValue = pyn;
                nso.FindProperty("posYPos").objectReferenceValue = pyp;
                nso.FindProperty("posZNeg").objectReferenceValue = pzn;
                nso.FindProperty("posZPos").objectReferenceValue = pzp;
                nso.FindProperty("rotXNeg").objectReferenceValue = rxn;
                nso.FindProperty("rotXPos").objectReferenceValue = rxp;
                nso.FindProperty("rotYNeg").objectReferenceValue = ryn;
                nso.FindProperty("rotYPos").objectReferenceValue = ryp;
                nso.FindProperty("rotZNeg").objectReferenceValue = rzn;
                nso.FindProperty("rotZPos").objectReferenceValue = rzp;
                nso.FindProperty("lockHeightToggle").objectReferenceValue = lockH;
                nso.FindProperty("lockRotationToggle").objectReferenceValue = lockR;
                nso.FindProperty("lockNoteText").objectReferenceValue = lockNote;
                nso.FindProperty("undoButton").objectReferenceValue = undo;
                nso.FindProperty("restoreSavedButton").objectReferenceValue = restore;
                nso.FindProperty("resetCurrentButton").objectReferenceValue = reset;
                nso.FindProperty("saveButton").objectReferenceValue = save;
                nso.FindProperty("prevButton").objectReferenceValue = prev;
                nso.FindProperty("nextButton").objectReferenceValue = next;
                nso.FindProperty("minimizeButton").objectReferenceValue = minimize;
                nso.FindProperty("restorePanelButton").objectReferenceValue = restorePanel;
                nso.FindProperty("dirtyBanner").objectReferenceValue = dirtyBanner;
                nso.FindProperty("dirtySaveButton").objectReferenceValue = dirtySave;
                nso.FindProperty("dirtyDiscardButton").objectReferenceValue = dirtyDiscard;
                nso.FindProperty("dirtyCancelButton").objectReferenceValue = dirtyCancel;
                nso.FindProperty("calibrationContentRoot").objectReferenceValue = content;
                nso.FindProperty("minimizedChrome").objectReferenceValue = minimized;
                nso.ApplyModifiedPropertiesWithoutUndo();

                ArGuidancePanelController panel = root.GetComponent<ArGuidancePanelController>();
                SerializedObject pso = new SerializedObject(panel);
                pso.FindProperty("componentDropdown").objectReferenceValue = dropdown;
                pso.FindProperty("showSelectedButton").objectReferenceValue = showSelected;
                pso.FindProperty("nudgeController").objectReferenceValue = nudge;
                pso.FindProperty("selectionController").objectReferenceValue = selection;
                pso.FindProperty("calibrationView").objectReferenceValue = calib.gameObject;
                pso.FindProperty("calBadge").objectReferenceValue = calBadge;
                pso.FindProperty("calibratingNameText").objectReferenceValue = calibratingName;
                pso.FindProperty("selectedComponentInfoText").objectReferenceValue = selectedComponentInfo;
                pso.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PanelPrefab);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var log = new StringBuilder();
            log.AppendLine("=== AR Guidance Calibration UX Apply ===");
            log.AppendLine(DateTime.Now.ToString("o"));
            log.AppendLine("Panel size: 720 x 560");
            log.AppendLine("CalibrationView: scrollable CalibrationContent, NorscopeTheme-driven visuals");
            log.AppendLine("Readouts: mm / degrees");
            log.AppendLine("Defaults: 2 mm / 1°");
            log.AppendLine("Hold: 0.35 / 0.10");
            log.AppendLine("Locks: precision only (grab unconstrained)");
            log.AppendLine("Screw scope: group-level only");
            log.AppendLine("Schema unchanged");
            log.AppendLine("CalBadge: Saved / Unsaved Changes / Never Calibrated (repository-backed)");
            File.WriteAllText(Path.GetFullPath(ApplyReportPath), log.ToString());
            Debug.Log("[Norscope Calibration UX] Apply COMPLETED\n" + log);
            Notify("Calibration UX Apply", "COMPLETED");
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

            sb.AppendLine("=== AR Guidance Calibration UX Validate ===");
            sb.AppendLine(DateTime.Now.ToString("o"));

            Check(File.Exists("Assets/Norscope/Runtime/ArGuidance/Calibration/ArPressAndHoldButton.cs"), "ArPressAndHoldButton");
            Check(File.Exists("Assets/Norscope/Runtime/ArGuidance/Calibration/ArCalibrationNudgeController.cs"), "ArCalibrationNudgeController");
            Check(File.Exists("Assets/Norscope/Runtime/ArGuidance/Calibration/ArCalibrationUndoStack.cs"), "ArCalibrationUndoStack");
            Check(File.Exists("Assets/Norscope/Runtime/ArGuidance/Calibration/ArCalibrationSelectionController.cs"), "ArCalibrationSelectionController");
            Check(File.Exists(ThemePath), "NorscopeTheme asset present");
            Check(File.Exists(RoundedSpritePath), "Rounded sprite present");

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPrefab);
            Check(prefab != null, "ArGuidancePanel.prefab");
            if (prefab != null)
            {
                Check(prefab.GetComponent<ArCalibrationNudgeController>() != null, "NudgeController on panel");
                Check(prefab.GetComponent<ArCalibrationSelectionController>() != null, "SelectionController on panel");
                Check(FindDeep(prefab.transform, "CalibrationContent") != null, "CalibrationContent");
                Check(FindDeep(prefab.transform, "MinimizedChrome") != null, "MinimizedChrome");
                Check(FindDeep(prefab.transform, "DirtyBanner") != null, "DirtyBanner");
                Check(FindDeep(prefab.transform, "LockHeight") != null, "LockHeight");
                Check(FindDeep(prefab.transform, "LockRotation") != null, "LockRotation");
                Check(FindDeep(prefab.transform, "PosStep2") != null, "PosStep2");
                Check(FindDeep(prefab.transform, "RotStep1") != null, "RotStep1");
                Check(FindDeep(prefab.transform, "PoseColumns") != null, "PoseColumns side-by-side");
                Check(FindDeep(prefab.transform, "PositionColumn") != null, "PositionColumn");
                Check(FindDeep(prefab.transform, "RotationColumn") != null, "RotationColumn");
                Check(FindDeep(prefab.transform, "Button_RestoreSaved") != null, "Restore Saved");
                Check(FindDeep(prefab.transform, "Button_Undo") != null, "Undo");
                Check(FindDeep(prefab.transform, "PrevComponent") != null, "Prev");
                Check(FindDeep(prefab.transform, "NextComponent") != null, "Next");
                Check(FindDeep(prefab.transform, "CalBadge") != null, "CalBadge");
                Check(FindDeep(prefab.transform, "CurrentlyCalibratingCard") != null, "Currently Calibrating card");
                Check(FindDeep(prefab.transform, "SelectedComponentCard") != null, "Selected Component card");
                Check(prefab.GetComponentsInChildren<TMP_InputField>(true).Length == 0, "No TMP_InputField");
                Check(prefab.GetComponentsInChildren<InputField>(true).Length == 0, "No InputField");
                Transform selector = FindDeep(prefab.transform, "ComponentSelector");
                TMP_Dropdown dd = selector != null ? selector.GetComponent<TMP_Dropdown>() : null;
                Check(dd != null, "ComponentSelector TMP_Dropdown");
                Check(dd != null && dd.template != null, "Dropdown template assigned");
                Check(dd != null && dd.template != null &&
                      dd.template.GetComponentInChildren<Toggle>(true) != null &&
                      dd.template.GetComponentInChildren<Toggle>(true).transform != dd.template,
                    "Dropdown template has child Toggle item");
                Check(dd != null && dd.itemText != null, "Dropdown itemText assigned");
                // Panel size
                RectTransform rt = prefab.GetComponent<RectTransform>();
                Check(rt != null && Mathf.Approximately(rt.sizeDelta.x, 720f) && Mathf.Approximately(rt.sizeDelta.y, 560f),
                    "Panel size 720x560");
                // Hold component present on axis buttons
                int holds = prefab.GetComponentsInChildren<ArPressAndHoldButton>(true).Length;
                Check(holds >= 12, "12+ ArPressAndHoldButton (" + holds + ")");
            }

            // Defaults in controller source
            string calSrc = File.ReadAllText("Assets/Norscope/Runtime/ArGuidance/ArCalibrationController.cs");
            Check(calSrc.Contains("positionStepIndex = 1"), "Default position step index 1 (2mm)");
            Check(calSrc.Contains("rotationStepIndex = 1"), "Default rotation step index 1 (1°)");
            Check(calSrc.Contains("lockHeight"), "Lock height support");
            Check(calSrc.Contains("lockRotation"), "Lock rotation support");
            Check(calSrc.Contains("localPosition"), "Nudges use local axes");
            Check(calSrc.Contains("HasSavedCalibration"), "HasSavedCalibration signal for CalBadge");
            Check(calSrc.Contains("ArCalibrationUndoStack(20)") || calSrc.Contains("new ArCalibrationUndoStack(20)"),
                "Undo stack capacity 20");

            string holdSrc = File.ReadAllText(
                "Assets/Norscope/Runtime/ArGuidance/Calibration/ArPressAndHoldButton.cs");
            Check(holdSrc.Contains("0.35f"), "Hold initial delay 0.35s");
            Check(holdSrc.Contains("0.10f"), "Hold repeat 0.10s");

            string nudgeSrc = File.ReadAllText(
                "Assets/Norscope/Runtime/ArGuidance/Calibration/ArCalibrationNudgeController.cs");
            Check(nudgeSrc.Contains("\" mm\""), "Position readout mm");
            Check(nudgeSrc.Contains("\"°\""), "Rotation readout degrees");
            Check(!nudgeSrc.Contains("TMP_InputField") && !nudgeSrc.Contains("InputField"),
                "Nudge has no editable fields");

            Check(File.Exists("Assets/Norscope/Runtime/ArGuidance/JsonArCalibrationRepository.cs"),
                "Calibration repo present (untouched schema)");
            Check(File.Exists("Assets/Norscope/Runtime/TaskLibrary/TaskStep.cs"), "TaskStep untouched path");
            Check(File.Exists("Assets/Norscope/Runtime/ArGuidance/QrTrackingOriginProvider.cs"),
                "QR stub untouched path");

            sb.AppendLine();
            sb.AppendLine("Grab constraints: NOT applied (locks = precision +/− only)");
            sb.AppendLine("Screw calibration: group-level only");
            sb.AppendLine("Schema version: unchanged");
            sb.AppendLine(pass ? "STATUS PASS" : "STATUS FAIL");
            report = sb.ToString();
            return pass;
        }

        // ── Theme token helpers ─────────────────────────────────────────────
        private enum ButtonStyle { Primary, Dark, Ghost, Amber }

        private static Color PanelBg(NorscopeTheme t) => t != null ? t.panelBackground : FallbackPanelBg;
        private static Color Card(NorscopeTheme t) => t != null ? t.elevatedPanelBackground : FallbackBtn;
        private static Color Elevated(NorscopeTheme t) => t != null ? t.elevatedPanelBackground : FallbackBtn;
        private static Color Accent(NorscopeTheme t) => t != null ? t.primaryAccent : FallbackCyan;
        private static Color BorderColor(NorscopeTheme t) => t != null ? t.border : new Color(0.15f, 0.19f, 0.26f, 1f);
        private static Color Primary(NorscopeTheme t) => t != null ? t.primaryText : Color.white;
        private static Color Secondary(NorscopeTheme t) => t != null ? t.secondaryText : new Color(0.63f, 0.66f, 0.70f, 1f);
        private static Color Dim(NorscopeTheme t) => t != null ? t.disabled : new Color(0.40f, 0.44f, 0.52f, 1f);
        private static Color Warning(NorscopeTheme t) => t != null ? t.warning : new Color(1f, 0.76f, 0.28f, 1f);

        private static TMP_FontAsset HeadingFont(NorscopeTheme t) =>
            t != null && t.fonts != null && t.fonts.headingFont != null ? t.fonts.headingFont : null;
        private static TMP_FontAsset BodyFont(NorscopeTheme t) =>
            t != null && t.fonts != null && t.fonts.bodyFont != null ? t.fonts.bodyFont : null;

        private static void AxisRow(Transform parent, string prefix, string axis, Color axisColor,
            NorscopeTheme theme, Sprite rounded,
            out ArPressAndHoldButton neg, out TMP_Text value, out ArPressAndHoldButton pos)
        {
            // Compact so two columns fit the panel without overlapping.
            GameObject row = CreateLayoutRow(parent, 38f);
            HorizontalLayoutGroup rowHl = row.GetComponent<HorizontalLayoutGroup>();
            rowHl.spacing = 6f;
            rowHl.childControlWidth = true;
            rowHl.childForceExpandWidth = false;

            GameObject badge = new GameObject("Axis" + prefix + axis, typeof(RectTransform));
            badge.transform.SetParent(row.transform, false);
            badge.GetComponent<RectTransform>().sizeDelta = new Vector2(24f, 24f);
            Image badgeImg = badge.AddComponent<Image>();
            badgeImg.sprite = rounded;
            badgeImg.type = Image.Type.Sliced;
            badgeImg.color = new Color(axisColor.r, axisColor.g, axisColor.b, 0.16f);
            TMP_Text badgeLabel = CreateTmp("Label", badge.transform, axis, 12f, 24f, theme, axisColor);
            badgeLabel.alignment = TextAlignmentOptions.Center;
            Stretch(badgeLabel.rectTransform, Vector2.zero, Vector2.zero);
            LayoutElement badgeLe = badge.AddComponent<LayoutElement>();
            badgeLe.preferredWidth = 24f;
            badgeLe.minWidth = 24f;
            badgeLe.preferredHeight = 24f;

            Button nb = CreateThemedButton(prefix + axis + "Neg", row.transform, "−", new Vector2(36f, 32f),
                ButtonStyle.Dark, theme, rounded);
            value = CreateTmp(prefix + axis + "Value", row.transform, "0.0", 13f, 32f, theme, Primary(theme));
            value.alignment = TextAlignmentOptions.Center;
            LayoutElement valueLe = value.gameObject.GetComponent<LayoutElement>();
            valueLe.flexibleWidth = 1f;
            valueLe.preferredWidth = 64f;
            valueLe.minWidth = 56f;
            GameObject valueBg = new GameObject("ValueBg", typeof(RectTransform));
            valueBg.transform.SetParent(value.transform.parent, false);
            valueBg.transform.SetSiblingIndex(value.transform.GetSiblingIndex());
            Image valueBgImg = valueBg.AddComponent<Image>();
            valueBgImg.sprite = rounded;
            valueBgImg.type = Image.Type.Sliced;
            valueBgImg.color = new Color(1f, 1f, 1f, 0.03f);
            valueBgImg.raycastTarget = false;
            valueBg.GetComponent<RectTransform>().sizeDelta = new Vector2(64f, 32f);
            LayoutElement valueBgLe = valueBg.AddComponent<LayoutElement>();
            valueBgLe.flexibleWidth = 1f;
            valueBgLe.preferredWidth = 64f;
            valueBgLe.minWidth = 56f;
            valueBgLe.preferredHeight = 32f;
            value.transform.SetParent(valueBg.transform, false);
            Stretch(value.rectTransform, Vector2.zero, Vector2.zero);

            Button pb = CreateThemedButton(prefix + axis + "Pos", row.transform, "+", new Vector2(36f, 32f),
                ButtonStyle.Dark, theme, rounded);
            neg = nb.gameObject.AddComponent<ArPressAndHoldButton>();
            pos = pb.gameObject.AddComponent<ArPressAndHoldButton>();
        }

        private static GameObject CreatePoseColumn(Transform parent, string name, NorscopeTheme theme)
        {
            GameObject col = CreatePanel(name, parent, false);
            LayoutElement le = col.AddComponent<LayoutElement>();
            le.flexibleWidth = 1f;
            le.minWidth = 280f;
            VerticalLayoutGroup vl = col.AddComponent<VerticalLayoutGroup>();
            vl.spacing = 8f;
            vl.padding = new RectOffset(0, 0, 0, 0);
            vl.childAlignment = TextAnchor.UpperLeft;
            vl.childControlWidth = true;
            vl.childControlHeight = false;
            vl.childForceExpandWidth = true;
            vl.childForceExpandHeight = false;
            return col;
        }

        private static Button CreateStepButton(string name, Transform parent, string label,
            NorscopeTheme theme, Sprite rounded)
        {
            return CreateThemedButton(name, parent, label, new Vector2(88f, 34f), ButtonStyle.Dark, theme, rounded);
        }

        private static GameObject CreateLayoutRow(Transform parent, float height)
        {
            GameObject row = CreatePanel("Row", parent, false);
            LayoutElement le = row.AddComponent<LayoutElement>();
            le.minHeight = height;
            le.preferredHeight = height;
            HorizontalLayoutGroup h = row.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 8f;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = false;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = true;
            return row;
        }

        private static void CreateSectionCaption(Transform parent, string text, NorscopeTheme theme)
        {
            TMP_Text t = CreateTmp("Section", parent, text.ToUpperInvariant(), 12f, 22f, theme, Accent(theme));
            t.fontStyle = FontStyles.Bold;
            LayoutElement le = t.gameObject.GetComponent<LayoutElement>();
            le.minHeight = 22f;
            le.preferredHeight = 22f;
        }

        private static GameObject CreateCard(Transform parent, string name, NorscopeTheme theme, Sprite rounded,
            bool tinted, RectOffset padding, bool horizontal = false)
        {
            GameObject card = CreatePanel(name, parent, false);
            Image bg = card.AddComponent<Image>();
            bg.sprite = rounded;
            bg.type = Image.Type.Sliced;
            Color accent = Accent(theme);
            bg.color = tinted ? new Color(accent.r, accent.g, accent.b, 0.06f) : Card(theme);

            if (horizontal)
            {
                HorizontalLayoutGroup hl = card.AddComponent<HorizontalLayoutGroup>();
                hl.padding = padding;
                hl.spacing = 10f;
                hl.childAlignment = TextAnchor.MiddleLeft;
                hl.childControlWidth = false;
                hl.childControlHeight = true;
                hl.childForceExpandWidth = false;
                hl.childForceExpandHeight = true;
                LayoutElement le = card.AddComponent<LayoutElement>();
                le.minHeight = 48f;
                le.preferredHeight = 48f;
            }
            else
            {
                VerticalLayoutGroup vl = card.AddComponent<VerticalLayoutGroup>();
                vl.padding = padding;
                vl.spacing = 6f;
                vl.childControlHeight = false;
                vl.childControlWidth = true;
                vl.childForceExpandHeight = false;
                vl.childForceExpandWidth = true;
                ContentSizeFitter csf = card.AddComponent<ContentSizeFitter>();
                csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                LayoutElement le = card.AddComponent<LayoutElement>();
                le.flexibleWidth = 1f;
            }

            return card;
        }

        private static GameObject CreatePanel(string name, Transform parent, bool stretch)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            if (stretch)
                Stretch(go.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);
            return go;
        }

        private static void Stretch(RectTransform rt, Vector2 min, Vector2 max)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = min;
            rt.offsetMax = max;
        }

        private static Button CreateThemedButton(string name, Transform parent, string label, Vector2 size,
            ButtonStyle style, NorscopeTheme theme, Sprite rounded)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = size;

            Image img = go.AddComponent<Image>();
            img.sprite = rounded;
            img.type = Image.Type.Sliced;
            Color textColor;
            switch (style)
            {
                case ButtonStyle.Primary:
                    img.color = Accent(theme);
                    textColor = new Color(0.02f, 0.05f, 0.06f, 1f);
                    break;
                case ButtonStyle.Amber:
                    Color warn = Warning(theme);
                    img.color = new Color(warn.r, warn.g, warn.b, 0.16f);
                    textColor = warn;
                    break;
                case ButtonStyle.Ghost:
                    img.color = new Color(1f, 1f, 1f, 0.03f);
                    textColor = Secondary(theme);
                    break;
                default: // Dark
                    img.color = Elevated(theme);
                    textColor = Primary(theme);
                    break;
            }

            Button btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            colors.pressedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0.4f);
            btn.colors = colors;

            TMP_Text tmp = CreateTmp("Label", go.transform, label, 14f, size.y - 4f, theme, textColor);
            RectTransform tr = tmp.rectTransform;
            Stretch(tr, new Vector2(4f, 2f), new Vector2(-4f, -2f));
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.minWidth = size.x;
            le.preferredWidth = size.x;
            le.minHeight = size.y;
            le.preferredHeight = size.y;
            return btn;
        }

        private static TMP_Text CreateTmp(string name, Transform parent, string text, float size, float height,
            NorscopeTheme theme, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(160f, height);
            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            if (BodyFont(theme) != null)
                tmp.font = BodyFont(theme);
            tmp.raycastTarget = false;
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.minHeight = height;
            le.preferredHeight = height;
            le.flexibleWidth = 1f;
            return tmp;
        }

        private static TMP_Dropdown CreateThemedDropdown(string name, Transform parent, float width,
            NorscopeTheme theme, Sprite rounded)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(width, 36f);
            Image img = go.AddComponent<Image>();
            img.sprite = rounded;
            img.type = Image.Type.Sliced;
            img.color = Elevated(theme);
            TMP_Dropdown dd = go.AddComponent<TMP_Dropdown>();
            TMP_Text caption = CreateTmp("Label", go.transform, "component", 13f, 32f, theme, Primary(theme));
            Stretch(caption.rectTransform, new Vector2(12f, 2f), new Vector2(-28f, -2f));
            dd.captionText = caption as TextMeshProUGUI;
            EnsureDropdownTemplate(dd, width, theme, rounded);
            LayoutElement le = go.AddComponent<LayoutElement>();
            if (width > 0f)
            {
                le.minWidth = width;
                le.preferredWidth = width;
            }
            else
            {
                le.minWidth = 220f;
                le.flexibleWidth = 1f;
            }
            le.minHeight = 36f;
            le.preferredHeight = 36f;
            return dd;
        }

        /// <summary>
        /// Builds a valid TMP_Dropdown Template with an Item Toggle child.
        /// Required by TMP_Dropdown.SetupTemplate / Show.
        /// </summary>
        private static void EnsureDropdownTemplate(TMP_Dropdown dropdown, float width, NorscopeTheme theme, Sprite rounded)
        {
            if (dropdown == null)
                return;

            Transform existing = dropdown.transform.Find("Template");
            if (existing != null)
                UnityEngine.Object.DestroyImmediate(existing.gameObject);

            var templateGo = new GameObject("Template", typeof(RectTransform), typeof(Image));
            templateGo.transform.SetParent(dropdown.transform, false);
            RectTransform templateRt = templateGo.GetComponent<RectTransform>();
            templateRt.anchorMin = new Vector2(0f, 0f);
            templateRt.anchorMax = new Vector2(1f, 0f);
            templateRt.pivot = new Vector2(0.5f, 1f);
            templateRt.anchoredPosition = new Vector2(0f, 2f);
            templateRt.sizeDelta = new Vector2(0f, 200f);
            Image templateImg = templateGo.GetComponent<Image>();
            templateImg.sprite = rounded;
            templateImg.type = Image.Type.Sliced;
            templateImg.color = PanelBg(theme);
            templateImg.raycastTarget = true;

            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewportGo.transform.SetParent(templateGo.transform, false);
            RectTransform viewportRt = viewportGo.GetComponent<RectTransform>();
            Stretch(viewportRt, Vector2.zero, Vector2.zero);
            Image viewportImg = viewportGo.GetComponent<Image>();
            viewportImg.color = Color.white;
            viewportImg.raycastTarget = true;
            Mask mask = viewportGo.GetComponent<Mask>();
            mask.showMaskGraphic = false;

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(viewportGo.transform, false);
            RectTransform contentRt = contentGo.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = new Vector2(0f, 36f);

            var itemGo = new GameObject("Item", typeof(RectTransform), typeof(Image), typeof(Toggle));
            itemGo.transform.SetParent(contentGo.transform, false);
            RectTransform itemRt = itemGo.GetComponent<RectTransform>();
            itemRt.anchorMin = new Vector2(0f, 0.5f);
            itemRt.anchorMax = new Vector2(1f, 0.5f);
            itemRt.pivot = new Vector2(0.5f, 0.5f);
            itemRt.sizeDelta = new Vector2(0f, 34f);
            Image itemBg = itemGo.GetComponent<Image>();
            itemBg.color = Elevated(theme);
            itemBg.raycastTarget = true;
            Toggle itemToggle = itemGo.GetComponent<Toggle>();
            itemToggle.targetGraphic = itemBg;
            itemToggle.isOn = true;
            itemToggle.toggleTransition = Toggle.ToggleTransition.Fade;

            var checkGo = new GameObject("Item Checkmark", typeof(RectTransform), typeof(Image));
            checkGo.transform.SetParent(itemGo.transform, false);
            RectTransform checkRt = checkGo.GetComponent<RectTransform>();
            checkRt.anchorMin = checkRt.anchorMax = new Vector2(0f, 0.5f);
            checkRt.pivot = new Vector2(0f, 0.5f);
            checkRt.anchoredPosition = new Vector2(8f, 0f);
            checkRt.sizeDelta = new Vector2(16f, 16f);
            Image checkImg = checkGo.GetComponent<Image>();
            checkImg.color = Accent(theme);
            checkImg.raycastTarget = false;
            itemToggle.graphic = checkImg;

            TMP_Text itemLabel = CreateTmp("Item Label", itemGo.transform, "Option", 13f, 30f, theme, Primary(theme));
            RectTransform itemLabelRt = itemLabel.rectTransform;
            itemLabelRt.anchorMin = Vector2.zero;
            itemLabelRt.anchorMax = Vector2.one;
            itemLabelRt.offsetMin = new Vector2(28f, 2f);
            itemLabelRt.offsetMax = new Vector2(-8f, -2f);
            itemLabel.raycastTarget = false;

            ScrollRect scroll = templateGo.AddComponent<ScrollRect>();
            scroll.content = contentRt;
            scroll.viewport = viewportRt;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            dropdown.template = templateRt;
            dropdown.itemText = itemLabel as TextMeshProUGUI;
            dropdown.captionText = dropdown.captionText; // keep existing
            templateGo.SetActive(false);
        }

        /// <summary>Surgical fix: assign a valid TMP template on ComponentSelector only.</summary>
        [MenuItem(Menu + "6) Fix Dropdown Template")]
        public static void FixDropdownTemplateMenu()
        {
            try
            {
                FixDropdownTemplateInternal();
                Notify("Calibration UX", "Dropdown template assigned.");
            }
            catch (Exception ex)
            {
                Debug.LogError("[Norscope Calibration UX] Fix Dropdown FAILED:\n" + ex);
                Notify("Calibration UX", "FAILED\n" + ex.Message);
            }
        }

        public static void FixDropdownTemplateInternal()
        {
            NorscopeTheme theme = AssetDatabase.LoadAssetAtPath<NorscopeTheme>(ThemePath);
            Sprite rounded = AssetDatabase.LoadAssetAtPath<Sprite>(RoundedSpritePath);
            GameObject root = PrefabUtility.LoadPrefabContents(PanelPrefab);
            try
            {
                Transform selector = FindDeep(root.transform, "ComponentSelector");
                if (selector == null)
                    throw new Exception("ComponentSelector missing on ArGuidancePanel");
                TMP_Dropdown dropdown = selector.GetComponent<TMP_Dropdown>();
                if (dropdown == null)
                    throw new Exception("TMP_Dropdown missing on ComponentSelector");

                float width = dropdown.GetComponent<RectTransform>().sizeDelta.x;
                if (width < 10f)
                    width = 300f;
                EnsureDropdownTemplate(dropdown, width, theme, rounded);
                PrefabUtility.SaveAsPrefabAsset(root, PanelPrefab);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Norscope Calibration UX] Dropdown template FIXED on ComponentSelector");
        }

        private static Toggle CreateThemedToggle(string name, Transform parent, string label, NorscopeTheme theme)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(180f, 32f);
            Image hit = go.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0.02f);
            Toggle toggle = go.AddComponent<Toggle>();
            toggle.targetGraphic = hit;
            CreateTmp("Label", go.transform, label, 13f, 30f, theme, Secondary(theme));
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.minWidth = 180f;
            le.preferredWidth = 180f;
            le.minHeight = 32f;
            return toggle;
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
    }
}
