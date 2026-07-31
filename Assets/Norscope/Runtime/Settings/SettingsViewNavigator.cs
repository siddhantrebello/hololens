using Norscope.Settings.Motion;
using UnityEngine;
using UnityEngine.UI;

namespace Norscope.Settings
{
    /// <summary>
    /// Switches only prefab-authored Settings views. It does not create, position,
    /// resize or restyle UI. Optional transition controller owns content-root motion.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SettingsViewNavigator : MonoBehaviour
    {
        [SerializeField] private GameObject settingsView;
        [SerializeField] private GameObject languageView;
        [SerializeField] private GameObject languageQuickView;
        [SerializeField] private GameObject audioView;
        // Legacy serialized slots kept so older SettingsPanel.prefab versions still load.
        // Display UI is removed; these stay null and are never wired.
        [SerializeField] private GameObject displayView;
        [SerializeField] private GameObject aboutView;

        [SerializeField] private Button languageButton;
        [SerializeField] private Button languageQuickButton;
        [SerializeField] private Button audioButton;
        [SerializeField] private Button displayButton;
        [SerializeField] private Button aboutButton;
        [SerializeField] private Button languageInternalBackButton;

        [SerializeField] private SettingsTransitionController transitionController;
        [SerializeField] private SettingsButtonVisualState languageNavVisual;
        [SerializeField] private SettingsButtonVisualState audioNavVisual;
        [SerializeField] private SettingsButtonVisualState aboutNavVisual;

        private SettingsInternalView _previousSettingsView =
            SettingsInternalView.Language;
        private bool _ready;

        public void Configure(
            GameObject authoredSettingsView,
            GameObject authoredLanguageView,
            GameObject authoredLanguageQuickView,
            GameObject authoredAudioView,
            GameObject authoredAboutView,
            Button authoredLanguageButton,
            Button authoredLanguageQuickButton,
            Button authoredAudioButton,
            Button authoredAboutButton,
            Button authoredLanguageBackButton)
        {
            settingsView = authoredSettingsView;
            languageView = authoredLanguageView;
            languageQuickView = authoredLanguageQuickView;
            audioView = authoredAudioView;
            displayView = null;
            aboutView = authoredAboutView;
            languageButton = authoredLanguageButton;
            languageQuickButton = authoredLanguageQuickButton;
            audioButton = authoredAudioButton;
            displayButton = null;
            aboutButton = authoredAboutButton;
            languageInternalBackButton = authoredLanguageBackButton;
            WireButtons();
        }

        public void ConfigureMotion(
            SettingsTransitionController controller,
            SettingsButtonVisualState languageVisual,
            SettingsButtonVisualState audioVisual,
            SettingsButtonVisualState aboutVisual)
        {
            transitionController = controller;
            languageNavVisual = languageVisual;
            audioNavVisual = audioVisual;
            aboutNavVisual = aboutVisual;
        }

        private void Awake()
        {
            // Hide any leftover Display UI from older prefab revisions.
            if (displayButton != null)
                displayButton.gameObject.SetActive(false);
            if (displayView != null)
                displayView.SetActive(false);

            try
            {
                SettingsMotionAutoWire.Ensure(this);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning(
                    "[Settings] Motion auto-wire skipped so panel can still open: " +
                    ex.Message);
                transitionController = null;
            }

            WireButtons();
            SettingsInternalView initial = NorscopeSettingsService.Instance != null
                ? NorscopeSettingsService.Instance.Current.lastSettingsView
                : SettingsInternalView.Language;
            if (!System.Enum.IsDefined(typeof(SettingsInternalView), initial))
                initial = SettingsInternalView.Language;

            _previousSettingsView = initial;
            try
            {
                if (transitionController != null)
                    transitionController.SnapToSettingsDetail(initial);
                else
                    ApplySectionImmediate(initial);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning(
                    "[Settings] Transition snap failed; falling back to immediate views: " +
                    ex.Message);
                transitionController = null;
                ApplySectionImmediate(initial);
            }

            UpdateSelectedVisuals(initial);
            if (NorscopeSettingsService.Instance != null)
                NorscopeSettingsService.Instance.SetLastSettingsView(initial);
            _ready = true;
        }

        private void OnDestroy()
        {
            UnwireButtons();
        }

        public void OpenLanguagePanel()
        {
            if (transitionController != null)
            {
                transitionController.TransitionToLanguageShell(animate: _ready);
                return;
            }

            if (settingsView != null)
                settingsView.SetActive(false);
            if (languageView != null)
                languageView.SetActive(true);
        }

        public void BackToSettings()
        {
            if (transitionController != null)
            {
                transitionController.TransitionToSettingsDetail(
                    _previousSettingsView, animate: _ready);
                UpdateSelectedVisuals(_previousSettingsView);
                return;
            }

            if (languageView != null)
                languageView.SetActive(false);
            if (settingsView != null)
                settingsView.SetActive(true);
            ShowSection(_previousSettingsView);
        }

        public void ShowSection(SettingsInternalView view)
        {
            if (!System.Enum.IsDefined(typeof(SettingsInternalView), view))
                view = SettingsInternalView.Language;

            _previousSettingsView = view;
            if (NorscopeSettingsService.Instance != null)
                NorscopeSettingsService.Instance.SetLastSettingsView(view);

            if (transitionController != null)
                transitionController.TransitionToSettingsDetail(view, animate: _ready);
            else
                ApplySectionImmediate(view);

            UpdateSelectedVisuals(view);
        }

        private void ApplySectionImmediate(SettingsInternalView view)
        {
            if (settingsView != null)
                settingsView.SetActive(true);
            if (languageView != null)
                languageView.SetActive(false);

            SetActive(languageQuickView, view == SettingsInternalView.Language);
            SetActive(audioView, view == SettingsInternalView.Audio);
            SetActive(displayView, false);
            SetActive(aboutView, view == SettingsInternalView.About);
        }

        private void UpdateSelectedVisuals(SettingsInternalView view)
        {
            if (languageNavVisual != null)
                languageNavVisual.SetSelected(view == SettingsInternalView.Language);
            if (audioNavVisual != null)
                audioNavVisual.SetSelected(view == SettingsInternalView.Audio);
            if (aboutNavVisual != null)
                aboutNavVisual.SetSelected(view == SettingsInternalView.About);
        }

        private void WireButtons()
        {
            UnwireButtons();
            if (languageButton != null)
                languageButton.onClick.AddListener(OpenLanguagePanel);
            if (languageQuickButton != null)
                languageQuickButton.onClick.AddListener(OpenLanguagePanel);
            if (audioButton != null)
                audioButton.onClick.AddListener(ShowAudio);
            if (aboutButton != null)
                aboutButton.onClick.AddListener(ShowAbout);
            if (languageInternalBackButton != null)
                languageInternalBackButton.onClick.AddListener(BackToSettings);
        }

        private void UnwireButtons()
        {
            if (languageButton != null)
                languageButton.onClick.RemoveListener(OpenLanguagePanel);
            if (languageQuickButton != null)
                languageQuickButton.onClick.RemoveListener(OpenLanguagePanel);
            if (audioButton != null)
                audioButton.onClick.RemoveListener(ShowAudio);
            if (aboutButton != null)
                aboutButton.onClick.RemoveListener(ShowAbout);
            if (languageInternalBackButton != null)
                languageInternalBackButton.onClick.RemoveListener(BackToSettings);
        }

        private void ShowAudio() => ShowSection(SettingsInternalView.Audio);
        private void ShowAbout() => ShowSection(SettingsInternalView.About);

        private static void SetActive(GameObject value, bool active)
        {
            if (value != null)
                value.SetActive(active);
        }
    }
}
