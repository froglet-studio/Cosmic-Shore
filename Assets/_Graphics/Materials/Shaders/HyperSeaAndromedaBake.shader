Shader "Hidden/CosmicShore/HyperSeaAndromedaBake"
{
    Properties
    {
        _AndromedaSize ("Size", Float) = 0.1
        [HDR]_AndromedaDiskColor ("Disk Color", Color) = (0.3, 0.35, 0.55, 1)
        [HDR]_AndromedaNucleusColor ("Nucleus Color", Color) = (0.6, 0.55, 0.4, 1)
        _AndromedaBrightness ("Brightness", Float) = 1.0
    }

    SubShader
    {
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            half _AndromedaSize;
            half4 _AndromedaDiskColor;
            half4 _AndromedaNucleusColor;
            half _AndromedaBrightness;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            float hash31(float3 p)
            {
                p = frac(p * float3(0.1031, 0.1030, 0.0973));
                p += dot(p, p.yxz + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float valueNoise(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float n000 = hash31(i);
                float n100 = hash31(i + float3(1, 0, 0));
                float n010 = hash31(i + float3(0, 1, 0));
                float n110 = hash31(i + float3(1, 1, 0));
                float n001 = hash31(i + float3(0, 0, 1));
                float n101 = hash31(i + float3(1, 0, 1));
                float n011 = hash31(i + float3(0, 1, 1));
                float n111 = hash31(i + float3(1, 1, 1));

                return lerp(
                    lerp(lerp(n000, n100, f.x), lerp(n010, n110, f.x), f.y),
                    lerp(lerp(n001, n101, f.x), lerp(n011, n111, f.x), f.y),
                    f.z
                );
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                // Map UV to Andromeda's local coordinate frame
                float extent = _AndromedaSize * 1.5;
                float u = (i.uv.x - 0.5) * 2.0 * extent;
                float v = (i.uv.y - 0.5) * 2.0 * extent;

                // Inclined elliptical disk (~77 deg tilt)
                float eu = u;
                float ev = v * 3.2;
                float r2 = eu * eu + ev * ev;
                float size2 = _AndromedaSize * _AndromedaSize;

                // Outer disk with smooth falloff
                float disk = exp(-r2 / (size2 * 0.25));

                // Bright compact nucleus
                float nucleus = exp(-r2 / (size2 * 0.008));

                // Spiral arms via log-spiral coordinates (continuous, no atan2 seam)
                float r = sqrt(r2);
                float spiralPhase = atan2(ev, eu) * 2.0 + r * 15.0 / _AndromedaSize;
                float spiral = valueNoise(float3(
                    sin(spiralPhase),
                    cos(spiralPhase),
                    r * 10.0 / _AndromedaSize + 3.7
                ));
                disk *= (0.4 + spiral * 0.6);

                // Dust lane across the minor axis
                float dustLane = 1.0 - 0.35 * exp(-ev * ev / (size2 * 0.003));
                disk *= dustLane;

                half3 color = disk * _AndromedaDiskColor.rgb
                            + nucleus * _AndromedaNucleusColor.rgb;

                return half4(color * _AndromedaBrightness, 1.0);
            }

            ENDCG
        }
    }
}
