using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using Unity.XR.CoreUtils;
using Norscope.Windows;

namespace Norscope.Editor
{
    /// <summary>
    /// Phase 4: wire SpatialWindow system into Bootstrap and validate.
    /// Does not rebuild InteractionTestPanel hierarchy.
    /// </summary>
    public static class Phase4SpatialWindowConfigurator
    {
        private const string BootstrapScenePath = "Assets/Norscope/Scenes/Bootstrap.unity";
        private const string InteractionTestPanelPath = "Assets/Norscope/UI/Panels/InteractionTestPanel.prefab";
        private const string ConfigAssetPath = "Assets/Norscope/Runtime/Windows/SpatialWindowConfig.asset";
        private const string WindowRootName = "SpatialWindowSystem";

        [InitializeOnLoadMethod]
        private static void AutoApplyFromFlag()
        {
            const string flag = "Assets/Norscope/Editor/.apply_phase4_spatial_window";

            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredEditMode && File.Exists(flag))
                {
                    EditorApplication.delayCall += TryApplyFromFlag;
                }
            };

            if (!File.Exists(flag))
            {
                return;
            }

            EditorApplication.delayCall += TryApplyFromFlag;
        }

        private static void TryApplyFromFlag()
        {
            const string flag = "Assets/Norscope/Editor/.apply_phase4_spatial_window";
            if (!File.Exists(flag))
            {
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.Log("[Norscope Phase4] Waiting for Play Mode exit before applying spatial window setup.");
                return;
            }

            try { File.Delete(flag); } catch { /* ignore */ }

            try
            {
                Apply();
                ValidateMenu();
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[Norscope Phase4] Auto-apply failed:\n" + ex);
            }
        }

        [MenuItem("Norscope/HoloLens/Apply Phase 4 Spatial Window")]
        public static void Apply()
        {
            EnsureConfigAsset();
            ApplyToBootstrapScene();
            AssetDatabase.SaveAssets();
            Debug.Log("[Norscope Phase4] Spatial window system applied to Bootstrap.");
        }

        [MenuItem("Norscope/HoloLens/Validate Spatial Window")]
        public static void ValidateMenu()
        {
            bool ok = Validate(out string report);
            Debug.Log(ok
                ? "[Norscope Phase4] Validate Spatial Window PASSED\n" + report
                : "[Norscope Phase4] Validate Spatial Window FAILED\n" + report);
            if (!ok)
            {
                EditorUtility.DisplayDialog("Validate Spatial Window", "FAILED\n\n" + report, "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Validate Spatial Window", "PASSED\n\n" + report, "OK");
            }
        }

        public static SpatialWindowConfig EnsureConfigAsset()
        {
            SpatialWindowConfig config = AssetDatabase.LoadAssetAtPath<SpatialWindowConfig>(ConfigAssetPath);
            if (config != null)
            {
                return config;
            }

            string dir = Path.GetDirectoryName(ConfigAssetPath)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir))
            {
                // Runtime/Windows should already exist for scripts.
            }

            config = ScriptableObject.CreateInstance<SpatialWindowConfig>();
            config.spawnDistanceMeters = 1.0f;
            config.verticalOffsetMeters = -0.10f;
            config.horizontalOffsetMeters = 0f;
            config.defaultWidthMeters = 0.55f;
            config.defaultHeightMeters = 0.38f;
            config.pixelsPerMeter = 1000f;
            config.minimumScale = 0.40f / 0.55f;
            config.maximumScale = 0.80f / 0.55f;
            config.preserveAspectRatio = true;
            config.resizeSensitivity = 1.0f;
            config.handlesVisible = true;
            config.handleWorldSizeMeters = 0.025f;
            config.preserveScaleOnRecenter = true;
            config.resetScaleOnRecenter = false;
            config.recenterDistanceMeters = 1.0f;
            config.recenterVerticalOffsetMeters = -0.10f;
            AssetDatabase.CreateAsset(config, ConfigAssetPath);
            AssetDatabase.SaveAssets();
            return config;
        }

        private static void ApplyToBootstrapScene()
        {
            Scene scene = EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);
            SpatialWindowConfig config = EnsureConfigAsset();
            GameObject panelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(InteractionTestPanelPath);
            if (panelPrefab == null)
            {
                throw new FileNotFoundException("InteractionTestPanel prefab missing.", InteractionTestPanelPath);
            }

            // Remove scene-placed panel instance so SpatialWindowManager owns the only runtime copy.
            // Prefab asset at Assets/Norscope/UI/Panels/InteractionTestPanel.prefab is untouched.
            GameObject scenePanel = GameObject.Find("InteractionTestPanel");
            if (scenePanel == null)
            {
                scenePanel = FindIncludingInactive("InteractionTestPanel");
            }

            if (scenePanel != null)
            {
                Undo.DestroyObjectImmediate(scenePanel);
            }

            GameObject system = GameObject.Find(WindowRootName);
            if (system == null)
            {
                system = new GameObject(WindowRootName);
            }

            SpatialWindowSpawner spawner = system.GetComponent<SpatialWindowSpawner>();
            if (spawner == null)
            {
                spawner = system.AddComponent<SpatialWindowSpawner>();
            }

            SpatialWindowManager manager = system.GetComponent<SpatialWindowManager>();
            if (manager == null)
            {
                manager = system.AddComponent<SpatialWindowManager>();
            }

            TemporarySpatialWindowLauncher launcher = system.GetComponent<TemporarySpatialWindowLauncher>();
            if (launcher == null)
            {
                launcher = system.AddComponent<TemporarySpatialWindowLauncher>();
            }

            spawner.SetReferences(config, panelPrefab);
            manager.SetReferences(spawner, config);
            EditorUtility.SetDirty(spawner);
            EditorUtility.SetDirty(manager);

            SerializedObject soSpawner = new SerializedObject(spawner);
            soSpawner.FindProperty("config").objectReferenceValue = config;
            soSpawner.FindProperty("interactionTestPanelPrefab").objectReferenceValue = panelPrefab;
            soSpawner.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject soManager = new SerializedObject(manager);
            soManager.FindProperty("spawner").objectReferenceValue = spawner;
            soManager.FindProperty("config").objectReferenceValue = config;
            soManager.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject soLauncher = new SerializedObject(launcher);
            soLauncher.FindProperty("manager").objectReferenceValue = manager;
            soLauncher.FindProperty("targetId").enumValueIndex = (int)SpatialWindowId.InteractionTestPanel;
            soLauncher.FindProperty("createWorldButtons").boolValue = true;
            SerializedProperty openOnStart = soLauncher.FindProperty("openOnStart");
            if (openOnStart != null)
            {
                openOnStart.boolValue = true;
            }

            soLauncher.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(system);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        public static bool Validate(out string report)
        {
            var sb = new StringBuilder();
            int fails = 0;
            Scene scene = EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);

            void Check(bool ok, string label)
            {
                sb.Append(ok ? "PASS" : "FAIL").Append(" — ").AppendLine(label);
                if (!ok)
                {
                    fails++;
                }
            }

            Check(Object.FindObjectsByType<XROrigin>(FindObjectsSortMode.None).Length == 1, "exactly one XR Origin");
            Check(Object.FindObjectsByType<XRInteractionManager>(FindObjectsSortMode.None).Length == 1, "exactly one XR Interaction Manager");
            Check(Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length == 1, "exactly one EventSystem");

            GameObject panelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(InteractionTestPanelPath);
            Check(panelPrefab != null, "InteractionTestPanel prefab reference exists");

            SpatialWindowConfig config = AssetDatabase.LoadAssetAtPath<SpatialWindowConfig>(ConfigAssetPath);
            Check(config != null, "SpatialWindowConfig asset exists");

            SpatialWindowManager manager = Object.FindFirstObjectByType<SpatialWindowManager>();
            SpatialWindowSpawner spawner = Object.FindFirstObjectByType<SpatialWindowSpawner>();
            Check(manager != null, "SpatialWindowManager in Bootstrap");
            Check(spawner != null, "SpatialWindowSpawner in Bootstrap");
            Check(Object.FindObjectsByType<SpatialWindowManager>(FindObjectsSortMode.None).Length <= 1, "no duplicate SpatialWindowManager");

            // Prefab structure (source of truth — do not rebuild).
            if (panelPrefab != null)
            {
                Transform grab = FindDeep(panelPrefab.transform, "GrabRegion");
                Transform resize = FindDeep(panelPrefab.transform, "BottomRight");
                Transform close = FindDeep(panelPrefab.transform, "CloseButton");
                Check(grab != null, "one grab region reference (GrabRegion)");
                Check(close != null && close.GetComponent<Button>() != null, "one close button reference");
                Check(resize != null, "resize handles assigned (BottomRight)");

                bool grabReady = grab != null && (grab.GetComponent<Graphic>() != null || grab.GetComponent<Collider>() != null);
                bool resizeReady = resize != null && (resize.GetComponent<Graphic>() != null || resize.GetComponent<Collider>() != null);
                Check(grabReady, "grab region has raycast/collider target");
                Check(resizeReady, "resize handle has raycast/collider target");
                Check(close != null && close.GetComponent<Graphic>() != null && close.GetComponent<Graphic>().raycastTarget, "close button raycast target");

                Norscope.UI.NorscopePanelRoot panelRoot = panelPrefab.GetComponent<Norscope.UI.NorscopePanelRoot>();
                if (panelRoot != null)
                {
                    Vector2 meters = panelRoot.SizeMeters;
                    Check(Mathf.Abs(meters.x - 0.55f) < 0.02f && Mathf.Abs(meters.y - 0.38f) < 0.02f,
                        "panel dimensions convert correctly to metres (~0.55×0.38)");
                }
                else
                {
                    RectTransform rt = panelPrefab.GetComponent<RectTransform>();
                    float ppm = config != null ? config.pixelsPerMeter : 1000f;
                    float w = rt != null ? rt.sizeDelta.x / ppm : 0f;
                    float h = rt != null ? rt.sizeDelta.y / ppm : 0f;
                    Check(Mathf.Abs(w - 0.55f) < 0.05f && Mathf.Abs(h - 0.38f) < 0.05f,
                        "panel dimensions convert correctly to metres (~0.55×0.38)");
                }
            }

            if (config != null)
            {
                Check(Mathf.Abs(config.spawnDistanceMeters - 1.0f) < 0.15f, "spawn distance ~1.0 m");
                Check(config.MinWidthMeters <= 0.41f && config.MaxWidthMeters >= 0.79f, "min/max width constraints (~0.40–0.80 m)");
            }

            // No Resources duplicate of the panel.
            string[] resourcePanels = AssetDatabase.FindAssets("InteractionTestPanel t:Prefab", new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.Replace('\\', '/').Contains("/Resources/"))
                .ToArray();
            Check(resourcePanels.Length == 0, "no Resources duplicate of InteractionTestPanel");

            // Prefab path uniqueness (single source).
            string[] allPanels = AssetDatabase.FindAssets("InteractionTestPanel t:Prefab", new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => Path.GetFileNameWithoutExtension(p) == "InteractionTestPanel")
                .ToArray();
            Check(allPanels.Length == 1 && allPanels[0].Replace('\\', '/') == InteractionTestPanelPath,
                "exactly one InteractionTestPanel prefab at source path");

            // Window ID uniqueness in scene managers (runtime dict is code-enforced).
            TemporarySpatialWindowLauncher[] launchers = Object.FindObjectsByType<TemporarySpatialWindowLauncher>(FindObjectsSortMode.None);
            Check(launchers.Length <= 1, "no duplicate temporary launchers / window IDs");

            // Missing scripts.
            bool missing = scene.GetRootGameObjects().Any(root =>
                root.GetComponentsInChildren<Component>(true).Any(c => c == null));
            Check(!missing, "no missing scripts");

            // Magenta materials in Bootstrap (common error shader).
            bool noMagenta = true;
            foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (r.sharedMaterial != null && r.sharedMaterial.shader != null &&
                    r.sharedMaterial.shader.name.Contains("InternalError"))
                {
                    noMagenta = false;
                    break;
                }
            }

            Check(noMagenta, "no magenta materials");

            GameObject scenePanel = FindIncludingInactive("InteractionTestPanel");
            Check(scenePanel == null || !scenePanel.activeInHierarchy,
                "no always-on scene InteractionTestPanel (manager-owned open)");

            Check(!Directory.Exists(Path.Combine(Application.dataPath, "Norscope/UI/Generated")),
                "no generated runtime UI folder");

            Check(Object.FindFirstObjectByType<TemporarySpatialWindowLauncher>() != null,
                "temporary open/close/recenter control present");

            report = sb.ToString();
            return fails == 0;
        }

        private static GameObject FindIncludingInactive(string name)
        {
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                Transform t = FindDeep(root.transform, name);
                if (t != null)
                {
                    return t.gameObject;
                }
            }

            return null;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root == null)
            {
                return null;
            }

            if (root.name == name)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeep(root.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
