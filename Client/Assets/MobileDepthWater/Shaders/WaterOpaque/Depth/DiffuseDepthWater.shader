Shader "Custom/Water/Depth/DiffuseWater"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _MainTex ("Texture", 2D) = "white" {}

        [Space(20)]
        _WaterColor ("Water color", Color) = (1, 1, 1, 1)
        _WaterTex ("Water texture", 2D) = "white" {}
        _Tiling ("Water tiling", Vector) = (1, 1, 1, 1)
        _TextureVisibility ("Texture visibility", Range(0, 1)) = 1

        [Space(20)]
        _DistTex ("Distortion", 2D) = "white" {}
        _DistTiling ("Distortion tiling", Vector) = (1, 1, 1, 1)

        [Space(20)]
        _WaterHeight ("Water height", Float) = 0
        _WaterDeep ("Water deep", Float) = 0
        _WaterDepth ("Water depth param", Range(0, 0.1)) = 0
        _WaterMinAlpha ("Water min alpha", Range(0, 1)) = 0

        [Space(20)]
        _BorderColor ("Border color", Color) = (1, 1, 1, 1)
        _BorderWidth ("Border width", Range(0, 1)) = 0

        [Space(20)]
        _MoveDirection ("Direction", Vector) = (0, 0, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        LOD 100

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex WaterDepthVertex
            #pragma fragment WaterDepthFragment
            #pragma multi_compile_fog
            #define WATER_USE_MAIN_TEX
            #include "Assets/MobileDepthWater/Shaders/WaterDepthURPCommon.hlsl"
            ENDHLSL
        }
    }
    FallBack Off
}
