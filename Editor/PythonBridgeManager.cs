using UnityEngine;
using UnityEditor;
using System.Diagnostics;
using System.IO;
using System.Text;
using AIUnityTester.Core; 

namespace AIUnityTester.Editor
{
    public class PythonBridgeManager : EditorWindow
    {
        // --- Server Process Variables ---
        private static Process _serverProcess;
        private string _pythonPath = "python";
        private string _serverScriptPath;
        private string _userConfigPath; // 유저가 편집 가능한 설정 파일 경로
        
        private Vector2 _scrollPos;
        private Vector2 _configScrollPos;
        private static StringBuilder _serverLog = new StringBuilder();
        
        private string _configContent = "";
        private bool _isConfigDirty = false;

        // --- Agent Control Variables ---
        private AITesterAgent _targetAgent;

        [MenuItem("AI Tester/Control Panel", false, 0)]
        public static void ShowWindow()
        {
            GetWindow<PythonBridgeManager>("AI Control Panel");
        }

        private void OnEnable()
        {
            // 1. 서버 스크립트 경로 (패키지 내부)
            string packageScriptPath = "Packages/com.luidin.ai-unity-tester/PythonBridge/server.py";
            _serverScriptPath = Path.GetFullPath(packageScriptPath);
            if (!File.Exists(_serverScriptPath))
            {
                // 로컬 테스트용 폴백
                _serverScriptPath = Path.GetFullPath("Assets/AIUnityTester/PythonBridge/server.py");
            }

            // 2. 유저 설정 파일 경로 (Assets/AIUnityTesterConfig/tools_config.json)
            string configFolder = Path.Combine(Application.dataPath, "AIUnityTesterConfig");
            if (!Directory.Exists(configFolder)) Directory.CreateDirectory(configFolder);
            
            _userConfigPath = Path.Combine(configFolder, "tools_config.json");
            
            // 설정 파일이 없으면 기본값 생성
            if (!File.Exists(_userConfigPath))
            {
                CreateDefaultConfig(_userConfigPath);
            }

            // 설정 파일 로드
            LoadConfig();

            FindAgent();
        }

        private void OnGUI()
        {
            GUILayout.Label("AI Unity Tester Control Panel", EditorStyles.boldLabel);
            DrawSeparator();
            
            DrawAgentControlSection();
            DrawSeparator();
            
            DrawConfigEditorSection(); // 설정 편집기 추가
            DrawSeparator();
            
            DrawServerControlSection();
        }

        private void DrawAgentControlSection()
        {
            GUILayout.Label("🤖 Agent Control", EditorStyles.boldLabel);
            
            if (_targetAgent == null)
            {
                EditorGUILayout.HelpBox("AITesterAgent not found in scene.", MessageType.Warning);
                if (GUILayout.Button("Find Agent in Scene")) FindAgent();
            }
            else
            {
                EditorGUILayout.ObjectField("Target Agent", _targetAgent, typeof(AITesterAgent), true);

                // 게임 설명 편집 (Agent의 필드를 직접 수정)
                EditorGUILayout.LabelField("Game Description:");
                EditorGUI.BeginChangeCheck();
                string newDesc = EditorGUILayout.TextArea(_targetAgent.gameDescription, GUILayout.Height(60));
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_targetAgent, "Modify Game Description");
                    _targetAgent.gameDescription = newDesc;
                }

                GUILayout.Space(5);
                
                // 실행 버튼들
                if (Application.isPlaying)
                {
                    if (!_targetAgent.IsRunning)
                    {
                        GUI.backgroundColor = Color.cyan;
                        if (GUILayout.Button("▶ Start AI Test", GUILayout.Height(30))) _targetAgent.StartTest();
                        GUI.backgroundColor = Color.white;
                    }
                    else
                    {
                        GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
                        if (GUILayout.Button("⏹ Stop AI Test", GUILayout.Height(30))) _targetAgent.StopTest();
                        GUI.backgroundColor = Color.white;
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("Enter Play Mode to run tests.", MessageType.Info);
                }
            }
        }

        private void DrawConfigEditorSection()
        {
            GUILayout.Label("⚙️ Tool Configuration (tools_config.json)", EditorStyles.boldLabel);
            
            if (GUILayout.Button("Reload Config File")) LoadConfig();

            _configScrollPos = EditorGUILayout.BeginScrollView(_configScrollPos, GUILayout.Height(100));
            EditorGUI.BeginChangeCheck();
            _configContent = EditorGUILayout.TextArea(_configContent, GUILayout.ExpandHeight(true));
            if (EditorGUI.EndChangeCheck())
            {
                _isConfigDirty = true;
            }
            EditorGUILayout.EndScrollView();

            if (_isConfigDirty)
            {
                GUI.backgroundColor = Color.yellow;
                if (GUILayout.Button("Save Configuration"))
                {
                    SaveConfig();
                }
                GUI.backgroundColor = Color.white;
            }
            EditorGUILayout.HelpBox($"Path: {_userConfigPath}", MessageType.None);
        }

        private void DrawServerControlSection()
        {
            GUILayout.Label("🐍 Python Bridge Server", EditorStyles.boldLabel); 
            
            _pythonPath = EditorGUILayout.TextField("Python Command", _pythonPath);

            if (_serverProcess == null || _serverProcess.HasExited)
            {
                GUI.backgroundColor = Color.green;
                if (GUILayout.Button("Start Server", GUILayout.Height(25))) StartServer();
                GUI.backgroundColor = Color.white;

                if (GUILayout.Button("Install Requirements (pip)", GUILayout.Height(20))) InstallRequirements();
            }
            else
            {
                GUI.backgroundColor = Color.red;
                if (GUILayout.Button("Stop Server", GUILayout.Height(25))) StopServer();
                GUI.backgroundColor = Color.white;
                
                GUILayout.Label("Server Logs:");
                _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, EditorStyles.helpBox, GUILayout.Height(120));
                EditorGUILayout.TextArea(_serverLog.ToString(), EditorStyles.miniLabel);
                EditorGUILayout.EndScrollView();
                
                if (GUILayout.Button("Clear Logs", EditorStyles.miniButton)) _serverLog.Clear();
            }
        }

        // --- Helper Methods ---

        private void CreateDefaultConfig(string path)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"selected_tool\": \"mock_cli\",");
            sb.AppendLine("  \"tools\": {");
            sb.AppendLine("    \"mock_cli\": {");
            sb.AppendLine("      \"command\": \"python\",");
            sb.AppendLine("      \"arguments\": [\"-c\", \"import json; print(json.dumps({'thought':'Mock Action', 'actionType':'Wait', 'duration':1.0}))\"],");
            sb.AppendLine("      \"description\": \"Internal mock for testing.\"");
            sb.AppendLine("    },");
            sb.AppendLine("    \"claude_cli\": {");
            sb.AppendLine("      \"command\": \"claude\",");
            sb.AppendLine("      \"arguments\": [\"--message\", \"{system_prompt}\\n\\nGame Description: {context}\\nImage: {image_path}\"],");
            sb.AppendLine("      \"description\": \"Anthropic Claude CLI\"");
            sb.AppendLine("    }");
            sb.AppendLine("  }");
            sb.AppendLine("}");
            
            File.WriteAllText(path, sb.ToString());
        }

        private void LoadConfig()
        {
            if (File.Exists(_userConfigPath))
            {
                _configContent = File.ReadAllText(_userConfigPath);
                _isConfigDirty = false;
            }
        }

        private void SaveConfig()
        {
            try
            {
                File.WriteAllText(_userConfigPath, _configContent);
                _isConfigDirty = false;
                UnityEngine.Debug.Log("Configuration Saved.");
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError($"Failed to save config: {e.Message}");
            }
        }

        private void StartServer()
        {
            if (!File.Exists(_serverScriptPath))
            {
                UnityEngine.Debug.LogError($"Server script not found at: {_serverScriptPath}");
                return;
            }

            try 
            {
                // --config 인자로 유저 설정 파일 경로를 전달
                string args = $"--config \"{_userConfigPath}\" ";

                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = _pythonPath;
                psi.Arguments = args;
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.CreateNoWindow = true;

                _serverProcess = new Process();
                _serverProcess.StartInfo = psi;
                
                _serverProcess.OutputDataReceived += (sender, e) => {
                    if (!string.IsNullOrEmpty(e.Data)) {
                        _serverLog.AppendLine(e.Data);
                        Repaint(); 
                    }
                };
                _serverProcess.ErrorDataReceived += (sender, e) => {
                    if (!string.IsNullOrEmpty(e.Data)) {
                        _serverLog.AppendLine($"[ERR] {e.Data}");
                        Repaint();
                    }
                };

                _serverProcess.Start();
                _serverProcess.BeginOutputReadLine();
                _serverProcess.BeginErrorReadLine();

                UnityEngine.Debug.Log($"Python Server Started. Config: {_userConfigPath}");
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError($"Failed to start server: {e.Message}");
            }
        }

        // ... StopServer, InstallRequirements, RunCommand, FindAgent, DrawSeparator 등은 기존과 동일하거나 위 코드에 포함됨 ...
        
        private void StopServer()
        {
            if (_serverProcess != null && !_serverProcess.HasExited)
            {
                _serverProcess.Kill();
                _serverProcess.Dispose();
                _serverProcess = null;
                UnityEngine.Debug.Log("Python Bridge Server Stopped.");
            }
        }

        private void InstallRequirements()
        {
            RunCommand(_pythonPath, "-m pip install fastapi uvicorn pydantic multipart");
        }

        private void RunCommand(string cmd, string args)
        {
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = cmd;
            psi.Arguments = args;
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.CreateNoWindow = true;

            using (Process p = Process.Start(psi))
            {
                string output = p.StandardOutput.ReadToEnd();
                string error = p.StandardError.ReadToEnd();
                p.WaitForExit();

                if (!string.IsNullOrEmpty(output)) UnityEngine.Debug.Log($"CMD Output: {output}");
                if (!string.IsNullOrEmpty(error))
                {
                    if (p.ExitCode != 0) UnityEngine.Debug.LogError($"CMD Error: {error}");
                    else UnityEngine.Debug.LogWarning($"CMD Notice: {error}");
                }
            }
        }

        private void FindAgent()
        {
            _targetAgent = Object.FindAnyObjectByType<AITesterAgent>();
        }

        private void DrawSeparator()
        {
            GUILayout.Space(10);
            Rect rect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 1));
            GUILayout.Space(10);
        }
        
        private void OnDestroy()
        {
            StopServer();
        }
    }
}