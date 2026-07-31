using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Norscope.DesignSystem;
using Norscope.Navigation;
using Norscope.TaskLibrary;
using Norscope.UI;

namespace Norscope.Editor
{
    /// <summary>
    /// Phase D2 changes presentation properties only. It never adds/removes/reparents objects
    /// and never edits Buttons, events, navigation, layout, RectTransforms, sprites or runtime.
    /// </summary>
    public static class PhaseD2PremiumVisualConfigurator
    {
        private const string ProfilePath =
            "Assets/Norscope/DesignSystem/Profiles/NorscopeDesignProfile.asset";
        private const string ThemePath =
            "Assets/Norscope/UI/DesignSystem/Themes/NorscopeTheme.asset";

        [MenuItem("Norscope/HoloLens/Phase D2/1) Capture Pre-D2 Baseline and Backups")]
        public static void CaptureMenu()
        {
            bool ok = D2VisualBaseline.CaptureAndBackUp(out string report);
            Debug.Log((ok ? "[Norscope D2] Baseline ready.\n" :
                "[Norscope D2] Baseline capture FAILED.\n") + report);
            EditorUtility.DisplayDialog(
                "Phase D2 baseline", (ok ? "READY\n\n" : "FAILED\n\n") + report, "OK");
        }

        [MenuItem("Norscope/HoloLens/Phase D2/2) Dry-Run Premium Visual Update")]
        public static void DryRunMenu()
        {
            bool ok = Preflight(out string report);
            Debug.Log((ok ? "[Norscope D2] Dry run READY\n" :
                "[Norscope D2] Dry run BLOCKED\n") + report);
            EditorUtility.DisplayDialog(
                "D2 Dry Run", (ok ? "READY\n\n" : "BLOCKED\n\n") + report, "OK");
        }

        [MenuItem("Norscope/HoloLens/Phase D2/3) Apply Premium Visual Language")]
        public static void ApplyMenu()
        {
            if (!Preflight(out string report))
            {
                EditorUtility.DisplayDialog("D2 blocked", report, "OK");
                return;
            }

            NorscopeDesignProfile profile =
                AssetDatabase.LoadAssetAtPath<NorscopeDesignProfile>(ProfilePath);
            NorscopeTheme theme =
                AssetDatabase.LoadAssetAtPath<NorscopeTheme>(ThemePath);
            if (profile == null || theme == null)
                throw new InvalidOperationException("D1 profile or compatibility theme is missing.");

            // One-time premium preset. Once schema 2 is established, Inspector edits win.
            if (profile.schemaVersion < 2)
            {
                ApplyPremiumPreset(profile);
                profile.schemaVersion = 2;
                EditorUtility.SetDirty(profile);
            }

            DeriveLegacyTheme(profile, theme);
            EditorUtility.SetDirty(theme);
            foreach (string path in D2VisualBaseline.TargetPrefabs)
                ApplyProfileToPrefab(path, profile);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Norscope D2] Premium visual language applied. " +
                      "No hierarchy, binding, navigation or runtime source was changed.");
        }

        [MenuItem("Norscope/HoloLens/Phase D2/4) Validate Premium Visual Language")]
        public static void ValidateMenu()
        {
            bool ok = Validate(out string report);
            Debug.Log((ok ? "[Norscope D2] Validation PASSED\n" :
                "[Norscope D2] Validation FAILED\n") + report);
            EditorUtility.DisplayDialog(
                "Validate D2", (ok ? "PASSED\n\n" : "FAILED\n\n") + report, "OK");
        }

        [MenuItem("Norscope/HoloLens/Phase D2/5) Restore Pre-D2 Visuals")]
        public static void RestoreMenu()
        {
            if (!EditorUtility.DisplayDialog(
                    "Restore pre-D2 visuals",
                    "Restore only the assets recorded by the immutable D2 backup manifest?",
                    "Restore",
                    "Cancel"))
                return;

            bool ok = D2VisualBaseline.Restore(out string report);
            AssetDatabase.Refresh();
            Debug.Log((ok ? "[Norscope D2] Restore succeeded\n" :
                "[Norscope D2] Restore failed\n") + report);
        }

        private static bool Preflight(out string report)
        {
            if (!File.Exists(D2VisualBaseline.BaselinePath))
            {
                report = "Run 1) Capture Pre-D2 Baseline and Backups first.";
                return false;
            }
            if (!File.Exists(ProfilePath) || !File.Exists(ThemePath))
            {
                report = "NorscopeDesignProfile or compatibility theme is missing.";
                return false;
            }
            if (!D2VisualBaseline.Compare(out report)) return false;
            report += "\n\nWill update presentation only in:\n" +
                      string.Join("\n", D2VisualBaseline.TargetPrefabs);
            return true;
        }

        private static bool Validate(out string report)
        {
            var builder = new System.Text.StringBuilder();
            bool ok = true;
            void Check(bool condition, string label)
            {
                builder.AppendLine((condition ? "PASS  " : "FAIL  ") + label);
                if (!condition) ok = false;
            }

            NorscopeDesignProfile profile =
                AssetDatabase.LoadAssetAtPath<NorscopeDesignProfile>(ProfilePath);
            NorscopeTheme theme =
                AssetDatabase.LoadAssetAtPath<NorscopeTheme>(ThemePath);
            Check(profile != null && profile.schemaVersion >= 2,
                "Authoritative D1 profile contains the D2 premium preset");
            Check(profile != null &&
                  profile.panels.edgeGlowStrength <= 0.1f &&
                  profile.cards.homeCard.hovered.edgeOpacity <= 0.2f &&
                  profile.cards.taskCard.hovered.edgeOpacity <= 0.2f,
                "Hover and panel glow remain restrained");
            Check(profile != null &&
                  !profile.features.enableHighlightPulse &&
                  !profile.features.enableScanEffect,
                "Pulse and scan remain disabled for HoloLens");
            Check(profile != null && ThemeMatchesProfile(theme, profile),
                "Legacy NorscopeTheme is derived from the authoritative profile");

            bool baselineMatches =
                D2VisualBaseline.Compare(out string baselineReport);
            Check(baselineMatches,
                "Hierarchy, Buttons, listeners, target graphics, sprites, Back, " +
                "navigation and functional source hashes are unchanged");
            if (!baselineMatches) builder.AppendLine(baselineReport);
            Check(NoCustomImageMaterials(),
                "No custom Image material or shader was introduced");
            Check(AllTargetsExist(), "All D2 target prefabs remain present");

            report = builder.ToString();
            return ok;
        }

        private static void ApplyPremiumPreset(NorscopeDesignProfile profile)
        {
            profile.colors.surfacePrimary =
                new Color(0.055f, 0.071f, 0.094f, 0.94f);
            profile.colors.surfaceElevated =
                new Color(0.082f, 0.106f, 0.137f, 0.97f);
            profile.colors.surfaceOverlay =
                new Color(0.11f, 0.137f, 0.173f, 0.84f);
            profile.colors.borderSubtle =
                new Color(0.22f, 0.267f, 0.325f, 0.72f);
            profile.colors.borderStrong =
                new Color(0.30f, 0.76f, 0.84f, 1f);
            profile.colors.textPrimary =
                new Color(0.945f, 0.957f, 0.969f, 1f);
            profile.colors.textSecondary =
                new Color(0.65f, 0.69f, 0.75f, 1f);
            profile.colors.textDisabled =
                new Color(0.39f, 0.43f, 0.49f, 1f);
            profile.colors.accentPrimary =
                new Color(0.30f, 0.76f, 0.84f, 1f);
            profile.colors.accentSecondary =
                new Color(0.35f, 0.55f, 0.82f, 1f);
            profile.colors.information = profile.colors.accentSecondary;
            profile.colors.success =
                new Color(0.31f, 0.72f, 0.48f, 1f);
            profile.colors.warning =
                new Color(0.91f, 0.62f, 0.22f, 1f);
            profile.colors.error =
                new Color(0.86f, 0.34f, 0.35f, 1f);
            profile.colors.verification = profile.colors.success;
            profile.colors.selection = profile.colors.accentPrimary;

            profile.panels.acrylicStrength = 0.12f;
            profile.panels.transparency = 0.06f;
            profile.panels.cornerRadius = 12f;
            profile.panels.borderThickness = 1f;
            profile.panels.shadowColor = new Color(0f, 0f, 0f, 0.28f);
            profile.panels.shadowStrength = 0.12f;
            profile.panels.edgeGlowColor = profile.colors.accentPrimary;
            profile.panels.edgeGlowStrength = 0.08f;
            profile.panels.depthSeparation = 0.0015f;

            ConfigureStates(
                profile.buttons.standard,
                profile.colors.surfaceElevated,
                profile.colors.borderSubtle,
                profile.colors.accentPrimary,
                profile.colors.textDisabled,
                profile.colors.success);
            ConfigureStates(
                profile.buttons.navigation,
                profile.colors.surfaceElevated,
                profile.colors.borderSubtle,
                profile.colors.accentPrimary,
                profile.colors.textDisabled,
                profile.colors.success);
            ConfigureStates(
                profile.buttons.primary,
                profile.colors.accentPrimary,
                profile.colors.accentPrimary,
                profile.colors.textPrimary,
                profile.colors.textDisabled,
                profile.colors.success);
            ConfigureStates(
                profile.cards.homeCard,
                profile.colors.surfaceElevated,
                profile.colors.borderSubtle,
                profile.colors.accentPrimary,
                profile.colors.textDisabled,
                profile.colors.success);
            ConfigureStates(
                profile.cards.taskCard,
                profile.colors.surfaceElevated,
                profile.colors.borderSubtle,
                profile.colors.accentPrimary,
                profile.colors.textDisabled,
                profile.colors.success);
            ConfigureStates(
                profile.cards.reportCard,
                profile.colors.surfaceElevated,
                profile.colors.borderSubtle,
                profile.colors.accentSecondary,
                profile.colors.textDisabled,
                profile.colors.success);

            profile.typography.displaySize = 28f;
            profile.typography.panelTitleSize = 24f;
            profile.typography.sectionTitleSize = 20f;
            profile.typography.cardTitleSize = 18f;
            profile.typography.bodySize = 14f;
            profile.typography.captionSize = 11.5f;
            profile.typography.metadataSize = 11.5f;
            profile.typography.labelSize = 14f;
            profile.typography.lineSpacing = 1f;
            profile.typography.paragraphSpacing = 8f;
            profile.typography.characterSpacing = 0.3f;
            profile.typography.titleWeight = FontWeight.SemiBold;
            profile.typography.bodyWeight = FontWeight.Regular;

            profile.motion.hoverInDuration = 0.14f;
            profile.motion.hoverOutDuration = 0.18f;
            profile.motion.pressDuration = 0.08f;
            profile.motion.releaseDuration = 0.14f;
            profile.motion.selectionDuration = 0.16f;
            profile.motion.verificationDuration = 0.26f;
            profile.motion.completionDuration = 0.22f;
            profile.motion.fadeDuration = 0.18f;
            profile.motion.slideDuration = 0.20f;
        }

        private static void ConfigureStates(
            InteractionStateSet states, Color surface, Color border,
            Color accent, Color disabled, Color success)
        {
            SetState(states.idle, surface, border, 0f, 0.07f, 0.18f, 1f);
            SetState(states.hovered, accent, accent, 0.022f, 0.16f, 0.28f, 1.005f);
            SetState(states.pressed, accent, accent, 0.04f, 0.24f, 0.36f, 0.985f);
            SetState(states.selected, accent, accent, 0.035f, 0.22f, 0.32f, 1f);
            SetState(states.disabled, disabled, disabled, 0.01f, 0.05f, 0.12f, 1f);
            SetState(states.highlighted, accent, accent, 0.045f, 0.28f, 0.4f, 1f);
            SetState(states.verified, success, success, 0.04f, 0.25f, 0.36f, 1f);
            SetState(states.completed, success, success, 0.025f, 0.18f, 0.26f, 1f);
        }

        private static void SetState(
            InteractionStateStyle state, Color surface, Color edge,
            float surfaceOpacity, float edgeOpacity, float outline, float scale)
        {
            state.surfaceColor = surface;
            state.edgeColor = edge;
            state.surfaceOpacity = surfaceOpacity;
            state.edgeOpacity = edgeOpacity;
            state.outlineStrength = outline;
            state.luminanceStrength = 1f;
            state.visualScale = scale;
            state.depthOffset = 0f;
        }

        private static void DeriveLegacyTheme(
            NorscopeDesignProfile profile, NorscopeTheme theme)
        {
            theme.panelBackground = profile.colors.surfacePrimary;
            theme.elevatedPanelBackground = profile.colors.surfaceElevated;
            theme.primaryAccent = profile.colors.accentPrimary;
            theme.secondaryAccent = profile.colors.accentSecondary;
            theme.primaryText = profile.colors.textPrimary;
            theme.secondaryText = profile.colors.textSecondary;
            theme.border = profile.colors.borderSubtle;
            theme.hover = Color.Lerp(
                profile.colors.surfaceElevated,
                profile.colors.accentPrimary, 0.08f);
            theme.pressed = Color.Lerp(
                profile.colors.surfaceElevated,
                Color.black, 0.12f);
            theme.disabled = profile.colors.textDisabled;
            theme.success = profile.colors.success;
            theme.warning = profile.colors.warning;
            theme.error = profile.colors.error;
            theme.titleSize = profile.typography.panelTitleSize;
            theme.headingSize = profile.typography.cardTitleSize;
            theme.bodySize = profile.typography.bodySize;
            theme.captionSize = profile.typography.captionSize;
            theme.largeCornerRadius = profile.panels.cornerRadius;
            theme.mediumCornerRadius = Mathf.Min(10f, profile.panels.cornerRadius);
            theme.smallCornerRadius = Mathf.Min(7f, profile.panels.cornerRadius);
            theme.thinBorderWidth = profile.panels.borderThickness;
            theme.standardBorderWidth = profile.panels.borderThickness;
            theme.hoverScale = profile.cards.homeCard.hovered.visualScale;
            theme.pressedScale = profile.buttons.standard.pressed.visualScale;
            theme.transitionDuration = profile.motion.hoverInDuration;
            theme.disabledOpacity = 0.42f;
        }

        private static void ApplyProfileToPrefab(
            string path, NorscopeDesignProfile profile)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                bool changed = false;
                foreach (Image image in root.GetComponentsInChildren<Image>(true))
                    changed |= StyleImage(image, root, profile);
                foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
                    changed |= StyleText(text, profile);

                foreach (HomePanelCard card in
                         root.GetComponentsInChildren<HomePanelCard>(true))
                {
                    SerializedObject serialized = new SerializedObject(card);
                    changed |= SetFloat(serialized, "hoverScale",
                        profile.cards.homeCard.hovered.visualScale);
                    changed |= SetFloat(serialized, "hoverLiftMeters",
                        profile.panels.depthSeparation);
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }

                foreach (TaskCardView card in
                         root.GetComponentsInChildren<TaskCardView>(true))
                {
                    SerializedObject serialized = new SerializedObject(card);
                    changed |= SetColor(serialized, "normalBorderColor",
                        profile.colors.borderSubtle);
                    changed |= SetColor(serialized, "hoverBorderColor",
                        WithAlpha(profile.colors.accentPrimary, 0.42f));
                    changed |= SetColor(serialized, "pressedBorderColor",
                        WithAlpha(profile.colors.accentPrimary, 0.62f));
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }

                if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static bool StyleImage(
            Image image, GameObject prefabRoot, NorscopeDesignProfile profile)
        {
            string name = image.gameObject.name;
            Color? desired = null;
            if (image.gameObject == prefabRoot)
                desired = profile.colors.surfacePrimary;
            else if (name == "TitleBar")
                desired = profile.colors.surfaceElevated;
            else if (name == "Border")
                desired = profile.colors.borderSubtle;
            else if (name == "ProgressTrack")
                desired = WithAlpha(profile.colors.borderSubtle, 0.55f);
            else if (name == "ProgressFill")
                desired = profile.colors.accentPrimary;
            else if (name == "WarningSection")
                desired = WithAlpha(profile.colors.warning, 0.10f);
            else if (name == "VerificationSection")
                desired = WithAlpha(profile.colors.success, 0.09f);
            else if (name == "ConfirmationCard")
                desired = profile.colors.surfaceElevated;
            else if (name == "CancelConfirmationView")
                desired = new Color(0f, 0f, 0f, 0.58f);
            else if (name.EndsWith("Button", StringComparison.Ordinal))
                desired = ButtonColor(name, profile);
            else if (name == "Background")
                desired = profile.colors.surfaceElevated;

            if (!desired.HasValue || Approximately(image.color, desired.Value))
                return false;
            image.color = desired.Value;
            return true;
        }

        private static Color ButtonColor(
            string name, NorscopeDesignProfile profile)
        {
            if (name == "FinishButton" || name == "ReturnHomeButton")
                return profile.colors.accentPrimary;
            if (name == "ConfirmCancelButton")
                return WithAlpha(profile.colors.error, 0.90f);
            return profile.colors.surfaceElevated;
        }

        private static bool StyleText(
            TMP_Text text, NorscopeDesignProfile profile)
        {
            string name = text.gameObject.name;
            Color color = profile.colors.textSecondary;
            float size = profile.typography.bodySize;
            FontWeight weight = profile.typography.bodyWeight;

            if (name == "Title")
            {
                color = profile.colors.textPrimary;
                size = profile.typography.panelTitleSize;
                weight = profile.typography.titleWeight;
            }
            else if (name.Contains("Heading") || name == "StepTitle")
            {
                color = name.Contains("Completion")
                    ? profile.colors.success
                    : profile.colors.textPrimary;
                size = profile.typography.sectionTitleSize;
                weight = profile.typography.titleWeight;
            }
            else if (name.Contains("TaskTitle") || name == "TaskName")
            {
                color = profile.colors.textPrimary;
                size = profile.typography.cardTitleSize;
                weight = profile.typography.titleWeight;
            }
            else if (name == "Label")
            {
                string parentName =
                    text.transform.parent != null ? text.transform.parent.name : string.Empty;
                color = parentName == "FinishButton" ||
                        parentName == "ReturnHomeButton"
                    ? profile.colors.surfacePrimary
                    : profile.colors.textPrimary;
                size = profile.typography.labelSize;
                weight = FontWeight.Medium;
            }
            else if (name.Contains("Warning"))
                color = profile.colors.warning;
            else if (name.Contains("Verification"))
                color = profile.colors.success;
            else if (name.Contains("Counter") ||
                     name.Contains("Duration") ||
                     name.Contains("Difficulty") ||
                     name.Contains("Subtitle"))
                size = profile.typography.metadataSize;

            bool changed = false;
            if (text.font != profile.typography.primaryFont &&
                profile.typography.primaryFont != null)
            {
                text.font = profile.typography.primaryFont;
                changed = true;
            }
            if (!Mathf.Approximately(text.fontSize, size))
            {
                text.fontSize = size;
                changed = true;
            }
            if (!Approximately(text.color, color))
            {
                text.color = color;
                changed = true;
            }
            if (text.fontWeight != weight)
            {
                text.fontWeight = weight;
                changed = true;
            }
            return changed;
        }

        private static bool SetFloat(
            SerializedObject target, string name, float value)
        {
            SerializedProperty property = target.FindProperty(name);
            if (property == null || Mathf.Approximately(property.floatValue, value))
                return false;
            property.floatValue = value;
            return true;
        }

        private static bool SetColor(
            SerializedObject target, string name, Color value)
        {
            SerializedProperty property = target.FindProperty(name);
            if (property == null || Approximately(property.colorValue, value))
                return false;
            property.colorValue = value;
            return true;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        private static bool Approximately(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < 0.0001f &&
                   Mathf.Abs(a.g - b.g) < 0.0001f &&
                   Mathf.Abs(a.b - b.b) < 0.0001f &&
                   Mathf.Abs(a.a - b.a) < 0.0001f;
        }

        private static bool ThemeMatchesProfile(
            NorscopeTheme theme, NorscopeDesignProfile profile)
        {
            return theme != null &&
                   Approximately(theme.panelBackground, profile.colors.surfacePrimary) &&
                   Approximately(theme.elevatedPanelBackground, profile.colors.surfaceElevated) &&
                   Approximately(theme.primaryAccent, profile.colors.accentPrimary) &&
                   Approximately(theme.primaryText, profile.colors.textPrimary) &&
                   Approximately(theme.secondaryText, profile.colors.textSecondary);
        }

        private static bool NoCustomImageMaterials()
        {
            foreach (string path in D2VisualBaseline.TargetPrefabs)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) return false;
                foreach (Image image in prefab.GetComponentsInChildren<Image>(true))
                {
                    if (image.material != null &&
                        image.material != Graphic.defaultGraphicMaterial)
                        return false;
                }
            }
            return true;
        }

        private static bool AllTargetsExist()
        {
            foreach (string path in D2VisualBaseline.TargetPrefabs)
                if (!File.Exists(path)) return false;
            return true;
        }
    }
}
