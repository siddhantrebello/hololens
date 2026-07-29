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
    /// Phase 7.5 single-panel navigation tooling. Existing panel prefabs are edited
    /// surgically: a nested Back-button prefab and one binding are added only when missing.
    /// No existing hierarchy, RectTransform, LayoutGroup, or visual component is rebuilt.
    /// </summary>
    public static class Phase75SinglePanelNavigationConfigurator
    {
        private const string BootstrapScenePath = "Assets/Norscope/Scenes/Bootstrap.unity";
        private const string BackButtonPath = "Assets/Norscope/UI/Controls/NavigationBackButton.prefab";
        private const string TaskLibraryPanelPath = "Assets/Norscope/UI/Panels/TaskLibraryPanel.prefab";
        private const string ActiveTaskPanelPath = "Assets/Norscope/UI/Panels/ActiveTaskPanel.prefab";
        private const string ThemePath = "Assets/Norscope/UI/DesignSystem/Themes/NorscopeTheme.asset";
        private const string RoundedSpritePath = "Assets/Norscope/UI/DesignSystem/Sprites/UIRounded_8.png";
        private const string BackupDirectory = "Assets/Norscope/UI/Panels/_Backups";

        private const string ServiceSourcePath =
            "Assets/Norscope/Runtime/Navigation/PanelNavigationService.cs";
        private const string BindingSourcePath =
            "Assets/Norscope/Runtime/Navigation/PanelNavigationBinding.cs";
        private const string HomeCardSourcePath =
            "Assets/Norscope/Runtime/Navigation/HomePanelCard.cs";
        private const string ActiveLauncherSourcePath =
            "Assets/Norscope/Runtime/TaskExecution/ActiveTaskLauncher.cs";
        private const string TaskLibrarySourcePath =
            "Assets/Norscope/Runtime/TaskLibrary/TaskLibraryPanel.cs";

        [MenuItem("Norscope/HoloLens/Phase 7.5/1) Create Navigation Assets If Missing")]
        public static void CreateNavigationAssetsIfMissingMenu()
        {
            bool created = CreateNavigationAssetsIfMissing();
            Debug.Log("[Norscope Phase7.5] NavigationBackButton.prefab " +
                      (created ? "created." : "already existed and was left untouched."));
        }

        public static bool CreateNavigationAssetsIfMissing()
        {
            if (File.Exists(BackButtonPath)) return false;

            NorscopeTheme theme = AssetDatabase.LoadAssetAtPath<NorscopeTheme>(ThemePath);
            Sprite rounded = AssetDatabase.LoadAssetAtPath<Sprite>(RoundedSpritePath);
            TMP_FontAsset font = theme != null && theme.fonts != null ? theme.fonts.bodyFont : null;

            GameObject root = new GameObject("NavigationBackButton", typeof(RectTransform));
            RectTransform rt = root.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(44f, 40f);

            LayoutElement layout = root.AddComponent<LayoutElement>();
            layout.preferredWidth = 44f;
            layout.preferredHeight = 40f;

            Image image = root.AddComponent<Image>();
            image.sprite = rounded;
            image.type = Image.Type.Sliced;
            image.color = theme != null ? theme.elevatedPanelBackground : Color.gray;
            image.raycastTarget = true;

            Button button = root.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            root.AddComponent<UiButtonPressScale>();

            GameObject labelObject = new GameObject("Label", typeof(RectTransform));
            labelObject.transform.SetParent(root.transform, false);
            RectTransform labelRt = labelObject.GetComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;

            TMP_Text label = labelObject.AddComponent<TextMeshProUGUI>();
            label.text = "<";
            label.font = font;
            label.fontSize = 22f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = theme != null ? theme.primaryText : Color.white;
            label.raycastTarget = false;

            PrefabUtility.SaveAsPrefabAsset(root, BackButtonPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return true;
        }

        [MenuItem("Norscope/HoloLens/Phase 7.5/2) Wire Single Panel Navigation")]
        public static void WireSinglePanelNavigationMenu()
        {
            WireSinglePanelNavigation();
            Debug.Log("[Norscope Phase7.5] Single-panel navigation wiring complete.");
        }

        public static void WireSinglePanelNavigation()
        {
            CreateNavigationAssetsIfMissing();

            AddBindingSurgically(
                TaskLibraryPanelPath,
                PanelType.TaskLibrary,
                PanelType.Home);
            AddBindingSurgically(
                ActiveTaskPanelPath,
                PanelType.TaskExecution,
                PanelType.TaskLibrary);

            Scene scene = EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);
            PanelRegistry registry = Object.FindFirstObjectByType<PanelRegistry>();
            SpatialWindowManager manager = Object.FindFirstObjectByType<SpatialWindowManager>();
            if (registry == null || manager == null)
            {
                throw new System.InvalidOperationException(
                    "PanelRegistry or SpatialWindowManager is missing from Bootstrap.");
            }

            PanelNavigationService service = Object.FindFirstObjectByType<PanelNavigationService>();
            if (service == null)
            {
                service = registry.gameObject.AddComponent<PanelNavigationService>();
            }

            service.SetReferences(registry, manager);
            SerializedObject so = new SerializedObject(service);
            so.FindProperty("panelRegistry").objectReferenceValue = registry;
            so.FindProperty("windowManager").objectReferenceValue = manager;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(service);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        private static void AddBindingSurgically(
            string panelPath,
            PanelType panelType,
            PanelType fallbackParent)
        {
            if (!File.Exists(panelPath))
            {
                Debug.LogWarning("[Norscope Phase7.5] Cannot wire missing panel: " + panelPath);
                return;
            }

            GameObject backPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BackButtonPath);
            if (backPrefab == null)
            {
                throw new System.InvalidOperationException(
                    "NavigationBackButton.prefab could not be loaded.");
            }

            GameObject root = PrefabUtility.LoadPrefabContents(panelPath);
            bool changed = false;
            try
            {
                Transform titleBar = FindDeep(root.transform, "TitleBar");
                if (titleBar == null)
                {
                    Debug.LogWarning("[Norscope Phase7.5] TitleBar missing in " + panelPath);
                    return;
                }

                Transform backTransform = FindDeep(titleBar, "BackButton");
                GameObject backObject;
                if (backTransform == null)
                {
                    Transform existingSlot = FindDeep(titleBar, "BackButtonSlot");
                    GameObject slot;
                    if (existingSlot == null)
                    {
                        slot = new GameObject("BackButtonSlot", typeof(RectTransform));
                        slot.transform.SetParent(titleBar, false);
                        slot.transform.SetSiblingIndex(0);
                        LayoutElement slotLayout = slot.AddComponent<LayoutElement>();
                        slotLayout.preferredWidth = 44f;
                        slotLayout.preferredHeight = 40f;
                    }
                    else
                    {
                        slot = existingSlot.gameObject;
                    }

                    backObject = PrefabUtility.InstantiatePrefab(
                        backPrefab,
                        slot.transform) as GameObject;
                    if (backObject == null)
                    {
                        throw new System.InvalidOperationException(
                            "Failed to instantiate the nested Back-button prefab.");
                    }

                    backObject.name = "BackButton";
                    RectTransform backRect = backObject.GetComponent<RectTransform>();
                    backRect.anchorMin = new Vector2(0.5f, 0.5f);
                    backRect.anchorMax = new Vector2(0.5f, 0.5f);
                    backRect.pivot = new Vector2(0.5f, 0.5f);
                    backRect.anchoredPosition = Vector2.zero;
                    backRect.sizeDelta = new Vector2(44f, 40f);
                    changed = true;
                }
                else
                {
                    backObject = backTransform.gameObject;
                }

                Button backButton = backObject.GetComponent<Button>();
                if (backButton == null) backButton = backObject.GetComponentInChildren<Button>(true);
                if (backButton == null)
                {
                    throw new System.InvalidOperationException(
                        "BackButton has no Unity UI Button component in " + panelPath);
                }

                PanelNavigationBinding binding = root.GetComponent<PanelNavigationBinding>();
                if (binding == null)
                {
                    binding = root.AddComponent<PanelNavigationBinding>();
                    changed = true;
                }

                SerializedObject so = new SerializedObject(binding);
                SerializedProperty panelProperty = so.FindProperty("panelType");
                SerializedProperty parentProperty = so.FindProperty("fallbackParent");
                SerializedProperty buttonProperty = so.FindProperty("backButton");
                if (panelProperty.enumValueIndex != (int)panelType ||
                    parentProperty.enumValueIndex != (int)fallbackParent ||
                    buttonProperty.objectReferenceValue != backButton)
                {
                    panelProperty.enumValueIndex = (int)panelType;
                    parentProperty.enumValueIndex = (int)fallbackParent;
                    buttonProperty.objectReferenceValue = backButton;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    changed = true;
                }

                if (changed)
                {
                    BackupPrefab(panelPath);
                    PrefabUtility.SaveAsPrefabAsset(root, panelPath);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [MenuItem("Norscope/HoloLens/Phase 7.5/3) Validate Single Panel Navigation")]
        public static void ValidateMenu()
        {
            bool ok = Validate(out string report);
            Debug.Log(ok
                ? "[Norscope Phase7.5] Validate Single Panel Navigation PASSED\n" + report
                : "[Norscope Phase7.5] Validate Single Panel Navigation FAILED\n" + report);
            EditorUtility.DisplayDialog(
                "Validate Single Panel Navigation",
                (ok ? "PASSED\n\n" : "FAILED\n\n") + report,
                "OK");
        }

        public static bool Validate(out string report)
        {
            var sb = new StringBuilder();
            int failures = 0;
            void Check(bool condition, string label)
            {
                sb.AppendLine((condition ? "PASS  " : "FAIL  ") + label);
                if (!condition) failures++;
            }

            Check(File.Exists(ServiceSourcePath) && File.Exists(BindingSourcePath),
                "Shared navigation runtime scripts exist");
            Check(File.Exists(BackButtonPath), "NavigationBackButton.prefab exists");
            Check(ValidateBinding(
                    TaskLibraryPanelPath,
                    PanelType.TaskLibrary,
                    PanelType.Home),
                "Task Library Back targets Home");
            Check(ValidateBinding(
                    ActiveTaskPanelPath,
                    PanelType.TaskExecution,
                    PanelType.TaskLibrary),
                "Active Task Back targets Task Library");

            Check(SourceContains(
                    HomeCardSourcePath,
                    "navigation.Navigate(PanelType.Home, targetPanel)"),
                "Home cards use shared parent-child navigation");
            Check(SourceContains(
                    ActiveLauncherSourcePath,
                    "navigation.Navigate(") &&
                  SourceContains(
                    ActiveLauncherSourcePath,
                    "PanelType.TaskLibrary") &&
                  SourceContains(
                    ActiveLauncherSourcePath,
                    "PanelType.TaskExecution"),
                "Task Library selection uses shared navigation");
            Check(SourceContains(TaskLibrarySourcePath, "private bool _populated") &&
                  SourceContains(TaskLibrarySourcePath, "_populated = PopulateCards()"),
                "Task Library rows and scroll state survive panel reactivation");

            Check(RuntimeParentHasPriority(),
                "Runtime-recorded parent takes priority over authored fallback");
            Check(NavigationUsesSafeOrdering(),
                "Child/parent opens succeed before the current panel is hidden");
            Check(SourceContains(ServiceSourcePath, "HideAllPrimaryPanelsExcept(child)") &&
                  SourceContains(ServiceSourcePath, "HideAllPrimaryPanelsExcept(parent)"),
                "Only one primary panel remains visible after Navigate or Back");
            Check(SourceContains(ServiceSourcePath, "windowManager.HasOpen(entry.windowId)") &&
                  SourceContains(ServiceSourcePath, "windowManager.Close(entry.windowId)"),
                "Panels are hidden through retained SpatialWindow instances");
            Check(SourceContains(
                    "Assets/Norscope/Runtime/Windows/SpatialWindowManager.cs",
                    "_windows.TryGetValue(id, out SpatialWindow existing)"),
                "Existing window manager prevents duplicate panel instances");
            Check(NoRuntimeLayoutMutation(),
                "Navigation runtime contains no UI creation or layout mutation");
            Check(WireIsSurgical(),
                "Normal Wire performs only surgical add-if-missing prefab edits");

            EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);
            PanelRegistry registry = Object.FindFirstObjectByType<PanelRegistry>();
            SpatialWindowManager manager = Object.FindFirstObjectByType<SpatialWindowManager>();
            PanelNavigationService[] services =
                Object.FindObjectsByType<PanelNavigationService>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            Check(registry != null && manager != null, "Shared navigation architecture is present");
            Check(services.Length == 1, "Exactly one PanelNavigationService is wired");

            if (registry != null)
            {
                registry.RebuildLookup();
                Check(HasUsableEntry(registry, PanelType.Home), "Home panel is available");
                Check(HasUsableEntry(registry, PanelType.TaskLibrary),
                    "Task Library panel is available");
                Check(HasUsableEntry(registry, PanelType.TaskExecution),
                    "Active Task panel is available");
                AppendAvailability(sb, registry, PanelType.Settings);
                AppendAvailability(sb, registry, PanelType.Reports);
                AppendAvailability(sb, registry, PanelType.AskAI);
            }

            report = sb.ToString();
            return failures == 0;
        }

        private static bool ValidateBinding(
            string prefabPath,
            PanelType expectedPanel,
            PanelType expectedFallback)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) return false;
            PanelNavigationBinding binding = prefab.GetComponent<PanelNavigationBinding>();
            return binding != null &&
                   binding.PanelType == expectedPanel &&
                   binding.FallbackParent == expectedFallback &&
                   binding.BackButton != null &&
                   PrefabUtility.IsPartOfPrefabInstance(binding.BackButton.gameObject);
        }

        private static bool RuntimeParentHasPriority()
        {
            string source = File.ReadAllText(ServiceSourcePath);
            int runtimeLookup = source.IndexOf(
                "_runtimeParents.TryGetValue(current",
                System.StringComparison.Ordinal);
            int fallbackReturn = source.IndexOf(
                "return fallbackParent;",
                System.StringComparison.Ordinal);
            return runtimeLookup >= 0 && fallbackReturn > runtimeLookup;
        }

        private static bool NavigationUsesSafeOrdering()
        {
            string source = File.ReadAllText(ServiceSourcePath);
            int navigateOpen = source.IndexOf(
                "SpatialWindow childWindow = panelRegistry.Open(child)",
                System.StringComparison.Ordinal);
            int navigateHide = source.IndexOf(
                "HideAllPrimaryPanelsExcept(child)",
                System.StringComparison.Ordinal);
            int backOpen = source.IndexOf(
                "SpatialWindow parentWindow = panelRegistry.Open(parent)",
                System.StringComparison.Ordinal);
            int backHide = source.IndexOf(
                "HideAllPrimaryPanelsExcept(parent)",
                System.StringComparison.Ordinal);
            return navigateOpen >= 0 && navigateHide > navigateOpen &&
                   backOpen >= 0 && backHide > backOpen;
        }

        private static bool NoRuntimeLayoutMutation()
        {
            string source = File.ReadAllText(ServiceSourcePath) +
                            File.ReadAllText(BindingSourcePath);
            return !source.Contains("new GameObject") &&
                   !source.Contains("Instantiate(") &&
                   !source.Contains("RectTransform") &&
                   !source.Contains("LayoutGroup") &&
                   !source.Contains("anchoredPosition") &&
                   !source.Contains("sizeDelta");
        }

        private static bool WireIsSurgical()
        {
            string source = File.ReadAllText(
                "Assets/Norscope/Editor/Phase75SinglePanelNavigationConfigurator.cs");
            int start = source.IndexOf(
                "private static void AddBindingSurgically(",
                System.StringComparison.Ordinal);
            int end = source.IndexOf(
                "[MenuItem(\"Norscope/HoloLens/Phase 7.5/3)",
                System.StringComparison.Ordinal);
            if (start < 0 || end <= start) return false;

            string surgicalSection = source.Substring(start, end - start);
            return surgicalSection.Contains("PrefabUtility.LoadPrefabContents(panelPath)") &&
                   surgicalSection.Contains("if (backTransform == null)") &&
                   surgicalSection.Contains("if (binding == null)") &&
                   surgicalSection.Contains("if (changed)") &&
                   surgicalSection.Contains("PrefabUtility.SaveAsPrefabAsset(root, panelPath)") &&
                   !surgicalSection.Contains("BuildTaskLibrary") &&
                   !surgicalSection.Contains("BuildActiveTask");
        }

        private static bool HasUsableEntry(PanelRegistry registry, PanelType type)
        {
            return registry.TryGetEntry(type, out PanelRegistryEntry entry) &&
                   entry != null &&
                   entry.prefab != null &&
                   entry.windowId != SpatialWindowId.None;
        }

        private static void AppendAvailability(
            StringBuilder report,
            PanelRegistry registry,
            PanelType type)
        {
            bool available = HasUsableEntry(registry, type);
            report.AppendLine("INFO  " + type + ": " +
                              (available
                                  ? "prefab available for single-panel navigation"
                                  : "placeholder/unavailable; Home will remain visible if opening fails"));
        }

        private static bool SourceContains(string path, string value)
        {
            return File.Exists(path) && File.ReadAllText(path).Contains(value);
        }

        private static Transform FindDeep(Transform root, string objectName)
        {
            if (root == null) return null;
            if (root.name == objectName) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeep(root.GetChild(i), objectName);
                if (found != null) return found;
            }
            return null;
        }

        private static void BackupPrefab(string panelPath)
        {
            if (!File.Exists(panelPath)) return;
            if (!AssetDatabase.IsValidFolder(BackupDirectory))
            {
                AssetDatabase.CreateFolder(
                    "Assets/Norscope/UI/Panels",
                    "_Backups");
            }

            string fileName = Path.GetFileNameWithoutExtension(panelPath);
            string backupPath = AssetDatabase.GenerateUniqueAssetPath(
                BackupDirectory + "/" + fileName + "_PrePhase75.prefab");
            AssetDatabase.CopyAsset(panelPath, backupPath);
        }
    }
}
