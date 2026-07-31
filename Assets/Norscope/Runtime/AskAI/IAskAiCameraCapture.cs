using System;

namespace Norscope.AskAI
{
    /// <summary>
    /// Platform-independent still-image capture for Ask AI.
    /// Does not own UI or workflow transitions.
    /// </summary>
    public interface IAskAiCameraCapture : IDisposable
    {
        bool IsCapturing { get; }

        /// <summary>
        /// Starts exactly one capture for the given session token.
        /// Returns false with a user-facing error when start is not possible.
        /// </summary>
        bool TryStartCapture(int sessionToken, out string errorMessage);

        /// <summary>Cancels any in-flight capture and suppresses stale results.</summary>
        void Cancel();

        /// <summary>
        /// Drains one queued camera event. Call from Unity main thread (Update).
        /// May also finish JPEG encoding of a pending raw frame on this call.
        /// </summary>
        bool TryDequeue(out AskAiCameraEvent cameraEvent);
    }
}
