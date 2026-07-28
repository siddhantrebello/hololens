using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Unity.XR.CoreUtils.Editor;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;

namespace Norscope.Editor
{
    /// <summary>
    /// Phase 2: HL2 OpenXR config, Bootstrap verification content, validation, UWP build.
    /// No XRI / hands / panels / AI.
    /// </summary>
    public static class Phase2DeployConfigurator
    {
        private const string BootstrapScenePath = "Assets/Norscope/Scenes/Bootstrap.unity";
        private const string BuildOutputPath = "Builds/UWP";
        private const string HoloLensFeatureSetId = "com.microsoft.openxr.featureset.hololens";
        private const string AppRemotingFeatureSetId = "com.microsoft.openxr.featureset.appremoting";

        private const string MixedRealityFeatureId = "com.microsoft.openxr.feature.hololens";
        private const string HandTrackingFeatureId = "com.microsoft.openxr.feature.handtracking";
        private const string MotionControllerFeatureId = "com.microsoft.openxr.feature.controller";
        private const string AppRemotingFeatureId = "com.microsoft.openxr.feature.appremoting";
        private const string PlayModeRemotingFeatureId = "com.microsoft.openxr.feature.playmoderemoting";

        public static void ConfigureAndValidate()
        {
            try
            {
                ConfigurePlayerCapabilities();
                EnsureXrLoaderAndInitOnStartup();
                ConfigureOpenXrFeatures();
                ConfigureBootstrapScene();
                SetHoloLens2ValidationRuleset();
                RunAndFixValidation();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[Norscope Phase2] ConfigureAndValidate completed successfully.");
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError("[Norscope Phase2] ConfigureAndValidate failed:\n" + ex);
                EditorApplication.Exit(1);
            }
        }

        public static void BuildUwpArm64Master()
        {
            try
            {
                ConfigurePlayerCapabilities();
                EnsureXrLoaderAndInitOnStartup();
                ConfigureOpenXrFeatures();
                ConfigureBootstrapScene();
                SetHoloLens2ValidationRuleset();
                RunAndFixValidation();

                if (!File.Exists(BootstrapScenePath))
                {
                    throw new FileNotFoundException("Bootstrap scene missing.", BootstrapScenePath);
                }

                EditorBuildSettings.scenes = new[]
                {
                    new EditorBuildSettingsScene(BootstrapScenePath, true)
                };

                NamedBuildTarget wsa = NamedBuildTarget.FromBuildTargetGroup(BuildTargetGroup.WSA);
                EditorUserBuildSettings.SwitchActiveBuildTarget(wsa, BuildTarget.WSAPlayer);
                EditorUserBuildSettings.wsaUWPBuildType = WSAUWPBuildType.D3D;
                EditorUserBuildSettings.wsaArchitecture = "ARM64";
                TrySetWsaBuildTypeMaster();

                PlayerSettings.SetScriptingBackend(wsa, ScriptingImplementation.IL2CPP);
                PlayerSettings.SetArchitecture(wsa, 2);
                PlayerSettings.WSA.SetTargetDeviceFamily(PlayerSettings.WSATargetFamily.Holographic, true);
                PlayerSettings.WSA.SetTargetDeviceFamily(PlayerSettings.WSATargetFamily.Desktop, false);
                PlayerSettings.WSA.SetTargetDeviceFamily(PlayerSettings.WSATargetFamily.Mobile, false);
                PlayerSettings.WSA.SetTargetDeviceFamily(PlayerSettings.WSATargetFamily.Xbox, false);
                PlayerSettings.WSA.SetTargetDeviceFamily(PlayerSettings.WSATargetFamily.Team, false);
                PlayerSettings.WSA.SetTargetDeviceFamily(PlayerSettings.WSATargetFamily.IoT, false);
                PlayerSettings.WSA.SetTargetDeviceFamily(PlayerSettings.WSATargetFamily.IoTHeadless, false);

                Directory.CreateDirectory(BuildOutputPath);

                var options = new BuildPlayerOptions
                {
                    scenes = new[] { BootstrapScenePath },
                    locationPathName = BuildOutputPath,
                    target = BuildTarget.WSAPlayer,
                    targetGroup = BuildTargetGroup.WSA,
                    options = BuildOptions.None
                };

                Debug.Log("[Norscope Phase2] Starting UWP ARM64 build (D3D, Master intent)...");
                var report = BuildPipeline.BuildPlayer(options);
                if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                {
                    throw new Exception("Unity UWP build failed: " + report.summary.result);
                }

                Debug.Log("[Norscope Phase2] Unity build succeeded: " + BuildOutputPath);
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError("[Norscope Phase2] Build failed:\n" + ex);
                EditorApplication.Exit(1);
            }
        }

        private static void ConfigurePlayerCapabilities()
        {
            PlayerSettings.WSA.SetCapability(PlayerSettings.WSACapability.SpatialPerception, true);
            PlayerSettings.WSA.SetCapability(PlayerSettings.WSACapability.Microphone, false);
            PlayerSettings.WSA.SetCapability(PlayerSettings.WSACapability.InternetClient, false);
            PlayerSettings.WSA.SetCapability(PlayerSettings.WSACapability.InternetClientServer, false);
            PlayerSettings.WSA.SetCapability(PlayerSettings.WSACapability.PrivateNetworkClientServer, false);

            // GazeInput only if required later by validation; start disabled.
            PlayerSettings.WSA.SetCapability(PlayerSettings.WSACapability.GazeInput, false);
        }

        private static void EnsureXrLoaderAndInitOnStartup()
        {
            XRGeneralSettings settings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.WSA);
            if (settings == null)
            {
                throw new Exception("XR General Settings missing for WSA/UWP.");
            }

            settings.InitManagerOnStart = true;
            EditorUtility.SetDirty(settings);

            XRManagerSettings manager = settings.AssignedSettings;
            if (manager == null || manager.activeLoaders == null ||
                !manager.activeLoaders.Any(l => l != null && l.GetType().Name.Contains("OpenXR")))
            {
                throw new Exception("OpenXR loader is not assigned for UWP.");
            }
        }

        private static void ConfigureOpenXrFeatures()
        {
            FeatureHelpers.RefreshFeatures(BuildTargetGroup.WSA);

            // Enable Microsoft HoloLens feature group; disable remoting group.
            SetFeatureSetEnabled(BuildTargetGroup.WSA, HoloLensFeatureSetId, true);
            SetFeatureSetEnabled(BuildTargetGroup.WSA, AppRemotingFeatureSetId, false);
            OpenXRFeatureSetManager.SetFeaturesFromEnabledFeatureSets(BuildTargetGroup.WSA);

            // Minimum immersive feature only: Mixed Reality Features.
            // Explicitly disable hands / controllers / remoting (no interaction in Phase 2).
            SetFeatureEnabled(BuildTargetGroup.WSA, MixedRealityFeatureId, true);
            SetFeatureEnabled(BuildTargetGroup.WSA, HandTrackingFeatureId, false);
            SetFeatureEnabled(BuildTargetGroup.WSA, MotionControllerFeatureId, false);
            SetFeatureEnabled(BuildTargetGroup.WSA, AppRemotingFeatureId, false);
            SetFeatureEnabled(BuildTargetGroup.WSA, PlayModeRemotingFeatureId, false);

            // Also keep remoting off for Standalone (Play Mode remoting).
            FeatureHelpers.RefreshFeatures(BuildTargetGroup.Standalone);
            SetFeatureEnabled(BuildTargetGroup.Standalone, AppRemotingFeatureId, false);
            SetFeatureEnabled(BuildTargetGroup.Standalone, PlayModeRemotingFeatureId, false);

            OpenXRSettings openXr = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.WSA);
            if (openXr != null)
            {
                openXr.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;
                openXr.depthSubmissionMode = OpenXRSettings.DepthSubmissionMode.Depth16Bit;
                EditorUtility.SetDirty(openXr);
            }

            LogEnabledFeatures(BuildTargetGroup.WSA);
        }

        private static void SetFeatureSetEnabled(BuildTargetGroup group, string featureSetId, bool enabled)
        {
            OpenXRFeatureSetManager.FeatureSet featureSet = OpenXRFeatureSetManager.GetFeatureSetWithId(group, featureSetId);
            if (featureSet == null)
            {
                Debug.LogWarning("[Norscope Phase2] Feature set not found: " + featureSetId);
                return;
            }

            featureSet.isEnabled = enabled;
        }

        private static void SetFeatureEnabled(BuildTargetGroup group, string featureId, bool enabled)
        {
            OpenXRFeature feature = FeatureHelpers.GetFeatureWithIdForBuildTarget(group, featureId);
            if (feature == null)
            {
                Debug.LogWarning("[Norscope Phase2] Feature not found: " + featureId);
                return;
            }

            feature.enabled = enabled;
            EditorUtility.SetDirty(feature);
        }

        private static void LogEnabledFeatures(BuildTargetGroup group)
        {
            string[] knownIds =
            {
                MixedRealityFeatureId,
                HandTrackingFeatureId,
                MotionControllerFeatureId,
                AppRemotingFeatureId,
                PlayModeRemotingFeatureId
            };

            foreach (string featureId in knownIds)
            {
                OpenXRFeature feature = FeatureHelpers.GetFeatureWithIdForBuildTarget(group, featureId);
                if (feature != null && feature.enabled)
                {
                    Debug.Log("[Norscope Phase2] Enabled OpenXR feature: " + feature.name + " [" + featureId + "]");
                }
            }
        }

        private static void ConfigureBootstrapScene()
        {
            Scene scene = EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);

            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                throw new Exception("Bootstrap scene has no Main Camera.");
            }

            mainCamera.clearFlags = CameraClearFlags.SolidColor;
            mainCamera.backgroundColor = Color.clear;
            mainCamera.nearClipPlane = 0.1f;
            mainCamera.farClipPlane = 100f;
            mainCamera.stereoTargetEye = StereoTargetEyeMask.Both;

            // Prefer Input System TrackedPoseDriver with explicit HMD bindings.
            TrackedPoseDriver tpd = mainCamera.GetComponent<TrackedPoseDriver>();
            if (tpd == null)
            {
                tpd = mainCamera.gameObject.AddComponent<TrackedPoseDriver>();
            }

            ConfigureHmdTrackedPoseDriver(tpd);

            // Verification content — world space only, no UI canvas.
            EnsureVerificationCube();
            EnsureVerificationLabel();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(BootstrapScenePath, true)
            };
        }

        private static void ConfigureHmdTrackedPoseDriver(TrackedPoseDriver tpd)
        {
            var positionAction = new UnityEngine.InputSystem.InputAction(
                name: "Position",
                type: UnityEngine.InputSystem.InputActionType.Value,
                binding: "<XRHMD>/centerEyePosition",
                expectedControlType: "Vector3");
            var rotationAction = new UnityEngine.InputSystem.InputAction(
                name: "Rotation",
                type: UnityEngine.InputSystem.InputActionType.Value,
                binding: "<XRHMD>/centerEyeRotation",
                expectedControlType: "Quaternion");
            var trackingStateAction = new UnityEngine.InputSystem.InputAction(
                name: "Tracking State",
                type: UnityEngine.InputSystem.InputActionType.Value,
                binding: "<XRHMD>/trackingState",
                expectedControlType: "Integer");

            tpd.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
            tpd.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
            tpd.positionInput = new UnityEngine.InputSystem.InputActionProperty(positionAction);
            tpd.rotationInput = new UnityEngine.InputSystem.InputActionProperty(rotationAction);
            tpd.trackingStateInput = new UnityEngine.InputSystem.InputActionProperty(trackingStateAction);
            EditorUtility.SetDirty(tpd);
        }

        private static void EnsureVerificationCube()
        {
            GameObject cube = GameObject.Find("VerificationCube");
            if (cube == null)
            {
                cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = "VerificationCube";
            }

            cube.transform.position = new Vector3(0f, 0f, 2f);
            cube.transform.localScale = Vector3.one * 0.2f;

            Renderer renderer = cube.GetComponent<Renderer>();
            if (renderer != null)
            {
                const string matPath = "Assets/Norscope/Materials/VerificationCube.mat";
                Directory.CreateDirectory("Assets/Norscope/Materials");
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null)
                {
                    Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                    if (shader == null)
                    {
                        shader = Shader.Find("Standard");
                    }

                    mat = new Material(shader);
                    mat.color = new Color(0.1f, 0.75f, 0.95f, 1f);
                    AssetDatabase.CreateAsset(mat, matPath);
                }

                renderer.sharedMaterial = mat;
            }
        }

        private static void EnsureVerificationLabel()
        {
            GameObject labelGo = GameObject.Find("VerificationLabel");
            if (labelGo == null)
            {
                labelGo = new GameObject("VerificationLabel");
            }

            labelGo.transform.position = new Vector3(0f, 0.25f, 2f);
            labelGo.transform.localScale = Vector3.one * 0.02f;

            TextMesh textMesh = labelGo.GetComponent<TextMesh>();
            if (textMesh == null)
            {
                textMesh = labelGo.AddComponent<TextMesh>();
            }

            textMesh.text = "NORSCOPE PLATFORM OK";
            textMesh.fontSize = 48;
            textMesh.characterSize = 1f;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.color = Color.white;
            textMesh.fontStyle = FontStyle.Bold;
        }

        private static void SetHoloLens2ValidationRuleset()
        {
            // ValidationSettings is internal; set via reflection.
            Type settingsType = FindType("Microsoft.MixedReality.OpenXR.ValidationSettings");
            Type rulesetType = FindType("Microsoft.MixedReality.OpenXR.ValidationRuleset");
            if (settingsType == null || rulesetType == null)
            {
                Debug.LogWarning("[Norscope Phase2] Could not locate ValidationSettings types.");
                return;
            }

            object holoLens2 = Enum.Parse(rulesetType, "HoloLens2");
            PropertyInfo prop = settingsType.GetProperty("CurrentRuleset", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (prop == null)
            {
                Debug.LogWarning("[Norscope Phase2] ValidationSettings.CurrentRuleset not found.");
                return;
            }

            prop.SetValue(null, holoLens2);
            Debug.Log("[Norscope Phase2] Validation ruleset set to HoloLens2.");
        }

        private static void RunAndFixValidation()
        {
            // Open Bootstrap so scene-only rules evaluate.
            EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);

            MethodInfo getIssues = typeof(BuildValidator).GetMethod(
                "GetCurrentValidationIssues",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (getIssues == null)
            {
                throw new Exception("BuildValidator.GetCurrentValidationIssues not found.");
            }

            var failures = new HashSet<BuildValidationRule>();
            getIssues.Invoke(null, new object[] { failures, BuildTargetGroup.WSA });

            Debug.Log("[Norscope Phase2] Validation issues before fix: " + failures.Count);
            foreach (BuildValidationRule rule in failures)
            {
                Debug.Log(string.Format(
                    "[Norscope Phase2] Issue (error={0}): {1} | {2}",
                    rule.Error,
                    rule.Category,
                    rule.Message));
            }

            // GazeInput: enable only if a failing rule mentions it.
            bool gazeRequired = failures.Any(r =>
                r != null &&
                r.Message != null &&
                r.Message.IndexOf("GazeInput", StringComparison.OrdinalIgnoreCase) >= 0);
            if (gazeRequired)
            {
                PlayerSettings.WSA.SetCapability(PlayerSettings.WSACapability.GazeInput, true);
                Debug.Log("[Norscope Phase2] Enabled GazeInput because validation required it.");
            }

            List<BuildValidationRule> fixable = failures
                .Where(r => r != null && r.FixIt != null && (r.Error || r.FixItAutomatic))
                .ToList();

            // Also auto-fix camera / HL2 recommended rules that are safe for deploy.
            foreach (BuildValidationRule rule in failures.Where(r => r != null && r.FixIt != null))
            {
                string msg = rule.Message ?? string.Empty;
                bool relevant =
                    rule.Error ||
                    msg.IndexOf("camera", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    msg.IndexOf("PoseDriver", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    msg.IndexOf("OpenXR", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    msg.IndexOf("feature set", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    msg.IndexOf("SpatialPerception", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    msg.IndexOf("render mode", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    msg.IndexOf("depth", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    msg.IndexOf("DX11", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    msg.IndexOf("Graphics API", StringComparison.OrdinalIgnoreCase) >= 0;

                // Never re-enable remoting / internet / mic via auto-fix.
                bool forbidden =
                    msg.IndexOf("Remoting", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    msg.IndexOf("InternetClient", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    msg.IndexOf("Microphone", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    msg.IndexOf("Hand tracking", StringComparison.OrdinalIgnoreCase) >= 0;

                if (relevant && !forbidden && !fixable.Contains(rule))
                {
                    fixable.Add(rule);
                }
            }

            if (fixable.Count > 0)
            {
                BuildValidator.FixIssues(fixable);
            }

            // Re-assert Phase 2 capability / feature constraints after auto-fix.
            ConfigurePlayerCapabilities();
            if (gazeRequired)
            {
                PlayerSettings.WSA.SetCapability(PlayerSettings.WSACapability.GazeInput, true);
            }

            SetFeatureEnabled(BuildTargetGroup.WSA, HandTrackingFeatureId, false);
            SetFeatureEnabled(BuildTargetGroup.WSA, MotionControllerFeatureId, false);
            SetFeatureEnabled(BuildTargetGroup.WSA, AppRemotingFeatureId, false);
            SetFeatureEnabled(BuildTargetGroup.WSA, PlayModeRemotingFeatureId, false);
            SetFeatureEnabled(BuildTargetGroup.WSA, MixedRealityFeatureId, true);
            SetFeatureSetEnabled(BuildTargetGroup.WSA, AppRemotingFeatureSetId, false);

            failures.Clear();
            getIssues.Invoke(null, new object[] { failures, BuildTargetGroup.WSA });

            // Filter remaining errors that block HL2 deploy.
            List<BuildValidationRule> remainingErrors = failures
                .Where(r => r != null && r.Error)
                .Where(r =>
                {
                    string msg = r.Message ?? string.Empty;
                    // Ignore remoting-required internet/mic errors; remoting is intentionally off.
                    return msg.IndexOf("Remoting", StringComparison.OrdinalIgnoreCase) < 0 &&
                           msg.IndexOf("InternetClient", StringComparison.OrdinalIgnoreCase) < 0 &&
                           msg.IndexOf("Microphone", StringComparison.OrdinalIgnoreCase) < 0;
                })
                .ToList();

            Debug.Log("[Norscope Phase2] Validation issues after fix: " + failures.Count +
                      " (remaining deploy errors: " + remainingErrors.Count + ")");
            foreach (BuildValidationRule rule in failures)
            {
                Debug.Log(string.Format(
                    "[Norscope Phase2] Remaining (error={0}): {1} | {2}",
                    rule.Error,
                    rule.Category,
                    rule.Message));
            }

            if (remainingErrors.Count > 0)
            {
                throw new Exception("Unresolved HoloLens deployment validation errors remain. See log.");
            }
        }

        private static void TrySetWsaBuildTypeMaster()
        {
            // Unity exposes WSABuildType enum; property name varies by editor version.
            PropertyInfo prop = typeof(EditorUserBuildSettings).GetProperty(
                "wsaBuildType",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (prop != null && prop.PropertyType.Name == "WSABuildType")
            {
                object master = Enum.Parse(prop.PropertyType, "Master");
                prop.SetValue(null, master);
                Debug.Log("[Norscope Phase2] Set EditorUserBuildSettings.wsaBuildType = Master");
                return;
            }

            // Fallback used by some Unity versions.
            MethodInfo setPlatform = typeof(EditorUserBuildSettings).GetMethod(
                "SetPlatformSettings",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(string), typeof(string), typeof(string) },
                null);
            if (setPlatform != null)
            {
                setPlatform.Invoke(null, new object[] { "WindowsStoreApps", "BuildConfiguration", "Master" });
                Debug.Log("[Norscope Phase2] Set WindowsStoreApps BuildConfiguration=Master via platform settings.");
            }
            else
            {
                Debug.LogWarning("[Norscope Phase2] Could not set WSA Master in Editor; MSBuild Master step will enforce it.");
            }
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
