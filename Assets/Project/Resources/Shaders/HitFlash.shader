// 피격 번쩍임용 단색 셰이더 (EnemyHitFlash가 맞은 순간 원래 재질 대신 잠깐 사용)
// 조명 없이 _FlashColor 그대로 출력 — 1보다 큰 HDR 값이면 블룸으로 살짝 빛남
// 색 이름을 _Color가 아닌 _FlashColor로 둔 이유: 엘리트 보라색 MaterialPropertyBlock(_Color·_BaseColor)이 렌더러에 남아 있어도 영향 없음
// Resources 폴더에 있어 Shader.Find로 찾을 수 있고 빌드에도 포함됨
Shader "TDShoot/HitFlash"
{
    Properties
    {
        [HDR] _FlashColor ("Flash Color", Color) = (1.5, 1.5, 1.5, 1)
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" "RenderType" = "Opaque" }

        Pass
        {
            Name "HitFlash"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _FlashColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return _FlashColor;
            }
            ENDHLSL
        }
    }
}
