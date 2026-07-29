using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Norscope.DesignSystem;
using Norscope.Navigation;
using Norscope.TaskLibrary;
using Norscope.UI;

namespace Norscope.Editor
{
    /// <summary>
    /// Phase D1 creates design assets and adds presentation-only overlays. It never rebuilds
    /// scoped prefabs and never edits existing Buttons, listeners, target graphics, sprites,
    /// interaction components, layouts, navigation, or business components.
    /// </summary>
    public static class PhaseD1DesignSystemConfigurator
    {
        private const string BootstrapPath = "Assets/Norscope/Scenes/Bootstrap.unity";
        private const string ProfilePath =
            "Assets/Norscope/DesignSystem/Profiles/NorscopeDesignProfile.asset";
        private const string VisualLayerPath =
            "Assets/Norscope/DesignSystem/Prefabs/NorscopeInteractionVisualLayer.prefab";
        private const string LegacyThemePath =
            "Assets/Norscope/UI/DesignSystem/Themes/NorscopeTheme.asset";
        private const string RoundedSpritePath =
            "Assets/Norscope/UI/DesignSystem/Sprites/UIRounded_8.png";
        private const string HomePanelPath =
            "Assets/Norscope/UI/Panels/HomePanel.prefab";
        private const string TaskCardPath =
            "Assets/Norscope/UI/Controls/TaskCard.prefab";
        private const string ActiveTaskPath =
            "Assets/Norscope/UI/Panels/ActiveTaskPanel.prefab";
        private const string BackButtonPath =
            "Assets/Norscope/UI/Controls/NavigationBackButton.prefab";
        private const string CloseButtonPath =
            "Assets/Norscope/UI/Controls/NorscopeCloseButton.prefab";

        private const string InteractionSourcePath =
            "Assets/Norscope/Runtime/DesignSystem/NorscopeInteractionVisual.cs";
        private const string HighlightSourcePath =
            "Assets/Norscope/Runtime/DesignSystem/NorscopeHighlightController.cs";

        [MenuItem("Norscope/HoloLens/Phase D1/1) Capture Functional Baseline")]
        public static void CaptureFunctionalBaselineMenu()
        {
            if (File.Exists(D1FunctionalBaseline.BaselinePath))
            {
                Debug.Log("[Norscope D1] Functional baseline already exists and was left untouched.");
                return;
            }

            if (AnyD1VisualAlreadyWired())
            {
                EditorUtility.DisplayDialog(
                    "D1 baseline not captured",
                    "D1 visual components already exist in a scoped prefab. Restore the pre-D1 " +
                    "prefab backups before capturing a baseline.",
                    "OK");
                return;
            }

            D1FunctionalBaselineData baseline = D1FunctionalBaseline.Capture();
            if (!D1FunctionalBaseline.CreateImmutableBackups(
                    baseline,
                    out string backupReport))
            {
                Debug.LogError("[Norscope D1] Baseline backup capture FAILED:\n" + backupReport);
                EditorUtility.DisplayDialog(
                    "Baseline capture failed",
                    "Not every scoped asset could be backed up. No functional baseline was " +
                    "saved, and wiring remains blocked.\n\n" + backupReport,
                    "OK");
                return;
            }

            D1FunctionalBaseline.Save(baseline);
            Debug.Log("[Norscope D1] Functional baseline and immutable backups captured.\n" +
                      backupReport);
        }

        [MenuItem("Norscope/HoloLens/Phase D1/2) Create Design Assets If Missing")]
        public static void CreateDesignAssetsIfMissingMenu()
        {
            CreateDesignAssetsIfMissing();
            Debug.Log("[Norscope D1] Design assets are present. Existing assets were not overwritten.");
        }

        public static void CreateDesignAssetsIfMissing()
        {
            EnsureFolder("Assets/Norscope/DesignSystem");
            EnsureFolder("Assets/Norscope/DesignSystem/Profiles");
            EnsureFolder("Assets/Norscope/DesignSystem/Prefabs");
            EnsureFolder("Assets/Norscope/DesignSystem/Backups");

            if (!File.Exists(ProfilePath))
            {
                CreateDesignProfile();
            }

            if (!File.Exists(VisualLayerPath))
            {
                CreateVisualLayerPrefab();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("Norscope/HoloLens/Phase D1/3) Dry-Run Wiring Analysis")]
        public static void DryRunWiringAnalysisMenu()
        {
            bool canProceed = AnalyzeWiring(out string report);
            Debug.Log((canProceed
                ? "[Norscope D1] Dry-Run Wiring Analysis: READY\n"
                : "[Norscope D1] Dry-Run Wiring Analysis: BLOCKED\n") + report);
            EditorUtility.DisplayDialog(
                "D1 Dry-Run Wiring Analysis",
                (canProceed ? "READY TO WIRE\n\n" : "WIRING SHOULD NOT PROCEED\n\n") +
                report,
                "OK");
        }

        [MenuItem("Norscope/HoloLens/Phase D1/4) Wire D1 Visuals Surgically")]
        public static void WireD1VisualsSurgicallyMenu()
        {
            if (!File.Exists(D1FunctionalBaseline.BaselinePath))
            {
                EditorUtility.DisplayDialog(
                    "Capture baseline first",
                    "Run Phase D1/1) Capture Functional Baseline before modifying prefabs.",
                    "OK");
                return;
            }

            if (!D1FunctionalBaseline.CompareAgainstSaved(out string preflight))
            {
                EditorUtility.DisplayDialog(
                    "D1 wiring blocked",
                    "Current functional bindings do not match the captured baseline:\n\n" +
                    preflight,
                    "OK");
                return;
            }

            if (!AnalyzeWiring(out string wiringAnalysis))
            {
                EditorUtility.DisplayDialog(
                    "D1 wiring blocked",
                    "The shared dry-run analysis found blocking issues:\n\n" +
                    wiringAnalysis,
                    "OK");
                return;
            }

            NorscopeDesignProfile profile =
                AssetDatabase.LoadAssetAtPath<NorscopeDesignProfile>(ProfilePath);
            GameObject layerPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(VisualLayerPath);
            if (profile == null || layerPrefab == null)
            {
                throw new InvalidOperationException("D1 profile or visual-layer prefab is missing.");
            }

            WiringPlan[] plans = GetWiringPlans();
            for (int i = 0; i < plans.Length; i++)
            {
                ApplyToPrefab(
                    plans[i].prefabPath,
                    plans[i].targetSelector,
                    profile,
                    layerPrefab);
            }

            WireProvider(profile);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            bool baselineMatches =
                D1FunctionalBaseline.CompareAgainstSaved(out string postflight);
            Debug.Log(baselineMatches
                ? "[Norscope D1] Surgical wiring complete. Functional baseline unchanged."
                : "[Norscope D1] ERROR: post-wiring baseline mismatch:\n" + postflight);
        }

        [MenuItem("Norscope/HoloLens/Phase D1/5) Validate Norscope Design System")]
        public static void ValidateMenu()
        {
            bool ok = Validate(out string report);
            Debug.Log(ok
                ? "[Norscope D1] Validate Norscope Design System PASSED\n" + report
                : "[Norscope D1] Validate Norscope Design System FAILED\n" + report);
            EditorUtility.DisplayDialog(
                "Validate Norscope Design System",
                (ok ? "PASSED\n\n" : "FAILED\n\n") + report,
                "OK");
        }

        public static bool Validate(out string report)
        {
            var builder = new StringBuilder();
            int failures = 0;
            void Check(bool condition, string label)
            {
                builder.AppendLine((condition ? "PASS  " : "FAIL  ") + label);
                if (!condition) failures++;
            }

            Check(File.Exists(D1FunctionalBaseline.BaselinePath),
                "Pre-D1 functional baseline exists");
            Check(D1FunctionalBaseline.HasCompleteBackupManifest(out string backupIssue),
                "Immutable pre-D1 backup manifest is complete and hash-valid");
            if (!string.IsNullOrEmpty(backupIssue))
            {
                builder.AppendLine("       " + backupIssue);
            }
            Check(File.Exists(ProfilePath), "NorscopeDesignProfile.asset exists");
            Check(File.Exists(VisualLayerPath),
                "NorscopeInteractionVisualLayer.prefab exists");

            NorscopeDesignProfile profile =
                AssetDatabase.LoadAssetAtPath<NorscopeDesignProfile>(ProfilePath);
            Check(ProfileIsComplete(profile),
                "Master profile contains grouped tokens, global toggles, and legacy reference");
            Check(profile != null &&
                  !profile.features.enableHighlightPulse &&
                  !profile.features.enableScanEffect &&
                  !profile.performance.allowPulseOnHoloLens &&
                  !profile.performance.allowScanOnHoloLens,
                "Expensive pulse and scan effects default off for HoloLens");
            Check(DisabledStateIsSafe(profile),
                "Disabled controls suppress hover/press visuals and use authored disabled style");

            Check(ValidateWiredTargets(
                    HomePanelPath,
                    root => root.GetComponentsInChildren<HomePanelCard>(true)
                        .Select(card => new VisualTarget(
                            card.gameObject,
                            NorscopeVisualRole.HomeCard)),
                    6),
                "Six Home cards have one D1 visual each");
            Check(ValidateWiredTargets(
                    TaskCardPath,
                    root => root.GetComponentsInChildren<TaskCardView>(true)
                        .Select(card => new VisualTarget(
                            card.gameObject,
                            NorscopeVisualRole.TaskCard)),
                    1),
                "TaskCard has one D1 visual");
            Check(ValidateWiredTargets(ActiveTaskPath, ActiveTaskTargets, 4),
                "Active Task Previous/Next/Finish/Close have D1 visuals");
            Check(ValidateWiredTargets(
                    BackButtonPath,
                    root => RootButtonTarget(root, NorscopeVisualRole.BackButton),
                    1),
                "Back button has one D1 visual");
            Check(ValidateWiredTargets(
                    CloseButtonPath,
                    root => RootButtonTarget(root, NorscopeVisualRole.CloseButton),
                    1),
                "Shared Close button has one D1 visual");
            Check(AllDecorativeGraphicsIgnoreRaycasts(),
                "Every D1 decorative Graphic has raycastTarget disabled");
            Check(VisualLayerIsLightweight(),
                "Visual layer uses two graphics, one outline, and no custom material");
            Check(NoDuplicateD1Components(),
                "No target has duplicate D1 interaction components");
            Check(RuntimePresentationIsFunctionallyIsolated(),
                "D1 runtime has no click, navigation, task, panel, window, input, or audio calls");
            Check(RuntimeDoesNotConstructOrLayOutUi(),
                "D1 runtime creates no hierarchy and writes no RectTransform or LayoutGroup");
            Check(WiringIsAddIfMissing(),
                "D1 tooling uses surgical add-if-missing wiring only");
            Check(DryRunIsReadOnly(),
                "Dry-run analysis contains no project mutation APIs");

            bool baselineMatches =
                D1FunctionalBaseline.CompareAgainstSaved(out string baselineReport);
            Check(baselineMatches,
                "Buttons, listeners, targets, sprites, functional references, navigation, " +
                "Back relationships, and source hashes match baseline");
            if (!baselineMatches) builder.AppendLine(baselineReport);

            EditorSceneManager.OpenScene(BootstrapPath, OpenSceneMode.Single);
            NorscopeDesignProvider[] providers =
                UnityEngine.Object.FindObjectsByType<NorscopeDesignProvider>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            Check(providers.Length == 1, "Exactly one NorscopeDesignProvider is present");
            Check(providers.Length == 1 && providers[0].ActiveProfile == profile,
                "Design provider references the authoritative profile");

            report = builder.ToString();
            return failures == 0;
        }

        [MenuItem("Norscope/HoloLens/Phase D1/6) Restore Pre-D1 Backups")]
        public static void RestorePreD1BackupsMenu()
        {
            D1FunctionalBaselineData baseline = D1FunctionalBaseline.Load();
            int assetCount = baseline != null && baseline.backups != null
                ? baseline.backups.Length
                : 0;
            if (assetCount == 0)
            {
                EditorUtility.DisplayDialog(
                    "Restore unavailable",
                    "The functional baseline has no immutable backup manifest. Nothing was changed.",
                    "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "Restore Pre-D1 Backups",
                    "Restore " + assetCount +
                    " assets recorded by the pre-D1 baseline?\n\n" +
                    "Only manifest-listed prefabs and Bootstrap can be overwritten. " +
                    "Current D1 prefab wiring in those assets will be removed.",
                    "Restore",
                    "Cancel"))
            {
                return;
            }

            bool success = D1FunctionalBaseline.RestoreImmutableBackups(out string report);
            AssetDatabase.Refresh();
            if (success &&
                SceneManager.GetActiveScene().path == BootstrapPath)
            {
                EditorSceneManager.OpenScene(BootstrapPath, OpenSceneMode.Single);
            }
            Debug.Log((success
                ? "[Norscope D1] Restore Pre-D1 Backups SUCCEEDED\n"
                : "[Norscope D1] Restore Pre-D1 Backups FAILED\n") + report);
            EditorUtility.DisplayDialog(
                "Restore Pre-D1 Backups",
                (success ? "SUCCESS\n\n" : "FAILED\n\n") + report,
                "OK");
        }

        private readonly struct VisualTarget
        {
            public readonly GameObject gameObject;
            public readonly NorscopeVisualRole role;

            public VisualTarget(GameObject target, NorscopeVisualRole visualRole)
            {
                gameObject = target;
                role = visualRole;
            }
        }

        private sealed class WiringPlan
        {
            public string prefabPath;
            public string displayName;
            public int expectedTargetCount;
            public Func<GameObject, IEnumerable<VisualTarget>> targetSelector;
        }

        private readonly struct TargetInspection
        {
            public readonly VisualTarget target;
            public readonly int visualCount;
            public readonly int layerCount;
            public readonly bool hasButton;
            public readonly bool layerIsValid;

            public TargetInspection(VisualTarget visualTarget)
            {
                target = visualTarget;
                visualCount = visualTarget.gameObject != null
                    ? visualTarget.gameObject
                        .GetComponents<NorscopeInteractionVisual>().Length
                    : 0;
                Transform[] directLayers = visualTarget.gameObject != null
                    ? Enumerable.Range(0, visualTarget.gameObject.transform.childCount)
                        .Select(index => visualTarget.gameObject.transform.GetChild(index))
                        .Where(child => child.name == "D1_VisualLayer")
                        .ToArray()
                    : Array.Empty<Transform>();
                layerCount = directLayers.Length;
                hasButton = visualTarget.gameObject != null &&
                            visualTarget.gameObject.GetComponent<Button>() != null;
                layerIsValid = layerCount == 1 &&
                               directLayers[0]
                                   .GetComponent<NorscopeHighlightController>() != null;
            }

            public bool WiringRequired => visualCount == 0 || layerCount == 0;
            public bool HasDuplicates => visualCount > 1 || layerCount > 1;
        }

        private static bool AnalyzeWiring(out string report)
        {
            var builder = new StringBuilder();
            int prefabsToModify = 0;
            int componentsToAdd = 0;
            int layersToAdd = 0;
            int warnings = 0;
            int errors = 0;
            int blockers = 0;

            void Block(string message)
            {
                blockers++;
                errors++;
                builder.AppendLine("BLOCKER  " + message);
            }

            if (!File.Exists(D1FunctionalBaseline.BaselinePath))
            {
                Block("Functional baseline is missing.");
            }
            else if (!D1FunctionalBaseline.HasCompleteBackupManifest(out string backupIssue))
            {
                Block("Immutable backup manifest is incomplete: " + backupIssue);
            }

            if (AssetDatabase.LoadAssetAtPath<NorscopeDesignProfile>(ProfilePath) == null)
            {
                Block("NorscopeDesignProfile.asset is missing.");
            }

            GameObject layerPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(VisualLayerPath);
            if (layerPrefab == null)
            {
                Block("NorscopeInteractionVisualLayer.prefab is missing.");
            }
            else if (!VisualLayerAssetIsSafe(layerPrefab))
            {
                Block("The shared visual-layer prefab is invalid or contains a raycast target.");
            }

            WiringPlan[] plans = GetWiringPlans();
            for (int planIndex = 0; planIndex < plans.Length; planIndex++)
            {
                WiringPlan plan = plans[planIndex];
                builder.AppendLine();
                builder.AppendLine("PREFAB  " + plan.prefabPath);
                GameObject prefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(plan.prefabPath);
                if (prefab == null)
                {
                    Block(plan.displayName + ": prefab is missing.");
                    continue;
                }

                VisualTarget[] targets = plan.targetSelector(prefab).ToArray();
                bool prefabRequiresWiring = false;
                builder.AppendLine("  Expected hierarchy targets: " +
                                   targets.Length + "/" + plan.expectedTargetCount);
                if (targets.Length != plan.expectedTargetCount)
                {
                    Block(plan.displayName + ": expected " +
                          plan.expectedTargetCount + " targets but found " + targets.Length + ".");
                }

                for (int targetIndex = 0; targetIndex < targets.Length; targetIndex++)
                {
                    TargetInspection inspection = InspectTarget(targets[targetIndex]);
                    string path = GetHierarchyPath(
                        inspection.target.gameObject.transform,
                        prefab.transform);
                    builder.AppendLine("  GameObject: " + path);
                    builder.AppendLine("    D1 component exists: " +
                                       (inspection.visualCount == 1 ? "yes" : "no"));
                    builder.AppendLine("    D1 visual layer exists: " +
                                       (inspection.layerCount == 1 ? "yes" : "no"));
                    builder.AppendLine("    Wiring required: " +
                                       (inspection.WiringRequired ? "yes" : "no"));
                    builder.AppendLine("    Duplicate visual components/layers: " +
                                       (inspection.HasDuplicates ? "yes" : "no"));
                    builder.AppendLine("    Required Button exists: " +
                                       (inspection.hasButton ? "yes" : "no"));
                    builder.AppendLine("    Expected hierarchy valid: " +
                                       (inspection.layerCount == 0 ||
                                        inspection.layerIsValid ? "yes" : "no"));

                    if (!inspection.hasButton)
                    {
                        Block(path + ": required Button is missing.");
                    }
                    if (inspection.HasDuplicates)
                    {
                        Block(path + ": duplicate D1 component or visual layer exists.");
                    }
                    if (inspection.layerCount == 1 && !inspection.layerIsValid)
                    {
                        Block(path + ": existing D1 visual layer is incomplete.");
                    }
                    if (inspection.visualCount == 0)
                    {
                        componentsToAdd++;
                        prefabRequiresWiring = true;
                    }
                    if (inspection.layerCount == 0)
                    {
                        layersToAdd++;
                        prefabRequiresWiring = true;
                    }
                }

                if (prefabRequiresWiring) prefabsToModify++;
            }

            int providerCount = CountProviderReferencesInBootstrap(out string providerIssue);
            builder.AppendLine();
            builder.AppendLine("BOOTSTRAP");
            builder.AppendLine("  NorscopeDesignProvider count: " + providerCount);
            if (!string.IsNullOrEmpty(providerIssue))
            {
                Block(providerIssue);
            }
            else if (providerCount > 1)
            {
                Block("Bootstrap contains duplicate NorscopeDesignProvider components.");
            }
            else if (providerCount == 0)
            {
                builder.AppendLine("  Wiring required: yes (one provider will be added)");
                componentsToAdd++;
            }
            else
            {
                builder.AppendLine("  Wiring required: no");
            }

            builder.AppendLine();
            builder.AppendLine("SUMMARY");
            builder.AppendLine("  Total prefabs to modify: " + prefabsToModify);
            builder.AppendLine("  Total components to add: " + componentsToAdd);
            builder.AppendLine("  Total visual layers to add: " + layersToAdd);
            builder.AppendLine("  Warnings: " + warnings);
            builder.AppendLine("  Errors: " + errors);
            builder.AppendLine("  Blockers: " + blockers);
            builder.AppendLine(blockers == 0
                ? "  Result: wiring may proceed."
                : "  Result: wiring should not proceed.");
            report = builder.ToString();
            return blockers == 0;
        }

        private static WiringPlan[] GetWiringPlans()
        {
            return new[]
            {
                new WiringPlan
                {
                    prefabPath = HomePanelPath,
                    displayName = "Home Panel cards",
                    expectedTargetCount = 6,
                    targetSelector = root =>
                        root.GetComponentsInChildren<HomePanelCard>(true)
                            .Select(card => new VisualTarget(
                                card.gameObject,
                                NorscopeVisualRole.HomeCard))
                },
                new WiringPlan
                {
                    prefabPath = TaskCardPath,
                    displayName = "Task Card",
                    expectedTargetCount = 1,
                    targetSelector = root =>
                        root.GetComponentsInChildren<TaskCardView>(true)
                            .Select(card => new VisualTarget(
                                card.gameObject,
                                NorscopeVisualRole.TaskCard))
                },
                new WiringPlan
                {
                    prefabPath = ActiveTaskPath,
                    displayName = "Active Task navigation",
                    expectedTargetCount = 4,
                    targetSelector = ActiveTaskTargets
                },
                new WiringPlan
                {
                    prefabPath = BackButtonPath,
                    displayName = "Navigation Back button",
                    expectedTargetCount = 1,
                    targetSelector = root =>
                        RootButtonTarget(root, NorscopeVisualRole.BackButton)
                },
                new WiringPlan
                {
                    prefabPath = CloseButtonPath,
                    displayName = "Shared Close button",
                    expectedTargetCount = 1,
                    targetSelector = root =>
                        RootButtonTarget(root, NorscopeVisualRole.CloseButton)
                }
            };
        }

        private static IEnumerable<VisualTarget> ActiveTaskTargets(GameObject root)
        {
            string[] names = { "PreviousButton", "NextButton", "FinishButton", "CloseButton" };
            for (int i = 0; i < names.Length; i++)
            {
                Transform target = FindDeep(root.transform, names[i]);
                if (target == null) continue;
                NorscopeVisualRole role = names[i] == "FinishButton"
                    ? NorscopeVisualRole.PrimaryButton
                    : (names[i] == "CloseButton"
                        ? NorscopeVisualRole.CloseButton
                        : NorscopeVisualRole.SecondaryButton);
                yield return new VisualTarget(target.gameObject, role);
            }
        }

        private static IEnumerable<VisualTarget> RootButtonTarget(
            GameObject root,
            NorscopeVisualRole role)
        {
            if (root.GetComponent<Button>() != null)
            {
                yield return new VisualTarget(root, role);
            }
        }

        private static void ApplyToPrefab(
            string prefabPath,
            Func<GameObject, IEnumerable<VisualTarget>> targetSelector,
            NorscopeDesignProfile profile,
            GameObject layerPrefab)
        {
            if (!File.Exists(prefabPath))
            {
                Debug.LogWarning("[Norscope D1] Scoped prefab missing: " + prefabPath);
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            bool changed = false;
            try
            {
                VisualTarget[] targets = targetSelector(root).ToArray();
                for (int i = 0; i < targets.Length; i++)
                {
                    changed |= EnsureVisual(targets[i], profile, layerPrefab);
                }

                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static bool EnsureVisual(
            VisualTarget target,
            NorscopeDesignProfile profile,
            GameObject layerPrefab)
        {
            if (target.gameObject == null) return false;
            TargetInspection inspection = InspectTarget(target);
            if (!inspection.hasButton)
            {
                throw new InvalidOperationException(
                    "Required Button is missing from " + target.gameObject.name + ".");
            }
            if (inspection.HasDuplicates)
            {
                throw new InvalidOperationException(
                    "Duplicate D1 component or visual layer exists on " +
                    target.gameObject.name + ".");
            }
            if (inspection.layerCount == 1 && !inspection.layerIsValid)
            {
                throw new InvalidOperationException(
                    "Existing D1 visual layer is incomplete on " +
                    target.gameObject.name + ".");
            }
            bool changed = false;

            Transform layerTransform = FindDirectChild(target.gameObject.transform, "D1_VisualLayer");
            GameObject layer;
            if (layerTransform == null)
            {
                layer = PrefabUtility.InstantiatePrefab(
                    layerPrefab,
                    target.gameObject.transform) as GameObject;
                if (layer == null)
                {
                    throw new InvalidOperationException("Could not instantiate D1 visual layer.");
                }

                layer.name = "D1_VisualLayer";
                RectTransform layerRect = layer.GetComponent<RectTransform>();
                layerRect.anchorMin = Vector2.zero;
                layerRect.anchorMax = Vector2.one;
                layerRect.offsetMin = Vector2.zero;
                layerRect.offsetMax = Vector2.zero;
                layer.transform.SetAsLastSibling();
                changed = true;
            }
            else
            {
                layer = layerTransform.gameObject;
            }

            NorscopeHighlightController controller =
                layer.GetComponent<NorscopeHighlightController>();
            if (controller == null)
            {
                throw new InvalidOperationException(
                    "D1 visual layer has no NorscopeHighlightController.");
            }

            NorscopeInteractionVisual visual =
                target.gameObject.GetComponent<NorscopeInteractionVisual>();
            if (visual == null)
            {
                visual = target.gameObject.AddComponent<NorscopeInteractionVisual>();
                Selectable selectable = target.gameObject.GetComponent<Selectable>();
                visual.SetReferences(target.role, selectable, controller, profile);
                changed = true;
            }
            else
            {
                SerializedObject so = new SerializedObject(visual);
                SerializedProperty selectableProperty = so.FindProperty("observedSelectable");
                SerializedProperty controllerProperty = so.FindProperty("highlightController");
                SerializedProperty profileProperty = so.FindProperty("fallbackProfile");
                if (selectableProperty.objectReferenceValue == null)
                {
                    selectableProperty.objectReferenceValue =
                        target.gameObject.GetComponent<Selectable>();
                    changed = true;
                }
                if (controllerProperty.objectReferenceValue == null)
                {
                    controllerProperty.objectReferenceValue = controller;
                    changed = true;
                }
                if (profileProperty.objectReferenceValue == null)
                {
                    profileProperty.objectReferenceValue = profile;
                    changed = true;
                }
                if (changed) so.ApplyModifiedPropertiesWithoutUndo();
            }

            return changed;
        }

        private static TargetInspection InspectTarget(VisualTarget target)
        {
            return new TargetInspection(target);
        }

        private static bool VisualLayerAssetIsSafe(GameObject layerPrefab)
        {
            if (layerPrefab == null ||
                layerPrefab.GetComponent<NorscopeHighlightController>() == null ||
                layerPrefab.GetComponent<Button>() != null)
            {
                return false;
            }

            Graphic[] graphics = layerPrefab.GetComponentsInChildren<Graphic>(true);
            return graphics.Length > 0 && graphics.All(graphic => !graphic.raycastTarget);
        }

        private static int CountProviderReferencesInBootstrap(out string issue)
        {
            issue = string.Empty;
            if (!File.Exists(BootstrapPath))
            {
                issue = "Bootstrap scene is missing.";
                return 0;
            }

            string[] scriptGuids =
                AssetDatabase.FindAssets("NorscopeDesignProvider t:MonoScript");
            if (scriptGuids.Length != 1)
            {
                issue = "Expected exactly one NorscopeDesignProvider script asset; found " +
                        scriptGuids.Length + ".";
                return 0;
            }

            string needle = "guid: " + scriptGuids[0];
            string yaml = File.ReadAllText(BootstrapPath);
            int count = 0;
            int searchFrom = 0;
            while ((searchFrom = yaml.IndexOf(
                       needle,
                       searchFrom,
                       StringComparison.Ordinal)) >= 0)
            {
                count++;
                searchFrom += needle.Length;
            }
            return count;
        }

        private static string GetHierarchyPath(Transform current, Transform root)
        {
            var names = new Stack<string>();
            Transform cursor = current;
            while (cursor != null)
            {
                names.Push(cursor.name);
                if (cursor == root) break;
                cursor = cursor.parent;
            }
            return string.Join("/", names);
        }

        private static void WireProvider(NorscopeDesignProfile profile)
        {
            Scene scene = EditorSceneManager.OpenScene(BootstrapPath, OpenSceneMode.Single);
            PanelRegistry registry =
                UnityEngine.Object.FindFirstObjectByType<PanelRegistry>();
            if (registry == null)
            {
                throw new InvalidOperationException("PanelRegistry is missing from Bootstrap.");
            }

            NorscopeDesignProvider provider =
                UnityEngine.Object.FindFirstObjectByType<NorscopeDesignProvider>();
            if (provider == null)
            {
                provider = registry.gameObject.AddComponent<NorscopeDesignProvider>();
            }

            SerializedObject so = new SerializedObject(provider);
            SerializedProperty profileProperty = so.FindProperty("activeProfile");
            if (profileProperty.objectReferenceValue != profile)
            {
                profileProperty.objectReferenceValue = profile;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(provider);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }

        private static void CreateDesignProfile()
        {
            NorscopeTheme legacy =
                AssetDatabase.LoadAssetAtPath<NorscopeTheme>(LegacyThemePath);
            NorscopeDesignProfile profile =
                ScriptableObject.CreateInstance<NorscopeDesignProfile>();
            profile.legacyThemeCompatibility = legacy;
            InitializeProfile(profile, legacy);
            AssetDatabase.CreateAsset(profile, ProfilePath);
        }

        private static void InitializeProfile(
            NorscopeDesignProfile profile,
            NorscopeTheme legacy)
        {
            Color panel = legacy != null ? legacy.panelBackground : Color.black;
            Color elevated = legacy != null ? legacy.elevatedPanelBackground : Color.black;
            Color accent = legacy != null ? legacy.primaryAccent : Color.cyan;
            Color secondaryAccent = legacy != null ? legacy.secondaryAccent : Color.blue;
            Color text = legacy != null ? legacy.primaryText : Color.white;
            Color secondaryText = legacy != null ? legacy.secondaryText : Color.gray;
            Color border = legacy != null ? legacy.border : Color.gray;
            Color disabled = legacy != null ? legacy.disabled : Color.gray;
            Color success = legacy != null ? legacy.success : Color.green;
            Color warning = legacy != null ? legacy.warning : Color.yellow;
            Color error = legacy != null ? legacy.error : Color.red;

            profile.colors.surfacePrimary = panel;
            profile.colors.surfaceElevated = elevated;
            profile.colors.surfaceOverlay = new Color(elevated.r, elevated.g, elevated.b, 0.55f);
            profile.colors.borderSubtle = border;
            profile.colors.borderStrong = accent;
            profile.colors.textPrimary = text;
            profile.colors.textSecondary = secondaryText;
            profile.colors.textDisabled = disabled;
            profile.colors.accentPrimary = accent;
            profile.colors.accentSecondary = secondaryAccent;
            profile.colors.information = secondaryAccent;
            profile.colors.success = success;
            profile.colors.warning = warning;
            profile.colors.error = error;
            profile.colors.verification = success;
            profile.colors.selection = accent;

            profile.panels.acrylicStrength = 0.18f;
            profile.panels.transparency = 0.08f;
            profile.panels.cornerRadius = legacy != null ? legacy.largeCornerRadius : 12f;
            profile.panels.borderThickness =
                legacy != null ? legacy.thinBorderWidth : 1f;
            profile.panels.shadowColor = new Color(0f, 0f, 0f, 0.35f);
            profile.panels.shadowStrength = 0.18f;
            profile.panels.edgeGlowColor = accent;
            profile.panels.edgeGlowStrength = 0.22f;
            profile.panels.depthSeparation = 0.002f;

            ConfigureStateSet(profile.buttons.standard, elevated, border, accent, disabled, success);
            ConfigureStateSet(profile.buttons.primary, accent, accent, text, disabled, success);
            ConfigureStateSet(profile.buttons.navigation, elevated, border, accent, disabled, success);
            ConfigureStateSet(profile.cards.homeCard, elevated, border, accent, disabled, success);
            ConfigureStateSet(profile.cards.taskCard, elevated, border, accent, disabled, success);
            ConfigureStateSet(profile.cards.reportCard, elevated, border, secondaryAccent, disabled, success);

            profile.typography.primaryFont =
                legacy != null && legacy.fonts != null ? legacy.fonts.bodyFont : null;
            profile.typography.displaySize = legacy != null ? legacy.titleSize : 26f;
            profile.typography.panelTitleSize = legacy != null ? legacy.titleSize : 26f;
            profile.typography.sectionTitleSize = legacy != null ? legacy.headingSize : 20f;
            profile.typography.cardTitleSize = legacy != null ? legacy.headingSize : 20f;
            profile.typography.bodySize = legacy != null ? legacy.bodySize : 14f;
            profile.typography.captionSize = legacy != null ? legacy.captionSize : 11f;
            profile.typography.metadataSize = legacy != null ? legacy.captionSize : 11f;
            profile.typography.labelSize = legacy != null ? legacy.bodySize : 14f;
            profile.typography.lineSpacing = 0f;
            profile.typography.paragraphSpacing = 8f;
            profile.typography.characterSpacing = 0f;

            profile.motion.hoverInDuration = 0.15f;
            profile.motion.hoverOutDuration = 0.18f;
            profile.motion.pressDuration = 0.09f;
            profile.motion.releaseDuration = 0.15f;
            profile.motion.selectionDuration = 0.16f;
            profile.motion.verificationDuration = 0.28f;
            profile.motion.completionDuration = 0.22f;
            profile.motion.fadeDuration = 0.18f;
            profile.motion.slideDuration = 0.2f;
            profile.motion.labelDelay = 0.12f;
            AnimationCurve easeOut = new AnimationCurve(
                new Keyframe(0f, 0f, 0f, 2f),
                new Keyframe(1f, 1f, 0f, 0f));
            profile.motion.defaultEase = easeOut;
            profile.motion.hoverEase = new AnimationCurve(easeOut.keys);
            profile.motion.pressEase = new AnimationCurve(easeOut.keys);
            profile.motion.verificationEase = new AnimationCurve(easeOut.keys);

            profile.highlights.outlineColor = accent;
            profile.highlights.outlineStrength = 1f;
            profile.highlights.innerGlowColor = accent;
            profile.highlights.innerGlowStrength = 0.18f;
            profile.highlights.scanColor = accent;
            profile.highlights.scanDuration = 0.4f;
            profile.highlights.scanWidth = 0.08f;
            profile.highlights.pulseAmplitude = 0.01f;
            profile.highlights.pulseDuration = 2.4f;
            profile.highlights.verificationColor = success;
            profile.highlights.verificationRippleDuration = 0.28f;
            profile.highlights.completedColor = success;
            profile.highlights.completedOpacity = 0.5f;

            profile.floatingLabels.font = profile.typography.primaryFont;
            profile.floatingLabels.fontSize = profile.typography.labelSize;
            profile.floatingLabels.textColor = text;
            profile.floatingLabels.backgroundColor =
                new Color(panel.r, panel.g, panel.b, 0.85f);
            profile.floatingLabels.borderColor = border;
            profile.floatingLabels.leaderLineColor = accent;
            profile.floatingLabels.leaderLineWidth = 1f;
            profile.floatingLabels.opacity = 0.95f;
            profile.floatingLabels.fadeInDuration = 0.18f;
            profile.floatingLabels.fadeOutDuration = 0.2f;
            profile.floatingLabels.distanceScale = 1f;
            profile.floatingLabels.maximumWidth = 320f;

            ConfigureNotification(profile.notifications.information, elevated, secondaryAccent, text);
            ConfigureNotification(profile.notifications.success, elevated, success, text);
            ConfigureNotification(profile.notifications.warning, elevated, warning, text);
            ConfigureNotification(profile.notifications.error, elevated, error, text);

            ConfigureStateSet(profile.futureAr.componentHighlight, Color.clear, border, accent, disabled, success);
            ConfigureStateSet(profile.futureAr.directionalArrow, Color.clear, border, accent, disabled, success);
            ConfigureStateSet(profile.futureAr.ghostComponent, Color.clear, border, secondaryAccent, disabled, success);
            ConfigureStateSet(profile.futureAr.stepMarker, Color.clear, border, accent, disabled, success);
            ConfigureStateSet(profile.futureAr.safetyZone, Color.clear, warning, warning, disabled, success);
            ConfigureStateSet(profile.futureAr.cadOverlay, Color.clear, border, secondaryAccent, disabled, success);
            profile.futureAr.maximumTransparentLayers = 2f;
            profile.futureAr.distanceFadeStart = 2f;
            profile.futureAr.distanceFadeEnd = 5f;

            profile.performance.maximumOutlinePasses = 1;
            profile.performance.maximumTransparentOverlayLayers = 2;
            profile.performance.allowPulseOnHoloLens = false;
            profile.performance.allowScanOnHoloLens = false;
        }

        private static void ConfigureStateSet(
            InteractionStateSet set,
            Color surface,
            Color edge,
            Color accent,
            Color disabled,
            Color verified)
        {
            ConfigureState(set.idle, surface, edge, 0f, 0.12f, 0.6f, 1f);
            ConfigureState(set.hovered, accent, accent, 0.055f, 0.48f, 1f, 1f);
            ConfigureState(set.pressed, accent, accent, 0.075f, 0.62f, 1.2f, 0.98f);
            ConfigureState(set.selected, accent, accent, 0.065f, 0.55f, 1f, 1f);
            ConfigureState(set.disabled, disabled, disabled, 0.015f, 0.08f, 0.5f, 1f);
            ConfigureState(set.highlighted, accent, accent, 0.08f, 0.68f, 1.2f, 1f);
            ConfigureState(set.verified, verified, verified, 0.075f, 0.62f, 1.1f, 1f);
            ConfigureState(set.completed, verified, verified, 0.045f, 0.42f, 0.9f, 1f);
        }

        private static void ConfigureState(
            InteractionStateStyle state,
            Color surface,
            Color edge,
            float surfaceOpacity,
            float edgeOpacity,
            float outline,
            float scale)
        {
            state.surfaceColor = surface;
            state.edgeColor = edge;
            state.surfaceOpacity = surfaceOpacity;
            state.edgeOpacity = edgeOpacity;
            state.outlineStrength = outline;
            state.luminanceStrength = 1f;
            state.visualScale = scale;
            state.depthOffset = 0f;
        }

        private static void ConfigureNotification(
            NotificationVariantTokens variant,
            Color surface,
            Color border,
            Color text)
        {
            variant.surfaceColor = surface;
            variant.borderColor = border;
            variant.textColor = text;
            variant.optionalIcon = null;
            variant.visibleDuration = 4f;
        }

        private static void CreateVisualLayerPrefab()
        {
            Sprite rounded = AssetDatabase.LoadAssetAtPath<Sprite>(RoundedSpritePath);
            GameObject root =
                new GameObject("NorscopeInteractionVisualLayer", typeof(RectTransform));
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(100f, 40f);

            Image surface = CreateOverlayImage(root.transform, "SurfaceGlow", rounded);
            Image edge = CreateOverlayImage(root.transform, "EdgeGlow", rounded);
            edge.color = new Color(1f, 1f, 1f, 0f);
            Outline outline = edge.gameObject.AddComponent<Outline>();
            outline.effectColor = Color.clear;
            outline.effectDistance = Vector2.one;
            outline.useGraphicAlpha = false;

            NorscopeHighlightController controller =
                root.AddComponent<NorscopeHighlightController>();
            // Existing scoped controls already own press/hover scaling. D1 deliberately leaves
            // its optional visual-scale target unassigned to avoid competing with those scripts.
            controller.SetReferences(surface, edge, outline, null, null);

            PrefabUtility.SaveAsPrefabAsset(root, VisualLayerPath);
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static Image CreateOverlayImage(
            Transform parent,
            string objectName,
            Sprite rounded)
        {
            GameObject child = new GameObject(objectName, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            RectTransform rect = child.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Image image = child.AddComponent<Image>();
            image.sprite = rounded;
            image.type = Image.Type.Sliced;
            image.color = Color.clear;
            image.raycastTarget = false;
            return image;
        }

        private static bool ProfileIsComplete(NorscopeDesignProfile profile)
        {
            return profile != null &&
                   profile.features != null &&
                   profile.colors != null &&
                   profile.panels != null &&
                   profile.buttons != null &&
                   profile.cards != null &&
                   profile.typography != null &&
                   profile.motion != null &&
                   profile.highlights != null &&
                   profile.floatingLabels != null &&
                   profile.notifications != null &&
                   profile.futureAr != null &&
                   profile.performance != null &&
                   profile.legacyThemeCompatibility != null &&
                   StateSetIsComplete(profile.buttons.standard) &&
                   StateSetIsComplete(profile.buttons.primary) &&
                   StateSetIsComplete(profile.buttons.navigation) &&
                   StateSetIsComplete(profile.cards.homeCard) &&
                   StateSetIsComplete(profile.cards.taskCard) &&
                   StateSetIsComplete(profile.cards.reportCard);
        }

        private static bool StateSetIsComplete(InteractionStateSet set)
        {
            return set != null &&
                   set.idle != null &&
                   set.hovered != null &&
                   set.pressed != null &&
                   set.selected != null &&
                   set.disabled != null &&
                   set.highlighted != null &&
                   set.verified != null &&
                   set.completed != null;
        }

        private static bool DisabledStateIsSafe(NorscopeDesignProfile profile)
        {
            if (profile == null ||
                !StateSetIsComplete(profile.buttons.standard) ||
                !File.Exists(InteractionSourcePath))
            {
                return false;
            }

            string source = File.ReadAllText(InteractionSourcePath);
            int disabledCheck = source.IndexOf(
                "if (!IsInteractable()) return NorscopeVisualState.Disabled;",
                StringComparison.Ordinal);
            int hoverCheck = source.IndexOf(
                "if (_hovered && profile != null && profile.features.enableHoverGlow)",
                StringComparison.Ordinal);
            return disabledCheck >= 0 &&
                   hoverCheck > disabledCheck &&
                   profile.buttons.standard.disabled.edgeOpacity <=
                   profile.buttons.standard.hovered.edgeOpacity;
        }

        private static bool ValidateWiredTargets(
            string prefabPath,
            Func<GameObject, IEnumerable<VisualTarget>> selector,
            int expected)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) return false;
            VisualTarget[] targets = selector(prefab).ToArray();
            return targets.Length == expected &&
                   targets.All(target =>
                   {
                       NorscopeInteractionVisual[] visuals =
                           target.gameObject.GetComponents<NorscopeInteractionVisual>();
                       return visuals.Length == 1 &&
                              visuals[0].HighlightController != null &&
                              visuals[0].ObservedSelectable != null;
                   });
        }

        private static bool AllDecorativeGraphicsIgnoreRaycasts()
        {
            GameObject layer =
                AssetDatabase.LoadAssetAtPath<GameObject>(VisualLayerPath);
            return layer != null &&
                   layer.GetComponentsInChildren<Graphic>(true)
                       .All(graphic => !graphic.raycastTarget);
        }

        private static bool VisualLayerIsLightweight()
        {
            GameObject layer =
                AssetDatabase.LoadAssetAtPath<GameObject>(VisualLayerPath);
            if (layer == null) return false;
            Graphic[] graphics = layer.GetComponentsInChildren<Graphic>(true);
            Outline[] outlines = layer.GetComponentsInChildren<Outline>(true);
            bool noCustomMaterial = graphics.All(graphic =>
            {
                SerializedObject so = new SerializedObject(graphic);
                SerializedProperty material = so.FindProperty("m_Material");
                return material == null || material.objectReferenceValue == null;
            });
            return graphics.Length == 2 && outlines.Length == 1 && noCustomMaterial;
        }

        private static bool NoDuplicateD1Components()
        {
            foreach (string path in new[]
                     {
                         HomePanelPath, TaskCardPath, ActiveTaskPath,
                         BackButtonPath, CloseButtonPath
                     })
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) return false;
                Transform[] transforms = prefab.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < transforms.Length; i++)
                {
                    if (transforms[i].GetComponents<NorscopeInteractionVisual>().Length > 1)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private static bool RuntimePresentationIsFunctionallyIsolated()
        {
            string source = File.ReadAllText(InteractionSourcePath) +
                            File.ReadAllText(HighlightSourcePath);
            string[] forbidden =
            {
                ".onClick", "AddListener", "RemoveListener", "PanelRegistry",
                "PanelNavigationService", "SpatialWindow", "TaskCardView",
                "ActiveTaskSession", "UIAudioManager", "InputAction"
            };
            return forbidden.All(value => !source.Contains(value));
        }

        private static bool RuntimeDoesNotConstructOrLayOutUi()
        {
            string source = File.ReadAllText(InteractionSourcePath) +
                            File.ReadAllText(HighlightSourcePath);
            string[] forbidden =
            {
                "new GameObject", "Instantiate(", "AddComponent<",
                "RectTransform", "LayoutGroup", "anchoredPosition",
                "sizeDelta", "SetSizeWithCurrentAnchors"
            };
            return forbidden.All(value => !source.Contains(value));
        }

        private static bool WiringIsAddIfMissing()
        {
            string source = File.ReadAllText(
                "Assets/Norscope/Editor/PhaseD1DesignSystemConfigurator.cs");
            int start = source.IndexOf(
                "private static bool EnsureVisual(",
                StringComparison.Ordinal);
            int end = source.IndexOf(
                "private static void WireProvider(",
                StringComparison.Ordinal);
            if (start < 0 || end <= start) return false;
            string section = source.Substring(start, end - start);
            return section.Contains("if (layerTransform == null)") &&
                   section.Contains("if (visual == null)") &&
                   section.Contains("PrefabUtility.InstantiatePrefab") &&
                   !section.Contains("DestroyImmediate") &&
                   !section.Contains("RemoveComponent");
        }

        private static bool DryRunIsReadOnly()
        {
            string source = File.ReadAllText(
                "Assets/Norscope/Editor/PhaseD1DesignSystemConfigurator.cs");
            int start = source.IndexOf(
                "private static bool AnalyzeWiring(",
                StringComparison.Ordinal);
            int end = source.IndexOf(
                "private static WiringPlan[] GetWiringPlans(",
                StringComparison.Ordinal);
            if (start < 0 || end <= start) return false;
            string section = source.Substring(start, end - start);
            string[] forbidden =
            {
                "SaveAsPrefabAsset", "SaveAssets", "Refresh(",
                "CreateAsset", "CreateFolder", "CopyAsset", "File.Copy",
                "AddComponent", "DestroyImmediate", "OpenScene",
                "MarkSceneDirty", "SaveScene", "InstantiatePrefab"
            };
            return forbidden.All(value => !section.Contains(value));
        }

        private static bool AnyD1VisualAlreadyWired()
        {
            foreach (string path in new[]
                     {
                         HomePanelPath, TaskCardPath, ActiveTaskPath,
                         BackButtonPath, CloseButtonPath
                     })
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null &&
                    prefab.GetComponentInChildren<NorscopeInteractionVisual>(true) != null)
                {
                    return true;
                }
            }
            return false;
        }

        private static Transform FindDirectChild(Transform parent, string childName)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == childName) return child;
            }
            return null;
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

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            string name = Path.GetFileName(folder);
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
