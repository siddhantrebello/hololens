using System.Collections;
using System.Text;
using Norscope.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace Norscope.AskAI
{
    /// <summary>
    /// Owns Ask AI workflow state transitions. Speech, camera, and backend analysis
    /// stay behind dedicated abstractions — no provider SDK types in this controller.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AskAiWorkflowController : MonoBehaviour
    {
        private const float MinProcessingDisplaySeconds = 0.15f;
        private const float MaxListeningSeconds = 15f;
        private const float MaxCapturingSeconds = 10f;
        private const float MaxProcessingSeconds = 60f;
        private const float TranscriptHoldSeconds = 0.15f;

        [SerializeField] private AskAiView view;
        [SerializeField] private Button primaryButton;
        [SerializeField] private AskAiBackendConfig backendConfig;

        private IAskAiSpeechCapture _speechCapture;
        private IAskAiCameraCapture _cameraCapture;
        private IAskAiAnalysisService _analysisService;
        private readonly AskAiRequest _currentRequest = new AskAiRequest();

        private AskAiWorkflowState _state = AskAiWorkflowState.Ready;
        private Coroutine _workflowRoutine;
        private Coroutine _listeningTimeoutRoutine;
        private Coroutine _capturingTimeoutRoutine;
        private Coroutine _processingTimeoutRoutine;
        private bool _listenersWired;
        private bool _sessionActive;
        private bool _completionHandled;
        private bool _captureCompletionHandled;
        private bool _analysisCompletionHandled;
        private int _sessionToken;
        private bool _voiceStartPlayed;
        private bool _voiceStopPlayed;
        private bool _voiceRecognizedPlayed;
        private float _processingEnteredAt = -1f;

        private readonly StringBuilder _finalTranscript = new StringBuilder();
        private string _currentHypothesis = string.Empty;
        private string _lastAppendedResult = string.Empty;

        public AskAiWorkflowState CurrentState => _state;
        public AskAiRequest CurrentRequest => _currentRequest;

        public void Configure(
            AskAiView askAiView,
            Button primary,
            IAskAiSpeechCapture speechCapture = null,
            IAskAiCameraCapture cameraCapture = null,
            IAskAiAnalysisService analysisService = null,
            AskAiBackendConfig config = null)
        {
            view = askAiView;
            primaryButton = primary;
            if (config != null)
                backendConfig = config;
            ReplaceSpeechCapture(speechCapture);
            ReplaceCameraCapture(cameraCapture);
            ReplaceAnalysisService(analysisService);
            WireListeners();
        }

        private void Awake()
        {
            if (_speechCapture == null)
                ReplaceSpeechCapture(AskAiSpeechCaptureFactory.Create());
            if (_cameraCapture == null)
                ReplaceCameraCapture(AskAiCameraCaptureFactory.Create());
            if (_analysisService == null)
                ReplaceAnalysisService(AskAiAnalysisServiceFactory.Create(backendConfig, this));
            if (view == null)
                view = GetComponent<AskAiView>();
            if (primaryButton == null && view != null)
                primaryButton = view.PrimaryButton;
            WireListeners();
        }

        private void OnEnable()
        {
            ResetToReady(animate: false);
        }

        private void OnDisable()
        {
            CancelWorkflow(suppressCallbacks: true);
            view?.SnapReadyPresentation();
        }

        private void OnDestroy()
        {
            CancelWorkflow(suppressCallbacks: true);
            UnwireListeners();
            DisposeSpeechCapture();
            DisposeCameraCapture();
            DisposeAnalysisService();
        }

        private void Update()
        {
            if (_speechCapture != null && _sessionActive)
            {
                while (_speechCapture.TryDequeue(out AskAiSpeechEvent speechEvent))
                    HandleSpeechEvent(speechEvent);
            }

            if (_cameraCapture != null &&
                (_state == AskAiWorkflowState.Capturing || _cameraCapture.IsCapturing))
            {
                while (_cameraCapture.TryDequeue(out AskAiCameraEvent cameraEvent))
                    HandleCameraEvent(cameraEvent);
            }

            if (_analysisService != null &&
                (_state == AskAiWorkflowState.Processing || _analysisService.IsBusy))
            {
                while (_analysisService.TryDequeue(out AskAiAnalysisEvent analysisEvent))
                    HandleAnalysisEvent(analysisEvent);
            }
        }

        private void WireListeners()
        {
            if (_listenersWired || primaryButton == null)
                return;
            primaryButton.onClick.RemoveListener(OnPrimaryClicked);
            primaryButton.onClick.AddListener(OnPrimaryClicked);
            _listenersWired = true;
        }

        private void UnwireListeners()
        {
            if (!_listenersWired || primaryButton == null)
                return;
            primaryButton.onClick.RemoveListener(OnPrimaryClicked);
            _listenersWired = false;
        }

        private void OnPrimaryClicked()
        {
            if (_state == AskAiWorkflowState.Listening ||
                _state == AskAiWorkflowState.Capturing ||
                _state == AskAiWorkflowState.Processing)
            {
                return;
            }

            UIAudioManager.Play(UIAudioEvent.ButtonClick);
            StartWorkflow();
        }

        private void StartWorkflow()
        {
            CancelWorkflow(suppressCallbacks: true);
            ClearTranscriptState();
            _currentRequest.Clear();
            _completionHandled = false;
            _captureCompletionHandled = false;
            _analysisCompletionHandled = false;
            _voiceStartPlayed = false;
            _voiceStopPlayed = false;
            _voiceRecognizedPlayed = false;
            _sessionToken++;

            if (_speechCapture == null)
                ReplaceSpeechCapture(AskAiSpeechCaptureFactory.Create());
            if (_cameraCapture == null)
                ReplaceCameraCapture(AskAiCameraCaptureFactory.Create());
            if (_analysisService == null)
                ReplaceAnalysisService(AskAiAnalysisServiceFactory.Create(backendConfig, this));

            EnterState(
                AskAiWorkflowState.Listening,
                "Listening…",
                body: string.Empty,
                buttonLabel: "Ask AI",
                buttonEnabled: false,
                hintVisible: false,
                microphoneActive: true,
                processingActive: false);

            if (!_speechCapture.TryStart(out string startError))
            {
                ShowWorkflowError(string.IsNullOrWhiteSpace(startError)
                    ? "Speech recognition is unavailable. Check the device speech and privacy settings."
                    : startError);
                return;
            }

            _sessionActive = true;
            PlayVoiceStartOnce();
            _listeningTimeoutRoutine = StartCoroutine(ListeningSafetyTimeout());
        }

        private IEnumerator ListeningSafetyTimeout()
        {
            float remaining = MaxListeningSeconds;
            while (remaining > 0f)
            {
                if (!_sessionActive || _state != AskAiWorkflowState.Listening)
                    yield break;
                remaining -= Time.unscaledDeltaTime;
                yield return null;
            }

            if (!_sessionActive || _completionHandled ||
                _state != AskAiWorkflowState.Listening)
                yield break;

            StopSpeechCapture();
            PlayVoiceStopOnce();
            FinalizeListeningSession(AskAiSpeechCompletionCause.TimeoutExceeded);
        }

        private void HandleSpeechEvent(AskAiSpeechEvent speechEvent)
        {
            if (!_sessionActive || _completionHandled)
                return;

            switch (speechEvent.Kind)
            {
                case AskAiSpeechEventKind.Hypothesis:
                    _currentHypothesis = speechEvent.Text ?? string.Empty;
                    RefreshListeningBody();
                    break;

                case AskAiSpeechEventKind.Result:
                    AppendFinalResult(speechEvent.Text);
                    _currentHypothesis = string.Empty;
                    RefreshListeningBody();
                    break;

                case AskAiSpeechEventKind.Completed:
                    PlayVoiceStopOnce();
                    FinalizeListeningSession(speechEvent.Cause);
                    break;

                case AskAiSpeechEventKind.Error:
                    PlayVoiceStopOnce();
                    ShowWorkflowError(string.IsNullOrWhiteSpace(speechEvent.ErrorMessage)
                        ? "Speech recognition is unavailable. Check the device speech and privacy settings."
                        : speechEvent.ErrorMessage);
                    break;
            }
        }

        private void AppendFinalResult(string text)
        {
            string normalized = NormalizeWhitespace(text);
            if (string.IsNullOrEmpty(normalized))
                return;

            if (!string.IsNullOrEmpty(_lastAppendedResult) &&
                string.Equals(normalized, _lastAppendedResult, System.StringComparison.Ordinal))
            {
                return;
            }

            string existing = _finalTranscript.ToString();
            if (!string.IsNullOrEmpty(existing) &&
                existing.EndsWith(normalized, System.StringComparison.Ordinal))
            {
                _lastAppendedResult = normalized;
                return;
            }

            if (_finalTranscript.Length > 0)
                _finalTranscript.Append(' ');
            _finalTranscript.Append(normalized);
            _lastAppendedResult = normalized;
        }

        private void RefreshListeningBody()
        {
            if (_state != AskAiWorkflowState.Listening || view == null)
                return;

            string finalized = _finalTranscript.ToString().Trim();
            string hypothesis = (_currentHypothesis ?? string.Empty).Trim();
            string body;
            if (string.IsNullOrEmpty(finalized))
                body = hypothesis;
            else if (string.IsNullOrEmpty(hypothesis))
                body = finalized;
            else if (hypothesis.StartsWith(finalized, System.StringComparison.OrdinalIgnoreCase))
                body = hypothesis;
            else
                body = finalized + " " + hypothesis;

            view.SetBodyText(body);
        }

        private void FinalizeListeningSession(AskAiSpeechCompletionCause cause)
        {
            if (_completionHandled)
                return;
            _completionHandled = true;
            _sessionActive = false;
            StopListeningTimeout();
            StopSpeechCapture();

            string transcript = NormalizeWhitespace(_finalTranscript.ToString());
            bool usable = !string.IsNullOrEmpty(transcript);

            if (!usable)
            {
                ShowNoSpeechDetected();
                return;
            }

            PlayVoiceRecognizedOnce();
            _currentRequest.Clear();
            _currentRequest.Transcript = transcript;
            _currentRequest.TimestampUtc = System.DateTime.UtcNow;
            _workflowRoutine = StartCoroutine(ContinueToCapturing(transcript));
        }

        private IEnumerator ContinueToCapturing(string transcript)
        {
            EnterState(
                AskAiWorkflowState.Listening,
                "Listening…",
                body: transcript,
                buttonLabel: "Ask AI",
                buttonEnabled: false,
                hintVisible: false,
                microphoneActive: false,
                processingActive: false);

            yield return WaitOrCancelled(TranscriptHoldSeconds);
            if (_workflowRoutine == null)
                yield break;

            EnterState(
                AskAiWorkflowState.Capturing,
                "Capturing the scene…",
                body: string.Empty,
                buttonLabel: "Ask AI",
                buttonEnabled: false,
                hintVisible: true,
                microphoneActive: false,
                processingActive: false);

            if (_cameraCapture == null)
                ReplaceCameraCapture(AskAiCameraCaptureFactory.Create());

            int captureToken = ++_sessionToken;
            _captureCompletionHandled = false;

            if (!_cameraCapture.TryStartCapture(captureToken, out string cameraError))
            {
                ShowWorkflowError(string.IsNullOrWhiteSpace(cameraError)
                    ? "Camera is unavailable. Check the device camera and privacy settings."
                    : cameraError);
                yield break;
            }

            _capturingTimeoutRoutine = StartCoroutine(CapturingSafetyTimeout(captureToken));
            _workflowRoutine = null;
        }

        private IEnumerator CapturingSafetyTimeout(int captureToken)
        {
            float remaining = MaxCapturingSeconds;
            while (remaining > 0f)
            {
                if (_captureCompletionHandled ||
                    _state != AskAiWorkflowState.Capturing)
                    yield break;
                remaining -= Time.unscaledDeltaTime;
                yield return null;
            }

            if (_captureCompletionHandled ||
                _state != AskAiWorkflowState.Capturing)
                yield break;

            CancelCameraCapture();
            ShowWorkflowError("Camera capture timed out. Please try again.");
        }

        private void HandleCameraEvent(AskAiCameraEvent cameraEvent)
        {
            if (_captureCompletionHandled)
                return;

            if (_state != AskAiWorkflowState.Capturing)
                return;

            if (cameraEvent.SessionToken != _sessionToken)
                return;

            switch (cameraEvent.Kind)
            {
                case AskAiCameraEventKind.Success:
                    if (cameraEvent.Image == null || !cameraEvent.Image.IsValid)
                    {
                        _captureCompletionHandled = true;
                        StopCapturingTimeout();
                        CancelCameraCapture();
                        ShowWorkflowError("Camera returned an empty image.");
                        return;
                    }

                    _captureCompletionHandled = true;
                    StopCapturingTimeout();
                    CancelCameraCapture();
                    _currentRequest.ApplyImage(cameraEvent.Image);
                    _workflowRoutine = StartCoroutine(ContinueToProcessing());
                    break;

                case AskAiCameraEventKind.Error:
                    _captureCompletionHandled = true;
                    StopCapturingTimeout();
                    CancelCameraCapture();
                    ShowWorkflowError(string.IsNullOrWhiteSpace(cameraEvent.ErrorMessage)
                        ? "Camera capture failed. Please try again."
                        : cameraEvent.ErrorMessage);
                    break;
            }
        }

        private IEnumerator ContinueToProcessing()
        {
            string transcript = _currentRequest.Transcript;
            if (string.IsNullOrWhiteSpace(transcript) || !_currentRequest.HasImage)
            {
                ShowWorkflowError("The captured image could not be analyzed. Please capture it again.");
                yield break;
            }

            EnterState(
                AskAiWorkflowState.Processing,
                "Analyzing…",
                body: string.Empty,
                buttonLabel: "Ask AI",
                buttonEnabled: false,
                hintVisible: true,
                microphoneActive: false,
                processingActive: true);
            UIAudioManager.Play(UIAudioEvent.AIThinking);
            _processingEnteredAt = Time.realtimeSinceStartup;
            _analysisCompletionHandled = false;

            // Optional flicker guard only — completion is driven by HTTP/mock analysis.
            yield return WaitOrCancelled(MinProcessingDisplaySeconds);
            if (_workflowRoutine == null)
                yield break;

            if (_analysisService == null)
                ReplaceAnalysisService(AskAiAnalysisServiceFactory.Create(backendConfig, this));

            int analysisToken = ++_sessionToken;
            if (!_analysisService.TryStartAnalysis(
                    _currentRequest, analysisToken, out string analysisError))
            {
                ShowWorkflowError(string.IsNullOrWhiteSpace(analysisError)
                    ? "The AI service is temporarily unavailable."
                    : analysisError);
                yield break;
            }

            _processingTimeoutRoutine = StartCoroutine(ProcessingSafetyTimeout(analysisToken));
            _workflowRoutine = null;
        }

        private IEnumerator ProcessingSafetyTimeout(int analysisToken)
        {
            float remaining = MaxProcessingSeconds;
            while (remaining > 0f)
            {
                if (_analysisCompletionHandled ||
                    _state != AskAiWorkflowState.Processing ||
                    analysisToken != _sessionToken)
                    yield break;
                remaining -= Time.unscaledDeltaTime;
                yield return null;
            }

            if (_analysisCompletionHandled ||
                _state != AskAiWorkflowState.Processing ||
                analysisToken != _sessionToken)
                yield break;

            CancelAnalysisService();
            ShowWorkflowError("The AI response took too long. Please try again.");
        }

        private void HandleAnalysisEvent(AskAiAnalysisEvent analysisEvent)
        {
            if (_analysisCompletionHandled)
                return;

            if (_state != AskAiWorkflowState.Processing)
                return;

            if (analysisEvent.SessionToken != _sessionToken)
                return;

            _analysisCompletionHandled = true;
            StopProcessingTimeout();
            CancelAnalysisService();

            AskAiAnalysisResponse response = analysisEvent.Response;
            if (analysisEvent.Kind == AskAiAnalysisEventKind.Success &&
                response != null &&
                response.Success &&
                !string.IsNullOrWhiteSpace(response.Answer))
            {
                float elapsed = _processingEnteredAt > 0f
                    ? Time.realtimeSinceStartup - _processingEnteredAt
                    : MinProcessingDisplaySeconds;
                float hold = Mathf.Max(0f, MinProcessingDisplaySeconds - elapsed);
                _workflowRoutine = StartCoroutine(
                    ShowAnswerAfterHold(FormatAnswerBody(response), hold));
                return;
            }

            string message = response != null && !string.IsNullOrWhiteSpace(response.UserMessage)
                ? response.UserMessage
                : "The AI service is temporarily unavailable.";
            ShowWorkflowError(message);
        }

        private IEnumerator ShowAnswerAfterHold(string answer, float holdSeconds)
        {
            // Always yield once so StartCoroutine can assign _workflowRoutine before we continue.
            // Do not gate on _workflowRoutine == null here: when hold is 0 this method used to
            // run fully synchronously inside StartCoroutine and abort before EnterState(Answer),
            // leaving the UI stuck on Analyzing even after a successful backend response.
            if (holdSeconds > 0f)
            {
                float remaining = holdSeconds;
                while (remaining > 0f)
                {
                    remaining -= Time.unscaledDeltaTime;
                    yield return null;
                }
            }
            else
            {
                yield return null;
            }

            EnterState(
                AskAiWorkflowState.Answer,
                string.Empty,
                body: answer,
                buttonLabel: "Ask Again",
                buttonEnabled: true,
                hintVisible: false,
                microphoneActive: false,
                processingActive: false);
            UIAudioManager.Play(UIAudioEvent.AIResponse);
            _workflowRoutine = null;
        }

        private void ShowNoSpeechDetected()
        {
            _currentRequest.Clear();
            EnterState(
                AskAiWorkflowState.Error,
                "Unable to complete analysis",
                body: "No speech detected. Please try again.",
                buttonLabel: "Try Again",
                buttonEnabled: true,
                hintVisible: true,
                microphoneActive: false,
                processingActive: false);
            UIAudioManager.Play(UIAudioEvent.Error);
        }

        private void ShowWorkflowError(string message)
        {
            _completionHandled = true;
            _captureCompletionHandled = true;
            _analysisCompletionHandled = true;
            _sessionActive = false;
            StopListeningTimeout();
            StopCapturingTimeout();
            StopProcessingTimeout();
            StopSpeechCapture();
            CancelCameraCapture();
            CancelAnalysisService();
            _currentRequest.Clear();
            EnterState(
                AskAiWorkflowState.Error,
                "Unable to complete analysis",
                body: message,
                buttonLabel: "Try Again",
                buttonEnabled: true,
                hintVisible: true,
                microphoneActive: false,
                processingActive: false);
            UIAudioManager.Play(UIAudioEvent.Error);
        }

        private IEnumerator WaitOrCancelled(float seconds)
        {
            float remaining = Mathf.Max(0f, seconds);
            while (remaining > 0f)
            {
                if (_workflowRoutine == null)
                    yield break;
                remaining -= Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private void CancelWorkflow(bool suppressCallbacks)
        {
            if (suppressCallbacks)
            {
                _completionHandled = true;
                _captureCompletionHandled = true;
                _analysisCompletionHandled = true;
            }

            _sessionActive = false;
            _sessionToken++;
            StopListeningTimeout();
            StopCapturingTimeout();
            StopProcessingTimeout();

            if (_workflowRoutine != null)
            {
                StopCoroutine(_workflowRoutine);
                _workflowRoutine = null;
            }

            StopSpeechCapture();
            CancelCameraCapture();
            CancelAnalysisService();
            ClearTranscriptState();
            _currentRequest.Clear();
        }

        private void ResetToReady(bool animate)
        {
            CancelWorkflow(suppressCallbacks: true);
            _completionHandled = false;
            _captureCompletionHandled = false;
            _analysisCompletionHandled = false;
            EnterState(
                AskAiWorkflowState.Ready,
                "Ready to listen",
                body: string.Empty,
                buttonLabel: "Ask AI",
                buttonEnabled: true,
                hintVisible: true,
                microphoneActive: false,
                processingActive: false,
                animate);
            view?.SnapReadyPresentation();
        }

        private void EnterState(
            AskAiWorkflowState state,
            string status,
            string body,
            string buttonLabel,
            bool buttonEnabled,
            bool hintVisible,
            bool microphoneActive,
            bool processingActive,
            bool animate = true)
        {
            _state = state;
            if (view != null)
            {
                view.Render(
                    state,
                    status,
                    body,
                    buttonLabel,
                    buttonEnabled,
                    hintVisible,
                    microphoneActive,
                    processingActive,
                    animate);
            }
        }

        private void ClearTranscriptState()
        {
            _finalTranscript.Length = 0;
            _currentHypothesis = string.Empty;
            _lastAppendedResult = string.Empty;
        }

        /// <summary>
        /// Compact body for the small Ask AI panel — answer only, plus a one-line
        /// safety note when present. Observations/actions stay in the structured
        /// response but are not dumped into the panel (they overflow the card).
        /// </summary>
        private static string FormatAnswerBody(AskAiAnalysisResponse response)
        {
            if (response == null)
                return string.Empty;

            string answer = (response.Answer ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(answer))
                return string.Empty;

            // Soft cap so a verbose model reply cannot spill the panel.
            const int maxChars = 280;
            if (answer.Length > maxChars)
                answer = answer.Substring(0, maxChars - 1).TrimEnd() + "…";

            if (!string.IsNullOrWhiteSpace(response.SafetyWarning))
            {
                string safety = response.SafetyWarning.Trim();
                if (safety.Length > 100)
                    safety = safety.Substring(0, 99).TrimEnd() + "…";
                return (answer + "\n\n⚠ " + safety).Trim();
            }

            return answer;
        }

        private void StopSpeechCapture()
        {
            if (_speechCapture == null)
                return;
            _speechCapture.Stop();
            while (_speechCapture.TryDequeue(out _)) { }
        }

        private void CancelCameraCapture()
        {
            if (_cameraCapture == null)
                return;
            _cameraCapture.Cancel();
            while (_cameraCapture.TryDequeue(out _)) { }
        }

        private void CancelAnalysisService()
        {
            if (_analysisService == null)
                return;
            _analysisService.Cancel();
            while (_analysisService.TryDequeue(out _)) { }
        }

        private void StopListeningTimeout()
        {
            if (_listeningTimeoutRoutine == null)
                return;
            StopCoroutine(_listeningTimeoutRoutine);
            _listeningTimeoutRoutine = null;
        }

        private void StopCapturingTimeout()
        {
            if (_capturingTimeoutRoutine == null)
                return;
            StopCoroutine(_capturingTimeoutRoutine);
            _capturingTimeoutRoutine = null;
        }

        private void StopProcessingTimeout()
        {
            if (_processingTimeoutRoutine == null)
                return;
            StopCoroutine(_processingTimeoutRoutine);
            _processingTimeoutRoutine = null;
        }

        private void ReplaceSpeechCapture(IAskAiSpeechCapture capture)
        {
            DisposeSpeechCapture();
            _speechCapture = capture ?? AskAiSpeechCaptureFactory.Create();
        }

        private void ReplaceCameraCapture(IAskAiCameraCapture capture)
        {
            DisposeCameraCapture();
            _cameraCapture = capture ?? AskAiCameraCaptureFactory.Create();
        }

        private void ReplaceAnalysisService(IAskAiAnalysisService service)
        {
            DisposeAnalysisService();
            _analysisService = service ??
                AskAiAnalysisServiceFactory.Create(backendConfig, this);
        }

        private void DisposeSpeechCapture()
        {
            if (_speechCapture == null)
                return;
            _speechCapture.Dispose();
            _speechCapture = null;
        }

        private void DisposeCameraCapture()
        {
            if (_cameraCapture == null)
                return;
            _cameraCapture.Dispose();
            _cameraCapture = null;
        }

        private void DisposeAnalysisService()
        {
            if (_analysisService == null)
                return;
            _analysisService.Dispose();
            _analysisService = null;
        }

        private void PlayVoiceStartOnce()
        {
            if (_voiceStartPlayed)
                return;
            _voiceStartPlayed = true;
            UIAudioManager.Play(UIAudioEvent.VoiceStartListening);
        }

        private void PlayVoiceStopOnce()
        {
            if (_voiceStopPlayed)
                return;
            _voiceStopPlayed = true;
            UIAudioManager.Play(UIAudioEvent.VoiceStopListening);
        }

        private void PlayVoiceRecognizedOnce()
        {
            if (_voiceRecognizedPlayed)
                return;
            _voiceRecognizedPlayed = true;
            UIAudioManager.Play(UIAudioEvent.VoiceRecognized);
        }

        private static string NormalizeWhitespace(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string[] parts = value.Trim().Split(
                (char[])null,
                System.StringSplitOptions.RemoveEmptyEntries);
            return string.Join(" ", parts);
        }

        public void ShowError(string message)
        {
            ShowWorkflowError(string.IsNullOrEmpty(message)
                ? "Something went wrong. Please try again."
                : message);
        }
    }
}
