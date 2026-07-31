using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Norscope.DesignSystem;
using Norscope.Navigation;
using Norscope.TaskExecution;
using Norscope.UI;
using Norscope.Windows;

namespace Norscope.Editor
{
    /// <summary>
    /// Creates the separate Task Completion prefab once and wires one registry entry.
    /// Existing prefabs and registry entries are never rebuilt.
    /// </summary>
    public static class Phase7TaskCompletionConfigurator
    {
        private const string PrefabPath =
            "Assets/Norscope/UI/Panels/TaskCompletionPanel.prefab";
        private const string BootstrapPath =
            "Assets/Norscope/Scenes/Bootstrap.unity";
        private const string ThemePath =
            "Assets/Norscope/UI/DesignSystem/Themes/NorscopeTheme.asset";
        private const string WorldConfigPath =
            "Assets/Norscope/UI/DesignSystem/NorscopeWorldSpaceUiConfig.asset";
        private const string RoundedSpritePath =
            "Assets/Norscope/UI/DesignSystem/Sprites/UIRounded_8.png";
        private const string D1LayerPath =
            "Assets/Norscope/DesignSystem/Prefabs/NorscopeInteractionVisualLayer.prefab";
        private const string D1ProfilePath =
            "Assets/Norscope/DesignSystem/Profiles/NorscopeDesignProfile.asset";

        [MenuItem("Norscope/HoloLens/Phase 7/8) Create Task Completion Panel If Missing")]
        public static void CreateIfMissingMenu()
        {
            bool created = CreateIfMissing();
            Debug.Log(created
                ? "[Norscope Phase7] Created " + PrefabPath
                : "[Norscope Phase7] TaskCompletionPanel already exists and was not overwritten.");
        }

        public static bool CreateIfMissing()
        {
            if (File.Exists(PrefabPath)) return false;
            BuildPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return true;
        }

        [MenuItem("Norscope/HoloLens/Phase 7/9) Wire Task Completion")]
        public static void WireMenu()
        {
            CreateIfMissing();
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) throw new InvalidOperationException("Completion prefab is missing.");

            Scene scene = EditorSceneManager.OpenScene(BootstrapPath, OpenSceneMode.Single);
            PanelRegistry registry =
                UnityEngine.Object.FindFirstObjectByType<PanelRegistry>();
            if (registry == null) throw new InvalidOperationException("PanelRegistry is missing.");

            SerializedObject serializedRegistry = new SerializedObject(registry);
            SerializedProperty entries = serializedRegistry.FindProperty("entries");
            SerializedProperty target = null;
            for (int i = 0; i < entries.arraySize; i++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                if ((PanelType)entry.FindPropertyRelative("panelType").enumValueIndex ==
                    PanelType.TaskCompletion)
                {
                    target = entry;
                    break;
                }
            }

            bool changed = false;
            if (target == null)
            {
                int index = entries.arraySize;
                entries.InsertArrayElementAtIndex(index);
                target = entries.GetArrayElementAtIndex(index);
                target.FindPropertyRelative("panelType").enumValueIndex =
                    (int)PanelType.TaskCompletion;
                target.FindPropertyRelative("windowId").enumValueIndex =
                    (int)SpatialWindowId.TaskCompletion;
                target.FindPropertyRelative("behaviour").enumValueIndex =
                    (int)PanelBehaviourType.WorldLocked;
                target.FindPropertyRelative("prefab").objectReferenceValue = prefab;
                changed = true;
            }
            else
            {
                SerializedProperty registeredPrefab =
                    target.FindPropertyRelative("prefab");
                if (registeredPrefab.objectReferenceValue != null &&
                    registeredPrefab.objectReferenceValue != prefab)
                {
                    throw new InvalidOperationException(
                        "TaskCompletion is already registered to a different prefab.");
                }

                if ((SpatialWindowId)target.FindPropertyRelative("windowId").enumValueIndex !=
                    SpatialWindowId.TaskCompletion)
                {
                    throw new InvalidOperationException(
                        "TaskCompletion has a conflicting SpatialWindowId.");
                }

                if (registeredPrefab.objectReferenceValue == null)
                {
                    registeredPrefab.objectReferenceValue = prefab;
                    changed = true;
                }
            }

            if (changed)
            {
                serializedRegistry.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(registry);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            registry.RebuildLookup();
            Debug.Log("[Norscope Phase7] Task Completion registry wiring is valid.");
        }

        [MenuItem("Norscope/HoloLens/Phase 7/10) Validate Task Completion")]
        public static void ValidateMenu()
        {
            var report = new System.Text.StringBuilder();
            bool ok = true;
            void Check(bool condition, string label)
            {
                report.AppendLine((condition ? "PASS  " : "FAIL  ") + label);
                if (!condition) ok = false;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Check(prefab != null, "TaskCompletionPanel.prefab exists");
            if (prefab != null)
            {
                Check(prefab.GetComponent<NorscopePanelRoot>() != null,
                    "Panel has NorscopePanelRoot");
                Check(prefab.GetComponent<SpatialWindowBindings>() != null,
                    "Panel has SpatialWindowBindings");
                Check(prefab.GetComponent<TaskCompletionPanel>() != null,
                    "Panel has TaskCompletionPanel component");
                Check(FindDeep(prefab.transform, "TitleBar") != null,
                    "TitleBar is authored");
                Check(FindDeep(prefab.transform, "CloseButton") != null,
                    "Close button is authored");
                Check(FindDeep(prefab.transform, "GrabRegion") != null,
                    "Grab region is authored");
                Check(FindDeep(prefab.transform, "BottomRight") != null,
                    "Resize handle is authored");
                Check(FindDeep(prefab.transform, "ReturnHomeButton") != null &&
                      FindDeep(prefab.transform, "GenerateReportButton") != null,
                    "Completion actions are authored");
                RectTransform rect = prefab.GetComponent<RectTransform>();
                Check(rect != null && rect.sizeDelta.x < 700f && rect.sizeDelta.y < 540f,
                    "Completion panel is smaller than Active Task");
            }

            EditorSceneManager.OpenScene(BootstrapPath, OpenSceneMode.Single);
            PanelRegistry registry =
                UnityEngine.Object.FindFirstObjectByType<PanelRegistry>();
            if (registry != null) registry.RebuildLookup();
            PanelRegistryEntry entry = null;
            bool registered = registry != null &&
                              registry.TryGetEntry(
                                  PanelType.TaskCompletion,
                                  out entry);
            Check(registered && entry.prefab == prefab,
                "TaskCompletion panel is registered");
            Check(registered &&
                  entry.windowId == SpatialWindowId.TaskCompletion &&
                  entry.behaviour == PanelBehaviourType.WorldLocked,
                "TaskCompletion is world-locked with a unique window ID");

            string runtime = File.ReadAllText(
                "Assets/Norscope/Runtime/TaskExecution/TaskCompletionPanel.cs");
            Check(!runtime.Contains("new GameObject") &&
                  !runtime.Contains("Instantiate(") &&
                  !runtime.Contains("RectTransform") &&
                  !runtime.Contains("LayoutGroup"),
                "Completion runtime does not construct or control visual layout");

            Debug.Log((ok
                ? "[Norscope Phase7] Validate Task Completion PASSED\n"
                : "[Norscope Phase7] Validate Task Completion FAILED\n") + report);
            EditorUtility.DisplayDialog(
                "Validate Task Completion",
                (ok ? "PASSED\n\n" : "FAILED\n\n") + report,
                "OK");
        }

        private static void BuildPrefab()
        {
            NorscopeTheme theme =
                AssetDatabase.LoadAssetAtPath<NorscopeTheme>(ThemePath);
            NorscopeWorldSpaceUiConfig world =
                AssetDatabase.LoadAssetAtPath<NorscopeWorldSpaceUiConfig>(WorldConfigPath);
            Sprite rounded =
                AssetDatabase.LoadAssetAtPath<Sprite>(RoundedSpritePath);
            TMP_FontAsset font = theme != null && theme.fonts != null
                ? theme.fonts.bodyFont
                : null;

            const float width = 480f;
            const float height = 300f;
            GameObject root = CreateUiObject("TaskCompletionPanel", null);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(width, height);
            Image background = root.AddComponent<Image>();
            background.sprite = rounded;
            background.type = rounded != null ? Image.Type.Sliced : Image.Type.Simple;
            background.color = theme != null
                ? theme.panelBackground
                : new Color(0.075f, 0.102f, 0.141f, 0.96f);

            GameObject titleBar = CreateUiObject("TitleBar", root.transform);
            SetTopLeft(titleBar.GetComponent<RectTransform>(), Vector2.zero,
                new Vector2(width, 56f));
            Image titleBackground = titleBar.AddComponent<Image>();
            titleBackground.sprite = rounded;
            titleBackground.type = rounded != null ? Image.Type.Sliced : Image.Type.Simple;
            titleBackground.color = theme != null
                ? theme.elevatedPanelBackground
                : new Color(0.1f, 0.14f, 0.19f, 1f);
            titleBackground.raycastTarget = false;

            GameObject grabRegion = CreateUiObject("GrabRegion", titleBar.transform);
            SetTopLeft(grabRegion.GetComponent<RectTransform>(), Vector2.zero,
                new Vector2(width - 56f, 56f));
            Image grabImage = grabRegion.AddComponent<Image>();
            grabImage.color = Color.clear;
            grabImage.raycastTarget = false;

            TMP_Text title = CreateText(titleBar.transform, "Title", "Task Complete",
                new Vector2(20f, -10f), new Vector2(340f, 38f), 22f,
                theme != null ? theme.primaryText : Color.white, font);
            Button close = CreateButton(titleBar.transform, "CloseButton", "×",
                new Vector2(424f, -8f), new Vector2(40f, 40f),
                theme != null ? theme.elevatedPanelBackground : Color.gray,
                theme != null ? theme.primaryText : Color.white, rounded, font,
                NorscopeVisualRole.CloseButton);

            TMP_Text heading = CreateText(root.transform, "CompletionHeading",
                "Task completed", new Vector2(28f, -82f), new Vector2(424f, 40f),
                28f, theme != null ? theme.success : Color.green, font);
            heading.fontStyle = FontStyles.Bold;
            TMP_Text taskTitle = CreateText(root.transform, "CompletionTaskTitle",
                "Task title", new Vector2(28f, -126f), new Vector2(424f, 34f),
                20f, theme != null ? theme.primaryText : Color.white, font);
            TMP_Text message = CreateText(root.transform, "CompletionMessage",
                "All task steps have been completed.",
                new Vector2(28f, -165f), new Vector2(424f, 42f), 14f,
                theme != null ? theme.secondaryText : Color.gray, font);

            Button home = CreateButton(root.transform, "ReturnHomeButton",
                "Return Home", new Vector2(28f, -232f), new Vector2(190f, 44f),
                theme != null ? theme.primaryAccent : Color.cyan,
                Color.white, rounded, font, NorscopeVisualRole.PrimaryButton);
            Button report = CreateButton(root.transform, "GenerateReportButton",
                "Generate Report", new Vector2(262f, -232f), new Vector2(190f, 44f),
                theme != null ? theme.elevatedPanelBackground : Color.gray,
                theme != null ? theme.primaryText : Color.white,
                rounded, font, NorscopeVisualRole.SecondaryButton);

            GameObject resizeHandles = CreateUiObject("ResizeHandles", root.transform);
            Stretch(resizeHandles.GetComponent<RectTransform>());
            GameObject corner = CreateUiObject("BottomRight", resizeHandles.transform);
            RectTransform cornerRect = corner.GetComponent<RectTransform>();
            cornerRect.anchorMin = cornerRect.anchorMax = Vector2.one;
            cornerRect.pivot = Vector2.one;
            cornerRect.anchoredPosition = new Vector2(-6f, 6f);
            cornerRect.sizeDelta = new Vector2(30f, 30f);
            Image cornerImage = corner.AddComponent<Image>();
            cornerImage.sprite = rounded;
            cornerImage.type = rounded != null ? Image.Type.Sliced : Image.Type.Simple;
            cornerImage.color = theme != null ? theme.primaryAccent : Color.cyan;
            cornerImage.raycastTarget = false;

            NorscopePanelRoot panelRoot = root.AddComponent<NorscopePanelRoot>();
            SerializedObject panelRootObject = new SerializedObject(panelRoot);
            Assign(panelRootObject, "theme", theme);
            Assign(panelRootObject, "worldSpaceConfig", world);
            Assign(panelRootObject, "root", rootRect);
            Assign(panelRootObject, "background", background);
            Assign(panelRootObject, "title", title);
            panelRootObject.FindProperty("sizeMeters").vector2Value =
                new Vector2(0.48f, 0.30f);
            panelRootObject.FindProperty("sizePixels").vector2Value =
                new Vector2(width, height);
            panelRootObject.FindProperty("useMetersAsSource").boolValue = true;
            panelRootObject.ApplyModifiedPropertiesWithoutUndo();
            rootRect.sizeDelta = new Vector2(width, height);

            SpatialWindowBindings bindings = root.AddComponent<SpatialWindowBindings>();
            SerializedObject bindingsObject = new SerializedObject(bindings);
            Assign(bindingsObject, "grabRegion", grabRegion.transform);
            Assign(bindingsObject, "resizeHandle", cornerRect);
            Assign(bindingsObject, "closeButtonRoot", close.transform);
            bindingsObject.ApplyModifiedPropertiesWithoutUndo();

            TaskCompletionPanel completion = root.AddComponent<TaskCompletionPanel>();
            SerializedObject completionObject = new SerializedObject(completion);
            Assign(completionObject, "taskTitleText", taskTitle);
            Assign(completionObject, "messageText", message);
            Assign(completionObject, "returnHomeButton", home);
            Assign(completionObject, "generateReportButton", report);
            completionObject.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static Button CreateButton(
            Transform parent, string name, string label, Vector2 position,
            Vector2 size, Color background, Color foreground, Sprite rounded,
            TMP_FontAsset font, NorscopeVisualRole role)
        {
            GameObject buttonObject = CreateUiObject(name, parent);
            SetTopLeft(buttonObject.GetComponent<RectTransform>(), position, size);
            Image image = buttonObject.AddComponent<Image>();
            image.sprite = rounded;
            image.type = rounded != null ? Image.Type.Sliced : Image.Type.Simple;
            image.color = background;
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            buttonObject.AddComponent<UiButtonPressScale>();
            TMP_Text text = CreateText(buttonObject.transform, "Label", label,
                Vector2.zero, size, 15f, foreground, font);
            Stretch(text.rectTransform);
            text.alignment = TextAlignmentOptions.Center;
            EnsureD1(buttonObject, button, role);
            return button;
        }

        private static TMP_Text CreateText(
            Transform parent, string name, string value, Vector2 position,
            Vector2 size, float fontSize, Color color, TMP_FontAsset font)
        {
            GameObject textObject = CreateUiObject(name, parent);
            SetTopLeft(textObject.GetComponent<RectTransform>(), position, size);
            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.font = font;
            text.fontSize = fontSize;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        private static void EnsureD1(
            GameObject buttonObject, Button button, NorscopeVisualRole role)
        {
            GameObject layerPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(D1LayerPath);
            NorscopeDesignProfile profile =
                AssetDatabase.LoadAssetAtPath<NorscopeDesignProfile>(D1ProfilePath);
            if (layerPrefab == null || profile == null) return;
            GameObject layer = PrefabUtility.InstantiatePrefab(
                layerPrefab, buttonObject.transform) as GameObject;
            if (layer == null) return;
            layer.name = "D1_VisualLayer";
            Stretch(layer.GetComponent<RectTransform>());
            Graphic[] graphics = layer.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++) graphics[i].raycastTarget = false;
            NorscopeInteractionVisual visual =
                buttonObject.AddComponent<NorscopeInteractionVisual>();
            visual.SetReferences(
                role, button,
                layer.GetComponent<NorscopeHighlightController>(), profile);
        }

        private static GameObject CreateUiObject(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            if (parent != null)
            {
                gameObject.layer = parent.gameObject.layer;
                gameObject.transform.SetParent(parent, false);
            }
            return gameObject;
        }

        private static void SetTopLeft(
            RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Assign(
            SerializedObject target, string propertyName, UnityEngine.Object value)
        {
            target.FindProperty(propertyName).objectReferenceValue = value;
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
    }
}
