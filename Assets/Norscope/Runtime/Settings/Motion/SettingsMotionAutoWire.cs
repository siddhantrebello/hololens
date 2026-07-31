using UnityEngine;
using UnityEngine.UI;

namespace Norscope.Settings.Motion
{
    /// <summary>
    /// Ensures Settings motion components exist and are wired under FigmaContent only.
    /// Safe to call repeatedly; never touches TitleBar / Grab / Resize / Close.
    /// </summary>
    public static class SettingsMotionAutoWire
    {
        public static void Ensure(SettingsViewNavigator navigator)
        {
            if (navigator == null) return;
            Transform root = navigator.transform;

            Transform settingsView = Find(root, "FigmaContent/SettingsView");
            Transform languageView = Find(root, "FigmaContent/LanguageView");
            Transform languageQuick = Find(root,
                "FigmaContent/SettingsView/Body/Detail/LanguageQuickView");
            Transform audioView = Find(root,
                "FigmaContent/SettingsView/Body/Detail/AudioView");
            Transform aboutView = Find(root,
                "FigmaContent/SettingsView/Body/Detail/AboutView");
            if (settingsView == null || languageView == null ||
                languageQuick == null || audioView == null || aboutView == null)
                return;

            SettingsPageTransition settingsPt = EnsurePage(settingsView.gameObject);
            SettingsPageTransition languagePt = EnsurePage(languageView.gameObject);
            SettingsPageTransition quickPt = EnsurePage(languageQuick.gameObject);
            SettingsPageTransition audioPt = EnsurePage(audioView.gameObject);
            SettingsPageTransition aboutPt = EnsurePage(aboutView.gameObject);

            SettingsTransitionController controller =
                root.GetComponent<SettingsTransitionController>();
            if (controller == null)
                controller = root.gameObject.AddComponent<SettingsTransitionController>();
            controller.Configure(settingsPt, languagePt, quickPt, audioPt, aboutPt);

            SettingsButtonVisualState langVisual = EnsureButton(
                Find(root, "FigmaContent/SettingsView/Body/Sidebar/LanguageButton"),
                SettingsButtonVisualState.Role.Navigation);
            SettingsButtonVisualState audioVisual = EnsureButton(
                Find(root, "FigmaContent/SettingsView/Body/Sidebar/AudioButton"),
                SettingsButtonVisualState.Role.Navigation);
            SettingsButtonVisualState aboutVisual = EnsureButton(
                Find(root, "FigmaContent/SettingsView/Body/Sidebar/AboutButton"),
                SettingsButtonVisualState.Role.Navigation);

            EnsureButton(
                Find(root,
                    "FigmaContent/SettingsView/Body/Detail/LanguageQuickView/ChangeLanguageButton"),
                SettingsButtonVisualState.Role.Generic);
            EnsureButton(
                Find(root, "FigmaContent/LanguageView/TopBar/InternalBackButton"),
                SettingsButtonVisualState.Role.Generic);

            Transform volumeCard = Find(root,
                "FigmaContent/SettingsView/Body/Detail/AudioView/MasterVolumeCard");
            SettingsVolumeFillVisual fill = null;
            if (volumeCard != null)
            {
                fill = EnsureFill(volumeCard);
                EnsureButton(volumeCard.Find("DecreaseButton"),
                    SettingsButtonVisualState.Role.PlusMinus);
                EnsureButton(volumeCard.Find("IncreaseButton"),
                    SettingsButtonVisualState.Role.PlusMinus);
            }

            Transform alert = Find(root,
                "FigmaContent/SettingsView/Body/Detail/AudioView/AlertSoundsToggle");
            SettingsToggleThumbVisual toggle = EnsureToggle(alert);

            navigator.ConfigureMotion(controller, langVisual, audioVisual, aboutVisual);

            SettingsAudioController audio =
                root.GetComponentInChildren<SettingsAudioController>(true);
            if (audio != null)
                audio.ConfigureVisuals(fill, toggle);
        }

        private static SettingsPageTransition EnsurePage(GameObject go)
        {
            CanvasGroup cg = go.GetComponent<CanvasGroup>();
            if (cg == null) cg = go.AddComponent<CanvasGroup>();
            SettingsPageTransition pt = go.GetComponent<SettingsPageTransition>();
            if (pt == null) pt = go.AddComponent<SettingsPageTransition>();
            pt.Configure(cg, go.transform as RectTransform);
            return pt;
        }

        private static SettingsButtonVisualState EnsureButton(
            Transform target, SettingsButtonVisualState.Role role)
        {
            if (target == null) return null;
            Button button = target.GetComponent<Button>();
            Image image = target.GetComponent<Image>();
            CanvasGroup cg = target.GetComponent<CanvasGroup>();
            if (cg == null) cg = target.gameObject.AddComponent<CanvasGroup>();
            SettingsButtonVisualState visual =
                target.GetComponent<SettingsButtonVisualState>();
            if (visual == null)
                visual = target.gameObject.AddComponent<SettingsButtonVisualState>();

            float pressScale = role == SettingsButtonVisualState.Role.PlusMinus ? 0.90f : 0.982f;
            float pressDur = role == SettingsButtonVisualState.Role.PlusMinus ? 0.12f : 0.14f;
            Graphic[] graphics = image != null ? new Graphic[] { image } : System.Array.Empty<Graphic>();
            visual.Configure(role, button, cg, graphics, pressScale, pressDur);
            if (button != null)
                button.transition = Selectable.Transition.None;
            return visual;
        }

        private static SettingsVolumeFillVisual EnsureFill(Transform volumeCard)
        {
            Transform track = volumeCard.Find("Track");
            if (track == null) return null;
            Image image = track.GetComponent<Image>();
            if (image == null) return null;
            SettingsVolumeFillVisual visual =
                volumeCard.GetComponent<SettingsVolumeFillVisual>();
            if (visual == null)
                visual = volumeCard.gameObject.AddComponent<SettingsVolumeFillVisual>();
            visual.Configure(image, 0.25f);
            return visual;
        }

        private static SettingsToggleThumbVisual EnsureToggle(Transform alertToggle)
        {
            if (alertToggle == null) return null;
            Transform track = alertToggle.Find("ToggleTrack");
            if (track == null) return null;
            Image trackImage = track.GetComponent<Image>();
            Transform thumb = track.Find("Thumb");
            if (thumb == null)
            {
                var go = new GameObject("Thumb", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                thumb = go.transform;
                thumb.SetParent(track, false);
                var rt = (RectTransform)thumb;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(14f, 14f);
                rt.anchoredPosition = new Vector2(-10f, 0f);
                var img = go.GetComponent<Image>();
                img.color = Color.white;
                img.raycastTarget = false;
            }

            SettingsToggleThumbVisual visual =
                alertToggle.GetComponent<SettingsToggleThumbVisual>();
            if (visual == null)
                visual = alertToggle.gameObject.AddComponent<SettingsToggleThumbVisual>();
            visual.Configure(
                thumb as RectTransform,
                trackImage,
                -10f,
                10f,
                new Color(0.176f, 0.231f, 0.286f, 1f),
                new Color(0.118f, 0.784f, 0.961f, 1f));
            return visual;
        }

        private static Transform Find(Transform root, string path)
        {
            if (root == null || string.IsNullOrEmpty(path)) return null;
            Transform t = root.Find(path);
            return t;
        }
    }
}
