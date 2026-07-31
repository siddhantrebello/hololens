using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Norscope.TaskExecution;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Norscope.Editor
{
    /// <summary>
    /// Applies cancel-active-task Figma Dialog (App.tsx) to CancelConfirmationView.
    /// Retains ContinueTaskButton / ConfirmCancelButton so runtime listeners stay intact.
    /// </summary>
    public static class PhaseT4CancelTaskFigmaConfigurator
    {
        private const string Menu = "Norscope/HoloLens/Cancel Task Figma/";
        private const string PanelPath =
            "Assets/Norscope/UI/Panels/ActiveTaskPanel.prefab";
        private const string RoundedPath =
            "Assets/Norscope/UI/DesignSystem/Sprites/UIRounded_8.png";
        private const string OutfitPath =
            "Assets/Norscope/UI/Fonts/Outfit SDF.asset";
        private const string InterPath =
            "Assets/Norscope/UI/Fonts/Inter SDF.asset";
        private const string LogoPath =
            "Assets/WhatsApp Image 2026-07-19 at 13.42.15-transparent.png";
        private const string DangerIconPath =
            "Assets/Norscope/UI/Icons/danger.png";
        private const string ClosePath =
            "Assets/Norscope/UI/DesignSystem/Icons/close.svg";
        private const string PlayPath =
            "Assets/Norscope/UI/Icons/Lucide/Raster/play.png";
        private const string BackupRoot = "Assets/Norscope/UI/Panels/_Backups";
        private const string BackupKey = "Norscope.CancelTaskFigma.Backup";
        private const string FigmaApp =
            "Temp/cancel_active_task_figma/src/app/App.tsx";
        private const string ApplyFlag = "Temp/NorscopeCancelDialogApply.flag";
        private const string ValidateFlag = "Temp/NorscopeCancelDialogValidate.flag";
        private const string CaptureFlag = "Temp/NorscopeCancelDialogCapture.flag";

        // App.tsx tokens
        private static readonly Color Surface = Hex("1A1D22");
        private static readonly Color Border = new Color(1f, 1f, 1f, 0.08f);
        private static readonly Color BorderHov = new Color(1f, 1f, 1f, 0.14f);
        private static readonly Color Accent = Hex("1EC8F5");
        private static readonly Color Danger = Hex("C0403B");
        private static readonly Color DangerDim = new Color(192f / 255f, 64f / 255f, 59f / 255f, 0.10f);
        private static readonly Color DangerBorder = new Color(192f / 255f, 64f / 255f, 59f / 255f, 0.22f);
        private static readonly Color TextPri = Hex("F0F3F7");
        private static readonly Color TextSec = Hex("C7CDD6");
        private static readonly Color ContinueSurface = new Color(1f, 1f, 1f, 0.04f);

        private static readonly Vector2 CardSize = new Vector2(700f, 340f);

        [InitializeOnLoadMethod]
        private static void PollFlags()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
        }

        private static void OnEditorUpdate()
        {
            if (File.Exists(CaptureFlag))
            {
                File.Delete(CaptureFlag);
                EditorApplication.delayCall += CaptureMenu;
                return;
            }

            if (File.Exists(ValidateFlag))
            {
                File.Delete(ValidateFlag);
                EditorApplication.delayCall += ValidateMenu;
                return;
            }

            if (File.Exists(ApplyFlag))
            {
                File.Delete(ApplyFlag);
                EditorApplication.delayCall += ApplyQuiet;
            }
        }

        [MenuItem(Menu + "1) Capture Cancel Baseline")]
        public static void CaptureMenu()
        {
            if (!File.Exists(PanelPath))
            {
                Debug.LogError("[Norscope Cancel Figma] ActiveTaskPanel.prefab missing.");
                return;
            }

            string folder = BackupRoot + "/CancelActiveTaskDialog_" +
                            DateTime.Now.ToString("yyyyMMdd_HHmmss");
            Directory.CreateDirectory(folder);
            FileUtil.CopyFileOrDirectory(PanelPath, folder + "/ActiveTaskPanel.prefab");
            File.WriteAllText(
                folder + "/manifest.txt",
                "Captured=" + DateTime.Now.ToString("O") + "\n" +
                "Source=" + PanelPath + "\n" +
                "Figma=" + FigmaApp + "\n");
            EditorPrefs.SetString(BackupKey, folder);
            AssetDatabase.Refresh();
            Debug.Log("[Norscope Cancel Figma] Baseline READY\n" + folder);
        }

        [MenuItem(Menu + "2) Dry Run")]
        public static void DryRunMenu()
        {
            bool ready = DryRun(out string report);
            Directory.CreateDirectory("Temp");
            File.WriteAllText("Temp/NorscopeCancelDialogDryRun.txt", report, Encoding.UTF8);
            Debug.Log((ready
                ? "[Norscope Cancel Figma] Dry run READY\n"
                : "[Norscope Cancel Figma] Dry run BLOCKED\n") + report);
        }

        [MenuItem(Menu + "3) Apply")]
        public static void ApplyMenu()
        {
            if (!DryRun(out string report))
            {
                Debug.LogError("[Norscope Cancel Figma] Apply BLOCKED\n" + report);
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "Apply Cancel Task Figma",
                    "Apply cancel-active-task App.tsx Dialog to CancelConfirmationView?\n\n" +
                    "Continue / Confirm Cancel buttons and cancellation logic are retained.",
                    "Apply",
                    "Cancel"))
                return;

            ApplyQuiet();
        }

        private static void ApplyQuiet()
        {
            EnsureBaseline();
            if (!DryRun(out string report) &&
                report.Contains("ActiveTaskPanel.prefab is missing."))
            {
                Debug.LogError("[Norscope Cancel Figma] Apply BLOCKED\n" + report);
                return;
            }

            ApplyPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateMenu();
            Debug.Log("[Norscope Cancel Figma] Apply COMPLETE");
        }

        [MenuItem(Menu + "4) Validate")]
        public static void ValidateMenu()
        {
            bool ok = Validate(out string report);
            Directory.CreateDirectory("Temp");
            File.WriteAllText("Temp/NorscopeCancelDialogValidate.txt", report, Encoding.UTF8);
            if (ok)
                Debug.Log("[Norscope Cancel Figma] Validation PASSED\n" + report);
            else
                Debug.LogError("[Norscope Cancel Figma] Validation FAILED\n" + report);
        }

        [MenuItem(Menu + "5) Restore")]
        public static void RestoreMenu()
        {
            string folder = LatestBackup() ?? EditorPrefs.GetString(BackupKey, string.Empty);
            string backup = folder + "/ActiveTaskPanel.prefab";
            if (string.IsNullOrEmpty(folder) || !File.Exists(backup))
            {
                Debug.LogError("[Norscope Cancel Figma] No captured backup.");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "Restore Cancel Task",
                    "Restore ActiveTaskPanel.prefab from:\n" + folder,
                    "Restore",
                    "Cancel"))
                return;

            FileUtil.ReplaceFile(backup, PanelPath);
            AssetDatabase.ImportAsset(PanelPath, ImportAssetOptions.ForceUpdate);
            Debug.Log("[Norscope Cancel Figma] Restore COMPLETE");
        }

        private static void EnsureBaseline()
        {
            if (LatestBackup() != null) return;
            CaptureMenu();
        }

        private static string LatestBackup()
        {
            if (!Directory.Exists(BackupRoot)) return null;
            string[] dirs = Directory.GetDirectories(BackupRoot, "CancelActiveTaskDialog_*");
            if (dirs.Length == 0)
                dirs = Directory.GetDirectories(BackupRoot, "CancelTaskFigma_*");
            Array.Sort(dirs);
            return dirs.Length > 0 ? dirs[dirs.Length - 1] : null;
        }

        private static bool DryRun(out string report)
        {
            var blockers = new List<string>();
            var lines = new StringBuilder();
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath);
            if (prefab == null) blockers.Add("ActiveTaskPanel.prefab is missing.");
            if (LatestBackup() == null)
                blockers.Add("Capture a fresh cancel baseline first.");
            if (AssetDatabase.LoadAssetAtPath<Sprite>(RoundedPath) == null)
                blockers.Add("Rounded UI sprite is missing.");
            if (!Renderable(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OutfitPath)) ||
                !Renderable(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(InterPath)))
                blockers.Add("Outfit/Inter TMP assets are not renderable.");
            if (!File.Exists(FigmaApp))
                blockers.Add("Temp/cancel_active_task_figma/src/app/App.tsx missing.");
            if (prefab != null)
            {
                Require(prefab.transform, blockers, "CancelConfirmationView",
                    "ConfirmationCard", "ContinueTaskButton", "ConfirmCancelButton");
                if (prefab.GetComponent<ActiveTaskPanel>() == null)
                    blockers.Add("ActiveTaskPanel component is missing.");
            }

            lines.AppendLine("SOURCE  " + FigmaApp);
            lines.AppendLine("TARGET  CancelConfirmationView in ActiveTaskPanel.prefab");
            lines.AppendLine("LAYOUT  " + CardSize.x + " x " + CardSize.y +
                             " centered dialog (App.tsx Dialog)");
            lines.AppendLine("KEEP    ContinueTaskButton + ConfirmCancelButton + cancel logic");
            lines.AppendLine("DROP    Task summary / progress fill / amber warning card / footer");
            lines.AppendLine("STYLE   Muted Continue + danger Cancel (#C0403B)");
            lines.AppendLine("OMIT    Don't ask again; full Active Task redesign");
            foreach (string blocker in blockers) lines.AppendLine("BLOCKER " + blocker);
            lines.AppendLine(blockers.Count == 0
                ? "READY — Apply may proceed."
                : "BLOCKED — no prefab should be modified.");
            report = lines.ToString();
            return blockers.Count == 0;
        }

        private static void ApplyPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PanelPath);
            try
            {
                ActiveTaskPanel panel = root.GetComponent<ActiveTaskPanel>();
                Transform view = Find(root.transform, "CancelConfirmationView");
                Transform card = Find(view, "ConfirmationCard");
                Button continueButton =
                    Find(view, "ContinueTaskButton").GetComponent<Button>();
                Button cancelButton =
                    Find(view, "ConfirmCancelButton").GetComponent<Button>();

                int continueListeners =
                    continueButton.onClick.GetPersistentEventCount();
                int cancelListeners =
                    cancelButton.onClick.GetPersistentEventCount();

                for (int i = card.childCount - 1; i >= 0; i--)
                {
                    Transform child = card.GetChild(i);
                    if (child == continueButton.transform ||
                        child == cancelButton.transform)
                        continue;
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                }

                Sprite rounded = AssetDatabase.LoadAssetAtPath<Sprite>(RoundedPath);
                Sprite logo = AssetDatabase.LoadAssetAtPath<Sprite>(LogoPath);
                Sprite warning = AssetDatabase.LoadAssetAtPath<Sprite>(DangerIconPath);
                Sprite closeIcon = AssetDatabase.LoadAssetAtPath<Sprite>(ClosePath);
                Sprite play = AssetDatabase.LoadAssetAtPath<Sprite>(PlayPath);
                TMP_FontAsset outfit =
                    AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OutfitPath);
                TMP_FontAsset inter =
                    AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(InterPath);

                RectTransform viewRt = view.GetComponent<RectTransform>();
                Stretch(viewRt);
                Image scrim = view.GetComponent<Image>();
                Style(scrim, null, new Color(0.039f, 0.051f, 0.071f, 0.88f), true);

                RectTransform cardRt = card.GetComponent<RectTransform>();
                Center(cardRt, CardSize);
                Image cardImage = card.GetComponent<Image>();
                Style(cardImage, rounded, Surface, false);

                // Top-edge highlight
                ImageNode(card, "TopSheen", null,
                    new Color(1f, 1f, 1f, 0.09f),
                    new Vector2(490f, 1f), new Vector2(105f, 0f));

                Image logoImage = ImageNode(card, "NorscopeLogo", logo,
                    new Color(1f, 1f, 1f, 0.55f),
                    new Vector2(104f, 16f), new Vector2(36f, 36f));
                logoImage.preserveAspect = true;
                logoImage.type = Image.Type.Simple;

                // Warning icon chip
                Image iconChip = ImageNode(card, "WarningIconChip", rounded,
                    DangerDim, new Vector2(36f, 36f), new Vector2(36f, 80f));
                Image warnIcon = ImageNode(iconChip.transform, "Icon", warning, Danger,
                    new Vector2(17f, 17f), new Vector2(9.5f, 9.5f));
                warnIcon.type = Image.Type.Simple;
                warnIcon.preserveAspect = true;

                Text(card, "Title", "Cancel Active Task?", outfit, 28f,
                    TextPri, new Vector2(86f, 78f), new Vector2(540f, 42f),
                    TextAlignmentOptions.TopLeft, FontStyles.Bold);

                Text(card, "Description",
                    "Your current task progress will be discarded.\n" +
                    "This action cannot be undone.",
                    inter, 15f, TextSec,
                    new Vector2(86f, 128f), new Vector2(540f, 64f),
                    TextAlignmentOptions.TopLeft, FontStyles.Normal);

                ImageNode(card, "Divider", null, Border,
                    new Vector2(628f, 1f), new Vector2(36f, 212f));

                StyleButton(
                    continueButton,
                    "Continue Task",
                    new Vector2(36f, 244f),
                    new Vector2(306f, 56f),
                    ContinueSurface,
                    TextPri,
                    outfit,
                    16f,
                    rounded,
                    play,
                    TextSec);
                StyleButton(
                    cancelButton,
                    "Cancel Task",
                    new Vector2(358f, 244f),
                    new Vector2(306f, 56f),
                    Danger,
                    Color.white,
                    outfit,
                    16f,
                    rounded,
                    closeIcon,
                    Color.white);

                // Clear unused progress summary bindings (removed from Figma Dialog).
                SerializedObject so = new SerializedObject(panel);
                Set(so, "cancelTaskTitleText", null);
                Set(so, "cancelProgressText", null);
                Set(so, "cancelDurationText", null);
                Set(so, "cancelProgressDetailText", null);
                Set(so, "cancelProgressFill", null);
                so.ApplyModifiedPropertiesWithoutUndo();

                if (continueButton.onClick.GetPersistentEventCount() != continueListeners ||
                    cancelButton.onClick.GetPersistentEventCount() != cancelListeners)
                    throw new InvalidOperationException(
                        "A functional persistent listener changed; Apply aborted.");

                view.gameObject.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root, PanelPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void StyleButton(
            Button button,
            string label,
            Vector2 position,
            Vector2 size,
            Color surface,
            Color textColor,
            TMP_FontAsset font,
            float fontSize,
            Sprite rounded,
            Sprite icon,
            Color iconColor)
        {
            RectTransform rt = button.GetComponent<RectTransform>();
            Place(rt, position, size);
            Image image = button.targetGraphic as Image ??
                          button.GetComponent<Image>();
            Style(image, rounded, surface, true);
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.05f, 1.05f, 1.05f, 1f);
            colors.pressedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.7f, 0.7f, 0.7f, 0.45f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            Transform oldBorder = button.transform.Find("FigmaBorder");
            if (oldBorder != null)
                UnityEngine.Object.DestroyImmediate(oldBorder.gameObject);

            TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
            if (text == null)
            {
                RectTransform textRt = Node(button.transform, "Label",
                    new Vector2(size.x - 48f, size.y), new Vector2(40f, 0f));
                text = textRt.gameObject.AddComponent<TextMeshProUGUI>();
            }

            text.text = label;
            text.font = font;
            text.fontSize = fontSize;
            text.fontStyle = FontStyles.Normal;
            text.color = textColor;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;

            Transform oldIcon = button.transform.Find("FigmaIcon");
            if (oldIcon != null)
                UnityEngine.Object.DestroyImmediate(oldIcon.gameObject);
            if (icon != null)
            {
                Image iconImage = ImageNode(
                    button.transform, "FigmaIcon", icon, iconColor,
                    new Vector2(15f, 15f),
                    new Vector2(size.x * 0.5f - 78f, size.y * 0.5f - 7.5f));
                iconImage.type = Image.Type.Simple;
                iconImage.preserveAspect = true;
            }
        }

        private static Image ImageNode(
            Transform parent, string name, Sprite sprite, Color color,
            Vector2 size, Vector2 position)
        {
            RectTransform rt = Node(parent, name, size, position);
            Image image = rt.gameObject.AddComponent<Image>();
            Style(image, sprite, color, false);
            return image;
        }

        private static TMP_Text Text(
            Transform parent, string name, string value, TMP_FontAsset font,
            float size, Color color, Vector2 position, Vector2 dimensions,
            TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft,
            FontStyles style = FontStyles.Normal)
        {
            RectTransform rt = Node(parent, name, dimensions, position);
            TMP_Text text = rt.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = alignment;
            text.enableWordWrapping = true;
            text.raycastTarget = false;
            text.margin = Vector4.zero;
            return text;
        }

        private static RectTransform Node(
            Transform parent, string name, Vector2 size, Vector2 position)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            Place(rt, position, size);
            return rt;
        }

        private static void Place(RectTransform rt, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(position.x, -position.y);
            rt.sizeDelta = size;
            rt.localScale = Vector3.one;
        }

        private static void Center(RectTransform rt, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = size;
            rt.localScale = Vector3.one;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
        }

        private static void Style(Image image, Sprite sprite, Color color, bool raycast)
        {
            if (image == null) return;
            image.sprite = sprite;
            image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            image.color = color;
            image.raycastTarget = raycast;
        }

        private static void Set(SerializedObject so, string property, UnityEngine.Object value)
        {
            SerializedProperty p = so.FindProperty(property);
            if (p != null) p.objectReferenceValue = value;
        }

        private static bool Validate(out string report)
        {
            var lines = new StringBuilder();
            bool ok = true;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath);
            void Check(bool condition, string label)
            {
                lines.AppendLine((condition ? "PASS  " : "FAIL  ") + label);
                ok &= condition;
            }

            Check(prefab != null, "ActiveTaskPanel.prefab exists");
            if (prefab != null)
            {
                Transform view = Find(prefab.transform, "CancelConfirmationView");
                Transform card = Find(prefab.transform, "ConfirmationCard");
                Button keep = Find(prefab.transform, "ContinueTaskButton")
                    ?.GetComponent<Button>();
                Button cancel = Find(prefab.transform, "ConfirmCancelButton")
                    ?.GetComponent<Button>();
                Check(view != null && !view.gameObject.activeSelf,
                    "Cancel overlay exists and starts hidden");
                Check(card != null &&
                      card.GetComponent<RectTransform>().sizeDelta == CardSize,
                    "Dialog card is " + CardSize.x + " x " + CardSize.y);
                Check(keep != null && cancel != null,
                    "Continue / Confirm Cancel buttons remain");
                Check(Find(card, "Title") != null &&
                      Find(card, "Description") != null &&
                      Find(card, "WarningIconChip") != null &&
                      Find(card, "Divider") != null &&
                      Find(card, "NorscopeLogo") != null,
                    "App.tsx dialog hierarchy exists");
                Check(Find(card, "TaskSummary") == null &&
                      Find(card, "WarningCard") == null &&
                      Find(card, "Footer") == null,
                    "Old summary / warning card / footer removed");
                ActiveTaskPanel panel = prefab.GetComponent<ActiveTaskPanel>();
                SerializedObject so = new SerializedObject(panel);
                Check(so.FindProperty("cancelTaskTitleText").objectReferenceValue == null &&
                      so.FindProperty("cancelProgressFill").objectReferenceValue == null,
                    "Unused progress summary refs cleared");
                Check(AllDecorationsIgnoreRaycasts(card, keep, cancel),
                    "Decorative graphics ignore raycasts");
                Check(cancel != null &&
                      (cancel.targetGraphic as Image) != null &&
                      ColorsClose((cancel.targetGraphic as Image).color, Danger),
                    "Cancel button uses danger surface");
            }

            report = lines.ToString();
            return ok;
        }

        private static bool ColorsClose(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < 0.05f &&
                   Mathf.Abs(a.g - b.g) < 0.05f &&
                   Mathf.Abs(a.b - b.b) < 0.05f;
        }

        private static bool AllDecorationsIgnoreRaycasts(
            Transform root, Button keep, Button cancel)
        {
            foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true))
            {
                if (keep != null && graphic == keep.targetGraphic) continue;
                if (cancel != null && graphic == cancel.targetGraphic) continue;
                if (graphic.raycastTarget) return false;
            }

            return true;
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

        private static void Require(Transform root, List<string> blockers, params string[] names)
        {
            foreach (string name in names)
                if (Find(root, name) == null) blockers.Add(name + " missing.");
        }

        private static bool Renderable(TMP_FontAsset font)
        {
            return font != null && font.material != null && font.atlasTexture != null;
        }

        private static Color Hex(string value)
        {
            ColorUtility.TryParseHtmlString("#" + value, out Color color);
            return color;
        }
    }
}
