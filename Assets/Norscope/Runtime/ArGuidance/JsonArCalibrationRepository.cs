using System.IO;
using UnityEngine;

namespace Norscope.ArGuidance
{
    public interface IArCalibrationRepository
    {
        string EquipmentId { get; }
        string FilePath { get; }
        ArCalibrationFile LoadOrCreate();
        void Save(ArCalibrationFile file);
        bool TryGetComponent(string componentId, out ArComponentCalibration calibration);
        void SaveComponent(ArComponentCalibration calibration);
        void ResetComponent(string componentId);
    }

    public sealed class JsonArCalibrationRepository : IArCalibrationRepository
    {
        public const int SchemaVersion = 1;
        public const string DefaultEquipmentId = "dell_precision_3570";
        public const string DefaultMarkerPayload = "NORSCOPE:DELL3570:DEMO01";

        private readonly string _equipmentId;
        private readonly string _directory;
        private ArCalibrationFile _cache;

        public JsonArCalibrationRepository(string equipmentId = DefaultEquipmentId)
        {
            _equipmentId = string.IsNullOrWhiteSpace(equipmentId)
                ? DefaultEquipmentId
                : equipmentId.Trim();
            _directory = Path.Combine(Application.persistentDataPath, "ArCalibration");
        }

        public string EquipmentId => _equipmentId;
        public string FilePath => Path.Combine(_directory, _equipmentId + ".json");

        public ArCalibrationFile LoadOrCreate()
        {
            if (_cache != null)
                return _cache;

            string path = FilePath;
            if (!File.Exists(path))
            {
                _cache = CreateDefaults();
                Save(_cache);
                return _cache;
            }

            try
            {
                string json = File.ReadAllText(path);
                ArCalibrationFile loaded = JsonUtility.FromJson<ArCalibrationFile>(json);
                if (loaded == null || loaded.components == null)
                    throw new InvalidDataException("Calibration JSON produced no data.");
                Sanitize(loaded);
                _cache = loaded;
                return _cache;
            }
            catch (System.Exception ex)
            {
                string malformed = path + ".malformed-" +
                                   System.DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                try { File.Move(path, malformed); }
                catch { /* continue */ }

                Debug.LogWarning(
                    "[AR Guidance] Malformed calibration recovered with defaults. " +
                    ex.Message);
                _cache = CreateDefaults();
                Save(_cache);
                return _cache;
            }
        }

        public void Save(ArCalibrationFile file)
        {
            if (file == null)
                return;
            Sanitize(file);
            Directory.CreateDirectory(_directory);
            string path = FilePath;
            string tmp = path + ".tmp";
            string json = JsonUtility.ToJson(file, true);
            File.WriteAllText(tmp, json);
            try
            {
                if (File.Exists(path))
                {
                    string bak = path + ".previous";
                    File.Replace(tmp, path, bak, true);
                    if (File.Exists(bak))
                        File.Delete(bak);
                }
                else
                {
                    File.Move(tmp, path);
                }
            }
            catch
            {
                File.Copy(tmp, path, true);
                if (File.Exists(tmp))
                    File.Delete(tmp);
            }

            _cache = file;
        }

        public bool TryGetComponent(string componentId, out ArComponentCalibration calibration)
        {
            calibration = null;
            ArCalibrationFile file = LoadOrCreate();
            if (file.components == null)
                return false;
            for (int i = 0; i < file.components.Length; i++)
            {
                if (file.components[i] != null &&
                    file.components[i].componentId == componentId)
                {
                    calibration = file.components[i];
                    return true;
                }
            }

            return false;
        }

        public void SaveComponent(ArComponentCalibration calibration)
        {
            if (calibration == null || string.IsNullOrEmpty(calibration.componentId))
                return;
            calibration.Sanitize();
            calibration.updatedUtc = System.DateTime.UtcNow.ToString("o");

            ArCalibrationFile file = LoadOrCreate();
            int index = IndexOf(file, calibration.componentId);
            if (index >= 0)
            {
                file.components[index] = calibration;
            }
            else
            {
                var list = new System.Collections.Generic.List<ArComponentCalibration>(
                    file.components ?? System.Array.Empty<ArComponentCalibration>())
                {
                    calibration
                };
                file.components = list.ToArray();
            }

            Save(file);
        }

        public void ResetComponent(string componentId)
        {
            if (string.IsNullOrEmpty(componentId))
                return;
            ArCalibrationFile file = LoadOrCreate();
            int index = IndexOf(file, componentId);
            if (index < 0)
                return;

            ArComponentCalibration defaults = CreateDefaultComponent(componentId);
            file.components[index] = defaults;
            Save(file);
        }

        private static int IndexOf(ArCalibrationFile file, string componentId)
        {
            if (file?.components == null)
                return -1;
            for (int i = 0; i < file.components.Length; i++)
            {
                if (file.components[i] != null &&
                    file.components[i].componentId == componentId)
                    return i;
            }

            return -1;
        }

        public static ArCalibrationFile CreateDefaults()
        {
            var file = new ArCalibrationFile
            {
                schemaVersion = SchemaVersion,
                equipmentId = DefaultEquipmentId,
                trackingProviderType = ManualTestOriginProvider.ProviderType,
                markerPayload = DefaultMarkerPayload,
                components = new[]
                {
                    CreateDefaultComponent("laptop_outline", ArVisualType.LaptopOutline),
                    CreateDefaultComponent("bottom_cover_screw_group", ArVisualType.ScrewGroup),
                    CreateDefaultComponent("lift_edge", ArVisualType.LiftEdge,
                        new Vector3(0f, 0f, -0.10f)),
                    CreateDefaultComponent("battery_connector", ArVisualType.ConnectorWarning,
                        new Vector3(0.08f, 0f, 0.04f)),
                    CreateDefaultComponent("cooling_fan", ArVisualType.FanHighlight,
                        new Vector3(-0.06f, 0f, 0.02f)),
                    CreateDefaultComponent("fan_airflow", ArVisualType.Airflow,
                        new Vector3(-0.06f, 0.01f, 0.02f))
                }
            };
            return file;
        }

        public static ArComponentCalibration CreateDefaultComponent(
            string componentId,
            ArVisualType visualType = ArVisualType.None,
            Vector3? localPosition = null)
        {
            if (visualType == ArVisualType.None)
                visualType = InferVisualType(componentId);

            return new ArComponentCalibration
            {
                componentId = componentId,
                visualType = visualType,
                localPosition = localPosition ?? Vector3.zero,
                localRotation = Quaternion.identity,
                localScale = Vector3.one,
                updatedUtc = string.Empty
            };
        }

        private static ArVisualType InferVisualType(string componentId)
        {
            switch (componentId)
            {
                case "laptop_outline": return ArVisualType.LaptopOutline;
                case "bottom_cover_screw_group": return ArVisualType.ScrewGroup;
                case "lift_edge": return ArVisualType.LiftEdge;
                case "battery_connector": return ArVisualType.ConnectorWarning;
                case "cooling_fan": return ArVisualType.FanHighlight;
                case "fan_airflow": return ArVisualType.Airflow;
                default: return ArVisualType.None;
            }
        }

        private static void Sanitize(ArCalibrationFile file)
        {
            if (file.schemaVersion <= 0)
                file.schemaVersion = SchemaVersion;
            if (string.IsNullOrEmpty(file.equipmentId))
                file.equipmentId = DefaultEquipmentId;
            if (string.IsNullOrEmpty(file.trackingProviderType))
                file.trackingProviderType = ManualTestOriginProvider.ProviderType;
            if (string.IsNullOrEmpty(file.markerPayload))
                file.markerPayload = DefaultMarkerPayload;
            if (file.components == null)
                file.components = System.Array.Empty<ArComponentCalibration>();
            for (int i = 0; i < file.components.Length; i++)
                file.components[i]?.Sanitize();
        }
    }
}
