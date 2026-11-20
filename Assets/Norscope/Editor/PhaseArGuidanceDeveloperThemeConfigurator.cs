using System;
using System.IO;
using System.Text;
using Norscope.ArGuidance;
using Norscope.Navigation;
using Norscope.UI;
using Norscope.Windows;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Norscope.Editor
{
    /// <summary>
    /// AR Guidance Developer-panel theme pass — Capture / Apply / Validate / Restore.
    ///
    /// Owns the panel chrome that PhaseArGuidanceConfigurator ("Phase 1") originally built:
    /// Background/Border, TitleBar, EquipmentLabel/TrackingStatusLabel, ModeTabs, and
    /// PreviewView (everything in it EXCEPT "PreviewGuidanceRow", which stays owned by
    /// PhaseArGuidanceBatteryTaskIntegrationConfigurator). Plus a new Footer row.
    ///
    /// IMPORTANT: PhaseArGuidanceConfigurator.ApplyMenu() ("Phase 1") is stale — it still
    /// wires SerializedObject fields (saveButton, posXNeg, rotXNeg, ...) that no longer exist
    /// on ArGuidancePanelController after the nudge/selection-controller architecture replaced
    /// them, and it fully regenerates the AR hologram prefabs from scratch on every Apply. Do
    /// NOT run "Norscope/HoloLens/AR Guidance/Phase 1/3) Apply" — it will throw partway through
    /// and will have already overwritten the hologram prefabs before that happens. This script
    /// uses the same safe, scoped PrefabUtility.LoadPrefabContents pattern as the Calibration UX
    /// and Battery Task Integration configurators instead of a from-scratch rebuild, and never
    /// touches hologram prefabs, Bootstrap wiring, or the equipment profile.
    /// </summary>
    public static class PhaseArGuidanceDeveloperThemeConfigurator
    {
        private const string Menu = "Norscope/HoloLens/AR Guidance/Developer Theme/";
        private const string BackupKey = "Norscope.ArGuidanceDeveloperTheme.Backup";
        private const string BackupRoot = "Assets/Norscope/UI/Panels/_Backups";
        private const string ApplyReportPath = "Temp/NorscopeArGuidanceDeveloperThemeApply.txt";
        private const string ValidateReportPath = "Temp/NorscopeArGuidanceDeveloperThemeValidate.txt";
        private const string PanelPrefab = "Assets/Norscope/UI/Panels/ArGuidancePanel.prefab";
        private const string ThemePath = "Assets/Norscope/UI/DesignSystem/Themes/NorscopeTheme.asset";
        private const string RoundedSpritePath = "Assets/Norscope/UI/DesignSystem/Sprites/UIRounded_8.png";

        private static readonly (string objectName, string title, string subtitle, string monogram)[] PreviewCards =
        {
            ("Toggle_LaptopOutline", "Back Panel", "Rear housing outline", "BP"),
            ("Toggle_LiftEdge", "Lift Edge", "Pry point", "LE"),
            ("Toggle_BatteryConnector", "Battery Connector", "Battery connector", "BC"),
            ("Toggle_CoolingFan", "Cooling Fan", "Intake assembly", "CF"),
            ("Toggle_FanAirflow", "Fan Airflow", "Exhaust path", "FA"),
        };

        [MenuItem(Menu + "1) Capture Baseline")]
        public static void CaptureBaselineMenu()
        {
            string folder = BackupRoot + "/ArGuidanceDeveloperTheme_" +
                            DateTime.Now.ToString("yyyyMMdd_HHmmss");
            Directory.CreateDirectory(folder);
            if (File.Exists(PanelPrefab))
                File.Copy(PanelPrefab, folder + "/ArGuidancePanel.prefab", true);
            EditorPrefs.SetString(BackupKey, folder);
            Debug.Log("[Norscope Developer Theme] Baseline → " + folder);
            Notify("Developer Theme", "Baseline captured.");
        }

        [MenuItem(Menu + "2) Apply")]
        public static void ApplyMenu()
        {
            try
            {
                ApplyInternal();
            }
            catch (Exception ex)
            {
                Debug.LogError("[Norscope Developer Theme] Apply FAILED:\n" + ex);
                Notify("Developer Theme Apply", "FAILED\n" + ex.Message);
            }
        }

        [MenuItem(Menu + "3) Validate")]
        public static void ValidateMenu()
        {
            bool pass = Validate(out string report);
            Directory.CreateDirectory(Path.GetFullPath("Temp"));
            File.WriteAllText(Path.GetFullPath(ValidateReportPath), report);
            Debug.Log(pass
                ? "[Norscope Developer Theme] Validate PASS\n" + report
                : "[Norscope Developer Theme] Validate FAIL\n" + report);
            Notify("Developer Theme Validate", pass ? "PASS" : "FAIL");
        }

        [MenuItem(Menu + "4) Restore Baseline")]
        public static void RestoreMenu()
        {
            string folder = EditorPrefs.GetString(BackupKey, string.Empty);
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                Debug.LogWarning("[Norscope Developer Theme] No baseline.");
                return;
            }

            string src = folder + "/ArGuidancePanel.prefab";
            if (File.Exists(src))
                File.Copy(src, PanelPrefab, true);
            AssetDatabase.Refresh();
            Debug.Log("[Norscope Developer Theme] Restored from " + folder);
        }

        public static void CaptureApplyValidate()
        {
            if (string.IsNullOrEmpty(EditorPrefs.GetString(BackupKey, string.Empty)))
                CaptureBaselineMenu();
            ApplyInternal();
            if (!Validate(out string report))
                throw new Exception(report);
            Directory.CreateDirectory(Path.GetFullPath("Temp"));
            File.WriteAllText(Path.GetFullPath(ValidateReportPath), report);
        }

        /// <summary>
        /// Batch: re-apply Developer Preview grid + Task Preview strip + Calibration UX
        /// so Preview/Calibration buttons stop overlapping. Does not move hologram prefabs.
        /// </summary>
        public static void FixOverlapBatch()
        {
            try
            {
                ApplyInternal();
                PhaseArGuidanceBatteryTaskIntegrationConfigurator.RefreshPreviewGuidanceRow();
                PhaseArGuidanceCalibrationUxConfigurator.CaptureApplyValidate();
                Debug.Log("[Norscope Developer Theme] FixOverlapBatch PASS");
                if (Application.isBatchMode)
                    EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError("[Norscope Developer Theme] FixOverlapBatch FAILED:\n" + ex);
                if (Application.isBatchMode)
                    EditorApplication.Exit(1);
            }
        }

        private static void Notify(string title, string message)
        {
            if (Application.isBatchMode)
                return;
            EditorUtility.DisplayDialog(title, message, "OK");
        }

        private static void ApplyInternal()
        {
            Directory.CreateDirectory(Path.GetFullPath("Temp"));
            NorscopeTheme theme = AssetDatabase.LoadAssetAtPath<NorscopeTheme>(ThemePath);
            Sprite rounded = AssetDatabase.LoadAssetAtPath<Sprite>(RoundedSpritePath);
            if (theme == null)
                Debug.LogWarning("[Norscope Developer Theme] NorscopeTheme asset not found at " +
                                  ThemePath + " — using fallback colors.");

            GameObject root = PrefabUtility.LoadPrefabContents(PanelPrefab);
            try
            {
                RectTransform rootRt = root.GetComponent<RectTransform>();
                rootRt.sizeDelta = new Vector2(720f, 560f);

                // ── Background / Border ─────────────────────────────────────
                Transform bg = FindDeep(root.transform, "Background");
                if (bg == null) throw new Exception("Background missing");
                Image bgImg = bg.GetComponent<Image>() ?? bg.gameObject.AddComponent<Image>();
                bgImg.color = PanelBg(theme);

                Transform border = FindDeep(root.transform, "Border");
                if (border == null) throw new Exception("Border missing");
                Image borderImg = border.GetComponent<Image>() ?? border.gameObject.AddComponent<Image>();
                borderImg.color = BorderColor(theme);
                borderImg.raycastTarget = false;

                // ── TitleBar (rebuilt in place) ─────────────────────────────
                Transform titleBar = FindDeep(root.transform, "TitleBar");
                if (titleBar == null) throw new Exception("TitleBar missing");
                for (int i = titleBar.childCount - 1; i >= 0; i--)
                    UnityEngine.Object.DestroyImmediate(titleBar.GetChild(i).gameObject);

                RectTransform titleBarRt = titleBar.GetComponent<RectTransform>();
                titleBarRt.anchorMin = new Vector2(0f, 1f);
                titleBarRt.anchorMax = new Vector2(1f, 1f);
                titleBarRt.pivot = new Vector2(0.5f, 1f);
                titleBarRt.offsetMin = new Vector2(0f, -62f);
                titleBarRt.offsetMax = new Vector2(0f, 0f);
                Image titleBarBg = titleBar.gameObject.GetComponent<Image>() ?? titleBar.gameObject.AddComponent<Image>();
                titleBarBg.color = new Color(Elevated(theme).r, Elevated(theme).g, Elevated(theme).b, 0.5f);
                titleBarBg.raycastTarget = false;

                Button back = CreateUiButton("BackButton", titleBar, "‹ Back", ButtonStyle.Ghost, theme, rounded,
                    new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -14f), new Vector2(84f, 34f));
                TMP_Text title = CreateUiTmp("Title", titleBar, "Developer Mode", 17f, theme, Primary(theme),
                    new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(280f, 30f));
                title.alignment = TextAlignmentOptions.Center;
                if (HeadingFont(theme) != null) title.font = HeadingFont(theme);

                GameObject grab = CreateUiPanel("GrabRegion", titleBar,
                    new Vector2(0.30f, 0f), new Vector2(0.80f, 1f), Vector2.zero, Vector2.zero);
                grab.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.01f);

                Button close = CreateUiButton("CloseButton", titleBar, "✕", ButtonStyle.Ghost, theme, rounded,
                    new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-14f, -14f), new Vector2(30f, 30f));

                // Re-wire structural bindings that lived on the old TitleBar children.
                PanelNavigationBinding nav = root.GetComponent<PanelNavigationBinding>();
                if (nav != null)
                {
                    SerializedObject navSo = new SerializedObject(nav);
                    navSo.FindProperty("backButton").objectReferenceValue = back;
                    navSo.ApplyModifiedPropertiesWithoutUndo();
                }

                SpatialWindowBindings bindings = root.GetComponent<SpatialWindowBindings>();
                if (bindings != null)
                {
                    SerializedObject bindSo = new SerializedObject(bindings);
                    bindSo.FindProperty("grabRegion").objectReferenceValue = grab.transform;
                    bindSo.FindProperty("closeButtonRoot").objectReferenceValue = close.transform;
                    bindSo.ApplyModifiedPropertiesWithoutUndo();
                }

                // ── Equipment / tracking-status row ─────────────────────────
                Transform equipmentT = FindDeep(root.transform, "EquipmentLabel");
                Transform trackingT = FindDeep(root.transform, "TrackingStatusLabel");
                if (equipmentT == null || trackingT == null)
                    throw new Exception("EquipmentLabel/TrackingStatusLabel missing");
                Transform statusParent = equipmentT.parent;
                UnityEngine.Object.DestroyImmediate(equipmentT.gameObject);
                UnityEngine.Object.DestroyImmediate(trackingT.gameObject);

                TMP_Text equipment = CreateUiTmp("EquipmentLabel", statusParent, "Dell Precision 3570", 13f,
                    theme, Secondary(theme),
                    new Vector2(0f, 1f), new Vector2(16f, -70f), new Vector2(300f, 22f));

                TMP_Text tracking = CreateUiTmp("TrackingStatusLabel", statusParent,
                    "Manual Test Origin · SIMULATION ORIGIN", 12f, theme, Secondary(theme),
                    new Vector2(1f, 1f), new Vector2(-16f, -70f), new Vector2(270f, 22f));
                tracking.alignment = TextAlignmentOptions.Right;

                Transform existingDot = FindDeep(root.transform, "StatusDot");
                if (existingDot != null)
                    UnityEngine.Object.DestroyImmediate(existingDot.gameObject);
                GameObject statusDot = new GameObject("StatusDot", typeof(RectTransform));
                statusDot.transform.SetParent(statusParent, false);
                RectTransform dotRt = statusDot.GetComponent<RectTransform>();
                dotRt.anchorMin = dotRt.anchorMax = new Vector2(1f, 1f);
                dotRt.pivot = new Vector2(1f, 0.5f);
                dotRt.anchoredPosition = new Vector2(-300f, -81f);
                dotRt.sizeDelta = new Vector2(7f, 7f);
                Image dotImg = statusDot.AddComponent<Image>();
                dotImg.sprite = rounded;
                dotImg.type = Image.Type.Sliced;
                // Simulation origin, not a real live QR lock — an honest neutral/amber dot
                // rather than a green "connected" one that would misrepresent tracking state.
                dotImg.color = Warning(theme);
                dotImg.raycastTarget = false;

                // ── ModeTabs (underline style) ───────────────────────────────
                Transform modeTabs = FindDeep(root.transform, "ModeTabs");
                if (modeTabs == null) throw new Exception("ModeTabs missing");
                for (int i = modeTabs.childCount - 1; i >= 0; i--)
                    UnityEngine.Object.DestroyImmediate(modeTabs.GetChild(i).gameObject);
                RectTransform modeTabsRt = modeTabs.GetComponent<RectTransform>();
                modeTabsRt.anchorMin = new Vector2(0f, 1f);
                modeTabsRt.anchorMax = new Vector2(1f, 1f);
                modeTabsRt.pivot = new Vector2(0.5f, 1f);
                modeTabsRt.offsetMin = new Vector2(0f, -136f);
                modeTabsRt.offsetMax = new Vector2(0f, -96f);
                Image tabsBorder = modeTabs.gameObject.GetComponent<Image>() ?? modeTabs.gameObject.AddComponent<Image>();
                tabsBorder.color = new Color(1f, 1f, 1f, 0.02f);
                tabsBorder.raycastTarget = false;

                Button previewTab = CreateUiTab("PreviewTab", modeTabs, "Preview", theme,
                    new Vector2(16f, -0f), new Vector2(110f, 40f), out GameObject previewUnderline);
                Button calibTab = CreateUiTab("CalibrationTab", modeTabs, "Calibration", theme,
                    new Vector2(130f, -0f), new Vector2(120f, 40f), out GameObject calibUnderline);

                // ── PreviewView (2-column card grid; PreviewGuidanceRow preserved) ──
                Transform previewView = FindDeep(root.transform, "PreviewView");
                if (previewView == null) throw new Exception("PreviewView missing");
                RectTransform previewRt = previewView.GetComponent<RectTransform>();
                previewRt.anchorMin = new Vector2(0f, 0f);
                previewRt.anchorMax = new Vector2(1f, 1f);
                previewRt.offsetMin = new Vector2(0f, 34f); // clears the 26px Footer + margin
                previewRt.offsetMax = new Vector2(0f, -136f);

                Transform previewGuidanceRow = FindDeep(previewView, "PreviewGuidanceRow");
                for (int i = previewView.childCount - 1; i >= 0; i--)
                {
                    Transform child = previewView.GetChild(i);
                    if (child != previewGuidanceRow)
                        UnityEngine.Object.DestroyImmediate(child.gameObject);
                }

                // Scrollable card area — 14 cards (5 non-screw + 9 independent H04 screws)
                // don't fit the panel's fixed height, so this mirrors CalibrationView's
                // ScrollRect/Viewport/GridLayoutGroup+ContentSizeFitter pattern instead of a
                // fixed-height grid.
                GameObject scrollArea = new GameObject("PreviewScroll", typeof(RectTransform));
                scrollArea.transform.SetParent(previewView, false);
                scrollArea.transform.SetAsFirstSibling();
                RectTransform scrollRt = scrollArea.GetComponent<RectTransform>();
                scrollRt.anchorMin = Vector2.zero;
                scrollRt.anchorMax = Vector2.one;
                // Bottom clears Task Preview strip (developer.zip PreviewTab); top clears Hide All.
                scrollRt.offsetMin = new Vector2(16f, 148f);
                scrollRt.offsetMax = new Vector2(-16f, -46f);
                ScrollRect scroll = scrollArea.AddComponent<ScrollRect>();
                scroll.horizontal = false;
                scroll.vertical = true;
                scroll.movementType = ScrollRect.MovementType.Clamped;
                scroll.scrollSensitivity = 24f;

                GameObject viewport = new GameObject("Viewport", typeof(RectTransform));
                viewport.transform.SetParent(scrollArea.transform, false);
                RectTransform viewportRt = viewport.GetComponent<RectTransform>();
                viewportRt.anchorMin = Vector2.zero;
                viewportRt.anchorMax = Vector2.one;
                viewportRt.offsetMin = Vector2.zero;
                viewportRt.offsetMax = Vector2.zero;
                Mask mask = viewport.AddComponent<Mask>();
                mask.showMaskGraphic = false;
                Image viewportImg = viewport.AddComponent<Image>();
                viewportImg.color = new Color(1f, 1f, 1f, 0.01f);

                GameObject cardGrid = new GameObject("CardGrid", typeof(RectTransform));
                cardGrid.transform.SetParent(viewport.transform, false);
                RectTransform gridRt = cardGrid.GetComponent<RectTransform>();
                gridRt.anchorMin = new Vector2(0f, 1f);
                gridRt.anchorMax = new Vector2(1f, 1f);
                gridRt.pivot = new Vector2(0.5f, 1f);
                gridRt.anchoredPosition = Vector2.zero;
                gridRt.sizeDelta = new Vector2(0f, 0f); // height auto-fit below
                GridLayoutGroup grid = cardGrid.AddComponent<GridLayoutGroup>();
                grid.padding = new RectOffset(0, 0, 0, 8);
                // developer.zip PreviewTab: 2-col grid, gap 10; cards need room for Show/Visible.
                grid.spacing = new Vector2(10f, 10f);
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = 2;
                // Panel content width ≈ 688; two cells + spacing must fit without overlap.
                grid.cellSize = new Vector2(334f, 104f);
                grid.childAlignment = TextAnchor.UpperLeft;
                ContentSizeFitter cardGridFitter = cardGrid.AddComponent<ContentSizeFitter>();
                cardGridFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                scroll.viewport = viewportRt;
                scroll.content = gridRt;

                Toggle[] toggles = new Toggle[PreviewCards.Length];
                TMP_Text[] toggleLabels = new TMP_Text[PreviewCards.Length];
                for (int i = 0; i < PreviewCards.Length; i++)
                {
                    var (objectName, cardTitle, subtitle, monogram) = PreviewCards[i];
                    BuildPreviewCard(cardGrid.transform, objectName, cardTitle, subtitle, monogram,
                        theme, rounded, out Toggle toggle, out TMP_Text stateLabel);
                    toggles[i] = toggle;
                    toggleLabels[i] = stateLabel;
                }

                Toggle[] h04Toggles = new Toggle[ArCalibrationIds.H04ScrewCount];
                TMP_Text[] h04ToggleLabels = new TMP_Text[ArCalibrationIds.H04ScrewCount];
                for (int i = 0; i < ArCalibrationIds.H04ScrewCount; i++)
                {
                    int screwNumber = i + 1;
                    string objectName = "Toggle_H04Screw" + screwNumber.ToString("00");
                    string cardTitle = "H04-" + screwNumber.ToString("00");
                    BuildPreviewCard(cardGrid.transform, objectName, cardTitle, "Bottom cover screw",
                        screwNumber.ToString("00"), theme, rounded, out Toggle toggle, out TMP_Text stateLabel);
                    h04Toggles[i] = toggle;
                    h04ToggleLabels[i] = stateLabel;
                }

                Button hideAll = CreateUiButton("Button_HideAll", previewView, "Hide All", ButtonStyle.Dark, theme, rounded,
                    new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -8f), new Vector2(96f, 30f));
                hideAll.transform.SetSiblingIndex(scrollArea.transform.GetSiblingIndex() + 1);

                // ── Footer (new) ─────────────────────────────────────────────
                Transform existingFooter = FindDeep(root.transform, "Footer");
                if (existingFooter != null)
                    UnityEngine.Object.DestroyImmediate(existingFooter.gameObject);
                GameObject footer = CreateUiPanel("Footer", root.transform,
                    new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, Vector2.zero);
                RectTransform footerRt = footer.GetComponent<RectTransform>();
                footerRt.pivot = new Vector2(0.5f, 0f);
                footerRt.sizeDelta = new Vector2(0f, 26f);
                footerRt.anchoredPosition = Vector2.zero;
                Image footerBg = footer.AddComponent<Image>();
                footerBg.color = new Color(Elevated(theme).r, Elevated(theme).g, Elevated(theme).b, 0.35f);
                footerBg.raycastTarget = false;
                TMP_Text footerLeft = CreateUiTmp("FooterLeft", footer.transform, "NORSCOPE · DEVELOPER MODE", 10f,
                    theme, Dim(theme), new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(240f, 18f));
                TMP_Text footerRight = CreateUiTmp("FooterRight", footer.transform, "v4.2.1 · HoloLens 2", 10f,
                    theme, Dim(theme), new Vector2(1f, 0.5f), new Vector2(-14f, 0f), new Vector2(180f, 18f));
                footerRight.alignment = TextAlignmentOptions.Right;

                // ── Re-wire ArGuidancePanelController ───────────────────────
                ArGuidancePanelController panel = root.GetComponent<ArGuidancePanelController>();
                if (panel == null) throw new Exception("ArGuidancePanelController missing");
                SerializedObject pso = new SerializedObject(panel);
                pso.FindProperty("titleText").objectReferenceValue = title;
                pso.FindProperty("equipmentText").objectReferenceValue = equipment;
                pso.FindProperty("trackingStatusText").objectReferenceValue = tracking;
                pso.FindProperty("previewView").objectReferenceValue = previewView.gameObject;
                pso.FindProperty("previewModeButton").objectReferenceValue = previewTab;
                pso.FindProperty("calibrationModeButton").objectReferenceValue = calibTab;
                pso.FindProperty("previewTabUnderline").objectReferenceValue = previewUnderline;
                pso.FindProperty("calibrationTabUnderline").objectReferenceValue = calibUnderline;
                pso.FindProperty("toggleLaptopOutline").objectReferenceValue = toggles[0];
                pso.FindProperty("toggleLiftEdge").objectReferenceValue = toggles[1];
                pso.FindProperty("toggleBatteryConnector").objectReferenceValue = toggles[2];
                pso.FindProperty("toggleCoolingFan").objectReferenceValue = toggles[3];
                pso.FindProperty("toggleFanAirflow").objectReferenceValue = toggles[4];
                pso.FindProperty("toggleLaptopOutlineLabel").objectReferenceValue = toggleLabels[0];
                pso.FindProperty("toggleLiftEdgeLabel").objectReferenceValue = toggleLabels[1];
                pso.FindProperty("toggleBatteryConnectorLabel").objectReferenceValue = toggleLabels[2];
                pso.FindProperty("toggleCoolingFanLabel").objectReferenceValue = toggleLabels[3];
                pso.FindProperty("toggleFanAirflowLabel").objectReferenceValue = toggleLabels[4];
                pso.FindProperty("hideAllButton").objectReferenceValue = hideAll;

                SerializedProperty h04TogglesProp = pso.FindProperty("h04ScrewToggles");
                SerializedProperty h04LabelsProp = pso.FindProperty("h04ScrewToggleLabels");
                h04TogglesProp.arraySize = h04Toggles.Length;
                h04LabelsProp.arraySize = h04ToggleLabels.Length;
                for (int i = 0; i < h04Toggles.Length; i++)
                {
                    h04TogglesProp.GetArrayElementAtIndex(i).objectReferenceValue = h04Toggles[i];
                    h04LabelsProp.GetArrayElementAtIndex(i).objectReferenceValue = h04ToggleLabels[i];
                }
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
            log.AppendLine("=== AR Guidance Developer Theme Apply ===");
            log.AppendLine(DateTime.Now.ToString("o"));
            log.AppendLine("Panel: " + PanelPrefab);
            log.AppendLine("Chrome restyled from developer.zip reference (Preview/Calibration Developer panel).");
            log.AppendLine("PreviewGuidanceRow left untouched (owned by Battery Task Integration configurator).");
            log.AppendLine("CalibrationView left untouched (owned by Calibration UX configurator).");
            File.WriteAllText(Path.GetFullPath(ApplyReportPath), log.ToString());
            Debug.Log("[Norscope Developer Theme] Apply COMPLETED\n" + log);
            Notify("Developer Theme Apply", "COMPLETED");
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

            sb.AppendLine("=== AR Guidance Developer Theme Validate ===");
            sb.AppendLine(DateTime.Now.ToString("o"));

            Check(File.Exists(ThemePath), "NorscopeTheme asset present");
            Check(File.Exists(RoundedSpritePath), "Rounded sprite present");

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPrefab);
            Check(prefab != null, "ArGuidancePanel.prefab");
            if (prefab != null)
            {
                Check(FindDeep(prefab.transform, "TitleBar") != null, "TitleBar");
                Check(FindDeep(prefab.transform, "BackButton") != null, "BackButton");
                Check(FindDeep(prefab.transform, "CloseButton") != null, "CloseButton");
                Check(FindDeep(prefab.transform, "GrabRegion") != null, "GrabRegion");
                Check(FindDeep(prefab.transform, "ModeTabs") != null, "ModeTabs");
                Check(FindDeep(prefab.transform, "PreviewView") != null, "PreviewView");
                Check(FindDeep(prefab.transform, "CardGrid") != null, "CardGrid");
                Check(FindDeep(prefab.transform, "PreviewGuidanceRow") != null,
                    "PreviewGuidanceRow preserved (owned by Battery Task Integration)");
                Check(FindDeep(prefab.transform, "CalibrationView") != null, "CalibrationView (untouched)");
                Check(FindDeep(prefab.transform, "Footer") != null, "Footer");
                Check(prefab.GetComponentsInChildren<Toggle>(true).Length >=
                      PreviewCards.Length + ArCalibrationIds.H04ScrewCount,
                    "14+ preview toggles (5 fixed + 9 independent H04 screws)");
                Check(prefab.GetComponent<ArGuidancePanelController>() != null, "ArGuidancePanelController");
                Check(prefab.GetComponent<SpatialWindowBindings>() != null, "SpatialWindowBindings");
                Check(prefab.GetComponent<PanelNavigationBinding>() != null, "PanelNavigationBinding");
                RectTransform rt = prefab.GetComponent<RectTransform>();
                Check(rt != null && Mathf.Approximately(rt.sizeDelta.x, 720f) && Mathf.Approximately(rt.sizeDelta.y, 560f),
                    "Panel size 720x560");
            }

            string ctrlSrc = File.ReadAllText("Assets/Norscope/Runtime/ArGuidance/ArGuidancePanelController.cs");
            Check(ctrlSrc.Contains("\"Developer Mode\""), "Title text = Developer Mode");
            Check(ctrlSrc.Contains("previewTabUnderline"), "Tab underline visual hook present");

            sb.AppendLine();
            sb.AppendLine("Manual Play Mode checks still required: header/tabs/footer visuals, Preview card");
            sb.AppendLine("grid toggles show/hide holograms, tab switch shows correct underline.");
            sb.AppendLine(pass ? "STATUS: PASS" : "STATUS: FAIL");
            report = sb.ToString();
            return pass;
        }

        // ── Card builder ─────────────────────────────────────────────────────
        private static void BuildPreviewCard(Transform parent, string name, string title, string subtitle,
            string monogram, NorscopeTheme theme, Sprite rounded, out Toggle toggle, out TMP_Text stateLabel)
        {
            GameObject card = new GameObject(name, typeof(RectTransform));
            card.transform.SetParent(parent, false);
            Image cardBg = card.AddComponent<Image>();
            cardBg.sprite = rounded;
            cardBg.type = Image.Type.Sliced;
            cardBg.color = Card(theme);

            GameObject icon = new GameObject("IconChip", typeof(RectTransform));
            icon.transform.SetParent(card.transform, false);
            RectTransform iconRt = icon.GetComponent<RectTransform>();
            iconRt.anchorMin = iconRt.anchorMax = new Vector2(0f, 1f);
            iconRt.pivot = new Vector2(0f, 1f);
            iconRt.anchoredPosition = new Vector2(12f, -12f);
            iconRt.sizeDelta = new Vector2(34f, 34f);
            Image iconBg = icon.AddComponent<Image>();
            iconBg.sprite = rounded;
            iconBg.type = Image.Type.Sliced;
            Color accent = Accent(theme);
            iconBg.color = new Color(accent.r, accent.g, accent.b, 0.14f);
            TMP_Text iconLabel = CreateUiTmpFill("Label", icon.transform, monogram, 12f, theme, accent,
                Vector2.zero, Vector2.zero);
            iconLabel.alignment = TextAlignmentOptions.Center;

            TMP_Text titleText = CreateUiTmp("Title", card.transform, title, 13f, theme, Primary(theme),
                new Vector2(0f, 1f), new Vector2(54f, -14f), new Vector2(257f, 18f));
            TMP_Text subtitleText = CreateUiTmp("Subtitle", card.transform, subtitle, 10.5f, theme, Dim(theme),
                new Vector2(0f, 1f), new Vector2(54f, -32f), new Vector2(257f, 16f));
            subtitleText.enableWordWrapping = false;
            subtitleText.overflowMode = TextOverflowModes.Ellipsis;
            titleText.enableWordWrapping = false;
            titleText.overflowMode = TextOverflowModes.Ellipsis;

            // Bottom "Show" / "Visible" toggle — developer.zip PreviewCard height 30.
            GameObject toggleGo = new GameObject("VisibilityToggle", typeof(RectTransform));
            toggleGo.transform.SetParent(card.transform, false);
            RectTransform toggleRt = toggleGo.GetComponent<RectTransform>();
            toggleRt.anchorMin = new Vector2(0f, 0f);
            toggleRt.anchorMax = new Vector2(1f, 0f);
            toggleRt.pivot = new Vector2(0.5f, 0f);
            toggleRt.anchoredPosition = new Vector2(0f, 10f);
            toggleRt.sizeDelta = new Vector2(-20f, 30f);
            Image toggleBg = toggleGo.AddComponent<Image>();
            toggleBg.sprite = rounded;
            toggleBg.type = Image.Type.Sliced;
            toggleBg.color = new Color(1f, 1f, 1f, 0.04f);
            toggle = toggleGo.AddComponent<Toggle>();
            toggle.targetGraphic = toggleBg;
            toggle.toggleTransition = Toggle.ToggleTransition.None;
            stateLabel = CreateUiTmpFill("Label", toggleGo.transform, "Show", 11f, theme, Secondary(theme),
                Vector2.zero, Vector2.zero);
            stateLabel.alignment = TextAlignmentOptions.Center;
            stateLabel.raycastTarget = false;
        }

        // ── Themed tab builder ───────────────────────────────────────────────
        private static Button CreateUiTab(string name, Transform parent, string label, NorscopeTheme theme,
            Vector2 anchoredPos, Vector2 size, out GameObject underline)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            Image hit = go.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0.001f);
            Button btn = go.AddComponent<Button>();
            btn.targetGraphic = hit;
            btn.transition = Selectable.Transition.None;

            TMP_Text tmp = CreateUiTmpFill("Label", go.transform, label, 13f, theme, Primary(theme),
                new Vector2(0f, 4f), new Vector2(0f, 4f));
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;

            underline = new GameObject("Underline", typeof(RectTransform));
            underline.transform.SetParent(go.transform, false);
            RectTransform underlineRt = underline.GetComponent<RectTransform>();
            underlineRt.anchorMin = new Vector2(0f, 0f);
            underlineRt.anchorMax = new Vector2(1f, 0f);
            underlineRt.pivot = new Vector2(0.5f, 0f);
            underlineRt.sizeDelta = new Vector2(0f, 2f);
            underlineRt.anchoredPosition = Vector2.zero;
            Image underlineImg = underline.AddComponent<Image>();
            underlineImg.color = Accent(theme);
            underlineImg.raycastTarget = false;
            underline.SetActive(false);

            return btn;
        }

        // ── Theme token helpers ─────────────────────────────────────────────
        private enum ButtonStyle { Ghost, Dark }

        private static readonly Color FallbackPanelBg = new Color(0.07f, 0.09f, 0.12f, 0.94f);
        private static readonly Color FallbackCyan = new Color(0f, 0.85f, 1f, 0.95f);

        private static Color PanelBg(NorscopeTheme t) => t != null ? t.panelBackground : FallbackPanelBg;
        private static Color Card(NorscopeTheme t) => t != null ? t.elevatedPanelBackground : FallbackPanelBg;
        private static Color Elevated(NorscopeTheme t) => t != null ? t.elevatedPanelBackground : FallbackPanelBg;
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

        private static GameObject CreateUiPanel(string name, Transform parent,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
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

        /// <summary>Point-anchor text: anchor==pivot, anchoredPos is the offset of that pivot
        /// corner from the same corner of the parent, sizeDelta is the box's width/height
        /// growing away from the pivot — the same proven convention CreateUiButton already
        /// uses. Do not pass a stretched (anchorMin != anchorMax) anchor here; use
        /// CreateUiTmpFill for that.</summary>
        private static TMP_Text CreateUiTmp(string name, Transform parent, string text, float size,
            NorscopeTheme theme, Color color, Vector2 anchor, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            if (BodyFont(theme) != null)
                tmp.font = BodyFont(theme);
            tmp.raycastTarget = false;
            return tmp;
        }

        /// <summary>Full-stretch text that fills its parent, inset by padMin (from the
        /// bottom-left) and padMax (from the top-right) — both expressed as positive-inward
        /// magnitudes, matching the CalibrationUx Stretch() convention.</summary>
        private static TMP_Text CreateUiTmpFill(string name, Transform parent, string text, float size,
            NorscopeTheme theme, Color color, Vector2 padMin, Vector2 padMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = padMin;
            rt.offsetMax = -padMax;
            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            if (BodyFont(theme) != null)
                tmp.font = BodyFont(theme);
            tmp.raycastTarget = false;
            return tmp;
        }

        private static Button CreateUiButton(string name, Transform parent, string label, ButtonStyle style,
            NorscopeTheme theme, Sprite rounded,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = anchorMin;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;

            Image img = go.AddComponent<Image>();
            img.sprite = rounded;
            img.type = Image.Type.Sliced;
            img.color = style == ButtonStyle.Dark ? Elevated(theme) : new Color(1f, 1f, 1f, 0.03f);
            Button btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            TMP_Text tmp = CreateUiTmpFill("Label", go.transform, label, 13f, theme,
                style == ButtonStyle.Dark ? Primary(theme) : Secondary(theme),
                new Vector2(4f, 2f), new Vector2(4f, 2f));
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
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
    }
}
