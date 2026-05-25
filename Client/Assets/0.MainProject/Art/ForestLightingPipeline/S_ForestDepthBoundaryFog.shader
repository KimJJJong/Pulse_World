Shader "RhythmRPG/Effects/ForestDepthBoundaryFog"
{
    Properties
    {
        _FogEnabled ("Fog Enabled", Float) = 0
        _FogZoneCount ("Fog Zone Count", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
        }

        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "ForestDepthBoundaryFog"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            #define MAX_FOG_ZONES 8

            float _FogEnabled;
            float _FogZoneCount;
            float4 _FogZoneCenters[MAX_FOG_ZONES];
            float4 _FogZoneRightAxes[MAX_FOG_ZONES];
            float4 _FogZoneForwardAxes[MAX_FOG_ZONES];
            float4 _FogZoneParams[MAX_FOG_ZONES];
            float4 _FogZoneNoiseParams[MAX_FOG_ZONES];
            float4 _FogZoneColors[MAX_FOG_ZONES];

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 cell = floor(p);
                float2 local = frac(p);
                float2 blend = local * local * (3.0 - 2.0 * local);

                float a = Hash(cell);
                float b = Hash(cell + float2(1.0, 0.0));
                float c = Hash(cell + float2(0.0, 1.0));
                float d = Hash(cell + float2(1.0, 1.0));

                return lerp(lerp(a, b, blend.x), lerp(c, d, blend.x), blend.y);
            }

            float ZoneMask(float3 worldPosition, int zoneIndex)
            {
                float3 delta = worldPosition - _FogZoneCenters[zoneIndex].xyz;
                float2 localPosition = float2(
                    dot(delta, normalize(_FogZoneRightAxes[zoneIndex].xyz)),
                    dot(delta, normalize(_FogZoneForwardAxes[zoneIndex].xyz))
                );

                float2 halfSize = max(_FogZoneParams[zoneIndex].xy, 0.001);
                float edgeDistance = min(halfSize.x - abs(localPosition.x), halfSize.y - abs(localPosition.y));

                float noiseStrength = _FogZoneNoiseParams[zoneIndex].x;
                float noiseScale = _FogZoneNoiseParams[zoneIndex].y;
                float noiseOffset = (ValueNoise(worldPosition.xz * noiseScale) * 2.0 - 1.0) * noiseStrength;
                float blendDistance = max(_FogZoneParams[zoneIndex].z, 0.001);

                return smoothstep(0.0, blendDistance, edgeDistance + noiseOffset);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                half4 sceneColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                if (_FogEnabled < 0.5 || _FogZoneCount < 0.5)
                {
                    return half4(sceneColor.rgb, 1.0);
                }

                float rawDepth = SampleSceneDepth(uv);

                #if UNITY_REVERSED_Z
                    if (rawDepth <= 0.0001)
                    {
                        return half4(sceneColor.rgb, 1.0);
                    }
                #else
                    if (rawDepth >= 0.9999)
                    {
                        return half4(sceneColor.rgb, 1.0);
                    }
                #endif

                float3 worldPosition = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
                float eyeDistance = distance(_WorldSpaceCameraPos, worldPosition);
                float3 finalColor = sceneColor.rgb;
                float accumulatedFog = 0.0;
                int zoneCount = min((int)_FogZoneCount, MAX_FOG_ZONES);

                for (int i = 0; i < MAX_FOG_ZONES; i++)
                {
                    if (i >= zoneCount)
                    {
                        break;
                    }

                    float areaMask = ZoneMask(worldPosition, i);
                    float density = max(_FogZoneParams[i].w, 0.0);
                    float distanceFog = 1.0 - exp(-density * eyeDistance);
                    float zoneFog = saturate(areaMask * distanceFog);
                    float contribution = zoneFog * (1.0 - accumulatedFog);

                    finalColor = lerp(finalColor, _FogZoneColors[i].rgb, contribution);
                    accumulatedFog = saturate(accumulatedFog + contribution);
                }

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }
}
