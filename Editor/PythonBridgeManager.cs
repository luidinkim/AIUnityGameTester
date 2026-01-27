using UnityEngine;
using UnityEditor;
using System.Diagnostics;
using System.IO;
using System.Text;
using AIUnityTester.Core; // AITesterAgent 참조

namespace AIUnityTester.Editor
{
    public class PythonBridgeManager : EditorWindow
    {
        // --- Server Process Variables ---
        private static Process _serverProcess;
        private string _pythonPath = "python";
        private string _serverScriptPath;
        private Vector2 _scrollPos;
        private static StringBuilder _serverLog = new StringBuilder();

        // --- Agent Control Variables ---
        private AITesterAgent _targetAgent;

        [MenuItem("AI Tester/Control Panel", false, 0)]
        public static void ShowWindow()
        {
            GetWindow<PythonBridgeManager>("AI Control Panel");
        }

        private void OnEnable()
        {
            // UPM 패키지 경로 대응
            string packagePath = "Packages/com.luidin.ai-unity-tester/PythonBridge/server.py";
            _serverScriptPath = Path.GetFullPath(packagePath);

            // 만약 패키지가 아니라 에셋 폴더에 직접 넣었을 경우를 대비한 예외 처리
            if (!File.Exists(_serverScriptPath))
            {
                _serverScriptPath = Path.GetFullPath("Assets/AIUnityTester/PythonBridge/server.py");
            }

            // Auto-find agent on open
            FindAgent();
        }

        private void OnGUI()
        {
            DrawAgentControlSection();
            DrawSeparator();
            DrawServerControlSection();
        }

        private void DrawAgentControlSection()
        {
            GUILayout.Label("🤖 Agent Control", EditorStyles.boldLabel);
            
            if (_targetAgent == null)
            {
                EditorGUILayout.HelpBox("AITesterAgent not found in scene.", MessageType.Warning);
                if (GUILayout.Button("Find Agent in Scene"))
                {
                    FindAgent();
                }
            }
            else
            {
                // Agent Status
                EditorGUILayout.ObjectField("Target Agent", _targetAgent, typeof(AITesterAgent), true);

                // Mode Selection
                EditorGUI.BeginChangeCheck();
                bool useLocal = EditorGUILayout.Toggle("Use Local (MCP) Mode", _targetAgent.useMCPBridgeMode);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_targetAgent, "Toggle AI Mode");
                    _targetAgent.useMCPBridgeMode = useLocal;
                }

                GUILayout.Space(5);

                // Control Buttons
                if (Application.isPlaying)
                {
                    if (!_targetAgent.IsRunning)
                    {
                        GUI.backgroundColor = Color.cyan;
                        if (GUILayout.Button("▶ Start AI Test", GUILayout.Height(30)))
                        {
                            _targetAgent.StartTest();
                        }
                        GUI.backgroundColor = Color.white;
                    }
                    else
                    {
                        GUI.backgroundColor = new Color(1f, 0.5f, 0.5f); // Soft Red
                        if (GUILayout.Button("⏹ Stop AI Test", GUILayout.Height(30)))
                        {
                            _targetAgent.StopTest();
                        }
                        GUI.backgroundColor = Color.white;
                        EditorGUILayout.HelpBox("AI Agent is RUNNING...", MessageType.Info);
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("Enter Play Mode to control the agent.", MessageType.Info);
                }
            }
        }

        private void DrawServerControlSection()
        {
            GUILayout.Label("🐍 Python Bridge Server", EditorStyles.boldLabel); 
            
            _pythonPath = EditorGUILayout.TextField("Python Command", _pythonPath);

            if (_serverProcess == null || _serverProcess.HasExited)
            {
                GUI.backgroundColor = Color.green;
                if (GUILayout.Button("Start Server", GUILayout.Height(25)))
                {
                    StartServer();
                }
                GUI.backgroundColor = Color.white;

                if (GUILayout.Button("Install Requirements (pip)", GUILayout.Height(20)))
                {
                    InstallRequirements();
                }
            }
            else
            {
                GUI.backgroundColor = Color.red;
                if (GUILayout.Button("Stop Server", GUILayout.Height(25)))
                {
                    StopServer();
                }
                GUI.backgroundColor = Color.white;
                
                GUILayout.Label("Server Logs:");
                _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.Height(150), EditorStyles.helpBox);
                EditorGUILayout.TextArea(_serverLog.ToString(), EditorStyles.miniLabel);
                EditorGUILayout.EndScrollView();
                
                if (GUILayout.Button("Clear Logs", EditorStyles.miniButton))
                {
                    _serverLog.Clear();
                }
            }
        }

        private void DrawSeparator()
        {
            GUILayout.Space(10);
            Rect rect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 1));
            GUILayout.Space(10);
        }

        private void FindAgent()
        {
            _targetAgent = FindObjectOfType<AITesterAgent>();
        }

        // --- Server Logic (Same as before) ---

        private void InstallRequirements()
        {
            UnityEngine.Debug.Log("Installing dependencies...");
            RunCommand(_pythonPath, "-m pip install fastapi uvicorn pydantic multipart");
        }

        private void StartServer()
        {
            if (!File.Exists(_serverScriptPath))
            {
                UnityEngine.Debug.LogError("Server script not found at: " + _serverScriptPath);
                return;
            }

            try 
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = _pythonPath;
                psi.Arguments = "\"" + _serverScriptPath + "\"";
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.CreateNoWindow = true;

                _serverProcess = new Process();
                _serverProcess.StartInfo = psi;
                
                _serverProcess.OutputDataReceived += (sender, e) => {
                    if (!string.IsNullOrEmpty(e.Data)) 
                    {
                        _serverLog.AppendLine(e.Data);
                        Repaint(); 
                    }
                };
                _serverProcess.ErrorDataReceived += (sender, e) => {
                    if (!string.IsNullOrEmpty(e.Data)) 
                    {
                        _serverLog.AppendLine("[ERR] " + e.Data);
                        Repaint();
                    }
                };

                _serverProcess.Start();
                _serverProcess.BeginOutputReadLine();
                _serverProcess.BeginErrorReadLine();

                UnityEngine.Debug.Log("Python Bridge Server Started.");
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError("Failed to start server: " + e.Message);
            }
        }

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

        private void RunCommand(string cmd, string args)
        {
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = cmd;
            psi.Arguments = args;
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.CreateNoWindow = true;

            Process p = Process.Start(psi);
            string output = p.StandardOutput.ReadToEnd();
            string error = p.StandardError.ReadToEnd();
            p.WaitForExit();

            UnityEngine.Debug.Log("CMD Output: " + output);
            if (!string.IsNullOrEmpty(error)) UnityEngine.Debug.LogError("CMD Error: " + error);
        }

        private void OnDestroy()
        {
            StopServer();
        }
    }
}
