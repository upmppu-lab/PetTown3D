Shader "PetTown/WorldDistanceFog"
{
 Properties { _FogColor("Fog",Color)=(.78,.84,.79,1) }
 SubShader
 {
  Tags {"Queue"="Overlay" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline"}
  Pass
  {
   Tags {"LightMode"="SRPDefaultUnlit"}
   Cull Off ZWrite Off ZTest Always Blend SrcAlpha OneMinusSrcAlpha
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   CBUFFER_START(UnityPerMaterial)
   float4 _FogColor,_FogCenter,_FogRange,_RiverLine,_WorldExtent,_ExploredBounds;
   float4 _Origin0,_Origin1,_Origin2,_Origin3,_Direction0,_Direction1,_Direction2,_Direction3;
   CBUFFER_END
   TEXTURE2D(_ExploredMap);SAMPLER(sampler_ExploredMap);
   struct A {float4 vertex:POSITION;float2 uv:TEXCOORD0;};
   struct V {float4 position:SV_POSITION;float2 uv:TEXCOORD0;};
   V vert(A v){V o;o.position=TransformObjectToHClip(v.vertex.xyz);o.uv=v.uv;return o;}
   half4 frag(V i):SV_Target
   {
    float3 origin=lerp(lerp(_Origin0.xyz,_Origin1.xyz,i.uv.x),lerp(_Origin3.xyz,_Origin2.xyz,i.uv.x),i.uv.y);
    float3 direction=lerp(lerp(_Direction0.xyz,_Direction1.xyz,i.uv.x),lerp(_Direction3.xyz,_Direction2.xyz,i.uv.x),i.uv.y);
    if(direction.y>=-.001)return half4(_FogColor.rgb,1);
    float3 ground=origin+direction*max(0,-origin.y/direction.y);
    float radial=smoothstep(_FogRange.x,_FogRange.y,length(ground.xz-_FogCenter.xz));
    float across=-(ground.x-(_RiverLine.x+_RiverLine.y*(ground.z-_RiverLine.z)))*_RiverLine.w;
    float bank=.55*smoothstep(_FogRange.z,_FogRange.w,across);
    float border=1-smoothstep(0,_WorldExtent.y,_WorldExtent.x-max(abs(ground.x),abs(ground.z)));
    float2 exploredUv=(ground.xz-_ExploredBounds.xy)/max(_ExploredBounds.zw,float2(1,1));
    float inside=step(0,exploredUv.x)*step(0,exploredUv.y)*step(exploredUv.x,1)*step(exploredUv.y,1);
    float discovered=SAMPLE_TEXTURE2D(_ExploredMap,sampler_ExploredMap,exploredUv).r*inside;
    float unknown=max(.30,max(radial,bank))*(1-discovered);
    float atmosphere=.10*smoothstep(100,200,length(ground.xz-_FogCenter.xz));
    float fog=max(border,max(unknown,atmosphere));
    float variation=frac(dot(floor(ground.xz*.15),float2(.754877,.569840)));
    fog=saturate(fog+(variation-.5)*.08*fog*(1-fog));
    return half4(_FogColor.rgb,fog);
   }
   ENDHLSL
  }
 }
}
