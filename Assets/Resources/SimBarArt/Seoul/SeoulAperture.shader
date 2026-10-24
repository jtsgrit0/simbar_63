Shader "SimBar/Seoul Window Aperture"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry+1" }
        Pass
        {
            Name "WindowAperture"
            Tags { "LightMode" = "UniversalForwardOnly" }
            Cull Off
            ColorMask 0
            ZWrite Off
            ZTest LEqual
            Stencil
            {
                Ref 1
                ReadMask 1
                WriteMask 1
                Comp Always
                Pass Replace
            }
            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }
            half4 Fragment(Varyings input) : SV_Target { return half4(0, 0, 0, 0); }
            ENDHLSL
        }
    }
    Fallback Off
}
