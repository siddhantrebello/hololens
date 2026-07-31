namespace Norscope.AskAI
{
    public enum AskAiCameraEventKind
    {
        Success = 0,
        Error = 1
    }

    /// <summary>Queued camera event drained on the Unity main thread.</summary>
    public readonly struct AskAiCameraEvent
    {
        public AskAiCameraEventKind Kind { get; }
        public int SessionToken { get; }
        public AskAiCapturedImage Image { get; }
        public string ErrorMessage { get; }

        public AskAiCameraEvent(
            AskAiCameraEventKind kind,
            int sessionToken,
            AskAiCapturedImage image,
            string errorMessage)
        {
            Kind = kind;
            SessionToken = sessionToken;
            Image = image;
            ErrorMessage = errorMessage ?? string.Empty;
        }

        public static AskAiCameraEvent Success(int sessionToken, AskAiCapturedImage image) =>
            new AskAiCameraEvent(AskAiCameraEventKind.Success, sessionToken, image, null);

        public static AskAiCameraEvent Error(int sessionToken, string message) =>
            new AskAiCameraEvent(AskAiCameraEventKind.Error, sessionToken, null, message);
    }
}
