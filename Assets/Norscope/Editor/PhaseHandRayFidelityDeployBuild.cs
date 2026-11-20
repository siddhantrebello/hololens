using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Norscope.Editor
{
    /// <summary>
    /// Build-only UWP export for Hand Ray Fidelity redeploy.
    /// Does not modify OpenXR, Aim Pose, masks, raycasters, or interaction settings.
    /// </summary>
    public static class PhaseHandRayFidelityDeployBuild
    {
        private const string BootstrapScenePath = "Assets/Norscope/Scenes/Bootstrap.unity";
        private const string BuildOutputPath = "Builds/UWP";

        public static void BuildUwpArm64Only()
        {
            try
            {
                if (!File.Exists(BootstrapScenePath))
                    throw new FileNotFoundException("Bootstrap scene missing.", BootstrapScenePath);

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

                // Bump UWP package version so HL2 can update-install without uninstall
                // (uninstall would wipe LocalState calibration poses).
                Version current = PlayerSettings.WSA.packageVersion;
                if (current.Major == 0 && current.Minor == 0 && current.Build == 0 && current.Revision == 0)
                    current = new Version(1, 0, 0, 0);
                PlayerSettings.WSA.packageVersion = new Version(
                    current.Major, current.Minor, current.Build, current.Revision + 1);

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

                Debug.Log("[Norscope Hand Ray Fidelity] Fresh UWP D3D ARM64 export (no interaction setting changes)...");
                var report = BuildPipeline.BuildPlayer(options);
                if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                    throw new Exception("Unity UWP build failed: " + report.summary.result);

                Debug.Log("[Norscope Hand Ray Fidelity] Unity export succeeded: " + BuildOutputPath +
                          "\nPackage product version: " + PlayerSettings.bundleVersion);
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError("[Norscope Hand Ray Fidelity] Build failed:\n" + ex);
                EditorApplication.Exit(1);
            }
        }
    }
}
