using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Unity.XR.CoreUtils;
using Norscope.Navigation;
using Norscope.UI;
using Norscope.Windows;

namespace Norscope.Editor
{
    /// <summary>
    /// Phase 5: Home Launcher + Home Panel + PanelRegistry. Does not build Task Library / AI / etc.
    /// </summary>
    public static class Phase5NavigationConfigurator
    {
        private const string BootstrapScenePath = "Assets/Norscope/Scenes/Bootstrap.unity";
        private const string PreviewScenePath = "Assets/Norscope/Scenes/UI_Preview.unity";
        private const string InteractionTestPanelPath = "Assets/Norscope/UI/Panels/InteractionTestPanel.prefab";
        private const string HomePanelPath = "Assets/Norscope/UI/Panels/HomePanel.prefab";
        private const string HomeLauncherPath = "Assets/Norscope/UI/Panels/HomeLauncher.prefab";
        private const string ConfigAssetPath = "Assets/Norscope/Runtime/Windows/SpatialWindowConfig.asset";
        private const string ThemePath = "Assets/Norscope/UI/DesignSystem/Themes/NorscopeTheme.asset";
        private const string WorldConfigPath = "Assets/Norscope/UI/DesignSystem/Themes/NorscopeWorldSpaceUiConfig.asset";
        private const string RoundedSpritePath = "Assets/Norscope/UI/DesignSystem/Sprites/UIRounded_8.png";
        private const string CloseButtonPath = "Assets/Norscope/UI/Controls/NorscopeCloseButton.prefab";
        private const string SpriteDir = "Assets/Norscope/UI/DesignSystem/Sprites";
        private const string WindowRootName = "SpatialWindowSystem";
        private const string FlagPath = "Assets/Norscope/Editor/.apply_phase5_navigation";
        private const string PrefabBackupDir = "Assets/Norscope/UI/Panels/_Backups";

        [InitializeOnLoadMethod]
        private static void AutoApplyFromFlag()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredEditMode && File.Exists(FlagPath))
                {
                    EditorApplication.delayCall += TryApplyFromFlag;
                }
            };

            if (!File.Exists(FlagPath))
            {
                return;
            }

            EditorApplication.delayCall += TryApplyFromFlag;
        }

        private static void TryApplyFromFlag()
        {
            if (!File.Exists(FlagPath))
            {
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.Log("[Norscope Phase5] Waiting for Play Mode exit before applying navigation.");
                return;
            }

            try { File.Delete(FlagPath); } catch { /* ignore */ }

            try
            {
                // Non-destructive only: auto-apply must never rebuild HomeLauncher/HomePanel hierarchy.
                WirePhase5Scene();
                ValidateMenu();
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[Norscope Phase5] Auto-apply failed:\n" + ex);
            }
        }

        // ---------------------------------------------------------------
        // 1) Create Phase 5 Prefabs If Missing — never touches an existing prefab.
        // ---------------------------------------------------------------

        [MenuItem("Norscope/HoloLens/Phase 5/1) Create Phase 5 Prefabs If Missing")]
        public static void CreatePhase5PrefabsIfMissingMenu()
        {
            CreatePhase5PrefabsIfMissing();
        }

        public static void CreatePhase5PrefabsIfMissing()
        {
            BuildHomeIconSprites(); // already file-exists guarded per icon; safe every time.
            bool launcherCreated = CreateHomeLauncherPrefabIfMissing();
            bool panelCreated = CreateHomePanelPrefabIfMissing();

            if (launcherCreated || panelCreated)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            Debug.Log("[Norscope Phase5] Create Phase 5 Prefabs If Missing — HomeLauncher: " +
                      (launcherCreated ? "created" : "already existed, left untouched") +
                      "; HomePanel: " + (panelCreated ? "created" : "already existed, left untouched") + ".");
        }

        private static bool CreateHomeLauncherPrefabIfMissing()
        {
            if (File.Exists(HomeLauncherPath))
            {
                return false;
            }

            BuildHomeLauncherPrefab();
            return true;
        }

        private static bool CreateHomePanelPrefabIfMissing()
        {
            if (File.Exists(HomePanelPath))
            {
                return false;
            }

            BuildHomePanelPrefab();
            return true;
        }

        // ---------------------------------------------------------------
        // 2) Wire Phase 5 Scene — scene references only. Never calls the prefab builders
        //    unconditionally; only ensures they exist via CreatePhase5PrefabsIfMissing().
        // ---------------------------------------------------------------

        [MenuItem("Norscope/HoloLens/Phase 5/2) Wire Phase 5 Scene")]
        public static void WirePhase5SceneMenu()
        {
            WirePhase5Scene();
        }

        public static void WirePhase5Scene()
        {
            CreatePhase5PrefabsIfMissing();
            EnsureConfigHeight();
            ApplyToBootstrapScene();
            ApplyToPreviewScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Norscope Phase5] Wire Phase 5 Scene complete. HomeLauncher/HomePanel prefabs were not modified.");
        }

        public static void ApplyAndValidateBatch()
        {
            try
            {
                WirePhase5Scene();
                bool ok = Validate(out string report);
                Debug.Log(ok
                    ? "[Norscope Phase5] Validate Navigation PASSED\n" + report
                    : "[Norscope Phase5] Validate Navigation FAILED\n" + report);
                EditorApplication.Exit(ok ? 0 : 1);
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[Norscope Phase5] ApplyAndValidateBatch failed:\n" + ex);
                EditorApplication.Exit(1);
            }
        }

        // ---------------------------------------------------------------
        // 4) Rebuild Phase 5 Prefabs (Destructive) — the ONLY path that overwrites
        //    HomeLauncher.prefab / HomePanel.prefab. Requires explicit confirmation
        //    and always backs up the existing prefabs first. Never auto-triggered.
        // ---------------------------------------------------------------

        [MenuItem("Norscope/HoloLens/Phase 5/4) Rebuild Phase 5 Prefabs (Destructive)")]
        public static void RebuildPhase5PrefabsDestructiveMenu()
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "Rebuild Phase 5 Prefabs — Destructive",
                "This regenerates HomeLauncher.prefab and HomePanel.prefab from code, completely " +
                "replacing their contents.\n\n" +
                "ANY MANUAL EDITS you made in Prefab Mode — colors, layout tweaks, added elements, " +
                "orientation fixes — will be PERMANENTLY LOST and replaced with the generated defaults.\n\n" +
                "A timestamped backup of both prefabs will be saved to:\n" + PrefabBackupDir +
                "\n\nThis cannot be undone with Ctrl+Z. Continue?",
                "Rebuild and Overwrite",
                "Cancel");

            if (!confirmed)
            {
                Debug.Log("[Norscope Phase5] Rebuild Phase 5 Prefabs (Destructive) cancelled by user.");
                return;
            }

            RebuildPhase5PrefabsDestructive();
        }

        public static void RebuildPhase5PrefabsDestructive()
        {
            BackupPrefabIfExists(HomeLauncherPath);
            BackupPrefabIfExists(HomePanelPath);

            BuildHomeIconSprites();
            BuildHomeLauncherPrefab();
            BuildHomePanelPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Norscope Phase5] Rebuild Phase 5 Prefabs (Destructive) complete. " +
                      "Run Wire Phase 5 Scene next to re-instantiate scene references.");
        }

        private static void BackupPrefabIfExists(string assetPath)
        {
            if (!File.Exists(assetPath))
            {
                return;
            }

            Directory.CreateDirectory(PrefabBackupDir);
            AssetDatabase.Refresh();

            string fileName = Path.GetFileNameWithoutExtension(assetPath);
            string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string backupPath = AssetDatabase.GenerateUniqueAssetPath(
                PrefabBackupDir + "/" + fileName + "_" + timestamp + ".prefab");
            AssetDatabase.CopyAsset(assetPath, backupPath);
            Debug.Log("[Norscope Phase5] Backed up " + assetPath + " -> " + backupPath);
        }

        // ---------------------------------------------------------------
        // 3) Validate Navigation — kept at its original top-level path for compatibility,
        //    plus a numbered alias under the Phase 5 submenu.
        // ---------------------------------------------------------------

        [MenuItem("Norscope/HoloLens/Phase 5/3) Validate Navigation")]
        [MenuItem("Norscope/Validate Navigation")]
        public static void ValidateMenu()
        {
            bool ok = Validate(out string report);
            Debug.Log(ok
                ? "[Norscope Phase5] Validate Navigation PASSED\n" + report
                : "[Norscope Phase5] Validate Navigation FAILED\n" + report);
            EditorUtility.DisplayDialog(
                "Validate Navigation",
                (ok ? "PASSED\n\n" : "FAILED\n\n") + report,
                "OK");
        }

        private static void EnsureConfigHeight()
        {
            SpatialWindowConfig config = AssetDatabase.LoadAssetAtPath<SpatialWindowConfig>(ConfigAssetPath);
            if (config == null)
            {
                return;
            }

            config.defaultWidthMeters = 0.55f;
            config.defaultHeightMeters = 0.40f;
            config.pixelsPerMeter = 1000f;
            config.spawnDistanceMeters = 1.0f;
            config.recenterDistanceMeters = 1.0f;
            EditorUtility.SetDirty(config);
        }

        private static void BuildHomeIconSprites()
        {
            Directory.CreateDirectory(SpriteDir);
            CreateSimpleIcon(SpriteDir + "/IconHome.png", DrawHomeIcon);
            CreateSimpleIcon(SpriteDir + "/IconTasks.png", DrawListIcon);
            CreateSimpleIcon(SpriteDir + "/IconAI.png", DrawSparkIcon);
            CreateSimpleIcon(SpriteDir + "/IconReports.png", DrawDocIcon);
            CreateSimpleIcon(SpriteDir + "/IconSettings.png", DrawGearIcon);
            CreateSimpleIcon(SpriteDir + "/IconStorage.png", DrawBoxIcon);
            CreateSimpleIcon(SpriteDir + "/IconAbout.png", DrawInfoIcon);
        }

        private delegate void IconDrawer(Texture2D tex);

        private static Sprite CreateSimpleIcon(string path, IconDrawer drawer)
        {
            if (!File.Exists(path))
            {
                var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
                for (int y = 0; y < 64; y++)
                {
                    for (int x = 0; x < 64; x++)
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }

                drawer(tex);
                tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
            }

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void DrawHomeIcon(Texture2D tex)
        {
            DrawLine(tex, 12, 30, 32, 14, 3);
            DrawLine(tex, 32, 14, 52, 30, 3);
            FillRect(tex, 18, 30, 46, 50, Color.white);
            FillRect(tex, 28, 38, 36, 50, Color.clear);
        }

        private static void DrawListIcon(Texture2D tex)
        {
            FillRect(tex, 14, 16, 50, 20, Color.white);
            FillRect(tex, 14, 30, 50, 34, Color.white);
            FillRect(tex, 14, 44, 50, 48, Color.white);
        }

        private static void DrawSparkIcon(Texture2D tex)
        {
            DrawLine(tex, 32, 10, 32, 54, 3);
            DrawLine(tex, 10, 32, 54, 32, 3);
            DrawLine(tex, 16, 16, 48, 48, 2);
            DrawLine(tex, 48, 16, 16, 48, 2);
        }

        private static void DrawDocIcon(Texture2D tex)
        {
            FillRect(tex, 18, 12, 46, 52, Color.white);
            FillRect(tex, 22, 20, 42, 24, Color.clear);
            FillRect(tex, 22, 30, 42, 34, Color.clear);
            FillRect(tex, 22, 40, 36, 44, Color.clear);
        }

        private static void DrawGearIcon(Texture2D tex)
        {
            FillCircle(tex, 32, 32, 16, Color.white);
            FillCircle(tex, 32, 32, 7, Color.clear);
            FillRect(tex, 29, 8, 35, 16, Color.white);
            FillRect(tex, 29, 48, 35, 56, Color.white);
            FillRect(tex, 8, 29, 16, 35, Color.white);
            FillRect(tex, 48, 29, 56, 35, Color.white);
        }

        private static void DrawBoxIcon(Texture2D tex)
        {
            DrawLine(tex, 14, 24, 32, 14, 2);
            DrawLine(tex, 32, 14, 50, 24, 2);
            DrawLine(tex, 14, 24, 14, 44, 2);
            DrawLine(tex, 50, 24, 50, 44, 2);
            DrawLine(tex, 14, 44, 32, 54, 2);
            DrawLine(tex, 50, 44, 32, 54, 2);
            DrawLine(tex, 14, 24, 50, 24, 2);
            DrawLine(tex, 32, 14, 32, 54, 2);
        }

        private static void DrawInfoIcon(Texture2D tex)
        {
            FillCircle(tex, 32, 32, 20, Color.white);
            FillCircle(tex, 32, 32, 16, Color.clear);
            FillRect(tex, 30, 18, 34, 24, Color.white);
            FillRect(tex, 30, 28, 34, 46, Color.white);
        }

        private static void FillRect(Texture2D tex, int x0, int y0, int x1, int y1, Color c)
        {
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    if (x >= 0 && x < tex.width && y >= 0 && y < tex.height)
                    {
                        tex.SetPixel(x, y, c);
                    }
                }
            }
        }

        private static void FillCircle(Texture2D tex, int cx, int cy, int r, Color c)
        {
            int r2 = r * r;
            for (int y = cy - r; y <= cy + r; y++)
            {
                for (int x = cx - r; x <= cx + r; x++)
                {
                    int dx = x - cx;
                    int dy = y - cy;
                    if (dx * dx + dy * dy <= r2 && x >= 0 && x < tex.width && y >= 0 && y < tex.height)
                    {
                        tex.SetPixel(x, y, c);
                    }
                }
            }
        }

        private static void DrawLine(Texture2D tex, int x0, int y0, int x1, int y1, int thickness)
        {
            int steps = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0));
            for (int i = 0; i <= steps; i++)
            {
                float t = steps == 0 ? 0f : i / (float)steps;
                int x = Mathf.RoundToInt(Mathf.Lerp(x0, x1, t));
                int y = Mathf.RoundToInt(Mathf.Lerp(y0, y1, t));
                for (int oy = -thickness; oy <= thickness; oy++)
                {
                    for (int ox = -thickness; ox <= thickness; ox++)
                    {
                        if (ox * ox + oy * oy <= thickness * thickness)
                        {
                            int px = x + ox;
                            int py = y + oy;
                            if (px >= 0 && px < tex.width && py >= 0 && py < tex.height)
                            {
                                tex.SetPixel(px, py, Color.white);
                            }
                        }
                    }
                }
            }
        }

        private static string BuildHomeLauncherPrefab()
        {
            NorscopeTheme theme = AssetDatabase.LoadAssetAtPath<NorscopeTheme>(ThemePath);
            Sprite rounded = AssetDatabase.LoadAssetAtPath<Sprite>(RoundedSpritePath);
            Sprite homeIcon = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteDir + "/IconHome.png");

            GameObject root = new GameObject("HomeLauncher", typeof(RectTransform));
            RectTransform rootRt = root.GetComponent<RectTransform>();
            rootRt.sizeDelta = new Vector2(65f, 65f);

            // VisualRoot carries all visible chrome. The root itself only ever holds a positive
            // (1,1,1) scale and identity rotation — it carries Button/HomeLauncher/
            // UiButtonPressScale (interaction), so it must never be negative-scaled or rotated
            // to orient the launcher. Standard orientation is X=45, Z=180 on VisualRoot; still
            // freely editable by hand afterward (nothing resets it on normal Wire/Create).
            GameObject visualRoot = new GameObject("VisualRoot", typeof(RectTransform));
            visualRoot.transform.SetParent(root.transform, false);
            RectTransform visualRootRt = visualRoot.GetComponent<RectTransform>();
            StretchFull(visualRootRt);
            visualRootRt.localRotation = Quaternion.Euler(45f, 0f, 180f);

            GameObject outlineGo = new GameObject("Outline", typeof(RectTransform));
            outlineGo.transform.SetParent(visualRoot.transform, false);
            RectTransform outlineRt = outlineGo.GetComponent<RectTransform>();
            StretchFull(outlineRt);
            Image outline = outlineGo.AddComponent<Image>();
            outline.sprite = rounded;
            outline.type = Image.Type.Sliced;
            outline.color = theme != null ? theme.border : new Color(0.15f, 0.19f, 0.26f, 0.85f);
            outline.raycastTarget = false;

            GameObject bgGo = new GameObject("Background", typeof(RectTransform));
            bgGo.transform.SetParent(visualRoot.transform, false);
            RectTransform bgRt = bgGo.GetComponent<RectTransform>();
            StretchFull(bgRt);
            bgRt.offsetMin = new Vector2(3f, 3f);
            bgRt.offsetMax = new Vector2(-3f, -3f);
            Image bg = bgGo.AddComponent<Image>();
            bg.sprite = rounded;
            bg.type = Image.Type.Sliced;
            bg.color = theme != null ? theme.panelBackground : new Color(0.075f, 0.102f, 0.141f, 0.92f);
            bg.raycastTarget = true;

            GameObject iconGo = new GameObject("Icon", typeof(RectTransform));
            iconGo.transform.SetParent(visualRoot.transform, false);
            RectTransform iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0.5f, 0.5f);
            iconRt.anchorMax = new Vector2(0.5f, 0.5f);
            iconRt.sizeDelta = new Vector2(28f, 28f);
            Image icon = iconGo.AddComponent<Image>();
            icon.sprite = homeIcon;
            icon.color = theme != null ? theme.primaryAccent : Color.cyan;
            icon.raycastTarget = false;
            icon.preserveAspect = true;

            // bg is still the interactive target graphic for the root Button — raycasting works
            // the same regardless of VisualRoot's rotation since RectTransform hit-testing follows
            // the actual transform hierarchy.
            Button button = root.AddComponent<Button>();
            button.targetGraphic = bg;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.05f, 1.05f, 1.05f, 1f);
            colors.pressedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.interactable = true;

            HomeLauncher launcher = root.AddComponent<HomeLauncher>();
            SerializedObject so = new SerializedObject(launcher);
            so.FindProperty("button").objectReferenceValue = button;
            so.FindProperty("background").objectReferenceValue = bg;
            so.FindProperty("outline").objectReferenceValue = outline;
            so.FindProperty("icon").objectReferenceValue = icon;
            if (theme != null)
            {
                so.FindProperty("normalOutline").colorValue = theme.border;
                so.FindProperty("hoverOutline").colorValue = theme.primaryAccent;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            root.AddComponent<Norscope.UiButtonPressScale>();

            PrefabUtility.SaveAsPrefabAsset(root, HomeLauncherPath);
            Object.DestroyImmediate(root);
            return HomeLauncherPath;
        }

        private static string BuildHomePanelPrefab()
        {
            NorscopeTheme theme = AssetDatabase.LoadAssetAtPath<NorscopeTheme>(ThemePath);
            NorscopeWorldSpaceUiConfig world = AssetDatabase.LoadAssetAtPath<NorscopeWorldSpaceUiConfig>(WorldConfigPath);
            Sprite rounded = AssetDatabase.LoadAssetAtPath<Sprite>(RoundedSpritePath);
            TMP_FontAsset font = theme != null && theme.fonts != null ? theme.fonts.bodyFont : null;
            if (font == null)
            {
                font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                    "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            }

            GameObject root = new GameObject("HomePanel", typeof(RectTransform));
            RectTransform rt = root.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(550f, 400f);

            GameObject bgGo = new GameObject("Background", typeof(RectTransform));
            bgGo.transform.SetParent(root.transform, false);
            StretchFull(bgGo.GetComponent<RectTransform>());
            Image bg = bgGo.AddComponent<Image>();
            bg.sprite = rounded;
            bg.type = Image.Type.Sliced;
            bg.color = theme != null ? theme.panelBackground : Color.black;
            bg.raycastTarget = true;
            bgGo.AddComponent<LayoutElement>().ignoreLayout = true;

            GameObject borderGo = new GameObject("Border", typeof(RectTransform));
            borderGo.transform.SetParent(root.transform, false);
            StretchFull(borderGo.GetComponent<RectTransform>());
            Image border = borderGo.AddComponent<Image>();
            border.sprite = rounded;
            border.type = Image.Type.Sliced;
            border.color = theme != null ? theme.border : Color.gray;
            border.raycastTarget = false;
            border.enabled = false;
            borderGo.AddComponent<LayoutElement>().ignoreLayout = true;

            VerticalLayoutGroup rootLayout = root.AddComponent<VerticalLayoutGroup>();
            rootLayout.padding = new RectOffset(0, 0, 0, 0);
            rootLayout.spacing = 0f;
            rootLayout.childControlWidth = true;
            rootLayout.childControlHeight = false;
            rootLayout.childForceExpandWidth = true;
            rootLayout.childForceExpandHeight = false;

            GameObject titleBar = new GameObject("TitleBar", typeof(RectTransform));
            titleBar.transform.SetParent(root.transform, false);
            Image titleBg = titleBar.AddComponent<Image>();
            titleBg.sprite = rounded;
            titleBg.type = Image.Type.Sliced;
            titleBg.color = theme != null ? theme.elevatedPanelBackground : Color.gray;
            HorizontalLayoutGroup titleHl = titleBar.AddComponent<HorizontalLayoutGroup>();
            titleHl.padding = new RectOffset(14, 10, 8, 8);
            titleHl.spacing = 8f;
            titleHl.childAlignment = TextAnchor.MiddleLeft;
            titleHl.childControlWidth = false;
            titleHl.childControlHeight = true;
            titleHl.childForceExpandHeight = true;
            LayoutElement titleLe = titleBar.AddComponent<LayoutElement>();
            titleLe.minHeight = 48f;
            titleLe.preferredHeight = 48f;

            GameObject titleGo = new GameObject("Title", typeof(RectTransform));
            titleGo.transform.SetParent(titleBar.transform, false);
            TMP_Text title = titleGo.AddComponent<TextMeshProUGUI>();
            title.text = "Home";
            title.font = font;
            title.fontSize = theme != null ? theme.headingSize : 20f;
            title.color = theme != null ? theme.primaryText : Color.white;
            title.raycastTarget = false;
            titleGo.AddComponent<LayoutElement>().flexibleWidth = 1f;

            GameObject grab = new GameObject("GrabRegion", typeof(RectTransform));
            grab.transform.SetParent(titleBar.transform, false);
            Image grabImg = grab.AddComponent<Image>();
            grabImg.color = theme != null
                ? new Color(theme.border.r, theme.border.g, theme.border.b, 0.65f)
                : Color.gray;
            grabImg.raycastTarget = true;
            LayoutElement grabLe = grab.AddComponent<LayoutElement>();
            grabLe.preferredWidth = 72f;
            grabLe.preferredHeight = 8f;

            GameObject closePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CloseButtonPath);
            GameObject close = closePrefab != null
                ? (GameObject)PrefabUtility.InstantiatePrefab(closePrefab)
                : new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
            close.name = "CloseButton";
            close.transform.SetParent(titleBar.transform, false);

            // Let titleHl compute Title/GrabRegion/CloseButton's positions once, freeze each of
            // them to an independent top-left RectTransform at that exact spot, then remove
            // titleHl — so every element in the title bar is individually movable/resizable in
            // Prefab Mode afterward, with nothing left to reset a manual edit.
            BakeChildrenToManualRects((RectTransform)titleBar.transform);
            Object.DestroyImmediate(titleHl);

            // ---------------------------------------------------------------------------
            // Content is a plain container — no layout group. Each of the six cards below
            // gets its own independent RectTransform (anchoredPosition/sizeDelta), authored
            // once here to reproduce the 3x2 arrangement (164x148 cells, 12px spacing, 16px
            // padding from Content's top-left). After generation, every card can be freely
            // dragged/resized in Prefab Mode — nothing recalculates or resets these values.
            // Card order follows child sibling order under "Content"; reorder children in
            // the Hierarchy to change card order.
            // ---------------------------------------------------------------------------
            const float CardCellWidth = 164f;
            const float CardCellHeight = 148f;
            const float CardSpacingX = 12f;
            const float CardSpacingY = 12f;
            const float CardPaddingLeft = 16f;
            const float CardPaddingTop = 16f;
            const int CardColumns = 3;
            const float ContentHeight = 2f * CardCellHeight + CardSpacingY + 2f * CardPaddingTop; // 340

            GameObject content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(root.transform, false);
            RectTransform contentRt = content.GetComponent<RectTransform>();
            contentRt.sizeDelta = new Vector2(contentRt.sizeDelta.x, ContentHeight);
            LayoutElement contentLe = content.AddComponent<LayoutElement>();
            contentLe.flexibleHeight = 1f;
            contentLe.preferredHeight = ContentHeight;

            var cardDefs = new (PanelType type, string title, string subtitle, string log, string iconPath)[]
            {
                (PanelType.TaskLibrary, "Tasks", "Manage workflows", "Opening Tasks", SpriteDir + "/IconTasks.png"),
                (PanelType.AskAI, "AI Assistant", "Ask Norscope AI", "Opening AI", SpriteDir + "/IconAI.png"),
                (PanelType.Reports, "Reports", "Inspection reports", "Opening Reports", SpriteDir + "/IconReports.png"),
                (PanelType.Settings, "Settings", "Application settings", "Opening Settings", SpriteDir + "/IconSettings.png"),
                (PanelType.Storage, "Storage", "Offline data", "Opening Storage", SpriteDir + "/IconStorage.png"),
                (PanelType.About, "About", "System information", "Opening About", SpriteDir + "/IconAbout.png"),
            };

            var cards = new List<HomePanelCard>();
            for (int i = 0; i < cardDefs.Length; i++)
            {
                var def = cardDefs[i];
                int col = i % CardColumns;
                int row = i / CardColumns;
                Vector2 anchoredPos = new Vector2(
                    CardPaddingLeft + col * (CardCellWidth + CardSpacingX),
                    -(CardPaddingTop + row * (CardCellHeight + CardSpacingY)));

                Sprite iconSprite = AssetDatabase.LoadAssetAtPath<Sprite>(def.iconPath);
                HomePanelCard card = CreateHomeCard(content.transform, def.type, def.title, def.subtitle, def.log, iconSprite, theme, rounded, font, anchoredPos);
                cards.Add(card);
            }

            // Let rootLayout compute TitleBar's and Content's positions/heights once, freeze
            // both to independent top-left RectTransforms, then remove rootLayout — so the
            // whole panel (title bar, content area, and every card inside it) is fully
            // hand-editable in Prefab Mode, with nothing left to reset a manual edit. Background/
            // Border/ResizeHandles are untouched (ignoreLayout — already freely stretch-anchored).
            BakeChildrenToManualRects(rt);
            Object.DestroyImmediate(rootLayout);

            GameObject handles = new GameObject("ResizeHandles", typeof(RectTransform));
            handles.transform.SetParent(root.transform, false);
            StretchFull(handles.GetComponent<RectTransform>());
            handles.AddComponent<LayoutElement>().ignoreLayout = true;
            PlaceHandle(handles.transform, "BottomRight", new Vector2(1f, 0f), new Vector2(-10f, 10f), theme);

            NorscopePanelRoot panelRoot = root.AddComponent<NorscopePanelRoot>();
            SerializedObject soPanel = new SerializedObject(panelRoot);
            soPanel.FindProperty("theme").objectReferenceValue = theme;
            soPanel.FindProperty("worldSpaceConfig").objectReferenceValue = world;
            soPanel.FindProperty("root").objectReferenceValue = rt;
            soPanel.FindProperty("background").objectReferenceValue = bg;
            soPanel.FindProperty("border").objectReferenceValue = border;
            soPanel.FindProperty("title").objectReferenceValue = title;
            soPanel.FindProperty("sizeMeters").vector2Value = new Vector2(0.55f, 0.40f);
            soPanel.FindProperty("sizePixels").vector2Value = new Vector2(550f, 400f);
            soPanel.FindProperty("useMetersAsSource").boolValue = true;
            soPanel.ApplyModifiedPropertiesWithoutUndo();
            panelRoot.Apply();

            SpatialWindowBindings bindings = root.AddComponent<SpatialWindowBindings>();
            SerializedObject soBind = new SerializedObject(bindings);
            soBind.FindProperty("grabRegion").objectReferenceValue = grab.transform;
            soBind.FindProperty("resizeHandle").objectReferenceValue = handles.transform.Find("BottomRight") as RectTransform;
            soBind.FindProperty("closeButtonRoot").objectReferenceValue = close.transform;
            soBind.ApplyModifiedPropertiesWithoutUndo();

            HomePanelController controller = root.AddComponent<HomePanelController>();
            SerializedObject soCtrl = new SerializedObject(controller);
            soCtrl.FindProperty("cards").arraySize = cards.Count;
            for (int i = 0; i < cards.Count; i++)
            {
                soCtrl.FindProperty("cards").GetArrayElementAtIndex(i).objectReferenceValue = cards[i];
            }

            soCtrl.ApplyModifiedPropertiesWithoutUndo();

            bgGo.transform.SetAsFirstSibling();

            PrefabUtility.SaveAsPrefabAsset(root, HomePanelPath);
            Object.DestroyImmediate(root);
            return HomePanelPath;
        }

        private static HomePanelCard CreateHomeCard(
            Transform parent,
            PanelType type,
            string titleText,
            string subtitleText,
            string logLabel,
            Sprite iconSprite,
            NorscopeTheme theme,
            Sprite rounded,
            TMP_FontAsset font,
            Vector2 anchoredPosition)
        {
            GameObject card = new GameObject("Card_" + type, typeof(RectTransform));
            card.transform.SetParent(parent, false);
            RectTransform cardRt = card.GetComponent<RectTransform>();
            // Independent RectTransform, anchored to Content's top-left — not driven by any
            // layout group, so this is freely movable/resizable by hand in Prefab Mode.
            cardRt.anchorMin = new Vector2(0f, 1f);
            cardRt.anchorMax = new Vector2(0f, 1f);
            cardRt.pivot = new Vector2(0f, 1f);
            cardRt.sizeDelta = new Vector2(164f, 148f);
            cardRt.anchoredPosition = anchoredPosition;

            Image borderImg = card.AddComponent<Image>();
            borderImg.sprite = rounded;
            borderImg.type = Image.Type.Sliced;
            borderImg.color = theme != null ? theme.border : Color.gray;
            // Raycast target spans the full card (unlike Background, which is inset 2px).
            // Without this, the hit area was 2px smaller than the visible card on every edge —
            // a naturally-jittery hand ray near a card's border flickers in/out of that dead
            // zone. Border sits behind Background so this doesn't change anything visually.
            borderImg.raycastTarget = true;

            GameObject bgGo = new GameObject("Background", typeof(RectTransform));
            bgGo.transform.SetParent(card.transform, false);
            RectTransform bgRt = bgGo.GetComponent<RectTransform>();
            StretchFull(bgRt);
            bgRt.offsetMin = new Vector2(2f, 2f);
            bgRt.offsetMax = new Vector2(-2f, -2f);
            Image bg = bgGo.AddComponent<Image>();
            bg.sprite = rounded;
            bg.type = Image.Type.Sliced;
            bg.color = theme != null ? theme.elevatedPanelBackground : Color.black;
            bg.raycastTarget = true;
            // Stays stretch-anchored (inset 2px) so it keeps filling the card if the card itself
            // is later resized by hand — excluded from the bake-to-manual-rect pass below.
            bgGo.AddComponent<LayoutElement>().ignoreLayout = true;

            VerticalLayoutGroup vl = card.AddComponent<VerticalLayoutGroup>();
            vl.padding = new RectOffset(12, 12, 12, 12);
            vl.spacing = 6f;
            vl.childAlignment = TextAnchor.UpperLeft;
            vl.childControlWidth = true;
            vl.childControlHeight = false;
            vl.childForceExpandWidth = true;
            vl.childForceExpandHeight = false;

            GameObject iconGo = new GameObject("Icon", typeof(RectTransform));
            iconGo.transform.SetParent(card.transform, false);
            Image icon = iconGo.AddComponent<Image>();
            icon.sprite = iconSprite;
            icon.color = theme != null ? theme.primaryAccent : Color.cyan;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            LayoutElement iconLe = iconGo.AddComponent<LayoutElement>();
            iconLe.preferredWidth = 28f;
            iconLe.preferredHeight = 28f;

            GameObject titleGo = new GameObject("Title", typeof(RectTransform));
            titleGo.transform.SetParent(card.transform, false);
            TMP_Text title = titleGo.AddComponent<TextMeshProUGUI>();
            title.text = titleText;
            title.font = font;
            title.fontSize = theme != null ? theme.headingSize : 18f;
            title.color = theme != null ? theme.primaryText : Color.white;
            title.raycastTarget = false;
            titleGo.AddComponent<LayoutElement>().preferredHeight = 28f;

            GameObject subGo = new GameObject("Subtitle", typeof(RectTransform));
            subGo.transform.SetParent(card.transform, false);
            TMP_Text subtitle = subGo.AddComponent<TextMeshProUGUI>();
            subtitle.text = subtitleText;
            subtitle.font = font;
            subtitle.fontSize = theme != null ? theme.captionSize : 11f;
            subtitle.color = theme != null ? theme.secondaryText : Color.gray;
            subtitle.raycastTarget = false;
            subGo.AddComponent<LayoutElement>().preferredHeight = 36f;

            // Let vl compute Icon/Title/Subtitle's positions once, freeze each of them to an
            // independent top-left RectTransform at that exact spot, then remove vl — so every
            // element inside the card (not just the card itself) is individually movable/
            // resizable in Prefab Mode. Background is skipped (ignoreLayout above) so it keeps
            // filling the card if the card is resized.
            BakeChildrenToManualRects(cardRt);
            Object.DestroyImmediate(vl);

            Button button = card.AddComponent<Button>();
            button.targetGraphic = bg;
            button.transition = Selectable.Transition.ColorTint;
            button.interactable = true;

            HomePanelCard cardComp = card.AddComponent<HomePanelCard>();
            SerializedObject so = new SerializedObject(cardComp);
            so.FindProperty("targetPanel").enumValueIndex = (int)type;
            so.FindProperty("logLabel").stringValue = logLabel;
            so.FindProperty("button").objectReferenceValue = button;
            so.FindProperty("background").objectReferenceValue = bg;
            so.FindProperty("border").objectReferenceValue = borderImg;
            so.FindProperty("icon").objectReferenceValue = icon;
            so.FindProperty("title").objectReferenceValue = title;
            so.FindProperty("subtitle").objectReferenceValue = subtitle;
            so.FindProperty("theme").objectReferenceValue = theme;
            so.ApplyModifiedPropertiesWithoutUndo();

            card.AddComponent<Norscope.UiButtonPressScale>();
            return cardComp;
        }

        private static void PlaceHandle(Transform parent, string name, Vector2 anchor, Vector2 anchoredPos, NorscopeTheme theme)
        {
            GameObject handle = new GameObject(name, typeof(RectTransform));
            handle.transform.SetParent(parent, false);
            RectTransform rt = handle.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.sizeDelta = new Vector2(16f, 16f);
            rt.anchoredPosition = anchoredPos;
            Image img = handle.AddComponent<Image>();
            img.color = theme != null
                ? new Color(theme.primaryAccent.r, theme.primaryAccent.g, theme.primaryAccent.b, 0.55f)
                : Color.cyan;
            img.raycastTarget = true;
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// Runs one layout pass on <paramref name="container"/> (letting its LayoutGroup
        /// position/size its children as authored), then converts every non-ignored direct
        /// child to an independent top-left-anchored RectTransform at that exact resulting
        /// position/size. After this, each child is a plain RectTransform — freely movable and
        /// resizable by hand in Prefab Mode — with nothing left to recompute or reset it.
        /// Children with LayoutElement.ignoreLayout = true (stretch-anchored chrome such as
        /// Background/Border/ResizeHandles) are left completely untouched.
        /// </summary>
        private static void BakeChildrenToManualRects(RectTransform container)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(container);

            for (int i = 0; i < container.childCount; i++)
            {
                RectTransform child = container.GetChild(i) as RectTransform;
                if (child == null)
                {
                    continue;
                }

                LayoutElement le = child.GetComponent<LayoutElement>();
                if (le != null && le.ignoreLayout)
                {
                    continue;
                }

                FreezeToTopLeftAnchor(child);
            }
        }

        /// <summary>
        /// Re-expresses a RectTransform's current on-screen position/size (whatever anchors it
        /// has right now, stretch or otherwise) as an equivalent top-left point anchor. Uses
        /// world corners so it is correct regardless of the RectTransform's prior anchor/pivot
        /// configuration.
        /// </summary>
        private static void FreezeToTopLeftAnchor(RectTransform rt)
        {
            RectTransform parentRt = rt.parent as RectTransform;
            if (parentRt == null)
            {
                return;
            }

            Vector3[] worldCorners = new Vector3[4]; // 0=BL, 1=TL, 2=TR, 3=BR
            rt.GetWorldCorners(worldCorners);

            Vector3 topLeftInParent = parentRt.InverseTransformPoint(worldCorners[1]);
            Vector3 bottomRightInParent = parentRt.InverseTransformPoint(worldCorners[3]);

            float width = bottomRightInParent.x - topLeftInParent.x;
            float height = topLeftInParent.y - bottomRightInParent.y;

            Rect parentLocalRect = parentRt.rect;
            float offsetX = topLeftInParent.x - parentLocalRect.xMin;
            float offsetY = parentLocalRect.yMax - topLeftInParent.y;

            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = new Vector2(offsetX, offsetY);
        }

        private static void ApplyToBootstrapScene()
        {
            Scene scene = EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);
            SpatialWindowConfig config = AssetDatabase.LoadAssetAtPath<SpatialWindowConfig>(ConfigAssetPath);
            GameObject homePanelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HomePanelPath);
            GameObject homeLauncherPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HomeLauncherPath);
            GameObject interactionPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(InteractionTestPanelPath);

            DestroyNamedIncludingInactive("HomePanel");

            GameObject system = GameObject.Find(WindowRootName);
            if (system == null)
            {
                system = new GameObject(WindowRootName);
            }

            SpatialWindowSpawner spawner = system.GetComponent<SpatialWindowSpawner>() ?? system.AddComponent<SpatialWindowSpawner>();
            SpatialWindowManager manager = system.GetComponent<SpatialWindowManager>() ?? system.AddComponent<SpatialWindowManager>();
            PanelRegistry registry = system.GetComponent<PanelRegistry>() ?? system.AddComponent<PanelRegistry>();

            TemporarySpatialWindowLauncher temp = system.GetComponent<TemporarySpatialWindowLauncher>();
            if (temp != null)
            {
                SerializedObject soTemp = new SerializedObject(temp);
                soTemp.FindProperty("createWorldButtons").boolValue = false;
                soTemp.FindProperty("openOnStart").boolValue = false;
                soTemp.ApplyModifiedPropertiesWithoutUndo();
                temp.enabled = false;
            }

            DestroyNamedIncludingInactive("TEMP_SpatialWindowLauncher");

            // This Wire step only owns the Home entry. Re-running it must not silently
            // un-register panels other phases (Phase 6 Task Library and beyond) have already
            // wired in, so carry forward whatever prefab is currently registered for every
            // other slot instead of hardcoding null.
            var entries = new PanelRegistryEntry[]
            {
                new PanelRegistryEntry { panelType = PanelType.Home, windowId = SpatialWindowId.Home, behaviour = PanelBehaviourType.RecenterOnOpen, prefab = homePanelPrefab },
                new PanelRegistryEntry { panelType = PanelType.TaskLibrary, windowId = SpatialWindowId.TaskLibrary, behaviour = PanelBehaviourType.WorldLocked, prefab = GetExistingPrefab(registry, PanelType.TaskLibrary) },
                new PanelRegistryEntry { panelType = PanelType.TaskExecution, windowId = SpatialWindowId.TaskExecution, behaviour = PanelBehaviourType.WorldLocked, prefab = GetExistingPrefab(registry, PanelType.TaskExecution) },
                new PanelRegistryEntry { panelType = PanelType.AskAI, windowId = SpatialWindowId.AskAI, behaviour = PanelBehaviourType.WorldLocked, prefab = GetExistingPrefab(registry, PanelType.AskAI) },
                new PanelRegistryEntry { panelType = PanelType.Reports, windowId = SpatialWindowId.Reports, behaviour = PanelBehaviourType.WorldLocked, prefab = GetExistingPrefab(registry, PanelType.Reports) },
                new PanelRegistryEntry { panelType = PanelType.Settings, windowId = SpatialWindowId.Settings, behaviour = PanelBehaviourType.WorldLocked, prefab = GetExistingPrefab(registry, PanelType.Settings) },
                new PanelRegistryEntry { panelType = PanelType.Storage, windowId = SpatialWindowId.Storage, behaviour = PanelBehaviourType.WorldLocked, prefab = GetExistingPrefab(registry, PanelType.Storage) },
                new PanelRegistryEntry { panelType = PanelType.About, windowId = SpatialWindowId.About, behaviour = PanelBehaviourType.WorldLocked, prefab = GetExistingPrefab(registry, PanelType.About) },
            };

            registry.SetReferences(manager, entries);
            SerializedObject soReg = new SerializedObject(registry);
            soReg.FindProperty("windowManager").objectReferenceValue = manager;
            SerializedProperty arr = soReg.FindProperty("entries");
            arr.arraySize = entries.Length;
            for (int i = 0; i < entries.Length; i++)
            {
                SerializedProperty e = arr.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("panelType").enumValueIndex = (int)entries[i].panelType;
                e.FindPropertyRelative("windowId").enumValueIndex = (int)entries[i].windowId;
                e.FindPropertyRelative("behaviour").enumValueIndex = (int)entries[i].behaviour;
                e.FindPropertyRelative("prefab").objectReferenceValue = entries[i].prefab;
            }

            soReg.ApplyModifiedPropertiesWithoutUndo();

            spawner.SetReferences(config, registry, interactionPrefab);
            SerializedObject soSpawner = new SerializedObject(spawner);
            soSpawner.FindProperty("config").objectReferenceValue = config;
            soSpawner.FindProperty("panelRegistry").objectReferenceValue = registry;
            soSpawner.FindProperty("interactionTestPanelPrefab").objectReferenceValue = interactionPrefab;
            soSpawner.ApplyModifiedPropertiesWithoutUndo();

            manager.SetReferences(spawner, config);
            SerializedObject soManager = new SerializedObject(manager);
            soManager.FindProperty("spawner").objectReferenceValue = spawner;
            soManager.FindProperty("config").objectReferenceValue = config;
            soManager.ApplyModifiedPropertiesWithoutUndo();

            GameObject existingLauncher = GameObject.Find("HomeLauncher");
            if (existingLauncher == null)
            {
                existingLauncher = FindIncludingInactive("HomeLauncher");
            }

            if (existingLauncher != null)
            {
                Object.DestroyImmediate(existingLauncher);
            }

            DestroyNamedIncludingInactive("HomeLauncherHost");

            GameObject host = new GameObject("HomeLauncherHost");
            Canvas canvas = host.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
            host.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;
            host.AddComponent<GraphicRaycaster>();
            var trackedType = System.Type.GetType(
                "UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster, Unity.XR.Interaction.Toolkit");
            if (trackedType != null)
            {
                host.AddComponent(trackedType);
            }

            RectTransform hostRt = host.GetComponent<RectTransform>();
            hostRt.sizeDelta = new Vector2(65f, 65f);
            host.transform.localScale = Vector3.one * 0.001f;

            HomeLauncherFollow follow = host.AddComponent<HomeLauncherFollow>();
            SerializedObject soFollow = new SerializedObject(follow);
            soFollow.FindProperty("followDistance").floatValue = 0.5f;
            soFollow.FindProperty("verticalOffset").floatValue = -0.5f;
            soFollow.FindProperty("horizontalOffset").floatValue = 0f;
            soFollow.FindProperty("followSpeed").floatValue = 14f;
            soFollow.FindProperty("rotationSpeed").floatValue = 14f;
            soFollow.ApplyModifiedPropertiesWithoutUndo();
            follow.SnapToTarget();

            GameObject launcherInstance = (GameObject)PrefabUtility.InstantiatePrefab(homeLauncherPrefab);
            launcherInstance.name = "HomeLauncher";
            launcherInstance.transform.SetParent(host.transform, false);
            RectTransform launcherRt = launcherInstance.GetComponent<RectTransform>();
            launcherRt.anchoredPosition = Vector2.zero;
            // Root scale/rotation are left exactly as authored on the prefab (always positive/
            // identity — orientation fixes belong on the VisualRoot child, never the interaction
            // root). No instance override is created here, so future prefab edits keep
            // propagating to this instance instead of being masked by a stuck override. This
            // instance is destroyed and freshly re-instantiated every time Wire Phase 5 Scene
            // runs (see the existingLauncher destroy above), which also clears any leftover
            // override from an older version of this method.

            HomeLauncher homeLauncher = launcherInstance.GetComponent<HomeLauncher>();
            if (homeLauncher != null)
            {
                SerializedObject soHl = new SerializedObject(homeLauncher);
                soHl.FindProperty("registry").objectReferenceValue = registry;
                soHl.ApplyModifiedPropertiesWithoutUndo();
            }

            int ui = LayerMask.NameToLayer("UI");
            if (ui >= 0)
            {
                foreach (Transform t in host.GetComponentsInChildren<Transform>(true))
                {
                    t.gameObject.layer = ui;
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void ApplyToPreviewScene()
        {
            if (!File.Exists(PreviewScenePath))
            {
                Debug.LogWarning("[Norscope Phase5] UI_Preview scene missing; skipping preview update.");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(PreviewScenePath, OpenSceneMode.Single);
            GameObject homePanelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HomePanelPath);
            GameObject homeLauncherPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HomeLauncherPath);
            GameObject interactionPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(InteractionTestPanelPath);
            NorscopeWorldSpaceUiConfig world = AssetDatabase.LoadAssetAtPath<NorscopeWorldSpaceUiConfig>(WorldConfigPath);

            DestroyNamedIncludingInactive("HomePanel");
            DestroyNamedIncludingInactive("HomeLauncher");
            DestroyNamedIncludingInactive("HomeLauncherPreview");
            DestroyNamedIncludingInactive("HomePanelPreview");

            GameObject canvasGo = GameObject.Find("WorldSpaceCanvas");
            if (canvasGo == null)
            {
                canvasGo = new GameObject("WorldSpaceCanvas", typeof(RectTransform));
                Canvas canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvasGo.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;
                canvasGo.AddComponent<GraphicRaycaster>();
            }

            RectTransform canvasRt = canvasGo.GetComponent<RectTransform>();
            canvasRt.sizeDelta = new Vector2(1200f, 500f);
            if (world != null)
            {
                world.ApplyToCanvas(canvasRt);
            }

            canvasGo.transform.position = Vector3.zero;

            GameObject interaction = GameObject.Find("InteractionTestPanel");
            if (interaction == null)
            {
                interaction = (GameObject)PrefabUtility.InstantiatePrefab(interactionPrefab);
                interaction.name = "InteractionTestPanel";
                interaction.transform.SetParent(canvasGo.transform, false);
            }

            RectTransform interactionRt = interaction.GetComponent<RectTransform>();
            interactionRt.anchoredPosition = new Vector2(-320f, 0f);

            GameObject homePanel = (GameObject)PrefabUtility.InstantiatePrefab(homePanelPrefab);
            homePanel.name = "HomePanel";
            homePanel.transform.SetParent(canvasGo.transform, false);
            RectTransform homeRt = homePanel.GetComponent<RectTransform>();
            homeRt.anchoredPosition = new Vector2(280f, 0f);

            GameObject launcherHost = new GameObject("HomeLauncherPreview", typeof(RectTransform));
            launcherHost.transform.SetParent(canvasGo.transform, false);
            RectTransform launcherHostRt = launcherHost.GetComponent<RectTransform>();
            launcherHostRt.anchoredPosition = new Vector2(0f, -220f);
            launcherHostRt.sizeDelta = new Vector2(65f, 65f);

            GameObject launcher = (GameObject)PrefabUtility.InstantiatePrefab(homeLauncherPrefab);
            launcher.name = "HomeLauncher";
            launcher.transform.SetParent(launcherHost.transform, false);

            if (GameObject.Find("EventSystem") == null && Object.FindFirstObjectByType<EventSystem>() == null)
            {
                GameObject es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                var inputModuleType = System.Type.GetType(
                    "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
                if (inputModuleType != null)
                {
                    es.AddComponent(inputModuleType);
                }
                else
                {
                    es.AddComponent<StandaloneInputModule>();
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);
        }

        public static bool Validate(out string report)
        {
            var sb = new StringBuilder();
            int fails = 0;

            void Check(bool ok, string label)
            {
                sb.AppendLine((ok ? "PASS  " : "FAIL  ") + label);
                if (!ok) fails++;
            }

            Check(File.Exists(HomeLauncherPath), "HomeLauncher.prefab exists");
            Check(File.Exists(HomePanelPath), "HomePanel.prefab exists");
            Check(File.Exists(InteractionTestPanelPath), "InteractionTestPanel.prefab exists");

            string[] launcherGuids = AssetDatabase.FindAssets("t:Prefab HomeLauncher");
            int launcherCount = launcherGuids.Count(g =>
                AssetDatabase.GUIDToAssetPath(g).Replace('\\', '/').EndsWith("/HomeLauncher.prefab"));
            Check(launcherCount == 1, "Exactly one HomeLauncher.prefab (found " + launcherCount + ")");

            string[] homePanelGuids = AssetDatabase.FindAssets("t:Prefab HomePanel");
            int homePanelCount = homePanelGuids.Count(g =>
                AssetDatabase.GUIDToAssetPath(g).Replace('\\', '/').EndsWith("/HomePanel.prefab"));
            Check(homePanelCount == 1, "Exactly one HomePanel.prefab (found " + homePanelCount + ")");

            Check(!Directory.Exists("Assets/Norscope/UI/Generated"), "No Generated UI folder");
            Check(!Directory.Exists("Assets/Resources") ||
                  AssetDatabase.FindAssets("HomePanel", new[] { "Assets/Resources" }).Length == 0,
                "No Resources duplicates for HomePanel");

            EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);
            PanelRegistry[] registries = Object.FindObjectsByType<PanelRegistry>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Check(registries.Length == 1, "Exactly one PanelRegistry (found " + registries.Length + ")");

            HomeLauncherFollow[] follows = Object.FindObjectsByType<HomeLauncherFollow>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Check(follows.Length == 1, "Exactly one HomeLauncherFollow (found " + follows.Length + ")");

            HomeLauncher[] launchers = Object.FindObjectsByType<HomeLauncher>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Check(launchers.Length == 1, "Exactly one HomeLauncher instance (found " + launchers.Length + ")");

            if (launchers.Length == 1)
            {
                Check(NoBlockingTransformOverride(launchers[0].gameObject),
                    "HomeLauncher root has no transform override blocking orientation changes");
            }

            XROrigin[] origins = Object.FindObjectsByType<XROrigin>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Check(origins.Length == 1, "Exactly one XROrigin");

            SpatialWindowManager[] managers = Object.FindObjectsByType<SpatialWindowManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Check(managers.Length == 1, "Exactly one SpatialWindowManager");

            if (registries.Length == 1)
            {
                PanelRegistry reg = registries[0];
                reg.RebuildLookup();
                bool hasHome = reg.TryGetEntry(PanelType.Home, out PanelRegistryEntry home) && home.prefab != null;
                Check(hasHome, "Registry has Home prefab");
                Check(hasHome && home.behaviour == PanelBehaviourType.RecenterOnOpen, "Home behaviour is RecenterOnOpen");
                Check(reg.TryGetEntry(PanelType.TaskLibrary, out PanelRegistryEntry tasks) &&
                      tasks.behaviour == PanelBehaviourType.WorldLocked,
                    "TaskLibrary behaviour is WorldLocked");

                var types = new HashSet<PanelType>();
                var ids = new HashSet<SpatialWindowId>();
                bool dupType = false;
                bool dupId = false;
                foreach (PanelRegistryEntry e in reg.Entries)
                {
                    if (e == null) continue;
                    if (!types.Add(e.panelType)) dupType = true;
                    if (e.windowId != SpatialWindowId.None && !ids.Add(e.windowId)) dupId = true;
                }

                Check(!dupType, "No duplicate PanelType IDs");
                Check(!dupId, "No duplicate SpatialWindowId values");
            }

            GameObject homePanelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HomePanelPath);
            if (homePanelPrefab != null)
            {
                NorscopePanelRoot size = homePanelPrefab.GetComponent<NorscopePanelRoot>();
                Check(size != null, "HomePanel has NorscopePanelRoot");
                if (size != null)
                {
                    Check(Mathf.Abs(size.SizeMeters.x - 0.55f) < 0.02f && Mathf.Abs(size.SizeMeters.y - 0.40f) < 0.02f,
                        "HomePanel size ~0.55 x 0.40 m");
                }

                Check(homePanelPrefab.GetComponent<SpatialWindowBindings>() != null, "HomePanel has SpatialWindowBindings");
                HomePanelController ctrl = homePanelPrefab.GetComponent<HomePanelController>();
                Check(ctrl != null && ctrl.Cards != null && ctrl.Cards.Length == 6, "HomePanel contains exactly six cards");
                Check(homePanelPrefab.transform.Find("TitleBar") != null, "HomePanel has TitleBar");

                Check(homePanelPrefab.GetComponentInChildren<GridLayoutGroup>(true) == null,
                    "HomePanel cards are individually positioned (no GridLayoutGroup)");

                Transform contentTransform = homePanelPrefab.transform.Find("Content");
                bool cardGridArrangementOk = false;
                if (contentTransform != null && contentTransform.childCount == 6)
                {
                    var distinctX = new HashSet<int>();
                    var distinctY = new HashSet<int>();
                    for (int i = 0; i < contentTransform.childCount; i++)
                    {
                        RectTransform cardChildRt = contentTransform.GetChild(i) as RectTransform;
                        if (cardChildRt == null) continue;
                        distinctX.Add(Mathf.RoundToInt(cardChildRt.anchoredPosition.x));
                        distinctY.Add(Mathf.RoundToInt(cardChildRt.anchoredPosition.y));
                    }

                    cardGridArrangementOk = distinctX.Count == 3 && distinctY.Count == 2;
                }

                Check(cardGridArrangementOk, "HomePanel cards form a 3 column x 2 row arrangement");
            }

            Check(NoAutoTriggerOnDestructiveRebuild(),
                "No automatic process regenerates HomePanel/HomeLauncher (destructive rebuild has no auto-trigger)");

            Check(NormalWiringPreservesPrefabs(),
                "Normal scene wiring (Create/Wire) does not overwrite HomeLauncher/HomePanel prefabs");

            GameObject launcherPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HomeLauncherPath);
            Check(launcherPrefab != null && launcherPrefab.GetComponent<HomeLauncher>() != null, "HomeLauncher prefab has HomeLauncher");
            Check(launcherPrefab != null && launcherPrefab.GetComponentInChildren<Button>(true) != null, "HomeLauncher has Button");

            if (File.Exists(PreviewScenePath))
            {
                Scene preview = EditorSceneManager.OpenScene(PreviewScenePath, OpenSceneMode.Additive);
                bool hasHome = false;
                bool hasLauncher = false;
                bool hasInteraction = false;
                foreach (GameObject go in preview.GetRootGameObjects())
                {
                    foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
                    {
                        if (t.name == "HomePanel") hasHome = true;
                        if (t.name == "HomeLauncher") hasLauncher = true;
                        if (t.name == "InteractionTestPanel") hasInteraction = true;
                    }
                }

                Check(hasHome && hasLauncher && hasInteraction,
                    "UI_Preview contains HomeLauncher, HomePanel, InteractionTestPanel");
                EditorSceneManager.CloseScene(preview, true);
            }

            EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);
            report = sb.ToString();
            return fails == 0;
        }

        /// <summary>
        /// True if the given prefab-instance GameObject's own transform (position aside) has no
        /// scale/rotation override recorded against the connected prefab — i.e. nothing is
        /// pinning the root's orientation independent of the prefab asset.
        /// </summary>
        private static bool NoBlockingTransformOverride(GameObject instance)
        {
            if (instance == null || !PrefabUtility.IsPartOfPrefabInstance(instance))
            {
                return true;
            }

            PropertyModification[] mods = PrefabUtility.GetPropertyModifications(instance);
            if (mods == null)
            {
                return true;
            }

            foreach (PropertyModification m in mods)
            {
                if (m.target != instance.transform)
                {
                    continue;
                }

                if (m.propertyPath.StartsWith("m_LocalScale") ||
                    m.propertyPath.StartsWith("m_LocalRotation") ||
                    m.propertyPath.StartsWith("m_LocalEulerAnglesHint"))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Reflection guard: the destructive rebuild must never be wired to run automatically
        /// (no [InitializeOnLoadMethod], no flag-file auto-trigger like the ones Create/Wire use).
        /// </summary>
        private static bool NoAutoTriggerOnDestructiveRebuild()
        {
            MethodInfo method = typeof(Phase5NavigationConfigurator).GetMethod(
                nameof(RebuildPhase5PrefabsDestructive), BindingFlags.Public | BindingFlags.Static);
            MethodInfo menuMethod = typeof(Phase5NavigationConfigurator).GetMethod(
                nameof(RebuildPhase5PrefabsDestructiveMenu), BindingFlags.Public | BindingFlags.Static);

            if (method == null || menuMethod == null)
            {
                return false;
            }

            bool methodHasAutoAttr = method.GetCustomAttribute<InitializeOnLoadMethodAttribute>() != null;
            bool menuHasAutoAttr = menuMethod.GetCustomAttribute<InitializeOnLoadMethodAttribute>() != null;
            return !methodHasAutoAttr && !menuHasAutoAttr;
        }

        /// <summary>
        /// Empirically proves normal Create/Wire operations never modify an existing
        /// HomeLauncher.prefab / HomePanel.prefab by hashing both files, running the real
        /// non-destructive entry point, and confirming the hashes are unchanged.
        /// </summary>
        private static bool NormalWiringPreservesPrefabs()
        {
            string launcherBefore = ComputeFileHash(HomeLauncherPath);
            string panelBefore = ComputeFileHash(HomePanelPath);

            CreatePhase5PrefabsIfMissing();

            string launcherAfter = ComputeFileHash(HomeLauncherPath);
            string panelAfter = ComputeFileHash(HomePanelPath);

            return launcherBefore == launcherAfter && panelBefore == panelAfter;
        }

        private static string ComputeFileHash(string assetPath)
        {
            if (!File.Exists(assetPath))
            {
                return "MISSING";
            }

            byte[] bytes = File.ReadAllBytes(assetPath);
            using (SHA256 sha = SHA256.Create())
            {
                return System.Convert.ToBase64String(sha.ComputeHash(bytes));
            }
        }

        /// <summary>Reads the prefab currently registered for a PanelType from an existing
        /// PanelRegistry component (before its entries array gets rebuilt), so re-running
        /// Wire Phase 5 Scene doesn't clobber registrations owned by other phases.</summary>
        private static GameObject GetExistingPrefab(PanelRegistry registry, PanelType type)
        {
            if (registry == null)
            {
                return null;
            }

            foreach (PanelRegistryEntry entry in registry.Entries)
            {
                if (entry != null && entry.panelType == type)
                {
                    return entry.prefab;
                }
            }

            return null;
        }

        private static void DestroyNamedIncludingInactive(string name)
        {
            GameObject go = GameObject.Find(name);
            if (go == null) go = FindIncludingInactive(name);
            if (go != null) Object.DestroyImmediate(go);
        }

        private static GameObject FindIncludingInactive(string name)
        {
            Transform[] all = Resources.FindObjectsOfTypeAll<Transform>();
            foreach (Transform t in all)
            {
                if (t == null || t.name != name) continue;
                if (!string.IsNullOrEmpty(t.gameObject.scene.name))
                {
                    return t.gameObject;
                }
            }

            return null;
        }
    }
}
