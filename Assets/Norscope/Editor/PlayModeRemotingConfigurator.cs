using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Interactions;

namespace Norscope.Editor
{
    /// <summary>
    /// Enables Holographic Remoting for editor Play Mode without changing the UWP deployment path.
    /// </summary>
    public static class PlayModeRemotingConfigurator
    {
        private const string WmrFeatureSetId = "com.microsoft.openxr.featureset.wmr";
        private const string MixedRealityFeatureId = "com.microsoft.openxr.feature.hololens";
        private const string MrHandTrackingFeatureId = "com.microsoft.openxr.feature.handtracking";
        private const string EyeGazeFeatureId = "com.unity.openxr.feature.input.eyetracking";
        private const string PlayModeRemotingFeatureId = "com.microsoft.openxr.feature.playmoderemoting";
        private const string AppRemotingFeatureId = "com.microsoft.openxr.feature.appremoting";

        [MenuItem("Norscope/XR/Configure Play Mode Remoting")]
        public static void Configure()
        {
            try
            {
                EnsureStandaloneOpenXrLoader();
                EnableStandaloneRemotingFeatures();
                AssetDatabase.SaveAssets();

                Debug.Log(
                    "[Norscope Remoting] Play Mode remoting is enabled for Standalone. " +
                    "UWP remoting and UWP deployment settings were not changed. " +
                    "Set the HoloLens IP in Mixed Reality/Remoting/Holographic Remoting for Play Mode before pressing Play.");
            }
            catch (Exception ex)
            {
                Debug.LogError("[Norscope Remoting] Failed:\n" + ex);
                throw;
            }
        }

        public static void ConfigureForBatchMode()
        {
            try
            {
                Configure();
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError("[Norscope Remoting] Batch mode failed:\n" + ex);
                EditorApplication.Exit(1);
            }
        }

        private static void EnsureStandaloneOpenXrLoader()
        {
            XRGeneralSettings xrGeneralSettings = FindStandaloneXrGeneralSettings();
            XRManagerSettings manager = xrGeneralSettings != null ? xrGeneralSettings.Manager : null;
            if (manager == null)
            {
                throw new InvalidOperationException("Could not get Standalone XR manager settings.");
            }

            bool assigned = false;
            foreach (XRLoader loader in manager.activeLoaders)
            {
                if (loader != null && loader.GetType() == typeof(OpenXRLoader))
                {
                    assigned = true;
                    break;
                }
            }

            if (!assigned)
            {
                if (!XRPackageMetadataStore.AssignLoader(manager, typeof(OpenXRLoader).Name, BuildTargetGroup.Standalone))
                {
                    throw new InvalidOperationException("Failed to assign the OpenXR loader for Standalone.");
                }
            }

            if (xrGeneralSettings != null)
            {
                xrGeneralSettings.InitManagerOnStart = true;
                EditorUtility.SetDirty(xrGeneralSettings);
            }
        }

        private static XRGeneralSettings FindStandaloneXrGeneralSettings()
        {
            // Prefer the XR Management API when available; fall back to the project asset.
            System.Type perTargetType = System.Type.GetType(
                "UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget, Unity.XR.Management.Editor");
            if (perTargetType != null)
            {
                var method = perTargetType.GetMethod(
                    "XRGeneralSettingsForBuildTarget",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (method != null)
                {
                    return method.Invoke(null, new object[] { BuildTargetGroup.Standalone }) as XRGeneralSettings;
                }
            }

            string[] guids = AssetDatabase.FindAssets("t:XRGeneralSettings");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                XRGeneralSettings settings = AssetDatabase.LoadAssetAtPath<XRGeneralSettings>(path);
                if (settings != null && path.IndexOf("Standalone", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return settings;
                }
            }

            // XRGeneralSettingsPerBuildTarget asset holds Standalone settings as a nested object.
            return null;
        }

        private static void EnableStandaloneRemotingFeatures()
        {
            FeatureHelpers.RefreshFeatures(BuildTargetGroup.Standalone);

            var featureSet = OpenXRFeatureSetManager.GetFeatureSetWithId(BuildTargetGroup.Standalone, WmrFeatureSetId);
            if (featureSet != null)
            {
                featureSet.isEnabled = true;
                OpenXRFeatureSetManager.SetFeaturesFromEnabledFeatureSets(BuildTargetGroup.Standalone);
            }

            OpenXRSettings settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Standalone);
            if (settings == null)
            {
                throw new InvalidOperationException("Could not get OpenXR settings for Standalone.");
            }

            SetFeatureEnabledById(BuildTargetGroup.Standalone, MixedRealityFeatureId, true);
            SetFeatureEnabledById(BuildTargetGroup.Standalone, MrHandTrackingFeatureId, true);
            SetFeatureEnabledById(BuildTargetGroup.Standalone, EyeGazeFeatureId, true);
            SetFeatureEnabledByTypeName(BuildTargetGroup.Standalone, "MicrosoftHandInteraction", true);
            SetFeatureEnabledById(BuildTargetGroup.Standalone, PlayModeRemotingFeatureId, true);

            // Keep packaged app remoting off; this setup is Play Mode only.
            SetFeatureEnabledById(BuildTargetGroup.Standalone, AppRemotingFeatureId, false);

            EditorUtility.SetDirty(settings);
        }

        private static void SetFeatureEnabledByTypeName(BuildTargetGroup group, string typeName, bool enabled)
        {
            OpenXRSettings settings = OpenXRSettings.GetSettingsForBuildTargetGroup(group);
            if (settings == null)
            {
                throw new InvalidOperationException("Could not get OpenXR settings for " + group + ".");
            }

            FieldInfo field = typeof(OpenXRSettings).GetField("features", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            var features = field != null ? field.GetValue(settings) as UnityEngine.XR.OpenXR.Features.OpenXRFeature[] : null;
            if (features == null)
            {
                throw new InvalidOperationException("Could not enumerate OpenXR features for " + group + ".");
            }

            foreach (var feature in features)
            {
                if (feature != null && feature.GetType().Name == typeName)
                {
                    feature.enabled = enabled;
                    EditorUtility.SetDirty(feature);
                    return;
                }
            }

            throw new InvalidOperationException("OpenXR feature type not found for " + group + ": " + typeName);
        }

        private static void SetFeatureEnabledById(BuildTargetGroup group, string featureId, bool enabled)
        {
            var feature = FeatureHelpers.GetFeatureWithIdForBuildTarget(group, featureId);
            if (feature == null)
            {
                throw new InvalidOperationException("OpenXR feature id not found for " + group + ": " + featureId);
            }

            feature.enabled = enabled;
            EditorUtility.SetDirty(feature);
        }
    }
}
