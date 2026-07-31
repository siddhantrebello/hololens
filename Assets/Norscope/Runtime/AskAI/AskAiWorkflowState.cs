namespace Norscope.AskAI
{
    /// <summary>Ask AI workflow states. Controller owns all transitions.</summary>
    public enum AskAiWorkflowState
    {
        Ready = 0,
        Listening = 1,
        Capturing = 2,
        Processing = 3,
        Answer = 4,
        Error = 5
    }
}
