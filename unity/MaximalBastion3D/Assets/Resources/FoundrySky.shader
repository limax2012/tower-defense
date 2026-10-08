Shader "Bastion/FoundrySky"
{
    Properties
    {
        _Tex("Industrial radiance", Cube) = "grey" {}
        _Exposure("Exposure", Float) = 1
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
                float3 direction = normalize(input.direction);
                float elevation = max(0, direction.y);
                float3 horizon = float3(.023, .043, .074);
                float3 zenith = float3(.0035, .009, .023);
                float3 sky = lerp(horizon, zenith, pow(elevation, .46));
                float horizonHaze = exp(-abs(direction.y) * 12);
                float industrialGlow = pow(saturate(dot(normalize(float3(-.4, 0, -.9)), normalize(float3(direction.x, .001, direction.z))) * .5 + .5), 5);
                sky += float3(.029, .012, .004) * horizonHaze * industrialGlow;
                // Broad stationary cloud layers remain quiet behind the architecture.
                float stratus = sin(direction.x * 4.7 + direction.z * 3.1 + direction.y * 27);
                stratus += .45 * sin(direction.z * 9.3 - direction.x * 6.1 + direction.y * 39);
                sky += float3(.0025, .0037, .0054) * stratus * saturate(1 - elevation * 1.8);
                return half4(max(sky, 0) * _Exposure, 1);
            }
            ENDHLSL
        }
    }
}
