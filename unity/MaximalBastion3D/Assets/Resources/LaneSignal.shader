Shader "Bastion/LaneSignal"
{
    Properties { [HDR] _BaseColor("Lamp radiance", Color) = (1,.45,.10,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 positionOS : POSITION; float2 path : TEXCOORD1; };
            struct V { float4 positionCS : SV_POSITION; float path : TEXCOORD0; half fog : TEXCOORD1; };
            CBUFFER_START(UnityPerMaterial) half4 _BaseColor; CBUFFER_END
            V Vert(A v)
            {
                V o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.path = v.path.x;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }
            half4 Frag(V i) : SV_Target
            {
                float phase = frac((i.path - _Time.y * 1.6) / 8.8);
                float pulse = pow(.5 + .5 * cos(phase * 6.2831853), 10);
                return half4(MixFog(_BaseColor.rgb * (.38 + pulse * .64), i.fog), 1);
            }
            ENDHLSL
        }
    }
}
