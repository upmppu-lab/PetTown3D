Shader "PetTown/Review/PaintedSky"
{
 Properties {_Zenith("Zenith",Color)=(.27,.61,.79,1) _Horizon("Horizon",Color)=(.74,.87,.88,1)}
 SubShader {Tags {"Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox"} Cull Off ZWrite Off
 Pass {HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 float4 _Zenith,_Horizon;
 struct A{float4 p:POSITION;};struct V{float4 p:SV_POSITION;float3 d:TEXCOORD0;};
 V vert(A a){V o;o.p=TransformObjectToHClip(a.p.xyz);o.d=a.p.xyz;return o;}
 float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
 float noise(float2 p){float2 q=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(q),hash(q+float2(1,0)),f.x),lerp(hash(q+float2(0,1)),hash(q+1),f.x),f.y);}
 half4 frag(V i):SV_Target{float3 d=normalize(i.d);float y=max(d.y,0);half3 c=lerp(_Horizon.rgb,_Zenith.rgb,pow(y,.43));float2 uv=d.xz/(y+.14);uv.x*=.7;uv*=4.8;float n=noise(uv)*.62+noise(uv*2.1)*.25+noise(uv*5)*.13;float clouds=smoothstep(.61,.68,n)*smoothstep(.025,.13,y)*smoothstep(.88,.55,y);c=lerp(c,lerp(half3(.78,.87,.89),half3(1,1,.97),n),clouds);return half4(c,1);}
 ENDHLSL}
 }
}