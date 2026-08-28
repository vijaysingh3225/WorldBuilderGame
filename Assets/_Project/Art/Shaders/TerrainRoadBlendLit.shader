Shader "WorldBuilder/Terrain Road Blend Lit"
{
    Properties
    {
        [MainTexture] _MossyLoamMap("Mossy Loam", 2D) = "white" {}
        _CanopyDuffMap("Canopy Duff", 2D) = "white" {}
        _MossCarpetMap("Moss Carpet", 2D) = "white" {}
        _GroundcoverMap("Creeping Groundcover", 2D) = "white" {}
        _StonyLichenMap("Stony Lichen Soil", 2D) = "white" {}
        _RoadMap("Road Map", 2D) = "white" {}
        _HabitatTiling("Habitat Tiling", Float) = 6.5
        _HabitatBrightness("Habitat Brightness", Range(0.5, 2.5)) = 1.55
        _HabitatBlendContrast("Habitat Blend Contrast", Range(1, 3)) = 1.35
        _ForestGroundSaturation("Forest Ground Saturation", Range(0, 1)) = 0.86
        _LoamTint("Mossy Loam Tint", Color) = (1.08, 1.02, 0.86, 1)
        _DuffTint("Canopy Duff Tint", Color) = (1.04, 0.79, 0.58, 1)
        _MossTint("Moss Carpet Tint", Color) = (0.72, 1.34, 0.72, 1)
        _GroundcoverTint("Creeping Groundcover Tint", Color) = (0.72, 1.42, 0.68, 1)
        _StonyTint("Stony Lichen Tint", Color) = (1.24, 1.18, 1.02, 1)
        _CliffTint("Cliff Rock Tint", Color) = (0.76, 0.78, 0.72, 1)
        _CliffProjectionScale("Cliff Projection Scale", Range(0.25, 2)) = 1
        _CliffSlopeStart("Cliff Slope Start", Range(0, 1)) = 0.28
        _CliffSlopeFull("Cliff Slope Full", Range(0, 1)) = 0.58
        _CliffStrataScale("Cliff Strata Scale", Range(0.05, 1)) = 0.24
        _CliffStrataStrength("Cliff Strata Strength", Range(0, 0.35)) = 0.13
        [HideInInspector] _Cull("Cull", Float) = 2
        [HideInInspector] _HabitatDebugMode("Habitat Debug Mode", Float) = 0
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
            Cull [_Cull]
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MossyLoamMap);
            SAMPLER(sampler_MossyLoamMap);
            TEXTURE2D(_CanopyDuffMap);
            SAMPLER(sampler_CanopyDuffMap);
            TEXTURE2D(_MossCarpetMap);
            SAMPLER(sampler_MossCarpetMap);
            TEXTURE2D(_GroundcoverMap);
            SAMPLER(sampler_GroundcoverMap);
            TEXTURE2D(_StonyLichenMap);
            SAMPLER(sampler_StonyLichenMap);
            TEXTURE2D(_RoadMap);
            SAMPLER(sampler_RoadMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _RoadMap_ST;
                half4 _GroundColor;
                half4 _RoadColor;
                half4 _LoamTint;
                half4 _DuffTint;
                half4 _MossTint;
                half4 _GroundcoverTint;
                half4 _StonyTint;
                half4 _CliffTint;
                float _HabitatTiling;
                float _HabitatBrightness;
                float _HabitatBlendContrast;
                float _ForestGroundSaturation;
                float _CliffProjectionScale;
                float _CliffSlopeStart;
                float _CliffSlopeFull;
                float _CliffStrataScale;
                float _CliffStrataStrength;
                float _HabitatDebugMode;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 habitatWeights : TEXCOORD1;
                float4 habitatSignals : TEXCOORD2;
                float2 habitatDebug : TEXCOORD3;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 habitatUv : TEXCOORD2;
                float2 roadUv : TEXCOORD3;
                half4 color : COLOR;
                half4 habitatWeights : TEXCOORD4;
                half4 habitatSignals : TEXCOORD5;
                half colonyInfluence : TEXCOORD6;
                half fogFactor : TEXCOORD7;
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
                output.habitatUv = input.uv * _HabitatTiling;
                output.roadUv = TRANSFORM_TEX(input.uv, _RoadMap);
                output.habitatWeights = input.habitatWeights;
                output.habitatSignals = input.habitatSignals;
                output.colonyInfluence = input.habitatDebug.x;
                output.color = input.color;
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 normalWS = normalize(input.normalWS);
                half4 weights = saturate(input.habitatWeights);
                half stonyWeight = saturate(
                    1.0h - weights.x - weights.y -
                    weights.z - weights.w);
                weights = pow(
                    max(weights, half4(0.0001h, 0.0001h, 0.0001h, 0.0001h)),
                    _HabitatBlendContrast);
                stonyWeight = pow(
                    max(stonyWeight, 0.0001h),
                    _HabitatBlendContrast);
                half weightTotal = max(
                    0.001h,
                    weights.x + weights.y + weights.z +
                    weights.w + stonyWeight);
                weights /= weightTotal;
                stonyWeight /= weightTotal;
                half4 loam =
                    SAMPLE_TEXTURE2D(
                        _MossyLoamMap,
                        sampler_MossyLoamMap,
                        input.habitatUv) *
                    _GroundColor * _LoamTint;
                half4 duff =
                    SAMPLE_TEXTURE2D(
                        _CanopyDuffMap,
                        sampler_CanopyDuffMap,
                        input.habitatUv) *
                    _GroundColor * _DuffTint;
                half4 moss = SAMPLE_TEXTURE2D(
                    _MossCarpetMap,
                    sampler_MossCarpetMap,
                    input.habitatUv) * _GroundColor * _MossTint;
                half4 groundcover = SAMPLE_TEXTURE2D(
                    _GroundcoverMap,
                    sampler_GroundcoverMap,
                    input.habitatUv) * _GroundColor * _GroundcoverTint;
                half mossLuminance = dot(
                    moss.rgb,
                    half3(0.299h, 0.587h, 0.114h));
                half groundcoverLuminance = dot(
                    groundcover.rgb,
                    half3(0.299h, 0.587h, 0.114h));
                moss.rgb = lerp(
                    mossLuminance.xxx,
                    moss.rgb,
                    _ForestGroundSaturation);
                groundcover.rgb = lerp(
                    groundcoverLuminance.xxx,
                    groundcover.rgb,
                    _ForestGroundSaturation);
                half4 stony = SAMPLE_TEXTURE2D(
                    _StonyLichenMap,
                    sampler_StonyLichenMap,
                    input.habitatUv) * _GroundColor * _StonyTint;
                half4 ground =
                    loam * weights.x +
                    duff * weights.y +
                    moss * weights.z +
                    groundcover * weights.w +
                    stony * stonyWeight;
                half4 road =
                    SAMPLE_TEXTURE2D(
                        _RoadMap,
                        sampler_RoadMap,
                        input.roadUv) *
                    _RoadColor;
                half roadBlend =
                    smoothstep(0.0h, 1.0h, input.color.a);
                int debugMode = (int)round(_HabitatDebugMode);
                if (debugMode == 0)
                {
                    // Terrain UVs are projected across XZ, so they collapse
                    // into streaks on near-vertical escarpments. Blend the
                    // existing stony habitat texture from the two world-space
                    // side axes only where the surface is genuinely steep.
                    // Ordinary terrain remains on its existing habitat blend.
                    half2 cliffAxisWeights = pow(
                        max(abs(normalWS.xz), half2(0.0001h, 0.0001h)),
                        4.0h);
                    cliffAxisWeights /= max(
                        0.0001h,
                        cliffAxisWeights.x + cliffAxisWeights.y);
                    float cliffUvScale =
                        (_HabitatTiling / 12.0) * _CliffProjectionScale;
                    half4 cliffFromX = SAMPLE_TEXTURE2D(
                        _StonyLichenMap,
                        sampler_StonyLichenMap,
                        input.positionWS.zy * cliffUvScale);
                    half4 cliffFromZ = SAMPLE_TEXTURE2D(
                        _StonyLichenMap,
                        sampler_StonyLichenMap,
                        input.positionWS.xy * cliffUvScale);
                    half4 cliffSource =
                        cliffFromX * cliffAxisWeights.x +
                        cliffFromZ * cliffAxisWeights.y;
                    half cliffLuminance = dot(
                        cliffSource.rgb,
                        half3(0.299h, 0.587h, 0.114h));
                    // Broad world-height bands suggest sedimentary strata.
                    // Two incommensurate waves keep the bands from reading as
                    // perfectly uniform stripes while retaining cheap,
                    // deterministic shading and the existing texture set.
                    half strata =
                        sin(input.positionWS.y * _CliffStrataScale +
                            input.positionWS.x * 0.035) * 0.62h +
                        sin(input.positionWS.y * _CliffStrataScale * 2.31 +
                            input.positionWS.z * 0.047) * 0.38h;
                    cliffLuminance *=
                        1.0h + strata * _CliffStrataStrength;
                    half4 cliffStony = half4(
                        cliffLuminance.xxx * _CliffTint.rgb,
                        cliffSource.a * _CliffTint.a);
                    half surfaceSteepness =
                        1.0h - saturate(abs(normalWS.y));
                    half cliffBlend = smoothstep(
                        _CliffSlopeStart,
                        max(_CliffSlopeStart + 0.0001, _CliffSlopeFull),
                        surfaceSteepness);
                    ground = lerp(ground, cliffStony, cliffBlend);
                    ground.rgb *= _HabitatBrightness;
                }
                else if (debugMode == 1)
                {
                    half dominant = weights.x;
                    ground = half4(0.24h, 0.48h, 0.22h, 1.0h);
                    if (weights.y > dominant)
                    {
                        dominant = weights.y;
                        ground = half4(0.34h, 0.20h, 0.10h, 1.0h);
                    }
                    if (weights.z > dominant)
                    {
                        dominant = weights.z;
                        ground = half4(0.16h, 0.62h, 0.35h, 1.0h);
                    }
                    if (weights.w > dominant)
                    {
                        dominant = weights.w;
                        ground = half4(0.38h, 0.78h, 0.28h, 1.0h);
                    }
                    if (stonyWeight > dominant)
                    {
                        ground = half4(0.58h, 0.60h, 0.55h, 1.0h);
                    }
                }
                else if (debugMode >= 2 && debugMode <= 6)
                {
                    half value = debugMode == 2 ? weights.x :
                        debugMode == 3 ? weights.y :
                        debugMode == 4 ? weights.z :
                        debugMode == 5 ? weights.w : stonyWeight;
                    ground = half4(value, value, value, 1.0h);
                }
                else if (debugMode >= 7)
                {
                    half value = debugMode == 7
                        ? input.habitatSignals.x
                        : debugMode == 8
                            ? input.habitatSignals.y
                            : debugMode == 9
                                ? input.habitatSignals.z
                                : debugMode == 10
                                    ? input.habitatSignals.w
                                    : input.colonyInfluence;
                    ground = half4(
                        value,
                        value * 0.82h,
                        1.0h - value,
                        1.0h);
                }
                half4 surface =
                    lerp(ground, road, roadBlend);

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
                    max(lighting, half3(0.30h, 0.32h, 0.34h));
                surface.rgb = MixFog(surface.rgb, input.fogFactor);
                return surface;
            }
            ENDHLSL
        }
    }
}
