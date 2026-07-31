using System.Collections.Concurrent;
using UnityEngine;

namespace Norscope.AskAI
{
    /// <summary>
    /// Editor-only mock camera capture. Never used in UWP device builds.
    /// Produces a deterministic JPEG and follows the same event queue contract.
    /// </summary>
    public sealed class EditorMockCameraCapture : IAskAiCameraCapture
    {
        public const int MockWidth = 64;
        public const int MockHeight = 36;
        public const int JpegQuality = 75;
        public const float CaptureDelaySeconds = 0.35f;

        private static byte[] _cachedJpeg;

        private readonly ConcurrentQueue<AskAiCameraEvent> _queue =
            new ConcurrentQueue<AskAiCameraEvent>();

        private int _sessionToken;
        private bool _capturing;
        private bool _disposed;
        private float _startedAt = -1f;
        private bool _completed;

        public bool IsCapturing => _capturing;

        public bool TryStartCapture(int sessionToken, out string errorMessage)
        {
            errorMessage = null;
            if (_disposed)
            {
                errorMessage = "Camera capture was disposed.";
                return false;
            }

            if (_capturing)
            {
                errorMessage = "A camera capture is already active.";
                return false;
            }

            while (_queue.TryDequeue(out _)) { }
            _sessionToken = sessionToken;
            _capturing = true;
            _completed = false;
            _startedAt = Time.realtimeSinceStartup;
            return true;
        }

        public void Cancel()
        {
            _capturing = false;
            _completed = true;
            while (_queue.TryDequeue(out _)) { }
        }

        public bool TryDequeue(out AskAiCameraEvent cameraEvent)
        {
            PumpTimeline();
            return _queue.TryDequeue(out cameraEvent);
        }

        private void PumpTimeline()
        {
            if (!_capturing || _completed || _startedAt < 0f)
                return;

            if (Time.realtimeSinceStartup - _startedAt < CaptureDelaySeconds)
                return;

            _completed = true;
            _capturing = false;

            byte[] jpeg = GetOrCreateDeterministicJpeg();
            var image = new AskAiCapturedImage(jpeg, MockWidth, MockHeight, "jpeg");
            _queue.Enqueue(AskAiCameraEvent.Success(_sessionToken, image));
        }

        private static byte[] GetOrCreateDeterministicJpeg()
        {
            if (_cachedJpeg != null && _cachedJpeg.Length > 0)
                return _cachedJpeg;

            var texture = new Texture2D(MockWidth, MockHeight, TextureFormat.RGB24, false);
            try
            {
                var pixels = new Color32[MockWidth * MockHeight];
                for (int y = 0; y < MockHeight; y++)
                {
                    for (int x = 0; x < MockWidth; x++)
                    {
                        byte tone = (byte)(40 + ((x + y) % 40));
                        pixels[y * MockWidth + x] = new Color32(tone, (byte)(tone + 20), 90, 255);
                    }
                }

                texture.SetPixels32(pixels);
                texture.Apply(false, false);
                _cachedJpeg = ImageConversion.EncodeToJPG(texture, JpegQuality);
            }
            finally
            {
                Object.Destroy(texture);
            }

            if (_cachedJpeg == null || _cachedJpeg.Length == 0)
                _cachedJpeg = new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 };

            return _cachedJpeg;
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
