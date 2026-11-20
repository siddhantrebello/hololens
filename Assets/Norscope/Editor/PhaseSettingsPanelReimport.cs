using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Norscope.Editor
{
    /// <summary>
    /// Forces SettingsPanel.prefab reimport when Unity marks it broken in Library cache.
    /// Also forwards Ask AI Phase 1 automation flags when present.
    /// </summary>
    public static class PhaseSettingsPanelReimport
    {
        private const string Flag = "Temp/NorscopeForceReimportSettingsPanel.flag";
        private const string AskAiApplyFlag = "Temp/NorscopeAskAiApply.flag";
        private const string AskAiValidateFlag = "Temp/NorscopeAskAiValidate.flag";
        private const string AskAiSpeechApplyFlag = "Temp/NorscopeAskAiSpeechApply.flag";
        private const string AskAiSpeechValidateFlag = "Temp/NorscopeAskAiSpeechValidate.flag";
        private const string AskAiCameraApplyFlag = "Temp/NorscopeAskAiCameraApply.flag";
        private const string AskAiCameraValidateFlag = "Temp/NorscopeAskAiCameraValidate.flag";
        private const string AskAiBackendApplyFlag = "Temp/NorscopeAskAiBackendApply.flag";
        private const string AskAiBackendValidateFlag = "Temp/NorscopeAskAiBackendValidate.flag";
        private const string PanelPath = "Assets/Norscope/UI/Panels/SettingsPanel.prefab";

        [InitializeOnLoadMethod]
        private static void Boot()
        {
            EditorApplication.update -= Poll;
            EditorApplication.update += Poll;
        }

        private static void Poll()
        {
            if (File.Exists(AskAiBackendApplyFlag))
            {
                File.Delete(AskAiBackendApplyFlag);
                EditorApplication.delayCall += InvokeAskAiBackendApply;
                return;
            }

            if (File.Exists(AskAiBackendValidateFlag))
            {
                File.Delete(AskAiBackendValidateFlag);
                EditorApplication.delayCall += InvokeAskAiBackendValidate;
                return;
            }

            if (File.Exists(AskAiCameraApplyFlag))
            {
                File.Delete(AskAiCameraApplyFlag);
                EditorApplication.delayCall += InvokeAskAiCameraApply;
                return;
            }

            if (File.Exists(AskAiCameraValidateFlag))
            {
                File.Delete(AskAiCameraValidateFlag);
                EditorApplication.delayCall += InvokeAskAiCameraValidate;
                return;
            }

            if (File.Exists(AskAiSpeechApplyFlag))
            {
                File.Delete(AskAiSpeechApplyFlag);
                EditorApplication.delayCall += InvokeAskAiSpeechApply;
                return;
            }

            if (File.Exists(AskAiSpeechValidateFlag))
            {
                File.Delete(AskAiSpeechValidateFlag);
                EditorApplication.delayCall += InvokeAskAiSpeechValidate;
                return;
            }

            if (File.Exists(AskAiApplyFlag))
            {
                File.Delete(AskAiApplyFlag);
                EditorApplication.delayCall += InvokeAskAiApply;
                return;
            }

            if (File.Exists(AskAiValidateFlag))
            {
                File.Delete(AskAiValidateFlag);
                EditorApplication.delayCall += InvokeAskAiValidate;
                return;
            }

            if (!File.Exists(Flag)) return;
            File.Delete(Flag);
            EditorApplication.delayCall += Reimport;
        }

        private static void InvokeAskAiBackendApply()
        {
            InvokeTypeMethod(
                "Norscope.Editor.PhaseAskAiBackendConfigurator, Norscope.Editor",
                "ApplyFromAutomation");
        }

        private static void InvokeAskAiBackendValidate()
        {
            InvokeTypeMethod(
                "Norscope.Editor.PhaseAskAiBackendConfigurator, Norscope.Editor",
                "ValidateFromAutomation");
        }

        private static void InvokeAskAiCameraApply()
        {
            InvokeTypeMethod(
                "Norscope.Editor.PhaseAskAiCameraConfigurator, Norscope.Editor",
                "ApplyFromAutomation");
        }

        private static void InvokeAskAiCameraValidate()
        {
            InvokeTypeMethod(
                "Norscope.Editor.PhaseAskAiCameraConfigurator, Norscope.Editor",
                "ValidateFromAutomation");
        }

        private static void InvokeAskAiSpeechApply()
        {
            InvokeTypeMethod(
                "Norscope.Editor.PhaseAskAiSpeechConfigurator, Norscope.Editor",
                "ApplyFromAutomation");
        }

        private static void InvokeAskAiSpeechValidate()
        {
            InvokeTypeMethod(
                "Norscope.Editor.PhaseAskAiSpeechConfigurator, Norscope.Editor",
                "ValidateFromAutomation");
        }

        private static void InvokeAskAiApply()
        {
            InvokeAskAi("ApplyFromAutomation");
        }

        private static void InvokeAskAiValidate()
        {
            InvokeAskAi("ValidateFromAutomation");
        }

        private static void InvokeAskAi(string methodName)
        {
            InvokeTypeMethod(
                "Norscope.Editor.PhaseAskAiPanelConfigurator, Norscope.Editor",
                methodName);
        }

        private static void InvokeTypeMethod(string typeName, string methodName)
        {
            System.Type type = System.Type.GetType(typeName);
            if (type == null)
            {
                Debug.LogError(
                    "[Settings] Type not loaded: " + typeName +
                    ". Press Ctrl+R and retry.");
                return;
            }

            MethodInfo method = type.GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.Static);
            if (method == null)
            {
                Debug.LogError("[Settings] Method missing: " + typeName + "." + methodName);
                return;
            }

            method.Invoke(null, null);
        }

        [MenuItem("Norscope/HoloLens/Settings/Force Reimport SettingsPanel")]
        public static void Reimport()
        {
            if (!File.Exists(PanelPath))
            {
                Debug.LogError("[Settings] Missing " + PanelPath);
                return;
            }

            AssetDatabase.ImportAsset(
                PanelPath,
                ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath);
            if (prefab == null)
            {
                Debug.LogError(
                    "[Settings] SettingsPanel.prefab still failed to load after reimport. " +
                    "Close Prefab Mode, press Ctrl+R, then run this menu again.");
                return;
            }

            var nav = prefab.GetComponent<Norscope.Settings.SettingsViewNavigator>();
            Debug.Log(
                "[Settings] SettingsPanel reimported OK. " +
                "Navigator=" + (nav != null) +
                " name=" + prefab.name);
        }
    }
}
