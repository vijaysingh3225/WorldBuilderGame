Shader "WorldBuilder/Stylized River Flow"
{
    Properties
    {
        [MainTexture] _BaseMap("Flow Texture", 2D) = "white" {}
        _DeepColor("Deep Water", Color) = (0.035, 0.09, 0.145, 1)
        _CurrentColor("Current Color", Color) = (0.18, 0.32, 0.42, 1)
        _FoamColor("Whitewater", Color) = (0.95, 0.96, 0.93, 1)
        _Opacity("Opacity", Range(0, 1)) = 0.99
        _FlowSpeed("Flow Speed", Range(0, 1)) = 0.22
        _SecondarySpeed("Secondary Speed", Range(0, 1)) = 0.22
        _FlowDirection("Flow Direction", Float) = 1
        _FlowPhase("Flow Phase", Range(0, 1)) = 0
        _FoamStrength("Foam Strength", Range(0, 1)) = 1
        _WaveHeight("Surface Relief", Range(0, 0.3)) = 0.22
        _NormalStrength("Moving Normal Strength", Range(0, 24)) = 10
        _StreamSeparation("Stream Separation", Range(0, 0.3)) = 0.20
        _BankEddyStrength("Bank Eddy Strength", Range(0, 0.12)) = 0.055
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            float4 _BaseMap_TexelSize;

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _DeepColor;
                half4 _CurrentColor;
                half4 _FoamColor;
                half _Opacity;
                half _FlowSpeed;
                half _SecondarySpeed;
                half _FlowDirection;
                half _FlowPhase;
                half _FoamStrength;
                half _WaveHeight;
                half _NormalStrength;
                half _StreamSeparation;
                half _BankEddyStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                float2 flowData : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half3 tangentWS : TEXCOORD2;
                half3 bitangentWS : TEXCOORD3;
                float2 uv : TEXCOORD4;
                half fogFactor : TEXCOORD5;
                half2 flowData : TEXCOORD6;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float2 flowUv =
                    TRANSFORM_TEX(
                        input.uv,
                        _BaseMap);
                float direction =
                    _FlowDirection >= 0.0h
                        ? 1.0
                        : -1.0;
                float travel =
                    (_Time.y * _FlowSpeed +
                     _FlowPhase) *
                    direction;
                float centerFactor =
                    saturate(
                        1.0 -
                        abs(input.uv.x * 2.0 - 1.0));
                float primaryWave =
                    sin(
                        (flowUv.y - travel) *
                        6.28318 +
                        flowUv.x * 2.35);
                float secondaryWave =
                    sin(
                        (flowUv.y - travel) *
                        10.6814 -
                        flowUv.x * 4.15 +
                        1.27);
                float edgeCalm =
                    lerp(
                        0.62,
                        1.0,
                        centerFactor);
                float4 displacedPosition =
                    input.positionOS;
                displacedPosition.y +=
                    (primaryWave * 0.38 +
                     secondaryWave * 0.16) *
                    _WaveHeight *
                    edgeCalm;
                VertexPositionInputs positionInputs =
                    GetVertexPositionInputs(
                        displacedPosition.xyz);
                VertexNormalInputs normalInputs =
                    GetVertexNormalInputs(
                        input.normalOS,
                        input.tangentOS);
                output.positionHCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.tangentWS = normalInputs.tangentWS;
                output.bitangentWS = normalInputs.bitangentWS;
                output.uv = flowUv;
                output.flowData = input.flowData;
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float direction =
                    _FlowDirection >= 0.0h ? 1.0 : -1.0;
                float centerFactor =
                    saturate(
                        1.0 -
                        abs(input.uv.x * 2.0 - 1.0));
                float primaryTravel =
                    (_Time.y * _FlowSpeed +
                     _FlowPhase) *
                    direction;
                float secondaryTravel =
                    (_Time.y * _FlowSpeed +
                     _FlowPhase * 0.63) *
                    direction;
                float tertiaryTravel =
                    (_Time.y * _FlowSpeed +
                     _FlowPhase * 0.37) *
                    direction;

                float laneOscillation =
                    sin(
                        input.uv.y * 0.83 -
                        _Time.y * 0.24 * direction +
                        input.flowData.x * 2.1);
                float laneA =
                    0.31 +
                    _StreamSeparation *
                    laneOscillation;
                float laneB =
                    0.69 -
                    _StreamSeparation *
                    laneOscillation;
                float laneC =
                    0.50 +
                    _StreamSeparation * 0.58 *
                    sin(
                        input.uv.y * 1.19 +
                        _Time.y * 0.17 * direction +
                        1.73);
                half laneAWeight =
                    1.0h -
                    smoothstep(
                        0.045h,
                        0.25h,
                        abs(input.uv.x - laneA));
                half laneBWeight =
                    1.0h -
                    smoothstep(
                        0.045h,
                        0.25h,
                        abs(input.uv.x - laneB));
                half laneCWeight =
                    1.0h -
                    smoothstep(
                        0.035h,
                        0.21h,
                        abs(input.uv.x - laneC));
                float lateralWarp =
                    sin(
                        input.uv.y * 1.61 -
                        primaryTravel * 3.7 +
                        input.uv.x * 4.3) *
                    lerp(
                        0.018,
                        _BankEddyStrength,
                        1.0 - centerFactor) +
                    input.flowData.x * 0.032 *
                    (input.uv.x - 0.5);
                float2 primaryUv =
                    input.uv - float2(0.0, primaryTravel);
                primaryUv.x += lateralWarp;
                float2 secondaryUv =
                    float2(
                        input.uv.x * 1.17 +
                            0.27 -
                            lateralWarp * 0.72,
                        input.uv.y -
                            secondaryTravel +
                            0.43);
                float2 tertiaryUv =
                    float2(
                        1.0 - input.uv.x * 0.91 +
                            0.18 +
                            lateralWarp * 0.48,
                        input.uv.y -
                            tertiaryTravel +
                            0.71);
                half3 primary =
                    SAMPLE_TEXTURE2D(
                        _BaseMap,
                        sampler_BaseMap,
                        primaryUv).rgb;
                half3 secondary =
                    SAMPLE_TEXTURE2D(
                        _BaseMap,
                        sampler_BaseMap,
                        secondaryUv).rgb;
                half3 tertiary =
                    SAMPLE_TEXTURE2D(
                        _BaseMap,
                        sampler_BaseMap,
                        tertiaryUv).rgb;
                half primaryLuminance =
                    dot(primary, half3(0.2126h, 0.7152h, 0.0722h));
                half secondaryLuminance =
                    dot(secondary, half3(0.2126h, 0.7152h, 0.0722h));
                half tertiaryLuminance =
                    dot(tertiary, half3(0.2126h, 0.7152h, 0.0722h));
                half primaryWeight =
                    0.24h + laneAWeight * 1.18h;
                half secondaryWeight =
                    0.24h + laneBWeight * 1.18h;
                half tertiaryWeight =
                    0.18h +
                    laneCWeight * 0.82h +
                    (1.0h - centerFactor) * 0.34h;
                half weightTotal =
                    primaryWeight +
                    secondaryWeight +
                    tertiaryWeight;
                half flowLuminance =
                    (primaryLuminance * primaryWeight +
                     secondaryLuminance * secondaryWeight +
                     tertiaryLuminance * tertiaryWeight) /
                    weightTotal;
                half currentMask =
                    saturate(
                        (flowLuminance - 0.105h) *
                        2.85h);
                half foamSource =
                    max(
                        primaryLuminance *
                            lerp(0.58h, 1.0h, laneAWeight),
                        max(
                            secondaryLuminance *
                                lerp(0.58h, 1.0h, laneBWeight),
                            tertiaryLuminance *
                                lerp(0.52h, 0.94h, laneCWeight)));
                half laneConvergence =
                    1.0h -
                    smoothstep(
                        0.05h,
                        0.34h,
                        abs(laneA - laneB));
                half foamMask =
                    smoothstep(
                        0.30h,
                        0.58h,
                        foamSource +
                        currentMask * 0.065h +
                        abs(input.flowData.x) * 0.085h +
                        laneConvergence * 0.11h +
                        centerFactor * 0.028h) *
                    _FoamStrength;

                half3 waterColor =
                    lerp(
                        _DeepColor.rgb,
                        _CurrentColor.rgb,
                        currentMask);
                waterColor =
                    lerp(
                        waterColor,
                        _FoamColor.rgb,
                        foamMask);

                float2 normalStep =
                    _BaseMap_TexelSize.xy * 3.0;
                half3 sampleAcross =
                    SAMPLE_TEXTURE2D(
                        _BaseMap,
                        sampler_BaseMap,
                        primaryUv +
                        float2(normalStep.x, 0.0)).rgb;
                half3 sampleAlong =
                    SAMPLE_TEXTURE2D(
                        _BaseMap,
                        sampler_BaseMap,
                        primaryUv +
                        float2(0.0, normalStep.y)).rgb;
                half luminanceAcross =
                    dot(
                        sampleAcross,
                        half3(0.2126h, 0.7152h, 0.0722h));
                half luminanceAlong =
                    dot(
                        sampleAlong,
                        half3(0.2126h, 0.7152h, 0.0722h));
                half3 normalTS =
                    normalize(
                        half3(
                            (primaryLuminance -
                             luminanceAcross) *
                            _NormalStrength +
                            cos(
                                (input.uv.y -
                                 primaryTravel) *
                                6.28318 +
                                input.uv.x * 2.35) *
                            0.22h,
                            (primaryLuminance -
                             luminanceAlong) *
                            _NormalStrength +
                            cos(
                                (input.uv.y -
                                 primaryTravel) *
                                10.6814 -
                                input.uv.x * 4.15 +
                                1.27) *
                            0.16h,
                            1.0h));
                half3 normalWS =
                    normalize(
                        input.tangentWS * normalTS.x +
                        input.bitangentWS * normalTS.y +
                        input.normalWS * normalTS.z);
                float4 shadowCoord =
                    TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half diffuse =
                    saturate(dot(normalWS, mainLight.direction)) *
                    mainLight.distanceAttenuation *
                    mainLight.shadowAttenuation;
                half3 ambient = SampleSH(normalWS);
                half3 lighting =
                    max(
                        ambient + mainLight.color * diffuse * 0.52h,
                        half3(0.42h, 0.45h, 0.50h));
                waterColor *= lighting;

                half3 viewDirection =
                    GetWorldSpaceNormalizeViewDir(input.positionWS);
                half3 halfDirection =
                    normalize(mainLight.direction + viewDirection);
                half restrainedHighlight =
                    pow(saturate(dot(normalWS, halfDirection)), 48.0h) *
                    mainLight.shadowAttenuation *
                    0.13h;
                waterColor +=
                    mainLight.color * restrainedHighlight;
                waterColor = MixFog(waterColor, input.fogFactor);

                half alpha =
                    saturate(_Opacity + foamMask * 0.10h);
                return half4(waterColor, alpha);
            }
            ENDHLSL
        }
    }
}
