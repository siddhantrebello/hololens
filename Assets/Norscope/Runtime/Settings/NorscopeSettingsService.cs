using System;
using System.IO;
using UnityEngine;

namespace Norscope.Settings
{
    /// <summary>
    /// Single settings-state owner. It loads before ordinary panel scripts and
    /// persists only approved values. Phase 2A permits mutation of the optional
    /// internal Settings view; later phases will add typed mutators for their fields.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    [DisallowMultipleComponent]
    public sealed class NorscopeSettingsService : MonoBehaviour
    {
        public static NorscopeSettingsService Instance { get; private set; }

        [SerializeField] private NorscopeSettingsConfiguration configuration;

        private NorscopeSettingsData _current;
        private string _settingsPath;

        public event Action<SettingsInternalView> InternalViewChanged;
        public event Action<NorscopeLanguage> LanguageChanged;
        public event Action<int> MasterVolumeChanged;
        public event Action<bool> AlertSoundsChanged;

        public NorscopeSettingsData Current =>
            (_current ?? NorscopeSettingsPersistence.CreateDefaults()).Copy();
        public string SettingsPath => _settingsPath;
        public string LicenseName =>
            configuration != null ? configuration.LicenseName : "Enterprise";

        public void Configure(NorscopeSettingsConfiguration value)
        {
            configuration = value;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[SETTINGS] Duplicate NorscopeSettingsService destroyed.");
                Destroy(this);
                return;
            }

            Instance = this;
            string fileName = configuration != null
                ? configuration.SettingsFileName
                : "norscope-settings.json";
            _settingsPath = Path.Combine(Application.persistentDataPath, fileName);
            _current = NorscopeSettingsPersistence.LoadOrCreate(_settingsPath);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void SetLastSettingsView(SettingsInternalView view)
        {
            if (_current == null)
                return;
            if (_current.lastSettingsView == view)
                return;

            _current.lastSettingsView = view;
            NorscopeSettingsPersistence.Save(_settingsPath, _current);
            InternalViewChanged?.Invoke(view);
        }

        public void SetLanguage(NorscopeLanguage language)
        {
            if (_current == null || _current.language == language)
                return;
            _current.language = language;
            _current.Sanitize();
            NorscopeSettingsPersistence.Save(_settingsPath, _current);
            LanguageChanged?.Invoke(_current.language);
        }

        public void SetMasterVolume(int percent)
        {
            if (_current == null) return;
            percent = Mathf.Clamp(percent, 0, 100);
            if (_current.masterVolume == percent) return;
            _current.masterVolume = percent;
            NorscopeSettingsPersistence.Save(_settingsPath, _current);
            MasterVolumeChanged?.Invoke(percent);
        }

        public void SetAlertSoundsEnabled(bool enabled)
        {
            if (_current == null || _current.alertSoundsEnabled == enabled) return;
            _current.alertSoundsEnabled = enabled;
            NorscopeSettingsPersistence.Save(_settingsPath, _current);
            AlertSoundsChanged?.Invoke(enabled);
        }
    }
}
