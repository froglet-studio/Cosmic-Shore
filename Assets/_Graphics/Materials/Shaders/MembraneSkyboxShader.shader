Shader "CosmicShore/MembraneSkybox"
{
    Properties
    {
        [Header(Surface)]
        [HDR]_GradientColor ("Gradient Color", Color) = (0.14, 0.21, 0.62, 1)
        _EdgeBlend ("Edge Blend", Range(0, 1)) = 0.9
        _GradientEdge ("Gradient Edge", Range(0, 1)) = 0.16
        _FresnelPower ("Fresnel Power", Range(0.5, 8)) = 3.0

        [Header(Vertex Displacement)]
        _Amplitude ("Amplitude", Range(0, 0.1)) = 0.02
        _Frequency ("Frequency", Range(0.1, 5)) = 0.58
        _DisplacementSpeed ("Displacement Speed", Range(0, 2)) = 0.3

        [Header(Ripple)]
        _RippleDensity ("Ripple Density", Range(0.1, 20)) = 5.69
        _RippleOrigin ("Ripple Origin", Vector) = (0, 0, 0, 0)
        _EffectRadius ("Effect Radius", Range(0, 20)) = 8.66

        [Header(Alpha Pores)]
        _PoreNoiseScale ("Pore Noise Scale", Range(0.5, 20)) = 4.0
        _PoreSpeed ("Pore Drift Speed", Range(0, 1)) = 0.08
        _PoreThreshold ("Pore Threshold", Range(0, 1)) = 0.42
        _PoreEdgeSoftness ("Pore Edge Softness", Range(0.01, 0.3)) = 0.08
        _FresnelOpacityPower ("Fresnel Opacity Power", Range(0.5, 8)) = 2.5
        _FresnelOpacityStrength ("Fresnel Opacity Strength", Range(0, 1)) = 0.85

        [Header(Emission)]
        [HDR]_HDREmission ("HDR Emission", Color) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
        }

        Pass
        {
            Name "MembraneSkybox"
            Cull Off
            ZWrite On
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 objectPos : TEXCOORD2;
                float noiseVal : TEXCOORD3;
                UNITY_FOG_COORDS(4)
            };

            // Surface
            float4 _GradientColor;
            float _EdgeBlend;
            float _GradientEdge;
            float _FresnelPower;

            // Vertex displacement
            float _Amplitude;
            float _Frequency;
            float _DisplacementSpeed;

            // Ripple
            float _RippleDensity;
            float4 _RippleOrigin;
            float _EffectRadius;

            // Alpha pores
            float _PoreNoiseScale;
            float _PoreSpeed;
            float _PoreThreshold;
            float _PoreEdgeSoftness;
            float _FresnelOpacityPower;
            float _FresnelOpacityStrength;

            // Emission
            float4 _HDREmission;

            // ---- Noise functions ----
            // Classic 3D gradient noise (Perlin-style)
            float3 mod289(float3 x) { return x - floor(x / 289.0) * 289.0; }
            float4 mod289(float4 x) { return x - floor(x / 289.0) * 289.0; }
            float4 permute(float4 x) { return mod289((x * 34.0 + 1.0) * x); }
            float4 taylorInvSqrt(float4 r) { return 1.79284291400159 - r * 0.85373472095314; }
            float3 fade(float3 t) { return t * t * t * (t * (t * 6.0 - 15.0) + 10.0); }

            float cnoise(float3 P)
            {
                float3 Pi0 = floor(P);
                float3 Pi1 = Pi0 + 1.0;
                Pi0 = mod289(Pi0);
                Pi1 = mod289(Pi1);
                float3 Pf0 = frac(P);
                float3 Pf1 = Pf0 - 1.0;
                float4 ix = float4(Pi0.x, Pi1.x, Pi0.x, Pi1.x);
                float4 iy = float4(Pi0.y, Pi0.y, Pi1.y, Pi1.y);
                float4 iz0 = float4(Pi0.z, Pi0.z, Pi0.z, Pi0.z);
                float4 iz1 = float4(Pi1.z, Pi1.z, Pi1.z, Pi1.z);

                float4 ixy = permute(permute(ix) + iy);
                float4 ixy0 = permute(ixy + iz0);
                float4 ixy1 = permute(ixy + iz1);

                float4 gx0 = ixy0 / 7.0;
                float4 gy0 = frac(floor(gx0) / 7.0) - 0.5;
                gx0 = frac(gx0);
                float4 gz0 = 0.5 - abs(gx0) - abs(gy0);
                float4 sz0 = step(gz0, 0.0);
                gx0 -= sz0 * (step(0.0, gx0) - 0.5);
                gy0 -= sz0 * (step(0.0, gy0) - 0.5);

                float4 gx1 = ixy1 / 7.0;
                float4 gy1 = frac(floor(gx1) / 7.0) - 0.5;
                gx1 = frac(gx1);
                float4 gz1 = 0.5 - abs(gx1) - abs(gy1);
                float4 sz1 = step(gz1, 0.0);
                gx1 -= sz1 * (step(0.0, gx1) - 0.5);
                gy1 -= sz1 * (step(0.0, gy1) - 0.5);

                float3 g000 = float3(gx0.x, gy0.x, gz0.x);
                float3 g100 = float3(gx0.y, gy0.y, gz0.y);
                float3 g010 = float3(gx0.z, gy0.z, gz0.z);
                float3 g110 = float3(gx0.w, gy0.w, gz0.w);
                float3 g001 = float3(gx1.x, gy1.x, gz1.x);
                float3 g101 = float3(gx1.y, gy1.y, gz1.y);
                float3 g011 = float3(gx1.z, gy1.z, gz1.z);
                float3 g111 = float3(gx1.w, gy1.w, gz1.w);

                float4 norm0 = taylorInvSqrt(float4(dot(g000,g000), dot(g010,g010), dot(g100,g100), dot(g110,g110)));
                g000 *= norm0.x; g010 *= norm0.y; g100 *= norm0.z; g110 *= norm0.w;
                float4 norm1 = taylorInvSqrt(float4(dot(g001,g001), dot(g011,g011), dot(g101,g101), dot(g111,g111)));
                g001 *= norm1.x; g011 *= norm1.y; g101 *= norm1.z; g111 *= norm1.w;

                float n000 = dot(g000, Pf0);
                float n100 = dot(g100, float3(Pf1.x, Pf0.y, Pf0.z));
                float n010 = dot(g010, float3(Pf0.x, Pf1.y, Pf0.z));
                float n110 = dot(g110, float3(Pf1.x, Pf1.y, Pf0.z));
                float n001 = dot(g001, float3(Pf0.x, Pf0.y, Pf1.z));
                float n101 = dot(g101, float3(Pf1.x, Pf0.y, Pf1.z));
                float n011 = dot(g011, float3(Pf0.x, Pf1.y, Pf1.z));
                float n111 = dot(g111, Pf1);

                float3 fade_xyz = fade(Pf0);
                float4 n_z = lerp(float4(n000, n100, n010, n110), float4(n001, n101, n011, n111), fade_xyz.z);
                float2 n_yz = lerp(n_z.xy, n_z.zw, fade_xyz.y);
                float n_xyz = lerp(n_yz.x, n_yz.y, fade_xyz.x);

                return n_xyz * 2.0 - 1.0;
            }

            // Gradient noise 2D (for pore pattern — cheaper than 3D)
            float2 gradientNoiseDir(float2 p)
            {
                p = fmod(p, 289.0);
                float x = fmod((34.0 * p.x + 1.0) * p.x, 289.0) + p.y;
                x = fmod((34.0 * x + 1.0) * x, 289.0);
                x = frac(x / 41.0) * 2.0 - 1.0;
                return normalize(float2(x - floor(x + 0.5), abs(x) - 0.5));
            }

            float gradientNoise2D(float2 p)
            {
                float2 ip = floor(p);
                float2 fp = frac(p);
                float d00 = dot(gradientNoiseDir(ip), fp);
                float d01 = dot(gradientNoiseDir(ip + float2(0, 1)), fp - float2(0, 1));
                float d10 = dot(gradientNoiseDir(ip + float2(1, 0)), fp - float2(1, 0));
                float d11 = dot(gradientNoiseDir(ip + float2(1, 1)), fp - float2(1, 1));
                fp = fp * fp * fp * (fp * (fp * 6.0 - 15.0) + 10.0);
                return lerp(lerp(d00, d01, fp.y), lerp(d10, d11, fp.y), fp.x) + 0.5;
            }

            v2f vert(appdata v)
            {
                v2f o;

                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 worldNormal = normalize(mul((float3x3)unity_ObjectToWorld, v.normal));

                // Perlin noise displacement along normal
                float time = _Time.y * _DisplacementSpeed;
                float3 noiseCoord = worldPos * _Frequency + float3(time, time * 0.7, time * 0.3);
                float noise = cnoise(noiseCoord);
                o.noiseVal = noise * 0.5 + 0.5;

                // Ripple modulation
                float distFromOrigin = length(worldPos - _RippleOrigin.xyz);
                float ripple = sin(distFromOrigin * _RippleDensity + _Time.y * 2.0);
                float rippleMask = saturate(1.0 - distFromOrigin / _EffectRadius);

                float displacement = noise * _Amplitude + ripple * _Amplitude * 0.3 * rippleMask;
                v.vertex.xyz += v.normal * displacement;

                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = worldNormal;
                o.objectPos = v.vertex.xyz;

                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);
                float3 normal = normalize(i.worldNormal);

                // Fresnel for edge glow (containment cue)
                float fresnel = pow(1.0 - saturate(dot(viewDir, normal)), _FresnelPower);

                // Surface color: gradient noise driven
                float gradientMask = smoothstep(_GradientEdge, _GradientEdge + _EdgeBlend, fresnel);
                float3 surfaceColor = lerp(float3(0.01, 0.01, 0.02), _GradientColor.rgb, gradientMask);
                surfaceColor += _HDREmission.rgb;

                // Subtle noise-based color variation
                surfaceColor += i.noiseVal * 0.05 * _GradientColor.rgb;

                // ---- Pore alpha (noise-driven cutout) ----
                // Use spherical UV derived from object-space normal for stable pore pattern
                float3 objNorm = normalize(i.objectPos);
                float u = atan2(objNorm.z, objNorm.x) / (2.0 * UNITY_PI) + 0.5;
                float v_coord = asin(saturate(objNorm.y * 0.999)) / UNITY_PI + 0.5;
                float2 poreUV = float2(u, v_coord) * _PoreNoiseScale;

                // Animate the pore pattern
                float poreTime = _Time.y * _PoreSpeed;
                poreUV += float2(poreTime * 0.37, poreTime * 0.53);

                float poreNoise = gradientNoise2D(poreUV);

                // Second octave for organic feel
                float poreNoise2 = gradientNoise2D(poreUV * 2.13 + float2(3.7, 1.2));
                poreNoise = poreNoise * 0.7 + poreNoise2 * 0.3;

                // Smoothstep cutoff for pore alpha
                float poreAlpha = smoothstep(
                    _PoreThreshold - _PoreEdgeSoftness,
                    _PoreThreshold + _PoreEdgeSoftness,
                    poreNoise
                );

                // Fresnel keeps edges opaque — containment feeling
                float fresnelOpacity = pow(fresnel, 1.0 / _FresnelOpacityPower) * _FresnelOpacityStrength;
                float alpha = saturate(poreAlpha + fresnelOpacity);

                // Hard clip very low alpha to avoid ghost fragments
                clip(alpha - 0.01);

                // Boost color at edges
                surfaceColor += fresnel * _GradientColor.rgb * 0.4;

                float4 col = float4(surfaceColor, alpha);
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }

    Fallback "Universal Render Pipeline/Unlit"
}
