Shader "PetTown/RoadSurface"
{
    Properties
    {
        _MainTex("Existing dirt texture", 2D) = "white" {}
        _Tint("Tint", Color) = (1,1,1,1)
        _Alt("Second colour", Color) = (.5,.5,.5,1)
        _Pattern("Pattern", Float) = 0
        _Cell("Cell size", Float) = .6
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-20" "RenderType"="Transparent" }
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
            half4 _Tint; half4 _Alt; float _Pattern; float _Cell;
            struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct V { float4 positionCS:SV_POSITION; float3 normalWS:TEXCOORD0; float2 uv:TEXCOORD1; half4 color:COLOR; float3 posWS:TEXCOORD2; };
            V vert(A i) { V o; o.positionCS=TransformObjectToHClip(i.positionOS.xyz); o.normalWS=TransformObjectToWorldNormal(i.normalOS); o.uv=i.uv; o.color=i.color; o.posWS=TransformObjectToWorld(i.positionOS.xyz); return o; }
            float h21(float2 p) { p=frac(p*float2(123.34,456.21)); p+=dot(p,p+45.32); return frac(p.x*p.y); }
            half4 frag(V i):SV_Target
            {
                // Mirror a central part of the existing ribbon texture: its baked
                // transparent side borders must not repeat as stripes across a junction.
                float u=.35+.3*(1-abs(frac(i.uv.x)*2-1));
                half3 dirt=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,float2(u,i.uv.y)).rgb*half3(1,.97,.86);
                float2 w=i.posWS.xz;
                half3 col=dirt*_Tint.rgb;
                int pat=(int)(_Pattern+.5);
                if(pat==1)
                {
                    float n=h21(floor(w*9)); float n2=h21(floor(w*3.1)+7);
                    col=_Tint.rgb*(.72+.42*n)*(.92+.16*n2);
                }
                else if(pat==2)
                {
                    float2 c=w/_Cell; float2 f=frac(c); float2 id=floor(c);
                    float ld=min(min(f.x,1-f.x),min(f.y,1-f.y));
                    float stone=smoothstep(.02,.07,ld);
                    col=lerp(_Alt.rgb,_Tint.rgb*(.86+.22*h21(id)),stone);
                }
                else if(pat==3)
                {
                    float2 c=float2(w.x/_Cell,w.y/(_Cell*.5)); c.x+=.5*fmod(floor(c.y),2); float2 f=frac(c); float2 id=floor(c);
                    float lx=min(f.x,1-f.x)*.9; float ly=min(f.y,1-f.y)*.5;
                    float stone=smoothstep(.015,.05,min(lx,ly));
                    col=lerp(_Alt.rgb,_Tint.rgb*(.8+.3*h21(id)),stone);
                }
                else if(pat==4)
                {
                    float p=frac(i.uv.x)*8; float id=floor(p); float f=frac(p);
                    float joint=floor(i.uv.y*2.2+h21(float2(id,3.7))*5); float tone=.8+.3*h21(float2(id,joint));
                    float gap=smoothstep(.0,.07,min(f,1-f));
                    col=lerp(_Alt.rgb,_Tint.rgb*tone,gap);
                }
                else if(pat==5)
                {
                    float n=h21(floor(w*22));
                    col=_Tint.rgb*(.9+.2*n);
                    float cx=abs(frac(i.uv.x)-.5);
                    float dash=step(cx,.018)*step(.5,frac(i.uv.y*3));
                    float edge=step(.43,cx)*step(cx,.455);
                    col=lerp(col,_Alt.rgb,max(dash,edge*.8));
                }
                else if(pat==6)
                {
                    float2 c=w/_Cell; float2 f=frac(c); float2 id=floor(c);
                    float vein=sin((w.x+w.y*.7)*3.2+h21(id)*6.2)*.5+.5;
                    float ld=min(min(f.x,1-f.x),min(f.y,1-f.y));
                    col=lerp(_Tint.rgb,_Alt.rgb,vein*.22);
                    col=lerp(_Alt.rgb*.8,col,smoothstep(.01,.035,ld));
                }
                else if(pat==7)
                {
                    float2 c=w/_Cell; float2 id=floor(c);
                    float2 centre=id+.5+(float2(h21(id+3.1),h21(id+8.7))-.5)*.3;
                    float d=length(c-centre);
                    float stone=smoothstep(.42,.34,d);
                    col=lerp(dirt*_Tint.rgb,_Alt.rgb*(.85+.25*h21(id)),stone);
                }
                Light light=GetMainLight();
                half3 lighting=max(SampleSH(normalize(i.normalWS))+light.color*saturate(dot(normalize(i.normalWS),light.direction)),half3(.35,.35,.35));
                return half4(col*lighting,i.color.a);
            }
            ENDHLSL
        }
    }
}
