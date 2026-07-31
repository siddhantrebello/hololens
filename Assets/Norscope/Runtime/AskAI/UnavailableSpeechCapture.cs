using System.Collections.Concurrent;

namespace Norscope.AskAI
{
    /// <summary>Fallback when no speech implementation is available.</summary>
    public class UnavailableSpeechCapture : IAskAiSpeechCapture
    {
        private readonly string _message;
        private readonly ConcurrentQueue<AskAiSpeechEvent> _queue =
            new ConcurrentQueue<AskAiSpeechEvent>();
        private bool _listening;

        public UnavailableSpeechCapture(string message)
        {
            _message = string.IsNullOrWhiteSpace(message)
                ? "Speech recognition is unavailable."
                : message.Trim();
        }

        public bool IsListening => _listening;

        public bool TryStart(out string errorMessage)
        {
            errorMessage = _message;
            _listening = false;
            _queue.Enqueue(AskAiSpeechEvent.Error(_message));
            return false;
        }

        public void Stop()
        {
            _listening = false;
        }

        public bool TryDequeue(out AskAiSpeechEvent speechEvent)
        {
            return _queue.TryDequeue(out speechEvent);
        }

        public void Dispose()
        {
            Stop();
            while (_queue.TryDequeue(out _)) { }
        }
    }
}
