using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Norscope.TaskExecution;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Norscope.Editor
{
    /// <summary>
    /// Task-Completion-only implementation of task complete.zip. Future PDF and
    /// evidence features are represented honestly as unavailable presentation.
    /// </summary>
    public static class PhaseT3TaskCompletionFigmaConfigurator
    {
        private const string PanelPath =
            "Assets/Norscope/UI/Panels/TaskCompletionPanel.prefab";
        private const string RoundedPath =
            "Assets/Norscope/UI/DesignSystem/Sprites/UIRounded_8.png";
        private const string OutfitPath =
            "Assets/Norscope/UI/Fonts/Outfit SDF.asset";
        private const string InterPath =
            "Assets/Norscope/UI/Fonts/Inter SDF.asset";
        private const string LogoPath =
            "Assets/WhatsApp Image 2026-07-19 at 13.42.15-transparent.png";
        private const string BackupKey = "Norscope.TaskCompletionFigma.Backup";

        private static readonly Color Bg = Hex("0D1117");
        private static readonly Color Panel = Hex("131A22");
        private static readonly Color Card = Hex("18212B");
        private static readonly Color Border = Hex("2D3B49");
        private static readonly Color Accent = Hex("1EC8F5");
        private static readonly Color Primary = Hex("F3F7FB");
        private static readonly Color Secondary = Hex("91A2B5");
        private static readonly Color Dim = Hex("4A5D6E");
        private static readonly Color Success = Hex("39D98A");

        [MenuItem("Norscope/HoloLens/Task Completion Figma/1) Capture Completion Baseline")]
        public static void CaptureMenu()
        {
            string folder =
                "Assets/Norscope/UI/Panels/_Backups/TaskCompletionFigma_" +
                DateTime.Now.ToString("yyyyMMdd_HHmmss");
            Directory.CreateDirectory(folder);
            FileUtil.CopyFileOrDirectory(PanelPath,
                folder + "/TaskCompletionPanel.prefab");
            File.WriteAllText(folder + "/manifest.txt",
                "Captured=" + DateTime.Now.ToString("O") + "\n" +
                "Source=" + PanelPath + "\n");
            EditorPrefs.SetString(BackupKey, folder);
            AssetDatabase.Refresh();
            Debug.Log("[Norscope Completion Figma] Baseline READY\n" + folder);
        }

        [MenuItem("Norscope/HoloLens/Task Completion Figma/2) Dry Run")]
        public static void DryRunMenu()
        {
            bool ready = DryRun(out string report);
            Debug.Log((ready
                ? "[Norscope Completion Figma] Dry run READY\n"
                : "[Norscope Completion Figma] Dry run BLOCKED\n") + report);
        }

        [MenuItem("Norscope/HoloLens/Task Completion Figma/3) Apply")]
        public static void ApplyMenu()
        {
            if (!DryRun(out string report))
            {
                Debug.LogError("[Norscope Completion Figma] Apply BLOCKED\n" +
                               report);
                return;
            }
            if (!EditorUtility.DisplayDialog("Apply Task Completion Figma",
                    "Apply task complete.zip to TaskCompletionPanel.prefab only?\n\n" +
                    "PDF, photo, video and voice features remain unimplemented.",
                    "Apply", "Cancel"))
                return;
            ApplyPanel();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Norscope Completion Figma] Apply COMPLETE");
        }

        [MenuItem("Norscope/HoloLens/Task Completion Figma/4) Validate")]
        public static void ValidateMenu()
        {
            bool ok = Validate(out string report);
            Debug.Log((ok
                ? "[Norscope Completion Figma] Validation PASSED\n"
                : "[Norscope Completion Figma] Validation FAILED\n") + report);
        }

        [MenuItem("Norscope/HoloLens/Task Completion Figma/5) Restore")]
        public static void RestoreMenu()
        {
            string folder = EditorPrefs.GetString(BackupKey, string.Empty);
            string backup = folder + "/TaskCompletionPanel.prefab";
            if (string.IsNullOrEmpty(folder) || !File.Exists(backup))
            {
                Debug.LogError("[Norscope Completion Figma] No captured backup.");
                return;
            }
            if (!EditorUtility.DisplayDialog("Restore Task Completion",
                    "Restore the captured TaskCompletionPanel.prefab?",
                    "Restore", "Cancel"))
                return;
            File.Copy(backup, PanelPath, true);
            AssetDatabase.Refresh();
            Debug.Log("[Norscope Completion Figma] Restore COMPLETE");
        }

        private static bool DryRun(out string report)
        {
            var lines = new StringBuilder();
            var blockers = new List<string>();
            string backup = EditorPrefs.GetString(BackupKey, string.Empty);
            if (!File.Exists(PanelPath)) blockers.Add("TaskCompletionPanel missing.");
            if (string.IsNullOrEmpty(backup) || !Directory.Exists(backup))
                blockers.Add("Capture a fresh completion baseline first.");
            if (!Renderable(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OutfitPath)) ||
                !Renderable(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(InterPath)))
                blockers.Add("Outfit/Inter TMP fonts are not renderable.");
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath);
            if (prefab == null ||
                prefab.GetComponent<TaskCompletionPanel>() == null)
                blockers.Add("TaskCompletionPanel component missing.");
            if (prefab != null)
            {
                Require(prefab.transform, blockers, "TitleBar", "CloseButton",
                    "GrabRegion", "BottomRight", "ReturnHomeButton",
                    "GenerateReportButton");
            }

            lines.AppendLine("SOURCE  task complete.zip / src/app/App.tsx");
            lines.AppendLine("TARGET  TaskCompletionPanel.prefab only");
            lines.AppendLine("LAYOUT  880 x 580 authored composition fitted into existing 480 x 300 window");
            lines.AppendLine("REAL    task name, duration, completion time and workflow steps");
            lines.AppendLine("FUTURE  PDF/evidence/verification rows show unavailable states");
            lines.AppendLine("ACTIONS existing Return Home and Generate Report placeholder preserved");
            foreach (string blocker in blockers) lines.AppendLine("BLOCKER " + blocker);
            lines.AppendLine(blockers.Count == 0
                ? "READY — Apply may proceed."
                : "BLOCKED — no prefab should be modified.");
            report = lines.ToString();
            return blockers.Count == 0;
        }

        private static void ApplyPanel()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PanelPath);
            try
            {
                TaskCompletionPanel panel =
                    root.GetComponent<TaskCompletionPanel>();
                SerializedObject so = new SerializedObject(panel);
                TMP_Text taskTitle = Ref<TMP_Text>(so, "taskTitleText");
                Button home = Ref<Button>(so, "returnHomeButton");
                Button report = Ref<Button>(so, "generateReportButton");

                Transform previous = root.transform.Find("FigmaContent");
                if (previous != null)
                {
                    Debug.Log(
                        "[Norscope Completion Figma] Existing authored FigmaContent " +
                        "was preserved; Apply made no changes.");
                    return;
                }

                TMP_FontAsset outfit =
                    AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OutfitPath);
                TMP_FontAsset inter =
                    AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(InterPath);
                Sprite rounded = AssetDatabase.LoadAssetAtPath<Sprite>(RoundedPath);
                Sprite logo = AssetDatabase.LoadAssetAtPath<Sprite>(LogoPath);

                HideLegacyPresentation(root, taskTitle, home, report);

                RectTransform content = Node(root.transform, "FigmaContent",
                    new Vector2(880f, 580f), Vector2.zero);
                content.anchorMin = content.anchorMax = new Vector2(0.5f, 0.5f);
                content.pivot = new Vector2(0.5f, 0.5f);
                content.anchoredPosition = Vector2.zero;
                content.localScale = Vector3.one * (300f / 580f);
                content.SetAsFirstSibling();
                Image backing = content.gameObject.AddComponent<Image>();
                Style(backing, rounded, Color.clear, false);

                ImageNode(content, "NorscopeLogo", logo, Color.white,
                    new Vector2(40f, 28f), new Vector2(116f, 24f));
                Pill(content, "VerifiedStatus", "✓  Verified", Success,
                    new Vector2(596f, 29f), new Vector2(76f, 22f), rounded, inter);
                Pill(content, "OfflineStatus", "●  Offline", Secondary,
                    new Vector2(678f, 29f), new Vector2(72f, 22f), rounded, inter);
                Pill(content, "CompletedStatus", "✓  Completed", Accent,
                    new Vector2(756f, 29f), new Vector2(84f, 22f), rounded, inter);

                TMP_Text heading = Text(content, "CompletionScreenTitle",
                    "Task Completed", outfit, 38f, Primary,
                    new Vector2(40f, 77f), new Vector2(600f, 48f));
                heading.fontStyle = FontStyles.Normal;
                TMP_Text subtitle = Text(content, "CompletionSubtitle",
                    "Maintenance workflow successfully completed", inter,
                    12f, Secondary, new Vector2(40f, 126f),
                    new Vector2(480f, 20f));
                ImageNode(content, "Separator", null,
                    new Color(Success.r, Success.g, Success.b, 0.22f),
                    new Vector2(40f, 157f), new Vector2(800f, 1f));

                Image summary = ImageNode(content, "CompletionSummary", rounded,
                    Card, new Vector2(40f, 174f), new Vector2(464f, 146f));
                ImageNode(summary.transform, "SuccessAccent", null, Success,
                    new Vector2(0f, 20f), new Vector2(2f, 106f));
                taskTitle.transform.SetParent(summary.transform, false);
                StyleText(taskTitle, outfit, 16f, Primary,
                    TextAlignmentOptions.TopLeft);
                Place(taskTitle.rectTransform, new Vector2(52f, 15f),
                    new Vector2(380f, 24f));
                Text(summary.transform, "CompletedSuccessfully",
                    "Completed successfully", inter, 10f, Success,
                    new Vector2(52f, 40f), new Vector2(220f, 16f));
                Text(summary.transform, "SuccessMark", "✓", outfit, 17f,
                    Success, new Vector2(15f, 16f), new Vector2(28f, 28f),
                    TextAlignmentOptions.Center);

                TMP_Text duration = Metric(summary.transform, "Duration",
                    "DURATION", "Not recorded", new Vector2(15f, 72f),
                    rounded, inter);
                TMP_Text completedAt = Metric(summary.transform, "CompletedAt",
                    "COMPLETED", "Not recorded", new Vector2(236f, 72f),
                    rounded, inter);
                TMP_Text verification = Metric(summary.transform,
                    "Verification", "VERIFICATION", "Not recorded",
                    new Vector2(15f, 110f), rounded, inter);
                TMP_Text evidence = Metric(summary.transform, "Evidence",
                    "EVIDENCE", "Not captured", new Vector2(236f, 110f),
                    rounded, inter);

                Image workflow = ImageNode(content, "WorkflowChecklist", rounded,
                    Card, new Vector2(40f, 330f), new Vector2(464f, 198f));
                Text(workflow.transform, "WorkflowHeading", "WORKFLOW STEPS",
                    inter, 9f, Dim, new Vector2(14f, 10f),
                    new Vector2(150f, 16f));
                TMP_Text stepCount = Text(workflow.transform,
                    "CompletedStepCount", "Workflow completed", inter,
                    9f, Success, new Vector2(300f, 10f),
                    new Vector2(148f, 16f), TextAlignmentOptions.Right);
                var rows = new List<TaskCompletionStepRowView>();
                for (int i = 0; i < 7; i++)
                    rows.Add(StepRow(workflow.transform, i, outfit, inter));

                Image verificationCard = ImageNode(content,
                    "VerificationResults", rounded, Card,
                    new Vector2(518f, 174f), new Vector2(322f, 104f));
                Text(verificationCard.transform, "Heading", "VERIFICATION",
                    inter, 9f, Dim, new Vector2(14f, 10f),
                    new Vector2(140f, 16f));
                ResultRow(verificationCard.transform, "Visual inspection",
                    "Not recorded", 34f, inter);
                ResultRow(verificationCard.transform, "AI verification",
                    "Not implemented", 57f, inter);
                ResultRow(verificationCard.transform, "Supervisor review",
                    "Not required", 80f, inter);

                Image outputs = ImageNode(content, "GeneratedOutputs", rounded,
                    Card, new Vector2(518f, 288f), new Vector2(322f, 134f));
                Text(outputs.transform, "Heading", "GENERATED OUTPUTS", inter,
                    9f, Dim, new Vector2(14f, 10f),
                    new Vector2(180f, 16f));
                OutputRow(outputs.transform, "Maintenance Report",
                    "Not generated", 34f, inter);
                OutputRow(outputs.transform, "Captured Photos / Video",
                    "Not captured", 58f, inter);
                OutputRow(outputs.transform, "Voice Notes",
                    "Not captured", 82f, inter);
                OutputRow(outputs.transform, "Execution Log",
                    "Future feature", 106f, inter);

                StyleButton(report, content, "Generate Report (Placeholder)",
                    new Vector2(518f, 435f), new Vector2(322f, 38f),
                    Card, Secondary, rounded, inter);
                StyleButton(home, content, "Return to Home",
                    new Vector2(518f, 482f), new Vector2(322f, 40f),
                    Accent, Bg, rounded, outfit);

                Text(content, "Footer", "v4.2.1 \u00B7 HoloLens 2", inter,
                    9f, Dim, new Vector2(680f, 552f),
                    new Vector2(160f, 16f), TextAlignmentOptions.Right);

                SetRef(so, "taskTitleText", taskTitle);
                SetRef(so, "messageText", subtitle);
                SetRef(so, "durationText", duration);
                SetRef(so, "completedAtText", completedAt);
                SetRef(so, "verificationStatusText", verification);
                SetRef(so, "evidenceStatusText", evidence);
                SetRef(so, "completedStepCountText", stepCount);
                SetArray(so.FindProperty("workflowRows"), rows);
                so.ApplyModifiedPropertiesWithoutUndo();
                SetLayer(root, root.layer);
                PrefabUtility.SaveAsPrefabAsset(root, PanelPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void HideLegacyPresentation(GameObject root,
            TMP_Text taskTitle, Button home, Button report)
        {
            var preserved = new HashSet<GameObject>
            {
                taskTitle?.gameObject, home?.gameObject, report?.gameObject
            };
            string[] keep = { "TitleBar", "ResizeHandles", "FigmaContent" };
            for (int i = 0; i < root.transform.childCount; i++)
            {
                Transform child = root.transform.GetChild(i);
                bool keepObject = Array.Exists(keep, n => n == child.name) ||
                                  preserved.Contains(child.gameObject);
                if (!keepObject) child.gameObject.SetActive(false);
            }
            Transform titleBar = Find(root.transform, "TitleBar");
            Image titleBarImage = titleBar?.GetComponent<Image>();
            if (titleBarImage != null)
            {
                titleBarImage.color = Color.clear;
                titleBarImage.raycastTarget = false;
            }
            Transform title = titleBar != null ? Find(titleBar, "Title") : null;
            if (title != null) title.gameObject.SetActive(false);
        }

        private static TaskCompletionStepRowView StepRow(Transform parent,
            int index, TMP_FontAsset outfit, TMP_FontAsset inter)
        {
            RectTransform row = Node(parent, "CompletedStep" + (index + 1),
                new Vector2(436f, 20f),
                new Vector2(14f, 34f + index * 22f));
            TMP_Text mark = Text(row, "Mark", "✓", inter, 9f, Success,
                new Vector2(0f, 1f), new Vector2(18f, 18f),
                TextAlignmentOptions.Center);
            TMP_Text title = Text(row, "Title", "Completed step", outfit, 10f,
                Secondary, new Vector2(25f, 1f), new Vector2(400f, 18f));
            TaskCompletionStepRowView view =
                row.gameObject.AddComponent<TaskCompletionStepRowView>();
            SerializedObject so = new SerializedObject(view);
            SetRef(so, "numberText", mark);
            SetRef(so, "titleText", title);
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        private static TMP_Text Metric(Transform parent, string name,
            string label, string value, Vector2 position, Sprite rounded,
            TMP_FontAsset font)
        {
            Image box = ImageNode(parent, name, rounded, Panel, position,
                new Vector2(213f, 32f));
            Text(box.transform, "Label", label, font, 7.5f, Dim,
                new Vector2(8f, 4f), new Vector2(100f, 10f));
            return Text(box.transform, "Value", value, font, 9.5f, Secondary,
                new Vector2(8f, 15f), new Vector2(195f, 13f));
        }

        private static void ResultRow(Transform parent, string label,
            string state, float y, TMP_FontAsset font)
        {
            Text(parent, label.Replace(" ", ""), label, font, 9.5f, Secondary,
                new Vector2(14f, y), new Vector2(180f, 16f));
            Text(parent, label.Replace(" ", "") + "State", state, font, 8.5f,
                Dim, new Vector2(190f, y), new Vector2(118f, 16f),
                TextAlignmentOptions.Right);
        }

        private static void OutputRow(Transform parent, string label,
            string state, float y, TMP_FontAsset font)
        {
            Text(parent, label.Replace(" ", ""), label, font, 9.5f, Secondary,
                new Vector2(14f, y), new Vector2(195f, 16f));
            Text(parent, label.Replace(" ", "") + "State", state, font, 8.5f,
                Dim, new Vector2(205f, y), new Vector2(103f, 16f),
                TextAlignmentOptions.Right);
        }

        private static void Pill(Transform parent, string name, string text,
            Color color, Vector2 position, Vector2 size, Sprite rounded,
            TMP_FontAsset font)
        {
            Image pill = ImageNode(parent, name, rounded, Card, position, size);
            Text(pill.transform, "Label", text, font, 9f, color, Vector2.zero,
                size, TextAlignmentOptions.Center);
        }

        private static void StyleButton(Button button, Transform parent,
            string label, Vector2 position, Vector2 size, Color background,
            Color foreground, Sprite rounded, TMP_FontAsset font)
        {
            button.transform.SetParent(parent, false);
            Place(button.GetComponent<RectTransform>(), position, size);
            Image image = button.targetGraphic as Image ??
                          button.GetComponent<Image>();
            Style(image, rounded, background, true);
            button.targetGraphic = image;
            TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
            {
                text.gameObject.SetActive(true);
                text.text = label;
                StyleText(text, font, 11f, foreground,
                    TextAlignmentOptions.Center);
                Stretch(text.rectTransform);
            }
        }

        private static bool Validate(out string report)
        {
            var lines = new StringBuilder();
            bool ok = true;
            void Check(bool value, string label)
            {
                ok &= value;
                lines.AppendLine((value ? "PASS  " : "FAIL  ") + label);
            }
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath);
            Check(prefab != null, "TaskCompletionPanel.prefab exists");
            if (prefab != null)
            {
                TaskCompletionPanel panel =
                    prefab.GetComponent<TaskCompletionPanel>();
                Check(panel != null, "TaskCompletionPanel component remains");
                RectTransform content =
                    prefab.transform.Find("FigmaContent") as RectTransform;
                Check(content != null && content.sizeDelta ==
                      new Vector2(880f, 580f),
                    "880 x 580 authored FigmaContent exists");
                Check(Find(prefab.transform, "CloseButton") != null,
                    "Close control remains");
                Check(Find(prefab.transform, "GrabRegion") != null,
                    "Grab region remains");
                Check(Find(prefab.transform, "BottomRight") != null,
                    "Resize handle remains");
                Check(Find(prefab.transform, "ReturnHomeButton") != null &&
                      Find(prefab.transform, "GenerateReportButton") != null,
                    "Existing functional actions remain");
                Check(prefab.GetComponentsInChildren<TaskCompletionStepRowView>(
                          true).Length == 7,
                    "Seven authored workflow row slots exist");
                Check(Find(prefab.transform, "GeneratedOutputs") != null,
                    "Future output states are visibly authored");
            }
            report = lines.ToString();
            return ok;
        }

        private static RectTransform Node(Transform parent, string name,
            Vector2 size, Vector2 position)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            Place(rt, position, size);
            return rt;
        }

        private static Image ImageNode(Transform parent, string name,
            Sprite sprite, Color color, Vector2 position, Vector2 size)
        {
            RectTransform rt = Node(parent, name, size, position);
            Image image = rt.gameObject.AddComponent<Image>();
            Style(image, sprite, color, false);
            return image;
        }

        private static TMP_Text Text(Transform parent, string name,
            string value, TMP_FontAsset font, float size, Color color,
            Vector2 position, Vector2 dimensions,
            TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft)
        {
            RectTransform rt = Node(parent, name, dimensions, position);
            TMP_Text text = rt.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            StyleText(text, font, size, color, alignment);
            return text;
        }

        private static void Style(Image image, Sprite sprite, Color color,
            bool raycast)
        {
            if (image == null) return;
            image.sprite = sprite;
            image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            image.color = color;
            image.raycastTarget = raycast;
        }

        private static void StyleText(TMP_Text text, TMP_FontAsset font,
            float size, Color color, TextAlignmentOptions alignment)
        {
            if (text == null) return;
            text.font = font;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.enableWordWrapping = true;
            text.margin = Vector4.zero;
        }

        private static void Place(RectTransform rt, Vector2 position,
            Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(position.x, -position.y);
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

        private static T Ref<T>(SerializedObject so, string name)
            where T : Object
        {
            return so.FindProperty(name)?.objectReferenceValue as T;
        }

        private static void SetRef(SerializedObject so, string name, Object value)
        {
            SerializedProperty property = so.FindProperty(name);
            if (property != null) property.objectReferenceValue = value;
        }

        private static void SetArray<T>(SerializedProperty property,
            IList<T> values) where T : Object
        {
            property.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        private static Transform Find(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform value = Find(root.GetChild(i), name);
                if (value != null) return value;
            }
            return null;
        }

        private static void Require(Transform root, List<string> blockers,
            params string[] names)
        {
            foreach (string name in names)
                if (Find(root, name) == null) blockers.Add(name + " missing.");
        }

        private static void SetLayer(GameObject root, int layer)
        {
            root.layer = layer;
            for (int i = 0; i < root.transform.childCount; i++)
                SetLayer(root.transform.GetChild(i).gameObject, layer);
        }

        private static bool Renderable(TMP_FontAsset font)
        {
            return font != null && font.material != null &&
                   font.atlasTexture != null;
        }

        private static Color Hex(string value)
        {
            ColorUtility.TryParseHtmlString("#" + value, out Color color);
            return color;
        }
    }
}
