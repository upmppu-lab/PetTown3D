Shader "PetTown/PlacementPreview"
{
    Properties { _BaseColor("Preview Color",Color)=(.3,.9,.5,.28) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+100" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            CBUFFER_END
            struct A { float4 positionOS:POSITION; };
            struct V { float4 positionCS:SV_POSITION; };
            V vert(A input){V output;output.positionCS=TransformObjectToHClip(input.positionOS.xyz);return output;}
            half4 frag(V input):SV_Target{return _BaseColor;}
            ENDHLSL
        }
    }
}