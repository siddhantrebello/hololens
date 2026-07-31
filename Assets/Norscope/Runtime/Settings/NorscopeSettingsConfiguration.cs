using UnityEngine;

namespace Norscope.Settings
{
    [CreateAssetMenu(
        fileName = "NorscopeSettingsConfiguration",
        menuName = "Norscope/Settings/Configuration",
        order = 40)]
    public sealed class NorscopeSettingsConfiguration : ScriptableObject
    {
        [SerializeField] private string settingsFileName = "norscope-settings.json";
        [SerializeField] private string licenseName = "Enterprise";

        public string SettingsFileName =>
            string.IsNullOrWhiteSpace(settingsFileName)
                ? "norscope-settings.json"
                : settingsFileName;

        public string LicenseName =>
            string.IsNullOrWhiteSpace(licenseName) ? "Enterprise" : licenseName;
    }
}
