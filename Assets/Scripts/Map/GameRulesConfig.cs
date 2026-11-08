using UnityEngine;

namespace ColorMaze
{
    /// <summary>게임 규칙 수치. 모든 맵이 같이 쓴다(애셋은 Assets/Data). 맵 매니저가 들고 플레이어와 기물에 나눠 준다.</summary>
    [CreateAssetMenu(menuName = "ColorMaze/게임 규칙", fileName = "GameRulesConfig")]
    public class GameRulesConfig : ScriptableObject
    {
        [Tooltip("채널 한도(R, G, B). 지금 색은 채널마다 0 ~ 한도로 잘린다.")]
        public ColorValue channelLimit = new(5, 5, 5);

        [Tooltip("값이 보이는 거리(유닛, 캐릭터에서 기물까지). 코어 루프 9단계에서 조준 거리로 정했고(v1은 1.3), 2026-11-08 조준을 없애며 가까운 값 표시 거리로 바뀜.")]
        public float interactDistance = 3f;

        [Tooltip("지금 색 구의 발광 세기 계수(가장 큰 채널 × 계수). 블룸이 꺼져 있어 1을 넘는 발광은 더 밝아 보이지 않으므로 " +
                 "값 1~5가 0.2~1이 되게 둔다(임시, 블룸은 미정).")]
        public float glowFactor = 0.2f;

        [Tooltip("낙사 안전망: 가장 낮은 블록의 바닥보다 이만큼 아래로 떨어지면 마지막 체크포인트(없으면 시작 지점)로 옮긴다.")]
        public float fallDepth = 10f;
    }
}
