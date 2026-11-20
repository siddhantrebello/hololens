using System.Collections;
using Norscope.DesignSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Norscope.AskAI
{
    /// <summary>
    /// Presentation-only Ask AI surface. Never transitions workflow state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AskAiView : MonoBehaviour
    {
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text hintText;
        [SerializeField] private TMP_Text secondaryHintText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private TMP_Text analysisLabel;
        [SerializeField] private TMP_Text statusPillText;
        [SerializeField] private Image statusPillBackground;
        [SerializeField] private Button primaryButton;
        [SerializeField] private TMP_Text primaryButtonLabel;
        [SerializeField] private Image microphoneImage;
        [SerializeField] private RectTransform microphoneRoot;
        [SerializeField] private RectTransform cameraRoot;
        [SerializeField] private CanvasGroup contentGroup;
        [SerializeField] private GameObject processingIndicator;
        [SerializeField] private float fadeDuration = 0.18f;
        [SerializeField] private float micPulseScale = 1.08f;
        [SerializeField] private float micPulseSeconds = 0.55f;

        private static readonly Color Accent = Hex("1EC8F5");
        private static readonly Color TextPri = Hex("EDF1F7");
        private static readonly Color TextSec = Hex("7A909F");
        private static readonly Color TextDim = Hex("364959");
        private static readonly Color Warning = Hex("E8A020");
        private static readonly Color Success = Hex("34C97A");

        private Coroutine _fadeRoutine;
        private Coroutine _pulseRoutine;
        private Vector3 _micRestScale = Vector3.one;
        private bool _micRestCaptured;

        public Button PrimaryButton => primaryButton;

        public void Configure(
            TMP_Text status,
            TMP_Text hint,
            TMP_Text secondaryHint,
            TMP_Text body,
            TMP_Text analysis,
            TMP_Text pillText,
            Image pillBackground,
            Button primary,
            TMP_Text primaryLabel,
            Image microphone,
            RectTransform micRoot,
            RectTransform cameraFrame,
            CanvasGroup content,
            GameObject processing)
        {
            statusText = status;
            hintText = hint;
            secondaryHintText = secondaryHint;
            bodyText = body;
            analysisLabel = analysis;
            statusPillText = pillText;
            statusPillBackground = pillBackground;
            primaryButton = primary;
            primaryButtonLabel = primaryLabel;
            microphoneImage = microphone;
            microphoneRoot = micRoot;
            cameraRoot = cameraFrame;
            contentGroup = content;
            processingIndicator = processing;
            CaptureMicRest();
        }

        private void Awake()
        {
            CaptureMicRest();
        }

        private void OnDisable()
        {
            StopPresentationRoutines();
            ResetMicScale();
        }

        private void OnDestroy()
        {
            StopPresentationRoutines();
        }

        public void SetBodyText(string body)
        {
            if (bodyText == null)
                return;

            bool hasBody = !string.IsNullOrEmpty(body);
            bodyText.gameObject.SetActive(hasBody);
            bodyText.text = body ?? string.Empty;
            SetInteractionCardVisible(hasBody, isAnswer: false);
        }

        private void SetInteractionCardVisible(bool visible, bool isAnswer)
        {
            if (bodyText == null)
                return;
            Transform stage = bodyText.transform.parent;
            Transform card = stage != null ? stage.parent : null;
            if (card == null || card.name != "InteractionCard")
                return;

            card.gameObject.SetActive(visible);
            if (!visible || !(card is RectTransform cardRt))
                return;

            // Listening transcript sits under StatusRow; Answer fills the stage area.
            float top = isAnswer ? 110f : 200f;
            float height = isAnswer ? 190f : 100f;
            cardRt.anchorMin = cardRt.anchorMax = new Vector2(0f, 1f);
            cardRt.pivot = new Vector2(0f, 1f);
            cardRt.anchoredPosition = new Vector2(24f, -top);
            cardRt.sizeDelta = new Vector2(432f, height);
        }

        private void SetStatusRowVisible(bool visible)
        {
            if (statusText == null)
                return;
            Transform column = statusText.transform.parent;
            Transform row = column != null ? column.parent : null;
            if (row != null && row.name == "StatusRow")
                row.gameObject.SetActive(visible);
        }

        public void Render(
            AskAiWorkflowState state,
            string status,
            string body,
            string buttonLabel,
            bool buttonEnabled,
            bool hintVisible,
            bool microphoneActive,
            bool processingActive,
            bool animate)
        {
            bool motion = animate && MotionEnabled();
            bool isAnswer = state == AskAiWorkflowState.Answer;
            bool isCapturing = state == AskAiWorkflowState.Capturing;
            bool isReady = state == AskAiWorkflowState.Ready;
            bool isError = state == AskAiWorkflowState.Error;

            if (statusText != null)
                statusText.text = status ?? string.Empty;

            if (hintText != null)
            {
                string hint = ResolveHint(state);
                bool showHint = !string.IsNullOrEmpty(hint) && !isAnswer;
                hintText.gameObject.SetActive(showHint);
                if (showHint)
                    hintText.text = hint;
            }

            if (secondaryHintText != null)
                secondaryHintText.gameObject.SetActive(false);

            if (analysisLabel != null)
                analysisLabel.gameObject.SetActive(isAnswer);

            bool hasBody = !string.IsNullOrEmpty(body);
            if (bodyText != null)
            {
                bodyText.gameObject.SetActive(hasBody);
                bodyText.text = body ?? string.Empty;
                bodyText.alignment = TextAlignmentOptions.TopLeft;
                bodyText.color = isAnswer ? TextPri : TextSec;
                bodyText.enableWordWrapping = true;
                bodyText.overflowMode = isAnswer
                    ? TextOverflowModes.Ellipsis
                    : TextOverflowModes.Overflow;
                LayoutElement bodyLayout = bodyText.GetComponent<LayoutElement>();
                if (bodyLayout != null)
                {
                    bodyLayout.preferredHeight = isAnswer ? 160f : 48f;
                    bodyLayout.minHeight = isAnswer ? 80f : 0f;
                    bodyLayout.flexibleHeight = isAnswer ? 1f : 0f;
                }
            }

            // InteractionCard only when transcript/answer body is present (not empty Ready).
            SetInteractionCardVisible(hasBody, isAnswer);
            SetStatusRowVisible(!isAnswer);

            if (statusText != null)
                statusText.gameObject.SetActive(!isAnswer);

            if (primaryButtonLabel != null)
                primaryButtonLabel.text = buttonLabel ?? string.Empty;

            if (primaryButton != null)
                primaryButton.interactable = buttonEnabled;

            if (processingIndicator != null)
                processingIndicator.SetActive(processingActive);

            if (microphoneRoot != null)
                microphoneRoot.gameObject.SetActive(
                    !isAnswer && !isCapturing && !isError && !processingActive);

            if (cameraRoot != null)
                cameraRoot.gameObject.SetActive(isCapturing);

            ApplyStatusPill(state);
            ApplyMicrophoneVisual(microphoneActive, motion);
            FadeContent(motion);
        }

        public void SnapReadyPresentation()
        {
            StopPresentationRoutines();
            ResetMicScale();
            if (contentGroup != null)
            {
                contentGroup.alpha = 1f;
                contentGroup.interactable = true;
                contentGroup.blocksRaycasts = true;
            }
        }

        private void ApplyStatusPill(AskAiWorkflowState state)
        {
            if (statusPillText == null)
                return;

            Transform pillRoot = statusPillBackground != null
                ? statusPillBackground.transform
                : statusPillText.transform.parent;
            if (pillRoot != null)
                pillRoot.gameObject.SetActive(state != AskAiWorkflowState.Ready);

            string label;
            Color color;
            Color bg;
            switch (state)
            {
                case AskAiWorkflowState.Listening:
                    label = "Listening";
                    color = Accent;
                    bg = new Color(Accent.r, Accent.g, Accent.b, 0.12f);
                    break;
                case AskAiWorkflowState.Capturing:
                    label = "Capturing";
                    color = Accent;
                    bg = new Color(Accent.r, Accent.g, Accent.b, 0.12f);
                    break;
                case AskAiWorkflowState.Processing:
                    label = "Analyzing";
                    color = TextSec;
                    bg = new Color(TextSec.r, TextSec.g, TextSec.b, 0.12f);
                    break;
                case AskAiWorkflowState.Answer:
                    label = "Done";
                    color = Success;
                    bg = new Color(Success.r, Success.g, Success.b, 0.10f);
                    break;
                case AskAiWorkflowState.Error:
                    label = "Error";
                    color = Warning;
                    bg = new Color(Warning.r, Warning.g, Warning.b, 0.12f);
                    break;
                default:
                    label = "Ready";
                    color = TextDim;
                    bg = new Color(TextDim.r, TextDim.g, TextDim.b, 0.12f);
                    break;
            }

            statusPillText.text = label;
            statusPillText.color = color;
            if (statusPillBackground != null)
                statusPillBackground.color = bg;
        }

        private void FadeContent(bool motion)
        {
            if (contentGroup == null)
                return;

            if (!motion)
            {
                if (_fadeRoutine != null)
                {
                    StopCoroutine(_fadeRoutine);
                    _fadeRoutine = null;
                }

                contentGroup.alpha = 1f;
                return;
            }

            if (!isActiveAndEnabled)
            {
                contentGroup.alpha = 1f;
                return;
            }

            if (_fadeRoutine != null)
                StopCoroutine(_fadeRoutine);
            _fadeRoutine = StartCoroutine(FadeRoutine());
        }

        private IEnumerator FadeRoutine()
        {
            contentGroup.alpha = 0.35f;
            float duration = Mathf.Max(0.01f, fadeDuration);
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / duration);
                contentGroup.alpha = Mathf.Lerp(0.35f, 1f, u * u * (3f - 2f * u));
                yield return null;
            }

            contentGroup.alpha = 1f;
            _fadeRoutine = null;
        }

        private void ApplyMicrophoneVisual(bool active, bool motion)
        {
            CaptureMicRest();
            if (microphoneImage != null)
            {
                Color c = microphoneImage.color;
                c.a = active ? 1f : 0.85f;
                microphoneImage.color = c;
            }

            if (_pulseRoutine != null)
            {
                StopCoroutine(_pulseRoutine);
                _pulseRoutine = null;
            }

            if (!active || !motion || microphoneRoot == null ||
                !microphoneRoot.gameObject.activeSelf)
            {
                ResetMicScale();
                return;
            }

            if (!isActiveAndEnabled)
            {
                ResetMicScale();
                return;
            }

            _pulseRoutine = StartCoroutine(PulseRoutine());
        }

        private IEnumerator PulseRoutine()
        {
            while (true)
            {
                float half = Mathf.Max(0.05f, micPulseSeconds * 0.5f);
                yield return ScaleMic(_micRestScale, _micRestScale * micPulseScale, half);
                yield return ScaleMic(_micRestScale * micPulseScale, _micRestScale, half);
            }
        }

        private IEnumerator ScaleMic(Vector3 from, Vector3 to, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / duration);
                float e = u * u * (3f - 2f * u);
                microphoneRoot.localScale = Vector3.LerpUnclamped(from, to, e);
                yield return null;
            }

            microphoneRoot.localScale = to;
        }

        private void CaptureMicRest()
        {
            if (_micRestCaptured || microphoneRoot == null)
                return;
            _micRestScale = microphoneRoot.localScale;
            _micRestCaptured = true;
        }

        private void ResetMicScale()
        {
            if (microphoneRoot != null && _micRestCaptured)
                microphoneRoot.localScale = _micRestScale;
        }

        private void StopPresentationRoutines()
        {
            if (_fadeRoutine != null)
            {
                StopCoroutine(_fadeRoutine);
                _fadeRoutine = null;
            }

            if (_pulseRoutine != null)
            {
                StopCoroutine(_pulseRoutine);
                _pulseRoutine = null;
            }
        }

        private static string ResolveHint(AskAiWorkflowState state)
        {
            switch (state)
            {
                case AskAiWorkflowState.Ready:
                    return "Press Ask AI and speak naturally.\nNORSCOPE analyzes your voice and the scene.";
                case AskAiWorkflowState.Capturing:
                    return "Keep looking at the equipment.";
                case AskAiWorkflowState.Processing:
                    return "Reviewing your question and the scene.";
                case AskAiWorkflowState.Error:
                    return "Check the connection and try again.";
                default:
                    return string.Empty;
            }
        }

        private static bool MotionEnabled()
        {
            NorscopeDesignProvider provider = NorscopeDesignProvider.Instance;
            return provider == null ||
                   provider.ActiveProfile == null ||
                   provider.ActiveProfile.features.enableMotion;
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out Color color);
            return color;
        }
    }
}
