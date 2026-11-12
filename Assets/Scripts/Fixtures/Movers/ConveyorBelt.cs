using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 컨베이어 벨트(2026-11-12, 이동 계열, 프리팹 `ConveyorBelt`). 블록 자리(칸 하나)에 깔리고 윗면이 블록 윗면과 같은 높이다.
    /// 무늬 모델 넷(`ConveyorBelt1~4`)을 1 → 2 → 3 → 4 → 1 차례로 하나씩 켠다. 차례는 게임 시간으로 정해 씬의 모든 벨트가 같은 순간에 같은 번호를 보이고,
    /// 일시정지하면 멈춘다. 모델 기준 흐름은 −Z이고 놓을 때 Y축으로 90°씩 돌린다.
    /// 올라탄 캐릭터는 <see cref="ThirdPersonController"/>가 <see cref="SurfaceVelocity"/>만큼 민다(무늬가 흐르는 빠르기와 같음).
    /// </summary>
    public class ConveyorBelt : FixtureBase
    {
        /// <summary>무늬 한 장마다 흐르는 거리(복셀 한 칸, 유닛).</summary>
        const float StepPerFrame = 1f / 16f;

        [SerializeField] ConveyorBeltSettings settings;

        [Tooltip("무늬 모델(ConveyorBelt1~4 차례). 하나씩 켠다.")]
        [SerializeField] GameObject[] frames;

        int shown = -1;

        /// <summary>윗면이 흐르는 속도(월드, 유닛/초).</summary>
        public Vector3 SurfaceVelocity => transform.rotation * Vector3.back * (StepPerFrame / settings.frameTime);

        void Update()
        {
            int index = (int)(Time.time / settings.frameTime) % frames.Length;
            if (index == shown) return;
            for (int i = 0; i < frames.Length; i++) frames[i].SetActive(i == index);
            shown = index;
        }
    }
}
