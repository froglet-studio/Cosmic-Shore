Shader "CosmicShore/BulkEnergyUnlit"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.2, 1, 0.8, 1)
        _Color ("Color", Color) = (0.2, 1, 0.8, 1)
        _EmissionColor ("Emission Color", Color) = (0.2, 1, 0.8, 1)
        _Alpha ("Alpha", Range(0, 1)) = 1
        _Pulse ("Pulse", Range(0, 4)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
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
            fixed4 _EmissionColor;
            float _Alpha;
            float _Pulse;

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
                float stripe = sin((i.uv.x * 31.0 + _Time.y * 5.5) * 6.2831853) * 0.5 + 0.5;
                float glow = 0.82 + _Pulse * 0.28 + stripe * 0.12;
                fixed4 color = lerp(_BaseColor, _EmissionColor, saturate(0.28 + _Pulse * 0.12));
                color.rgb *= glow;
                color.a = saturate(_Alpha * _BaseColor.a * (0.62 + _Pulse * 0.08 + stripe * 0.08));
                return color * i.color;
            }
            ENDCG
        }
    }
    FallBack "Unlit/Color"
}
