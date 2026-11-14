using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 3인칭 플레이어 이동 수치(애셋은 Assets/Data/Player, <see cref="ThirdPersonController"/>가 읽음). 애셋이라 Play 모드에서 고친 값도 남는다.
    /// 쓰는 곳: <see cref="ThirdPersonController"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Slime~ColorMaker~/플레이어 이동 설정", fileName = "PlayerMoveSettings")]
    public class PlayerMoveSettings : ScriptableObject
    {
        [Tooltip("이동 속도(유닛/초)")]
        [Min(0f)] public float moveSpeed = 2f;

        [Tooltip("관성 시간(초, 지면 전용). 직접 이동은 출발할 때 이 시간 동안 일정하게 빨라져 이동 속도에 이르고, 키를 떼면 그때 속도에서 이 시간 동안 일정하게 느려져 멈춘다. " +
                 "컨베이어 벨트를 벗어날 때도 벨트가 밀던 속도에서 이 시간 동안 느려진다. 미끄러지는 거리 = 속도 × 시간 ÷ 2(1/3초면 벨트 속도 3에서 0.5칸, 이동 속도 2에서 약 0.33칸). 0이면 바로.")]
        [Min(0f)] public float inertiaTime = 1f / 3f;

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
