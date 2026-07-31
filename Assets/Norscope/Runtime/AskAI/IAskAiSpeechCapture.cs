using System;

namespace Norscope.AskAI
{
    /// <summary>
    /// Platform-independent speech capture for Ask AI Listening.
    /// Does not own UI or workflow transitions.
    /// </summary>
    public interface IAskAiSpeechCapture : IDisposable
    {
        bool IsListening { get; }

        /// <summary>
        /// Starts a single dictation session.
        /// Returns false with a user-facing error when start is not possible.
        /// </summary>
        bool TryStart(out string errorMessage);

        /// <summary>Stops recognition and suppresses further useful results.</summary>
        void Stop();

        /// <summary>
        /// Drains one queued platform event. Call from Unity main thread (Update).
        /// </summary>
        bool TryDequeue(out AskAiSpeechEvent speechEvent);
    }
}
