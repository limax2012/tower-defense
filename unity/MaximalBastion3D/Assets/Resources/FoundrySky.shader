Shader "Bastion/FoundrySky"
{
    Properties
    {
        _Tex("Industrial radiance", Cube) = "grey" {}
        _Exposure("Exposure", Float) = .65
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURECUBE(_Tex); SAMPLER(sampler_Tex);
            float _Exposure;
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 direction : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                return half4(SAMPLE_TEXTURECUBE(_Tex, sampler_Tex, input.direction).rgb * _Exposure, 1);
            }
            ENDHLSL
        }
    }
}
