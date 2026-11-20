using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Norscope.Editor
{
    /// <summary>
    /// Ask AI Phase 3: one still image via PhotoCapture behind IAskAiCameraCapture.
    /// Processing still uses MockAskAiService for the answer.
    /// </summary>
    public static class PhaseAskAiCameraConfigurator
    {
        private const string Menu = "Norscope/HoloLens/Ask AI Camera/";
        private const string BackupRoot = "Assets/Norscope/UI/Panels/_Backups";
        private const string BackupKey = "Norscope.AskAI.Camera.Backup";
        private const string ApplyFlag = "Temp/NorscopeAskAiCameraApply.flag";
        private const string ValidateFlag = "Temp/NorscopeAskAiCameraValidate.flag";
        private const string CaptureFlag = "Temp/NorscopeAskAiCameraCapture.flag";

        private static readonly string[] RuntimeFiles =
        {
            "Assets/Norscope/Runtime/AskAI/IAskAiCameraCapture.cs",
            "Assets/Norscope/Runtime/AskAI/AskAiCameraEvent.cs",
            "Assets/Norscope/Runtime/AskAI/AskAiCapturedImage.cs",
            "Assets/Norscope/Runtime/AskAI/AskAiRequest.cs",
            "Assets/Norscope/Runtime/AskAI/AskAiCameraCaptureFactory.cs",
            "Assets/Norscope/Runtime/AskAI/WsaPhotoCaptureProvider.cs",
            "Assets/Norscope/Runtime/AskAI/EditorMockCameraCapture.cs",
            "Assets/Norscope/Runtime/AskAI/UnavailableCameraCapture.cs",
            "Assets/Norscope/Runtime/AskAI/AskAiWorkflowController.cs",
            "Assets/Norscope/Runtime/AskAI/AskAiWorkflowState.cs",
            "Assets/Norscope/Runtime/AskAI/AskAiView.cs"
        };

        [InitializeOnLoadMethod]
        private static void PollFlags()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
        }

        private static void OnEditorUpdate()
        {
            if (File.Exists(CaptureFlag))
            {
                File.Delete(CaptureFlag);
                EditorApplication.delayCall += CaptureMenu;
                return;
            }

            if (File.Exists(ValidateFlag))
            {
                File.Delete(ValidateFlag);
                EditorApplication.delayCall += ValidateMenu;
                return;
            }

            if (File.Exists(ApplyFlag))
            {
                File.Delete(ApplyFlag);
                EditorApplication.delayCall += ApplyQuiet;
            }
        }

        [MenuItem(Menu + "1) Capture Baseline")]
        public static void CaptureMenu()
        {
            string folder = BackupRoot + "/AskAICamera_" +
                            DateTime.Now.ToString("yyyyMMdd_HHmmss");
            Directory.CreateDirectory(folder);
            Directory.CreateDirectory(folder + "/AskAI");
            FileUtil.CopyFileOrDirectory(
                "Assets/Norscope/UI/Panels/AskAIPanel.prefab",
                folder + "/AskAIPanel.prefab");
            FileUtil.CopyFileOrDirectory(
                "ProjectSettings/ProjectSettings.asset",
                folder + "/ProjectSettings.asset");
            FileUtil.CopyFileOrDirectory(
                "Assets/Norscope/Editor/Phase2DeployConfigurator.cs",
                folder + "/Phase2DeployConfigurator.cs.bak");
            foreach (string path in RuntimeFiles)
            {
                if (File.Exists(path))
                    FileUtil.CopyFileOrDirectory(
                        path,
                        folder + "/AskAI/" + Path.GetFileName(path) + ".bak");
            }

            File.WriteAllText(
                folder + "/manifest.txt",
                "Ask AI Phase 3 Camera baseline " + DateTime.Now.ToString("O") + "\n",
                Encoding.UTF8);
            EditorPrefs.SetString(BackupKey, folder);
            AssetDatabase.Refresh();
            Debug.Log("[Norscope Ask AI Camera] Baseline READY\n" + folder);
        }

        [MenuItem(Menu + "2) Dry Run")]
        public static void DryRunMenu()
        {
            bool ready = DryRun(out string report);
            Directory.CreateDirectory("Temp");
            File.WriteAllText("Temp/NorscopeAskAiCameraDryRun.txt", report, Encoding.UTF8);
            Debug.Log((ready
                ? "[Norscope Ask AI Camera] Dry run READY\n"
                : "[Norscope Ask AI Camera] Dry run BLOCKED\n") + report);
        }

        [MenuItem(Menu + "3) Apply")]
        public static void ApplyMenu()
        {
            if (!DryRun(out string report))
            {
                Debug.LogError("[Norscope Ask AI Camera] Apply BLOCKED\n" + report);
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "Apply Ask AI Phase 3 Camera",
                    "Add Capturing state, wire PhotoCapture behind IAskAiCameraCapture, " +
                    "preserve WebCam+Microphone, keep Processing on MockAskAiService?\n\n" +
                    "No backend/LLM/upload/TTS.",
                    "Apply",
                    "Cancel"))
                return;

            ApplyQuiet();
        }

        public static void ApplyFromAutomation()
        {
            ApplyQuiet();
        }

        public static void ValidateFromAutomation()
        {
            ValidateMenu();
        }

        private static void ApplyQuiet()
        {
            if (LatestBackup() == null)
                CaptureMenu();

            EnableCameraCapabilities();
            EnsurePhase2DeployPreservesCapabilities();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateMenu();
            Debug.Log("[Norscope Ask AI Camera] Apply COMPLETE");
        }

        [MenuItem(Menu + "4) Validate")]
        public static void ValidateMenu()
        {
            bool ok = Validate(out string report);
            Directory.CreateDirectory("Temp");
            File.WriteAllText("Temp/NorscopeAskAiCameraValidate.txt", report, Encoding.UTF8);
            if (ok)
                Debug.Log("[Norscope Ask AI Camera] Validation PASSED\n" + report);
            else
                Debug.LogError("[Norscope Ask AI Camera] Validation FAILED\n" + report);
        }

        [MenuItem(Menu + "5) Restore Baseline")]
        public static void RestoreMenu()
        {
            string folder = EditorPrefs.GetString(BackupKey, string.Empty);
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                folder = LatestBackup();
            }

            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                Debug.LogError("[Norscope Ask AI Camera] No baseline found.");
                return;
            }

            Debug.Log("[Norscope Ask AI Camera] Restore available at:\n" + folder);
        }

        private static string LatestBackup()
        {
            if (!Directory.Exists(BackupRoot)) return null;
            string[] dirs = Directory.GetDirectories(BackupRoot, "AskAICamera_*");
            Array.Sort(dirs);
            return dirs.Length > 0 ? dirs[dirs.Length - 1] : null;
        }

        private static bool DryRun(out string report)
        {
            var lines = new StringBuilder();
            var blockers = new List<string>();
            lines.AppendLine("NORSCOPE ASK AI PHASE 3 CAMERA — DRY RUN");
            lines.AppendLine("Generated: " + DateTime.Now.ToString("O"));
            if (!File.Exists("Assets/Norscope/UI/Panels/AskAIPanel.prefab"))
                blockers.Add("AskAIPanel.prefab missing.");
            if (!File.Exists("Assets/Norscope/Runtime/AskAI/AskAiWorkflowController.cs"))
                blockers.Add("AskAiWorkflowController missing.");
            lines.AppendLine("PLAN  Capturing state + PhotoCapture behind IAskAiCameraCapture");
            lines.AppendLine("PLAN  Closest resolution to 1280x720; JPEG q75; 10s timeout");
            lines.AppendLine("PLAN  Processing still MockAskAiService.GetMockAnswer()");
            lines.AppendLine("PLAN  WebCam=true Microphone=true preserved in Phase2Deploy");
            foreach (string blocker in blockers)
                lines.AppendLine("BLOCKER " + blocker);
            lines.AppendLine(blockers.Count == 0 ? "STATUS: READY" : "STATUS: BLOCKED");
            report = lines.ToString();
            return blockers.Count == 0;
        }

        private static void EnableCameraCapabilities()
        {
            PlayerSettings.WSA.SetCapability(PlayerSettings.WSACapability.WebCam, true);
            PlayerSettings.WSA.SetCapability(PlayerSettings.WSACapability.Microphone, true);
            Debug.Log("[Norscope Ask AI Camera] WSA WebCam+Microphone enabled.");
        }

        private static void EnsurePhase2DeployPreservesCapabilities()
        {
            string path = "Assets/Norscope/Editor/Phase2DeployConfigurator.cs";
            string source = File.ReadAllText(path);
            bool changed = false;

            if (source.Contains(
                    "SetCapability(PlayerSettings.WSACapability.Microphone, false)"))
            {
                source = source.Replace(
                    "SetCapability(PlayerSettings.WSACapability.Microphone, false)",
                    "SetCapability(PlayerSettings.WSACapability.Microphone, true)");
                changed = true;
            }

            if (source.Contains(
                    "SetCapability(PlayerSettings.WSACapability.WebCam, false)"))
            {
                source = source.Replace(
                    "SetCapability(PlayerSettings.WSACapability.WebCam, false)",
                    "SetCapability(PlayerSettings.WSACapability.WebCam, true)");
                changed = true;
            }

            if (!source.Contains(
                    "SetCapability(PlayerSettings.WSACapability.WebCam, true)"))
            {
                const string needle =
                    "SetCapability(PlayerSettings.WSACapability.Microphone, true);";
                int idx = source.IndexOf(needle, StringComparison.Ordinal);
                if (idx < 0)
                    throw new InvalidOperationException(
                        "Phase2DeployConfigurator missing Microphone=true anchor.");
                source = source.Insert(
                    idx + needle.Length,
                    "\n            PlayerSettings.WSA.SetCapability(" +
                    "PlayerSettings.WSACapability.WebCam, true);");
                changed = true;
            }

            if (changed)
            {
                File.WriteAllText(path, source, Encoding.UTF8);
                AssetDatabase.ImportAsset(path);
            }
        }

        private static bool Validate(out string report)
        {
            var lines = new StringBuilder();
            bool ok = true;
            void Check(bool condition, string label)
            {
                lines.AppendLine((condition ? "PASS  " : "FAIL  ") + label);
                ok &= condition;
            }

            foreach (string path in RuntimeFiles)
                Check(File.Exists(path), "Exists " + Path.GetFileName(path));

            string controller = File.ReadAllText(
                "Assets/Norscope/Runtime/AskAI/AskAiWorkflowController.cs");
            string state = File.ReadAllText(
                "Assets/Norscope/Runtime/AskAI/AskAiWorkflowState.cs");
            string factory = File.ReadAllText(
                "Assets/Norscope/Runtime/AskAI/AskAiCameraCaptureFactory.cs");
            string wsa = File.ReadAllText(
                "Assets/Norscope/Runtime/AskAI/WsaPhotoCaptureProvider.cs");
            string editorMock = File.ReadAllText(
                "Assets/Norscope/Runtime/AskAI/EditorMockCameraCapture.cs");
            string request = File.ReadAllText(
                "Assets/Norscope/Runtime/AskAI/AskAiRequest.cs");
            string mockAnswer = File.ReadAllText(
                "Assets/Norscope/Runtime/AskAI/MockAskAiService.cs");
            string deploy = File.ReadAllText(
                "Assets/Norscope/Editor/Phase2DeployConfigurator.cs");

            Check(state.Contains("Capturing"),
                "AskAiWorkflowState includes Capturing");
            Check(controller.Contains("IAskAiCameraCapture") &&
                  controller.Contains("AskAiCameraCaptureFactory") &&
                  !controller.Contains("using UnityEngine.Windows.WebCam") &&
                  !controller.Contains("PhotoCapture"),
                "Controller coordinates camera via abstraction only");
            Check(controller.Contains("MaxCapturingSeconds = 10f"),
                "Controller camera safety timeout is 10 seconds");
            Check(controller.Contains("Capturing the scene"),
                "Capturing status text is authored");
            Check(controller.Contains("AskAiWorkflowState.Capturing") &&
                  controller.Contains("AskAiWorkflowState.Listening") &&
                  controller.Contains("AskAiWorkflowState.Processing"),
                "Busy states include Listening/Capturing/Processing");
            Check((controller.Contains("GetMockAnswer()") ||
                   controller.Contains("IAskAiAnalysisService")) &&
                  mockAnswer.Contains("DefaultAnswer"),
                "Processing still uses MockAskAiService answer");
            Check(controller.Contains("_currentRequest.Clear()"),
                "AskAiRequest is cleared on cancel/error/session reset");
            Check(request.Contains("ImageBytes") &&
                  request.Contains("Transcript"),
                "AskAiRequest holds transcript + image bytes");
            Check(wsa.Contains("TargetWidth = 1280") &&
                  wsa.Contains("TargetHeight = 720") &&
                  wsa.Contains("SelectClosestResolution") &&
                  wsa.Contains("JpegQuality = 75"),
                "WSA targets closest-to-1280x720 JPEG q75");
            Check(wsa.Contains("EncodeToJPG") &&
                  wsa.Contains("CopyRawImageDataIntoBuffer"),
                "WSA encodes in-memory JPEG from PhotoCaptureFrame");
            Check(wsa.Contains("#if UNITY_WSA && !UNITY_EDITOR"),
                "WSA PhotoCapture is compilation-guarded");
            Check(factory.Contains("UNITY_EDITOR") &&
                  factory.Contains("EditorMockCameraCapture") &&
                  factory.Contains("UNITY_WSA") &&
                  factory.Contains("WsaPhotoCaptureProvider"),
                "Factory selects Editor mock vs WSA camera");
            Check(editorMock.Contains("EncodeToJPG") &&
                  editorMock.Contains("MockWidth"),
                "Editor mock produces deterministic JPEG");
            Check(PlayerSettings.WSA.GetCapability(PlayerSettings.WSACapability.WebCam),
                "PlayerSettings WSA WebCam capability is enabled");
            Check(PlayerSettings.WSA.GetCapability(PlayerSettings.WSACapability.Microphone),
                "PlayerSettings WSA Microphone capability is enabled");
            Check(deploy.Contains(
                      "SetCapability(PlayerSettings.WSACapability.WebCam, true)") &&
                  !deploy.Contains(
                      "SetCapability(PlayerSettings.WSACapability.WebCam, false)") &&
                  deploy.Contains(
                      "SetCapability(PlayerSettings.WSACapability.Microphone, true)") &&
                  !deploy.Contains(
                      "SetCapability(PlayerSettings.WSACapability.Microphone, false)"),
                "Phase2DeployConfigurator preserves WebCam+Microphone");
            Check(!controller.Contains("OpenAI") &&
                  !controller.Contains("UnityWebRequest") &&
                  !controller.Contains("HttpClient") &&
                  !wsa.Contains("OpenAI") &&
                  !controller.Contains("TextToSpeech") &&
                  !controller.Contains("OCR"),
                "No backend/LLM/upload/TTS/OCR added");
            Check(!EditorUtility.scriptCompilationFailed, "Zero C# compile errors");

            lines.AppendLine();
            lines.AppendLine("CAMERA ABSTRACTION:");
            lines.AppendLine("  IAskAiCameraCapture");
            lines.AppendLine("  WsaPhotoCaptureProvider (UNITY_WSA && !UNITY_EDITOR)");
            lines.AppendLine("  EditorMockCameraCapture (UNITY_EDITOR)");
            lines.AppendLine("RESOLUTION: closest SupportedResolutions to 1280x720");
            lines.AppendLine("JPEG: EncodeToJPG quality 75; MaxCapture=10s");
            lines.AppendLine("CONFIRM: Processing still uses MockAskAiService.GetMockAnswer()");
            lines.AppendLine("CONFIRM: no backend/LLM/upload/TTS/OCR/detection");
            lines.AppendLine(
                "NOTE: Package.appxmanifest webcam capability is generated at UWP build;");
            lines.AppendLine(
                "      verify after Phase2 UWP build. Physical HL2 camera tests remain.");

            report = lines.ToString();
            return ok;
        }
    }
}
