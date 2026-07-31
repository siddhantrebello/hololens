#if UNITY_WSA && !UNITY_EDITOR
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Windows.WebCam;

namespace Norscope.AskAI
{
    /// <summary>
    /// HoloLens/UWP still capture via UnityEngine.Windows.WebCam.PhotoCapture.
    /// Platform callbacks queue raw/JPEG work; Texture2D encode/destroy runs on main-thread TryDequeue.
    /// </summary>
    public sealed class WsaPhotoCaptureProvider : IAskAiCameraCapture
    {
        public const int TargetWidth = 1280;
        public const int TargetHeight = 720;
        public const int JpegQuality = 75;

        private readonly ConcurrentQueue<AskAiCameraEvent> _queue =
            new ConcurrentQueue<AskAiCameraEvent>();

        private readonly object _gate = new object();

        private PhotoCapture _photoCapture;
        private int _activeSessionToken;
        private int _captureWidth;
        private int _captureHeight;
        private bool _isCapturing;
        private bool _disposed;
        private bool _cancelRequested;
        private PendingRawFrame _pendingRaw;

        private struct PendingRawFrame
        {
            public bool HasValue;
            public int SessionToken;
            public byte[] BgraBytes;
            public int Width;
            public int Height;
            public string ErrorMessage;
        }

        public bool IsCapturing => _isCapturing;

        public bool TryStartCapture(int sessionToken, out string errorMessage)
        {
            errorMessage = null;
            if (_disposed)
            {
                errorMessage = "Camera capture was disposed.";
                return false;
            }

            if (_isCapturing)
            {
                errorMessage = "A camera capture is already active.";
                return false;
            }

            ClearQueue();
            _pendingRaw = default;
            _cancelRequested = false;
            _activeSessionToken = sessionToken;
            _isCapturing = true;

            try
            {
                Resolution resolution = SelectClosestResolution(TargetWidth, TargetHeight);
                _captureWidth = resolution.width;
                _captureHeight = resolution.height;

                PhotoCapture.CreateAsync(false, OnPhotoCaptureCreated);
                return true;
            }
            catch (Exception ex)
            {
                _isCapturing = false;
                errorMessage = MapStartError(ex);
                return false;
            }
        }

        public void Cancel()
        {
            _cancelRequested = true;
            StopAndDisposePhotoCapture();
            _pendingRaw = default;
            ClearQueue();
            _isCapturing = false;
        }

        public bool TryDequeue(out AskAiCameraEvent cameraEvent)
        {
            EncodePendingRawOnMainThread();
            return _queue.TryDequeue(out cameraEvent);
        }

        private void OnPhotoCaptureCreated(PhotoCapture captureObject)
        {
            if (!IsActiveSession())
            {
                captureObject?.Dispose();
                return;
            }

            _photoCapture = captureObject;
            var parameters = new CameraParameters
            {
                hologramOpacity = 0f,
                cameraResolutionWidth = _captureWidth,
                cameraResolutionHeight = _captureHeight,
                pixelFormat = CapturePixelFormat.BGRA32
            };

            try
            {
                _photoCapture.StartPhotoModeAsync(parameters, OnPhotoModeStarted);
            }
            catch (Exception ex)
            {
                EnqueueError(MapCaptureError(ex.Message));
                StopAndDisposePhotoCapture();
            }
        }

        private void OnPhotoModeStarted(PhotoCapture.PhotoCaptureResult result)
        {
            if (!IsActiveSession())
            {
                StopAndDisposePhotoCapture();
                return;
            }

            if (!result.success)
            {
                EnqueueError("Camera access failed. Check device camera and privacy settings.");
                StopAndDisposePhotoCapture();
                return;
            }

            try
            {
                _photoCapture.TakePhotoAsync(OnCapturedToMemory);
            }
            catch (Exception ex)
            {
                EnqueueError(MapCaptureError(ex.Message));
                StopAndDisposePhotoCapture();
            }
        }

        private void OnCapturedToMemory(
            PhotoCapture.PhotoCaptureResult result,
            PhotoCaptureFrame frame)
        {
            try
            {
                if (!IsActiveSession())
                    return;

                if (!result.success || frame == null)
                {
                    EnqueueError("Camera capture failed. Please try again.");
                    return;
                }

                var buffer = new List<byte>();
                frame.CopyRawImageDataIntoBuffer(buffer);
                if (buffer.Count == 0)
                {
                    EnqueueError("Camera returned an empty image.");
                    return;
                }

                lock (_gate)
                {
                    _pendingRaw = new PendingRawFrame
                    {
                        HasValue = true,
                        SessionToken = _activeSessionToken,
                        BgraBytes = buffer.ToArray(),
                        Width = _captureWidth,
                        Height = _captureHeight
                    };
                }
            }
            catch (Exception ex)
            {
                EnqueueError(MapCaptureError(ex.Message));
            }
            finally
            {
                try { frame?.Dispose(); } catch { /* ignore */ }
                StopAndDisposePhotoCapture();
            }
        }

        private void EncodePendingRawOnMainThread()
        {
            PendingRawFrame pending;
            lock (_gate)
            {
                pending = _pendingRaw;
                _pendingRaw = default;
            }

            if (!pending.HasValue)
                return;

            if (_cancelRequested || pending.SessionToken != _activeSessionToken)
            {
                pending.BgraBytes = null;
                _isCapturing = false;
                return;
            }

            if (!string.IsNullOrEmpty(pending.ErrorMessage))
            {
                EnqueueError(pending.ErrorMessage);
                return;
            }

            Texture2D texture = null;
            try
            {
                texture = new Texture2D(
                    pending.Width,
                    pending.Height,
                    TextureFormat.BGRA32,
                    false);
                texture.LoadRawTextureData(pending.BgraBytes);
                texture.Apply(false, false);
                byte[] jpeg = ImageConversion.EncodeToJPG(texture, JpegQuality);
                pending.BgraBytes = null;

                if (jpeg == null || jpeg.Length == 0)
                {
                    EnqueueError("Failed to encode the captured image.");
                    return;
                }

                var image = new AskAiCapturedImage(
                    jpeg, pending.Width, pending.Height, "jpeg");
                _isCapturing = false;
                _queue.Enqueue(AskAiCameraEvent.Success(pending.SessionToken, image));
            }
            catch (Exception ex)
            {
                EnqueueError(MapCaptureError(ex.Message));
            }
            finally
            {
                if (texture != null)
                    UnityEngine.Object.Destroy(texture);
            }
        }

        private bool IsActiveSession()
        {
            return !_disposed && !_cancelRequested && _isCapturing;
        }

        private void EnqueueError(string message)
        {
            int token = _activeSessionToken;
            _isCapturing = false;
            _queue.Enqueue(AskAiCameraEvent.Error(
                token,
                string.IsNullOrWhiteSpace(message)
                    ? "Camera capture failed. Please try again."
                    : message));
        }

        private void StopAndDisposePhotoCapture()
        {
            PhotoCapture capture = _photoCapture;
            _photoCapture = null;
            if (capture == null)
                return;

            try
            {
                capture.StopPhotoModeAsync(_ =>
                {
                    try { capture.Dispose(); } catch { /* ignore */ }
                });
            }
            catch
            {
                try { capture.Dispose(); } catch { /* ignore */ }
            }
        }

        public static Resolution SelectClosestResolution(int targetWidth, int targetHeight)
        {
            IEnumerable<Resolution> supported = PhotoCapture.SupportedResolutions;
            if (supported == null)
                throw new InvalidOperationException("No supported camera resolutions.");

            Resolution[] list = supported.ToArray();
            if (list.Length == 0)
                throw new InvalidOperationException("No supported camera resolutions.");

            Resolution best = list[0];
            long bestScore = long.MaxValue;
            for (int i = 0; i < list.Length; i++)
            {
                Resolution candidate = list[i];
                long dw = (long)candidate.width - targetWidth;
                long dh = (long)candidate.height - targetHeight;
                long score = dw * dw + dh * dh;

                // Deterministic tie-break: prefer landscape, then lower width, then lower height.
                bool better = score < bestScore;
                if (!better && score == bestScore)
                {
                    bool candidateLandscape = candidate.width >= candidate.height;
                    bool bestLandscape = best.width >= best.height;
                    if (candidateLandscape && !bestLandscape)
                        better = true;
                    else if (candidateLandscape == bestLandscape)
                    {
                        if (candidate.width < best.width)
                            better = true;
                        else if (candidate.width == best.width &&
                                 candidate.height < best.height)
                            better = true;
                    }
                }

                if (better)
                {
                    best = candidate;
                    bestScore = score;
                }
            }

            return best;
        }

        private static string MapStartError(Exception ex)
        {
            string msg = ex != null ? ex.Message : string.Empty;
            if (msg.IndexOf("privacy", StringComparison.OrdinalIgnoreCase) >= 0 ||
                msg.IndexOf("permission", StringComparison.OrdinalIgnoreCase) >= 0 ||
                msg.IndexOf("access", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Camera access is required to use Ask AI.";
            }

            return "Camera is unavailable. Check the device camera and privacy settings.";
        }

        private static string MapCaptureError(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return "Camera capture failed. Please try again.";
            if (message.IndexOf("privacy", StringComparison.OrdinalIgnoreCase) >= 0 ||
                message.IndexOf("permission", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Camera access is required to use Ask AI.";
            }

            return "Camera capture failed. Please try again.";
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
            Cancel();
            ClearQueue();
        }
    }
}
#else
namespace Norscope.AskAI
{
    /// <summary>
    /// Stub so the type name exists in Editor/non-WSA compiles.
    /// Device builds use the real PhotoCapture implementation above.
    /// </summary>
    public sealed class WsaPhotoCaptureProvider : UnavailableCameraCapture
    {
        public const int TargetWidth = 1280;
        public const int TargetHeight = 720;
        public const int JpegQuality = 75;

        public WsaPhotoCaptureProvider()
            : base("Camera capture is only available on HoloLens/UWP builds.")
        {
        }
    }
}
#endif
