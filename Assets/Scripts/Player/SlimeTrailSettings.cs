using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 캐릭터 바닥 흔적 수치(애셋은 Assets/Data/Player). 애셋이라 Play 모드에서 고친 값도 남는다.
    /// 쓰는 곳: <see cref="SlimeTrail"/>. 픽셀 크기(1/16유닛)와 찍는 넓이(캐릭터 발바닥)는 정해져 있다.
    /// </summary>
    [CreateAssetMenu(menuName = "Slime~ColorMaker~/바닥 흔적 설정", fileName = "SlimeTrailSettings")]
    public class SlimeTrailSettings : ScriptableObject
    {
        [Tooltip("흔적 픽셀이 남아 있는 시간(초). 마지막으로 찍힌 순간부터 점점 투명해져 이 시간이 지나면 없어진다.")]
        [Min(0.1f)] public float lifetime = 5f;

        [Tooltip("처음 진하기(불투명도, 0 = 투명 ~ 1 = 불투명). 남은 시간에 비례해 0으로 줄어든다.")]
        [Range(0f, 1f)] public float opacity = 0.7f;
    }
}
