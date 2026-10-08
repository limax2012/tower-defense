Shader "Bastion/Ghost"
{
    Properties { _BaseColor("Tint", Color) = (.2,.8,.6,.35) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct V { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };
            CBUFFER_START(UnityPerMaterial) half4 _BaseColor; CBUFFER_END
            V Vert(A v) { V o; o.positionCS=TransformObjectToHClip(v.positionOS.xyz); o.normalWS=TransformObjectToWorldNormal(v.normalOS); return o; }
            half4 Frag(V i) : SV_Target { return half4(_BaseColor.rgb*(.4+.6*abs(i.normalWS.y)),_BaseColor.a); }
            ENDHLSL
        }
    }
}
