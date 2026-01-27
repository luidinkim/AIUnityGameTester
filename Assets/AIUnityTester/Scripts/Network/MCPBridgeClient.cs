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
        private const string BASE_URL = "http://127.0.0.1:8000";
        private const string ENDPOINT = "/ask";

        public async UniTask<bool> InitializeAsync()
        {
            // 간단한 헬스 체크 (서버가 켜져 있는지 확인)
            // 실제 구현 시 /health 같은 가벼운 엔드포인트를 호출하는 것이 좋음.
            // 여기서는 생략하고 true 반환.
            Debug.Log("[MCPBridgeClient] Initialized (Target: Localhost)");
            return await UniTask.FromResult(true);
        }

        public async UniTask<AIActionData> RequestActionAsync(Texture2D screenshot, string context)
        {
            byte[] imageBytes = screenshot.EncodeToJPG(75); // 품질 75%로 압축

            WWWForm form = new WWWForm();
            form.AddBinaryData("screenshot", imageBytes, "screen.jpg", "image/jpeg");
            form.AddField("context", context);

            using (UnityWebRequest www = UnityWebRequest.Post(BASE_URL + ENDPOINT, form))
            {
                // 타임아웃 설정 (로컬 LLM은 느릴 수 있음)
                www.timeout = 60; 

                try 
                {
                    await www.SendWebRequest();

                    if (www.result != UnityWebRequest.Result.Success)
                    {
                        Debug.LogError($"[MCPBridgeClient] Network Error: {www.error}");
                        return null;
                    }

                    string jsonResponse = www.downloadHandler.text;
                    // Python 서버의 응답(JSON)을 파싱
                    // 주의: Python의 snake_case와 C#의 camelCase 매핑 필요할 수 있음.
                    // 여기서는 간단히 구조가 같다고 가정하거나 직접 매핑.
                    
                    // JSON 데이터 보정을 위해 간단한 전처리(필요시)
                    return JsonConvert.DeserializeObject<AIActionData>(jsonResponse);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[MCPBridgeClient] Exception: {e.Message}");
                    return null;
                }
            }
        }
    }
}
