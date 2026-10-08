Shader "Bastion/Shield"
{
    Properties { _Impact("Impact", Range(0,1)) = 0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; };
            CBUFFER_START(UnityPerMaterial)
                float _Impact;
            CBUFFER_END
            Varyings Vert(Attributes v)
            {
                Varyings o; o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS); return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                float facing = saturate(dot(normalize(i.normalWS), normalize(GetWorldSpaceViewDir(i.positionWS))));
                float edge = pow(1 - facing, 3.5);
                half3 color = lerp(half3(.14, .62, 1.1), half3(.55, 1.1, 1.5), _Impact);
                return half4(color, .012 + edge * (.24 + _Impact * .23));
            }
            ENDHLSL
        }
    }
}
