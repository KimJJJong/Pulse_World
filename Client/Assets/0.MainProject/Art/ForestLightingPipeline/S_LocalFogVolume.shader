Shader "RhythmRPG/Effects/LocalFogVolume"
{
    Properties
    {
        _FogColor ("Fog Color", Color) = (0.45, 0.85, 1.0, 0.35)
        _Center ("Center (World)", Vector) = (0, 0, 0, 0)
        _Radius ("Radius", Float) = 4
        _Density ("Density", Range(0, 1)) = 0.35
        _EdgeFade ("Edge Fade", Float) = 1.8
        _HeightFade ("Height Fade", Float) = 1.2
        _NoiseScale ("Noise Scale", Float) = 1.6
        _NoiseStrength ("Noise Strength", Range(0, 1)) = 0.18
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "LocalFogVolume"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _FogColor;
                float4 _Center;
                float _Radius;
                float _Density;
                float _EdgeFade;
                float _HeightFade;
                float _NoiseScale;
                float _NoiseStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            float HashNoise(float3 value)
            {
                float3 cell = floor(value);
                return frac(sin(dot(cell, float3(12.9898, 78.233, 37.719))) * 43758.5453);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 delta = input.positionWS - _Center.xyz;
                float radius = max(_Radius, 0.001);
                float radial = saturate(1.0 - length(delta) / radius);
                float edge = pow(radial, max(_EdgeFade, 0.001));
                float height = saturate(1.0 - abs(delta.y) / max(_HeightFade, 0.001));
                float noise = lerp(1.0, 0.65 + HashNoise(input.positionWS * _NoiseScale) * 0.35, _NoiseStrength);
                float alpha = _FogColor.a * _Density * edge * height * noise;
                return half4(_FogColor.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
