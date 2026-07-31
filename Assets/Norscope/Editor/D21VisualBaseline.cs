using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Norscope.Editor
{
    [Serializable]
    internal sealed class D21BackupManifest
    {
        public string capturedUtc;
        public D21BackupEntry[] entries;
    }

    [Serializable]
    internal sealed class D21BackupEntry
    {
        public string source;
        public string backup;
        public string sha256;
    }

    internal static class D21VisualBaseline
    {
        internal const string ManifestPath =
            "Assets/Norscope/Editor/Validation/D21VisualBaseline.json";
        private const string BackupRoot =
            "Assets/Norscope/DesignSystem/Backups";

        internal static readonly string[] VisualAssets =
        {
            "Assets/Norscope/UI/Panels/HomePanel.prefab",
            "Assets/Norscope/UI/Panels/TaskLibraryPanel.prefab",
            "Assets/Norscope/UI/Panels/ActiveTaskPanel.prefab",
            "Assets/Norscope/UI/Panels/TaskCompletionPanel.prefab",
            "Assets/Norscope/UI/Controls/TaskCard.prefab",
            "Assets/Norscope/UI/Controls/NavigationBackButton.prefab",
            "Assets/Norscope/UI/Controls/NorscopeCloseButton.prefab",
            "Assets/Norscope/UI/Controls/NorscopePrimaryButton.prefab",
            "Assets/Norscope/UI/Controls/NorscopeSecondaryButton.prefab",
            "Assets/Norscope/DesignSystem/Prefabs/NorscopeInteractionVisualLayer.prefab",
            "Assets/Norscope/DesignSystem/Profiles/NorscopeDesignProfile.asset",
            "Assets/Norscope/UI/DesignSystem/Themes/NorscopeTheme.asset"
        };

        internal static bool Capture(out string report)
        {
            if (File.Exists(ManifestPath))
            {
                report = "The immutable pre-D2.1 manifest already exists; it was not overwritten.";
                return true;
            }

            string[] missing = VisualAssets.Where(path => !File.Exists(path)).ToArray();
            if (missing.Length > 0)
            {
                report = "Missing required visual assets:\n" + string.Join("\n", missing);
                return false;
            }

            if (!D2VisualBaseline.CompareAssetsOnly(out string functionalReport))
            {
                report = "Functional baseline is not clean:\n" + functionalReport;
                return false;
            }

            string folder = BackupRoot + "/PreD21_" +
                            DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            EnsureFolder(BackupRoot);
            EnsureFolder(folder);
            var entries = new List<D21BackupEntry>();

            foreach (string source in VisualAssets)
            {
                string backup = folder + "/" + Path.GetFileName(source);
                if (!AssetDatabase.CopyAsset(source, backup))
                {
                    report = "Could not back up " + source;
                    return false;
                }

                string hash = Hash(backup);
                if (hash != Hash(source))
                {
                    report = "Backup verification failed for " + source;
                    return false;
                }
                entries.Add(new D21BackupEntry
                {
                    source = source,
                    backup = backup,
                    sha256 = hash
                });
            }

            var manifest = new D21BackupManifest
            {
                capturedUtc = DateTime.UtcNow.ToString("O"),
                entries = entries.ToArray()
            };
            Directory.CreateDirectory(Path.GetDirectoryName(ManifestPath));
            File.WriteAllText(ManifestPath, JsonUtility.ToJson(manifest, true));
            AssetDatabase.ImportAsset(ManifestPath, ImportAssetOptions.ForceUpdate);
            report = "Captured and verified " + entries.Count +
                     " visual assets. Functional D2 baseline is clean.";
            return true;
        }

        internal static bool Restore(out string report)
        {
            D21BackupManifest manifest = Load();
            if (manifest == null || manifest.entries == null)
            {
                report = "Pre-D2.1 backup manifest is missing.";
                return false;
            }

            var restored = new List<string>();
            var skipped = new List<string>();
            foreach (D21BackupEntry entry in manifest.entries)
            {
                if (entry == null || !File.Exists(entry.backup) ||
                    Hash(entry.backup) != entry.sha256)
                {
                    skipped.Add(entry != null ? entry.source : "<invalid entry>");
                    continue;
                }

                File.Copy(entry.backup, entry.source, true);
                AssetDatabase.ImportAsset(entry.source,
                    ImportAssetOptions.ForceSynchronousImport |
                    ImportAssetOptions.ForceUpdate);
                restored.Add(entry.source);
            }

            report = "Timestamp: " + DateTime.UtcNow.ToString("O") +
                     "\n\nRestored:\n" + string.Join("\n", restored) +
                     "\n\nSkipped or missing:\n" +
                     (skipped.Count == 0 ? "None" : string.Join("\n", skipped));
            return skipped.Count == 0;
        }

        internal static bool Exists => File.Exists(ManifestPath);

        internal static string GetBackupFor(string source)
        {
            D21BackupManifest manifest = Load();
            D21BackupEntry entry = manifest?.entries?
                .FirstOrDefault(value => value != null && value.source == source);
            return entry != null ? entry.backup : string.Empty;
        }

        private static D21BackupManifest Load()
        {
            return File.Exists(ManifestPath)
                ? JsonUtility.FromJson<D21BackupManifest>(
                    File.ReadAllText(ManifestPath))
                : null;
        }

        private static string Hash(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                byte[] bytes = sha.ComputeHash(stream);
                var builder = new StringBuilder(bytes.Length * 2);
                foreach (byte value in bytes) builder.Append(value.ToString("x2"));
                return builder.ToString();
            }
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
