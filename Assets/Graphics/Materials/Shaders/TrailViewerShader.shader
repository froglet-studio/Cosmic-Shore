// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

Shader "Custom/ViewAngleBasedColorBlendHDR"
{
    Properties
    {
        _Color0 ("Color when parallel", Color) = (1, 0, 0, 1)
        _Color1 ("Color when perpendicular", Color) = (0, 0, 1, 1)
        _Brightness ("Brightness", Range(1, 10)) = 1
        _Opacity ("Opacity", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

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
                float3 worldPos : TEXCOORD1;  // Pass world position
                float4 vertex : SV_POSITION;
            };

            float4 _Color0;
            float4 _Color1;
            float _Brightness;
            float _Opacity;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;  // Compute world position
                o.viewDir = normalize(_WorldSpaceCameraPos - o.worldPos);
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                float3 lineDir = normalize(ddx(i.worldPos) + ddy(i.worldPos));

                float angleCosine = abs(dot(lineDir, i.viewDir));
                half4 blendedColor = lerp(_Color1, _Color0, angleCosine);
                blendedColor.a *= _Opacity;
                return blendedColor * _Brightness;
                return _Color0;
            }
            ENDCG
        }
    }
}