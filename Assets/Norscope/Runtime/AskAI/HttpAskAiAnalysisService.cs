using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace Norscope.AskAI
{
    /// <summary>
    /// Multipart HTTP client for the NORSCOPE Ask AI backend.
    /// Does not contain OpenAI types or API keys.
    /// </summary>
    public sealed class HttpAskAiAnalysisService : IAskAiAnalysisService
    {
        private const int MaxQuestionChars = 2000;
        private const int MaxImageBytes = 5 * 1024 * 1024;

        private readonly AskAiBackendConfig _config;
        private readonly MonoBehaviour _host;
        private readonly ConcurrentQueue<AskAiAnalysisEvent> _queue =
            new ConcurrentQueue<AskAiAnalysisEvent>();

        private UnityWebRequest _activeRequest;
        private Coroutine _routine;
        private int _activeToken;
        private bool _busy;
        private bool _disposed;

        public HttpAskAiAnalysisService(AskAiBackendConfig config, MonoBehaviour host)
        {
            _config = config;
            _host = host;
        }

        public bool IsBusy => _busy;

        public bool TryStartAnalysis(AskAiRequest request, int sessionToken, out string errorMessage)
        {
            errorMessage = null;
            if (_disposed)
            {
                errorMessage = "Analysis service was disposed.";
                return false;
            }

            if (_busy)
            {
                errorMessage = "An analysis request is already active.";
                return false;
            }

            if (_host == null || !_host.isActiveAndEnabled)
            {
                errorMessage = "Analysis host is unavailable.";
                return false;
            }

            if (request == null ||
                string.IsNullOrWhiteSpace(request.Transcript) ||
                !request.HasImage)
            {
                errorMessage = "The captured image could not be analyzed. Please capture it again.";
                return false;
            }

            if (request.Transcript.Length > MaxQuestionChars)
            {
                errorMessage = "The question is too long. Please try a shorter question.";
                return false;
            }

            if (request.ImageBytes.Length > MaxImageBytes)
            {
                errorMessage = "The captured image could not be analyzed. Please capture it again.";
                return false;
            }

            if (_config == null || string.IsNullOrWhiteSpace(_config.BaseUrl))
            {
                errorMessage = "Unable to connect to the AI service. Check the network and try again.";
                return false;
            }

            ClearQueue();
            _activeToken = sessionToken;
            _busy = true;
            _routine = _host.StartCoroutine(SendRoutine(request, sessionToken));
            return true;
        }

        public void Cancel()
        {
            if (_routine != null && _host != null)
            {
                _host.StopCoroutine(_routine);
                _routine = null;
            }

            if (_activeRequest != null)
            {
                try { _activeRequest.Abort(); } catch { /* ignore */ }
                try { _activeRequest.Dispose(); } catch { /* ignore */ }
                _activeRequest = null;
            }

            _busy = false;
            ClearQueue();
        }

        public bool TryDequeue(out AskAiAnalysisEvent analysisEvent)
        {
            return _queue.TryDequeue(out analysisEvent);
        }

        private IEnumerator SendRoutine(AskAiRequest request, int sessionToken)
        {
            // Copy JPEG bytes without re-encoding.
            byte[] jpeg = request.ImageBytes;
            string question = request.Transcript.Trim();
            string sessionId = sessionToken.ToString();
            string timestamp = request.TimestampUtc == default
                ? DateTime.UtcNow.ToString("o")
                : request.TimestampUtc.ToString("o");

            var form = new List<IMultipartFormSection>
            {
                new MultipartFormDataSection("question", question),
                new MultipartFormFileSection(
                    "image", jpeg, "capture.jpg", "image/jpeg"),
                new MultipartFormDataSection("session_id", sessionId),
                new MultipartFormDataSection("timestamp_utc", timestamp)
            };

            string url = _config.AskAiEndpoint;
            UnityWebRequest www = UnityWebRequest.Post(url, form);
            www.timeout = Mathf.CeilToInt(_config.RequestTimeoutSeconds);
            _activeRequest = www;

            yield return www.SendWebRequest();

            if (sessionToken != _activeToken)
            {
                CleanupRequest(www);
                _busy = false;
                _routine = null;
                yield break;
            }

            AskAiAnalysisResponse response = ParseResponse(www);
            CleanupRequest(www);
            _busy = false;
            _routine = null;

            if (response.Success)
                _queue.Enqueue(AskAiAnalysisEvent.Success(sessionToken, response));
            else
                _queue.Enqueue(AskAiAnalysisEvent.Error(sessionToken, response));
        }

        private AskAiAnalysisResponse ParseResponse(UnityWebRequest www)
        {
            long code = www.responseCode;
            string body = www.downloadHandler != null
                ? www.downloadHandler.text
                : string.Empty;

            bool networkError =
                www.result == UnityWebRequest.Result.ConnectionError;
            bool timeout =
                www.result == UnityWebRequest.Result.ConnectionError &&
                !string.IsNullOrEmpty(www.error) &&
                www.error.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0;

            if (www.result == UnityWebRequest.Result.ConnectionError ||
                www.result == UnityWebRequest.Result.DataProcessingError)
            {
                if (timeout ||
                    (!string.IsNullOrEmpty(www.error) &&
                     www.error.IndexOf("timed out", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    return AskAiAnalysisResponse.Fail(
                        "TIMEOUT",
                        "The AI response took too long. Please try again.");
                }

                return AskAiAnalysisResponse.Fail(
                    "NO_NETWORK",
                    "Unable to connect to the AI service. Check the network and try again.");
            }

            AskAiAnalysisResponseDto dto = null;
            if (!string.IsNullOrWhiteSpace(body))
            {
                try
                {
                    dto = JsonUtility.FromJson<AskAiAnalysisResponseDto>(body);
                }
                catch
                {
                    dto = null;
                }
            }

            if (code == 200 && dto != null && dto.success)
            {
                if (string.IsNullOrWhiteSpace(dto.answer))
                {
                    return AskAiAnalysisResponse.Fail(
                        "INVALID_JSON",
                        "The AI service is temporarily unavailable.",
                        dto.request_id);
                }

                return new AskAiAnalysisResponse
                {
                    Success = true,
                    Answer = dto.answer,
                    Confidence = dto.confidence,
                    VisualObservations = dto.visual_observations ?? Array.Empty<string>(),
                    RecommendedAction = dto.recommended_action,
                    SafetyWarning = dto.safety_warning,
                    NeedsMoreInformation = dto.needs_more_information,
                    RequestId = dto.request_id ?? string.Empty,
                    UserMessage = dto.answer
                };
            }

            if (dto != null && !dto.success && dto.error != null &&
                !string.IsNullOrWhiteSpace(dto.error.message))
            {
                return AskAiAnalysisResponse.Fail(
                    dto.error.code ?? "BACKEND_ERROR",
                    dto.error.message,
                    dto.request_id);
            }

            return AskAiAnalysisResponse.Fail(
                MapStatusCode(code),
                MapStatusMessage(code),
                dto != null ? dto.request_id : null);
        }

        private static string MapStatusCode(long code)
        {
            if (code == 408 || code == 504) return "TIMEOUT";
            if (code == 413 || code == 415 || code == 400) return "INVALID_IMAGE";
            if (code == 429) return "RATE_LIMITED";
            if (code == 0) return "NO_NETWORK";
            return "BACKEND_ERROR";
        }

        private static string MapStatusMessage(long code)
        {
            if (code == 408 || code == 504)
                return "The AI response took too long. Please try again.";
            if (code == 413 || code == 415 || code == 400)
                return "The captured image could not be analyzed. Please capture it again.";
            if (code == 429)
                return "The AI service is busy. Please try again shortly.";
            if (code == 0)
                return "Unable to connect to the AI service. Check the network and try again.";
            return "The AI service is temporarily unavailable.";
        }

        private void CleanupRequest(UnityWebRequest www)
        {
            if (_activeRequest == www)
                _activeRequest = null;
            if (www != null)
                www.Dispose();
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
        }

        [Serializable]
        private sealed class AskAiAnalysisResponseDto
        {
            public bool success;
            public string answer;
            public float confidence;
            public string[] visual_observations;
            public string recommended_action;
            public string safety_warning;
            public bool needs_more_information;
            public string request_id;
            public AskAiApiErrorDto error;
        }

        [Serializable]
        private sealed class AskAiApiErrorDto
        {
            public string code;
            public string message;
        }
    }
}
