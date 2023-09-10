Shader"Custom/VolumeDisplayShader"
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
        _GapThickness ("GapThickness", Range(0, 0.5)) = 0.05
        _DividerThickness ("Divider Thickness", Range(0, 0.3)) = 0.01
        _OuterGlowIntensity ("Outer Glow Intensity", Range(0, 1)) = 0.5
        _OuterGlowDistance ("Outer Glow Distance", Range(0, 0.1)) = 0.05
        _InnerGlowIntensity ("Inner Glow Intensity", Range(0, 1)) = 0.5
        _InnerGlowDistance ("Inner Glow Distance", Range(0, 0.1)) = 0.05
        _ArcGlowDistance ("Arc Glow Distance", Range(0, 0.1)) = 0.05
        _ArcGlowIntensity ("Arc Glow Intensity", Range(0, 1)) = 0.05
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

                        // Shader properties
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
            float _InnerGlowIntensity;
            float _InnerGlowDistance;
            float _ArcGlowDistance;
            float _ArcGlowIntensity;


            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float ComputeGlowStrength(float distance, float maxDistance)
            {
                return max(0.0, 1.0 - distance / maxDistance);
            }

            float remapRadius(float value, float arcGlowDistance)
            {
                return value * (1.0 -  2 * arcGlowDistance) + .7f * arcGlowDistance;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                _Radius1 = remapRadius(_Radius1, _ArcGlowDistance);
                _Radius2 = remapRadius(_Radius2, _ArcGlowDistance);
                _Radius3 = remapRadius(_Radius3, _ArcGlowDistance);
    
                float scaleFactor = 1.0 / (1.0 + _OuterGlowDistance);
                float2 scaledUV = i.uv * scaleFactor + (1.0 - scaleFactor) * 0.5;
                float2 centeredUV = (float2(1.0 - i.uv.x, i.uv.y) * 2) - 1;
                float angle = atan2(centeredUV.y, centeredUV.x);
                if (angle < 0.0)
                    angle += 6.28318530718; // Add 2*pi if negative
                float dist = length(centeredUV);

                // Determine dominant color for border
                float4 dominantColor;
                if (_Radius1 >= _Radius2 && _Radius1 >= _Radius3)
                    dominantColor = _Color1;
                else if (_Radius2 >= _Radius1 && _Radius2 >= _Radius3)
                    dominantColor = _Color2;
                else
                    dominantColor = _Color3;

                float borderStart = 1.0 * scaleFactor - _BorderThickness;
                float borderEnd = 1.0 * scaleFactor;

                if (dist > borderStart && dist <= borderEnd)
                    return dominantColor;

                        if (dist > borderEnd && dist <= borderEnd + _OuterGlowDistance * scaleFactor)
                return dominantColor * _OuterGlowIntensity * ComputeGlowStrength(dist - borderEnd, _OuterGlowDistance);

                        if (dist > borderStart - _InnerGlowDistance * scaleFactor && dist <= borderStart)
                return dominantColor * _InnerGlowIntensity * ComputeGlowStrength(borderStart - dist, _InnerGlowDistance);

                if (dist > borderStart - _GapThickness * scaleFactor && dist <= borderStart)
                    return fixed4(0, 0, 0, 0);

                float maxDist = borderStart - _GapThickness * scaleFactor;
                float distanceToOuterEdge = maxDist - dist;
                float distanceToInnerEdge;
                float distanceToDivider;
                float4 segmentColor;

                float thirdOfCircle = 2.09439510239; // 2*pi/3
                float twoThirdsOfCircle = 4.18879020478; // 4*pi/3
    
                // Adjust the distanceToDivider by scaling with the distance from the center
    

                if (angle < thirdOfCircle && dist <= maxDist - _GapThickness * scaleFactor && dist > maxDist - _Radius1)
                {
                    distanceToDivider = min(angle, thirdOfCircle - angle);
                    distanceToInnerEdge = dist - (maxDist - _Radius1);
                    segmentColor = _Color1;
                }
                else if (angle < twoThirdsOfCircle && angle > thirdOfCircle && dist <= maxDist - _GapThickness * scaleFactor && dist > maxDist - _Radius2)
                {
                    distanceToDivider = min(angle - thirdOfCircle, twoThirdsOfCircle - angle);
                    distanceToInnerEdge = dist - (maxDist - _Radius2);
                    segmentColor = _Color2;
                }
                else if (angle > twoThirdsOfCircle && dist <= maxDist - _GapThickness * scaleFactor && dist > maxDist - _Radius3)
                {
                    distanceToDivider = min(angle - twoThirdsOfCircle, 6.28318530718 - angle);
                    distanceToInnerEdge = dist - (maxDist - _Radius3);
                    segmentColor = _Color3;
                }
                else
                {
                    return fixed4(0, 0, 0, 0);
                }
                
                distanceToDivider = distanceToDivider * dist;
    
                float outerArcStrength = _ArcGlowIntensity * ComputeGlowStrength(distanceToOuterEdge, _ArcGlowDistance);
                float innerArcStrength = ComputeGlowStrength(distanceToInnerEdge, _ArcGlowDistance);
                float dividerStrength = ComputeGlowStrength(distanceToDivider, _DividerThickness / 2.0);
                float overallStrength = max(max(outerArcStrength, innerArcStrength), dividerStrength);

                return lerp(segmentColor, fixed4(0, 0, 0, 0), overallStrength);
            }

            ENDCG
        }
    }
}