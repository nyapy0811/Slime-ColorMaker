using System;
using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 재질감(2026-11-14, 애셋은 Assets/Data/Finishes): 광택·반사와 빛 맺힘·주변 반사 켜기. 프리팹의 <see cref="VoxPaint"/>가 부위(일반·값 색·반투명)마다,
    /// 미끄러운 코팅 설정이 종류(기름·얼음)마다 고른다. 같은 애셋을 여러 곳이 같이 쓰고, 값을 고치면 쓰는 곳이 모두 바뀐다.
    /// 복셀 모델에는 '원래 재질 + 이 재질감' 재질을 이 애셋의 하위 애셋으로 만들어 쓴다(에디터의 SurfaceFinishMaterials, 같은 프리팹끼리 재질을 같이 써 묶어 그리기 유지).
    /// </summary>
    [CreateAssetMenu(menuName = "Slime~ColorMaker~/재질감", fileName = "SurfaceFinish")]
    public class SurfaceFinish : ScriptableObject
    {
        /// <summary>재질감을 고르지 않은 복셀 재질의 광택(가져오기 코드의 값).</summary>
        public const float DefaultSmoothness = 0.2f;

        static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        static readonly int MetallicId = Shader.PropertyToID("_Metallic");
        static readonly int SpecularHighlightsId = Shader.PropertyToID("_SpecularHighlights");
        static readonly int EnvironmentReflectionsId = Shader.PropertyToID("_EnvironmentReflections");
        const string SpecularHighlightsOff = "_SPECULARHIGHLIGHTS_OFF";
        const string EnvironmentReflectionsOff = "_ENVIRONMENTREFLECTIONS_OFF";

        [Tooltip("광택(Smoothness, 0~1). 높을수록 빛 맺힘이 작고 또렷해지고 주변 반사가 선명해진다. 재질감을 고르지 않은 모델은 0.2.")]
        [Range(0f, 1f)] public float smoothness = DefaultSmoothness;

        [Tooltip("반사(Metallic, 0~1). 높을수록 주변(맵 하늘 상자)이 많이 비치고 자기 색이 반사에 섞인다(금속처럼). 재질감을 고르지 않은 모델은 0.")]
        [Range(0f, 1f)] public float metallic;

        [Tooltip("빛 맺힘(Specular Highlights): 태양·조명이 표면에 만드는 반짝 점. 끄면 생기지 않는다.")]
        public bool specularHighlights = true;

        [Tooltip("주변 반사(Environment Reflections): 맵 하늘 상자가 표면에 비치는 것. 끄면 비치지 않는다.")]
        public bool environmentReflections = true;

        /// <summary>인스펙터에서 값이 바뀌었다(에디터가 이 재질감의 재질을 다시 맞추고, 코팅이 다시 그린다).</summary>
        public static event Action<SurfaceFinish> Changed;

        void OnValidate() => Changed?.Invoke(this);

        /// <summary>재질에 이 재질감을 입힌다(URP Lit·Lit Glass의 광택·반사 값과 켜기 키워드).</summary>
        public void ApplyTo(Material material)
        {
            material.SetFloat(SmoothnessId, smoothness);
            material.SetFloat(MetallicId, metallic);
            material.SetFloat(SpecularHighlightsId, specularHighlights ? 1f : 0f);
            material.SetFloat(EnvironmentReflectionsId, environmentReflections ? 1f : 0f);
            SetKeyword(material, SpecularHighlightsOff, !specularHighlights);
            SetKeyword(material, EnvironmentReflectionsOff, !environmentReflections);
        }

        /// <summary>재질이 이미 이 재질감과 같은가.</summary>
        public bool Matches(Material material) =>
            Mathf.Approximately(material.GetFloat(SmoothnessId), smoothness)
            && Mathf.Approximately(material.GetFloat(MetallicId), metallic)
            && material.IsKeywordEnabled(SpecularHighlightsOff) == !specularHighlights
            && material.IsKeywordEnabled(EnvironmentReflectionsOff) == !environmentReflections;

        static void SetKeyword(Material material, string keyword, bool on)
        {
            if (on) material.EnableKeyword(keyword);
            else material.DisableKeyword(keyword);
        }
    }
}
