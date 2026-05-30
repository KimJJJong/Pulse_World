#ifndef MOBILE_DEPTH_WATER_URP_COMMON_INCLUDED
#define MOBILE_DEPTH_WATER_URP_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);
TEXTURE2D(_WaterTex);
SAMPLER(sampler_WaterTex);
TEXTURE2D(_DistTex);
SAMPLER(sampler_DistTex);

CBUFFER_START(UnityPerMaterial)
float4 _MainTex_ST;
half4 _Color;
half4 _WaterColor;
float4 _Tiling;
half _TextureVisibility;
float4 _DistTiling;
float _WaterHeight;
float _WaterDeep;
float _WaterDepth;
float _WaterMinAlpha;
half4 _BorderColor;
float _BorderWidth;
float4 _MoveDirection;
CBUFFER_END

struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 uv : TEXCOORD0;
};

struct Varyings
{
    float2 uv : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    float camHeightOverWater : TEXCOORD2;
    float waterDepth : TEXCOORD3;
    half3 diffuseLight : TEXCOORD4;
    half fogFactor : TEXCOORD5;
    float4 positionCS : SV_POSITION;
};

float SafeDenominator(float value)
{
    return abs(value) < 0.0001 ? (value < 0.0 ? -0.0001 : 0.0001) : value;
}

half3 DiffuseLight(float3 normalWS)
{
    normalWS = normalize(normalWS);

    Light mainLight = GetMainLight();
    half mainLightAmount = saturate(dot(normalWS, mainLight.direction));
    half3 directLight = mainLight.color * mainLightAmount;
    half3 ambientLight = SampleSH(normalWS);

    return max(directLight + ambientLight, half3(0.12, 0.12, 0.12));
}

float2 WaterPlaneUV(float3 positionWS, float camHeightOverWater)
{
    float3 cameraPositionWS = GetCameraPositionWS();
    float3 camToWorldRay = positionWS - cameraPositionWS;
    float3 rayToWaterPlane = camHeightOverWater / SafeDenominator(camToWorldRay.y) * camToWorldRay;
    return rayToWaterPlane.xz - cameraPositionWS.xz;
}

Varyings WaterDepthVertex(Attributes input)
{
    Varyings output;

    output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
    output.positionCS = TransformWorldToHClip(output.positionWS);
    output.uv = input.uv * _MainTex_ST.xy + _MainTex_ST.zw;

    float3 cameraPositionWS = GetCameraPositionWS();
    float3 camToWorldRay = output.positionWS - cameraPositionWS;
    output.camHeightOverWater = cameraPositionWS.y - _WaterHeight;

    float3 rayToWaterPlane = output.camHeightOverWater / SafeDenominator(-camToWorldRay.y) * camToWorldRay;
    float depth = length(camToWorldRay - rayToWaterPlane);
    output.waterDepth = depth * _WaterDepth * saturate(rayToWaterPlane.y - camToWorldRay.y);

    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
    output.diffuseLight = DiffuseLight(normalWS);

    float3 worldPosOnPlane = cameraPositionWS + rayToWaterPlane;
    float3 positionForFog = lerp(worldPosOnPlane, output.positionWS, output.positionWS.y > _WaterHeight);
    output.fogFactor = ComputeFogFactor(TransformWorldToHClip(positionForFog).z);

    return output;
}

half4 MainColor(Varyings input)
{
#if defined(WATER_USE_MAIN_TEX)
    half4 mainColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _Color;
#else
    half4 mainColor = _Color;
#endif

    mainColor.rgb *= input.diffuseLight;
    return mainColor;
}

half4 WaterDepthFragment(Varyings input) : SV_Target
{
    float lengthUnderWater = max(0.0, _WaterHeight - input.positionWS.y);
    half underWater = lengthUnderWater > 0.0 ? 1.0h : 0.0h;
    half borderAlpha = lerp(underWater * _BorderColor.a, 0.0h, saturate(lengthUnderWater / max(_BorderWidth, 0.0001)));
    half waterAlpha = saturate(lengthUnderWater / max(_WaterDeep, 0.0001) + _WaterMinAlpha + input.waterDepth);

    half4 mainColor = MainColor(input);

    float2 waterUV = WaterPlaneUV(input.positionWS, input.camHeightOverWater);
    half4 distortion = SAMPLE_TEXTURE2D(_DistTex, sampler_DistTex, waterUV * _DistTiling.xy) * 2.0h - 1.0h;
    float2 distortedUV = ((waterUV + distortion.rg) - _Time.y * _MoveDirection.xz) * _Tiling.xy;

    half4 waterColor = SAMPLE_TEXTURE2D(_WaterTex, sampler_WaterTex, distortedUV);
    waterColor = lerp(_WaterColor, half4(1.0h, 1.0h, 1.0h, 1.0h), waterColor.r * _TextureVisibility);

    half4 finalColor = lerp(mainColor, waterColor, _WaterColor.a * waterAlpha * underWater);
    finalColor.rgb = lerp(finalColor.rgb, _BorderColor.rgb, borderAlpha);
    finalColor.rgb = MixFog(finalColor.rgb, input.fogFactor);

    return finalColor;
}

#endif
