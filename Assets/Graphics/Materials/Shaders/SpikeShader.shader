Shader "Custom/LineFresnelShader"
{
    Properties
    {
        _Color ("Main Color", Color) = (1, 1, 1, 1)
        _BlendColor ("Blend Color", Color) = (0, 0, 0, 1)
        _OverlayIntensity ("Overlay Intensity", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 otherVertex : TEXCOORD1; // The other endpoint of the line
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 viewPos : TEXCOORD0;
                float3 viewOtherPos : TEXCOORD1;
            };

            float4 _Color;
            float4 _BlendColor;
            float _OverlayIntensity;

            v2f vert(appdata v)
            {
                v2f o;
                o.viewPos = mul(UNITY_MATRIX_V, mul(unity_ObjectToWorld, v.vertex)).xyz;
                o.viewOtherPos = mul(UNITY_MATRIX_V, mul(unity_ObjectToWorld, v.otherVertex)).xyz;
                o.vertex = UnityObjectToClipPos(v.vertex);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                float3 viewDirection = -normalize(i.viewPos);
                float3 lineDirection = normalize(i.viewOtherPos - i.viewPos);
    
                float fresnelValue = abs(dot(lineDirection, viewDirection));

                half4 col = lerp(_BlendColor, _Color, fresnelValue);
                return col;
            }
            ENDCG
        }
    }
}
