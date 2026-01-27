using UnityEngine;
using UnityEngine.UI;
using System.Text;
using System.Collections.Generic;

namespace AIUnityTester.Modules
{
    public class UIHierarchyDumper : MonoBehaviour
    {
        /// <summary>
        /// 현재 활성화된 캔버스의 모든 UI 요소 정보를 문자열로 추출합니다.
        /// </summary>
        public string DumpHierarchy()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== Active UI Elements ===");

            // 씬 내의 모든 캔버스 찾기
            Canvas[] canvases = FindObjectsOfType<Canvas>();
            foreach (var canvas in canvases)
            {
                if (!canvas.gameObject.activeInHierarchy) continue;
                
                sb.AppendLine($"[Canvas] {canvas.name} (RenderMode: {canvas.renderMode})");
                TraverseHierarchy(canvas.transform, sb, 1);
            }

            return sb.ToString();
        }

        private void TraverseHierarchy(Transform parent, StringBuilder sb, int depth)
        {
            foreach (Transform child in parent)
            {
                if (!child.gameObject.activeInHierarchy) continue;

                // UI 컴포넌트 확인 (Button, InputField, Text 등)
                string typeInfo = GetUIType(child);
                string screenInfo = GetScreenRectInfo(child as RectTransform);
                
                string indent = new string('-', depth * 2);
                sb.AppendLine($"{indent} {typeInfo} \"{child.name}\" {screenInfo}");

                // 재귀 호출
                TraverseHierarchy(child, sb, depth + 1);
            }
        }

        private string GetUIType(Transform t)
        {
            if (t.GetComponent<Button>()) return "[Button]";
            if (t.GetComponent<TMPro.TMP_InputField>() || t.GetComponent<InputField>()) return "[Input]";
            if (t.GetComponent<TMPro.TextMeshProUGUI>() || t.GetComponent<Text>()) return "[Text]";
            if (t.GetComponent<ScrollRect>()) return "[Scroll]";
            if (t.GetComponent<Toggle>()) return "[Toggle]";
            if (t.GetComponent<Slider>()) return "[Slider]";
            if (t.GetComponent<Image>()) return "[Image]";
            return "[Rect]";
        }

        private string GetScreenRectInfo(RectTransform rect)
        {
            if (rect == null) return "";

            // 월드 좌표를 스크린 좌표로 변환
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            
            // UI가 스크린 스페이스 오버레이가 아닐 경우 카메라도 고려해야 함
            // 여기서는 ScreenSpaceOverlay 또는 Camera 모드에서 WorldCamera가 설정된 경우를 포괄적으로 처리
            // Canvas.renderMode에 따라 로직이 달라질 수 있으나, GetWorldCorners는 대부분 유효함.

            // Canvas Scaler 등으로 인해 실제 렌더링 픽셀과 다를 수 있으므로
            // Camera.main.WorldToScreenPoint 등을 사용해야 할 수도 있음.
            // 하지만 UI(Screen Space - Overlay)는 WorldCorners가 곧 Screen 좌표임.

            // 중심점 계산
            Vector2 center = (corners[0] + corners[2]) / 2;
            float width = Vector3.Distance(corners[0], corners[3]);
            float height = Vector3.Distance(corners[0], corners[1]);

            return $"(Pos: {center.x:F0},{center.y:F0} Size: {width:F0}x{height:F0})";
        }
    }
}
