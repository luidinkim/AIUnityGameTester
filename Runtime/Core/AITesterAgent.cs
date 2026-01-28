using System.Collections;
using UnityEngine;
using Cysharp.Threading.Tasks;
using AIUnityTester.Network;
using AIUnityTester.Data;

namespace AIUnityTester.Core
{
    public class AITesterAgent : MonoBehaviour
    {
        [Header("Settings")]
        public bool useMCPBridgeMode = true; 
        [TextArea(3, 10)] public string gameDescription = "Describe your game objectives and controls here.";
        [SerializeField] private float actionDelay = 1.0f; 

        [Header("Modules")]
        [SerializeField] private Modules.InputExecutor executor; 
        [SerializeField] private Modules.UIHierarchyDumper uiDumper; 

        private ILLMClient _llmClient;
        public bool IsRunning { get; private set; } = false; 

        private void Start()
        {
            // 모듈 자동 연결
            if (executor == null) executor = GetComponent<Modules.InputExecutor>();
            if (uiDumper == null) uiDumper = GetComponent<Modules.UIHierarchyDumper>();
            if (uiDumper == null) uiDumper = gameObject.AddComponent<Modules.UIHierarchyDumper>();

            Debug.Log("Agent Ready. Open 'AI Tester > Control Panel' to operate.");
        }
        
        // ... (Update 생략) ...

        public void StartTest()
        {
            if (IsRunning) return;
            
            if (useMCPBridgeMode)
            {
                _llmClient = new MCPBridgeClient();
            }
            else
            {
                Debug.LogWarning("DirectAPIClient is not implemented yet. Switching to MCPBridge.");
                _llmClient = new MCPBridgeClient();
            }

            StartCoroutine(RunTestLoop());
        }

        public void StopTest()
        {
            IsRunning = false;
            StopAllCoroutines();
            Debug.Log("=== AI Testing Stopped ===");
        }

        private IEnumerator RunTestLoop()
        {
            IsRunning = true;
            Debug.Log($"=== AI Testing Started (Mode: {(useMCPBridgeMode ? "Local/MCP" : "Cloud API")}) ===");
            
            UniTask initTask = _llmClient.InitializeAsync();
            yield return new WaitUntil(() => initTask.Status.IsCompleted());

            while (IsRunning)
            {
                yield return new WaitForEndOfFrame();

                Texture2D screenShot = CaptureScreen();
                string context = GetGameContext();
                
                // 게임 설명과 컨텍스트를 합쳐서 전달
                string fullContext = $"[Game Description]\n{gameDescription}\n\n[Current State]\n{context}";

                var task = _llmClient.RequestActionAsync(screenShot, fullContext);
                yield return new WaitUntil(() => task.Status.IsCompleted());
// ... (이하 동일)

                AIActionData decision = task.GetAwaiter().GetResult();

                // 3. Resume & Act
                // Time.timeScale = 1; 
                
                if (decision != null)
                {
                    Debug.Log($"[AI Decision] {decision.thought} -> {decision.actionType}");
                    ExecuteAction(decision);
                }
                else
                {
                    Debug.LogError("Failed to get decision from AI.");
                }

                // 4. Wait for result
                yield return new WaitForSeconds(actionDelay);
                
                // 메모리 정리
                Destroy(screenShot);
            }
        }

        private Texture2D CaptureScreen()
        {
            Texture2D tex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            tex.Apply();
            return tex;
        }

        private string GetGameContext()
        {
            if (uiDumper != null)
            {
                return uiDumper.DumpHierarchy();
            }
            return "Context Info Not Available (UIHierarchyDumper missing)";
        }

        private void ExecuteAction(AIActionData action)
        {
            if (executor != null)
            {
                executor.Execute(action);
            }
            else
            {
                Debug.LogWarning("InputExecutor is missing! Cannot execute action.");
            }
        }
    }
}
