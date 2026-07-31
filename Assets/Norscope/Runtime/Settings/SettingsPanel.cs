using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Norscope.Localization;

namespace Norscope.Settings
{
    /// <summary>
    /// Binds runtime metadata to the authored Settings prefab. Deferred controls
    /// remain visibly disabled and receive no value-changing listeners.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SettingsPanel : MonoBehaviour
    {
        [SerializeField] private SettingsViewNavigator viewNavigator;
        [SerializeField] private Selectable[] deferredControls =
            System.Array.Empty<Selectable>();

        [Header("About")]
        [SerializeField] private TMP_Text applicationValue;
        [SerializeField] private TMP_Text versionValue;
        [SerializeField] private TMP_Text buildValue;
        [SerializeField] private TMP_Text platformValue;
        [SerializeField] private TMP_Text unityValue;
        [FormerlySerializedAs("licenseValue")]
        [SerializeField] private TMP_Text companyValue;
        [SerializeField] private TMP_Text copyrightValue;

        [Header("Language")]
        [SerializeField] private Button englishLanguageButton;
        [SerializeField] private Button germanLanguageButton;

        public SettingsViewNavigator ViewNavigator => viewNavigator;

        public void Configure(
            SettingsViewNavigator navigator,
            Selectable[] authoredDeferredControls,
            TMP_Text authoredApplicationValue,
            TMP_Text authoredVersionValue,
            TMP_Text authoredBuildValue,
            TMP_Text authoredPlatformValue,
            TMP_Text authoredUnityValue,
            TMP_Text authoredCompanyValue,
            TMP_Text authoredCopyrightValue)
        {
            viewNavigator = navigator;
            deferredControls = authoredDeferredControls ??
                               System.Array.Empty<Selectable>();
            applicationValue = authoredApplicationValue;
            versionValue = authoredVersionValue;
            buildValue = authoredBuildValue;
            platformValue = authoredPlatformValue;
            unityValue = authoredUnityValue;
            companyValue = authoredCompanyValue;
            copyrightValue = authoredCopyrightValue;
            ApplyBindings();
        }

        private void Awake()
        {
            WireLanguageButtons();
            ApplyBindings();
        }

        private void OnDestroy()
        {
            if (englishLanguageButton != null)
                englishLanguageButton.onClick.RemoveListener(SelectEnglish);
            if (germanLanguageButton != null)
                germanLanguageButton.onClick.RemoveListener(SelectGerman);
            LocalizationService.LanguageChanged -= ApplyBindings;
        }

        private void WireLanguageButtons()
        {
            if (englishLanguageButton != null)
            {
                englishLanguageButton.onClick.RemoveListener(SelectEnglish);
                englishLanguageButton.onClick.AddListener(SelectEnglish);
            }
            if (germanLanguageButton != null)
            {
                germanLanguageButton.onClick.RemoveListener(SelectGerman);
                germanLanguageButton.onClick.AddListener(SelectGerman);
            }
            LocalizationService.LanguageChanged -= ApplyBindings;
            LocalizationService.LanguageChanged += ApplyBindings;
        }

        private void SelectEnglish()
        {
            if (NorscopeSettingsService.Instance != null)
                NorscopeSettingsService.Instance.SetLanguage(NorscopeLanguage.English);
        }

        private void SelectGerman()
        {
            if (NorscopeSettingsService.Instance != null)
                NorscopeSettingsService.Instance.SetLanguage(NorscopeLanguage.German);
        }

        private void ApplyBindings()
        {
            for (int i = 0; i < deferredControls.Length; i++)
            {
                if (deferredControls[i] != null)
                    deferredControls[i].interactable = false;
            }

            SetText(applicationValue, SettingsAboutBinder.ApplicationName);
            SetText(versionValue, SettingsAboutBinder.Version);
            SetText(buildValue, SettingsAboutBinder.Build);
            SetText(platformValue, SettingsAboutBinder.Platform);
            SetText(unityValue, SettingsAboutBinder.UnityVersion);
            SetText(companyValue, SettingsAboutBinder.Company);
            SetText(copyrightValue, SettingsAboutBinder.Copyright);
        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target != null)
                target.text = value;
        }
    }
}
