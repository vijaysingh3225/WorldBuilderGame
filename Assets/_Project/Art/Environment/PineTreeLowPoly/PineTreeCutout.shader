Shader "WorldBuilder/Pine Tree Cutout"
{
    Properties
    {
        _BaseMap ("Base Color", 2D) = "white" {}
        _OpacityMap ("Opacity", 2D) = "white" {}
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.34
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "AlphaTest"
            "RenderType" = "TransparentCutout"
        }

        Cull Off
        ZWrite On

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float fogFactor : TEXCOORD2;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_OpacityMap);
            SAMPLER(sampler_OpacityMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _OpacityMap_ST;
                float4 _Tint;
                float _Cutoff;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs =
                    GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.normalWS =
                    TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor =
                    ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 baseColor =
                    SAMPLE_TEXTURE2D(
                        _BaseMap,
                        sampler_BaseMap,
                        input.uv);
                half opacity =
                    SAMPLE_TEXTURE2D(
                        _OpacityMap,
                        sampler_OpacityMap,
                        input.uv).r;
                clip(opacity - _Cutoff);

                Light mainLight =
                    GetMainLight();
                half diffuse =
                    saturate(
                        dot(
                            normalize(input.normalWS),
                            mainLight.direction));
                half lighting = 0.42h + diffuse * 0.58h;
                half3 color =
                    baseColor.rgb *
                    _Tint.rgb *
                    lighting *
                    mainLight.color;
                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_OpacityMap);
            SAMPLER(sampler_OpacityMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _OpacityMap_ST;
                float4 _Tint;
                float _Cutoff;
            CBUFFER_END

            Varyings ShadowVert(Attributes input)
            {
                Varyings output;
                output.positionCS =
                    TransformObjectToHClip(input.positionOS.xyz);
                output.uv =
                    input.uv *
                    _OpacityMap_ST.xy +
                    _OpacityMap_ST.zw;
                return output;
            }

            half4 ShadowFrag(Varyings input) : SV_Target
            {
                half opacity =
                    SAMPLE_TEXTURE2D(
                        _OpacityMap,
                        sampler_OpacityMap,
                        input.uv).r;
                clip(opacity - _Cutoff);
                return 0;
            }
            ENDHLSL
        }
    }
}
