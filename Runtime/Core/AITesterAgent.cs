using System.Collections;
using UnityEngine;
using Cysharp.Threading.Tasks;
using AIUnityTester.Network;
using AIUnityTester.Data;

namespace AIUnityTester.Core
{
    public class AITesterAgent : MonoBehaviour
    {
        public enum ExecutionMode
        {
            MCPBridge,          // Python Bridge를 통한 로컬 LLM
            DirectGeminiFlash,  // Gemini 3 Flash (빠른 응답)
            DirectGeminiPro,    // Gemini 3 Pro High (고성능)
            DirectClaudeOpus    // Claude Opus 4.5 with Thinking
        }

        [Header("Execution Mode")]
        public ExecutionMode executionMode = ExecutionMode.MCPBridge;
        
        [Header("LLM Settings")]
        [Tooltip("API Key for Gemini/Claude. Required for Direct modes AND Native Bridge (High Speed).")]
        public string apiKey = "";

        [Header("Game Context")]
        [TextArea(3, 10)] public string gameDescription = "Describe your game objectives and controls here.";
        public float actionDelay = 1.0f; 

        [Header("Modules")]
        [SerializeField] private Modules.InputExecutor executor; 
        [SerializeField] private Modules.UIHierarchyDumper uiDumper; 

        [Header("Reporting")]
        public bool recordTestReport = true;

        private ILLMClient _llmClient;
        private TestReportManager _reportManager;
        public bool IsRunning { get; private set; } = false; 

        private void Start()
        {
            // 모듈 자동 연결
            if (executor == null) executor = GetComponent<Modules.InputExecutor>();
            if (uiDumper == null) uiDumper = GetComponent<Modules.UIHierarchyDumper>();
            if (uiDumper == null) uiDumper = gameObject.AddComponent<Modules.UIHierarchyDumper>();

            // 리포트 매니저 초기화
            _reportManager = new TestReportManager();

            Debug.Log("[AITesterAgent] Ready. Open 'AI Tester > Control Panel' to operate.");
        }

        public void StartTest()
        {
            if (IsRunning) return;
            
            _llmClient = CreateClient();

            if (_llmClient == null)
            {
                Debug.LogError("[AITesterAgent] Failed to create LLM client.");
                return;
            }

            // 리포트 기록 시작
            if (recordTestReport)
            {
                _reportManager.StartNewReport();
            }

            StartCoroutine(RunTestLoop());
        }

        private ILLMClient CreateClient()
        {
            switch (executionMode)
            {
                case ExecutionMode.MCPBridge:
                    // 에디터에서 설정한 포트 읽기 (기본값 8000)
                    int serverPort = PlayerPrefs.GetInt("AITester_ServerPort", 8000);
                    Debug.Log($"[AITesterAgent] Using MCP Bridge Mode (Port: {serverPort})");
                    return new MCPBridgeClient(serverPort, apiKey);

                case ExecutionMode.DirectGeminiFlash:
                    if (string.IsNullOrEmpty(apiKey))
                    {
                        Debug.LogError("[AITesterAgent] API Key is required for Direct Gemini Flash mode!");
                        return null;
                    }
                    Debug.Log("[AITesterAgent] Using Direct Gemini 3 Flash");
                    return new DirectAPIClient(DirectAPIClient.APIProvider.GeminiFlash, apiKey);

                case ExecutionMode.DirectGeminiPro:
                    if (string.IsNullOrEmpty(apiKey))
                    {
                        Debug.LogError("[AITesterAgent] API Key is required for Direct Gemini Pro mode!");
                        return null;
                    }
                    Debug.Log("[AITesterAgent] Using Direct Gemini 3 Pro (High)");
                    return new DirectAPIClient(DirectAPIClient.APIProvider.GeminiPro, apiKey);

                case ExecutionMode.DirectClaudeOpus:
                    if (string.IsNullOrEmpty(apiKey))
                    {
                        Debug.LogError("[AITesterAgent] API Key is required for Direct Claude Opus mode!");
                        return null;
                    }
                    Debug.Log("[AITesterAgent] Using Direct Claude Opus 4.5 (Thinking)");
                    return new DirectAPIClient(DirectAPIClient.APIProvider.ClaudeOpus, apiKey);

                default:
                    return new MCPBridgeClient();
            }
        }

        public void StopTest()
        {
            IsRunning = false;
            StopAllCoroutines();

            // 리포트 저장
            if (recordTestReport && _reportManager != null && _reportManager.IsRecording)
            {
                _reportManager.EndReport();
                string mdPath = _reportManager.ExportToMarkdown();
                string htmlPath = _reportManager.ExportToHTML();
                Debug.Log($"[AITesterAgent] Reports saved: {mdPath}");
            }

            Debug.Log("=== AI Testing Stopped ===");
        }

        private IEnumerator RunTestLoop()
        {
            IsRunning = true;
            Debug.Log($"=== AI Testing Started (Mode: {executionMode}) ===");
            
            UniTask<bool> initTask = _llmClient.InitializeAsync();
            yield return new WaitUntil(() => initTask.Status.IsCompleted());

            if (!initTask.GetAwaiter().GetResult())
            {
                Debug.LogError("[AITesterAgent] Failed to initialize LLM client.");
                IsRunning = false;
                yield break;
            }

            int failureCount = 0;
            const int maxConsecutiveFailures = 3;

            while (IsRunning)
            {
                yield return new WaitForEndOfFrame();

                Texture2D screenShot = CaptureScreen();
                string context = GetGameContext();
                string fullContext = $"[Game Description]\n{gameDescription}\n\n[Current State]\n{context}";

                var task = _llmClient.RequestActionAsync(screenShot, fullContext);
                yield return new WaitUntil(() => task.Status.IsCompleted());

                AIActionData decision = task.GetAwaiter().GetResult();

                if (decision != null)
                {
                    failureCount = 0; // 성공 시 카운트 초기화
                    
                    // User Request: Print explicit reasoning log
                    Debug.Log($"[AI Reasoning] {decision.thought}");
                    Debug.Log($"[AI Decision] Action: {decision.actionType} | Pos: {decision.screenPosition} | Text: {decision.textToType}");
                    
                    if (recordTestReport)
                    {
                        _reportManager.LogStep(decision.thought, decision, screenShot);
                    }

                    var execTask = ExecuteAction(decision);
                    yield return new WaitUntil(() => execTask.Status.IsCompleted());
                }
                else
                {
                    failureCount++;
                    Debug.LogWarning($"[AITesterAgent] Failed to get decision (Failure {failureCount}/{maxConsecutiveFailures}). Retrying in 2s...");
                    
                    if (failureCount >= maxConsecutiveFailures)
                    {
                        Debug.LogError("[AITesterAgent] Too many consecutive failures. Stopping test.");
                        Destroy(screenShot);
                        StopTest();
                        yield break;
                    }

                    Destroy(screenShot);
                    yield return new WaitForSeconds(2.0f); // 실패 시 잠시 대기
                    continue;
                }

                Destroy(screenShot);
                
                // 만약 결정이 "Wait"이고 고유의 duration이 있다면 그것을 사용, 아니면 기본 actionDelay 사용
                float finalDelay = actionDelay;
                if (decision != null && decision.actionType == "Wait" && decision.duration > 0)
                {
                    finalDelay = decision.duration;
                }
                
                yield return new WaitForSeconds(finalDelay);
            }

            Debug.Log("=== AI Testing Stopped ===");
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

        private async UniTask ExecuteAction(AIActionData action)
        {
            if (executor != null)
            {
                await executor.Execute(action);
            }
            else
            {
                Debug.LogWarning("[AITesterAgent] InputExecutor is missing! Cannot execute action.");
            }
        }
    }
}
