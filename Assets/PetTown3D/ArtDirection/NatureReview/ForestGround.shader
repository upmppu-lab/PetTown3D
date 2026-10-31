Shader "PetTown/Review/ForestGround"
{
 Properties { _GrassShade("Grass shade",Vector)=(.28,.39,.18,1) _GrassLight("Grass light",Vector)=(.38,.47,.26,1) _Grass("Grass",2D)="white"{} _Soil("Soil",2D)="white"{} _PaintStrength("Paint strength",Range(0,1))=1 _Coverage("Meadow coverage",2D)="white"{} _CoverageBounds("World bounds",Vector)=(-110,-110,220,220) _UseCoverage("Use coverage",Float)=0 }
 SubShader {
 Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
 Pass {
 Tags { "LightMode"="UniversalForward" }
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
 #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
 #pragma multi_compile_fog
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
 CBUFFER_START(UnityPerMaterial)
 float _PaintStrength,_UseCoverage;float4 _CoverageBounds,_GrassShade,_GrassLight;
 CBUFFER_END
 TEXTURE2D(_Coverage);SAMPLER(sampler_Coverage);
 TEXTURE2D(_Grass); SAMPLER(sampler_Grass); TEXTURE2D(_Soil); SAMPLER(sampler_Soil);
 struct A { float4 p:POSITION; float3 n:NORMAL; float4 c:COLOR; };
 struct V { float4 p:SV_POSITION; float3 w:TEXCOORD0; float3 n:TEXCOORD1; float4 c:TEXCOORD2; float fog:TEXCOORD3; };
 float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
 float noise(float2 p){float2 q=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(q),hash(q+float2(1,0)),f.x),lerp(hash(q+float2(0,1)),hash(q+1),f.x),f.y);}
 V vert(A a){V o;o.w=TransformObjectToWorld(a.p.xyz);o.p=TransformWorldToHClip(o.w);o.n=TransformObjectToWorldNormal(a.n);o.c=a.c;o.fog=ComputeFogFactor(o.p.z);return o;}
 half4 frag(V i):SV_Target {
 float n=noise(i.w.xz*.48)*.6+noise(i.w.xz*2.8)*.4;
 float edge=smoothstep(.17,.81,i.c.r*_PaintStrength+(n-.5)*.19);
 half3 grass=SAMPLE_TEXTURE2D(_Grass,sampler_Grass,i.w.xz*.22).rgb;
 grass=lerp(grass,half3(.24,.40,.12),.42)*( .94+noise(i.w.xz*.11)*.12);
 float2 maskUv=(i.w.xz-_CoverageBounds.xy)/_CoverageBounds.zw;maskUv=1-abs(frac(maskUv*.5)*2-1);half4 coverage=SAMPLE_TEXTURE2D(_Coverage,sampler_Coverage,maskUv);
 grass=lerp(grass,lerp(_GrassShade.rgb,_GrassLight.rgb,coverage.a),_UseCoverage*.74);edge=max(edge,coverage.r*_UseCoverage);
 half3 soil=SAMPLE_TEXTURE2D(_Soil,sampler_Soil,i.w.xz*.32).rgb;
 soil=lerp(soil,half3(.46,.33,.21),.68);
 soil*=.88+noise(i.w.xz*.8)*.20;float pebble=pow(noise(i.w.xz*9),13);soil+=pebble*.12;half3 color=lerp(grass,soil,edge);
 Light sun=GetMainLight(TransformWorldToShadowCoord(i.w));
 half3 normal=normalize(i.n);half3 illumination=SampleSH(normal)+sun.color*saturate(dot(normal,sun.direction))*lerp(.33,1,sun.shadowAttenuation);
 return half4(MixFog(color*illumination,i.fog),1);
 }
 ENDHLSL
 }
 UsePass "Universal Render Pipeline/Lit/ShadowCaster"
 }
}