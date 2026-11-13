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
        [Tooltip("벨트 속도(유닛/초, 캐릭터 이동 속도와 같은 단위). 올라탄 캐릭터를 이 빠르기로 밀고, 무늬도 이 빠르기로 흐른다(무늬 한 장 = 1/16유닛이 보이는 시간은 1/16 ÷ 이 값).")]
        [Min(0.01f)] public float speed = 0.625f;
    }
}
