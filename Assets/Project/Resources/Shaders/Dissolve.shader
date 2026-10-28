// 적 사망 디졸브 셰이더 (EnemyDissolve가 쓰러진 시체의 재질을 잠깐 이것으로 교체)
// 원래 텍스처·색(_BaseMap·_BaseColor — 엘리트 보라 PropertyBlock 그대로 적용) + 간단 조명(주 조명 + 주변광)
// 월드 좌표 3D 값 노이즈(텍스처 불필요)가 _Progress보다 작은 곳을 잘라내고, 잘리는 경계를 _EdgeColor(HDR)로 빛나게 → 블룸
// Resources 폴더에 있어 Shader.Find로 찾을 수 있고 빌드에도 포함됨
Shader "TDShoot/Dissolve"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _Progress ("Progress", Range(0, 1.1)) = 0
        [HDR] _EdgeColor ("Edge Color", Color) = (3, 0.9, 0.25, 1)
        _EdgeWidth ("Edge Width", Range(0, 0.3)) = 0.08
        _NoiseScale ("Noise Scale", Float) = 3
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "AlphaTest" "RenderType" = "TransparentCutout" }

        Pass
        {
            Name "Dissolve"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off // 잘린 구멍으로 안쪽 면이 보이도록

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Progress;
                half4 _EdgeColor;
                half _EdgeWidth;
                half _NoiseScale;
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
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
            };

            float Hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            // 3D 값 노이즈 (0~1)
            float Noise(float3 x)
            {
                float3 i = floor(x);
                float3 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(
                    lerp(lerp(Hash(i + float3(0, 0, 0)), Hash(i + float3(1, 0, 0)), f.x),
                         lerp(Hash(i + float3(0, 1, 0)), Hash(i + float3(1, 1, 0)), f.x), f.y),
                    lerp(lerp(Hash(i + float3(0, 0, 1)), Hash(i + float3(1, 0, 1)), f.x),
                         lerp(Hash(i + float3(0, 1, 1)), Hash(i + float3(1, 1, 1)), f.x), f.y), f.z);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionHCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 frag(Varyings input, FRONT_FACE_TYPE isFrontFace : FRONT_FACE_SEMANTIC) : SV_Target
            {
                float3 p = input.positionWS * _NoiseScale;
                float n = Noise(p) * 0.7 + Noise(p * 2.3) * 0.3;
                clip(n - _Progress);

                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                half3 normal = normalize(input.normalWS) * IS_FRONT_VFACE(isFrontFace, 1.0, -1.0);
                Light mainLight = GetMainLight();
                half3 lighting = mainLight.color * saturate(dot(normal, mainLight.direction)) + SampleSH(normal);
                half3 color = albedo.rgb * lighting;

                // 잘리는 경계 근처를 불씨 색으로 (진행 중일 때만)
                half edge = (1.0 - saturate((n - _Progress) / max(_EdgeWidth, 0.001))) * step(0.001, _Progress);
                color = lerp(color, _EdgeColor.rgb, edge);
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
