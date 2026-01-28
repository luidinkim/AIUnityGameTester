# AI Unity Game Tester - Development Progress 🚀

**Repository:** [https://github.com/luidinkim/AIUnityGameTester.git](https://github.com/luidinkim/AIUnityGameTester.git)
**Architecture:** Unity Package (UPM) + Local Python Bridge (MCP)

## 🟢 Completed (구현 완료)

### 1. Core Architecture
- [x] **Unity Package Structure**: Refactored to UPM standard (`Runtime`, `Editor`, `package.json`, `.asmdef`).
- [x] **Dual-Mode Design**: Designed for both Direct API (Cloud) and MCP Bridge (Local/CLI).
- [x] **Stop-and-Think Strategy**: `Time.timeScale` control to handle LLM latency in real-time games.

### 2. Unity Modules (`Runtime`)
- [x] **AI Tester Agent**:
    - Manages the test loop (Capture -> Think -> Act).
    - Includes `Game Description` field for custom prompt injection.
- [x] **Input Executor**:
    - Simulates `Click`, `Drag`, `KeyPress` using Unity's **New Input System**.
    - Supports virtual mouse and keyboard devices.
- [x] **Observer (UI Dumper)**:
    - Extracts Hierarchy tree and screen coordinates from Canvas (UGUI & TextMeshPro).
- [x] **MCP Bridge Client**:
    - Communicates with local Python server via HTTP POST.

### 3. Python Bridge (`PythonBridge`)
- [x] **Universal CLI Wrapper (`server.py`)**:
    - Runs as a FastAPI server.
    - Executes external CLI tools (Claude, Gemini, Curl) based on config.
    - Parses CLI stdout to extract JSON actions.
- [x] **Dynamic Configuration**:
    - `tools_config.json` allows users to define custom tool commands.
    - `mock_agent.py` included for connectivity testing.

### 4. Editor Tooling (`Editor`)
- [x] **AI Control Panel (`PythonBridgeManager`)**:
    - **One-Click Server**: Install `pip` requirements and start/stop server from Unity.
    - **Config Editor**: Create, edit, and save `tools_config.json` directly in the Inspector.
    - **Agent Control**: Find agent, toggle modes, and start/stop tests.
    - **Log Viewer**: View server logs (stdout/stderr) in real-time.

### 5. Bug Fixes & Polish
- [x] **Compiler Errors**: Fixed string escaping, API deprecation, and missing namespace errors.
- [x] **Dependencies**: Added `UniTask`, `InputSystem`, `TextMeshPro` to `package.json`.
- [x] **Meta Files**: All meta files generated and synced with Git.

---

## 🟡 To Do / Next Steps (남은 과제)

### 1. Direct API Implementation
- [ ] **DirectAPIClient.cs**: Implement `ILLMClient` for Google Gemini / OpenAI REST API.
- [ ] **Secure Key Management**: Store API keys in `ScriptableObject` or environment variables (not in code).

### 2. Reporting System
- [ ] **Test Report Generator**: Create an HTML/Markdown report after a test session.
- [ ] **Artifact Saving**: Save screenshots of failed actions or bugs found.

### 3. Usability & Content
- [ ] **Demo Scene**: Create a simple sample scene (e.g., a button and a moving target) to demonstrate the agent.
- [ ] **Preset Configs**: Provide pre-made configs for popular tools (Ollama, cursor-cli, etc.).

### 4. Advanced Features
- [ ] **Vision Analysis**: Add a specialized module to draw bounding boxes on the screenshot for debugging what the AI sees.
- [ ] **Feedback Loop**: Feed the result of the action (success/fail) back to the AI in the next turn.

---
*Last Updated: 2026-01-27*