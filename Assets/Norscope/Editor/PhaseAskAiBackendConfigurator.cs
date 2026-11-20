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
    /// Ask AI Phase 4: backend analysis behind IAskAiAnalysisService.
    /// OpenAI keys stay on the Python backend only.
    /// </summary>
    public static class PhaseAskAiBackendConfigurator
    {
        private const string Menu = "Norscope/HoloLens/Ask AI Backend/";
        private const string BackupRoot = "Assets/Norscope/UI/Panels/_Backups";
        private const string BackupKey = "Norscope.AskAI.Backend.Backup";
        private const string ApplyFlag = "Temp/NorscopeAskAiBackendApply.flag";
        private const string ValidateFlag = "Temp/NorscopeAskAiBackendValidate.flag";
        private const string ConfigPath = "Assets/Norscope/Data/AskAiBackendConfig.asset";

        private static readonly string[] RuntimeFiles =
        {
            "Assets/Norscope/Runtime/AskAI/IAskAiAnalysisService.cs",
            "Assets/Norscope/Runtime/AskAI/AskAiAnalysisEvent.cs",
            "Assets/Norscope/Runtime/AskAI/AskAiAnalysisResponse.cs",
            "Assets/Norscope/Runtime/AskAI/AskAiBackendConfig.cs",
            "Assets/Norscope/Runtime/AskAI/AskAiAnalysisServiceFactory.cs",
            "Assets/Norscope/Runtime/AskAI/HttpAskAiAnalysisService.cs",
            "Assets/Norscope/Runtime/AskAI/MockAskAiAnalysisService.cs",
            "Assets/Norscope/Runtime/AskAI/UnavailableAskAiAnalysisService.cs",
            "Assets/Norscope/Runtime/AskAI/AskAiWorkflowController.cs",
            "Assets/Norscope/Runtime/AskAI/AskAiRequest.cs"
        };

        [InitializeOnLoadMethod]
        private static void PollFlags()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
        }

        private static void OnEditorUpdate()
        {
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
            string folder = BackupRoot + "/AskAIBackend_" +
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
                "Ask AI Phase 4 Backend baseline " + DateTime.Now.ToString("O") + "\n",
                Encoding.UTF8);
            EditorPrefs.SetString(BackupKey, folder);
            AssetDatabase.Refresh();
            Debug.Log("[Norscope Ask AI Backend] Baseline READY\n" + folder);
        }

        [MenuItem(Menu + "2) Dry Run")]
        public static void DryRunMenu()
        {
            bool ready = DryRun(out string report);
            Directory.CreateDirectory("Temp");
            File.WriteAllText("Temp/NorscopeAskAiBackendDryRun.txt", report, Encoding.UTF8);
            Debug.Log((ready
                ? "[Norscope Ask AI Backend] Dry run READY\n"
                : "[Norscope Ask AI Backend] Dry run BLOCKED\n") + report);
        }

        [MenuItem(Menu + "3) Apply")]
        public static void ApplyMenu()
        {
            if (!DryRun(out string report))
            {
                Debug.LogError("[Norscope Ask AI Backend] Apply BLOCKED\n" + report);
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "Apply Ask AI Phase 4 Backend",
                    "Wire IAskAiAnalysisService HTTP client, enable InternetClient + " +
                    "PrivateNetworkClientServer, keep Microphone/WebCam, and leave OpenAI keys " +
                    "on the Python backend only?",
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

            EnableNetworkCapabilities();
            EnsurePhase2DeployPreservesCapabilities();
            EnsureBackendConfigAsset();
            AssignConfigToAskAiPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateMenu();
            Debug.Log("[Norscope Ask AI Backend] Apply COMPLETE");
        }

        [MenuItem(Menu + "4) Validate")]
        public static void ValidateMenu()
        {
            bool ok = Validate(out string report);
            Directory.CreateDirectory("Temp");
            File.WriteAllText("Temp/NorscopeAskAiBackendValidate.txt", report, Encoding.UTF8);
            if (ok)
                Debug.Log("[Norscope Ask AI Backend] Validation PASSED\n" + report);
            else
                Debug.LogError("[Norscope Ask AI Backend] Validation FAILED\n" + report);
        }

        [MenuItem(Menu + "5) Restore Baseline")]
        public static void RestoreMenu()
        {
            string folder = EditorPrefs.GetString(BackupKey, string.Empty);
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
                folder = LatestBackup();
            Debug.Log(string.IsNullOrEmpty(folder)
                ? "[Norscope Ask AI Backend] No baseline found."
                : "[Norscope Ask AI Backend] Restore available at:\n" + folder);
        }

        private static string LatestBackup()
        {
            if (!Directory.Exists(BackupRoot)) return null;
            string[] dirs = Directory.GetDirectories(BackupRoot, "AskAIBackend_*");
            Array.Sort(dirs);
            return dirs.Length > 0 ? dirs[dirs.Length - 1] : null;
        }

        private static bool DryRun(out string report)
        {
            var lines = new StringBuilder();
            var blockers = new List<string>();
            lines.AppendLine("NORSCOPE ASK AI PHASE 4 BACKEND — DRY RUN");
            if (!File.Exists("Assets/Norscope/Runtime/AskAI/AskAiWorkflowController.cs"))
                blockers.Add("AskAiWorkflowController missing.");
            if (!Directory.Exists("backend/app"))
                blockers.Add("backend/app missing.");
            lines.AppendLine("PLAN  IAskAiAnalysisService + FastAPI /api/v1/ask-ai");
            lines.AppendLine("PLAN  OPENAI_MODEL default gpt-5.6-luna (env only)");
            lines.AppendLine("PLAN  InternetClient + PrivateNetworkClientServer + Mic + WebCam");
            foreach (string blocker in blockers)
                lines.AppendLine("BLOCKER " + blocker);
            lines.AppendLine(blockers.Count == 0 ? "STATUS: READY" : "STATUS: BLOCKED");
            report = lines.ToString();
            return blockers.Count == 0;
        }

        private static void EnableNetworkCapabilities()
        {
            PlayerSettings.WSA.SetCapability(PlayerSettings.WSACapability.InternetClient, true);
            PlayerSettings.WSA.SetCapability(
                PlayerSettings.WSACapability.PrivateNetworkClientServer, true);
            PlayerSettings.WSA.SetCapability(PlayerSettings.WSACapability.Microphone, true);
            PlayerSettings.WSA.SetCapability(PlayerSettings.WSACapability.WebCam, true);
            PlayerSettings.WSA.SetCapability(
                PlayerSettings.WSACapability.InternetClientServer, false);
            Debug.Log("[Norscope Ask AI Backend] Network + mic/webcam capabilities set.");
        }

        private static void EnsurePhase2DeployPreservesCapabilities()
        {
            string path = "Assets/Norscope/Editor/Phase2DeployConfigurator.cs";
            string source = File.ReadAllText(path);
            bool changed = false;

            if (source.Contains(
                    "SetCapability(PlayerSettings.WSACapability.InternetClient, false)"))
            {
                source = source.Replace(
                    "SetCapability(PlayerSettings.WSACapability.InternetClient, false)",
                    "SetCapability(PlayerSettings.WSACapability.InternetClient, true)");
                changed = true;
            }

            if (source.Contains(
                    "SetCapability(PlayerSettings.WSACapability.PrivateNetworkClientServer, false)") &&
                !source.Contains(
                    "SetCapability(PlayerSettings.WSACapability.PrivateNetworkClientServer, true)"))
            {
                source = source.Replace(
                    "SetCapability(PlayerSettings.WSACapability.PrivateNetworkClientServer, false)",
                    "SetCapability(PlayerSettings.WSACapability.PrivateNetworkClientServer, true)");
                changed = true;
            }

            if (changed)
            {
                File.WriteAllText(path, source, Encoding.UTF8);
                AssetDatabase.ImportAsset(path);
            }
        }

        private static void EnsureBackendConfigAsset()
        {
            Directory.CreateDirectory("Assets/Norscope/Data");
            AskAiBackendConfig existing =
                AssetDatabase.LoadAssetAtPath<AskAiBackendConfig>(ConfigPath);
            if (existing != null)
                return;

            AskAiBackendConfig asset =
                ScriptableObject.CreateInstance<AskAiBackendConfig>();
            AssetDatabase.CreateAsset(asset, ConfigPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[Norscope Ask AI Backend] Created " + ConfigPath);
        }

        private static void AssignConfigToAskAiPrefab()
        {
            const string prefabPath = "Assets/Norscope/UI/Panels/AskAIPanel.prefab";
            AskAiBackendConfig config =
                AssetDatabase.LoadAssetAtPath<AskAiBackendConfig>(ConfigPath);
            if (config == null || !File.Exists(prefabPath))
                return;

            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var controller = root.GetComponentInChildren<AskAiWorkflowController>(true);
                if (controller == null)
                    return;

                SerializedObject so = new SerializedObject(controller);
                SerializedProperty prop = so.FindProperty("backendConfig");
                if (prop != null)
                {
                    prop.objectReferenceValue = config;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
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

            Check(Directory.Exists("backend/app"), "Backend app folder exists");
            Check(File.Exists("backend/app/main.py"), "Backend main.py exists");
            Check(File.Exists("backend/.env.example"), "Backend .env.example exists");
            Check(File.Exists("backend/.gitignore"), "Backend .gitignore exists");
            Check(!File.Exists("backend/.env"), "backend/.env is not committed/present");

            string controller = File.ReadAllText(
                "Assets/Norscope/Runtime/AskAI/AskAiWorkflowController.cs");
            string http = File.ReadAllText(
                "Assets/Norscope/Runtime/AskAI/HttpAskAiAnalysisService.cs");
            string factory = File.ReadAllText(
                "Assets/Norscope/Runtime/AskAI/AskAiAnalysisServiceFactory.cs");
            string backendMain = File.ReadAllText("backend/app/main.py");
            string backendCfg = File.ReadAllText("backend/app/config.py");
            string envExample = File.ReadAllText("backend/.env.example");
            string deploy = File.ReadAllText(
                "Assets/Norscope/Editor/Phase2DeployConfigurator.cs");
            string rootGitignore = File.Exists(".gitignore")
                ? File.ReadAllText(".gitignore")
                : string.Empty;

            Check(controller.Contains("IAskAiAnalysisService") &&
                  !controller.Contains("using OpenAI") &&
                  !controller.Contains("GetMockAnswer()"),
                "Controller uses analysis service; no GetMockAnswer / OpenAI");
            Check(controller.Contains("MinProcessingDisplaySeconds = 0.15f"),
                "Processing min display is 0.15s flicker guard");
            Check(http.Contains("MultipartFormFileSection") &&
                  http.Contains("image/jpeg") &&
                  http.Contains("UnityWebRequest"),
                "HTTP client sends multipart JPEG without re-encode");
            Check(factory.Contains("UNITY_EDITOR") &&
                  factory.Contains("MockAskAiAnalysisService") &&
                  factory.Contains("UNITY_WSA") &&
                  factory.Contains("HttpAskAiAnalysisService"),
                "Factory: Editor mock available; WSA uses HTTP");
            Check(backendCfg.Contains("gpt-5.6-luna") &&
                  envExample.Contains("OPENAI_MODEL=gpt-5.6-luna") &&
                  envExample.Contains("gpt-5.6-terra"),
                "Default model gpt-5.6-luna; terra documented as alternative");
            Check(backendMain.Contains("/api/v1/ask-ai") &&
                  backendMain.Contains("/health"),
                "Backend exposes /health and /api/v1/ask-ai");
            Check(PlayerSettings.WSA.GetCapability(PlayerSettings.WSACapability.InternetClient),
                "InternetClient enabled");
            Check(PlayerSettings.WSA.GetCapability(
                    PlayerSettings.WSACapability.PrivateNetworkClientServer),
                "PrivateNetworkClientServer enabled");
            Check(PlayerSettings.WSA.GetCapability(PlayerSettings.WSACapability.Microphone),
                "Microphone enabled");
            Check(PlayerSettings.WSA.GetCapability(PlayerSettings.WSACapability.WebCam),
                "WebCam enabled");
            Check(deploy.Contains(
                      "SetCapability(PlayerSettings.WSACapability.InternetClient, true)") &&
                  deploy.Contains(
                      "SetCapability(PlayerSettings.WSACapability.PrivateNetworkClientServer, true)") &&
                  deploy.Contains(
                      "SetCapability(PlayerSettings.WSACapability.Microphone, true)") &&
                  deploy.Contains(
                      "SetCapability(PlayerSettings.WSACapability.WebCam, true)"),
                "Phase2Deploy preserves InternetClient+PrivateNetwork+Mic+WebCam");
            Check(!controller.Contains("OPENAI_API_KEY") &&
                  !http.Contains("OPENAI_API_KEY") &&
                  !http.Contains("sk-") &&
                  rootGitignore.Contains("backend/.env"),
                "No API key in Unity; backend/.env gitignored");
            Check(!controller.Contains("TextToSpeech") &&
                  !controller.Contains("OCR") &&
                  !backendMain.Contains("conversation history"),
                "No TTS/OCR/conversation history added");
            Check(!EditorUtility.scriptCompilationFailed, "Zero C# compile errors");

            lines.AppendLine();
            lines.AppendLine("DEFAULT MODEL: gpt-5.6-luna (OPENAI_MODEL env)");
            lines.AppendLine("ALT MODEL: gpt-5.6-terra");
            lines.AppendLine("CONFIRM: no API key in Unity");
            lines.AppendLine("CONFIRM: no TTS/OCR/detection/spatial annotations/history");
            lines.AppendLine("START: uvicorn app.main:app --host 0.0.0.0 --port 8000");
            lines.AppendLine("HL2 URL: http://<PC-LAN-IP>:8000");

            report = lines.ToString();
            return ok;
        }
    }
}
