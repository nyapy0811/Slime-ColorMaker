using UnityEngine;
using UnityEngine.Serialization;

namespace SlimeColorMaker
{
    /// <summary>
    /// 카메라와 므엥 사이를 가리는 물체를 흐리게 하는 수치(2026-11-08, 애셋은 Assets/Data). 애셋이라 Play 모드에서 고친 값도 남는다.
    /// 쓰는 곳: <see cref="CameraOcclusionFader"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Slime ~ColorMaker~/카메라 가림 흐림 설정", fileName = "CameraFadeSettings")]
    public class CameraFadeSettings : ScriptableObject
    {
        [Tooltip("투명 시작 반지름(유닛, 므엥 깊이에서). 카메라에서 본 시선이 므엥 깊이에서 므엥 몸 가운데로부터 이 거리 안을 지나면 그 시선 위의 지형·기물이 흐려지기 시작한다(화면에서 므엥 둘레의 원).")]
        [FormerlySerializedAs("radius")]
        [Range(0.05f, 5f)] public float startRadius = 0.6f;

        [Tooltip("최대 투명도 반지름(유닛, 므엥 깊이에서). 시선이 이 거리 안을 지나면 최대 투명도이고, 여기서 투명 시작 반지름까지 점점 옅어진다. 투명 시작 반지름보다 크면 그 값으로 본다.")]
        [Range(0.05f, 5f)] public float fullRadius = 0.3f;

        [Tooltip("최대 투명도: 최대 투명도 반지름 안에서 다 흐려졌을 때 뚫는 픽셀 비율. 0이면 그대로, 1이면 안 보인다.")]
        [Range(0f, 1f)] public float fadeAmount = 0.75f;

        [Tooltip("앞 기준 거리(유닛). 므엥 몸 가운데에서 카메라 쪽으로 이만큼 간 평면(축에 수직)보다 카메라 쪽에 있는 것만 흐린다. 0.5면 므엥 겉면, 늘리면 므엥 옆·뒤의 것이 덜 흐려진다.")]
        [Range(0f, 3f)] public float frontOffset = 0.5f;

        [Tooltip("므엥을 가리는 것이 생기면 다 흐려지고, 없어지면 원래대로 돌아오는 데 걸리는 시간(초)")]
        [Range(0.01f, 1f)] public float fadeTime = 0.15f;
    }
}
