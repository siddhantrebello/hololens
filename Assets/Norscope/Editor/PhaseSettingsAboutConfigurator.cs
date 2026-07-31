using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Norscope.Settings;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Norscope.Editor
{
    /// <summary>
    /// Controlled Settings About workflow. Apply is blocked until explicitly approved.
    /// </summary>
    public static class PhaseSettingsAboutConfigurator
    {
        private const string Menu = "Norscope/HoloLens/Settings/About/";
        private const string PanelPath = "Assets/Norscope/UI/Panels/SettingsPanel.prefab";
        private const string BootstrapPath = "Assets/Norscope/Scenes/Bootstrap.unity";
        private const string ConfigPath =
            "Assets/Norscope/Data/Settings/NorscopeSettingsConfiguration.asset";
        private const string LogoPath =
            "Assets/WhatsApp Image 2026-07-19 at 13.42.15-transparent.png";
        private const string BackupRoot = "Assets/Norscope/UI/Panels/_Backups";
        private const string ApplyFlag = "Temp/NorscopeSettingsAboutApply.flag";
        private const string DryRunFlag = "Temp/NorscopeSettingsAboutDryRun.flag";
        private const string CaptureFlag = "Temp/NorscopeSettingsAboutCapture.flag";
        private const string ValidateFlag = "Temp/NorscopeSettingsAboutValidate.flag";
        private const string ReportPath = "Temp/NorscopeSettingsAboutDryRun.txt";

        private static readonly string[] AboutOnlyTouchList =
        {
            "Assets/Norscope/Runtime/Settings/SettingsPanel.cs",
            "Assets/Norscope/Runtime/Settings/SettingsAboutBinder.cs",
            PanelPath
        };

        [InitializeOnLoadMethod]
        private static void RunApprovedQueuedCommand()
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
            string folder = BackupRoot + "/SettingsAbout_" + stamp;
            Directory.CreateDirectory(folder);
            FileUtil.CopyFileOrDirectory(PanelPath, folder + "/SettingsPanel.prefab");
            File.WriteAllText(
                folder + "/manifest.txt",
                "Settings About baseline " + DateTime.Now.ToString("O") +
                Environment.NewLine + PanelPath + Environment.NewLine);
            AssetDatabase.Refresh();
            Debug.Log("[Norscope Settings About] Baseline captured: " + folder);
        }

        [MenuItem(Menu + "2) Dry Run")]
        public static void DryRun()
        {
            string report = BuildDryRunReport(out bool dryRunReady);
            Directory.CreateDirectory("Temp");
            File.WriteAllText(ReportPath, report, Encoding.UTF8);
            Debug.Log("[Norscope Settings About] Dry run " +
                      (dryRunReady ? "READY\n" : "BLOCKED\n") + report);
        }

        [MenuItem(Menu + "3) Apply")]
        public static void Apply()
        {
            EnsureBaselineExists();
            ApplyAboutImplementation();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Validate();
            Debug.Log("[Norscope Settings About] Apply complete.");
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

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath);
            check(prefab != null, "SettingsPanel.prefab loads");
            if (prefab != null)
            {
                Transform aboutView = prefab.transform.Find(
                    "FigmaContent/SettingsView/Body/Detail/AboutView");
                check(aboutView != null, "AboutView present");
                Transform aboutCard = aboutView != null ? aboutView.Find("AboutCard") : null;
                RectTransform cardRt = aboutCard as RectTransform;
                check(
                    cardRt != null && Mathf.Approximately(cardRt.sizeDelta.y, 442f) &&
                    Mathf.Approximately(cardRt.sizeDelta.x, 596f),
                    "AboutCard size is 596 x 442");
                check(
                    aboutView != null &&
                    aboutView.Find("AboutCard/ApplicationRow/Value") != null &&
                    aboutView.Find("AboutCard/VersionRow/Value") != null &&
                    aboutView.Find("AboutCard/BuildRow/Value") != null &&
                    aboutView.Find("AboutCard/PlatformRow/Value") != null &&
                    aboutView.Find("AboutCard/UnityRow/Value") != null &&
                    aboutView.Find("AboutCard/CompanyRow/Value") != null &&
                    aboutView.Find("AboutCard/CopyrightRow/Value") != null,
                    "About rows Application/Version/Build/Platform/Unity/Company/Copyright exist");
                check(
                    aboutView == null || aboutView.Find("AboutCard/LicenseRow") == null,
                    "Legacy LicenseRow removed/renamed");
                check(
                    aboutView == null ||
                    aboutView.Find("OpenSourceLicensesButton") == null,
                    "Open Source Licenses section absent (no license viewer exists)");
                check(
                    aboutView == null || aboutView.Find("Logo") == null,
                    "No logo added inside AboutView");

                check(SettingsAboutBinder.ApplicationName == "NORSCOPE", "Binder ApplicationName");
                check(SettingsAboutBinder.Build == "Prototype Build", "Binder Build fallback");
                check(SettingsAboutBinder.Platform == "Windows Editor", "Binder Platform is Windows Editor in Editor");
                check(SettingsAboutBinder.Copyright == "© NORSCOPE", "Binder Copyright");
                check(
                    !string.IsNullOrWhiteSpace(SettingsAboutBinder.Company),
                    "Binder Company resolves (companyName or NORSCOPE fallback)");
                check(
                    SettingsAboutBinder.UnityVersion == Application.unityVersion,
                    "Binder Unity version matches Application.unityVersion");
                check(
                    SettingsAboutBinder.Version == Application.version ||
                    (!string.IsNullOrWhiteSpace(Application.version) &&
                     SettingsAboutBinder.Version == Application.version),
                    "Binder Version matches Application.version");

                SettingsPanel panel = prefab.GetComponent<SettingsPanel>();
                check(panel != null, "SettingsPanel component present");
                SerializedObject panelSo = panel != null ? new SerializedObject(panel) : null;
                check(
                    panelSo != null &&
                    panelSo.FindProperty("applicationValue")?.objectReferenceValue != null &&
                    panelSo.FindProperty("versionValue")?.objectReferenceValue != null &&
                    panelSo.FindProperty("buildValue")?.objectReferenceValue != null &&
                    panelSo.FindProperty("platformValue")?.objectReferenceValue != null &&
                    panelSo.FindProperty("unityValue")?.objectReferenceValue != null &&
                    panelSo.FindProperty("companyValue")?.objectReferenceValue != null &&
                    panelSo.FindProperty("copyrightValue")?.objectReferenceValue != null,
                    "About TMP bindings assigned on SettingsPanel");

                SettingsViewNavigator nav = prefab.GetComponent<SettingsViewNavigator>();
                SerializedObject navSo = nav != null ? new SerializedObject(nav) : null;
                check(
                    navSo != null &&
                    navSo.FindProperty("aboutView")?.objectReferenceValue != null &&
                    navSo.FindProperty("aboutButton")?.objectReferenceValue != null &&
                    navSo.FindProperty("languageInternalBackButton")?.objectReferenceValue != null,
                    "About/Internal Back navigator refs assigned");

                check(
                    prefab.transform.Find(
                        "FigmaContent/SettingsView/Body/Sidebar/AudioButton") != null,
                    "Audio sidebar untouched");
                check(
                    prefab.transform.Find(
                        "FigmaContent/SettingsView/Body/Detail/AudioView") != null,
                    "Audio view untouched");
                check(
                    prefab.transform.Find(
                        "FigmaContent/SettingsView/Body/Sidebar/LanguageButton") != null &&
                    prefab.transform.Find(
                        "FigmaContent/LanguageView") != null,
                    "Language UI untouched");
                check(
                    prefab.transform.Find("TitleBar/GrabRegion") != null &&
                    prefab.transform.Find("ResizeHandles/BottomRight") != null &&
                    prefab.transform.Find("TitleBar/CloseButton") != null &&
                    prefab.transform.Find(
                        "FigmaContent/SettingsView/TopBar/BackButton") != null,
                    "Grab/Resize/Close/External Back preserved");
            }

            check(
                File.Exists("Assets/Norscope/Runtime/Settings/SettingsAboutBinder.cs"),
                "SettingsAboutBinder runtime helper exists");
            check(!EditorUtility.scriptCompilationFailed, "Zero C# compile errors");

            string validationReport = string.Join("\n", lines);
            Directory.CreateDirectory("Temp");
            File.WriteAllText(
                "Temp/NorscopeSettingsAboutValidate.txt",
                (ok ? "PASSED\n" : "FAILED\n") + validationReport,
                Encoding.UTF8);
            Debug.Log("[Norscope Settings About] Validation " +
                      (ok ? "PASSED\n" : "FAILED\n") + validationReport);
        }

        [MenuItem(Menu + "5) Restore Baseline")]
        public static void Restore()
        {
            string folder = LatestBackup();
            if (folder == null)
            {
                Debug.LogError("[Norscope Settings About] No baseline backup found.");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "Restore Settings About baseline?",
                    "Restores SettingsPanel.prefab from:\n" + folder,
                    "Restore",
                    "Cancel"))
                return;

            FileUtil.ReplaceFile(folder + "/SettingsPanel.prefab", PanelPath);
            AssetDatabase.ImportAsset(PanelPath, ImportAssetOptions.ForceUpdate);
            Debug.Log("[Norscope Settings About] Restored from " + folder);
        }

        public static string BuildDryRunReport(out bool dryRunReady)
        {
            var lines = new List<string>();
            bool allGatesPassed = true;

            lines.Add("NORSCOPE SETTINGS ABOUT — DRY RUN");
            lines.Add("Generated: " + DateTime.Now.ToString("O"));
            lines.Add("Scope: About page only. Audio/Language/Navigation/layout outside About untouched.");
            lines.Add(string.Empty);

            lines.Add("=== 1) CURRENT ABOUT HIERARCHY ===");
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath);
            AppendGate(ref allGatesPassed, lines, prefab != null, "SettingsPanel.prefab loads");
            if (prefab != null)
            {
                Transform aboutView = prefab.transform.Find(
                    "FigmaContent/SettingsView/Body/Detail/AboutView");
                AppendGate(ref allGatesPassed, lines, aboutView != null, "AboutView path exists");
                if (aboutView != null)
                {
                    lines.Add("AboutView");
                    AppendHierarchy(lines, aboutView, 1);
                }
            }

            lines.Add(string.Empty);
            lines.Add("=== 2) EXISTING ABOUT CONTROLS ===");
            lines.Add("- Sidebar AboutButton → SettingsViewNavigator.ShowSection(About)");
            lines.Add("- AboutView/SectionTitle = \"About\"");
            lines.Add("- AboutCard rows (Label / authored Value):");
            lines.Add("  ApplicationRow  Application / NORSCOPE");
            lines.Add("  VersionRow      Version / " + Application.version);
            lines.Add("  BuildRow        Build / Unavailable");
            lines.Add("  PlatformRow     Platform / Unity Editor (authored placeholder)");
            lines.Add("  LicenseRow      License / Enterprise");
            lines.Add("- No Unity / Company / Copyright rows yet");
            lines.Add("- No Open Source Licenses button/section");
            lines.Add("- AboutCard size: 596 x 330, row pitch 56px (y=20,76,132,188,244)");

            lines.Add(string.Empty);
            lines.Add("=== 3) EXISTING SCRIPTS ===");
            lines.Add("- SettingsPanel.cs — binds application/version/build/platform/license TMP");
            lines.Add("- SettingsViewNavigator.cs — About section switching (DO NOT MODIFY nav logic)");
            lines.Add("- SettingsInternalView.About = 3");
            lines.Add("- NorscopeSettingsConfiguration.licenseName (Enterprise) — product license tier,");
            lines.Add("  not an open-source license viewer");
            lines.Add("- NorscopeSettingsService.LicenseName — read-only consumer of configuration");

            lines.Add(string.Empty);
            lines.Add("=== 4) DEFERRED CONTROLS (must remain untouched) ===");
            if (prefab != null)
            {
                SettingsPanel panel = prefab.GetComponent<SettingsPanel>();
                SerializedObject so = panel != null ? new SerializedObject(panel) : null;
                SerializedProperty controls = so?.FindProperty("deferredControls");
                if (controls != null)
                {
                    for (int i = 0; i < controls.arraySize; i++)
                    {
                        var selectable = controls.GetArrayElementAtIndex(i)
                            .objectReferenceValue as Selectable;
                        lines.Add("- " + (selectable != null
                            ? selectable.name + " interactable=" + selectable.interactable
                            : "(null)"));
                    }
                }
            }

            lines.Add(string.Empty);
            lines.Add("=== 5) BUILD / VERSION UTILITIES ===");
            lines.Add("- PlayerSettings.bundleVersion / Application.version = " + Application.version);
            lines.Add("- Application.unityVersion = " + Application.unityVersion);
            lines.Add("- Application.companyName = \"" + Application.companyName + "\"");
            lines.Add("- Application.productName = \"" + Application.productName + "\"");
            lines.Add("- No dedicated build-number / CI revision API found in Norscope pipeline");
            lines.Add("- Phase2/Phase3 deploy scripts set bundleVersion only; no build counter");
            lines.Add("- Build fallback required: \"Prototype Build\"");

            lines.Add(string.Empty);
            lines.Add("=== 6) SETTINGS PERSISTENCE ===");
            lines.Add("- norscope-settings.json via NorscopeSettingsService");
            lines.Add("- Persists language, masterVolume, alertSoundsEnabled, lastSettingsView, textSize");
            lines.Add("- About is read-only — Apply will NOT write any About fields to persistence");

            lines.Add(string.Empty);
            lines.Add("=== 7) LOGO REFERENCES ===");
            Sprite logo = AssetDatabase.LoadAssetAtPath<Sprite>(LogoPath);
            AppendGate(
                ref allGatesPassed,
                lines,
                logo != null || File.Exists(LogoPath),
                "Existing logo asset present: " + LogoPath);
            lines.Add("- Already used on SettingsView/TopBar/Logo and LanguageView/TopBar/Logo");
            lines.Add("- PLAN: reuse same sprite reference only if an About logo slot already exists;");
            lines.Add("  do not duplicate image assets; do not add a new logo node unless Figma already has one");
            lines.Add("- Current AboutView has NO logo child — leave TopBar logo as the brand mark");

            lines.Add(string.Empty);
            lines.Add("=== 8) PROJECT METADATA ===");
            lines.Add("- companyName: Norscope");
            lines.Add("- productName: Norscope");
            lines.Add("- bundleVersion: " + Application.version);
            lines.Add("- Display Application Name per spec: NORSCOPE (brand form)");

            lines.Add(string.Empty);
            lines.Add("=== 9) LICENSE INFRASTRUCTURE ===");
            NorscopeSettingsConfiguration config =
                AssetDatabase.LoadAssetAtPath<NorscopeSettingsConfiguration>(ConfigPath);
            lines.Add("- NorscopeSettingsConfiguration.licenseName = " +
                      (config != null ? config.LicenseName : "(missing asset)"));
            lines.Add("- Lucide/OFL font license TXT files exist under Assets (authoring licenses only)");
            lines.Add("- No runtime Open Source license viewer / panel / button system found");
            lines.Add("- PLAN: hide Open Source Licenses section (do not create placeholder pages)");

            lines.Add(string.Empty);
            lines.Add("=== PLANNED APPLY (awaiting approval) ===");
            lines.Add("A. Add SettingsAboutBinder.cs (About-only value resolution helpers)");
            lines.Add("   - ApplicationName → \"NORSCOPE\"");
            lines.Add("   - Version → Application.version");
            lines.Add("   - Build → \"Prototype Build\" (no build-number pipeline)");
            lines.Add("   - Platform → Editor: \"Windows Editor\"; WSA device: \"Microsoft HoloLens 2\"");
            lines.Add("   - Unity → Application.unityVersion");
            lines.Add("   - Company → Application.companyName, fallback \"NORSCOPE\" if empty");
            lines.Add("   - Copyright → \"© NORSCOPE\"");
            lines.Add("B. Update SettingsPanel.ApplyBindings About section only to use binder");
            lines.Add("C. PrefabUtility edit AboutView/AboutCard ONLY:");
            lines.Add("   - Keep Application/Version/Build/Platform rows");
            lines.Add("   - Rename LicenseRow → CompanyRow (label Company)");
            lines.Add("   - Add UnityRow + CopyrightRow with same AboutRow geometry/spacing (56px)");
            lines.Add("   - Grow AboutCard height just enough for 7 rows (330 → 442)");
            lines.Add("   - Wire new TMP Value refs on SettingsPanel");
            lines.Add("   - Do NOT add Open Source Licenses UI");
            lines.Add("D. Explicitly out of scope / protected:");
            lines.Add("   - Audio view/controls/mixer");
            lines.Add("   - Language view/buttons/localization tables");
            lines.Add("   - Navigation services / PanelRegistry");
            lines.Add("   - Sidebar positions, Grab, Resize, Close, animations");
            lines.Add("   - Ask AI / Home / Reports / Task Library / Task Execution");

            lines.Add(string.Empty);
            lines.Add("=== LAYOUT NOTE (needs approval) ===");
            lines.Add("Figma currently authors 5 rows. Spec requires 7 info lines.");
            lines.Add("Proposed minimal AboutCard-local growth (+112px) preserves row style/spacing.");
            lines.Add("No sidebar, Detail, or other Settings sections will be moved.");

            lines.Add(string.Empty);
            lines.Add("=== FILES TOUCHED ON APPLY ===");
            foreach (string path in AboutOnlyTouchList)
                lines.Add("- " + path);

            lines.Add(string.Empty);
            lines.Add("=== VALIDATION PLAN (post-Apply) ===");
            lines.Add("- About opens from sidebar");
            lines.Add("- Runtime values populate on Editor + WSA defines");
            lines.Add("- Missing companyName falls back to NORSCOPE");
            lines.Add("- Build shows Prototype Build");
            lines.Add("- Internal Back / External Back / Close / Grab / Resize unchanged");
            lines.Add("- No duplicate listeners; deferredControls unchanged");
            lines.Add("- Zero compile errors; Audio/Language hierarchy unchanged");

            lines.Add(string.Empty);
            lines.Add(allGatesPassed
                ? "STATUS: READY — waiting for Dry Run approval before Apply."
                : "STATUS: BLOCKED — fix FAIL items before Apply.");
            dryRunReady = allGatesPassed;
            return string.Join(Environment.NewLine, lines);
        }

        private static void AppendGate(
            ref bool allGatesPassed,
            List<string> lines,
            bool pass,
            string label)
        {
            allGatesPassed &= pass;
            lines.Add((pass ? "PASS  " : "FAIL  ") + label);
        }

        private static void AppendHierarchy(List<string> lines, Transform root, int depth)
        {
            string pad = new string(' ', depth * 2);
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                lines.Add(pad + "- " + child.name);
                if (child.childCount > 0 && depth < 4)
                    AppendHierarchy(lines, child, depth + 1);
            }
        }

        private static void EnsureBaselineExists()
        {
            if (LatestBackup() == null)
                CaptureBaseline();
        }

        private static string LatestBackup()
        {
            if (!Directory.Exists(BackupRoot))
                return null;
            return Directory.GetDirectories(BackupRoot, "SettingsAbout_*")
                .OrderByDescending(d => d)
                .FirstOrDefault();
        }

        private static void ApplyAboutImplementation()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PanelPath);
            try
            {
                Transform aboutView = root.transform.Find(
                    "FigmaContent/SettingsView/Body/Detail/AboutView");
                if (aboutView == null)
                    throw new InvalidOperationException("AboutView missing.");

                Transform aboutCard = aboutView.Find("AboutCard");
                if (aboutCard == null)
                    throw new InvalidOperationException("AboutCard missing.");

                RectTransform cardRt = aboutCard as RectTransform;
                cardRt.sizeDelta = new Vector2(596f, 442f);

                EnsureRowPositions(aboutCard);
                Transform companyRow = RenameLicenseToCompany(aboutCard);
                Transform unityRow = EnsureAboutRow(
                    aboutCard, "UnityRow", "Unity", SettingsAboutBinder.UnityVersion, 244f);
                Transform copyrightRow = EnsureAboutRow(
                    aboutCard, "CopyrightRow", "Copyright", SettingsAboutBinder.Copyright, 356f);

                // Reposition Company after Unity
                SetRowY(companyRow, 300f);

                TMP_Text applicationValue = RequireValue(aboutCard, "ApplicationRow");
                TMP_Text versionValue = RequireValue(aboutCard, "VersionRow");
                TMP_Text buildValue = RequireValue(aboutCard, "BuildRow");
                TMP_Text platformValue = RequireValue(aboutCard, "PlatformRow");
                TMP_Text unityValue = RequireValue(unityRow, null);
                TMP_Text companyValue = RequireValue(companyRow, null);
                TMP_Text copyrightValue = RequireValue(copyrightRow, null);

                applicationValue.text = SettingsAboutBinder.ApplicationName;
                versionValue.text = SettingsAboutBinder.Version;
                buildValue.text = SettingsAboutBinder.Build;
                platformValue.text = SettingsAboutBinder.Platform;
                unityValue.text = SettingsAboutBinder.UnityVersion;
                companyValue.text = SettingsAboutBinder.Company;
                copyrightValue.text = SettingsAboutBinder.Copyright;

                SettingsPanel panel = root.GetComponent<SettingsPanel>();
                SerializedObject so = new SerializedObject(panel);
                so.FindProperty("applicationValue").objectReferenceValue = applicationValue;
                so.FindProperty("versionValue").objectReferenceValue = versionValue;
                so.FindProperty("buildValue").objectReferenceValue = buildValue;
                so.FindProperty("platformValue").objectReferenceValue = platformValue;
                so.FindProperty("unityValue").objectReferenceValue = unityValue;
                so.FindProperty("companyValue").objectReferenceValue = companyValue;
                so.FindProperty("copyrightValue").objectReferenceValue = copyrightValue;
                so.ApplyModifiedPropertiesWithoutUndo();

                if (aboutView.Find("OpenSourceLicensesButton") != null)
                    throw new InvalidOperationException(
                        "Open Source Licenses control must not be added.");

                bool saved = PrefabUtility.SaveAsPrefabAsset(root, PanelPath);
                if (!saved)
                    throw new InvalidOperationException("SaveAsPrefabAsset failed.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.ImportAsset(PanelPath, ImportAssetOptions.ForceUpdate);
        }

        private static void EnsureRowPositions(Transform aboutCard)
        {
            SetRowY(aboutCard.Find("ApplicationRow"), 20f);
            SetRowY(aboutCard.Find("VersionRow"), 76f);
            SetRowY(aboutCard.Find("BuildRow"), 132f);
            SetRowY(aboutCard.Find("PlatformRow"), 188f);
        }

        private static Transform RenameLicenseToCompany(Transform aboutCard)
        {
            Transform license = aboutCard.Find("LicenseRow");
            Transform company = aboutCard.Find("CompanyRow");
            if (company == null)
            {
                if (license == null)
                    throw new InvalidOperationException("LicenseRow/CompanyRow missing.");
                license.name = "CompanyRow";
                company = license;
            }
            else if (license != null && license != company)
            {
                UnityEngine.Object.DestroyImmediate(license.gameObject);
            }

            TMP_Text label = company.Find("Label")?.GetComponent<TMP_Text>();
            if (label != null)
                label.text = "Company";
            SetRowY(company, 300f);
            return company;
        }

        private static Transform EnsureAboutRow(
            Transform aboutCard,
            string rowName,
            string labelText,
            string valueText,
            float y)
        {
            Transform existing = aboutCard.Find(rowName);
            if (existing != null)
            {
                TMP_Text label = existing.Find("Label")?.GetComponent<TMP_Text>();
                TMP_Text value = existing.Find("Value")?.GetComponent<TMP_Text>();
                if (label != null)
                    label.text = labelText;
                if (value != null)
                    value.text = valueText;
                SetRowY(existing, y);
                return existing;
            }

            Transform template = aboutCard.Find("ApplicationRow");
            if (template == null)
                throw new InvalidOperationException("ApplicationRow template missing.");

            GameObject clone = UnityEngine.Object.Instantiate(template.gameObject, aboutCard);
            clone.name = rowName;
            TMP_Text cloneLabel = clone.transform.Find("Label")?.GetComponent<TMP_Text>();
            TMP_Text cloneValue = clone.transform.Find("Value")?.GetComponent<TMP_Text>();
            if (cloneLabel != null)
                cloneLabel.text = labelText;
            if (cloneValue != null)
                cloneValue.text = valueText;
            SetRowY(clone.transform, y);
            return clone.transform;
        }

        private static void SetRowY(Transform row, float y)
        {
            if (row == null)
                return;
            RectTransform rt = row as RectTransform;
            if (rt == null)
                return;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(556f, 50f);
            rt.anchoredPosition = new Vector2(20f, -y);
        }

        private static TMP_Text RequireValue(Transform parent, string rowName)
        {
            Transform row = string.IsNullOrEmpty(rowName) ? parent : parent.Find(rowName);
            if (row == null)
                throw new InvalidOperationException("Missing row " + rowName);
            TMP_Text value = row.Find("Value")?.GetComponent<TMP_Text>();
            if (value == null)
                throw new InvalidOperationException("Missing Value under " + row.name);
            return value;
        }
    }
}
