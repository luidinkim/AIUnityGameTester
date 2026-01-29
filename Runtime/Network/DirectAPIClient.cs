using System;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using AIUnityTester.Data;

namespace AIUnityTester.Network
{
    /// <summary>
    /// Python 브리지 없이 직접 LLM API를 호출하는 클라이언트.
    /// 지원 모델: Claude Opus 4.5 (Thinking), Gemini 3 Pro (High), Gemini 3 Flash
    /// </summary>
    public class DirectAPIClient : ILLMClient
    {
        public enum APIProvider
        {
            GeminiFlash,    // Gemini 3 Flash (빠른 응답)
            GeminiPro,      // Gemini 3 Pro High (고성능)
            ClaudeOpus      // Claude Opus 4.5 with Thinking
        }

        public APIProvider Provider { get; set; } = APIProvider.GeminiFlash;
        public string ApiKey { get; set; } = "";
        public string ModelName { get; private set; }
        public string SystemPrompt { get; set; } = "";

        // API Endpoints
        private const string GEMINI_API_URL = "https://generativelanguage.googleapis.com/v1beta/models/{0}:generateContent?key={1}";
        private const string ANTHROPIC_API_URL = "https://api.anthropic.com/v1/messages";

        // Model Names
        private const string MODEL_GEMINI_FLASH = "gemini-3.0-flash";
        private const string MODEL_GEMINI_PRO = "gemini-3.0-pro-high";
        private const string MODEL_CLAUDE_OPUS = "claude-opus-4-5-20250420";

        public DirectAPIClient(APIProvider provider, string apiKey)
        {
            Provider = provider;
            ApiKey = apiKey;

            // Provider에 따른 모델명 자동 설정
            ModelName = provider switch
            {
                APIProvider.GeminiFlash => MODEL_GEMINI_FLASH,
                APIProvider.GeminiPro => MODEL_GEMINI_PRO,
                APIProvider.ClaudeOpus => MODEL_CLAUDE_OPUS,
                _ => MODEL_GEMINI_FLASH
            };

            // Default system prompt
            SystemPrompt = @"You are an autonomous QA agent testing a Unity game.
Analyze the provided game screen image and the UI context text.
Decide the next action to test the game or find bugs.

RESPONSE FORMAT:
You MUST respond ONLY with a valid JSON object. Do not include markdown code blocks or any explanation outside the JSON.

JSON SCHEMA:
{
  ""thought"": ""Reasoning behind the action"",
  ""actionType"": ""Click"" | ""Drag"" | ""Wait"" | ""KeyPress"" | ""Type"",
  ""screenPosition"": { ""x"": 0.0 to 1.0, ""y"": 0.0 to 1.0 },
  ""targetPosition"": { ""x"": 0.0 to 1.0, ""y"": 0.0 to 1.0 }, 
  ""keyName"": ""Space"" | ""W"" | ""Enter"" ... (if KeyPress),
  ""textToType"": ""string"" (if Type),
  ""duration"": float (seconds, for Wait)
}";
        }

        public async UniTask<bool> InitializeAsync()
        {
            if (string.IsNullOrEmpty(ApiKey))
            {
                Debug.LogError("[DirectAPIClient] API Key is not set!");
                return false;
            }

            Debug.Log($"[DirectAPIClient] Initialized (Provider: {Provider}, Model: {ModelName})");
            return await UniTask.FromResult(true);
        }

        public async UniTask<AIActionData> RequestActionAsync(Texture2D screenshot, string context)
        {
            try
            {
                string base64Image = EncodeToBase64(screenshot);
                string fullPrompt = context;

                string response;
                
                if (Provider == APIProvider.ClaudeOpus)
                {
                    response = await CallClaudeAPIAsync(base64Image, fullPrompt);
                }
                else
                {
                    response = await CallGeminiAPIAsync(base64Image, fullPrompt);
                }

                if (string.IsNullOrEmpty(response))
                {
                    Debug.LogError("[DirectAPIClient] Empty response from API");
                    return CreateErrorAction("Empty response from API");
                }

                // Extract JSON from response
                AIActionData action = ParseActionFromResponse(response);
                return action;
            }
            catch (Exception e)
            {
                Debug.LogError($"[DirectAPIClient] Error: {e.Message}");
                return CreateErrorAction(e.Message);
            }
        }

        private string EncodeToBase64(Texture2D texture)
        {
            byte[] bytes = texture.EncodeToJPG(75);
            return Convert.ToBase64String(bytes);
        }

        private async UniTask<string> CallGeminiAPIAsync(string base64Image, string prompt)
        {
            string url = string.Format(GEMINI_API_URL, ModelName, ApiKey);

            // Gemini API Request Body
            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new object[]
                        {
                            new { text = SystemPrompt + "\n\n" + prompt },
                            new
                            {
                                inline_data = new
                                {
                                    mime_type = "image/jpeg",
                                    data = base64Image
                                }
                            }
                        }
                    }
                },
                generationConfig = new
                {
                    temperature = 0.4f,
                    maxOutputTokens = 2048
                }
            };

            string jsonBody = JsonConvert.SerializeObject(requestBody);

            using (UnityWebRequest www = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
                www.uploadHandler = new UploadHandlerRaw(bodyRaw);
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");
                www.timeout = 120; // Gemini Pro는 더 오래 걸릴 수 있음

                await www.SendWebRequest();

                if (www.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[DirectAPIClient] Gemini API Error: {www.error}\n{www.downloadHandler.text}");
                    return null;
                }

                // Parse Gemini response
                JObject responseJson = JObject.Parse(www.downloadHandler.text);
                string text = responseJson["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString();
                return text;
            }
        }

        private async UniTask<string> CallClaudeAPIAsync(string base64Image, string prompt)
        {
            // Claude Opus 4.5 with Extended Thinking
            var requestBody = new
            {
                model = ModelName,
                max_tokens = 16000,
                thinking = new
                {
                    type = "enabled",
                    budget_tokens = 10000  // Thinking에 할당할 토큰
                },
                messages = new object[]
                {
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new
                            {
                                type = "image",
                                source = new
                                {
                                    type = "base64",
                                    media_type = "image/jpeg",
                                    data = base64Image
                                }
                            },
                            new { type = "text", text = prompt }
                        }
                    }
                },
                system = SystemPrompt
            };

            string jsonBody = JsonConvert.SerializeObject(requestBody);

            using (UnityWebRequest www = new UnityWebRequest(ANTHROPIC_API_URL, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
                www.uploadHandler = new UploadHandlerRaw(bodyRaw);
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");
                www.SetRequestHeader("x-api-key", ApiKey);
                www.SetRequestHeader("anthropic-version", "2023-06-01");
                www.timeout = 180; // Thinking 모드는 오래 걸림

                await www.SendWebRequest();

                if (www.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[DirectAPIClient] Claude API Error: {www.error}\n{www.downloadHandler.text}");
                    return null;
                }

                // Parse Claude response
                JObject responseJson = JObject.Parse(www.downloadHandler.text);
                
                // Claude Thinking 응답에서 text 추출
                JArray content = responseJson["content"] as JArray;
                if (content != null)
                {
                    foreach (var block in content)
                    {
                        if (block["type"]?.ToString() == "text")
                        {
                            return block["text"]?.ToString();
                        }
                    }
                }

                return null;
            }
        }

        private AIActionData ParseActionFromResponse(string response)
        {
            try
            {
                // Find JSON in response (handle markdown code blocks)
                string jsonStr = response;
                
                // Remove markdown code blocks if present
                if (response.Contains("```json"))
                {
                    int start = response.IndexOf("```json") + 7;
                    int end = response.IndexOf("```", start);
                    if (end > start)
                    {
                        jsonStr = response.Substring(start, end - start).Trim();
                    }
                }
                else if (response.Contains("```"))
                {
                    int start = response.IndexOf("```") + 3;
                    int end = response.IndexOf("```", start);
                    if (end > start)
                    {
                        jsonStr = response.Substring(start, end - start).Trim();
                    }
                }
                else
                {
                    // Try to find raw JSON
                    int start = response.IndexOf('{');
                    int end = response.LastIndexOf('}') + 1;
                    if (start >= 0 && end > start)
                    {
                        jsonStr = response.Substring(start, end - start);
                    }
                }

                // Parse JSON to AIActionData
                JObject json = JObject.Parse(jsonStr);

                AIActionData action = new AIActionData
                {
                    thought = json["thought"]?.ToString() ?? "",
                    actionType = json["actionType"]?.ToString() ?? "Wait",
                    screenPosition = new Vector2(
                        json["screenPosition"]?["x"]?.Value<float>() ?? 0f,
                        json["screenPosition"]?["y"]?.Value<float>() ?? 0f
                    ),
                    targetPosition = new Vector2(
                        json["targetPosition"]?["x"]?.Value<float>() ?? 0f,
                        json["targetPosition"]?["y"]?.Value<float>() ?? 0f
                    ),
                    keyName = json["keyName"]?.ToString() ?? "",
                    textToType = json["textToType"]?.ToString() ?? "",
                    duration = json["duration"]?.Value<float>() ?? 1.0f
                };

                Debug.Log($"[DirectAPIClient] Parsed action: {action.actionType} - {action.thought}");
                return action;
            }
            catch (Exception e)
            {
                Debug.LogError($"[DirectAPIClient] Failed to parse response: {e.Message}\nRaw: {response}");
                return CreateErrorAction($"Parse error: {e.Message}");
            }
        }

        private AIActionData CreateErrorAction(string message)
        {
            return new AIActionData
            {
                thought = $"Error: {message}",
                actionType = "Wait",
                screenPosition = Vector2.zero,
                targetPosition = Vector2.zero,
                keyName = "",
                textToType = "",
                duration = 2.0f
            };
        }
    }
}
