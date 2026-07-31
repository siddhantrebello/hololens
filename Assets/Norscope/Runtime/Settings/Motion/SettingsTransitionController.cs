using UnityEngine;

namespace Norscope.Settings.Motion
{
    /// <summary>
    /// Orchestrates Settings content-root transitions. Never moves SpatialWindow chrome.
    /// Last requested target wins; interrupted animations end in a deterministic state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SettingsTransitionController : MonoBehaviour
    {
        private enum Shell
        {
            Settings,
            Language
        }

        [SerializeField] private SettingsPageTransition settingsContentRoot;
        [SerializeField] private SettingsPageTransition languageContentRoot;
        [SerializeField] private SettingsPageTransition languageQuickRoot;
        [SerializeField] private SettingsPageTransition audioRoot;
        [SerializeField] private SettingsPageTransition aboutRoot;
        [SerializeField] private bool respectDesignMotionGate = true;

        private Shell _currentShell = Shell.Settings;
        private SettingsInternalView _currentDetail = SettingsInternalView.Language;
        private int _requestId;
        private SettingsPageTransition _activeDetail;

        public SettingsInternalView CurrentDetail => _currentDetail;
        public bool IsOnLanguageShell => _currentShell == Shell.Language;

        public void Configure(
            SettingsPageTransition settingsContent,
            SettingsPageTransition languageContent,
            SettingsPageTransition languageQuick,
            SettingsPageTransition audio,
            SettingsPageTransition about)
        {
            settingsContentRoot = settingsContent;
            languageContentRoot = languageContent;
            languageQuickRoot = languageQuick;
            audioRoot = audio;
            aboutRoot = about;
        }

        private void OnDisable()
        {
            _requestId++;
            RestoreDeterministic(_currentShell, _currentDetail);
        }

        public void SnapToSettingsDetail(SettingsInternalView detail)
        {
            _requestId++;
            _currentShell = Shell.Settings;
            _currentDetail = Normalize(detail);
            RestoreDeterministic(Shell.Settings, _currentDetail);
        }

        public void SnapToLanguageShell()
        {
            _requestId++;
            _currentShell = Shell.Language;
            RestoreDeterministic(Shell.Language, _currentDetail);
        }

        public void TransitionToSettingsDetail(SettingsInternalView detail, bool animate)
        {
            detail = Normalize(detail);
            int request = ++_requestId;
            bool motion = animate && IsMotionEnabled();

            if (_currentShell == Shell.Language)
            {
                TransitionShell(Shell.Settings, detail, motion, request);
                return;
            }

            if (detail == _currentDetail)
            {
                SnapToSettingsDetail(detail);
                return;
            }

            TransitionDetail(detail, motion, request);
        }

        public void TransitionToLanguageShell(bool animate)
        {
            int request = ++_requestId;
            bool motion = animate && IsMotionEnabled();
            if (_currentShell == Shell.Language)
            {
                SnapToLanguageShell();
                return;
            }

            TransitionShell(Shell.Language, _currentDetail, motion, request);
        }

        private void TransitionDetail(SettingsInternalView detail, bool motion, int request)
        {
            SettingsPageTransition incoming = ResolveDetail(detail);
            SettingsPageTransition outgoing = _activeDetail ?? ResolveDetail(_currentDetail);
            _currentDetail = detail;
            _currentShell = Shell.Settings;

            if (outgoing != null && outgoing != incoming)
            {
                outgoing.PlayExit(
                    SettingsPageTransition.Mode.DetailFadeUp,
                    0,
                    motion,
                    () =>
                    {
                        if (request != _requestId) return;
                        outgoing.gameObject.SetActive(false);
                    });
            }

            if (incoming == null)
                return;

            _activeDetail = incoming;
            incoming.PlayEnter(
                SettingsPageTransition.Mode.DetailFadeUp,
                0,
                motion,
                () =>
                {
                    if (request != _requestId) return;
                    HideOtherDetails(incoming);
                });
        }

        private void TransitionShell(
            Shell shell,
            SettingsInternalView detail,
            bool motion,
            int request)
        {
            SettingsPageTransition outgoing =
                _currentShell == Shell.Language ? languageContentRoot : settingsContentRoot;
            SettingsPageTransition incoming =
                shell == Shell.Language ? languageContentRoot : settingsContentRoot;

            int enterDirection = shell == Shell.Language ? 1 : -1;
            int exitDirection = shell == Shell.Language ? -1 : 1;

            _currentShell = shell;
            _currentDetail = Normalize(detail);

            if (outgoing != null && outgoing != incoming)
            {
                outgoing.PlayExit(
                    SettingsPageTransition.Mode.HorizontalSlide,
                    exitDirection,
                    motion,
                    () =>
                    {
                        if (request != _requestId) return;
                        outgoing.SnapHidden();
                    });
            }

            if (incoming == null)
                return;

            if (shell == Shell.Settings)
                PrepareDetailImmediate(_currentDetail);

            incoming.PlayEnter(
                SettingsPageTransition.Mode.HorizontalSlide,
                enterDirection,
                motion,
                () =>
                {
                    if (request != _requestId) return;
                    if (shell == Shell.Language)
                        settingsContentRoot?.SnapHidden();
                    else
                        languageContentRoot?.SnapHidden();
                });
        }

        private void PrepareDetailImmediate(SettingsInternalView detail)
        {
            SettingsPageTransition target = ResolveDetail(detail);
            HideOtherDetails(target);
            if (target != null)
            {
                target.SnapVisible(true);
                _activeDetail = target;
            }
        }

        private void RestoreDeterministic(Shell shell, SettingsInternalView detail)
        {
            detail = Normalize(detail);
            if (shell == Shell.Language)
            {
                settingsContentRoot?.SnapHidden();
                if (languageContentRoot != null)
                {
                    languageContentRoot.gameObject.SetActive(true);
                    languageContentRoot.SnapVisible(true);
                }

                languageQuickRoot?.SnapHidden();
                audioRoot?.SnapHidden();
                aboutRoot?.SnapHidden();
                _activeDetail = null;
            }
            else
            {
                languageContentRoot?.SnapHidden();
                if (settingsContentRoot != null)
                {
                    settingsContentRoot.gameObject.SetActive(true);
                    settingsContentRoot.SnapVisible(true);
                }

                PrepareDetailImmediate(detail);
            }
        }

        private void HideOtherDetails(SettingsPageTransition keep)
        {
            if (languageQuickRoot != null && languageQuickRoot != keep)
                languageQuickRoot.SnapHidden();
            if (audioRoot != null && audioRoot != keep)
                audioRoot.SnapHidden();
            if (aboutRoot != null && aboutRoot != keep)
                aboutRoot.SnapHidden();
        }

        private SettingsPageTransition ResolveDetail(SettingsInternalView detail)
        {
            switch (detail)
            {
                case SettingsInternalView.Audio:
                    return audioRoot;
                case SettingsInternalView.About:
                    return aboutRoot;
                case SettingsInternalView.Language:
                default:
                    return languageQuickRoot;
            }
        }

        private static SettingsInternalView Normalize(SettingsInternalView view)
        {
            if (!System.Enum.IsDefined(typeof(SettingsInternalView), view))
                return SettingsInternalView.Language;
            return view;
        }

        private bool IsMotionEnabled()
        {
            if (!respectDesignMotionGate)
                return true;

            var provider = Norscope.DesignSystem.NorscopeDesignProvider.Instance;
            if (provider == null || provider.ActiveProfile == null)
                return true;
            return provider.ActiveProfile.features.enableMotion;
        }
    }
}
