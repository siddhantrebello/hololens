using System;

namespace Norscope.AskAI
{
    public enum AskAiAnalysisEventKind
    {
        Success = 0,
        Error = 1
    }

    public readonly struct AskAiAnalysisEvent
    {
        public AskAiAnalysisEventKind Kind { get; }
        public int SessionToken { get; }
        public AskAiAnalysisResponse Response { get; }

        public AskAiAnalysisEvent(
            AskAiAnalysisEventKind kind,
            int sessionToken,
            AskAiAnalysisResponse response)
        {
            Kind = kind;
            SessionToken = sessionToken;
            Response = response;
        }

        public static AskAiAnalysisEvent Success(int token, AskAiAnalysisResponse response) =>
            new AskAiAnalysisEvent(AskAiAnalysisEventKind.Success, token, response);

        public static AskAiAnalysisEvent Error(int token, AskAiAnalysisResponse response) =>
            new AskAiAnalysisEvent(AskAiAnalysisEventKind.Error, token, response);
    }
}
