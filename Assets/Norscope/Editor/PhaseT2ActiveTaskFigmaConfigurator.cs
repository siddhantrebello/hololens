using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Norscope.TaskExecution;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Norscope.Editor
{
    /// <summary>
    /// Active-Task-only implementation of active task.zip. All layout is authored
    /// into the prefab by this editor command; runtime scripts only bind data and
    /// existing actions.
    /// </summary>
    public static class PhaseT2ActiveTaskFigmaConfigurator
    {
        private const string PanelPath =
            "Assets/Norscope/UI/Panels/ActiveTaskPanel.prefab";
        private const string RoundedPath =
            "Assets/Norscope/UI/DesignSystem/Sprites/UIRounded_8.png";
        private const string OutfitPath =
            "Assets/Norscope/UI/Fonts/Outfit SDF.asset";
        private const string InterPath =
            "Assets/Norscope/UI/Fonts/Inter SDF.asset";
        private const string LogoPath =
            "Assets/WhatsApp Image 2026-07-19 at 13.42.15-transparent.png";
        private const string IconRoot =
            "Assets/Norscope/UI/Icons/Lucide/Raster/";
        private const string BackupKey = "Norscope.ActiveTaskFigma.Backup";

        private static readonly Color Bg = Hex("0D1117");
        private static readonly Color Panel = Hex("131A22");
        private static readonly Color Card = Hex("18212B");
        private static readonly Color Selected = Hex("192438");
        private static readonly Color Border = Hex("2D3B49");
        private static readonly Color Accent = Hex("1EC8F5");
        private static readonly Color Primary = Hex("F3F7FB");
        private static readonly Color Secondary = Hex("91A2B5");
        private static readonly Color Dim = Hex("4A5D6E");
        private static readonly Color Success = Hex("39D98A");
        private static readonly Color Warning = Hex("F5A623");

        [MenuItem("Norscope/HoloLens/Active Task Figma/1) Capture Active Task Baseline")]
        public static void CaptureMenu()
        {
            string folder = "Assets/Norscope/UI/Panels/_Backups/ActiveTaskFigma_" +
                            DateTime.Now.ToString("yyyyMMdd_HHmmss");
            Directory.CreateDirectory(folder);
            FileUtil.CopyFileOrDirectory(PanelPath,
                folder + "/ActiveTaskPanel.prefab");
            File.WriteAllText(folder + "/manifest.txt",
                "Captured=" + DateTime.Now.ToString("O") + "\n" +
                "Source=" + PanelPath + "\n");
            EditorPrefs.SetString(BackupKey, folder);
            AssetDatabase.Refresh();
            Debug.Log("[Norscope Active Task Figma] Baseline READY\n" + folder);
        }

        [MenuItem("Norscope/HoloLens/Active Task Figma/2) Dry Run")]
        public static void DryRunMenu()
        {
            bool ready = DryRun(out string report);
            Debug.Log((ready
                ? "[Norscope Active Task Figma] Dry run READY\n"
                : "[Norscope Active Task Figma] Dry run BLOCKED\n") + report);
        }

        [MenuItem("Norscope/HoloLens/Active Task Figma/3) Apply")]
        public static void ApplyMenu()
        {
            if (!DryRun(out string report))
            {
                Debug.LogError("[Norscope Active Task Figma] Apply BLOCKED\n" +
                               report);
                return;
            }
            if (!EditorUtility.DisplayDialog("Apply Active Task Figma",
                    "Apply active task.zip to ActiveTaskPanel.prefab only?\n\n" +
                    "Existing task behavior and spatial-window controls are preserved.",
                    "Apply", "Cancel"))
                return;

            ApplyPanel();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Norscope Active Task Figma] Apply COMPLETE");
        }

        [MenuItem("Norscope/HoloLens/Active Task Figma/4) Validate")]
        public static void ValidateMenu()
        {
            bool ok = Validate(out string report);
            Debug.Log((ok
                ? "[Norscope Active Task Figma] Validation PASSED\n"
                : "[Norscope Active Task Figma] Validation FAILED\n") + report);
        }

        [MenuItem("Norscope/HoloLens/Active Task Figma/5) Restore")]
        public static void RestoreMenu()
        {
            string folder = EditorPrefs.GetString(BackupKey, string.Empty);
            string backup = folder + "/ActiveTaskPanel.prefab";
            if (string.IsNullOrEmpty(folder) || !File.Exists(backup))
            {
                Debug.LogError("[Norscope Active Task Figma] No captured backup.");
                return;
            }
            if (!EditorUtility.DisplayDialog("Restore Active Task",
                    "Restore the captured ActiveTaskPanel.prefab?", "Restore",
                    "Cancel"))
                return;
            File.Copy(backup, PanelPath, true);
            AssetDatabase.Refresh();
            Debug.Log("[Norscope Active Task Figma] Restore COMPLETE");
        }

        private static bool DryRun(out string report)
        {
            var result = new StringBuilder();
            var blockers = new List<string>();
            string backup = EditorPrefs.GetString(BackupKey, string.Empty);
            if (!File.Exists(PanelPath)) blockers.Add("ActiveTaskPanel.prefab missing.");
            if (string.IsNullOrEmpty(backup) || !Directory.Exists(backup))
                blockers.Add("Capture an Active Task baseline first.");
            if (!Renderable(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OutfitPath)) ||
                !Renderable(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(InterPath)))
                blockers.Add("Outfit/Inter TMP fonts are not renderable.");

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath);
            ActiveTaskPanel panel = prefab != null
                ? prefab.GetComponent<ActiveTaskPanel>() : null;
            if (panel == null) blockers.Add("ActiveTaskPanel component missing.");
            if (prefab != null)
            {
                CheckRequired(prefab.transform, blockers, "TaskView", "TitleBar",
                    "CloseButton", "GrabRegion", "ResizeHandles", "BottomRight",
                    "BackButtonSlot", "CancelConfirmationView", "CompletionView");
            }

            result.AppendLine("SOURCE  active task.zip / src/app/App.tsx");
            result.AppendLine("TARGET  ActiveTaskPanel.prefab only");
            result.AppendLine("LAYOUT  fixed 880 x 580, fitted inside existing XRI SpatialWindow");
            result.AppendLine("ACTIONS existing Previous, Next/Finish and Cancel are retained");
            result.AppendLine("STATIC  Figma Pause/Evidence/Verification controls remain presentation-only");
            result.AppendLine("PRESERVE Close, Back, Grab, Resize, confirmation and completion views");
            foreach (string blocker in blockers) result.AppendLine("BLOCKER " + blocker);
            result.AppendLine(blockers.Count == 0
                ? "READY — Apply may proceed."
                : "BLOCKED — no prefab should be modified.");
            report = result.ToString();
            return blockers.Count == 0;
        }

        private static void ApplyPanel()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PanelPath);
            try
            {
                ActiveTaskPanel panel = root.GetComponent<ActiveTaskPanel>();
                SerializedObject so = new SerializedObject(panel);

                GameObject taskView = Ref<GameObject>(so, "taskView");
                TMP_Text legacyTitle = Ref<TMP_Text>(so, "titleText");
                TMP_Text stepCounter = Ref<TMP_Text>(so, "stepCounterText");
                Image progressFill = Ref<Image>(so, "progressFill");
                TMP_Text stepTitle = Ref<TMP_Text>(so, "stepTitleText");
                TMP_Text instruction = Ref<TMP_Text>(so, "instructionText");
                GameObject imageSection = Ref<GameObject>(so, "imageSection");
                Image referenceImage = Ref<Image>(so, "referenceImage");
                GameObject warningSection = Ref<GameObject>(so, "warningSection");
                TMP_Text warningText = Ref<TMP_Text>(so, "warningText");
                GameObject verificationSection =
                    Ref<GameObject>(so, "verificationSection");
                TMP_Text verificationText =
                    Ref<TMP_Text>(so, "verificationText");
                Button previous = Ref<Button>(so, "previousButton");
                Button next = Ref<Button>(so, "nextButton");
                Button finish = Ref<Button>(so, "finishButton");
                Button cancel = Ref<Button>(so, "cancelButton");

                if (taskView != null && taskView.name == "FigmaContent")
                {
                    EnsureOptionalImagePresentation(
                        taskView.GetComponent<RectTransform>(), so);
                    IntegrateAsPanel(root, null,
                        taskView.GetComponent<RectTransform>(), legacyTitle, so);
                    so.ApplyModifiedPropertiesWithoutUndo();
                    SetLayerRecursive(root, root.layer);
                    PrefabUtility.SaveAsPrefabAsset(root, PanelPath);
                    return;
                }

                Transform staging = new GameObject("__FigmaStaging",
                    typeof(RectTransform)).transform;
                staging.SetParent(root.transform, false);
                Detach(staging, stepCounter != null ? stepCounter.gameObject : null);
                Detach(staging, progressFill != null
                    ? progressFill.transform.parent.gameObject : null);
                Detach(staging, stepTitle != null ? stepTitle.gameObject : null);
                Detach(staging, instruction != null ? instruction.gameObject : null);
                Detach(staging, imageSection);
                Detach(staging, warningSection);
                Detach(staging, verificationSection);
                Detach(staging, previous != null ? previous.gameObject : null);
                Detach(staging, next != null ? next.gameObject : null);
                Detach(staging, finish != null ? finish.gameObject : null);
                Detach(staging, cancel != null ? cancel.gameObject : null);

                for (int i = taskView.transform.childCount - 1; i >= 0; i--)
                    Object.DestroyImmediate(taskView.transform.GetChild(i).gameObject);

                RemoveLayoutDrivers(taskView);
                LayoutElement taskElement =
                    taskView.GetComponent<LayoutElement>() ??
                    taskView.AddComponent<LayoutElement>();
                taskElement.ignoreLayout = true;
                RectTransform taskRt = taskView.GetComponent<RectTransform>();
                taskRt.anchorMin = new Vector2(0.5f, 0.5f);
                taskRt.anchorMax = new Vector2(0.5f, 0.5f);
                taskRt.pivot = new Vector2(0.5f, 0.5f);
                taskRt.anchoredPosition = new Vector2(0f, -25f);
                taskRt.sizeDelta = new Vector2(664f, 438f);

                Sprite rounded = AssetDatabase.LoadAssetAtPath<Sprite>(RoundedPath);
                TMP_FontAsset outfit =
                    AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OutfitPath);
                TMP_FontAsset inter =
                    AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(InterPath);

                RectTransform content = Node(taskView.transform, "FigmaContent",
                    new Vector2(880f, 580f), Vector2.zero);
                content.localScale = Vector3.one * 0.75f;
                Image contentSurface = content.gameObject.AddComponent<Image>();
                Style(contentSurface, rounded, Bg, false);

                // Header
                Sprite logo = AssetDatabase.LoadAssetAtPath<Sprite>(LogoPath);
                ImageNode(content, "NorscopeLogo", logo, Color.white,
                    new Vector2(40f, 28f), new Vector2(116f, 24f));
                TMP_Text offlineStatus = TextNode(content, "OfflineStatus",
                    "●  Offline", inter, 10f,
                    Success, new Vector2(542f, 29f), new Vector2(78f, 22f),
                    TextAlignmentOptions.Center);
                TMP_Text headerStepCounter = TextNode(content,
                    "HeaderStepCounter", "Step 3 of 7", inter, 10f, Accent,
                    new Vector2(626f, 29f), new Vector2(82f, 22f),
                    TextAlignmentOptions.Center);
                TextNode(content, "RemainingTime", "~12 min remaining", inter,
                    10f, Secondary, new Vector2(714f, 29f),
                    new Vector2(126f, 22f), TextAlignmentOptions.Center);

                Transform backSlot = Find(root.transform, "BackButtonSlot");
                if (backSlot != null)
                {
                    backSlot.SetParent(content, false);
                    Place(backSlot.GetComponent<RectTransform>(),
                        new Vector2(40f, 59f), new Vector2(110f, 24f));
                }

                TMP_Text headerTitle = TextNode(content, "HeaderTaskTitle",
                    "Clean Laptop Cooling Fan", outfit, 34f, Primary,
                    new Vector2(40f, 86f), new Vector2(610f, 42f));
                headerTitle.fontStyle = FontStyles.Normal;
                TMP_Text subtitle = TextNode(content, "TaskSubtitle",
                    "Guided maintenance workflow", inter, 12f, Secondary,
                    new Vector2(40f, 130f), new Vector2(360f, 20f));

                if (legacyTitle != null)
                {
                    legacyTitle.color = new Color(0f, 0f, 0f, 0f);
                    legacyTitle.raycastTarget = false;
                }

                Image separator = ImageNode(content, "Separator", null, Border,
                    new Vector2(40f, 177f), new Vector2(800f, 1f));
                separator.type = Image.Type.Simple;

                // Progress track and authored markers.
                Transform progressTrack = progressFill != null
                    ? progressFill.transform.parent : null;
                if (progressTrack != null)
                {
                    progressTrack.SetParent(content, false);
                    Place(progressTrack.GetComponent<RectTransform>(),
                        new Vector2(40f, 158f), new Vector2(800f, 3f));
                    Image trackImage = progressTrack.GetComponent<Image>();
                    if (trackImage != null) Style(trackImage, rounded, Border, false);
                    progressFill.color = Accent;
                    progressFill.raycastTarget = false;
                }
                var markers = new List<Image>();
                for (int i = 0; i < 7; i++)
                {
                    Image marker = ImageNode(content, "ProgressMarker" + (i + 1),
                        rounded, i < 3 ? Accent : Border,
                        new Vector2(40f + i * 133f, 155f), new Vector2(8f, 8f));
                    markers.Add(marker);
                }

                // Left column: current step card.
                Image current = ImageNode(content, "CurrentStepCard", rounded,
                    Selected, new Vector2(40f, 194f), new Vector2(468f, 112f));
                ImageNode(current.transform, "LeftAccent", null, Accent,
                    new Vector2(0f, 14f), new Vector2(2f, 84f));
                if (stepCounter != null)
                {
                    stepCounter.transform.SetParent(current.transform, false);
                    StyleText(stepCounter, inter, 9f, Accent,
                        TextAlignmentOptions.TopLeft);
                    stepCounter.fontStyle = FontStyles.Bold;
                    Place(stepCounter.rectTransform, new Vector2(16f, 13f),
                        new Vector2(116f, 16f));
                }
                if (stepTitle != null)
                {
                    stepTitle.transform.SetParent(current.transform, false);
                    StyleText(stepTitle, outfit, 17f, Primary,
                        TextAlignmentOptions.TopLeft);
                    stepTitle.fontStyle = FontStyles.Bold;
                    Place(stepTitle.rectTransform, new Vector2(16f, 32f),
                        new Vector2(420f, 26f));
                }
                if (instruction != null)
                {
                    instruction.transform.SetParent(current.transform, false);
                    StyleText(instruction, inter, 11f, Secondary,
                        TextAlignmentOptions.TopLeft);
                    Place(instruction.rectTransform, new Vector2(16f, 61f),
                        new Vector2(430f, 42f));
                }

                // Visual guidance remains visible; the bound image is optional.
                Image guidance = ImageNode(content, "VisualGuidance", rounded,
                    Panel, new Vector2(40f, 316f), new Vector2(468f, 92f));
                TextNode(guidance.transform, "GuidanceLabel", "VISUAL GUIDANCE",
                    inter, 9f, Dim, new Vector2(14f, 10f),
                    new Vector2(180f, 16f));
                TextNode(guidance.transform, "GuidancePlaceholder",
                    "Reference image appears here when supplied by the task step.",
                    inter, 10f, Dim, new Vector2(14f, 36f),
                    new Vector2(440f, 34f));
                if (imageSection != null)
                {
                    RemoveLayoutDrivers(imageSection);
                    imageSection.transform.SetParent(guidance.transform, false);
                    Stretch(imageSection.GetComponent<RectTransform>(), 8f);
                    Image sectionImage = imageSection.GetComponent<Image>();
                    if (sectionImage != null)
                        sectionImage.color = new Color(0f, 0f, 0f, 0f);
                    if (referenceImage != null)
                    {
                        Stretch(referenceImage.rectTransform, 0f);
                        referenceImage.preserveAspect = true;
                        referenceImage.raycastTarget = false;
                    }
                }

                if (warningSection != null)
                {
                    RemoveLayoutDrivers(warningSection);
                    warningSection.transform.SetParent(content, false);
                    Place(warningSection.GetComponent<RectTransform>(),
                        new Vector2(40f, 418f), new Vector2(468f, 60f));
                    Style(warningSection.GetComponent<Image>(), rounded,
                        new Color(Warning.r, Warning.g, Warning.b, 0.07f), false);
                    if (warningText != null)
                    {
                        StyleText(warningText, inter, 10f,
                            new Color(Warning.r, Warning.g, Warning.b, 0.84f),
                            TextAlignmentOptions.TopLeft);
                        Place(warningText.rectTransform, new Vector2(38f, 9f),
                            new Vector2(412f, 42f));
                    }
                    TextNode(warningSection.transform, "SafetyLabel", "SAFETY",
                        inter, 9f, Warning, new Vector2(14f, 9f),
                        new Vector2(70f, 16f));
                }

                TMP_Text verificationStatus = null;
                if (verificationSection != null)
                {
                    RemoveLayoutDrivers(verificationSection);
                    verificationSection.transform.SetParent(content, false);
                    Place(verificationSection.GetComponent<RectTransform>(),
                        new Vector2(40f, 488f), new Vector2(468f, 64f));
                    Style(verificationSection.GetComponent<Image>(), rounded,
                        Card, false);
                    if (verificationText != null)
                    {
                        StyleText(verificationText, inter, 10f, Secondary,
                            TextAlignmentOptions.TopLeft);
                        Place(verificationText.rectTransform,
                            new Vector2(14f, 29f), new Vector2(330f, 27f));
                    }
                    TextNode(verificationSection.transform, "VerificationLabel",
                        "VERIFICATION REQUIRED", inter, 9f, Dim,
                        new Vector2(14f, 9f), new Vector2(190f, 16f));
                    verificationStatus = TextNode(verificationSection.transform,
                        "VerificationStatus", "Pending", inter, 9f, Dim,
                        new Vector2(376f, 8f), new Vector2(76f, 20f),
                        TextAlignmentOptions.Center);
                }

                // Right column workflow rows.
                Image workflow = ImageNode(content, "WorkflowCard", rounded,
                    Card, new Vector2(522f, 194f), new Vector2(318f, 218f));
                TextNode(workflow.transform, "WorkflowLabel", "WORKFLOW STEPS",
                    inter, 9f, Dim, new Vector2(12f, 10f),
                    new Vector2(180f, 16f));
                var rows = new List<ActiveTaskStepRowView>();
                for (int i = 0; i < 7; i++)
                    rows.Add(CreateWorkflowRow(workflow.transform, i, rounded,
                        outfit, inter));

                Image evidence = ImageNode(content, "EvidenceCard", rounded,
                    Card, new Vector2(522f, 422f), new Vector2(318f, 46f));
                TextNode(evidence.transform, "EvidenceLabel", "EVIDENCE",
                    inter, 9f, Dim, new Vector2(12f, 8f),
                    new Vector2(80f, 14f));
                TextNode(evidence.transform, "EvidenceStatus",
                    "No evidence added", inter, 9f, Secondary,
                    new Vector2(12f, 25f), new Vector2(150f, 14f));
                TextNode(evidence.transform, "EvidenceActions",
                    "Capture photo   ·   Add voice note", inter, 9f, Dim,
                    new Vector2(154f, 16f), new Vector2(152f, 18f),
                    TextAlignmentOptions.Right);

                // Existing action controls keep their original Button components.
                StyleAction(next, content, "Confirm and Continue", Accent, Bg,
                    new Vector2(522f, 478f), new Vector2(318f, 42f), rounded,
                    outfit, true);
                StyleAction(finish, content, "Finish Task", Accent, Bg,
                    new Vector2(522f, 478f), new Vector2(318f, 42f), rounded,
                    outfit, true);
                StyleAction(previous, content, "Previous Step", Card, Secondary,
                    new Vector2(522f, 527f), new Vector2(154f, 36f), rounded,
                    outfit, false);
                StyleAction(cancel, content, "Cancel Task",
                    new Color(0f, 0f, 0f, 0f), Dim,
                    new Vector2(686f, 527f), new Vector2(154f, 36f), rounded,
                    inter, false);

                TextNode(content, "Footer", "v4.2.1 · HoloLens 2", inter, 9f,
                    Dim, new Vector2(680f, 565f), new Vector2(160f, 15f),
                    TextAlignmentOptions.Right);

                SetRef(so, "headerTaskTitleText", headerTitle);
                SetRef(so, "headerStepCounterText", headerStepCounter);
                SetRef(so, "offlineStatusText", offlineStatus);
                SetRef(so, "taskSubtitleText", subtitle);
                SetRef(so, "remainingTimeText",
                    Find(content, "RemainingTime").GetComponent<TMP_Text>());
                SetRef(so, "verificationStatusText", verificationStatus);
                SetArray(so.FindProperty("workflowRows"), rows);
                SetArray(so.FindProperty("progressMarkers"), markers);

                IntegrateAsPanel(root, taskView, content, legacyTitle, so);
                so.ApplyModifiedPropertiesWithoutUndo();

                if (staging.childCount == 0)
                    Object.DestroyImmediate(staging.gameObject);

                SetLayerRecursive(root, root.layer);
                PrefabUtility.SaveAsPrefabAsset(root, PanelPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void IntegrateAsPanel(GameObject root,
            GameObject legacyTaskView, RectTransform content,
            TMP_Text legacyTitle, SerializedObject panel)
        {
            content.SetParent(root.transform, false);
            content.SetAsFirstSibling();
            content.anchorMin = content.anchorMax = new Vector2(0.5f, 0.5f);
            content.pivot = new Vector2(0.5f, 0.5f);
            content.sizeDelta = new Vector2(880f, 580f);
            content.anchoredPosition = Vector2.zero;
            float scale = root.GetComponent<RectTransform>().rect.width / 880f;
            content.localScale = Vector3.one * scale;

            Image authoredBacking = content.GetComponent<Image>();
            if (authoredBacking != null)
                authoredBacking.color = Color.clear;

            Transform titleBar = Find(root.transform, "TitleBar");
            Image titleBarImage = titleBar != null
                ? titleBar.GetComponent<Image>() : null;
            if (titleBarImage != null)
            {
                titleBarImage.color = Color.clear;
                titleBarImage.raycastTarget = false;
            }

            if (legacyTitle != null)
            {
                legacyTitle.color = Color.clear;
                legacyTitle.raycastTarget = false;
                legacyTitle.gameObject.SetActive(false);
            }

            SetRef(panel, "taskView", content.gameObject);
            if (legacyTaskView != null && legacyTaskView != content.gameObject)
                legacyTaskView.SetActive(false);
        }

        private static void EnsureOptionalImagePresentation(
            RectTransform content, SerializedObject panel)
        {
            GameObject existingSection = Ref<GameObject>(panel, "imageSection");
            Image existingImage = Ref<Image>(panel, "referenceImage");
            if (existingSection != null && existingImage != null) return;

            Sprite rounded = AssetDatabase.LoadAssetAtPath<Sprite>(RoundedPath);
            TMP_FontAsset inter =
                AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(InterPath);
            Transform existingGuidance = Find(content, "VisualGuidance");
            Image guidance = existingGuidance != null
                ? existingGuidance.GetComponent<Image>()
                : ImageNode(content, "VisualGuidance", rounded, Panel,
                    new Vector2(40f, 316f), new Vector2(468f, 92f));

            if (Find(guidance.transform, "GuidanceLabel") == null)
                TextNode(guidance.transform, "GuidanceLabel",
                    "VISUAL GUIDANCE", inter, 9f, Dim,
                    new Vector2(14f, 10f), new Vector2(180f, 16f));
            if (Find(guidance.transform, "GuidancePlaceholder") == null)
                TextNode(guidance.transform, "GuidancePlaceholder",
                    "Reference image appears here when supplied by the task step.",
                    inter, 10f, Dim, new Vector2(14f, 36f),
                    new Vector2(440f, 34f));

            var section = new GameObject("ImageSection",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            section.transform.SetParent(guidance.transform, false);
            Stretch(section.GetComponent<RectTransform>(), 8f);
            Image sectionSurface = section.GetComponent<Image>();
            sectionSurface.color = Color.clear;
            sectionSurface.raycastTarget = false;

            var imageObject = new GameObject("ReferenceImage",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(section.transform, false);
            Image reference = imageObject.GetComponent<Image>();
            Stretch(reference.rectTransform, 0f);
            reference.preserveAspect = true;
            reference.raycastTarget = false;

            SetRef(panel, "imageSection", section);
            SetRef(panel, "referenceImage", reference);
        }

        private static ActiveTaskStepRowView CreateWorkflowRow(Transform parent,
            int index, Sprite rounded, TMP_FontAsset outfit, TMP_FontAsset inter)
        {
            Image surface = ImageNode(parent, "WorkflowStep" + (index + 1),
                rounded, index == 2 ? Selected : new Color(0f, 0f, 0f, 0f),
                new Vector2(8f, 31f + index * 25f),
                new Vector2(302f, 24f));
            Image indicator = ImageNode(surface.transform, "State", rounded,
                index < 2 ? Success : index == 2 ? Accent : Dim,
                new Vector2(8f, 5f), new Vector2(14f, 14f));
            TMP_Text number = TextNode(surface.transform, "Number",
                (index + 1).ToString(), inter, 8f, Primary,
                new Vector2(8f, 4f), new Vector2(14f, 16f),
                TextAlignmentOptions.Center);
            TMP_Text title = TextNode(surface.transform, "Title", "Task step",
                outfit, 10f, index <= 2 ? Primary : Secondary,
                new Vector2(30f, 3f), new Vector2(258f, 18f));
            ActiveTaskStepRowView view =
                surface.gameObject.AddComponent<ActiveTaskStepRowView>();
            SerializedObject so = new SerializedObject(view);
            SetRef(so, "surface", surface);
            SetRef(so, "stateIndicator", indicator);
            SetRef(so, "numberText", number);
            SetRef(so, "titleText", title);
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        private static void StyleAction(Button button, Transform parent,
            string label, Color surfaceColor, Color textColor, Vector2 position,
            Vector2 size, Sprite rounded, TMP_FontAsset font, bool primary)
        {
            if (button == null) return;
            button.transform.SetParent(parent, false);
            Place(button.GetComponent<RectTransform>(), position, size);
            Image image = button.targetGraphic as Image ??
                          button.GetComponent<Image>();
            Style(image, rounded, surfaceColor, true);
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = primary
                ? new Color(1f, 1f, 1f, 1f)
                : new Color(1.08f, 1.08f, 1.08f, 1f);
            colors.pressedColor = new Color(0.86f, 0.9f, 0.94f, 1f);
            button.colors = colors;
            TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
            {
                text.text = label;
                StyleText(text, font, primary ? 12f : 11f, textColor,
                    TextAlignmentOptions.Center);
                Stretch(text.rectTransform, 4f);
            }
        }

        private static bool Validate(out string report)
        {
            var lines = new StringBuilder();
            bool ok = true;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath);
            void Check(bool value, string label)
            {
                ok &= value;
                lines.AppendLine((value ? "PASS  " : "FAIL  ") + label);
            }
            Check(prefab != null, "ActiveTaskPanel.prefab exists");
            if (prefab != null)
            {
                ActiveTaskPanel panel = prefab.GetComponent<ActiveTaskPanel>();
                Check(panel != null, "ActiveTaskPanel component remains present");
                Check(Find(prefab.transform, "FigmaContent") != null,
                    "Authored 880 x 580 FigmaContent exists");
                RectTransform figmaRect =
                    Find(prefab.transform, "FigmaContent") as RectTransform;
                Check(figmaRect != null &&
                      Approximately(figmaRect.sizeDelta, new Vector2(880f, 580f)) &&
                      Approximately(figmaRect.anchoredPosition, Vector2.zero) &&
                      figmaRect.parent == prefab.transform &&
                      Mathf.Approximately(figmaRect.localScale.x, 700f / 880f) &&
                      Mathf.Approximately(figmaRect.localScale.y, 700f / 880f),
                    "FigmaContent is the integrated panel view");
                if (panel != null)
                {
                    SerializedObject panelSo = new SerializedObject(panel);
                    Check(Ref<GameObject>(panelSo, "taskView") ==
                          figmaRect?.gameObject,
                        "ActiveTaskPanel.taskView is the authored Figma panel");
                    Check(Ref<GameObject>(panelSo, "imageSection") != null,
                        "Optional ImageSection reference is assigned");
                    Check(Ref<Image>(panelSo, "referenceImage") != null,
                        "Optional ReferenceImage reference is assigned");
                }
                Check(Find(prefab.transform, "CloseButton") != null,
                    "Close button remains present");
                Check(Find(prefab.transform, "GrabRegion") != null,
                    "Grab region remains present");
                Check(Find(prefab.transform, "BottomRight") != null,
                    "Resize handle remains present");
                Check(Find(prefab.transform, "BackButtonSlot") != null,
                    "Back button remains present");
                Check(prefab.GetComponentsInChildren<Button>(true).Length >= 8,
                    "Existing functional controls remain present");
                Check(prefab.GetComponentsInChildren<ActiveTaskStepRowView>(true)
                          .Length == 7,
                    "Seven authored workflow row slots exist");
                Transform figma = Find(prefab.transform, "FigmaContent");
                foreach (Graphic graphic in figma != null
                             ? figma.GetComponentsInChildren<Graphic>(true)
                             : Array.Empty<Graphic>())
                {
                    if (graphic.GetComponentInParent<Button>() == null &&
                        graphic.raycastTarget)
                    {
                        ok = false;
                        lines.AppendLine("FAIL  Decorative graphic raycasts: " +
                                         PathOf(graphic.transform));
                    }
                }
            }
            report = lines.ToString();
            return ok;
        }

        private static void CheckRequired(Transform root, List<string> blockers,
            params string[] names)
        {
            foreach (string name in names)
                if (Find(root, name) == null) blockers.Add(name + " missing.");
        }

        private static void Detach(Transform staging, GameObject go)
        {
            if (go != null && go.transform.parent != staging)
                go.transform.SetParent(staging, false);
        }

        private static void RemoveLayoutDrivers(GameObject target)
        {
            if (target == null) return;
            foreach (LayoutGroup group in target.GetComponents<LayoutGroup>())
                Object.DestroyImmediate(group);
            foreach (ContentSizeFitter fitter in
                     target.GetComponents<ContentSizeFitter>())
                Object.DestroyImmediate(fitter);
        }

        private static RectTransform Node(Transform parent, string name,
            Vector2 size, Vector2 position)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(position.x, -position.y);
            rt.sizeDelta = size;
            return rt;
        }

        private static Image ImageNode(Transform parent, string name,
            Sprite sprite, Color color, Vector2 position, Vector2 size)
        {
            RectTransform rt = Node(parent, name, size, position);
            Image image = rt.gameObject.AddComponent<Image>();
            Style(image, sprite, color, false);
            return image;
        }

        private static TMP_Text TextNode(Transform parent, string name,
            string value, TMP_FontAsset font, float size, Color color,
            Vector2 position, Vector2 dimensions,
            TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft)
        {
            RectTransform rt = Node(parent, name, dimensions, position);
            TMP_Text text = rt.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            StyleText(text, font, size, color, alignment);
            return text;
        }

        private static void Style(Image image, Sprite sprite, Color color,
            bool raycast)
        {
            if (image == null) return;
            image.sprite = sprite;
            image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            image.color = color;
            image.raycastTarget = raycast;
        }

        private static void StyleText(TMP_Text text, TMP_FontAsset font,
            float size, Color color, TextAlignmentOptions alignment)
        {
            if (text == null) return;
            text.font = font;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.enableWordWrapping = true;
            text.margin = Vector4.zero;
        }

        private static void Place(RectTransform rt, Vector2 position,
            Vector2 size)
        {
            if (rt == null) return;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(position.x, -position.y);
            rt.sizeDelta = size;
            rt.localScale = Vector3.one;
        }

        private static void Stretch(RectTransform rt, float inset)
        {
            if (rt == null) return;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(-2f * inset, -2f * inset);
            rt.localScale = Vector3.one;
        }

        private static T Ref<T>(SerializedObject so, string name)
            where T : Object
        {
            SerializedProperty property = so.FindProperty(name);
            return property != null ? property.objectReferenceValue as T : null;
        }

        private static void SetRef(SerializedObject so, string name, Object value)
        {
            SerializedProperty property = so.FindProperty(name);
            if (property != null) property.objectReferenceValue = value;
        }

        private static void SetArray<T>(SerializedProperty property,
            IList<T> values) where T : Object
        {
            property.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        private static Transform Find(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = Find(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        private static void SetLayerRecursive(GameObject root, int layer)
        {
            root.layer = layer;
            for (int i = 0; i < root.transform.childCount; i++)
                SetLayerRecursive(root.transform.GetChild(i).gameObject, layer);
        }

        private static bool Renderable(TMP_FontAsset font)
        {
            return font != null && font.material != null &&
                   font.atlasTexture != null;
        }

        private static bool Approximately(Vector2 a, Vector2 b)
        {
            return Mathf.Abs(a.x - b.x) < 0.01f &&
                   Mathf.Abs(a.y - b.y) < 0.01f;
        }

        private static Color Hex(string value)
        {
            ColorUtility.TryParseHtmlString("#" + value, out Color color);
            return color;
        }

        private static string PathOf(Transform target)
        {
            string path = target.name;
            while (target.parent != null)
            {
                target = target.parent;
                path = target.name + "/" + path;
            }
            return path;
        }
    }
}
