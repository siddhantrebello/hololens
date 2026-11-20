using System;

namespace Norscope.AskAI
{
    /// <summary>
    /// Backend analysis seam. OpenAI details stay on the server; Unity only sees
    /// structured AskAiAnalysisResponse values.
    /// </summary>
    public interface IAskAiAnalysisService : IDisposable
    {
        bool IsBusy { get; }

        /// <summary>
        /// Starts exactly one analysis request for the given session token.
        /// Returns false when the request cannot start.
        /// </summary>
        bool TryStartAnalysis(AskAiRequest request, int sessionToken, out string errorMessage);

        /// <summary>Aborts any in-flight HTTP request and clears queued results.</summary>
        void Cancel();

        /// <summary>Drain one queued result. Call from Unity main thread (Update).</summary>
        bool TryDequeue(out AskAiAnalysisEvent analysisEvent);
    }
}
