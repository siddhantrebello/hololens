using System.Collections.Concurrent;

namespace Norscope.AskAI
{
    public sealed class UnavailableAskAiAnalysisService : IAskAiAnalysisService
    {
        private readonly string _message;
        private readonly ConcurrentQueue<AskAiAnalysisEvent> _queue =
            new ConcurrentQueue<AskAiAnalysisEvent>();

        public UnavailableAskAiAnalysisService(string message = null)
        {
            _message = string.IsNullOrWhiteSpace(message)
                ? "The AI service is temporarily unavailable."
                : message.Trim();
        }

        public bool IsBusy => false;

        public bool TryStartAnalysis(AskAiRequest request, int sessionToken, out string errorMessage)
        {
            errorMessage = _message;
            _queue.Enqueue(AskAiAnalysisEvent.Error(
                sessionToken,
                AskAiAnalysisResponse.Fail("UNAVAILABLE", _message)));
            return false;
        }

        public void Cancel()
        {
            while (_queue.TryDequeue(out _)) { }
        }

        public bool TryDequeue(out AskAiAnalysisEvent analysisEvent)
        {
            return _queue.TryDequeue(out analysisEvent);
        }

        public void Dispose()
        {
            Cancel();
        }
    }
}
