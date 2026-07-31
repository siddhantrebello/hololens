using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Norscope.Navigation;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Norscope.Editor
{
    /// <summary>
    /// Home-Launcher-only implementation of home launcher.zip.
    /// It preserves the existing root Button, navigation, follow component and
    /// authored VisualRoot orientation. Only presentation children are rebuilt.
    /// </summary>
    public static class PhaseL1HomeLauncherFigmaConfigurator
    {
        private const string PrefabPath =
            "Assets/Norscope/UI/Panels/HomeLauncher.prefab";
        private const string RoundedPath =
            "Assets/Norscope/UI/DesignSystem/Sprites/UIRounded_8.png";
        private const string LogoPath =
            "Assets/WhatsApp Image 2026-07-19 at 13.42.15-transparent.png";
        private const string OutfitPath =
            "Assets/Norscope/UI/Fonts/Outfit SDF.asset";
        private const string BackupKey = "Norscope.HomeLauncherFigma.Backup";

        private static readonly Color Idle = Hex("18253C");
        private static readonly Color Accent = Hex("1EC8F5");
        private static readonly Color Primary = Hex("F3F7FB");
        private static readonly Color Success = Hex("39D98A");

        [MenuItem("Norscope/HoloLens/Home Launcher Figma/1) Capture Launcher Baseline")]
        public static void CaptureMenu()
        {
            if (!File.Exists(PrefabPath))
            {
                Debug.LogError("[Norscope Launcher Figma] HomeLauncher.prefab missing.");
                return;
            }

            string folder =
                "Assets/Norscope/UI/Panels/_Backups/HomeLauncherFigma_" +
                DateTime.Now.ToString("yyyyMMdd_HHmmss");
            Directory.CreateDirectory(folder);
            FileUtil.CopyFileOrDirectory(PrefabPath, folder + "/HomeLauncher.prefab");
            File.WriteAllText(folder + "/manifest.txt",
                "Captured=" + DateTime.Now.ToString("O") + "\n" +
                "Source=" + PrefabPath + "\n");
            EditorPrefs.SetString(BackupKey, folder);
            AssetDatabase.Refresh();
            Debug.Log("[Norscope Launcher Figma] Baseline READY\n" + folder);
        }

        [MenuItem("Norscope/HoloLens/Home Launcher Figma/2) Dry Run")]
        public static void DryRunMenu()
        {
            bool ready = DryRun(out string report);
            Debug.Log((ready
                ? "[Norscope Launcher Figma] Dry run READY\n"
                : "[Norscope Launcher Figma] Dry run BLOCKED\n") + report);
        }

        [MenuItem("Norscope/HoloLens/Home Launcher Figma/3) Apply")]
        public static void ApplyMenu()
        {
            if (!DryRun(out string report))
            {
                Debug.LogError("[Norscope Launcher Figma] Apply BLOCKED\n" + report);
                return;
            }

            if (!EditorUtility.DisplayDialog("Apply Home Launcher Figma",
                    "Apply home launcher.zip to HomeLauncher.prefab only?\n\n" +
                    "The root Button, Home navigation, head-follow settings and " +
                    "VisualRoot orientation will be preserved.",
                    "Apply", "Cancel"))
                return;

            ApplyPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Norscope Launcher Figma] Apply COMPLETE");
        }

        [MenuItem("Norscope/HoloLens/Home Launcher Figma/4) Validate")]
        public static void ValidateMenu()
        {
            bool ok = Validate(out string report);
            Debug.Log((ok
                ? "[Norscope Launcher Figma] Validation PASSED\n"
                : "[Norscope Launcher Figma] Validation FAILED\n") + report);
        }

        [MenuItem("Norscope/HoloLens/Home Launcher Figma/5) Restore")]
        public static void RestoreMenu()
        {
            string folder = EditorPrefs.GetString(BackupKey, string.Empty);
            string backup = folder + "/HomeLauncher.prefab";
            if (string.IsNullOrEmpty(folder) || !File.Exists(backup))
            {
                Debug.LogError("[Norscope Launcher Figma] No captured backup.");
                return;
            }

            if (!EditorUtility.DisplayDialog("Restore Home Launcher",
                    "Restore only the captured HomeLauncher.prefab?",
                    "Restore", "Cancel"))
                return;

            File.Copy(backup, PrefabPath, true);
            AssetDatabase.Refresh();
            Debug.Log("[Norscope Launcher Figma] Restore COMPLETE");
        }

        private static bool DryRun(out string report)
        {
            var blockers = new List<string>();
            var lines = new StringBuilder();
            string backup = EditorPrefs.GetString(BackupKey, string.Empty);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Sprite rounded = AssetDatabase.LoadAssetAtPath<Sprite>(RoundedPath);
            Sprite logo = AssetDatabase.LoadAssetAtPath<Sprite>(LogoPath);
            TMP_FontAsset outfit =
                AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OutfitPath);

            if (prefab == null) blockers.Add("HomeLauncher.prefab is missing.");
            if (string.IsNullOrEmpty(backup) || !File.Exists(backup + "/HomeLauncher.prefab"))
                blockers.Add("Capture a fresh launcher baseline first.");
            if (rounded == null) blockers.Add("UIRounded_8 sprite is missing.");
            if (logo == null) blockers.Add("Approved transparent Norscope logo is missing.");
            if (outfit == null || outfit.material == null)
                blockers.Add("Outfit TMP font is not renderable.");

            if (prefab != null)
            {
                if (prefab.GetComponent<Button>() == null)
                    blockers.Add("Root Button is missing.");
                if (prefab.GetComponent<HomeLauncher>() == null)
                    blockers.Add("HomeLauncher component is missing.");
                Transform visual = prefab.transform.Find("VisualRoot");
                if (visual == null) blockers.Add("VisualRoot is missing.");
                if (prefab.GetComponent<HomeLauncherFollow>() != null)
                    lines.AppendLine("INFO  HomeLauncherFollow is on the prefab and will remain unchanged.");
                if (prefab.transform.localScale.x <= 0f ||
                    prefab.transform.localScale.y <= 0f ||
                    prefab.transform.localScale.z <= 0f)
                    blockers.Add("Interactive root scale must remain positive.");
            }

            lines.AppendLine("SOURCE  home launcher.zip / src/app/App.tsx");
            lines.AppendLine("TARGET  HomeLauncher.prefab only");
            lines.AppendLine("SIZE    88 x 88 authored launcher surface");
            lines.AppendLine("ADD     depth, glow, focus ring, sheen, ready dot and floating Home label");
            lines.AppendLine("MOTION  hover 1.034 / focus 1.042 / press 0.952; ease-out ~180 ms");
            lines.AppendLine("KEEP    root Button, target, click, audio, Home navigation and VisualRoot rotation");
            lines.AppendLine("KEEP    head-follow distance, offset, smoothing and world-space scale");
            foreach (string blocker in blockers) lines.AppendLine("BLOCKER " + blocker);
            lines.AppendLine(blockers.Count == 0
                ? "READY — Apply may proceed."
                : "BLOCKED — no prefab should be modified.");
            report = lines.ToString();
            return blockers.Count == 0;
        }

        private static void ApplyPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                Button button = root.GetComponent<Button>();
                HomeLauncher launcher = root.GetComponent<HomeLauncher>();
                RectTransform rootRt = root.GetComponent<RectTransform>();
                RectTransform visual =
                    root.transform.Find("VisualRoot") as RectTransform;
                for (int i = visual.childCount - 1; i >= 0; i--)
                    UnityEngine.Object.DestroyImmediate(visual.GetChild(i).gameObject);

                rootRt.sizeDelta = new Vector2(88f, 88f);
                // Keep the approved 45-degree spatial tilt, but do not retain the
                // obsolete Z=180 flip: it inverts the authored logo and label.
                visual.localRotation = Quaternion.Euler(45f, 0f, 0f);
                visual.localScale = Vector3.one;
                visual.anchoredPosition = Vector2.zero;
                visual.sizeDelta = Vector2.zero;

                Sprite rounded = AssetDatabase.LoadAssetAtPath<Sprite>(RoundedPath);
                Sprite logo = AssetDatabase.LoadAssetAtPath<Sprite>(LogoPath);
                TMP_FontAsset outfit =
                    AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OutfitPath);

                Image glow = Layer(visual, "OuterGlow", rounded,
                    new Color(Accent.r, Accent.g, Accent.b, 0f),
                    new Vector2(94f, 94f), Vector2.zero);
                Image focus = Layer(visual, "FocusOutline", rounded,
                    new Color(Accent.r, Accent.g, Accent.b, 0f),
                    new Vector2(90f, 90f), Vector2.zero);
                Image outline = Layer(visual, "Outline", rounded,
                    new Color(Accent.r, Accent.g, Accent.b, 0.13f),
                    new Vector2(88f, 88f), Vector2.zero);
                Image background = Layer(visual, "Background", rounded, Idle,
                    new Vector2(86f, 86f), Vector2.zero);
                background.raycastTarget = true;

                Layer(visual, "InnerDepth", rounded,
                    new Color(0.035f, 0.065f, 0.11f, 0.24f),
                    new Vector2(78f, 78f), new Vector2(0f, -2f));
                Layer(visual, "TopSheen", rounded,
                    new Color(1f, 1f, 1f, 0.055f),
                    new Vector2(64f, 2f), new Vector2(0f, 38f));

                Image icon = Layer(visual, "Icon", logo,
                    new Color(1f, 1f, 1f, 0.85f),
                    new Vector2(48f, 48f), new Vector2(0f, 1f));
                icon.preserveAspect = true;
                icon.type = Image.Type.Simple;

                Layer(visual, "ReadyDotRing", rounded,
                    new Color(0.02f, 0.04f, 0.06f, 0.9f),
                    new Vector2(13f, 13f), new Vector2(32f, -32f));
                Image dot = Layer(visual, "ReadyDot", rounded, Success,
                    new Vector2(9f, 9f), new Vector2(32f, -32f));

                CanvasGroup label = BuildLabel(visual, rounded, outfit);

                button.targetGraphic = background;
                button.transition = Selectable.Transition.None;

                UiButtonPressScale press = root.GetComponent<UiButtonPressScale>();
                if (press != null) press.enabled = false;

                SerializedObject so = new SerializedObject(launcher);
                Set(so, "button", button);
                Set(so, "background", background);
                Set(so, "outline", outline);
                Set(so, "icon", icon);
                Set(so, "visualRoot", visual);
                Set(so, "focusOutline", focus);
                Set(so, "outerGlow", glow);
                Set(so, "readyDot", dot);
                Set(so, "hoverLabel", label);
                so.FindProperty("useAuthoredPresentation").boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static CanvasGroup BuildLabel(Transform parent, Sprite rounded,
            TMP_FontAsset outfit)
        {
            RectTransform root = Node(parent, "HoverLabel",
                new Vector2(62f, 24f), new Vector2(0f, 60f));
            CanvasGroup group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            Image bg = root.gameObject.AddComponent<Image>();
            bg.sprite = rounded;
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.055f, 0.086f, 0.133f, 0.96f);
            bg.raycastTarget = false;

            RectTransform textRt = Node(root, "Label", new Vector2(54f, 20f),
                Vector2.zero);
            TMP_Text text = textRt.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = "Home";
            text.font = outfit;
            text.fontSize = 11f;
            text.fontStyle = FontStyles.Normal;
            text.color = Primary;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;

            Image arrow = Layer(root, "Pointer", rounded,
                new Color(0.055f, 0.086f, 0.133f, 0.96f),
                new Vector2(7f, 7f), new Vector2(0f, -13f));
            arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            return group;
        }

        private static Image Layer(Transform parent, string name, Sprite sprite,
            Color color, Vector2 size, Vector2 position)
        {
            RectTransform rt = Node(parent, name, size, position);
            Image image = rt.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static RectTransform Node(Transform parent, string name,
            Vector2 size, Vector2 position)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            rt.localScale = Vector3.one;
            return rt;
        }

        private static void Set(SerializedObject so, string property,
            UnityEngine.Object value)
        {
            SerializedProperty p = so.FindProperty(property);
            if (p != null) p.objectReferenceValue = value;
        }

        private static bool Validate(out string report)
        {
            var lines = new StringBuilder();
            bool ok = true;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            void Check(bool condition, string message)
            {
                lines.AppendLine((condition ? "PASS  " : "FAIL  ") + message);
                ok &= condition;
            }

            Check(prefab != null, "HomeLauncher.prefab exists");
            if (prefab != null)
            {
                RectTransform rt = prefab.GetComponent<RectTransform>();
                Button button = prefab.GetComponent<Button>();
                HomeLauncher launcher = prefab.GetComponent<HomeLauncher>();
                Transform visual = prefab.transform.Find("VisualRoot");
                Check(rt != null && rt.sizeDelta == new Vector2(88f, 88f),
                    "Launcher authored size is 88 x 88");
                Check(button != null, "Original root Button remains");
                Check(launcher != null, "HomeLauncher navigation component remains");
                Check(prefab.transform.localScale.x > 0f &&
                      prefab.transform.localScale.y > 0f &&
                      prefab.transform.localScale.z > 0f,
                    "Interactive root uses positive scale");
                Check(visual != null, "VisualRoot remains");
                Check(Find(visual, "Background") != null &&
                      Find(visual, "Icon") != null &&
                      Find(visual, "ReadyDot") != null &&
                      Find(visual, "HoverLabel") != null,
                    "Approved authored visual layers exist");
                Check(button != null && button.targetGraphic != null &&
                      button.targetGraphic.gameObject.name == "Background",
                    "Existing Button has one full-surface target graphic");
                Check(AllDecorationIgnoresRaycasts(visual, button.targetGraphic),
                    "All decorative graphics ignore raycasts");
            }
            report = lines.ToString();
            return ok;
        }

        private static Transform Find(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = Find(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        private static bool AllDecorationIgnoresRaycasts(Transform root,
            Graphic target)
        {
            if (root == null) return false;
            foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true))
                if (graphic != target && graphic.raycastTarget) return false;
            return true;
        }

        private static Color Hex(string value)
        {
            ColorUtility.TryParseHtmlString("#" + value, out Color color);
            return color;
        }
    }
}
