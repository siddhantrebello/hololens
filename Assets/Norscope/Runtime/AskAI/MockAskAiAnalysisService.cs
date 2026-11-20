using System.Collections.Concurrent;
using UnityEngine;

namespace Norscope.AskAI
{
    /// <summary>
    /// Editor/offline mock analysis. Never used as the HoloLens production path.
    /// </summary>
    public sealed class MockAskAiAnalysisService : IAskAiAnalysisService
    {
        public const float MockDelaySeconds = 0.45f;

        private readonly ConcurrentQueue<AskAiAnalysisEvent> _queue =
            new ConcurrentQueue<AskAiAnalysisEvent>();
        private readonly MonoBehaviour _host;

        private Coroutine _routine;
        private bool _busy;
        private bool _disposed;
        private int _token;

        public MockAskAiAnalysisService(MonoBehaviour host)
        {
            _host = host;
        }

        public bool IsBusy => _busy;

        public bool TryStartAnalysis(AskAiRequest request, int sessionToken, out string errorMessage)
        {
            errorMessage = null;
            if (_disposed)
            {
                errorMessage = "Analysis service was disposed.";
                return false;
            }

            if (_busy)
            {
                errorMessage = "An analysis request is already active.";
                return false;
            }

            if (request == null ||
                string.IsNullOrWhiteSpace(request.Transcript) ||
                !request.HasImage)
            {
                errorMessage = "The captured image could not be analyzed. Please capture it again.";
                return false;
            }

            if (_host == null || !_host.isActiveAndEnabled)
            {
                errorMessage = "Analysis host is unavailable.";
                return false;
            }

            while (_queue.TryDequeue(out _)) { }
            _token = sessionToken;
            _busy = true;
            _routine = _host.StartCoroutine(CompleteAfterDelay(sessionToken, request.Transcript));
            return true;
        }

        public void Cancel()
        {
            if (_routine != null && _host != null)
            {
                _host.StopCoroutine(_routine);
                _routine = null;
            }

            _busy = false;
            while (_queue.TryDequeue(out _)) { }
        }

        public bool TryDequeue(out AskAiAnalysisEvent analysisEvent)
        {
            return _queue.TryDequeue(out analysisEvent);
        }

        private System.Collections.IEnumerator CompleteAfterDelay(int token, string transcript)
        {
            float remaining = MockDelaySeconds;
            while (remaining > 0f)
            {
                if (token != _token)
                {
                    _busy = false;
                    _routine = null;
                    yield break;
                }

                remaining -= Time.unscaledDeltaTime;
                yield return null;
            }

            _busy = false;
            _routine = null;
            var response = new AskAiAnalysisResponse
            {
                Success = true,
                Answer = MockAskAiService.DefaultAnswer,
                Confidence = 0.7f,
                VisualObservations = new[] { "[Editor Mock] Visual analysis placeholder." },
                RecommendedAction = "Power off the device before cleaning.",
                SafetyWarning = "Disconnect external power before servicing.",
                NeedsMoreInformation = false,
                RequestId = "editor-mock",
                UserMessage = MockAskAiService.DefaultAnswer
            };
            _queue.Enqueue(AskAiAnalysisEvent.Success(token, response));
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            Cancel();
        }
    }
}
