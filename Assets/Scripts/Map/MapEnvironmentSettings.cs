using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace SlimeColorMaker
{
    /// <summary>
    /// 맵 환경 수치(애셋은 Assets/Data/Map): 하늘·환경 조명·반사(반사 설정 애셋을 가리킴)·안개·태양·구운 조명. 모든 맵이 MapRoot의 <see cref="MapEnvironment"/>로
    /// 같은 애셋을 쓴다(한 곳만 고치면 모든 맵이 바뀜). 애셋이라 Play 모드에서 고친 값도 남고, 고치면 열린 맵에 바로 보인다.
    /// </summary>
    [CreateAssetMenu(menuName = "Slime~ColorMaker~/맵 환경 설정", fileName = "MapEnvironmentSettings")]
    public class MapEnvironmentSettings : ScriptableObject
    {
        /// <summary>애셋 값이 바뀌었다(인스펙터에서 고침). 열린 맵의 <see cref="MapEnvironment"/>가 다시 적용한다.</summary>
        public static event Action Changed;

        [Header("하늘")]
        [Tooltip("하늘 상자 재질")]
        public Material skybox;

        [Header("환경 조명")]
        [Tooltip("환경 조명 방식: Skybox(하늘 상자에서), Trilight(위·가운데·아래 3색 그라디언트), Flat(위 색 한 가지)")]
        public AmbientMode ambientMode = AmbientMode.Trilight;

        [Tooltip("위 색(Trilight), 한 가지 색(Flat)")]
        [ColorUsage(false, true)] public Color ambientSkyColor = new(0.412f, 0.412f, 0.412f);

        [Tooltip("가운데(수평) 색, Trilight에서만")]
        [ColorUsage(false, true)] public Color ambientEquatorColor = new(0.581f, 0.581f, 0.581f);

        [Tooltip("아래 색, Trilight에서만")]
        [ColorUsage(false, true)] public Color ambientGroundColor = new(0.047f, 0.047f, 0.047f);

        [Tooltip("환경 조명 세기, Skybox 방식에서만")]
        [Range(0f, 8f)] public float ambientIntensity = 1f;

        [Header("반사")]
        [Tooltip("반사 설정(Assets/Data/Map/ReflectionSettings): ① 평면 반사 → ② 실내 반사 탐침 → ③ 하늘 반사 순서로 쓰는 반사의 값이 모두 여기 있다.")]
        public ReflectionSettings reflection;

        [Header("안개")]
        [Tooltip("안개를 켠다")]
        public bool fog;

        [Tooltip("안개 색")]
        public Color fogColor = new(0.5f, 0.5f, 0.5f);

        [Tooltip("안개 방식: Linear(시작~끝 거리), Exponential·ExponentialSquared(농도)")]
        public FogMode fogMode = FogMode.ExponentialSquared;

        [Tooltip("안개 농도(Exponential 방식)")]
        [Range(0f, 1f)] public float fogDensity = 0.01f;

        [Tooltip("안개 시작 거리(Linear 방식, 유닛)")]
        [Min(0f)] public float fogStart;

        [Tooltip("안개가 다 차는 거리(Linear 방식, 유닛)")]
        [Min(0f)] public float fogEnd = 300f;

        [Header("태양")]
        [Tooltip("태양(방향광) 색")]
        public Color sunColor = Color.white;

        [Tooltip("태양 세기")]
        [Range(0f, 8f)] public float sunIntensity = 2f;

        [Tooltip("태양 방향(오일러 각, 도). X가 높이(90이면 바로 위에서), Y가 수평 방향")]
        public Vector3 sunAngles = new(50f, -30f, 0f);

        [Tooltip("그림자: None(없음), Hard(딱딱함), Soft(부드러움)")]
        public LightShadows sunShadows = LightShadows.Soft;

        [Tooltip("그림자 짙기")]
        [Range(0f, 1f)] public float sunShadowStrength = 1f;

        [Header("구운 조명 (바꾸면 맵을 저장할 때 다시 구움)")]
        [Tooltip("태양 빛 번짐 세기: 태양 빛이 블록에 튕겨 주변을 밝히는 정도(구운 조명에 들어감). 0이면 번지지 않는다.")]
        [Range(0f, 4f)] public float sunBounce = 1f;

        [Tooltip("빛 번짐 탐침 간격(유닛). 작을수록 빛 번짐이 촘촘하지만 굽기가 느리고 데이터가 커진다. 1이면 블록 한 칸.")]
        [Range(0.25f, 3f)] public float probeSpacing = 1f;

        void OnValidate() => Changed?.Invoke();
    }
}
