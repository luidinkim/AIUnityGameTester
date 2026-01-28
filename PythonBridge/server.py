from fastapi import FastAPI, UploadFile, File, Form
from pydantic import BaseModel
import uvicorn
import json
import os
import subprocess
import argparse
import sys

# --- Configuration ---
HOST = "127.0.0.1"
PORT = 8000

# Default paths (will be overridden by args)
CONFIG_FILE = "tools_config.json"

app = FastAPI()

class ActionResponse(BaseModel):
    thought: str
    actionType: str
    screenPosition: dict
    targetPosition: dict
    keyName: str
    textToType: str
    duration: float

def load_config():
    if os.path.exists(CONFIG_FILE):
        try:
            with open(CONFIG_FILE, "r") as f:
                return json.load(f)
        except Exception as e:
            print(f"[Bridge] Error reading config file at {CONFIG_FILE}: {e}")
    else:
        print(f"[Bridge] Config file not found at {CONFIG_FILE}")
    return None

def extract_json(text):
    try:
        start = text.find('{')
        end = text.rfind('}') + 1
        if start != -1 and end != -1:
            json_str = text[start:end]
            return json.loads(json_str)
    except Exception as e:
        print(f"[Bridge] JSON Extraction Error: {e}")
    return None

@app.post("/ask", response_model=ActionResponse)
async def ask_llm(
    screenshot: UploadFile = File(...), 
    context: str = Form(...)
):
    # 1. Save Image
    image_path = os.path.abspath("last_frame.jpg")
    image_bytes = await screenshot.read()
    with open(image_path, "wb") as f:
        f.write(image_bytes)
    
    print(f"[Bridge] Request Received. Context len: {len(context)}")

    # 2. Load Config
    config = load_config()
    
    if not config:
        return create_error_response(f"Configuration file not found at {CONFIG_FILE}")

    tool_name = config.get("selected_tool", "mock_cli")
    tool = config["tools"].get(tool_name)

    if not tool:
        return create_error_response(f"Tool '{tool_name}' not defined in config.")

    # 3. Construct Command
    cmd = tool["command"]
    args_template = tool["arguments"]
    
    # Replace placeholders
    final_args = []
    # System Prompt is now embedded in the tool definition or context, 
    # but strictly speaking, we want a hardcoded system prompt for JSON format.
    # Let's create a minimal system prompt string here if needed, or rely on the tool config.
    system_prompt_text = "You are a QA agent. Respond in JSON."

    for arg in args_template:
        replaced = arg.replace("{image_path}", image_path)
        replaced = replaced.replace("{context}", context)
        replaced = replaced.replace("{system_prompt}", system_prompt_text) 
        final_args.append(replaced)

    full_command = [cmd] + final_args
    print(f"[Bridge] Executing Tool '{tool_name}': {full_command}")

    # 4. Execute CLI
    try:
        # shell=True is often needed for 'python' or complex commands on Windows
        # checking if command is python script execution
        use_shell = False
        if cmd == "python" or cmd.endswith(".py") or cmd == "curl":
            use_shell = True
            
        result = subprocess.run(
            full_command, 
            capture_output=True, 
            text=True, 
            encoding='utf-8',
            shell=use_shell
        )
        
        stdout = result.stdout
        stderr = result.stderr

        if stderr:
            print(f"[Bridge] CLI Stderr: {stderr}")

        # 5. Parse Output
        parsed_json = extract_json(stdout)
        
        if parsed_json:
            print(f"[Bridge] Valid JSON received: {parsed_json.get('actionType')}")
            return parsed_json
        else:
            print(f"[Bridge] Failed to parse JSON. Raw Output:\n{stdout}")
            return create_error_response("Failed to parse JSON from tool output. Check server logs.")

    except Exception as e:
        print(f"[Bridge] Execution Error: {e}")
        return create_error_response(f"Execution Error: {str(e)}")

def create_error_response(msg):
    return {
        "thought": f"Error: {msg}",
        "actionType": "Wait",
        "screenPosition": {"x":0,"y":0},
        "targetPosition": {"x":0,"y":0},
        "keyName": "",
        "textToType": "",
        "duration": 2.0
    }

if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--config", type=str, help="Path to tools_config.json")
    args = parser.parse_args()

    if args.config:
        CONFIG_FILE = args.config

    print(f"Starting MCP Bridge Server at http://{HOST}:{PORT}")
    print(f"Using Config File: {CONFIG_FILE}")
    uvicorn.run(app, host=HOST, port=PORT)
