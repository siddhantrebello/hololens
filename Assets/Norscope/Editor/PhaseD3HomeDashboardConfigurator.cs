using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Norscope.Navigation;
using Norscope.UI;
using Norscope.Windows;

namespace Norscope.Editor
{
    /// <summary>
    /// Home-only approved visual redesign. No runtime construction or functional wiring.
    /// </summary>
    public static class PhaseD3HomeDashboardConfigurator
    {
        // Figma Make maximum-width composition. The legacy screenshot plan used
        // 690 x 470 and is deliberately unreachable from the menu commands.
        private static readonly Vector2 PanelSize = new Vector2(880f, 580f);
        private static readonly Vector2 CardSize = new Vector2(260f, 120f);
        private static readonly Color PanelColor =
            new Color(0.05098f, 0.06667f, 0.0902f, 0.85f);
        private static readonly Color CardColor =
            new Color(0.09412f, 0.12941f, 0.16863f, 1f);
        private static readonly Color BorderColor =
            new Color(0.17647f, 0.23137f, 0.28627f, 1f);
        private static readonly Color PrimaryText =
            new Color(0.95294f, 0.96863f, 0.98431f, 1f);
        private static readonly Color SecondaryText =
            new Color(0.56863f, 0.63529f, 0.7098f, 1f);
        private static readonly Color Accent =
            new Color(0.11765f, 0.78431f, 0.96078f, 1f);

        private const string FontFolder = "Assets/Norscope/UI/Fonts";
        private const string OutfitFontPath =
            FontFolder + "/Outfit-Variable.ttf";
        private const string InterFontPath =
            FontFolder + "/Inter-Variable.ttf";
        private const string OutfitTmpPath =
            FontFolder + "/Outfit SDF.asset";
        private const string InterTmpPath =
            FontFolder + "/Inter SDF.asset";
        private const string IconFolder =
            "Assets/Norscope/UI/Icons/Lucide/Raster";
        private const string RoundedSpritePath =
            "Assets/Norscope/UI/DesignSystem/Sprites/UIRounded_8.png";
        private const string BrandLogoPath =
            "Assets/WhatsApp Image 2026-07-19 at 13.42.15-transparent.png";

        private sealed class Report
        {
            internal readonly StringBuilder Text = new StringBuilder();
            internal int Changes;
            internal void Change(string path, string component, string property,
                string before, string after)
            {
                Changes++;
                Text.AppendLine(path + " | " + component + "." + property +
                                " | " + before + " -> " + after);
            }
            internal void Info(string value) => Text.AppendLine("INFO  " + value);
        }

        [MenuItem("Norscope/HoloLens/Phase D3 Home/1) Capture Home Baseline")]
        public static void CaptureMenu()
        {
            bool ok = D3HomeBaseline.Capture(out string report);
            Debug.Log((ok ? "[Norscope D3] Home baseline READY\n" :
                "[Norscope D3] Home baseline FAILED\n") + report);
            EditorUtility.DisplayDialog("D3 Home Baseline",
                (ok ? "READY\n\n" : "FAILED\n\n") + report, "OK");
        }

        [MenuItem("Norscope/HoloLens/Phase D3 Home/2) Dry Run Home Redesign")]
        public static void DryRunMenu()
        {
            bool ok = BuildFigmaDryRun(out string report);
            Debug.Log((ok ? "[Norscope D3] Dry run READY\n" :
                "[Norscope D3] Dry run BLOCKED\n") + report);
            EditorUtility.DisplayDialog("D3 Home Dry Run",
                ok ? "READY. See Console for the complete Figma Make plan."
                   : "BLOCKED. Apply remains disabled. See Console.", "OK");
        }

        [MenuItem("Norscope/HoloLens/Phase D3 Home/3) Apply Home Redesign")]
        public static void ApplyMenu()
        {
            if (!BuildFigmaDryRun(out string preflightReport))
            {
                Debug.LogError("[Norscope D3] Apply preflight BLOCKED\n" +
                               preflightReport);
                EditorUtility.DisplayDialog("D3 Home Apply blocked",
                    "A genuine dry-run validation failure remains. Nothing was " +
                    "modified. See Console.", "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog("Apply approved Home Dashboard",
                    "Dry run is READY and the verified Home backup matches.\n\n" +
                    "Apply the approved Figma Make presentation to HomePanel.prefab?\n\n" +
                    "This will configure the prepared font/icon assets and save only " +
                    "HomePanel.prefab. Existing navigation, events, close, grab and " +
                    "resize bindings will be preserved.",
                    "Apply Home Redesign", "Cancel"))
                return;

            bool applied = ExecuteFigmaApply(out string applyReport);
            Debug.Log((applied
                    ? "[Norscope D3] Apply COMPLETE\n"
                    : "[Norscope D3] Apply FAILED\n") +
                applyReport);
            EditorUtility.DisplayDialog("D3 Home Apply",
                applied
                    ? "COMPLETE\n\nHomePanel.prefab now contains the approved authored " +
                      "presentation. Run 4) Validate Home Redesign."
                    : "FAILED\n\nSee Console. The verified Home backup remains available.",
                "OK");
        }

        [MenuItem("Norscope/HoloLens/Phase D3 Home/4) Validate Home Redesign")]
        public static void ValidateMenu()
        {
            bool ok = Validate(out string report);
            Debug.Log((ok ? "[Norscope D3] Validate Home Redesign PASSED\n" :
                "[Norscope D3] Validate Home Redesign FAILED\n") + report);
            EditorUtility.DisplayDialog("Validate D3 Home",
                (ok ? "PASSED\n\n" : "FAILED\n\n") + report, "OK");
        }

        [MenuItem("Norscope/HoloLens/Phase D3 Home/5) Restore Home")]
        public static void RestoreMenu()
        {
            if (!EditorUtility.DisplayDialog("Restore Home",
                    "Restore only HomePanel.prefab from the verified D3 Home backup?",
                    "Restore Home", "Cancel"))
                return;
            bool ok = D3HomeBaseline.Restore(out string report);
            Debug.Log((ok ? "[Norscope D3] Restore Home SUCCEEDED\n" :
                "[Norscope D3] Restore Home FAILED\n") + report);
        }

        [MenuItem("Norscope/HoloLens/Phase D3 Home/6) Repair Home Text Fonts")]
        public static void RepairHomeTextFontsMenu()
        {
            var report = new StringBuilder();
            bool outfitReady = RepairTmpFontAsset(
                OutfitFontPath, OutfitTmpPath, report);
            bool interReady = RepairTmpFontAsset(
                InterFontPath, InterTmpPath, report);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            bool ready = outfitReady && interReady;
            Debug.Log((ready
                    ? "[Norscope D3] Home text font repair PASSED\n"
                    : "[Norscope D3] Home text font repair FAILED\n") +
                report);
            EditorUtility.DisplayDialog("D3 Home Text Fonts",
                (ready ? "PASSED\n\n" : "FAILED\n\n") + report,
                "OK");
        }

        private static bool BuildFigmaDryRun(out string output)
        {
            var report = new StringBuilder();
            var blockers = new List<string>();
            var warnings = new List<string>();

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                D3HomeBaseline.HomePath);
            if (prefab == null)
            {
                output = "BLOCKER  HomePanel.prefab is missing.";
                return false;
            }
            bool baselineClean =
                D3HomeBaseline.Compare(false, out string baselineReport);
            bool approvedState =
                D3HomeBaseline.Compare(true, out string approvedReport);
            if (!baselineClean && !approvedState)
                blockers.Add("Fresh Home baseline does not match the current project. " +
                             "Run 1) Capture Home Baseline after scripts finish compiling. " +
                             baselineReport + "\n" + approvedReport);

            HomePanelCard[] cards =
                prefab.GetComponentsInChildren<HomePanelCard>(true);
            if (cards.Length != 6)
                blockers.Add("Expected exactly six existing HomePanelCard components.");
            if (prefab.GetComponent<NorscopePanelRoot>() == null)
                blockers.Add("NorscopePanelRoot is missing.");
            if (prefab.transform.Find("TitleBar/GrabRegion") == null)
                blockers.Add("Existing GrabRegion is missing.");
            if (prefab.transform.Find("ResizeHandles/BottomRight") == null)
                blockers.Add("Existing resize handle is missing.");

            string[] requiredFiles =
            {
                "Assets/Norscope/UI/Fonts/Outfit-Variable.ttf",
                "Assets/Norscope/UI/Fonts/Inter-Variable.ttf",
                "Assets/Norscope/UI/Icons/Lucide/Raster/layers.png",
                "Assets/Norscope/UI/Icons/Lucide/Raster/square-check-big.png",
                "Assets/Norscope/UI/Icons/Lucide/Raster/cpu.png",
                "Assets/Norscope/UI/Icons/Lucide/Raster/chart-no-axes-column-increasing.png",
                "Assets/Norscope/UI/Icons/Lucide/Raster/settings.png",
                "Assets/Norscope/UI/Icons/Lucide/Raster/database.png",
                "Assets/Norscope/UI/Icons/Lucide/Raster/info.png",
                "Assets/Norscope/UI/Icons/Lucide/Raster/chevron-right.png",
                BrandLogoPath
            };
            foreach (string path in requiredFiles)
            {
                if (!System.IO.File.Exists(path))
                    blockers.Add("Approved asset missing: " + path);
            }

            string packageManifest = System.IO.File.Exists("Packages/manifest.json")
                ? System.IO.File.ReadAllText("Packages/manifest.json")
                : string.Empty;
            if (packageManifest.IndexOf("mixedreality.toolkit",
                    StringComparison.OrdinalIgnoreCase) >= 0 ||
                packageManifest.IndexOf("mrtk",
                    StringComparison.OrdinalIgnoreCase) >= 0)
                blockers.Add("An MRTK dependency was found; D3 must remain on the " +
                             "existing Norscope XRI window architecture.");
            if (prefab.GetComponent<SpatialWindowBindings>() == null)
                blockers.Add("HomePanel is missing SpatialWindowBindings.");

            RectTransform shell = prefab.GetComponent<RectTransform>();
            Vector2 shellSize = shell != null ? shell.rect.size : Vector2.zero;
            float contentScale = Mathf.Min(
                shellSize.x / PanelSize.x,
                shellSize.y / PanelSize.y);
            if (contentScale <= 0f || contentScale > 1f)
                blockers.Add("The 880 x 580 composition cannot be proportionally fitted " +
                             "inside the existing authored shell.");

            string homeCardSource =
                "Assets/Norscope/Runtime/Navigation/HomePanelCard.cs";
            if (System.IO.File.Exists(homeCardSource))
            {
                string source = System.IO.File.ReadAllText(homeCardSource);
                if (!source.Contains("PresentationMode") ||
                    !source.Contains("PresentationMode.Authored"))
                    blockers.Add("HomePanelCard does not contain the approved per-instance " +
                                 "presentation ownership mode.");
                if (!source.Contains(
                        "if (presentationMode == PresentationMode.SharedTheme)"))
                    blockers.Add("Shared-theme presentation is not guarded by the " +
                                 "per-instance presentation mode.");
            }
            else
                blockers.Add("HomePanelCard.cs is missing.");

            int authoredOutsideHome = 0;
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            foreach (string guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject candidate =
                    AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (candidate == null) continue;
                foreach (HomePanelCard card in candidate
                             .GetComponentsInChildren<HomePanelCard>(true))
                {
                    SerializedObject cardSo = new SerializedObject(card);
                    SerializedProperty mode =
                        cardSo.FindProperty("presentationMode");
                    bool authored = mode != null && mode.enumValueIndex ==
                        (int)HomePanelCard.PresentationMode.Authored;
                    if (authored && path != D3HomeBaseline.HomePath)
                        authoredOutsideHome++;
                }
            }
            if (authoredOutsideHome != 0)
                blockers.Add("HomePanelCard Authored mode is already used outside " +
                             "HomePanel.prefab.");

            report.AppendLine("FIGMA MAKE SOURCE: fixed 880 x 580 authored composition");
            report.AppendLine("PREFAB: " + D3HomeBaseline.HomePath);
            report.AppendLine();
            report.AppendLine("PLANNED ASSET IMPORTS");
            report.AppendLine("- Outfit variable TTF (weights 300 and 500 used)");
            report.AppendLine("- Inter variable TTF (weights 300, 400 and 500 used)");
            report.AppendLine("- TMP font assets generated during a future approved Apply");
            report.AppendLine("- Lucide 0.487.0 Layers, CheckSquare, Cpu, BarChart2, " +
                              "Settings, Database, Info and ChevronRight");
            report.AppendLine("- Official SVG sources retained; 256 px transparent UI " +
                              "sprite rasterizations prepared");
            report.AppendLine();
            report.AppendLine("PLANNED AUTHORED COMPOSITION");
            report.AppendLine("- Existing Norscope XRI SpatialWindow remains authoritative");
            report.AppendLine("- Existing shell remains " +
                              shellSize.x.ToString("0.##") + " x " +
                              shellSize.y.ToString("0.##") +
                              " with its world-space scale unchanged");
            report.AppendLine("- New authored FigmaContent: 880 x 580 UI units, local " +
                              "visual scale " + contentScale.ToString("0.#####") +
                              ", centered inside the existing shell");
            report.AppendLine("- Resulting fitted content footprint: " +
                              (PanelSize.x * contentScale).ToString("0.##") + " x " +
                              (PanelSize.y * contentScale).ToString("0.##") +
                              " shell units");
            report.AppendLine("- Main inset: left/right 40, top 52, bottom 44");
            report.AppendLine("- Wordmark: x 40, y 52; icon box 22 x 22; gap 8");
            report.AppendLine("- Home title: x 40, y 102; Outfit 62.4, weight 300");
            report.AppendLine("- Subtitle: 14.08, Inter 300; 8 below title");
            report.AppendLine("- Separator: 800 x 1; 32 below header; 24 above cards");
            report.AppendLine("- Grid: three 260-unit columns, two rows, 10-unit gaps");
            report.AppendLine("- Cards: x 40/310/580; y 246/376; size 260 x 120");
            report.AppendLine("- Card padding: 18 top, 22 sides, 20 bottom");
            report.AppendLine("- Icon container: 30 x 30; glyph 14 x 14; row gap 11");
            report.AppendLine("- Card title: Outfit 18.08, nearest TMP weight 500");
            report.AppendLine("- Description: Inter 12.8, weight 300, line height 1.65, " +
                              "left indent 41");
            report.AppendLine("- ChevronRight: 14 x 14, stroke 2");
            report.AppendLine("- Footer: v4.2.1 · HoloLens 2; Inter 10.88; right aligned");
            report.AppendLine("- BrandLogo: supplied transparent NORSCOPE sprite, " +
                              "72 x 54, above the Home title");
            report.AppendLine();
            report.AppendLine("PLANNED PRESENTATION ADDITIONS");
            report.AppendLine("- FigmaContent visual container (presentation only)");
            report.AppendLine("- Wordmark container, HeaderSubtitle, Separator and Footer");
            report.AppendLine("- Six IconContainer objects and six Chevron Images");
            report.AppendLine("- Per-card TopSheen, FocusOutline, FocusAccent and Shadow");
            report.AppendLine("- Every decorative Image raycastTarget = false");
            report.AppendLine("- Existing card Button remains the sole card interaction target");
            report.AppendLine();
            report.AppendLine("PRESERVED");
            report.AppendLine("- Card Buttons, destinations, listeners and HomePanelCard refs");
            report.AppendLine("- Close control, GrabRegion, resize handle and hit areas");
            report.AppendLine("- SpatialWindow bindings, world transform, navigation and audio");
            report.AppendLine("- The sole runtime preparation change is the approved " +
                              "HomePanelCard presentation-ownership mode; no business " +
                              "logic, navigation, audio or workflow code changes");
            report.AppendLine();
            report.AppendLine("PLANNED HOMEPANELCARD OWNERSHIP");
            report.AppendLine("- Set presentationMode = Authored on exactly the six " +
                              "HomePanel.prefab cards");
            report.AppendLine("- Keep every HomePanelCard outside HomePanel.prefab at " +
                              "SharedTheme (enum value 0/default)");
            report.AppendLine("- Preserve every Button ColorBlock and targetGraphic");
            report.AppendLine("- Set each existing UiButtonPressScale.pressedScale to 1.0 " +
                              "on these six cards only, preventing duplicate scale ownership");
            report.AppendLine("- HomePanelCard Authored state owns hover/focus/press scale " +
                              "using serialized values 1.013 / 1.013 / 0.983");
            report.AppendLine("- SharedTheme mode retains the exact legacy theme path");
            report.AppendLine("- No object-name lookup exists in HomePanelCard runtime code");
            report.AppendLine();
            report.AppendLine("DRY-RUN VALIDATION");
            report.AppendLine("- Target prefab count: 1 (HomePanel.prefab only)");
            report.AppendLine("- Planned Authored cards: 6");
            report.AppendLine("- Existing Authored cards outside Home: " +
                              authoredOutsideHome);
            report.AppendLine("- Existing card UnityEvents/navigation references are " +
                              "captured by the fresh D3 baseline and will be compared");
            report.AppendLine("- SpatialWindowBindings, CloseButton, GrabRegion and " +
                              "BottomRight resize handle are present");
            report.AppendLine("- No MRTK package or dependency is required or introduced");
            report.AppendLine("- Raw Outfit/Inter sources and all eight Lucide raster " +
                              "sources are present; future Apply will create TMP assets " +
                              "and assign imported UI sprites atomically");
            report.AppendLine("- Authored mode prevents Awake from applying shared-theme " +
                              "card colors, typography, icon tint or Button ColorTint");

            foreach (string warning in warnings)
                report.AppendLine("WARNING  " + warning);
            foreach (string blocker in blockers)
                report.AppendLine("BLOCKER  " + blocker);

            report.AppendLine();
            report.AppendLine(blockers.Count == 0
                ? "READY — no blockers found."
                : "BLOCKED — wiring must not proceed.");
            output = report.ToString();
            return blockers.Count == 0;
        }

        private static bool SimulateApplyToFirstMutation(out string output)
        {
            var report = new StringBuilder();
            report.AppendLine("SIMULATION MODE — no writes, saves or imports are permitted");

            if (!BuildFigmaDryRun(out string preflight))
            {
                output = report + "\nABORT: BuildFigmaDryRun returned BLOCKED.\n" +
                         preflight;
                return false;
            }
            report.AppendLine("PASS  BuildFigmaDryRun returned READY");

            if (!D3HomeBaseline.Compare(false, out string baseline) &&
                !D3HomeBaseline.Compare(true, out baseline))
            {
                output = report + "\nABORT: fresh functional baseline mismatch.\n" +
                         baseline;
                return false;
            }
            report.AppendLine("PASS  Fresh Home baseline matches current project");

            GameObject root = null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(D3HomeBaseline.HomePath);
                if (root == null)
                {
                    output = report + "\nABORT: PrefabUtility.LoadPrefabContents failed.";
                    return false;
                }
                report.AppendLine("PASS  HomePanel.prefab loaded read-only for simulation");

                HomePanelCard[] cards =
                    root.GetComponentsInChildren<HomePanelCard>(true);
                if (cards.Length != 6)
                {
                    output = report + "\nABORT: expected six HomePanelCard components; " +
                             "found " + cards.Length + ".";
                    return false;
                }
                report.AppendLine("PASS  Resolved exactly six existing Home cards");

                if (root.GetComponent<NorscopePanelRoot>() == null ||
                    root.GetComponent<SpatialWindowBindings>() == null)
                {
                    output = report +
                             "\nABORT: required panel root/window binding is missing.";
                    return false;
                }
                report.AppendLine("PASS  NorscopePanelRoot and SpatialWindowBindings resolved");

                Transform grab = root.transform.Find("TitleBar/GrabRegion");
                Transform resize =
                    root.transform.Find("ResizeHandles/BottomRight");
                if (grab == null || resize == null)
                {
                    output = report +
                             "\nABORT: grab or resize interaction reference is missing.";
                    return false;
                }
                report.AppendLine("PASS  GrabRegion and resize handle remain intact");

                foreach (HomePanelCard card in cards)
                {
                    SerializedObject cardSo = new SerializedObject(card);
                    if (cardSo.FindProperty("presentationMode") == null ||
                        cardSo.FindProperty("button")?.objectReferenceValue == null ||
                        cardSo.FindProperty("background")?.objectReferenceValue == null ||
                        cardSo.FindProperty("border")?.objectReferenceValue == null ||
                        cardSo.FindProperty("icon")?.objectReferenceValue == null ||
                        cardSo.FindProperty("title")?.objectReferenceValue == null ||
                        cardSo.FindProperty("subtitle")?.objectReferenceValue == null)
                    {
                        output = report + "\nABORT: authored-mode or an existing card " +
                                 "functional/presentation reference is unresolved on " +
                                 card.name + ".";
                        return false;
                    }
                }
                report.AppendLine("PASS  All existing card references resolve");

                Transform existingFigmaContent =
                    root.transform.Find("FigmaContent");
                report.AppendLine(existingFigmaContent == null
                    ? "NEXT  First planned prefab mutation: create authored " +
                      "HomePanel/FigmaContent RectTransform"
                    : "NEXT  First planned prefab mutation: update existing " +
                      "HomePanel/FigmaContent authored RectTransform");
                report.AppendLine("STOP  Simulation halted before GameObject creation, " +
                                  "component mutation, importer change, TMP asset creation, " +
                                  "PrefabUtility.SaveAsPrefabAsset or AssetDatabase.SaveAssets");
                output = report.ToString();
                return true;
            }
            finally
            {
                if (root != null)
                    PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static bool ExecuteFigmaApply(out string output)
        {
            var report = new StringBuilder();
            if (!BuildFigmaDryRun(out string preflight))
            {
                output = "Apply aborted before mutation.\n" + preflight;
                return false;
            }
            if (!D3HomeBaseline.Compare(false, out string baseline) &&
                !D3HomeBaseline.Compare(true, out baseline))
            {
                output = "Apply aborted: verified baseline mismatch.\n" + baseline;
                return false;
            }

            TMP_FontAsset outfit = GetOrCreateTmpFont(
                OutfitFontPath, OutfitTmpPath, report);
            TMP_FontAsset inter = GetOrCreateTmpFont(
                InterFontPath, InterTmpPath, report);
            if (outfit == null || inter == null)
            {
                output = "Apply aborted: TMP font preparation failed.\n" + report;
                return false;
            }

            var sprites = new Dictionary<string, Sprite>();
            string[] iconNames =
            {
                "layers", "square-check-big", "cpu",
                "chart-no-axes-column-increasing", "settings",
                "database", "info", "chevron-right"
            };
            foreach (string iconName in iconNames)
            {
                Sprite sprite = PrepareSprite(
                    IconFolder + "/" + iconName + ".png", report);
                if (sprite == null)
                {
                    output = "Apply aborted: sprite preparation failed for " +
                             iconName + ".\n" + report;
                    return false;
                }
                sprites[iconName] = sprite;
            }
            Sprite brandLogo = PrepareSprite(BrandLogoPath, report);
            if (brandLogo == null)
            {
                output = "Apply aborted: NORSCOPE brand sprite preparation failed.\n" +
                         report;
                return false;
            }
            sprites["brand-logo"] = brandLogo;

            Sprite rounded = AssetDatabase.LoadAssetAtPath<Sprite>(
                RoundedSpritePath);
            if (rounded == null)
            {
                output = "Apply aborted: existing rounded UI sprite is missing.";
                return false;
            }

            GameObject root = null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(D3HomeBaseline.HomePath);
                ApplyFigmaPresentation(root, outfit, inter, rounded, sprites, report);
                PrefabUtility.SaveAsPrefabAsset(root, D3HomeBaseline.HomePath);
                report.AppendLine("SAVED  " + D3HomeBaseline.HomePath);
            }
            catch (Exception exception)
            {
                output = "Apply failed before completion: " + exception + "\n" + report;
                return false;
            }
            finally
            {
                if (root != null)
                    PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            output = report.ToString();
            return true;
        }

        private static void ApplyFigmaPresentation(
            GameObject root,
            TMP_FontAsset outfit,
            TMP_FontAsset inter,
            Sprite rounded,
            Dictionary<string, Sprite> sprites,
            StringBuilder report)
        {
            RectTransform shell = root.GetComponent<RectTransform>();
            Vector2 shellSize = shell.rect.size;
            float scale = Mathf.Min(
                shellSize.x / PanelSize.x,
                shellSize.y / PanelSize.y);

            Image figmaBacking = GetOrCreateImage(
                root.transform, "FigmaContent", rounded);
            SetCenteredRect(figmaBacking.rectTransform, PanelSize, scale);
            figmaBacking.type = Image.Type.Sliced;
            figmaBacking.color = PanelColor;
            figmaBacking.raycastTarget = false;
            figmaBacking.transform.SetAsFirstSibling();

            RectTransform content = (RectTransform)Require(
                root.transform, "Content");
            SetCenteredRect(content, PanelSize, scale);

            Transform titleBar = Require(root.transform, "TitleBar");
            Image titleBarImage = titleBar.GetComponent<Image>();
            if (titleBarImage != null)
                titleBarImage.color = Color.clear;

            TMP_Text title = Require(titleBar, "Title").GetComponent<TMP_Text>();
            SetTopLeftScaledRect(title.rectTransform,
                new Vector2(40f, 102f), new Vector2(500f, 70f), scale);
            StyleText(title, "Home", outfit, 62.4f,
                FontWeight.Light, PrimaryText, TextAlignmentOptions.Left);

            TMP_Text headerSubtitle = titleBar.Find("HeaderSubtitle")?
                .GetComponent<TMP_Text>();
            if (headerSubtitle == null)
                headerSubtitle = CreateText("HeaderSubtitle", titleBar, title);
            SetTopLeftScaledRect(headerSubtitle.rectTransform,
                new Vector2(40f, 172.4f), new Vector2(500f, 24f), scale);
            StyleText(headerSubtitle, "Choose what you want to do", inter,
                14.08f, FontWeight.Light, SecondaryText,
                TextAlignmentOptions.Left);

            BuildWordmark(content, outfit, rounded, sprites["layers"]);
            Transform wordmark = content.Find("Wordmark");
            if (wordmark != null) wordmark.gameObject.SetActive(false);
            Image brandLogo = GetOrCreateImage(
                content, "BrandLogo", sprites["brand-logo"]);
            SetTopLeftRect(brandLogo.rectTransform,
                new Vector2(40f, 42f), new Vector2(72f, 54f));
            brandLogo.type = Image.Type.Simple;
            brandLogo.preserveAspect = true;
            brandLogo.color = Color.white;
            brandLogo.raycastTarget = false;
            Image separator = GetOrCreateImage(content, "Separator", null);
            SetTopLeftRect(separator.rectTransform,
                new Vector2(40f, 221.4f), new Vector2(800f, 1f));
            separator.color = new Color(
                BorderColor.r, BorderColor.g, BorderColor.b, 0.7f);
            separator.raycastTarget = false;

            TMP_Text footer = GetOrCreateText(content, "Footer", inter);
            SetTopLeftRect(footer.rectTransform,
                new Vector2(650f, 524f), new Vector2(190f, 18f));
            StyleText(footer, "v4.2.1 \u00B7 HoloLens 2", inter, 10.88f,
                FontWeight.Regular,
                new Color(0.2902f, 0.3647f, 0.4314f, 1f),
                TextAlignmentOptions.Right);

            string[] iconKeys =
            {
                "square-check-big", "cpu",
                "chart-no-axes-column-increasing", "settings",
                "database", "info"
            };
            string[] descriptions =
            {
                "Manage work orders and field inspection checklists assigned to your team.",
                "Intelligent diagnostics, part recommendations, and guided repair workflows.",
                "Equipment analytics, inspection histories, and compliance documentation.",
                "Workspace preferences, notifications, and HoloLens device configuration.",
                "Technical documentation, schematics, and equipment asset libraries.",
                "Version information, licensing details, and support resources."
            };
            Vector2[] positions =
            {
                new Vector2(40f, 246.4f), new Vector2(310f, 246.4f),
                new Vector2(580f, 246.4f), new Vector2(40f, 376.4f),
                new Vector2(310f, 376.4f), new Vector2(580f, 376.4f)
            };

            for (int i = 0; i < D3HomeBaseline.CardNames.Length; i++)
            {
                Transform card = Require(content,
                    D3HomeBaseline.CardNames[i]);
                StyleFigmaCard(card, positions[i], descriptions[i],
                    sprites[iconKeys[i]], sprites["chevron-right"],
                    outfit, inter, rounded);
            }

            report.AppendLine("APPLIED  880 x 580 Figma composition at scale " +
                              scale.ToString("0.#####"));
            report.AppendLine("APPLIED  six Authored Home cards and Lucide icon set");
            report.AppendLine("PRESERVED card navigation/listeners and spatial controls");
        }

        private static void BuildWordmark(
            Transform content,
            TMP_FontAsset outfit,
            Sprite rounded,
            Sprite layers)
        {
            RectTransform wordmark = GetOrCreateRect(content, "Wordmark");
            SetTopLeftRect(wordmark,
                new Vector2(40f, 52f), new Vector2(150f, 22f));

            Image container = GetOrCreateImage(
                wordmark, "IconContainer", rounded);
            SetTopLeftRect(container.rectTransform,
                Vector2.zero, new Vector2(22f, 22f));
            container.color = new Color(
                Accent.r, Accent.g, Accent.b, 0.08f);
            container.type = Image.Type.Sliced;
            container.raycastTarget = false;

            Image icon = GetOrCreateImage(container.transform, "Icon", layers);
            SetCenteredChildRect(icon.rectTransform, new Vector2(11f, 11f));
            icon.color = new Color(
                Accent.r, Accent.g, Accent.b, 0.75f);
            icon.type = Image.Type.Simple;
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            TMP_Text label = GetOrCreateText(wordmark, "Label", outfit);
            SetTopLeftRect(label.rectTransform,
                new Vector2(30f, 0f), new Vector2(120f, 22f));
            StyleText(label, "NORSCOPE", outfit, 10.4f,
                FontWeight.Medium,
                new Color(Accent.r, Accent.g, Accent.b, 0.5f),
                TextAlignmentOptions.MidlineLeft);
            label.characterSpacing = 22f;
        }

        private static void StyleFigmaCard(
            Transform card,
            Vector2 position,
            string description,
            Sprite iconSprite,
            Sprite chevronSprite,
            TMP_FontAsset outfit,
            TMP_FontAsset inter,
            Sprite rounded)
        {
            string path = card.name;
            RectTransform cardRt = (RectTransform)card;
            SetTopLeftRect(cardRt, position, CardSize);

            Image border = card.GetComponent<Image>();
            Image background = card.Find("Background")?.GetComponent<Image>();
            Image icon = card.Find("Icon")?.GetComponent<Image>();
            TMP_Text title = card.Find("Title")?.GetComponent<TMP_Text>();
            TMP_Text subtitle = card.Find("Subtitle")?.GetComponent<TMP_Text>();
            HomePanelCard cardLogic = card.GetComponent<HomePanelCard>();
            if (border == null || background == null || icon == null ||
                title == null || subtitle == null || cardLogic == null)
                throw new InvalidOperationException(
                    "Card authored references missing: " + path);

            border.sprite = rounded;
            border.type = Image.Type.Sliced;
            border.color = BorderColor;
            background.sprite = rounded;
            background.type = Image.Type.Sliced;
            background.color = CardColor;
            SetStretchRect(background.rectTransform, 1f);

            Image shadow = GetOrCreateImage(card, "Shadow", rounded);
            SetStretchRect(shadow.rectTransform, -4f);
            shadow.rectTransform.anchoredPosition = new Vector2(0f, -2f);
            shadow.color = new Color(0f, 0f, 0f, 0.22f);
            shadow.type = Image.Type.Sliced;
            shadow.raycastTarget = false;
            shadow.transform.SetAsFirstSibling();

            Image iconContainer =
                GetOrCreateImage(card, "IconContainer", rounded);
            SetTopLeftRect(iconContainer.rectTransform,
                new Vector2(22f, 18f), new Vector2(30f, 30f));
            iconContainer.color = new Color(
                Accent.r, Accent.g, Accent.b, 0.07f);
            iconContainer.type = Image.Type.Sliced;
            iconContainer.raycastTarget = false;

            SetTopLeftRect(icon.rectTransform,
                new Vector2(30f, 26f), new Vector2(14f, 14f));
            icon.sprite = iconSprite;
            icon.type = Image.Type.Simple;
            icon.preserveAspect = true;
            icon.color = new Color(
                Accent.r, Accent.g, Accent.b, 0.62f);

            SetTopLeftRect(title.rectTransform,
                new Vector2(63f, 19f), new Vector2(155f, 26f));
            StyleText(title, title.text, outfit, 18.08f,
                FontWeight.Medium,
                new Color(0.902f, 0.9451f, 0.9882f, 0.9f),
                TextAlignmentOptions.MidlineLeft);

            SetTopLeftRect(subtitle.rectTransform,
                new Vector2(63f, 58f), new Vector2(175f, 48f));
            StyleText(subtitle, description, inter, 12.8f,
                FontWeight.Light, SecondaryText,
                TextAlignmentOptions.TopLeft);
            subtitle.lineSpacing = 65f;
            subtitle.textWrappingMode = TextWrappingModes.Normal;

            Image chevron =
                GetOrCreateImage(card, "Chevron", chevronSprite);
            SetTopLeftRect(chevron.rectTransform,
                new Vector2(224f, 26f), new Vector2(14f, 14f));
            chevron.sprite = chevronSprite;
            chevron.type = Image.Type.Simple;
            chevron.preserveAspect = true;
            chevron.color = new Color(
                0.2902f, 0.3647f, 0.4314f, 1f);
            chevron.raycastTarget = false;

            Image sheen = GetOrCreateImage(card, "TopSheen", null);
            SetTopLeftRect(sheen.rectTransform,
                new Vector2(13f, 0f), new Vector2(234f, 1f));
            sheen.color = new Color(1f, 1f, 1f, 0.04f);
            sheen.raycastTarget = false;

            Image focusOutline =
                GetOrCreateImage(card, "FocusOutline", rounded);
            SetStretchRect(focusOutline.rectTransform, -3f);
            focusOutline.color = new Color(
                Accent.r, Accent.g, Accent.b, 0f);
            focusOutline.type = Image.Type.Sliced;
            focusOutline.raycastTarget = false;

            Image focusAccent =
                GetOrCreateImage(card, "FocusAccent", null);
            SetBottomCenterRect(focusAccent.rectTransform,
                new Vector2(166.4f, 2f));
            focusAccent.color = new Color(
                Accent.r, Accent.g, Accent.b, 0f);
            focusAccent.raycastTarget = false;

            SerializedObject cardSo = new SerializedObject(cardLogic);
            cardSo.FindProperty("presentationMode").enumValueIndex =
                (int)HomePanelCard.PresentationMode.Authored;
            cardSo.FindProperty("authoredFocusOutline").objectReferenceValue =
                focusOutline;
            cardSo.FindProperty("authoredFocusAccent").objectReferenceValue =
                focusAccent;
            cardSo.ApplyModifiedPropertiesWithoutUndo();

            UiButtonPressScale press = card.GetComponent<UiButtonPressScale>();
            if (press != null)
            {
                SerializedObject pressSo = new SerializedObject(press);
                pressSo.FindProperty("pressedScale").floatValue = 1f;
                pressSo.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static TMP_FontAsset GetOrCreateTmpFont(
            string fontPath, string assetPath, StringBuilder report)
        {
            TMP_FontAsset existing =
                AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            Font source = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
            if (source == null) return null;
            if (existing != null)
            {
                return RepairTmpFontAsset(
                    source, existing, assetPath, report)
                    ? existing
                    : null;
            }
            TMP_FontAsset created = TMP_FontAsset.CreateFontAsset(source);
            if (created == null) return null;
            created.name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            AssetDatabase.CreateAsset(created, assetPath);
            AddGeneratedFontSubAssets(created, created, report);
            report.AppendLine("CREATED " + assetPath);
            return created;
        }

        private static bool RepairTmpFontAsset(
            string fontPath, string assetPath, StringBuilder report)
        {
            Font source = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
            TMP_FontAsset existing =
                AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (source == null)
            {
                report.AppendLine("FAIL  Missing source font: " + fontPath);
                return false;
            }
            if (existing == null)
            {
                report.AppendLine("FAIL  Missing TMP font asset: " + assetPath);
                return false;
            }

            return RepairTmpFontAsset(source, existing, assetPath, report);
        }

        private static bool RepairTmpFontAsset(
            Font source, TMP_FontAsset existing, string assetPath,
            StringBuilder report)
        {
            if (existing.material != null && existing.atlasTexture != null)
            {
                report.AppendLine("PASS  Font asset already renderable: " +
                                  assetPath);
                return true;
            }

            TMP_FontAsset generated = TMP_FontAsset.CreateFontAsset(source);
            if (generated == null || generated.material == null ||
                generated.atlasTexture == null)
            {
                if (generated != null)
                    UnityEngine.Object.DestroyImmediate(generated);
                report.AppendLine("FAIL  TMP could not generate render data: " +
                                  assetPath);
                return false;
            }

            string preservedName = existing.name;
            Material material = generated.material;
            Texture2D atlas = generated.atlasTexture;
            material.name = preservedName + " Material";
            atlas.name = preservedName + " Atlas";

            AssetDatabase.AddObjectToAsset(material, existing);
            AssetDatabase.AddObjectToAsset(atlas, existing);
            EditorUtility.CopySerialized(generated, existing);
            existing.name = preservedName;
            EditorUtility.SetDirty(existing);
            UnityEngine.Object.DestroyImmediate(generated);

            bool repaired =
                existing.material != null && existing.atlasTexture != null;
            report.AppendLine((repaired ? "REPAIRED  " : "FAIL  ") +
                              assetPath +
                              " (main asset GUID and prefab references preserved)");
            return repaired;
        }

        private static void AddGeneratedFontSubAssets(
            TMP_FontAsset generated, TMP_FontAsset owner, StringBuilder report)
        {
            if (generated.material == null || generated.atlasTexture == null)
            {
                report.AppendLine("WARNING  Generated TMP font has incomplete " +
                                  "render data: " + owner.name);
                return;
            }

            generated.material.name = owner.name + " Material";
            generated.atlasTexture.name = owner.name + " Atlas";
            AssetDatabase.AddObjectToAsset(generated.material, owner);
            AssetDatabase.AddObjectToAsset(generated.atlasTexture, owner);
        }

        private static Sprite PrepareSprite(
            string path, StringBuilder report)
        {
            TextureImporter importer =
                AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null && System.IO.File.Exists(path))
            {
                AssetDatabase.ImportAsset(path,
                    ImportAssetOptions.ForceSynchronousImport |
                    ImportAssetOptions.ForceUpdate);
                importer = AssetImporter.GetAtPath(path) as TextureImporter;
            }
            if (importer == null) return null;
            bool changed = importer.textureType != TextureImporterType.Sprite ||
                           importer.spriteImportMode != SpriteImportMode.Single ||
                           importer.mipmapEnabled;
            if (changed)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
                report.AppendLine("CONFIGURED " + path + " as UI Sprite");
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static RectTransform GetOrCreateRect(
            Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null) return existing as RectTransform;
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        private static Image GetOrCreateImage(
            Transform parent, string name, Sprite sprite)
        {
            Transform existing = parent.Find(name);
            Image image = existing != null
                ? existing.GetComponent<Image>()
                : null;
            if (image == null)
            {
                GameObject go = new GameObject(name,
                    typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(parent, false);
                image = go.GetComponent<Image>();
            }
            if (sprite != null)
            {
                image.sprite = sprite;
            }
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text GetOrCreateText(
            Transform parent, string name, TMP_FontAsset font)
        {
            Transform existing = parent.Find(name);
            TMP_Text text = existing != null
                ? existing.GetComponent<TMP_Text>()
                : null;
            if (text == null)
            {
                GameObject go = new GameObject(name,
                    typeof(RectTransform), typeof(CanvasRenderer),
                    typeof(TextMeshProUGUI));
                go.transform.SetParent(parent, false);
                text = go.GetComponent<TMP_Text>();
            }
            text.font = font;
            text.raycastTarget = false;
            return text;
        }

        private static void StyleText(
            TMP_Text text, string value, TMP_FontAsset font, float size,
            FontWeight weight, Color color, TextAlignmentOptions alignment)
        {
            text.text = value;
            text.font = font;
            text.fontSize = size;
            text.fontWeight = weight;
            text.color = color;
            text.alignment = alignment;
            text.margin = Vector4.zero;
            text.raycastTarget = false;
        }

        private static void SetCenteredRect(
            RectTransform rt, Vector2 size, float scale)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot =
                new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = size;
            rt.localScale = Vector3.one * scale;
            rt.localRotation = Quaternion.identity;
        }

        private static void SetTopLeftScaledRect(
            RectTransform rt, Vector2 position, Vector2 size, float scale)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition =
                new Vector2(position.x * scale, -position.y * scale);
            rt.sizeDelta = size;
            rt.localScale = Vector3.one * scale;
            rt.localRotation = Quaternion.identity;
        }

        private static void SetTopLeftRect(
            RectTransform rt, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(position.x, -position.y);
            rt.sizeDelta = size;
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
        }

        private static void SetCenteredChildRect(
            RectTransform rt, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot =
                new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = size;
            rt.localScale = Vector3.one;
        }

        private static void SetStretchRect(RectTransform rt, float inset)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.one * (-2f * inset);
            rt.localScale = Vector3.one;
        }

        private static void SetBottomCenterRect(
            RectTransform rt, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot =
                new Vector2(0.5f, 0f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = size;
            rt.localScale = Vector3.one;
        }

        private static bool BuildPlan(bool apply, out string output)
        {
            bool clean = D3HomeBaseline.Compare(false, out string cleanReport);
            bool approvedState = D3HomeBaseline.Compare(true, out string approvedReport);
            if (!clean && !approvedState)
            {
                output = "Home differs from both the captured baseline and the approved " +
                         "D3 state.\n" + cleanReport + "\n" + approvedReport;
                return false;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(D3HomeBaseline.HomePath);
            var report = new Report();
            bool changed = false;
            try
            {
                RectTransform rootRt = root.GetComponent<RectTransform>();
                changed |= Rect(report, rootRt, "HomePanel",
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    Vector2.zero, PanelSize, apply);

                NorscopePanelRoot panel = root.GetComponent<NorscopePanelRoot>();
                SerializedObject panelSo = new SerializedObject(panel);
                changed |= SerializedVector2(report, panelSo, "sizeMeters",
                    new Vector2(0.69f, 0.47f), "HomePanel", apply);
                changed |= SerializedVector2(report, panelSo, "sizePixels",
                    PanelSize, "HomePanel", apply);
                SerializedProperty titleReference = panelSo.FindProperty("title");
                if (titleReference.objectReferenceValue != null)
                {
                    report.Change("HomePanel", "NorscopePanelRoot", "title",
                        titleReference.objectReferenceValue.name, "<null>");
                    if (apply) titleReference.objectReferenceValue = null;
                    changed = true;
                }
                if (apply) panelSo.ApplyModifiedPropertiesWithoutUndo();

                Image rootBackground = root.transform.Find("Background")?
                    .GetComponent<Image>();
                Image rootBorder = root.transform.Find("Border")?
                    .GetComponent<Image>();
                changed |= ColorValue(report, rootBackground, "Image", "color",
                    PanelColor, apply);
                changed |= ColorValue(report, rootBorder, "Image", "color",
                    BorderColor, apply);

                Transform titleBar = Require(root.transform, "TitleBar");
                RectTransform titleBarRt = (RectTransform)titleBar;
                changed |= Rect(report, titleBarRt, "HomePanel/TitleBar",
                    new Vector2(0f, 1f), new Vector2(0f, 1f),
                    Vector2.zero, new Vector2(690f, 96f), apply);

                TMP_Text title = titleBar.Find("Title").GetComponent<TMP_Text>();
                changed |= Rect(report, title.rectTransform,
                    "HomePanel/TitleBar/Title",
                    new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(24f, -12f), new Vector2(360f, 42f), apply);
                changed |= Text(report, title, "HomePanel/TitleBar/Title",
                    "Home", 30f, FontWeight.Bold, PrimaryText, apply);

                TMP_Text headerSubtitle = titleBar.Find("HeaderSubtitle")?
                    .GetComponent<TMP_Text>();
                if (headerSubtitle == null)
                {
                    report.Change("HomePanel/TitleBar/HeaderSubtitle",
                        "GameObject", "hierarchy", "<missing>",
                        "CREATE RectTransform + CanvasRenderer + TextMeshProUGUI");
                    if (apply)
                    {
                        headerSubtitle = CreateText("HeaderSubtitle", titleBar, title);
                    }
                    changed = true;
                }
                if (headerSubtitle != null)
                {
                    changed |= Rect(report, headerSubtitle.rectTransform,
                        "HomePanel/TitleBar/HeaderSubtitle",
                        new Vector2(0f, 1f), new Vector2(0f, 1f),
                        new Vector2(24f, -54f), new Vector2(420f, 28f), apply);
                    changed |= Text(report, headerSubtitle,
                        "HomePanel/TitleBar/HeaderSubtitle",
                        "Choose what you want to do", 14f,
                        FontWeight.Regular, SecondaryText, apply);
                }
                else
                {
                    ReportNewTextProperties(report);
                }

                RectTransform close = FindRect(titleBar, "CloseButton");
                if (close != null)
                    changed |= RectPositionOnly(report, close,
                        "HomePanel/TitleBar/CloseButton",
                        new Vector2(1f, 1f), new Vector2(-20f, -18f), apply);

                RectTransform grab = FindRect(titleBar, "GrabRegion");
                changed |= RectStretch(report, grab,
                    "HomePanel/TitleBar/GrabRegion",
                    new Vector2(0f, 0f), new Vector2(1f, 1f),
                    new Vector2(-32f, 0f), new Vector2(-64f, 0f), apply);

                RectTransform content = (RectTransform)Require(root.transform, "Content");
                changed |= Rect(report, content, "HomePanel/Content",
                    new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(0f, -96f), new Vector2(690f, 374f), apply);

                Vector2[] positions =
                {
                    new Vector2(20f, -18f), new Vector2(242f, -18f),
                    new Vector2(464f, -18f), new Vector2(20f, -196f),
                    new Vector2(242f, -196f), new Vector2(464f, -196f)
                };
                for (int i = 0; i < D3HomeBaseline.CardNames.Length; i++)
                {
                    Transform card = Require(content,
                        D3HomeBaseline.CardNames[i]);
                    changed |= StyleCard(report, card, positions[i], apply);
                }

                report.Info("Chevron asset required: no approved right-arrow sprite " +
                            "exists; all six Image.sprite fields remain empty.");
                if (apply && changed)
                    PrefabUtility.SaveAsPrefabAsset(root, D3HomeBaseline.HomePath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            output = "Planned changes: " + report.Changes +
                     "\nState before plan: " + (clean ? "CAPTURED BASELINE" :
                         "APPROVED D3 (idempotent update)") +
                     "\n\n" + report.Text;
            return true;
        }

        private static bool StyleCard(
            Report report, Transform card, Vector2 position, bool apply)
        {
            bool changed = Rect(report, (RectTransform)card,
                "HomePanel/Content/" + card.name,
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                position, CardSize, apply);
            string path = "HomePanel/Content/" + card.name;
            Image border = card.GetComponent<Image>();
            Image background = card.Find("Background")?.GetComponent<Image>();
            changed |= ColorValue(report, border, "Image", "color",
                BorderColor, apply, path);
            changed |= ColorValue(report, background, "Image", "color",
                CardColor, apply, path + "/Background");

            Image icon = card.Find("Icon")?.GetComponent<Image>();
            if (icon != null)
            {
                changed |= Rect(report, icon.rectTransform, path + "/Icon",
                    new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(18f, -18f), new Vector2(48f, 48f), apply);
                changed |= ColorValue(report, icon, "Image", "color",
                    Accent, apply, path + "/Icon");
            }

            TMP_Text title = card.Find("Title")?.GetComponent<TMP_Text>();
            changed |= Rect(report, title.rectTransform, path + "/Title",
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(18f, -78f), new Vector2(156f, 30f), apply);
            changed |= Text(report, title, path + "/Title",
                title.text, 18f, FontWeight.SemiBold, PrimaryText, apply);

            TMP_Text subtitle = card.Find("Subtitle")?.GetComponent<TMP_Text>();
            changed |= Rect(report, subtitle.rectTransform, path + "/Subtitle",
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(18f, -112f), new Vector2(156f, 42f), apply);
            changed |= Text(report, subtitle, path + "/Subtitle",
                subtitle.text, 12f, FontWeight.Regular, SecondaryText, apply);
            if (subtitle.enableWordWrapping != true)
            {
                report.Change(path + "/Subtitle", "TextMeshProUGUI",
                    "enableWordWrapping", subtitle.enableWordWrapping.ToString(), "True");
                if (apply) subtitle.enableWordWrapping = true;
                changed = true;
            }

            Image chevron = card.Find("Chevron")?.GetComponent<Image>();
            if (chevron == null)
            {
                report.Change(path + "/Chevron", "GameObject", "hierarchy",
                    "<missing>", "CREATE RectTransform + CanvasRenderer + Image");
                if (apply)
                {
                    GameObject go = new GameObject("Chevron",
                        typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    go.transform.SetParent(card, false);
                    chevron = go.GetComponent<Image>();
                    chevron.sprite = null;
                    chevron.raycastTarget = false;
                }
                changed = true;
            }
            if (chevron != null)
            {
                changed |= Rect(report, chevron.rectTransform, path + "/Chevron",
                    new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(176f, -70f), new Vector2(16f, 28f), apply);
                changed |= ColorValue(report, chevron, "Image", "color",
                    PrimaryText, apply, path + "/Chevron");
                if (chevron.sprite != null)
                {
                    report.Change(path + "/Chevron", "Image", "sprite",
                        chevron.sprite.name, "<null — approved asset unavailable>");
                    if (apply) chevron.sprite = null;
                    changed = true;
                }
                if (chevron.raycastTarget)
                {
                    report.Change(path + "/Chevron", "Image", "raycastTarget",
                        "True", "False");
                    if (apply) chevron.raycastTarget = false;
                    changed = true;
                }
            }
            else
            {
                report.Change(path + "/Chevron", "RectTransform", "authored layout",
                    "<new>", "position (176,-70), size (16,28), top-left anchors");
                report.Change(path + "/Chevron", "Image", "sprite/raycastTarget",
                    "<new>", "null / False");
            }
            return changed;
        }

        private static bool Validate(out string output)
        {
            var text = new StringBuilder();
            bool ok = true;
            void Check(bool value, string label)
            {
                text.AppendLine((value ? "PASS  " : "FAIL  ") + label);
                if (!value) ok = false;
            }

            Check(D3HomeBaseline.Compare(true, out string baseline),
                "Only approved Home hierarchy and title-reference changes occurred");
            if (!baseline.StartsWith("Home functional baseline matches"))
                text.AppendLine(baseline);

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                D3HomeBaseline.HomePath);
            Check(prefab != null, "HomePanel.prefab exists");
            if (prefab != null)
            {
                NorscopePanelRoot panel = prefab.GetComponent<NorscopePanelRoot>();
                SerializedObject so = new SerializedObject(panel);
                Check(so.FindProperty("title").objectReferenceValue == null,
                    "Home-only NorscopePanelRoot.title reference is cleared");
                Check(so.FindProperty("theme").objectReferenceValue != null &&
                      so.FindProperty("worldSpaceConfig").objectReferenceValue != null &&
                      so.FindProperty("root").objectReferenceValue != null &&
                      so.FindProperty("background").objectReferenceValue != null &&
                      so.FindProperty("border").objectReferenceValue != null,
                    "All other NorscopePanelRoot references remain assigned");

                Transform titleBar = prefab.transform.Find("TitleBar");
                TMP_Text title = titleBar?.Find("Title")?.GetComponent<TMP_Text>();
                TMP_Text subtitle = titleBar?.Find("HeaderSubtitle")?
                    .GetComponent<TMP_Text>();
                Check(title != null && title.text == "Home" &&
                      Mathf.Approximately(title.fontSize, 62.4f) &&
                      title.fontWeight == FontWeight.Light &&
                      title.font ==
                      AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OutfitTmpPath),
                    "Home title matches the approved Outfit light hierarchy");
                Check(subtitle != null &&
                      subtitle.text == "Choose what you want to do" &&
                      subtitle.font ==
                      AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(InterTmpPath),
                    "Approved Inter header subtitle is authored");

                HomePanelCard[] cards =
                    prefab.GetComponentsInChildren<HomePanelCard>(true);
                Check(cards.Length == 6, "Exactly six original Home cards remain");
                Check(cards.All(card =>
                {
                    Image image = card.transform.Find("Chevron")?
                        .GetComponent<Image>();
                    return image != null && image.sprite != null &&
                           !image.raycastTarget;
                }), "Six approved non-interactive Lucide Chevrons are assigned");
                Check(cards.All(card =>
                    ((RectTransform)card.transform).sizeDelta == CardSize),
                    "All six cards have equal approved proportions");
                Check(cards.All(card =>
                    card.VisualPresentationMode ==
                    HomePanelCard.PresentationMode.Authored),
                    "Exactly the six Home cards use Authored presentation");
                Check(cards.All(card =>
                {
                    UiButtonPressScale press =
                        card.GetComponent<UiButtonPressScale>();
                    if (press == null) return false;
                    SerializedObject pressSo = new SerializedObject(press);
                    return Mathf.Approximately(
                        pressSo.FindProperty("pressedScale").floatValue, 1f);
                }), "Legacy press-scale component cannot compete with Authored scale");
                Check(cards.All(card =>
                {
                    string[] decorative =
                    {
                        "Shadow", "IconContainer", "Chevron",
                        "TopSheen", "FocusOutline", "FocusAccent"
                    };
                    return decorative.All(name =>
                    {
                        Image image = card.transform.Find(name)?
                            .GetComponent<Image>();
                        return image != null && !image.raycastTarget;
                    });
                }), "All approved card decoration exists and ignores raycasts");
                RectTransform figma = prefab.transform.Find("FigmaContent")
                    as RectTransform;
                Check(figma != null && figma.sizeDelta == PanelSize &&
                      Mathf.Approximately(figma.localScale.x, 690f / 880f),
                    "880 x 580 Figma composition is proportionally fitted");
                RectTransform content = prefab.transform.Find("Content")
                    as RectTransform;
                Check(content != null && content.sizeDelta == PanelSize &&
                      Mathf.Approximately(content.localScale.x, 690f / 880f),
                    "Existing authored Content uses the matching fit scale");
                Check(prefab.transform.Find("Content/Wordmark") != null &&
                      prefab.transform.Find("Content/BrandLogo") != null &&
                      prefab.transform.Find("Content/Separator") != null &&
                      prefab.transform.Find("Content/Footer") != null,
                    "Brand logo, wordmark slot, separator and footer are authored");
                Image brandLogo = prefab.transform.Find("Content/BrandLogo")?
                    .GetComponent<Image>();
                Check(brandLogo != null && brandLogo.sprite != null &&
                      brandLogo.preserveAspect && !brandLogo.raycastTarget,
                    "Supplied NORSCOPE brand sprite is assigned above Home title");
                Check(prefab.GetComponentsInChildren<Button>(true).Length >= 7,
                    "All original card and close Buttons remain");
                Check(prefab.transform.Find("TitleBar/GrabRegion") != null,
                    "Grab region remains");
                Check(prefab.transform.Find("ResizeHandles/BottomRight") != null,
                    "Resize handle remains");
                Check(prefab.GetComponent<SpatialWindowBindings>() != null,
                    "Existing Norscope XRI SpatialWindowBindings remains");
            }
            int authoredOutsideHome = AssetDatabase.FindAssets("t:Prefab")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path != D3HomeBaseline.HomePath)
                .Select(path => AssetDatabase.LoadAssetAtPath<GameObject>(path))
                .Where(value => value != null)
                .SelectMany(value =>
                    value.GetComponentsInChildren<HomePanelCard>(true))
                .Count(card => card.VisualPresentationMode ==
                    HomePanelCard.PresentationMode.Authored);
            Check(authoredOutsideHome == 0,
                "No HomePanelCard outside HomePanel uses Authored mode");
            Check(NoD3RuntimeSource(),
                "No additional D3-named runtime source was introduced");
            output = text.ToString();
            return ok;
        }

        private static bool NoD3RuntimeSource()
        {
            return !System.IO.Directory.GetFiles(
                    "Assets/Norscope/Runtime", "*D3*.cs",
                    System.IO.SearchOption.AllDirectories).Any();
        }

        private static TMP_Text CreateText(
            string name, Transform parent, TMP_Text reference)
        {
            GameObject go = new GameObject(name,
                typeof(RectTransform), typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            TMP_Text text = go.GetComponent<TMP_Text>();
            text.font = reference.font;
            text.raycastTarget = false;
            text.alignment = TextAlignmentOptions.Left;
            return text;
        }

        private static void ReportNewTextProperties(Report report)
        {
            report.Change("HomePanel/TitleBar/HeaderSubtitle", "RectTransform",
                "authored layout", "<new>",
                "position (24,-54), size (420,28), top-left anchors");
            report.Change("HomePanel/TitleBar/HeaderSubtitle", "TextMeshProUGUI",
                "text/fontSize/fontWeight/color",
                "<new>", "Choose what you want to do / 14 / Regular / muted");
        }

        private static bool Rect(
            Report report, RectTransform value, string path,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 position,
            Vector2 size, bool apply)
        {
            bool changed = false;
            changed |= Vector(report, path, "RectTransform", "anchorMin",
                value.anchorMin, anchorMin, result => value.anchorMin = result, apply);
            changed |= Vector(report, path, "RectTransform", "anchorMax",
                value.anchorMax, anchorMax, result => value.anchorMax = result, apply);
            changed |= Vector(report, path, "RectTransform", "anchoredPosition",
                value.anchoredPosition, position,
                result => value.anchoredPosition = result, apply);
            changed |= Vector(report, path, "RectTransform", "sizeDelta",
                value.sizeDelta, size, result => value.sizeDelta = result, apply);
            if (value.pivot != new Vector2(0f, 1f) && path != "HomePanel")
            {
                report.Change(path, "RectTransform", "pivot",
                    V(value.pivot), V(new Vector2(0f, 1f)));
                if (apply) value.pivot = new Vector2(0f, 1f);
                changed = true;
            }
            return changed;
        }

        private static bool RectPositionOnly(
            Report report, RectTransform value, string path,
            Vector2 anchors, Vector2 position, bool apply)
        {
            bool changed = false;
            changed |= Vector(report, path, "RectTransform", "anchorMin",
                value.anchorMin, anchors, result => value.anchorMin = result, apply);
            changed |= Vector(report, path, "RectTransform", "anchorMax",
                value.anchorMax, anchors, result => value.anchorMax = result, apply);
            changed |= Vector(report, path, "RectTransform", "anchoredPosition",
                value.anchoredPosition, position,
                result => value.anchoredPosition = result, apply);
            return changed;
        }

        private static bool RectStretch(
            Report report, RectTransform value, string path,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 position,
            Vector2 size, bool apply)
        {
            bool changed = false;
            changed |= Vector(report, path, "RectTransform", "anchorMin",
                value.anchorMin, anchorMin, result => value.anchorMin = result, apply);
            changed |= Vector(report, path, "RectTransform", "anchorMax",
                value.anchorMax, anchorMax, result => value.anchorMax = result, apply);
            changed |= Vector(report, path, "RectTransform", "anchoredPosition",
                value.anchoredPosition, position,
                result => value.anchoredPosition = result, apply);
            changed |= Vector(report, path, "RectTransform", "sizeDelta",
                value.sizeDelta, size, result => value.sizeDelta = result, apply);
            if (value.pivot != new Vector2(0.5f, 0.5f))
            {
                report.Change(path, "RectTransform", "pivot",
                    V(value.pivot), V(new Vector2(0.5f, 0.5f)));
                if (apply) value.pivot = new Vector2(0.5f, 0.5f);
                changed = true;
            }
            return changed;
        }

        private static bool Text(
            Report report, TMP_Text value, string path, string content,
            float size, FontWeight weight, Color color, bool apply)
        {
            bool changed = false;
            if (value.text != content)
            {
                report.Change(path, "TextMeshProUGUI", "text", value.text, content);
                if (apply) value.text = content;
                changed = true;
            }
            if (!Mathf.Approximately(value.fontSize, size))
            {
                report.Change(path, "TextMeshProUGUI", "fontSize",
                    value.fontSize.ToString("0.##"), size.ToString("0.##"));
                if (apply) value.fontSize = size;
                changed = true;
            }
            if (value.fontWeight != weight)
            {
                report.Change(path, "TextMeshProUGUI", "fontWeight",
                    value.fontWeight.ToString(), weight.ToString());
                if (apply) value.fontWeight = weight;
                changed = true;
            }
            changed |= ColorValue(report, value, "TextMeshProUGUI", "color",
                color, apply, path);
            if (value.alignment != TextAlignmentOptions.Left)
            {
                report.Change(path, "TextMeshProUGUI", "alignment",
                    value.alignment.ToString(), TextAlignmentOptions.Left.ToString());
                if (apply) value.alignment = TextAlignmentOptions.Left;
                changed = true;
            }
            return changed;
        }

        private static bool ColorValue(
            Report report, Graphic value, string component, string property,
            Color desired, bool apply, string path = null)
        {
            if (value == null || Approximately(value.color, desired)) return false;
            string objectPath = path ?? "HomePanel/" + value.gameObject.name;
            report.Change(objectPath, component, property,
                C(value.color), C(desired));
            if (apply) value.color = desired;
            return true;
        }

        private static bool SerializedVector2(
            Report report, SerializedObject so, string propertyName,
            Vector2 desired, string path, bool apply)
        {
            SerializedProperty property = so.FindProperty(propertyName);
            if ((property.vector2Value - desired).sqrMagnitude < 0.0001f)
                return false;
            report.Change(path, "NorscopePanelRoot", propertyName,
                V(property.vector2Value), V(desired));
            if (apply) property.vector2Value = desired;
            return true;
        }

        private static bool Vector(
            Report report, string path, string component, string property,
            Vector2 before, Vector2 after, Action<Vector2> setter, bool apply)
        {
            if ((before - after).sqrMagnitude < 0.0001f) return false;
            report.Change(path, component, property, V(before), V(after));
            if (apply) setter(after);
            return true;
        }

        private static Transform Require(Transform root, string path)
        {
            Transform value = root.Find(path);
            if (value == null) throw new InvalidOperationException(
                "Required Home object missing: " + path);
            return value;
        }

        private static RectTransform FindRect(Transform root, string name)
        {
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            Transform found = all.FirstOrDefault(value => value.name == name);
            return found as RectTransform;
        }

        private static Image FindImage(Transform root, string name)
        {
            return root.GetComponentsInChildren<Image>(true)
                .FirstOrDefault(value => value.gameObject.name == name);
        }

        private static bool Approximately(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < 0.0001f &&
                   Mathf.Abs(a.g - b.g) < 0.0001f &&
                   Mathf.Abs(a.b - b.b) < 0.0001f &&
                   Mathf.Abs(a.a - b.a) < 0.0001f;
        }

        private static string V(Vector2 value) =>
            "(" + value.x.ToString("0.##") + "," +
            value.y.ToString("0.##") + ")";

        private static string C(Color value) =>
            "(" + value.r.ToString("0.###") + "," +
            value.g.ToString("0.###") + "," +
            value.b.ToString("0.###") + "," +
            value.a.ToString("0.###") + ")";
    }
}
