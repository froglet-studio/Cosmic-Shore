
Shader "Custom/VolumeDisplayShader"
{
    Properties
    {
        _Color1 ("Color1", Color) = (1, 0, 0, 1)
        _Color2 ("Color2", Color) = (0, 1, 0, 1)
        _Color3 ("Color3", Color) = (0, 0, 1, 1)
        _Radius1 ("Radius1", Range(0, 1)) = 0.5
        _Radius2 ("Radius2", Range(0, 1)) = 0.5
        _Radius3 ("Radius3", Range(0, 1)) = 0.5
        _BorderThickness ("BorderThickness", Range(0, 0.1)) = 0.05
        _GapThickness ("GapThickness", Range(0, 0.1)) = 0.05
        _DividerThickness ("Divider Thickness", Range(0, 0.05)) = 0.01
        _OuterGlowIntensity ("Outer Glow Intensity", Range(0, 1)) = 0.5
        _OuterGlowDistance ("Outer Glow Distance", Range(0, 0.1)) = 0.05
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        
        // Blend settings for URP transparency
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
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            float4 _Color1;
            float4 _Color2;
            float4 _Color3;
            float _Radius1;
            float _Radius2;
            float _Radius3;
            float _BorderThickness;
            float _GapThickness;
            float _DividerThickness;
            float _OuterGlowIntensity;
            float _OuterGlowDistance;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Convert UV to centered coordinates
                float2 mirroredUV = float2(1.0 - i.uv.x, i.uv.y);
float2 centeredUV = mirroredUV * 2 - 1;

                // Compute angle
                float angle = atan2(centeredUV.y, centeredUV.x);
                if (angle < 0.0) angle += 6.28318530718; // Add 2*pi if negative
                
                // Compute distance from center
                float dist = length(centeredUV);

                // Determine dominant color for border
                float4 dominantColor = _Color1;
                float maxRadius = _Radius1;
                if (_Radius2 > maxRadius)
                {
                    dominantColor = _Color2;
                    maxRadius = _Radius2;
                }
                if (_Radius3 > maxRadius)
                {
                    dominantColor = _Color3;
                    maxRadius = _Radius3;
                }

                // If within the border range
                float borderStart = 1.0 - _BorderThickness;
                float borderEnd = 1.0;
                if (dist > borderStart && dist <= borderEnd)
                    return dominantColor;

                // Outer glow effect
                float glowStart = borderEnd;
                float glowEnd = glowStart + _OuterGlowDistance;
                if (dist > glowStart && dist <= glowEnd)
                {
                    float glowFactor = 1.0 - (dist - glowStart) / _OuterGlowDistance;
                    return dominantColor * _OuterGlowIntensity * glowFactor;
                }

                // If within the gap, return transparent
                float gapStart = borderStart - _GapThickness;
                if (dist > gapStart && dist <= borderStart)
                    return fixed4(0, 0, 0, 0);

                // Determine segment based on angle
                float thirdOfCircle = 2.09439510239; // 2*pi/3
                float twoThirdsOfCircle = 4.18879020478; // 4*pi/3

                float maxDist = gapStart;
                float segmentDivider = _DividerThickness / 2.0;

                if (angle < thirdOfCircle)
                {
                    if (angle > thirdOfCircle - segmentDivider || angle < segmentDivider)
                        return fixed4(0, 0, 0, 0);
                    if (dist <= maxDist && dist > maxDist - _Radius1)
                        return _Color1;
                }
                else if (angle < twoThirdsOfCircle)
                {
                    if (angle > twoThirdsOfCircle - segmentDivider || angle < thirdOfCircle + segmentDivider)
                        return fixed4(0, 0, 0, 0);
                    if (dist <= maxDist && dist > maxDist - _Radius2)
                        return _Color2;
                }
                else
                {
                    if (angle < twoThirdsOfCircle + segmentDivider || angle > 6.28318530718 - segmentDivider)
                        return fixed4(0, 0, 0, 0);
                    if (dist <= maxDist && dist > maxDist - _Radius3)
                        return _Color3;
                }

                // Default (transparent)
                return fixed4(0, 0, 0, 0);
            }
            ENDCG
        }
    }
}
