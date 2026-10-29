// 바닥 안개 전용 셰이더 (AtmosphereLighting GroundFog 파티클)
// URP Particles/Lit 대신 사용 — 빌드에서 반투명 변형이 빠져 안개 판이 사각형으로 보이던 문제(2026-10-29) 대응
// 키워드로 바뀌는 표면 설정이 없어 변형 걱정 없음, Resources 폴더라 Shader.Find로 찾고 빌드에 포함됨
// 텍스처 알파 x 파티클 색 x _BaseColor로 부드러운 가장자리, 조명 = 주변광(SH) + 주 조명 + 추가 조명(Forward+ 클러스터 / 일반)
//   면 방향(NdotL)과 그림자는 쓰지 않음 → 빛 웅덩이 안에서만 안개가 드러남
Shader "TDShoot/GroundFog"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            Name "GroundFog"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half4 color : COLOR;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half4 baseColor = tex * input.color * _BaseColor;

                half3 light = SampleSH(half3(0, 1, 0));
                Light mainLight = GetMainLight();
                light += mainLight.color * mainLight.distanceAttenuation;

                // LIGHT_LOOP_BEGIN이 inputData 이름을 직접 사용
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

            #if defined(_ADDITIONAL_LIGHTS) || USE_CLUSTER_LIGHT_LOOP
                uint lightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(lightCount)
                    Light additional = GetAdditionalLight(lightIndex, input.positionWS);
                    light += additional.color * additional.distanceAttenuation;
                LIGHT_LOOP_END
            #endif

                return half4(baseColor.rgb * light, baseColor.a);
            }
            ENDHLSL
        }
    }
}
