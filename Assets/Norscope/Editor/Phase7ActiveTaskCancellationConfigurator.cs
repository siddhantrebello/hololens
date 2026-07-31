using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Norscope.DesignSystem;
using Norscope.TaskExecution;
using Norscope.UI;

namespace Norscope.Editor
{
    /// <summary>
    /// One-time, surgical authoring for Active Task cancellation. Existing objects and
    /// authored RectTransforms are never rewritten. Runtime code only toggles the authored
    /// confirmation view and wires its buttons.
    /// </summary>
    public static class Phase7ActiveTaskCancellationConfigurator
    {
        private const string PanelPath =
            "Assets/Norscope/UI/Panels/ActiveTaskPanel.prefab";
        private const string BackupDirectory =
            "Assets/Norscope/UI/Panels/_Backups";
        private const string RoundedSpritePath =
            "Assets/Norscope/UI/DesignSystem/Sprites/UIRounded_8.png";
        private const string ThemePath =
            "Assets/Norscope/UI/DesignSystem/Themes/NorscopeTheme.asset";
        private const string D1LayerPath =
            "Assets/Norscope/DesignSystem/Prefabs/NorscopeInteractionVisualLayer.prefab";
        private const string D1ProfilePath =
            "Assets/Norscope/DesignSystem/Profiles/NorscopeDesignProfile.asset";

        [MenuItem("Norscope/HoloLens/Phase 7/6) Add Cancel Confirmation (Surgical)")]
        public static void AddCancelConfirmationMenu()
        {
            if (!File.Exists(PanelPath))
            {
                EditorUtility.DisplayDialog(
                    "Active Task prefab missing",
                    PanelPath + " does not exist.",
                    "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "Add Active Task cancellation",
                    "This creates a timestamped backup, then adds only missing Cancel and " +
                    "confirmation-card objects. Existing hierarchy, controls, listeners, " +
                    "positions and styling are preserved.\n\nAll new RectTransforms remain " +
                    "editable in Prefab Mode and are never reset at runtime.",
                    "Back Up and Add",
                    "Cancel"))
            {
                return;
            }

            Backup();
            bool changed = ApplySurgically(out string report);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Norscope Phase7] Active Task cancellation " +
                      (changed ? "added." : "already present; no prefab changes were needed.") +
                      "\n" + report);
        }

        [MenuItem("Norscope/HoloLens/Phase 7/7) Validate Active Task Cancellation")]
        public static void ValidateCancellationMenu()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath);
            bool ok = prefab != null;
            var report = new System.Text.StringBuilder();
            void Check(bool condition, string label)
            {
                report.AppendLine((condition ? "PASS  " : "FAIL  ") + label);
                if (!condition) ok = false;
            }

            Check(prefab != null, "ActiveTaskPanel.prefab exists");
            if (prefab != null)
            {
                Transform confirmation =
                    FindDeep(prefab.transform, "CancelConfirmationView");
                Transform cancel = FindDeep(prefab.transform, "CancelButton");
                Transform keep = FindDeep(prefab.transform, "ContinueTaskButton");
                Transform confirm = FindDeep(prefab.transform, "ConfirmCancelButton");
                Check(cancel != null && cancel.GetComponent<Button>() != null,
                    "Cancel Task button is authored");
                Check(confirmation != null && !confirmation.gameObject.activeSelf,
                    "Confirmation card is authored and initially hidden");
                Check(keep != null && keep.GetComponent<Button>() != null,
                    "Continue Task button is authored");
                Check(confirm != null && confirm.GetComponent<Button>() != null,
                    "Confirm Cancel button is authored");

                ActiveTaskPanel panel = prefab.GetComponent<ActiveTaskPanel>();
                SerializedObject serializedPanel =
                    panel != null ? new SerializedObject(panel) : null;
                Check(serializedPanel != null &&
                      serializedPanel.FindProperty("cancelButton").objectReferenceValue != null &&
                      serializedPanel.FindProperty("cancelConfirmationView").objectReferenceValue != null &&
                      serializedPanel.FindProperty("continueTaskButton").objectReferenceValue != null &&
                      serializedPanel.FindProperty("confirmCancelButton").objectReferenceValue != null,
                    "All cancellation references are assigned");
                Check(AllDecorativeGraphicsIgnoreRaycasts(cancel, keep, confirm),
                    "D1 decorative graphics ignore raycasts");
            }

            string panelSource = File.ReadAllText(
                "Assets/Norscope/Runtime/TaskExecution/ActiveTaskPanel.cs");
            string sessionSource = File.ReadAllText(
                "Assets/Norscope/Runtime/TaskExecution/ActiveTaskSession.cs");
            Check(!panelSource.Contains("GetComponent<RectTransform>") &&
                  !panelSource.Contains("AddComponent<") &&
                  !panelSource.Contains("new GameObject") &&
                  !panelSource.Contains("Instantiate("),
                "Cancellation runtime contains no visual hierarchy or layout mutation");
            Check(sessionSource.Contains("public bool Cancel()"),
                "ActiveTaskSession exposes explicit cancellation");

            Debug.Log((ok
                ? "[Norscope Phase7] Validate Active Task Cancellation PASSED\n"
                : "[Norscope Phase7] Validate Active Task Cancellation FAILED\n") +
                report);
            EditorUtility.DisplayDialog(
                "Validate Active Task Cancellation",
                (ok ? "PASSED\n\n" : "FAILED\n\n") + report,
                "OK");
        }

        private static bool ApplySurgically(out string report)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PanelPath);
            bool changed = false;
            try
            {
                ActiveTaskPanel panel = root.GetComponent<ActiveTaskPanel>();
                Transform taskView = FindDeep(root.transform, "TaskView");
                Transform bottomNavigation = FindDeep(root.transform, "BottomNavigation");
                if (panel == null || taskView == null || bottomNavigation == null)
                {
                    throw new InvalidOperationException(
                        "ActiveTaskPanel, TaskView or BottomNavigation is missing.");
                }

                NorscopeTheme theme =
                    AssetDatabase.LoadAssetAtPath<NorscopeTheme>(ThemePath);
                Sprite rounded =
                    AssetDatabase.LoadAssetAtPath<Sprite>(RoundedSpritePath);
                TMP_FontAsset font = theme != null && theme.fonts != null
                    ? theme.fonts.bodyFont
                    : null;

                Button cancelButton = GetOrCreateButton(
                    bottomNavigation,
                    "CancelButton",
                    "Cancel Task",
                    new Vector2(122f, 42f),
                    InitialCancelPosition(bottomNavigation),
                    theme != null ? theme.elevatedPanelBackground : new Color(0.12f, 0.15f, 0.2f, 1f),
                    theme != null ? theme.primaryText : Color.white,
                    rounded,
                    font,
                    ref changed);

                GameObject confirmation = GetOrCreateConfirmationView(
                    root.transform,
                    theme,
                    rounded,
                    font,
                    ref changed,
                    out Button continueButton,
                    out Button confirmButton);

                SerializedObject serializedPanel = new SerializedObject(panel);
                changed |= Assign(serializedPanel, "cancelButton", cancelButton);
                changed |= Assign(serializedPanel, "cancelConfirmationView", confirmation);
                changed |= Assign(serializedPanel, "continueTaskButton", continueButton);
                changed |= Assign(serializedPanel, "confirmCancelButton", confirmButton);
                serializedPanel.ApplyModifiedPropertiesWithoutUndo();

                changed |= EnsureD1Visual(
                    cancelButton.gameObject,
                    NorscopeVisualRole.SecondaryButton);
                changed |= EnsureD1Visual(
                    continueButton.gameObject,
                    NorscopeVisualRole.SecondaryButton);
                changed |= EnsureD1Visual(
                    confirmButton.gameObject,
                    NorscopeVisualRole.PrimaryButton);

                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, PanelPath);
                }

                report =
                    "CancelButton: " + HierarchyPath(cancelButton.transform, root.transform) +
                    "\nConfirmation card: " +
                    HierarchyPath(confirmation.transform, root.transform) +
                    "\nContinue button: " +
                    HierarchyPath(continueButton.transform, root.transform) +
                    "\nConfirm button: " +
                    HierarchyPath(confirmButton.transform, root.transform);
                return changed;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static GameObject GetOrCreateConfirmationView(
            Transform panelRoot,
            NorscopeTheme theme,
            Sprite rounded,
            TMP_FontAsset font,
            ref bool changed,
            out Button continueButton,
            out Button confirmButton)
        {
            Transform existing = FindDirectChild(panelRoot, "CancelConfirmationView");
            GameObject view;
            if (existing != null)
            {
                view = existing.gameObject;
            }
            else
            {
                view = CreateUiObject("CancelConfirmationView", panelRoot);
                RectTransform viewRect = view.GetComponent<RectTransform>();
                Stretch(viewRect);
                Image scrim = view.AddComponent<Image>();
                scrim.color = new Color(0f, 0f, 0f, 0.62f);
                scrim.raycastTarget = true;

                GameObject card = CreateUiObject("ConfirmationCard", view.transform);
                RectTransform cardRect = card.GetComponent<RectTransform>();
                cardRect.anchorMin = cardRect.anchorMax = new Vector2(0.5f, 0.5f);
                cardRect.pivot = new Vector2(0.5f, 0.5f);
                cardRect.anchoredPosition = Vector2.zero;
                cardRect.sizeDelta = new Vector2(440f, 250f);
                Image cardImage = card.AddComponent<Image>();
                cardImage.sprite = rounded;
                cardImage.type = rounded != null ? Image.Type.Sliced : Image.Type.Simple;
                cardImage.color = theme != null
                    ? theme.elevatedPanelBackground
                    : new Color(0.075f, 0.102f, 0.141f, 1f);
                cardImage.raycastTarget = false;

                CreateText(
                    card.transform,
                    "Heading",
                    "Cancel active task?",
                    new Vector2(28f, -24f),
                    new Vector2(384f, 38f),
                    24f,
                    FontStyles.Bold,
                    theme != null ? theme.primaryText : Color.white,
                    font);
                CreateText(
                    card.transform,
                    "Message",
                    "Your current task progress will be discarded.",
                    new Vector2(28f, -76f),
                    new Vector2(384f, 56f),
                    15f,
                    FontStyles.Normal,
                    theme != null ? theme.secondaryText : Color.white,
                    font);

                continueButton = GetOrCreateButton(
                    card.transform,
                    "ContinueTaskButton",
                    "Continue Task",
                    new Vector2(170f, 44f),
                    new Vector2(28f, -174f),
                    theme != null ? theme.elevatedPanelBackground : Color.gray,
                    theme != null ? theme.primaryText : Color.white,
                    rounded,
                    font,
                    ref changed);
                confirmButton = GetOrCreateButton(
                    card.transform,
                    "ConfirmCancelButton",
                    "Confirm Cancel",
                    new Vector2(170f, 44f),
                    new Vector2(242f, -174f),
                    theme != null ? theme.error : new Color(0.8f, 0.2f, 0.2f, 1f),
                    Color.white,
                    rounded,
                    font,
                    ref changed);

                view.SetActive(false);
                view.transform.SetAsLastSibling();
                changed = true;
                return view;
            }

            Transform cardTransform = FindDeep(view.transform, "ConfirmationCard");
            Transform continueTransform = FindDeep(view.transform, "ContinueTaskButton");
            Transform confirmTransform = FindDeep(view.transform, "ConfirmCancelButton");
            if (cardTransform == null || continueTransform == null || confirmTransform == null)
            {
                throw new InvalidOperationException(
                    "Existing CancelConfirmationView is incomplete. It was not overwritten.");
            }

            continueButton = continueTransform.GetComponent<Button>();
            confirmButton = confirmTransform.GetComponent<Button>();
            if (continueButton == null || confirmButton == null)
            {
                throw new InvalidOperationException(
                    "Existing cancellation controls are missing Button components.");
            }
            return view;
        }

        private static Button GetOrCreateButton(
            Transform parent,
            string objectName,
            string label,
            Vector2 size,
            Vector2 position,
            Color background,
            Color foreground,
            Sprite rounded,
            TMP_FontAsset font,
            ref bool changed)
        {
            Transform existing = FindDirectChild(parent, objectName);
            if (existing != null)
            {
                Button existingButton = existing.GetComponent<Button>();
                if (existingButton == null)
                {
                    throw new InvalidOperationException(
                        objectName + " exists without a Button and was not modified.");
                }
                return existingButton;
            }

            GameObject buttonObject = CreateUiObject(objectName, parent);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Image image = buttonObject.AddComponent<Image>();
            image.sprite = rounded;
            image.type = rounded != null ? Image.Type.Sliced : Image.Type.Simple;
            image.color = background;
            image.raycastTarget = true;

            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            buttonObject.AddComponent<UiButtonPressScale>();

            TMP_Text text = CreateText(
                buttonObject.transform,
                "Label",
                label,
                Vector2.zero,
                size,
                15f,
                FontStyles.Bold,
                foreground,
                font);
            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.offsetMin = new Vector2(10f, 4f);
            textRect.offsetMax = new Vector2(-10f, -4f);
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            changed = true;
            return button;
        }

        private static TMP_Text CreateText(
            Transform parent,
            string objectName,
            string value,
            Vector2 position,
            Vector2 size,
            float fontSize,
            FontStyles style,
            Color color,
            TMP_FontAsset font)
        {
            GameObject textObject = CreateUiObject(objectName, parent);
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.font = font;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.enableWordWrapping = true;
            text.raycastTarget = false;
            return text;
        }

        private static bool EnsureD1Visual(
            GameObject buttonObject,
            NorscopeVisualRole role)
        {
            if (buttonObject.GetComponent<NorscopeInteractionVisual>() != null)
            {
                return false;
            }

            GameObject layerPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(D1LayerPath);
            NorscopeDesignProfile profile =
                AssetDatabase.LoadAssetAtPath<NorscopeDesignProfile>(D1ProfilePath);
            if (layerPrefab == null || profile == null)
            {
                Debug.LogWarning(
                    "[Norscope Phase7] D1 assets are unavailable; cancellation remains functional.");
                return false;
            }

            GameObject layer = PrefabUtility.InstantiatePrefab(
                layerPrefab,
                buttonObject.transform) as GameObject;
            if (layer == null) return false;
            layer.name = "D1_VisualLayer";
            Stretch(layer.GetComponent<RectTransform>());
            layer.transform.SetAsLastSibling();
            Graphic[] graphics = layer.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                graphics[i].raycastTarget = false;
            }

            NorscopeHighlightController controller =
                layer.GetComponent<NorscopeHighlightController>();
            NorscopeInteractionVisual visual =
                buttonObject.AddComponent<NorscopeInteractionVisual>();
            visual.SetReferences(
                role,
                buttonObject.GetComponent<Button>(),
                controller,
                profile);
            return true;
        }

        private static bool AllDecorativeGraphicsIgnoreRaycasts(
            params Transform[] controls)
        {
            for (int i = 0; i < controls.Length; i++)
            {
                if (controls[i] == null) return false;
                Transform layer = FindDirectChild(controls[i], "D1_VisualLayer");
                if (layer == null) continue;
                Graphic[] graphics =
                    layer.GetComponentsInChildren<Graphic>(true);
                for (int graphicIndex = 0;
                     graphicIndex < graphics.Length;
                     graphicIndex++)
                {
                    if (graphics[graphicIndex].raycastTarget) return false;
                }
            }
            return true;
        }

        private static Vector2 InitialCancelPosition(Transform bottomNavigation)
        {
            Transform previous = FindDirectChild(bottomNavigation, "PreviousButton");
            RectTransform previousRect =
                previous != null ? previous.GetComponent<RectTransform>() : null;
            return previousRect != null
                ? previousRect.anchoredPosition + new Vector2(-134f, 0f)
                : Vector2.zero;
        }

        private static bool Assign(
            SerializedObject target,
            string propertyName,
            UnityEngine.Object value)
        {
            SerializedProperty property = target.FindProperty(propertyName);
            if (property == null)
            {
                throw new MissingFieldException(
                    typeof(ActiveTaskPanel).Name,
                    propertyName);
            }
            if (property.objectReferenceValue == value) return false;
            property.objectReferenceValue = value;
            return true;
        }

        private static GameObject CreateUiObject(string objectName, Transform parent)
        {
            var gameObject = new GameObject(objectName, typeof(RectTransform));
            gameObject.layer = parent.gameObject.layer;
            gameObject.transform.SetParent(parent, false);
            return gameObject;
        }

        private static void Stretch(RectTransform rect)
        {
            if (rect == null) return;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Transform FindDirectChild(Transform parent, string childName)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == childName) return child;
            }
            return null;
        }

        private static Transform FindDeep(Transform root, string objectName)
        {
            if (root == null) return null;
            if (root.name == objectName) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeep(root.GetChild(i), objectName);
                if (found != null) return found;
            }
            return null;
        }

        private static string HierarchyPath(Transform target, Transform root)
        {
            string path = target.name;
            Transform current = target.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                if (current == root) break;
                current = current.parent;
            }
            return path;
        }

        private static void Backup()
        {
            if (!AssetDatabase.IsValidFolder(BackupDirectory))
            {
                throw new DirectoryNotFoundException(BackupDirectory);
            }
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string backupPath = AssetDatabase.GenerateUniqueAssetPath(
                BackupDirectory + "/ActiveTaskPanel_PreCancel_" +
                timestamp + ".prefab");
            if (!AssetDatabase.CopyAsset(PanelPath, backupPath))
            {
                throw new IOException("Could not back up " + PanelPath);
            }
        }
    }
}
