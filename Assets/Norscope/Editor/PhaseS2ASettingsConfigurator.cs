using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Norscope.Navigation;
using Norscope.Settings;
using Norscope.UI;
using Norscope.Windows;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Norscope.Editor
{
    /// <summary>
    /// Phase 2A preparation only. Capture records the current Settings integration
    /// boundary; Dry Run is strictly read-only. Apply, Validate and Restore remain
    /// intentionally locked until the Phase 2A dry run has been reviewed.
    /// </summary>
    public static class PhaseS2ASettingsConfigurator
    {
        private const string MenuRoot =
            "Norscope/HoloLens/Settings Figma/Phase 2A/";
        private const string BootstrapPath =
            "Assets/Norscope/Scenes/Bootstrap.unity";
        private const string HomePanelPath =
            "Assets/Norscope/UI/Panels/HomePanel.prefab";
        private const string SettingsPanelPath =
            "Assets/Norscope/UI/Panels/SettingsPanel.prefab";
        private const string SettingsConfigPath =
            "Assets/Norscope/Data/Settings/NorscopeSettingsConfiguration.asset";
        private const string BaselinePath =
            "Assets/Norscope/Editor/Validation/SettingsPhase2A/Phase2ABaseline.json";
        private const string BackupPreference =
            "Norscope.Settings.Phase2A.BackupFolder";
        private const string RoundedPath =
            "Assets/Norscope/UI/DesignSystem/Sprites/UIRounded_8.png";
        private const string ThemePath =
            "Assets/Norscope/UI/DesignSystem/Themes/NorscopeTheme.asset";
        private const string WorldConfigPath =
            "Assets/Norscope/UI/DesignSystem/Themes/NorscopeWorldSpaceUiConfig.asset";
        private const string OutfitPath =
            "Assets/Norscope/UI/Fonts/Outfit SDF.asset";
        private const string InterPath =
            "Assets/Norscope/UI/Fonts/Inter SDF.asset";
        private const string LogoPath =
            "Assets/WhatsApp Image 2026-07-19 at 13.42.15-transparent.png";
        private const string CloseButtonPath =
            "Assets/Norscope/UI/Controls/NorscopeCloseButton.prefab";
        private const string BackButtonPath =
            "Assets/Norscope/UI/Controls/NavigationBackButton.prefab";
        private const string TemporaryIconSourceFolder =
            "Temp/settings_icons_rendered";
        private const string ApprovedApplyRequestPath =
            "Temp/NorscopeSettingsPhase2A.apply";
        private const string ApprovedValidateRequestPath =
            "Temp/NorscopeSettingsPhase2A.validate";

        private static readonly string[] ProtectedPaths =
        {
            BootstrapPath,
            HomePanelPath,
            "Assets/Norscope/UI/Panels/HomeLauncher.prefab",
            "Assets/Norscope/UI/Panels/TaskLibraryPanel.prefab",
            "Assets/Norscope/UI/Controls/TaskCard.prefab",
            "Assets/Norscope/UI/Panels/ActiveTaskPanel.prefab",
            "Assets/Norscope/UI/Panels/TaskCompletionPanel.prefab",
            "Assets/Norscope/UI/Controls/NavigationBackButton.prefab",
            "Assets/Norscope/UI/Controls/NorscopeCloseButton.prefab",
            "Assets/Norscope/Runtime/Navigation/PanelRegistry.cs",
            "Assets/Norscope/Runtime/Navigation/PanelNavigationService.cs",
            "Assets/Norscope/Runtime/Windows/SpatialWindowManager.cs",
            "Assets/Norscope/Runtime/Windows/SpatialWindow.cs",
            "ProjectSettings/ProjectSettings.asset"
        };

        private static readonly string[] PlannedRuntimeScripts =
        {
            "Assets/Norscope/Runtime/Settings/NorscopeLanguage.cs",
            "Assets/Norscope/Runtime/Settings/NorscopeTextSize.cs",
            "Assets/Norscope/Runtime/Settings/SettingsInternalView.cs",
            "Assets/Norscope/Runtime/Settings/NorscopeSettingsData.cs",
            "Assets/Norscope/Runtime/Settings/NorscopeSettingsPersistence.cs",
            "Assets/Norscope/Runtime/Settings/NorscopeSettingsService.cs",
            "Assets/Norscope/Runtime/Settings/NorscopeSettingsConfiguration.cs",
            "Assets/Norscope/Runtime/Settings/SettingsPanel.cs",
            "Assets/Norscope/Runtime/Settings/SettingsViewNavigator.cs"
        };

        private static readonly string[] PlannedIconAssets =
        {
            "Assets/Norscope/UI/Icons/Lucide/Raster/globe.png",
            "Assets/Norscope/UI/Icons/Lucide/Raster/volume-2.png",
            "Assets/Norscope/UI/Icons/Lucide/Raster/monitor.png",
            "Assets/Norscope/UI/Icons/Lucide/Raster/user.png",
            "Assets/Norscope/UI/Icons/Lucide/Raster/arrow-left.png",
            "Assets/Norscope/UI/Icons/Lucide/Raster/check.png",
            "Assets/Norscope/UI/Icons/Lucide/Raster/plus.png",
            "Assets/Norscope/UI/Icons/Lucide/Raster/minus.png",
            "Assets/Norscope/UI/Icons/Lucide/Raster/volume-1.png",
            "Assets/Norscope/UI/Icons/Lucide/Raster/volume-x.png",
            "Assets/Norscope/UI/Icons/Lucide/Raster/bell.png",
            "Assets/Norscope/UI/Icons/Lucide/Raster/mic.png",
            "Assets/Norscope/UI/Icons/Lucide/Raster/sun.png",
            "Assets/Norscope/UI/Icons/Lucide/Raster/type.png",
            "Assets/Norscope/UI/Icons/Lucide/Raster/eye.png"
        };

        private static readonly string[] RequiredExistingAssets =
        {
            "Assets/Norscope/UI/Controls/NorscopeCloseButton.prefab",
            "Assets/Norscope/UI/Controls/NavigationBackButton.prefab",
            "Assets/Norscope/UI/DesignSystem/Sprites/UIRounded_8.png",
            "Assets/Norscope/UI/Fonts/Outfit SDF.asset",
            "Assets/Norscope/UI/Fonts/Inter SDF.asset",
            "Assets/Norscope/UI/Icons/Lucide/Raster/settings.png",
            "Assets/Norscope/UI/Icons/Lucide/Raster/info.png",
            "Assets/Norscope/UI/Icons/Lucide/Raster/wifi-off.png",
            "Assets/Norscope/UI/Icons/Lucide/Raster/chevron-right.png"
        };

        [Serializable]
        private sealed class BaselineEntry
        {
            public string path;
            public string sha256;
            public long length;
            public bool existed;
        }

        [Serializable]
        private sealed class Phase2ABaseline
        {
            public int schemaVersion = 1;
            public string capturedUtc;
            public string backupFolder;
            public bool settingsPanelExisted;
            public bool settingsConfigurationExisted;
            public List<BaselineEntry> entries = new List<BaselineEntry>();
        }

        [MenuItem(MenuRoot + "1) Capture Settings Baseline")]
        public static void CaptureBaselineMenu()
        {
            if (!File.Exists(BootstrapPath) || !File.Exists(HomePanelPath))
            {
                Debug.LogError(
                    "[Norscope Settings 2A] Baseline BLOCKED: Bootstrap or HomePanel is missing.");
                return;
            }

            if (File.Exists(BaselinePath) &&
                !EditorUtility.DisplayDialog(
                    "Replace Phase 2A baseline?",
                    "A Phase 2A baseline already exists. Replace it with a fresh " +
                    "snapshot of the current project?\n\nThis does not modify any " +
                    "runtime asset or prefab.",
                    "Capture Fresh Baseline",
                    "Cancel"))
            {
                return;
            }

            string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            string backupFolder =
                "Assets/Norscope/UI/Panels/_Backups/SettingsPhase2A_" + timestamp;
            Directory.CreateDirectory(backupFolder);
            FileUtil.CopyFileOrDirectory(
                BootstrapPath,
                backupFolder + "/Bootstrap.unity");

            var baseline = new Phase2ABaseline
            {
                capturedUtc = DateTime.UtcNow.ToString("O"),
                backupFolder = backupFolder,
                settingsPanelExisted = File.Exists(SettingsPanelPath),
                settingsConfigurationExisted = File.Exists(SettingsConfigPath)
            };

            for (int i = 0; i < ProtectedPaths.Length; i++)
            {
                baseline.entries.Add(CaptureEntry(ProtectedPaths[i]));
            }

            Directory.CreateDirectory(Path.GetDirectoryName(BaselinePath) ??
                                      "Assets/Norscope/Editor/Validation");
            File.WriteAllText(
                BaselinePath,
                JsonUtility.ToJson(baseline, true),
                new UTF8Encoding(false));
            File.WriteAllText(
                backupFolder + "/manifest.txt",
                BuildBackupManifest(baseline),
                new UTF8Encoding(false));
            EditorPrefs.SetString(BackupPreference, backupFolder);
            AssetDatabase.Refresh();

            Debug.Log(
                "[Norscope Settings 2A] Baseline CAPTURED\n" +
                "Manifest: " + BaselinePath + "\n" +
                "Bootstrap backup: " + backupFolder + "/Bootstrap.unity\n" +
                "Protected hashes: " + baseline.entries.Count + "\n" +
                "No prefab, scene object, runtime script or registry entry was modified.");
        }

        [MenuItem(MenuRoot + "2) Dry Run")]
        public static void DryRunMenu()
        {
            bool ready = BuildDryRun(out string report);
            Debug.Log(
                (ready
                    ? "[Norscope Settings 2A] Dry run READY\n"
                    : "[Norscope Settings 2A] Dry run BLOCKED\n") +
                report);
        }

        [MenuItem(MenuRoot + "3) Apply")]
        public static void ApplyMenu()
        {
            ExecuteApprovedApply(true);
        }

        [InitializeOnLoadMethod]
        private static void RunExplicitApprovedApplyRequest()
        {
            bool applyRequested = File.Exists(ApprovedApplyRequestPath);
            bool validateRequested = File.Exists(ApprovedValidateRequestPath);
            if (!applyRequested && !validateRequested)
                return;

            EditorApplication.delayCall += () =>
            {
                if (File.Exists(ApprovedApplyRequestPath))
                {
                    File.Delete(ApprovedApplyRequestPath);
                    Debug.Log(
                        "[Norscope Settings 2A] Executing the explicitly approved " +
                        "one-shot Apply request.");
                    ExecuteApprovedApply(false);
                }

                if (File.Exists(ApprovedValidateRequestPath))
                {
                    File.Delete(ApprovedValidateRequestPath);
                    Debug.Log(
                        "[Norscope Settings 2A] Executing the explicitly approved " +
                        "one-shot Validate request.");
                    ValidateMenu();
                }
            };
        }

        private static void ExecuteApprovedApply(bool requireDialog)
        {
            if (!BuildDryRun(out string report))
            {
                Debug.LogError("[Norscope Settings 2A] Apply BLOCKED\n" + report);
                return;
            }

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (SceneManager.GetSceneAt(i).isDirty)
                {
                    Debug.LogError(
                        "[Norscope Settings 2A] Apply BLOCKED: save or discard " +
                        "open scene changes before applying.");
                    return;
                }
            }

            if (requireDialog && !EditorUtility.DisplayDialog(
                    "Apply NORSCOPE Settings Phase 2A?",
                    "Create the approved Settings runtime, prefab, configuration and " +
                    "Lucide sprites, then modify only Bootstrap.unity?\n\n" +
                    "Home and all workflow prefabs remain untouched.",
                    "Apply Phase 2A",
                    "Cancel"))
            {
                return;
            }

            string bootstrapBefore = ComputeSha256(BootstrapPath);
            try
            {
                PrepareIconAssets();
                NorscopeSettingsConfiguration configuration =
                    CreateConfigurationIfMissing();
                GameObject settingsPrefab =
                    CreateSettingsPrefabIfMissing(configuration);
                WireBootstrap(settingsPrefab, configuration);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                bool valid = ValidateApplied(out string validation);
                Debug.Log(
                    (valid
                        ? "[Norscope Settings 2A] Apply COMPLETE\n"
                        : "[Norscope Settings 2A] Apply completed but validation FAILED\n") +
                    "Bootstrap changed: " +
                    (!string.Equals(
                        bootstrapBefore,
                        ComputeSha256(BootstrapPath),
                        StringComparison.OrdinalIgnoreCase)) +
                    "\n" + validation);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[Norscope Settings 2A] Apply FAILED. No corrective edits were " +
                    "attempted.\n" + exception);
            }
        }

        [MenuItem(MenuRoot + "4) Validate")]
        public static void ValidateMenu()
        {
            bool valid = ValidateApplied(out string report);
            Debug.Log(
                (valid
                    ? "[Norscope Settings 2A] Validation PASSED\n"
                    : "[Norscope Settings 2A] Validation FAILED\n") +
                report);
        }

        [MenuItem(MenuRoot + "5) Restore Baseline")]
        public static void RestoreMenu()
        {
            Phase2ABaseline baseline = LoadBaseline(new List<string>());
            string backup = baseline != null
                ? baseline.backupFolder + "/Bootstrap.unity"
                : string.Empty;
            if (baseline == null || !File.Exists(backup))
            {
                Debug.LogError(
                    "[Norscope Settings 2A] Restore BLOCKED: captured Bootstrap backup missing.");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "Restore pre-Phase-2A assets?",
                    "Restore Bootstrap.unity and remove only the Phase 2A-created " +
                    "Settings prefab, configuration asset and fifteen icon sprites?\n\n" +
                    "The additive runtime/editor source files remain available so the " +
                    "project continues compiling and Phase 2A can be reapplied.",
                    "Restore",
                    "Cancel"))
            {
                return;
            }

            File.Copy(backup, BootstrapPath, true);
            AssetDatabase.DeleteAsset(SettingsPanelPath);
            AssetDatabase.DeleteAsset(SettingsConfigPath);
            for (int i = 0; i < PlannedIconAssets.Length; i++)
                AssetDatabase.DeleteAsset(PlannedIconAssets[i]);
            AssetDatabase.Refresh();
            Debug.Log(
                "[Norscope Settings 2A] Restore COMPLETE\n" +
                "- Restored " + BootstrapPath + "\n" +
                "- Removed Phase 2A prefab/configuration/icons\n" +
                "- Retained additive Settings source and tooling so Unity compiles.");
        }

        private static bool BuildDryRun(out string report)
        {
            var blockers = new List<string>();
            var warnings = new List<string>();
            var lines = new StringBuilder(16000);

            Phase2ABaseline baseline = LoadBaseline(blockers);
            if (baseline != null)
            {
                ValidateFreshBaseline(baseline, blockers);
            }

            RequireFile(BootstrapPath, blockers);
            RequireFile(HomePanelPath, blockers);
            for (int i = 0; i < RequiredExistingAssets.Length; i++)
            {
                RequireFile(RequiredExistingAssets[i], blockers);
            }

            for (int i = 0; i < PlannedRuntimeScripts.Length; i++)
                RequireFile(PlannedRuntimeScripts[i], blockers);

            if (File.Exists(SettingsPanelPath))
            {
                blockers.Add(
                    "SettingsPanel.prefab already exists; Phase 2A is create-if-missing only.");
            }

            if (File.Exists(SettingsConfigPath))
            {
                blockers.Add(
                    "NorscopeSettingsConfiguration.asset already exists; " +
                    "Phase 2A must not overwrite it.");
            }

            InspectRegistryAndNavigation(blockers, warnings);

            for (int i = 0; i < PlannedIconAssets.Length; i++)
            {
                if (File.Exists(PlannedIconAssets[i]))
                {
                    warnings.Add(
                        "Icon already exists and will be reused, not overwritten: " +
                        PlannedIconAssets[i]);
                }
                else
                {
                    string source = TemporaryIconSourceFolder + "/" +
                                    Path.GetFileName(PlannedIconAssets[i]);
                    if (!File.Exists(source))
                        blockers.Add(
                            "Prepared official Lucide source is missing: " + source);
                }
            }

            AppendHeader(lines);
            AppendNewScripts(lines);
            AppendNewAssets(lines);
            AppendHierarchy(lines);
            AppendRegistryAndNavigation(lines);
            AppendPersistence(lines);
            AppendDisabledControls(lines);
            AppendAbout(lines);
            AppendSpatialWindow(lines);
            AppendExistingModifications(lines);
            AppendProtectedScope(lines);

            lines.AppendLine();
            lines.AppendLine("DRY-RUN CHECKS");
            lines.AppendLine("- Read-only execution: no AssetDatabase save/import/refresh");
            lines.AppendLine("- No GameObject creation and no prefab load-for-edit");
            lines.AppendLine("- No scene open/save and no registry mutation");
            lines.AppendLine("- No runtime scripts, data assets or folders created");
            lines.AppendLine("- No Home or workflow prefab modifications planned");

            for (int i = 0; i < warnings.Count; i++)
            {
                lines.AppendLine("WARNING  " + warnings[i]);
            }

            for (int i = 0; i < blockers.Count; i++)
            {
                lines.AppendLine("BLOCKER  " + blockers[i]);
            }

            lines.AppendLine();
            lines.AppendLine(blockers.Count == 0
                ? "READY - Phase 2A plan is internally consistent. Apply remains locked pending approval."
                : "BLOCKED - do not implement or apply Phase 2A.");

            report = lines.ToString();
            return blockers.Count == 0;
        }

        private static void AppendHeader(StringBuilder lines)
        {
            lines.AppendLine("SOURCE  settings.zip / src/app/App.tsx");
            lines.AppendLine("SCOPE   Phase 2A preparation only");
            lines.AppendLine("TARGET  one new SettingsPanel.prefab");
            lines.AppendLine("LAYOUT  fixed authored 880 x 580 composition");
            lines.AppendLine("WINDOW  existing Norscope XRI SpatialWindow, WorldLocked");
            lines.AppendLine("APPLY   intentionally disabled in this tooling revision");
            lines.AppendLine();
        }

        private static void AppendNewScripts(StringBuilder lines)
        {
            lines.AppendLine("EXACT NEW RUNTIME SCRIPTS PLANNED FOR FUTURE APPROVED APPLY");
            for (int i = 0; i < PlannedRuntimeScripts.Length; i++)
            {
                lines.AppendLine("- " + PlannedRuntimeScripts[i]);
            }
            lines.AppendLine();
            lines.AppendLine("SCRIPT RESPONSIBILITIES");
            lines.AppendLine("- NorscopeSettingsData: versioned serializable persisted values only");
            lines.AppendLine("- NorscopeSettingsPersistence: atomic JSON load/write/recovery");
            lines.AppendLine("- NorscopeSettingsService: single state owner and typed change events");
            lines.AppendLine("- NorscopeSettingsConfiguration: serialized license and file configuration");
            lines.AppendLine("- SettingsPanel: binds authored controls; creates no UI hierarchy");
            lines.AppendLine("- SettingsViewNavigator: switches authored internal views only");
            lines.AppendLine("- Enum files: stable language, text-size and internal-view identifiers");
            lines.AppendLine();
        }

        private static void AppendNewAssets(StringBuilder lines)
        {
            lines.AppendLine("EXACT NEW ASSETS PLANNED FOR FUTURE APPROVED APPLY");
            lines.AppendLine("- " + SettingsPanelPath);
            lines.AppendLine("- " + SettingsConfigPath);
            lines.AppendLine("  schemaVersion=1, licenseName=Enterprise, fileName=norscope-settings.json");
            for (int i = 0; i < PlannedIconAssets.Length; i++)
            {
                lines.AppendLine("- " + PlannedIconAssets[i] +
                                 " (official Lucide geometry; UI sprite)");
            }
            lines.AppendLine();
            lines.AppendLine("NOT CREATED IN PHASE 2A");
            lines.AppendLine("- No AudioMixer (Phase 2C)");
            lines.AppendLine("- No localization table or LocalizedText (Phase 2B)");
            lines.AppendLine("- No text-scaling component (Phase 2D)");
            lines.AppendLine("- No TTS, speech-recognition or microphone service");
            lines.AppendLine();
        }

        private static void AppendHierarchy(StringBuilder lines)
        {
            lines.AppendLine("EXACT AUTHORED SETTINGS PREFAB HIERARCHY");
            lines.AppendLine("SettingsPanel [Canvas, NorscopePanelRoot, SpatialWindowBindings,");
            lines.AppendLine("               SettingsPanel, SettingsViewNavigator, PanelNavigationBinding]");
            lines.AppendLine("|- Background");
            lines.AppendLine("|- Border");
            lines.AppendLine("|- FigmaContent [880 x 580]");
            lines.AppendLine("|  |- SettingsView");
            lines.AppendLine("|  |  |- TopBar");
            lines.AppendLine("|  |  |  |- Logo");
            lines.AppendLine("|  |  |  |- BrandSeparator");
            lines.AppendLine("|  |  |  |- SettingsBreadcrumb");
            lines.AppendLine("|  |  |  |- OfflinePill");
            lines.AppendLine("|  |  |  `- TechnicianPill");
            lines.AppendLine("|  |  |- Body");
            lines.AppendLine("|  |  |  |- Sidebar");
            lines.AppendLine("|  |  |  |  |- LanguageButton");
            lines.AppendLine("|  |  |  |  |- AudioButton");
            lines.AppendLine("|  |  |  |  |- AboutButton");
            lines.AppendLine("|  |  |  |  `- TechnicianCard");
            lines.AppendLine("|  |  |  `- Detail");
            lines.AppendLine("|  |  |     |- SectionHeader");
            lines.AppendLine("|  |  |     |- LanguageQuickView");
            lines.AppendLine("|  |  |     |- AudioView");
            lines.AppendLine("|  |  |     |  |- MasterVolumeCard [disabled until Phase 2C]");
            lines.AppendLine("|  |  |     |  |- VoiceGuidanceCard [disabled]");
            lines.AppendLine("|  |  |     |  |- VoiceGuidanceToggle [disabled]");
            lines.AppendLine("|  |  |     |  |- AlertSoundsToggle [disabled until Phase 2C]");
            lines.AppendLine("|  |  |     |  `- MicrophoneToggle [disabled]");
            lines.AppendLine("|  |  |     `- AboutView");
            lines.AppendLine("|  |  `- Footer");
            lines.AppendLine("|  `- LanguageView");
            lines.AppendLine("|     |- TopBar [InternalBackButton and breadcrumb]");
            lines.AppendLine("|     |- Body");
            lines.AppendLine("|     |  |- LanguageSelection");
            lines.AppendLine("|     |  |  |- EnglishCard [disabled until Phase 2B]");
            lines.AppendLine("|     |  |  `- GermanCard [disabled until Phase 2B]");
            lines.AppendLine("|     |  `- TranslationPreview");
            lines.AppendLine("|     `- Footer");
            lines.AppendLine("|- TitleBar");
            lines.AppendLine("|  |- GrabRegion");
            lines.AppendLine("|  `- CloseButton [existing shared prefab]");
            lines.AppendLine("`- ResizeHandles");
            lines.AppendLine("   `- BottomRight");
            lines.AppendLine();
            lines.AppendLine("All hierarchy objects are prefab-authored. Runtime may bind data and");
            lines.AppendLine("switch authored views; it will not create, position or resize UI.");
            lines.AppendLine();
        }

        private static void AppendRegistryAndNavigation(StringBuilder lines)
        {
            lines.AppendLine("REGISTRY AND NAVIGATION");
            lines.AppendLine("- Bootstrap PanelRegistry entry PanelType.Settings (6)");
            lines.AppendLine("- SpatialWindowId.Settings (7)");
            lines.AppendLine("- PanelBehaviourType.WorldLocked (2)");
            lines.AppendLine("- Replace only the current null Settings prefab reference");
            lines.AppendLine("- PanelNavigationBinding: panelType=Settings, fallbackParent=Home");
            lines.AppendLine("- External Back uses existing PanelNavigationService");
            lines.AppendLine("- Language Back is internal and restores the prior Settings view");
            lines.AppendLine("- Existing Home Settings card remains unchanged");
            lines.AppendLine("- Duplicate prevention remains owned by SpatialWindowManager");
            lines.AppendLine();
        }

        private static void AppendPersistence(StringBuilder lines)
        {
            lines.AppendLine("PERSISTENCE SCHEMA");
            lines.AppendLine("- schemaVersion: 1");
            lines.AppendLine("- language: en");
            lines.AppendLine("- masterVolume: 100 (current effective volume preserved)");
            lines.AppendLine("- alertSoundsEnabled: true");
            lines.AppendLine("- textSize: Medium");
            lines.AppendLine("- lastSettingsView: Language (optional convenience state)");
            lines.AppendLine("- File: Application.persistentDataPath/norscope-settings.json");
            lines.AppendLine("- Write: temporary file then atomic replace/move where supported");
            lines.AppendLine("- Recovery: quarantine malformed JSON, log once, use safe defaults");
            lines.AppendLine("- Migration: schema-version switch with additive future migrations");
            lines.AppendLine("- Service initializes before user-facing panels through Bootstrap");
            lines.AppendLine("- Phase 2A controls do not mutate values owned by later phases");
            lines.AppendLine("- No PlayerPrefs and no unsupported voice/brightness fields");
            lines.AppendLine();
        }

        private static void AppendDisabledControls(StringBuilder lines)
        {
            lines.AppendLine("DISABLED / DEFERRED CONTROL BEHAVIOR");
            lines.AppendLine("- Voice Guidance toggle and volume: non-interactable,");
            lines.AppendLine("  \"Voice guidance not installed\"");
            lines.AppendLine("- Microphone: non-interactable, \"Voice input not installed\"");
            lines.AppendLine("- Language cards: visible but non-interactable until Phase 2B");
            lines.AppendLine("- Master Volume and Alert Sounds: visible but non-interactable");
            lines.AppendLine("  until their real AudioMixer integration in Phase 2C");
            lines.AppendLine("- Disabled controls have no value-changing listeners and persist nothing");
            lines.AppendLine();
        }

        private static void AppendAbout(StringBuilder lines)
        {
            lines.AppendLine("ABOUT METADATA BINDINGS");
            lines.AppendLine("- Application: NORSCOPE");
            lines.AppendLine("- Version: Application.version");
            lines.AppendLine("- Build: Unavailable");
            lines.AppendLine("- Platform: runtime UWP/HoloLens platform resolver");
            lines.AppendLine("- License: NorscopeSettingsConfiguration.licenseName (Enterprise)");
            lines.AppendLine("- Figma's v4.2.1 is not used as runtime metadata");
            lines.AppendLine("- Stale Phase2DeployConfigurator version assignment remains isolated;");
            lines.AppendLine("  Settings 2A does not invoke or modify that tooling");
            lines.AppendLine();
        }

        private static void AppendSpatialWindow(StringBuilder lines)
        {
            lines.AppendLine("SPATIAL WINDOW BINDINGS");
            lines.AppendLine("- Existing XRI SpatialWindow architecture only; no MRTK");
            lines.AppendLine("- NorscopePanelRoot on prefab root");
            lines.AppendLine("- SpatialWindowBindings receives authored GrabRegion,");
            lines.AppendLine("  BottomRight resize handle and CloseButton root");
            lines.AppendLine("- Existing close prefab retains Button and audio behavior");
            lines.AppendLine("- World-space scale and window transforms are not authored by Settings code");
            lines.AppendLine("- Fixed 880 x 580 content is fitted inside the existing window convention");
            lines.AppendLine();
        }

        private static void AppendExistingModifications(StringBuilder lines)
        {
            lines.AppendLine("EXACT EXISTING FILE MODIFICATION PLANNED");
            lines.AppendLine("- " + BootstrapPath);
            lines.AppendLine("  Add one NorscopeSettingsService component/object and assign configuration");
            lines.AppendLine("  Assign SettingsPanel.prefab to the existing Settings registry entry");
            lines.AppendLine();
            lines.AppendLine("NO EXISTING RUNTIME SOURCE MODIFICATION IN PHASE 2A");
            lines.AppendLine("- PanelRegistry, PanelNavigationService, SpatialWindowManager and");
            lines.AppendLine("  SpatialWindow remain byte-for-byte unchanged");
            lines.AppendLine("- UIAudioManager remains unchanged until Phase 2C");
            lines.AppendLine();
        }

        private static void AppendProtectedScope(StringBuilder lines)
        {
            lines.AppendLine("PROTECTED AND UNTOUCHED");
            lines.AppendLine("- HomePanel.prefab and HomeLauncher.prefab");
            lines.AppendLine("- TaskLibraryPanel.prefab and TaskCard.prefab");
            lines.AppendLine("- ActiveTaskPanel.prefab and TaskCompletionPanel.prefab");
            lines.AppendLine("- NavigationBackButton.prefab and NorscopeCloseButton.prefab");
            lines.AppendLine("- Task data, workflow state, reports, Ask AI and cancellation logic");
            lines.AppendLine("- Audio, localization and text-size implementation deferred to 2C/2B/2D");
            lines.AppendLine();
        }

        private static void PrepareIconAssets()
        {
            for (int i = 0; i < PlannedIconAssets.Length; i++)
            {
                string target = PlannedIconAssets[i];
                if (!File.Exists(target))
                {
                    string source = TemporaryIconSourceFolder + "/" +
                                    Path.GetFileName(target);
                    if (!File.Exists(source))
                        throw new FileNotFoundException(
                            "Prepared official Lucide sprite source is missing.", source);

                    Directory.CreateDirectory(
                        Path.GetDirectoryName(target) ??
                        "Assets/Norscope/UI/Icons/Lucide/Raster");
                    File.Copy(source, target, false);
                }

                AssetDatabase.ImportAsset(
                    target,
                    ImportAssetOptions.ForceSynchronousImport |
                    ImportAssetOptions.ForceUpdate);
                TextureImporter importer =
                    AssetImporter.GetAtPath(target) as TextureImporter;
                if (importer == null)
                    throw new InvalidOperationException(
                        "Texture importer unavailable for " + target);

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.maxTextureSize = 256;
                importer.SaveAndReimport();
            }
        }

        private static NorscopeSettingsConfiguration CreateConfigurationIfMissing()
        {
            NorscopeSettingsConfiguration existing =
                AssetDatabase.LoadAssetAtPath<NorscopeSettingsConfiguration>(
                    SettingsConfigPath);
            if (existing != null)
                return existing;

            Directory.CreateDirectory(
                Path.GetDirectoryName(SettingsConfigPath) ??
                "Assets/Norscope/Data/Settings");
            var configuration =
                ScriptableObject.CreateInstance<NorscopeSettingsConfiguration>();
            configuration.name = "NorscopeSettingsConfiguration";
            AssetDatabase.CreateAsset(configuration, SettingsConfigPath);
            return configuration;
        }

        private static GameObject CreateSettingsPrefabIfMissing(
            NorscopeSettingsConfiguration configuration)
        {
            GameObject existing =
                AssetDatabase.LoadAssetAtPath<GameObject>(SettingsPanelPath);
            if (existing != null)
                return existing;

            Sprite rounded = AssetDatabase.LoadAssetAtPath<Sprite>(RoundedPath);
            TMP_FontAsset outfit =
                AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OutfitPath);
            TMP_FontAsset inter =
                AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(InterPath);
            Sprite logo = AssetDatabase.LoadAssetAtPath<Sprite>(LogoPath);
            if (rounded == null || outfit == null || inter == null)
                throw new InvalidOperationException(
                    "Required Settings visual assets are missing.");

            GameObject root = new GameObject(
                "SettingsPanel",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(NorscopePanelRoot),
                typeof(SpatialWindowBindings),
                typeof(SettingsPanel),
                typeof(SettingsViewNavigator),
                typeof(PanelNavigationBinding));
            try
            {
                root.layer = 5;
                RectTransform rootRt = root.GetComponent<RectTransform>();
                rootRt.sizeDelta = new Vector2(880f, 580f);
                rootRt.localScale = Vector3.one * 0.001f;
                Canvas canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.sortingOrder = 10;
                root.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 1f;

                Image background = ImageNode(
                    root.transform, "Background", rounded, Hex("0D1117"));
                Stretch(background.rectTransform);
                background.type = Image.Type.Sliced;
                background.raycastTarget = false;

                Image border = ImageNode(
                    root.transform, "Border", rounded, Hex("2D3B49"));
                Stretch(border.rectTransform, -1f);
                border.type = Image.Type.Sliced;
                border.raycastTarget = false;

                RectTransform content =
                    RectNode(root.transform, "FigmaContent");
                Stretch(content);

                var deferred = new List<Selectable>();
                BuildSettingsView(
                    content,
                    rounded,
                    outfit,
                    inter,
                    logo,
                    deferred,
                    out GameObject settingsView,
                    out GameObject languageQuickView,
                    out GameObject audioView,
                    out GameObject aboutView,
                    out Button languageNav,
                    out Button languageQuick,
                    out Button audioNav,
                    out Button aboutNav,
                    out TMP_Text applicationValue,
                    out TMP_Text versionValue,
                    out TMP_Text buildValue,
                    out TMP_Text platformValue,
                    out TMP_Text unityValue,
                    out TMP_Text companyValue,
                    out TMP_Text copyrightValue,
                    out Button externalBack);

                BuildLanguageView(
                    content,
                    rounded,
                    outfit,
                    inter,
                    logo,
                    deferred,
                    out GameObject languageView,
                    out Button internalBack);
                languageView.SetActive(false);

                RectTransform titleBar =
                    RectNode(root.transform, "TitleBar");
                SetTopLeft(titleBar, Vector2.zero, new Vector2(880f, 56f));

                Image grab = ImageNode(
                    titleBar, "GrabRegion", null, new Color(0f, 0f, 0f, 0f));
                SetTopLeft(
                    grab.rectTransform,
                    new Vector2(120f, 0f),
                    new Vector2(680f, 56f));
                grab.raycastTarget = true;

                GameObject closePrefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(CloseButtonPath);
                GameObject close = closePrefab != null
                    ? PrefabUtility.InstantiatePrefab(closePrefab, titleBar) as GameObject
                    : null;
                if (close == null)
                    throw new InvalidOperationException("Close button prefab missing.");
                close.name = "CloseButton";
                SetTopRight(
                    close.GetComponent<RectTransform>(),
                    new Vector2(14f, 10f),
                    new Vector2(40f, 40f));

                RectTransform resizeHandles =
                    RectNode(root.transform, "ResizeHandles");
                Stretch(resizeHandles);
                Image resize = ImageNode(
                    resizeHandles, "BottomRight", rounded,
                    new Color(0.18f, 0.24f, 0.3f, 0.9f));
                SetBottomRight(
                    resize.rectTransform,
                    new Vector2(6f, 6f),
                    new Vector2(28f, 28f));
                resize.raycastTarget = true;

                SettingsViewNavigator navigator =
                    root.GetComponent<SettingsViewNavigator>();
                navigator.Configure(
                    settingsView,
                    languageView,
                    languageQuickView,
                    audioView,
                    aboutView,
                    languageNav,
                    languageQuick,
                    audioNav,
                    aboutNav,
                    internalBack);

                SettingsPanel settingsPanel = root.GetComponent<SettingsPanel>();
                settingsPanel.Configure(
                    navigator,
                    deferred.ToArray(),
                    applicationValue,
                    versionValue,
                    buildValue,
                    platformValue,
                    unityValue,
                    companyValue,
                    copyrightValue);

                PanelNavigationBinding navigation =
                    root.GetComponent<PanelNavigationBinding>();
                navigation.Configure(
                    PanelType.Settings,
                    PanelType.Home,
                    externalBack);

                ConfigurePanelRoot(
                    root.GetComponent<NorscopePanelRoot>(),
                    rootRt,
                    background,
                    border);
                ConfigureWindowBindings(
                    root.GetComponent<SpatialWindowBindings>(),
                    grab.transform,
                    resize.rectTransform,
                    close.transform);
                SetLayerRecursively(root, 5);

                GameObject saved =
                    PrefabUtility.SaveAsPrefabAsset(root, SettingsPanelPath);
                if (saved == null)
                    throw new InvalidOperationException(
                        "SettingsPanel.prefab could not be saved.");
                return saved;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void BuildSettingsView(
            Transform content,
            Sprite rounded,
            TMP_FontAsset outfit,
            TMP_FontAsset inter,
            Sprite logo,
            List<Selectable> deferred,
            out GameObject settingsView,
            out GameObject languageQuickView,
            out GameObject audioView,
            out GameObject aboutView,
            out Button languageNav,
            out Button languageQuick,
            out Button audioNav,
            out Button aboutNav,
            out TMP_Text applicationValue,
            out TMP_Text versionValue,
            out TMP_Text buildValue,
            out TMP_Text platformValue,
            out TMP_Text unityValue,
            out TMP_Text companyValue,
            out TMP_Text copyrightValue,
            out Button externalBack)
        {
            RectTransform view = RectNode(content, "SettingsView");
            Stretch(view);
            settingsView = view.gameObject;

            RectTransform topBar = RectNode(view, "TopBar");
            SetTopLeft(topBar, Vector2.zero, new Vector2(880f, 58f));
            Image topBorder = ImageNode(
                topBar, "BottomBorder", null, Hex("2D3B49"));
            SetBottomLeft(
                topBorder.rectTransform,
                Vector2.zero,
                new Vector2(880f, 1f));
            topBorder.raycastTarget = false;

            Image logoImage = ImageNode(
                topBar, "Logo", logo, new Color(1f, 1f, 1f, 0.8f));
            SetTopLeft(
                logoImage.rectTransform,
                new Vector2(28f, 20f),
                new Vector2(104f, 18f));
            logoImage.preserveAspect = true;
            logoImage.raycastTarget = false;

            Image separator = ImageNode(
                topBar, "BrandSeparator", null, Hex("2D3B49"));
            SetTopLeft(
                separator.rectTransform,
                new Vector2(144f, 21f),
                new Vector2(1f, 16f));
            separator.raycastTarget = false;

            Image settingsIcon = ImageNode(
                topBar, "SettingsIcon", Icon("settings"), Hex("4A5D6E"));
            SetTopLeft(
                settingsIcon.rectTransform,
                new Vector2(157f, 23f),
                new Vector2(12f, 12f));
            settingsIcon.preserveAspect = true;
            settingsIcon.raycastTarget = false;
            TextNode(
                topBar, "SettingsBreadcrumb", "Settings", outfit, 14f,
                Hex("91A2B5"), new Vector2(175f, 18f), new Vector2(110f, 22f),
                TextAlignmentOptions.MidlineLeft);

            Pill(topBar, "OfflinePill", "Offline", Icon("wifi-off"),
                 new Vector2(692f, 17f), new Vector2(74f, 24f),
                 rounded, inter);
            Pill(topBar, "TechnicianPill", "Technician", Icon("user"),
                 new Vector2(774f, 17f), new Vector2(88f, 24f),
                 rounded, inter);

            GameObject backPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(BackButtonPath);
            GameObject back = backPrefab != null
                ? PrefabUtility.InstantiatePrefab(backPrefab, topBar) as GameObject
                : null;
            if (back == null)
                throw new InvalidOperationException("NavigationBackButton prefab missing.");
            back.name = "BackButton";
            SetTopLeft(
                back.GetComponent<RectTransform>(),
                new Vector2(2f, 9f),
                new Vector2(40f, 40f));
            externalBack = back.GetComponent<Button>();

            RectTransform body = RectNode(view, "Body");
            SetTopLeft(body, new Vector2(0f, 58f), new Vector2(880f, 490f));
            RectTransform sidebar = RectNode(body, "Sidebar");
            SetTopLeft(sidebar, Vector2.zero, new Vector2(220f, 490f));
            Image sidebarSurface = sidebar.gameObject.AddComponent<Image>();
            sidebarSurface.sprite = rounded;
            sidebarSurface.type = Image.Type.Sliced;
            sidebarSurface.color = new Color(0.075f, 0.102f, 0.133f, 0.55f);
            sidebarSurface.raycastTarget = false;
            Image sideBorder = ImageNode(
                sidebar, "RightBorder", null, Hex("2D3B49"));
            SetTopRight(
                sideBorder.rectTransform,
                Vector2.zero,
                new Vector2(1f, 490f));
            sideBorder.raycastTarget = false;

            languageNav = NavButton(
                sidebar, "LanguageButton", "Language", "Interface language",
                Icon("globe"), 20f, rounded, outfit, inter);
            audioNav = NavButton(
                sidebar, "AudioButton", "Audio", "Speech & alerts",
                Icon("volume-2"), 82f, rounded, outfit, inter);
            aboutNav = NavButton(
                sidebar, "AboutButton", "About", "Version info",
                Icon("info"), 144f, rounded, outfit, inter);
            TechnicianCard(
                sidebar, rounded, outfit, inter, Icon("user"));

            RectTransform detail = RectNode(body, "Detail");
            SetTopLeft(detail, new Vector2(220f, 0f), new Vector2(660f, 490f));

            languageQuickView = SectionView(
                detail, "LanguageQuickView", "Language", outfit);
            LanguageQuick(
                languageQuickView.transform, rounded, outfit, inter,
                out languageQuick);

            audioView = SectionView(detail, "AudioView", "Audio", outfit);
            BuildAudioView(audioView.transform, rounded, outfit, inter, deferred);

            aboutView = SectionView(detail, "AboutView", "About", outfit);
            BuildAboutView(
                aboutView.transform,
                rounded,
                outfit,
                inter,
                out applicationValue,
                out versionValue,
                out buildValue,
                out platformValue,
                out unityValue,
                out companyValue,
                out copyrightValue);

            audioView.SetActive(false);
            aboutView.SetActive(false);

            Footer(view, inter);
        }

        private static void BuildLanguageView(
            Transform content,
            Sprite rounded,
            TMP_FontAsset outfit,
            TMP_FontAsset inter,
            Sprite logo,
            List<Selectable> deferred,
            out GameObject languageView,
            out Button internalBack)
        {
            RectTransform view = RectNode(content, "LanguageView");
            Stretch(view);
            languageView = view.gameObject;

            RectTransform topBar = RectNode(view, "TopBar");
            SetTopLeft(topBar, Vector2.zero, new Vector2(880f, 58f));
            Image border = ImageNode(
                topBar, "BottomBorder", null, Hex("2D3B49"));
            SetBottomLeft(border.rectTransform, Vector2.zero, new Vector2(880f, 1f));
            border.raycastTarget = false;

            Image logoImage = ImageNode(
                topBar, "Logo", logo, new Color(1f, 1f, 1f, 0.8f));
            SetTopLeft(
                logoImage.rectTransform,
                new Vector2(28f, 20f),
                new Vector2(104f, 18f));
            logoImage.preserveAspect = true;
            logoImage.raycastTarget = false;
            Image separator = ImageNode(
                topBar, "BrandSeparator", null, Hex("2D3B49"));
            SetTopLeft(
                separator.rectTransform,
                new Vector2(144f, 21f),
                new Vector2(1f, 16f));
            separator.raycastTarget = false;

            internalBack = IconLabelButton(
                topBar,
                "InternalBackButton",
                "Settings",
                Icon("arrow-left"),
                new Vector2(157f, 12f),
                new Vector2(112f, 34f),
                rounded,
                inter,
                true);
            TextNode(
                topBar, "BreadcrumbSeparator", "/", inter, 12f, Hex("4A5D6E"),
                new Vector2(276f, 18f), new Vector2(12f, 22f),
                TextAlignmentOptions.Center);
            TextNode(
                topBar, "LanguageBreadcrumb", "Language", outfit, 14f,
                Hex("91A2B5"), new Vector2(296f, 18f), new Vector2(100f, 22f),
                TextAlignmentOptions.MidlineLeft);
            Pill(topBar, "LanguagePill", "English", Icon("globe"),
                 new Vector2(774f, 17f), new Vector2(88f, 24f),
                 rounded, inter);

            RectTransform body = RectNode(view, "Body");
            SetTopLeft(body, new Vector2(0f, 58f), new Vector2(880f, 490f));
            RectTransform selection = RectNode(body, "LanguageSelection");
            SetTopLeft(
                selection, new Vector2(0f, 0f), new Vector2(640f, 490f));
            TextNode(
                selection, "Title", "Choose Language", outfit, 26f,
                Hex("F3F7FB"), new Vector2(32f, 28f), new Vector2(400f, 36f),
                TextAlignmentOptions.TopLeft);
            Image accent = ImageNode(
                selection, "TitleAccent", null, Hex("1EC8F5"));
            SetTopLeft(
                accent.rectTransform,
                new Vector2(32f, 68f),
                new Vector2(32f, 2f));
            accent.raycastTarget = false;
            TextNode(
                selection, "Subtitle",
                "The interface updates immediately after selection.",
                inter, 13f, Hex("91A2B5"),
                new Vector2(32f, 80f), new Vector2(520f, 24f),
                TextAlignmentOptions.TopLeft);

            Button english = LanguageCard(
                selection, "EnglishCard", "English", "English",
                new Vector2(32f, 126f), rounded, outfit, inter, true);
            Button german = LanguageCard(
                selection, "GermanCard", "Deutsch", "German",
                new Vector2(32f, 220f), rounded, outfit, inter, false);
            english.interactable = false;
            german.interactable = false;
            deferred.Add(english);
            deferred.Add(german);
            TextNode(
                selection, "UnavailableMessage",
                "Language switching is installed in Phase 2B.",
                inter, 12f, Hex("4A5D6E"),
                new Vector2(32f, 326f), new Vector2(520f, 22f),
                TextAlignmentOptions.Center);

            RectTransform preview = RectNode(body, "TranslationPreview");
            SetTopLeft(
                preview, new Vector2(640f, 0f), new Vector2(240f, 490f));
            Image previewBg = preview.gameObject.AddComponent<Image>();
            previewBg.color = new Color(0.075f, 0.102f, 0.133f, 0.6f);
            previewBg.raycastTarget = false;
            TextNode(
                preview, "Heading", "TRANSLATION PREVIEW", inter, 10.5f,
                Hex("4A5D6E"), new Vector2(24f, 28f), new Vector2(190f, 20f),
                TextAlignmentOptions.TopLeft);
            PreviewRow(preview, "HomeRow", "Home", "Home", 72f, outfit, inter);
            PreviewRow(
                preview, "TasksRow", "Task Library", "Task Library",
                138f, outfit, inter);
            PreviewRow(
                preview, "SettingsRow", "Settings", "Settings",
                204f, outfit, inter);
            PreviewRow(
                preview, "CompleteRow", "Task Completed", "Task Completed",
                270f, outfit, inter);
            TextNode(
                preview, "LivePreview", "●  Live preview", inter, 11f,
                Hex("91A2B5"), new Vector2(24f, 432f), new Vector2(180f, 20f),
                TextAlignmentOptions.MidlineLeft);

            Footer(view, inter);
        }

        private static void BuildAudioView(
            Transform parent,
            Sprite rounded,
            TMP_FontAsset outfit,
            TMP_FontAsset inter,
            List<Selectable> deferred)
        {
            VolumeCard(
                parent, "MasterVolumeCard", "Master Volume", "100",
                Icon("volume-2"), 82f, rounded, outfit, inter, deferred,
                "Audio integration pending Phase 2C");
            VolumeCard(
                parent, "VoiceGuidanceCard", "Voice Guidance", "—",
                Icon("volume-1"), 208f, rounded, outfit, inter, deferred,
                "Voice guidance not installed");
            ToggleCard(
                parent, "VoiceGuidanceToggle", "Voice guidance",
                "Voice guidance not installed", Icon("volume-1"),
                new Vector2(32f, 342f), new Vector2(180f, 72f),
                rounded, outfit, inter, deferred);
            ToggleCard(
                parent, "AlertSoundsToggle", "Alert sounds",
                "Available in Phase 2C", Icon("bell"),
                new Vector2(222f, 342f), new Vector2(180f, 72f),
                rounded, outfit, inter, deferred);
            ToggleCard(
                parent, "MicrophoneToggle", "Microphone",
                "Voice input not installed", Icon("mic"),
                new Vector2(412f, 342f), new Vector2(180f, 72f),
                rounded, outfit, inter, deferred);
        }

        private static void BuildAboutView(
            Transform parent,
            Sprite rounded,
            TMP_FontAsset outfit,
            TMP_FontAsset inter,
            out TMP_Text applicationValue,
            out TMP_Text versionValue,
            out TMP_Text buildValue,
            out TMP_Text platformValue,
            out TMP_Text unityValue,
            out TMP_Text companyValue,
            out TMP_Text copyrightValue)
        {
            Image card = ImageNode(parent, "AboutCard", rounded, Hex("131A22"));
            SetTopLeft(
                card.rectTransform,
                new Vector2(32f, 82f),
                new Vector2(596f, 442f));
            card.type = Image.Type.Sliced;
            card.raycastTarget = false;

            applicationValue = AboutRow(
                card.transform, "ApplicationRow", "Application",
                SettingsAboutBinder.ApplicationName,
                20f, outfit, inter);
            versionValue = AboutRow(
                card.transform, "VersionRow", "Version",
                SettingsAboutBinder.Version,
                76f, outfit, inter);
            buildValue = AboutRow(
                card.transform, "BuildRow", "Build",
                SettingsAboutBinder.Build,
                132f, outfit, inter);
            platformValue = AboutRow(
                card.transform, "PlatformRow", "Platform",
                SettingsAboutBinder.Platform,
                188f, outfit, inter);
            unityValue = AboutRow(
                card.transform, "UnityRow", "Unity",
                SettingsAboutBinder.UnityVersion,
                244f, outfit, inter);
            companyValue = AboutRow(
                card.transform, "CompanyRow", "Company",
                SettingsAboutBinder.Company,
                300f, outfit, inter);
            copyrightValue = AboutRow(
                card.transform, "CopyrightRow", "Copyright",
                SettingsAboutBinder.Copyright,
                356f, outfit, inter);
        }

        private static GameObject SectionView(
            Transform parent,
            string name,
            string title,
            TMP_FontAsset outfit)
        {
            RectTransform view = RectNode(parent, name);
            Stretch(view);
            TextNode(
                view, "SectionTitle", title, outfit, 26f, Hex("F3F7FB"),
                new Vector2(32f, 26f), new Vector2(400f, 36f),
                TextAlignmentOptions.TopLeft);
            Image accent = ImageNode(
                view, "TitleAccent", null, Hex("1EC8F5"));
            SetTopLeft(
                accent.rectTransform,
                new Vector2(32f, 68f),
                new Vector2(32f, 2f));
            accent.raycastTarget = false;
            return view.gameObject;
        }

        private static void LanguageQuick(
            Transform parent,
            Sprite rounded,
            TMP_FontAsset outfit,
            TMP_FontAsset inter,
            out Button changeButton)
        {
            Image current = ImageNode(
                parent, "CurrentLanguageCard", rounded,
                new Color(0.118f, 0.784f, 0.961f, 0.1f));
            SetTopLeft(
                current.rectTransform,
                new Vector2(32f, 82f),
                new Vector2(596f, 126f));
            current.type = Image.Type.Sliced;
            current.raycastTarget = false;
            TextNode(
                current.transform, "Flag", "EN", outfit, 24f, Hex("1EC8F5"),
                new Vector2(22f, 35f), new Vector2(60f, 44f),
                TextAlignmentOptions.Center);
            TextNode(
                current.transform, "Caption", "ACTIVE LANGUAGE", inter, 10.5f,
                Hex("4A5D6E"), new Vector2(100f, 26f), new Vector2(250f, 18f),
                TextAlignmentOptions.TopLeft);
            TextNode(
                current.transform, "NativeName", "English", outfit, 24f,
                Hex("1EC8F5"), new Vector2(100f, 47f), new Vector2(300f, 34f),
                TextAlignmentOptions.TopLeft);
            TextNode(
                current.transform, "Name", "English", inter, 12f,
                Hex("91A2B5"), new Vector2(100f, 82f), new Vector2(250f, 20f),
                TextAlignmentOptions.TopLeft);
            Image check = ImageNode(
                current.transform, "SelectedCheck", Icon("check"), Hex("0D1117"));
            SetTopRight(
                check.rectTransform,
                new Vector2(24f, 44f),
                new Vector2(18f, 18f));
            check.preserveAspect = true;
            check.raycastTarget = false;

            changeButton = IconLabelButton(
                parent, "ChangeLanguageButton", "Change Language", Icon("globe"),
                new Vector2(32f, 224f), new Vector2(596f, 72f),
                rounded, inter, false);
            TextNode(
                changeButton.transform, "Description",
                "English and Deutsch available",
                inter, 11.5f, Hex("4A5D6E"),
                new Vector2(52f, 38f), new Vector2(420f, 20f),
                TextAlignmentOptions.TopLeft);
            Image chevron = ImageNode(
                changeButton.transform, "Chevron", Icon("chevron-right"),
                Hex("4A5D6E"));
            SetTopRight(
                chevron.rectTransform,
                new Vector2(18f, 28f),
                new Vector2(15f, 15f));
            chevron.preserveAspect = true;
            chevron.raycastTarget = false;
            TextNode(
                parent, "ImmediateNote",
                "Language switching will update all NORSCOPE screens in Phase 2B.",
                inter, 12f, Hex("4A5D6E"),
                new Vector2(32f, 314f), new Vector2(596f, 30f),
                TextAlignmentOptions.TopLeft);
        }

        private static void VolumeCard(
            Transform parent,
            string name,
            string label,
            string value,
            Sprite iconSprite,
            float y,
            Sprite rounded,
            TMP_FontAsset outfit,
            TMP_FontAsset inter,
            List<Selectable> deferred,
            string unavailableMessage)
        {
            Image card = ImageNode(parent, name, rounded, Hex("131A22"));
            SetTopLeft(
                card.rectTransform,
                new Vector2(32f, y),
                new Vector2(596f, 112f));
            card.type = Image.Type.Sliced;
            card.raycastTarget = false;

            Image icon = ImageNode(card.transform, "Icon", iconSprite, Hex("4A5D6E"));
            SetTopLeft(icon.rectTransform, new Vector2(20f, 18f), new Vector2(16f, 16f));
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            TextNode(
                card.transform, "Label", label, outfit, 14.5f, Hex("F3F7FB"),
                new Vector2(46f, 14f), new Vector2(250f, 24f),
                TextAlignmentOptions.MidlineLeft);
            TextNode(
                card.transform, "Value", value, outfit, 20f, Hex("4A5D6E"),
                new Vector2(520f, 12f), new Vector2(52f, 28f),
                TextAlignmentOptions.MidlineRight);

            Button minus = IconOnlyButton(
                card.transform, "DecreaseButton", Icon("minus"),
                new Vector2(20f, 54f), new Vector2(38f, 38f), rounded);
            Button plus = IconOnlyButton(
                card.transform, "IncreaseButton", Icon("plus"),
                new Vector2(538f, 54f), new Vector2(38f, 38f), rounded);
            minus.interactable = false;
            plus.interactable = false;
            deferred.Add(minus);
            deferred.Add(plus);

            Image track = ImageNode(
                card.transform, "Track", rounded, Hex("2D3B49"));
            SetTopLeft(
                track.rectTransform,
                new Vector2(74f, 67f),
                new Vector2(448f, 6f));
            track.type = Image.Type.Sliced;
            track.raycastTarget = false;
            TextNode(
                card.transform, "UnavailableMessage", unavailableMessage,
                inter, 9.5f, Hex("4A5D6E"),
                new Vector2(74f, 80f), new Vector2(448f, 18f),
                TextAlignmentOptions.Center);
        }

        private static void ToggleCard(
            Transform parent,
            string name,
            string label,
            string unavailable,
            Sprite iconSprite,
            Vector2 position,
            Vector2 size,
            Sprite rounded,
            TMP_FontAsset outfit,
            TMP_FontAsset inter,
            List<Selectable> deferred)
        {
            Button button = ButtonSurface(
                parent, name, position, size, rounded, Hex("131A22"));
            button.interactable = false;
            deferred.Add(button);
            Image icon = ImageNode(
                button.transform, "Icon", iconSprite, Hex("4A5D6E"));
            SetTopLeft(icon.rectTransform, new Vector2(14f, 16f), new Vector2(14f, 14f));
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            TextNode(
                button.transform, "Label", label, outfit, 12.5f, Hex("91A2B5"),
                new Vector2(38f, 10f), new Vector2(size.x - 88f, 22f),
                TextAlignmentOptions.MidlineLeft);
            TextNode(
                button.transform, "UnavailableMessage", unavailable,
                inter, 9.5f, Hex("4A5D6E"),
                new Vector2(38f, 34f), new Vector2(size.x - 52f, 24f),
                TextAlignmentOptions.TopLeft);
            Image toggle = ImageNode(
                button.transform, "ToggleTrack", rounded, Hex("2D3B49"));
            SetTopRight(
                toggle.rectTransform,
                new Vector2(12f, 20f),
                new Vector2(36f, 20f));
            toggle.type = Image.Type.Sliced;
            toggle.raycastTarget = false;
        }

        private static Button LanguageCard(
            Transform parent,
            string name,
            string native,
            string english,
            Vector2 position,
            Sprite rounded,
            TMP_FontAsset outfit,
            TMP_FontAsset inter,
            bool selected)
        {
            Button button = ButtonSurface(
                parent, name, position, new Vector2(576f, 80f), rounded,
                selected
                    ? new Color(0.118f, 0.784f, 0.961f, 0.1f)
                    : Hex("131A22"));
            TextNode(
                button.transform, "LanguageCode",
                native == "English" ? "EN" : "DE",
                outfit, 16f, selected ? Hex("1EC8F5") : Hex("91A2B5"),
                new Vector2(18f, 22f), new Vector2(46f, 36f),
                TextAlignmentOptions.Center);
            TextNode(
                button.transform, "NativeName", native, outfit, 17.5f,
                selected ? Hex("1EC8F5") : Hex("F3F7FB"),
                new Vector2(82f, 16f), new Vector2(300f, 28f),
                TextAlignmentOptions.TopLeft);
            TextNode(
                button.transform, "EnglishName", english, inter, 12f,
                Hex("91A2B5"), new Vector2(82f, 45f), new Vector2(300f, 20f),
                TextAlignmentOptions.TopLeft);
            Image state = ImageNode(
                button.transform, "SelectionState",
                selected ? Icon("check") : null,
                selected ? Hex("1EC8F5") : Hex("2D3B49"));
            SetTopRight(
                state.rectTransform,
                new Vector2(22f, 26f),
                new Vector2(28f, 28f));
            state.preserveAspect = true;
            state.raycastTarget = false;
            return button;
        }

        private static void PreviewRow(
            Transform parent,
            string name,
            string source,
            string preview,
            float y,
            TMP_FontAsset outfit,
            TMP_FontAsset inter)
        {
            RectTransform row = RectNode(parent, name);
            SetTopLeft(row, new Vector2(24f, y), new Vector2(192f, 58f));
            TextNode(
                row, "Source", source, inter, 11f, Hex("4A5D6E"),
                Vector2.zero, new Vector2(192f, 20f),
                TextAlignmentOptions.TopLeft);
            TextNode(
                row, "Preview", preview, outfit, 15f, Hex("F3F7FB"),
                new Vector2(0f, 24f), new Vector2(192f, 25f),
                TextAlignmentOptions.TopLeft);
        }

        private static TMP_Text AboutRow(
            Transform parent,
            string name,
            string label,
            string value,
            float y,
            TMP_FontAsset outfit,
            TMP_FontAsset inter)
        {
            RectTransform row = RectNode(parent, name);
            SetTopLeft(row, new Vector2(20f, y), new Vector2(556f, 50f));
            TextNode(
                row, "Label", label, inter, 12f, Hex("91A2B5"),
                Vector2.zero, new Vector2(230f, 50f),
                TextAlignmentOptions.MidlineLeft);
            return TextNode(
                row, "Value", value, outfit, 14f, Hex("F3F7FB"),
                new Vector2(240f, 0f), new Vector2(316f, 50f),
                TextAlignmentOptions.MidlineRight);
        }

        private static Button NavButton(
            Transform parent,
            string name,
            string label,
            string description,
            Sprite iconSprite,
            float y,
            Sprite rounded,
            TMP_FontAsset outfit,
            TMP_FontAsset inter)
        {
            Button button = ButtonSurface(
                parent, name, new Vector2(12f, y), new Vector2(196f, 56f),
                rounded, new Color(0f, 0f, 0f, 0f));
            Image icon = ImageNode(
                button.transform, "Icon", iconSprite, Hex("91A2B5"));
            SetTopLeft(icon.rectTransform, new Vector2(12f, 18f), new Vector2(15f, 15f));
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            TextNode(
                button.transform, "Label", label, outfit, 13.5f, Hex("F3F7FB"),
                new Vector2(38f, 8f), new Vector2(142f, 22f),
                TextAlignmentOptions.MidlineLeft);
            TextNode(
                button.transform, "Description", description, inter, 10.5f,
                Hex("4A5D6E"), new Vector2(38f, 29f), new Vector2(146f, 18f),
                TextAlignmentOptions.MidlineLeft);
            return button;
        }

        private static void TechnicianCard(
            Transform parent,
            Sprite rounded,
            TMP_FontAsset outfit,
            TMP_FontAsset inter,
            Sprite userSprite)
        {
            Image card = ImageNode(
                parent, "TechnicianCard", rounded, Hex("131A22"));
            SetTopLeft(
                card.rectTransform,
                new Vector2(12f, 394f),
                new Vector2(196f, 72f));
            card.type = Image.Type.Sliced;
            card.raycastTarget = false;
            Image iconBg = ImageNode(
                card.transform, "UserIconBackground", rounded,
                new Color(0.118f, 0.784f, 0.961f, 0.1f));
            SetTopLeft(
                iconBg.rectTransform,
                new Vector2(12f, 20f),
                new Vector2(32f, 32f));
            iconBg.type = Image.Type.Sliced;
            iconBg.raycastTarget = false;
            Image icon = ImageNode(
                iconBg.transform, "UserIcon", userSprite, Hex("1EC8F5"));
            Center(icon.rectTransform, new Vector2(14f, 14f));
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            TextNode(
                card.transform, "Name", "Technician", outfit, 12.5f,
                Hex("F3F7FB"), new Vector2(54f, 16f), new Vector2(124f, 22f),
                TextAlignmentOptions.MidlineLeft);
            TextNode(
                card.transform, "Language", "English", inter, 10.5f,
                Hex("4A5D6E"), new Vector2(54f, 38f), new Vector2(124f, 18f),
                TextAlignmentOptions.MidlineLeft);
        }

        private static void Footer(Transform parent, TMP_FontAsset inter)
        {
            RectTransform footer = RectNode(parent, "Footer");
            SetBottomLeft(footer, Vector2.zero, new Vector2(880f, 32f));
            Image border = ImageNode(
                footer, "TopBorder", null, Hex("2D3B49"));
            SetTopLeft(border.rectTransform, Vector2.zero, new Vector2(880f, 1f));
            border.raycastTarget = false;
            TextNode(
                footer, "Product", "NORSCOPE Enterprise · HoloLens 2",
                inter, 10.5f, Hex("4A5D6E"),
                new Vector2(32f, 7f), new Vector2(360f, 18f),
                TextAlignmentOptions.MidlineLeft);
            TextNode(
                footer, "Version", "Runtime version",
                inter, 10.5f, Hex("4A5D6E"),
                new Vector2(688f, 7f), new Vector2(160f, 18f),
                TextAlignmentOptions.MidlineRight);
        }

        private static void Pill(
            Transform parent,
            string name,
            string label,
            Sprite iconSprite,
            Vector2 position,
            Vector2 size,
            Sprite rounded,
            TMP_FontAsset inter)
        {
            Image pill = ImageNode(parent, name, rounded, Hex("131A22"));
            SetTopLeft(pill.rectTransform, position, size);
            pill.type = Image.Type.Sliced;
            pill.raycastTarget = false;
            Image icon = ImageNode(
                pill.transform, "Icon", iconSprite, Hex("4A5D6E"));
            SetTopLeft(icon.rectTransform, new Vector2(8f, 7f), new Vector2(10f, 10f));
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            TextNode(
                pill.transform, "Label", label, inter, 10f, Hex("4A5D6E"),
                new Vector2(23f, 3f), new Vector2(size.x - 28f, 18f),
                TextAlignmentOptions.MidlineLeft);
        }

        private static Button IconLabelButton(
            Transform parent,
            string name,
            string label,
            Sprite iconSprite,
            Vector2 position,
            Vector2 size,
            Sprite rounded,
            TMP_FontAsset inter,
            bool transparent)
        {
            Button button = ButtonSurface(
                parent, name, position, size, rounded,
                transparent ? new Color(0f, 0f, 0f, 0f) : Hex("131A22"));
            Image icon = ImageNode(
                button.transform, "Icon", iconSprite, Hex("91A2B5"));
            SetTopLeft(icon.rectTransform, new Vector2(14f, 10f), new Vector2(14f, 14f));
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            TextNode(
                button.transform, "Label", label, inter, 13f, Hex("F3F7FB"),
                new Vector2(38f, 6f), new Vector2(size.x - 52f, 24f),
                TextAlignmentOptions.MidlineLeft);
            return button;
        }

        private static Button IconOnlyButton(
            Transform parent,
            string name,
            Sprite sprite,
            Vector2 position,
            Vector2 size,
            Sprite rounded)
        {
            Button button = ButtonSurface(
                parent, name, position, size, rounded, Hex("18212B"));
            Image icon = ImageNode(
                button.transform, "Icon", sprite, Hex("4A5D6E"));
            Center(icon.rectTransform, new Vector2(14f, 14f));
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            return button;
        }

        private static Button TextButton(
            Transform parent,
            string name,
            string label,
            Vector2 position,
            Vector2 size,
            Sprite rounded,
            TMP_FontAsset inter)
        {
            Button button = ButtonSurface(
                parent, name, position, size, rounded, Hex("18212B"));
            TextNode(
                button.transform, "Label", label, inter, 11.5f, Hex("4A5D6E"),
                Vector2.zero, size, TextAlignmentOptions.Center);
            return button;
        }

        private static Button ButtonSurface(
            Transform parent,
            string name,
            Vector2 position,
            Vector2 size,
            Sprite rounded,
            Color color)
        {
            Image image = ImageNode(parent, name, rounded, color);
            SetTopLeft(image.rectTransform, position, size);
            image.type = Image.Type.Sliced;
            image.raycastTarget = true;
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.04f, 1.04f, 1.04f, 1f);
            colors.pressedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0.36f);
            button.colors = colors;
            return button;
        }

        private static RectTransform RectNode(Transform parent, string name)
        {
            var value = new GameObject(name, typeof(RectTransform));
            value.transform.SetParent(parent, false);
            return value.GetComponent<RectTransform>();
        }

        private static Image ImageNode(
            Transform parent,
            string name,
            Sprite sprite,
            Color color)
        {
            RectTransform rect = RectNode(parent, name);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            return image;
        }

        private static TMP_Text TextNode(
            Transform parent,
            string name,
            string value,
            TMP_FontAsset font,
            float size,
            Color color,
            Vector2 position,
            Vector2 dimensions,
            TextAlignmentOptions alignment)
        {
            RectTransform rect = RectNode(parent, name);
            SetTopLeft(rect, position, dimensions);
            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.font = font;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }

        private static Sprite Icon(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Norscope/UI/Icons/Lucide/Raster/" + name + ".png");
        }

        private static void ConfigurePanelRoot(
            NorscopePanelRoot panel,
            RectTransform root,
            Image background,
            Image border)
        {
            SerializedObject so = new SerializedObject(panel);
            so.FindProperty("theme").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<NorscopeTheme>(ThemePath);
            so.FindProperty("worldSpaceConfig").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<NorscopeWorldSpaceUiConfig>(
                    WorldConfigPath);
            so.FindProperty("root").objectReferenceValue = root;
            so.FindProperty("background").objectReferenceValue = background;
            so.FindProperty("border").objectReferenceValue = border;
            so.FindProperty("title").objectReferenceValue = null;
            so.FindProperty("sizePixels").vector2Value = new Vector2(880f, 580f);
            so.FindProperty("sizeMeters").vector2Value = new Vector2(0.88f, 0.58f);
            so.FindProperty("useMetersAsSource").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureWindowBindings(
            SpatialWindowBindings bindings,
            Transform grab,
            RectTransform resize,
            Transform close)
        {
            SerializedObject so = new SerializedObject(bindings);
            so.FindProperty("grabRegion").objectReferenceValue = grab;
            so.FindProperty("resizeHandle").objectReferenceValue = resize;
            so.FindProperty("closeButtonRoot").objectReferenceValue = close;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireBootstrap(
            GameObject settingsPrefab,
            NorscopeSettingsConfiguration configuration)
        {
            SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                Scene scene = EditorSceneManager.OpenScene(
                    BootstrapPath, OpenSceneMode.Single);
                PanelRegistry registry =
                    UnityEngine.Object.FindFirstObjectByType<PanelRegistry>(
                        FindObjectsInactive.Include);
                if (registry == null)
                    throw new InvalidOperationException(
                        "PanelRegistry is missing from Bootstrap.");

                NorscopeSettingsService[] services =
                    UnityEngine.Object.FindObjectsByType<NorscopeSettingsService>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None);
                if (services.Length > 1)
                    throw new InvalidOperationException(
                        "Duplicate NorscopeSettingsService components already exist.");
                NorscopeSettingsService service = services.Length == 1
                    ? services[0]
                    : registry.gameObject.AddComponent<NorscopeSettingsService>();
                service.Configure(configuration);
                EditorUtility.SetDirty(service);

                SerializedObject registrySo = new SerializedObject(registry);
                SerializedProperty entries = registrySo.FindProperty("entries");
                int settingsEntryCount = 0;
                for (int i = 0; i < entries.arraySize; i++)
                {
                    SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                    if (entry.FindPropertyRelative("panelType").enumValueIndex !=
                        (int)PanelType.Settings)
                        continue;

                    settingsEntryCount++;
                    if (entry.FindPropertyRelative("windowId").enumValueIndex !=
                            (int)SpatialWindowId.Settings ||
                        entry.FindPropertyRelative("behaviour").enumValueIndex !=
                            (int)PanelBehaviourType.WorldLocked)
                    {
                        throw new InvalidOperationException(
                            "Existing Settings registry identifiers/behaviour changed.");
                    }
                    entry.FindPropertyRelative("prefab").objectReferenceValue =
                        settingsPrefab;
                }

                if (settingsEntryCount != 1)
                    throw new InvalidOperationException(
                        "Expected exactly one existing Settings registry entry; found " +
                        settingsEntryCount + ".");

                registrySo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(registry);
                if (!EditorSceneManager.SaveScene(scene))
                    throw new IOException("Bootstrap scene could not be saved.");
            }
            finally
            {
                if (previousSetup != null && previousSetup.Length > 0)
                    EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
            }
        }

        private static bool ValidateApplied(out string report)
        {
            var checks = new List<string>();
            bool ok = true;
            Action<bool, string> check = (condition, label) =>
            {
                checks.Add((condition ? "PASS  " : "FAIL  ") + label);
                if (!condition) ok = false;
            };

            check(
                !EditorUtility.scriptCompilationFailed,
                "Unity compiles with zero C# errors");

            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(SettingsPanelPath);
            check(prefab != null, "SettingsPanel.prefab exists");
            if (prefab != null)
            {
                string[] hierarchy =
                {
                    "Background",
                    "Border",
                    "FigmaContent",
                    "FigmaContent/SettingsView",
                    "FigmaContent/SettingsView/TopBar",
                    "FigmaContent/SettingsView/Body/Sidebar/LanguageButton",
                    "FigmaContent/SettingsView/Body/Sidebar/AudioButton",
                    "FigmaContent/SettingsView/Body/Sidebar/AboutButton",
                    "FigmaContent/SettingsView/Body/Detail/LanguageQuickView",
                    "FigmaContent/SettingsView/Body/Detail/AudioView",
                    "FigmaContent/SettingsView/Body/Detail/AboutView",
                    "FigmaContent/LanguageView",
                    "FigmaContent/LanguageView/TopBar/InternalBackButton",
                    "TitleBar/GrabRegion",
                    "TitleBar/CloseButton",
                    "ResizeHandles/BottomRight"
                };
                bool hierarchyValid = true;
                for (int i = 0; i < hierarchy.Length; i++)
                    hierarchyValid &= prefab.transform.Find(hierarchy[i]) != null;
                RectTransform content =
                    prefab.transform.Find("FigmaContent") as RectTransform;
                hierarchyValid &= content != null &&
                                  Approximately(content.rect.size, new Vector2(880f, 580f));
                check(
                    hierarchyValid,
                    "SettingsPanel contains the reported authored 880 x 580 hierarchy");

                check(
                    prefab.GetComponent<NorscopePanelRoot>() != null &&
                    prefab.GetComponent<SpatialWindowBindings>() != null &&
                    prefab.GetComponent<SettingsPanel>() != null &&
                    prefab.GetComponent<SettingsViewNavigator>() != null,
                    "Settings panel runtime and SpatialWindow bindings are present");

                SpatialWindowBindings bindings =
                    prefab.GetComponent<SpatialWindowBindings>();
                check(
                    bindings != null &&
                    bindings.GrabRegion != null &&
                    bindings.ResizeHandle != null &&
                    bindings.CloseButton != null,
                    "Close, grab and resize references are assigned");

                PanelNavigationBinding navigation =
                    prefab.GetComponent<PanelNavigationBinding>();
                check(
                    navigation != null &&
                    navigation.PanelType == PanelType.Settings &&
                    navigation.FallbackParent == PanelType.Home &&
                    navigation.BackButton != null,
                    "External Back returns to Home through existing navigation");

                SettingsViewNavigator navigator =
                    prefab.GetComponent<SettingsViewNavigator>();
                SerializedObject navSo =
                    navigator != null ? new SerializedObject(navigator) : null;
                check(
                    navSo != null &&
                    Assigned(navSo, "settingsView") &&
                    Assigned(navSo, "languageView") &&
                    Assigned(navSo, "languageQuickView") &&
                    Assigned(navSo, "audioView") &&
                    Assigned(navSo, "aboutView") &&
                    Assigned(navSo, "languageButton") &&
                    Assigned(navSo, "languageQuickButton") &&
                    Assigned(navSo, "audioButton") &&
                    Assigned(navSo, "aboutButton") &&
                    Assigned(navSo, "languageInternalBackButton"),
                    "Language, Audio, About and internal Language Back are wired");

                SettingsPanel panel = prefab.GetComponent<SettingsPanel>();
                SerializedObject panelSo =
                    panel != null ? new SerializedObject(panel) : null;
                bool deferredValid = panelSo != null;
                if (deferredValid)
                {
                    SerializedProperty controls =
                        panelSo.FindProperty("deferredControls");
                    deferredValid = controls != null && controls.arraySize >= 6;
                    for (int i = 0;
                         deferredValid && i < controls.arraySize;
                         i++)
                    {
                        Selectable selectable =
                            controls.GetArrayElementAtIndex(i).objectReferenceValue
                                as Selectable;
                        deferredValid &= selectable != null &&
                                         !selectable.interactable;
                        if (selectable is Button button)
                            deferredValid &=
                                button.onClick.GetPersistentEventCount() == 0;
                    }
                }
                check(
                    deferredValid,
                    "Deferred controls are visibly disabled and have no persistent value listeners");

                check(
                    panelSo != null &&
                    Assigned(panelSo, "applicationValue") &&
                    Assigned(panelSo, "versionValue") &&
                    Assigned(panelSo, "buildValue") &&
                    Assigned(panelSo, "platformValue") &&
                    Assigned(panelSo, "unityValue") &&
                    Assigned(panelSo, "companyValue") &&
                    Assigned(panelSo, "copyrightValue"),
                    "About metadata bindings are assigned");

                string runtimeSource = ReadRuntimeSettingsSource();
                check(
                    !runtimeSource.Contains("new GameObject") &&
                    !runtimeSource.Contains("Instantiate(") &&
                    !runtimeSource.Contains("AddComponent<") &&
                    !runtimeSource.Contains("RectTransform"),
                    "Settings runtime performs no UI hierarchy construction or layout mutation");
            }

            ValidateBootstrap(check);
            ValidatePersistence(check);
            ValidateProtectedFiles(check);

            check(
                File.Exists(
                    "Assets/Norscope/UI/Panels/_Backups/SettingsPhase2A_20260730_162703/Bootstrap.unity") ||
                HasBaselineBackup(),
                "Restore Baseline remains available");

            bool noDeferredImplementations =
                !Directory.Exists("Assets/Norscope/Runtime/Localization") &&
                !Directory.Exists("Assets/Norscope/Runtime/Voice") &&
                !File.Exists("Assets/Norscope/Audio/NorscopeAudioMixer.mixer") &&
                !ReadRuntimeSettingsSource().Contains("AudioMixer");
            check(
                noDeferredImplementations,
                "No localization, AudioMixer, TTS, microphone or text-scaling implementation introduced");

            string packageManifest = ReadText("Packages/manifest.json");
            check(
                !packageManifest.Contains("mrtk", StringComparison.OrdinalIgnoreCase),
                "No MRTK dependency introduced");

            report = string.Join("\n", checks);
            return ok;
        }

        private static void ValidateBootstrap(Action<bool, string> check)
        {
            SceneSetup[] previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                Scene scene = EditorSceneManager.OpenScene(
                    BootstrapPath, OpenSceneMode.Single);
                NorscopeSettingsService[] services =
                    UnityEngine.Object.FindObjectsByType<NorscopeSettingsService>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None);
                check(
                    services.Length == 1,
                    "Bootstrap contains exactly one NorscopeSettingsService");
                check(
                    services.Length == 1 &&
                    new SerializedObject(services[0])
                        .FindProperty("configuration").objectReferenceValue != null,
                    "Settings service has its configuration assigned");

                PanelRegistry registry =
                    UnityEngine.Object.FindFirstObjectByType<PanelRegistry>(
                        FindObjectsInactive.Include);
                int count = 0;
                bool referenceValid = false;
                bool identityValid = false;
                if (registry != null)
                {
                    SerializedObject so = new SerializedObject(registry);
                    SerializedProperty entries = so.FindProperty("entries");
                    for (int i = 0; i < entries.arraySize; i++)
                    {
                        SerializedProperty entry =
                            entries.GetArrayElementAtIndex(i);
                        if (entry.FindPropertyRelative("panelType").enumValueIndex !=
                            (int)PanelType.Settings)
                            continue;
                        count++;
                        referenceValid =
                            entry.FindPropertyRelative("prefab").objectReferenceValue ==
                            AssetDatabase.LoadAssetAtPath<GameObject>(SettingsPanelPath);
                        identityValid =
                            entry.FindPropertyRelative("windowId").enumValueIndex ==
                            (int)SpatialWindowId.Settings &&
                            entry.FindPropertyRelative("behaviour").enumValueIndex ==
                            (int)PanelBehaviourType.WorldLocked;
                    }
                }

                check(
                    count == 1 && referenceValid,
                    "Existing Settings registry entry references SettingsPanel.prefab");
                check(
                    count == 1 && identityValid,
                    "Settings is one WorldLocked SpatialWindow using the existing IDs");

                string home = ReadText(HomePanelPath);
                check(
                    Regex.IsMatch(home, @"targetPanel:\s*6(?:\r?\n|\s)"),
                    "Home Settings card still opens PanelType.Settings");
                string navigationSource = ReadText(
                    "Assets/Norscope/Runtime/Navigation/PanelNavigationService.cs");
                check(
                    navigationSource.Contains(
                        "failed; current panel remains visible.") &&
                    navigationSource.IndexOf(
                        "panelRegistry.Open(child)",
                        StringComparison.Ordinal) <
                    navigationSource.IndexOf(
                        "HideAllPrimaryPanelsExcept(child)",
                        StringComparison.Ordinal),
                    "Home remains visible if Settings fails to open");
                check(
                    ReadText(
                        "Assets/Norscope/Runtime/Windows/SpatialWindowManager.cs")
                    .Contains("Dictionary<SpatialWindowId, SpatialWindow>"),
                    "Existing manager retains one window per SpatialWindowId");
            }
            finally
            {
                if (previous != null && previous.Length > 0)
                    EditorSceneManager.RestoreSceneManagerSetup(previous);
            }
        }

        private static void ValidatePersistence(Action<bool, string> check)
        {
            string folder = "Temp/SettingsPhase2AValidation";
            string path = folder + "/norscope-settings.json";
            try
            {
                Directory.CreateDirectory(folder);
                if (File.Exists(path))
                    File.Delete(path);
                NorscopeSettingsData created =
                    NorscopeSettingsPersistence.LoadOrCreate(path);
                check(
                    File.Exists(path) &&
                    created.schemaVersion == 1 &&
                    created.language == NorscopeLanguage.English &&
                    created.masterVolume == 100 &&
                    created.alertSoundsEnabled &&
                    created.textSize == NorscopeTextSize.Medium,
                    "Settings JSON is created with schema 1 and approved defaults");

                File.WriteAllText(path, "{ malformed", Encoding.UTF8);
                NorscopeSettingsData recovered =
                    NorscopeSettingsPersistence.LoadOrCreate(path);
                check(
                    recovered.schemaVersion == 1 &&
                    recovered.language == NorscopeLanguage.English &&
                    recovered.masterVolume == 100 &&
                    recovered.alertSoundsEnabled &&
                    recovered.textSize == NorscopeTextSize.Medium &&
                    File.Exists(path),
                    "Missing or malformed settings JSON recovers safely");
            }
            catch (Exception exception)
            {
                check(false, "Settings JSON validation failed: " + exception.Message);
            }
        }

        private static void ValidateProtectedFiles(Action<bool, string> check)
        {
            var blockers = new List<string>();
            Phase2ABaseline baseline = LoadBaseline(blockers);
            bool unchanged = baseline != null && baseline.entries != null;
            if (unchanged)
            {
                for (int i = 0; i < baseline.entries.Count; i++)
                {
                    BaselineEntry entry = baseline.entries[i];
                    if (entry.path == BootstrapPath)
                        continue;
                    bool exists = File.Exists(entry.path);
                    unchanged &= exists == entry.existed;
                    if (exists && entry.existed)
                        unchanged &= string.Equals(
                            entry.sha256,
                            ComputeSha256(entry.path),
                            StringComparison.OrdinalIgnoreCase);
                }
            }
            check(
                unchanged,
                "No protected prefab or runtime architecture file changed");
        }

        private static bool Assigned(SerializedObject value, string property)
        {
            SerializedProperty found = value.FindProperty(property);
            return found != null && found.objectReferenceValue != null;
        }

        private static string ReadRuntimeSettingsSource()
        {
            var result = new StringBuilder();
            for (int i = 0; i < PlannedRuntimeScripts.Length; i++)
                result.AppendLine(ReadText(PlannedRuntimeScripts[i]));
            return result.ToString();
        }

        private static bool HasBaselineBackup()
        {
            var blockers = new List<string>();
            Phase2ABaseline baseline = LoadBaseline(blockers);
            return baseline != null &&
                   File.Exists(baseline.backupFolder + "/Bootstrap.unity");
        }

        private static bool Approximately(Vector2 left, Vector2 right)
        {
            return Mathf.Abs(left.x - right.x) < 0.1f &&
                   Mathf.Abs(left.y - right.y) < 0.1f;
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;
            for (int i = 0; i < root.transform.childCount; i++)
                SetLayerRecursively(root.transform.GetChild(i).gameObject, layer);
        }

        private static void Stretch(RectTransform rect, float inset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        private static void SetTopLeft(
            RectTransform rect,
            Vector2 position,
            Vector2 size)
        {
            rect.anchorMin = Vector2.up;
            rect.anchorMax = Vector2.up;
            rect.pivot = Vector2.up;
            rect.anchoredPosition = new Vector2(position.x, -position.y);
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        private static void SetTopRight(
            RectTransform rect,
            Vector2 inset,
            Vector2 size)
        {
            rect.anchorMin = Vector2.one;
            rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(-inset.x, -inset.y);
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        private static void SetBottomLeft(
            RectTransform rect,
            Vector2 inset,
            Vector2 size)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = inset;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        private static void SetBottomRight(
            RectTransform rect,
            Vector2 inset,
            Vector2 size)
        {
            rect.anchorMin = Vector2.right;
            rect.anchorMax = Vector2.right;
            rect.pivot = Vector2.right;
            rect.anchoredPosition = new Vector2(-inset.x, inset.y);
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        private static void Center(RectTransform rect, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        private static Color Hex(string value)
        {
            if (ColorUtility.TryParseHtmlString("#" + value, out Color color))
                return color;
            return Color.white;
        }

        private static void InspectRegistryAndNavigation(
            List<string> blockers,
            List<string> warnings)
        {
            string bootstrap = ReadText(BootstrapPath);
            string home = ReadText(HomePanelPath);
            string panelType = ReadText(
                "Assets/Norscope/Runtime/Navigation/PanelType.cs");
            string windowId = ReadText(
                "Assets/Norscope/Runtime/Windows/SpatialWindowId.cs");

            if (!Regex.IsMatch(
                    bootstrap,
                    @"-\s+panelType:\s*6\s+windowId:\s*7\s+behaviour:\s*2\s+prefab:\s*\{fileID:\s*0\}",
                    RegexOptions.Multiline))
            {
                blockers.Add(
                    "Bootstrap does not contain the expected null WorldLocked Settings " +
                    "registry entry (PanelType 6 / SpatialWindowId 7).");
            }

            if (!Regex.IsMatch(
                    home,
                    @"targetPanel:\s*6(?:\r?\n|\s)",
                    RegexOptions.Multiline))
            {
                blockers.Add(
                    "HomePanel does not contain a card targeting PanelType.Settings (6).");
            }

            if (!panelType.Contains("Settings = 6"))
            {
                blockers.Add("PanelType.Settings is missing or its serialized value changed.");
            }

            if (!windowId.Contains("Settings = 7"))
            {
                blockers.Add("SpatialWindowId.Settings is missing or its serialized value changed.");
            }

            if (ReadText(
                    "Assets/Norscope/Editor/Phase2DeployConfigurator.cs")
                .Contains("PlayerSettings.bundleVersion = \"0.3.2\""))
            {
                warnings.Add(
                    "Phase2DeployConfigurator contains the stale 0.3.2 assignment. " +
                    "It is outside Settings 2A and will not be invoked.");
            }
        }

        private static Phase2ABaseline LoadBaseline(List<string> blockers)
        {
            if (!File.Exists(BaselinePath))
            {
                blockers.Add(
                    "Fresh Phase 2A baseline missing. Run 1) Capture Settings Baseline.");
                return null;
            }

            try
            {
                Phase2ABaseline baseline =
                    JsonUtility.FromJson<Phase2ABaseline>(ReadText(BaselinePath));
                if (baseline == null || baseline.schemaVersion != 1)
                {
                    blockers.Add("Phase 2A baseline is unreadable or has an unsupported schema.");
                    return null;
                }

                if (baseline.settingsPanelExisted ||
                    baseline.settingsConfigurationExisted)
                {
                    blockers.Add(
                        "Baseline was captured after a Settings panel/configuration " +
                        "already existed. Capture from the approved pre-Phase-2A state.");
                }

                string backup = baseline.backupFolder + "/Bootstrap.unity";
                if (string.IsNullOrEmpty(baseline.backupFolder) || !File.Exists(backup))
                {
                    blockers.Add("Baseline Bootstrap backup is missing.");
                }

                return baseline;
            }
            catch (Exception exception)
            {
                blockers.Add(
                    "Phase 2A baseline could not be parsed: " + exception.Message);
                return null;
            }
        }

        private static void ValidateFreshBaseline(
            Phase2ABaseline baseline,
            List<string> blockers)
        {
            if (baseline.entries == null)
            {
                blockers.Add("Baseline contains no protected-file entries.");
                return;
            }

            for (int i = 0; i < baseline.entries.Count; i++)
            {
                BaselineEntry entry = baseline.entries[i];
                bool exists = File.Exists(entry.path);
                if (exists != entry.existed)
                {
                    blockers.Add("Protected file existence changed: " + entry.path);
                    continue;
                }

                if (exists && !string.Equals(
                        ComputeSha256(entry.path),
                        entry.sha256,
                        StringComparison.OrdinalIgnoreCase))
                {
                    blockers.Add(
                        "Protected file changed after baseline capture: " + entry.path);
                }
            }
        }

        private static BaselineEntry CaptureEntry(string path)
        {
            bool exists = File.Exists(path);
            return new BaselineEntry
            {
                path = path,
                existed = exists,
                length = exists ? new FileInfo(path).Length : 0L,
                sha256 = exists ? ComputeSha256(path) : string.Empty
            };
        }

        private static string BuildBackupManifest(Phase2ABaseline baseline)
        {
            var text = new StringBuilder();
            text.AppendLine("NORSCOPE Settings Phase 2A baseline");
            text.AppendLine("SchemaVersion=" + baseline.schemaVersion);
            text.AppendLine("CapturedUtc=" + baseline.capturedUtc);
            text.AppendLine("BootstrapSource=" + BootstrapPath);
            text.AppendLine("SettingsPanelExisted=" + baseline.settingsPanelExisted);
            text.AppendLine(
                "SettingsConfigurationExisted=" +
                baseline.settingsConfigurationExisted);
            for (int i = 0; i < baseline.entries.Count; i++)
            {
                BaselineEntry entry = baseline.entries[i];
                text.AppendLine(
                    entry.path + "|" + entry.existed + "|" +
                    entry.length + "|" + entry.sha256);
            }
            return text.ToString();
        }

        private static void RequireFile(string path, List<string> blockers)
        {
            if (!File.Exists(path))
            {
                blockers.Add("Required existing asset is missing: " + path);
            }
        }

        private static string ReadText(string path)
        {
            return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
        }

        private static string ComputeSha256(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                byte[] hash = sha.ComputeHash(stream);
                var value = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                {
                    value.Append(hash[i].ToString("x2"));
                }
                return value.ToString();
            }
        }
    }
}
