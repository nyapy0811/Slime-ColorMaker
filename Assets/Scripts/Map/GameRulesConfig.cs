using UnityEngine;

namespace ColorMaze
{
    /// <summary>게임 규칙 수치(채널 한도는 고정이라 코어 규칙 <see cref="ColorRules.Limit"/>에 있음). 모든 맵이 같이 쓴다(애셋은 Assets/Data). 맵 매니저가 들고 플레이어와 기물에 나눠 준다.</summary>
    [CreateAssetMenu(menuName = "ColorMaze/게임 규칙", fileName = "GameRulesConfig")]
    public class GameRulesConfig : ScriptableObject
    {
        [Tooltip("값이 보이는 거리(유닛, 캐릭터에서 기물까지). 코어 루프 9단계에서 조준 거리로 정했고(v1은 1.3), 2026-11-08 조준을 없애며 가까운 값 표시 거리로 바뀜.")]
        public float interactDistance = 3f;

        [Tooltip("낙사 안전망: 가장 낮은 블록의 바닥보다 이만큼 아래로 떨어지면 마지막 체크포인트(없으면 시작 지점)로 옮긴다.")]
        public float fallDepth = 10f;
    }
}
