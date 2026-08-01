Shader "WorldBuilder/Terrain Road Blend Lit"
{
    Properties
    {
        [MainTexture] _GroundMap("Ground Map", 2D) = "white" {}
        _RoadMap("Road Map", 2D) = "white" {}
        [MainColor] _GroundColor("Ground Color", Color) = (1, 1, 1, 1)
        _RoadColor("Road Color", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_GroundMap);
            SAMPLER(sampler_GroundMap);
            TEXTURE2D(_RoadMap);
            SAMPLER(sampler_RoadMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _GroundMap_ST;
                float4 _RoadMap_ST;
                half4 _GroundColor;
                half4 _RoadColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 groundUv : TEXCOORD2;
                float2 roadUv : TEXCOORD3;
                half4 color : COLOR;
                half fogFactor : TEXCOORD4;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs =
                    GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs =
                    GetVertexNormalInputs(input.normalOS);
                output.positionHCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.groundUv = TRANSFORM_TEX(input.uv, _GroundMap);
                output.roadUv = TRANSFORM_TEX(input.uv, _RoadMap);
                output.color = input.color;
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 ground =
                    SAMPLE_TEXTURE2D(
                        _GroundMap,
                        sampler_GroundMap,
                        input.groundUv) *
                    _GroundColor;
                half4 road =
                    SAMPLE_TEXTURE2D(
                        _RoadMap,
                        sampler_RoadMap,
                        input.roadUv) *
                    _RoadColor;
                half roadBlend =
                    smoothstep(0.0h, 1.0h, input.color.a);
                half4 surface =
                    lerp(ground, road, roadBlend);
                surface.rgb *= input.color.rgb;

                half3 normalWS = normalize(input.normalWS);
                float4 shadowCoord =
                    TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half normalToLight = dot(normalWS, mainLight.direction);
                half wrappedDiffuse =
                    saturate((normalToLight + 0.16h) / 1.16h);
                half direct =
                    lerp(
                        saturate(normalToLight),
                        wrappedDiffuse,
                        0.24h) *
                    mainLight.distanceAttenuation *
                    mainLight.shadowAttenuation;
                half3 ambientLighting = SampleSH(normalWS) * 0.86h;
                half3 lighting =
                    ambientLighting +
                    mainLight.color * direct * 1.12h;
                surface.rgb *=
                    max(lighting, half3(0.18h, 0.20h, 0.22h));
                surface.rgb = MixFog(surface.rgb, input.fogFactor);
                return surface;
            }
            ENDHLSL
        }
    }
}
