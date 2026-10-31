Shader "PetTown/HairForeground"
{
 Properties { _MainTex ("Hair Texture", 2D) = "white" {} _Color ("Tint", Color) = (1,1,1,1) _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.35 }
 SubShader
 {
  Tags { "Queue"="Transparent+5" "RenderType"="TransparentCutout" "IgnoreProjector"="True" }
  Pass
  {
   Cull Back
   ZWrite Off
   ZTest Always
   Blend SrcAlpha OneMinusSrcAlpha
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
   struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };
   sampler2D _MainTex; float4 _MainTex_ST; fixed4 _Color; float _Cutoff;
   v2f vert(appdata v) { v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.uv=TRANSFORM_TEX(v.uv,_MainTex); return o; }
   fixed4 frag(v2f i):SV_Target { fixed4 c=tex2D(_MainTex,i.uv)*_Color; clip(c.a-_Cutoff); return c; }
   ENDCG
  }
 }
 Fallback "Unlit/Transparent"
}
