using TMPro;
using UnityEngine;

namespace Norscope.Localization
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    public sealed class LocalizedText : MonoBehaviour
    {
        [SerializeField] private string localizationKey = string.Empty;
        private TMP_Text _target;

        public string Key => localizationKey;
        public void Configure(string key)
        {
            localizationKey = key;
            if (Application.isPlaying) Refresh();
        }

        private void Awake() { _target = GetComponent<TMP_Text>(); }
        private void OnEnable()
        {
            LocalizationService.LanguageChanged -= Refresh;
            LocalizationService.LanguageChanged += Refresh;
            Refresh();
        }
        private void OnDisable() { LocalizationService.LanguageChanged -= Refresh; }
        public void Refresh()
        {
            if (_target == null) _target = GetComponent<TMP_Text>();
            if (_target != null && !string.IsNullOrWhiteSpace(localizationKey))
                _target.text = LocalizationService.Get(localizationKey);
        }
    }
}
