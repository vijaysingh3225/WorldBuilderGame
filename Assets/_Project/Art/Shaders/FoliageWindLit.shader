Shader "WorldBuilder/Foliage Wind Lit"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.35
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 0
        _WindStrength("Canopy Sway", Range(0, 0.5)) = 0.28
        _WindSpeed("Wind Speed", Range(0, 4)) = 0.82
        _RustleStrength("Leaf Rustle", Range(0, 0.12)) = 0.042
        _RustleSpeed("Rustle Speed", Range(0, 12)) = 4.8
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Cutoff;
                half _WindStrength;
                half _WindSpeed;
                half _RustleStrength;
                half _RustleSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 positionWS =
                    TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS =
                    TransformObjectToWorldNormal(input.normalOS);
                float3 objectOriginWS =
                    TransformObjectToWorld(float3(0.0, 0.0, 0.0));
                float heightAboveRoot =
                    max(0.0, positionWS.y - objectOriginWS.y);
                float heightWeight =
                    saturate(0.30 + heightAboveRoot * 0.075);
                heightWeight =
                    heightWeight * heightWeight *
                    (3.0 - 2.0 * heightWeight);

                float treePhase =
                    dot(
                        objectOriginWS.xz,
                        float2(0.071, 0.053));
                float windTime =
                    _Time.y * _WindSpeed;
                float primarySway =
                    sin(windTime + treePhase) +
                    sin(windTime * 0.47 + treePhase * 1.73) *
                    0.38;
                float crossSway =
                    cos(windTime * 0.81 + treePhase * 1.31);
                float gust =
                    0.72 +
                    sin(windTime * 0.19 + treePhase) * 0.28;
                positionWS.xz +=
                    float2(primarySway, crossSway * 0.58) *
                    (_WindStrength * gust * heightWeight);

                float rustle =
                    sin(
                        _Time.y * _RustleSpeed +
                        dot(positionWS.xz, float2(2.17, 1.63)) +
                        positionWS.y * 1.31 +
                        treePhase * 2.0);
                positionWS +=
                    normalWS *
                    (rustle * _RustleStrength *
                     lerp(0.35, 1.0, heightWeight));

                output.positionWS = positionWS;
                output.positionHCS =
                    TransformWorldToHClip(positionWS);
                output.normalWS = normalWS;
                output.uv =
                    TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor =
                    ComputeFogFactor(output.positionHCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half4 surface =
                    SAMPLE_TEXTURE2D(
                        _BaseMap,
                        sampler_BaseMap,
                        input.uv) *
                    _BaseColor;
                #if defined(_ALPHATEST_ON)
                    clip(surface.a - _Cutoff);
                #endif

                half3 normalWS = normalize(input.normalWS);
                float4 shadowCoord =
                    TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half normalToLight =
                    dot(normalWS, mainLight.direction);
                half wrappedDiffuse =
                    saturate(
                        (normalToLight + 0.22h) /
                        1.22h);
                half direct =
                    wrappedDiffuse *
                    mainLight.distanceAttenuation *
                    mainLight.shadowAttenuation;
                half3 viewDirection =
                    GetWorldSpaceNormalizeViewDir(
                        input.positionWS);
                half fresnel =
                    pow(
                        1.0h -
                        saturate(
                            dot(normalWS, viewDirection)),
                        4.0h) *
                    0.035h;
                half3 ambientLighting =
                    SampleSH(normalWS) * 0.88h;
                half3 lighting =
                    ambientLighting +
                    mainLight.color * direct * 1.08h;
                surface.rgb *=
                    max(
                        lighting,
                        half3(0.18h, 0.20h, 0.22h));
                surface.rgb +=
                    ambientLighting * fresnel;
                surface.rgb =
                    MixFog(surface.rgb, input.fogFactor);
                return surface;
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
