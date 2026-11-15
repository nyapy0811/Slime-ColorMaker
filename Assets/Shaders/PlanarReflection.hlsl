// Slime~ColorMaker~: ① 평면 반사(ReflectionSettings). Lit Glass의 ForwardLit 단계가 URP Lit의 LitPassFragment 대신 PlanarReflectionLitPassFragment를 쓴다.
// _PLANAR_REFLECTION(코팅 재질만 켬)이 켜져 있고 이 픽셀이 위를 보는 면이며 그 높이의 반사 그림(PlanarReflection.cs가 그림)이 있으면,
// ②·③ 반사(실내 반사 탐침·하늘)를 빼고 평면 반사를 더한다(간접광은 선형이라 URP의 GlobalIllumination 결과에서 그 항만 바꾼 것과 같다).
// 나머지(빛, 그림자, 빛 번짐 탐침, 안개)는 URP Lit과 같다. LitPassFragment 본문은 URP 17.6에서 복사했다(URP를 올리면 다시 복사).
#ifndef SLIME_PLANAR_REFLECTION_INCLUDED
#define SLIME_PLANAR_REFLECTION_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"

TEXTURE2D(_PlanarReflectionTex0);
TEXTURE2D(_PlanarReflectionTex1);
TEXTURE2D(_PlanarReflectionTex2);
TEXTURE2D(_PlanarReflectionTex3);
SAMPLER(sampler_PlanarReflectionTex0);
float4 _PlanarReflectionHeights; // 평면 높이(칸 x: 0~3번 그림)
float4 _PlanarReflectionValid;   // 그 그림이 이번 프레임에 있으면 1

// 이 픽셀 높이의 평면 반사. 없으면 false.
bool SamplePlanarReflection(float3 positionWS, float2 screenUV, half mip, out half3 reflection)
{
    reflection = 0;
    const float tolerance = 0.01;
    if (_PlanarReflectionValid.x > 0.5 && abs(positionWS.y - _PlanarReflectionHeights.x) < tolerance)
        reflection = SAMPLE_TEXTURE2D_LOD(_PlanarReflectionTex0, sampler_PlanarReflectionTex0, screenUV, mip).rgb;
    else if (_PlanarReflectionValid.y > 0.5 && abs(positionWS.y - _PlanarReflectionHeights.y) < tolerance)
        reflection = SAMPLE_TEXTURE2D_LOD(_PlanarReflectionTex1, sampler_PlanarReflectionTex0, screenUV, mip).rgb;
    else if (_PlanarReflectionValid.z > 0.5 && abs(positionWS.y - _PlanarReflectionHeights.z) < tolerance)
        reflection = SAMPLE_TEXTURE2D_LOD(_PlanarReflectionTex2, sampler_PlanarReflectionTex0, screenUV, mip).rgb;
    else if (_PlanarReflectionValid.w > 0.5 && abs(positionWS.y - _PlanarReflectionHeights.w) < tolerance)
        reflection = SAMPLE_TEXTURE2D_LOD(_PlanarReflectionTex3, sampler_PlanarReflectionTex0, screenUV, mip).rgb;
    else
        return false;
    return true;
}

// 주변 반사를 평면 반사로 바꾸는 만큼의 색 차이.
half3 PlanarReflectionDelta(InputData inputData, SurfaceData surfaceData)
{
#if defined(_PLANAR_REFLECTION) && !defined(_ENVIRONMENTREFLECTIONS_OFF)
    if (inputData.normalWS.y < 0.9) return 0; // 윗면만(옆면 띠는 그대로)

    BRDFData brdfData;
    InitializeBRDFData(surfaceData, brdfData);
    half3 planar;
    if (!SamplePlanarReflection(inputData.positionWS, inputData.normalizedScreenSpaceUV, PerceptualRoughnessToMipmapLevel(brdfData.perceptualRoughness), planar))
        return 0;

    half3 reflectVector = reflect(-inputData.viewDirectionWS, inputData.normalWS);
    half3 environment = GlossyEnvironmentReflection(reflectVector, inputData.positionWS, brdfData.perceptualRoughness, 1.0h, inputData.normalizedScreenSpaceUV);
    half NoV = saturate(dot(inputData.normalWS, inputData.viewDirectionWS));
    half fresnelTerm = Pow4(1.0 - NoV);
    AmbientOcclusionFactor aoFactor = CreateAmbientOcclusionFactor(inputData, surfaceData);
    return (planar - environment) * EnvironmentBRDFSpecular(brdfData, fresnelTerm) * aoFactor.indirectAmbientOcclusion;
#else
    return 0;
#endif
}

void PlanarReflectionLitPassFragment(
    Varyings input
    , out half4 outColor : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out uint outRenderingLayers : SV_Target1
#endif
)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

#if defined(_PARALLAXMAP)
#if defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
    half3 viewDirTS = input.viewDirTS;
#else
    half3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
    half3 viewDirTS = GetViewDirectionTangentSpace(input.tangentWS, input.normalWS, viewDirWS);
#endif
    ApplyPerPixelDisplacement(viewDirTS, input.uv);
#endif

    SurfaceData surfaceData;
    InitializeStandardLitSurfaceData(input.uv, surfaceData);

#ifdef LOD_FADE_CROSSFADE
    LODFadeCrossFade(input.positionCS);
#endif

    InputData inputData;
    InitializeInputData(input, surfaceData.normalTS, inputData);
    SETUP_DEBUG_TEXTURE_DATA(inputData, UNDO_TRANSFORM_TEX(input.uv, _BaseMap));

#if defined(_DBUFFER)
    ApplyDecalToSurfaceData(input.positionCS, surfaceData, inputData);
#endif

    InitializeBakedGIData(input, inputData);

    half4 color = UniversalFragmentPBR(inputData, surfaceData);
    color.rgb += PlanarReflectionDelta(inputData, surfaceData); // Slime~ColorMaker~: 평면 반사
    color.rgb = MixFog(color.rgb, inputData.fogCoord);
    color.a = OutputAlpha(color.a, IsSurfaceTypeTransparent());

    outColor = color;

#ifdef _WRITE_RENDERING_LAYERS
    outRenderingLayers = EncodeMeshRenderingLayer();
#endif
}

#endif
