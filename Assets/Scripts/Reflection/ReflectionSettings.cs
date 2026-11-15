using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace SlimeColorMaker
{
    /// <summary>
    /// 반사 설정(애셋은 Assets/Data/Map, 맵 환경 설정이 가리킴). 반사는 픽셀마다 아래 순서로 처음 있는 것을 쓴다.
    /// ① 평면 반사(<see cref="PlanarReflection"/>): 코팅 윗면, Play 중. 주변 물체·캐릭터·하늘이 제자리에 비친다.
    /// ② 실내 반사 탐침: 덮인 공간(방·굴·처마 밑) 안. 조명 굽기 때 자동으로 놓고 굽는다(<c>IndoorReflectionProbes</c>).
    /// ③ 하늘 반사: 그 밖 모두. 하늘 상자나 큐브맵을 비춘다(씬의 RenderSettings, 맵 환경이 적용).
    /// ②의 값을 바꾸면 맵을 저장할 때 조명을 다시 굽는다.
    /// </summary>
    [CreateAssetMenu(menuName = "Slime~ColorMaker~/반사 설정", fileName = "ReflectionSettings")]
    public class ReflectionSettings : ScriptableObject
    {
        /// <summary>애셋 값이 바뀌었다(인스펙터에서 고침). 열린 맵의 맵 환경이 다시 적용한다.</summary>
        public static event Action Changed;

        /// <summary>지금 열린 맵의 반사 설정(맵 환경이 적용할 때 정함).</summary>
        public static ReflectionSettings Current { get; set; }

        [Header("① 평면 반사 (코팅 윗면, Play 중)")]
        [Tooltip("켜면 코팅 윗면에 주변 물체·캐릭터·하늘이 제자리에 비친다(코팅 높이마다 장면을 한 번 더 그림). 끄면 ②·③을 비춘다.")]
        public bool planar = true;

        [Tooltip("평면 반사 그림의 해상도(화면 대비 배수). 작을수록 가볍고 흐리다.")]
        [Range(0.25f, 1f)] public float planarScale = 0.5f;

        [Tooltip("한 번에 그리는 반사 평면(코팅 높이) 수의 최대(1~4). 보이는 높이가 더 많으면 카메라와 높이가 가까운 것부터 그리고, 나머지는 ②·③.")]
        [Range(1, PlanarReflection.MaxPlanes)] public int planarMaxPlanes = 2;

        [Header("② 실내 반사 탐침 (덮인 공간, 바꾸면 다시 굽기)")]
        [Tooltip("위가 덮인 빈 칸 덩어리(방·굴·처마 밑)가 이 칸 수 이상이면 반사 탐침을 하나 둔다. 그 안의 반사가 하늘 대신 그 공간을 비춘다. 0이면 두지 않는다.")]
        [Min(0)] public int indoorMinCells = 4;

        [Tooltip("실내 반사 탐침 해상도(픽셀, 2의 거듭제곱으로 맞춤). 클수록 반사가 또렷하지만 굽기가 느리고 데이터가 커진다.")]
        [Range(16, 256)] public int indoorResolution = 64;

        [Tooltip("실내 반사 탐침 범위 경계에서 바깥 반사(하늘)와 섞는 거리(칸). 클수록 실내외 전환이 부드럽다. 굽지 않아도 바로 적용.")]
        [Range(0f, 2f)] public float indoorBlendDistance = 1f;

        [Header("③ 하늘 반사 (그 밖 모두)")]
        [Tooltip("하늘 반사 방식: Skybox(하늘 상자를 비춤), Custom(아래 큐브맵을 비춤, 비우면 반사 없음)")]
        public DefaultReflectionMode skyMode = DefaultReflectionMode.Skybox;

        [Tooltip("Custom 방식에서 비출 큐브맵")]
        public Cubemap skyCubemap;

        [Tooltip("하늘 반사 세기")]
        [Range(0f, 1f)] public float skyIntensity = 1f;

        void OnValidate() => Changed?.Invoke();
    }
}
