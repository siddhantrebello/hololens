using System.Collections.Concurrent;
using UnityEngine;

namespace Norscope.AskAI
{
    /// <summary>Fallback when no camera implementation is available.</summary>
    public class UnavailableCameraCapture : IAskAiCameraCapture
    {
        private readonly string _message;
        private readonly ConcurrentQueue<AskAiCameraEvent> _queue =
            new ConcurrentQueue<AskAiCameraEvent>();
        private int _sessionToken;
        private bool _capturing;

        public UnavailableCameraCapture(string message)
        {
            _message = string.IsNullOrWhiteSpace(message)
                ? "Camera is unavailable."
                : message.Trim();
        }

        public bool IsCapturing => _capturing;

        public bool TryStartCapture(int sessionToken, out string errorMessage)
        {
            errorMessage = _message;
            _sessionToken = sessionToken;
            _capturing = false;
            _queue.Enqueue(AskAiCameraEvent.Error(sessionToken, _message));
            return false;
        }

        public void Cancel()
        {
            _capturing = false;
            while (_queue.TryDequeue(out _)) { }
        }

        public bool TryDequeue(out AskAiCameraEvent cameraEvent)
        {
            return _queue.TryDequeue(out cameraEvent);
        }

        public void Dispose()
        {
            Cancel();
        }
    }
}
