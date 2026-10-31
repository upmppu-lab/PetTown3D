Shader "PetTown/Environment/AssetFoliage"
{
    Properties
    {
        [MainTexture] _BaseMap("Original asset texture",2D)="white" {}
        [MainColor] _BaseColor("Tint",Color)=(1,1,1,1)
        _Cutoff("Cutout",Range(0,1))=.25
        _AlphaClip("Clip",Float)=1
        _WindStrength("Wind",Float)=.1
        _VertexAO("Original vertex AO",Range(0,1))=.5
        _UpNormals("Grass normals",Range(0,1))=0
        _MeadowGrass("Meadow palette",Float)=0
        _FadeStart("Detail fade start",Float)=0
        _FadeEnd("Detail fade end",Float)=0
        _Cull("Cull",Float)=0
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest"}
        Cull Off
        Pass
        {
            Tags {"LightMode"="UniversalForward"}
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor,_BaseMap_ST;float _Cutoff,_AlphaClip,_WindStrength,_VertexAO,_UpNormals,_MeadowGrass,_FadeStart,_FadeEnd;
            CBUFFER_END
            float4 _PetTownGrassFocus;
            struct A {float4 p:POSITION;float3 n:NORMAL;float2 uv:TEXCOORD0;half4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V {float4 p:SV_POSITION;float3 w:TEXCOORD0;float3 n:TEXCOORD1;float2 uv:TEXCOORD2;half ao:TEXCOORD3;half tint:TEXCOORD4;half blade:TEXCOORD5;UNITY_VERTEX_INPUT_INSTANCE_ID};
            V vert(A a)
            {
                V o;UNITY_SETUP_INSTANCE_ID(a);UNITY_TRANSFER_INSTANCE_ID(a,o);
                float3 pivot=TransformObjectToWorld(float3(0,0,0));float3 p=a.p.xyz;
                if(_FadeEnd>0){float detail=1-smoothstep(_FadeStart,_FadeEnd,length(pivot.xz-_PetTownGrassFocus.xz));p*=detail;}
                float wind=sin(_Time.y*1.7+pivot.x*.23+pivot.z*.31)+sin(_Time.y*.77+pivot.z*.43)*.35;
                p.xz+=float2(wind,wind*.37)*_WindStrength*saturate(p.y)*saturate(p.y);
                o.w=TransformObjectToWorld(p);o.p=TransformWorldToHClip(o.w);o.n=normalize(lerp(TransformObjectToWorldNormal(a.n),float3(0,1,0),_UpNormals));o.uv=TRANSFORM_TEX(a.uv,_BaseMap);
                o.blade=saturate(a.color.r);o.ao=lerp(1,lerp(.42,1,o.blade),_VertexAO);o.tint=.94+frac(sin(dot(pivot.xz,float2(17.17,41.13)))*4375.58)*.12;return o;
            }
            half4 frag(V i):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);half4 c=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv)*_BaseColor;clip(c.a-_Cutoff);
                half3 meadow=lerp(half3(.15,.34,.12),half3(.36,.56,.24),i.blade);
                c.rgb=lerp(c.rgb,meadow,_MeadowGrass*.78);
                Light sun=GetMainLight(TransformWorldToShadowCoord(i.w));half3 n=normalize(i.n);half d=saturate(dot(n,sun.direction)*.65+.35);
                half3 lit=SampleSH(n)+sun.color*d*lerp(.25,1,sun.shadowAttenuation);
                return half4(c.rgb*max(lit,half3(.3,.3,.3))*i.ao*i.tint,1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
