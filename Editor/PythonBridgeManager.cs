using UnityEngine;
using UnityEditor;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Collections.Generic;
using AIUnityTester.Core; 
using Newtonsoft.Json.Linq;

namespace AIUnityTester.Editor
{
    public class PythonBridgeManager : EditorWindow
    {
        // --- Server Process Variables ---
        private static Process _serverProcess;
        private string _pythonPath = "python";
        private string _serverScriptPath;
        private string _userConfigPath;
        private string _systemPromptPath;
        
        private Vector2 _scrollPos;
        private Vector2 _configScrollPos;
        private Vector2 _promptScrollPos;
        private static StringBuilder _serverLog = new StringBuilder();
        private int _serverPort = 8000;  // 서버 포트
        
        private string _configContent = "";
        private string _systemPromptContent = "";
        private bool _isConfigDirty = false;
        private bool _isPromptDirty = false;

        // --- Tool Selection ---
        private List<string> _toolNames = new List<string>();
        private int _selectedToolIndex = 0;
        private string _currentToolDescription = "";

        // --- Foldout States ---
        private bool _showAgentSection = true;
        private bool _showToolSection = true;
        private bool _showPromptSection = false;
        private bool _showServerSection = true;
        private bool _showAdvancedConfig = false;
        private bool _showReportSection = false;

        // --- Report ---
        private string _reportFolderPath;

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
                _serverScriptPath = Path.GetFullPath("Assets/Plugins/AIUnityGameTester/PythonBridge/server.py");
            }

            // 2. 유저 설정 파일 경로 (Assets/AIUnityTesterConfig/)
            string configFolder = Path.Combine(Application.dataPath, "AIUnityTesterConfig");
            if (!Directory.Exists(configFolder)) Directory.CreateDirectory(configFolder);
            
            _userConfigPath = Path.Combine(configFolder, "tools_config.json");
            _systemPromptPath = Path.Combine(configFolder, "system_prompt.txt");
            
            // 설정 파일이 없으면 패키지에서 복사
            CopyDefaultFileIfMissing(_userConfigPath, "tools_config.json");
            CopyDefaultFileIfMissing(_systemPromptPath, "system_prompt.txt");

            // 설정 파일 로드
            LoadConfig();
            LoadSystemPrompt();
            ParseToolList();

            FindAgent();
            
            // 저장된 포트 불러오기
            _serverPort = PlayerPrefs.GetInt("AITester_ServerPort", 8000);
        }

        private void OnDisable()
        {
            // 에디터 창 닫힐 때 서버 정리
            StopServer();
        }

        private void CopyDefaultFileIfMissing(string targetPath, string fileName)
        {
            if (File.Exists(targetPath)) return;

            string packagePath = $"Packages/com.luidin.ai-unity-tester/PythonBridge/{fileName}";
            string fullPackagePath = Path.GetFullPath(packagePath);
            
            if (!File.Exists(fullPackagePath))
            {
                fullPackagePath = Path.GetFullPath($"Assets/Plugins/AIUnityGameTester/PythonBridge/{fileName}");
            }

            if (File.Exists(fullPackagePath))
            {
                File.Copy(fullPackagePath, targetPath);
                UnityEngine.Debug.Log($"[AI Tester] Copied default {fileName} to {targetPath}");
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginVertical();
            
            GUILayout.Label("🤖 AI Unity Tester Control Panel", EditorStyles.boldLabel);
            DrawServerStatus();
            DrawSeparator();
            
            _showAgentSection = EditorGUILayout.Foldout(_showAgentSection, "🎮 Agent Control", true);
            if (_showAgentSection) DrawAgentControlSection();
            
            DrawSeparator();
            
            _showToolSection = EditorGUILayout.Foldout(_showToolSection, "🔧 Tool Selection", true);
            if (_showToolSection) DrawToolSelectionSection();
            
            DrawSeparator();
            
            _showPromptSection = EditorGUILayout.Foldout(_showPromptSection, "📝 System Prompt", true);
            if (_showPromptSection) DrawSystemPromptSection();
            
            DrawSeparator();
            
            _showServerSection = EditorGUILayout.Foldout(_showServerSection, "🐍 Python Bridge Server", true);
            if (_showServerSection) DrawServerControlSection();
            
            DrawSeparator();
            
            _showAdvancedConfig = EditorGUILayout.Foldout(_showAdvancedConfig, "⚙️ Advanced Configuration (JSON)", true);
            if (_showAdvancedConfig) DrawConfigEditorSection();
            
            DrawSeparator();
            
            _showReportSection = EditorGUILayout.Foldout(_showReportSection, "📄 Test Reports", true);
            if (_showReportSection) DrawReportSection();
            
            EditorGUILayout.EndVertical();
        }

        private void DrawReportSection()
        {
            EditorGUI.indentLevel++;
            
            if (string.IsNullOrEmpty(_reportFolderPath))
            {
                _reportFolderPath = Path.Combine(Application.persistentDataPath, "AITesterReports");
            }

            EditorGUILayout.HelpBox($"Reports are saved to:\n{_reportFolderPath}", MessageType.None);
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("📂 Open Report Folder"))
            {
                if (!Directory.Exists(_reportFolderPath))
                {
                    Directory.CreateDirectory(_reportFolderPath);
                }
                System.Diagnostics.Process.Start("explorer.exe", _reportFolderPath.Replace("/", "\\"));
            }
            
            if (GUILayout.Button("🔄 Refresh"))
            {
                // Future: List recent reports
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUI.indentLevel--;
        }

        private void DrawServerStatus()
        {
            EditorGUILayout.BeginHorizontal();
            
            bool isRunning = _serverProcess != null && !_serverProcess.HasExited;
            string statusIcon = isRunning ? "🟢" : "🔴";
            string statusText = isRunning ? "Server Running" : "Server Stopped";
            
            GUILayout.Label($"{statusIcon} {statusText}", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            
            if (_toolNames.Count > 0 && _selectedToolIndex < _toolNames.Count)
            {
                GUILayout.Label($"Tool: {_toolNames[_selectedToolIndex]}", EditorStyles.miniLabel);
            }
            
            EditorGUILayout.EndHorizontal();
        }

        private void DrawAgentControlSection()
        {
            EditorGUI.indentLevel++;
            
            if (_targetAgent == null)
            {
                EditorGUILayout.HelpBox("AITesterAgent not found in scene.", MessageType.Warning);
                if (GUILayout.Button("Find Agent in Scene")) FindAgent();
            }
            else
            {
                EditorGUILayout.ObjectField("Target Agent", _targetAgent, typeof(AITesterAgent), true);

                EditorGUILayout.LabelField("Game Description:");
                EditorGUI.BeginChangeCheck();
                string newDesc = EditorGUILayout.TextArea(_targetAgent.gameDescription, GUILayout.Height(60));
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_targetAgent, "Modify Game Description");
                    _targetAgent.gameDescription = newDesc;
                }

                GUILayout.Space(5);

                EditorGUI.indentLevel--;

                GUILayout.Space(5);
                EditorGUILayout.LabelField("Simulation Settings:");
                EditorGUI.indentLevel++;
                
                EditorGUI.BeginChangeCheck();
                float newDelay = EditorGUILayout.FloatField("Action Delay", _targetAgent.actionDelay);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_targetAgent, "Modify Action Delay");
                    _targetAgent.actionDelay = newDelay;
                }

                EditorGUI.BeginChangeCheck();
                bool newRecord = EditorGUILayout.Toggle("Record Report", _targetAgent.recordTestReport);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_targetAgent, "Modify Record Report");
                    _targetAgent.recordTestReport = newRecord;
                }
                EditorGUI.indentLevel--;

                GUILayout.Space(5);
                
                if (Application.isPlaying)
                {
                    EditorGUILayout.BeginHorizontal();
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
                    EditorGUILayout.EndHorizontal();
                }
                else
                {
                    EditorGUILayout.HelpBox("Enter Play Mode to run tests.", MessageType.Info);
                }
            }
            
            EditorGUI.indentLevel--;
        }

        private void DrawToolSelectionSection()
        {
            EditorGUI.indentLevel++;
            
            if (_toolNames.Count == 0)
            {
                EditorGUILayout.HelpBox("No tools defined. Check tools_config.json.", MessageType.Warning);
                if (GUILayout.Button("Reload Configuration"))
                {
                    LoadConfig();
                    ParseToolList();
                }
            }
            else
            {
                EditorGUI.BeginChangeCheck();
                _selectedToolIndex = EditorGUILayout.Popup("Active Tool", _selectedToolIndex, _toolNames.ToArray());
                if (EditorGUI.EndChangeCheck())
                {
                    UpdateSelectedTool();
                }

                if (!string.IsNullOrEmpty(_currentToolDescription))
                {
                    EditorGUILayout.HelpBox(_currentToolDescription, MessageType.None);
                }

                // --- Tool Specific API Key ---
                try
                {
                    JObject config = JObject.Parse(_configContent);
                    string toolName = _toolNames[_selectedToolIndex];
                    JObject tool = config["tools"]?[toolName] as JObject;
                    
                    if (tool != null && tool["requires_api_key"] != null && (bool)tool["requires_api_key"])
                    {
                        string currentKey = tool["api_key"]?.ToString() ?? "";
                        if (currentKey == "YOUR_API_KEY_HERE") currentKey = "";

                        EditorGUI.BeginChangeCheck();
                        string newKey = EditorGUILayout.TextField("Tool API Key", currentKey);
                        if (EditorGUI.EndChangeCheck())
                        {
                            tool["api_key"] = newKey;
                            _configContent = config.ToString(Newtonsoft.Json.Formatting.Indented);
                            SaveConfig(); // Auto-save
                        }
                    }
                }
                catch {}
                // -----------------------------

                GUILayout.Space(5);
                
                GUILayout.Space(5);
                
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Add New Tool..."))
                {
                    ShowAddToolWizard();
                }
                if (GUILayout.Button("Edit Tool..."))
                {
                    ShowEditToolWizard();
                }
                if (GUILayout.Button("Refresh"))
                {
                    LoadConfig();
                    ParseToolList();
                }
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUI.indentLevel--;
        }

        private void DrawSystemPromptSection()
        {
            EditorGUI.indentLevel++;
            
            if (GUILayout.Button("Reload from File")) LoadSystemPrompt();

            _promptScrollPos = EditorGUILayout.BeginScrollView(_promptScrollPos, GUILayout.Height(120));
            EditorGUI.BeginChangeCheck();
            _systemPromptContent = EditorGUILayout.TextArea(_systemPromptContent, GUILayout.ExpandHeight(true));
            if (EditorGUI.EndChangeCheck())
            {
                _isPromptDirty = true;
            }
            EditorGUILayout.EndScrollView();

            if (_isPromptDirty)
            {
                GUI.backgroundColor = Color.yellow;
                if (GUILayout.Button("💾 Save System Prompt"))
                {
                    SaveSystemPrompt();
                }
                GUI.backgroundColor = Color.white;
            }
            
            EditorGUI.indentLevel--;
        }

        private void DrawConfigEditorSection()
        {
            EditorGUI.indentLevel++;
            
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
                if (GUILayout.Button("💾 Save Configuration"))
                {
                    SaveConfig();
                    ParseToolList();
                }
                GUI.backgroundColor = Color.white;
            }
            EditorGUILayout.HelpBox($"Path: {_userConfigPath}", MessageType.None);
            
            EditorGUI.indentLevel--;
        }

        private void DrawServerControlSection()
        {
            EditorGUI.indentLevel++;
            
            _pythonPath = EditorGUILayout.TextField("Python Command", _pythonPath);
            
            EditorGUI.BeginChangeCheck();
            _serverPort = EditorGUILayout.IntField("Server Port", _serverPort);
            if (EditorGUI.EndChangeCheck())
            {
                PlayerPrefs.SetInt("AITester_ServerPort", _serverPort);
                PlayerPrefs.Save();
            }

            if (_serverProcess == null || _serverProcess.HasExited)
            {
                EditorGUILayout.BeginHorizontal();
                GUI.backgroundColor = Color.green;
                if (GUILayout.Button("▶ Start Server", GUILayout.Height(25))) StartServer();
                GUI.backgroundColor = Color.white;

                if (GUILayout.Button("Install Requirements", GUILayout.Height(25))) InstallRequirements();
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                GUI.backgroundColor = Color.red;
                if (GUILayout.Button("⏹ Stop Server", GUILayout.Height(25))) StopServer();
                GUI.backgroundColor = Color.white;
                
                GUILayout.Label("Server Logs:");
                _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, EditorStyles.helpBox, GUILayout.Height(100));
                EditorGUILayout.TextArea(_serverLog.ToString(), EditorStyles.miniLabel);
                EditorGUILayout.EndScrollView();
                
                if (GUILayout.Button("Clear Logs", EditorStyles.miniButton)) _serverLog.Clear();
                
                GUILayout.Space(5);
                if (GUILayout.Button("Reset Memory (Restart Processes)", EditorStyles.miniButton))
                {
                    ResetServerMemory();
                }
                
                GUILayout.Space(5);
                if (GUILayout.Button("Reset Memory (Restart Processes)", EditorStyles.miniButton))
                {
                    ResetServerMemory();
                }
            }
            
            EditorGUI.indentLevel--;
        }

        private void ParseToolList()
        {
            _toolNames.Clear();
            
            if (string.IsNullOrEmpty(_configContent)) return;

            try
            {
                JObject config = JObject.Parse(_configContent);
                string selectedTool = config["selected_tool"]?.ToString() ?? "";
                JObject tools = config["tools"] as JObject;

                if (tools != null)
                {
                    int index = 0;
                    foreach (var tool in tools)
                    {
                        _toolNames.Add(tool.Key);
                        if (tool.Key == selectedTool)
                        {
                            _selectedToolIndex = index;
                            _currentToolDescription = tool.Value["description"]?.ToString() ?? "";
                        }
                        index++;
                    }
                }
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError($"[AI Tester] Failed to parse config: {e.Message}");
            }
        }

        private void UpdateSelectedTool()
        {
            if (_selectedToolIndex >= 0 && _selectedToolIndex < _toolNames.Count)
            {
                string toolName = _toolNames[_selectedToolIndex];
                
                try
                {
                    JObject config = JObject.Parse(_configContent);
                    config["selected_tool"] = toolName;
                    _configContent = config.ToString(Newtonsoft.Json.Formatting.Indented);
                    
                    JObject tools = config["tools"] as JObject;
                    if (tools != null && tools[toolName] != null)
                    {
                        _currentToolDescription = tools[toolName]["description"]?.ToString() ?? "";
                    }
                    
                    SaveConfig();
                }
                catch (System.Exception e)
                {
                    UnityEngine.Debug.LogError($"[AI Tester] Failed to update tool: {e.Message}");
                }
            }
        }

        private void ShowAddToolWizard()
        {
            AddToolWizard.ShowWindow(_userConfigPath, () => {
                LoadConfig();
                ParseToolList();
            });
        }
        
        private void ShowEditToolWizard()
        {
            if (_selectedToolIndex < 0 || _selectedToolIndex >= _toolNames.Count) return;
            
            string toolName = _toolNames[_selectedToolIndex];
            try
            {
                JObject config = JObject.Parse(_configContent);
                JObject tool = config["tools"]?[toolName] as JObject;
                if (tool != null)
                {
                    EditToolWizard.ShowWindow(_userConfigPath, toolName, tool, () => {
                        LoadConfig();
                        ParseToolList();
                        for(int i=0; i<_toolNames.Count; i++) {
                            if (_toolNames[i] == toolName) _selectedToolIndex = i;
                        }
                    });
                }
            }
            catch {}
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
                UnityEngine.Debug.Log("[AI Tester] Configuration Saved.");
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError($"[AI Tester] Failed to save config: {e.Message}");
            }
        }

        private void LoadSystemPrompt()
        {
            if (File.Exists(_systemPromptPath))
            {
                _systemPromptContent = File.ReadAllText(_systemPromptPath);
                _isPromptDirty = false;
            }
        }

        private void SaveSystemPrompt()
        {
            try
            {
                File.WriteAllText(_systemPromptPath, _systemPromptContent);
                _isPromptDirty = false;
                UnityEngine.Debug.Log("[AI Tester] System Prompt Saved.");
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError($"[AI Tester] Failed to save system prompt: {e.Message}");
            }
        }

        private void StartServer()
        {
            if (!File.Exists(_serverScriptPath))
            {
                UnityEngine.Debug.LogError($"[AI Tester] Server script not found at: {_serverScriptPath}");
                return;
            }

            try 
            {
                string serverDir = Path.GetDirectoryName(_serverScriptPath);
                string serverScript = Path.GetFileName(_serverScriptPath);
                string args = $"\"{serverScript}\" --config \"{_userConfigPath}\" --prompt \"{_systemPromptPath}\" --port {_serverPort}";

                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = _pythonPath;
                psi.Arguments = args;
                psi.WorkingDirectory = serverDir;
                psi.UseShellExecute = true;
                psi.CreateNoWindow = false;

                _serverProcess = new Process();
                _serverProcess.StartInfo = psi;
                _serverProcess.Start();

                UnityEngine.Debug.Log($"[AI Tester] Python Server Started. WorkDir: {serverDir}");
                UnityEngine.Debug.Log($"[AI Tester] Config: {_userConfigPath}");
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError($"[AI Tester] Failed to start server: {e.Message}");
            }
        }
        
        private void StopServer()
        {
            if (_serverProcess != null && !_serverProcess.HasExited)
            {
                try
                {
                    _serverProcess.Kill();
                    _serverProcess.WaitForExit(3000);
                }
                catch (System.Exception e)
                {
                    UnityEngine.Debug.LogWarning($"[AI Tester] Process kill warning: {e.Message}");
                }
                _serverProcess.Dispose();
                _serverProcess = null;
            }

            KillProcessOnPort(_serverPort);
            
            UnityEngine.Debug.Log($"[AI Tester] Python Bridge Server Stopped (Port {_serverPort} freed).");
        }

        private void KillProcessOnPort(int port)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "cmd.exe";
                psi.Arguments = $"/c netstat -ano | findstr :{port} | findstr LISTENING";
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                psi.CreateNoWindow = true;

                using (Process p = Process.Start(psi))
                {
                    string output = p.StandardOutput.ReadToEnd();
                    p.WaitForExit();

                    if (!string.IsNullOrEmpty(output))
                    {
                        string[] lines = output.Split('\n');
                        foreach (string line in lines)
                        {
                            string[] parts = line.Trim().Split(new char[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length >= 5)
                            {
                                string pid = parts[parts.Length - 1];
                                if (int.TryParse(pid, out int pidNum) && pidNum > 0)
                                {
                                    try
                                    {
                                        Process.GetProcessById(pidNum).Kill();
                                        UnityEngine.Debug.Log($"[AI Tester] Killed process {pidNum} on port {port}");
                                    }
                                    catch { }
                                }
                            }
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning($"[AI Tester] Port cleanup warning: {e.Message}");
            }
        }

        private void InstallRequirements()
        {
            RunCommand(_pythonPath, "-m pip install fastapi uvicorn pydantic python-multipart");
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

                if (!string.IsNullOrEmpty(output)) UnityEngine.Debug.Log($"[AI Tester] {output}");
                if (!string.IsNullOrEmpty(error))
                {
                    if (p.ExitCode != 0) UnityEngine.Debug.LogError($"[AI Tester] {error}");
                    else UnityEngine.Debug.LogWarning($"[AI Tester] {error}");
                }
            }
        }

        private void FindAgent()
        {
            _targetAgent = Object.FindAnyObjectByType<AITesterAgent>();
        }

        private void DrawSeparator()
        {
            GUILayout.Space(5);
            Rect rect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 1));
            GUILayout.Space(5);
        }
        
        private void OnDestroy()
        {
            StopServer();
        }

        private async void ResetServerMemory()
        {
            if (_serverProcess == null || _serverProcess.HasExited) return;
            string url = $"http://127.0.0.1:{_serverPort}/reset";
            using (UnityEngine.Networking.UnityWebRequest request = new UnityEngine.Networking.UnityWebRequest(url, "POST"))
            {
                request.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
                var op = request.SendWebRequest();
                while (!op.isDone) await System.Threading.Tasks.Task.Yield();

                if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    UnityEngine.Debug.Log("[AI Tester] Memory Reset Successful.");
                }
                else
                {
                    UnityEngine.Debug.LogError($"[AI Tester] Check failed: {request.error}");
                }
            }
        }
    }

    // --- Add Tool Wizard ---
    public class AddToolWizard : EditorWindow
    {
        private string _toolName = "";
        private string _command = "";
        private string _arguments = "";
        private string _description = "";
        private bool _requiresApiKey = false;
        private bool _imageSupport = true;
        private string _modelName = "";

        private string _configPath;
        private System.Action _onComplete;

        public static void ShowWindow(string configPath, System.Action onComplete)
        {
            var window = GetWindow<AddToolWizard>("Add New Tool");
            window._configPath = configPath;
            window._onComplete = onComplete;
            window.minSize = new Vector2(400, 300);
        }

        private void OnGUI()
        {
            GUILayout.Label("Add New LLM Tool", EditorStyles.boldLabel);
            GUILayout.Space(10);

            _toolName = EditorGUILayout.TextField("Tool Name (ID)", _toolName);
            _command = EditorGUILayout.TextField("Command", _command);
            
            EditorGUILayout.LabelField("Arguments (comma separated):");
            _arguments = EditorGUILayout.TextArea(_arguments, GUILayout.Height(40));
            
            _description = EditorGUILayout.TextField("Description", _description);
            _modelName = EditorGUILayout.TextField("Model Name", _modelName);
            _requiresApiKey = EditorGUILayout.Toggle("Requires API Key", _requiresApiKey);
            _imageSupport = EditorGUILayout.Toggle("Image Support", _imageSupport);

            GUILayout.Space(10);
            
            EditorGUILayout.HelpBox("Placeholders: {system_prompt}, {context}, {image_path}, {image_base64}", MessageType.Info);

            GUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Cancel")) Close();
            
            GUI.enabled = !string.IsNullOrEmpty(_toolName) && !string.IsNullOrEmpty(_command);
            if (GUILayout.Button("Add Tool"))
            {
                AddTool();
                Close();
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
        }

        private void AddTool()
        {
            try
            {
                string json = File.ReadAllText(_configPath);
                JObject config = JObject.Parse(json);
                
                JObject tools = config["tools"] as JObject;
                if (tools == null)
                {
                    tools = new JObject();
                    config["tools"] = tools;
                }

                string[] argArray = _arguments.Split(',');
                JArray argsJson = new JArray();
                foreach (var arg in argArray)
                {
                   if(!string.IsNullOrWhiteSpace(arg)) argsJson.Add(arg.Trim());
                }

                JObject newTool = new JObject
                {
                    ["command"] = _command,
                    ["arguments"] = argsJson,
                    ["description"] = _description,
                    ["requires_api_key"] = _requiresApiKey,
                    ["image_support"] = _imageSupport
                };
                if (!string.IsNullOrEmpty(_modelName)) newTool["model_name"] = _modelName;

                tools[_toolName] = newTool;

                File.WriteAllText(_configPath, config.ToString(Newtonsoft.Json.Formatting.Indented));
                UnityEngine.Debug.Log($"[AI Tester] Tool '{_toolName}' added successfully.");

                _onComplete?.Invoke();
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError($"[AI Tester] Failed to add tool: {e.Message}");
            }
        }
    }

    public class EditToolWizard : EditorWindow
    {
        private string _originalToolName = "";
        private string _toolName = "";
        private string _command = "";
        private string _arguments = "";
        private string _description = "";
        private bool _requiresApiKey = false;
        private bool _imageSupport = true;
        private string _modelName = "";

        private string _configPath;
        private System.Action _onComplete;

        public static void ShowWindow(string configPath, string toolName, JObject toolData, System.Action onComplete)
        {
            var window = GetWindow<EditToolWizard>("Edit Tool");
            window._configPath = configPath;
            window._originalToolName = toolName;
            window._toolName = toolName;
            window._onComplete = onComplete;
            
            window._command = toolData["command"]?.ToString() ?? "";
            window._description = toolData["description"]?.ToString() ?? "";
            window._requiresApiKey = (bool?)toolData["requires_api_key"] ?? false;
            window._imageSupport = (bool?)toolData["image_support"] ?? true;
            window._modelName = toolData["model_name"]?.ToString() ?? "";
            
            JArray args = toolData["arguments"] as JArray;
            if (args != null)
            {
                window._arguments = string.Join(", ", args.ToObject<string[]>());
            }

            window.minSize = new Vector2(400, 350);
        }

        private void OnGUI()
        {
            GUILayout.Label($"Edit Tool: {_originalToolName}", EditorStyles.boldLabel);
            GUILayout.Space(10);

            _toolName = EditorGUILayout.TextField("Tool Name (ID)", _toolName);
            _command = EditorGUILayout.TextField("Command", _command);
            
            EditorGUILayout.LabelField("Arguments (comma separated):");
            _arguments = EditorGUILayout.TextArea(_arguments, GUILayout.Height(40));
            
            _description = EditorGUILayout.TextField("Description", _description);
            _modelName = EditorGUILayout.TextField("Model Name", _modelName);
            _requiresApiKey = EditorGUILayout.Toggle("Requires API Key", _requiresApiKey);
            _imageSupport = EditorGUILayout.Toggle("Image Support", _imageSupport);

            GUILayout.Space(10);
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Cancel")) Close();
            
            if (GUILayout.Button("Save Changes"))
            {
                SaveChanges();
                Close();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void SaveChanges()
        {
            try
            {
                string json = File.ReadAllText(_configPath);
                JObject config = JObject.Parse(json);
                JObject tools = config["tools"] as JObject;

                string[] argArray = _arguments.Split(',');
                JArray argsJson = new JArray();
                foreach (var arg in argArray) if(!string.IsNullOrWhiteSpace(arg)) argsJson.Add(arg.Trim());

                JObject newToolData = new JObject
                {
                    ["command"] = _command,
                    ["arguments"] = argsJson,
                    ["description"] = _description,
                    ["requires_api_key"] = _requiresApiKey,
                    ["image_support"] = _imageSupport
                };
                if (!string.IsNullOrEmpty(_modelName)) newToolData["model_name"] = _modelName;
                
                if (tools[_originalToolName] != null)
                {
                    string oldKey = tools[_originalToolName]["api_key"]?.ToString();
                    if (!string.IsNullOrEmpty(oldKey)) newToolData["api_key"] = oldKey;
                }

                if (_toolName != _originalToolName)
                {
                    tools.Remove(_originalToolName);
                    if (config["selected_tool"]?.ToString() == _originalToolName)
                    {
                        config["selected_tool"] = _toolName;
                    }
                }
                
                tools[_toolName] = newToolData;

                File.WriteAllText(_configPath, config.ToString(Newtonsoft.Json.Formatting.Indented));
                UnityEngine.Debug.Log($"[AI Tester] Tool '{_toolName}' updated.");
                _onComplete?.Invoke();
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError($"[AI Tester] Failed to save tool: {e.Message}");
            }
        }
    }
}