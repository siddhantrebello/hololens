using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Norscope.Navigation;
using Norscope.TaskExecution;
using Norscope.TaskLibrary;
using Norscope.UI;
using Norscope.Windows;

namespace Norscope.Editor
{
    /// <summary>
    /// Phase 7: Active Task execution panel only. Normal Create/Wire never overwrites the
    /// authored prefab; only the explicit destructive rebuild command can replace it.
    /// </summary>
    public static class Phase7ActiveTaskConfigurator
    {
        private const string BootstrapScenePath = "Assets/Norscope/Scenes/Bootstrap.unity";
        private const string ActiveTaskPanelPath = "Assets/Norscope/UI/Panels/ActiveTaskPanel.prefab";
        private const string TaskLibraryPanelPath = "Assets/Norscope/UI/Panels/TaskLibraryPanel.prefab";
        private const string ThemePath = "Assets/Norscope/UI/DesignSystem/Themes/NorscopeTheme.asset";
        private const string WorldConfigPath = "Assets/Norscope/UI/DesignSystem/Themes/NorscopeWorldSpaceUiConfig.asset";
        private const string RoundedSpritePath = "Assets/Norscope/UI/DesignSystem/Sprites/UIRounded_8.png";
        private const string BackupDir = "Assets/Norscope/UI/Panels/_Backups";

        private const string SessionSourcePath = "Assets/Norscope/Runtime/TaskExecution/ActiveTaskSession.cs";
        private const string LauncherSourcePath = "Assets/Norscope/Runtime/TaskExecution/ActiveTaskLauncher.cs";
        private const string PanelSourcePath = "Assets/Norscope/Runtime/TaskExecution/ActiveTaskPanel.cs";

        [MenuItem("Norscope/HoloLens/Phase 7/1) Create Active Task Panel If Missing")]
        public static void CreateIfMissingMenu()
        {
            bool created = CreateIfMissing();
            Debug.Log("[Norscope Phase7] ActiveTaskPanel.prefab " +
                      (created ? "created." : "already existed and was left untouched."));
        }

        public static bool CreateIfMissing()
        {
            if (File.Exists(ActiveTaskPanelPath))
            {
                return false;
            }

            BuildActiveTaskPanelPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return true;
        }

        [MenuItem("Norscope/HoloLens/Phase 7/2) Wire Active Task Scene")]
        public static void WireSceneMenu()
        {
            WireScene();
            Debug.Log("[Norscope Phase7] Active Task scene wiring complete.");
        }

        public static void WireScene()
        {
            CreateIfMissing();

            Scene scene = EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);
            GameObject panelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ActiveTaskPanelPath);
            PanelRegistry registry = Object.FindFirstObjectByType<PanelRegistry>();
            SpatialWindowManager manager = Object.FindFirstObjectByType<SpatialWindowManager>();
            if (panelPrefab == null || registry == null || manager == null)
            {
                throw new System.InvalidOperationException(
                    "ActiveTaskPanel prefab, PanelRegistry, or SpatialWindowManager is missing.");
            }

            SerializedObject soRegistry = new SerializedObject(registry);
            SerializedProperty entries = soRegistry.FindProperty("entries");
            bool found = false;
            for (int i = 0; i < entries.arraySize; i++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                if ((PanelType)entry.FindPropertyRelative("panelType").enumValueIndex != PanelType.TaskExecution)
                {
                    continue;
                }

                entry.FindPropertyRelative("windowId").enumValueIndex = (int)SpatialWindowId.TaskExecution;
                entry.FindPropertyRelative("behaviour").enumValueIndex = (int)PanelBehaviourType.WorldLocked;
                entry.FindPropertyRelative("prefab").objectReferenceValue = panelPrefab;
                found = true;
                break;
            }

            if (!found)
            {
                throw new System.InvalidOperationException(
                    "PanelRegistry has no existing TaskExecution placeholder entry.");
            }

            soRegistry.ApplyModifiedPropertiesWithoutUndo();
            registry.RebuildLookup();

            GameObject host = registry.gameObject;
            ActiveTaskSession session = host.GetComponent<ActiveTaskSession>();
            if (session == null)
            {
                session = host.AddComponent<ActiveTaskSession>();
            }

            ActiveTaskLauncher launcher = host.GetComponent<ActiveTaskLauncher>();
            if (launcher == null)
            {
                launcher = host.AddComponent<ActiveTaskLauncher>();
            }

            launcher.SetReferences(session, registry, manager);
            SerializedObject soLauncher = new SerializedObject(launcher);
            soLauncher.FindProperty("session").objectReferenceValue = session;
            soLauncher.FindProperty("panelRegistry").objectReferenceValue = registry;
            soLauncher.FindProperty("windowManager").objectReferenceValue = manager;
            soLauncher.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(host);
            EditorUtility.SetDirty(registry);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Norscope/HoloLens/Phase 7/3) Validate Active Task")]
        public static void ValidateMenu()
        {
            bool ok = Validate(out string report);
            Debug.Log(ok
                ? "[Norscope Phase7] Validate Active Task PASSED\n" + report
                : "[Norscope Phase7] Validate Active Task FAILED\n" + report);
            EditorUtility.DisplayDialog(
                "Validate Active Task",
                (ok ? "PASSED\n\n" : "FAILED\n\n") + report,
                "OK");
        }

        [MenuItem("Norscope/HoloLens/Phase 7/4) Rebuild Active Task Panel (Destructive)")]
        public static void RebuildDestructiveMenu()
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "Rebuild ActiveTaskPanel.prefab - Destructive",
                "This replaces ONLY:\n" + ActiveTaskPanelPath +
                "\n\nAll manual prefab edits will be lost. A timestamped backup is created first.",
                "Back Up and Rebuild",
                "Cancel");
            if (!confirmed) return;

            BackupPrefabIfExists(ActiveTaskPanelPath);
            BuildActiveTaskPanelPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Norscope Phase7] Destructive Active Task panel rebuild complete.");
        }

        [MenuItem("Norscope/HoloLens/Phase 7/5) Make Active Task Fully Manually Editable")]
        public static void MakeFullyManuallyEditableMenu()
        {
            if (!File.Exists(ActiveTaskPanelPath))
            {
                EditorUtility.DisplayDialog(
                    "Active Task prefab missing",
                    ActiveTaskPanelPath + " does not exist.",
                    "OK");
                return;
            }

            bool confirmed = EditorUtility.DisplayDialog(
                "Make Active Task Fully Manually Editable",
                "This one-time conversion preserves the panel's current appearance, backs up " +
                "the prefab, then removes the Layout Groups and Layout Elements that prevent " +
                "manual RectTransform movement.\n\nRuntime bindings and the nested Back button " +
                "remain intact.",
                "Back Up and Convert",
                "Cancel");
            if (!confirmed) return;

            BackupPrefabIfExists(ActiveTaskPanelPath);
            BakeActiveTaskLayoutToRectTransforms();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Norscope Phase7] ActiveTaskPanel is now manually positioned. " +
                      "Normal Wire/Validate commands will not overwrite it.");
        }

        private sealed class RectTransformSnapshot
        {
            public RectTransform rect;
            public float left;
            public float top;
            public float width;
            public float height;
            public int depth;
        }

        private static void BakeActiveTaskLayoutToRectTransforms()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(ActiveTaskPanelPath);
            try
            {
                RectTransform rootRect = root.GetComponent<RectTransform>();
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(rootRect);

                var controlledRects = new System.Collections.Generic.HashSet<RectTransform>();
                LayoutGroup[] groups = root.GetComponentsInChildren<LayoutGroup>(true);
                for (int i = 0; i < groups.Length; i++)
                {
                    Transform groupTransform = groups[i].transform;
                    for (int childIndex = 0; childIndex < groupTransform.childCount; childIndex++)
                    {
                        RectTransform child =
                            groupTransform.GetChild(childIndex) as RectTransform;
                        if (child != null) controlledRects.Add(child);
                    }
                }

                ContentSizeFitter[] fitters =
                    root.GetComponentsInChildren<ContentSizeFitter>(true);
                for (int i = 0; i < fitters.Length; i++)
                {
                    RectTransform fitted = fitters[i].transform as RectTransform;
                    if (fitted != null && fitted != rootRect) controlledRects.Add(fitted);
                }

                var snapshots =
                    new System.Collections.Generic.List<RectTransformSnapshot>();
                foreach (RectTransform rect in controlledRects)
                {
                    if (rect == null || rect.parent == null) continue;
                    var corners = new Vector3[4];
                    rect.GetWorldCorners(corners);
                    Vector3 bottomLeft = rect.parent.InverseTransformPoint(corners[0]);
                    Vector3 topRight = rect.parent.InverseTransformPoint(corners[2]);
                    snapshots.Add(new RectTransformSnapshot
                    {
                        rect = rect,
                        left = bottomLeft.x,
                        top = topRight.y,
                        width = topRight.x - bottomLeft.x,
                        height = topRight.y - bottomLeft.y,
                        depth = GetDepth(rect)
                    });
                }

                for (int i = groups.Length - 1; i >= 0; i--)
                {
                    if (!BelongsToNestedPrefab(groups[i].gameObject, root))
                    {
                        Object.DestroyImmediate(groups[i]);
                    }
                }

                for (int i = fitters.Length - 1; i >= 0; i--)
                {
                    if (!BelongsToNestedPrefab(fitters[i].gameObject, root))
                    {
                        Object.DestroyImmediate(fitters[i]);
                    }
                }

                LayoutElement[] layoutElements =
                    root.GetComponentsInChildren<LayoutElement>(true);
                for (int i = layoutElements.Length - 1; i >= 0; i--)
                {
                    if (!BelongsToNestedPrefab(layoutElements[i].gameObject, root))
                    {
                        Object.DestroyImmediate(layoutElements[i]);
                    }
                }

                snapshots.Sort((a, b) => a.depth.CompareTo(b.depth));
                for (int i = 0; i < snapshots.Count; i++)
                {
                    RectTransformSnapshot snapshot = snapshots[i];
                    if (snapshot.rect == null ||
                        BelongsToNestedPrefab(snapshot.rect.gameObject, root))
                    {
                        continue;
                    }

                    snapshot.rect.anchorMin = new Vector2(0f, 1f);
                    snapshot.rect.anchorMax = new Vector2(0f, 1f);
                    snapshot.rect.pivot = new Vector2(0f, 1f);
                    snapshot.rect.sizeDelta =
                        new Vector2(snapshot.width, snapshot.height);
                    snapshot.rect.anchoredPosition =
                        new Vector2(snapshot.left, snapshot.top);
                }

                PrefabUtility.SaveAsPrefabAsset(root, ActiveTaskPanelPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static int GetDepth(Transform transform)
        {
            int depth = 0;
            Transform current = transform;
            while (current != null)
            {
                depth++;
                current = current.parent;
            }
            return depth;
        }

        private static bool BelongsToNestedPrefab(GameObject gameObject, GameObject panelRoot)
        {
            GameObject nearest = PrefabUtility.GetNearestPrefabInstanceRoot(gameObject);
            return nearest != null && nearest != panelRoot;
        }

        private static string BuildActiveTaskPanelPrefab()
        {
            NorscopeTheme theme = AssetDatabase.LoadAssetAtPath<NorscopeTheme>(ThemePath);
            NorscopeWorldSpaceUiConfig world =
                AssetDatabase.LoadAssetAtPath<NorscopeWorldSpaceUiConfig>(WorldConfigPath);
            Sprite rounded = AssetDatabase.LoadAssetAtPath<Sprite>(RoundedSpritePath);
            TMP_FontAsset font = theme != null && theme.fonts != null ? theme.fonts.bodyFont : null;

            const float width = 700f;
            const float height = 540f;

            GameObject root = new GameObject("ActiveTaskPanel", typeof(RectTransform));
            RectTransform rootRt = root.GetComponent<RectTransform>();
            rootRt.sizeDelta = new Vector2(width, height);

            Image rootBackground = root.AddComponent<Image>();
            rootBackground.sprite = rounded;
            rootBackground.type = Image.Type.Sliced;
            rootBackground.color = theme != null ? theme.panelBackground : new Color(0.075f, 0.102f, 0.141f, 0.96f);
            rootBackground.raycastTarget = true;

            VerticalLayoutGroup rootLayout = root.AddComponent<VerticalLayoutGroup>();
            rootLayout.padding = new RectOffset(0, 0, 0, 0);
            rootLayout.spacing = 0f;
            rootLayout.childControlWidth = true;
            rootLayout.childControlHeight = true;
            rootLayout.childForceExpandWidth = true;
            rootLayout.childForceExpandHeight = false;

            GameObject titleBar = CreateTitleBar(root.transform, theme, rounded, font, out TMP_Text titleText,
                out GameObject grabRegion, out GameObject closeButton);

            GameObject taskView = new GameObject("TaskView", typeof(RectTransform));
            taskView.transform.SetParent(root.transform, false);
            LayoutElement taskViewLayout = taskView.AddComponent<LayoutElement>();
            taskViewLayout.flexibleHeight = 1f;
            VerticalLayoutGroup taskLayout = taskView.AddComponent<VerticalLayoutGroup>();
            taskLayout.padding = new RectOffset(18, 18, 14, 14);
            taskLayout.spacing = 12f;
            taskLayout.childControlWidth = true;
            taskLayout.childControlHeight = true;
            taskLayout.childForceExpandWidth = true;
            taskLayout.childForceExpandHeight = false;

            GameObject stepHeader = new GameObject("StepHeader", typeof(RectTransform));
            stepHeader.transform.SetParent(taskView.transform, false);
            stepHeader.AddComponent<LayoutElement>().preferredHeight = 42f;
            VerticalLayoutGroup stepHeaderLayout = stepHeader.AddComponent<VerticalLayoutGroup>();
            stepHeaderLayout.spacing = 5f;
            stepHeaderLayout.childControlWidth = true;
            stepHeaderLayout.childControlHeight = true;
            stepHeaderLayout.childForceExpandWidth = true;
            stepHeaderLayout.childForceExpandHeight = false;

            TMP_Text stepCounter = CreateText(stepHeader.transform, "StepCounter", "Step 1 of 1",
                font, 15f, theme != null ? theme.secondaryText : Color.gray, 20f);

            GameObject progressTrack = new GameObject("ProgressTrack", typeof(RectTransform));
            progressTrack.transform.SetParent(stepHeader.transform, false);
            progressTrack.AddComponent<LayoutElement>().preferredHeight = 8f;
            Image progressTrackImage = progressTrack.AddComponent<Image>();
            progressTrackImage.sprite = rounded;
            progressTrackImage.type = Image.Type.Sliced;
            progressTrackImage.color = theme != null ? theme.border : Color.gray;
            progressTrackImage.raycastTarget = false;

            GameObject progressFillGo = new GameObject("ProgressFill", typeof(RectTransform));
            progressFillGo.transform.SetParent(progressTrack.transform, false);
            StretchFull(progressFillGo.GetComponent<RectTransform>());
            Image progressFill = progressFillGo.AddComponent<Image>();
            progressFill.sprite = rounded;
            progressFill.type = Image.Type.Filled;
            progressFill.fillMethod = Image.FillMethod.Horizontal;
            progressFill.fillOrigin = 0;
            progressFill.fillAmount = 0.25f;
            progressFill.color = theme != null ? theme.primaryAccent : Color.cyan;
            progressFill.raycastTarget = false;

            GameObject mainContent = new GameObject("MainContent", typeof(RectTransform));
            mainContent.transform.SetParent(taskView.transform, false);
            LayoutElement mainContentLayout = mainContent.AddComponent<LayoutElement>();
            mainContentLayout.flexibleHeight = 1f;
            VerticalLayoutGroup mainLayout = mainContent.AddComponent<VerticalLayoutGroup>();
            mainLayout.spacing = 10f;
            mainLayout.childControlWidth = true;
            mainLayout.childControlHeight = true;
            mainLayout.childForceExpandWidth = true;
            mainLayout.childForceExpandHeight = false;

            TMP_Text stepTitle = CreateText(mainContent.transform, "StepTitle", "Step title",
                font, 22f, theme != null ? theme.primaryText : Color.white, 32f);
            TMP_Text instruction = CreateText(mainContent.transform, "Instruction",
                "Instruction text appears here.", font, 16f,
                theme != null ? theme.primaryText : Color.white, 76f);

            GameObject imageSection = CreateSection(mainContent.transform, "ImageSection", theme, rounded, 120f);
            Image referenceImage = CreateReferenceImage(imageSection.transform);

            GameObject warningSection = CreateSection(mainContent.transform, "WarningSection", theme, rounded, 54f);
            TMP_Text warningText = CreateText(warningSection.transform, "WarningText", "Warning",
                font, 14f, theme != null ? theme.warning : Color.yellow, 42f);

            GameObject verificationSection = CreateSection(mainContent.transform, "VerificationSection", theme, rounded, 54f);
            TMP_Text verificationText = CreateText(verificationSection.transform, "VerificationText",
                "Verification", font, 14f, theme != null ? theme.success : Color.green, 42f);

            GameObject bottomNav = new GameObject("BottomNavigation", typeof(RectTransform));
            bottomNav.transform.SetParent(taskView.transform, false);
            bottomNav.AddComponent<LayoutElement>().preferredHeight = 46f;
            HorizontalLayoutGroup navLayout = bottomNav.AddComponent<HorizontalLayoutGroup>();
            navLayout.spacing = 12f;
            navLayout.childAlignment = TextAnchor.MiddleRight;
            navLayout.childControlWidth = false;
            navLayout.childControlHeight = true;
            navLayout.childForceExpandWidth = false;
            navLayout.childForceExpandHeight = true;

            GameObject spacer = new GameObject("Spacer", typeof(RectTransform));
            spacer.transform.SetParent(bottomNav.transform, false);
            spacer.AddComponent<LayoutElement>().flexibleWidth = 1f;
            Button previousButton = CreateButton(bottomNav.transform, "PreviousButton", "Previous", 116f, theme, rounded, font, false);
            Button nextButton = CreateButton(bottomNav.transform, "NextButton", "Next", 104f, theme, rounded, font, true);
            Button finishButton = CreateButton(bottomNav.transform, "FinishButton", "Finish", 104f, theme, rounded, font, true);

            GameObject completionView = CreateCompletionView(root.transform, theme, rounded, font,
                out TMP_Text completionTaskTitle, out TMP_Text completionMessage,
                out Button returnHomeButton, out Button reportButton);
            completionView.SetActive(false);

            GameObject resizeHandles = new GameObject("ResizeHandles", typeof(RectTransform));
            resizeHandles.transform.SetParent(root.transform, false);
            StretchFull(resizeHandles.GetComponent<RectTransform>());
            LayoutElement handlesLayout = resizeHandles.AddComponent<LayoutElement>();
            handlesLayout.ignoreLayout = true;
            PlaceResizeHandle(resizeHandles.transform, theme);

            NorscopePanelRoot panelRoot = root.AddComponent<NorscopePanelRoot>();
            SerializedObject soPanelRoot = new SerializedObject(panelRoot);
            soPanelRoot.FindProperty("theme").objectReferenceValue = theme;
            soPanelRoot.FindProperty("worldSpaceConfig").objectReferenceValue = world;
            soPanelRoot.FindProperty("root").objectReferenceValue = rootRt;
            soPanelRoot.FindProperty("background").objectReferenceValue = rootBackground;
            soPanelRoot.FindProperty("title").objectReferenceValue = titleText;
            soPanelRoot.FindProperty("sizeMeters").vector2Value = new Vector2(width / 1000f, height / 1000f);
            soPanelRoot.FindProperty("sizePixels").vector2Value = new Vector2(width, height);
            soPanelRoot.FindProperty("useMetersAsSource").boolValue = true;
            soPanelRoot.ApplyModifiedPropertiesWithoutUndo();

            SpatialWindowBindings bindings = root.AddComponent<SpatialWindowBindings>();
            SerializedObject soBindings = new SerializedObject(bindings);
            soBindings.FindProperty("grabRegion").objectReferenceValue = grabRegion.transform;
            soBindings.FindProperty("resizeHandle").objectReferenceValue =
                resizeHandles.transform.Find("BottomRight") as RectTransform;
            soBindings.FindProperty("closeButtonRoot").objectReferenceValue = closeButton.transform;
            soBindings.ApplyModifiedPropertiesWithoutUndo();

            ActiveTaskPanel panel = root.AddComponent<ActiveTaskPanel>();
            SerializedObject soPanel = new SerializedObject(panel);
            SetRef(soPanel, "taskView", taskView);
            SetRef(soPanel, "completionView", completionView);
            SetRef(soPanel, "titleText", titleText);
            SetRef(soPanel, "stepCounterText", stepCounter);
            SetRef(soPanel, "progressFill", progressFill);
            SetRef(soPanel, "stepTitleText", stepTitle);
            SetRef(soPanel, "instructionText", instruction);
            SetRef(soPanel, "imageSection", imageSection);
            SetRef(soPanel, "referenceImage", referenceImage);
            SetRef(soPanel, "warningSection", warningSection);
            SetRef(soPanel, "warningText", warningText);
            SetRef(soPanel, "verificationSection", verificationSection);
            SetRef(soPanel, "verificationText", verificationText);
            SetRef(soPanel, "previousButton", previousButton);
            SetRef(soPanel, "nextButton", nextButton);
            SetRef(soPanel, "finishButton", finishButton);
            SetRef(soPanel, "completionTaskTitleText", completionTaskTitle);
            SetRef(soPanel, "completionMessageText", completionMessage);
            SetRef(soPanel, "returnHomeButton", returnHomeButton);
            SetRef(soPanel, "generateReportButton", reportButton);
            soPanel.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, ActiveTaskPanelPath);
            Object.DestroyImmediate(root);
            return ActiveTaskPanelPath;
        }

        private static GameObject CreateTitleBar(
            Transform parent,
            NorscopeTheme theme,
            Sprite rounded,
            TMP_FontAsset font,
            out TMP_Text titleText,
            out GameObject grabRegion,
            out GameObject closeButton)
        {
            GameObject titleBar = new GameObject("TitleBar", typeof(RectTransform));
            titleBar.transform.SetParent(parent, false);
            titleBar.AddComponent<LayoutElement>().preferredHeight = 56f;
            Image background = titleBar.AddComponent<Image>();
            background.sprite = rounded;
            background.type = Image.Type.Sliced;
            background.color = theme != null ? theme.elevatedPanelBackground : Color.gray;

            HorizontalLayoutGroup layout = titleBar.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(16, 10, 8, 8);
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = true;

            titleText = CreateText(titleBar.transform, "TaskTitle", "Tasks", font, 22f,
                theme != null ? theme.primaryText : Color.white, 40f);
            titleText.gameObject.GetComponent<LayoutElement>().flexibleWidth = 1f;

            grabRegion = new GameObject("GrabRegion", typeof(RectTransform));
            grabRegion.transform.SetParent(titleBar.transform, false);
            LayoutElement grabLayout = grabRegion.AddComponent<LayoutElement>();
            grabLayout.preferredWidth = 90f;
            grabLayout.preferredHeight = 10f;
            Image grabImage = grabRegion.AddComponent<Image>();
            grabImage.color = theme != null
                ? new Color(theme.border.r, theme.border.g, theme.border.b, 0.75f)
                : Color.gray;
            grabImage.raycastTarget = true;

            Button close = CreateButton(titleBar.transform, "CloseButton", "×", 40f, theme, rounded, font, false);
            closeButton = close.gameObject;
            return titleBar;
        }

        private static GameObject CreateCompletionView(
            Transform parent,
            NorscopeTheme theme,
            Sprite rounded,
            TMP_FontAsset font,
            out TMP_Text taskTitle,
            out TMP_Text message,
            out Button returnHome,
            out Button generateReport)
        {
            GameObject view = new GameObject("CompletionView", typeof(RectTransform));
            view.transform.SetParent(parent, false);
            LayoutElement viewLayout = view.AddComponent<LayoutElement>();
            viewLayout.flexibleHeight = 1f;
            VerticalLayoutGroup layout = view.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(40, 40, 50, 36);
            layout.spacing = 18f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            CreateText(view.transform, "CompletionHeading", "Task Complete", font, 30f,
                theme != null ? theme.success : Color.green, 44f);
            taskTitle = CreateText(view.transform, "CompletionTaskTitle", "Task title", font, 22f,
                theme != null ? theme.primaryText : Color.white, 34f);
            message = CreateText(view.transform, "CompletionMessage",
                "All task steps have been completed.", font, 16f,
                theme != null ? theme.secondaryText : Color.gray, 60f);

            GameObject actions = new GameObject("CompletionActions", typeof(RectTransform));
            actions.transform.SetParent(view.transform, false);
            actions.AddComponent<LayoutElement>().preferredHeight = 48f;
            HorizontalLayoutGroup actionsLayout = actions.AddComponent<HorizontalLayoutGroup>();
            actionsLayout.spacing = 14f;
            actionsLayout.childAlignment = TextAnchor.MiddleCenter;
            actionsLayout.childControlWidth = false;
            actionsLayout.childControlHeight = true;
            actionsLayout.childForceExpandHeight = true;

            returnHome = CreateButton(actions.transform, "ReturnHomeButton", "Return Home", 150f, theme, rounded, font, true);
            generateReport = CreateButton(actions.transform, "GenerateReportButton", "Generate Report", 170f, theme, rounded, font, false);
            return view;
        }

        private static GameObject CreateSection(
            Transform parent,
            string name,
            NorscopeTheme theme,
            Sprite rounded,
            float preferredHeight)
        {
            GameObject section = new GameObject(name, typeof(RectTransform));
            section.transform.SetParent(parent, false);
            section.AddComponent<LayoutElement>().preferredHeight = preferredHeight;
            Image image = section.AddComponent<Image>();
            image.sprite = rounded;
            image.type = Image.Type.Sliced;
            image.color = theme != null ? theme.elevatedPanelBackground : Color.black;
            image.raycastTarget = false;
            VerticalLayoutGroup layout = section.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 8, 8);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            return section;
        }

        private static Image CreateReferenceImage(Transform parent)
        {
            GameObject imageGo = new GameObject("ReferenceImage", typeof(RectTransform));
            imageGo.transform.SetParent(parent, false);
            Image image = imageGo.AddComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text CreateText(
            Transform parent,
            string name,
            string value,
            TMP_FontAsset font,
            float fontSize,
            Color color,
            float preferredHeight)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            TMP_Text text = go.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.font = font;
            text.fontSize = fontSize;
            text.color = color;
            text.raycastTarget = false;
            text.enableWordWrapping = true;
            go.AddComponent<LayoutElement>().preferredHeight = preferredHeight;
            return text;
        }

        private static Button CreateButton(
            Transform parent,
            string name,
            string label,
            float width,
            NorscopeTheme theme,
            Sprite rounded,
            TMP_FontAsset font,
            bool primary)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            LayoutElement layout = go.AddComponent<LayoutElement>();
            layout.preferredWidth = width;
            layout.preferredHeight = 40f;

            Image image = go.AddComponent<Image>();
            image.sprite = rounded;
            image.type = Image.Type.Sliced;
            image.color = primary && theme != null
                ? theme.primaryAccent
                : (theme != null ? theme.elevatedPanelBackground : Color.gray);
            image.raycastTarget = true;

            Button button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            button.interactable = true;
            go.AddComponent<UiButtonPressScale>();

            GameObject labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            StretchFull(labelGo.GetComponent<RectTransform>());
            TMP_Text text = labelGo.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.font = font;
            text.fontSize = 14f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = primary ? Color.black : (theme != null ? theme.primaryText : Color.white);
            text.raycastTarget = false;
            return button;
        }

        private static void PlaceResizeHandle(Transform parent, NorscopeTheme theme)
        {
            GameObject handle = new GameObject("BottomRight", typeof(RectTransform));
            handle.transform.SetParent(parent, false);
            RectTransform rt = handle.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.sizeDelta = new Vector2(20f, 20f);
            rt.anchoredPosition = new Vector2(-8f, 8f);
            Image image = handle.AddComponent<Image>();
            image.color = theme != null
                ? new Color(theme.primaryAccent.r, theme.primaryAccent.g, theme.primaryAccent.b, 0.7f)
                : Color.cyan;
            image.raycastTarget = true;
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void SetRef(SerializedObject serializedObject, string propertyName, Object value)
        {
            serializedObject.FindProperty(propertyName).objectReferenceValue = value;
        }

        private static void BackupPrefabIfExists(string assetPath)
        {
            if (!File.Exists(assetPath)) return;
            Directory.CreateDirectory(BackupDir);
            AssetDatabase.Refresh();
            string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string backupPath = AssetDatabase.GenerateUniqueAssetPath(
                BackupDir + "/ActiveTaskPanel_" + timestamp + ".prefab");
            AssetDatabase.CopyAsset(assetPath, backupPath);
        }

        public static bool Validate(out string report)
        {
            var sb = new StringBuilder();
            int fails = 0;
            void Check(bool ok, string label)
            {
                sb.AppendLine((ok ? "PASS  " : "FAIL  ") + label);
                if (!ok) fails++;
            }

            Check(File.Exists(ActiveTaskPanelPath), "ActiveTaskPanel.prefab exists");
            Check(File.Exists(SessionSourcePath) && File.Exists(LauncherSourcePath) && File.Exists(PanelSourcePath),
                "Active Task runtime scripts exist");

            bool panelExists = File.Exists(ActiveTaskPanelPath);
            string panelBefore = ComputeHash(ActiveTaskPanelPath);
            string libraryBefore = ComputeHash(TaskLibraryPanelPath);
            if (panelExists)
            {
                CreateIfMissing();
            }
            Check(panelExists && panelBefore == ComputeHash(ActiveTaskPanelPath),
                "Normal Create does not overwrite ActiveTaskPanel.prefab");
            Check(libraryBefore == ComputeHash(TaskLibraryPanelPath),
                "TaskLibraryPanel.prefab remains untouched");

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ActiveTaskPanelPath);
            ActiveTaskPanel panel = prefab != null ? prefab.GetComponent<ActiveTaskPanel>() : null;
            Check(prefab != null && prefab.GetComponent<NorscopePanelRoot>() != null,
                "Panel has NorscopePanelRoot");
            Check(prefab != null && prefab.GetComponent<SpatialWindowBindings>() != null,
                "Panel has SpatialWindowBindings");
            Check(panel != null, "Panel has ActiveTaskPanel component");
            Check(prefab != null && prefab.transform.Find("TitleBar") != null, "Panel has TitleBar");
            Check(prefab != null && prefab.transform.Find("TitleBar/CloseButton") != null, "Panel has CloseButton");
            Check(prefab != null && prefab.transform.Find("TitleBar/GrabRegion") != null, "Panel has GrabRegion");
            Check(prefab != null &&
                  prefab.GetComponentsInChildren<LayoutGroup>(true).Length == 0,
                "Active Task visual elements use manually editable RectTransforms");

            if (panel != null)
            {
                SerializedObject so = new SerializedObject(panel);
                string[] refs = {
                    "taskView", "completionView", "titleText", "stepCounterText", "progressFill",
                    "stepTitleText", "instructionText", "imageSection", "referenceImage",
                    "warningSection", "warningText", "verificationSection", "verificationText",
                    "previousButton", "nextButton", "finishButton", "completionTaskTitleText",
                    "completionMessageText", "returnHomeButton", "generateReportButton"
                };
                Check(refs.All(name => so.FindProperty(name).objectReferenceValue != null),
                    "All authored prefab references are assigned");
            }

            Check(TaskSelectionPassesDefinition(), "Selected TaskDefinition is passed from Task Library");
            Check(SessionNavigationWorks(), "Session preserves step order and Previous/Next/Finish rules");

            PanelRuleResults panelRules = ValidatePanelRules(prefab);
            Check(panelRules.previousDisabledOnFirst, "Previous is disabled on the first step");
            Check(panelRules.nextAndFinishVisibility, "Next advances correctly and Finish appears only on the last step");
            Check(panelRules.optionalImageVisibility, "Optional image section hides/shows correctly");
            Check(panelRules.optionalWarningVisibility, "Optional warning section hides/shows correctly");
            Check(panelRules.optionalVerificationVisibility, "Optional verification section hides/shows correctly");
            Check(panelRules.completionState, "Completion state appears after Finish");

            Check(NoRuntimeHierarchyConstruction(), "No runtime visual hierarchy construction exists");
            Check(NoLayoutResetMethods(), "No Update/OnValidate layout reset exists");
            Check(WireDoesNotSavePrefab(), "Normal Wire contains no prefab save/rebuild operation");
            Check(OnlyTargetPrefabCanBeSaved(), "Phase 7 tooling cannot overwrite shared or Task Library prefabs");

            EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);
            PanelRegistry registry = Object.FindFirstObjectByType<PanelRegistry>();
            ActiveTaskSession session = Object.FindFirstObjectByType<ActiveTaskSession>();
            ActiveTaskLauncher launcher = Object.FindFirstObjectByType<ActiveTaskLauncher>();
            Check(registry != null, "PanelRegistry present");
            if (registry != null)
            {
                registry.RebuildLookup();
                bool registered = registry.TryGetEntry(PanelType.TaskExecution, out PanelRegistryEntry entry);
                Check(registered && entry.prefab != null, "TaskExecution panel is registered");
                Check(registered && entry.behaviour == PanelBehaviourType.WorldLocked,
                    "TaskExecution panel is WorldLocked");
                Check(registered && entry.windowId == SpatialWindowId.TaskExecution,
                    "TaskExecution maps to SpatialWindowId.TaskExecution");
            }

            Check(session != null && launcher != null, "ActiveTaskSession and ActiveTaskLauncher are wired");
            report = sb.ToString();
            return fails == 0;
        }

        private sealed class PanelRuleResults
        {
            public bool previousDisabledOnFirst;
            public bool nextAndFinishVisibility = true;
            public bool optionalImageVisibility = true;
            public bool optionalWarningVisibility = true;
            public bool optionalVerificationVisibility = true;
            public bool completionState;
        }

        private static PanelRuleResults ValidatePanelRules(GameObject prefab)
        {
            var results = new PanelRuleResults();
            if (prefab == null)
            {
                results.nextAndFinishVisibility = false;
                results.optionalImageVisibility = false;
                results.optionalWarningVisibility = false;
                results.optionalVerificationVisibility = false;
                return results;
            }

            GameObject instance = Object.Instantiate(prefab);
            GameObject sessionGo = new GameObject("Phase7PanelRuleSession");
            try
            {
                ActiveTaskPanel panel = instance.GetComponent<ActiveTaskPanel>();
                ActiveTaskSession session = sessionGo.AddComponent<ActiveTaskSession>();
                if (panel == null)
                {
                    results.nextAndFinishVisibility = false;
                    return results;
                }

                SerializedObject so = new SerializedObject(panel);
                GameObject taskView = so.FindProperty("taskView").objectReferenceValue as GameObject;
                GameObject completionView = so.FindProperty("completionView").objectReferenceValue as GameObject;
                GameObject imageSection = so.FindProperty("imageSection").objectReferenceValue as GameObject;
                GameObject warningSection = so.FindProperty("warningSection").objectReferenceValue as GameObject;
                GameObject verificationSection =
                    so.FindProperty("verificationSection").objectReferenceValue as GameObject;
                Button previous = so.FindProperty("previousButton").objectReferenceValue as Button;
                Button next = so.FindProperty("nextButton").objectReferenceValue as Button;
                Button finish = so.FindProperty("finishButton").objectReferenceValue as Button;

                string[] taskGuids = AssetDatabase.FindAssets("t:TaskDefinition");
                var tasks = taskGuids.Select(AssetDatabase.GUIDToAssetPath)
                    .Select(AssetDatabase.LoadAssetAtPath<TaskDefinition>)
                    .Where(task => task != null && task.Steps.Count > 0)
                    .ToList();
                if (tasks.Count == 0)
                {
                    results.nextAndFinishVisibility = false;
                    results.optionalImageVisibility = false;
                    results.optionalWarningVisibility = false;
                    results.optionalVerificationVisibility = false;
                    return results;
                }

                bool firstChecked = false;
                foreach (TaskDefinition task in tasks)
                {
                    session.Begin(task);
                    for (int i = 0; i < task.Steps.Count; i++)
                    {
                        panel.Bind(session, null, null);
                        TaskStep step = task.Steps[i];

                        if (!firstChecked)
                        {
                            results.previousDisabledOnFirst = previous != null && !previous.interactable;
                            firstChecked = true;
                        }

                        bool isLast = i == task.Steps.Count - 1;
                        results.nextAndFinishVisibility &=
                            next != null && finish != null &&
                            next.gameObject.activeSelf == !isLast &&
                            finish.gameObject.activeSelf == isLast;
                        results.optionalImageVisibility &=
                            imageSection != null && imageSection.activeSelf == (step.image != null);
                        results.optionalWarningVisibility &=
                            warningSection != null &&
                            warningSection.activeSelf == !string.IsNullOrWhiteSpace(step.warning);
                        results.optionalVerificationVisibility &=
                            verificationSection != null &&
                            verificationSection.activeSelf == !string.IsNullOrWhiteSpace(step.verification);

                        if (!isLast) session.Next();
                    }
                }

                TaskDefinition completionTask = tasks[0];
                session.Begin(completionTask);
                while (session.CanGoNext) session.Next();
                session.Finish();
                panel.Bind(session, null, null);
                results.completionState =
                    taskView != null && completionView != null &&
                    !taskView.activeSelf && completionView.activeSelf;
                return results;
            }
            finally
            {
                Object.DestroyImmediate(instance);
                Object.DestroyImmediate(sessionGo);
            }
        }

        private static bool TaskSelectionPassesDefinition()
        {
            EventInfo eventInfo = typeof(TaskCardView).GetEvent("TaskOpened",
                BindingFlags.Public | BindingFlags.Static);
            if (eventInfo == null) return false;
            System.Type handler = eventInfo.EventHandlerType;
            return handler != null && handler.IsGenericType &&
                   handler.GetGenericArguments().Length == 1 &&
                   handler.GetGenericArguments()[0] == typeof(TaskDefinition);
        }

        private static bool SessionNavigationWorks()
        {
            string[] guids = AssetDatabase.FindAssets("t:TaskDefinition");
            TaskDefinition task = guids.Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<TaskDefinition>)
                .FirstOrDefault(candidate => candidate != null && candidate.Steps.Count >= 2);
            if (task == null) return false;

            GameObject go = new GameObject("Phase7ValidationSession");
            try
            {
                ActiveTaskSession session = go.AddComponent<ActiveTaskSession>();
                if (!session.Begin(task)) return false;
                if (session.CurrentStep != task.Steps[0] || session.CanGoPrevious) return false;
                if (!session.Next() || session.CurrentStep != task.Steps[1]) return false;
                if (!session.Previous() || session.CurrentStep != task.Steps[0]) return false;
                while (session.CanGoNext) session.Next();
                if (!session.IsOnLastStep || !session.Finish() || !session.IsComplete) return false;
                return true;
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        private static bool NoRuntimeHierarchyConstruction()
        {
            string source = File.ReadAllText(PanelSourcePath) +
                            File.ReadAllText(SessionSourcePath) +
                            File.ReadAllText(LauncherSourcePath);
            return !source.Contains("new GameObject(") &&
                   !source.Contains("Instantiate(") &&
                   !source.Contains("AddComponent<RectTransform>") &&
                   !source.Contains("AddComponent<Layout");
        }

        private static bool NoLayoutResetMethods()
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public |
                                       BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            return typeof(ActiveTaskPanel).GetMethod("Update", flags) == null &&
                   typeof(ActiveTaskPanel).GetMethod("OnValidate", flags) == null &&
                   typeof(ActiveTaskPanel).GetMethod("Start", flags) == null &&
                   typeof(ActiveTaskPanel).GetMethod("OnEnable", flags) == null;
        }

        private static bool WireDoesNotSavePrefab()
        {
            string source = File.ReadAllText(
                "Assets/Norscope/Editor/Phase7ActiveTaskConfigurator.cs");
            int start = source.IndexOf("public static void WireScene()", System.StringComparison.Ordinal);
            int end = source.IndexOf("[MenuItem(\"Norscope/HoloLens/Phase 7/3)",
                System.StringComparison.Ordinal);
            if (start < 0 || end <= start) return false;
            string wireSection = source.Substring(start, end - start);
            return !wireSection.Contains("SaveAsPrefabAsset") &&
                   !wireSection.Contains("BuildActiveTaskPanelPrefab");
        }

        private static bool OnlyTargetPrefabCanBeSaved()
        {
            string source = File.ReadAllText(
                "Assets/Norscope/Editor/Phase7ActiveTaskConfigurator.cs");
            System.Text.RegularExpressions.MatchCollection saveCalls =
                System.Text.RegularExpressions.Regex.Matches(
                    source,
                    @"PrefabUtility\s*\.\s*SaveAsPrefabAsset\s*\(\s*root\s*,\s*(\w+)\s*\)");

            return saveCalls.Count > 0 &&
                   saveCalls.Cast<System.Text.RegularExpressions.Match>()
                       .All(match => match.Groups[1].Value == "ActiveTaskPanelPath");
        }

        private static string ComputeHash(string assetPath)
        {
            if (!File.Exists(assetPath)) return "MISSING";
            byte[] bytes = File.ReadAllBytes(assetPath);
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                return System.Convert.ToBase64String(sha.ComputeHash(bytes));
            }
        }
    }
}
