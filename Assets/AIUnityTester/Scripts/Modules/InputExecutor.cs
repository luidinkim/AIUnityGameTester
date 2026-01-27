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
            // 문자열로 된 키 이름을 Key enum으로 변환하는 로직 필요
            // 예: "Space" -> Key.Space
            if (System.Enum.TryParse(keyName, out Key key))
            {
                InputSystem.QueueConfigSettingChangedEvent(_virtualKeyboard, null); // Reset state if needed
                // Key Down & Up
                // 가상 장치에 대한 입력은 InputSystem.QueueStateEvent를 통해 더 정교하게 제어 가능
                Debug.Log($"[InputExecutor] Virtual KeyPress: {key}");
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
