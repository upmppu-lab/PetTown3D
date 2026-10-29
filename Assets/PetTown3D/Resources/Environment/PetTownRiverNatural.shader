Shader "PetTown/RiverNatural"
{
    Properties
    {
        _MainTex("Existing bank soil",2D)="white" {}
        _Bank("Bank surface",Float)=0
        _DeepColor("Clear deep water",Color)=(.07,.48,.54,1)
        _ShallowColor("Shallow water",Color)=(.50,.77,.65,1)
        _FlowSpeed("Flow phase per second",Float)=.025
    }
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
            float _Bank;half4 _DeepColor,_ShallowColor;float _FlowSpeed;
            CBUFFER_END
            struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct V { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; float2 uv:TEXCOORD2; half depth:TEXCOORD3; };
            V vert(A i)
            {
                V o; o.positionWS=TransformObjectToWorld(i.positionOS.xyz); o.positionCS=TransformWorldToHClip(o.positionWS);
                o.normalWS=TransformObjectToWorldNormal(i.normalOS); o.uv=i.uv; o.depth=i.color.r; return o;
            }
            float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            half4 frag(V i):SV_Target
            {
                Light light=GetMainLight();
                half3 lighting=max(SampleSH(normalize(i.normalWS))+light.color*saturate(dot(normalize(i.normalWS),light.direction)),half3(.4,.4,.4));
                if(_Bank>.5)
                {
                    float2 p=i.positionWS.xz;
                    float z=p.y;
                    float center=sin(z*.031)*12+sin(z*.09)*3;
                    float slope=.372*cos(z*.031)+.27*cos(z*.09);
                    float bankDistance=abs(p.x-center)/sqrt(1+slope*slope);
                    float bankT=(bankDistance-3.95)/5.15;
                    float alpha=smoothstep(0,.07,bankT)*(1-smoothstep(.82,1,bankT));
                    clip(alpha-.001);
                    half3 soil=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,p*.38).rgb;
                    float2 tile=floor(p*9),local=frac(p*9)-.5;
                    float seed=hash(tile);
                    float2 pebbleCenter=float2(hash(tile+17),hash(tile+41))*.38-.19;
                    float radius=lerp(.11,.29,seed);
                    float d=length((local-pebbleCenter)*float2(1,1.35))-radius;
                    float aa=max(fwidth(d),.015);
                    float pebble=(1-smoothstep(-aa,aa,d))*step(.38,seed);
                    half3 sand=lerp(half3(.48,.40,.29),half3(.68,.59,.43),hash(floor(p*35))*.6);
                    half3 earth=lerp(soil,sand,.65);
                    half3 gravel=lerp(half3(.29,.32,.30),half3(.65,.61,.51),seed);
                    half3 color=lerp(earth,gravel,pebble*.9);
                    // Dark damp soil below the water, dry mixed soil farther out.
                    color*=lerp(.76,1.0,smoothstep(.1,.65,bankT));

                    return half4(color*lighting,alpha);
                }
                float edge=min(i.uv.x,1-i.uv.x);
                float depth=saturate(min(i.depth,edge*3.2));
                half3 color=lerp(_ShallowColor.rgb,_DeepColor.rgb,smoothstep(0,.7,depth));
                // UV.y is arc length / 5m. Translating this phase follows the
                // winding stream rather than a global world axis. No clamp seams.
                float phase=i.uv.y-_Time.y*_FlowSpeed;
                float ripple=sin(phase*6.283185+sin(i.uv.x*22)*.48);
                float fine=sin(phase*25.13274+i.uv.x*37);
                color+=(ripple*.012+fine*.006);
                color+=pow(saturate(ripple),18)*.025;
                // Only the outer ~8cm fades; the full central surface stays clear.
                float alpha=smoothstep(0,.008,edge);
                return half4(color*lerp(half3(1,1,1),lighting,.4),alpha);
            }
            ENDHLSL
        }
    }
}