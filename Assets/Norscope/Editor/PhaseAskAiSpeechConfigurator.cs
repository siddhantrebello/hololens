using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Norscope.AskAI;
using UnityEditor;
using UnityEngine;

namespace Norscope.Editor
{
    /// <summary>
    /// Ask AI Phase 2: real HoloLens dictation behind IAskAiSpeechCapture.
    /// Processing still uses MockAskAiService for the answer.
    /// </summary>
    public static class PhaseAskAiSpeechConfigurator
    {
        private const string Menu = "Norscope/HoloLens/Ask AI Speech/";
        private const string BackupRoot = "Assets/Norscope/UI/Panels/_Backups";
        private const string BackupKey = "Norscope.AskAI.Speech.Backup";
        private const string ApplyFlag = "Temp/NorscopeAskAiSpeechApply.flag";
        private const string ValidateFlag = "Temp/NorscopeAskAiSpeechValidate.flag";
        private const string CaptureFlag = "Temp/NorscopeAskAiSpeechCapture.flag";

        private static readonly string[] RuntimeFiles =
        {
            "Assets/Norscope/Runtime/AskAI/IAskAiSpeechCapture.cs",
            "Assets/Norscope/Runtime/AskAI/AskAiSpeechEvent.cs",
            "Assets/Norscope/Runtime/AskAI/AskAiSpeechCaptureFactory.cs",
            "Assets/Norscope/Runtime/AskAI/WsaDictationSpeechCapture.cs",
            "Assets/Norscope/Runtime/AskAI/EditorMockSpeechCapture.cs",
            "Assets/Norscope/Runtime/AskAI/UnavailableSpeechCapture.cs",
            "Assets/Norscope/Runtime/AskAI/AskAiWorkflowController.cs",
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
            string folder = BackupRoot + "/AskAISpeech_" +
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
                "Ask AI Phase 2 Speech baseline " + DateTime.Now.ToString("O") + "\n",
                Encoding.UTF8);
            EditorPrefs.SetString(BackupKey, folder);
            AssetDatabase.Refresh();
            Debug.Log("[Norscope Ask AI Speech] Baseline READY\n" + folder);
        }

        [MenuItem(Menu + "2) Dry Run")]
        public static void DryRunMenu()
        {
            bool ready = DryRun(out string report);
            Directory.CreateDirectory("Temp");
            File.WriteAllText("Temp/NorscopeAskAiSpeechDryRun.txt", report, Encoding.UTF8);
            Debug.Log((ready
                ? "[Norscope Ask AI Speech] Dry run READY\n"
                : "[Norscope Ask AI Speech] Dry run BLOCKED\n") + report);
        }

        [MenuItem(Menu + "3) Apply")]
        public static void ApplyMenu()
        {
            if (!DryRun(out string report))
            {
                Debug.LogError("[Norscope Ask AI Speech] Apply BLOCKED\n" + report);
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "Apply Ask AI Phase 2 Speech",
                    "Enable Microphone, wire DictationRecognizer behind IAskAiSpeechCapture, " +
                    "and keep Processing on MockAskAiService?\n\nNo camera/LLM/TTS.",
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

            EnableMicrophoneCapability();
            EnsurePhase2DeployPreservesMicrophone();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateMenu();
            Debug.Log("[Norscope Ask AI Speech] Apply COMPLETE");
        }

        [MenuItem(Menu + "4) Validate")]
        public static void ValidateMenu()
        {
            bool ok = Validate(out string report);
            Directory.CreateDirectory("Temp");
            File.WriteAllText("Temp/NorscopeAskAiSpeechValidate.txt", report, Encoding.UTF8);
            if (ok)
                Debug.Log("[Norscope Ask AI Speech] Validation PASSED\n" + report);
            else
                Debug.LogError("[Norscope Ask AI Speech] Validation FAILED\n" + report);
        }

        private static string LatestBackup()
        {
            if (!Directory.Exists(BackupRoot)) return null;
            string[] dirs = Directory.GetDirectories(BackupRoot, "AskAISpeech_*");
            Array.Sort(dirs);
            return dirs.Length > 0 ? dirs[dirs.Length - 1] : null;
        }

        private static bool DryRun(out string report)
        {
            var lines = new StringBuilder();
            var blockers = new List<string>();
            lines.AppendLine("NORSCOPE ASK AI PHASE 2 SPEECH — DRY RUN");
            lines.AppendLine("Generated: " + DateTime.Now.ToString("O"));
            if (!File.Exists("Assets/Norscope/UI/Panels/AskAIPanel.prefab"))
                blockers.Add("AskAIPanel.prefab missing (complete Phase 1 first).");
            if (!File.Exists("Assets/Norscope/Runtime/AskAI/AskAiWorkflowController.cs"))
                blockers.Add("AskAiWorkflowController missing.");
            lines.AppendLine("PLAN  DictationRecognizer behind IAskAiSpeechCapture");
            lines.AppendLine("PLAN  InitialSilence=5.0 AutoSilence=1.2 MaxListen=15");
            lines.AppendLine("PLAN  Processing still MockAskAiService.GetMockAnswer()");
            lines.AppendLine("PLAN  Microphone capability = true; Phase2Deploy preserves it");
            foreach (string blocker in blockers)
                lines.AppendLine("BLOCKER " + blocker);
            lines.AppendLine(blockers.Count == 0 ? "STATUS: READY" : "STATUS: BLOCKED");
            report = lines.ToString();
            return blockers.Count == 0;
        }

        private static void EnableMicrophoneCapability()
        {
            PlayerSettings.WSA.SetCapability(PlayerSettings.WSACapability.Microphone, true);
            Debug.Log("[Norscope Ask AI Speech] WSA Microphone capability enabled.");
        }

        private static void EnsurePhase2DeployPreservesMicrophone()
        {
            string path = "Assets/Norscope/Editor/Phase2DeployConfigurator.cs";
            string source = File.ReadAllText(path);
            if (source.Contains(
                    "SetCapability(PlayerSettings.WSACapability.Microphone, false)"))
            {
                source = source.Replace(
                    "SetCapability(PlayerSettings.WSACapability.Microphone, false)",
                    "SetCapability(PlayerSettings.WSACapability.Microphone, true)");
                File.WriteAllText(path, source, Encoding.UTF8);
                AssetDatabase.ImportAsset(path);
            }

            if (!source.Contains(
                    "SetCapability(PlayerSettings.WSACapability.Microphone, true)"))
            {
                throw new InvalidOperationException(
                    "Phase2DeployConfigurator does not preserve Microphone=true.");
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
            string factory = File.ReadAllText(
                "Assets/Norscope/Runtime/AskAI/AskAiSpeechCaptureFactory.cs");
            string wsa = File.ReadAllText(
                "Assets/Norscope/Runtime/AskAI/WsaDictationSpeechCapture.cs");
            string editorMock = File.ReadAllText(
                "Assets/Norscope/Runtime/AskAI/EditorMockSpeechCapture.cs");
            string mockAnswer = File.ReadAllText(
                "Assets/Norscope/Runtime/AskAI/MockAskAiService.cs");
            string deploy = File.ReadAllText(
                "Assets/Norscope/Editor/Phase2DeployConfigurator.cs");
            string view = File.ReadAllText(
                "Assets/Norscope/Runtime/AskAI/AskAiView.cs");

            Check(controller.Contains("IAskAiSpeechCapture") &&
                  controller.Contains("AskAiSpeechCaptureFactory") &&
                  !controller.Contains("using UnityEngine.Windows.Speech") &&
                  !System.Text.RegularExpressions.Regex.IsMatch(
                      controller,
                      @"\bnew\s+DictationRecognizer\b|\bDictationRecognizer\s+\w+"),
                "Controller coordinates speech via abstraction only");
            Check(controller.Contains("MaxListeningSeconds = 15f"),
                "Controller safety timeout is 15 seconds");
            Check((controller.Contains("GetMockAnswer()") ||
                   controller.Contains("IAskAiAnalysisService")) &&
                  mockAnswer.Contains("DefaultAnswer"),
                "Processing uses mock answer service or analysis abstraction");
            Check(!controller.Contains("GetMockTranscript()") ||
                  controller.Contains("IAskAiSpeechCapture"),
                "Listening no longer depends solely on mock transcript timer");
            Check(controller.Contains("No speech detected. Please try again."),
                "Empty completion maps to No speech detected");
            Check(wsa.Contains("InitialSilenceTimeoutSeconds = 5f") &&
                  wsa.Contains("AutoSilenceTimeoutSeconds = 1.2f"),
                "WSA timeouts InitialSilence=5.0 AutoSilence=1.2");
            Check(wsa.Contains("DictationHypothesis") &&
                  wsa.Contains("DictationResult") &&
                  wsa.Contains("DictationComplete") &&
                  wsa.Contains("DictationError"),
                "WSA uses full DictationRecognizer lifecycle");
            Check(wsa.Contains("#if UNITY_WSA && !UNITY_EDITOR"),
                "WSA DictationRecognizer is compilation-guarded");
            Check(factory.Contains("UNITY_EDITOR") &&
                  factory.Contains("EditorMockSpeechCapture") &&
                  factory.Contains("UNITY_WSA") &&
                  factory.Contains("WsaDictationSpeechCapture"),
                "Factory selects Editor mock vs WSA capture");
            Check(editorMock.Contains("[Editor Mock]"),
                "Editor mock transcript is clearly identified");
            Check(view.Contains("SetBodyText"),
                "View supports hypothesis/final body updates");
            Check(PlayerSettings.WSA.GetCapability(PlayerSettings.WSACapability.Microphone),
                "PlayerSettings WSA Microphone capability is enabled");
            Check(deploy.Contains(
                      "SetCapability(PlayerSettings.WSACapability.Microphone, true)") &&
                  !deploy.Contains(
                      "SetCapability(PlayerSettings.WSACapability.Microphone, false)"),
                "Phase2DeployConfigurator preserves Microphone=true");
            Check(!controller.Contains("OpenAI") &&
                  !controller.Contains("WebRequest") &&
                  !controller.Contains("using UnityEngine.Windows.WebCam") &&
                  !controller.Contains("PhotoCapture") &&
                  !wsa.Contains("WebRequest"),
                "No LLM/backend; camera not inlined in controller");
            Check(!EditorUtility.scriptCompilationFailed, "Zero C# compile errors");

            lines.AppendLine(string.Empty);
            lines.AppendLine("SPEECH ABSTRACTION:");
            lines.AppendLine("  IAskAiSpeechCapture");
            lines.AppendLine("  WsaDictationSpeechCapture (UNITY_WSA && !UNITY_EDITOR)");
            lines.AppendLine("  EditorMockSpeechCapture (UNITY_EDITOR)");
            lines.AppendLine("TIMEOUTS: InitialSilence=5.0s AutoSilence=1.2s MaxListen=15s");
            lines.AppendLine("CONFIRM: Processing still uses MockAskAiService.GetMockAnswer()");
            lines.AppendLine("CONFIRM: no camera/backend/LLM/TTS");
            lines.AppendLine("NOTE: Package.appxmanifest mic capability is generated at UWP build;");
            lines.AppendLine("      verify after Phase2 UWP build. Online/offline dictation requires device test.");

            report = lines.ToString();
            return ok;
        }
    }
}
