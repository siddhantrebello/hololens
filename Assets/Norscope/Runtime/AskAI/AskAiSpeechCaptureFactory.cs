namespace Norscope.AskAI
{
    /// <summary>Creates the platform-appropriate Ask AI speech capture implementation.</summary>
    public static class AskAiSpeechCaptureFactory
    {
        public static IAskAiSpeechCapture Create()
        {
#if UNITY_EDITOR
            return new EditorMockSpeechCapture();
#elif UNITY_WSA
            return new WsaDictationSpeechCapture();
#else
            return new UnavailableSpeechCapture(
                "Speech recognition is unavailable on this platform.");
#endif
        }
    }
}
