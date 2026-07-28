using System.IO;
using Norscope.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Norscope.Editor
{
    /// <summary>
    /// One-shot builder for the Canvas design-system prefabs and preview scene.
    /// Prefabs are fully authored assets afterward — this menu is not a runtime dependency.
    /// </summary>
    public static class NorscopeUiDesignSystemBuilder
    {
        private const string Root = "Assets/Norscope/UI";
        private const string ThemePath = Root + "/DesignSystem/Themes/NorscopeTheme.asset";
        private const string WorldConfigPath = Root + "/DesignSystem/Themes/NorscopeWorldSpaceUiConfig.asset";
        private const string SpriteDir = Root + "/DesignSystem/Sprites";
        private const string ControlsDir = Root + "/Controls";
        private const string PanelsDir = Root + "/Panels";
        private const string PreviewScenePath = "Assets/Norscope/Scenes/UI_Preview.unity";

        [MenuItem("Norscope/UI/Build Canvas Design System (One-Shot)")]
        public static void Build()
        {
            EnsureFolders();
            Sprite rounded = CreateOrLoadRoundedSprite("UIRounded_8", 64, 8, 8);
            Sprite roundedSm = CreateOrLoadRoundedSprite("UIRounded_6", 64, 6, 6);
            Sprite white = CreateOrLoadSolidSprite("UIWhite", 32);
            Sprite closeIcon = CreateOrLoadCloseSprite();
            Sprite checkIcon = CreateOrLoadCheckSprite();

            TMP_FontAsset font = FindDefaultTmpFont();
            NorscopeTheme theme = CreateOrLoadTheme(font);
            NorscopeWorldSpaceUiConfig world = CreateOrLoadWorldConfig();

            // Controls
            string primary = BuildButtonPrefab("NorscopePrimaryButton", NorscopeButtonControl.ButtonVariant.Primary, "Primary", null, new Vector2(180f, 40f), theme, rounded, font);
            string secondary = BuildButtonPrefab("NorscopeSecondaryButton", NorscopeButtonControl.ButtonVariant.Secondary, "Secondary", null, new Vector2(180f, 40f), theme, rounded, font);
            string iconBtn = BuildButtonPrefab("NorscopeIconButton", NorscopeButtonControl.ButtonVariant.Icon, string.Empty, checkIcon, new Vector2(40f, 40f), theme, roundedSm, font);
            string closeBtn = BuildButtonPrefab("NorscopeCloseButton", NorscopeButtonControl.ButtonVariant.Close, string.Empty, closeIcon, new Vector2(32f, 32f), theme, roundedSm, font);
            string card = BuildFramePrefab("NorscopeCard", NorscopeThemedFrame.FrameRole.Card, new Vector2(220f, 140f), theme, rounded, true);
            string badge = BuildBadgePrefab(theme, roundedSm, font);
            string tab = BuildTabPrefab(theme, roundedSm, font);
            string panelFrame = BuildFramePrefab("NorscopePanelFrame", NorscopeThemedFrame.FrameRole.PanelFrame, new Vector2(550f, 380f), theme, rounded, true);
            string titleBar = BuildTitleBarPrefab(theme, rounded, font, closeBtn);
            string grabBar = BuildGrabBarPrefab(theme, white);
            string resize = BuildResizeHandlePrefab(theme, white);

            string panel = BuildInteractionTestPanel(theme, world, rounded, font, primary, secondary, iconBtn, closeBtn, badge);

            BuildPreviewScene(panel, world);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Norscope UI] Canvas design system built.\n" +
                      "Theme: " + ThemePath + "\n" +
                      "Panel: " + panel + "\n" +
                      "Preview: " + PreviewScenePath + "\n" +
                      "Also created: " + primary + ", " + secondary + ", " + iconBtn + ", " + closeBtn + ", " +
                      card + ", " + badge + ", " + tab + ", " + panelFrame + ", " + titleBar + ", " + grabBar + ", " + resize);
        }

        public static void BuildForBatchMode()
        {
            try
            {
                Build();
                EditorApplication.Exit(0);
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[Norscope UI] Build failed:\n" + ex);
                EditorApplication.Exit(1);
            }
        }

        private static void EnsureFolders()
        {
            string[] folders =
            {
                "Assets/Norscope",
                "Assets/Norscope/UI",
                "Assets/Norscope/UI/DesignSystem",
                "Assets/Norscope/UI/DesignSystem/Themes",
                "Assets/Norscope/UI/DesignSystem/Materials",
                "Assets/Norscope/UI/DesignSystem/Sprites",
                "Assets/Norscope/UI/DesignSystem/Fonts",
                "Assets/Norscope/UI/DesignSystem/Icons",
                "Assets/Norscope/UI/Controls",
                "Assets/Norscope/UI/Panels",
                "Assets/Norscope/UI/Preview",
                "Assets/Norscope/Scenes"
            };

            foreach (string folder in folders)
            {
                if (!AssetDatabase.IsValidFolder(folder))
                {
                    string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
                    string name = Path.GetFileName(folder);
                    if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(name))
                    {
                        AssetDatabase.CreateFolder(parent, name);
                    }
                }
            }
        }

        private static NorscopeTheme CreateOrLoadTheme(TMP_FontAsset font)
        {
            NorscopeTheme theme = AssetDatabase.LoadAssetAtPath<NorscopeTheme>(ThemePath);
            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<NorscopeTheme>();
                AssetDatabase.CreateAsset(theme, ThemePath);
            }

            theme.fonts.titleFont = font;
            theme.fonts.headingFont = font;
            theme.fonts.bodyFont = font;
            theme.fonts.captionFont = font;
            EditorUtility.SetDirty(theme);
            return theme;
        }

        private static NorscopeWorldSpaceUiConfig CreateOrLoadWorldConfig()
        {
            NorscopeWorldSpaceUiConfig cfg = AssetDatabase.LoadAssetAtPath<NorscopeWorldSpaceUiConfig>(WorldConfigPath);
            if (cfg == null)
            {
                cfg = ScriptableObject.CreateInstance<NorscopeWorldSpaceUiConfig>();
                cfg.pixelsPerMeter = 1000f;
                AssetDatabase.CreateAsset(cfg, WorldConfigPath);
            }

            return cfg;
        }

        private static TMP_FontAsset FindDefaultTmpFont()
        {
            EnsureTmpEssentialsImported();

            string[] guids = AssetDatabase.FindAssets("LiberationSans SDF t:TMP_FontAsset");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                if (font != null)
                {
                    return font;
                }
            }

            // Absolute package fallback paths (unitypackage import target).
            string[] fallbacks =
            {
                "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset",
                "Assets/TextMesh Pro/Fonts/LiberationSans SDF.asset"
            };
            foreach (string path in fallbacks)
            {
                TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                if (font != null)
                {
                    return font;
                }
            }

            try
            {
                if (TMP_Settings.defaultFontAsset != null)
                {
                    return TMP_Settings.defaultFontAsset;
                }
            }
            catch (System.Exception)
            {
                // TMP settings may be missing until essentials are imported.
            }

            throw new System.InvalidOperationException(
                "TMP font not found. Import TMP Essential Resources (Window > TextMeshPro > Import TMP Essential Resources), then rerun Norscope/UI/Build Canvas Design System.");
        }

        private static void EnsureTmpEssentialsImported()
        {
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset") != null)
            {
                return;
            }

            Debug.LogWarning("[Norscope UI] TMP Essential Resources not found under Assets/TextMesh Pro. Skipping package import in batchmode.");
        }

        private static Sprite CreateOrLoadSolidSprite(string name, int size)
        {
            string path = SpriteDir + "/" + name + ".png";
            if (File.Exists(path))
            {
                return AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(255, 255, 255, 255);
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            SetSpriteImporter(path, 0);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static Sprite CreateOrLoadRoundedSprite(string name, int size, int radius, int border)
        {
            string path = SpriteDir + "/" + name + ".png";
            if (!File.Exists(path))
            {
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        bool inside = InsideRoundedRect(x, y, size, size, radius);
                        tex.SetPixel(x, y, inside ? Color.white : Color.clear);
                    }
                }

                tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
            }

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            SetSpriteImporter(path, border);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static Sprite CreateOrLoadCloseSprite()
        {
            string path = SpriteDir + "/IconClose.png";
            if (!File.Exists(path))
            {
                int size = 64;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                Clear(tex);
                DrawLine(tex, 16, 16, 47, 47, 3);
                DrawLine(tex, 47, 16, 16, 47, 3);
                tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
            }

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            SetSpriteImporter(path, 0);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static Sprite CreateOrLoadCheckSprite()
        {
            string path = SpriteDir + "/IconCheck.png";
            if (!File.Exists(path))
            {
                int size = 64;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                Clear(tex);
                DrawLine(tex, 14, 32, 26, 44, 3);
                DrawLine(tex, 26, 44, 50, 18, 3);
                tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
            }

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            SetSpriteImporter(path, 0);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void SetSpriteImporter(string path, int border)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            if (border > 0)
            {
                importer.spriteBorder = new Vector4(border, border, border, border);
            }

            importer.SaveAndReimport();
        }

        private static bool InsideRoundedRect(int x, int y, int w, int h, int r)
        {
            int cx = Mathf.Clamp(x, r, w - 1 - r);
            int cy = Mathf.Clamp(y, r, h - 1 - r);
            int dx = x - cx;
            int dy = y - cy;
            return dx * dx + dy * dy <= r * r;
        }

        private static void Clear(Texture2D tex)
        {
            for (int y = 0; y < tex.height; y++)
            {
                for (int x = 0; x < tex.width; x++)
                {
                    tex.SetPixel(x, y, Color.clear);
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
                        int px = x + ox;
                        int py = y + oy;
                        if (px >= 0 && py >= 0 && px < tex.width && py < tex.height)
                        {
                            tex.SetPixel(px, py, Color.white);
                        }
                    }
                }
            }
        }

        private static string BuildButtonPrefab(
            string name,
            NorscopeButtonControl.ButtonVariant variant,
            string text,
            Sprite icon,
            Vector2 size,
            NorscopeTheme theme,
            Sprite rounded,
            TMP_FontAsset font)
        {
            string path = ControlsDir + "/" + name + ".prefab";
            GameObject root = new GameObject(name, typeof(RectTransform));
            RectTransform rt = root.GetComponent<RectTransform>();
            rt.sizeDelta = size;

            Image bg = root.AddComponent<Image>();
            bg.sprite = rounded;
            bg.type = Image.Type.Sliced;
            bg.raycastTarget = true;

            Button button = root.AddComponent<Button>();
            button.targetGraphic = bg;
            root.AddComponent<UiButtonPressScale>();

            HorizontalLayoutGroup hl = root.AddComponent<HorizontalLayoutGroup>();
            hl.childAlignment = TextAnchor.MiddleCenter;
            hl.childControlWidth = false;
            hl.childControlHeight = false;
            hl.childForceExpandWidth = false;
            hl.childForceExpandHeight = false;
            hl.padding = new RectOffset(12, 12, 8, 8);
            hl.spacing = 8f;

            LayoutElement le = root.AddComponent<LayoutElement>();
            le.preferredWidth = size.x;
            le.preferredHeight = size.y;

            Image iconImg = null;
            if (icon != null || variant == NorscopeButtonControl.ButtonVariant.Icon || variant == NorscopeButtonControl.ButtonVariant.Close)
            {
                GameObject iconGo = new GameObject("Icon", typeof(RectTransform));
                iconGo.transform.SetParent(root.transform, false);
                iconImg = iconGo.AddComponent<Image>();
                iconImg.sprite = icon;
                iconImg.raycastTarget = false;
                RectTransform iconRt = iconGo.GetComponent<RectTransform>();
                iconRt.sizeDelta = new Vector2(18f, 18f);
                iconGo.AddComponent<LayoutElement>().preferredWidth = 18f;
            }

            GameObject labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(root.transform, false);
            TMP_Text label = labelGo.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.font = font;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            labelGo.GetComponent<RectTransform>().sizeDelta = new Vector2(size.x - 24f, size.y - 8f);
            if (variant == NorscopeButtonControl.ButtonVariant.Icon || variant == NorscopeButtonControl.ButtonVariant.Close)
            {
                labelGo.SetActive(false);
            }

            NorscopeButtonControl control = root.AddComponent<NorscopeButtonControl>();
            SerializedObject so = new SerializedObject(control);
            so.FindProperty("theme").objectReferenceValue = theme;
            so.FindProperty("variant").enumValueIndex = (int)variant;
            so.FindProperty("button").objectReferenceValue = button;
            so.FindProperty("background").objectReferenceValue = bg;
            so.FindProperty("iconImage").objectReferenceValue = iconImg;
            so.FindProperty("label").objectReferenceValue = label;
            so.FindProperty("root").objectReferenceValue = rt;
            so.FindProperty("layoutElement").objectReferenceValue = le;
            so.FindProperty("text").stringValue = text;
            so.FindProperty("icon").objectReferenceValue = icon;
            so.FindProperty("size").vector2Value = size;
            so.ApplyModifiedPropertiesWithoutUndo();
            control.ApplyAll();

            string prefabPath = SavePrefab(root, path);
            Object.DestroyImmediate(root);
            return prefabPath;
        }

        private static string BuildBadgePrefab(NorscopeTheme theme, Sprite rounded, TMP_FontAsset font)
        {
            string path = ControlsDir + "/NorscopeStatusBadge.prefab";
            GameObject root = new GameObject("NorscopeStatusBadge", typeof(RectTransform));
            RectTransform rt = root.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(88f, 24f);
            Image bg = root.AddComponent<Image>();
            bg.sprite = rounded;
            bg.type = Image.Type.Sliced;
            HorizontalLayoutGroup hl = root.AddComponent<HorizontalLayoutGroup>();
            hl.childAlignment = TextAnchor.MiddleCenter;
            hl.padding = new RectOffset(10, 10, 4, 4);
            LayoutElement le = root.AddComponent<LayoutElement>();

            GameObject labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(root.transform, false);
            TMP_Text label = labelGo.AddComponent<TextMeshProUGUI>();
            label.text = "Ready";
            label.font = font;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;

            NorscopeBadgeControl control = root.AddComponent<NorscopeBadgeControl>();
            SerializedObject so = new SerializedObject(control);
            so.FindProperty("theme").objectReferenceValue = theme;
            so.FindProperty("background").objectReferenceValue = bg;
            so.FindProperty("label").objectReferenceValue = label;
            so.FindProperty("root").objectReferenceValue = rt;
            so.FindProperty("layoutElement").objectReferenceValue = le;
            so.ApplyModifiedPropertiesWithoutUndo();
            control.Apply();

            string prefabPath = SavePrefab(root, path);
            Object.DestroyImmediate(root);
            return prefabPath;
        }

        private static string BuildTabPrefab(NorscopeTheme theme, Sprite rounded, TMP_FontAsset font)
        {
            string path = ControlsDir + "/NorscopeTab.prefab";
            GameObject root = new GameObject("NorscopeTab", typeof(RectTransform));
            RectTransform rt = root.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(96f, 32f);
            Image bg = root.AddComponent<Image>();
            bg.sprite = rounded;
            bg.type = Image.Type.Sliced;
            Button button = root.AddComponent<Button>();
            button.targetGraphic = bg;
            HorizontalLayoutGroup hl = root.AddComponent<HorizontalLayoutGroup>();
            hl.childAlignment = TextAnchor.MiddleCenter;
            hl.padding = new RectOffset(12, 12, 6, 6);
            LayoutElement le = root.AddComponent<LayoutElement>();

            GameObject labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(root.transform, false);
            TMP_Text label = labelGo.AddComponent<TextMeshProUGUI>();
            label.text = "Tab";
            label.font = font;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;

            NorscopeTabControl control = root.AddComponent<NorscopeTabControl>();
            SerializedObject so = new SerializedObject(control);
            so.FindProperty("theme").objectReferenceValue = theme;
            so.FindProperty("button").objectReferenceValue = button;
            so.FindProperty("background").objectReferenceValue = bg;
            so.FindProperty("label").objectReferenceValue = label;
            so.FindProperty("root").objectReferenceValue = rt;
            so.FindProperty("layoutElement").objectReferenceValue = le;
            so.ApplyModifiedPropertiesWithoutUndo();
            control.Apply();

            string prefabPath = SavePrefab(root, path);
            Object.DestroyImmediate(root);
            return prefabPath;
        }

        private static string BuildFramePrefab(
            string name,
            NorscopeThemedFrame.FrameRole role,
            Vector2 size,
            NorscopeTheme theme,
            Sprite rounded,
            bool withLayout)
        {
            string path = ControlsDir + "/" + name + ".prefab";
            GameObject root = new GameObject(name, typeof(RectTransform));
            RectTransform rt = root.GetComponent<RectTransform>();
            rt.sizeDelta = size;
            Image bg = root.AddComponent<Image>();
            bg.sprite = rounded;
            bg.type = Image.Type.Sliced;
            bg.raycastTarget = true;

            LayoutElement le = root.AddComponent<LayoutElement>();
            le.preferredWidth = size.x;
            le.preferredHeight = size.y;

            if (withLayout)
            {
                VerticalLayoutGroup vl = root.AddComponent<VerticalLayoutGroup>();
                vl.padding = new RectOffset(12, 12, 12, 12);
                vl.spacing = 8f;
                vl.childControlWidth = true;
                vl.childControlHeight = false;
                vl.childForceExpandWidth = true;
                vl.childForceExpandHeight = false;
            }

            NorscopeThemedFrame frame = root.AddComponent<NorscopeThemedFrame>();
            SerializedObject so = new SerializedObject(frame);
            so.FindProperty("theme").objectReferenceValue = theme;
            so.FindProperty("role").enumValueIndex = (int)role;
            so.FindProperty("background").objectReferenceValue = bg;
            so.FindProperty("root").objectReferenceValue = rt;
            so.FindProperty("layoutElement").objectReferenceValue = le;
            so.FindProperty("size").vector2Value = size;
            so.ApplyModifiedPropertiesWithoutUndo();
            frame.Apply();

            string prefabPath = SavePrefab(root, path);
            Object.DestroyImmediate(root);
            return prefabPath;
        }

        private static string BuildTitleBarPrefab(NorscopeTheme theme, Sprite rounded, TMP_FontAsset font, string closePrefabPath)
        {
            string path = ControlsDir + "/NorscopeTitleBar.prefab";
            GameObject root = new GameObject("NorscopeTitleBar", typeof(RectTransform));
            RectTransform rt = root.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(550f, 44f);
            Image bg = root.AddComponent<Image>();
            bg.sprite = rounded;
            bg.type = Image.Type.Sliced;

            HorizontalLayoutGroup hl = root.AddComponent<HorizontalLayoutGroup>();
            hl.padding = new RectOffset(12, 8, 6, 6);
            hl.spacing = 8f;
            hl.childAlignment = TextAnchor.MiddleLeft;
            hl.childControlWidth = false;
            hl.childControlHeight = true;
            hl.childForceExpandWidth = false;
            hl.childForceExpandHeight = true;

            LayoutElement le = root.AddComponent<LayoutElement>();
            le.minHeight = 44f;
            le.preferredHeight = 44f;
            le.flexibleWidth = 1f;

            GameObject titleGo = new GameObject("Title", typeof(RectTransform));
            titleGo.transform.SetParent(root.transform, false);
            TMP_Text title = titleGo.AddComponent<TextMeshProUGUI>();
            title.text = "Panel Title";
            title.font = font;
            title.fontSize = theme.headingSize;
            title.color = theme.primaryText;
            title.raycastTarget = false;
            LayoutElement titleLe = titleGo.AddComponent<LayoutElement>();
            titleLe.flexibleWidth = 1f;
            titleLe.preferredHeight = 32f;

            GameObject grab = new GameObject("GrabRegion", typeof(RectTransform));
            grab.transform.SetParent(root.transform, false);
            Image grabImg = grab.AddComponent<Image>();
            grabImg.color = new Color(1f, 1f, 1f, 0.001f);
            grabImg.raycastTarget = true;
            LayoutElement grabLe = grab.AddComponent<LayoutElement>();
            grabLe.preferredWidth = 80f;
            grabLe.preferredHeight = 24f;

            GameObject closeInstance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(closePrefabPath));
            closeInstance.name = "CloseButton";
            closeInstance.transform.SetParent(root.transform, false);

            NorscopeThemedFrame frame = root.AddComponent<NorscopeThemedFrame>();
            SerializedObject so = new SerializedObject(frame);
            so.FindProperty("theme").objectReferenceValue = theme;
            so.FindProperty("role").enumValueIndex = (int)NorscopeThemedFrame.FrameRole.TitleBar;
            so.FindProperty("background").objectReferenceValue = bg;
            so.FindProperty("root").objectReferenceValue = rt;
            so.FindProperty("layoutElement").objectReferenceValue = le;
            so.FindProperty("size").vector2Value = new Vector2(550f, 44f);
            so.ApplyModifiedPropertiesWithoutUndo();
            frame.Apply();

            string prefabPath = SavePrefab(root, path);
            Object.DestroyImmediate(root);
            return prefabPath;
        }

        private static string BuildGrabBarPrefab(NorscopeTheme theme, Sprite white)
        {
            string path = ControlsDir + "/NorscopeGrabBar.prefab";
            GameObject root = new GameObject("NorscopeGrabBar", typeof(RectTransform));
            RectTransform rt = root.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(64f, 6f);
            Image bg = root.AddComponent<Image>();
            bg.sprite = white;
            bg.raycastTarget = true;
            LayoutElement le = root.AddComponent<LayoutElement>();
            le.preferredWidth = 64f;
            le.preferredHeight = 6f;

            NorscopeThemedFrame frame = root.AddComponent<NorscopeThemedFrame>();
            SerializedObject so = new SerializedObject(frame);
            so.FindProperty("theme").objectReferenceValue = theme;
            so.FindProperty("role").enumValueIndex = (int)NorscopeThemedFrame.FrameRole.GrabBar;
            so.FindProperty("background").objectReferenceValue = bg;
            so.FindProperty("root").objectReferenceValue = rt;
            so.FindProperty("layoutElement").objectReferenceValue = le;
            so.FindProperty("size").vector2Value = new Vector2(64f, 6f);
            so.ApplyModifiedPropertiesWithoutUndo();
            frame.Apply();

            string prefabPath = SavePrefab(root, path);
            Object.DestroyImmediate(root);
            return prefabPath;
        }

        private static string BuildResizeHandlePrefab(NorscopeTheme theme, Sprite white)
        {
            string path = ControlsDir + "/NorscopeResizeHandle.prefab";
            GameObject root = new GameObject("NorscopeResizeHandle", typeof(RectTransform));
            RectTransform rt = root.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(16f, 16f);
            Image bg = root.AddComponent<Image>();
            bg.sprite = white;
            bg.raycastTarget = true;
            LayoutElement le = root.AddComponent<LayoutElement>();
            le.preferredWidth = 16f;
            le.preferredHeight = 16f;

            NorscopeThemedFrame frame = root.AddComponent<NorscopeThemedFrame>();
            SerializedObject so = new SerializedObject(frame);
            so.FindProperty("theme").objectReferenceValue = theme;
            so.FindProperty("role").enumValueIndex = (int)NorscopeThemedFrame.FrameRole.ResizeHandle;
            so.FindProperty("background").objectReferenceValue = bg;
            so.FindProperty("root").objectReferenceValue = rt;
            so.FindProperty("layoutElement").objectReferenceValue = le;
            so.FindProperty("size").vector2Value = new Vector2(16f, 16f);
            so.ApplyModifiedPropertiesWithoutUndo();
            frame.Apply();

            string prefabPath = SavePrefab(root, path);
            Object.DestroyImmediate(root);
            return prefabPath;
        }

        private static string BuildInteractionTestPanel(
            NorscopeTheme theme,
            NorscopeWorldSpaceUiConfig world,
            Sprite rounded,
            TMP_FontAsset font,
            string primaryPath,
            string secondaryPath,
            string iconPath,
            string closePath,
            string badgePath)
        {
            string path = PanelsDir + "/InteractionTestPanel.prefab";
            GameObject root = new GameObject("InteractionTestPanel", typeof(RectTransform));
            RectTransform rt = root.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(550f, 380f);

            // Background
            GameObject bgGo = new GameObject("Background", typeof(RectTransform));
            bgGo.transform.SetParent(root.transform, false);
            RectTransform bgRt = bgGo.GetComponent<RectTransform>();
            StretchFull(bgRt);
            Image bg = bgGo.AddComponent<Image>();
            bg.sprite = rounded;
            bg.type = Image.Type.Sliced;
            bg.color = theme.panelBackground;
            bg.raycastTarget = true;
            LayoutElement bgIgnore = bgGo.AddComponent<LayoutElement>();
            bgIgnore.ignoreLayout = true;

            // Border outline (slightly inset sibling with darker color via outline image under)
            GameObject borderGo = new GameObject("Border", typeof(RectTransform));
            borderGo.transform.SetParent(root.transform, false);
            RectTransform borderRt = borderGo.GetComponent<RectTransform>();
            StretchFull(borderRt);
            Image border = borderGo.AddComponent<Image>();
            border.sprite = rounded;
            border.type = Image.Type.Sliced;
            border.color = theme.border;
            border.raycastTarget = false;
            border.enabled = false;
            LayoutElement borderIgnore = borderGo.AddComponent<LayoutElement>();
            borderIgnore.ignoreLayout = true;

            VerticalLayoutGroup rootLayout = root.AddComponent<VerticalLayoutGroup>();
            rootLayout.padding = new RectOffset(0, 0, 0, 0);
            rootLayout.spacing = 0f;
            rootLayout.childControlWidth = true;
            rootLayout.childControlHeight = false;
            rootLayout.childForceExpandWidth = true;
            rootLayout.childForceExpandHeight = false;

            // TitleBar
            GameObject titleBar = new GameObject("TitleBar", typeof(RectTransform));
            titleBar.transform.SetParent(root.transform, false);
            Image titleBg = titleBar.AddComponent<Image>();
            titleBg.sprite = rounded;
            titleBg.type = Image.Type.Sliced;
            titleBg.color = theme.elevatedPanelBackground;
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
            title.text = "Interaction Test";
            title.font = font;
            title.fontSize = theme.headingSize;
            title.color = theme.primaryText;
            title.raycastTarget = false;
            titleGo.AddComponent<LayoutElement>().flexibleWidth = 1f;

            GameObject grab = new GameObject("GrabRegion", typeof(RectTransform));
            grab.transform.SetParent(titleBar.transform, false);
            Image grabImg = grab.AddComponent<Image>();
            grabImg.color = new Color(theme.border.r, theme.border.g, theme.border.b, 0.65f);
            grabImg.raycastTarget = true;
            LayoutElement grabLe = grab.AddComponent<LayoutElement>();
            grabLe.preferredWidth = 72f;
            grabLe.preferredHeight = 8f;

            GameObject close = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(closePath));
            close.name = "CloseButton";
            close.transform.SetParent(titleBar.transform, false);

            // Content
            GameObject content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(root.transform, false);
            VerticalLayoutGroup contentVl = content.AddComponent<VerticalLayoutGroup>();
            contentVl.padding = new RectOffset(16, 16, 12, 12);
            contentVl.spacing = 12f;
            contentVl.childAlignment = TextAnchor.UpperLeft;
            contentVl.childControlWidth = true;
            contentVl.childControlHeight = false;
            contentVl.childForceExpandWidth = true;
            LayoutElement contentLe = content.AddComponent<LayoutElement>();
            contentLe.flexibleHeight = 1f;
            contentLe.preferredHeight = 280f;

            GameObject descGo = new GameObject("Description", typeof(RectTransform));
            descGo.transform.SetParent(content.transform, false);
            TMP_Text desc = descGo.AddComponent<TextMeshProUGUI>();
            desc.text = "Editable Norscope Canvas controls for HL2 far-ray and near poke testing.";
            desc.font = font;
            desc.fontSize = theme.bodySize;
            desc.color = theme.secondaryText;
            desc.raycastTarget = false;
            LayoutElement descLe = descGo.AddComponent<LayoutElement>();
            descLe.preferredHeight = 48f;

            GameObject buttonColumn = new GameObject("ButtonColumn", typeof(RectTransform));
            buttonColumn.transform.SetParent(content.transform, false);
            VerticalLayoutGroup btnVl = buttonColumn.AddComponent<VerticalLayoutGroup>();
            btnVl.spacing = 10f;
            btnVl.childAlignment = TextAnchor.UpperCenter;
            btnVl.childControlWidth = false;
            btnVl.childControlHeight = false;
            btnVl.childForceExpandWidth = false;
            LayoutElement btnColLe = buttonColumn.AddComponent<LayoutElement>();
            btnColLe.preferredHeight = 150f;

            GameObject p = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(primaryPath));
            p.name = "PrimaryButton";
            p.transform.SetParent(buttonColumn.transform, false);

            GameObject s = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(secondaryPath));
            s.name = "SecondaryButton";
            s.transform.SetParent(buttonColumn.transform, false);

            GameObject i = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(iconPath));
            i.name = "IconButton";
            i.transform.SetParent(buttonColumn.transform, false);

            GameObject statusRow = new GameObject("StatusRow", typeof(RectTransform));
            statusRow.transform.SetParent(content.transform, false);
            HorizontalLayoutGroup statusHl = statusRow.AddComponent<HorizontalLayoutGroup>();
            statusHl.spacing = 8f;
            statusHl.childAlignment = TextAnchor.MiddleLeft;
            LayoutElement statusLe = statusRow.AddComponent<LayoutElement>();
            statusLe.preferredHeight = 28f;

            GameObject badge = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(badgePath));
            badge.name = "ReadyBadge";
            badge.transform.SetParent(statusRow.transform, false);

            // ResizeHandles
            GameObject handles = new GameObject("ResizeHandles", typeof(RectTransform));
            handles.transform.SetParent(root.transform, false);
            StretchFull(handles.GetComponent<RectTransform>());
            LayoutElement handlesIgnore = handles.AddComponent<LayoutElement>();
            handlesIgnore.ignoreLayout = true;
            PlaceHandle(handles.transform, "BottomRight", new Vector2(1f, 0f), new Vector2(-10f, 10f), theme);

            NorscopePanelRoot panelRoot = root.AddComponent<NorscopePanelRoot>();
            SerializedObject so = new SerializedObject(panelRoot);
            so.FindProperty("theme").objectReferenceValue = theme;
            so.FindProperty("worldSpaceConfig").objectReferenceValue = world;
            so.FindProperty("root").objectReferenceValue = rt;
            so.FindProperty("background").objectReferenceValue = bg;
            so.FindProperty("border").objectReferenceValue = border;
            so.FindProperty("title").objectReferenceValue = title;
            so.FindProperty("sizeMeters").vector2Value = new Vector2(0.55f, 0.38f);
            so.FindProperty("sizePixels").vector2Value = new Vector2(550f, 380f);
            so.FindProperty("useMetersAsSource").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            panelRoot.Apply();

            // Ensure Background stays behind authored layout children visually:
            bgGo.transform.SetAsFirstSibling();

            string prefabPath = SavePrefab(root, path);
            Object.DestroyImmediate(root);
            return prefabPath;
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
            img.color = new Color(theme.primaryAccent.r, theme.primaryAccent.g, theme.primaryAccent.b, 0.55f);
            img.raycastTarget = true;
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void BuildPreviewScene(string panelPrefabPath, NorscopeWorldSpaceUiConfig world)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject camGo = new GameObject("PreviewCamera");
            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.06f, 0.08f, 1f);
            cam.transform.position = new Vector3(0f, 0f, -1.2f);
            cam.nearClipPlane = 0.05f;
            cam.orthographic = false;

            GameObject lightGo = new GameObject("PreviewLight");
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.8f;
            lightGo.transform.rotation = Quaternion.Euler(40f, -30f, 0f);

            GameObject canvasGo = new GameObject("WorldSpaceCanvas", typeof(RectTransform));
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = cam;
            canvasGo.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;
            canvasGo.AddComponent<GraphicRaycaster>();
            // TrackedDeviceGraphicRaycaster is optional in preview; include if type available.
            var trackedType = System.Type.GetType("UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster, Unity.XR.Interaction.Toolkit");
            if (trackedType != null)
            {
                canvasGo.AddComponent(trackedType);
            }

            RectTransform canvasRt = canvasGo.GetComponent<RectTransform>();
            canvasRt.sizeDelta = new Vector2(550f, 380f);
            world.ApplyToCanvas(canvasRt);
            canvasGo.transform.position = new Vector3(0f, 0f, 0f);

            GameObject panelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(panelPrefabPath);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(panelPrefab);
            instance.name = "InteractionTestPanel";
            instance.transform.SetParent(canvasGo.transform, false);
            RectTransform panelRt = instance.GetComponent<RectTransform>();
            panelRt.anchoredPosition = Vector2.zero;

            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            var inputModuleType = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (inputModuleType != null)
            {
                es.AddComponent(inputModuleType);
            }
            else
            {
                es.AddComponent<StandaloneInputModule>();
            }

            Directory.CreateDirectory("Assets/Norscope/Scenes");
            EditorSceneManager.SaveScene(scene, PreviewScenePath);

            // Ensure preview scene is not in build settings.
            var scenes = EditorBuildSettings.scenes;
            bool changed = false;
            var list = new System.Collections.Generic.List<EditorBuildSettingsScene>();
            foreach (var s in scenes)
            {
                if (s.path == PreviewScenePath)
                {
                    changed = true;
                    continue;
                }

                list.Add(s);
            }

            if (changed)
            {
                EditorBuildSettings.scenes = list.ToArray();
            }
        }

        private static string SavePrefab(GameObject root, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(root, path);
            return path;
        }
    }
}
