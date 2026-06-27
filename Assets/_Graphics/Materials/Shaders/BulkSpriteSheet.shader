Shader "CosmicShore/BulkSpriteSheet"
{
    Properties
    {
        _MainTex ("Sprite Sheet", 2D) = "white" {}
        _TintColor ("Tint", Color) = (1, 1, 1, 1)
        _Frame ("Frame", Float) = 0
        _Columns ("Columns", Float) = 4
        _Rows ("Rows", Float) = 3
        _Alpha ("Alpha", Range(0, 1)) = 1
        _Glow ("Glow", Range(0, 4)) = 1
        _BlackToAlpha ("Black To Alpha", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent+20" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed4 _TintColor;
            float _Frame;
            float _Columns;
            float _Rows;
            float _Alpha;
            float _Glow;
            float _BlackToAlpha;

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
                float columns = max(1.0, _Columns);
                float rows = max(1.0, _Rows);
                float frameCount = columns * rows;
                float frame = floor(fmod(max(0.0, _Frame), frameCount));
                float column = fmod(frame, columns);
                float rowFromTop = floor(frame / columns);
                float rowFromBottom = rows - 1.0 - rowFromTop;
                o.uv = float2((v.uv.x + column) / columns, (v.uv.y + rowFromBottom) / rows);
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 sample = tex2D(_MainTex, i.uv);
                float glowAlpha = saturate(max(sample.r, max(sample.g, sample.b)) * 1.35);
                sample.a = lerp(sample.a, glowAlpha, _BlackToAlpha);
                fixed4 color = sample * _TintColor * i.color;
                color.rgb *= _Glow;
                color.a = sample.a * _TintColor.a * _Alpha * i.color.a;
                return color;
            }
            ENDCG
        }
    }
    FallBack "Sprites/Default"
}
