# Tech Stack & Decision Records

## 1. Core Platform
*   **Engine:** Unity 2022.3 LTS (or Unity 6)
*   **Language:** C# (C# 9.0 compliant)
*   **Architecture Pattern:** Strategy Pattern (To switch between AI Providers)

## 2. AI Connectivity (Dual Mode)

### Mode A: Direct API (Cloud)
*   **Provider:** Google Gemini Pro Vision / OpenAI GPT-4o
*   **Protocol:** HTTPS (UnityWebRequest)
*   **Use Case:** Production, High-Quality CI/CD runs.

### Mode B: MCP Bridge (Local/Subscription)
*   **Provider:** Local LLM (Ollama), Claude Code, Gemini CLI
*   **Middleware:** Python (FastAPI + Uvicorn)
    *   *Reason:* Unity와 로컬 CLI/MCP Tool 사이의 통신 중계(Buffer) 및 프로토콜 변환.
*   **Protocol:** WebSocket or HTTP (Localhost)
*   **Use Case:** Infinite Debugging, Dev-loop, Cost saving.

## 3. Unity Modules
*   **Input System:** `com.unity.inputsystem` (Virtual Input simulation)
*   **Async:** `UniTask` (Essential for managing TimeScale pausing and API waiting)
*   **Serialization:** `Newtonsoft.Json`

## 4. Testing & Reporting
*   **Output:** Markdown Logs + Screenshots