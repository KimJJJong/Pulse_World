Shader "RhythmRPG/Nature/LowPolyWeed"
{
    Properties
    {
        _BottomColor ("Bottom Color", Color) = (0.16, 0.29, 0.10, 1)
        _MiddleColor ("Middle Color", Color) = (0.36, 0.55, 0.16, 1)
        _TopColor ("Top Color", Color) = (0.72, 0.82, 0.28, 1)
        _GradientSteps ("Gradient Steps", Range(1, 5)) = 3
        _LightSteps ("Light Steps", Range(1, 5)) = 3
        _AmbientStrength ("Ambient Strength", Range(0, 1)) = 0.38
        _HueVariation ("Hue Variation", Range(0, 0.5)) = 0.16
        _ValueVariation ("Value Variation", Range(0, 0.5)) = 0.10
        _HeightVariation ("Height Variation", Range(0, 0.5)) = 0.12
        _WindStrength ("Wind Strength", Range(0, 1)) = 0.34
        _WindSpeed ("Wind Speed", Range(0, 6)) = 1.15
        _WindFrequency ("Wind Frequency", Range(0, 8)) = 2.25
        _WindNoise ("Wind Noise", Range(0, 1)) = 0.28
        _BendHeight ("Bend Height", Range(0.05, 3)) = 1.0
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

            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BottomColor;
                half4 _MiddleColor;
                half4 _TopColor;
                half _GradientSteps;
                half _LightSteps;
                half _AmbientStrength;
                half _HueVariation;
                half _ValueVariation;
                half _HeightVariation;
                half _WindStrength;
                half _WindSpeed;
                half _WindFrequency;
                half _WindNoise;
                half _BendHeight;
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
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half2 uv : TEXCOORD2;
                half seed : TEXCOORD3;
                half fogFactor : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            float3 ApplyWeedWind(float3 positionOS, float2 uv, out half seed)
            {
                float3 pivotWS = float3(unity_ObjectToWorld._m03, unity_ObjectToWorld._m13, unity_ObjectToWorld._m23);
                seed = Hash21(pivotWS.xz + float2(11.7, 3.1));

                float heightMask = saturate(max(uv.y, positionOS.y / max(0.001, _BendHeight)));
                float heightScale = 1.0 + (seed - 0.5) * 2.0 * _HeightVariation;
                positionOS.y *= lerp(1.0, heightScale, heightMask);

                float3 positionWS = TransformObjectToWorld(positionOS);
                float time = _Time.y * _WindSpeed + seed * 6.2831853;
                float waveA = sin(time + positionWS.x * _WindFrequency + positionWS.z * (_WindFrequency * 0.73));
                float waveB = sin(time * 1.71 + positionWS.x * 0.41 - positionWS.z * 0.63);
                float sway = (waveA + waveB * _WindNoise) * _WindStrength * heightMask * heightMask * 0.28;

                float2 windDir = normalize(float2(0.68 + seed * 0.38, 0.46 - seed * 0.92));
                positionWS.xz += windDir * sway;
                return positionWS;
            }

            half3 SteppedGradient(half height01)
            {
                half steps = max(1.0h, round(_GradientSteps));
                half stepped = height01;
                if (steps > 1.5h)
                {
                    stepped = saturate(floor(height01 * steps) / max(1.0h, steps - 1.0h));
                }

                half3 lower = lerp(_BottomColor.rgb, _MiddleColor.rgb, smoothstep(0.0h, 0.58h, stepped));
                return lerp(lower, _TopColor.rgb, smoothstep(0.42h, 1.0h, stepped));
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                half seed;
                float3 positionWS = ApplyWeedWind(input.positionOS.xyz, input.uv, seed);
                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.seed = seed;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half height01 = saturate(input.uv.y);
                half3 baseColor = SteppedGradient(height01);

                half hueShift = (input.seed - 0.5h) * 2.0h * _HueVariation;
                half3 hueTint = half3(1.0h - hueShift * 0.45h, 1.0h + hueShift * 0.28h, 1.0h - abs(hueShift) * 0.35h);
                half valueTint = lerp(1.0h - _ValueVariation, 1.0h + _ValueVariation, input.seed);
                baseColor = saturate(baseColor * hueTint * valueTint);

                half3 normalWS = normalize(input.normalWS);
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                half ndotl = saturate(abs(dot(normalWS, mainLight.direction)));
                half lightSteps = max(1.0h, round(_LightSteps));
                half steppedLight = floor(ndotl * lightSteps) / lightSteps;
                half lightAmount = saturate(_AmbientStrength + steppedLight * mainLight.shadowAttenuation * (1.0h - _AmbientStrength));

                half3 color = baseColor * mainLight.color * lightAmount;
                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            Cull Off
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BottomColor;
                half4 _MiddleColor;
                half4 _TopColor;
                half _GradientSteps;
                half _LightSteps;
                half _AmbientStrength;
                half _HueVariation;
                half _ValueVariation;
                half _HeightVariation;
                half _WindStrength;
                half _WindSpeed;
                half _WindFrequency;
                half _WindNoise;
                half _BendHeight;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            float3 ApplyWeedWind(float3 positionOS, float2 uv)
            {
                float3 pivotWS = float3(unity_ObjectToWorld._m03, unity_ObjectToWorld._m13, unity_ObjectToWorld._m23);
                float seed = Hash21(pivotWS.xz + float2(11.7, 3.1));

                float heightMask = saturate(max(uv.y, positionOS.y / max(0.001, _BendHeight)));
                float heightScale = 1.0 + (seed - 0.5) * 2.0 * _HeightVariation;
                positionOS.y *= lerp(1.0, heightScale, heightMask);

                float3 positionWS = TransformObjectToWorld(positionOS);
                float time = _Time.y * _WindSpeed + seed * 6.2831853;
                float waveA = sin(time + positionWS.x * _WindFrequency + positionWS.z * (_WindFrequency * 0.73));
                float waveB = sin(time * 1.71 + positionWS.x * 0.41 - positionWS.z * 0.63);
                float sway = (waveA + waveB * _WindNoise) * _WindStrength * heightMask * heightMask * 0.28;

                float2 windDir = normalize(float2(0.68 + seed * 0.38, 0.46 - seed * 0.92));
                positionWS.xz += windDir * sway;
                return positionWS;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 positionWS = ApplyWeedWind(input.positionOS.xyz, input.uv);
                output.positionCS = TransformWorldToHClip(positionWS);

                #if UNITY_REVERSED_Z
                    output.positionCS.z = min(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    output.positionCS.z = max(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return 0;
            }
            ENDHLSL
        }
    }
}
