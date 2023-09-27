Shader "Custom/ViewAngleBasedColorBlendHDR"
{
    Properties
    {
        _Color0 ("Color when parallel", Color) = (1, 0, 0, 1)
        _Color1 ("Color when perpendicular", Color) = (0, 0, 1, 1)
        _StartPoint ("Line Start Point", Vector) = (0, 0, 0, 0)
        _EndPoint ("Line End Point", Vector) = (1, 1, 1, 1)
        _Brightness ("Brightness", Range(1, 10)) = 1
        _Opacity ("Opacity", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100

        Blend SrcAlpha OneMinusSrcAlpha // Standard transparency
        ZWrite Off // Turn off Z-writing

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float3 viewDir : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            float4 _StartPoint;
            float4 _EndPoint;
            float4 _Color0;
            float4 _Color1;
            float _Brightness;
            float _Opacity;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.viewDir = normalize(_WorldSpaceCameraPos - v.vertex.xyz);
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                float3 lineDir = normalize(_EndPoint.xyz - _StartPoint.xyz);
                float angleCosine = abs(dot(lineDir, i.viewDir));

                half4 blendedColor = lerp(_Color1, _Color0, angleCosine);
                blendedColor.a *= _Opacity; // Adjust the alpha value using the opacity
                return blendedColor * _Brightness; // Multiply by brightness factor for HDR
            }
            ENDCG
        }
    }
}
