using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Unity.XR.CoreUtils;
using Unity.XR.CoreUtils.Editor;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactables.Visuals;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
using UnityEditor.XR.Management;

namespace Norscope.Editor
{
    /// <summary>
    /// Phase 3: Official XRI + XR Hands interaction foundation only.
    /// </summary>
    public static class Phase3HandInteractionConfigurator
    {
        private const string BootstrapScenePath = "Assets/Norscope/Scenes/Bootstrap.unity";
        private const string BuildOutputPath = "Builds/UWP";
        private const string XrOriginPrefabPath =
            "Assets/Samples/XR Interaction Toolkit/3.2.1/Hands Interaction Demo/Prefabs/XR Origin Hands (XR Rig).prefab";

        // OpenXR / MR feature ids
        private const string MixedRealityFeatureId = "com.microsoft.openxr.feature.hololens";
        private const string MrHandTrackingFeatureId = "com.microsoft.openxr.feature.handtracking";
        private const string HandTrackingSubsystemFeatureId = "com.unity.openxr.feature.input.handtracking"; // shared id; enable by type too
        private const string HandInteractionProfileFeatureId = "com.unity.openxr.feature.input.handinteraction";
        private const string AppRemotingFeatureId = "com.microsoft.openxr.feature.appremoting";
        private const string PlayModeRemotingFeatureId = "com.microsoft.openxr.feature.playmoderemoting";

        public static void ConfigureValidateAndBuild()
        {
            try
            {
                ImportRequiredSamples();
                ConfigureOpenXrHandFeatures();
                BuildHandTestScene();
                ValidateSceneAndOpenXr();
                BuildUwpArm64MasterInternal();
                Debug.Log("[Norscope Phase3] ConfigureValidateAndBuild completed.");
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError("[Norscope Phase3] Failed:\n" + ex);
                EditorApplication.Exit(1);
            }
        }

        public static void ConfigureAndValidateOnly()
        {
            try
            {
                ImportRequiredSamples();
                ConfigureOpenXrHandFeatures();
                BuildHandTestScene();
                ValidateSceneAndOpenXr();
                AssetDatabase.SaveAssets();
                Debug.Log("[Norscope Phase3] ConfigureAndValidateOnly completed.");
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError("[Norscope Phase3] Failed:\n" + ex);
                EditorApplication.Exit(1);
            }
        }

        private static void ImportRequiredSamples()
        {
            // Samples are copied into Assets/Samples (Starter Assets + Hands Interaction Demo + HandVisualizer).
            if (!File.Exists(XrOriginPrefabPath))
            {
                throw new FileNotFoundException(
                    "XR Origin Hands prefab missing. Expected at: " + XrOriginPrefabPath);
            }

            Debug.Log("[Norscope Phase3] Using XR Origin Hands prefab: " + XrOriginPrefabPath);
        }

        private static void ConfigureOpenXrHandFeatures()
        {
            FeatureHelpers.RefreshFeatures(BuildTargetGroup.WSA);

            // Keep remoting off.
            SetFeatureEnabledById(BuildTargetGroup.WSA, AppRemotingFeatureId, false);
            SetFeatureEnabledById(BuildTargetGroup.WSA, PlayModeRemotingFeatureId, false);

            // Required for HL2 immersive + hands.
            SetFeatureEnabledById(BuildTargetGroup.WSA, MixedRealityFeatureId, true);
            SetFeatureEnabledById(BuildTargetGroup.WSA, MrHandTrackingFeatureId, true);

            // Enable both types that historically share a feature id string.
            SetFeatureEnabledByTypeName(BuildTargetGroup.WSA, "HandTracking", true);          // XR Hands subsystem
            SetFeatureEnabledByTypeName(BuildTargetGroup.WSA, "MicrosoftHandInteraction", true); // MSFT hand interaction profile

            // XR_EXT_hand_interaction profile used by modern XRI hand aim/pinch paths.
            SetFeatureEnabledById(BuildTargetGroup.WSA, HandInteractionProfileFeatureId, true);

            OpenXRSettings openXr = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.WSA);
            if (openXr != null)
            {
                openXr.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;
                openXr.depthSubmissionMode = OpenXRSettings.DepthSubmissionMode.Depth16Bit;
                EditorUtility.SetDirty(openXr);
            }

            XRGeneralSettings xrSettings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.WSA);
            if (xrSettings != null)
            {
                xrSettings.InitManagerOnStart = true;
                EditorUtility.SetDirty(xrSettings);
            }

            LogEnabledOpenXrFeatures(BuildTargetGroup.WSA);
        }

        private static void SetFeatureEnabledById(BuildTargetGroup group, string featureId, bool enabled)
        {
            OpenXRFeature feature = FeatureHelpers.GetFeatureWithIdForBuildTarget(group, featureId);
            if (feature == null)
            {
                Debug.LogWarning("[Norscope Phase3] Feature id not found: " + featureId);
                return;
            }

            feature.enabled = enabled;
            EditorUtility.SetDirty(feature);
            Debug.Log("[Norscope Phase3] Feature " + feature.GetType().Name + " [" + featureId + "] => " + enabled);
        }

        private static void SetFeatureEnabledByTypeName(BuildTargetGroup group, string typeName, bool enabled)
        {
            OpenXRSettings settings = OpenXRSettings.GetSettingsForBuildTargetGroup(group);
            if (settings == null)
            {
                return;
            }

            // features array is internal; use Feature + GetFeatures reflection / FeatureHelpers refresh side effects.
            FieldInfo field = typeof(OpenXRSettings).GetField("features", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            OpenXRFeature[] features = field != null ? field.GetValue(settings) as OpenXRFeature[] : null;
            if (features == null)
            {
                // Fallback: try GetFeatures via extension method pattern used by package.
                MethodInfo getFeatures = typeof(OpenXRSettings).GetMethod("GetFeatures", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
                if (getFeatures != null)
                {
                    features = getFeatures.Invoke(settings, null) as OpenXRFeature[];
                }
            }

            if (features == null)
            {
                Debug.LogWarning("[Norscope Phase3] Could not enumerate OpenXR features for type enable: " + typeName);
                return;
            }

            bool found = false;
            foreach (OpenXRFeature feature in features)
            {
                if (feature != null && feature.GetType().Name == typeName)
                {
                    feature.enabled = enabled;
                    EditorUtility.SetDirty(feature);
                    found = true;
                    Debug.Log("[Norscope Phase3] Feature type " + typeName + " => " + enabled);
                }
            }

            if (!found)
            {
                Debug.LogWarning("[Norscope Phase3] Feature type not found: " + typeName);
            }
        }

        private static void LogEnabledOpenXrFeatures(BuildTargetGroup group)
        {
            OpenXRSettings settings = OpenXRSettings.GetSettingsForBuildTargetGroup(group);
            FieldInfo field = typeof(OpenXRSettings).GetField("features", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            OpenXRFeature[] features = field != null ? field.GetValue(settings) as OpenXRFeature[] : null;
            if (features == null)
            {
                return;
            }

            foreach (OpenXRFeature feature in features)
            {
                if (feature != null && feature.enabled)
                {
                    Debug.Log("[Norscope Phase3] ENABLED: " + feature.GetType().FullName);
                }
            }
        }

        private static void BuildHandTestScene()
        {
            Scene scene = EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);

            // Remove Phase 2 verification content and any previous Phase 3 content.
            DestroyIfExists("VerificationCube");
            DestroyIfExists("VerificationLabel");
            DestroyIfExists("XR Origin Hands (XR Rig)");
            DestroyIfExists("XR Origin");
            DestroyIfExists("HandTestCube");
            DestroyIfExists("HandTestSphere");
            DestroyIfExists("HandTestCanvas");
            DestroyIfExists("DiagnosticsPanel");
            DestroyIfExists("EventSystem");
            DestroyIfExists("XR Interaction Manager");

            // Camera under old hierarchy is replaced by XR Origin camera.
            Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            foreach (Camera cam in cameras)
            {
                if (cam != null && cam.gameObject.name == "Main Camera")
                {
                    UnityEngine.Object.DestroyImmediate(cam.gameObject);
                }
            }

            GameObject originPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(XrOriginPrefabPath);
            if (originPrefab == null)
            {
                throw new Exception("Failed to load XR Origin Hands prefab.");
            }

            GameObject origin = (GameObject)PrefabUtility.InstantiatePrefab(originPrefab);
            origin.name = "XR Origin Hands (XR Rig)";
            origin.transform.position = Vector3.zero;

            EnsureInteractionManager();
            EnsureEventSystem();
            CreateGrabTargets();
            CreateHandTestCanvas();
            CreateDiagnosticsPanel();

            // Ensure XR Origin camera is clear for passthrough.
            Camera xrCam = origin.GetComponentInChildren<Camera>();
            if (xrCam != null)
            {
                xrCam.clearFlags = CameraClearFlags.SolidColor;
                xrCam.backgroundColor = Color.clear;
                xrCam.nearClipPlane = 0.1f;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(BootstrapScenePath, true)
            };
        }

        private static void DestroyIfExists(string name)
        {
            GameObject go = GameObject.Find(name);
            if (go != null)
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static void EnsureInteractionManager()
        {
            if (UnityEngine.Object.FindFirstObjectByType<XRInteractionManager>() != null)
            {
                return;
            }

            GameObject manager = new GameObject("XR Interaction Manager");
            manager.AddComponent<XRInteractionManager>();
        }

        private static void EnsureEventSystem()
        {
            EventSystem existing = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
            if (existing != null)
            {
                if (existing.GetComponent<XRUIInputModule>() == null)
                {
                    // Replace legacy modules with XR UI Input Module.
                    foreach (BaseInputModule module in existing.GetComponents<BaseInputModule>())
                    {
                        UnityEngine.Object.DestroyImmediate(module);
                    }

                    existing.gameObject.AddComponent<XRUIInputModule>();
                }

                return;
            }

            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<XRUIInputModule>();
        }

        private static void CreateGrabTargets()
        {
            CreateGrabPrimitive("HandTestCube", PrimitiveType.Cube, new Vector3(-0.25f, 0f, 1.5f),
                new Color(0.20f, 0.55f, 0.85f, 1f));
            CreateGrabPrimitive("HandTestSphere", PrimitiveType.Sphere, new Vector3(0.25f, 0f, 1.5f),
                new Color(0.25f, 0.70f, 0.55f, 1f));
        }

        private static void CreateGrabPrimitive(string name, PrimitiveType type, Vector3 position, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.position = position;
            go.transform.localScale = Vector3.one * 0.15f;

            Rigidbody rb = go.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = go.AddComponent<Rigidbody>();
            }

            rb.useGravity = false;
            rb.isKinematic = true;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            XRGrabInteractable grab = go.AddComponent<XRGrabInteractable>();
            grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
            grab.throwOnDetach = false;

            Renderer renderer = go.GetComponent<Renderer>();
            Material mat = CreateColorMaterial(name + "Mat", color);
            renderer.sharedMaterial = mat;

            // Simple grab/hover tint via XRTintInteractableVisual if available.
            var tint = go.AddComponent<XRTintInteractableVisual>();
            tint.tintColor = new Color(1f, 0.92f, 0.55f, 1f);
            tint.tintOnHover = true;
            tint.tintOnSelection = true;
        }

        private static Material CreateColorMaterial(string assetName, Color color)
        {
            string dir = "Assets/Norscope/Materials";
            Directory.CreateDirectory(dir);
            string path = dir + "/" + assetName + ".mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    shader = Shader.Find("Standard");
                }

                mat = new Material(shader);
                mat.color = color;
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.color = color;
                EditorUtility.SetDirty(mat);
            }

            return mat;
        }

        private static void CreateHandTestCanvas()
        {
            GameObject canvasGo = new GameObject("HandTestCanvas");
            canvasGo.transform.position = new Vector3(0f, 1.15f, 1.2f);
            canvasGo.transform.localScale = Vector3.one * 0.001f;

            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 10f;
            canvasGo.AddComponent<GraphicRaycaster>();
            canvasGo.AddComponent<TrackedDeviceGraphicRaycaster>();

            RectTransform canvasRt = canvasGo.GetComponent<RectTransform>();
            canvasRt.sizeDelta = new Vector2(600f, 420f);

            // Panel background
            GameObject panel = CreateUiPanel(canvasGo.transform, "Panel", Vector2.zero, new Vector2(600f, 420f),
                new Color(0.10f, 0.12f, 0.16f, 0.92f));

            CreateUiText(panel.transform, "Title", "NORSCOPE HAND TEST", new Vector2(0f, 150f), 36,
                FontStyle.Bold, TextAnchor.MiddleCenter);

            CreateUiButton(panel.transform, "ButtonA", "Button A", new Vector2(0f, 40f), () =>
            {
                // Wired at runtime via HandTestButtonLogger.
            });
            CreateUiButton(panel.transform, "ButtonB", "Button B", new Vector2(0f, -40f), null);
            CreateUiButton(panel.transform, "ButtonC", "Button C", new Vector2(0f, -120f), null);

            canvasGo.AddComponent<Norscope.HandTestButtonLogger>();
        }

        private static void CreateDiagnosticsPanel()
        {
            GameObject canvasGo = new GameObject("DiagnosticsPanel");
            canvasGo.transform.position = new Vector3(-0.55f, 0.45f, 1.1f);
            canvasGo.transform.localScale = Vector3.one * 0.001f;

            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
            canvasGo.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;
            canvasGo.AddComponent<GraphicRaycaster>();
            canvasGo.AddComponent<TrackedDeviceGraphicRaycaster>();

            RectTransform rt = canvasGo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(420f, 280f);

            GameObject panel = CreateUiPanel(canvasGo.transform, "Panel", Vector2.zero, new Vector2(420f, 280f),
                new Color(0.08f, 0.09f, 0.12f, 0.88f));

            Text status = CreateUiText(panel.transform, "StatusText",
                "Left Hand: -\nRight Hand: -\nFar Interaction: -\nNear Interaction: -\nPinch: -",
                Vector2.zero, 22, FontStyle.Normal, TextAnchor.UpperLeft);
            RectTransform statusRt = status.GetComponent<RectTransform>();
            statusRt.sizeDelta = new Vector2(380f, 240f);
            statusRt.anchoredPosition = new Vector2(0f, -10f);

            var diagnostics = canvasGo.AddComponent<Norscope.HandInteractionDiagnostics>();
            diagnostics.showDiagnostics = true;
            diagnostics.statusText = status;
        }

        private static GameObject CreateUiPanel(Transform parent, string name, Vector2 anchoredPos, Vector2 size, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            Image image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = true;
            return go;
        }

        private static Text CreateUiText(Transform parent, string name, string content, Vector2 anchoredPos, int fontSize,
            FontStyle style, TextAnchor anchor)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(560f, 60f);
            Text text = go.GetComponent<Text>();
            text.text = content;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = anchor;
            text.color = new Color(0.92f, 0.94f, 0.96f, 1f);
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (text.font == null)
            {
                text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            text.raycastTarget = false;
            return text;
        }

        private static Button CreateUiButton(Transform parent, string name, string label, Vector2 anchoredPos, Action unused)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(320f, 64f);

            Image image = go.GetComponent<Image>();
            image.color = new Color(0.18f, 0.32f, 0.48f, 1f);

            Button button = go.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.18f, 0.32f, 0.48f, 1f);
            colors.highlightedColor = new Color(0.28f, 0.48f, 0.70f, 1f); // hover
            colors.pressedColor = new Color(0.12f, 0.22f, 0.34f, 1f);     // press
            colors.selectedColor = colors.highlightedColor;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.transition = Selectable.Transition.ColorTint;

            // Slight press scale feedback
            go.AddComponent<Norscope.UiButtonPressScale>();

            CreateUiText(go.transform, "Label", label, Vector2.zero, 28, FontStyle.Bold, TextAnchor.MiddleCenter);

            return button;
        }

        private static void ValidateSceneAndOpenXr()
        {
            EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);

            int origins = UnityEngine.Object.FindObjectsByType<XROrigin>(FindObjectsSortMode.None).Length;
            int eventSystems = UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length;
            int uiModules = UnityEngine.Object.FindObjectsByType<XRUIInputModule>(FindObjectsSortMode.None).Length;
            int managers = UnityEngine.Object.FindObjectsByType<XRInteractionManager>(FindObjectsSortMode.None).Length;

            Debug.Log($"[Norscope Phase3] Counts => XROrigin:{origins} EventSystem:{eventSystems} XRUIInputModule:{uiModules} XRInteractionManager:{managers}");

            if (origins != 1)
            {
                throw new Exception("Expected exactly one XR Origin, found " + origins);
            }

            if (eventSystems != 1)
            {
                throw new Exception("Expected exactly one EventSystem, found " + eventSystems);
            }

            if (uiModules != 1)
            {
                throw new Exception("Expected exactly one XRUIInputModule, found " + uiModules);
            }

            if (managers != 1)
            {
                throw new Exception("Expected exactly one XRInteractionManager, found " + managers);
            }

            if (GameObject.Find("HandTestCube") == null || GameObject.Find("HandTestSphere") == null)
            {
                throw new Exception("Grab targets missing.");
            }

            if (GameObject.Find("HandTestCanvas") == null)
            {
                throw new Exception("Hand test canvas missing.");
            }

            // OpenXR validation (errors only).
            SetHoloLens2ValidationRuleset();
            MethodInfo getIssues = typeof(BuildValidator).GetMethod(
                "GetCurrentValidationIssues",
                BindingFlags.NonPublic | BindingFlags.Static);
            var failures = new HashSet<BuildValidationRule>();
            getIssues.Invoke(null, new object[] { failures, BuildTargetGroup.WSA });

            List<BuildValidationRule> errors = failures
                .Where(r => r != null && r.Error)
                .Where(r =>
                {
                    string msg = r.Message ?? string.Empty;
                    return msg.IndexOf("Remoting", StringComparison.OrdinalIgnoreCase) < 0 &&
                           msg.IndexOf("InternetClient", StringComparison.OrdinalIgnoreCase) < 0 &&
                           msg.IndexOf("Microphone", StringComparison.OrdinalIgnoreCase) < 0;
                })
                .ToList();

            foreach (BuildValidationRule rule in failures)
            {
                Debug.Log($"[Norscope Phase3] Validation (error={rule.Error}): {rule.Category} | {rule.Message}");
            }

            if (errors.Count > 0)
            {
                // Attempt automatic fixes for remaining errors once.
                BuildValidator.FixIssues(errors.Where(e => e.FixIt != null).ToList());
                ConfigureOpenXrHandFeatures();

                failures.Clear();
                getIssues.Invoke(null, new object[] { failures, BuildTargetGroup.WSA });
                errors = failures
                    .Where(r => r != null && r.Error)
                    .Where(r =>
                    {
                        string msg = r.Message ?? string.Empty;
                        return msg.IndexOf("Remoting", StringComparison.OrdinalIgnoreCase) < 0 &&
                               msg.IndexOf("InternetClient", StringComparison.OrdinalIgnoreCase) < 0 &&
                               msg.IndexOf("Microphone", StringComparison.OrdinalIgnoreCase) < 0;
                    })
                    .ToList();
            }

            if (errors.Count > 0)
            {
                throw new Exception("OpenXR validation errors remain (" + errors.Count + "). See log.");
            }

            Debug.Log("[Norscope Phase3] Validation passed (no deploy-blocking errors).");
        }

        private static void SetHoloLens2ValidationRuleset()
        {
            Type settingsType = FindType("Microsoft.MixedReality.OpenXR.ValidationSettings");
            Type rulesetType = FindType("Microsoft.MixedReality.OpenXR.ValidationRuleset");
            if (settingsType == null || rulesetType == null)
            {
                return;
            }

            object holoLens2 = Enum.Parse(rulesetType, "HoloLens2");
            PropertyInfo prop = settingsType.GetProperty("CurrentRuleset", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            prop?.SetValue(null, holoLens2);
        }

        private static void BuildUwpArm64MasterInternal()
        {
            NamedBuildTarget wsa = NamedBuildTarget.FromBuildTargetGroup(BuildTargetGroup.WSA);
            EditorUserBuildSettings.SwitchActiveBuildTarget(wsa, BuildTarget.WSAPlayer);
            EditorUserBuildSettings.wsaUWPBuildType = WSAUWPBuildType.D3D;
            EditorUserBuildSettings.wsaArchitecture = "ARM64";

            PlayerSettings.SetScriptingBackend(wsa, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetArchitecture(wsa, 2);
            PlayerSettings.WSA.SetTargetDeviceFamily(PlayerSettings.WSATargetFamily.Holographic, true);
            PlayerSettings.WSA.SetTargetDeviceFamily(PlayerSettings.WSATargetFamily.Desktop, false);
            PlayerSettings.WSA.SetTargetDeviceFamily(PlayerSettings.WSATargetFamily.Mobile, false);

            Directory.CreateDirectory(BuildOutputPath);
            var options = new BuildPlayerOptions
            {
                scenes = new[] { BootstrapScenePath },
                locationPathName = BuildOutputPath,
                target = BuildTarget.WSAPlayer,
                targetGroup = BuildTargetGroup.WSA,
                options = BuildOptions.None
            };

            Debug.Log("[Norscope Phase3] Building UWP...");
            var report = UnityEditor.BuildPipeline.BuildPlayer(options);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                throw new Exception("Unity UWP build failed: " + report.summary.result);
            }

            Debug.Log("[Norscope Phase3] Unity build succeeded.");
        }

        private static Type FindType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }
    }
}
