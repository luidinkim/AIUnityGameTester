from fastapi import FastAPI, UploadFile, File, Form
from pydantic import BaseModel
import uvicorn
import json
import os
import subprocess
import re

# --- Configuration ---
HOST = "127.0.0.1"
PORT = 8000
CONFIG_FILE = "tools_config.json"
PROMPT_FILE = "system_prompt.txt"

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
        with open(CONFIG_FILE, "r") as f:
            return json.load(f)
    return None

def load_system_prompt():
    if os.path.exists(PROMPT_FILE):
        with open(PROMPT_FILE, "r") as f:
            return f.read()
    return "Respond in JSON."

def extract_json(text):
    """
    Extracts JSON object from a string that might contain other text.
    """
    try:
        # Try finding the first { and last }
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

    # 2. Load Config & Prompt
    config = load_config()
    system_prompt = load_system_prompt()
    
    if not config:
        return create_error_response("Configuration file not found.")

    tool_name = config.get("selected_tool", "mock_cli")
    tool = config["tools"].get(tool_name)

    if not tool:
        return create_error_response(f"Tool '{tool_name}' not defined in config.")

    # 3. Construct Command
    cmd = tool["command"]
    args_template = tool["arguments"]
    
    # Replace placeholders
    final_args = []
    for arg in args_template:
        replaced = arg.replace("{image_path}", image_path)
        replaced = replaced.replace("{context}", context)
        replaced = replaced.replace("{system_prompt}", system_prompt)
        final_args.append(replaced)

    full_command = [cmd] + final_args
    print(f"[Bridge] Executing: {full_command}")

    # 4. Execute CLI
    try:
        # shell=True only if needed (e.g. windows specific commands), generally False is safer
        # On Windows, python commands might need `python` as cmd.
        result = subprocess.run(
            full_command, 
            capture_output=True, 
            text=True, 
            encoding='utf-8'
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
            return create_error_response("Failed to parse JSON from tool output.")

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
        "duration": 1.0
    }

if __name__ == "__main__":
    print(f"Starting MCP Bridge Server at http://{HOST}:{PORT}")
    print(f"Edit 'tools_config.json' to configure your AI tool.")
    uvicorn.run(app, host=HOST, port=PORT)