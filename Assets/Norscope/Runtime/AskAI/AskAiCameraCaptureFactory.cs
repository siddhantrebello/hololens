namespace Norscope.AskAI
{
    /// <summary>Creates the platform-appropriate Ask AI camera capture implementation.</summary>
    public static class AskAiCameraCaptureFactory
    {
        public static IAskAiCameraCapture Create()
        {
#if UNITY_EDITOR
            return new EditorMockCameraCapture();
#elif UNITY_WSA
            return new WsaPhotoCaptureProvider();
#else
            return new UnavailableCameraCapture(
                "Camera capture is unavailable on this platform.");
#endif
        }
    }
}
