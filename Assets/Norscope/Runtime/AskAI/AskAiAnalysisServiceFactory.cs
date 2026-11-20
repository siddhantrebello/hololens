using UnityEngine;

namespace Norscope.AskAI
{
    public static class AskAiAnalysisServiceFactory
    {
        public static IAskAiAnalysisService Create(AskAiBackendConfig config, MonoBehaviour host)
        {
#if UNITY_EDITOR
            if (config == null || config.UseMockInEditor)
                return new MockAskAiAnalysisService(host);
            return new HttpAskAiAnalysisService(config, host);
#elif UNITY_WSA
            if (config == null)
                return new UnavailableAskAiAnalysisService(
                    "Unable to connect to the AI service. Check the network and try again.");
            return new HttpAskAiAnalysisService(config, host);
#else
            return new UnavailableAskAiAnalysisService();
#endif
        }
    }
}
