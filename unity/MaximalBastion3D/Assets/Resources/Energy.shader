Shader "Bastion/Energy"
{
    Properties { [HDR] _BaseColor("Energy", Color) = (1,1,1,1) _Fade("Fade", Float) = 1 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; };
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor; float _Fade;
            CBUFFER_END
            Varyings Vert(Attributes v) { Varyings o; o.positionWS=TransformObjectToWorld(v.positionOS.xyz); o.positionCS=TransformWorldToHClip(o.positionWS); o.normalWS=TransformObjectToWorldNormal(v.normalOS); return o; }
            half4 Frag(Varyings i) : SV_Target
            {
                float facing = abs(dot(normalize(i.normalWS), normalize(GetWorldSpaceViewDir(i.positionWS))));
                return half4(_BaseColor.rgb, _BaseColor.a * _Fade * smoothstep(0, .85, facing));
            }
            ENDHLSL
        }
    }
}
