using UnityEngine;

namespace ColorMaze
{
    /// <summary>
    /// 므엥의 말랑말랑한 출렁임 수치(2026-11-08, 애셋은 Assets/Data). 애셋이라 Play 모드에서 고친 값도 Play를 멈춘 뒤 남는다
    /// (보면서 고치는 용도). 쓰는 곳: <see cref="PlayerSquashView"/>.
    /// 출렁임은 둘이다: 위아래로 늘고 눌림(부피 유지), 윗부분이 발밑 기준으로 기울어 흔들림(푸딩 느낌).
    /// 반동 값은 출렁임 속도에 한 번 더하는 양이다(위아래는 늘어나는 비율/초, 기울기는 도/초).
    /// </summary>
    [CreateAssetMenu(menuName = "ColorMaze/므엥 출렁임 설정", fileName = "PlayerSquashSettings")]
    public class PlayerSquashSettings : ScriptableObject
    {
        [Header("위아래 출렁임")]
        [Tooltip("최대 출렁임. 0.1이면 위아래로 ±10%까지 늘거나 눌린다(옆으로는 부피가 같게 반대로).")]
        [Range(0f, 0.5f)] public float maxStretch = 0.1f;

        [Tooltip("스프링 단단함. 클수록 빠르고 짧게 출렁인다.")]
        [Range(10f, 1000f)] public float stiffness = 200f;

        [Tooltip("감쇠 비율. 0에 가까우면 오래 출렁이고, 1이면 넘치지 않고 바로 멈춘다.")]
        [Range(0f, 1f)] public float damping = 0.3f;

        [Tooltip("바닥면이 옆(x·z)으로 따라 퍼지는 비율. 1이면 윗면만큼, 0이면 바닥 크기 고정. 바닥은 늘 평평하고 밀리지 않으며, 그 사이 높이는 이 비율에서 꼭대기 100%로 고르게 이어진다.")]
        [Range(0f, 1f)] public float bottomSpread = 1f;

        [Header("윗부분 흔들림(기울기)")]
        [Tooltip("최대 기울기(도). 발밑을 기준으로 윗부분이 이만큼까지 쏠린다.")]
        [Range(0f, 30f)] public float maxLean = 8f;

        [Tooltip("가속도(유닛/초²) 1당 기울기(도). 빨라지면 뒤로, 느려지면 앞으로, 돌면 바깥으로 쏠린다. 최고 가속은 약 15.")]
        [Range(0f, 2f)] public float leanPerAccel = 0.4f;

        [Tooltip("기울기 스프링 단단함. 클수록 빠르게 흔들린다.")]
        [Range(10f, 1000f)] public float leanStiffness = 150f;

        [Tooltip("기울기 감쇠 비율. 낮을수록 푸딩처럼 여러 번 흔들리다 멈춘다.")]
        [Range(0f, 1f)] public float leanDamping = 0.12f;

        [Header("출발 (멈춰 있다가 움직이기 시작)")]
        [Tooltip("위로 늘어나는 반동")]
        [Range(0f, 3f)] public float startStretch = 0.6f;

        [Tooltip("윗부분이 뒤로 처지는 반동(도/초)")]
        [Range(0f, 200f)] public float startLean = 30f;

        [Header("이동 중")]
        [Tooltip("걷는 동안 위아래로 늘었다 눌리는 크기(최고 속도일 때)")]
        [Range(0f, 0.5f)] public float walkBounce = 0.05f;

        [Tooltip("1유닛 걸을 때 통통 튀는 횟수")]
        [Range(0.1f, 5f)] public float bouncesPerUnit = 1.2f;

        [Tooltip("움직인다고 보는 수평 속도(유닛/초)")]
        [Range(0f, 2f)] public float moveThreshold = 0.3f;

        [Header("도착 (움직이다 멈춤)")]
        [Tooltip("아래로 눌리는 반동")]
        [Range(0f, 3f)] public float stopSquash = 0.6f;

        [Tooltip("윗부분이 가던 쪽으로 넘치는 반동(도/초)")]
        [Range(0f, 200f)] public float stopLean = 40f;

        [Header("착지 (공중에서 땅에 닿음)")]
        [Tooltip("이 속도(유닛/초)보다 빨리 떨어지다 땅에 닿을 때만 철퍽한다. 한 칸 낙하는 약 5.3, 두 칸은 약 7.4. 계단을 걸어 내려갈 때는 넘지 않는다.")]
        [Range(0f, 15f)] public float landSpeedThreshold = 4f;

        [Tooltip("낙하 속도 1당 눌리는 반동")]
        [Range(0f, 5f)] public float landKick = 0.25f;
    }
}
