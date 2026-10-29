// 천장 조명 빛줄기 (가짜 볼류메트릭, AtmosphereLighting이 원뿔 메시에 사용)
// 조명 없이 _Color, 가산 혼합. 카메라를 정면으로 보는 면일수록 진하고 윤곽(옆면)일수록 옅게 → 원뿔 가장자리가 부드러움
// uv.y = 1(위, 조명) → 0(바닥)으로 옅어짐. 깜빡임은 렌더러별 MaterialPropertyBlock으로 _Color 알파 조절
// Resources 폴더에 있어 Shader.Find로 찾을 수 있고 빌드에도 포함됨
Shader "TDShoot/LightShaft"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.88, 0.7, 0.12)
        _EdgePower ("Edge Softness", Float) = 1.5
        _FadePower ("Vertical Fade", Float) = 1.3
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            Name "LightShaft"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _EdgePower;
                half _FadePower;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionHCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 viewDir = GetWorldSpaceNormalizeViewDir(input.positionWS); // 직교 카메라도 대응
                half facing = abs(dot(normalize(input.normalWS), viewDir));
                half edge = pow(facing, _EdgePower);
                half fade = pow(saturate(input.uv.y), _FadePower);
                return half4(_Color.rgb, _Color.a * edge * fade);
            }
            ENDHLSL
        }
    }
}
