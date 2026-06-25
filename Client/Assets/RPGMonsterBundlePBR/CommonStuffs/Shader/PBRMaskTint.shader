// URP port of the legacy PBRMaskTint surface shader.
Shader "PBRMaskTint"
{
    Properties
    {
        _Albedo("Albedo", 2D) = "white" {}
        _MetallicSmoothness("MetallicSmoothness", 2D) = "white" {}
        _Emission("Emission", 2D) = "white" {}
        _Mask01("Mask01", 2D) = "white" {}
        _Mask02("Mask02", 2D) = "white" {}
        _Mask03("Mask03", 2D) = "white" {}
        _Color01("Color01", Color) = (0.7205882, 0.08477508, 0.08477508, 0)
        _Color02("Color02", Color) = (0.02649222, 0.3602941, 0.09785674, 0)
        _Color03("Color03", Color) = (0.07628676, 0.2567445, 0.6102941, 0)
        _Color04("Color04", Color) = (1, 0.6729082, 0, 0)
        _Color05("Color05", Color) = (0.3161438, 0.08018869, 1, 0)
        _Color06("Color06", Color) = (0.829558, 0.2311321, 1, 0)
        _Color07("Color07", Color) = (0.5660378, 0.23073, 0.03470988, 0)
        _Color08("Color08", Color) = (0.3584906, 0.3584906, 0.3584906, 0)
        _Color09("Color09", Color) = (0.9622642, 0.6942402, 0.521983, 0)
        [HDR] _EmissionPower("EmissionPower", Color) = (0, 0, 0, 0)
        [HideInInspector] [HDR] _EmissionColor("EmissionColor", Color) = (0, 0, 0, 0)
        _Color01Power("Color01Power", Range(0, 10)) = 1
        _Color02Power("Color02Power", Range(0, 16)) = 1
        _Color03Power("Color03Power", Range(0, 10)) = 1
        _Color04Power("Color04Power", Range(0, 10)) = 1
        _Color05Power("Color05Power", Range(0, 10)) = 1
        _Color06Power("Color06Power", Range(0, 10)) = 1
        _Color07Power("Color07Power", Range(0, 10)) = 1
        _Color08Power("Color08Power", Range(0, 10)) = 1
        _Color09Power("Color09Power", Range(0, 10)) = 1
        [HideInInspector] _texcoord("", 2D) = "white" {}
        [HideInInspector] __dirty("", Int) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "IsEmissive" = "true"
            "IgnoreProjector" = "True"
        }
        LOD 300
        Cull Back

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _MIXED_LIGHTING_SUBTRACTIVE
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float2 lightmapUV : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 4);
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Albedo_ST;
                float4 _MetallicSmoothness_ST;
                float4 _Emission_ST;
                float4 _Mask01_ST;
                float4 _Mask02_ST;
                float4 _Mask03_ST;
                half4 _Color01;
                half4 _Color02;
                half4 _Color03;
                half4 _Color04;
                half4 _Color05;
                half4 _Color06;
                half4 _Color07;
                half4 _Color08;
                half4 _Color09;
                half4 _EmissionPower;
                half4 _EmissionColor;
                half _Color01Power;
                half _Color02Power;
                half _Color03Power;
                half _Color04Power;
                half _Color05Power;
                half _Color06Power;
                half _Color07Power;
                half _Color08Power;
                half _Color09Power;
            CBUFFER_END

            TEXTURE2D(_Albedo); SAMPLER(sampler_Albedo);
            TEXTURE2D(_MetallicSmoothness); SAMPLER(sampler_MetallicSmoothness);
            TEXTURE2D(_Emission); SAMPLER(sampler_Emission);
            TEXTURE2D(_Mask01); SAMPLER(sampler_Mask01);
            TEXTURE2D(_Mask02); SAMPLER(sampler_Mask02);
            TEXTURE2D(_Mask03); SAMPLER(sampler_Mask03);

            Varyings LitPassVertex(Attributes input)
            {
                Varyings output = (Varyings)0;

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);

                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(vertexInput.positionCS.z);

                OUTPUT_LIGHTMAP_UV(input.lightmapUV, unity_LightmapST, output.lightmapUV);
                OUTPUT_SH(output.normalWS, output.vertexSH);

                return output;
            }

            half3 GetTintedAlbedo(float2 baseUV)
            {
                float2 albedoUV = TRANSFORM_TEX(baseUV, _Albedo);
                float2 mask01UV = TRANSFORM_TEX(baseUV, _Mask01);
                float2 mask02UV = TRANSFORM_TEX(baseUV, _Mask02);
                float2 mask03UV = TRANSFORM_TEX(baseUV, _Mask03);

                half3 baseColor = SAMPLE_TEXTURE2D(_Albedo, sampler_Albedo, albedoUV).rgb;
                half3 mask01 = SAMPLE_TEXTURE2D(_Mask01, sampler_Mask01, mask01UV).rgb;
                half3 mask02 = SAMPLE_TEXTURE2D(_Mask02, sampler_Mask02, mask02UV).rgb;
                half3 mask03 = SAMPLE_TEXTURE2D(_Mask03, sampler_Mask03, mask03UV).rgb;

                half3 tintBlend =
                    min(mask01.r.xxx, _Color01.rgb) * _Color01Power +
                    min(mask01.g.xxx, _Color02.rgb) * _Color02Power +
                    min(mask01.b.xxx, _Color03.rgb) * _Color03Power +
                    min(mask02.r.xxx, _Color04.rgb) * _Color04Power +
                    min(mask02.g.xxx, _Color05.rgb) * _Color05Power +
                    min(mask02.b.xxx, _Color06.rgb) * _Color06Power +
                    min(mask03.r.xxx, _Color07.rgb) * _Color07Power +
                    min(mask03.g.xxx, _Color08.rgb) * _Color08Power +
                    min(mask03.b.xxx, _Color09.rgb) * _Color09Power;

                half coverage = mask01.r + mask01.g + mask01.b
                    + mask02.r + mask02.g + mask02.b
                    + mask03.r + mask03.g + mask03.b;

                half3 tintedColor = saturate(baseColor * tintBlend);
                return lerp(baseColor, tintedColor, coverage);
            }

            half4 LitPassFragment(Varyings input) : SV_Target
            {
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = NormalizeNormalPerPixel(input.normalWS);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = SAMPLE_SHADOWMASK(input.lightmapUV);
                inputData.bakedGI = SAMPLE_GI(input.lightmapUV, input.vertexSH, inputData.normalWS);
                inputData.fogCoord = input.fogFactor;

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = GetTintedAlbedo(input.uv);
                surfaceData.normalTS = half3(0, 0, 1);

                float2 metallicUV = TRANSFORM_TEX(input.uv, _MetallicSmoothness);
                half4 metallicSmoothness = SAMPLE_TEXTURE2D(_MetallicSmoothness, sampler_MetallicSmoothness, metallicUV);
                surfaceData.metallic = metallicSmoothness.r;
                surfaceData.smoothness = metallicSmoothness.a;

                float2 emissionUV = TRANSFORM_TEX(input.uv, _Emission);
                half3 emissionColor = max(_EmissionPower.rgb, _EmissionColor.rgb);
                surfaceData.emission = SAMPLE_TEXTURE2D(_Emission, sampler_Emission, emissionUV).rgb * emissionColor;
                surfaceData.occlusion = 1.0h;
                surfaceData.alpha = 1.0h;

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            float3 _LightDirection;

            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output = (Varyings)0;

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);

                float4 positionCS = TransformWorldToHClip(
                    ApplyShadowBias(vertexInput.positionWS, normalInput.normalWS, _LightDirection)
                );

                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif

                output.positionCS = positionCS;
                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings DepthOnlyVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 DepthOnlyFragment(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
            };

            Varyings DepthNormalsVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = GetVertexNormalInputs(input.normalOS).normalWS;
                return output;
            }

            float4 DepthNormalsFragment(Varyings input) : SV_Target
            {
                return float4(PackNormalOctRectEncode(TransformWorldToViewDir(input.normalWS, true)), 0.0, 0.0);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
