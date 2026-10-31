Shader "PetTown/Cozy/Stream"
{
    Properties { _Bank("Bank surface",Float)=0 _FlowSpeed("Flow phase per second",Float)=.025 _MeadowCoverage("Meadow coverage",2D)="white"{} _CoverageBounds("World bounds",Vector)=(-110,-110,220,220) _UseCoverage("Use coverage",Float)=0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-10" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Offset -1,-1
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float _Bank,_FlowSpeed,_UseCoverage;float4 _CoverageBounds;
            CBUFFER_END
            TEXTURE2D(_MeadowCoverage);SAMPLER(sampler_MeadowCoverage);
            struct A {float4 p:POSITION;float3 n:NORMAL;float2 uv:TEXCOORD0;half4 c:COLOR;};
            struct V {float4 p:SV_POSITION;float3 w:TEXCOORD0;float3 n:TEXCOORD1;float2 uv:TEXCOORD2;half depth:TEXCOORD3;};
            V vert(A a){V o;o.w=TransformObjectToWorld(a.p.xyz);o.p=TransformWorldToHClip(o.w);o.n=TransformObjectToWorldNormal(a.n);o.uv=a.uv;o.depth=a.c.r;return o;}
            float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}
            half4 frag(V i):SV_Target
            {
                Light sun=GetMainLight(TransformWorldToShadowCoord(i.w));half3 normal=normalize(i.n);
                half3 lit=max(SampleSH(normal)+sun.color*saturate(dot(normal,sun.direction))*lerp(.5,1,sun.shadowAttenuation),half3(.4,.4,.4));
                if(_Bank>.5)
                {
                    float2 p=i.w.xz;float center=sin(p.y*.031)*12+sin(p.y*.09)*3;
                    float slope=.372*cos(p.y*.031)+.27*cos(p.y*.09);
                    float distance=abs(p.x-center)/sqrt(1+slope*slope);
                    float n=noise(p*.18)*.72+noise(p*1.4)*.28;
                    half4 cover=SAMPLE_TEXTURE2D(_MeadowCoverage,sampler_MeadowCoverage,(p-_CoverageBounds.xy)/_CoverageBounds.zw);
                    half3 grass=lerp(half3(.30,.42,.21),half3(.38,.47,.26),lerp(n,cover.a,_UseCoverage));
                    // A narrow irregular sandy shore, then meadow over the existing smooth slope.
                    float bankVariation=(noise(p*.42)-.5)*.85+(noise(p*1.7)-.5)*.25;
                    float shore=smoothstep(5.3,6.9,distance+bankVariation);
                    half3 sand=lerp(half3(.48,.41,.29),half3(.66,.58,.41),noise(p*2));
                    grass=lerp(grass,half3(.46,.33,.21),cover.r*_UseCoverage*.65);
                    float2 cell=floor(p*4),local=frac(p*4)-.5;float seed=hash(cell);
                    float pebble=1-smoothstep(.13,.18,length(local-float2(hash(cell+3),hash(cell+7))*.28+.14));
                    sand=lerp(sand,lerp(half3(.42,.46,.43),half3(.68,.69,.58),seed),pebble*step(.65,seed)*.6);
                    sand*=lerp(.70,1,smoothstep(4.7,5.9,distance));
                    float alpha=smoothstep(3.95,4.22,distance)*(1-smoothstep(8.92,9.08,distance));
                    return half4(lerp(sand,grass,shore)*lit,alpha);
                }
                float edge=min(i.uv.x,1-i.uv.x),depth=saturate(min(i.depth,edge*3.8));
                half3 water=lerp(half3(.34,.67,.58),half3(.065,.43,.47),smoothstep(0,.75,depth));
                // Arc-length UVs remain unchanged. Ripples travel down the winding centerline.
                float phase=i.uv.y-_Time.y*_FlowSpeed;
                float ripple=sin(phase*18+sin(i.uv.x*38+phase*3)*.34);
                float fine=sin(phase*41+i.uv.x*65);
                water+=ripple*.006+fine*.003;
                water+=pow(saturate(sin(phase*32+i.uv.x*42)),32)*.016;
                water+=pow(saturate(sin(phase*54+i.uv.x*71)),40)*.010*(1-depth);
                return half4(water*lerp(half3(1,1,1),lit,.25),smoothstep(0,.008,edge));
            }
            ENDHLSL
        }
    }
}
