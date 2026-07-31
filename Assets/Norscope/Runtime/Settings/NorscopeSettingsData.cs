using System;

namespace Norscope.Settings
{
    /// <summary>
    /// Versioned settings payload. Phase 2A reserves approved future values but
    /// exposes no UI that can mutate them before their implementation phase.
    /// Unsupported voice, microphone and brightness fields are intentionally absent.
    /// </summary>
    [Serializable]
    public sealed class NorscopeSettingsData
    {
        public int schemaVersion = 1;
        public NorscopeLanguage language = NorscopeLanguage.English;
        public int masterVolume = 100;
        public bool alertSoundsEnabled = true;
        public NorscopeTextSize textSize = NorscopeTextSize.Medium;
        public SettingsInternalView lastSettingsView = SettingsInternalView.Language;

        public NorscopeSettingsData Copy()
        {
            return new NorscopeSettingsData
            {
                schemaVersion = schemaVersion,
                language = language,
                masterVolume = masterVolume,
                alertSoundsEnabled = alertSoundsEnabled,
                textSize = textSize,
                lastSettingsView = lastSettingsView
            };
        }

        public void Sanitize()
        {
            schemaVersion = 1;
            masterVolume = Math.Max(0, Math.Min(100, masterVolume));
            if (!Enum.IsDefined(typeof(NorscopeLanguage), language))
                language = NorscopeLanguage.English;
            if (!Enum.IsDefined(typeof(NorscopeTextSize), textSize))
                textSize = NorscopeTextSize.Medium;
            if (!Enum.IsDefined(typeof(SettingsInternalView), lastSettingsView))
                lastSettingsView = SettingsInternalView.Language;
        }
    }
}
