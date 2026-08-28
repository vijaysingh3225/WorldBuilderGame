Shader "WorldBuilder/Deep Ocean"
{
    Properties
    {
        _DeepColor("Deep Ocean", Color) = (0.012, 0.055, 0.14, 1)
        _CurrentColor("Moving Highlights", Color) = (0.025, 0.14, 0.28, 1)
        _WaveScale("Wave Scale", Range(0.005, 0.2)) = 0.045
        _WaveSpeed("Wave Speed", Range(0, 0.3)) = 0.035
        _WaveStrength("Wave Strength", Range(0, 1)) = 0.16
        _Smoothness("Smoothness", Range(0, 1)) = 0.72
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
            "RenderType" = "Opaque"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _DeepColor;
                half4 _CurrentColor;
                float _WaveScale;
                float _WaveSpeed;
                float _WaveStrength;
                float _Smoothness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half fogFactor : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs =
                    GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.fogFactor =
                    ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float scale = max(0.001, _WaveScale);
                float time = _Time.y * _WaveSpeed;
                float phaseA =
                    input.positionWS.x * scale +
                    input.positionWS.z * scale * 0.72 +
                    time;
                float phaseB =
                    input.positionWS.x * scale * -0.43 +
                    input.positionWS.z * scale * 1.31 -
                    time * 0.73 + 1.7;
                float phaseC =
                    input.positionWS.x * scale * 1.67 +
                    input.positionWS.z * scale * -0.29 +
                    time * 0.41 + 3.2;
                float wave =
                    sin(phaseA) * 0.50 +
                    sin(phaseB) * 0.32 +
                    sin(phaseC) * 0.18;

                float derivativeX =
                    cos(phaseA) * scale * 0.50 +
                    cos(phaseB) * scale * -0.43 * 0.32 +
                    cos(phaseC) * scale * 1.67 * 0.18;
                float derivativeZ =
                    cos(phaseA) * scale * 0.72 * 0.50 +
                    cos(phaseB) * scale * 1.31 * 0.32 +
                    cos(phaseC) * scale * -0.29 * 0.18;
                half3 normalWS = normalize(half3(
                    -derivativeX * _WaveStrength * 18.0,
                    1.0,
                    -derivativeZ * _WaveStrength * 18.0));

                Light mainLight = GetMainLight();
                half diffuse = saturate(
                    dot(normalWS, mainLight.direction));
                half3 viewDirection = SafeNormalize(
                    GetCameraPositionWS() - input.positionWS);
                half3 halfDirection = SafeNormalize(
                    mainLight.direction + viewDirection);
                half specular = pow(
                    saturate(dot(normalWS, halfDirection)),
                    lerp(12.0h, 96.0h, saturate(_Smoothness)));
                half movingHighlight =
                    saturate(wave * 0.5 + 0.5) * 0.22h;
                half3 color = lerp(
                    _DeepColor.rgb,
                    _CurrentColor.rgb,
                    movingHighlight);
                color *= lerp(0.82h, 1.05h, diffuse);
                color += specular * 0.18h * mainLight.color;
                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }
}
