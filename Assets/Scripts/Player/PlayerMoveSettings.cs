using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 3인칭 플레이어 이동 수치(2026-11-13, 애셋은 Assets/Data, 그전에는 <see cref="ThirdPersonController"/> 컴포넌트 칸). 애셋이라 Play 모드에서 고친 값도 남는다.
    /// 쓰는 곳: <see cref="ThirdPersonController"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Slime~ColorMaker~/플레이어 이동 설정", fileName = "PlayerMoveSettings")]
    public class PlayerMoveSettings : ScriptableObject
    {
        [Tooltip("이동 속도(유닛/초)")]
        [Min(0f)] public float moveSpeed = 2f;

        [Tooltip("정지 상태에서 최대 속도까지 걸리는 시간(초, 지면 전용)")]
        [Min(0f)] public float accelerationTime = 0.2f;

        [Tooltip("가는 방향으로 도는 빠르기(도/초)")]
        [Min(0f)] public float turnSpeed = 720f;

        [Tooltip("중력 가속도(유닛/초²). 점프를 없앨 때 그 전의 낙하 느낌(점프 높이 1.05, 체공 0.8초)에서 역산한 값.")]
        [Min(0f)] public float gravity = 13.125f;

        [Tooltip("턱 넘기 높이(칸). 가는 쪽에 이 높이 이하의 턱(계단 한 단 0.25 등)이 있으면 걸어서 올라가고, 바닥에 서 있다가 이 높이 이하로 내려가는 곳에서는 떨어지지 않고 바로 내려선다.")]
        [Range(0f, 0.5f)] public float stepHeight = 0.3f;

        [Tooltip("턱을 오르내릴 때 몸 모델과 카메라가 바뀐 높이를 따라가는 시간(초). 충돌은 한 번에 오르내리고 보이는 것만 부드럽게 따라간다. 0이면 바로.")]
        [Range(0f, 0.5f)] public float stepSmoothTime = 0.1f;
    }
}
