Shader "PetTown/ArtDirection/Meadow"
{
    Properties { _BaseColor("Tint",Color)=(1,1,1,1) }
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
            struct A { float4 p:POSITION; float3 n:NORMAL; };
            struct V { float4 p:SV_POSITION; float3 w:TEXCOORD0;float3 n:TEXCOORD1; };
            float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}
            V vert(A a){V o;o.w=TransformObjectToWorld(a.p.xyz);o.p=TransformWorldToHClip(o.w);o.n=TransformObjectToWorldNormal(a.n);return o;}
            half4 frag(V i):SV_Target
            {
                float n=noise(i.w.xz*.18)*.7+noise(i.w.xz*1.4)*.3;
                half3 grass=lerp(half3(.40,.60,.19),half3(.59,.75,.30),n);
                Light sun=GetMainLight(TransformWorldToShadowCoord(i.w));half d=saturate(dot(normalize(i.n),sun.direction));
                half3 lit=grass*(SampleSH(normalize(i.n))+sun.color*d*lerp(.45,1,sun.shadowAttenuation));return half4(lit,1);
            }
            ENDHLSL
        }
    }
}
