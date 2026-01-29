# AI Unity Game Tester 🤖🎮

**AI Unity Game Tester** is an autonomous QA agent that uses Multimodal LLMs (Google Gemini 1.5/2.0) to playtest your Unity game, find bugs, and generate reports.

It supports **Headless Local CLI** (Free & Unlimited) and **Direct Cloud API** (Fast) modes, making it versatile for both local debugging and CI/CD pipelines.

---

## ✨ Key Features

### 🧠 Dual AI Modes
1.  **Gemini Headless (Local CLI)**
    *   **Cost**: Free (via Google AI Studio credentials).
    *   **Speed**: Moderate.
    *   **Advantage**: Unlimited queries, no per-request cost. Runs locally via `gemini.cmd` subprocess.
2.  **Gemini Flash API (Cloud)**
    *   **Cost**: API Key billing (Free tier available).
    *   **Speed**: ⚡ Very Fast (0.5s ~ 1.5s).
    *   **Advantage**: Best for quick regression testing.

### 🖱️ Validated Input System
*   **Hybrid Input**: Combines Virtual Mouse (Unity Input System) for gameplay with **Direct UI Event Invocation** (uGUI) for menus.
*   **100% Click Reliability**: Automatically detecting UI elements via Raycast and strictly invoking `IPointerDown/Up/Click` ensures buttons never miss a click.
*   **Coordinate Auto-Correction**: Automatically handles Y-axis inversion between AI vision (Top-Left) and Unity UI (Bottom-Left).

### 📝 Comprehensive Reporting
*   **Reasoning Logs**: See exactly *why* the AI made a decision with dedicated `[AI Reasoning]` logs in the console.
*   **Markdown & HTML Reports**: Auto-generates detailed test reports including:
    *   Step-by-step actions.
    *   AI Thought Process.
    *   Screenshots of every step.

---

## 📦 Installation

### Prerequisites
*   Unity 2022.3 or later.
*   Python 3.10+ installed and added to PATH.
*   **Google Gemini CLI** (for Headless mode):
    ```bash
    pip install -U google-genai
    # Ensure you have authenticated or have API keys set up
    ```

### Install via Unity Package Manager
1.  Open Unity -> Window -> Package Manager.
2.  Click **"+"** -> **"Add package from git URL..."**.
3.  Enter the repository URL:
    ```
    https://github.com/luidinkim/AIUnityGameTester.git?path=/Assets/Plugins/AIUnityGameTester
    ```

---

## 🚀 Getting Started

1.  **Open Control Panel**:
    *   Menu: `AI Tester` -> `Control Panel`.
2.  **Select Driver**:
    *   **Gemini_Headless**: Uses your local Python environment. Best for deep testing.
    *   **Gemini_Flash_API**: Requires API Key. Best for speed.
3.  **Run Server** (Headless Mode only):
    *   Click "Start Server" in the control panel.
    *   Wait for the green "Online" indicator.
4.  **Start Testing**:
    *   Enter Play Mode.
    *   Click "Start Test" in the inspector or Control Panel.

---

## 🛠️ Configuration

The tool configuration is located at `Assets/AIUnityTesterConfig/tools_config.json`.
*   **selected_tool**: Determines which mode runs by default.
*   You can edit this file manually or use the Unity Editor UI.

---

## 🤝 Contributing
Issues and Pull Requests are welcome!
