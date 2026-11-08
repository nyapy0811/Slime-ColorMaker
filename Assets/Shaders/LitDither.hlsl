// ColorMaze: Lit Dither 셰이더의 점무늬 투명(2026-11-08, 카메라에서 므엥을 보는 시선 구멍). 각 단계의 URP 패스 파일 뒤에 포함해 그 단계의 프래그먼트를 감싼다.
// 뚫는 비율 = 흐림 진행(_OcclusionFade) × 최대 투명도 × 시선 비율. 시선 비율은 카메라에서 이 픽셀로 가는 시선이 므엥 깊이(시선 축 위 므엥 몸 가운데)에서
// 축으로부터 떨어진 거리가 최대 투명도 반지름 안이면 1, 투명 시작 반지름 밖이면 0, 사이는 고르게 줄어든다 — 깊이와 상관없이 시선마다 같아서
// 같은 시선 위의 면은 같은 자리에서 함께 뚫린다. 앞 기준 평면(므엥 몸 가운데에서 카메라 쪽으로 앞 기준 거리, 축에 수직)보다 카메라 반대쪽이거나
// 므엥 발 높이 이하인 픽셀은 뚫지 않는다. 값은 모두 CameraOcclusionFader가 주는 전역 값이다(재질 값이 아니라 SRP Batcher에 그대로 묶임).
#ifndef COLORMAZE_LIT_DITHER_INCLUDED
#define COLORMAZE_LIT_DITHER_INCLUDED

float _OcclusionFade;
float4 _OcclusionAxisStart; // xyz: 므엥 몸 가운데
float4 _OcclusionAxisEnd;   // xyz: 카메라
float4 _OcclusionShape;     // x: 최대 투명도 반지름, y: 투명 시작 반지름, z: 최대 투명도, w: 앞 기준 거리
float _OcclusionFeet;       // 므엥 발 높이(이하는 뚫지 않음)

// 4x4 Bayer 순서. 모든 면이 같은 화면 자리를 뚫는다.
static const float DitherBayer4x4[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

float OcclusionAmount(float4 positionCS)
{
    float3 positionWS = ComputeWorldSpacePosition(GetNormalizedScreenSpaceUV(positionCS), positionCS.z, UNITY_MATRIX_I_VP);
    if (positionWS.y <= _OcclusionFeet) return 0.0;
    float3 camera = _OcclusionAxisEnd.xyz;
    float3 toTarget = _OcclusionAxisStart.xyz - camera;
    float targetDistance = length(toTarget);
    float3 axis = toTarget / max(targetDistance, 1e-5);
    float3 ray = positionWS - camera;
    float along = dot(ray, axis);
    if (along >= targetDistance - _OcclusionShape.w) return 0.0; // 앞 기준 평면보다 카메라 반대쪽
    float rayLength = length(ray);
    float cosine = along / max(rayLength, 1e-5);
    if (cosine <= 1e-3) return 0.0;
    float offAxis = targetDistance * sqrt(saturate(1.0 - cosine * cosine)) / cosine; // 므엥 깊이에서 시선이 축에서 떨어진 거리
    float shape = saturate((_OcclusionShape.y - offAxis) / max(_OcclusionShape.y - _OcclusionShape.x, 1e-4));
    return _OcclusionFade * _OcclusionShape.z * shape;
}

void DitherClip(float4 positionCS)
{
    if (_OcclusionFade <= 0.0) return;
    float fade = OcclusionAmount(positionCS);
    uint2 pixel = (uint2)positionCS.xy % 4u;
    float threshold = (DitherBayer4x4[pixel.y * 4u + pixel.x] + 0.5) / 16.0;
    clip(threshold - fade);
}

#endif

#if defined(UNIVERSAL_FORWARD_LIT_PASS_INCLUDED) && !defined(COLORMAZE_DITHER_FORWARD)
#define COLORMAZE_DITHER_FORWARD
void LitDitherForwardFragment(
    Varyings input
    , out half4 outColor : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out uint outRenderingLayers : SV_Target1
#endif
)
{
    DitherClip(input.positionCS);
    LitPassFragment(input, outColor
#ifdef _WRITE_RENDERING_LAYERS
        , outRenderingLayers
#endif
    );
}
#endif

#if defined(UNIVERSAL_LIT_GBUFFER_PASS_INCLUDED) && !defined(COLORMAZE_DITHER_GBUFFER)
#define COLORMAZE_DITHER_GBUFFER
GBufferFragOutput LitDitherGBufferFragment(Varyings input)
{
    DitherClip(input.positionCS);
    return LitGBufferPassFragment(input);
}
#endif

#if defined(UNIVERSAL_DEPTH_ONLY_PASS_INCLUDED) && !defined(COLORMAZE_DITHER_DEPTH_ONLY)
#define COLORMAZE_DITHER_DEPTH_ONLY
half LitDitherDepthOnlyFragment(Varyings input) : SV_TARGET
{
    DitherClip(input.positionCS);
    return DepthOnlyFragment(input);
}
#endif

#if defined(UNIVERSAL_FORWARD_LIT_DEPTH_NORMALS_PASS_INCLUDED) && !defined(COLORMAZE_DITHER_DEPTH_NORMALS)
#define COLORMAZE_DITHER_DEPTH_NORMALS
void LitDitherDepthNormalsFragment(
    Varyings input
    , out half4 outNormalWS : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out uint outRenderingLayers : SV_Target1
#endif
)
{
    DitherClip(input.positionCS);
    DepthNormalsFragment(input, outNormalWS
#ifdef _WRITE_RENDERING_LAYERS
        , outRenderingLayers
#endif
    );
}
#endif
