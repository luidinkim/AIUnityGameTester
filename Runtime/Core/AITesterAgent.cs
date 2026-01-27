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
        public bool useMCPBridgeMode = true; // Editor Window에서 접근 가능하도록 public으로 변경
        [SerializeField] private float actionDelay = 1.0f; 

        [Header("Modules")]
        [SerializeField] private Modules.InputExecutor executor; 
        [SerializeField] private Modules.UIHierarchyDumper uiDumper; 

        private ILLMClient _llmClient;
        public bool IsRunning { get; private set; } = false; // 외부에서 상태 확인 가능

        private void Start()
        {
            // 모듈 자동 연결
            if (executor == null) executor = GetComponent<Modules.InputExecutor>();
            if (uiDumper == null) uiDumper = GetComponent<Modules.UIHierarchyDumper>();
            if (uiDumper == null) uiDumper = gameObject.AddComponent<Modules.UIHierarchyDumper>();

            Debug.Log("Agent Ready. Open 'AI Tester > Control Panel' to operate.");
        }

        private void Update()
        {
            // 키보드 단축키 유지 (선택 사항)
            if (Input.GetKeyDown(KeyCode.K) && !IsRunning)
            {
                StartTest();
            }
        }

        public void StartTest()
        {
            if (IsRunning) return;
            
            // 모드에 따라 클라이언트 다시 초기화
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
            
            // 초기화 대기
            UniTask initTask = _llmClient.InitializeAsync();
            yield return new WaitUntil(() => initTask.Status.IsCompleted());

            while (IsRunning)
            {
                // 1. Freeze & Capture
                // Time.timeScale = 0; // 필요시 활성화
                yield return new WaitForEndOfFrame();

                Texture2D screenShot = CaptureScreen();
                string context = GetGameContext();

                // 2. Think (Async)
                var task = _llmClient.RequestActionAsync(screenShot, context);
                yield return new WaitUntil(() => task.Status.IsCompleted());

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
