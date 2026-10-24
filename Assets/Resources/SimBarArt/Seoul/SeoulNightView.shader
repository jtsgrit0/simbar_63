Shader "SimBar/Seoul Night Window View"
{
    Properties
    {
        [MainTexture] _BaseMap("Seoul Night Panorama", 2D) = "white" {}
        [MainColor] _BaseColor("Tint", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry+2" }
        Pass
        {
            Name "SeoulNightView"
            Tags { "LightMode" = "UniversalForwardOnly" }
            Cull Off
            ZWrite On
            ZTest LEqual
            Stencil
            {
                Ref 1
                ReadMask 1
                WriteMask 0
                Comp Equal
                Pass Keep
            }
            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };
            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }
            half4 Fragment(Varyings input) : SV_Target
            {
                half3 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
