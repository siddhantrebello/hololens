#if UNITY_WSA && !UNITY_EDITOR
using System;
using System.Collections.Concurrent;
using UnityEngine.Windows.Speech;

namespace Norscope.AskAI
{
    /// <summary>
    /// HoloLens/UWP dictation via UnityEngine.Windows.Speech.DictationRecognizer.
    /// Platform callbacks are queued; consumers must drain on the Unity main thread.
    /// </summary>
    public sealed class WsaDictationSpeechCapture : IAskAiSpeechCapture
    {
        public const float InitialSilenceTimeoutSeconds = 5f;
        public const float AutoSilenceTimeoutSeconds = 1.2f;

        private readonly ConcurrentQueue<AskAiSpeechEvent> _queue =
            new ConcurrentQueue<AskAiSpeechEvent>();

        private DictationRecognizer _recognizer;
        private bool _listening;
        private bool _disposed;
        private bool _handlersWired;

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
            try
            {
                EnsureRecognizer();
                if (_recognizer == null)
                {
                    errorMessage =
                        "Speech recognition is unavailable. Check the device speech and privacy settings.";
                    return false;
                }

                if (_recognizer.Status == SpeechSystemStatus.Failed)
                {
                    errorMessage =
                        "Speech recognition is unavailable. Check the device speech and privacy settings.";
                    return false;
                }

                _listening = true;
                _recognizer.Start();
                return true;
            }
            catch (Exception ex)
            {
                _listening = false;
                errorMessage = MapException(ex);
                _queue.Enqueue(AskAiSpeechEvent.Error(errorMessage));
                return false;
            }
        }

        public void Stop()
        {
            if (!_listening && _recognizer == null)
                return;

            _listening = false;
            try
            {
                if (_recognizer != null &&
                    _recognizer.Status == SpeechSystemStatus.Running)
                {
                    _recognizer.Stop();
                }
            }
            catch
            {
                // Best-effort stop.
            }
        }

        public bool TryDequeue(out AskAiSpeechEvent speechEvent)
        {
            return _queue.TryDequeue(out speechEvent);
        }

        private void EnsureRecognizer()
        {
            if (_recognizer != null)
                return;

            _recognizer = new DictationRecognizer();
            _recognizer.InitialSilenceTimeoutSeconds = InitialSilenceTimeoutSeconds;
            _recognizer.AutoSilenceTimeoutSeconds = AutoSilenceTimeoutSeconds;
            WireHandlers(true);
        }

        private void WireHandlers(bool subscribe)
        {
            if (_recognizer == null)
                return;

            if (subscribe)
            {
                if (_handlersWired)
                    return;
                _recognizer.DictationHypothesis += OnHypothesis;
                _recognizer.DictationResult += OnResult;
                _recognizer.DictationComplete += OnComplete;
                _recognizer.DictationError += OnError;
                _handlersWired = true;
            }
            else if (_handlersWired)
            {
                _recognizer.DictationHypothesis -= OnHypothesis;
                _recognizer.DictationResult -= OnResult;
                _recognizer.DictationComplete -= OnComplete;
                _recognizer.DictationError -= OnError;
                _handlersWired = false;
            }
        }

        private void OnHypothesis(string text)
        {
            if (!_listening)
                return;
            _queue.Enqueue(AskAiSpeechEvent.Hypothesis(text));
        }

        private void OnResult(string text, ConfidenceLevel confidence)
        {
            if (!_listening)
                return;
            _queue.Enqueue(AskAiSpeechEvent.Result(text));
        }

        private void OnComplete(DictationCompletionCause cause)
        {
            _listening = false;
            _queue.Enqueue(AskAiSpeechEvent.Completed(MapCause(cause)));
        }

        private void OnError(string error, int hresult)
        {
            _listening = false;
            string message = MapError(error, hresult);
            _queue.Enqueue(AskAiSpeechEvent.Error(message));
        }

        private static AskAiSpeechCompletionCause MapCause(DictationCompletionCause cause)
        {
            // UWP player DictationCompletionCause does not expose InitialSilenceTimeout /
            // BabbleTimeout members (Editor stubs may). Map the portable values only.
            switch (cause)
            {
                case DictationCompletionCause.Complete:
                    return AskAiSpeechCompletionCause.Complete;
                case DictationCompletionCause.TimeoutExceeded:
                    return AskAiSpeechCompletionCause.TimeoutExceeded;
                case DictationCompletionCause.Canceled:
                    return AskAiSpeechCompletionCause.Cancelled;
                default:
                    return AskAiSpeechCompletionCause.Unknown;
            }
        }

        private static string MapError(string error, int hresult)
        {
            string combined = (error ?? string.Empty) + " " + hresult;
            string lower = combined.ToLowerInvariant();
            if (lower.Contains("privacy") ||
                lower.Contains("permission") ||
                lower.Contains("access") ||
                lower.Contains("microphone"))
            {
                return "Microphone access is required to use Ask AI.";
            }

            if (lower.Contains("language") || lower.Contains("locale"))
            {
                return "Speech recognition language is unsupported on this device.";
            }

            if (lower.Contains("network") || lower.Contains("service"))
            {
                return "Speech recognition is unavailable. Check the device speech and privacy settings.";
            }

            return "Speech recognition is unavailable. Check the device speech and privacy settings.";
        }

        private static string MapException(Exception ex)
        {
            string message = ex != null ? ex.Message : string.Empty;
            string lower = message.ToLowerInvariant();
            if (lower.Contains("microphone") ||
                lower.Contains("permission") ||
                lower.Contains("access") ||
                lower.Contains("privacy"))
            {
                return "Microphone access is required to use Ask AI.";
            }

            return "Speech recognition is unavailable. Check the device speech and privacy settings.";
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
            _listening = false;
            try
            {
                WireHandlers(false);
                if (_recognizer != null)
                {
                    if (_recognizer.Status == SpeechSystemStatus.Running)
                        _recognizer.Stop();
                    _recognizer.Dispose();
                }
            }
            catch
            {
                // Best-effort dispose.
            }

            _recognizer = null;
            ClearQueue();
        }
    }
}
#else
namespace Norscope.AskAI
{
    /// <summary>
    /// Stub so the type name exists in Editor/non-WSA compiles.
    /// Device builds use the real DictationRecognizer implementation above.
    /// </summary>
    public sealed class WsaDictationSpeechCapture : UnavailableSpeechCapture
    {
        public const float InitialSilenceTimeoutSeconds = 5f;
        public const float AutoSilenceTimeoutSeconds = 1.2f;

        public WsaDictationSpeechCapture()
            : base("Speech recognition is only available on HoloLens/UWP builds.")
        {
        }
    }
}
#endif
