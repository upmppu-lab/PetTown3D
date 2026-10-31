Shader "PetTown/WeatherRain"
{
 SubShader
 {
  Tags {"RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline"}
  Pass
  {
   Tags {"LightMode"="SRPDefaultUnlit"}
   Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   struct A{float4 position:POSITION;half4 color:COLOR;float2 uv:TEXCOORD0;};
   struct V{float4 position:SV_POSITION;half4 color:COLOR;float2 uv:TEXCOORD0;};
   V vert(A a){V o;o.position=TransformObjectToHClip(a.position.xyz);o.color=a.color;o.uv=a.uv;return o;}
   half4 frag(V i):SV_Target{half alpha=(1-smoothstep(.05,.5,abs(i.uv.x-.5)))*smoothstep(0,.12,i.uv.y)*(1-smoothstep(.8,1,i.uv.y));return half4(i.color.rgb,i.color.a*alpha);}
   ENDHLSL
  }
 }
}
