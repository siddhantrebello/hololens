using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace Norscope.Settings
{
    /// <summary>
    /// Versioned JSON persistence with safe defaults and best-effort atomic writes.
    /// This class is independent from UI and is directly testable with temporary paths.
    /// </summary>
    public static class NorscopeSettingsPersistence
    {
        public static NorscopeSettingsData LoadOrCreate(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Settings path must not be empty.", nameof(path));

            if (!File.Exists(path))
            {
                NorscopeSettingsData defaults = CreateDefaults();
                Save(path, defaults);
                return defaults;
            }

            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                NorscopeSettingsData loaded =
                    JsonUtility.FromJson<NorscopeSettingsData>(json);
                if (loaded == null)
                    throw new InvalidDataException("Settings JSON produced no data.");

                MigrateAndSanitize(loaded);
                return loaded;
            }
            catch (Exception exception)
            {
                string malformedPath =
                    path + ".malformed-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                try
                {
                    File.Move(path, malformedPath);
                }
                catch
                {
                    // Recovery must continue even if the damaged file cannot be moved.
                }

                Debug.LogWarning(
                    "[SETTINGS] Malformed settings recovered with safe defaults. " +
                    exception.Message);
                NorscopeSettingsData defaults = CreateDefaults();
                Save(path, defaults);
                return defaults;
            }
        }

        public static void Save(string path, NorscopeSettingsData data)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Settings path must not be empty.", nameof(path));
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            data.Sanitize();
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            string temporaryPath = path + ".tmp";
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(temporaryPath, json, new UTF8Encoding(false));

            try
            {
                if (File.Exists(path))
                {
                    string backupPath = path + ".previous";
                    File.Replace(temporaryPath, path, backupPath, true);
                    if (File.Exists(backupPath))
                        File.Delete(backupPath);
                }
                else
                {
                    File.Move(temporaryPath, path);
                }
            }
            catch
            {
                File.Copy(temporaryPath, path, true);
                File.Delete(temporaryPath);
            }
        }

        public static NorscopeSettingsData CreateDefaults()
        {
            return new NorscopeSettingsData();
        }

        private static void MigrateAndSanitize(NorscopeSettingsData data)
        {
            // Schema 1 is the initial additive schema. Future migrations belong here.
            if (data.schemaVersion <= 0)
                data.schemaVersion = 1;
            data.Sanitize();
        }
    }
}
