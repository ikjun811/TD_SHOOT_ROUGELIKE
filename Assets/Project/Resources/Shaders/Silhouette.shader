// 벽 등에 가려진 부분에만 단색 실루엣을 그리는 셰이더 (SilhouetteRenderer가 몸체 재질 뒤에 덧붙이거나 복제 렌더러에 사용)
// 색 속성 이름 _SilhouetteColor: 원래 몸체에 덧붙여 그릴 때 엘리트 PropertyBlock(_Color·_BaseColor)이 덮지 않게
// 1패스(SRPDefaultUnlit): 캐릭터가 보이는 픽셀에 스텐실 표시 → 자기 몸에 가려진 부분(몸통 뒤 팔 등)은 실루엣 제외
// 2패스(UniversalForward): 가려진 픽셀(ZTest Greater) 중 표시 없는 곳에만 반투명 색, 같은 픽셀 중복 블렌딩 방지
// SilhouetteRenderer는 패스별로 재질을 나눠 큐를 다르게 줌 → 모든 캐릭터의 표시가 끝난 뒤 색칠 (여러 메시로 된 모델의 관절·장비 대응)
// Resources 폴더에 있어 Shader.Find로 찾을 수 있고 빌드에도 포함됨
Shader "TDShoot/Silhouette"
{
    Properties
    {
        _SilhouetteColor ("Color", Color) = (0.3, 0.85, 1, 0.6)
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent+100" "RenderType" = "Transparent" "IgnoreProjector" = "True" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _SilhouetteColor;
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
        ENDHLSL

        Pass
        {
            Name "SilhouetteMask"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            ZWrite Off
            ZTest LEqual
            ColorMask 0
            Stencil
            {
                Ref 64
                ReadMask 64
                WriteMask 64
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            half4 frag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "SilhouetteColor"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite Off
            ZTest Greater
            Blend SrcAlpha OneMinusSrcAlpha
            Stencil
            {
                Ref 64
                ReadMask 64
                WriteMask 64
                Comp NotEqual
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            half4 frag(Varyings input) : SV_Target
            {
                return _SilhouetteColor;
            }
            ENDHLSL
        }
    }
}
