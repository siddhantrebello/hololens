using UnityEngine;

namespace Norscope.AskAI
{
    /// <summary>
    /// Safe backend configuration only. Never store API keys or secrets here.
    /// </summary>
    [CreateAssetMenu(
        fileName = "AskAiBackendConfig",
        menuName = "Norscope/Ask AI/Backend Config")]
    public sealed class AskAiBackendConfig : ScriptableObject
    {
        [Tooltip("Editor: http://127.0.0.1:8000  |  HL2 LAN: http://<PC-LAN-IP>:8000  |  Prod: https://api.norscope.com")]
        [SerializeField] private string baseUrl = "http://127.0.0.1:8000";

        [SerializeField] private float requestTimeoutSeconds = 45f;

        [Tooltip("When true in the Unity Editor, use MockAskAiAnalysisService instead of HTTP.")]
        [SerializeField] private bool useMockInEditor = true;

        public string BaseUrl => string.IsNullOrWhiteSpace(baseUrl)
            ? "http://127.0.0.1:8000"
            : baseUrl.Trim().TrimEnd('/');

        public float RequestTimeoutSeconds => Mathf.Clamp(requestTimeoutSeconds, 5f, 120f);

        public bool UseMockInEditor => useMockInEditor;

        public string AskAiEndpoint => BaseUrl + "/api/v1/ask-ai";
    }
}
