from fastapi import FastAPI, UploadFile, File, Form
from pydantic import BaseModel
import uvicorn
import base64
import json
import os

# --- Configuration ---
HOST = "127.0.0.1"
PORT = 8000

app = FastAPI()

class ActionResponse(BaseModel):
    thought: str
    actionType: str
    screenPosition: dict  # {"x": 0.5, "y": 0.5}
    targetPosition: dict  # {"x": 0.0, "y": 0.0}
    keyName: str
    textToType: str
    duration: float

@app.post("/ask", response_model=ActionResponse)
async def ask_llm(
    screenshot: UploadFile = File(...), 
    context: str = Form(...)
):
    """
    Unity로부터 스크린샷과 컨텍스트를 받아 LLM에게 문의하고 행동을 반환합니다.
    """
    
    # 1. 이미지 저장 (디버깅용)
    image_bytes = await screenshot.read()
    with open("last_frame.jpg", "wb") as f:
        f.write(image_bytes)
    
    print(f"[Bridge] Received Request. Context: {context[:50]}...")

    # ------------------------------------------------------------------
    # TODO: 여기서 실제 로컬 LLM이나 CLI 툴을 호출하세요.
    # 예: os.system("gemini-cli ask --image last_frame.jpg ...")
    # 지금은 테스트를 위해 Mock(더미) 데이터를 반환합니다.
    # ------------------------------------------------------------------
    
    # -- Mock Logic (Example) --
    # 만약 컨텍스트에 'Menu'가 있으면 Start 버튼을 누르는 척 함.
    mock_thought = "I see the main menu. I should click the Start button."
    mock_action = "Click"
    mock_pos = {"x": 0.5, "y": 0.6}
    
    response = {
        "thought": mock_thought,
        "actionType": mock_action,
        "screenPosition": mock_pos,
        "targetPosition": {"x": 0, "y": 0},
        "keyName": "",
        "textToType": "",
        "duration": 0.1
    }
    
    print(f"[Bridge] Sending Response: {response['actionType']}")
    return response

if __name__ == "__main__":
    print(f"Starting AI Bridge Server at http://{HOST}:{PORT}")
    uvicorn.run(app, host=HOST, port=PORT)
