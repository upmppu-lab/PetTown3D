Shader "PetTown/GroundNatural"
{
    Properties
    {
        _TexDirt("Dirt", 2D) = "white" {}
        _TexSparse("Sparse Grass", 2D) = "white" {}
        _TexDense("Dense Grass", 2D) = "white" {}
        _TexHeather("Heather", 2D) = "white" {}
        _Tint("Tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_TexDirt); SAMPLER(sampler_TexDirt);
            TEXTURE2D(_TexSparse); SAMPLER(sampler_TexSparse);
            TEXTURE2D(_TexDense); SAMPLER(sampler_TexDense);
            TEXTURE2D(_TexHeather); SAMPLER(sampler_TexHeather);
            float4 _TexDirt_ST;
            half4 _Tint;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                float3 normalWS : TEXCOORD1;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = posInputs.positionCS;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _TexDirt);
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 cDirt = SAMPLE_TEXTURE2D(_TexDirt, sampler_TexDirt, IN.uv);
                half4 cSparse = SAMPLE_TEXTURE2D(_TexSparse, sampler_TexSparse, IN.uv);
                half4 cDense = SAMPLE_TEXTURE2D(_TexDense, sampler_TexDense, IN.uv);
                half4 cHeather = SAMPLE_TEXTURE2D(_TexHeather, sampler_TexHeather, IN.uv);
                half4 albedo = cDirt*IN.color.r + cSparse*IN.color.g + cDense*IN.color.b + cHeather*IN.color.a;
                // Keep the original four texture layers, reduce photographic contrast.
                half dirtL=dot(cDirt.rgb,half3(.2126,.7152,.0722));
                half sparseL=dot(cSparse.rgb,half3(.2126,.7152,.0722));
                half denseL=dot(cDense.rgb,half3(.2126,.7152,.0722));
                half heatherL=dot(cHeather.rgb,half3(.2126,.7152,.0722));
                cDirt.rgb=lerp(half3(.48,.38,.24),cDirt.rgb,.38)*lerp(.88,1.12,dirtL);
                cSparse.rgb=lerp(half3(.22,.37,.13),cSparse.rgb,.38)*lerp(.88,1.12,sparseL);
                cDense.rgb=lerp(half3(.18,.32,.11),cDense.rgb,.38)*lerp(.88,1.12,denseL);
                cHeather.rgb=lerp(half3(.36,.40,.19),cHeather.rgb,.4)*lerp(.88,1.12,heatherL);
                albedo=cDirt*IN.color.r+cSparse*IN.color.g+cDense*IN.color.b+cHeather*IN.color.a;
                albedo *= _Tint;

                Light mainLight = GetMainLight();
                half3 normalWS = normalize(IN.normalWS);
                half NdotL = saturate(dot(normalWS, mainLight.direction));
                half3 lighting = mainLight.color * NdotL + SampleSH(normalWS);
                half3 color = albedo.rgb * max(lighting, half3(0.35,0.35,0.35));
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
