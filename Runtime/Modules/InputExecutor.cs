using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using AIUnityTester.Data;

namespace AIUnityTester.Modules
{
    /// <summary>
    /// AIActionData를 실제 Unity Input으로 변환하여 실행합니다.
    /// New Input System의 가상 이벤트를 사용합니다.
    /// </summary>
    public class InputExecutor : MonoBehaviour
    {
        private Mouse _virtualMouse;
        private Keyboard _virtualKeyboard;

        private void Awake()
        {
            // 가상 입력 장치 준비 (실제 마우스/키보드와 별개로 작동 가능)
            _virtualMouse = InputSystem.AddDevice<Mouse>("AI_Virtual_Mouse");
            _virtualKeyboard = InputSystem.AddDevice<Keyboard>("AI_Virtual_Keyboard");
        }

        private void OnDestroy()
        {
            if (_virtualMouse != null) InputSystem.RemoveDevice(_virtualMouse);
            if (_virtualKeyboard != null) InputSystem.RemoveDevice(_virtualKeyboard);
        }

        public void Execute(AIActionData action)
        {
            if (action == null) return;

            switch (action.actionType)
            {
                case "Click":
                    PerformClick(action.screenPosition);
                    break;
                case "KeyPress":
                    PerformKeyPress(action.keyName);
                    break;
                case "Type":
                    PerformType(action.textToType);
                    break;
                case "Wait":
                    // Wait은 Agent 루프에서 처리됨
                    break;
            }
        }

        private void PerformClick(Vector2 normalizedPos)
        {
            // 0~1 좌표를 픽셀 좌표로 변환
            Vector2 pixelPos = new Vector2(
                normalizedPos.x * Screen.width,
                normalizedPos.y * Screen.height
            );

            // 마우스 이동 및 클릭 이벤트 생성
            InputSystem.QueueStateEvent(_virtualMouse, new MouseState
            {
                position = pixelPos,
                buttons = 1 << (int)MouseButton.Left
            });

            // 클릭 뗌 (Release) 이벤트도 즉시 큐에 추가
            InputSystem.QueueStateEvent(_virtualMouse, new MouseState
            {
                position = pixelPos,
                buttons = 0
            });

            Debug.Log($"[InputExecutor] Virtual Click at {pixelPos}");
        }

        private void PerformKeyPress(string keyName)
        {
            // 문자열로 된 키 이름을 Key enum으로 변환
            if (System.Enum.TryParse(keyName, true, out Key key))
            {
                // 1. Key Down
                InputSystem.QueueStateEvent(_virtualKeyboard, new KeyboardState(key));
                
                // 2. Short Delay (Optional but recommended for some games to detect press)
                // In a synchronous context, we just queue the release immediately after.
                // If the game needs frame-perfect hold, we might need a coroutine.
                
                // 3. Key Up (Release)
                // Creating an empty state essentially releases keys if we don't set them
                // But for safety, we should explicitly handle it or rely on the fact 
                // that the next state update will clear it if not persisted.
                // A simpler way for "Press and Release":
                
                // For now, let's just queue the press. 
                // To properly simulate a "Click" on keyboard, we need to wait a frame usually.
                // But since this executor is fire-and-forget, we queue both events.
                
                // Note: KeyboardState constructor with a key sets that key to be pressed.
                // To release, we queue a default state.
                InputSystem.QueueStateEvent(_virtualKeyboard, new KeyboardState()); 

                Debug.Log($"[InputExecutor] Virtual KeyPress: {key}");
            }
            else
            {
                Debug.LogWarning($"[InputExecutor] Could not parse key: {keyName}");
            }
        }

        private void PerformType(string text)
        {
            Debug.Log($"[InputExecutor] Virtual Typing: {text}");
            // Typing은 InputSystem의 대기열보다는 타겟 컴포넌트(TMP_InputField 등)에 
            // 직접 주입하는 방식이 더 안정적일 수 있음 (확장 예정)
        }
    }
}
