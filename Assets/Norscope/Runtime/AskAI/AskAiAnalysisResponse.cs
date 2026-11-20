using System;

namespace Norscope.AskAI
{
    [Serializable]
    public sealed class AskAiApiError
    {
        public string Code;
        public string Message;

        public AskAiApiError()
        {
        }

        public AskAiApiError(string code, string message)
        {
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
        }
    }

    [Serializable]
    public sealed class AskAiAnalysisResponse
    {
        public bool Success;
        public string Answer;
        public float Confidence;
        public string[] VisualObservations;
        public string RecommendedAction;
        public string SafetyWarning;
        public bool NeedsMoreInformation;
        public string RequestId;
        public AskAiApiError Error;
        public string UserMessage;

        public static AskAiAnalysisResponse Fail(string code, string userMessage, string requestId = null)
        {
            return new AskAiAnalysisResponse
            {
                Success = false,
                Answer = string.Empty,
                VisualObservations = Array.Empty<string>(),
                Error = new AskAiApiError(code, userMessage),
                UserMessage = userMessage,
                RequestId = requestId ?? string.Empty
            };
        }
    }
}
