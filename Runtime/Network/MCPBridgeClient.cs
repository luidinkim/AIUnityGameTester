using System;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using AIUnityTester.Data;

namespace AIUnityTester.Network
{
    public class MCPBridgeClient : ILLMClient
    {
        private string _baseUrl;
        private const string ENDPOINT = "/ask";

        public string ApiKey { get; set; }
        public int Port { get; set; } = 8000;

        public MCPBridgeClient(int port = 8000, string apiKey = null)
        {
            Port = port;
            ApiKey = apiKey;
            _baseUrl = $"http://127.0.0.1:{Port}";
        }

        public async UniTask<bool> InitializeAsync()
        {
            Debug.Log($"[MCPBridgeClient] Initialized (Target: {_baseUrl})");
            
            // Health check
            try
            {
                using (UnityWebRequest www = UnityWebRequest.Get(_baseUrl + "/health"))
                {
                    www.timeout = 5;
                    await www.SendWebRequest();
                    
                    if (www.result == UnityWebRequest.Result.Success)
                    {
                        Debug.Log("[MCPBridgeClient] Server is healthy!");
                        return true;
                    }
                    else
                    {
                        Debug.LogError($"[MCPBridgeClient] Server health check failed: {www.error}");
                        return false;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[MCPBridgeClient] Connection failed: {e.Message}");
                return false;
            }
        }

        public async UniTask<AIActionData> RequestActionAsync(Texture2D screenshot, string context)
        {
            byte[] imageBytes = screenshot.EncodeToJPG(75);

            WWWForm form = new WWWForm();
            form.AddBinaryData("screenshot", imageBytes, "screen.jpg", "image/jpeg");
            form.AddField("context", context); 
            if (!string.IsNullOrEmpty(ApiKey))
            {
                form.AddField("api_key", ApiKey);
            }

            using (UnityWebRequest www = UnityWebRequest.Post(_baseUrl + ENDPOINT, form))
            {
                www.timeout = 120; // 로컬 LLM은 느릴 수 있음

                try 
                {
                    await www.SendWebRequest();

                    if (www.result != UnityWebRequest.Result.Success)
                    {
                        Debug.LogError($"[MCPBridgeClient] Network Error: {www.error}");
                        return CreateErrorAction(www.error);
                    }

                    string jsonResponse = www.downloadHandler.text;
                    return JsonConvert.DeserializeObject<AIActionData>(jsonResponse);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[MCPBridgeClient] Exception: {e.Message}");
                    return CreateErrorAction(e.Message);
                }
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
