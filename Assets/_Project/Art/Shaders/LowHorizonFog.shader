Shader "WorldBuilder/Low Horizon Fog"
{
    Properties
    {
        _BaseColor("Fog Color", Color) = (0.22, 0.36, 0.34, 1)
        _NoiseScale("Broad Noise Scale", Float) = 0.035
        _DetailScale("Detail Noise Scale", Float) = 0.11
        _Drift("Drift Direction", Vector) = (0.010, 0.006, 0, 0)
        _DepthFadeDistance("Ground Softness", Float) = 1.35
        _NearFadeDistance("Near Fade Distance", Float) = 8
        _Undulation("Vertical Undulation", Float) = 0.08
        _PatchScale("Fog Bank Spacing", Float) = 0.025
        _PatchCoverage("Fog Bank Coverage", Range(0, 1)) = 0.38
        _PatchRadius("Fog Bank Radius", Range(0.1, 0.49)) = 0.42
    }
    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent+20"
            "RenderPipeline"="UniversalPipeline"
        }
        Pass
        {
            Name "Low Horizon Fog"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            ColorMask RGB

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float3 positionWS : TEXCOORD0;
                float4 screenPosition : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _NoiseScale;
                float _DetailScale;
                float4 _Drift;
                float _DepthFadeDistance;
                float _NearFadeDistance;
                float _Undulation;
                float _PatchScale;
                float _PatchCoverage;
                float _PatchRadius;
            CBUFFER_END

            float Hash21(float2 coordinate)
            {
                coordinate = frac(
                    coordinate * float2(123.34, 456.21));
                coordinate += dot(
                    coordinate,
                    coordinate + 45.32);
                return frac(coordinate.x * coordinate.y);
            }

            float ValueNoise(float2 coordinate)
            {
                float2 cell = floor(coordinate);
                float2 fraction = frac(coordinate);
                fraction = fraction * fraction * (3.0 - 2.0 * fraction);
                float bottom = lerp(
                    Hash21(cell),
                    Hash21(cell + float2(1.0, 0.0)),
                    fraction.x);
                float top = lerp(
                    Hash21(cell + float2(0.0, 1.0)),
                    Hash21(cell + float2(1.0, 1.0)),
                    fraction.x);
                return lerp(bottom, top, fraction.y);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(
                    input.positionOS.xyz);
                float time = _Time.y;
                float broadWave =
                    sin(positionWS.x * 0.041 + positionWS.z * 0.027 + time * 0.10) +
                    sin(positionWS.x * -0.019 + positionWS.z * 0.052 - time * 0.07);
                positionWS.y += broadWave * _Undulation * 0.5;
                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.screenPosition = ComputeScreenPos(output.positionCS);
                output.color = input.color * _BaseColor;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float time = _Time.y;
                float2 broadCoordinate =
                    input.positionWS.xz * _NoiseScale +
                    _Drift.xy * time;
                float2 detailCoordinate =
                    input.positionWS.xz * _DetailScale -
                    _Drift.yx * time * 1.7;
                float densityNoise =
                    ValueNoise(broadCoordinate) * 0.72 +
                    ValueNoise(detailCoordinate) * 0.28;
                float brokenDensity = smoothstep(0.34, 0.72, densityNoise);

                // Divide the island into broad cells, then enable only a
                // subset of them. Each selected cell contains one softly
                // feathered bank, leaving deliberate clear ground between
                // pockets instead of covering the whole map with a sheet.
                float2 patchCoordinate = input.positionWS.xz *
                    max(0.001, _PatchScale);
                float2 patchCell = floor(patchCoordinate);
                float2 patchLocal = frac(patchCoordinate) - 0.5;
                float2 patchOffset =
                    (float2(
                        Hash21(patchCell + float2(17.1, 3.7)),
                        Hash21(patchCell + float2(5.3, 23.9))) -
                     0.5) * 0.18;
                float patchSelected = step(
                    1.0 - saturate(_PatchCoverage),
                    Hash21(patchCell + float2(11.7, 29.3)));
                float patchDistance = length(
                    patchLocal - patchOffset);
                float patchMask = patchSelected *
                    (1.0 - smoothstep(
                        max(0.05, _PatchRadius * 0.52),
                        max(0.06, _PatchRadius),
                        patchDistance));
                brokenDensity *= patchMask;

                float cameraDistance = distance(
                    GetCameraPositionWS().xz,
                    input.positionWS.xz);
                float nearFade = smoothstep(
                    1.5,
                    max(1.6, _NearFadeDistance),
                    cameraDistance);

                float2 screenUV = input.screenPosition.xy /
                    max(input.screenPosition.w, 0.0001);
                float rawSceneDepth = SampleSceneDepth(screenUV);
                float sceneEyeDepth = LinearEyeDepth(
                    rawSceneDepth,
                    _ZBufferParams);
                float fogEyeDepth = input.screenPosition.w;
                float depthFade = saturate(
                    (sceneEyeDepth - fogEyeDepth) /
                    max(0.05, _DepthFadeDistance));

                half alpha = input.color.a *
                    brokenDensity *
                    nearFade *
                    depthFade;
                return half4(input.color.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
