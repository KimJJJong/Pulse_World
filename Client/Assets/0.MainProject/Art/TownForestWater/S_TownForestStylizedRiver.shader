Shader "RhythmRPG/TownForestStylizedRiver"
{
    Properties
    {
        _DeepColor ("Deep Water", Color) = (0.027, 0.098, 0.137, 0.92)
        _ShallowColor ("Shallow Water", Color) = (0.063, 0.196, 0.267, 0.68)
        _FlowColor ("Flow Highlight", Color) = (0.424, 0.624, 0.686, 0.24)
        _FoamColor ("Foam", Color) = (0.788, 0.980, 1.000, 0.48)
        _Alpha ("Base Alpha", Range(0, 1)) = 0.78
        _FlowSpeed ("Flow Speed", Float) = 0.9
        _RippleScale ("Ripple Scale", Float) = 18.0
        _FoamWidth ("Foam Width", Range(0, 0.5)) = 0.10
        _FoamStrength ("Foam Strength", Range(0, 1)) = 0.38
        _FlowStrength ("Flow Strength", Range(0, 1)) = 0.11
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
            Name "StylizedRiver"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _DeepColor;
                half4 _ShallowColor;
                half4 _FlowColor;
                half4 _FoamColor;
                half _Alpha;
                half _FlowSpeed;
                half _RippleScale;
                half _FoamWidth;
                half _FoamStrength;
                half _FlowStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half time = _Time.y * _FlowSpeed;
                half edgeDistance = min(input.uv.y, 1.0h - input.uv.y);
                half edge = saturate(1.0h - smoothstep(0.0h, max(0.001h, _FoamWidth), edgeDistance));

                half waveA = sin((input.uv.x * _RippleScale) + time * 2.1h + input.positionWS.z * 0.45h);
                half waveB = sin((input.uv.x * (_RippleScale * 0.47h)) - time * 1.4h + input.positionWS.x * 0.31h);
                half ripple = saturate((waveA + waveB) * 0.22h + 0.5h);

                half thinLine = smoothstep(0.83h, 1.0h, sin(input.uv.x * 42.0h - time * 5.0h) * 0.5h + 0.5h);
                half brokenFlow = thinLine * smoothstep(0.18h, 0.55h, edgeDistance) * smoothstep(0.55h, 0.22h, edgeDistance);
                half foamNoise = saturate((sin(input.uv.x * 23.0h + time * 3.8h + input.positionWS.x * 0.2h) * 0.5h + 0.5h));
                half foam = edge * foamNoise * _FoamStrength;

                half3 waterColor = lerp(_DeepColor.rgb, _ShallowColor.rgb, ripple * 0.42h + edge * 0.18h);
                waterColor = lerp(waterColor, _FlowColor.rgb, brokenFlow * _FlowStrength);
                waterColor = lerp(waterColor, _FoamColor.rgb, foam);

                half alpha = saturate(_Alpha + brokenFlow * _FlowColor.a + foam * _FoamColor.a + edge * 0.1h);
                return half4(waterColor, alpha);
            }
            ENDHLSL
        }
    }
}
