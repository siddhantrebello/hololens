using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Norscope.Navigation;
using Norscope.TaskExecution;
using Norscope.TaskLibrary;
using Norscope.Windows;

namespace Norscope.Editor
{
    [Serializable]
    internal sealed class D1FunctionalBaselineData
    {
        public int schemaVersion = 1;
        public string capturedUtc;
        public D1PrefabRecord[] prefabs;
        public D1SourceHash[] functionalSourceHashes;
        public D1NavigationEntry[] navigationEntries;
        public string backupTimestamp;
        public D1BackupRecord[] backups;
    }

    [Serializable]
    internal sealed class D1PrefabRecord
    {
        public string assetPath;
        public string[] hierarchyPaths;
        public D1ButtonRecord[] buttons;
        public D1SpriteRecord[] sprites;
        public D1ComponentRecord[] functionalComponents;
    }

    [Serializable]
    internal sealed class D1ButtonRecord
    {
        public string hierarchyPath;
        public string componentIdentity;
        public string targetGraphicIdentity;
        public int transition;
        public bool interactable;
        public D1ListenerRecord[] persistentListeners;
    }

    [Serializable]
    internal sealed class D1ListenerRecord
    {
        public string targetIdentity;
        public string methodName;
        public int mode;
        public int callState;
        public string argumentSnapshot;
    }

    [Serializable]
    internal sealed class D1SpriteRecord
    {
        public string hierarchyPath;
        public string imageIdentity;
        public string spriteIdentity;
    }

    [Serializable]
    internal sealed class D1ComponentRecord
    {
        public string hierarchyPath;
        public string typeName;
        public string componentIdentity;
        public string fieldSnapshot;
    }

    [Serializable]
    internal sealed class D1SourceHash
    {
        public string assetPath;
        public string sha256;
    }

    [Serializable]
    internal sealed class D1NavigationEntry
    {
        public int panelType;
        public int windowId;
        public int behaviour;
        public string prefabIdentity;
    }

    [Serializable]
    internal sealed class D1BackupRecord
    {
        public string sourcePath;
        public string backupPath;
        public string backupSha256;
    }

    internal static class D1FunctionalBaseline
    {
        internal const string BaselinePath =
            "Assets/Norscope/Editor/Validation/D1FunctionalBindingsBaseline.json";
        private const string BootstrapPath = "Assets/Norscope/Scenes/Bootstrap.unity";
        private const string BackupRoot = "Assets/Norscope/DesignSystem/Backups";

        internal static readonly string[] ScopedPrefabPaths =
        {
            "Assets/Norscope/UI/Panels/HomePanel.prefab",
            "Assets/Norscope/UI/Panels/TaskLibraryPanel.prefab",
            "Assets/Norscope/UI/Panels/ActiveTaskPanel.prefab",
            "Assets/Norscope/UI/Controls/TaskCard.prefab",
            "Assets/Norscope/UI/Controls/NavigationBackButton.prefab",
            "Assets/Norscope/UI/Controls/NorscopeCloseButton.prefab"
        };

        private static readonly string[] FunctionalSourcePaths =
        {
            "Assets/Norscope/Runtime/Navigation/HomePanelCard.cs",
            "Assets/Norscope/Runtime/Navigation/PanelNavigationBinding.cs",
            "Assets/Norscope/Runtime/Navigation/PanelNavigationService.cs",
            "Assets/Norscope/Runtime/Navigation/PanelRegistry.cs",
            "Assets/Norscope/Runtime/TaskLibrary/TaskCardView.cs",
            "Assets/Norscope/Runtime/TaskLibrary/TaskLibraryPanel.cs",
            "Assets/Norscope/Runtime/TaskExecution/ActiveTaskPanel.cs",
            "Assets/Norscope/Runtime/TaskExecution/ActiveTaskSession.cs",
            "Assets/Norscope/Runtime/TaskExecution/ActiveTaskLauncher.cs",
            "Assets/Norscope/Runtime/Windows/SpatialWindow.cs",
            "Assets/Norscope/Runtime/Windows/SpatialWindowManager.cs",
            "Assets/Norscope/Runtime/UiButtonPressScale.cs"
        };

        internal static D1FunctionalBaselineData Capture()
        {
            D1PrefabRecord[] prefabs = ScopedPrefabPaths
                .Select(CapturePrefab)
                .ToArray();
            D1SourceHash[] sourceHashes = FunctionalSourcePaths
                .Select(path => new D1SourceHash
                {
                    assetPath = path,
                    sha256 = ComputeHash(path)
                })
                .ToArray();

            EditorSceneManager.OpenScene(BootstrapPath, OpenSceneMode.Single);
            PanelRegistry registry = UnityEngine.Object.FindFirstObjectByType<PanelRegistry>();
            D1NavigationEntry[] navigation = registry != null
                ? registry.Entries.Where(entry => entry != null)
                    .Select(entry => new D1NavigationEntry
                    {
                        panelType = (int)entry.panelType,
                        windowId = (int)entry.windowId,
                        behaviour = (int)entry.behaviour,
                        prefabIdentity = StableIdentity(entry.prefab)
                    })
                    .OrderBy(entry => entry.panelType)
                    .ToArray()
                : Array.Empty<D1NavigationEntry>();

            return new D1FunctionalBaselineData
            {
                schemaVersion = 1,
                capturedUtc = DateTime.UtcNow.ToString("O"),
                prefabs = prefabs,
                functionalSourceHashes = sourceHashes,
                navigationEntries = navigation,
                backupTimestamp = string.Empty,
                backups = Array.Empty<D1BackupRecord>()
            };
        }

        internal static bool CreateImmutableBackups(
            D1FunctionalBaselineData baseline,
            out string report)
        {
            if (baseline == null)
            {
                report = "Baseline data is null.";
                return false;
            }

            string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            string snapshotFolder = BackupRoot + "/PreD1_" + timestamp;
            EnsureAssetFolder(BackupRoot);
            EnsureAssetFolder(snapshotFolder);

            var records = new List<D1BackupRecord>();
            var messages = new List<string>();
            string[] sources = ScopedPrefabPaths
                .Concat(new[] { BootstrapPath })
                .Distinct()
                .ToArray();

            for (int i = 0; i < sources.Length; i++)
            {
                string source = sources[i];
                if (!File.Exists(source))
                {
                    messages.Add("MISSING  " + source);
                    continue;
                }

                string backupPath = snapshotFolder + "/" + Path.GetFileName(source);
                if (!AssetDatabase.CopyAsset(source, backupPath))
                {
                    messages.Add("FAILED   " + source);
                    continue;
                }

                string sourceHash = ComputeHash(source);
                string backupHash = ComputeHash(backupPath);
                if (sourceHash != backupHash)
                {
                    messages.Add("FAILED   " + source + " (backup content mismatch)");
                    continue;
                }

                records.Add(new D1BackupRecord
                {
                    sourcePath = source,
                    backupPath = backupPath,
                    backupSha256 = backupHash
                });
                messages.Add("BACKED UP " + source + " -> " + backupPath);
            }

            baseline.backupTimestamp = timestamp;
            baseline.backups = records.ToArray();
            bool complete = records.Count == sources.Length;
            report = string.Join("\n", messages);
            return complete;
        }

        internal static bool RestoreImmutableBackups(out string report)
        {
            D1FunctionalBaselineData baseline = Load();
            var restored = new List<string>();
            var skipped = new List<string>();
            var missing = new List<string>();
            var failed = new List<string>();
            string restoreTimestamp = DateTime.UtcNow.ToString("O");

            if (baseline == null || baseline.backups == null || baseline.backups.Length == 0)
            {
                report = "Restore timestamp: " + restoreTimestamp +
                         "\nFAILED: baseline backup manifest is missing.";
                return false;
            }

            HashSet<string> allowedSources = new HashSet<string>(
                ScopedPrefabPaths.Concat(new[] { BootstrapPath }),
                StringComparer.Ordinal);
            for (int i = 0; i < baseline.backups.Length; i++)
            {
                D1BackupRecord record = baseline.backups[i];
                if (record == null ||
                    string.IsNullOrEmpty(record.sourcePath) ||
                    string.IsNullOrEmpty(record.backupPath))
                {
                    skipped.Add(record != null ? record.sourcePath : "<invalid manifest entry>");
                    continue;
                }

                if (!allowedSources.Contains(record.sourcePath))
                {
                    skipped.Add(record.sourcePath + " (not a D1 baseline-scoped asset)");
                    continue;
                }

                if (!File.Exists(record.backupPath))
                {
                    missing.Add(record.backupPath);
                    continue;
                }

                if (ComputeHash(record.backupPath) != record.backupSha256)
                {
                    failed.Add(record.sourcePath + " (backup hash mismatch)");
                    continue;
                }

                try
                {
                    File.Copy(record.backupPath, record.sourcePath, true);
                    AssetDatabase.ImportAsset(
                        record.sourcePath,
                        ImportAssetOptions.ForceSynchronousImport |
                        ImportAssetOptions.ForceUpdate);
                    restored.Add(record.sourcePath);
                }
                catch (Exception exception)
                {
                    failed.Add(record.sourcePath + " (" + exception.Message + ")");
                }
            }

            var builder = new StringBuilder();
            builder.AppendLine("Restore timestamp: " + restoreTimestamp);
            builder.AppendLine("Baseline snapshot: " + baseline.backupTimestamp);
            AppendReportSection(builder, "Restored assets", restored);
            AppendReportSection(builder, "Skipped assets", skipped);
            AppendReportSection(builder, "Missing backups", missing);
            AppendReportSection(builder, "Failures", failed);
            bool success = restored.Count == allowedSources.Count &&
                           skipped.Count == 0 &&
                           missing.Count == 0 &&
                           failed.Count == 0;
            builder.AppendLine("Result: " + (success ? "SUCCESS" : "FAILED"));
            report = builder.ToString();
            return success;
        }

        internal static bool HasCompleteBackupManifest(out string issue)
        {
            D1FunctionalBaselineData baseline = Load();
            if (baseline == null)
            {
                issue = "baseline file is missing";
                return false;
            }

            string[] requiredSources = ScopedPrefabPaths
                .Concat(new[] { BootstrapPath })
                .Distinct()
                .ToArray();
            if (baseline.backups == null ||
                baseline.backups.Length != requiredSources.Length)
            {
                issue = "expected " + requiredSources.Length +
                        " backup records but found " +
                        (baseline.backups != null ? baseline.backups.Length : 0);
                return false;
            }

            for (int i = 0; i < requiredSources.Length; i++)
            {
                string source = requiredSources[i];
                D1BackupRecord[] matches = baseline.backups
                    .Where(record => record != null && record.sourcePath == source)
                    .ToArray();
                if (matches.Length != 1)
                {
                    issue = source + " has " + matches.Length + " manifest records";
                    return false;
                }

                D1BackupRecord record = matches[0];
                if (!File.Exists(record.backupPath))
                {
                    issue = "backup is missing for " + source;
                    return false;
                }
                if (string.IsNullOrEmpty(record.backupSha256) ||
                    ComputeHash(record.backupPath) != record.backupSha256)
                {
                    issue = "backup hash is invalid for " + source;
                    return false;
                }
            }

            issue = string.Empty;
            return true;
        }

        internal static void Save(D1FunctionalBaselineData baseline)
        {
            string directory = Path.GetDirectoryName(BaselinePath);
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(BaselinePath, JsonUtility.ToJson(baseline, true));
            AssetDatabase.ImportAsset(BaselinePath, ImportAssetOptions.ForceUpdate);
        }

        internal static D1FunctionalBaselineData Load()
        {
            if (!File.Exists(BaselinePath)) return null;
            return JsonUtility.FromJson<D1FunctionalBaselineData>(
                File.ReadAllText(BaselinePath));
        }

        internal static bool CompareAgainstSaved(out string report)
        {
            D1FunctionalBaselineData saved = Load();
            if (saved == null)
            {
                report = "Functional baseline is missing.";
                return false;
            }

            D1FunctionalBaselineData current = Capture();
            var differences = new List<string>();
            CompareSources(saved, current, differences);
            CompareNavigation(saved, current, differences);
            ComparePrefabs(saved, current, differences);
            report = differences.Count == 0
                ? "All recorded functional bindings match the pre-D1 baseline."
                : string.Join("\n", differences);
            return differences.Count == 0;
        }

        private static D1PrefabRecord CapturePrefab(string assetPath)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab == null)
            {
                return new D1PrefabRecord
                {
                    assetPath = assetPath,
                    hierarchyPaths = Array.Empty<string>(),
                    buttons = Array.Empty<D1ButtonRecord>(),
                    sprites = Array.Empty<D1SpriteRecord>(),
                    functionalComponents = Array.Empty<D1ComponentRecord>()
                };
            }

            string[] hierarchy = prefab.GetComponentsInChildren<Transform>(true)
                .Select(transform => HierarchyPath(prefab.transform, transform))
                .OrderBy(path => path)
                .ToArray();
            D1ButtonRecord[] buttons = prefab.GetComponentsInChildren<Button>(true)
                .Select(button => CaptureButton(prefab.transform, button))
                .OrderBy(record => record.hierarchyPath)
                .ToArray();
            D1SpriteRecord[] sprites = prefab.GetComponentsInChildren<Image>(true)
                .Select(image => new D1SpriteRecord
                {
                    hierarchyPath = HierarchyPath(prefab.transform, image.transform),
                    imageIdentity = StableIdentity(image),
                    spriteIdentity = StableIdentity(image.sprite)
                })
                .OrderBy(record => record.hierarchyPath)
                .ThenBy(record => record.imageIdentity)
                .ToArray();

            var components = new List<D1ComponentRecord>();
            CaptureComponents<HomePanelCard>(prefab, components, new[]
            {
                "targetPanel", "button", "background", "border", "icon", "title",
                "subtitle", "theme"
            });
            CaptureComponents<TaskCardView>(prefab, components, new[]
            {
                "icon", "titleText", "descriptionText", "durationText",
                "difficultyBadgeBackground", "difficultyBadgeText", "cardButton", "border",
                "theme", "fallbackIcon"
            });
            CaptureComponents<ActiveTaskPanel>(prefab, components, new[]
            {
                "taskView", "completionView", "titleText", "stepCounterText", "progressFill",
                "stepTitleText", "instructionText", "imageSection", "referenceImage",
                "warningSection", "warningText", "verificationSection", "verificationText",
                "previousButton", "nextButton", "finishButton", "completionTaskTitleText",
                "completionMessageText", "returnHomeButton", "generateReportButton"
            });
            CaptureComponents<PanelNavigationBinding>(prefab, components, new[]
            {
                "panelType", "backButton", "fallbackParent"
            });
            CaptureComponents<SpatialWindowBindings>(prefab, components, new[]
            {
                "grabRegion", "resizeHandle", "closeButtonRoot"
            });

            return new D1PrefabRecord
            {
                assetPath = assetPath,
                hierarchyPaths = hierarchy,
                buttons = buttons,
                sprites = sprites,
                functionalComponents = components
                    .OrderBy(record => record.hierarchyPath)
                    .ThenBy(record => record.typeName)
                    .ToArray()
            };
        }

        private static D1ButtonRecord CaptureButton(Transform root, Button button)
        {
            SerializedObject so = new SerializedObject(button);
            SerializedProperty calls =
                so.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
            var listeners = new List<D1ListenerRecord>();
            if (calls != null)
            {
                for (int i = 0; i < calls.arraySize; i++)
                {
                    SerializedProperty call = calls.GetArrayElementAtIndex(i);
                    SerializedProperty arguments = call.FindPropertyRelative("m_Arguments");
                    listeners.Add(new D1ListenerRecord
                    {
                        targetIdentity = StableIdentity(
                            call.FindPropertyRelative("m_Target").objectReferenceValue),
                        methodName =
                            call.FindPropertyRelative("m_MethodName").stringValue,
                        mode = call.FindPropertyRelative("m_Mode").enumValueIndex,
                        callState =
                            call.FindPropertyRelative("m_CallState").enumValueIndex,
                        argumentSnapshot = arguments != null
                            ? SerializedPropertySnapshot(arguments)
                            : string.Empty
                    });
                }
            }

            return new D1ButtonRecord
            {
                hierarchyPath = HierarchyPath(root, button.transform),
                componentIdentity = StableIdentity(button),
                targetGraphicIdentity = StableIdentity(button.targetGraphic),
                transition = (int)button.transition,
                interactable = button.interactable,
                persistentListeners = listeners.ToArray()
            };
        }

        private static void CaptureComponents<T>(
            GameObject prefab,
            List<D1ComponentRecord> output,
            string[] fieldNames) where T : Component
        {
            T[] components = prefab.GetComponentsInChildren<T>(true);
            for (int i = 0; i < components.Length; i++)
            {
                SerializedObject so = new SerializedObject(components[i]);
                var fields = new List<string>();
                for (int fieldIndex = 0; fieldIndex < fieldNames.Length; fieldIndex++)
                {
                    string fieldName = fieldNames[fieldIndex];
                    SerializedProperty property = so.FindProperty(fieldName);
                    fields.Add(fieldName + "=" +
                               (property != null
                                   ? SerializedPropertySnapshot(property)
                                   : "<missing>"));
                }

                output.Add(new D1ComponentRecord
                {
                    hierarchyPath =
                        HierarchyPath(prefab.transform, components[i].transform),
                    typeName = typeof(T).FullName,
                    componentIdentity = StableIdentity(components[i]),
                    fieldSnapshot = string.Join("|", fields)
                });
            }
        }

        private static string SerializedPropertySnapshot(SerializedProperty property)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.ObjectReference:
                    return StableIdentity(property.objectReferenceValue);
                case SerializedPropertyType.Enum:
                    return property.enumValueIndex.ToString();
                case SerializedPropertyType.Boolean:
                    return property.boolValue.ToString();
                case SerializedPropertyType.Integer:
                    return property.longValue.ToString();
                case SerializedPropertyType.Float:
                    return property.doubleValue.ToString("R");
                case SerializedPropertyType.String:
                    return property.stringValue ?? string.Empty;
                default:
                    return property.propertyPath + ":" + property.propertyType;
            }
        }

        private static void CompareSources(
            D1FunctionalBaselineData saved,
            D1FunctionalBaselineData current,
            List<string> differences)
        {
            foreach (D1SourceHash expected in saved.functionalSourceHashes)
            {
                D1SourceHash actual = current.functionalSourceHashes.FirstOrDefault(
                    item => item.assetPath == expected.assetPath);
                if (actual == null || actual.sha256 != expected.sha256)
                {
                    differences.Add("Functional source changed: " + expected.assetPath);
                }
            }
        }

        private static void CompareNavigation(
            D1FunctionalBaselineData saved,
            D1FunctionalBaselineData current,
            List<string> differences)
        {
            string expected = JsonUtility.ToJson(
                new D1NavigationWrapper { entries = saved.navigationEntries });
            string actual = JsonUtility.ToJson(
                new D1NavigationWrapper { entries = current.navigationEntries });
            if (expected != actual)
            {
                differences.Add("PanelRegistry navigation relationships changed.");
            }
        }

        private static void ComparePrefabs(
            D1FunctionalBaselineData saved,
            D1FunctionalBaselineData current,
            List<string> differences)
        {
            foreach (D1PrefabRecord expected in saved.prefabs)
            {
                D1PrefabRecord actual = current.prefabs.FirstOrDefault(
                    item => item.assetPath == expected.assetPath);
                if (actual == null)
                {
                    differences.Add("Scoped prefab missing: " + expected.assetPath);
                    continue;
                }

                foreach (string path in expected.hierarchyPaths)
                {
                    if (!actual.hierarchyPaths.Contains(path))
                    {
                        differences.Add(
                            "Original hierarchy object missing: " + expected.assetPath + " :: " +
                            path);
                    }
                }

                CompareRecordArray(
                    expected.assetPath,
                    "Button binding",
                    expected.buttons.Select(item => JsonUtility.ToJson(item)).ToArray(),
                    actual.buttons.Select(item => JsonUtility.ToJson(item)).ToArray(),
                    differences);
                CompareRecordArray(
                    expected.assetPath,
                    "Sprite reference",
                    expected.sprites.Select(item => JsonUtility.ToJson(item)).ToArray(),
                    actual.sprites.Select(item => JsonUtility.ToJson(item)).ToArray(),
                    differences,
                    true);
                CompareRecordArray(
                    expected.assetPath,
                    "Functional component",
                    expected.functionalComponents.Select(item => JsonUtility.ToJson(item)).ToArray(),
                    actual.functionalComponents.Select(item => JsonUtility.ToJson(item)).ToArray(),
                    differences);
            }
        }

        private static void CompareRecordArray(
            string assetPath,
            string label,
            string[] expected,
            string[] actual,
            List<string> differences,
            bool allowAdditional = false)
        {
            for (int i = 0; i < expected.Length; i++)
            {
                if (!actual.Contains(expected[i]))
                {
                    differences.Add(label + " changed: " + assetPath);
                    return;
                }
            }

            if (!allowAdditional && expected.Length != actual.Length)
            {
                differences.Add(label + " count changed: " + assetPath);
            }
        }

        private static string HierarchyPath(Transform root, Transform target)
        {
            if (target == root) return root.name;
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

        private static string StableIdentity(UnityEngine.Object value)
        {
            if (value == null) return "null";
            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    value,
                    out string guid,
                    out long localId))
            {
                return guid + ":" + localId;
            }
            return value.GetType().FullName + ":" + value.name;
        }

        private static string ComputeHash(string assetPath)
        {
            if (!File.Exists(assetPath)) return "MISSING";
            using (SHA256 sha = SHA256.Create())
            {
                return Convert.ToBase64String(
                    sha.ComputeHash(File.ReadAllBytes(assetPath)));
            }
        }

        private static void EnsureAssetFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            string name = Path.GetFileName(folder);
            if (!string.IsNullOrEmpty(parent)) EnsureAssetFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static void AppendReportSection(
            StringBuilder builder,
            string heading,
            List<string> values)
        {
            builder.AppendLine(heading + " (" + values.Count + "):");
            if (values.Count == 0)
            {
                builder.AppendLine("  none");
                return;
            }

            for (int i = 0; i < values.Count; i++)
            {
                builder.AppendLine("  " + values[i]);
            }
        }

        [Serializable]
        private sealed class D1NavigationWrapper
        {
            public D1NavigationEntry[] entries;
        }
    }
}
