Shader "Bastion/FoundryVolume"
{
    Properties
    {
        _BaseColor("Scattering", Color) = (.4,.5,.55,.15)
        _Density("Density", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Front
            ZTest Always
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            UNITY_INSTANCING_BUFFER_START(Particle)
                UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
                UNITY_DEFINE_INSTANCED_PROP(float, _Density)
            UNITY_INSTANCING_BUFFER_END(Particle)
            Varyings Vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v,o);
                o.positionWS=TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS=TransformWorldToHClip(o.positionWS);
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float3 origin=TransformWorldToObject(GetCameraPositionWS());
                float3 ray=normalize(TransformWorldToObject(i.positionWS)-origin);
                float b=dot(origin,ray);
                float3 closest=origin-ray*b;
                float discriminant=.25-dot(closest,closest);
                if(discriminant<=0) discard;
                float root=sqrt(discriminant);
                float entry=max(0,-b-root), exitDistance=-b+root;
                float2 screenUV=i.positionCS.xy/_ScaledScreenParams.xy;
                float depth=SampleSceneDepth(screenUV);
                #if !UNITY_REVERSED_Z
                    depth=lerp(UNITY_NEAR_CLIP_VALUE,1,depth);
                #endif
                float3 sceneWS=ComputeWorldSpacePosition(screenUV,depth,UNITY_MATRIX_I_VP);
                exitDistance=min(exitDistance,dot(TransformWorldToObject(sceneWS)-origin,ray));
                float stepLength=max(0,exitDistance-entry)/6;
                float mass=0;
                [unroll] for(int n=0;n<6;n++)
                {
                    float3 p=origin+ray*(entry+(n+.5)*stepLength);
                    float profile=saturate(1-4*dot(p,p));
                    float swirl=.78+.14*sin(p.x*13+p.y*9+_Time.y*.7)*sin(p.z*11-p.y*7);
                    mass+=profile*profile*swirl*stepLength;
                }
                float4 color=UNITY_ACCESS_INSTANCED_PROP(Particle,_BaseColor);
                float density=UNITY_ACCESS_INSTANCED_PROP(Particle,_Density);
                float opacity=(1-exp(-mass*density*5))*color.a;
                return half4(color.rgb,opacity);
            }
            ENDHLSL
        }
    }
}
