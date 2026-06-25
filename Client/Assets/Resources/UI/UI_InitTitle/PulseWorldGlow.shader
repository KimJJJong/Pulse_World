Shader "RhythmRPG/UI/PulseWorldGlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Graphic Color", Color) = (1, 1, 1, 1)
        _TintColor ("Glow Tint", Color) = (0.1, 1, 0.95, 1)
        _Intensity ("Intensity", Range(0, 2)) = 0.7
        _Sparkle ("Sparkle", Range(0, 1)) = 0.25
        _TimeOffset ("Time Offset", Float) = 0
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

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha One

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TintColor;
            float _Intensity;
            float _Sparkle;
            float _TimeOffset;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                o.color = v.color * _Color * _TintColor;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv) * i.color;
                float t = _Time.y + _TimeOffset;
                float ribbon = sin((i.uv.x * 18.0) + (i.uv.y * 11.0) + t * 3.4) * 0.5 + 0.5;
                float pulse = sin(t * 2.2 + i.uv.x * 5.0) * 0.5 + 0.5;
                float glitter = smoothstep(0.82, 1.0, ribbon) * smoothstep(0.35, 1.0, pulse) * _Sparkle;
                float alpha = tex.a * saturate((_Intensity * 0.7) + glitter);
                tex.rgb *= _Intensity + glitter * 1.75;
                tex.a = alpha;
                return tex;
            }
            ENDCG
        }
    }
}
