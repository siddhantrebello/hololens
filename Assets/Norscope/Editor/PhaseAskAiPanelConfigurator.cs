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
            "Assets/Norscope/UI/DesignSystem/NorscopeWorldSpaceUiConfig.asset";
        private const string RoundedPath =
            "Assets/Norscope/UI/DesignSystem/Sprites/UIRounded_8.png";
        private const string OutfitPath =
            "Assets/Norscope/UI/Fonts/Outfit SDF.asset";
        private const string InterPath =
            "Assets/Norscope/UI/Fonts/Inter SDF.asset";
        private const string MicPath =
            "Assets/Norscope/UI/Icons/Lucide/Raster/mic.png";
        private const string CloseButtonPath =
            "Assets/Norscope/UI/Controls/NorscopeCloseButton.prefab";
        private const string BackButtonPath =
            "Assets/Norscope/UI/Controls/NavigationBackButton.prefab";
        private const string BackupKey = "Norscope.AskAI.Phase1.Backup";
        private const string ApplyFlag = "Temp/NorscopeAskAiApply.flag";
        private const string ValidateFlag = "Temp/NorscopeAskAiValidate.flag";
        private const string CaptureFlag = "Temp/NorscopeAskAiCapture.flag";

        private const float Width = 700f;
        private const float Height = 520f;

        private static readonly Color Bg = Hex("0D1117");
        private static readonly Color Elevated = Hex("131A22");
        private static readonly Color Border = Hex("2D3B49");
        private static readonly Color Accent = Hex("1EC8F5");
        private static readonly Color Primary = Hex("F3F7FB");
        private static readonly Color Secondary = Hex("91A2B5");
        private static readonly Color Dim = Hex("4A5D6E");

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

                Image background = ImageNode(root.transform, "Background", rounded, Bg);
                Stretch(background.rectTransform);
                background.type = Image.Type.Sliced;
                background.raycastTarget = false;

                Image border = ImageNode(root.transform, "Border", rounded, Border);
                Stretch(border.rectTransform, -1f);
                border.type = Image.Type.Sliced;
                border.raycastTarget = false;

                // --- Chrome (not animated) ---
                RectTransform titleBar = RectNode(root.transform, "TitleBar");
                SetTopLeft(titleBar, Vector2.zero, new Vector2(Width, 56f));
                Image titleBg = titleBar.gameObject.AddComponent<Image>();
                titleBg.sprite = rounded;
                titleBg.type = Image.Type.Sliced;
                titleBg.color = Elevated;
                titleBg.raycastTarget = false;

                RectTransform grab = RectNode(titleBar, "GrabRegion");
                SetTopLeft(grab, Vector2.zero, new Vector2(Width - 120f, 56f));
                Image grabImage = grab.gameObject.AddComponent<Image>();
                grabImage.color = Color.clear;
                grabImage.raycastTarget = true;

                GameObject backPrefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(BackButtonPath);
                GameObject back = (GameObject)PrefabUtility.InstantiatePrefab(
                    backPrefab, titleBar);
                back.name = "BackButton";
                RectTransform backRt = back.GetComponent<RectTransform>();
                backRt.anchorMin = backRt.anchorMax = new Vector2(0f, 1f);
                backRt.pivot = new Vector2(0f, 1f);
                backRt.anchoredPosition = new Vector2(12f, -8f);
                backRt.sizeDelta = new Vector2(40f, 40f);
                Button backButton = back.GetComponentInChildren<Button>(true);

                GameObject closePrefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(CloseButtonPath);
                GameObject close = (GameObject)PrefabUtility.InstantiatePrefab(
                    closePrefab, titleBar);
                close.name = "CloseButton";
                RectTransform closeRt = close.GetComponent<RectTransform>();
                closeRt.anchorMin = closeRt.anchorMax = new Vector2(1f, 1f);
                closeRt.pivot = new Vector2(1f, 1f);
                closeRt.anchoredPosition = new Vector2(-12f, -8f);
                closeRt.sizeDelta = new Vector2(40f, 40f);

                RectTransform resizeHandles = RectNode(root.transform, "ResizeHandles");
                Stretch(resizeHandles);
                RectTransform corner = RectNode(resizeHandles, "BottomRight");
                corner.anchorMin = corner.anchorMax = Vector2.one;
                corner.pivot = Vector2.one;
                corner.anchoredPosition = new Vector2(-6f, 6f);
                corner.sizeDelta = new Vector2(30f, 30f);
                Image cornerImage = corner.gameObject.AddComponent<Image>();
                cornerImage.sprite = rounded;
                cornerImage.type = Image.Type.Sliced;
                cornerImage.color = new Color(Accent.r, Accent.g, Accent.b, 0.35f);
                cornerImage.raycastTarget = false;

                // --- Animated content ---
                RectTransform content = RectNode(root.transform, "Content");
                SetTopLeft(content, new Vector2(0f, 56f), new Vector2(Width, Height - 56f));
                CanvasGroup contentGroup = content.gameObject.AddComponent<CanvasGroup>();

                TMP_Text title = TextNode(
                    content, "Title", "Ask AI", outfit, 36f, Primary,
                    new Vector2(40f, 24f), new Vector2(520f, 44f),
                    TextAlignmentOptions.TopLeft, FontStyles.Normal);
                TMP_Text subtitle = TextNode(
                    content, "Subtitle",
                    "Ask a question about what you are currently seeing.",
                    inter, 15f, Secondary,
                    new Vector2(40f, 72f), new Vector2(620f, 40f),
                    TextAlignmentOptions.TopLeft, FontStyles.Normal);

                RectTransform micRoot = RectNode(content, "Microphone");
                SetTopLeft(micRoot, new Vector2(290f, 140f), new Vector2(120f, 120f));
                Image micRing = micRoot.gameObject.AddComponent<Image>();
                micRing.sprite = rounded;
                micRing.type = Image.Type.Sliced;
                micRing.color = new Color(Accent.r, Accent.g, Accent.b, 0.12f);
                micRing.raycastTarget = false;
                Image micIcon = ImageNode(
                    micRoot, "Icon", mic, new Color(Accent.r, Accent.g, Accent.b, 0.85f),
                    new Vector2(30f, 30f), new Vector2(60f, 60f));
                micIcon.type = Image.Type.Simple;
                micIcon.preserveAspect = true;

                TMP_Text status = TextNode(
                    content, "StatusText", "Ready", outfit, 20f, Primary,
                    new Vector2(40f, 280f), new Vector2(620f, 32f),
                    TextAlignmentOptions.Center, FontStyles.Normal);

                TMP_Text hint = TextNode(
                    content, "HintText",
                    "Press Ask AI and speak naturally.",
                    inter, 14f, Dim,
                    new Vector2(40f, 318f), new Vector2(620f, 28f),
                    TextAlignmentOptions.Center, FontStyles.Normal);

                TMP_Text body = TextNode(
                    content, "BodyText", string.Empty, inter, 16f, Secondary,
                    new Vector2(60f, 352f), new Vector2(580f, 72f),
                    TextAlignmentOptions.Center, FontStyles.Normal);
                body.gameObject.SetActive(false);

                GameObject processing = new GameObject(
                    "ProcessingIndicator", typeof(RectTransform), typeof(Image));
                processing.transform.SetParent(content, false);
                processing.layer = 5;
                RectTransform processingRt = processing.GetComponent<RectTransform>();
                SetTopLeft(processingRt, new Vector2(335f, 250f), new Vector2(30f, 30f));
                Image processingImage = processing.GetComponent<Image>();
                processingImage.sprite = rounded;
                processingImage.type = Image.Type.Sliced;
                processingImage.color = Accent;
                processingImage.raycastTarget = false;
                processing.SetActive(false);

                Button primary = ButtonNode(
                    content, "PrimaryButton", rounded, Accent,
                    new Vector2(40f, 430f), new Vector2(620f, 56f));
                TMP_Text primaryLabel = TextNode(
                    primary.transform, "Label", "Ask AI", outfit, 18f, Color.white,
                    Vector2.zero, new Vector2(620f, 56f),
                    TextAlignmentOptions.Center, FontStyles.Normal);
                Stretch(primaryLabel.rectTransform);

                // Wire components
                NorscopePanelRoot panelRoot = root.GetComponent<NorscopePanelRoot>();
                SerializedObject panelSo = new SerializedObject(panelRoot);
                Set(panelSo, "theme", theme);
                Set(panelSo, "worldSpaceConfig", world);
                Set(panelSo, "root", rootRt);
                Set(panelSo, "background", background);
                Set(panelSo, "border", border);
                Set(panelSo, "title", title);
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

                // Fallback Home only — runtime parent from Navigate wins when available.
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
                    body,
                    primary,
                    primaryLabel,
                    micIcon,
                    micRoot,
                    contentGroup,
                    processing);

                AskAiWorkflowController controller =
                    root.GetComponent<AskAiWorkflowController>();
                controller.Configure(view, primary, new MockAskAiService());

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
                Check(FindDeep(prefab.transform, "GrabRegion") != null, "GrabRegion authored");
                Check(FindDeep(prefab.transform, "CloseButton") != null, "CloseButton authored");
                Check(FindDeep(prefab.transform, "BottomRight") != null, "BottomRight resize authored");
                Check(FindDeep(prefab.transform, "BackButton") != null, "BackButton authored");
                Check(FindDeep(prefab.transform, "PrimaryButton") != null, "PrimaryButton authored");
                Check(FindDeep(prefab.transform, "StatusText") != null, "StatusText authored");
                Check(FindDeep(prefab.transform, "HintText") != null, "HintText authored");
                Check(FindDeep(prefab.transform, "Microphone") != null, "Microphone authored");
                Check(FindDeep(prefab.transform, "ProcessingIndicator") != null,
                    "ProcessingIndicator authored");

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
                      viewSo.FindProperty("primaryButton").objectReferenceValue != null &&
                      viewSo.FindProperty("microphoneImage").objectReferenceValue != null,
                    "AskAiView cached refs assigned");
                Check(ctrlSo.FindProperty("view").objectReferenceValue != null &&
                      ctrlSo.FindProperty("primaryButton").objectReferenceValue != null,
                    "AskAiWorkflowController cached refs assigned");

                string controllerSource = File.ReadAllText(
                    "Assets/Norscope/Runtime/AskAI/AskAiWorkflowController.cs");
                string serviceSource = File.ReadAllText(
                    "Assets/Norscope/Runtime/AskAI/MockAskAiService.cs");
                Check(!serviceSource.Contains("StartCoroutine") &&
                      !serviceSource.Contains("UIAudioManager") &&
                      !serviceSource.Contains("MonoBehaviour"),
                    "MockAskAiService has no UI/timing/audio");
                Check(controllerSource.Contains("ListeningSeconds = 2f") &&
                      controllerSource.Contains("ProcessingSeconds = 1.5f"),
                    "Controller owns Listening 2.0s and Processing 1.5s");
                Check(controllerSource.Contains("VoiceStartListening") &&
                      controllerSource.Contains("AIThinking") &&
                      controllerSource.Contains("AIResponse"),
                    "Semantic UIAudio events referenced once per transition path");
                Check(controllerSource.Contains("IAskAiSpeechCapture") ||
                      File.Exists("Assets/Norscope/Runtime/AskAI/IAskAiSpeechCapture.cs"),
                    "Speech capture abstraction present (Phase 2)");
                Check(!controllerSource.Contains("OpenAI") &&
                      !controllerSource.Contains("UnityEngine.Windows.Speech") &&
                      !controllerSource.Contains("WebRequest"),
                    "Controller has no LLM/backend and no direct Windows.Speech dependency");
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
