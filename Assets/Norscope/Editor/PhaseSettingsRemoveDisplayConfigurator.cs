using System;
using System.Collections.Generic;
using System.IO;
using Norscope.Settings;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Norscope.Editor
{
    /// <summary>
    /// Removes the Display section from SettingsPanel via PrefabUtility so YAML stays valid.
    /// </summary>
    public static class PhaseSettingsRemoveDisplayConfigurator
    {
        private const string Menu = "Norscope/HoloLens/Settings/Remove Display/";
        private const string PanelPath = "Assets/Norscope/UI/Panels/SettingsPanel.prefab";
        private const string BackupRoot = "Assets/Norscope/UI/Panels/_Backups";
        private const string ApplyFlag = "Temp/NorscopeSettingsRemoveDisplayApply.flag";
        private const string ReimportFlag = "Temp/NorscopeSettingsForceReimportPanel.flag";

        [InitializeOnLoadMethod]
        private static void RunApprovedQueuedCommand()
        {
            if (File.Exists(ReimportFlag))
            {
                File.Delete(ReimportFlag);
                EditorApplication.delayCall += () =>
                {
                    AssetDatabase.ImportAsset(PanelPath, ImportAssetOptions.ForceUpdate);
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath);
                    Debug.Log(prefab != null
                        ? "[Settings Remove Display] Prefab reimported and loads OK."
                        : "[Settings Remove Display] Prefab still fails to load after reimport.");
                };
            }

            if (!File.Exists(ApplyFlag))
                return;
            File.Delete(ApplyFlag);
            EditorApplication.delayCall += Apply;
        }

        [MenuItem(Menu + "Apply — remove Display section")]
        public static void ApplyFromMenu()
        {
            Apply();
        }

        public static void Apply()
        {
            if (!File.Exists(PanelPath))
                throw new InvalidOperationException("Missing " + PanelPath);

            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string folder = BackupRoot + "/SettingsRemoveDisplay_" + stamp;
            Directory.CreateDirectory(folder);
            FileUtil.CopyFileOrDirectory(PanelPath, folder + "/SettingsPanel.prefab");
            File.WriteAllText(
                folder + "/manifest.txt",
                "Remove Display section " + DateTime.Now.ToString("O") + Environment.NewLine);

            GameObject root = PrefabUtility.LoadPrefabContents(PanelPath);
            try
            {
                Transform displayButton = root.transform.Find(
                    "FigmaContent/SettingsView/Body/Sidebar/DisplayButton");
                Transform displayView = root.transform.Find(
                    "FigmaContent/SettingsView/Body/Detail/DisplayView");
                Transform aboutButton = root.transform.Find(
                    "FigmaContent/SettingsView/Body/Sidebar/AboutButton");

                if (displayButton == null && displayView == null)
                {
                    Debug.Log("[Settings Remove Display] Display section already absent.");
                    return;
                }

                if (displayButton != null)
                    UnityEngine.Object.DestroyImmediate(displayButton.gameObject);
                if (displayView != null)
                    UnityEngine.Object.DestroyImmediate(displayView.gameObject);

                if (aboutButton is RectTransform aboutRt)
                    aboutRt.anchoredPosition = new Vector2(12f, -144f);

                ScrubDeferredControls(root);
                ClearLegacyDisplayRefs(root);

                bool saved = PrefabUtility.SaveAsPrefabAsset(root, PanelPath);
                if (!saved)
                    throw new InvalidOperationException("SaveAsPrefabAsset returned false.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.ImportAsset(PanelPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            try
            {
                ValidateOrThrow();
            }
            catch (Exception ex)
            {
                FileUtil.ReplaceFile(folder + "/SettingsPanel.prefab", PanelPath);
                AssetDatabase.ImportAsset(PanelPath, ImportAssetOptions.ForceUpdate);
                throw new InvalidOperationException(
                    "Remove Display validation failed; restored backup. " + ex.Message, ex);
            }

            Debug.Log(
                "[Settings Remove Display] Applied. DisplayButton/DisplayView removed. Backup: " +
                folder);
        }

        private static void ScrubDeferredControls(GameObject root)
        {
            SettingsPanel panel = root.GetComponent<SettingsPanel>();
            if (panel == null)
                return;

            SerializedObject so = new SerializedObject(panel);
            SerializedProperty controls = so.FindProperty("deferredControls");
            if (controls == null || !controls.isArray)
                return;

            for (int i = controls.arraySize - 1; i >= 0; i--)
            {
                UnityEngine.Object value =
                    controls.GetArrayElementAtIndex(i).objectReferenceValue;
                if (value == null)
                    controls.DeleteArrayElementAtIndex(i);
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ClearLegacyDisplayRefs(GameObject root)
        {
            SettingsViewNavigator navigator = root.GetComponent<SettingsViewNavigator>();
            if (navigator == null)
                return;

            SerializedObject so = new SerializedObject(navigator);
            SerializedProperty displayView = so.FindProperty("displayView");
            SerializedProperty displayButton = so.FindProperty("displayButton");
            if (displayView != null)
                displayView.objectReferenceValue = null;
            if (displayButton != null)
                displayButton.objectReferenceValue = null;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ValidateOrThrow()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath);
            if (prefab == null)
                throw new InvalidOperationException("SettingsPanel.prefab failed to load after save.");

            if (prefab.transform.Find(
                    "FigmaContent/SettingsView/Body/Sidebar/DisplayButton") != null)
                throw new InvalidOperationException("DisplayButton still present.");
            if (prefab.transform.Find(
                    "FigmaContent/SettingsView/Body/Detail/DisplayView") != null)
                throw new InvalidOperationException("DisplayView still present.");
            if (prefab.transform.Find(
                    "FigmaContent/SettingsView/Body/Sidebar/AboutButton") == null)
                throw new InvalidOperationException("AboutButton missing.");
            if (prefab.transform.Find(
                    "FigmaContent/SettingsView/Body/Sidebar/LanguageButton") == null ||
                prefab.transform.Find(
                    "FigmaContent/SettingsView/Body/Sidebar/AudioButton") == null)
                throw new InvalidOperationException("Language/Audio sidebar buttons missing.");

            SettingsViewNavigator navigator = prefab.GetComponent<SettingsViewNavigator>();
            if (navigator == null)
                throw new InvalidOperationException("SettingsViewNavigator missing.");

            SerializedObject navSo = new SerializedObject(navigator);
            string[] required =
            {
                "settingsView", "languageView", "languageQuickView", "audioView",
                "aboutView", "languageButton", "languageQuickButton", "audioButton",
                "aboutButton", "languageInternalBackButton"
            };
            foreach (string name in required)
            {
                SerializedProperty property = navSo.FindProperty(name);
                if (property == null || property.objectReferenceValue == null)
                    throw new InvalidOperationException("Navigator missing reference: " + name);
            }
        }
    }
}
