Shader "PetTown/Cozy/Meadow"
{
    Properties { _Tint("Tint",Color)=(1,1,1,1) }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            struct A { float4 p:POSITION;float3 n:NORMAL;float4 c:COLOR; };
            struct V { float4 p:SV_POSITION;float3 w:TEXCOORD0;float3 n:TEXCOORD1;float4 c:TEXCOORD2; };
            float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}
            V vert(A a){V o;o.w=TransformObjectToWorld(a.p.xyz);o.p=TransformWorldToHClip(o.w);o.n=TransformObjectToWorldNormal(a.n);o.c=a.c;return o;}
            half4 frag(V i):SV_Target
            {
                float n=noise(i.w.xz*.18)*.72+noise(i.w.xz*1.4)*.28;
                half3 grass=lerp(half3(.28,.43,.20),half3(.40,.53,.28),n);
                // Preserve painted ground regions without retaining photographic contrast.
                grass=lerp(grass,half3(.57,.46,.27),saturate(i.c.r)*.22);
                grass=lerp(grass,half3(.48,.58,.25),saturate(i.c.a)*.12);
                float fleck=smoothstep(.93,.99,hash(floor(i.w.xz*7)));
                grass+=fleck*.018;
                Light sun=GetMainLight(TransformWorldToShadowCoord(i.w));half3 normal=normalize(i.n);
                half3 light=SampleSH(normal)+sun.color*saturate(dot(normal,sun.direction))*lerp(.5,1,sun.shadowAttenuation);
                return half4(grass*max(light,half3(.4,.4,.4)),1);
            }
            ENDHLSL
        }
    }
}
