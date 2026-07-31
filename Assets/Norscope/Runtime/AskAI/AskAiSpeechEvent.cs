namespace Norscope.AskAI
{
    /// <summary>Speech capture completion causes mapped from the platform recognizer.</summary>
    public enum AskAiSpeechCompletionCause
    {
        Unknown = 0,
        Complete = 1,
        TimeoutExceeded = 2,
        InitialSilenceTimeout = 3,
        BabbleTimeout = 4,
        Cancelled = 5
    }

    public enum AskAiSpeechEventKind
    {
        Hypothesis = 0,
        Result = 1,
        Completed = 2,
        Error = 3
    }

    /// <summary>Queued speech event drained on the Unity main thread.</summary>
    public readonly struct AskAiSpeechEvent
    {
        public AskAiSpeechEventKind Kind { get; }
        public string Text { get; }
        public string ErrorMessage { get; }
        public AskAiSpeechCompletionCause Cause { get; }

        public AskAiSpeechEvent(
            AskAiSpeechEventKind kind,
            string text,
            string errorMessage,
            AskAiSpeechCompletionCause cause)
        {
            Kind = kind;
            Text = text ?? string.Empty;
            ErrorMessage = errorMessage ?? string.Empty;
            Cause = cause;
        }

        public static AskAiSpeechEvent Hypothesis(string text) =>
            new AskAiSpeechEvent(AskAiSpeechEventKind.Hypothesis, text, null,
                AskAiSpeechCompletionCause.Unknown);

        public static AskAiSpeechEvent Result(string text) =>
            new AskAiSpeechEvent(AskAiSpeechEventKind.Result, text, null,
                AskAiSpeechCompletionCause.Unknown);

        public static AskAiSpeechEvent Completed(AskAiSpeechCompletionCause cause) =>
            new AskAiSpeechEvent(AskAiSpeechEventKind.Completed, null, null, cause);

        public static AskAiSpeechEvent Error(string message) =>
            new AskAiSpeechEvent(AskAiSpeechEventKind.Error, null, message,
                AskAiSpeechCompletionCause.Unknown);
    }
}
