using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Norscope.DesignSystem;
using Norscope.UI;

namespace Norscope.Editor
{
    /// <summary>
    /// D2.1 writes authored presentation values only. The same planner powers dry-run
    /// and apply. It never changes hierarchy, RectTransforms, sprites, Buttons or events.
    /// </summary>
    public static class PhaseD21VisiblePrefabConfigurator
    {
        private const string ProfilePath =
            "Assets/Norscope/DesignSystem/Profiles/NorscopeDesignProfile.asset";
        private const string ThemePath =
            "Assets/Norscope/UI/DesignSystem/Themes/NorscopeTheme.asset";

        private static readonly Color Panel =
            new Color(0.027f, 0.039f, 0.055f, 0.98f);
        private static readonly Color Elevated =
            new Color(0.065f, 0.086f, 0.113f, 0.99f);
        private static readonly Color Card =
            new Color(0.086f, 0.113f, 0.145f, 1f);
        private static readonly Color Border =
            new Color(0.255f, 0.315f, 0.385f, 0.82f);
        private static readonly Color Accent =
            new Color(0.24f, 0.72f, 0.82f, 1f);
        private static readonly Color PrimaryText =
            new Color(0.965f, 0.975f, 0.985f, 1f);
        private static readonly Color SecondaryText =
            new Color(0.68f, 0.73f, 0.79f, 1f);
        private static readonly Color Success =
            new Color(0.31f, 0.76f, 0.49f, 1f);
        private static readonly Color Warning =
            new Color(0.94f, 0.65f, 0.24f, 1f);
        private static readonly Color Error =
            new Color(0.88f, 0.35f, 0.37f, 1f);

        private sealed class Plan
        {
            internal readonly StringBuilder Details = new StringBuilder();
            internal int Changes;
            internal int Blocked;
            internal readonly HashSet<string> Categories = new HashSet<string>();

            internal void Add(string asset, string path, string property,
                string oldValue, string newValue, string category)
            {
                Changes++;
                Categories.Add(category);
                int separator = property.IndexOf('.');
                string component = separator > 0
                    ? property.Substring(0, separator) : "<serialized asset>";
                string member = separator > 0
                    ? property.Substring(separator + 1) : property;
                string runtimeOwner = RuntimeVisualOwner(asset, path, property);
                Details.AppendLine(
                    "Prefab: " + asset +
                    " | GameObject: " + path +
                    " | Component: " + component +
                    " | Property: " + member +
                    " | Old: " + oldValue +
                    " | New: " + newValue +
                    " | Prefab Mode: " +
                    (asset.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)
                        ? "YES" : "PROFILE ASSET") +
                    " | Runtime overwrite: " + runtimeOwner);
            }

            internal void Block(string asset, string path, string property, string reason)
            {
                Blocked++;
                Details.AppendLine("BLOCKED | " + asset + " | " + path + " | " +
                                   property + " | " + reason);
            }

            internal void Note(string value)
            {
                Details.AppendLine("NOTE | " + value);
            }
        }

        private static string RuntimeVisualOwner(
            string asset, string path, string property)
        {
            if (!asset.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                return "No; authoritative/derived asset value";
            if (path.Contains("D1_VisualLayer", StringComparison.Ordinal))
                return "Yes — NorscopeInteractionVisual applies profile state";
            if (asset.EndsWith("/HomePanel.prefab", StringComparison.Ordinal) &&
                path.Contains("/Card_", StringComparison.Ordinal))
                return "Yes for card colors/type — HomePanelCard uses derived theme";
            if (asset.EndsWith("/TaskCard.prefab", StringComparison.Ordinal) &&
                (path.Contains("Border", StringComparison.Ordinal) ||
                 path.Contains("Difficulty", StringComparison.Ordinal)))
                return "Yes for interaction/difficulty state — TaskCardView";
            if (asset.Contains("NorscopePrimaryButton") ||
                asset.Contains("NorscopeSecondaryButton"))
                return "Yes for owned background/label/layout — NorscopeButtonControl";
            if ((asset.Contains("/Panels/") &&
                 (path.EndsWith("/Background", StringComparison.Ordinal) ||
                  path.EndsWith("/Border", StringComparison.Ordinal) ||
                  path.EndsWith("/Title", StringComparison.Ordinal))))
                return "Yes for panel root visuals — NorscopePanelRoot uses derived theme";
            return "No visual overwrite identified";
        }

        [MenuItem("Norscope/HoloLens/Phase D2.1/1) Capture Pre-D2.1 Visual Baseline")]
        public static void CaptureMenu()
        {
            bool ok = D21VisualBaseline.Capture(out string report);
            Debug.Log((ok ? "[Norscope D2.1] Baseline READY\n" :
                "[Norscope D2.1] Baseline FAILED\n") + report);
            EditorUtility.DisplayDialog("D2.1 Visual Baseline",
                (ok ? "READY\n\n" : "FAILED\n\n") + report, "OK");
        }

        [MenuItem("Norscope/HoloLens/Phase D2.1/2) Dry-Run Visible Prefab Redesign")]
        public static void DryRunMenu()
        {
            bool ready = BuildPlan(false, out string report);
            Debug.Log((ready ? "[Norscope D2.1] Dry run READY\n" :
                "[Norscope D2.1] Dry run BLOCKED\n") + report);
            EditorUtility.DisplayDialog("D2.1 Dry Run",
                (ready ? "READY — see Console for every change.\n\n" :
                    "BLOCKED — see Console.\n\n") + Summary(report), "OK");
        }

        [MenuItem("Norscope/HoloLens/Phase D2.1/3) Apply Visible Prefab Redesign")]
        public static void ApplyMenu()
        {
            if (!BuildPlan(false, out string dryRun))
            {
                EditorUtility.DisplayDialog("D2.1 blocked", Summary(dryRun), "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog("Apply D2.1 visible redesign",
                    "Apply the exact presentation-only changes reported by the dry run?\n\n" +
                    "No hierarchy, RectTransform, Button, event, sprite, navigation or " +
                    "functional source will be changed.",
                    "Apply", "Cancel"))
                return;

            bool ok = BuildPlan(true, out string report);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log((ok ? "[Norscope D2.1] Apply COMPLETE\n" :
                "[Norscope D2.1] Apply FAILED\n") + report);
        }

        [MenuItem("Norscope/HoloLens/Phase D2.1/4) Validate D2.1")]
        public static void ValidateMenu()
        {
            bool ok = Validate(out string report);
            Debug.Log((ok ? "[Norscope D2.1] Validation PASSED\n" :
                "[Norscope D2.1] Validation FAILED\n") + report);
            EditorUtility.DisplayDialog("Validate D2.1",
                (ok ? "PASSED\n\n" : "FAILED\n\n") + report, "OK");
        }

        [MenuItem("Norscope/HoloLens/Phase D2.1/5) Restore Pre-D2.1 Visuals")]
        public static void RestoreMenu()
        {
            if (!EditorUtility.DisplayDialog("Restore pre-D2.1 visuals",
                    "Restore only the visual assets listed in the immutable D2.1 " +
                    "backup manifest?", "Restore", "Cancel"))
                return;

            bool ok = D21VisualBaseline.Restore(out string report);
            Debug.Log((ok ? "[Norscope D2.1] Restore SUCCEEDED\n" :
                "[Norscope D2.1] Restore FAILED\n") + report);
        }

        private static bool BuildPlan(bool apply, out string report)
        {
            var plan = new Plan();
            if (!D21VisualBaseline.Exists)
            {
                report = "Run 1) Capture Pre-D2.1 Visual Baseline first.";
                return false;
            }
            if (!D2VisualBaseline.CompareAssetsOnly(out string functionalReport))
            {
                report = "Functional baseline mismatch:\n" + functionalReport;
                return false;
            }

            NorscopeDesignProfile profile =
                AssetDatabase.LoadAssetAtPath<NorscopeDesignProfile>(ProfilePath);
            NorscopeTheme theme =
                AssetDatabase.LoadAssetAtPath<NorscopeTheme>(ThemePath);
            if (profile == null || theme == null)
            {
                report = "NorscopeDesignProfile or compatibility theme is missing.";
                return false;
            }

            PlanProfile(profile, plan, apply);
            PlanTheme(theme, plan, apply);

            foreach (string path in D21VisualBaseline.VisualAssets
                         .Where(value => value.EndsWith(".prefab",
                             StringComparison.OrdinalIgnoreCase)))
            {
                if (!File.Exists(path))
                {
                    plan.Block(path, "<asset>", "exists", "Prefab is missing.");
                    continue;
                }
                ProcessPrefab(path, plan, apply);
            }

            if (apply)
            {
                EditorUtility.SetDirty(profile);
                EditorUtility.SetDirty(theme);
            }

            bool substantial =
                plan.Changes >= 35 &&
                plan.Categories.Contains("surface") &&
                plan.Categories.Contains("typography") &&
                plan.Categories.Contains("border") &&
                plan.Categories.Contains("button");

            report = "Mode: " + (apply ? "APPLY" : "DRY RUN") +
                     "\nPlanned presentation changes: " + plan.Changes +
                     "\nBlocked/unsafe properties: " + plan.Blocked +
                     "\nCategories: " + string.Join(", ", plan.Categories.OrderBy(v => v)) +
                     "\nExpected visual difference: " +
                     (substantial ? "SUBSTANTIAL" : "INSUFFICIENT") +
                     "\n\n" + plan.Details;
            return plan.Blocked == 0 && substantial;
        }

        private static void ProcessPrefab(string path, Plan plan, bool apply)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            bool changed = false;
            try
            {
                int gridCount = root.GetComponentsInChildren<GridLayoutGroup>(true)
                    .Count(grid => !IsNestedPrefabObject(root, grid.gameObject));
                int layoutElementCount = root
                    .GetComponentsInChildren<LayoutElement>(true)
                    .Count(element =>
                        !IsNestedPrefabObject(root, element.gameObject));
                plan.Note(path + " | authored GridLayoutGroups=" + gridCount +
                          ", authored LayoutElements=" + layoutElementCount +
                          ". RectTransforms are excluded.");
                if (gridCount == 0)
                    plan.Note(path +
                              " | GridLayoutGroup.cellSize: NOT APPLICABLE; " +
                              "no authored GridLayoutGroup exists.");
                else
                    plan.Note(path +
                              " | GridLayoutGroup.cellSize: PRESERVED; changing " +
                              "card dimensions without panel-specific available-area " +
                              "calculation is unsafe. Existing grid spacing is planned.");

                foreach (Image image in root.GetComponentsInChildren<Image>(true))
                {
                    if (IsNestedPrefabObject(root, image.gameObject)) continue;
                    Color? desired = DesiredImageColor(image, root, path);
                    if (desired.HasValue)
                    {
                        string category = image.gameObject.name.EndsWith(
                            "Button", StringComparison.Ordinal)
                            ? "button" : "surface";
                        changed |= ChangeColor(path, PathOf(image.transform, root.transform),
                            "Image.color", image.color, desired.Value, category,
                            value => image.color = value, plan, apply);
                    }
                }

                foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (IsNestedPrefabObject(root, text.gameObject)) continue;
                    string textPath = PathOf(text.transform, root.transform);
                    TextStyle style = DesiredTextStyle(
                        text, textPath);
                    changed |= ChangeColor(path, textPath,
                        "TMP_Text.color", text.color, style.Color, "typography",
                        value => text.color = value, plan, apply);
                    changed |= ChangeFloat(path, textPath,
                        "TMP_Text.fontSize", text.fontSize, style.Size, "typography",
                        value => text.fontSize = value, plan, apply);
                    if (text.fontWeight != style.Weight)
                    {
                        plan.Add(path, PathOf(text.transform, root.transform),
                            "TMP_Text.fontWeight", text.fontWeight.ToString(),
                            style.Weight.ToString(), "typography");
                        if (apply) text.fontWeight = style.Weight;
                        changed = true;
                    }
                    Vector4? desiredMargin = DesiredTextMargin(text, textPath);
                    if (desiredMargin.HasValue)
                        changed |= ChangeVector4(path, textPath, "TMP_Text.margin",
                            text.margin, desiredMargin.Value, "spacing",
                            value => text.margin = value, plan, apply);
                }

                foreach (LayoutGroup group in root.GetComponentsInChildren<LayoutGroup>(true))
                {
                    if (IsNestedPrefabObject(root, group.gameObject)) continue;
                    string objectPath = PathOf(group.transform, root.transform);
                    float desiredSpacing = DesiredSpacing(group, path);
                    if (group is HorizontalOrVerticalLayoutGroup linear)
                    {
                        changed |= ChangeFloat(path, objectPath,
                            group.GetType().Name + ".spacing", linear.spacing,
                            desiredSpacing, "spacing", value => linear.spacing = value,
                            plan, apply);
                        RectOffset desiredPadding =
                            DesiredPadding(linear, path);
                        if (!SamePadding(linear.padding, desiredPadding))
                        {
                            plan.Add(path, objectPath,
                                group.GetType().Name + ".padding",
                                PaddingText(linear.padding),
                                PaddingText(desiredPadding), "spacing");
                            if (apply)
                            {
                                linear.padding = new RectOffset(
                                    desiredPadding.left, desiredPadding.right,
                                    desiredPadding.top, desiredPadding.bottom);
                            }
                            changed = true;
                        }
                    }
                }

                foreach (Outline outline in root.GetComponentsInChildren<Outline>(true))
                {
                    if (IsNestedPrefabObject(root, outline.gameObject)) continue;
                    Color desired = WithAlpha(Border, 0.42f);
                    changed |= ChangeColor(path, PathOf(outline.transform, root.transform),
                        "Outline.effectColor", outline.effectColor, desired, "border",
                        value => outline.effectColor = value, plan, apply);
                }

                foreach (GridLayoutGroup grid in
                         root.GetComponentsInChildren<GridLayoutGroup>(true))
                {
                    if (IsNestedPrefabObject(root, grid.gameObject)) continue;
                    string objectPath = PathOf(grid.transform, root.transform);
                    Vector2 desiredSpacing = new Vector2(16f, 16f);
                    changed |= ChangeVector2(path, objectPath,
                        "GridLayoutGroup.spacing", grid.spacing, desiredSpacing,
                        "spacing", value => grid.spacing = value, plan, apply);
                    // Cell size remains Inspector-authored. D2.1 never guesses a new
                    // width/height for a grid whose available area is panel-specific.
                }

                foreach (LayoutElement element in
                         root.GetComponentsInChildren<LayoutElement>(true))
                {
                    if (IsNestedPrefabObject(root, element.gameObject)) continue;
                    string objectPath = PathOf(element.transform, root.transform);
                    DesiredPreferredSize(path, element, root,
                        out float? preferredWidth, out float? preferredHeight);
                    if (preferredWidth.HasValue)
                        changed |= ChangeFloat(path, objectPath,
                            "LayoutElement.preferredWidth", element.preferredWidth,
                            preferredWidth.Value, "proportion",
                            value => element.preferredWidth = value, plan, apply);
                    if (preferredHeight.HasValue)
                        changed |= ChangeFloat(path, objectPath,
                            "LayoutElement.preferredHeight", element.preferredHeight,
                            preferredHeight.Value, "proportion",
                            value => element.preferredHeight = value, plan, apply);
                }

                foreach (NorscopeButtonControl control in
                         root.GetComponentsInChildren<NorscopeButtonControl>(true))
                {
                    if (IsNestedPrefabObject(root, control.gameObject)) continue;
                    string objectPath = PathOf(control.transform, root.transform);
                    SerializedObject serialized = new SerializedObject(control);
                    SerializedProperty size = serialized.FindProperty("size");
                    SerializedProperty spacing = serialized.FindProperty("spacing");
                    SerializedProperty padding = serialized.FindProperty("padding");
                    DesiredButtonControlSize(path, control.gameObject.name,
                        out Vector2 desiredSize);

                    if (size != null &&
                        (size.vector2Value - desiredSize).sqrMagnitude > 0.000001f)
                    {
                        plan.Add(path, objectPath, "NorscopeButtonControl.size",
                            Vector2Text(size.vector2Value), Vector2Text(desiredSize),
                            "proportion");
                        if (apply) size.vector2Value = desiredSize;
                        changed = true;
                    }
                    if (spacing != null &&
                        !Mathf.Approximately(spacing.floatValue, 12f))
                    {
                        plan.Add(path, objectPath, "NorscopeButtonControl.spacing",
                            spacing.floatValue.ToString("0.###"), "12", "spacing");
                        if (apply) spacing.floatValue = 12f;
                        changed = true;
                    }
                    if (padding != null)
                    {
                        SerializedProperty left = padding.FindPropertyRelative("m_Left");
                        SerializedProperty right = padding.FindPropertyRelative("m_Right");
                        SerializedProperty top = padding.FindPropertyRelative("m_Top");
                        SerializedProperty bottom = padding.FindPropertyRelative("m_Bottom");
                        RectOffset oldPadding = left != null && right != null &&
                                                top != null && bottom != null
                            ? new RectOffset(left.intValue, right.intValue,
                                top.intValue, bottom.intValue)
                            : null;
                        var desiredPadding = new RectOffset(14, 14, 8, 8);
                        if (!SamePadding(oldPadding, desiredPadding))
                        {
                            plan.Add(path, objectPath,
                                "NorscopeButtonControl.padding",
                                PaddingText(oldPadding), PaddingText(desiredPadding),
                                "spacing");
                            if (apply && oldPadding != null)
                            {
                                left.intValue = desiredPadding.left;
                                right.intValue = desiredPadding.right;
                                top.intValue = desiredPadding.top;
                                bottom.intValue = desiredPadding.bottom;
                            }
                            changed = true;
                        }
                    }
                    if (apply) serialized.ApplyModifiedPropertiesWithoutUndo();
                }

                if (apply && changed) PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Color? DesiredImageColor(
            Image image, GameObject root, string asset)
        {
            string name = image.gameObject.name;
            string full = PathOf(image.transform, root.transform);

            if (name == "EdgeGlow")
                return WithAlpha(Accent, 0.10f);
            if (name == "SurfaceGlow")
                return WithAlpha(Accent, 0.018f);
            if (name == "Border" || name.EndsWith("Border", StringComparison.Ordinal))
                return Border;
            if (name == "ProgressFill")
                return Accent;
            if (name == "ProgressTrack")
                return WithAlpha(Border, 0.42f);
            if (name.Contains("Warning"))
                return WithAlpha(Warning, name.Contains("Icon") ? 1f : 0.14f);
            if (name.Contains("Verification"))
                return WithAlpha(Success, name.Contains("Icon") ? 1f : 0.12f);
            if (name == "CancelConfirmationView")
                return new Color(0f, 0f, 0f, 0.66f);
            if (name == "ConfirmationCard")
                return Elevated;
            if (name.EndsWith("Button", StringComparison.Ordinal))
            {
                if (name.Contains("Finish") || name.Contains("ReturnHome") ||
                    name.Contains("Next") || name.Contains("Open"))
                    return Accent;
                if (name.Contains("ConfirmCancel"))
                    return Error;
                return Elevated;
            }
            if (name == "TitleBar" || full.Contains("/TitleBar/"))
                return Elevated;
            if (name == "Background")
            {
                if (full.Contains("Card_", StringComparison.Ordinal) ||
                    asset.EndsWith("/TaskCard.prefab", StringComparison.Ordinal))
                    return Card;
                return Elevated;
            }
            if (image.gameObject == root)
                return Panel;
            if (name.Contains("Badge"))
                return WithAlpha(Accent, 0.18f);
            if (name.Contains("Icon"))
                return Accent;
            return null;
        }

        private readonly struct TextStyle
        {
            internal readonly Color Color;
            internal readonly float Size;
            internal readonly FontWeight Weight;
            internal TextStyle(Color color, float size, FontWeight weight)
            {
                Color = color;
                Size = size;
                Weight = weight;
            }
        }

        private static TextStyle DesiredTextStyle(TMP_Text text, string fullPath)
        {
            string name = text.gameObject.name;
            string parent = text.transform.parent != null
                ? text.transform.parent.name : string.Empty;

            if (name == "Title" &&
                !fullPath.Contains("Card_", StringComparison.Ordinal))
                return new TextStyle(PrimaryText, 26f, FontWeight.SemiBold);
            if (name.Contains("CompletionHeading"))
                return new TextStyle(Success, 24f, FontWeight.SemiBold);
            if (name.Contains("Heading") || name == "StepTitle")
                return new TextStyle(PrimaryText, 21f, FontWeight.SemiBold);
            if (name.Contains("TaskTitle") || name == "TaskName" ||
                (name == "Title" &&
                 fullPath.Contains("Card_", StringComparison.Ordinal)))
                return new TextStyle(PrimaryText, 18f, FontWeight.SemiBold);
            if (name == "Label")
            {
                Color labelColor = parent.Contains("Finish") ||
                                   parent.Contains("ReturnHome") ||
                                   parent.Contains("Next") ||
                                   parent.Contains("Open")
                    ? Panel : PrimaryText;
                return new TextStyle(labelColor, 14f, FontWeight.Medium);
            }
            if (name.Contains("Warning"))
                return new TextStyle(Warning, 13f, FontWeight.Medium);
            if (name.Contains("Verification"))
                return new TextStyle(Success, 13f, FontWeight.Medium);
            if (name.Contains("Counter") || name.Contains("Duration") ||
                name.Contains("Difficulty") || name.Contains("Subtitle") ||
                name.Contains("Caption"))
                return new TextStyle(SecondaryText, 12f, FontWeight.Regular);
            if (name.Contains("Instruction") || name.Contains("Description") ||
                name.Contains("Message"))
                return new TextStyle(SecondaryText, 14.5f, FontWeight.Regular);
            return new TextStyle(SecondaryText, 14f, FontWeight.Regular);
        }

        private static float DesiredSpacing(LayoutGroup group, string asset)
        {
            string name = group.gameObject.name;
            if (name == "Content" && asset.Contains("TaskLibraryPanel"))
                return 12f;
            if (name.Contains("Navigation") || name.Contains("Actions") ||
                name.Contains("Button"))
                return 12f;
            return 10f;
        }

        private static Vector4? DesiredTextMargin(TMP_Text text, string path)
        {
            string name = text.gameObject.name;
            if (name == "Title" || name.Contains("Heading") ||
                name == "StepTitle" || name.Contains("TaskTitle"))
                return new Vector4(0f, 0f, 0f, 2f);
            if (name.Contains("Counter") || name.Contains("Duration") ||
                name.Contains("Difficulty") || name.Contains("Subtitle") ||
                name.Contains("Caption"))
                return Vector4.zero;
            if (name == "Label" || name.Contains("Instruction") ||
                name.Contains("Description") || name.Contains("Message") ||
                name.Contains("Warning") || name.Contains("Verification"))
                return new Vector4(2f, 1f, 2f, 1f);
            return null;
        }

        private static void DesiredPreferredSize(
            string asset, LayoutElement element, GameObject root,
            out float? width, out float? height)
        {
            width = null;
            height = null;
            string name = element.gameObject.name;

            if (asset.EndsWith("/TaskCard.prefab", StringComparison.Ordinal) &&
                element.gameObject == root)
            {
                height = 64f;
                return;
            }

            if (!name.EndsWith("Button", StringComparison.Ordinal) &&
                !asset.EndsWith("/NavigationBackButton.prefab",
                    StringComparison.Ordinal) &&
                !asset.EndsWith("/NorscopeCloseButton.prefab",
                    StringComparison.Ordinal) &&
                !asset.EndsWith("/NorscopePrimaryButton.prefab",
                    StringComparison.Ordinal) &&
                !asset.EndsWith("/NorscopeSecondaryButton.prefab",
                    StringComparison.Ordinal))
                return;

            height = 44f;
            if (name.Contains("Close") ||
                asset.EndsWith("/NorscopeCloseButton.prefab",
                    StringComparison.Ordinal))
            {
                width = 40f;
                height = 40f;
            }
            else if (name.Contains("Back") ||
                     asset.EndsWith("/NavigationBackButton.prefab",
                         StringComparison.Ordinal))
                width = 96f;
            else if (name.Contains("Previous") || name.Contains("Cancel") ||
                     asset.EndsWith("/NorscopeSecondaryButton.prefab",
                         StringComparison.Ordinal))
                width = 112f;
            else
                width = 124f;
        }

        private static void DesiredButtonControlSize(
            string asset, string name, out Vector2 size)
        {
            if (name.Contains("Close") ||
                asset.EndsWith("/NorscopeCloseButton.prefab",
                    StringComparison.Ordinal))
                size = new Vector2(40f, 40f);
            else if (name.Contains("Back") ||
                     asset.EndsWith("/NavigationBackButton.prefab",
                         StringComparison.Ordinal))
                size = new Vector2(96f, 44f);
            else if (name.Contains("Previous") || name.Contains("Cancel") ||
                     asset.EndsWith("/NorscopeSecondaryButton.prefab",
                         StringComparison.Ordinal))
                size = new Vector2(112f, 44f);
            else
                size = new Vector2(124f, 44f);
        }

        private static RectOffset DesiredPadding(
            HorizontalOrVerticalLayoutGroup group, string asset)
        {
            string name = group.gameObject.name;
            if (name == "Content" && asset.Contains("TaskLibraryPanel"))
                return new RectOffset(16, 16, 12, 12);
            if (name.Contains("Navigation") || name.Contains("Actions"))
                return new RectOffset(12, 12, 8, 8);
            return group.padding;
        }

        private static void PlanProfile(
            NorscopeDesignProfile profile, Plan plan, bool apply)
        {
            ChangeProfileColor(profile, "colors.surfacePrimary",
                profile.colors.surfacePrimary, Panel,
                value => profile.colors.surfacePrimary = value, plan, apply);
            ChangeProfileColor(profile, "colors.surfaceElevated",
                profile.colors.surfaceElevated, Elevated,
                value => profile.colors.surfaceElevated = value, plan, apply);
            ChangeProfileColor(profile, "colors.surfaceOverlay",
                profile.colors.surfaceOverlay, Card,
                value => profile.colors.surfaceOverlay = value, plan, apply);
            ChangeProfileColor(profile, "colors.borderSubtle",
                profile.colors.borderSubtle, Border,
                value => profile.colors.borderSubtle = value, plan, apply);
            ChangeProfileColor(profile, "colors.accentPrimary",
                profile.colors.accentPrimary, Accent,
                value => profile.colors.accentPrimary = value, plan, apply);
            ChangeProfileColor(profile, "colors.textPrimary",
                profile.colors.textPrimary, PrimaryText,
                value => profile.colors.textPrimary = value, plan, apply);
            ChangeProfileColor(profile, "colors.textSecondary",
                profile.colors.textSecondary, SecondaryText,
                value => profile.colors.textSecondary = value, plan, apply);
            ChangeProfileFloat(profile, "typography.panelTitleSize",
                profile.typography.panelTitleSize, 26f,
                value => profile.typography.panelTitleSize = value, plan, apply);
            ChangeProfileFloat(profile, "typography.sectionTitleSize",
                profile.typography.sectionTitleSize, 21f,
                value => profile.typography.sectionTitleSize = value, plan, apply);
            ChangeProfileFloat(profile, "typography.bodySize",
                profile.typography.bodySize, 14.5f,
                value => profile.typography.bodySize = value, plan, apply);
            ChangeProfileFloat(profile, "typography.metadataSize",
                profile.typography.metadataSize, 12f,
                value => profile.typography.metadataSize = value, plan, apply);
            ChangeProfileFloat(profile, "panels.edgeGlowStrength",
                profile.panels.edgeGlowStrength, 0.06f,
                value => profile.panels.edgeGlowStrength = value, plan, apply);
            ChangeProfileFloat(profile, "composition.panelPadding",
                profile.composition.panelPadding, 16f,
                value => profile.composition.panelPadding = value, plan, apply);
            ChangeProfileFloat(profile, "composition.sectionSpacing",
                profile.composition.sectionSpacing, 12f,
                value => profile.composition.sectionSpacing = value, plan, apply);
            ChangeProfileFloat(profile, "composition.titleToContentSpacing",
                profile.composition.titleToContentSpacing, 16f,
                value => profile.composition.titleToContentSpacing = value, plan, apply);
            ChangeProfileFloat(profile, "composition.cardPadding",
                profile.composition.cardPadding, 14f,
                value => profile.composition.cardPadding = value, plan, apply);
            ChangeProfileFloat(profile, "composition.cardSpacing",
                profile.composition.cardSpacing, 12f,
                value => profile.composition.cardSpacing = value, plan, apply);
            ChangeProfileFloat(profile, "composition.iconToTextSpacing",
                profile.composition.iconToTextSpacing, 12f,
                value => profile.composition.iconToTextSpacing = value, plan, apply);
            ChangeProfileFloat(profile, "composition.taskCardHeight",
                profile.composition.taskCardHeight, 64f,
                value => profile.composition.taskCardHeight = value, plan, apply);
            ChangeProfileFloat(profile, "composition.buttonHeight",
                profile.composition.buttonHeight, 44f,
                value => profile.composition.buttonHeight = value, plan, apply);
            PlanInteractionSet(profile.cards.homeCard, "cards.homeCard",
                Card, plan, apply);
            PlanInteractionSet(profile.cards.taskCard, "cards.taskCard",
                Card, plan, apply);
            PlanInteractionSet(profile.buttons.standard, "buttons.standard",
                Elevated, plan, apply);
            PlanInteractionSet(profile.buttons.navigation, "buttons.navigation",
                Elevated, plan, apply);
            PlanInteractionSet(profile.buttons.primary, "buttons.primary",
                Accent, plan, apply);
            if (profile.schemaVersion != 3)
            {
                plan.Add(ProfilePath, "<asset>", "schemaVersion",
                    profile.schemaVersion.ToString(), "3", "profile");
                if (apply) profile.schemaVersion = 3;
            }
        }

        private static void PlanInteractionSet(
            InteractionStateSet set, string path, Color idleSurface,
            Plan plan, bool apply)
        {
            PlanInteractionState(set.idle, path + ".idle", idleSurface, Border,
                0.012f, 0.10f, 1f, plan, apply);
            PlanInteractionState(set.hovered, path + ".hovered", Accent, Accent,
                0.028f, 0.18f, 1.008f, plan, apply);
            PlanInteractionState(set.pressed, path + ".pressed", Accent, Accent,
                0.045f, 0.26f, 0.985f, plan, apply);
            PlanInteractionState(set.selected, path + ".selected", Accent, Accent,
                0.035f, 0.23f, 1f, plan, apply);
            PlanInteractionState(set.disabled, path + ".disabled",
                new Color(0.25f, 0.28f, 0.32f, 1f),
                new Color(0.25f, 0.28f, 0.32f, 1f),
                0.008f, 0.04f, 1f, plan, apply);
        }

        private static void PlanInteractionState(
            InteractionStateStyle state, string path, Color surface, Color edge,
            float surfaceOpacity, float edgeOpacity, float scale,
            Plan plan, bool apply)
        {
            ChangeProfileColor(state, path + ".surfaceColor",
                state.surfaceColor, surface,
                value => state.surfaceColor = value, plan, apply);
            ChangeProfileColor(state, path + ".edgeColor",
                state.edgeColor, edge,
                value => state.edgeColor = value, plan, apply);
            ChangeProfileFloat(state, path + ".surfaceOpacity",
                state.surfaceOpacity, surfaceOpacity,
                value => state.surfaceOpacity = value, plan, apply);
            ChangeProfileFloat(state, path + ".edgeOpacity",
                state.edgeOpacity, edgeOpacity,
                value => state.edgeOpacity = value, plan, apply);
            ChangeProfileFloat(state, path + ".visualScale",
                state.visualScale, scale,
                value => state.visualScale = value, plan, apply);
        }

        private static void PlanTheme(NorscopeTheme theme, Plan plan, bool apply)
        {
            ChangeProfileColor(theme, "panelBackground", theme.panelBackground, Panel,
                value => theme.panelBackground = value, plan, apply, ThemePath);
            ChangeProfileColor(theme, "elevatedPanelBackground",
                theme.elevatedPanelBackground, Card,
                value => theme.elevatedPanelBackground = value, plan, apply, ThemePath);
            ChangeProfileColor(theme, "primaryAccent", theme.primaryAccent, Accent,
                value => theme.primaryAccent = value, plan, apply, ThemePath);
            ChangeProfileColor(theme, "primaryText", theme.primaryText, PrimaryText,
                value => theme.primaryText = value, plan, apply, ThemePath);
            ChangeProfileColor(theme, "secondaryText", theme.secondaryText, SecondaryText,
                value => theme.secondaryText = value, plan, apply, ThemePath);
            ChangeProfileColor(theme, "border", theme.border, Border,
                value => theme.border = value, plan, apply, ThemePath);
            ChangeProfileFloat(theme, "titleSize", theme.titleSize, 26f,
                value => theme.titleSize = value, plan, apply, ThemePath);
            // NorscopePanelRoot and legacy HomePanelCard both consume headingSize.
            // A strong 21 pt compatibility value keeps runtime hierarchy apparent;
            // authored prefab-specific titles remain more precise in Prefab Mode.
            ChangeProfileFloat(theme, "headingSize", theme.headingSize, 21f,
                value => theme.headingSize = value, plan, apply, ThemePath);
            ChangeProfileFloat(theme, "bodySize", theme.bodySize, 14.5f,
                value => theme.bodySize = value, plan, apply, ThemePath);
            ChangeProfileFloat(theme, "captionSize", theme.captionSize, 12f,
                value => theme.captionSize = value, plan, apply, ThemePath);
        }

        private static bool Validate(out string report)
        {
            var lines = new StringBuilder();
            bool ok = true;
            void Check(bool condition, string label)
            {
                lines.AppendLine((condition ? "PASS  " : "FAIL  ") + label);
                if (!condition) ok = false;
            }

            Check(D21VisualBaseline.Exists, "Pre-D2.1 visual backup manifest exists");
            Check(D2VisualBaseline.Compare(out string functional),
                "Functional hashes, hierarchy, controls, Button targets, listeners, " +
                "sprites, navigation and Back relationships are unchanged");
            if (!string.IsNullOrEmpty(functional) &&
                !functional.StartsWith("Functional bindings match"))
                lines.AppendLine(functional);

            NorscopeDesignProfile profile =
                AssetDatabase.LoadAssetAtPath<NorscopeDesignProfile>(ProfilePath);
            NorscopeTheme theme =
                AssetDatabase.LoadAssetAtPath<NorscopeTheme>(ThemePath);
            Check(profile != null && profile.schemaVersion >= 3,
                "D2.1 profile schema is applied");
            Check(profile != null &&
                  ColorDistance(profile.colors.surfacePrimary,
                      profile.colors.surfaceElevated) > 0.045f &&
                  ColorDistance(profile.colors.surfaceElevated,
                      profile.colors.surfaceOverlay) > 0.035f,
                "Panel, elevated and card surfaces are materially distinct");
            Check(profile != null &&
                  profile.typography.panelTitleSize -
                  profile.typography.bodySize >= 10f &&
                  profile.typography.sectionTitleSize >
                  profile.typography.cardTitleSize,
                "Typography hierarchy is materially distinct");
            Check(profile != null && theme != null &&
                  Approximately(theme.panelBackground, profile.colors.surfacePrimary) &&
                  Approximately(theme.primaryAccent, profile.colors.accentPrimary) &&
                  Approximately(theme.primaryText, profile.colors.textPrimary),
                "Legacy theme is synchronized from the authoritative profile");
            Check(ProfileDiffersMateriallyFromBaseline(profile),
                "Before/after authoritative design tokens differ materially");
            Check(AllDecorativeGraphicsIgnoreRaycasts(),
                "All D1 decorative overlay graphics ignore raycasts");
            Check(NoDuplicateD1Visuals(),
                "No duplicate D1 visual components exist");
            Check(PrefabPresentationIsAuthored(profile, theme),
                "Prefab Mode contains authored premium surface and typography values");
            Check(TaskListSpacingIsAuthored(),
                "Task Library row spacing is authored on its VerticalLayoutGroup");
            Check(NoRuntimeBusinessLogicAdded(),
                "D2.1 introduced no runtime source or business logic");
            Check(NoCustomImageMaterials(),
                "No new material, shader or expensive Image treatment was introduced");

            report = lines.ToString();
            return ok;
        }

        private static bool PrefabPresentationIsAuthored(
            NorscopeDesignProfile profile, NorscopeTheme theme)
        {
            if (profile == null || theme == null) return false;
            GameObject home = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Norscope/UI/Panels/HomePanel.prefab");
            GameObject task = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Norscope/UI/Controls/TaskCard.prefab");
            if (home == null || task == null) return false;
            TMP_Text homeTitle = home.GetComponentsInChildren<TMP_Text>(true)
                .FirstOrDefault(text => text.gameObject.name == "Title");
            Image taskBackground = task.GetComponentsInChildren<Image>(true)
                .FirstOrDefault(image => image.gameObject.name == "Background");
            // NorscopePanelRoot.OnValidate intentionally writes the derived legacy
            // heading token into authored prefabs. That compatibility token is 21 pt
            // because HomePanelCard also consumes it; using the profile's 26 pt panel
            // title here would make every Home card heading 26 pt at runtime.
            bool authoredHierarchy =
                homeTitle != null &&
                homeTitle.fontSize >= theme.headingSize - 0.01f &&
                homeTitle.fontSize - theme.bodySize >= 6f &&
                Approximately(homeTitle.color, theme.primaryText);
            bool authoredCardSurface =
                taskBackground != null &&
                ColorDistance(taskBackground.color,
                    profile.colors.surfacePrimary) > 0.04f &&
                ColorDistance(taskBackground.color,
                    profile.colors.surfaceElevated) > 0.015f;
            return authoredHierarchy &&
                   authoredCardSurface;
        }

        private static bool TaskListSpacingIsAuthored()
        {
            GameObject panel = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Norscope/UI/Panels/TaskLibraryPanel.prefab");
            if (panel == null) return false;
            VerticalLayoutGroup content = panel
                .GetComponentsInChildren<VerticalLayoutGroup>(true)
                .FirstOrDefault(group => group.gameObject.name == "Content");
            return content != null && content.spacing >= 11.9f;
        }

        private static bool AllDecorativeGraphicsIgnoreRaycasts()
        {
            GameObject overlay = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Norscope/DesignSystem/Prefabs/NorscopeInteractionVisualLayer.prefab");
            return overlay != null &&
                   overlay.GetComponentsInChildren<Graphic>(true)
                       .All(graphic => !graphic.raycastTarget);
        }

        private static bool NoDuplicateD1Visuals()
        {
            foreach (string path in D21VisualBaseline.VisualAssets
                         .Where(value => value.EndsWith(".prefab")))
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) return false;
                foreach (Transform transform in
                         prefab.GetComponentsInChildren<Transform>(true))
                {
                    if (transform.GetComponents<NorscopeInteractionVisual>().Length > 1)
                        return false;
                }
            }
            return true;
        }

        private static bool NoRuntimeBusinessLogicAdded()
        {
            return D2VisualBaseline.Compare(out _);
        }

        private static bool ProfileDiffersMateriallyFromBaseline(
            NorscopeDesignProfile current)
        {
            if (current == null) return false;
            string backupPath = D21VisualBaseline.GetBackupFor(ProfilePath);
            NorscopeDesignProfile before =
                AssetDatabase.LoadAssetAtPath<NorscopeDesignProfile>(backupPath);
            return before != null &&
                   ColorDistance(before.colors.surfacePrimary,
                       current.colors.surfacePrimary) > 0.02f &&
                   ColorDistance(before.colors.surfaceElevated,
                       current.colors.surfaceElevated) > 0.02f &&
                   Mathf.Abs(before.typography.panelTitleSize -
                             current.typography.panelTitleSize) >= 1f;
        }

        private static bool NoCustomImageMaterials()
        {
            foreach (string path in D21VisualBaseline.VisualAssets
                         .Where(value => value.EndsWith(".prefab")))
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) return false;
                if (prefab.GetComponentsInChildren<Image>(true).Any(image =>
                        image.material != null &&
                        image.material != Graphic.defaultGraphicMaterial))
                    return false;
            }
            return true;
        }

        private static bool ChangeColor(
            string asset, string path, string property, Color oldValue,
            Color newValue, string category, Action<Color> setter,
            Plan plan, bool apply)
        {
            if (Approximately(oldValue, newValue)) return false;
            plan.Add(asset, path, property, ColorText(oldValue), ColorText(newValue),
                property.Contains("Border") || path.Contains("Border")
                    ? "border" : category);
            if (apply) setter(newValue);
            return true;
        }

        private static bool ChangeFloat(
            string asset, string path, string property, float oldValue,
            float newValue, string category, Action<float> setter,
            Plan plan, bool apply)
        {
            if (Mathf.Approximately(oldValue, newValue)) return false;
            plan.Add(asset, path, property, oldValue.ToString("0.###"),
                newValue.ToString("0.###"), category);
            if (apply) setter(newValue);
            return true;
        }

        private static bool ChangeVector2(
            string asset, string path, string property, Vector2 oldValue,
            Vector2 newValue, string category, Action<Vector2> setter,
            Plan plan, bool apply)
        {
            if ((oldValue - newValue).sqrMagnitude < 0.000001f) return false;
            plan.Add(asset, path, property,
                "(" + oldValue.x.ToString("0.###") + ", " +
                oldValue.y.ToString("0.###") + ")",
                "(" + newValue.x.ToString("0.###") + ", " +
                newValue.y.ToString("0.###") + ")", category);
            if (apply) setter(newValue);
            return true;
        }

        private static bool ChangeVector4(
            string asset, string path, string property, Vector4 oldValue,
            Vector4 newValue, string category, Action<Vector4> setter,
            Plan plan, bool apply)
        {
            if ((oldValue - newValue).sqrMagnitude < 0.000001f) return false;
            plan.Add(asset, path, property,
                Vector4Text(oldValue), Vector4Text(newValue), category);
            if (apply) setter(newValue);
            return true;
        }

        private static void ChangeProfileColor<T>(
            T target, string property, Color oldValue, Color newValue,
            Action<Color> setter, Plan plan, bool apply,
            string asset = ProfilePath)
        {
            ChangeColor(asset, "<asset>", property, oldValue, newValue,
                "profile", setter, plan, apply);
        }

        private static void ChangeProfileFloat<T>(
            T target, string property, float oldValue, float newValue,
            Action<float> setter, Plan plan, bool apply,
            string asset = ProfilePath)
        {
            ChangeFloat(asset, "<asset>", property, oldValue, newValue,
                "profile", setter, plan, apply);
        }

        private static string PathOf(Transform target, Transform root)
        {
            var names = new Stack<string>();
            Transform current = target;
            while (current != null)
            {
                names.Push(current.name);
                if (current == root) break;
                current = current.parent;
            }
            return string.Join("/", names);
        }

        private static bool IsNestedPrefabObject(GameObject loadedRoot, GameObject target)
        {
            GameObject nearest = PrefabUtility.GetNearestPrefabInstanceRoot(target);
            return nearest != null && nearest != loadedRoot;
        }

        private static Color WithAlpha(Color value, float alpha)
        {
            value.a = alpha;
            return value;
        }

        private static bool Approximately(Color a, Color b)
        {
            return ColorDistance(a, b) < 0.0002f &&
                   Mathf.Abs(a.a - b.a) < 0.0002f;
        }

        private static float ColorDistance(Color a, Color b)
        {
            float r = a.r - b.r;
            float g = a.g - b.g;
            float bl = a.b - b.b;
            return Mathf.Sqrt(r * r + g * g + bl * bl);
        }

        private static string ColorText(Color value)
        {
            return "(" + value.r.ToString("0.###") + ", " +
                   value.g.ToString("0.###") + ", " +
                   value.b.ToString("0.###") + ", " +
                   value.a.ToString("0.###") + ")";
        }

        private static bool SamePadding(RectOffset a, RectOffset b)
        {
            return a != null && b != null &&
                   a.left == b.left && a.right == b.right &&
                   a.top == b.top && a.bottom == b.bottom;
        }

        private static string PaddingText(RectOffset value)
        {
            if (value == null) return "<null>";
            return "L" + value.left + " R" + value.right +
                   " T" + value.top + " B" + value.bottom;
        }

        private static string Vector4Text(Vector4 value)
        {
            return "(" + value.x.ToString("0.###") + ", " +
                   value.y.ToString("0.###") + ", " +
                   value.z.ToString("0.###") + ", " +
                   value.w.ToString("0.###") + ")";
        }

        private static string Vector2Text(Vector2 value)
        {
            return "(" + value.x.ToString("0.###") + ", " +
                   value.y.ToString("0.###") + ")";
        }

        private static string Summary(string report)
        {
            string[] lines = report.Split('\n');
            return string.Join("\n", lines.Take(6));
        }
    }
}
