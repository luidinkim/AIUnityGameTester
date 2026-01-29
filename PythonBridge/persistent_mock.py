import sys
import json
import time

# Force unbuffered stdout/stderr
sys.stdout.reconfigure(line_buffering=True)
sys.stderr.reconfigure(line_buffering=True)

print("Mock Persistent Agent Started. Waiting for input...", flush=True)

while True:
    try:
        line = sys.stdin.readline()
        if not line:
            break
        
        line = line.strip()
        if not line:
            continue
            
        # Log to stderr to verify stderr capturing
        print(f"DEBUG: Received {len(line)} chars", file=sys.stderr, flush=True)
        
        # Simulate processing time
        time.sleep(0.5)
        
        response = {
            "thought": f"Echo: {line[:20]}...",
            "actionType": "Wait",
            "screenPosition": {"x": 0.5, "y": 0.5},
            "targetPosition": {"x": 0.0, "y": 0.0},
            "keyName": "",
            "textToType": "",
            "duration": 1.0
        }
        
        print(json.dumps(response), flush=True)
        
    except KeyboardInterrupt:
        break
    except Exception as e:
        print(f"Error: {e}", file=sys.stderr, flush=True)
