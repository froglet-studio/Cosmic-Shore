Shader "UI/Gradient" {
    Properties {
        _Color ("Top Color", Color) = (0,0,0,1)
        _FadeHeight ("Fade Height", Float) = 100
    }
    SubShader {
        Tags {"Queue"="Transparent" "RenderType"="Transparent"}
        LOD 100

        CGPROGRAM
        #pragma surface surf Lambert alpha

        fixed4 _Color;
        float _FadeHeight;

        struct Input {
            float2 uv_MainTex;
        };

        void surf (Input IN, inout SurfaceOutput o) {
            float alpha = 1 - (IN.uv_MainTex.y * _FadeHeight);
            o.Albedo = _Color.rgb;
            o.Alpha = saturate(alpha);
        }
        ENDCG
    }
    FallBack "Diffuse"
}
