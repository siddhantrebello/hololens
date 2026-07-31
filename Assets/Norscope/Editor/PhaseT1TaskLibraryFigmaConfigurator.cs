using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Norscope.TaskLibrary;
using Norscope.UI;
using Norscope.Windows;
using Object = UnityEngine.Object;

namespace Norscope.Editor
{
    /// <summary>
    /// Task-only implementation of the approved taskpanel.zip Figma Make source.
    /// It never touches Home, Active Task, navigation, window manager, or registry.
    /// </summary>
    public static class PhaseT1TaskLibraryFigmaConfigurator
    {
        private const string PanelPath =
            "Assets/Norscope/UI/Panels/TaskLibraryPanel.prefab";
        private const string CardPath =
            "Assets/Norscope/UI/Controls/TaskCard.prefab";
        private const string DatabasePath =
            "Assets/Norscope/Data/TaskLibraryDatabase.asset";
        private const string RoundedPath =
            "Assets/Norscope/UI/DesignSystem/Sprites/UIRounded_8.png";
        private const string OutfitPath =
            "Assets/Norscope/UI/Fonts/Outfit SDF.asset";
        private const string InterPath =
            "Assets/Norscope/UI/Fonts/Inter SDF.asset";
        private const string IconRoot =
            "Assets/Norscope/UI/Icons/Lucide/Raster/";
        private const string LogoPath =
            "Assets/WhatsApp Image 2026-07-19 at 13.42.15-transparent.png";
        private const string BackupPrefKey =
            "Norscope.TaskLibraryFigma.BackupFolder";
        private const string BrandLogoFlag =
            "Temp/NorscopeTaskLibraryBrandLogo.flag";

        private static readonly Color Bg =
            Hex("0D1117");
        private static readonly Color Panel =
            Hex("131A22");
        private static readonly Color Card =
            Hex("18212B");
        private static readonly Color Border =
            Hex("2D3B49");
        private static readonly Color Accent =
            Hex("1EC8F5");
        private static readonly Color Primary =
            Hex("F3F7FB");
        private static readonly Color Secondary =
            Hex("91A2B5");
        private static readonly Color Dim =
            Hex("4A5D6E");
        private static readonly Color Success =
            Hex("39D98A");

        [InitializeOnLoadMethod]
        private static void PollBrandLogoFlag()
        {
            EditorApplication.update -= OnBrandLogoPoll;
            EditorApplication.update += OnBrandLogoPoll;
        }

        private static void OnBrandLogoPoll()
        {
            if (!File.Exists(BrandLogoFlag))
                return;
            File.Delete(BrandLogoFlag);
            EditorApplication.delayCall += ApplyBrandLogo;
        }

        [MenuItem("Norscope/HoloLens/Task Library Figma/6) Apply Brand Logo")]
        public static void ApplyBrandLogo()
        {
            Sprite logo = AssetDatabase.LoadAssetAtPath<Sprite>(LogoPath);
            if (logo == null)
            {
                Debug.LogError("[Norscope Task Figma] Logo missing: " + LogoPath);
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(PanelPath);
            try
            {
                Transform content = FindDeep(root.transform, "FigmaContent") ??
                                    FindDeep(root.transform, "Content");
                if (content == null)
                    throw new InvalidOperationException("Task Library content root missing.");

                DestroyNamed(content, "Wordmark");
                DestroyNamed(content, "WordmarkIcon");
                DestroyNamed(content, "NorscopeLogo");

                Image logoImage = ImageNode(
                    content, "NorscopeLogo", logo, Color.white,
                    new Vector2(40f, 28f), new Vector2(120f, 36f));
                logoImage.type = Image.Type.Simple;
                logoImage.preserveAspect = true;
                logoImage.raycastTarget = false;

                PrefabUtility.SaveAsPrefabAsset(root, PanelPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("Temp");
            File.WriteAllText(
                "Temp/NorscopeTaskLibraryBrandLogo.txt",
                "PASS  NORSCOPE wordmark replaced with logo\n",
                Encoding.UTF8);
            Debug.Log("[Norscope Task Figma] Brand logo Apply COMPLETE");
        }

        private static void DestroyNamed(Transform parent, string name)
        {
            Transform existing = FindDeep(parent, name);
            if (existing != null)
                Object.DestroyImmediate(existing.gameObject);
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeep(root.GetChild(i), name);
                if (found != null) return found;
            }

            return null;
        }
        [MenuItem("Norscope/HoloLens/Task Library Figma/1) Capture Task Baseline")]
        public static void CaptureMenu()
        {
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string folder =
                "Assets/Norscope/UI/Panels/_Backups/TaskLibraryFigma_" + stamp;
            Directory.CreateDirectory(folder);
            string panelBackup = folder + "/TaskLibraryPanel.prefab";
            string cardBackup = folder + "/TaskCard.prefab";
            FileUtil.CopyFileOrDirectory(PanelPath, panelBackup);
            FileUtil.CopyFileOrDirectory(CardPath, cardBackup);
            AssetDatabase.Refresh();

            File.WriteAllText(folder + "/manifest.txt",
                "Captured=" + DateTime.Now.ToString("O") + "\n" +
                PanelPath + "=" + Hash(PanelPath) + "\n" +
                CardPath + "=" + Hash(CardPath) + "\n");
            EditorPrefs.SetString(BackupPrefKey, folder);
            AssetDatabase.Refresh();
            Debug.Log("[Norscope Task Figma] Baseline READY\n" + folder);
        }

        [MenuItem("Norscope/HoloLens/Task Library Figma/2) Dry Run")]
        public static void DryRunMenu()
        {
            bool ready = DryRun(out string report);
            Debug.Log((ready
                    ? "[Norscope Task Figma] Dry run READY\n"
                    : "[Norscope Task Figma] Dry run BLOCKED\n") + report);
            EditorUtility.DisplayDialog("Task Library Figma",
                ready ? "READY. See Console for the complete mapping."
                      : "BLOCKED. See Console.", "OK");
        }

        [MenuItem("Norscope/HoloLens/Task Library Figma/3) Apply")]
        public static void ApplyMenu()
        {
            if (!DryRun(out string preflight))
            {
                Debug.LogError("[Norscope Task Figma] Apply BLOCKED\n" +
                               preflight);
                return;
            }
            if (!EditorUtility.DisplayDialog("Apply Task Library Figma",
                    "Apply the supplied 880-unit Task Library design to only:\n\n" +
                    PanelPath + "\n" + CardPath +
                    "\n\nThe captured backups remain restorable.",
                    "Apply", "Cancel"))
                return;

            PrepareAllSprites();
            ApplyCard();
            ApplyPanel();
            AssignInitialTaskIcons();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Norscope Task Figma] Apply COMPLETE");
        }

        [MenuItem("Norscope/HoloLens/Task Library Figma/4) Validate")]
        public static void ValidateMenu()
        {
            bool ok = Validate(out string report);
            Debug.Log((ok
                    ? "[Norscope Task Figma] Validation PASSED\n"
                    : "[Norscope Task Figma] Validation FAILED\n") + report);
        }

        [MenuItem("Norscope/HoloLens/Task Library Figma/5) Restore")]
        public static void RestoreMenu()
        {
            string folder = EditorPrefs.GetString(BackupPrefKey, string.Empty);
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                Debug.LogError("[Norscope Task Figma] No captured backup.");
                return;
            }
            if (!EditorUtility.DisplayDialog("Restore Task Library",
                    "Restore TaskLibraryPanel.prefab and TaskCard.prefab from:\n" +
                    folder + "?", "Restore", "Cancel"))
                return;

            File.Copy(folder + "/TaskLibraryPanel.prefab", PanelPath, true);
            File.Copy(folder + "/TaskCard.prefab", CardPath, true);
            AssetDatabase.Refresh();
            Debug.Log("[Norscope Task Figma] Restore COMPLETE\n" + folder);
        }

        private static bool DryRun(out string report)
        {
            var text = new StringBuilder();
            var blockers = new List<string>();
            string backup = EditorPrefs.GetString(BackupPrefKey, string.Empty);
            if (!File.Exists(PanelPath)) blockers.Add("TaskLibraryPanel missing.");
            if (!File.Exists(CardPath)) blockers.Add("TaskCard missing.");
            if (!File.Exists(DatabasePath)) blockers.Add("Task database missing.");
            if (string.IsNullOrEmpty(backup) || !Directory.Exists(backup))
                blockers.Add("Capture a fresh Task baseline first.");

            TMP_FontAsset outfit =
                AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OutfitPath);
            TMP_FontAsset inter =
                AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(InterPath);
            if (!Renderable(outfit) || !Renderable(inter))
            {
                blockers.Add("Outfit/Inter TMP assets are not renderable. Run " +
                    "Phase D3 Home/6) Repair Home Text Fonts first.");
            }

            string[] icons =
            {
                "layers", "search", "sliders-horizontal", "chevron-down",
                "clock", "wifi-off", "calendar-days", "play", "file-text",
                "fan", "activity", "wind", "zap", "droplets",
                "chevron-right", "database", "cpu"
            };
            foreach (string icon in icons)
            {
                if (!File.Exists(IconRoot + icon + ".png"))
                    blockers.Add("Missing Lucide raster: " + icon);
            }

            text.AppendLine("SOURCE  taskpanel.zip / src/app/App.tsx");
            text.AppendLine("TARGET  fixed 880 x 650 authored composition");
            text.AppendLine("PREFABS TaskLibraryPanel + TaskCard only");
            text.AppendLine("PANEL   header, search, filters, scroll list, detail, footer");
            text.AppendLine("CARD    50-unit reusable compact row");
            text.AppendLine("DATA    existing TaskDefinition assets; no task names in runtime code");
            text.AppendLine("WINDOW  existing Close/Back/Grab/Resize preserved");
            text.AppendLine("ACTIVE  Start Task uses existing TaskCardView.TaskOpened event");
            foreach (string blocker in blockers)
                text.AppendLine("BLOCKER " + blocker);
            text.AppendLine(blockers.Count == 0
                ? "READY — Apply may proceed."
                : "BLOCKED — no prefab should be modified.");
            report = text.ToString();
            return blockers.Count == 0;
        }

        private static void ApplyCard()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(CardPath);
            try
            {
                for (int i = root.transform.childCount - 1; i >= 0; i--)
                    Object.DestroyImmediate(root.transform.GetChild(i).gameObject);

                RectTransform rt = EnsureRect(root);
                rt.sizeDelta = new Vector2(456f, 50f);
                LayoutElement le = Ensure<LayoutElement>(root);
                le.minHeight = 50f;
                le.preferredHeight = 50f;
                le.flexibleHeight = 0f;

                Sprite rounded = SpriteAt("rounded", RoundedPath);
                TMP_FontAsset outfit =
                    AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OutfitPath);
                TMP_FontAsset inter =
                    AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(InterPath);

                Image border = Ensure<Image>(root);
                StyleImage(border, rounded, Border, true);
                Button button = Ensure<Button>(root);
                button.targetGraphic = border;
                button.transition = Selectable.Transition.None;

                Image surface = ImageNode(root.transform, "Surface", rounded,
                    Card, new Vector2(1f, 1f), new Vector2(455f, 49f));
                Stretch(surface.rectTransform, 1f);
                surface.raycastTarget = false;

                Image selectedAccent = ImageNode(root.transform,
                    "SelectedAccent", rounded, Accent,
                    new Vector2(0f, 11f), new Vector2(2f, 28f));
                selectedAccent.gameObject.SetActive(false);

                Image iconBox = ImageNode(root.transform, "IconContainer",
                    rounded, new Color(Accent.r, Accent.g, Accent.b, 0.07f),
                    new Vector2(13f, 10f), new Vector2(30f, 30f));
                Image icon = ImageNode(iconBox.transform, "Icon", null,
                    new Color(Accent.r, Accent.g, Accent.b, 0.72f),
                    new Vector2(8.5f, 8.5f), new Vector2(13f, 13f));
                icon.preserveAspect = true;

                TMP_Text title = TextNode(root.transform, "TaskTitle", outfit,
                    13.92f, FontWeight.Medium, Primary,
                    new Vector2(54f, 7f), new Vector2(270f, 19f));

                Image categoryBg = ImageNode(root.transform, "CategoryBadge",
                    rounded, new Color(0.388f, 0.702f, 0.929f, 0.10f),
                    new Vector2(54f, 29f), new Vector2(72f, 14f));
                TMP_Text category = TextNode(categoryBg.transform, "Label",
                    inter, 10.72f, FontWeight.Medium,
                    new Color(0.388f, 0.702f, 0.929f, 1f),
                    new Vector2(4f, 0f), new Vector2(64f, 14f));

                Image clock = ImageNode(root.transform, "Clock",
                    SpriteAt("clock"), Secondary,
                    new Vector2(134f, 31f), new Vector2(9f, 9f));
                clock.preserveAspect = true;
                TMP_Text duration = TextNode(root.transform, "Duration", inter,
                    10.88f, FontWeight.Regular, Secondary,
                    new Vector2(147f, 28f), new Vector2(62f, 15f));
                TMP_Text steps = TextNode(root.transform, "StepCount", inter,
                    10.88f, FontWeight.Regular, Secondary,
                    new Vector2(211f, 28f), new Vector2(62f, 15f));

                Image difficultyBg = ImageNode(root.transform,
                    "DifficultyBadge", rounded,
                    new Color(Success.r, Success.g, Success.b, 0.10f),
                    new Vector2(350f, 18f), new Vector2(76f, 16f));
                TMP_Text difficulty = TextNode(difficultyBg.transform, "Label",
                    inter, 10.72f, FontWeight.Medium, Success,
                    new Vector2(3f, 0f), new Vector2(70f, 16f));
                difficulty.alignment = TextAlignmentOptions.Center;

                Image chevron = ImageNode(root.transform, "Chevron",
                    SpriteAt("chevron-right"), Dim,
                    new Vector2(435f, 18.5f), new Vector2(13f, 13f));
                chevron.preserveAspect = true;

                TaskCardView view = Ensure<TaskCardView>(root);
                SerializedObject so = new SerializedObject(view);
                SetRef(so, "icon", icon);
                SetRef(so, "titleText", title);
                SetRef(so, "descriptionText", null);
                SetRef(so, "categoryText", category);
                SetRef(so, "durationText", duration);
                SetRef(so, "stepCountText", steps);
                SetRef(so, "difficultyBadgeBackground", difficultyBg);
                SetRef(so, "difficultyBadgeText", difficulty);
                SetRef(so, "selectedAccent", selectedAccent);
                SetRef(so, "chevron", chevron);
                SetRef(so, "cardButton", button);
                SetRef(so, "surface", surface);
                SetRef(so, "border", border);
                SetRef(so, "theme",
                    AssetDatabase.LoadAssetAtPath<NorscopeTheme>(
                        "Assets/Norscope/UI/DesignSystem/NorscopeTheme.asset"));
                SetRef(so, "fallbackIcon", SpriteAt("fan"));
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, CardPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ApplyPanel()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PanelPath);
            try
            {
                Transform oldFigma = root.transform.Find("FigmaContent");
                if (oldFigma != null)
                    Object.DestroyImmediate(oldFigma.gameObject);

                Transform oldScroll = root.transform.Find("ScrollView");
                if (oldScroll != null)
                    Object.DestroyImmediate(oldScroll.gameObject);

                Sprite rounded = SpriteAt("rounded", RoundedPath);
                TMP_FontAsset outfit =
                    AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OutfitPath);
                TMP_FontAsset inter =
                    AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(InterPath);
                RectTransform rootRt = EnsureRect(root);

                Image background = root.transform.Find("Background")?
                    .GetComponent<Image>();
                if (background != null)
                    background.color = new Color(Bg.r, Bg.g, Bg.b, 0.85f);

                GameObject contentGo = new GameObject("FigmaContent",
                    typeof(RectTransform));
                contentGo.transform.SetParent(root.transform, false);
                RectTransform content = contentGo.GetComponent<RectTransform>();
                content.sizeDelta = new Vector2(880f, 650f);
                content.anchorMin = content.anchorMax =
                    content.pivot = new Vector2(0.5f, 0.5f);
                float fit = Mathf.Min(
                    rootRt.rect.width / 880f, rootRt.rect.height / 650f);
                content.localScale = Vector3.one * fit;

                BuildHeader(content, outfit, inter, rounded);

                RectTransform left = RectNode(content, "TaskListPane",
                    new Vector2(40f, 190f), new Vector2(456f, 400f));
                TMP_InputField search = BuildSearch(left, inter, rounded);
                Button[] filterButtons = BuildFilters(left, inter, rounded);
                BuildSort(left, inter, rounded);
                ScrollRect scroll = BuildScroll(left);
                TMP_Text empty = TextNode(left, "EmptyState", inter, 13f,
                    FontWeight.Regular, Dim,
                    new Vector2(0f, 155f), new Vector2(456f, 30f));
                empty.text = "No tasks match your search";
                empty.alignment = TextAlignmentOptions.Center;
                empty.gameObject.SetActive(false);

                RectTransform detailRt = RectNode(content, "DetailPane",
                    new Vector2(510f, 190f), new Vector2(330f, 432f));
                Image detailBg = Ensure<Image>(detailRt.gameObject);
                StyleImage(detailBg, rounded, Card, false);
                TaskLibraryDetailView detail =
                    BuildDetail(detailRt, outfit, inter, rounded);

                TMP_Text footer = TextNode(content, "Footer", inter, 10.88f,
                    FontWeight.Regular, Dim,
                    new Vector2(650f, 630f), new Vector2(190f, 16f));
                footer.text = "v4.2.1 · HoloLens 2";
                footer.alignment = TextAlignmentOptions.MidlineRight;

                TaskLibraryPanel panel =
                    root.GetComponent<TaskLibraryPanel>();
                if (panel == null) panel = root.AddComponent<TaskLibraryPanel>();
                SerializedObject panelSo = new SerializedObject(panel);
                SetRef(panelSo, "taskDatabase",
                    AssetDatabase.LoadAssetAtPath<TaskLibraryDatabase>(
                        DatabasePath));
                SetRef(panelSo, "cardContent", scroll.content);
                SetRef(panelSo, "cardPrefab",
                    AssetDatabase.LoadAssetAtPath<TaskCardView>(CardPath));
                SetRef(panelSo, "searchInput", search);
                SetRef(panelSo, "detailView", detail);
                SetRef(panelSo, "taskScrollRect", scroll);
                SetRef(panelSo, "emptyStateText", empty);
                SerializedProperty filters =
                    panelSo.FindProperty("categoryFilters");
                string[] names =
                    { "All", "Mechanical", "Electrical", "Inspection", "Cleaning" };
                filters.arraySize = names.Length;
                for (int i = 0; i < names.Length; i++)
                {
                    SerializedProperty item =
                        filters.GetArrayElementAtIndex(i);
                    item.FindPropertyRelative("category").stringValue = names[i];
                    item.FindPropertyRelative("button").objectReferenceValue =
                        filterButtons[i];
                }
                panelSo.ApplyModifiedPropertiesWithoutUndo();

                NorscopePanelRoot panelRoot =
                    root.GetComponent<NorscopePanelRoot>();
                if (panelRoot != null)
                {
                    SerializedObject rootSo = new SerializedObject(panelRoot);
                    SerializedProperty titleRef = rootSo.FindProperty("title");
                    if (titleRef != null) titleRef.objectReferenceValue = null;
                    rootSo.ApplyModifiedPropertiesWithoutUndo();
                }

                content.SetSiblingIndex(Mathf.Max(
                    1, root.transform.childCount - 2));
                PrefabUtility.SaveAsPrefabAsset(root, PanelPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void BuildHeader(
            RectTransform parent, TMP_FontAsset outfit, TMP_FontAsset inter,
            Sprite rounded)
        {
            Sprite logo = AssetDatabase.LoadAssetAtPath<Sprite>(LogoPath);
            Image logoImage = ImageNode(
                parent, "NorscopeLogo", logo, Color.white,
                new Vector2(40f, 28f), new Vector2(120f, 36f));
            logoImage.type = Image.Type.Simple;
            logoImage.preserveAspect = true;
            logoImage.raycastTarget = false;

            TMP_Text title = TextNode(parent, "Title", outfit, 48f,
                FontWeight.Light, Primary,
                new Vector2(40f, 86f), new Vector2(500f, 52f));
            title.text = "Task Library";
            TMP_Text subtitle = TextNode(parent, "Subtitle", inter, 14.08f,
                FontWeight.Light, Secondary,
                new Vector2(40f, 142f), new Vector2(400f, 20f));
            subtitle.text = "Select a maintenance workflow";
            Image separator = ImageNode(parent, "Separator", null, Border,
                new Vector2(40f, 178f), new Vector2(800f, 1f));
            separator.raycastTarget = false;
        }

        private static TMP_InputField BuildSearch(
            RectTransform parent, TMP_FontAsset inter, Sprite rounded)
        {
            RectTransform root = RectNode(parent, "Search",
                Vector2.zero, new Vector2(314f, 38f));
            Image bg = Ensure<Image>(root.gameObject);
            StyleImage(bg, rounded, Card, true);
            Image icon = ImageNode(root, "Icon", SpriteAt("search"), Dim,
                new Vector2(12f, 12.5f), new Vector2(13f, 13f));
            icon.preserveAspect = true;
            RectTransform area = RectNode(root, "TextArea",
                new Vector2(34f, 2f), new Vector2(268f, 34f));
            TMP_Text placeholder = TextNode(area, "Placeholder", inter, 13.12f,
                FontWeight.Light, Dim, Vector2.zero,
                new Vector2(268f, 34f));
            placeholder.text = "Search tasks";
            TMP_Text text = TextNode(area, "Text", inter, 13.12f,
                FontWeight.Light, Primary, Vector2.zero,
                new Vector2(268f, 34f));
            TMP_InputField input = root.gameObject.AddComponent<TMP_InputField>();
            input.textViewport = area;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.targetGraphic = bg;
            input.lineType = TMP_InputField.LineType.SingleLine;
            return input;
        }

        private static Button[] BuildFilters(
            RectTransform parent, TMP_FontAsset inter, Sprite rounded)
        {
            string[] names =
                { "All", "Mechanical", "Electrical", "Inspection", "Cleaning" };
            float[] widths = { 42f, 82f, 74f, 76f, 67f };
            var result = new Button[names.Length];
            float x = 0f;
            for (int i = 0; i < names.Length; i++)
            {
                RectTransform rt = RectNode(parent, "Filter_" + names[i],
                    new Vector2(x, 47f), new Vector2(widths[i], 27f));
                Image bg = Ensure<Image>(rt.gameObject);
                StyleImage(bg, rounded, i == 0 ? Accent : Card, true);
                Button button = rt.gameObject.AddComponent<Button>();
                button.targetGraphic = bg;
                TMP_Text label = TextNode(rt, "Label", inter, 11.36f,
                    i == 0 ? FontWeight.Medium : FontWeight.Regular,
                    i == 0 ? Bg : Secondary,
                    Vector2.zero, widths[i] * Vector2.right + 27f * Vector2.up);
                label.alignment = TextAlignmentOptions.Center;
                label.text = names[i];
                result[i] = button;
                x += widths[i] + 5f;
            }
            return result;
        }

        private static void BuildSort(
            RectTransform parent, TMP_FontAsset inter, Sprite rounded)
        {
            RectTransform rt = RectNode(parent, "SortMostRecent",
                new Vector2(322f, 0f), new Vector2(134f, 38f));
            Image bg = Ensure<Image>(rt.gameObject);
            StyleImage(bg, rounded, Card, true);
            Button button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            button.interactable = false;
            Image sliders = ImageNode(rt, "Icon",
                SpriteAt("sliders-horizontal"), Secondary,
                new Vector2(10f, 13.5f), new Vector2(11f, 11f));
            sliders.preserveAspect = true;
            TMP_Text label = TextNode(rt, "Label", inter, 12.16f,
                FontWeight.Regular, Secondary,
                new Vector2(27f, 0f), new Vector2(88f, 38f));
            label.text = "Most recent";
            Image arrow = ImageNode(rt, "ChevronDown",
                SpriteAt("chevron-down"), Secondary,
                new Vector2(116f, 14f), new Vector2(10f, 10f));
            arrow.preserveAspect = true;
        }

        private static ScrollRect BuildScroll(RectTransform parent)
        {
            RectTransform scrollRt = RectNode(parent, "ScrollView",
                new Vector2(0f, 85f), new Vector2(456f, 315f));
            ScrollRect scroll = scrollRt.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 20f;
            RectTransform viewport = RectNode(scrollRt, "Viewport",
                Vector2.zero, new Vector2(456f, 315f));
            Image vp = Ensure<Image>(viewport.gameObject);
            vp.color = Color.clear;
            vp.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform content = RectNode(viewport, "Content",
                Vector2.zero, new Vector2(456f, 0f));
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            // Horizontally stretched: width offset must be zero. Keeping the
            // authored 456 here would add it to the 456-wide viewport and make
            // every layout-controlled TaskCard roughly twice as wide.
            content.sizeDelta = new Vector2(0f, 0f);
            VerticalLayoutGroup layout =
                content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter =
                content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;
            return scroll;
        }

        private static TaskLibraryDetailView BuildDetail(
            RectTransform root, TMP_FontAsset outfit, TMP_FontAsset inter,
            Sprite rounded)
        {
            TMP_Text title = TextNode(root, "TaskTitle", outfit, 17.28f,
                FontWeight.Medium, Primary,
                new Vector2(20f, 18f), new Vector2(290f, 28f));

            TMP_Text[] values = new TMP_Text[4];
            string[] labels = { "CATEGORY", "DURATION", "DIFFICULTY", "STEPS" };
            for (int i = 0; i < 4; i++)
            {
                float x = 20f + (i % 2) * 148.5f;
                float y = 59f + (i / 2) * 49f;
                RectTransform cell = RectNode(root, "Meta_" + labels[i],
                    new Vector2(x, y), new Vector2(141.5f, 42f));
                Image bg = Ensure<Image>(cell.gameObject);
                StyleImage(bg, rounded, Panel, false);
                TMP_Text label = TextNode(cell, "Label", inter, 9.92f,
                    FontWeight.Medium, Dim,
                    new Vector2(10f, 6f), new Vector2(122f, 12f));
                label.text = labels[i];
                label.characterSpacing = 9f;
                values[i] = TextNode(cell, "Value", inter, 12.8f,
                    FontWeight.Regular, Secondary,
                    new Vector2(10f, 20f), new Vector2(122f, 17f));
            }

            RectTransform offline = RectNode(root, "Offline",
                new Vector2(20f, 160f), new Vector2(116f, 16f));
            Image offlineIcon = ImageNode(offline, "Icon",
                SpriteAt("wifi-off"), Success,
                Vector2.zero, new Vector2(10f, 10f));
            offlineIcon.preserveAspect = true;
            TMP_Text offlineText = TextNode(offline, "Label", inter, 11.2f,
                FontWeight.Regular, Success,
                new Vector2(15f, -2f), new Vector2(101f, 16f));
            offlineText.text = "Offline available";
            TMP_Text updated = TextNode(root, "Updated", inter, 11.2f,
                FontWeight.Regular, Dim,
                new Vector2(155f, 158f), new Vector2(155f, 18f));

            Image separator = ImageNode(root, "Separator", null, Border,
                new Vector2(20f, 181f), new Vector2(290f, 1f));
            TMP_Text description = TextNode(root, "Description", inter, 12.16f,
                FontWeight.Light, Secondary,
                new Vector2(20f, 191f), new Vector2(290f, 42f));
            description.enableWordWrapping = true;

            TMP_Text stepLabel = TextNode(root, "StepsLabel", inter, 9.92f,
                FontWeight.Medium, Dim,
                new Vector2(20f, 239f), new Vector2(150f, 13f));
            stepLabel.text = "STEPS PREVIEW";
            stepLabel.characterSpacing = 9f;

            var rows = new GameObject[5];
            var rowTexts = new TMP_Text[5];
            for (int i = 0; i < 5; i++)
            {
                RectTransform row = RectNode(root, "Step_" + (i + 1),
                    new Vector2(20f, 256f + i * 23f),
                    new Vector2(290f, 18f));
                rows[i] = row.gameObject;
                Image numberBg = ImageNode(row, "Number", rounded,
                    Color.clear, Vector2.zero, new Vector2(18f, 18f));
                numberBg.color = new Color(Border.r, Border.g, Border.b, 0.5f);
                TMP_Text number = TextNode(numberBg.transform, "Label", inter,
                    9.28f, FontWeight.Medium, Dim,
                    Vector2.zero, new Vector2(18f, 18f));
                number.text = (i + 1).ToString();
                number.alignment = TextAlignmentOptions.Center;
                rowTexts[i] = TextNode(row, "Text", inter, 11.84f,
                    FontWeight.Light, Secondary,
                    new Vector2(27f, 0f), new Vector2(263f, 18f));
            }

            RectTransform extra = RectNode(root, "ExtraSteps",
                new Vector2(20f, 371f), new Vector2(290f, 18f));
            TMP_Text extraText = TextNode(extra, "Label", inter, 11.36f,
                FontWeight.Regular, Dim,
                new Vector2(27f, 0f), new Vector2(263f, 18f));

            Button start = ButtonNode(root, "StartButton", rounded, Accent,
                new Vector2(20f, 392f), new Vector2(190f, 40f));
            Image play = ImageNode(start.transform, "Icon", SpriteAt("play"),
                Bg, new Vector2(49f, 14f), new Vector2(12f, 12f));
            play.preserveAspect = true;
            TMP_Text startLabel = TextNode(start.transform, "Label", outfit,
                13.6f, FontWeight.SemiBold, Bg,
                new Vector2(66f, 0f), new Vector2(100f, 40f));
            startLabel.text = "Start Task";

            Button details = ButtonNode(root, "DetailsButton", rounded,
                Color.clear, new Vector2(218f, 392f), new Vector2(92f, 40f));
            Image detailsBg = details.targetGraphic as Image;
            if (detailsBg != null) detailsBg.color = Panel;
            TMP_Text detailsLabel = TextNode(details.transform, "Label", outfit,
                13.12f, FontWeight.Regular, Secondary,
                Vector2.zero, new Vector2(92f, 40f));
            detailsLabel.text = "Details";
            detailsLabel.alignment = TextAlignmentOptions.Center;

            TaskLibraryDetailView view =
                root.gameObject.AddComponent<TaskLibraryDetailView>();
            SerializedObject so = new SerializedObject(view);
            SetRef(so, "titleText", title);
            SetRef(so, "categoryText", values[0]);
            SetRef(so, "durationText", values[1]);
            SetRef(so, "difficultyText", values[2]);
            SetRef(so, "stepCountText", values[3]);
            SetRef(so, "offlineGroup", offline.gameObject);
            SetRef(so, "updatedText", updated);
            SetRef(so, "descriptionText", description);
            SetArray(so.FindProperty("stepPreviewTexts"), rowTexts);
            SetArray(so.FindProperty("stepPreviewRows"), rows);
            SetRef(so, "extraStepsGroup", extra.gameObject);
            SetRef(so, "extraStepsText", extraText);
            SetRef(so, "startButton", start);
            SetRef(so, "detailsButton", details);
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        private static void AssignInitialTaskIcons()
        {
            TaskLibraryDatabase database =
                AssetDatabase.LoadAssetAtPath<TaskLibraryDatabase>(DatabasePath);
            if (database == null) return;
            foreach (TaskDefinition task in database.Tasks)
            {
                if (task == null || task.Icon != null) continue;
                string icon = task.TaskId.Contains("fan") ? "fan" :
                    task.TaskId.Contains("battery") ? "zap" :
                    task.TaskId.Contains("ssd") ? "database" :
                    task.TaskId.Contains("ram") ? "cpu" :
                    task.TaskId.Contains("keyboard") ? "layers" : "activity";
                SerializedObject so = new SerializedObject(task);
                so.FindProperty("icon").objectReferenceValue = SpriteAt(icon);
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(task);
            }
        }

        private static void PrepareAllSprites()
        {
            foreach (string path in Directory.GetFiles(
                         IconRoot.TrimEnd('/'), "*.png"))
                PrepareSprite(path.Replace('\\', '/'));
            PrepareSprite(RoundedPath);
        }

        private static void PrepareSprite(string path)
        {
            TextureImporter importer =
                AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }

        private static bool Validate(out string report)
        {
            var lines = new List<string>();
            bool ok = true;
            void Check(bool pass, string message)
            {
                lines.Add((pass ? "PASS  " : "FAIL  ") + message);
                ok &= pass;
            }

            GameObject panel =
                AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath);
            GameObject card =
                AssetDatabase.LoadAssetAtPath<GameObject>(CardPath);
            Check(panel != null, "TaskLibraryPanel.prefab exists");
            Check(card != null, "TaskCard.prefab exists");
            if (panel != null)
            {
                Check(panel.transform.Find("FigmaContent") != null,
                    "880-unit FigmaContent is authored");
                Check(panel.transform.Find(
                        "FigmaContent/TaskListPane/ScrollView/Viewport/Content") != null,
                    "Task list ScrollRect hierarchy is authored");
                Check(panel.transform.Find("FigmaContent/DetailPane") != null,
                    "Selected-task detail pane is authored");
                Check(panel.GetComponent<SpatialWindowBindings>() != null,
                    "Spatial window bindings remain");
                Check(panel.transform.Find("TitleBar/GrabRegion") != null,
                    "Grab region remains");
                Check(panel.GetComponent<TaskLibraryPanel>() != null,
                    "TaskLibraryPanel runtime binding remains");
            }
            if (card != null)
            {
                LayoutElement le = card.GetComponent<LayoutElement>();
                Check(le != null && Mathf.Approximately(
                        le.preferredHeight, 50f),
                    "TaskCard authored height is 50");
                Check(card.GetComponent<TaskCardView>() != null,
                    "TaskCardView remains");
                Check(card.GetComponent<Button>() != null,
                    "TaskCard Button remains the sole row action target");
            }
            report = string.Join("\n", lines);
            return ok;
        }

        private static bool Renderable(TMP_FontAsset font)
        {
            return font != null && font.material != null &&
                   font.atlasTexture != null;
        }

        private static Sprite SpriteAt(string name, string explicitPath = null)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(
                explicitPath ?? IconRoot + name + ".png");
        }

        private static RectTransform RectNode(
            Transform parent, string name, Vector2 topLeft, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            SetTopLeft(rt, topLeft, size);
            return rt;
        }

        private static Image ImageNode(
            Transform parent, string name, Sprite sprite, Color color,
            Vector2 topLeft, Vector2 size)
        {
            RectTransform rt = RectNode(parent, name, topLeft, size);
            Image image = rt.gameObject.AddComponent<Image>();
            StyleImage(image, sprite, color, false);
            return image;
        }

        private static TMP_Text TextNode(
            Transform parent, string name, TMP_FontAsset font, float size,
            FontWeight weight, Color color, Vector2 topLeft, Vector2 rectSize)
        {
            RectTransform rt = RectNode(parent, name, topLeft, rectSize);
            TMP_Text text = rt.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = size;
            text.fontWeight = weight;
            text.color = color;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.raycastTarget = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }

        private static Button ButtonNode(
            Transform parent, string name, Sprite sprite, Color color,
            Vector2 topLeft, Vector2 size)
        {
            RectTransform rt = RectNode(parent, name, topLeft, size);
            Image image = rt.gameObject.AddComponent<Image>();
            StyleImage(image, sprite, color, true);
            Button button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            return button;
        }

        private static void StyleImage(
            Image image, Sprite sprite, Color color, bool raycast)
        {
            image.sprite = sprite;
            image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            image.color = color;
            image.raycastTarget = raycast;
        }

        private static void SetTopLeft(
            RectTransform rt, Vector2 topLeft, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(topLeft.x, -topLeft.y);
            rt.sizeDelta = size;
        }

        private static void Stretch(RectTransform rt, float inset)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(-2f * inset, -2f * inset);
        }

        private static RectTransform EnsureRect(GameObject go)
        {
            return go.GetComponent<RectTransform>() ??
                   go.AddComponent<RectTransform>();
        }

        private static T Ensure<T>(GameObject go) where T : Component
        {
            return go.GetComponent<T>() ?? go.AddComponent<T>();
        }

        private static void SetRef(
            SerializedObject so, string property, Object value)
        {
            SerializedProperty field = so.FindProperty(property);
            if (field != null) field.objectReferenceValue = value;
        }

        private static void SetArray<T>(
            SerializedProperty property, T[] values) where T : Object
        {
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue =
                    values[i];
        }

        private static Color Hex(string value)
        {
            ColorUtility.TryParseHtmlString("#" + value, out Color color);
            return color;
        }

        private static string Hash(string path)
        {
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(
                    sha.ComputeHash(File.ReadAllBytes(path)))
                    .Replace("-", string.Empty);
        }
    }
}
