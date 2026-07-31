using System.Collections.Concurrent;
using UnityEngine;

namespace Norscope.AskAI
{
    /// <summary>
    /// Editor-only mock speech capture. Never used in UWP device builds.
    /// Emits a clearly identified test transcript through the same event queue.
    /// </summary>
    public sealed class EditorMockSpeechCapture : IAskAiSpeechCapture
    {
        public const string TestTranscript =
            "[Editor Mock] What is wrong with this cooling fan?";

        private readonly ConcurrentQueue<AskAiSpeechEvent> _queue =
            new ConcurrentQueue<AskAiSpeechEvent>();

        private bool _listening;
        private bool _disposed;
        private float _startedAt = -1f;
        private bool _sentHypothesis;
        private bool _sentResult;
        private bool _sentComplete;

        public bool IsListening => _listening;

        public bool TryStart(out string errorMessage)
        {
            errorMessage = null;
            if (_disposed)
            {
                errorMessage = "Speech capture was disposed.";
                return false;
            }

            if (_listening)
            {
                errorMessage = "A speech session is already active.";
                return false;
            }

            ClearQueue();
            _listening = true;
            _startedAt = Time.realtimeSinceStartup;
            _sentHypothesis = false;
            _sentResult = false;
            _sentComplete = false;
            return true;
        }

        public void Stop()
        {
            if (!_listening)
                return;
            _listening = false;
            if (!_sentComplete)
            {
                _sentComplete = true;
                _queue.Enqueue(AskAiSpeechEvent.Completed(AskAiSpeechCompletionCause.Cancelled));
            }
        }

        public bool TryDequeue(out AskAiSpeechEvent speechEvent)
        {
            PumpTimeline();
            return _queue.TryDequeue(out speechEvent);
        }

        private void PumpTimeline()
        {
            if (!_listening || _startedAt < 0f)
                return;

            float elapsed = Time.realtimeSinceStartup - _startedAt;
            if (!_sentHypothesis && elapsed >= 0.45f)
            {
                _sentHypothesis = true;
                _queue.Enqueue(AskAiSpeechEvent.Hypothesis(
                    "[Editor Mock] What is wrong with this"));
            }

            if (!_sentResult && elapsed >= 1.1f)
            {
                _sentResult = true;
                _queue.Enqueue(AskAiSpeechEvent.Result(TestTranscript));
            }

            if (!_sentComplete && elapsed >= 1.35f)
            {
                _sentComplete = true;
                _listening = false;
                _queue.Enqueue(AskAiSpeechEvent.Completed(AskAiSpeechCompletionCause.Complete));
            }
        }

        private void ClearQueue()
        {
            while (_queue.TryDequeue(out _)) { }
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            Stop();
            ClearQueue();
        }
    }
}
