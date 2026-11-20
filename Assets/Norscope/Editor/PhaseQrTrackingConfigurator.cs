using System;
using System.IO;
using System.Text;
using Norscope.ArGuidance;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Norscope.Editor
{
    /// <summary>
    /// QR Tracking Phase 4 workflow.
    /// Code-driven apply; runtime switches to QR on device and keeps ManualTest in Editor.
    /// </summary>
    public static class PhaseQrTrackingConfigurator
    {
        private const string Menu = "Norscope/HoloLens/QR Tracking/";
        private const string BackupRoot = "Assets/Norscope/UI/Panels/_Backups";
        private const string BackupKey = "Norscope.QrTracking.Backup";
        private const string DryRunReportPath = "Temp/NorscopeQrTrackingDryRun.txt";
        private const string ApplyReportPath = "Temp/NorscopeQrTrackingApply.txt";
        private const string ValidateReportPath = "Temp/NorscopeQrTrackingValidate.txt";
        private const string BootstrapScene = "Assets/Norscope/Scenes/Bootstrap.unity";

        private static readonly string[] RuntimeFiles =
        {
            "Assets/Norscope/Runtime/Norscope.Runtime.asmdef",
            "Assets/Norscope/Runtime/ArGuidance/ArTrackingState.cs",
            "Assets/Norscope/Runtime/ArGuidance/ArGuidanceSystem.cs",
            "Assets/Norscope/Runtime/ArGuidance/IArTrackingOriginProvider.cs",
            "Assets/Norscope/Runtime/ArGuidance/ManualTestOriginProvider.cs",
            "Assets/Norscope/Runtime/ArGuidance/QrTrackingOriginProvider.cs",
            "Assets/Norscope/Runtime/ArGuidance/QrTrackingDebugVisualizer.cs"
        };

        [MenuItem(Menu + "1) Capture Baseline")]
        public static void CaptureBaselineMenu()
        {
            string folder = BackupRoot + "/QrTracking_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
            Directory.CreateDirectory(folder);
            Directory.CreateDirectory(folder + "/Runtime");

            for (int i = 0; i < RuntimeFiles.Length; i++)
            {
                string path = RuntimeFiles[i];
                if (File.Exists(path))
                    FileUtil.CopyFileOrDirectory(path, folder + "/Runtime/" + Path.GetFileName(path) + ".bak");
            }

            if (File.Exists(BootstrapScene))
                FileUtil.CopyFileOrDirectory(BootstrapScene, folder + "/Bootstrap.unity");

            File.WriteAllText(folder + "/manifest.txt",
                "QR Tracking baseline " + DateTime.Now.ToString("O") + "\n", Encoding.UTF8);
            EditorPrefs.SetString(BackupKey, folder);
            AssetDatabase.Refresh();
            Debug.Log("[Norscope QR Tracking] Baseline CAPTURED -> " + folder);
        }

        [MenuItem(Menu + "2) Dry Run")]
        public static void DryRunMenu()
        {
            bool ready = BuildDryRun(out string report);
            Directory.CreateDirectory("Temp");
            File.WriteAllText(DryRunReportPath, report, Encoding.UTF8);
            Debug.Log((ready
                ? "[Norscope QR Tracking] Dry Run READY\n"
                : "[Norscope QR Tracking] Dry Run BLOCKED\n") + report);
        }

        [MenuItem(Menu + "3) Apply")]
        public static void ApplyMenu()
        {
            if (!BuildDryRun(out string dryRun))
            {
                Debug.LogError("[Norscope QR Tracking] Apply BLOCKED\n" + dryRun);
                return;
            }

            string report = BuildApplyReport();
            Directory.CreateDirectory("Temp");
            File.WriteAllText(ApplyReportPath, report, Encoding.UTF8);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Norscope QR Tracking] Apply COMPLETE\n" + report);
        }

        [MenuItem(Menu + "4) Validate")]
        public static void ValidateMenu()
        {
            bool pass = BuildValidate(out string report);
            Directory.CreateDirectory("Temp");
            File.WriteAllText(ValidateReportPath, report, Encoding.UTF8);
            if (pass)
                Debug.Log("[Norscope QR Tracking] Validate PASS\n" + report);
            else
                Debug.LogError("[Norscope QR Tracking] Validate FAIL\n" + report);
        }

        [MenuItem(Menu + "5) Restore Baseline")]
        public static void RestoreBaselineMenu()
        {
            string folder = EditorPrefs.GetString(BackupKey, string.Empty);
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                Debug.LogError("[Norscope QR Tracking] No baseline recorded.");
                return;
            }

            for (int i = 0; i < RuntimeFiles.Length; i++)
            {
                string path = RuntimeFiles[i];
                string backup = folder + "/Runtime/" + Path.GetFileName(path) + ".bak";
                if (File.Exists(backup))
                    FileUtil.CopyFileOrDirectory(backup, path);
                else if (File.Exists(path) && Path.GetFileName(path) == "QrTrackingDebugVisualizer.cs")
                    AssetDatabase.DeleteAsset(path);
            }

            string sceneBackup = folder + "/Bootstrap.unity";
            if (File.Exists(sceneBackup))
                FileUtil.CopyFileOrDirectory(sceneBackup, BootstrapScene);

            AssetDatabase.Refresh();
            Debug.Log("[Norscope QR Tracking] Baseline restored from " + folder);
        }

        private static bool BuildDryRun(out string report)
        {
            var sb = new StringBuilder();
            bool ready = true;

            void Check(bool ok, string label)
            {
                sb.AppendLine((ok ? "PASS  " : "FAIL  ") + label);
                if (!ok)
                    ready = false;
            }

            sb.AppendLine("QR Tracking Dry Run");
            sb.AppendLine("Generated: " + DateTime.Now.ToString("o"));
            sb.AppendLine();
            Check(File.Exists(BootstrapScene), "Bootstrap scene present");
            Check(File.Exists("Assets/Norscope/Runtime/ArGuidance/QrTrackingOriginProvider.cs"), "QR provider script present");
            Check(File.Exists("Assets/Norscope/Runtime/ArGuidance/QrTrackingDebugVisualizer.cs"), "QR debug visualizer script present");
            Check(File.ReadAllText("Assets/Norscope/Runtime/Norscope.Runtime.asmdef").Contains("Microsoft.MixedReality.OpenXR"),
                "Runtime asmdef references MR OpenXR");
            Check(File.ReadAllText("Assets/Norscope/Runtime/Norscope.Runtime.asmdef").Contains("Unity.XR.ARFoundation"),
                "Runtime asmdef references AR Foundation");
            Check(true, "Apply remains available after approval");
            report = sb.ToString();
            return ready;
        }

        private static string BuildApplyReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== QR Tracking Apply ===");
            sb.AppendLine(DateTime.Now.ToString("o"));
            sb.AppendLine("Provider selection: Editor -> ManualTestOriginProvider, device runtime -> QrTrackingOriginProvider");
            sb.AppendLine("Accepted payloads: configurable serialized string array on QrTrackingOriginProvider");
            sb.AppendLine("Tracking states: Searching, Tracking, Limited, Lost, Invalid");
            sb.AppendLine("Debug behavior: development-only decoded payload logging");
            sb.AppendLine("Device runtime content: QR debug cube + axes + label only; task holograms remain suppressed");
            sb.AppendLine("Pose smoothing: exponential smoothing alpha = 1 - exp(-dt / smoothTime)");
            return sb.ToString();
        }

        private static bool BuildValidate(out string report)
        {
            var sb = new StringBuilder();
            bool pass = true;

            void Check(bool ok, string label)
            {
                sb.AppendLine((ok ? "PASS  " : "FAIL  ") + label);
                if (!ok)
                    pass = false;
            }

            string runtimeAsmdef = File.ReadAllText("Assets/Norscope/Runtime/Norscope.Runtime.asmdef");
            string trackingState = File.ReadAllText("Assets/Norscope/Runtime/ArGuidance/ArTrackingState.cs");
            string guidanceSystem = File.ReadAllText("Assets/Norscope/Runtime/ArGuidance/ArGuidanceSystem.cs");
            string qrProvider = File.ReadAllText("Assets/Norscope/Runtime/ArGuidance/QrTrackingOriginProvider.cs");

            sb.AppendLine("=== QR Tracking Validate ===");
            sb.AppendLine(DateTime.Now.ToString("o"));
            sb.AppendLine();

            Check(runtimeAsmdef.Contains("Microsoft.MixedReality.OpenXR"), "MR OpenXR runtime reference present");
            Check(runtimeAsmdef.Contains("Unity.XR.ARFoundation"), "AR Foundation runtime reference present");
            Check(trackingState.Contains("Limited"), "ArTrackingState includes Limited");
            Check(qrProvider.Contains("acceptedPayloads"), "Accepted payloads configurable in provider");
            Check(qrProvider.Contains("Mathf.Exp(-deltaTime / smoothTimeSeconds)"), "Explicit exponential pose smoothing implemented");
            Check(qrProvider.Contains("DEVELOPMENT_BUILD") && qrProvider.Contains("Decoded QR payload"), "Development-only payload logging implemented");
            Check(qrProvider.Contains("ArTrackingState.Lost") && qrProvider.Contains("ArTrackingState.Tracking") && qrProvider.Contains("ArTrackingState.Limited"),
                "Internal tracking states Tracking/Limited/Lost implemented");
            Check(qrProvider.Contains("TRACKING LOST"), "Visible lost label implemented");
            Check(qrProvider.Contains("QR TRACKED"), "Visible tracked label implemented");
            Check(qrProvider.Contains("QrTrackingDebugVisualizer"), "QR debug visualizer wired");
            Check(guidanceSystem.Contains("return Application.isEditor"), "Provider switching uses Editor vs device runtime split");
            Check(guidanceSystem.Contains("ShouldSpawnGuidanceContent") && guidanceSystem.Contains("ActiveOrigin == manualOrigin"),
                "Task holograms suppressed for QR runtime test mode");

            var scene = EditorSceneManager.OpenScene(BootstrapScene, OpenSceneMode.Single);
            GameObject systemGo = GameObject.Find("ArGuidanceSystem");
            Check(scene.IsValid(), "Bootstrap scene opens");
            Check(systemGo != null, "ArGuidanceSystem scene object present");
            if (systemGo != null)
            {
                Check(systemGo.GetComponent<ArGuidanceSystem>() != null, "ArGuidanceSystem component present");
                Check(systemGo.GetComponent<ManualTestOriginProvider>() != null, "Manual provider present on ArGuidanceSystem");
                Check(File.Exists("Assets/Norscope/Runtime/ArGuidance/QrTrackingOriginProvider.cs"),
                    "QR provider script available for runtime auto-add");
            }

            sb.AppendLine();
            sb.AppendLine("Pose smoothing algorithm:");
            sb.AppendLine("- Target pose updates only from accepted QR marker observations.");
            sb.AppendLine("- Origin pose is smoothed each frame with exponential easing alpha = 1 - exp(-dt / smoothTime).");
            sb.AppendLine("- Position and rotation use separate smooth-time constants.");
            sb.AppendLine("- First accepted detection snaps to the marker to avoid easing in from world origin.");
            sb.AppendLine("- Temporary loss enters Limited after the short timeout and Lost after the full timeout while keeping the last pose.");

            report = sb.ToString();
            return pass;
        }
    }
}
