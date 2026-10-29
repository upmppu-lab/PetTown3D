Shader "PetTown/UI/WindowColors"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _SurfaceMode ("Paper 0 / Header 1", Float) = 0
        _HeaderCutout ("Header cutout", Vector) = (0,0,0,0)
        _CutoutEnabled ("Header opening", Float) = 0
        _HeaderTint ("Header RGB", Vector) = (0.02,0.025,0.03,1)
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct appdata { float4 vertex:POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex:SV_POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; float4 worldPosition:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            sampler2D _MainTex;
            fixed4 _Color, _TextureSampleAdd;
            float4 _ClipRect, _HeaderTint, _HeaderCutout;
            float _SurfaceMode, _CutoutEnabled;
            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.worldPosition=v.vertex;
                o.vertex=UnityObjectToClipPos(v.vertex);
                o.uv=v.uv;
                o.color=v.color*_Color;
                return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                fixed4 source=tex2D(_MainTex,i.uv)+_TextureSampleAdd;
                // Keep the original ivory artwork and its colored frame.
                fixed3 whitePaper=source.rgb;
                float light=dot(source.rgb,float3(.2126,.7152,.0722));
                fixed3 grayHeader=_HeaderTint.rgb*(.85+.30*light);
                fixed4 color=fixed4(lerp(whitePaper,grayHeader,step(.5,_SurfaceMode)),source.a)*i.color;
                // The title bar must reveal the scene, rather than opaque paper underneath it.
                float2 halfSize=max(0,(_HeaderCutout.zw-_HeaderCutout.xy)*.5);
                float2 d=abs(i.worldPosition.xy-(_HeaderCutout.xy+_HeaderCutout.zw)*.5)-halfSize+8;
                float hole=1-step(0,length(max(d,0))+min(max(d.x,d.y),0)-8);
                color.a*=1-hole*_CutoutEnabled;
                #ifdef UNITY_UI_CLIP_RECT
                color.a*=UnityGet2DClipping(i.worldPosition.xy,_ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a-.001);
                #endif
                return color;
            }
            ENDCG
        }
    }
}
