using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 컨베이어 벨트 수치(2026-11-12, 애셋은 Assets/Data). 애셋이라 Play 모드에서 고친 값도 남는다.
    /// 쓰는 곳: <see cref="ConveyorBelt"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Slime~ColorMaker~/컨베이어 벨트 설정", fileName = "ConveyorBeltSettings")]
    public class ConveyorBeltSettings : ScriptableObject
    {
        [Tooltip("무늬 한 장(ConveyorBelt1 → 2)이 보이는 시간(초). 한 장마다 무늬가 1/16유닛 흐르고, 올라탄 캐릭터도 같은 빠르기로 민다(0.1초면 초당 0.625유닛).")]
        [Min(0.01f)] public float frameTime = 0.1f;
    }
}
