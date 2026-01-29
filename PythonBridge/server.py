from fastapi import FastAPI, UploadFile, File, Form
from pydantic import BaseModel
import uvicorn
import json
import os
import subprocess
import argparse
import base64
import sys
import threading
import queue
import time
import re
from contextlib import asynccontextmanager
import asyncio

# --- Configuration ---
HOST = "127.0.0.1"
PORT = 8000
CONFIG_FILE = "tools_config.json"
SYSTEM_PROMPT_FILE = "system_prompt.txt"

# ANSI escape code regex
ANSI_ESCAPE = re.compile(r'\x1B(?:[@-Z\\-_]|\[[0-?]*[ -/]*[@-~])')

def log(message):
    print(message, flush=True)

# --- Native SDK Engine (Direct API Replacement) ---
try:
    import google.generativeai as genai
    from PIL import Image
    SDK_AVAILABLE = True
except ImportError:
    SDK_AVAILABLE = False

class NativeGeminiEngine:
    def __init__(self):
        self.current_api_key = None
        self.model = None

    def configure(self, api_key, model_name="gemini-3-flash-preview"):
        if self.current_api_key != api_key: # Re-configure if key changes
             log(f"[Native] Configuring Gemini SDK with provided key")
             genai.configure(api_key=api_key)
             self.current_api_key = api_key
        
        # Always update model object
        self.model = genai.GenerativeModel(model_name)

    async def generate_action(self, system_prompt, context, image_path, api_key, model_name="gemini-3-flash-preview"):
        try:
            self.configure(api_key, model_name)
            
            # Prepare contents
            contents = [f"{system_prompt}\n\n[Game Context]\n{context}\n\nRespond with JSON only."]
            if image_path and os.path.exists(image_path):
                img = Image.open(image_path)
                contents.append(img)
            
            response = self.model.generate_content(contents)
            return response.text, None
        except Exception as e:
            return None, str(e)

    # New: Chat Session Support
    def start_chat(self, tool_name, system_prompt, history=[]):
        if not self.model: return False
        try:
            # Note: system_instruction is supported in newer genai versions
            # If fail, we prepend to first message
            self.chat_sessions[tool_name] = self.model.start_chat(history=history)
            self.chat_syst_prompts[tool_name] = system_prompt
            return True
        except: return False

    async def generate_chat_response(self, tool_name, context, image_path, api_key, model_name):
        try:
            self.configure(api_key, model_name)
            
            if tool_name not in getattr(self, 'chat_sessions', {}):
                if not hasattr(self, 'chat_sessions'): self.chat_sessions = {}
                if not hasattr(self, 'chat_syst_prompts'): self.chat_syst_prompts = {}
                self.chat_sessions[tool_name] = self.model.start_chat(history=[])
                # We need to know system prompt... handled in caller?
            
            chat = self.chat_sessions[tool_name]
            
            # Construct message
            msg_parts = []
            
            # Inject system prompt if new or reset? 
            # Chat history keeps context, so system prompt usually sent once or implicitly.
            # But here we just send user context.
            
            # Simple approach: Prepend system prompt to the context if history is empty?
            # Or just send context.
            
            full_text = f"[Game Context]\n{context}\n\nRespond with JSON only."
            if len(chat.history) == 0:
                 # First turn: Add System Prompt
                 # Note: Ideally system_instruction in start_chat, but let's prepend here
                 sys_p = getattr(self, 'chat_syst_prompts', {}).get(tool_name, "")
                 if sys_p: full_text = f"SYSTEM: {sys_p}\n\n{full_text}"

            msg_parts.append(full_text)
            
            if image_path and os.path.exists(image_path):
                img = Image.open(image_path)
                msg_parts.append(img)
                
            response = await chat.send_message_async(msg_parts)
            return response.text, None
        except Exception as e:
            return None, str(e)

native_engine = NativeGeminiEngine()

# --- Gemini Headless Engine (Direct Subprocess) ---
import subprocess

class GeminiHeadlessEngine:
    def __init__(self):
        # On Windows, npm installs usually result in a .cmd file.
        self.cmd_command = "gemini.cmd" if os.name == 'nt' else "gemini"

    async def generate_action(self, system_prompt, context, image_path):
        """Run gemini CLI headlessly without a separate bridge script"""
        try:
            # 1. Prepare Full Input (System + Context + Image)
            # Strategy: Use Stdin for prompt (text) and Arguments for image file.
            
            cmd = [self.cmd_command, "--output-format", "json"]
            
            # Add image if exists
            if image_path and os.path.exists(image_path):
                # We strip the "IMAGE: @" fake syntax and just pass the file path as an arg
                # The CLI usually accepts files as arguments.
                cmd.append(image_path)
            
            # Construct Text Prompt
            # We combine System Prompt and Context here because strict Stdin input is one stream.
            full_text_input = f"{system_prompt}\n\n[Game Context]\n{context}\n\nRespond with JSON only."
            
            log(f"[GeminiHeadless] Running: {' '.join(cmd)}")
            log(f"[GeminiHeadless] Input Length: {len(full_text_input)}")
            
            # 2. Execute
            # shell=True required for .cmd on Windows often
            use_shell = (os.name == 'nt')
            
            # Run synchronously for reliability first (asyncio subprocess with .cmd is tricky on Windows)
            # We use a thread to avoid blocking the event loop
            loop = asyncio.get_event_loop()
            result = await loop.run_in_executor(
                None,
                lambda: subprocess.run(
                    cmd,
                    input=full_text_input,
                    capture_output=True,
                    text=True,
                    shell=use_shell,
                    encoding='utf-8',
                    timeout=120
                )
            )
            
            if result.returncode != 0:
                log(f"[GeminiHeadless] ❌ Error: {result.stderr}")
                return None, f"CLI Error: {result.stderr[:200]}"
            
            # 3. Parse Output
            raw_stdout = result.stdout
            parsed_json = self._extract_json(raw_stdout)
            
            if not parsed_json:
                 # Try to clean up markdown blocks if _extract_json didn't catch it
                 return raw_stdout, None 
            
            return parsed_json, None

        except subprocess.TimeoutExpired:
            return None, "Timeout: Gemini CLI took too long."
        except Exception as e:
            return None, f"Execution Error: {e}"

    def _extract_json(self, text):
        parsed = None
        try:
            # First try direct load using the global clean helper if needed, or just json.loads
            # The global extract_json handles regex finding too, which is robust.
            parsed = extract_json(text)
        except: pass
        
        if parsed and isinstance(parsed, dict):
            # Check for nested 'response' key (common in some CLI wrappers)
            if "response" in parsed:
                inner_val = parsed["response"]
                if isinstance(inner_val, str):
                    try:
                        # Recursively parse the string inside 'response'
                        inner_parsed = extract_json(inner_val)
                        if inner_parsed: return inner_parsed
                    except: pass
                elif isinstance(inner_val, dict):
                    return inner_val
        
        return parsed

terminal_bridge_engine = GeminiHeadlessEngine()
process_manager = None # Removed PersistentProcessManager usage

@asynccontextmanager
async def lifespan(app: FastAPI):
    # Startup LOGIC
    log(f"--- Starting AI Unity Server on port {PORT} ---")
    log(f"Config: {CONFIG_FILE}")
    log(f"Prompt: {SYSTEM_PROMPT_FILE}")
    yield
    # Shutdown logic
    log("--- Stopping AI Unity Server ---")

app = FastAPI(lifespan=lifespan)

class ActionResponse(BaseModel):
    thought: str
    actionType: str
    screenPosition: dict = {"x": 0.5, "y": 0.5}
    targetPosition: dict = {"x": 0.0, "y": 0.0}
    keyName: str = ""
    textToType: str = ""
    duration: float = 2.0

def normalize_response(data: dict) -> dict:
    """Normalize specific LLM output inconsistencies to ActionResponse schema"""
    # 1. Map 'reason' or 'explanation' to 'thought'
    if "thought" not in data:
        if "reason" in data: data["thought"] = data["reason"]
        elif "explanation" in data: data["thought"] = data["explanation"]
        else: data["thought"] = "No thought provided."

    # 2. Map 'action' to 'actionType'
    if "actionType" not in data:
        if "action" in data: data["actionType"] = data["action"]
        else: data["actionType"] = "Wait" # Default

    # 3. Handle 'click' alias
    if data["actionType"].lower() == "click":
        data["actionType"] = "Click"

    # 4. Map 'position' to 'screenPosition'
    if "screenPosition" not in data and "position" in data:
        pos = data["position"]
        # If position is absolute (e.g., > 1.0), normalize? 
        # For now just pass it through, assuming Unity side handles it or it's already normalized (?)
        # usage says: {'x': 540, 'y': 104}. The server usually expects normalized.
        # But let's just map the key first.
        data["screenPosition"] = pos

    return data

class HealthResponse(BaseModel):
    status: str
    config_loaded: bool
    selected_tool: str

def load_config():
    if os.path.exists(CONFIG_FILE):
        try:
            with open(CONFIG_FILE, "r", encoding="utf-8") as f:
                return json.load(f)
        except Exception as e: log(f"[Error] Config: {e}")
    return None

def load_system_prompt():
    if os.path.exists(SYSTEM_PROMPT_FILE):
        try:
            with open(SYSTEM_PROMPT_FILE, "r", encoding="utf-8") as f:
                return f.read().strip()
        except Exception as e: log(f"[Error] Prompt: {e}")
    return "You are a QA agent. Respond in JSON format only."

def extract_json(text):
    if not text: return None
    # If text is already a dict or list (parsed by upstream logic), return it directly
    if isinstance(text, (dict, list)):
        return text
        
    clean_text = ANSI_ESCAPE.sub('', str(text)) # Ensure string for regex
    try:
        start = clean_text.find('{')
        end = clean_text.rfind('}') + 1
        if start != -1 and end > start:
            return json.loads(clean_text[start:end])
    except: pass
    return None

def create_error_response(msg):
    return {
        "thought": f"Error: {msg}",
        "actionType": "Wait",
        "screenPosition": {"x":0.5,"y":0.5},
        "targetPosition": {"x":0.0,"y":0.0},
        "keyName": "",
        "textToType": "",
        "duration": 2.0
    }

@app.on_event("shutdown")
def shutdown_event():
    pass # No persistent processes to stop

@app.get("/health", response_model=HealthResponse)
async def health_check():
    config = load_config()
    return {
        "status": "running",
        "config_loaded": config is not None,
        "selected_tool": config.get("selected_tool", "none") if config else "none"
    }

@app.post("/reset")
async def reset_service():
    # process_manager.stop_all() # Removed
    native_engine.current_api_key = None
    native_engine.current_api_key = None
    log("[Bridge] Reset All Processes and Memory.")
    return {"status": "reset_complete", "message": "All persistent tools and memory have been reset."}

@app.post("/ask", response_model=ActionResponse)
async def ask_llm(
    screenshot: UploadFile = File(...), 
    context: str = Form(...),
    api_key: str = Form(None)
):
    image_path = os.path.abspath("last_frame.jpg")
    try:
        content = await screenshot.read()
        with open(image_path, "wb") as f: f.write(content)
    except Exception as e: return create_error_response(f"Image Save Error: {e}")

    config = load_config()
    if not config: return create_error_response("Config not found.")

    tool_name = config.get("selected_tool", "gemini_cli")
    
    # 1. Native SDK Route
    selected_tool_config = config.get("tools", {}).get(tool_name, {})
    if selected_tool_config.get("command") == "internal":
        log(f"[Bridge] Using Native SDK Engine (Tool: {tool_name})...")
        tool_conf = selected_tool_config
        final_api_key = api_key
        if not final_api_key or final_api_key == "":
            final_api_key = tool_conf.get("api_key", "")
            if final_api_key == "YOUR_API_KEY_HERE": final_api_key = ""
        if not final_api_key:
             return create_error_response("API Key missing.")

        model_name = tool_conf.get("model_name", "gemini-3-flash-preview")
        system_prompt = load_system_prompt()
        
        # Check Persistent Mode
        if tool_conf.get("persistent", False):
            # Ensure chat initialized
            if tool_name not in getattr(native_engine, 'chat_sessions', {}):
                 if not hasattr(native_engine, 'chat_sessions'): native_engine.chat_sessions = {}
                 if not hasattr(native_engine, 'chat_syst_prompts'): native_engine.chat_syst_prompts = {}
                 native_engine.chat_syst_prompts[tool_name] = system_prompt # Store for first append
            
            raw_resp, err = await native_engine.generate_chat_response(tool_name, context, image_path, final_api_key, model_name)
        else:
            # One-shot
            raw_resp, err = await native_engine.generate_action(system_prompt, context, image_path, final_api_key, model_name)
        
        if err: return create_error_response(f"SDK Error: {err}")

        parsed = extract_json(raw_resp)
        if parsed: return normalize_response(parsed)
        return create_error_response(f"SDK Parse Error. Raw: {str(raw_resp)[:200]}")

    # 1.5 Terminal Bridge Route (for Option B)
    if selected_tool_config.get("command") == "terminal_bridge":
        log(f"[Bridge] Using Terminal Bridge (Tool: {tool_name})...")
        system_prompt = load_system_prompt()
        ans, err = await terminal_bridge_engine.generate_action(system_prompt, context, image_path)
        
        if err: 
            log(f"[Bridge] Error: {err}")
            return create_error_response(err)
            
        log(f"[Bridge] Raw Answer: {str(ans)[:100]}...") # Debug logging
        parsed = extract_json(ans)
        if parsed: return normalize_response(parsed)
        return create_error_response(f"Bridge Parse Error: {str(ans)[:100]}")

    # 2. Process Manager Route
    tool = config.get("tools", {}).get(tool_name)
    if not tool: return create_error_response(f"Tool {tool_name} not defined.")
    system_prompt = load_system_prompt()
    
    if tool.get("persistent", False):
        return await execute_persistent(tool_name, config, context, system_prompt, image_path, api_key)
    else:
        args_template = tool["arguments"]
        final_args = []
        for arg in args_template:
            replaced = arg.replace("{image_path}", image_path).replace("{context}", context).replace("{system_prompt}", system_prompt)
            final_args.append(replaced)
        return await execute_oneshot(tool_name, tool, [tool["command"]] + final_args)

async def execute_oneshot(tool_name, tool, cmd_list):
    log(f"[Bridge] Oneshot: {' '.join(cmd_list)}")
    try:
        result = subprocess.run(cmd_list, capture_output=True, text=True, encoding='utf-8', shell=(os.name=='nt'), timeout=60)
        parsed = extract_json(result.stdout)
        if parsed: return parsed
        stderr = ANSI_ESCAPE.sub('', result.stderr).strip()
        return create_error_response(f"JSON Parse Error. Stderr: {stderr[:100]}")
    except Exception as e: return create_error_response(str(e))

async def execute_persistent(tool_name, config, context, system_prompt, image_path, api_key):
    info = process_manager.get_info(tool_name, config)
    if not info: return create_error_response("Failed to start persistent process.")

    # PTY MODE
    if "pty_proc" in info:
        prompt = ""
        # We always include system prompt in the file for safety/completeness if the CLI supports big context
        # Or optimization: check initialized.
        # But file strategy acts like detailed prompt.
        if not info.get("initialized", False):
            prompt += f"SYSTEM: {system_prompt}\n"
            info["initialized"] = True
            log(f"[Bridge] PTY Sending SYSTEM PROMPT...")
        
        prompt += f"IMAGE: @{image_path}\nCONTEXT: {context}\nRespond with JSON only."
        
        # Save to temp file to bypass TTY line limits (4096 chars)
        temp_prompt_path = os.path.abspath("temp_prompt.txt")
        try:
            with open(temp_prompt_path, "w", encoding="utf-8") as f:
                f.write(prompt)
        except Exception as e:
            return create_error_response(f"File Write Error: {e}")

        # Send @filename command
        input_cmd = f"@{temp_prompt_path}\n"
        
        log(f"[Bridge] [PTY] Sending via file: {input_cmd.strip()}")
        try:
            info["pty_proc"].write(input_cmd)
            
            output_chunks = []
            start_time = time.time()
            timeout = 45.0
            
            while time.time() - start_time < timeout:
                try:
                    # WinPTY read usually returns string, might throw if empty?
                    # We might need to handle blocking. Assuming pywinpty read() works.
                    chunk = info["pty_proc"].read() 
                    if chunk:
                        log(f"[PTY Chunk] {repr(chunk)}") # Real-time logging
                        output_chunks.append(chunk)
                        full_resp = "".join(output_chunks)
                        # Remove ANSI
                        full_resp_clean = ANSI_ESCAPE.sub('', full_resp)
                        parsed = extract_json(full_resp_clean)
                        if parsed:
                            log(f"[Bridge] PTY response parsed.")
                            return parsed
                except Exception:
                    pass
                
                time.sleep(0.1) # polling delay
                 
                if not info["pty_proc"].isalive():
                    log(f"[Bridge] PTY died.")
                    break

            raw = "".join(output_chunks)
            clean = ANSI_ESCAPE.sub('', raw).strip()
            log(f"[Bridge] PTY Timeout. Snippet: {clean[:100]}")
            return create_error_response(f"PTY Timeout. Output: {clean[:50]}...")
            
        except Exception as e:
            return create_error_response(f"PTY Error: {e}")

    # STANDARD PIPE MODE
    stdout_reader = info["stdout"]
    stderr_reader = info["stderr"]
    stdout_reader.clear()
    stderr_reader.clear()

    # Optimization: Only send SYSTEM prompt if not initialized
    prompt = ""
    if not info.get("initialized", False):
        prompt += f"SYSTEM: {system_prompt}\n"
        info["initialized"] = True
        log(f"[Bridge] Sending SYSTEM PROMPT to new session '{tool_name}'")
    
    prompt += f"IMAGE: @{image_path}\nCONTEXT: {context}\nRespond with JSON only."
    input_text = prompt.replace("\n", " ").strip() + "\n"

    log(f"[Bridge] [Persistent] Sending to {tool_name} ({len(input_text)} bytes)...")
    try:
        info["proc"].stdin.write(input_text)
        info["proc"].stdin.flush()
        
        output_chunks = []
        start_time = time.time()
        timeout = 45
        
        while time.time() - start_time < timeout:
            chunk = stdout_reader.get_output(timeout=0.2)
            if chunk:
                output_chunks.append(chunk)
                full_resp = "".join(output_chunks)
                parsed = extract_json(full_resp)
                if parsed:
                    log(f"[Bridge] Persistent response parsed successfully.")
                    return parsed
            
            err_chunk = stderr_reader.get_output(timeout=0.01)
            if err_chunk:
                # Log stderr but don't abort immediately unless severe?
                # Usually standard CLI warnings go to stderr.
                pass

            if info["proc"].poll() is not None:
                log(f"[Bridge] Process '{tool_name}' died during execution.")
                rem_err = stderr_reader.get_output(timeout=1.0)
                if rem_err: log(f"[Bridge] Final STDERR: {rem_err.strip()}")
                break
        
        raw_out = "".join(output_chunks)
        clean_out = ANSI_ESCAPE.sub('', raw_out).strip()
        log(f"[Bridge] Timeout. Raw len: {len(raw_out)}")
        if clean_out: log(f"[Bridge] Snippet: {clean_out[:100]}")
        
        err_out = stderr_reader.get_output(timeout=0.1)
        if err_out:
            return create_error_response(f"Persistent Process Error (STDERR): {err_out[:200]}")
            
        return create_error_response(f"Persistent timeout. Output snippet: {clean_out[:50]}...")
    except Exception as e:
        return create_error_response(f"Persistent error: {e}")

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="AI Unity Tester MCP Bridge Server")
    parser.add_argument("--config", type=str, help="Path to tools_config.json")
    parser.add_argument("--prompt", type=str, help="Path to system_prompt.txt")
    parser.add_argument("--port", type=int, default=8000, help="Server port (default: 8000)")
    args = parser.parse_args()

    if args.config: CONFIG_FILE = os.path.abspath(args.config)
    if args.prompt: SYSTEM_PROMPT_FILE = os.path.abspath(args.prompt)
    if args.port: PORT = args.port

    uvicorn.run(app, host=HOST, port=PORT)
