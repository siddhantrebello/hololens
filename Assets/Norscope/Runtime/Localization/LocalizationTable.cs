using System;
using System.Collections.Generic;
using UnityEngine;

namespace Norscope.Localization
{
    [Serializable]
    public sealed class LocalizationEntry
    {
        public string key = string.Empty;
        [TextArea] public string value = string.Empty;
    }

    [CreateAssetMenu(fileName = "LocalizationTable", menuName = "Norscope/Localization/Table")]
    public sealed class LocalizationTable : ScriptableObject
    {
        [SerializeField] private string locale = "en";
        [SerializeField] private List<LocalizationEntry> entries = new List<LocalizationEntry>();
        private Dictionary<string, string> _lookup;

        public string Locale => locale;
        public IReadOnlyList<LocalizationEntry> Entries => entries;

        public void Configure(string localeCode, List<LocalizationEntry> values)
        {
            locale = localeCode;
            entries = values ?? new List<LocalizationEntry>();
            _lookup = null;
        }

        public bool TryGet(string key, out string value)
        {
            if (_lookup == null)
            {
                _lookup = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (LocalizationEntry entry in entries)
                    if (entry != null && !string.IsNullOrWhiteSpace(entry.key))
                        _lookup[entry.key] = entry.value ?? string.Empty;
            }
            return _lookup.TryGetValue(key, out value);
        }
    }
}
