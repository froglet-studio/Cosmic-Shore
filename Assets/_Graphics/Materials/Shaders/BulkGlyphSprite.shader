Shader "CosmicShore/BulkGlyphSprite"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.02, 0.04, 0.06, 0.9)
        _Color ("Color", Color) = (0.02, 0.04, 0.06, 0.9)
        _DarkColor ("Dark Color", Color) = (0, 0.004, 0.012, 0.95)
        _AccentColor ("Accent Color", Color) = (0.04, 0.95, 1, 0.75)
        _Alpha ("Alpha", Range(0, 1)) = 0.85
        _Pulse ("Pulse", Range(0, 4)) = 0
        _Phase ("Phase", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent+15" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _BaseColor;
            fixed4 _Color;
            fixed4 _DarkColor;
            fixed4 _AccentColor;
            float _Alpha;
            float _Pulse;
            float _Phase;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float time = _Time.y + _Phase;
                float border = 1.0 - smoothstep(0.0, 0.08, min(min(uv.x, uv.y), min(1.0 - uv.x, 1.0 - uv.y)));
                float scan = step(0.62, frac(uv.y * 9.0 + sin(uv.x * 15.0 + time * 1.7) * 0.18 + time * 0.42));
                float circuit = smoothstep(0.026, 0.0, abs(frac(uv.x * 4.0 + time * 0.07) - 0.5) - 0.18);
                float notch = step(0.78, frac((uv.x + uv.y) * 5.0 + sin(time * 0.8)));
                float glyph = saturate(border * 0.72 + scan * 0.46 + circuit * 0.62 + notch * 0.28);
                float vignette = smoothstep(0.0, 0.35, uv.x) * smoothstep(0.0, 0.35, uv.y) *
                                 smoothstep(0.0, 0.35, 1.0 - uv.x) * smoothstep(0.0, 0.35, 1.0 - uv.y);

                fixed4 color;
                color.rgb = lerp(_DarkColor.rgb, _BaseColor.rgb, 0.38 + _Pulse * 0.08);
                color.rgb = lerp(color.rgb, _AccentColor.rgb * (0.55 + _Pulse * 0.22), glyph * 0.34);
                color.a = saturate(_Alpha * vignette * (0.18 + glyph * 0.78));
                return color * i.color;
            }
            ENDCG
        }
    }
    FallBack "Sprites/Default"
}
