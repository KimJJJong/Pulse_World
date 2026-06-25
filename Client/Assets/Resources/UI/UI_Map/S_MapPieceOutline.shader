Shader "UI/RhythmRPG/Map Piece Outline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 0.78, 0.18, 1)
        _OutlineWidth ("Outline Width", Range(0.5, 8)) = 2.5
        _OutlineAlpha ("Outline Alpha", Range(0, 1)) = 0.92
        _FillAlpha ("Fill Alpha", Range(0, 1)) = 0.16

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            float4 _ClipRect;
            float _OutlineWidth;
            fixed _OutlineAlpha;
            fixed _FillAlpha;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed SampleAlpha(float2 uv)
            {
                return tex2D(_MainTex, uv).a;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                const float diagonal = 0.70710678;
                float2 texel = _MainTex_TexelSize.xy * max(_OutlineWidth, 0.5);

                fixed center = SampleAlpha(IN.texcoord);
                fixed maxNeighbor = center;
                maxNeighbor = max(maxNeighbor, SampleAlpha(IN.texcoord + float2(texel.x, 0)));
                maxNeighbor = max(maxNeighbor, SampleAlpha(IN.texcoord + float2(-texel.x, 0)));
                maxNeighbor = max(maxNeighbor, SampleAlpha(IN.texcoord + float2(0, texel.y)));
                maxNeighbor = max(maxNeighbor, SampleAlpha(IN.texcoord + float2(0, -texel.y)));
                maxNeighbor = max(maxNeighbor, SampleAlpha(IN.texcoord + float2(texel.x, texel.y) * diagonal));
                maxNeighbor = max(maxNeighbor, SampleAlpha(IN.texcoord + float2(-texel.x, texel.y) * diagonal));
                maxNeighbor = max(maxNeighbor, SampleAlpha(IN.texcoord + float2(texel.x, -texel.y) * diagonal));
                maxNeighbor = max(maxNeighbor, SampleAlpha(IN.texcoord + float2(-texel.x, -texel.y) * diagonal));

                fixed outsideOutline = saturate((maxNeighbor - center) * 4.0);
                fixed fill = center * _FillAlpha;
                fixed alpha = saturate(fill + outsideOutline * _OutlineAlpha) * IN.color.a;

                fixed4 color = fixed4(IN.color.rgb, alpha);

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }
}
