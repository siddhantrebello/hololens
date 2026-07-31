using System;
using TMPro;
using UnityEngine;
using Norscope.UI;

namespace Norscope.DesignSystem
{
    [Serializable]
    public sealed class GlobalVisualFeatures
    {
        public bool enableHoverGlow = true;
        public bool enableMotion = true;
        public bool enableHighlightPulse;
        public bool enableScanEffect;
        public bool enablePanelShadows = true;
        public bool enableVerificationGlow = true;
        public bool enableFloatingLabels = true;
        public bool enableNotifications = true;
        public bool enableTransparency = true;
        public bool enableDepthResponse = true;
    }

    [Serializable]
    public sealed class SemanticColorTokens
    {
        public Color surfacePrimary;
        public Color surfaceElevated;
        public Color surfaceOverlay;
        public Color borderSubtle;
        public Color borderStrong;
        public Color textPrimary;
        public Color textSecondary;
        public Color textDisabled;
        public Color accentPrimary;
        public Color accentSecondary;
        public Color information;
        public Color success;
        public Color warning;
        public Color error;
        public Color verification;
        public Color selection;
    }

    [Serializable]
    public sealed class InteractionStateStyle
    {
        public Color surfaceColor;
        public Color edgeColor;
        [Range(0f, 1f)] public float surfaceOpacity;
        [Range(0f, 1f)] public float edgeOpacity;
        [Min(0f)] public float outlineStrength;
        [Min(0f)] public float luminanceStrength = 1f;
        [Range(0.98f, 1.02f)] public float visualScale = 1f;
        public float depthOffset;
    }

    [Serializable]
    public sealed class InteractionStateSet
    {
        public InteractionStateStyle idle = new InteractionStateStyle();
        public InteractionStateStyle hovered = new InteractionStateStyle();
        public InteractionStateStyle pressed = new InteractionStateStyle();
        public InteractionStateStyle selected = new InteractionStateStyle();
        public InteractionStateStyle disabled = new InteractionStateStyle();
        public InteractionStateStyle highlighted = new InteractionStateStyle();
        public InteractionStateStyle verified = new InteractionStateStyle();
        public InteractionStateStyle completed = new InteractionStateStyle();

        public InteractionStateStyle Get(NorscopeVisualState state)
        {
            switch (state)
            {
                case NorscopeVisualState.Hovered: return hovered;
                case NorscopeVisualState.Pressed: return pressed;
                case NorscopeVisualState.Selected: return selected;
                case NorscopeVisualState.Disabled: return disabled;
                case NorscopeVisualState.Highlighted: return highlighted;
                case NorscopeVisualState.Verified: return verified;
                case NorscopeVisualState.Completed: return completed;
                default: return idle;
            }
        }
    }

    [Serializable]
    public sealed class PanelStyleTokens
    {
        [Range(0f, 1f)] public float acrylicStrength;
        [Range(0f, 1f)] public float transparency;
        [Min(0f)] public float cornerRadius;
        [Min(0f)] public float borderThickness;
        public Color shadowColor;
        [Range(0f, 1f)] public float shadowStrength;
        public Color edgeGlowColor;
        [Range(0f, 2f)] public float edgeGlowStrength;
        [Min(0f)] public float depthSeparation;
    }

    [Serializable]
    public sealed class ButtonStyleTokens
    {
        public InteractionStateSet standard = new InteractionStateSet();
        public InteractionStateSet primary = new InteractionStateSet();
        public InteractionStateSet navigation = new InteractionStateSet();
    }

    [Serializable]
    public sealed class CardStyleTokens
    {
        public InteractionStateSet homeCard = new InteractionStateSet();
        public InteractionStateSet taskCard = new InteractionStateSet();
        public InteractionStateSet reportCard = new InteractionStateSet();
    }

    [Serializable]
    public sealed class TypographyTokens
    {
        public TMP_FontAsset primaryFont;
        public float displaySize;
        public float panelTitleSize;
        public float sectionTitleSize;
        public float cardTitleSize;
        public float bodySize;
        public float captionSize;
        public float metadataSize;
        public float labelSize;
        public float lineSpacing;
        public float paragraphSpacing;
        public float characterSpacing;
        public FontWeight titleWeight = FontWeight.SemiBold;
        public FontWeight bodyWeight = FontWeight.Regular;
    }

    [Serializable]
    public sealed class MotionTokens
    {
        [Min(0f)] public float hoverInDuration;
        [Min(0f)] public float hoverOutDuration;
        [Min(0f)] public float pressDuration;
        [Min(0f)] public float releaseDuration;
        [Min(0f)] public float selectionDuration;
        [Min(0f)] public float verificationDuration;
        [Min(0f)] public float completionDuration;
        [Min(0f)] public float fadeDuration;
        [Min(0f)] public float slideDuration;
        [Min(0f)] public float labelDelay;
        public AnimationCurve defaultEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        public AnimationCurve hoverEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        public AnimationCurve pressEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        public AnimationCurve verificationEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    }

    [Serializable]
    public sealed class CompositionTokens
    {
        [Header("Panel and sections")]
        public float panelPadding = 16f;
        public float sectionSpacing = 12f;
        public float titleToContentSpacing = 16f;

        [Header("Cards")]
        public float cardPadding = 14f;
        public float cardSpacing = 12f;
        public float iconToTextSpacing = 12f;
        public float taskCardHeight = 64f;

        [Header("Buttons")]
        public float buttonHeight = 44f;
        public float primaryButtonWidth = 124f;
        public float secondaryButtonWidth = 112f;
        public float navigationButtonWidth = 96f;
        public float closeButtonSize = 40f;

        [Header("Typography")]
        public Vector4 titleMargins = new Vector4(0f, 0f, 0f, 2f);
        public Vector4 bodyMargins = new Vector4(2f, 1f, 2f, 1f);
        public Vector4 metadataMargins = Vector4.zero;
    }

    [Serializable]
    public sealed class HighlightStyleTokens
    {
        public Color outlineColor;
        [Min(0f)] public float outlineStrength;
        public Color innerGlowColor;
        [Range(0f, 2f)] public float innerGlowStrength;
        public Color scanColor;
        [Min(0f)] public float scanDuration;
        [Min(0f)] public float scanWidth;
        [Range(0f, 0.1f)] public float pulseAmplitude;
        [Min(0f)] public float pulseDuration;
        public Color verificationColor;
        [Min(0f)] public float verificationRippleDuration;
        public Color completedColor;
        [Range(0f, 1f)] public float completedOpacity;
    }

    [Serializable]
    public sealed class FloatingLabelTokens
    {
        public TMP_FontAsset font;
        public float fontSize;
        public Color textColor;
        public Color backgroundColor;
        public Color borderColor;
        public Color leaderLineColor;
        [Min(0f)] public float leaderLineWidth;
        [Range(0f, 1f)] public float opacity;
        [Min(0f)] public float fadeInDuration;
        [Min(0f)] public float fadeOutDuration;
        [Min(0f)] public float distanceScale;
        [Min(0f)] public float maximumWidth;
    }

    [Serializable]
    public sealed class NotificationVariantTokens
    {
        public Color surfaceColor;
        public Color borderColor;
        public Color textColor;
        public Sprite optionalIcon;
        [Min(0f)] public float visibleDuration;
    }

    [Serializable]
    public sealed class NotificationTokens
    {
        public NotificationVariantTokens information = new NotificationVariantTokens();
        public NotificationVariantTokens success = new NotificationVariantTokens();
        public NotificationVariantTokens warning = new NotificationVariantTokens();
        public NotificationVariantTokens error = new NotificationVariantTokens();
    }

    [Serializable]
    public sealed class ArStyleTokens
    {
        public InteractionStateSet componentHighlight = new InteractionStateSet();
        public InteractionStateSet directionalArrow = new InteractionStateSet();
        public InteractionStateSet ghostComponent = new InteractionStateSet();
        public InteractionStateSet stepMarker = new InteractionStateSet();
        public InteractionStateSet safetyZone = new InteractionStateSet();
        public InteractionStateSet cadOverlay = new InteractionStateSet();
        public float maximumTransparentLayers;
        public float distanceFadeStart;
        public float distanceFadeEnd;
    }

    [Serializable]
    public sealed class PerformanceTokens
    {
        [Range(0, 4)] public int maximumOutlinePasses = 1;
        [Range(0, 4)] public int maximumTransparentOverlayLayers = 2;
        public bool allowPulseOnHoloLens;
        public bool allowScanOnHoloLens;
    }

    [CreateAssetMenu(
        fileName = "NorscopeDesignProfile",
        menuName = "Norscope/Design System/Norscope Design Profile")]
    public sealed class NorscopeDesignProfile : ScriptableObject
    {
        [Min(1)] public int schemaVersion = 1;

        [Header("Global Visual Toggles")]
        public GlobalVisualFeatures features = new GlobalVisualFeatures();

        [Header("Semantic Palette")]
        public SemanticColorTokens colors = new SemanticColorTokens();

        [Header("Panel Style")]
        public PanelStyleTokens panels = new PanelStyleTokens();

        [Header("Button Style")]
        public ButtonStyleTokens buttons = new ButtonStyleTokens();

        [Header("Card Style")]
        public CardStyleTokens cards = new CardStyleTokens();

        [Header("Typography")]
        public TypographyTokens typography = new TypographyTokens();

        [Header("Motion")]
        public MotionTokens motion = new MotionTokens();

        [Header("Composition")]
        public CompositionTokens composition = new CompositionTokens();

        [Header("Highlight Style")]
        public HighlightStyleTokens highlights = new HighlightStyleTokens();

        [Header("Floating Labels — Reserved")]
        public FloatingLabelTokens floatingLabels = new FloatingLabelTokens();

        [Header("Notifications — Reserved")]
        public NotificationTokens notifications = new NotificationTokens();

        [Header("Future AR Styles — Reserved")]
        public ArStyleTokens futureAr = new ArStyleTokens();

        [Header("HoloLens Performance")]
        public PerformanceTokens performance = new PerformanceTokens();

        [Header("Legacy Compatibility — derived asset; do not edit manually")]
        public NorscopeTheme legacyThemeCompatibility;

        public InteractionStateStyle GetInteractionStyle(
            NorscopeVisualRole role,
            NorscopeVisualState state)
        {
            switch (role)
            {
                case NorscopeVisualRole.HomeCard:
                    return cards.homeCard.Get(state);
                case NorscopeVisualRole.TaskCard:
                    return cards.taskCard.Get(state);
                case NorscopeVisualRole.ReportCard:
                    return cards.reportCard.Get(state);
                case NorscopeVisualRole.PrimaryButton:
                    return buttons.primary.Get(state);
                case NorscopeVisualRole.BackButton:
                case NorscopeVisualRole.CloseButton:
                    return buttons.navigation.Get(state);
                default:
                    return buttons.standard.Get(state);
            }
        }
    }
}
