using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Norscope.Navigation;
using Norscope.Settings;
using Norscope.UI;
using Norscope.Windows;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace Norscope.Editor
{
    /// <summary>
    /// Settings-only interaction repair preparation. Capture and Dry Run are
    /// read-only with respect to production assets. Apply stays approval-locked.
    /// </summary>
    public static class PhaseSettingsInteractionRepairConfigurator
    {
        private const string Menu =
            "Norscope/HoloLens/Settings Figma/Interaction Repair/";
        private const string PrefabPath =
            "Assets/Norscope/UI/Panels/SettingsPanel.prefab";
        private const string BaselinePath =
            "Assets/Norscope/Editor/Validation/SettingsInteractionRepair/Baseline.json";
        private const string BackupPreference =
            "Norscope.Settings.InteractionRepair.Backup";
        private const string CaptureRequest =
            "Temp/NorscopeSettingsInteraction.capture";
        private const string DryRunRequest =
            "Temp/NorscopeSettingsInteraction.dryrun";
        private const string ApplyRequest =
            "Temp/NorscopeSettingsInteraction.apply";
        private const string ValidateRequest =
            "Temp/NorscopeSettingsInteraction.validate";

        private static readonly ControlSpec[] Controls =
        {
            C("Language sidebar",
                "FigmaContent/SettingsView/Body/Sidebar/LanguageButton",
                true, "SettingsViewNavigator.OpenLanguagePanel"),
            C("Audio sidebar",
                "FigmaContent/SettingsView/Body/Sidebar/AudioButton",
                true, "SettingsViewNavigator.ShowSection(Audio)"),
            C("About sidebar",
                "FigmaContent/SettingsView/Body/Sidebar/AboutButton",
                true, "SettingsViewNavigator.ShowSection(About)"),
            C("Open Language",
                "FigmaContent/SettingsView/Body/Detail/LanguageQuickView/ChangeLanguageButton",
                true, "SettingsViewNavigator.OpenLanguagePanel"),
            C("Internal Back",
                "FigmaContent/LanguageView/TopBar/InternalBackButton",
                true, "SettingsViewNavigator.BackToSettings"),
            C("External Back",
                "FigmaContent/SettingsView/TopBar/BackButton",
                true, "PanelNavigationBinding.NavigateBack"),
            C("Close", "TitleBar/CloseButton", true,
                "SpatialWindow.Close")
        };

        private static readonly string[] DeferredNames =
        {
            "EnglishCard", "GermanCard", "MasterVolumeCard",
            "AlertSoundsToggle", "VoiceGuidanceCard",
            "VoiceGuidanceToggle", "MicrophoneToggle"
        };

        [Serializable]
        private sealed class Baseline
        {
            public int schemaVersion = 1;
            public string capturedUtc;
            public string prefabHash;
            public string backupPath;
        }

        private sealed class ControlSpec
        {
            public string label;
            public string path;
            public bool shouldInteract;
            public string runtimeBinding;
        }

        [MenuItem(Menu + "1) Capture Interaction Baseline")]
        public static void CaptureMenu()
        {
            if (!File.Exists(PrefabPath))
            {
                Debug.LogError("[Settings Interaction Repair] Baseline BLOCKED: " +
                               PrefabPath + " is missing.");
                return;
            }
            string stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            string folder =
                "Assets/Norscope/UI/Panels/_Backups/SettingsInteraction_" + stamp;
            Directory.CreateDirectory(folder);
            string backup = folder + "/SettingsPanel.prefab";
            File.Copy(PrefabPath, backup, true);
            Directory.CreateDirectory(
                Path.GetDirectoryName(BaselinePath) ??
                "Assets/Norscope/Editor/Validation");
            var baseline = new Baseline
            {
                capturedUtc = DateTime.UtcNow.ToString("O"),
                prefabHash = Hash(PrefabPath),
                backupPath = backup
            };
            File.WriteAllText(BaselinePath, JsonUtility.ToJson(baseline, true),
                new UTF8Encoding(false));
            EditorPrefs.SetString(BackupPreference, backup);
            AssetDatabase.Refresh();
            Debug.Log("[Settings Interaction Repair] Baseline CAPTURED\n" +
                      "Prefab hash: " + baseline.prefabHash + "\nBackup: " + backup);
        }

        [MenuItem(Menu + "2) Dry Run")]
        public static void DryRunMenu()
        {
            bool ready = BuildDryRun(out string report);
            Debug.Log((ready
                    ? "[Settings Interaction Repair] Dry run READY\n"
                    : "[Settings Interaction Repair] Dry run BLOCKED\n") +
                report);
        }

        [MenuItem(Menu + "3) Apply")]
        public static void ApplyMenu()
        {
            if (!BuildDryRun(out string report))
            {
                Debug.LogError("[Settings Interaction Repair] Apply BLOCKED\n" + report);
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                Canvas canvas = root.GetComponent<Canvas>();
                GraphicRaycaster standard = root.GetComponent<GraphicRaycaster>();
                TrackedDeviceGraphicRaycaster[] existing =
                    root.GetComponents<TrackedDeviceGraphicRaycaster>();
                if (canvas == null || standard == null || existing.Length != 0)
                {
                    Debug.LogError(
                        "[Settings Interaction Repair] Apply BLOCKED: expected one " +
                        "Canvas, one existing GraphicRaycaster and zero tracked raycasters.");
                    return;
                }

                root.AddComponent<TrackedDeviceGraphicRaycaster>();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                "[Settings Interaction Repair] Apply COMPLETE\n" +
                "Added exactly one TrackedDeviceGraphicRaycaster to SettingsPanel root.\n" +
                "No hierarchy object, Button, listener, layout or visual property was changed.");
        }

        [MenuItem(Menu + "4) Validate")]
        public static void ValidateMenu()
        {
            var failures = new List<string>();
            var passes = new List<string>();
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Check(prefab != null, "SettingsPanel.prefab exists", failures, passes);
            if (prefab != null)
            {
                Check(prefab.GetComponents<Canvas>().Length == 1,
                    "Exactly one root Canvas", failures, passes);
                Check(prefab.GetComponents<GraphicRaycaster>().Length == 1,
                    "Existing GraphicRaycaster preserved", failures, passes);
                Check(prefab.GetComponents<TrackedDeviceGraphicRaycaster>().Length == 1,
                    "Exactly one TrackedDeviceGraphicRaycaster", failures, passes);
                CanvasScaler scaler = prefab.GetComponent<CanvasScaler>();
                SerializedObject scalerSo =
                    scaler != null ? new SerializedObject(scaler) : null;
                SerializedProperty worldPreset = scalerSo != null
                    ? scalerSo.FindProperty("m_PresetInfoIsWorld")
                    : null;
                Check(worldPreset != null && !worldPreset.boolValue,
                    "CanvasScaler.m_PresetInfoIsWorld remains 0",
                    failures, passes);
                foreach (ControlSpec spec in Controls)
                {
                    Transform target = prefab.transform.Find(spec.path);
                    Button button = target != null ? target.GetComponent<Button>() : null;
                    Check(button != null && button.interactable &&
                          button.targetGraphic != null &&
                          button.targetGraphic.raycastTarget,
                        spec.label + " remains interactable with targetGraphic",
                        failures, passes);
                }
                Check(NavigatorAssigned(prefab.GetComponent<SettingsViewNavigator>()),
                    "All SettingsViewNavigator references remain assigned",
                    failures, passes);
                SpatialWindowBindings bindings =
                    prefab.GetComponent<SpatialWindowBindings>();
                Check(bindings != null && bindings.CloseButton != null &&
                      bindings.GrabRegion != null && bindings.ResizeHandle != null,
                    "Close, GrabRegion and resize bindings remain assigned",
                    failures, passes);
                Check(DeferredControlsDisabled(prefab),
                    "All deferred controls remain individually disabled",
                    failures, passes);
                Check(NoUnexpectedRaycastTargets(prefab),
                    "No invisible decorative Graphic intercepts input",
                    failures, passes);
            }

            Check(!EditorUtility.scriptCompilationFailed,
                "Unity compiles with zero C# errors", failures, passes);
            ValidateBootstrapInput(failures, passes);

            bool live = EditorApplication.isPlaying &&
                        UnityEngine.Object.FindObjectsByType<SettingsViewNavigator>(
                            FindObjectsInactive.Include, FindObjectsSortMode.None)
                            .Any(value => !EditorUtility.IsPersistent(value));
            if (live)
                passes.Add("Live Settings instance detected in Play Mode");
            else
                failures.Add(
                    "Actual Play Mode mouse/XRI interaction pass not executed: " +
                    "no live Settings instance is currently open");

            StringBuilder output = new StringBuilder();
            foreach (string pass in passes) output.AppendLine("PASS  " + pass);
            foreach (string failure in failures) output.AppendLine("PENDING/FAIL  " + failure);
            output.AppendLine(
                "MANUAL REQUIRED  Open Settings and test Language, Audio, " +
                "About, Open Language, Internal Back, External Back and Close with mouse.");
            output.AppendLine(
                "HEADSET REQUIRED  Repeat the same controls with HoloLens/XRI pointer; " +
                "also verify Grab and Resize.");
            output.AppendLine(
                "REOPEN TEST  Open/close Settings five times and verify each click " +
                "performs one transition.");
            Debug.Log((failures.Count == 0
                    ? "[Settings Interaction Repair] Validation PASSED\n"
                    : "[Settings Interaction Repair] Validation INCOMPLETE\n") +
                output);
        }

        [MenuItem(Menu + "5) Restore Baseline")]
        public static void RestoreMenu()
        {
            var blockers = new List<string>();
            Baseline baseline = LoadBaseline(blockers);
            if (baseline == null || blockers.Count > 0)
            {
                Debug.LogError("[Settings Interaction Repair] Restore BLOCKED\n" +
                               string.Join("\n", blockers));
                return;
            }
            if (!EditorUtility.DisplayDialog(
                    "Restore Settings interaction baseline?",
                    "Restore SettingsPanel.prefab exactly to its pre-repair backup?",
                    "Restore", "Cancel"))
                return;
            File.Copy(baseline.backupPath, PrefabPath, true);
            AssetDatabase.Refresh();
            Debug.Log("[Settings Interaction Repair] Baseline RESTORED\n" +
                      "SettingsPanel hash: " + Hash(PrefabPath));
        }

        [InitializeOnLoadMethod]
        private static void ProcessPreparationRequests()
        {
            if (!File.Exists(CaptureRequest) && !File.Exists(DryRunRequest) &&
                !File.Exists(ApplyRequest) && !File.Exists(ValidateRequest))
                return;
            EditorApplication.delayCall += () =>
            {
                if (File.Exists(CaptureRequest))
                {
                    File.Delete(CaptureRequest);
                    CaptureMenu();
                }
                if (File.Exists(DryRunRequest))
                {
                    File.Delete(DryRunRequest);
                    DryRunMenu();
                }
                if (File.Exists(ApplyRequest))
                {
                    File.Delete(ApplyRequest);
                    ApplyMenu();
                    File.WriteAllText(ValidateRequest, "validate");
                }
                if (File.Exists(ValidateRequest))
                {
                    File.Delete(ValidateRequest);
                    ValidateMenu();
                }
            };
        }

        private static bool BuildDryRun(out string report)
        {
            var blockers = new List<string>();
            var lines = new StringBuilder(16000);
            Baseline baseline = LoadBaseline(blockers);
            if (baseline != null && Hash(PrefabPath) != baseline.prefabHash)
                blockers.Add("SettingsPanel.prefab changed after baseline capture.");

            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                blockers.Add("SettingsPanel.prefab cannot be loaded.");
                report = string.Join("\n", blockers);
                return false;
            }

            Canvas canvas = prefab.GetComponent<Canvas>();
            GraphicRaycaster mouseRaycaster = prefab.GetComponent<GraphicRaycaster>();
            TrackedDeviceGraphicRaycaster tracked =
                prefab.GetComponent<TrackedDeviceGraphicRaycaster>();
            lines.AppendLine("ROOT CAUSE");
            lines.AppendLine("- Settings owns a nested world-space Canvas.");
            lines.AppendLine("- Standard GraphicRaycaster: " +
                             Present(mouseRaycaster));
            lines.AppendLine("- TrackedDeviceGraphicRaycaster on that Canvas: " +
                             Present(tracked));
            lines.AppendLine("- The outer SpatialWindow canvas has an XRI raycaster, " +
                             "but nested-canvas graphics require the raycaster on " +
                             "their owning Canvas for reliable tracked-pointer hits.");
            lines.AppendLine("- Planned repair: add exactly one " +
                             "TrackedDeviceGraphicRaycaster to the existing Settings root.");
            if (canvas == null || mouseRaycaster == null)
                blockers.Add("Settings root Canvas or GraphicRaycaster is missing.");
            if (tracked != null)
                blockers.Add("TrackedDeviceGraphicRaycaster already exists; reported " +
                             "failure has a different root cause.");

            lines.AppendLine();
            lines.AppendLine("INTENDED CONTROL INVENTORY");
            foreach (ControlSpec spec in Controls)
                AppendControl(lines, prefab, spec, blockers);

            SettingsViewNavigator navigator =
                prefab.GetComponent<SettingsViewNavigator>();
            SettingsPanel panel = prefab.GetComponent<SettingsPanel>();
            SpatialWindowBindings bindings =
                prefab.GetComponent<SpatialWindowBindings>();
            NorscopePanelRoot panelRoot = prefab.GetComponent<NorscopePanelRoot>();
            lines.AppendLine();
            lines.AppendLine("SERIALIZED OWNERS");
            lines.AppendLine("- SettingsViewNavigator: " + Present(navigator));
            lines.AppendLine("- SettingsPanel: " + Present(panel));
            lines.AppendLine("- NorscopePanelRoot: " + Present(panelRoot));
            lines.AppendLine("- SpatialWindowBindings: " + Present(bindings));
            lines.AppendLine("- Close/Grab/Resize assigned: " +
                (bindings != null && bindings.CloseButton != null &&
                 bindings.GrabRegion != null && bindings.ResizeHandle != null));
            if (!NavigatorAssigned(navigator))
                blockers.Add("One or more SettingsViewNavigator references are missing.");

            lines.AppendLine();
            lines.AppendLine("RAYCAST AUDIT");
            foreach (Graphic graphic in prefab.GetComponentsInChildren<Graphic>(true))
            {
                if (!graphic.raycastTarget) continue;
                Selectable selectable = graphic.GetComponent<Selectable>();
                bool intended = selectable != null &&
                                selectable.targetGraphic == graphic;
                bool spatialHandle = graphic.name == "GrabRegion" ||
                                     graphic.name == "BottomRight";
                lines.AppendLine("- " + PathOf(graphic.transform, prefab.transform) +
                                 " | intended=" + (intended || spatialHandle));
                if (!intended && !spatialHandle)
                    blockers.Add("Unexpected raycast target: " +
                                 PathOf(graphic.transform, prefab.transform));
            }
            lines.AppendLine("- Full-screen Background raycastTarget: " +
                Raycast(prefab.transform.Find("Background")));
            lines.AppendLine("- Full-screen Border raycastTarget: " +
                Raycast(prefab.transform.Find("Border")));
            lines.AppendLine("- CanvasGroups found: " +
                             prefab.GetComponentsInChildren<CanvasGroup>(true).Length);

            lines.AppendLine();
            lines.AppendLine("DEFERRED CONTROLS");
            foreach (string name in DeferredNames)
            {
                Selectable[] matches = prefab.GetComponentsInChildren<Selectable>(true)
                    .Where(value => value.name == name).ToArray();
                if (matches.Length == 0 && (name == "MasterVolumeCard" ||
                                           name == "VoiceGuidanceCard"))
                {
                    lines.AppendLine("- " + name +
                                     ": visual container; child controls are in deferred array");
                    continue;
                }
                foreach (Selectable value in matches)
                {
                    lines.AppendLine("- " + name + ": interactable=" +
                                     value.interactable);
                    if (value.interactable)
                        blockers.Add("Deferred control is interactable: " + name);
                }
            }

            AppendSceneInput(lines);
            AppendRuntimeInstance(lines, prefab);

            lines.AppendLine();
            lines.AppendLine("EXACT PLANNED CHANGES");
            lines.AppendLine("- Modify: " + PrefabPath);
            lines.AppendLine("- Add component: TrackedDeviceGraphicRaycaster on root Canvas");
            lines.AppendLine("- Button additions: 0");
            lines.AppendLine("- Listener changes: 0");
            lines.AppendLine("- raycastTarget changes: 0");
            lines.AppendLine("- CanvasGroup changes: 0");
            lines.AppendLine("- RectTransform/hierarchy/visual changes: 0");
            lines.AppendLine("- Runtime source changes: 0");
            lines.AppendLine("- Editor tooling only: " +
                             "Assets/Norscope/Editor/PhaseSettingsInteractionRepairConfigurator.cs");
            lines.AppendLine("- Localization is not applied or modified.");
            lines.AppendLine("- Audio, text size, TTS, microphone, workflow, reports, " +
                             "navigation architecture and SpatialWindow code are untouched.");

            foreach (string blocker in blockers)
                lines.AppendLine("BLOCKER  " + blocker);
            lines.AppendLine(blockers.Count == 0
                ? "READY - Apply remains locked pending approval."
                : "BLOCKED - do not apply.");
            report = lines.ToString();
            return blockers.Count == 0;
        }

        private static void AppendControl(StringBuilder lines, GameObject root,
            ControlSpec spec, List<string> blockers)
        {
            Transform target = root.transform.Find(spec.path);
            Button button = target != null ? target.GetComponent<Button>() : null;
            Graphic graphic = button != null ? button.targetGraphic : null;
            Canvas owner = target != null ? target.GetComponentInParent<Canvas>(true) : null;
            CanvasGroup[] groups = target != null
                ? target.GetComponentsInParent<CanvasGroup>(true)
                : Array.Empty<CanvasGroup>();
            lines.AppendLine(spec.label + " :: " + spec.path);
            lines.AppendLine("  Button=" + Present(button) +
                             " interactable=" + (button != null && button.interactable));
            lines.AppendLine("  targetGraphic=" +
                             (graphic != null ? graphic.name : "<missing>") +
                             " raycastTarget=" + (graphic != null && graphic.raycastTarget));
            lines.AppendLine("  persistentListeners=" +
                             (button != null
                                 ? button.onClick.GetPersistentEventCount()
                                 : -1) +
                             " runtimeBinding=" + spec.runtimeBinding);
            lines.AppendLine("  owningCanvas=" +
                             (owner != null ? owner.name : "<missing>") +
                             " GraphicRaycaster=" +
                             (owner != null &&
                              owner.GetComponent<GraphicRaycaster>() != null) +
                             " TrackedDeviceGraphicRaycaster=" +
                             (owner != null &&
                              owner.GetComponent<TrackedDeviceGraphicRaycaster>() != null));
            lines.AppendLine("  parentCanvasGroups=" + groups.Length +
                             (groups.Length == 0 ? string.Empty :
                                 " [" + string.Join(", ", groups.Select(value =>
                                     "interactable=" + value.interactable +
                                     "/blocks=" + value.blocksRaycasts)) + "]"));
            if (target == null || button == null || !button.interactable ||
                graphic == null || !graphic.raycastTarget)
                blockers.Add(spec.label + " serialized interaction target is invalid.");
        }

        private static void AppendSceneInput(StringBuilder lines)
        {
            const string bootstrapPath =
                "Assets/Norscope/Scenes/Bootstrap.unity";
            Scene bootstrap = SceneManager.GetSceneByPath(bootstrapPath);
            bool openedForAudit = !bootstrap.IsValid() || !bootstrap.isLoaded;
            if (openedForAudit)
                bootstrap = EditorSceneManager.OpenScene(
                    bootstrapPath, OpenSceneMode.Additive);
            try
            {
                EventSystem eventSystem = bootstrap.GetRootGameObjects()
                    .SelectMany(root =>
                        root.GetComponentsInChildren<EventSystem>(true))
                    .FirstOrDefault();
                XRUIInputModule xr = eventSystem != null
                    ? eventSystem.GetComponent<XRUIInputModule>()
                    : null;
                lines.AppendLine();
                lines.AppendLine("SCENE INPUT STACK (Bootstrap asset)");
                lines.AppendLine("- EventSystem: " + Present(eventSystem));
                lines.AppendLine("- XRUIInputModule: " + Present(xr));
                lines.AppendLine("- XR input enabled: " +
                    (xr != null && xr.enableXRInput));
                lines.AppendLine("- Mouse input enabled: " +
                    (xr != null && xr.enableMouseInput));
                lines.AppendLine("- Existing outer SpatialWindowSpawner creates both " +
                                 "GraphicRaycaster and TrackedDeviceGraphicRaycaster.");
            }
            finally
            {
                if (openedForAudit && bootstrap.IsValid())
                    EditorSceneManager.CloseScene(bootstrap, true);
            }
        }

        private static void AppendRuntimeInstance(StringBuilder lines,
            GameObject prefab)
        {
            SettingsViewNavigator[] navigators =
                UnityEngine.Object.FindObjectsByType<SettingsViewNavigator>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);
            SettingsViewNavigator runtime = navigators.FirstOrDefault(value =>
                !EditorUtility.IsPersistent(value));
            lines.AppendLine();
            lines.AppendLine("PLAY-MODE INSTANCE");
            lines.AppendLine("- Editor is playing: " + EditorApplication.isPlaying);
            lines.AppendLine("- Live SettingsViewNavigator instance: " +
                             Present(runtime));
            if (runtime != null)
            {
                Canvas canvas = runtime.GetComponent<Canvas>();
                lines.AppendLine("- Live root Canvas: " + Present(canvas));
                lines.AppendLine("- Live root TrackedDeviceGraphicRaycaster: " +
                    Present(canvas != null
                        ? canvas.GetComponent<TrackedDeviceGraphicRaycaster>()
                        : null));
                lines.AppendLine("- Serialized navigator references: " +
                                 NavigatorAssigned(runtime));
            }
            lines.AppendLine("- Automated mouse/XRI clicks are intentionally deferred " +
                             "to post-Apply validation; Dry Run does not invoke actions.");
        }

        private static bool NavigatorAssigned(SettingsViewNavigator navigator)
        {
            if (navigator == null) return false;
            SerializedObject so = new SerializedObject(navigator);
            string[] names =
            {
                "settingsView", "languageView", "languageQuickView", "audioView",
                "aboutView", "languageButton",
                "languageQuickButton", "audioButton",
                "aboutButton", "languageInternalBackButton"
                // displayView/displayButton are legacy optional slots and may be null
            };
            return names.All(name =>
            {
                SerializedProperty property = so.FindProperty(name);
                return property != null && property.objectReferenceValue != null;
            });
        }

        private static bool DeferredControlsDisabled(GameObject prefab)
        {
            SettingsPanel panel = prefab.GetComponent<SettingsPanel>();
            if (panel == null) return false;
            SerializedObject so = new SerializedObject(panel);
            SerializedProperty controls = so.FindProperty("deferredControls");
            if (controls == null || controls.arraySize == 0) return false;
            for (int i = 0; i < controls.arraySize; i++)
            {
                Selectable selectable = controls.GetArrayElementAtIndex(i)
                    .objectReferenceValue as Selectable;
                if (selectable == null || selectable.interactable) return false;
            }
            return true;
        }

        private static bool NoUnexpectedRaycastTargets(GameObject prefab)
        {
            foreach (Graphic graphic in
                     prefab.GetComponentsInChildren<Graphic>(true))
            {
                if (!graphic.raycastTarget) continue;
                Selectable selectable = graphic.GetComponent<Selectable>();
                if (selectable != null && selectable.targetGraphic == graphic)
                    continue;
                if (graphic.name == "GrabRegion" || graphic.name == "BottomRight")
                    continue;
                return false;
            }
            return true;
        }

        private static void ValidateBootstrapInput(
            List<string> failures, List<string> passes)
        {
            const string path = "Assets/Norscope/Scenes/Bootstrap.unity";
            Scene scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened)
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                EventSystem eventSystem = scene.GetRootGameObjects()
                    .SelectMany(root =>
                        root.GetComponentsInChildren<EventSystem>(true))
                    .FirstOrDefault();
                XRUIInputModule input = eventSystem != null
                    ? eventSystem.GetComponent<XRUIInputModule>()
                    : null;
                Check(eventSystem != null && input != null &&
                      input.enableXRInput && input.enableMouseInput,
                    "Bootstrap EventSystem supports mouse and XR UI input",
                    failures, passes);
            }
            finally
            {
                if (opened && scene.IsValid())
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void Check(bool condition, string message,
            List<string> failures, List<string> passes)
        {
            (condition ? passes : failures).Add(message);
        }

        private static Baseline LoadBaseline(List<string> blockers)
        {
            if (!File.Exists(BaselinePath))
            {
                blockers.Add("Capture Interaction Baseline has not been run.");
                return null;
            }
            try
            {
                Baseline value =
                    JsonUtility.FromJson<Baseline>(File.ReadAllText(BaselinePath));
                if (value == null || value.schemaVersion != 1 ||
                    !File.Exists(value.backupPath))
                    blockers.Add("Interaction baseline or backup is invalid.");
                return value;
            }
            catch (Exception exception)
            {
                blockers.Add("Baseline read failed: " + exception.Message);
                return null;
            }
        }

        private static string Hash(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream))
                    .Replace("-", string.Empty).ToLowerInvariant();
        }

        private static string PathOf(Transform value, Transform root)
        {
            var names = new Stack<string>();
            while (value != null && value != root)
            {
                names.Push(value.name);
                value = value.parent;
            }
            return string.Join("/", names);
        }

        private static string Raycast(Transform value)
        {
            Graphic graphic = value != null ? value.GetComponent<Graphic>() : null;
            return graphic != null ? graphic.raycastTarget.ToString() : "<missing>";
        }

        private static string Present(UnityEngine.Object value) =>
            value != null ? "present" : "missing";

        private static ControlSpec C(string label, string path,
            bool shouldInteract, string binding) =>
            new ControlSpec
            {
                label = label,
                path = path,
                shouldInteract = shouldInteract,
                runtimeBinding = binding
            };
    }
}
