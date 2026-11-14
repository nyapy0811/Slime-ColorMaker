// Slime~ColorMaker~: 캐릭터 바닥 흔적(SlimeTrail)용 URP 셰이더. 조명 없이 꼭짓점 색(선형 색, 알파 = 진하기)만 그린다.
// URP 기본 Unlit은 꼭짓점 색을 쓰지 않아 따로 둔다. 바닥 바로 위에 깔리므로 깊이를 쓰지 않고 바닥 쪽으로 깊이를 조금 당긴다.
Shader "Slime~ColorMaker~/Trail"
{
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Offset -1, -1

        Pass
        {
            Name "Unlit"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return input.color;
            }
            ENDHLSL
        }
    }
}
