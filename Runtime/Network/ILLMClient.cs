using Cysharp.Threading.Tasks; // UniTask 사용 가정 (없으면 Task로 대체 가능하지만 권장)
using UnityEngine;
using AIUnityTester.Data;

namespace AIUnityTester.Network
{
    /// <summary>
    /// LLM 통신을 위한 공통 인터페이스.
    /// Direct API 모드와 MCP Bridge 모드가 이를 구현합니다.
    /// </summary>
    public interface ILLMClient
    {
        /// <summary>
        /// 초기화 (API Key 설정, 서버 연결 확인 등)
        /// </summary>
        UniTask<bool> InitializeAsync();

        /// <summary>
        /// 현재 게임 상황을 보내고 다음 행동을 요청합니다.
        /// </summary>
        /// <param name="screenshot">현재 화면 캡처 (Texture2D)</param>
        /// <param name="context">현재 상태에 대한 추가 텍스트 정보 (UI 트리 등)</param>
        /// <returns>AI가 결정한 행동 데이터</returns>
        UniTask<AIActionData> RequestActionAsync(Texture2D screenshot, string context);
    }
}
