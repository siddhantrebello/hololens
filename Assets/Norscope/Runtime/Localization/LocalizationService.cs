using System;
using Norscope.Settings;
using UnityEngine;

namespace Norscope.Localization
{
    [DefaultExecutionOrder(-9000)]
    [DisallowMultipleComponent]
    public sealed class LocalizationService : MonoBehaviour
    {
        public static LocalizationService Instance { get; private set; }
        [SerializeField] private LocalizationTable englishTable;
        [SerializeField] private LocalizationTable germanTable;
        public static event Action LanguageChanged;

        public NorscopeLanguage CurrentLanguage =>
            NorscopeSettingsService.Instance != null
                ? NorscopeSettingsService.Instance.Current.language
                : NorscopeLanguage.English;

        public void Configure(LocalizationTable english, LocalizationTable german)
        {
            englishTable = english;
            germanTable = german;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            if (NorscopeSettingsService.Instance != null)
                NorscopeSettingsService.Instance.LanguageChanged += OnSettingsLanguageChanged;
        }

        private void OnDestroy()
        {
            if (NorscopeSettingsService.Instance != null)
                NorscopeSettingsService.Instance.LanguageChanged -= OnSettingsLanguageChanged;
            if (Instance == this) Instance = null;
        }

        private static void OnSettingsLanguageChanged(NorscopeLanguage language)
        {
            LanguageChanged?.Invoke();
        }

        public static string Get(string key, params object[] args)
        {
            string value = Instance != null ? Instance.Resolve(key) : key;
            return args != null && args.Length > 0 ? string.Format(value, args) : value;
        }

        private string Resolve(string key)
        {
            string value;
            if (CurrentLanguage == NorscopeLanguage.German &&
                germanTable != null && germanTable.TryGet(key, out value) &&
                !string.IsNullOrEmpty(value))
                return value;
            if (englishTable != null && englishTable.TryGet(key, out value))
                return value;
            return key;
        }
    }
}
