import sys
import json

# Arguments: script_name, image_path, context
if __name__ == "__main__":
    # Mock Response
    response = {
        "thought": "I am a Mock Agent via CLI Wrapper. I see the screen.",
        "actionType": "Wait",
        "screenPosition": {"x": 0.5, "y": 0.5},
        "targetPosition": {"x": 0.0, "y": 0.0},
        "keyName": "",
        "textToType": "",
        "duration": 1.0
    }
    print(json.dumps(response))
