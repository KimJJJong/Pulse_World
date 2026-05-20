Shader "RhythmRPG/Effects/AdditiveGlow"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _GlowColor ("Glow Color", Color) = (0.18, 0.9, 1, 1)
        _Intensity ("Intensity", Float) = 1.5
        _Alpha ("Alpha", Range(0, 1)) = 0.35
        _BaseStrength ("Base Strength", Range(0, 1)) = 0.2
        _FresnelStrength ("Fresnel Strength", Range(0, 2)) = 0.9
        _FresnelPower ("Fresnel Power", Range(0.1, 8)) = 1.7
        _UseTextureMask ("Use Texture Mask", Float) = 0
        _UseFresnel ("Use Fresnel", Float) = 1
        _UseTextureAlpha ("Use Texture Alpha", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "AdditiveGlow"
            Tags { "LightMode" = "UniversalForward" }

            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _GlowColor;
                half _Intensity;
                half _Alpha;
                half _BaseStrength;
                half _FresnelStrength;
                half _FresnelPower;
                half _UseTextureMask;
                half _UseFresnel;
                half _UseTextureAlpha;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half3 normalWS : TEXCOORD0;
                half3 viewDirWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewDirWS = GetCameraPositionWS() - positionWS;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half rgbMask = max(tex.a, max(max(tex.r, tex.g), tex.b));
                half alphaOnly = step(0.5h, _UseTextureAlpha);
                half sampledMask = lerp(rgbMask, tex.a, alphaOnly);
                half textureMask = lerp(1.0h, sampledMask, step(0.5h, _UseTextureMask));

                half3 normalWS = normalize(input.normalWS);
                half3 viewDirWS = normalize(input.viewDirWS);
                half fresnel = pow(saturate(1.0h - dot(normalWS, viewDirWS)), max(0.01h, _FresnelPower));
                half fresnelMask = saturate(_BaseStrength + fresnel * _FresnelStrength);
                half useFresnel = step(0.5h, _UseFresnel);
                half mask = lerp(textureMask, textureMask * fresnelMask, useFresnel);

                half3 glow = _GlowColor.rgb * _Intensity * _Alpha * mask;
                return half4(glow, mask);
            }
            ENDHLSL
        }
    }
}
