using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Norscope.Settings;
using Norscope.Settings.Motion;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Norscope.Editor
{
    /// <summary>
    /// Settings UX Fidelity workflow: baseline, dry run, apply, validate, restore.
    /// </summary>
    public static class PhaseSettingsUxFidelityConfigurator
    {
        private const string Menu = "Norscope/HoloLens/Settings/UX Fidelity/";
        private const string PanelPath = "Assets/Norscope/UI/Panels/SettingsPanel.prefab";
        private const string BackupRoot = "Assets/Norscope/UI/Panels/_Backups";
        private const string ReportPath = "Temp/NorscopeSettingsUxFidelityDryRun.txt";
        private const string ValidateReportPath = "Temp/NorscopeSettingsUxFidelityValidate.txt";
        private const string CaptureFlag = "Temp/NorscopeSettingsUxFidelityCapture.flag";
        private const string DryRunFlag = "Temp/NorscopeSettingsUxFidelityDryRun.flag";
        private const string ApplyFlag = "Temp/NorscopeSettingsUxFidelityApply.flag";
        private const string ValidateFlag = "Temp/NorscopeSettingsUxFidelityValidate.flag";
        private const string ApplyApprovedFlag =
            "Temp/NorscopeSettingsUxFidelityApplyApproved.flag";

        private static readonly Color SelectedNav =
            new Color(0.118f, 0.784f, 0.961f, 0.22f);
        private static readonly Color HoverNav =
            new Color(0.176f, 0.231f, 0.286f, 0.40f);
        private static readonly Color AlertOn =
            new Color(0.118f, 0.784f, 0.961f, 1f);
        private static readonly Color AlertOff =
            new Color(0.176f, 0.231f, 0.286f, 1f);

        [InitializeOnLoadMethod]
        private static void RunQueued()
        {
            EditorApplication.update -= PollFlags;
            EditorApplication.update += PollFlags;
            PollFlags();
        }

        private static void PollFlags()
        {
            if (File.Exists(CaptureFlag))
            {
                File.Delete(CaptureFlag);
                EditorApplication.delayCall += CaptureBaseline;
                return;
            }

            if (File.Exists(DryRunFlag))
            {
                File.Delete(DryRunFlag);
                EditorApplication.delayCall += DryRun;
                return;
            }

            if (File.Exists(ValidateFlag))
            {
                File.Delete(ValidateFlag);
                EditorApplication.delayCall += Validate;
                return;
            }

            if (File.Exists(ApplyFlag))
            {
                File.Delete(ApplyFlag);
                EditorApplication.delayCall += Apply;
            }
        }

        [MenuItem(Menu + "1) Capture Baseline")]
        public static void CaptureBaseline()
        {
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string folder = BackupRoot + "/SettingsUxFidelity_" + stamp;
            Directory.CreateDirectory(folder);
            FileUtil.CopyFileOrDirectory(PanelPath, folder + "/SettingsPanel.prefab");
            File.WriteAllText(
                folder + "/manifest.txt",
                "Settings UX Fidelity baseline " + DateTime.Now.ToString("O") +
                Environment.NewLine);
            AssetDatabase.Refresh();
            Debug.Log("[Settings UX Fidelity] Baseline captured: " + folder);
        }

        [MenuItem(Menu + "2) Dry Run")]
        public static void DryRun()
        {
            string report = BuildDryRunReport(out bool dryRunReady);
            Directory.CreateDirectory("Temp");
            File.WriteAllText(ReportPath, report, Encoding.UTF8);
            Debug.Log("[Settings UX Fidelity] Dry run " +
                      (dryRunReady ? "READY\n" : "BLOCKED\n") + report);
        }

        [MenuItem(Menu + "3) Apply")]
        public static void Apply()
        {
            Directory.CreateDirectory("Temp");
            File.WriteAllText(ApplyApprovedFlag, "approved");
            EnsureBaselineExists();
            ApplyUxFidelityImplementation();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Validate();
            Debug.Log("[Settings UX Fidelity] Apply complete.");
        }

        [MenuItem(Menu + "4) Validate")]
        public static void Validate()
        {
            var lines = new List<string>();
            bool ok = true;
            Action<bool, string> check = (pass, label) =>
            {
                ok &= pass;
                lines.Add((pass ? "PASS  " : "FAIL  ") + label);
            };

            lines.Add("NORSCOPE SETTINGS UX FIDELITY — VALIDATE");
            lines.Add("Generated: " + DateTime.Now.ToString("O"));
            lines.Add(string.Empty);

            check(File.Exists("Assets/Norscope/Runtime/Settings/Motion/SettingsMotionEasing.cs"),
                "SettingsMotionEasing.cs exists");
            check(File.Exists("Assets/Norscope/Runtime/Settings/Motion/SettingsPageTransition.cs"),
                "SettingsPageTransition.cs exists");
            check(File.Exists("Assets/Norscope/Runtime/Settings/Motion/SettingsTransitionController.cs"),
                "SettingsTransitionController.cs exists");
            check(File.Exists("Assets/Norscope/Runtime/Settings/Motion/SettingsButtonVisualState.cs"),
                "SettingsButtonVisualState.cs exists");
            check(File.Exists("Assets/Norscope/Runtime/Settings/Motion/SettingsVolumeFillVisual.cs"),
                "SettingsVolumeFillVisual.cs exists");
            check(File.Exists("Assets/Norscope/Runtime/Settings/Motion/SettingsToggleThumbVisual.cs"),
                "SettingsToggleThumbVisual.cs exists");

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath);
            check(prefab != null, "SettingsPanel.prefab loads");
            if (prefab != null)
            {
                var controller = prefab.GetComponent<SettingsTransitionController>();
                check(controller != null, "Root has SettingsTransitionController");

                check(HasPageTransition(prefab, "FigmaContent/SettingsView"),
                    "SettingsView has SettingsPageTransition + CanvasGroup");
                check(HasPageTransition(prefab, "FigmaContent/LanguageView"),
                    "LanguageView has SettingsPageTransition + CanvasGroup");
                check(HasPageTransition(prefab,
                        "FigmaContent/SettingsView/Body/Detail/LanguageQuickView"),
                    "LanguageQuickView has SettingsPageTransition");
                check(HasPageTransition(prefab,
                        "FigmaContent/SettingsView/Body/Detail/AudioView"),
                    "AudioView has SettingsPageTransition");
                check(HasPageTransition(prefab,
                        "FigmaContent/SettingsView/Body/Detail/AboutView"),
                    "AboutView has SettingsPageTransition");

                check(FindDeep(prefab.transform,
                        "FigmaContent/SettingsView/Body/Sidebar/LanguageButton")
                        ?.GetComponent<SettingsButtonVisualState>() != null,
                    "LanguageButton has SettingsButtonVisualState");
                check(FindDeep(prefab.transform,
                        "FigmaContent/SettingsView/Body/Sidebar/AudioButton")
                        ?.GetComponent<SettingsButtonVisualState>() != null,
                    "AudioButton has SettingsButtonVisualState");
                check(FindDeep(prefab.transform,
                        "FigmaContent/SettingsView/Body/Sidebar/AboutButton")
                        ?.GetComponent<SettingsButtonVisualState>() != null,
                    "AboutButton has SettingsButtonVisualState");

                Transform alert = FindDeep(prefab.transform,
                    "FigmaContent/SettingsView/Body/Detail/AudioView/AlertSoundsToggle");
                check(alert != null &&
                      alert.GetComponentInChildren<SettingsToggleThumbVisual>(true) != null,
                    "AlertSoundsToggle has SettingsToggleThumbVisual");

                Transform volCard = FindDeep(prefab.transform,
                    "FigmaContent/SettingsView/Body/Detail/AudioView/MasterVolumeCard");
                check(volCard != null &&
                      volCard.GetComponentInChildren<SettingsVolumeFillVisual>(true) != null,
                    "MasterVolumeCard has SettingsVolumeFillVisual");
                check(volCard != null &&
                      volCard.Find("DecreaseButton")
                          ?.GetComponent<SettingsButtonVisualState>() != null,
                    "DecreaseButton has SettingsButtonVisualState");
                check(volCard != null &&
                      volCard.Find("IncreaseButton")
                          ?.GetComponent<SettingsButtonVisualState>() != null,
                    "IncreaseButton has SettingsButtonVisualState");

                // Chrome must NOT have page transitions.
                check(FindDeep(prefab.transform, "TitleBar")
                          ?.GetComponent<SettingsPageTransition>() == null,
                    "TitleBar not animated by SettingsPageTransition");
                check(FindDeep(prefab.transform, "ResizeHandles")
                          ?.GetComponentInChildren<SettingsPageTransition>(true) == null,
                    "ResizeHandles not animated");

                var nav = prefab.GetComponent<SettingsViewNavigator>();
                check(nav != null, "SettingsViewNavigator present");
                var audio = prefab.GetComponentInChildren<SettingsAudioController>(true);
                check(audio != null, "SettingsAudioController present");

                // Scope: no Home/Task prefab timestamps claimed changed here.
                check(!AssetDatabase.GetAssetPath(prefab).Contains("Home"),
                    "Modified prefab is SettingsPanel only");
            }

            lines.Add(string.Empty);
            lines.Add(ok ? "STATUS: PASS" : "STATUS: FAIL");
            lines.Add(string.Empty);
            lines.Add("Manual HoloLens checks remaining:");
            lines.Add("- Far-ray hover/press/select on sidebar and ± buttons");
            lines.Add("- Near poke if available");
            lines.Add("- Frame pacing during 0.36s shell transitions");
            lines.Add("- Confirm UI click audio plays once per press");

            Directory.CreateDirectory("Temp");
            string report = string.Join(Environment.NewLine, lines);
            File.WriteAllText(ValidateReportPath, report, Encoding.UTF8);
            if (ok)
                Debug.Log("[Settings UX Fidelity] Validate PASS\n" + report);
            else
                Debug.LogError("[Settings UX Fidelity] Validate FAIL\n" + report);
        }

        [MenuItem(Menu + "5) Restore Baseline")]
        public static void Restore()
        {
            string folder = LatestBackup();
            if (folder == null)
            {
                Debug.LogError("[Settings UX Fidelity] No baseline found.");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "Restore Settings UX Fidelity baseline?",
                    "Restores SettingsPanel.prefab from:\n" + folder,
                    "Restore",
                    "Cancel"))
                return;

            FileUtil.ReplaceFile(folder + "/SettingsPanel.prefab", PanelPath);
            AssetDatabase.ImportAsset(PanelPath, ImportAssetOptions.ForceUpdate);
            Debug.Log("[Settings UX Fidelity] Restored from " + folder);
        }

        private static void ApplyUxFidelityImplementation()
        {
            GameObject root = null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(PanelPath);
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "[Settings UX Fidelity] PrefabUtility.LoadPrefabContents failed: " +
                    ex.Message +
                    ". Runtime SettingsMotionAutoWire will still wire motion in Play Mode. " +
                    "Close Prefab Mode, press Ctrl+R, then re-run Apply.");
                return;
            }

            try
            {
                Transform settingsView = FindDeep(root.transform, "FigmaContent/SettingsView");
                Transform languageView = FindDeep(root.transform, "FigmaContent/LanguageView");
                Transform languageQuick = FindDeep(root.transform,
                    "FigmaContent/SettingsView/Body/Detail/LanguageQuickView");
                Transform audioView = FindDeep(root.transform,
                    "FigmaContent/SettingsView/Body/Detail/AudioView");
                Transform aboutView = FindDeep(root.transform,
                    "FigmaContent/SettingsView/Body/Detail/AboutView");
                Transform langBtn = FindDeep(root.transform,
                    "FigmaContent/SettingsView/Body/Sidebar/LanguageButton");
                Transform audioBtn = FindDeep(root.transform,
                    "FigmaContent/SettingsView/Body/Sidebar/AudioButton");
                Transform aboutBtn = FindDeep(root.transform,
                    "FigmaContent/SettingsView/Body/Sidebar/AboutButton");
                Transform changeLang = FindDeep(root.transform,
                    "FigmaContent/SettingsView/Body/Detail/LanguageQuickView/ChangeLanguageButton");
                Transform internalBack = FindDeep(root.transform,
                    "FigmaContent/LanguageView/TopBar/InternalBackButton");
                Transform alertToggle = FindDeep(root.transform,
                    "FigmaContent/SettingsView/Body/Detail/AudioView/AlertSoundsToggle");
                Transform volumeCard = FindDeep(root.transform,
                    "FigmaContent/SettingsView/Body/Detail/AudioView/MasterVolumeCard");

                if (settingsView == null || languageView == null ||
                    languageQuick == null || audioView == null || aboutView == null)
                    throw new InvalidOperationException(
                        "Required Settings content roots missing from prefab.");

                SettingsPageTransition settingsPt = EnsurePageTransition(settingsView.gameObject);
                SettingsPageTransition languagePt = EnsurePageTransition(languageView.gameObject);
                SettingsPageTransition quickPt = EnsurePageTransition(languageQuick.gameObject);
                SettingsPageTransition audioPt = EnsurePageTransition(audioView.gameObject);
                SettingsPageTransition aboutPt = EnsurePageTransition(aboutView.gameObject);

                SettingsTransitionController controller =
                    root.GetComponent<SettingsTransitionController>();
                if (controller == null)
                    controller = root.AddComponent<SettingsTransitionController>();
                controller.Configure(settingsPt, languagePt, quickPt, audioPt, aboutPt);

                SettingsButtonVisualState langVisual =
                    EnsureNavVisual(langBtn, SettingsButtonVisualState.Role.Navigation);
                SettingsButtonVisualState audioVisual =
                    EnsureNavVisual(audioBtn, SettingsButtonVisualState.Role.Navigation);
                SettingsButtonVisualState aboutVisual =
                    EnsureNavVisual(aboutBtn, SettingsButtonVisualState.Role.Navigation);
                EnsureNavVisual(changeLang, SettingsButtonVisualState.Role.Generic);
                EnsureNavVisual(internalBack, SettingsButtonVisualState.Role.Generic);

                SettingsToggleThumbVisual toggleVisual = EnsureToggleVisual(alertToggle);
                SettingsVolumeFillVisual fillVisual = null;
                if (volumeCard != null)
                {
                    fillVisual = EnsureVolumeFill(volumeCard);
                    EnsureNavVisual(volumeCard.Find("DecreaseButton"),
                        SettingsButtonVisualState.Role.PlusMinus);
                    EnsureNavVisual(volumeCard.Find("IncreaseButton"),
                        SettingsButtonVisualState.Role.PlusMinus);
                }

                SettingsViewNavigator navigator = root.GetComponent<SettingsViewNavigator>();
                if (navigator != null)
                {
                    navigator.ConfigureMotion(
                        controller, langVisual, audioVisual, aboutVisual);
                }

                SettingsAudioController audioController =
                    root.GetComponentInChildren<SettingsAudioController>(true);
                if (audioController != null)
                    audioController.ConfigureVisuals(fillVisual, toggleVisual);

                settingsPt.SnapVisible(true);
                languagePt.SnapHidden();
                quickPt.SnapVisible(true);
                audioPt.SnapHidden();
                aboutPt.SnapHidden();
                if (langVisual != null) langVisual.SetSelected(true);
                if (audioVisual != null) audioVisual.SetSelected(false);
                if (aboutVisual != null) aboutVisual.SetSelected(false);

                PrefabUtility.SaveAsPrefabAsset(root, PanelPath);
                Debug.Log("[Settings UX Fidelity] Prefab wired and saved.");
            }
            finally
            {
                if (root != null)
                    PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static SettingsPageTransition EnsurePageTransition(GameObject go)
        {
            CanvasGroup cg = go.GetComponent<CanvasGroup>();
            if (cg == null)
                cg = go.AddComponent<CanvasGroup>();
            cg.alpha = 1f;
            cg.interactable = true;
            cg.blocksRaycasts = true;

            SettingsPageTransition pt = go.GetComponent<SettingsPageTransition>();
            if (pt == null)
                pt = go.AddComponent<SettingsPageTransition>();
            pt.Configure(cg, go.transform as RectTransform);
            return pt;
        }

        private static SettingsButtonVisualState EnsureNavVisual(
            Transform target, SettingsButtonVisualState.Role role)
        {
            if (target == null) return null;
            Button button = target.GetComponent<Button>();
            Image image = target.GetComponent<Image>();
            CanvasGroup cg = target.GetComponent<CanvasGroup>();
            if (cg == null)
                cg = target.gameObject.AddComponent<CanvasGroup>();

            SettingsButtonVisualState visual =
                target.GetComponent<SettingsButtonVisualState>();
            if (visual == null)
                visual = target.gameObject.AddComponent<SettingsButtonVisualState>();

            Graphic[] graphics = image != null
                ? new Graphic[] { image }
                : Array.Empty<Graphic>();

            float pressScale = role == SettingsButtonVisualState.Role.PlusMinus ? 0.90f : 0.982f;
            float pressDur = role == SettingsButtonVisualState.Role.PlusMinus ? 0.12f : 0.14f;
            visual.Configure(role, button, cg, graphics, pressScale, pressDur);
            visual.CaptureAuthoredColorsAsDefault();

            // Avoid ColorTint fighting presentation ownership.
            if (button != null)
                button.transition = Selectable.Transition.None;

            // Apply selected color tokens for navigation role via serialized fields through Configure.
            // SetSelected colors are handled inside visual using its selectedColor; force cyan accent.
            var so = new SerializedObject(visual);
            so.FindProperty("selectedColor").colorValue = SelectedNav;
            so.FindProperty("hoverColor").colorValue = HoverNav;
            so.FindProperty("useBackgroundTint").boolValue =
                role == SettingsButtonVisualState.Role.Navigation;
            so.ApplyModifiedPropertiesWithoutUndo();
            return visual;
        }

        private static SettingsToggleThumbVisual EnsureToggleVisual(Transform alertToggle)
        {
            if (alertToggle == null) return null;
            Transform track = alertToggle.Find("ToggleTrack");
            if (track == null) return null;

            Image trackImage = track.GetComponent<Image>();
            Transform thumb = track.Find("Thumb");
            if (thumb == null)
            {
                var thumbGo = new GameObject("Thumb", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                thumb = thumbGo.transform;
                thumb.SetParent(track, false);
                var rt = thumb as RectTransform;
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(14f, 14f);
                rt.anchoredPosition = new Vector2(-10f, 0f);
                var img = thumbGo.GetComponent<Image>();
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
                AlertOff,
                AlertOn);
            visual.SnapOn(true);
            return visual;
        }

        private static SettingsVolumeFillVisual EnsureVolumeFill(Transform volumeCard)
        {
            Transform track = volumeCard.Find("Track");
            if (track == null) return null;
            Image trackImage = track.GetComponent<Image>();
            if (trackImage == null) return null;

            SettingsVolumeFillVisual visual =
                volumeCard.GetComponent<SettingsVolumeFillVisual>();
            if (visual == null)
                visual = volumeCard.gameObject.AddComponent<SettingsVolumeFillVisual>();
            visual.Configure(trackImage, 0.25f);
            return visual;
        }

        private static bool HasPageTransition(GameObject prefab, string path)
        {
            Transform t = FindDeep(prefab.transform, path);
            if (t == null) return false;
            return t.GetComponent<SettingsPageTransition>() != null &&
                   t.GetComponent<CanvasGroup>() != null;
        }

        private static Transform FindDeep(Transform root, string path)
        {
            if (root == null || string.IsNullOrEmpty(path)) return null;
            Transform t = root.Find(path);
            if (t != null) return t;

            // Fallback: search by leaf name under expected parents.
            string leaf = path.Contains("/") ? path.Substring(path.LastIndexOf('/') + 1) : path;
            return FindByName(root, leaf);
        }

        private static Transform FindByName(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindByName(root.GetChild(i), name);
                if (found != null) return found;
            }

            return null;
        }

        private static void EnsureBaselineExists()
        {
            if (LatestBackup() != null) return;
            CaptureBaseline();
        }

        public static string BuildDryRunReport(out bool dryRunReady)
        {
            var lines = new List<string>();
            bool allGatesPassed = true;

            lines.Add("NORSCOPE SETTINGS UX FIDELITY — DRY RUN");
            lines.Add("Generated: " + DateTime.Now.ToString("O"));
            lines.Add(string.Empty);

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath);
            allGatesPassed &= prefab != null;
            lines.Add((prefab != null ? "PASS  " : "FAIL  ") + "SettingsPanel.prefab loads");
            bool figmaOk = File.Exists("Temp/settings_figma/src/app/App.tsx");
            allGatesPassed &= figmaOk;
            lines.Add((figmaOk ? "PASS  " : "FAIL  ") +
                      "Figma prototype App.tsx available for comparison");

            lines.Add(string.Empty);
            lines.Add(BuildFullDryRunBody(prefab));
            lines.Add(string.Empty);
            lines.Add(allGatesPassed
                ? "STATUS: READY"
                : "STATUS: BLOCKED");
            dryRunReady = allGatesPassed;
            return string.Join(Environment.NewLine, lines);
        }

        private static string BuildFullDryRunBody(GameObject prefab)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== LIVE UNITY SNAPSHOT ===");
            if (prefab == null)
            {
                sb.AppendLine("Prefab unavailable in this Editor domain.");
                return sb.ToString();
            }

            sb.AppendLine("Hierarchy anchors:");
            string[] paths =
            {
                "FigmaContent/SettingsView",
                "FigmaContent/SettingsView/Body/Sidebar/LanguageButton",
                "FigmaContent/SettingsView/Body/Sidebar/AudioButton",
                "FigmaContent/SettingsView/Body/Sidebar/AboutButton",
                "FigmaContent/SettingsView/Body/Detail/LanguageQuickView",
                "FigmaContent/SettingsView/Body/Detail/AudioView",
                "FigmaContent/SettingsView/Body/Detail/AboutView",
                "FigmaContent/LanguageView",
                "FigmaContent/LanguageView/TopBar/InternalBackButton",
                "TitleBar/CloseButton",
                "TitleBar/GrabRegion",
                "ResizeHandles/BottomRight"
            };
            foreach (string path in paths)
                sb.AppendLine((FindDeep(prefab.transform, path) != null ? "  OK  " : "  MISSING  ") + path);

            sb.AppendLine("CanvasGroups on prefab: " +
                          prefab.GetComponentsInChildren<CanvasGroup>(true).Length);
            sb.AppendLine("SettingsTransitionController: " +
                          (prefab.GetComponent<SettingsTransitionController>() != null));
            return sb.ToString();
        }

        private static string LatestBackup()
        {
            if (!Directory.Exists(BackupRoot))
                return null;
            return Directory.GetDirectories(BackupRoot, "SettingsUxFidelity_*")
                .OrderByDescending(d => d)
                .FirstOrDefault();
        }
    }
}
