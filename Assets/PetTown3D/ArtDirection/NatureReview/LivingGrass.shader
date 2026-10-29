Shader "PetTown/Review/LivingGrass"
{
 Properties {_RootColor("Root",Color)=(.075,.22,.08,1) _TipColor("Tip",Color)=(.28,.50,.14,1) _Height("Source height",Float)=.33 _Wind("Wind",Float)=.08 _FadeStart("Fade start",Float)=0 _FadeEnd("Fade end",Float)=0}
 SubShader {Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"} Cull Off
 Pass {Tags {"LightMode"="UniversalForward"}
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_instancing
 #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
 #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
 #pragma multi_compile_fog
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
 CBUFFER_START(UnityPerMaterial)
 float4 _RootColor,_TipColor;float _Height,_Wind,_FadeStart,_FadeEnd;
 CBUFFER_END
 float4 _PetTownGrassFocus;
 struct A{float4 p:POSITION;float3 n:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID};
 struct V{float4 p:SV_POSITION;float3 w:TEXCOORD0;float h:TEXCOORD1;float fog:TEXCOORD2;float variation:TEXCOORD3;UNITY_VERTEX_INPUT_INSTANCE_ID};
 V vert(A a){V o;UNITY_SETUP_INSTANCE_ID(a);UNITY_TRANSFER_INSTANCE_ID(a,o);float3 pivot=TransformObjectToWorld(float3(0,0,0));float h=saturate(a.p.y/max(.1,_Height));float3 p=a.p.xyz;float visibility=1;if(_FadeEnd>_FadeStart)visibility=1-smoothstep(_FadeStart,_FadeEnd,length(pivot.xz-_PetTownGrassFocus.xz));p*=visibility;float wave=sin(_Time.y*1.3+pivot.x*.55+pivot.z*.38)+.35*sin(_Time.y*.7+pivot.z*.8);p.xz+=float2(wave,wave*.37)*_Wind*h*h*visibility;o.w=TransformObjectToWorld(p);o.p=TransformWorldToHClip(o.w);o.h=h;o.fog=ComputeFogFactor(o.p.z);o.variation=.90+.20*frac(sin(dot(pivot.xz,float2(13.13,37.17)))*4375.58);return o;}
 half4 frag(V i):SV_Target{UNITY_SETUP_INSTANCE_ID(i);Light sun=GetMainLight(TransformWorldToShadowCoord(i.w));half3 col=lerp(_RootColor.rgb,_TipColor.rgb,smoothstep(0,1,i.h))*i.variation;half3 illumination=SampleSH(float3(0,1,0))+sun.color*(.5+.5*saturate(sun.direction.y))*lerp(.20,1,sun.shadowAttenuation);return half4(MixFog(col*illumination,i.fog),1);}
 ENDHLSL
 }
 }
}