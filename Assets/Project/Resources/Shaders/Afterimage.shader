// 대시 잔상용 반투명 셰이더 (DashAfterimage가 사용)
// 조명 없이 _Color, 가장자리(림)일수록 밝게 + 가산 혼합으로 살짝 빛남, 깊이 쓰기 없음
// 알파는 잔상마다 MaterialPropertyBlock으로 줄여 사라지게 함
// Resources 폴더에 있어 Shader.Find로 찾을 수 있고 빌드에도 포함됨
Shader "TDShoot/Afterimage"
{
    Properties
    {
        _Color ("Color", Color) = (0.3, 0.85, 1, 0.5)
        _RimPower ("Rim Power", Float) = 2
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" }

        Pass
        {
            Name "Afterimage"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _RimPower;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionHCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 viewDir = GetWorldSpaceNormalizeViewDir(input.positionWS); // 직교 카메라도 대응
                half rim = pow(1.0h - saturate(dot(normalize(input.normalWS), viewDir)), _RimPower);
                return half4(_Color.rgb, _Color.a * (0.35h + 0.65h * rim));
            }
            ENDHLSL
        }
    }
}
