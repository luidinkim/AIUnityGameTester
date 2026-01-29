using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Cysharp.Threading.Tasks;
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

        public async UniTask Execute(AIActionData action)
        {
            if (action == null) return;

            switch (action.actionType)
            {
                case "Click":
                    await PerformClick(action.screenPosition);
                    break;
                case "KeyPress":
                    PerformKeyPress(action.keyName);
                    break;
                case "Type":
                    PerformType(action.textToType);
                    break;
                case "Wait":
                    // Wait duration is handled in AITesterAgent
                    Debug.Log($"[InputExecutor] AI requested Wait for {action.duration}s");
                    break;
            }
        }

        private async UniTask PerformClick(Vector2 normalizedPos)
        {
            // Unity coordinates: (0,0) is Bottom-Left.
            // AI coordinates: (0,0) is Top-Left.
            // AI sends 0.95 for Bottom. Unity needs 0.05ish for Bottom.
            // Therefore, Inversion IS required.
            Vector2 pixelPos = new Vector2(
                normalizedPos.x * Screen.width,
                (1.0f - normalizedPos.y) * Screen.height
            );

            Debug.Log($"[InputExecutor] Screen Resolution: {Screen.width}x{Screen.height}");
            Debug.Log($"[InputExecutor] Virtual Click Attempt at {pixelPos} (Normalized: {normalizedPos})");

            bool uiClicked = PerformUIClick(pixelPos);
            
            // Even if UI was clicked, we move the virtual mouse for visual feedback and gameplay inputs
            // 1. Move
            InputSystem.QueueStateEvent(_virtualMouse, new MouseState { position = pixelPos });
            InputSystem.Update();
            
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate); 

            // 2. Press
            InputSystem.QueueStateEvent(_virtualMouse, new MouseState
            {
                position = pixelPos,
                buttons = 1 << (int)MouseButton.Left
            });
            InputSystem.Update();
            
            await UniTask.Delay(System.TimeSpan.FromSeconds(0.1f)); 

            // 3. Release
            InputSystem.QueueStateEvent(_virtualMouse, new MouseState
            {
                position = pixelPos,
                buttons = 0
            });
            InputSystem.Update();
            
            await UniTask.Yield();

            Debug.Log($"[InputExecutor] Virtual Click Completed at {pixelPos}. UI Hit: {uiClicked}");
        }

        private bool PerformUIClick(Vector2 screenPos)
        {
            if (UnityEngine.EventSystems.EventSystem.current == null) return false;

            var eventData = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
            {
                position = screenPos
            };

            var results = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
            UnityEngine.EventSystems.EventSystem.current.RaycastAll(eventData, results);

            if (results.Count > 0)
            {
                // Log all hits for debugging
                foreach (var res in results)
                {
                    Debug.Log($"[InputExecutor] Raycast Hit: {res.gameObject.name} (Depth: {res.depth}, SortingLayer: {res.sortingLayer})");
                }

                var target = results[0].gameObject;
                Debug.Log($"[InputExecutor] Engaging Primary Target: {target.name}");

                // Simulate typical button click sequence: Down -> Up -> Click
                UnityEngine.EventSystems.ExecuteEvents.Execute(target, eventData, UnityEngine.EventSystems.ExecuteEvents.pointerDownHandler);
                UnityEngine.EventSystems.ExecuteEvents.Execute(target, eventData, UnityEngine.EventSystems.ExecuteEvents.pointerUpHandler);
                UnityEngine.EventSystems.ExecuteEvents.Execute(target, eventData, UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
                return true;
            }
            return false;
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
