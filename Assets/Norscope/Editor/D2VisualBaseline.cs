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

namespace Norscope.Editor
{
    [Serializable]
    internal sealed class D2BaselineData
    {
        public string capturedUtc;
        public D2PrefabRecord[] prefabs;
        public D2SourceRecord[] sources;
        public D2NavigationRecord[] navigation;
        public D2BackupRecord[] backups;
    }

    [Serializable]
    internal sealed class D2PrefabRecord
    {
        public string path;
        public string[] hierarchy;
        public D2ButtonRecord[] buttons;
        public D2SpriteRecord[] sprites;
        public D2BackRecord[] backBindings;
    }

    [Serializable]
    internal sealed class D2ButtonRecord
    {
        public string path;
        public string identity;
        public string targetGraphicIdentity;
        public D2ListenerRecord[] listeners;
    }

    [Serializable]
    internal sealed class D2ListenerRecord
    {
        public string targetIdentity;
        public string method;
    }

    [Serializable]
    internal sealed class D2SpriteRecord
    {
        public string path;
        public string imageIdentity;
        public string spriteIdentity;
    }

    [Serializable]
    internal sealed class D2BackRecord
    {
        public string path;
        public int panelType;
        public int fallbackParent;
        public string buttonIdentity;
    }

    [Serializable]
    internal sealed class D2SourceRecord
    {
        public string path;
        public string sha256;
    }

    [Serializable]
    internal sealed class D2NavigationRecord
    {
        public int panelType;
        public int windowId;
        public int behaviour;
        public string prefabIdentity;
    }

    [Serializable]
    internal sealed class D2BackupRecord
    {
        public string source;
        public string backup;
        public string sha256;
    }

    internal static class D2VisualBaseline
    {
        internal const string BaselinePath =
            "Assets/Norscope/Editor/Validation/D2PremiumVisualBaseline.json";
        private const string BootstrapPath =
            "Assets/Norscope/Scenes/Bootstrap.unity";
        private const string BackupRoot =
            "Assets/Norscope/DesignSystem/Backups";

        internal static readonly string[] TargetPrefabs =
        {
            "Assets/Norscope/UI/Panels/HomePanel.prefab",
            "Assets/Norscope/UI/Panels/TaskLibraryPanel.prefab",
            "Assets/Norscope/UI/Panels/ActiveTaskPanel.prefab",
            "Assets/Norscope/UI/Panels/TaskCompletionPanel.prefab",
            "Assets/Norscope/UI/Controls/TaskCard.prefab",
            "Assets/Norscope/UI/Controls/NavigationBackButton.prefab",
            "Assets/Norscope/UI/Controls/NorscopeCloseButton.prefab",
            "Assets/Norscope/UI/Controls/NorscopePrimaryButton.prefab",
            "Assets/Norscope/UI/Controls/NorscopeSecondaryButton.prefab"
        };

        internal static readonly string[] FunctionalSources =
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
            "Assets/Norscope/Runtime/TaskExecution/TaskCompletionPanel.cs",
            "Assets/Norscope/Runtime/Windows/SpatialWindow.cs",
            "Assets/Norscope/Runtime/Windows/SpatialWindowManager.cs"
        };

        internal static bool CaptureAndBackUp(out string report)
        {
            if (File.Exists(BaselinePath))
            {
                report = "D2 baseline already exists and was left untouched.";
                return true;
            }

            string[] missing = TargetPrefabs
                .Concat(FunctionalSources)
                .Concat(new[] { BootstrapPath })
                .Where(path => !File.Exists(path))
                .ToArray();
            if (missing.Length > 0)
            {
                report = "Missing required assets:\n" + string.Join("\n", missing);
                return false;
            }

            D2BaselineData data = CaptureCurrent();
            string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            string folder = BackupRoot + "/PreD2_" + timestamp;
            EnsureFolder(BackupRoot);
            EnsureFolder(folder);

            string[] backupSources = TargetPrefabs
                .Concat(new[]
                {
                    BootstrapPath,
                    "Assets/Norscope/DesignSystem/Profiles/NorscopeDesignProfile.asset",
                    "Assets/Norscope/UI/DesignSystem/Themes/NorscopeTheme.asset"
                })
                .Distinct()
                .ToArray();
            var backups = new List<D2BackupRecord>();
            foreach (string source in backupSources)
            {
                if (!File.Exists(source))
                {
                    report = "Backup source missing: " + source;
                    return false;
                }

                string backup = folder + "/" + Path.GetFileName(source);
                if (!AssetDatabase.CopyAsset(source, backup))
                {
                    report = "Could not back up: " + source;
                    return false;
                }

                string hash = Hash(backup);
                if (hash != Hash(source))
                {
                    report = "Backup verification failed: " + source;
                    return false;
                }
                backups.Add(new D2BackupRecord
                {
                    source = source,
                    backup = backup,
                    sha256 = hash
                });
            }

            data.backups = backups.ToArray();
            Directory.CreateDirectory(Path.GetDirectoryName(BaselinePath));
            File.WriteAllText(BaselinePath, JsonUtility.ToJson(data, true));
            AssetDatabase.ImportAsset(BaselinePath, ImportAssetOptions.ForceUpdate);
            report = "Captured " + data.prefabs.Length + " prefab baselines and " +
                     backups.Count + " verified backups.";
            return true;
        }

        internal static bool Compare(out string report)
        {
            D2BaselineData saved = Load();
            if (saved == null)
            {
                report = "D2 baseline is missing.";
                return false;
            }

            D2BaselineData current = CaptureCurrent();
            var issues = new List<string>();
            ComparePrefabs(saved.prefabs, current.prefabs, issues);
            CompareSources(saved.sources, current.sources, issues);
            if (SerializeArray(saved.navigation) !=
                SerializeArray(current.navigation))
            {
                issues.Add("PanelRegistry navigation relationships changed.");
            }
            report = issues.Count == 0
                ? "Functional bindings match the pre-D2 baseline."
                : string.Join("\n", issues);
            return issues.Count == 0;
        }

        /// <summary>
        /// Compares every prefab binding and functional source hash without opening or
        /// changing the user's current scene. Used by strictly non-destructive dry runs.
        /// PanelRegistry relationships remain covered by the full Validate command.
        /// </summary>
        internal static bool CompareAssetsOnly(out string report)
        {
            D2BaselineData saved = Load();
            if (saved == null)
            {
                report = "D2 baseline is missing.";
                return false;
            }

            var issues = new List<string>();
            D2PrefabRecord[] prefabs = TargetPrefabs
                .Select(CapturePrefab)
                .ToArray();
            D2SourceRecord[] sources = FunctionalSources
                .Select(path => new D2SourceRecord
                {
                    path = path,
                    sha256 = Hash(path)
                }).ToArray();
            ComparePrefabs(saved.prefabs, prefabs, issues);
            CompareSources(saved.sources, sources, issues);
            report = issues.Count == 0
                ? "Prefab bindings and functional source hashes match the pre-D2 baseline."
                : string.Join("\n", issues);
            return issues.Count == 0;
        }

        internal static bool Restore(out string report)
        {
            D2BaselineData data = Load();
            if (data == null || data.backups == null || data.backups.Length == 0)
            {
                report = "D2 backup manifest is missing.";
                return false;
            }

            var restored = new List<string>();
            var failures = new List<string>();
            foreach (D2BackupRecord record in data.backups)
            {
                if (record == null || !File.Exists(record.backup) ||
                    Hash(record.backup) != record.sha256)
                {
                    failures.Add(record != null ? record.source : "<invalid record>");
                    continue;
                }

                try
                {
                    File.Copy(record.backup, record.source, true);
                    AssetDatabase.ImportAsset(
                        record.source,
                        ImportAssetOptions.ForceSynchronousImport |
                        ImportAssetOptions.ForceUpdate);
                    restored.Add(record.source);
                }
                catch (Exception exception)
                {
                    failures.Add(record.source + ": " + exception.Message);
                }
            }

            report = "Restored:\n" + string.Join("\n", restored) +
                     "\n\nFailures:\n" +
                     (failures.Count == 0 ? "None" : string.Join("\n", failures));
            return failures.Count == 0;
        }

        private static D2BaselineData CaptureCurrent()
        {
            D2PrefabRecord[] prefabs = TargetPrefabs
                .Select(CapturePrefab)
                .ToArray();
            D2SourceRecord[] sources = FunctionalSources
                .Select(path => new D2SourceRecord { path = path, sha256 = Hash(path) })
                .ToArray();

            EditorSceneManager.OpenScene(BootstrapPath, OpenSceneMode.Single);
            PanelRegistry registry =
                UnityEngine.Object.FindFirstObjectByType<PanelRegistry>();
            D2NavigationRecord[] navigation = registry != null
                ? registry.Entries.Where(entry => entry != null)
                    .Select(entry => new D2NavigationRecord
                    {
                        panelType = (int)entry.panelType,
                        windowId = (int)entry.windowId,
                        behaviour = (int)entry.behaviour,
                        prefabIdentity = Identity(entry.prefab)
                    })
                    .OrderBy(entry => entry.panelType)
                    .ToArray()
                : Array.Empty<D2NavigationRecord>();

            return new D2BaselineData
            {
                capturedUtc = DateTime.UtcNow.ToString("O"),
                prefabs = prefabs,
                sources = sources,
                navigation = navigation,
                backups = Array.Empty<D2BackupRecord>()
            };
        }

        private static D2PrefabRecord CapturePrefab(string path)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Transform[] transforms =
                prefab.GetComponentsInChildren<Transform>(true);
            D2ButtonRecord[] buttons = prefab
                .GetComponentsInChildren<Button>(true)
                .Select(button => new D2ButtonRecord
                {
                    path = HierarchyPath(button.transform, prefab.transform),
                    identity = Identity(button),
                    targetGraphicIdentity = Identity(button.targetGraphic),
                    listeners = Enumerable.Range(
                            0, button.onClick.GetPersistentEventCount())
                        .Select(index => new D2ListenerRecord
                        {
                            targetIdentity =
                                Identity(button.onClick.GetPersistentTarget(index)),
                            method = button.onClick.GetPersistentMethodName(index)
                        }).ToArray()
                }).OrderBy(record => record.path).ToArray();
            D2SpriteRecord[] sprites = prefab
                .GetComponentsInChildren<Image>(true)
                .Select(image => new D2SpriteRecord
                {
                    path = HierarchyPath(image.transform, prefab.transform),
                    imageIdentity = Identity(image),
                    spriteIdentity = Identity(image.sprite)
                }).OrderBy(record => record.path).ToArray();
            D2BackRecord[] backs = prefab
                .GetComponentsInChildren<PanelNavigationBinding>(true)
                .Select(binding => new D2BackRecord
                {
                    path = HierarchyPath(binding.transform, prefab.transform),
                    panelType = (int)binding.PanelType,
                    fallbackParent = (int)binding.FallbackParent,
                    buttonIdentity = Identity(binding.BackButton)
                }).ToArray();

            return new D2PrefabRecord
            {
                path = path,
                hierarchy = transforms
                    .Select(transform => HierarchyPath(transform, prefab.transform))
                    .OrderBy(value => value).ToArray(),
                buttons = buttons,
                sprites = sprites,
                backBindings = backs
            };
        }

        private static void ComparePrefabs(
            D2PrefabRecord[] expected,
            D2PrefabRecord[] current,
            List<string> issues)
        {
            foreach (D2PrefabRecord original in expected)
            {
                D2PrefabRecord now = current.FirstOrDefault(item => item.path == original.path);
                if (now == null)
                {
                    issues.Add("Missing prefab: " + original.path);
                    continue;
                }
                if (!original.hierarchy.SequenceEqual(now.hierarchy))
                    issues.Add("Hierarchy changed: " + original.path);
                if (SerializeArray(original.buttons) != SerializeArray(now.buttons))
                    issues.Add("Button target/listener changed: " + original.path);
                if (SerializeArray(original.sprites) != SerializeArray(now.sprites))
                    issues.Add("Sprite reference changed: " + original.path);
                if (SerializeArray(original.backBindings) !=
                    SerializeArray(now.backBindings))
                    issues.Add("Back relationship changed: " + original.path);
            }
        }

        private static void CompareSources(
            D2SourceRecord[] expected,
            D2SourceRecord[] current,
            List<string> issues)
        {
            foreach (D2SourceRecord original in expected)
            {
                D2SourceRecord now =
                    current.FirstOrDefault(item => item.path == original.path);
                if (now == null || now.sha256 != original.sha256)
                    issues.Add("Functional source changed: " + original.path);
            }
        }

        private static D2BaselineData Load()
        {
            return File.Exists(BaselinePath)
                ? JsonUtility.FromJson<D2BaselineData>(
                    File.ReadAllText(BaselinePath))
                : null;
        }

        private static string Identity(UnityEngine.Object target)
        {
            if (target == null) return string.Empty;
            return AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                target, out string guid, out long localId)
                ? guid + ":" + localId
                : target.GetType().FullName + ":" + target.name;
        }

        private static string HierarchyPath(Transform target, Transform root)
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

        private static string Hash(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                byte[] bytes = sha.ComputeHash(stream);
                var builder = new StringBuilder(bytes.Length * 2);
                for (int i = 0; i < bytes.Length; i++)
                    builder.Append(bytes[i].ToString("x2"));
                return builder.ToString();
            }
        }

        private static string SerializeArray<T>(T[] values)
        {
            return values == null
                ? string.Empty
                : string.Join("|", values.Select(value => JsonUtility.ToJson(value)));
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
