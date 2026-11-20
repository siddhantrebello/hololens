using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Norscope.AskAI;
using Norscope.Navigation;
using Norscope.UI;
using Norscope.Windows;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Norscope.Editor
{
    /// <summary>
    /// Ask AI Phase 1: mock UI + workflow only. Capture → Dry Run → Apply → Validate.
    /// </summary>
    public static class PhaseAskAiPanelConfigurator
    {
        private const string Menu = "Norscope/HoloLens/Ask AI/";
        private const string PrefabPath =
            "Assets/Norscope/UI/Panels/AskAIPanel.prefab";
        private const string BootstrapPath =
            "Assets/Norscope/Scenes/Bootstrap.unity";
        private const string BackupRoot =
            "Assets/Norscope/UI/Panels/_Backups";
        private const string ThemePath =
            "Assets/Norscope/UI/DesignSystem/Themes/NorscopeTheme.asset";
        private const string WorldConfigPath =
            "Assets/Norscope/UI/DesignSystem/Themes/NorscopeWorldSpaceUiConfig.asset";
        private const string RoundedPath =
            "Assets/Norscope/UI/DesignSystem/Sprites/UIRounded_8.png";
        private const string OutfitPath =
            "Assets/Norscope/UI/Fonts/Outfit SDF.asset";
        private const string InterPath =
            "Assets/Norscope/UI/Fonts/Inter SDF.asset";
        private const string MicPath =
            "Assets/Norscope/UI/Icons/Lucide/Raster/mic.png";
        private const string CameraIconPath =
            "Assets/Norscope/UI/Icons/Lucide/Raster/monitor.png";
        private const string LogoPath =
            "Assets/WhatsApp Image 2026-07-19 at 13.42.15-transparent.png";
        private const string CloseButtonPath =
            "Assets/Norscope/UI/Controls/NorscopeCloseButton.prefab";
        private const string BackButtonPath =
            "Assets/Norscope/UI/Controls/NavigationBackButton.prefab";
        private const string BackupKey = "Norscope.AskAI.Phase1.Backup";
        private const string ApplyFlag = "Temp/NorscopeAskAiApply.flag";
        private const string ValidateFlag = "Temp/NorscopeAskAiValidate.flag";
        private const string CaptureFlag = "Temp/NorscopeAskAiCapture.flag";

        // Figma Make Ask AI: width 480; height 520 fits Ready layout on HL2.
        private const float Width = 480f;
        private const float Height = 520f;
        private const float TopBarHeight = 52f;
        private const float FooterHeight = 36f;
        private const float GrabWidth = 400f;
        private const float GrabHeight = 30f;
        private const float PadX = 24f;

        private static readonly Color Card = Hex("141B22");
        private static readonly Color Surface = Hex("0F1419");
        private static readonly Color Border = Hex("1E2D3A");
        private static readonly Color Accent = Hex("1EC8F5");
        private static readonly Color AccentDim = new Color(30f / 255f, 200f / 255f, 245f / 255f, 0.07f);
        private static readonly Color AccentBd = new Color(30f / 255f, 200f / 255f, 245f / 255f, 0.22f);
        private static readonly Color Primary = Hex("EDF1F7");
        private static readonly Color Secondary = Hex("7A909F");
        private static readonly Color Dim = Hex("364959");
        private static readonly Color ButtonText = Hex("060C10");

        [InitializeOnLoadMethod]
        private static void PollFlags()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
        }

        private static void OnEditorUpdate()
        {
            if (File.Exists(CaptureFlag))
            {
                File.Delete(CaptureFlag);
                EditorApplication.delayCall += CaptureMenu;
                return;
            }

            if (File.Exists(ValidateFlag))
            {
                File.Delete(ValidateFlag);
                EditorApplication.delayCall += ValidateMenu;
                return;
            }

            if (File.Exists(ApplyFlag))
            {
                File.Delete(ApplyFlag);
                EditorApplication.delayCall += ApplyQuiet;
            }
        }

        [MenuItem(Menu + "1) Capture Baseline")]
        public static void CaptureMenu()
        {
            string folder = BackupRoot + "/AskAI_" +
                            DateTime.Now.ToString("yyyyMMdd_HHmmss");
            Directory.CreateDirectory(folder);
            FileUtil.CopyFileOrDirectory(BootstrapPath, folder + "/Bootstrap.unity");
            if (File.Exists(PrefabPath))
                FileUtil.CopyFileOrDirectory(PrefabPath, folder + "/AskAIPanel.prefab");
            File.WriteAllText(
                folder + "/manifest.txt",
                "Ask AI Phase 1 baseline " + DateTime.Now.ToString("O") + "\n" +
                "Bootstrap=" + BootstrapPath + "\n" +
                "AskAIPanel=" + (File.Exists(PrefabPath) ? PrefabPath : "MISSING") + "\n",
                Encoding.UTF8);
            EditorPrefs.SetString(BackupKey, folder);
            AssetDatabase.Refresh();
            Debug.Log("[Norscope Ask AI] Baseline READY\n" + folder);
        }

        [MenuItem(Menu + "2) Dry Run")]
        public static void DryRunMenu()
        {
            bool ready = DryRun(out string report);
            Directory.CreateDirectory("Temp");
            File.WriteAllText("Temp/NorscopeAskAiDryRun.txt", report, Encoding.UTF8);
            Debug.Log((ready
                ? "[Norscope Ask AI] Dry run READY\n"
                : "[Norscope Ask AI] Dry run BLOCKED\n") + report);
        }

        [MenuItem(Menu + "3) Apply")]
        public static void ApplyMenu()
        {
            if (!DryRun(out string report))
            {
                Debug.LogError("[Norscope Ask AI] Apply BLOCKED\n" + report);
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "Apply Ask AI Phase 1",
                    "Create AskAIPanel.prefab (mock workflow) and assign it on Bootstrap PanelRegistry AskAI entry?\n\n" +
                    "No speech / camera / LLM / TTS will be added.",
                    "Apply",
                    "Cancel"))
                return;

            ApplyQuiet();
        }

        private static void ApplyQuiet()
        {
            EnsureBaseline();
            if (!DryRun(out string report) &&
                report.Contains("BLOCKER"))
            {
                // Allow Apply when only soft warnings; hard stop if required assets missing.
                if (report.Contains("Rounded UI sprite") ||
                    report.Contains("Outfit/Inter") ||
                    report.Contains("Bootstrap.unity missing"))
                {
                    Debug.LogError("[Norscope Ask AI] Apply BLOCKED\n" + report);
                    return;
                }
            }

            BuildPrefab();
            WireBootstrapRegistry();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateMenu();
            Debug.Log("[Norscope Ask AI] Apply COMPLETE");
        }

        /// <summary>Flag-file / automation entry (no confirmation dialog).</summary>
        public static void ApplyFromAutomation()
        {
            ApplyQuiet();
        }

        public static void ValidateFromAutomation()
        {
            ValidateMenu();
        }

        [MenuItem(Menu + "4) Validate")]
        public static void ValidateMenu()
        {
            bool ok = Validate(out string report);
            Directory.CreateDirectory("Temp");
            File.WriteAllText("Temp/NorscopeAskAiValidate.txt", report, Encoding.UTF8);
            if (ok)
                Debug.Log("[Norscope Ask AI] Validation PASSED\n" + report);
            else
                Debug.LogError("[Norscope Ask AI] Validation FAILED\n" + report);
        }

        [MenuItem(Menu + "5) Restore")]
        public static void RestoreMenu()
        {
            string folder = LatestBackup() ?? EditorPrefs.GetString(BackupKey, string.Empty);
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                Debug.LogError("[Norscope Ask AI] No captured backup.");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "Restore Ask AI Phase 1",
                    "Restore Bootstrap (and AskAIPanel if backed up) from:\n" + folder,
                    "Restore",
                    "Cancel"))
                return;

            string bootstrapBackup = folder + "/Bootstrap.unity";
            if (File.Exists(bootstrapBackup))
                FileUtil.ReplaceFile(bootstrapBackup, BootstrapPath);
            string panelBackup = folder + "/AskAIPanel.prefab";
            if (File.Exists(panelBackup))
                FileUtil.ReplaceFile(panelBackup, PrefabPath);
            else if (File.Exists(PrefabPath))
                AssetDatabase.DeleteAsset(PrefabPath);

            AssetDatabase.Refresh();
            Debug.Log("[Norscope Ask AI] Restore COMPLETE\n" + folder);
        }

        private static void EnsureBaseline()
        {
            if (LatestBackup() != null) return;
            CaptureMenu();
        }

        private static string LatestBackup()
        {
            if (!Directory.Exists(BackupRoot)) return null;
            string[] dirs = Directory.GetDirectories(BackupRoot, "AskAI_*");
            Array.Sort(dirs);
            return dirs.Length > 0 ? dirs[dirs.Length - 1] : null;
        }

        private static bool DryRun(out string report)
        {
            var lines = new StringBuilder();
            var blockers = new List<string>();
            lines.AppendLine("NORSCOPE ASK AI PHASE 1 — DRY RUN");
            lines.AppendLine("Generated: " + DateTime.Now.ToString("O"));
            lines.AppendLine("Scope: mock UI + workflow only.");
            lines.AppendLine(string.Empty);

            if (!File.Exists(BootstrapPath))
                blockers.Add("Bootstrap.unity missing.");
            if (AssetDatabase.LoadAssetAtPath<Sprite>(RoundedPath) == null)
                blockers.Add("Rounded UI sprite missing.");
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OutfitPath) == null ||
                AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(InterPath) == null)
                blockers.Add("Outfit/Inter TMP fonts missing.");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(CloseButtonPath) == null)
                blockers.Add("NorscopeCloseButton.prefab missing.");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(BackButtonPath) == null)
                blockers.Add("NavigationBackButton.prefab missing.");

            lines.AppendLine(File.Exists(PrefabPath)
                ? "INFO  AskAIPanel.prefab already exists (Apply will rebuild)."
                : "INFO  AskAIPanel.prefab will be created.");
            lines.AppendLine("PLAN  Runtime: AskAiWorkflowState/View/Controller + IAskAiService/MockAskAiService");
            lines.AppendLine("PLAN  Prefab chrome: TitleBar/Grab/Close/Resize + Back (fallback Home)");
            lines.AppendLine("PLAN  Bootstrap: assign prefab only on PanelType.AskAI entry");
            lines.AppendLine("PLAN  No speech/camera/LLM/TTS");
            lines.AppendLine(string.Empty);
            foreach (string blocker in blockers)
                lines.AppendLine("BLOCKER " + blocker);
            lines.AppendLine(blockers.Count == 0
                ? "STATUS: READY"
                : "STATUS: BLOCKED");
            report = lines.ToString();
            return blockers.Count == 0;
        }

        private static void BuildPrefab()
        {
            NorscopeTheme theme =
                AssetDatabase.LoadAssetAtPath<NorscopeTheme>(ThemePath);
            NorscopeWorldSpaceUiConfig world =
                AssetDatabase.LoadAssetAtPath<NorscopeWorldSpaceUiConfig>(WorldConfigPath);
            Sprite rounded = AssetDatabase.LoadAssetAtPath<Sprite>(RoundedPath);
            TMP_FontAsset outfit =
                AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OutfitPath);
            TMP_FontAsset inter =
                AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(InterPath);
            Sprite mic = AssetDatabase.LoadAssetAtPath<Sprite>(MicPath);
            Sprite cameraIcon = AssetDatabase.LoadAssetAtPath<Sprite>(CameraIconPath);
            Sprite logo = AssetDatabase.LoadAssetAtPath<Sprite>(LogoPath);
            if (rounded == null || outfit == null || inter == null)
                throw new InvalidOperationException("Required Ask AI visual assets missing.");

            if (File.Exists(PrefabPath))
                AssetDatabase.DeleteAsset(PrefabPath);

            GameObject root = new GameObject(
                "AskAIPanel",
                typeof(RectTransform),
                typeof(NorscopePanelRoot),
                typeof(SpatialWindowBindings),
                typeof(PanelNavigationBinding),
                typeof(AskAiView),
                typeof(AskAiWorkflowController));
            try
            {
                root.layer = 5;
                RectTransform rootRt = root.GetComponent<RectTransform>();
                rootRt.sizeDelta = new Vector2(Width, Height);

                Image background = ImageNode(root.transform, "Background", rounded, Card);
                Stretch(background.rectTransform);
                background.type = Image.Type.Sliced;
                background.raycastTarget = false;

                Image border = ImageNode(root.transform, "Border", rounded, Border);
                Stretch(border.rectTransform, -1f);
                border.type = Image.Type.Sliced;
                border.raycastTarget = false;

                // --- Top bar (Figma header + HL2 back) ---
                RectTransform titleBar = RectNode(root.transform, "TitleBar");
                SetTopLeft(titleBar, Vector2.zero, new Vector2(Width, TopBarHeight));
                Image titleBg = titleBar.gameObject.AddComponent<Image>();
                titleBg.color = Color.clear;
                titleBg.raycastTarget = false;

                // Grab sits on the TOP edge of the panel (above the title bar chrome).
                RectTransform grab = RectNode(titleBar, "GrabRegion");
                grab.anchorMin = grab.anchorMax = new Vector2(0.5f, 1f);
                grab.pivot = new Vector2(0.5f, 0f);
                grab.anchoredPosition = Vector2.zero;
                grab.sizeDelta = new Vector2(GrabWidth, GrabHeight);
                Image grabImage = grab.gameObject.AddComponent<Image>();
                grabImage.sprite = rounded;
                grabImage.type = Image.Type.Sliced;
                grabImage.color = new Color(15f / 255f, 20f / 255f, 25f / 255f, 0.92f);
                grabImage.raycastTarget = true;

                // HL2-only: small back (not in Figma).
                GameObject backPrefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(BackButtonPath);
                GameObject back = (GameObject)PrefabUtility.InstantiatePrefab(
                    backPrefab, titleBar);
                back.name = "BackButton";
                RectTransform backRt = back.GetComponent<RectTransform>();
                backRt.anchorMin = backRt.anchorMax = new Vector2(0f, 1f);
                backRt.pivot = new Vector2(0f, 1f);
                backRt.anchoredPosition = new Vector2(12f, -12f);
                backRt.sizeDelta = new Vector2(28f, 28f);
                Button backButton = back.GetComponentInChildren<Button>(true);

                if (logo != null)
                {
                    Image logoImage = ImageNode(
                        titleBar, "Logo", logo, new Color(1f, 1f, 1f, 0.7f),
                        new Vector2(46f, 18f), new Vector2(72f, 15f));
                    logoImage.type = Image.Type.Simple;
                    logoImage.preserveAspect = true;
                }

                Image sep2 = ImageNode(
                    titleBar, "SeparatorRight", null, Border,
                    new Vector2(124f, 20f), new Vector2(1f, 12f));
                sep2.raycastTarget = false;

                TextNode(
                    titleBar, "SectionLabel", "ASK AI", inter, 9.5f, Dim,
                    new Vector2(134f, 18f), new Vector2(72f, 16f),
                    TextAlignmentOptions.MidlineLeft, FontStyles.Bold);

                // Status pill ONLY when not Ready (hidden by default).
                Image pillBg = ImageNode(
                    titleBar, "StatusPill", rounded,
                    new Color(Dim.r, Dim.g, Dim.b, 0.12f));
                pillBg.type = Image.Type.Sliced;
                RectTransform pillRt = pillBg.rectTransform;
                pillRt.anchorMin = pillRt.anchorMax = new Vector2(1f, 1f);
                pillRt.pivot = new Vector2(1f, 1f);
                pillRt.anchoredPosition = new Vector2(-46f, -13f);
                pillRt.sizeDelta = new Vector2(96f, 22f);
                TMP_Text pillText = TextNode(
                    pillBg.transform, "Label", "Ready", inter, 9.5f, Dim,
                    Vector2.zero, new Vector2(96f, 22f),
                    TextAlignmentOptions.Center, FontStyles.Normal);
                Stretch(pillText.rectTransform);
                pillBg.gameObject.SetActive(false);

                GameObject closePrefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(CloseButtonPath);
                GameObject close = (GameObject)PrefabUtility.InstantiatePrefab(
                    closePrefab, titleBar);
                close.name = "CloseButton";
                PrefabUtility.UnpackPrefabInstance(
                    close, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                RectTransform closeRt = close.GetComponent<RectTransform>();
                closeRt.anchorMin = closeRt.anchorMax = new Vector2(1f, 1f);
                closeRt.pivot = new Vector2(1f, 1f);
                closeRt.anchoredPosition = new Vector2(-16f, -13f);
                // NorscopeButtonControl.OnValidate reapplies serialized size — set that first.
                NorscopeButtonControl closeControl =
                    close.GetComponent<NorscopeButtonControl>();
                if (closeControl != null)
                {
                    SerializedObject closeSo = new SerializedObject(closeControl);
                    closeSo.FindProperty("size").vector2Value = new Vector2(26f, 26f);
                    closeSo.ApplyModifiedPropertiesWithoutUndo();
                }

                closeRt.sizeDelta = new Vector2(26f, 26f);
                LayoutElement closeLe = close.GetComponent<LayoutElement>();
                if (closeLe != null)
                {
                    closeLe.minWidth = 26f;
                    closeLe.minHeight = 26f;
                    closeLe.preferredWidth = 26f;
                    closeLe.preferredHeight = 26f;
                }

                CenterCloseButtonIcon(close);

                RectTransform resizeHandles = RectNode(root.transform, "ResizeHandles");
                Stretch(resizeHandles);
                RectTransform corner = RectNode(resizeHandles, "BottomRight");
                corner.anchorMin = corner.anchorMax = Vector2.one;
                corner.pivot = Vector2.one;
                corner.anchoredPosition = new Vector2(-8f, 8f);
                corner.sizeDelta = new Vector2(28f, 28f);
                Image cornerImage = corner.gameObject.AddComponent<Image>();
                cornerImage.sprite = rounded;
                cornerImage.type = Image.Type.Sliced;
                cornerImage.color = new Color(Accent.r, Accent.g, Accent.b, 0.28f);
                cornerImage.raycastTarget = false;

                // --- Content (Figma: 24px horizontal padding) ---
                float contentHeight = Height - TopBarHeight - FooterHeight;
                float contentW = Width - PadX * 2f; // 432
                RectTransform content = RectNode(root.transform, "Content");
                SetTopLeft(
                    content,
                    new Vector2(0f, TopBarHeight),
                    new Vector2(Width, contentHeight));
                CanvasGroup contentGroup = content.gameObject.AddComponent<CanvasGroup>();

                TMP_Text title = TextNode(
                    content, "Title", "Ask AI", outfit, 32f, Primary,
                    new Vector2(PadX, 14f), new Vector2(contentW, 40f),
                    TextAlignmentOptions.TopLeft, FontStyles.Normal);
                title.characterSpacing = -1.5f;

                TextNode(
                    content, "Subtitle",
                    "Ask a question about the equipment you're looking at.",
                    inter, 12.5f, Secondary,
                    new Vector2(PadX, 56f), new Vector2(contentW, 22f),
                    TextAlignmentOptions.TopLeft, FontStyles.Normal);

                Image contentDivider = ImageNode(
                    content, "ContentDivider", null, Border,
                    new Vector2(PadX, 90f), new Vector2(contentW, 1f));
                contentDivider.raycastTarget = false;

                // Ready: HORIZONTAL row — 52×52 mic + text column.
                RectTransform statusRow = RectNode(content, "StatusRow");
                SetTopLeft(
                    statusRow,
                    new Vector2(PadX, 110f),
                    new Vector2(contentW, 80f));
                HorizontalLayoutGroup rowLayout =
                    statusRow.gameObject.AddComponent<HorizontalLayoutGroup>();
                rowLayout.childAlignment = TextAnchor.MiddleLeft;
                rowLayout.childControlWidth = false;
                rowLayout.childControlHeight = false;
                rowLayout.childForceExpandWidth = false;
                rowLayout.childForceExpandHeight = false;
                rowLayout.spacing = 20f;
                rowLayout.padding = new RectOffset(0, 0, 0, 0);

                const float iconBox = 52f;
                RectTransform micRoot = RectNode(statusRow, "Microphone");
                LayoutElement micLe = micRoot.gameObject.AddComponent<LayoutElement>();
                micLe.preferredWidth = iconBox;
                micLe.preferredHeight = iconBox;
                micLe.minWidth = iconBox;
                micLe.minHeight = iconBox;
                FitLayoutChild(micRoot, iconBox, iconBox);
                Image micRing = micRoot.gameObject.AddComponent<Image>();
                micRing.sprite = rounded;
                micRing.type = Image.Type.Sliced;
                micRing.color = AccentDim;
                micRing.pixelsPerUnitMultiplier = 0.5f;
                micRing.raycastTarget = false;
                Image micBorder = ImageNode(micRoot, "MicBorder", rounded, AccentBd);
                Stretch(micBorder.rectTransform, -1f);
                micBorder.type = Image.Type.Sliced;
                micBorder.pixelsPerUnitMultiplier = 0.5f;
                micBorder.raycastTarget = false;
                Image micIcon = ImageNode(
                    micRoot, "Icon", mic, Accent, Vector2.zero, new Vector2(24f, 24f));
                micIcon.type = Image.Type.Simple;
                micIcon.preserveAspect = true;
                CenterRect(micIcon.rectTransform, new Vector2(24f, 24f));

                RectTransform cameraRoot = RectNode(statusRow, "CameraFrame");
                LayoutElement camLe = cameraRoot.gameObject.AddComponent<LayoutElement>();
                camLe.preferredWidth = iconBox;
                camLe.preferredHeight = iconBox;
                FitLayoutChild(cameraRoot, iconBox, iconBox);
                Image cameraRing = cameraRoot.gameObject.AddComponent<Image>();
                cameraRing.sprite = rounded;
                cameraRing.type = Image.Type.Sliced;
                cameraRing.color = AccentDim;
                cameraRing.pixelsPerUnitMultiplier = 0.5f;
                cameraRing.raycastTarget = false;
                Image cameraImg = ImageNode(
                    cameraRoot, "Icon",
                    cameraIcon != null ? cameraIcon : mic, Accent,
                    Vector2.zero, new Vector2(24f, 24f));
                cameraImg.type = Image.Type.Simple;
                cameraImg.preserveAspect = true;
                CenterRect(cameraImg.rectTransform, new Vector2(24f, 24f));
                cameraRoot.gameObject.SetActive(false);

                GameObject processing = new GameObject(
                    "ProcessingIndicator",
                    typeof(RectTransform),
                    typeof(Image),
                    typeof(LayoutElement));
                processing.transform.SetParent(statusRow, false);
                processing.layer = 5;
                LayoutElement procLe = processing.GetComponent<LayoutElement>();
                procLe.preferredWidth = iconBox;
                procLe.preferredHeight = iconBox;
                FitLayoutChild(processing.GetComponent<RectTransform>(), iconBox, iconBox);
                Image processingImage = processing.GetComponent<Image>();
                processingImage.sprite = rounded;
                processingImage.type = Image.Type.Sliced;
                processingImage.color = AccentDim;
                processingImage.pixelsPerUnitMultiplier = 0.5f;
                processingImage.raycastTarget = false;
                processing.SetActive(false);

                float textColW = contentW - iconBox - 20f;
                RectTransform textCol = RectNode(statusRow, "StatusTextColumn");
                LayoutElement textColLe = textCol.gameObject.AddComponent<LayoutElement>();
                textColLe.preferredWidth = textColW;
                textColLe.preferredHeight = 72f;
                textColLe.flexibleWidth = 1f;
                FitLayoutChild(textCol, textColW, 72f);
                VerticalLayoutGroup textLayout =
                    textCol.gameObject.AddComponent<VerticalLayoutGroup>();
                textLayout.childAlignment = TextAnchor.MiddleLeft;
                textLayout.childControlWidth = true;
                textLayout.childControlHeight = false;
                textLayout.childForceExpandWidth = true;
                textLayout.childForceExpandHeight = false;
                textLayout.spacing = 4f;

                TMP_Text status = LayoutText(
                    textCol, "StatusText", "Ready to listen", outfit, 16f, Primary,
                    textColW, 22f,
                    TextAlignmentOptions.MidlineLeft, FontStyles.Normal);

                TMP_Text hint = LayoutText(
                    textCol, "HintText",
                    "Press Ask AI and speak naturally.\nNORSCOPE analyzes your voice and the scene.",
                    inter, 12.5f, Secondary,
                    textColW, 40f,
                    TextAlignmentOptions.TopLeft, FontStyles.Normal);

                // Kept for Configure/API compatibility; always hidden at runtime.
                TMP_Text secondaryHint = LayoutText(
                    textCol, "SecondaryHint", string.Empty, inter, 11f, Dim,
                    textColW, 1f,
                    TextAlignmentOptions.TopLeft, FontStyles.Normal);
                secondaryHint.gameObject.SetActive(false);

                // Answer / transcript card (surface fill); hidden until body present.
                RectTransform card = RectNode(content, "InteractionCard");
                SetTopLeft(
                    card,
                    new Vector2(PadX, 200f),
                    new Vector2(contentW, 100f));
                Image cardBg = card.gameObject.AddComponent<Image>();
                cardBg.sprite = rounded;
                cardBg.type = Image.Type.Sliced;
                cardBg.color = Surface;
                cardBg.raycastTarget = false;
                Image cardBorder = ImageNode(card, "CardBorder", rounded, Border);
                Stretch(cardBorder.rectTransform, -1f);
                cardBorder.type = Image.Type.Sliced;
                cardBorder.raycastTarget = false;
                card.gameObject.SetActive(false);

                RectTransform stage = RectNode(card, "StageStack");
                Stretch(stage, 12f);
                VerticalLayoutGroup stageLayout =
                    stage.gameObject.AddComponent<VerticalLayoutGroup>();
                stageLayout.childAlignment = TextAnchor.UpperLeft;
                stageLayout.childControlHeight = false;
                stageLayout.childControlWidth = true;
                stageLayout.childForceExpandHeight = false;
                stageLayout.childForceExpandWidth = true;
                stageLayout.spacing = 8f;

                TMP_Text analysisLabel = LayoutText(
                    stage, "AnalysisLabel", "NORSCOPE ANALYSIS", inter, 10f,
                    Hex("34C97A"),
                    contentW - 24f, 16f,
                    TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
                analysisLabel.characterSpacing = 4f;
                analysisLabel.gameObject.SetActive(false);

                TMP_Text body = LayoutText(
                    stage, "BodyText", string.Empty, inter, 14f, Primary,
                    contentW - 24f, 72f,
                    TextAlignmentOptions.TopLeft, FontStyles.Normal);
                body.GetComponent<LayoutElement>().flexibleHeight = 1f;
                body.gameObject.SetActive(false);

                // Primary: full content width (432), height 48, radius ~12.
                float btnW = contentW;
                const float btnH = 48f;
                Button primary = ButtonNode(
                    content, "PrimaryButton", rounded, Accent,
                    new Vector2(PadX, contentHeight - 20f - btnH),
                    new Vector2(btnW, btnH));
                primary.GetComponent<Image>().pixelsPerUnitMultiplier = 0.5f;
                RectTransform btnRow = RectNode(primary.transform, "LabelRow");
                Stretch(btnRow);
                HorizontalLayoutGroup btnLayout =
                    btnRow.gameObject.AddComponent<HorizontalLayoutGroup>();
                btnLayout.childAlignment = TextAnchor.MiddleCenter;
                btnLayout.childControlWidth = false;
                btnLayout.childControlHeight = false;
                btnLayout.childForceExpandWidth = false;
                btnLayout.childForceExpandHeight = false;
                btnLayout.spacing = 8f;
                Image primaryMic = ImageNode(
                    btnRow, "MicIcon", mic, ButtonText, Vector2.zero, new Vector2(14f, 14f));
                primaryMic.type = Image.Type.Simple;
                primaryMic.preserveAspect = true;
                LayoutElement primaryMicLe =
                    primaryMic.gameObject.AddComponent<LayoutElement>();
                primaryMicLe.preferredWidth = 14f;
                primaryMicLe.preferredHeight = 14f;
                FitLayoutChild(primaryMic.rectTransform, 14f, 14f);
                TMP_Text primaryLabel = TextNode(
                    btnRow, "Label", "Ask AI", outfit, 15f, ButtonText,
                    Vector2.zero, new Vector2(72f, btnH),
                    TextAlignmentOptions.Center, FontStyles.Bold);
                LayoutElement labelLe = primaryLabel.gameObject.AddComponent<LayoutElement>();
                labelLe.preferredWidth = 72f;
                labelLe.preferredHeight = btnH;
                FitLayoutChild(primaryLabel.rectTransform, 72f, btnH);

                // --- Footer: stage dots (5) + version ---
                RectTransform footer = RectNode(root.transform, "Footer");
                footer.anchorMin = new Vector2(0f, 0f);
                footer.anchorMax = new Vector2(1f, 0f);
                footer.pivot = new Vector2(0.5f, 0f);
                footer.anchoredPosition = Vector2.zero;
                footer.sizeDelta = new Vector2(0f, FooterHeight);
                Image footerDivider = ImageNode(footer, "FooterDivider", null, Border);
                RectTransform footerDivRt = footerDivider.rectTransform;
                footerDivRt.anchorMin = new Vector2(0f, 1f);
                footerDivRt.anchorMax = new Vector2(1f, 1f);
                footerDivRt.pivot = new Vector2(0.5f, 1f);
                footerDivRt.anchoredPosition = Vector2.zero;
                footerDivRt.sizeDelta = new Vector2(0f, 1f);

                RectTransform dots = RectNode(footer, "StageDots");
                dots.anchorMin = dots.anchorMax = new Vector2(0f, 0.5f);
                dots.pivot = new Vector2(0f, 0.5f);
                dots.anchoredPosition = new Vector2(PadX, 0f);
                dots.sizeDelta = new Vector2(80f, 12f);
                HorizontalLayoutGroup dotsLayout =
                    dots.gameObject.AddComponent<HorizontalLayoutGroup>();
                dotsLayout.childAlignment = TextAnchor.MiddleLeft;
                dotsLayout.childControlWidth = false;
                dotsLayout.childControlHeight = false;
                dotsLayout.childForceExpandWidth = false;
                dotsLayout.childForceExpandHeight = false;
                dotsLayout.spacing = 4f;
                for (int i = 0; i < 5; i++)
                {
                    bool active = i == 0;
                    Image dot = ImageNode(
                        dots, "Dot" + i, rounded,
                        active ? Accent : Border);
                    dot.type = Image.Type.Sliced;
                    RectTransform dotRt = dot.rectTransform;
                    FitLayoutChild(dotRt, active ? 14f : 3f, 3f);
                    LayoutElement dotLe = dot.gameObject.AddComponent<LayoutElement>();
                    dotLe.preferredWidth = active ? 14f : 3f;
                    dotLe.preferredHeight = 3f;
                }

                TMP_Text footerMeta = TextNode(
                    footer, "FooterMeta", "v4.2.1 · HoloLens 2", inter, 9.5f, Dim,
                    Vector2.zero, new Vector2(160f, 18f),
                    TextAlignmentOptions.MidlineRight, FontStyles.Normal);
                RectTransform footerMetaRt = footerMeta.rectTransform;
                footerMetaRt.anchorMin = footerMetaRt.anchorMax = new Vector2(1f, 0.5f);
                footerMetaRt.pivot = new Vector2(1f, 0.5f);
                footerMetaRt.anchoredPosition = new Vector2(-PadX, 0f);
                footerMetaRt.sizeDelta = new Vector2(160f, 18f);

                // Do not bind title/background/border to NorscopePanelRoot — theme.Apply()
                // would crush the redesign title to headingSize (18) and recolor surfaces.
                NorscopePanelRoot panelRoot = root.GetComponent<NorscopePanelRoot>();
                SerializedObject panelSo = new SerializedObject(panelRoot);
                Set(panelSo, "theme", theme);
                Set(panelSo, "worldSpaceConfig", world);
                Set(panelSo, "root", rootRt);
                Set(panelSo, "background", null);
                Set(panelSo, "border", null);
                Set(panelSo, "title", null);
                panelSo.FindProperty("sizePixels").vector2Value =
                    new Vector2(Width, Height);
                panelSo.FindProperty("useMetersAsSource").boolValue = false;
                panelSo.ApplyModifiedPropertiesWithoutUndo();

                SpatialWindowBindings bindings =
                    root.GetComponent<SpatialWindowBindings>();
                SerializedObject bindSo = new SerializedObject(bindings);
                Set(bindSo, "grabRegion", grab);
                Set(bindSo, "resizeHandle", corner);
                Set(bindSo, "closeButtonRoot", close.transform);
                bindSo.ApplyModifiedPropertiesWithoutUndo();

                PanelNavigationBinding navigation =
                    root.GetComponent<PanelNavigationBinding>();
                navigation.Configure(
                    PanelType.AskAI,
                    PanelType.Home,
                    backButton);

                AskAiView view = root.GetComponent<AskAiView>();
                view.Configure(
                    status,
                    hint,
                    secondaryHint,
                    body,
                    analysisLabel,
                    pillText,
                    pillBg,
                    primary,
                    primaryLabel,
                    micIcon,
                    micRoot,
                    cameraRoot,
                    contentGroup,
                    processing);

                AskAiWorkflowController controller =
                    root.GetComponent<AskAiWorkflowController>();
                AskAiBackendConfig backendConfig =
                    AssetDatabase.LoadAssetAtPath<AskAiBackendConfig>(
                        "Assets/Norscope/Data/AskAiBackendConfig.asset");
                controller.Configure(view, primary, config: backendConfig);

                // Re-assert close size + centered icon after any OnValidate from wiring/save prep.
                if (closeRt != null)
                {
                    closeRt.sizeDelta = new Vector2(26f, 26f);
                    NorscopeButtonControl closeCtrl =
                        close.GetComponent<NorscopeButtonControl>();
                    if (closeCtrl != null)
                    {
                        SerializedObject closeSo = new SerializedObject(closeCtrl);
                        closeSo.FindProperty("size").vector2Value = new Vector2(26f, 26f);
                        closeSo.ApplyModifiedPropertiesWithoutUndo();
                    }

                    CenterCloseButtonIcon(close);
                }

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void WireBootstrapRegistry()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
                throw new InvalidOperationException("AskAIPanel.prefab missing after Build.");

            Scene scene = EditorSceneManager.OpenScene(BootstrapPath, OpenSceneMode.Single);
            PanelRegistry registry =
                UnityEngine.Object.FindFirstObjectByType<PanelRegistry>();
            if (registry == null)
                throw new InvalidOperationException("PanelRegistry missing in Bootstrap.");

            SerializedObject serializedRegistry = new SerializedObject(registry);
            SerializedProperty entries = serializedRegistry.FindProperty("entries");
            SerializedProperty target = null;
            for (int i = 0; i < entries.arraySize; i++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                if ((PanelType)entry.FindPropertyRelative("panelType").enumValueIndex ==
                    PanelType.AskAI)
                {
                    target = entry;
                    break;
                }
            }

            if (target == null)
                throw new InvalidOperationException(
                    "Bootstrap PanelRegistry has no AskAI entry.");

            SpatialWindowId windowId =
                (SpatialWindowId)target.FindPropertyRelative("windowId").enumValueIndex;
            if (windowId != SpatialWindowId.AskAI)
                throw new InvalidOperationException(
                    "AskAI entry has unexpected SpatialWindowId=" + windowId);

            SerializedProperty prefabProp = target.FindPropertyRelative("prefab");
            prefabProp.objectReferenceValue = prefab;
            serializedRegistry.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(registry);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            registry.RebuildLookup();
        }

        private static bool Validate(out string report)
        {
            var lines = new StringBuilder();
            bool ok = true;
            void Check(bool condition, string label)
            {
                lines.AppendLine((condition ? "PASS  " : "FAIL  ") + label);
                ok &= condition;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Check(prefab != null, "AskAIPanel.prefab exists");
            if (prefab != null)
            {
                Check(prefab.GetComponent<NorscopePanelRoot>() != null,
                    "NorscopePanelRoot present");
                Check(prefab.GetComponent<SpatialWindowBindings>() != null,
                    "SpatialWindowBindings present");
                Check(prefab.GetComponent<PanelNavigationBinding>() != null,
                    "PanelNavigationBinding present");
                Check(prefab.GetComponent<AskAiView>() != null,
                    "AskAiView present");
                Check(prefab.GetComponent<AskAiWorkflowController>() != null,
                    "AskAiWorkflowController present");

                Check(FindDeep(prefab.transform, "TitleBar") != null, "TitleBar authored");
                Transform grabTf = FindDeep(prefab.transform, "GrabRegion");
                Check(grabTf != null, "GrabRegion authored");
                if (grabTf is RectTransform grabRt)
                {
                    Check(Mathf.Approximately(grabRt.sizeDelta.x, GrabWidth) &&
                          Mathf.Approximately(grabRt.sizeDelta.y, GrabHeight),
                        "GrabRegion size is 400x30");
                    Check(Mathf.Approximately(grabRt.anchorMin.x, 0.5f) &&
                          Mathf.Approximately(grabRt.anchorMin.y, 1f) &&
                          Mathf.Approximately(grabRt.pivot.x, 0.5f) &&
                          Mathf.Approximately(grabRt.pivot.y, 0f),
                        "GrabRegion centered on top edge (anchor 0.5,1 pivot 0.5,0)");
                    Image grabImg = grabTf.GetComponent<Image>();
                    Check(grabImg != null &&
                          grabImg.raycastTarget &&
                          grabImg.type == Image.Type.Sliced &&
                          grabImg.sprite != null &&
                          grabImg.color.a > 0.5f,
                        "GrabRegion visible sliced sprite + raycast");
                }

                Check(FindDeep(prefab.transform, "CloseButton") != null, "CloseButton authored");
                Check(FindDeep(prefab.transform, "BottomRight") != null, "BottomRight resize authored");
                Check(FindDeep(prefab.transform, "BackButton") != null, "BackButton authored");
                Check(FindDeep(prefab.transform, "PrimaryButton") != null, "PrimaryButton authored");
                Check(FindDeep(prefab.transform, "StatusText") != null, "StatusText authored");
                Check(FindDeep(prefab.transform, "HintText") != null, "HintText authored");
                Check(FindDeep(prefab.transform, "Microphone") != null, "Microphone authored");

                Transform cardTf = FindDeep(prefab.transform, "InteractionCard");
                Check(cardTf != null, "InteractionCard authored");
                if (cardTf is RectTransform cardRt)
                {
                    Check(Mathf.Approximately(cardRt.sizeDelta.x, Width - PadX * 2f),
                        "InteractionCard is 432 wide (480-48)");
                }

                Check(FindDeep(prefab.transform, "StatusPill") != null, "StatusPill authored");
                Check(FindDeep(prefab.transform, "StageStack") != null, "StageStack authored");
                Check(FindDeep(prefab.transform, "Footer") != null, "Footer authored");
                Check(FindDeep(prefab.transform, "StageDots") != null, "StageDots authored");
                Check(FindDeep(prefab.transform, "ProcessingIndicator") != null,
                    "ProcessingIndicator authored");
                Check(FindDeep(prefab.transform, "SecondaryHint") != null,
                    "SecondaryHint authored");
                Check(FindDeep(prefab.transform, "AnalysisLabel") != null,
                    "AnalysisLabel authored");
                Check(FindDeep(prefab.transform, "LabelRow") != null,
                    "PrimaryButton LabelRow authored");

                Transform primaryTf = FindDeep(prefab.transform, "PrimaryButton");
                if (primaryTf is RectTransform primaryRt)
                {
                    Check(Mathf.Approximately(primaryRt.sizeDelta.x, Width - PadX * 2f) &&
                          Mathf.Approximately(primaryRt.sizeDelta.y, 48f),
                        "PrimaryButton is 432x48");
                }

                Transform micTf = FindDeep(prefab.transform, "Microphone");
                if (micTf is RectTransform micRt)
                {
                    Check(Mathf.Approximately(micRt.sizeDelta.x, 52f) &&
                          Mathf.Approximately(micRt.sizeDelta.y, 52f),
                        "Microphone icon box is 52x52");
                }

                Transform closeTf = FindDeep(prefab.transform, "CloseButton");
                if (closeTf is RectTransform closeRtCheck)
                {
                    Check(Mathf.Approximately(closeRtCheck.sizeDelta.x, 26f) &&
                          Mathf.Approximately(closeRtCheck.sizeDelta.y, 26f),
                        "CloseButton is 26x26");
                }

                NorscopePanelRoot panelRoot = prefab.GetComponent<NorscopePanelRoot>();
                if (panelRoot != null)
                {
                    SerializedObject panelCheck = new SerializedObject(panelRoot);
                    Check(panelCheck.FindProperty("title").objectReferenceValue == null &&
                          panelCheck.FindProperty("background").objectReferenceValue == null &&
                          panelCheck.FindProperty("border").objectReferenceValue == null,
                        "NorscopePanelRoot title/background/border null (theme won't crush redesign)");
                    Check(panelCheck.FindProperty("theme").objectReferenceValue != null &&
                          panelCheck.FindProperty("worldSpaceConfig").objectReferenceValue != null &&
                          panelCheck.FindProperty("root").objectReferenceValue != null &&
                          !panelCheck.FindProperty("useMetersAsSource").boolValue,
                        "NorscopePanelRoot theme/world/root wired, useMetersAsSource false");
                }

                RectTransform rootRt = prefab.GetComponent<RectTransform>();
                Check(rootRt != null &&
                      Mathf.Approximately(rootRt.sizeDelta.x, Width) &&
                      Mathf.Approximately(rootRt.sizeDelta.y, Height),
                    "Panel size is 480x520");

                SpatialWindowBindings bindings =
                    prefab.GetComponent<SpatialWindowBindings>();
                Check(bindings != null &&
                      bindings.GrabRegion != null &&
                      bindings.ResizeHandle != null &&
                      bindings.CloseButton != null,
                    "SpatialWindowBindings refs assigned");

                PanelNavigationBinding nav =
                    prefab.GetComponent<PanelNavigationBinding>();
                Check(nav != null &&
                      nav.PanelType == PanelType.AskAI &&
                      nav.FallbackParent == PanelType.Home &&
                      nav.BackButton != null,
                    "Back uses PanelNavigationService (fallback Home)");

                AskAiView view = prefab.GetComponent<AskAiView>();
                AskAiWorkflowController controller =
                    prefab.GetComponent<AskAiWorkflowController>();
                SerializedObject viewSo = new SerializedObject(view);
                SerializedObject ctrlSo = new SerializedObject(controller);
                Check(viewSo.FindProperty("statusText").objectReferenceValue != null &&
                      viewSo.FindProperty("hintText").objectReferenceValue != null &&
                      viewSo.FindProperty("secondaryHintText").objectReferenceValue != null &&
                      viewSo.FindProperty("bodyText").objectReferenceValue != null &&
                      viewSo.FindProperty("analysisLabel").objectReferenceValue != null &&
                      viewSo.FindProperty("statusPillText").objectReferenceValue != null &&
                      viewSo.FindProperty("statusPillBackground").objectReferenceValue != null &&
                      viewSo.FindProperty("primaryButton").objectReferenceValue != null &&
                      viewSo.FindProperty("primaryButtonLabel").objectReferenceValue != null &&
                      viewSo.FindProperty("microphoneImage").objectReferenceValue != null &&
                      viewSo.FindProperty("microphoneRoot").objectReferenceValue != null &&
                      viewSo.FindProperty("cameraRoot").objectReferenceValue != null &&
                      viewSo.FindProperty("contentGroup").objectReferenceValue != null &&
                      viewSo.FindProperty("processingIndicator").objectReferenceValue != null,
                    "AskAiView cached refs assigned");
                Check(ctrlSo.FindProperty("view").objectReferenceValue != null &&
                      ctrlSo.FindProperty("primaryButton").objectReferenceValue != null &&
                      ctrlSo.FindProperty("backendConfig").objectReferenceValue != null,
                    "AskAiWorkflowController cached refs + backendConfig assigned");

                string controllerSource = File.ReadAllText(
                    "Assets/Norscope/Runtime/AskAI/AskAiWorkflowController.cs");
                string serviceSource = File.ReadAllText(
                    "Assets/Norscope/Runtime/AskAI/MockAskAiService.cs");
                Check(!serviceSource.Contains("StartCoroutine") &&
                      !serviceSource.Contains("UIAudioManager") &&
                      !serviceSource.Contains("MonoBehaviour"),
                    "MockAskAiService has no UI/timing/audio");
                Check(controllerSource.Contains("MaxListeningSeconds = 15f") &&
                      controllerSource.Contains("MinProcessingDisplaySeconds = 0.15f"),
                    "Controller owns listening safety and min processing display");
                Check(controllerSource.Contains("VoiceStartListening") &&
                      controllerSource.Contains("AIThinking") &&
                      controllerSource.Contains("AIResponse"),
                    "Semantic UIAudio events referenced once per transition path");
                Check(controllerSource.Contains("IAskAiSpeechCapture") ||
                      File.Exists("Assets/Norscope/Runtime/AskAI/IAskAiSpeechCapture.cs"),
                    "Speech capture abstraction present (Phase 2)");
                Check(!controllerSource.Contains("using OpenAI") &&
                      !controllerSource.Contains("UnityEngine.Windows.Speech") &&
                      !controllerSource.Contains("UnityWebRequest"),
                    "Controller has no provider SDK / direct Windows.Speech / UnityWebRequest");
            }

            // Bootstrap registry
            Scene previous = SceneManager.GetActiveScene();
            string previousPath = previous.path;
            Scene bootstrap = EditorSceneManager.OpenScene(
                BootstrapPath, OpenSceneMode.Single);
            try
            {
                PanelRegistry registry =
                    UnityEngine.Object.FindFirstObjectByType<PanelRegistry>();
                Check(registry != null, "Bootstrap PanelRegistry found");
                if (registry != null)
                {
                    registry.RebuildLookup();
                    bool hasEntry = registry.TryGetEntry(
                        PanelType.AskAI, out PanelRegistryEntry entry);
                    Check(hasEntry, "AskAI registry entry exists");
                    Check(hasEntry && entry.prefab == prefab,
                        "AskAI registry prefab == AskAIPanel.prefab");
                    Check(hasEntry && entry.windowId == SpatialWindowId.AskAI,
                        "AskAI windowId unchanged");
                    Check(hasEntry && entry.behaviour == PanelBehaviourType.WorldLocked,
                        "AskAI remains WorldLocked");
                    Check(hasEntry && entry.prefab != null,
                        "No null/placeholder AskAI prefab");
                }
            }
            finally
            {
                if (!string.IsNullOrEmpty(previousPath) &&
                    File.Exists(previousPath) &&
                    previousPath != BootstrapPath)
                {
                    EditorSceneManager.OpenScene(previousPath, OpenSceneMode.Single);
                }
            }

            Check(!EditorUtility.scriptCompilationFailed, "Zero C# compile errors");

            lines.AppendLine(string.Empty);
            lines.AppendLine("NAV NOTE: PanelNavigationBinding.fallbackParent=Home.");
            lines.AppendLine("Runtime parent from PanelNavigationService.Navigate wins when present;");
            lines.AppendLine("Home is fallback only when no runtime parent is recorded.");
            lines.AppendLine("SpatialWindow is added at spawn by SpatialWindowSpawner (established pattern).");
            lines.AppendLine("CONFIRM: no real AI/speech/camera/backend/TTS added.");
            lines.AppendLine("CONFIRM: Home/Settings/Task Library/Task Execution/Reports untouched.");

            report = lines.ToString();
            return ok;
        }

        private static void CenterCloseButtonIcon(GameObject close)
        {
            if (close == null)
                return;

            NorscopeButtonControl control = close.GetComponent<NorscopeButtonControl>();
            if (control != null)
            {
                SerializedObject so = new SerializedObject(control);
                so.FindProperty("size").vector2Value = new Vector2(26f, 26f);
                SerializedProperty pad = so.FindProperty("padding");
                if (pad != null)
                {
                    pad.FindPropertyRelative("m_Left").intValue = 0;
                    pad.FindPropertyRelative("m_Right").intValue = 0;
                    pad.FindPropertyRelative("m_Top").intValue = 0;
                    pad.FindPropertyRelative("m_Bottom").intValue = 0;
                }

                SerializedProperty spacing = so.FindProperty("spacing");
                if (spacing != null)
                    spacing.floatValue = 0f;
                so.ApplyModifiedPropertiesWithoutUndo();
                // Apply size with zero padding (do not call ApplyAll before zeroing).
                control.ApplyAll();
            }

            HorizontalLayoutGroup hl = close.GetComponent<HorizontalLayoutGroup>();
            if (hl != null)
            {
                hl.enabled = true;
                hl.padding = new RectOffset(0, 0, 0, 0);
                hl.spacing = 0f;
                hl.childAlignment = TextAnchor.MiddleCenter;
                hl.childControlWidth = false;
                hl.childControlHeight = false;
                hl.childForceExpandWidth = false;
                hl.childForceExpandHeight = false;
            }

            Transform label = close.transform.Find("Label");
            if (label != null)
                label.gameObject.SetActive(false);

            Transform icon = close.transform.Find("Icon");
            if (icon is RectTransform iconRt)
            {
                iconRt.anchorMin = iconRt.anchorMax = new Vector2(0.5f, 0.5f);
                iconRt.pivot = new Vector2(0.5f, 0.5f);
                iconRt.anchoredPosition = Vector2.zero;
                iconRt.sizeDelta = new Vector2(12f, 12f);
                iconRt.localScale = Vector3.one;
                LayoutElement iconLe = icon.GetComponent<LayoutElement>();
                if (iconLe == null)
                    iconLe = icon.gameObject.AddComponent<LayoutElement>();
                iconLe.minWidth = 12f;
                iconLe.minHeight = 12f;
                iconLe.preferredWidth = 12f;
                iconLe.preferredHeight = 12f;
                iconLe.flexibleWidth = 0f;
                iconLe.flexibleHeight = 0f;
                iconLe.ignoreLayout = false;

                Image iconImage = icon.GetComponent<Image>();
                if (iconImage != null)
                {
                    iconImage.preserveAspect = true;
                    iconImage.type = Image.Type.Simple;
                }
            }

            RectTransform closeRt = close.GetComponent<RectTransform>();
            if (closeRt != null)
                closeRt.sizeDelta = new Vector2(26f, 26f);
        }

        private static Image ImageNode(
            Transform parent, string name, Sprite sprite, Color color)
        {
            return ImageNode(parent, name, sprite, color, Vector2.zero, Vector2.zero);
        }

        private static Image ImageNode(
            Transform parent, string name, Sprite sprite, Color color,
            Vector2 topLeft, Vector2 size)
        {
            RectTransform rt = RectNode(parent, name);
            if (size.sqrMagnitude > 0f)
                SetTopLeft(rt, topLeft, size);
            Image image = rt.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Button ButtonNode(
            Transform parent, string name, Sprite sprite, Color color,
            Vector2 topLeft, Vector2 size)
        {
            RectTransform rt = RectNode(parent, name);
            SetTopLeft(rt, topLeft, size);
            Image image = rt.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            image.color = color;
            image.raycastTarget = true;
            Button button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.05f, 1.05f, 1.05f, 1f);
            colors.pressedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
            colors.disabledColor = new Color(0.7f, 0.7f, 0.7f, 0.45f);
            button.colors = colors;
            return button;
        }

        private static TMP_Text LayoutText(
            Transform parent, string name, string value, TMP_FontAsset font,
            float size, Color color, float preferredWidth, float preferredHeight,
            TextAlignmentOptions align, FontStyles style)
        {
            RectTransform rt = RectNode(parent, name);
            FitLayoutChild(rt, preferredWidth, preferredHeight);
            TextMeshProUGUI text = rt.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.font = font;
            text.fontSize = size;
            text.color = color;
            text.alignment = align;
            text.fontStyle = style;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            LayoutElement le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = preferredWidth;
            le.preferredHeight = preferredHeight;
            le.flexibleWidth = 0f;
            le.flexibleHeight = 0f;
            return text;
        }

        private static void FitLayoutChild(RectTransform rt, float width, float height)
        {
            // Centered fixed size — Vertical/HorizontalLayoutGroup drives position.
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(width, height);
        }

        private static void CenterRect(RectTransform rt, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = size;
        }

        private static TMP_Text TextNode(
            Transform parent, string name, string value, TMP_FontAsset font,
            float size, Color color, Vector2 topLeft, Vector2 rectSize,
            TextAlignmentOptions align, FontStyles style)
        {
            RectTransform rt = RectNode(parent, name);
            SetTopLeft(rt, topLeft, rectSize);
            TextMeshProUGUI text = rt.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.font = font;
            text.fontSize = size;
            text.color = color;
            text.alignment = align;
            text.fontStyle = style;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            return text;
        }

        private static RectTransform RectNode(Transform parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = parent != null ? parent.gameObject.layer : 5;
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        private static void SetTopLeft(RectTransform rt, Vector2 topLeft, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(topLeft.x, -topLeft.y);
            rt.sizeDelta = size;
        }

        private static void SetTopCenter(RectTransform rt, float topY, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -topY);
            rt.sizeDelta = size;
        }

        private static void Stretch(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        private static void Set(
            SerializedObject so, string property, UnityEngine.Object value)
        {
            SerializedProperty p = so.FindProperty(property);
            if (p != null)
                p.objectReferenceValue = value;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeep(root.GetChild(i), name);
                if (found != null) return found;
            }

            return null;
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex.TrimStart('#'), out Color color);
            return color;
        }
    }
}
