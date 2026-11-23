using System;
using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 모델 겉모습 프리셋(애셋은 Assets/Data/Looks). 복셀 모델(<see cref="VoxPaint"/>)이 고르면 자기 값 대신 이 값을 쓴다:
    /// 투명도, 발광 세기·광원(범위·세기 배율·그림자), 부위마다의 재질감. 칠하는 색은 기물·캐릭터 코드가 바꾸는 값이라 넣지 않는다.
    /// 여러 모델·프리팹이 같이 쓰고, 고치면 쓰는 곳이 모두 바뀐다(에디터가 재질도 다시 끼움).
    /// </summary>
    [CreateAssetMenu(menuName = "Slime~ColorMaker~/모델 겉모습 프리셋", fileName = "ModelLook")]
    public class ModelLook : ScriptableObject
    {
        public const string Folder = "Assets/Data/Looks";

        [Tooltip("반투명 부분(41~56)의 투명도. 0이면 불투명하고 1이면 안 보인다.")]
        [Range(0f, 1f)] public float transparency = VoxPaint.DefaultTransparency;

        [Tooltip("방벽 묶음 바깥 테두리(가장자리 띠·모서리 조각)의 투명도(0 = 불투명 ~ 1 = 안 보임). 방벽만 쓴다(Play 중, 이어진 판과 맞닿은 테두리는 안쪽이라 위 값).")]
        [Range(0f, 1f)] public float edgeTransparency = VoxPaint.DefaultTransparency;

        [Tooltip("발광 부분(Blockbench Light, 렌더러 Light)의 발광 세기. 발광 = 칠하는 색 × 휘도 × 이 값. 광원 세기 = 이 값 × 광원 세기 배율. 0이면 빛나지 않는다.")]
        [Min(0f)] public float lightIntensity = 1f;

        [Tooltip("광원(발광 부분마다 하나, 포인트 라이트)이 닿는 거리(칸). 이 거리에서 0이 된다.")]
        [Min(0.01f)] public float lightRange = 3f;

        [Tooltip("광원 세기 = 발광 세기 × 이 값. 0이면 광원을 끈다(발광·번짐은 그대로).")]
        [Min(0f)] public float lightScale = 0.5f;

        [Tooltip("광원 그림자. 없음이 가장 가볍다(빛이 블록을 뚫고 닿음). 켜면 광원마다 그림자를 그려 무거워진다.")]
        public LightShadows lightShadows = LightShadows.None;

        [Tooltip("일반 칸(값 색·반투명이 아닌 칸)의 재질감(Assets/Data/Finishes). 비우면 광택 0.2 · 반사 0 · 빛 맺힘 · 주변 반사 켬.")]
        public SurfaceFinish bodyFinish;

        [Tooltip("칠하는 값 색 칸(33~40, 렌더러 ValueColor)의 재질감.")]
        public SurfaceFinish valueFinish;

        [Tooltip("반투명 칸(41~56, 렌더러 Glass·GlassValueColor)의 재질감.")]
        public SurfaceFinish glassFinish;

        [Tooltip("발광 부분(렌더러 Light)의 재질감.")]
        public SurfaceFinish lightFinish;

        /// <summary>인스펙터에서 값이 바뀌었다(쓰는 모델들이 다시 칠하고, 에디터가 재질을 다시 끼운다).</summary>
        public static event Action<ModelLook> Changed;

        void OnValidate() => Changed?.Invoke(this);
    }
}
