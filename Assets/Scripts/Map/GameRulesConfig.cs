using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>게임 규칙 수치(채널 한도는 고정이라 코어 규칙 <see cref="ColorRules.Limit"/>에 있음). 모든 맵이 같이 쓴다(애셋은 Assets/Data/Game). 맵 매니저가 들고 플레이어와 기물에 나눠 준다.</summary>
    [CreateAssetMenu(menuName = "Slime~ColorMaker~/게임 규칙", fileName = "GameRulesConfig")]
    public class GameRulesConfig : ScriptableObject
    {
        [Tooltip("값이 보이기 시작하는 거리(유닛, 캐릭터 몸 가운데에서 기물 가장 가까운 부분까지). 이 거리에서 값 상자가 '시작 크기'·투명으로 나타난다.")]
        public float interactDistance = 4f;

        [Tooltip("값 상자가 제 크기·불투명이 되는 거리(유닛). 보이기 시작하는 거리에서 이 거리까지 점점 커지고 진해지며, 이보다 가까우면 그대로다.")]
        public float valueFullDistance = 2f;

        [Tooltip("보이기 시작하는 거리에서 값 상자의 크기(제 크기 = 1). 0이면 점에서 커진다.")]
        [Range(0f, 1f)] public float valueStartScale = 0f;

        [Tooltip("낙사 안전망: 가장 낮은 블록의 바닥보다 이만큼 아래로 떨어지면 마지막 체크포인트(없으면 시작 지점)로 옮긴다.")]
        public float fallDepth = 10f;
    }
}
