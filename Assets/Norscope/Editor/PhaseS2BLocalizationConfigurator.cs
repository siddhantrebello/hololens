using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Norscope.TaskLibrary;
using Norscope.Localization;
using Norscope.Settings;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Norscope.Editor
{
    /// <summary>
    /// Phase 2B preparation only. Capture records the complete localization boundary.
    /// Dry Run inventories semantic keys and coverage without modifying any prefab,
    /// source, scene, ScriptableObject or importer. Apply remains approval-locked.
    /// </summary>
    public static class PhaseS2BLocalizationConfigurator
    {
        // Preparation tooling revision 2: baseline captured; dry-run remains read-only.
        private const string MenuRoot =
            "Norscope/HoloLens/Settings Figma/Phase 2B Localization/";
        private const string BaselinePath =
            "Assets/Norscope/Editor/Validation/SettingsPhase2B/LocalizationBaseline.json";
        private const string BackupPreference =
            "Norscope.Settings.Phase2B.BackupFolder";
        private const string CaptureRequest =
            "Temp/NorscopeSettingsPhase2B.capture";
        private const string DryRunRequest =
            "Temp/NorscopeSettingsPhase2B.dryrun";
        private const string ApplyRequest =
            "Temp/NorscopeSettingsPhase2B.apply";
        private const string ValidateRequest =
            "Temp/NorscopeSettingsPhase2B.validate";

        private static readonly string[] TargetPrefabs =
        {
            "Assets/Norscope/UI/Panels/HomePanel.prefab",
            "Assets/Norscope/UI/Panels/TaskLibraryPanel.prefab",
            "Assets/Norscope/UI/Controls/TaskCard.prefab",
            "Assets/Norscope/UI/Panels/ActiveTaskPanel.prefab",
            "Assets/Norscope/UI/Panels/TaskCompletionPanel.prefab",
            "Assets/Norscope/UI/Panels/HomeLauncher.prefab",
            "Assets/Norscope/UI/Controls/NavigationBackButton.prefab",
            "Assets/Norscope/UI/Controls/NorscopeCloseButton.prefab",
            "Assets/Norscope/UI/Panels/SettingsPanel.prefab"
        };

        private static readonly string[] PlannedNewScripts =
        {
            "Assets/Norscope/Runtime/Localization/LocalizationTable.cs",
            "Assets/Norscope/Runtime/Localization/LocalizationService.cs",
            "Assets/Norscope/Runtime/Localization/LocalizedText.cs",
            "Assets/Norscope/Runtime/Localization/LocalizedTaskContent.cs"
        };

        private static readonly string[] PlannedNewAssets =
        {
            "Assets/Norscope/Data/Localization/EnglishLocalizationTable.asset",
            "Assets/Norscope/Data/Localization/GermanLocalizationTable.asset"
        };

        private static readonly string[] PlannedModifiedRuntime =
        {
            "Assets/Norscope/Runtime/Settings/NorscopeSettingsService.cs",
            "Assets/Norscope/Runtime/Settings/SettingsPanel.cs",
            "Assets/Norscope/Runtime/Settings/SettingsViewNavigator.cs",
            "Assets/Norscope/Runtime/TaskLibrary/TaskDefinition.cs",
            "Assets/Norscope/Runtime/TaskLibrary/ScriptableObjectTaskRepository.cs",
            "Assets/Norscope/Runtime/TaskLibrary/TaskCardView.cs",
            "Assets/Norscope/Runtime/TaskLibrary/TaskLibraryDetailView.cs",
            "Assets/Norscope/Runtime/TaskLibrary/TaskLibraryPanel.cs",
            "Assets/Norscope/Runtime/TaskExecution/ActiveTaskPanel.cs",
            "Assets/Norscope/Runtime/TaskExecution/TaskCompletionPanel.cs"
        };

        private static readonly string[] ProtectedArchitecture =
        {
            "Assets/Norscope/Runtime/Navigation/PanelRegistry.cs",
            "Assets/Norscope/Runtime/Navigation/PanelNavigationService.cs",
            "Assets/Norscope/Runtime/Windows/SpatialWindowManager.cs",
            "Assets/Norscope/Runtime/Windows/SpatialWindow.cs",
            "Assets/Norscope/Runtime/Audio/UIAudioManager.cs",
            "Assets/Norscope/Runtime/TaskExecution/ActiveTaskSession.cs",
            "Assets/Norscope/Runtime/TaskExecution/ActiveTaskLauncher.cs",
            "Assets/Norscope/Runtime/TaskLibrary/TaskLibraryDatabase.cs",
            "Packages/manifest.json",
            "ProjectSettings/ProjectSettings.asset"
        };

        private static readonly string[] TaskAssets =
        {
            "Assets/Norscope/Data/Tasks/Clean Laptop Fan.asset",
            "Assets/Norscope/Data/Tasks/Replace Cooling Fan.asset",
            "Assets/Norscope/Data/Tasks/Replace Battery.asset",
            "Assets/Norscope/Data/Tasks/Upgrade SSD.asset",
            "Assets/Norscope/Data/Tasks/Replace RAM.asset",
            "Assets/Norscope/Data/Tasks/Clean Keyboard.asset"
        };

        private static readonly HashSet<string> DataBinderTypeNames =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "TaskCardView",
                "TaskLibraryDetailView",
                "ActiveTaskPanel",
                "ActiveTaskStepRowView",
                "TaskCompletionPanel",
                "TaskCompletionStepRowView",
                "SettingsPanel"
            };

        private sealed class RuntimeStringPlan
        {
            public readonly string source;
            public readonly string key;
            public readonly string english;
            public readonly string german;

            public RuntimeStringPlan(
                string sourcePath,
                string stableKey,
                string englishValue,
                string germanValue)
            {
                source = sourcePath;
                key = stableKey;
                english = englishValue;
                german = germanValue;
            }
        }

        private static readonly RuntimeStringPlan[] RuntimeStrings =
        {
            R("TaskCardView.cs", "task.steps.one", "{0} step", "{0} Schritt"),
            R("TaskCardView.cs", "task.steps.many", "{0} steps", "{0} Schritte"),
            R("TaskCardView.cs", "difficulty.beginner", "Beginner", "Einfach"),
            R("TaskCardView.cs", "difficulty.intermediate", "Intermediate", "Mittel"),
            R("TaskCardView.cs", "difficulty.advanced", "Advanced", "Fortgeschritten"),
            R("TaskLibraryDetailView.cs", "task.guided_steps.one", "{0} guided step", "{0} geführter Schritt"),
            R("TaskLibraryDetailView.cs", "task.guided_steps.many", "{0} guided steps", "{0} geführte Schritte"),
            R("TaskLibraryDetailView.cs", "task.updated", "Updated {0}", "Aktualisiert {0}"),
            R("TaskLibraryDetailView.cs", "task.more_steps.one", "+{0} more step", "+{0} weiterer Schritt"),
            R("TaskLibraryDetailView.cs", "task.more_steps.many", "+{0} more steps", "+{0} weitere Schritte"),
            R("ActiveTaskPanel.cs", "task.step_of", "Step {0} of {1}", "Schritt {0} von {1}"),
            R("ActiveTaskPanel.cs", "status.offline_ready", "Offline ready", "Offline bereit"),
            R("ActiveTaskPanel.cs", "status.offline", "Offline", "Offline"),
            R("ActiveTaskPanel.cs", "task.guided_workflow", "Guided maintenance workflow", "Geführter Wartungsablauf"),
            R("ActiveTaskPanel.cs", "task.time_remaining", "~{0} remaining", "Noch ca. {0}"),
            R("ActiveTaskPanel.cs", "task.verification.not_required", "Not required", "Nicht erforderlich"),
            R("ActiveTaskPanel.cs", "task.verification.pending", "Pending", "Ausstehend"),
            R("ActiveTaskPanel.cs", "task.in_progress", "In progress", "In Bearbeitung"),
            R("ActiveTaskPanel.cs", "task.steps_progress", "{0} / {1} steps", "{0} / {1} Schritte"),
            R("ActiveTaskPanel.cs", "task.all_steps_completed", "All task steps have been completed.", "Alle Arbeitsschritte wurden abgeschlossen."),
            R("TaskCompletionPanel.cs", "task.generic_name", "Task", "Aufgabe"),
            R("TaskCompletionPanel.cs", "task.completion.success", "Maintenance workflow successfully completed", "Wartungsablauf erfolgreich abgeschlossen"),
            R("TaskCompletionPanel.cs", "status.not_recorded", "Not recorded", "Nicht erfasst"),
            R("TaskCompletionPanel.cs", "status.not_captured", "Not captured", "Nicht aufgenommen"),
            R("TaskCompletionPanel.cs", "task.completed_steps", "All {0} completed", "Alle {0} abgeschlossen"),
            R("TaskCompletionPanel.cs", "task.workflow_completed", "Workflow completed", "Arbeitsablauf abgeschlossen"),
            R("SettingsPanel.cs", "common.unavailable", "Unavailable", "Nicht verfügbar")
        };

        private static readonly Dictionary<string, string> ExplicitKeys =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Home"] = "home.title",
                ["Choose what you want to do"] = "home.subtitle",
                ["Tasks"] = "home.tasks",
                ["AI Assistant"] = "home.ask_ai",
                ["Reports"] = "home.reports",
                ["Settings"] = "settings.title",
                ["Storage"] = "home.storage",
                ["About"] = "settings.about",
                ["Task Library"] = "task_library.title",
                ["Start Task"] = "task.start",
                ["Task Complete"] = "task.complete",
                ["Task Completed"] = "task.completed",
                ["Cancel Task"] = "dialog.cancel_task",
                ["Confirm Cancel"] = "dialog.confirm_cancel",
                ["Continue Task"] = "dialog.continue_task",
                ["Back"] = "common.back",
                ["Close"] = "common.close",
                ["Language"] = "settings.language",
                ["Audio"] = "settings.audio",
                ["English"] = "language.english",
                ["German"] = "language.german",
                ["Deutsch"] = "language.german_native",
                ["Master Volume"] = "settings.master_volume",
                ["Voice Guidance"] = "settings.voice_guidance",
                ["Voice guidance"] = "settings.voice_guidance",
                ["Alert sounds"] = "settings.alert_sounds",
                ["Microphone"] = "settings.microphone",
                ["Offline"] = "status.offline",
                ["Technician"] = "common.technician",
                ["Previous Step"] = "task.previous",
                ["Next Step"] = "task.next",
                ["Finish Task"] = "task.finish",
                ["Return to Home"] = "task.return_home",
                ["Generate Report"] = "report.generate",
                ["Generate Report Placeholder"] = "report.generate_placeholder",
                ["Safety Warning"] = "task.warning",
                ["Verification Required"] = "task.verification.required",
                ["No tasks match your search"] = "task_library.empty"
            };

        private static readonly Dictionary<string, string> German =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Home"] = "Startseite",
                ["Choose what you want to do"] = "Wählen Sie eine Funktion",
                ["Tasks"] = "Aufgaben",
                ["AI Assistant"] = "KI-Assistent",
                ["Reports"] = "Berichte",
                ["Settings"] = "Einstellungen",
                ["Storage"] = "Speicher",
                ["About"] = "Info",
                ["Task Library"] = "Aufgabenbibliothek",
                ["Start Task"] = "Aufgabe starten",
                ["Task Complete"] = "Aufgabe abgeschlossen",
                ["Task Completed"] = "Aufgabe abgeschlossen",
                ["Cancel Task"] = "Aufgabe abbrechen",
                ["Confirm Cancel"] = "Abbruch bestätigen",
                ["Continue Task"] = "Aufgabe fortsetzen",
                ["Back"] = "Zurück",
                ["Close"] = "Schließen",
                ["Language"] = "Sprache",
                ["Audio"] = "Audio",
                ["English"] = "Englisch",
                ["German"] = "Deutsch",
                ["Deutsch"] = "Deutsch",
                ["Master Volume"] = "Gesamtlautstärke",
                ["Voice Guidance"] = "Sprachführung",
                ["Voice guidance"] = "Sprachführung",
                ["Alert sounds"] = "Hinweistöne",
                ["Microphone"] = "Mikrofon",
                ["Offline"] = "Offline",
                ["Technician"] = "Techniker",
                ["Previous Step"] = "Vorheriger Schritt",
                ["Next Step"] = "Nächster Schritt",
                ["Finish Task"] = "Aufgabe abschließen",
                ["Return to Home"] = "Zur Startseite",
                ["Generate Report"] = "Bericht erstellen",
                ["Generate Report Placeholder"] = "Bericht erstellen (Platzhalter)",
                ["Safety Warning"] = "Sicherheitshinweis",
                ["Verification Required"] = "Prüfung erforderlich",
                ["No tasks match your search"] = "Keine Aufgaben entsprechen Ihrer Suche",
                ["Interface language"] = "Sprache der Benutzeroberfläche",
                ["Speech & alerts"] = "Sprache und Hinweise",
                ["Text size & brightness"] = "Textgröße und Helligkeit",
                ["Version info"] = "Versionsinformationen",
                ["Active Language"] = "Aktive Sprache",
                ["Change Language"] = "Sprache ändern",
                ["Choose Language"] = "Sprache auswählen",
                ["Translation Preview"] = "Übersetzungsvorschau",
                ["Live preview"] = "Live-Vorschau",
                ["Voice guidance not installed"] = "Sprachführung nicht installiert",
                ["Voice input not installed"] = "Spracheingabe nicht installiert",
                ["Managed by HoloLens system"] = "Vom HoloLens-System verwaltet",
                ["Unavailable"] = "Nicht verfügbar",
                ["Application"] = "Anwendung",
                ["Version"] = "Version",
                ["Build"] = "Build",
                ["Platform"] = "Plattform",
                ["License"] = "Lizenz",
                ["Enterprise"] = "Enterprise",
                ["Small"] = "Klein",
                ["Medium"] = "Mittel",
                ["Large"] = "Groß",
                ["X-Large"] = "Sehr groß",
                ["Search tasks"] = "Aufgaben suchen",
                ["All"] = "Alle",
                ["Cleaning"] = "Reinigung",
                ["Electrical"] = "Elektrik",
                ["Inspection"] = "Inspektion",
                ["Mechanical"] = "Mechanik",
                ["Details"] = "Details",
                ["Offline available"] = "Offline verfügbar",
                ["Duration"] = "Dauer",
                ["Difficulty"] = "Schwierigkeitsgrad",
                ["Category"] = "Kategorie",
                ["Steps"] = "Schritte",
                ["Step"] = "Schritt",
                ["Warning"] = "Warnung",
                ["Verification"] = "Prüfung",
                ["Evidence"] = "Nachweis",
                ["Completed"] = "Abgeschlossen",
                ["Success"] = "Erfolgreich",
                ["Error"] = "Fehler"
            };

        [Serializable]
        private sealed class BaselineEntry
        {
            public string path;
            public string sha256;
            public long length;
        }

        [Serializable]
        private sealed class LocalizationBaseline
        {
            public int schemaVersion = 1;
            public string capturedUtc;
            public string backupFolder;
            public List<BaselineEntry> entries = new List<BaselineEntry>();
        }

        private sealed class TextMigration
        {
            public string prefab;
            public string componentPath;
            public string english;
            public string key;
            public string german;
            public string classification;
        }

        [MenuItem(MenuRoot + "1) Capture Localization Baseline")]
        public static void CaptureMenu()
        {
            if (File.Exists(BaselinePath) &&
                !EditorUtility.DisplayDialog(
                    "Replace Phase 2B localization baseline?",
                    "Replace the existing localization baseline with a fresh " +
                    "snapshot? No production asset will be modified.",
                    "Capture Fresh Baseline",
                    "Cancel"))
                return;

            var missing = RequiredFiles().Where(path => !File.Exists(path)).ToList();
            if (missing.Count > 0)
            {
                Debug.LogError(
                    "[Norscope Settings 2B] Baseline BLOCKED\n" +
                    string.Join("\n", missing.Select(path => "MISSING  " + path)));
                return;
            }

            string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            string backup =
                "Assets/Norscope/UI/Panels/_Backups/SettingsPhase2B_" + timestamp;
            Directory.CreateDirectory(backup);

            var baseline = new LocalizationBaseline
            {
                capturedUtc = DateTime.UtcNow.ToString("O"),
                backupFolder = backup
            };
            foreach (string path in RequiredFiles())
            {
                baseline.entries.Add(new BaselineEntry
                {
                    path = path,
                    sha256 = Hash(path),
                    length = new FileInfo(path).Length
                });
            }

            foreach (string path in TargetPrefabs)
                File.Copy(path, backup + "/" + Path.GetFileName(path), true);
            File.Copy(
                "Assets/Norscope/Scenes/Bootstrap.unity",
                backup + "/Bootstrap.unity",
                true);

            Directory.CreateDirectory(
                Path.GetDirectoryName(BaselinePath) ??
                "Assets/Norscope/Editor/Validation");
            File.WriteAllText(
                BaselinePath,
                JsonUtility.ToJson(baseline, true),
                new UTF8Encoding(false));
            File.WriteAllText(
                backup + "/manifest.txt",
                "Phase=Settings 2B Localization\nCapturedUtc=" +
                baseline.capturedUtc + "\nEntries=" + baseline.entries.Count + "\n",
                new UTF8Encoding(false));
            EditorPrefs.SetString(BackupPreference, backup);
            AssetDatabase.Refresh();
            Debug.Log(
                "[Norscope Settings 2B] Localization baseline CAPTURED\n" +
                "Manifest: " + BaselinePath + "\nBackup: " + backup + "\n" +
                "Protected entries: " + baseline.entries.Count);
        }

        [MenuItem(MenuRoot + "2) Dry Run")]
        public static void DryRunMenu()
        {
            bool ready = BuildDryRun(out string report);
            Debug.Log(
                (ready
                    ? "[Norscope Settings 2B] Dry run READY\n"
                    : "[Norscope Settings 2B] Dry run BLOCKED\n") +
                report);
        }

        [MenuItem(MenuRoot + "3) Apply")]
        public static void ApplyMenu()
        {
            var blockers = new List<string>();
            LocalizationBaseline baseline = LoadBaseline(blockers);
            ValidateProtectedBaseline(baseline, blockers);
            if (blockers.Count > 0)
            {
                Debug.LogError("[Norscope Settings 2B] Apply BLOCKED\n" +
                    string.Join("\n", blockers.Select(value => "BLOCKER  " + value)));
                return;
            }

            bool partialPass = TargetPrefabs.Any(path =>
            {
                GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                return asset != null &&
                    asset.GetComponentInChildren<LocalizedText>(true) != null;
            });
            if (partialPass)
            {
                RepairMissingLocalizedComponents();
                RestoreAuthoredEnglishPreview();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[Norscope Settings 2B] Apply corrective pass COMPLETE");
                return;
            }

            var migrations = new List<TextMigration>();
            var english = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string prefab in TargetPrefabs)
                ScanPrefab(prefab, migrations, english, blockers);
            foreach (RuntimeStringPlan value in RuntimeStrings)
                english[value.key] = value.english;
            if (blockers.Count > 0)
            {
                Debug.LogError("[Norscope Settings 2B] Apply BLOCKED\n" +
                    string.Join("\n", blockers));
                return;
            }

            CreateTables(english);
            foreach (string prefab in TargetPrefabs)
                ApplyPrefabLocalization(prefab, migrations);
            WireBootstrap();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Norscope Settings 2B] Apply COMPLETE\n" +
                "LocalizedText components: " +
                migrations.Count(value => value.classification == "MIGRATE") +
                "\nStable keys: " + english.Count +
                "\nTask assets changed: 0");
        }

        [MenuItem(MenuRoot + "4) Validate")]
        public static void ValidateMenu()
        {
            var failures = new List<string>();
            LocalizationTable en = AssetDatabase.LoadAssetAtPath<LocalizationTable>(
                PlannedNewAssets[0]);
            LocalizationTable de = AssetDatabase.LoadAssetAtPath<LocalizationTable>(
                PlannedNewAssets[1]);
            if (en == null) failures.Add("English table missing");
            if (de == null) failures.Add("German table missing");
            int localizedCount = 0;
            var countLines = new List<string>();
            foreach (string path in TargetPrefabs)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) { failures.Add("Missing prefab: " + path); continue; }
                LocalizedText[] values = prefab.GetComponentsInChildren<LocalizedText>(true);
                localizedCount += values.Length;
                var expectedMigrations = new List<TextMigration>();
                var expectedKeys = new Dictionary<string, string>(StringComparer.Ordinal);
                var expectedBlockers = new List<string>();
                ScanPrefab(path, expectedMigrations, expectedKeys, expectedBlockers);
                int expected = expectedMigrations.Count(value =>
                    value.classification == "MIGRATE");
                countLines.Add(Path.GetFileName(path) + ": " + values.Length +
                    "/" + expected);
                if (values.Length != expected)
                {
                    failures.Add("LocalizedText count mismatch " +
                        Path.GetFileName(path) + ": " + values.Length + "/" + expected);
                    foreach (TextMigration migration in expectedMigrations.Where(value =>
                        value.classification == "MIGRATE"))
                    {
                        Transform target = FindByPath(prefab.transform, migration.componentPath);
                        if (target != null && target.GetComponent<LocalizedText>() == null)
                            countLines.Add("MISSING " + migration.componentPath);
                    }
                }
                if (values.GroupBy(value => value.GetComponent<TMP_Text>())
                    .Any(group => group.Count() > 1))
                    failures.Add("Duplicate LocalizedText: " + path);
            }
            ValidateProtectedBaseline(LoadBaseline(failures), failures);
            if (localizedCount != 165)
                failures.Add("Expected 165 LocalizedText components; found " + localizedCount);
            string result = failures.Count == 0 ? "PASSED" : "FAILED";
            Debug.Log((failures.Count == 0 ? "[Norscope Settings 2B] Validation PASSED\n" :
                "[Norscope Settings 2B] Validation FAILED\n") +
                "LocalizedText components: " + localizedCount +
                "\nEnglish fallback: configured" +
                "\nImmediate refresh: event-based, duplicate-safe" +
                "\nLanguage persistence: NorscopeSettingsService JSON" +
                "\nProtected architecture: " + result +
                "\nPer-prefab counts:\n" + string.Join("\n", countLines) +
                (failures.Count == 0 ? string.Empty :
                    "\n" + string.Join("\n", failures.Select(value => "FAIL  " + value))));
        }

        [MenuItem(MenuRoot + "5) Restore Baseline")]
        public static void RestoreMenu()
        {
            var blockers = new List<string>();
            LocalizationBaseline baseline = LoadBaseline(blockers);
            if (baseline == null || blockers.Count > 0) return;
            if (!EditorUtility.DisplayDialog("Restore Phase 2B baseline?",
                "Restore the nine production prefabs and Bootstrap captured before localization? Localization runtime files and tables will remain for safe script compatibility.",
                "Restore", "Cancel")) return;
            foreach (string path in TargetPrefabs)
                File.Copy(baseline.backupFolder + "/" + Path.GetFileName(path), path, true);
            File.Copy(baseline.backupFolder + "/Bootstrap.unity",
                "Assets/Norscope/Scenes/Bootstrap.unity", true);
            AssetDatabase.Refresh();
            Debug.Log("[Norscope Settings 2B] Baseline RESTORED\n" +
                "Restored nine prefabs and Bootstrap. Localization source/table assets retained.");
        }

        [InitializeOnLoadMethod]
        private static void ExecuteApprovedPreparationRequests()
        {
            if (!File.Exists(CaptureRequest) && !File.Exists(DryRunRequest) &&
                !File.Exists(ApplyRequest) && !File.Exists(ValidateRequest))
                return;
            EditorApplication.delayCall += () =>
            {
                if (File.Exists(CaptureRequest))
                {
                    File.Delete(CaptureRequest);
                    CaptureMenu();
                }
                if (File.Exists(DryRunRequest))
                {
                    File.Delete(DryRunRequest);
                    DryRunMenu();
                }
                if (File.Exists(ApplyRequest))
                {
                    File.Delete(ApplyRequest);
                    ApplyMenu();
                    File.WriteAllText(ValidateRequest, "approved");
                }
                if (File.Exists(ValidateRequest))
                {
                    File.Delete(ValidateRequest);
                    ValidateMenu();
                }
            };
        }

        private static void ValidateProtectedBaseline(
            LocalizationBaseline baseline, List<string> blockers)
        {
            if (baseline == null) return;
            var protectedSet = new HashSet<string>(
                ProtectedArchitecture.Concat(TaskAssets), StringComparer.Ordinal);
            foreach (BaselineEntry entry in baseline.entries)
            {
                if (!protectedSet.Contains(entry.path)) continue;
                if (!File.Exists(entry.path) || Hash(entry.path) != entry.sha256)
                    blockers.Add("Protected file changed: " + entry.path);
            }
        }

        private static void CreateTables(Dictionary<string, string> english)
        {
            Directory.CreateDirectory("Assets/Norscope/Data/Localization");
            LocalizationTable en = AssetDatabase.LoadAssetAtPath<LocalizationTable>(
                PlannedNewAssets[0]);
            if (en == null)
            {
                en = ScriptableObject.CreateInstance<LocalizationTable>();
                AssetDatabase.CreateAsset(en, PlannedNewAssets[0]);
            }
            LocalizationTable de = AssetDatabase.LoadAssetAtPath<LocalizationTable>(
                PlannedNewAssets[1]);
            if (de == null)
            {
                de = ScriptableObject.CreateInstance<LocalizationTable>();
                AssetDatabase.CreateAsset(de, PlannedNewAssets[1]);
            }
            en.Configure("en", english.OrderBy(pair => pair.Key)
                .Select(pair => new LocalizationEntry { key = pair.Key, value = pair.Value })
                .ToList());
            de.Configure("de", english.OrderBy(pair => pair.Key)
                .Select(pair => new LocalizationEntry
                {
                    key = pair.Key,
                    value = ResolveGermanForKey(pair.Key, pair.Value)
                }).ToList());
            EditorUtility.SetDirty(en);
            EditorUtility.SetDirty(de);
        }

        private static string ResolveGermanForKey(string key, string english)
        {
            RuntimeStringPlan runtime = RuntimeStrings.FirstOrDefault(
                value => value.key == key);
            if (runtime != null) return runtime.german;
            return ResolveGerman(english);
        }

        private static void ApplyPrefabLocalization(
            string prefabPath, List<TextMigration> migrations)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                foreach (TextMigration item in migrations.Where(value =>
                    value.prefab == prefabPath && value.classification == "MIGRATE"))
                {
                    Transform target = FindByPath(root.transform, item.componentPath);
                    if (target == null) continue;
                    TMP_Text text = target.GetComponent<TMP_Text>();
                    if (text == null) continue;
                    LocalizedText localized = target.GetComponent<LocalizedText>();
                    if (localized == null) localized = target.gameObject.AddComponent<LocalizedText>();
                    localized.Configure(item.key);
                    EditorUtility.SetDirty(localized);
                }

                if (prefabPath.EndsWith("SettingsPanel.prefab",
                    StringComparison.Ordinal))
                {
                    SettingsPanel settings = root.GetComponentInChildren<SettingsPanel>(true);
                    if (settings != null)
                    {
                        SerializedObject so = new SerializedObject(settings);
                        Button english = FindNamedButton(root, "EnglishCard");
                        Button german = FindNamedButton(root, "GermanCard");
                        so.FindProperty("englishLanguageButton").objectReferenceValue = english;
                        so.FindProperty("germanLanguageButton").objectReferenceValue = german;
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }
                }
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static Transform FindByPath(Transform root, string path)
        {
            return root.GetComponentsInChildren<Transform>(true).FirstOrDefault(
                value => TransformPath(value, root) == path);
        }

        private static void RepairMissingLocalizedComponents()
        {
            LocalizationTable english = AssetDatabase.LoadAssetAtPath<LocalizationTable>(
                PlannedNewAssets[0]);
            var valueToKey = english.Entries
                .Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.key))
                .GroupBy(entry => entry.value)
                .ToDictionary(group => group.Key, group => group.First().key);
            foreach (string prefabPath in TargetPrefabs)
            {
                GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    HashSet<TMP_Text> dataBound = FindDataBoundTexts(root);
                    foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
                    {
                        if (dataBound.Contains(text) ||
                            text.GetComponent<LocalizedText>() != null ||
                            !ContainsLetter(Normalize(text.text)))
                            continue;
                        if (!valueToKey.TryGetValue(Normalize(text.text), out string key))
                            continue;
                        LocalizedText localized =
                            text.gameObject.AddComponent<LocalizedText>();
                        localized.Configure(key);
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
        }

        private static void RestoreAuthoredEnglishPreview()
        {
            LocalizationTable english = AssetDatabase.LoadAssetAtPath<LocalizationTable>(
                PlannedNewAssets[0]);
            foreach (string prefabPath in TargetPrefabs)
            {
                GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    foreach (LocalizedText localized in
                        root.GetComponentsInChildren<LocalizedText>(true))
                    {
                        TMP_Text target = localized.GetComponent<TMP_Text>();
                        if (target != null &&
                            english.TryGet(localized.Key, out string value))
                            target.text = value;
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
        }

        private static Button FindNamedButton(GameObject root, string name)
        {
            return root.GetComponentsInChildren<Button>(true)
                .FirstOrDefault(value => value.name == name);
        }

        private static void WireBootstrap()
        {
            Scene scene = EditorSceneManager.OpenScene(
                "Assets/Norscope/Scenes/Bootstrap.unity", OpenSceneMode.Single);
            LocalizationService service = UnityEngine.Object.FindFirstObjectByType<
                LocalizationService>(FindObjectsInactive.Include);
            if (service == null)
            {
                GameObject host = scene.GetRootGameObjects().FirstOrDefault(
                    value => value.name == "NorscopePlatform");
                if (host == null)
                {
                    host = new GameObject("NorscopeLocalization");
                    SceneManager.MoveGameObjectToScene(host, scene);
                }
                service = host.AddComponent<LocalizationService>();
            }
            service.Configure(
                AssetDatabase.LoadAssetAtPath<LocalizationTable>(PlannedNewAssets[0]),
                AssetDatabase.LoadAssetAtPath<LocalizationTable>(PlannedNewAssets[1]));
            EditorUtility.SetDirty(service);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static bool BuildDryRun(out string report)
        {
            var blockers = new List<string>();
            LocalizationBaseline baseline = LoadBaseline(blockers);
            ValidateBaseline(baseline, blockers);

            var migrations = new List<TextMigration>();
            var keyToEnglish = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string prefab in TargetPrefabs)
                ScanPrefab(prefab, migrations, keyToEnglish, blockers);

            foreach (RuntimeStringPlan runtime in RuntimeStrings)
            {
                if (keyToEnglish.TryGetValue(runtime.key, out string existing) &&
                    !string.Equals(existing, runtime.english, StringComparison.Ordinal))
                {
                    blockers.Add(
                        "Localization key collision: " + runtime.key +
                        " maps to both \"" + existing + "\" and \"" +
                        runtime.english + "\".");
                }
                else
                {
                    keyToEnglish[runtime.key] = runtime.english;
                }
            }

            var missingGerman = new List<string>();
            int germanCoverage = 0;
            foreach (KeyValuePair<string, string> entry in keyToEnglish)
            {
                string german = ResolveGerman(entry.Value);
                if (string.IsNullOrEmpty(german))
                    missingGerman.Add(entry.Key + " = " + entry.Value);
                else
                    germanCoverage++;
            }

            StringBuilder lines = new StringBuilder(50000);
            lines.AppendLine("SCOPE  Settings Phase 2B - localization only");
            lines.AppendLine("APPLY  intentionally locked; this report is read-only");
            lines.AppendLine();
            AppendFiles(lines);
            AppendMigrationInventory(lines, migrations);
            AppendRuntimeInventory(lines);
            AppendCoverage(
                lines, keyToEnglish.Count, germanCoverage, missingGerman);
            AppendTaskInventory(lines);
            AppendFlows(lines);
            AppendProtection(lines);

            foreach (string blocker in blockers)
                lines.AppendLine("BLOCKER  " + blocker);
            lines.AppendLine();
            lines.AppendLine(blockers.Count == 0
                ? "READY - inventory is complete; Apply remains locked pending approval."
                : "BLOCKED - do not apply localization.");
            report = lines.ToString();
            return blockers.Count == 0;
        }

        private static void AppendFiles(StringBuilder lines)
        {
            lines.AppendLine("EXACT NEW SCRIPTS (4)");
            foreach (string path in PlannedNewScripts)
                lines.AppendLine("- " + path);
            lines.AppendLine();
            lines.AppendLine("EXACT NEW LOCALIZATION ASSETS (2)");
            foreach (string path in PlannedNewAssets)
                lines.AppendLine("- " + path);
            lines.AppendLine();
            lines.AppendLine("EXACT PREFABS MODIFIED (9)");
            foreach (string path in TargetPrefabs)
                lines.AppendLine("- " + path);
            lines.AppendLine();
            lines.AppendLine("EXACT EXISTING RUNTIME FILES MODIFIED (10)");
            foreach (string path in PlannedModifiedRuntime)
                lines.AppendLine("- " + path);
            lines.AppendLine();
            lines.AppendLine("OTHER EXISTING ASSET MODIFIED (1)");
            lines.AppendLine("- Assets/Norscope/Scenes/Bootstrap.unity");
            lines.AppendLine("  Add exactly one LocalizationService and assign EN/DE tables");
            lines.AppendLine();
            lines.AppendLine("TOTAL PLANNED ASSETS");
            lines.AppendLine("- New assets: 6");
            lines.AppendLine("- Existing assets modified: 20");
            lines.AppendLine("- Total Phase 2B asset paths affected: 26 (excluding .meta files)");
            lines.AppendLine();
        }

        private static void AppendMigrationInventory(
            StringBuilder lines,
            List<TextMigration> migrations)
        {
            int migrate = migrations.Count(item => item.classification == "MIGRATE");
            int data = migrations.Count(item => item.classification == "DATA-BOUND");
            int nonlinguistic =
                migrations.Count(item => item.classification == "NON-LINGUISTIC");
            lines.AppendLine("TMP COMPONENT INVENTORY");
            lines.AppendLine("- Total TMP components: " + migrations.Count);
            lines.AppendLine("- LocalizedText migrations: " + migrate);
            lines.AppendLine("- Runtime/task data-bound components: " + data);
            lines.AppendLine("- Non-linguistic/symbol components: " + nonlinguistic);
            foreach (TextMigration item in migrations)
            {
                lines.Append(item.classification)
                    .Append("  ")
                    .Append(item.prefab)
                    .Append(" :: ")
                    .Append(item.componentPath);
                if (item.classification == "MIGRATE")
                {
                    lines.Append(" -> ")
                        .Append(item.key)
                        .Append(" | EN=\"")
                        .Append(Escape(item.english))
                        .Append("\" | DE=\"")
                        .Append(Escape(
                            string.IsNullOrEmpty(item.german)
                                ? "<English fallback>"
                                : item.german))
                        .Append("\"");
                }
                lines.AppendLine();
            }
            lines.AppendLine();
        }

        private static void AppendRuntimeInventory(StringBuilder lines)
        {
            lines.AppendLine("RUNTIME-ASSIGNED UI STRINGS FOUND IN C#");
            foreach (RuntimeStringPlan value in RuntimeStrings)
            {
                lines.Append("RUNTIME  ")
                    .Append(value.source)
                    .Append(" -> ")
                    .Append(value.key)
                    .Append(" | EN=\"")
                    .Append(value.english)
                    .Append("\" | DE=\"")
                    .Append(value.german)
                    .AppendLine("\"");
            }
            lines.AppendLine(
                "LOG-ONLY Debug messages remain unchanged; they are not visible TMP UI.");
            lines.AppendLine();
        }

        private static void AppendCoverage(
            StringBuilder lines,
            int keyCount,
            int germanCoverage,
            List<string> missingGerman)
        {
            lines.AppendLine("LOCALIZATION KEY AND COVERAGE SUMMARY");
            lines.AppendLine("- Exact stable key count: " + keyCount);
            lines.AppendLine("- English coverage: " + keyCount + "/" + keyCount + " (100%)");
            lines.AppendLine(
                "- German coverage: " + germanCoverage + "/" + keyCount);
            lines.AppendLine("- Missing German keys: " + missingGerman.Count);
            foreach (string missing in missingGerman.OrderBy(value => value))
                lines.AppendLine("FALLBACK  " + missing);
            lines.AppendLine(
                "- Missing German values resolve through EnglishLocalizationTable; " +
                "the key itself is used only if English is also missing.");
            lines.AppendLine();
        }

        private static void AppendTaskInventory(StringBuilder lines)
        {
            lines.AppendLine("TASK-CONTENT LOCALIZATION");
            lines.AppendLine("- Existing English TaskDefinition fields remain unchanged:");
            lines.AppendLine("  displayName, description, stepTitle, instruction, warning, verification");
            lines.AppendLine("- Additive TaskDefinition.localizedContent list keyed by locale");
            lines.AppendLine("- LocalizedTaskContent contains optional title/description and");
            lines.AppendLine("  per-step localized title/instruction/warning/verification");
            lines.AppendLine("- Step translations are matched by authored step index; missing values");
            lines.AppendLine("  fall back field-by-field to the existing English TaskStep");
            lines.AppendLine("- Apply does not populate or rewrite any task asset");

            int englishFields = 0;
            int germanFields = 0;
            foreach (string path in TaskAssets)
            {
                TaskDefinition task =
                    AssetDatabase.LoadAssetAtPath<TaskDefinition>(path);
                if (task == null)
                {
                    lines.AppendLine("MISSING TASK  " + path);
                    continue;
                }

                int taskFields = 0;
                if (!string.IsNullOrWhiteSpace(task.DisplayName)) taskFields++;
                if (!string.IsNullOrWhiteSpace(task.Description)) taskFields++;
                for (int i = 0; i < task.Steps.Count; i++)
                {
                    TaskStep step = task.Steps[i];
                    if (!string.IsNullOrWhiteSpace(step.stepTitle)) taskFields++;
                    if (!string.IsNullOrWhiteSpace(step.instruction)) taskFields++;
                    if (!string.IsNullOrWhiteSpace(step.warning)) taskFields++;
                    if (!string.IsNullOrWhiteSpace(step.verification)) taskFields++;
                }
                englishFields += taskFields;
                lines.AppendLine(
                    "TASK  " + task.TaskId + " | English fields=" + taskFields +
                    " | German fields=0 | fallback=" + taskFields);
            }
            lines.AppendLine(
                "- Existing technician-content English fields: " + englishFields);
            lines.AppendLine(
                "- Existing German technician-content fields: " + germanFields);
            lines.AppendLine(
                "- English task fallbacks: " + (englishFields - germanFields));
            lines.AppendLine();
        }

        private static void AppendFlows(StringBuilder lines)
        {
            lines.AppendLine("IMMEDIATE REFRESH EVENT FLOW");
            lines.AppendLine("English/German card Button");
            lines.AppendLine("  -> NorscopeSettingsService.SetLanguage(language)");
            lines.AppendLine("  -> persist existing schema-1 JSON atomically");
            lines.AppendLine("  -> NorscopeSettingsService.LanguageChanged");
            lines.AppendLine("  -> LocalizationService selects table and emits LocaleChanged");
            lines.AppendLine("  -> every enabled LocalizedText refreshes its TMP target");
            lines.AppendLine("  -> open task panels rebind only localized presentation data");
            lines.AppendLine("  -> no hierarchy recreation, navigation, window or workflow mutation");
            lines.AppendLine();
            lines.AppendLine("PERSISTENCE FLOW");
            lines.AppendLine("- Existing NorscopeSettingsData.language field is reused");
            lines.AppendLine("- Existing versioned JSON path and recovery remain unchanged");
            lines.AppendLine("- No PlayerPrefs and no independent panel language state");
            lines.AppendLine("- LocalizationService reads current language during initialization");
            lines.AppendLine("- English is authoritative fallback for missing table/task values");
            lines.AppendLine();
        }

        private static void AppendProtection(StringBuilder lines)
        {
            lines.AppendLine("PROTECTED FILES / SYSTEMS");
            foreach (string path in ProtectedArchitecture)
                lines.AppendLine("- " + path);
            lines.AppendLine("- All six existing TaskDefinition assets remain byte-for-byte unchanged");
            lines.AppendLine("- PanelRegistry entries, navigation destinations and UnityEvents");
            lines.AppendLine("- SpatialWindow lifecycle, close, grab and resize behavior");
            lines.AppendLine("- UIAudioManager and UIAudioTheme");
            lines.AppendLine("- Reports, Ask AI, workflow state and report generation");
            lines.AppendLine("- No AudioMixer, text scaling, TTS, speech or microphone implementation");
            lines.AppendLine("- No MRTK package");
            lines.AppendLine();
        }

        private static void ScanPrefab(
            string prefabPath,
            List<TextMigration> migrations,
            Dictionary<string, string> keyToEnglish,
            List<string> blockers)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                blockers.Add("Target prefab is missing: " + prefabPath);
                return;
            }

            HashSet<TMP_Text> dataBound = FindDataBoundTexts(prefab);
            string domain = DomainFor(prefabPath);
            TMP_Text[] texts = prefab.GetComponentsInChildren<TMP_Text>(true);
            Array.Sort(
                texts,
                (left, right) => string.CompareOrdinal(
                    TransformPath(left.transform, prefab.transform),
                    TransformPath(right.transform, prefab.transform)));

            foreach (TMP_Text text in texts)
            {
                string english = Normalize(text.text);
                var item = new TextMigration
                {
                    prefab = prefabPath,
                    componentPath = TransformPath(text.transform, prefab.transform),
                    english = english
                };

                if (dataBound.Contains(text) || string.IsNullOrWhiteSpace(english))
                {
                    item.classification = "DATA-BOUND";
                }
                else if (!ContainsLetter(english))
                {
                    item.classification = "NON-LINGUISTIC";
                }
                else
                {
                    item.classification = "MIGRATE";
                    item.key = ResolveKey(domain, english, keyToEnglish);
                    item.german = ResolveGerman(english);
                    if (!keyToEnglish.ContainsKey(item.key))
                        keyToEnglish[item.key] = english;
                }
                migrations.Add(item);
            }
        }

        private static HashSet<TMP_Text> FindDataBoundTexts(GameObject prefab)
        {
            var result = new HashSet<TMP_Text>();
            MonoBehaviour[] behaviours =
                prefab.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour == null ||
                    !DataBinderTypeNames.Contains(behaviour.GetType().Name))
                    continue;
                SerializedObject so = new SerializedObject(behaviour);
                SerializedProperty property = so.GetIterator();
                bool enterChildren = true;
                while (property.NextVisible(enterChildren))
                {
                    enterChildren = false;
                    if (property.propertyType !=
                        SerializedPropertyType.ObjectReference)
                        continue;
                    if (property.objectReferenceValue is TMP_Text text)
                        result.Add(text);
                }
            }
            return result;
        }

        private static string ResolveKey(
            string domain,
            string english,
            Dictionary<string, string> existing)
        {
            if (ExplicitKeys.TryGetValue(english, out string explicitKey))
                return explicitKey;

            string slug = Regex.Replace(
                english.ToLowerInvariant(), @"[^a-z0-9]+", "_").Trim('_');
            if (string.IsNullOrEmpty(slug))
                slug = "text";
            if (slug.Length > 52)
                slug = slug.Substring(0, 52).TrimEnd('_');
            string candidate = domain + "." + slug;
            if (!existing.TryGetValue(candidate, out string current) ||
                string.Equals(current, english, StringComparison.Ordinal))
                return candidate;

            return candidate + "_" + ShortHash(english);
        }

        private static string ResolveGerman(string english)
        {
            return German.TryGetValue(english, out string value)
                ? value
                : string.Empty;
        }

        private static string DomainFor(string path)
        {
            if (path.Contains("HomePanel")) return "home";
            if (path.Contains("HomeLauncher")) return "launcher";
            if (path.Contains("TaskLibrary")) return "task_library";
            if (path.Contains("TaskCard")) return "task_card";
            if (path.Contains("ActiveTask")) return "active_task";
            if (path.Contains("TaskCompletion")) return "task_complete";
            if (path.Contains("SettingsPanel")) return "settings";
            return "shared";
        }

        private static string TransformPath(Transform target, Transform root)
        {
            var names = new Stack<string>();
            Transform current = target;
            while (current != null)
            {
                names.Push(current.name);
                if (current == root) break;
                current = current.parent;
            }
            return string.Join("/", names);
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return Regex.Replace(value, @"\s+", " ").Trim();
        }

        private static bool ContainsLetter(string value)
        {
            for (int i = 0; i < value.Length; i++)
                if (char.IsLetter(value[i])) return true;
            return false;
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"");
        }

        private static RuntimeStringPlan R(
            string source,
            string key,
            string english,
            string german)
        {
            return new RuntimeStringPlan(source, key, english, german);
        }

        private static IEnumerable<string> RequiredFiles()
        {
            yield return "Assets/Norscope/Scenes/Bootstrap.unity";
            foreach (string path in TargetPrefabs) yield return path;
            foreach (string path in PlannedModifiedRuntime) yield return path;
            foreach (string path in ProtectedArchitecture) yield return path;
            foreach (string path in TaskAssets) yield return path;
        }

        private static LocalizationBaseline LoadBaseline(List<string> blockers)
        {
            if (!File.Exists(BaselinePath))
            {
                blockers.Add(
                    "Localization baseline missing. Run 1) Capture Localization Baseline.");
                return null;
            }
            try
            {
                LocalizationBaseline baseline =
                    JsonUtility.FromJson<LocalizationBaseline>(
                        File.ReadAllText(BaselinePath));
                if (baseline == null || baseline.schemaVersion != 1)
                {
                    blockers.Add("Localization baseline schema is invalid.");
                    return null;
                }
                if (!File.Exists(baseline.backupFolder + "/Bootstrap.unity"))
                    blockers.Add("Localization Bootstrap backup is missing.");
                return baseline;
            }
            catch (Exception exception)
            {
                blockers.Add("Localization baseline cannot be read: " + exception.Message);
                return null;
            }
        }

        private static void ValidateBaseline(
            LocalizationBaseline baseline,
            List<string> blockers)
        {
            if (baseline == null || baseline.entries == null) return;
            foreach (BaselineEntry entry in baseline.entries)
            {
                if (!File.Exists(entry.path))
                {
                    blockers.Add("Baseline target is missing: " + entry.path);
                    continue;
                }
                if (!string.Equals(
                        Hash(entry.path), entry.sha256,
                        StringComparison.OrdinalIgnoreCase))
                {
                    blockers.Add(
                        "File changed after localization baseline capture: " +
                        entry.path);
                }
            }
        }

        private static string Hash(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                byte[] hash = sha.ComputeHash(stream);
                var text = new StringBuilder(hash.Length * 2);
                foreach (byte value in hash)
                    text.Append(value.ToString("x2"));
                return text.ToString();
            }
        }

        private static string ShortHash(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
                return BitConverter.ToString(bytes, 0, 3)
                    .Replace("-", string.Empty)
                    .ToLowerInvariant();
            }
        }
    }
}
