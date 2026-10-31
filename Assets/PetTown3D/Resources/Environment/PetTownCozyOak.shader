Shader "PetTown/Cozy/NaturalOakBoards"
{
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
        Pass
        {
            Tags {"LightMode"="UniversalForward"}
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            struct A {float4 p:POSITION;float3 n:NORMAL;float2 uv:TEXCOORD0;float4 c:COLOR;};
            struct V {float4 p:SV_POSITION;float3 w:TEXCOORD0;float3 n:TEXCOORD1;float2 uv:TEXCOORD2;float4 c:TEXCOORD3;};
            float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}
            V vert(A a){V o;o.w=TransformObjectToWorld(a.p.xyz);o.p=TransformWorldToHClip(o.w);o.n=TransformObjectToWorldNormal(a.n);o.uv=a.uv;o.c=a.c;return o;}
            half4 frag(V i):SV_Target
            {
                float2 p=i.uv;float seed=i.c.a;
                float bend=noise(float2(p.x*.34,p.y*3.6)+seed*19)*3;
                float grain=sin(p.y*147+bend+sin(p.x*.7+seed*12)*1.3)*.018;
                grain+=noise(float2(p.x*.9,p.y*79)+seed*17)*.035-.017;
                float2 knot=float2(frac(p.x*.31+seed*3.7)-.5,frac(p.y*2.55+seed*9.2)-.5)*float2(2,1);
                float k=length(knot);float rings=sin(k*88)*.014*(1-smoothstep(.08,.23,k))*step(.55,seed);
                half3 wood=i.c.rgb+grain-rings;wood*=1-(1-smoothstep(.01,.05,k))*.15*step(.55,seed);
                Light sun=GetMainLight(TransformWorldToShadowCoord(i.w));half3 n=normalize(i.n);
                half3 light=SampleSH(n)+sun.color*saturate(dot(n,sun.direction))*lerp(.15,1,sun.shadowAttenuation);
                return half4(wood*max(light,half3(.28,.28,.28)),1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
