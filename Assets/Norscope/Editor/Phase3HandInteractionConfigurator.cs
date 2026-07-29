using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Unity.XR.CoreUtils;
using Unity.XR.CoreUtils.Editor;
using Norscope.Windows;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.InputSystem.XR;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactables.Visuals;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;
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
        private const string XriInputActionsPath =
            "Assets/Samples/XR Interaction Toolkit/3.2.1/Starter Assets/XRI Default Input Actions.inputactions";
        private const string InteractionTestPanelPath = "Assets/Norscope/UI/Panels/InteractionTestPanel.prefab";

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

        [MenuItem("Norscope/HoloLens/Apply Phase 3 Hand Ray Fix")]
        public static void ApplyFixMenu()
        {
            try
            {
                ApplyFix();
                Debug.Log("[Norscope Phase3] Hand ray fix applied. Run Validate Hand Interaction next.");
            }
            catch (Exception ex)
            {
                Debug.LogError("[Norscope Phase3] Apply fix failed:\n" + ex);
                throw;
            }
        }

        [MenuItem("Norscope/HoloLens/Validate Hand Interaction")]
        public static void ValidateMenu()
        {
            bool pass = Validate(out string report);
            if (pass)
            {
                Debug.Log("[Norscope Phase3] Validate Hand Interaction: PASS\n" + report);
                EditorUtility.DisplayDialog("Validate Hand Interaction", "PASS\n\nSee Console.", "OK");
            }
            else
            {
                Debug.LogError("[Norscope Phase3] Validate Hand Interaction: FAIL\n" + report);
                EditorUtility.DisplayDialog("Validate Hand Interaction", "FAIL\n\nSee Console.", "OK");
            }
        }

        [MenuItem("Norscope/HoloLens/Build Phase 3 UWP (D3D ARM64)")]
        public static void BuildPhase3UwpPackageMenu()
        {
            try
            {
                BuildPhase3UwpPackage();
            }
            catch (Exception ex)
            {
                Debug.LogError("[Norscope Phase3] Build failed:\n" + ex);
                EditorUtility.DisplayDialog("Phase 3 Build", "FAILED\n\n" + ex.Message, "OK");
            }
        }

        [MenuItem("Norscope/XR/How to use Play Mode Remoting")]
        public static void RemotingHelp()
        {
            const string msg =
                "Holographic Remoting (Play Mode only)\n\n" +
                "1. Norscope → XR → Configure Play Mode Remoting\n" +
                "2. Mixed Reality → Remoting → Holographic Remoting for Play Mode\n" +
                "3. Enter HoloLens IP\n" +
                "4. Press Play\n" +
                "5. Stop Play to disconnect\n\n" +
                "UWP App Remoting stays OFF. Final approval needs a standalone HL2 install.";
            EditorUtility.DisplayDialog("Play Mode Remoting", msg, "OK");
            Debug.Log("[Norscope Remoting] " + msg.Replace("\n", " | "));
        }

        public static void ApplyFix()
        {
            EnsureGrabLayer();
            ConfigureOpenXrHandFeatures();
            VerifyAimBindings();
            ReapplySceneWiring();
            AssetDatabase.SaveAssets();
        }

        public static void EnsureGrabLayer()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            SerializedProperty layer6 = layers.GetArrayElementAtIndex(6);
            if (layer6.stringValue != Norscope.Phase3HandRayCorrector.GrabLayerName)
            {
                layer6.stringValue = Norscope.Phase3HandRayCorrector.GrabLayerName;
                tagManager.ApplyModifiedProperties();
            }
        }

        public static void VerifyAimBindings()
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(XriInputActionsPath);
            if (asset == null)
            {
                throw new Exception("Missing XRI input actions at " + XriInputActionsPath);
            }

            bool hasHolo = false;
            bool hasExt = false;
            foreach (InputActionMap map in asset.actionMaps)
            {
                foreach (string name in new[] { "Aim Position", "Aim Rotation" })
                {
                    InputAction action = map.FindAction(name, false);
                    if (action == null)
                    {
                        continue;
                    }

                    foreach (InputBinding b in action.bindings)
                    {
                        if (string.IsNullOrEmpty(b.path))
                        {
                            continue;
                        }

                        if (b.path.Contains("HoloLensHand") && b.path.Contains("pointer"))
                        {
                            hasHolo = true;
                        }

                        if ((b.path.Contains("<HandInteraction>") || b.path.Contains("HandInteractionPoses")) &&
                            b.path.Contains("pointer"))
                        {
                            hasExt = true;
                        }
                    }
                }
            }

            if (!hasHolo)
            {
                throw new Exception("Aim actions missing HoloLensHand pointer bindings.");
            }

            if (hasExt)
            {
                Debug.LogWarning("[Norscope Phase3] EXT HandInteraction pointer still on Aim actions.");
            }
            else
            {
                Debug.Log("[Norscope Phase3] Aim uses HoloLensHand pointer (OpenXR aim).");
            }
        }

        /// <summary>
        /// Lighter re-apply used by the interactive "Apply Phase 3 Hand Ray Fix" menu item.
        /// Assumes the scene already has an XR Origin (from BuildHandTestScene); does not rebuild it.
        /// </summary>
        private static void ReapplySceneWiring()
        {
            Scene scene = EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);
            DestroyIfExists("VerificationCube");
            DestroyIfExists("VerificationLabel");

            XROrigin origin = UnityEngine.Object.FindFirstObjectByType<XROrigin>();
            if (origin == null)
            {
                throw new Exception("XR Origin Hands missing from Bootstrap.");
            }

            // HL2 Phase 3 uses hands only. Controllers from the nested XR Origin sample
            // duplicate NearFar/Poke interactors and must stay disabled.
            DisableNamedUnder(origin.transform, "Left Controller");
            DisableNamedUnder(origin.transform, "Right Controller");

            Norscope.Phase3HandRayCorrector corrector = origin.GetComponent<Norscope.Phase3HandRayCorrector>();
            if (corrector == null)
            {
                corrector = origin.gameObject.AddComponent<Norscope.Phase3HandRayCorrector>();
            }

            Norscope.Phase3UiPokeSupport pokeSupport = origin.GetComponent<Norscope.Phase3UiPokeSupport>();
            if (pokeSupport == null)
            {
                pokeSupport = origin.gameObject.AddComponent<Norscope.Phase3UiPokeSupport>();
            }

            corrector.Apply();
            EditorUtility.SetDirty(corrector);
            EditorUtility.SetDirty(pokeSupport);
            foreach (NearFarInteractor nf in UnityEngine.Object.FindObjectsByType<NearFarInteractor>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(nf);
                foreach (Component c in nf.GetComponentsInChildren<Component>(true))
                {
                    if (c != null)
                    {
                        PrefabUtility.RecordPrefabInstancePropertyModifications(c);
                    }
                }
            }

            int grabLayer = LayerMask.NameToLayer(Norscope.Phase3HandRayCorrector.GrabLayerName);
            SetLayer("HandTestCube", grabLayer);
            SetLayer("HandTestSphere", grabLayer);

            // TrackedDeviceGraphicRaycaster filters by interactor raycastLayerMask.
            // Far mask is UI|Grab, so canvases must be on UI (not Default).
            int uiLayer = LayerMask.NameToLayer("UI");
            SetLayer("HandTestCanvas", uiLayer);
            SetLayer("InteractionTestCanvas", uiLayer);
            SetLayer("DiagnosticsPanel", uiLayer);
            SetLayer("InteractionTestPanel", uiLayer);

            foreach (HandInteractionDiagnostics d in UnityEngine.Object.FindObjectsByType<HandInteractionDiagnostics>(FindObjectsSortMode.None))
            {
                d.showHandDiagnostics = true;
                EditorUtility.SetDirty(d);
            }

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

            Check(UnityEngine.Object.FindObjectsByType<XROrigin>(FindObjectsSortMode.None).Length == 1, "exactly one XR Origin");
            Check(UnityEngine.Object.FindObjectsByType<XRInteractionManager>(FindObjectsSortMode.None).Length == 1, "exactly one XR Interaction Manager");
            Check(UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length == 1, "exactly one EventSystem");
            Check(UnityEngine.Object.FindObjectsByType<XRUIInputModule>(FindObjectsSortMode.None).Length == 1, "exactly one XRUIInputModule");

            NearFarInteractor[] nearFar = UnityEngine.Object.FindObjectsByType<NearFarInteractor>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(IsHandInteractor)
                .ToArray();
            XRPokeInteractor[] pokes = UnityEngine.Object.FindObjectsByType<XRPokeInteractor>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(IsHandInteractor)
                .ToArray();
            Check(nearFar.Length == 2, "one left+right hand NearFarInteractor (" + nearFar.Length + ")");
            Check(pokes.Length == 2, "one left+right hand XRPokeInteractor (" + pokes.Length + ")");

            int activeControllerInteractors = UnityEngine.Object.FindObjectsByType<NearFarInteractor>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Count(nf => !IsHandInteractor(nf));
            Check(activeControllerInteractors == 0, "controller NearFarInteractors disabled (" + activeControllerInteractors + " active)");

            Transform leftHand = FindNamedInScene("Left Hand");
            Transform rightHand = FindNamedInScene("Right Hand");
            Check(leftHand != null && leftHand.gameObject.activeInHierarchy, "Left Hand active");
            Check(rightHand != null && rightHand.gameObject.activeInHierarchy, "Right Hand active");
            Check(leftHand != null && FindChild(leftHand, "Aim Pose") != null &&
                  leftHand.GetComponentsInChildren<TrackedPoseDriver>(true).Count(d => d.gameObject.name == "Aim Pose") == 1,
                "Left Hand Aim Pose drivers (1)");
            Check(rightHand != null && FindChild(rightHand, "Aim Pose") != null &&
                  rightHand.GetComponentsInChildren<TrackedPoseDriver>(true).Count(d => d.gameObject.name == "Aim Pose") == 1,
                "Right Hand Aim Pose drivers (1)");

            bool aimOk = true;
            bool lineOk = true;
            bool maskOk = true;
            bool noMagenta = true;

            foreach (NearFarInteractor nf in nearFar)
            {
                Transform hand = FindHandRoot(nf.transform);
                Transform aim = FindChild(hand, "Aim Pose");

                if (nf.farInteractionCaster is CurveInteractionCaster caster)
                {
                    if (aim != null && caster.castOrigin != aim)
                    {
                        aimOk = false;
                    }

                    if ((caster.raycastMask.value & 1) != 0)
                    {
                        maskOk = false;
                    }

                    if (Mathf.Abs(caster.castDistance - Norscope.Phase3HandRayCorrector.DiagnosticRayLengthMeters) > 0.05f)
                    {
                        lineOk = false;
                    }
                }
                else
                {
                    aimOk = false;
                }

                CurveVisualController visual = nf.GetComponentInChildren<CurveVisualController>(true);
                if (visual == null || visual.lineOriginTransform != aim ||
                    Mathf.Abs(visual.restingVisualLineLength - Norscope.Phase3HandRayCorrector.DiagnosticRayLengthMeters) > 0.05f)
                {
                    lineOk = false;
                }

                LineRenderer lr = nf.GetComponentInChildren<LineRenderer>(true);
                if (lr != null && lr.sharedMaterial != null && lr.sharedMaterial.shader != null &&
                    lr.sharedMaterial.shader.name.Contains("InternalError"))
                {
                    noMagenta = false;
                }
            }

            Check(aimOk, "far cast origin = Aim Pose");
            Check(lineOk, "line visual Aim Pose + ~3m length");
            Check(maskOk, "Default/spatial excluded from far cast mask");
            Check(noMagenta, "no magenta ray material");
            Check(!AimBindsGrip(), "no grip fallback on Aim actions");
            Check(IsTypeEnabled(BuildTargetGroup.WSA, "MicrosoftHandInteraction"), "Microsoft Hand Interaction enabled");
            Check(IsTypeEnabled(BuildTargetGroup.WSA, "HandTracking"), "XR Hands subsystem enabled");
            Check(!IsIdEnabled(BuildTargetGroup.WSA, HandInteractionProfileFeatureId), "XR_EXT Hand Interaction disabled on WSA");
            Check(!IsIdEnabled(BuildTargetGroup.WSA, AppRemotingFeatureId), "UWP App Remoting disabled");

            GameObject cube = GameObject.Find("HandTestCube");
            GameObject sphere = GameObject.Find("HandTestSphere");
            Check(cube != null && cube.GetComponent<XRGrabInteractable>() != null, "cube grab");
            Check(sphere != null && sphere.GetComponent<XRGrabInteractable>() != null, "sphere grab");

            // Since Phase 4, InteractionTestPanel is runtime-owned by SpatialWindowManager /
            // SpatialWindowSpawner (spawned on demand) and must NOT exist as a static scene object.
            Check(ValidateInteractionTestPanelRuntimeRegistration(), "InteractionTestPanel runtime registration valid");

            Check(GameObject.Find("HandTestCanvas") != null, "HandTestCanvas present");
            GameObject handCanvas = GameObject.Find("HandTestCanvas");
            if (handCanvas != null)
            {
                int uiLayer = LayerMask.NameToLayer("UI");
                Check(handCanvas.layer == uiLayer, "HandTestCanvas on UI layer");
            }

            Check(UnityEngine.Object.FindObjectsByType<Norscope.Phase3HandRayCorrector>(FindObjectsSortMode.None).Length >= 1, "Phase3HandRayCorrector present");
            Check(UnityEngine.Object.FindObjectsByType<Norscope.Phase3UiPokeSupport>(FindObjectsSortMode.None).Length >= 1, "Phase3UiPokeSupport present");

            bool missing = scene.GetRootGameObjects().Any(root => root.GetComponentsInChildren<Component>(true).Any(c => c == null));
            Check(!missing, "no missing scripts");
            Check(CountOpenXrErrors() == 0, "no OpenXR deploy-blocking errors");

            report = sb.ToString();
            return fails == 0;
        }

        private static bool AimBindsGrip()
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(XriInputActionsPath);
            if (asset == null)
            {
                return false;
            }

            foreach (InputActionMap map in asset.actionMaps)
            {
                foreach (string name in new[] { "Aim Position", "Aim Rotation" })
                {
                    InputAction action = map.FindAction(name, false);
                    if (action == null)
                    {
                        continue;
                    }

                    foreach (InputBinding b in action.bindings)
                    {
                        if (string.IsNullOrEmpty(b.path))
                        {
                            continue;
                        }

                        string p = b.path.ToLowerInvariant();
                        if (p.Contains("grip") || p.Contains("devicepose") || p.Contains("palm") || p.Contains("wrist"))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private static int CountOpenXrErrors()
        {
            try
            {
                MethodInfo getIssues = typeof(BuildValidator).GetMethod("GetCurrentValidationIssues", BindingFlags.NonPublic | BindingFlags.Static);
                var failures = new HashSet<BuildValidationRule>();
                getIssues.Invoke(null, new object[] { failures, BuildTargetGroup.WSA });
                return failures.Count(r => r != null && r.Error &&
                    (r.Message == null ||
                     (r.Message.IndexOf("Remoting", StringComparison.OrdinalIgnoreCase) < 0 &&
                      r.Message.IndexOf("InternetClient", StringComparison.OrdinalIgnoreCase) < 0 &&
                      r.Message.IndexOf("Microphone", StringComparison.OrdinalIgnoreCase) < 0)));
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// InteractionTestPanel has no static scene presence since Phase 4 — it is spawned at
        /// runtime by SpatialWindowManager/SpatialWindowSpawner. This checks the architecture
        /// that replaces it is intact, instead of looking for a scene-placed instance.
        /// </summary>
        private static bool ValidateInteractionTestPanelRuntimeRegistration()
        {
            bool ok = true;

            GameObject panelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(InteractionTestPanelPath);
            if (panelPrefab == null)
            {
                Debug.LogWarning("[Norscope Phase3] InteractionTestPanel prefab asset missing at " + InteractionTestPanelPath);
                ok = false;
            }

            SpatialWindowSpawner[] spawners = UnityEngine.Object.FindObjectsByType<SpatialWindowSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (spawners.Length != 1)
            {
                Debug.LogWarning("[Norscope Phase3] Expected exactly one SpatialWindowSpawner, found " + spawners.Length);
                ok = false;
            }

            if (spawners.Length == 1 && panelPrefab != null)
            {
                var soSpawner = new SerializedObject(spawners[0]);
                SerializedProperty prefabProp = soSpawner.FindProperty("interactionTestPanelPrefab");
                GameObject registeredPrefab = prefabProp != null ? prefabProp.objectReferenceValue as GameObject : null;
                if (registeredPrefab != panelPrefab)
                {
                    Debug.LogWarning("[Norscope Phase3] SpatialWindowSpawner.interactionTestPanelPrefab does not reference " + InteractionTestPanelPath);
                    ok = false;
                }
            }

            if (UnityEngine.Object.FindObjectsByType<SpatialWindowManager>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length != 1)
            {
                Debug.LogWarning("[Norscope Phase3] Expected exactly one SpatialWindowManager in Bootstrap.");
                ok = false;
            }

            GameObject scenePanel = GameObject.Find("InteractionTestPanel");
            if (scenePanel == null)
            {
                Transform[] all = Resources.FindObjectsOfTypeAll<Transform>();
                foreach (Transform t in all)
                {
                    if (t != null && t.name == "InteractionTestPanel" && !string.IsNullOrEmpty(t.gameObject.scene.name))
                    {
                        scenePanel = t.gameObject;
                        break;
                    }
                }
            }

            if (scenePanel != null)
            {
                Debug.LogWarning("[Norscope Phase3] InteractionTestPanel must not be scene-placed — runtime ownership belongs to SpatialWindowManager.");
                ok = false;
            }

            return ok;
        }

        public static void BuildPhase3UwpPackage()
        {
            if (!Validate(out string report))
            {
                throw new Exception("Validate Hand Interaction failed. Fix before packaging.\n" + report);
            }

            ConfigureOpenXrHandFeatures();

            // Keep SpatialPerception; do not strip hand features (unlike Phase 2).
            PlayerSettings.WSA.SetCapability(PlayerSettings.WSACapability.SpatialPerception, true);
            PlayerSettings.bundleVersion = "0.3.2";

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(BootstrapScenePath, true)
            };

            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WSA, BuildTarget.WSAPlayer);
            EditorUserBuildSettings.wsaUWPBuildType = WSAUWPBuildType.D3D;
            EditorUserBuildSettings.wsaArchitecture = "ARM64";

#pragma warning disable CS0618
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.WSA, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetArchitecture(BuildTargetGroup.WSA, 2);
#pragma warning restore CS0618
            PlayerSettings.WSA.SetTargetDeviceFamily(PlayerSettings.WSATargetFamily.Holographic, true);
            PlayerSettings.WSA.SetTargetDeviceFamily(PlayerSettings.WSATargetFamily.Desktop, false);
            PlayerSettings.WSA.SetTargetDeviceFamily(PlayerSettings.WSATargetFamily.Mobile, false);

            Directory.CreateDirectory(BuildOutputPath);
            AssetDatabase.SaveAssets();

            var options = new BuildPlayerOptions
            {
                scenes = new[] { BootstrapScenePath },
                locationPathName = BuildOutputPath,
                target = BuildTarget.WSAPlayer,
                targetGroup = BuildTargetGroup.WSA,
                options = BuildOptions.None
            };

            Debug.Log("[Norscope Phase3] Building UWP D3D ARM64 (hands-enabled)...");
            var buildReport = UnityEditor.BuildPipeline.BuildPlayer(options);
            if (buildReport.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                throw new Exception("Unity UWP build failed: " + buildReport.summary.result);
            }

            Debug.Log("[Norscope Phase3] Unity export succeeded: " + BuildOutputPath +
                      "\nNext: MSBuild Master ARM64, then WinAppDeployCmd install over USB." +
                      "\nPackage version: " + PlayerSettings.bundleVersion);
            EditorUtility.DisplayDialog(
                "Phase 3 Unity Export",
                "Unity UWP export succeeded.\n\nNext the agent/MSBuild will package Master ARM64 and install over USB.\nDo not Device Portal remote-launch — open from Start menu.",
                "OK");
        }

        private static Transform FindChild(Transform root, string name)
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
                Transform f = FindChild(root.GetChild(i), name);
                if (f != null)
                {
                    return f;
                }
            }

            return null;
        }

        private static void DisableNamedUnder(Transform root, string name)
        {
            Transform t = FindChild(root, name);
            if (t == null)
            {
                return;
            }

            if (t.gameObject.activeSelf)
            {
                t.gameObject.SetActive(false);
                PrefabUtility.RecordPrefabInstancePropertyModifications(t.gameObject);
                EditorUtility.SetDirty(t.gameObject);
                Debug.Log("[Norscope Phase3] Disabled " + name + " (HL2 hands-only).");
            }
        }

        private static bool IsHandInteractor(Component c)
        {
            return FindHandRoot(c.transform) != null;
        }

        private static Transform FindHandRoot(Transform t)
        {
            while (t != null)
            {
                if (t.name == "Left Hand" || t.name == "Right Hand")
                {
                    return t;
                }

                t = t.parent;
            }

            return null;
        }

        private static Transform FindNamedInScene(string name)
        {
            GameObject go = GameObject.Find(name);
            return go != null ? go.transform : null;
        }

        private static void SetLayer(string name, int layer)
        {
            GameObject go = GameObject.Find(name);
            if (go == null || layer < 0)
            {
                return;
            }

            foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = layer;
            }

            EditorUtility.SetDirty(go);
        }

        private static bool IsTypeEnabled(BuildTargetGroup group, string typeName)
        {
            OpenXRSettings settings = OpenXRSettings.GetSettingsForBuildTargetGroup(group);
            FieldInfo field = typeof(OpenXRSettings).GetField("features", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            OpenXRFeature[] features = field != null ? field.GetValue(settings) as OpenXRFeature[] : null;
            return features != null && features.Any(f => f != null && f.GetType().Name == typeName && f.enabled);
        }

        private static bool IsIdEnabled(BuildTargetGroup group, string id)
        {
            OpenXRFeature feature = FeatureHelpers.GetFeatureWithIdForBuildTarget(group, id);
            return feature != null && feature.enabled;
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

            // HL2: use Microsoft Hand Interaction aim/pointer only.
            // Keeping XR_EXT Hand Interaction enabled creates a competing pointer pose and skews far rays.
            SetFeatureEnabledById(BuildTargetGroup.WSA, HandInteractionProfileFeatureId, false);

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

            // Standalone target (Play Mode Remoting / in-editor hand testing) needs the same
            // Microsoft Hand Interaction profile, with XR_EXT Hand Interaction kept off.
            FeatureHelpers.RefreshFeatures(BuildTargetGroup.Standalone);
            SetFeatureEnabledByTypeName(BuildTargetGroup.Standalone, "MicrosoftHandInteraction", true);
            SetFeatureEnabledById(BuildTargetGroup.Standalone, HandInteractionProfileFeatureId, false);
            SetFeatureEnabledByTypeName(BuildTargetGroup.Standalone, "HandTracking", true);
            SetFeatureEnabledById(BuildTargetGroup.Standalone, MrHandTrackingFeatureId, true);
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

            EnsureGrabLayer();
            DisableNamedUnder(origin.transform, "Left Controller");
            DisableNamedUnder(origin.transform, "Right Controller");

            var corrector = origin.GetComponent<Norscope.Phase3HandRayCorrector>();
            if (corrector == null)
            {
                corrector = origin.AddComponent<Norscope.Phase3HandRayCorrector>();
            }

            var pokeSupport = origin.GetComponent<Norscope.Phase3UiPokeSupport>();
            if (pokeSupport == null)
            {
                pokeSupport = origin.AddComponent<Norscope.Phase3UiPokeSupport>();
            }

            corrector.Apply();
            EditorUtility.SetDirty(corrector);
            EditorUtility.SetDirty(pokeSupport);
            foreach (NearFarInteractor nf in UnityEngine.Object.FindObjectsByType<NearFarInteractor>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(nf);
                foreach (Component c in nf.GetComponentsInChildren<Component>(true))
                {
                    if (c != null)
                    {
                        PrefabUtility.RecordPrefabInstancePropertyModifications(c);
                    }
                }
            }

            // Camera under XR Origin for passthrough.
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

            int grabLayer = LayerMask.NameToLayer(Norscope.Phase3HandRayCorrector.GrabLayerName);
            if (grabLayer >= 0)
            {
                go.layer = grabLayer;
            }

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
            canvasGo.layer = LayerMask.NameToLayer("UI");
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
            canvasGo.layer = LayerMask.NameToLayer("UI");
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
            diagnostics.showHandDiagnostics = true;
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
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WSA, BuildTarget.WSAPlayer);
            EditorUserBuildSettings.wsaUWPBuildType = WSAUWPBuildType.D3D;
            EditorUserBuildSettings.wsaArchitecture = "ARM64";

#pragma warning disable CS0618
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.WSA, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetArchitecture(BuildTargetGroup.WSA, 2);
#pragma warning restore CS0618
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
