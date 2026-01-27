using System;
using UnityEngine;

namespace AIUnityTester.Data
{
    [Serializable]
    public class AIActionData
    {
        // AI의 생각/추론 과정 (Logs에 표시)
        public string thought;

        // 수행할 행동: "Click", "Drag", "Wait", "KeyPress", "Type"
        public string actionType;

        // 화면 좌표 (0.0 ~ 1.0). (0,0)은 좌하단 or 좌상단(구현에 따름)
        public Vector2 screenPosition;
        
        // Drag일 경우 끝점
        public Vector2 targetPosition;

        // 특정 키 입력 (예: "Space", "W", "Enter")
        public string keyName;
        
        // 타이핑할 텍스트
        public string textToType;

        // 행동 지속 시간 (초)
        public float duration;
    }
}
