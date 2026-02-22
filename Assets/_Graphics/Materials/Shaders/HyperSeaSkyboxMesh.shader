// Mesh variant of the HyperSea skybox - for use on the SkyboxModel (BigMembraneVariant).
// Unlike the skybox variant, this computes view direction from camera to vertex
// in world space, so it works correctly on any mesh regardless of camera position.
//
// Usage: Create a material with this shader and assign it to the SkyboxModel mesh renderer.

Shader "CosmicShore/HyperSeaSkyboxMesh"
{
    Properties
    {
        [Header(Base Atmosphere)]
        _DeepColor ("Deep Space Color", Color) = (0.01, 0.005, 0.03, 1)
        _AmbientColor ("Ambient Glow Color", Color) = (0.04, 0.025, 0.07, 1)
        _AmbientStrength ("Ambient Glow Strength", Range(0, 0.5)) = 0.12

        [Header(Galactic Plane)]
        _GalacticNormal ("Galactic Plane Normal", Vector) = (0.1, 1.0, 0.15, 0)
        _GalacticColor ("Galactic Plane Color", Color) = (0.45, 0.4, 0.32, 1)
        [HDR]_GalacticEmission ("Galactic Emission Boost", Color) = (0.6, 0.5, 0.35, 1)
        _GalacticBrightness ("Galactic Brightness", Range(0, 5)) = 1.4
        _GalacticWidth ("Galactic Width", Range(0.02, 0.8)) = 0.18
        _GalacticNoiseScale ("Galactic Noise Scale", Range(0.5, 15)) = 4.0
        _GalacticNoiseStrength ("Galactic Edge Noise", Range(0, 1)) = 0.45

        [Header(Star Field)]
        _StarDensity ("Star Density", Range(20, 200)) = 80
        _StarBrightness ("Star Brightness", Range(0, 5)) = 2.5
        _StarConcentration ("Star Galactic Concentration", Range(0, 3)) = 1.2
        _TwinkleSpeed ("Twinkle Speed", Range(0, 5)) = 1.5

        [Header(Nebulae)]
        [HDR]_NebulaColor1 ("Nebula Color 1 (Rose)", Color) = (0.5, 0.06, 0.3, 1)
        [HDR]_NebulaColor2 ("Nebula Color 2 (Teal)", Color) = (0.06, 0.35, 0.45, 1)
        [HDR]_NebulaColor3 ("Nebula Color 3 (Violet)", Color) = (0.2, 0.06, 0.4, 1)
        _NebulaStrength ("Nebula Strength", Range(0, 2)) = 0.55
        _NebulaScale ("Nebula Scale", Range(0.5, 10)) = 2.5

        [Header(Dust Lanes)]
        _DustStrength ("Dust Strength", Range(0, 1)) = 0.45
        _DustScale ("Dust Scale", Range(0.5, 10)) = 2.0

        [Header(Galactic Core)]
        _CoreDirection ("Core Direction", Vector) = (1.0, -0.1, 0.3, 0)
        [HDR]_CoreColor ("Core Color", Color) = (1.0, 0.85, 0.5, 1)
        _CoreBrightness ("Core Brightness", Range(0, 15)) = 5.0
        _CoreSize ("Core Size", Range(0.01, 0.3)) = 0.1
        _CoreHaloSize ("Core Halo Size", Range(0.05, 1.0)) = 0.35
        [HDR]_CoreHaloColor ("Core Halo Color", Color) = (0.5, 0.3, 0.15, 1)

        [Header(Animation)]
        _DriftSpeed ("Drift Speed", Range(0, 0.1)) = 0.008
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Opaque"
            "UniversalMaterialType"="Unlit"
            "Queue"="Geometry-100"
        }

        Pass
        {
            Name "HyperSeaMesh"
            Cull Front
            ZWrite On

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"

            // ---- Properties ----
            half4 _DeepColor;
            half4 _AmbientColor;
            half _AmbientStrength;

            float4 _GalacticNormal;
            half4 _GalacticColor;
            half4 _GalacticEmission;
            half _GalacticBrightness;
            half _GalacticWidth;
            half _GalacticNoiseScale;
            half _GalacticNoiseStrength;

            half _StarDensity;
            half _StarBrightness;
            half _StarConcentration;
            half _TwinkleSpeed;

            half4 _NebulaColor1;
            half4 _NebulaColor2;
            half4 _NebulaColor3;
            half _NebulaStrength;
            half _NebulaScale;

            half _DustStrength;
            half _DustScale;

            float4 _CoreDirection;
            half4 _CoreColor;
            half _CoreBrightness;
            half _CoreSize;
            half _CoreHaloSize;
            half4 _CoreHaloColor;

            half _DriftSpeed;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 viewDir : TEXCOORD0;
            };

            // ================================================================
            // NOISE TOOLKIT
            // ================================================================

            float hash31(float3 p)
            {
                p = frac(p * float3(0.1031, 0.1030, 0.0973));
                p += dot(p, p.yxz + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float3 hash33(float3 p)
            {
                p = frac(p * float3(0.1031, 0.1030, 0.0973));
                p += dot(p, p.yxz + 33.33);
                return frac(float3(
                    (p.x + p.y) * p.z,
                    (p.x + p.z) * p.y,
                    (p.y + p.z) * p.x
                ));
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

            float fbm3(float3 p)
            {
                float v = 0.0;
                float a = 0.5;
                float3 shift = float3(100.0, 0.0, 0.0);
                for (int idx = 0; idx < 3; idx++)
                {
                    v += a * valueNoise(p);
                    p = p * 2.0 + shift;
                    a *= 0.5;
                }
                return v;
            }

            float fbm4(float3 p)
            {
                float v = 0.0;
                float a = 0.5;
                float3 shift = float3(100.0, 0.0, 0.0);
                for (int idx = 0; idx < 4; idx++)
                {
                    v += a * valueNoise(p);
                    p = p * 2.0 + shift;
                    a *= 0.5;
                }
                return v;
            }

            // ================================================================
            // PROCEDURAL LAYERS
            // ================================================================

            half3 starColor(float temp)
            {
                half3 col = lerp(half3(0.65, 0.75, 1.0), half3(1.0, 0.95, 0.88), saturate(temp * 2.5));
                col = lerp(col, half3(1.0, 0.78, 0.4), saturate((temp - 0.5) * 3.5));
                col = lerp(col, half3(1.0, 0.45, 0.25), saturate((temp - 0.82) * 5.5));
                return col;
            }

            half3 computeStars(float3 dir, float time)
            {
                half3 result = 0;
                float3 galNorm = normalize(_GalacticNormal.xyz);
                float scale = _StarDensity;
                float3 p = dir * scale;
                float3 id = floor(p);
                float3 f = frac(p) - 0.5;

                for (int x = -1; x <= 1; x++)
                for (int y = -1; y <= 1; y++)
                for (int z = -1; z <= 1; z++)
                {
                    float3 neighbor = float3(x, y, z);
                    float3 cellId = id + neighbor;
                    float h = hash31(cellId);
                    float3 cellDir = normalize((cellId + 0.5) / scale);
                    float galDist = abs(dot(cellDir, galNorm));
                    float probability = 0.25 + 0.35 * pow(1.0 - galDist, _StarConcentration);
                    if (h > probability) continue;

                    float3 offset = hash33(cellId) - 0.5;
                    float3 starPos = neighbor + offset - f;
                    float dist = length(starPos);
                    float star = exp(-dist * dist * 500.0);

                    float temp = hash31(cellId + 127.1);
                    half3 col = starColor(temp);
                    float twinkle = sin(time * _TwinkleSpeed + h * 80.0) * 0.25 + 0.75;
                    float brightness = 0.4 + h * 2.5;
                    result += star * col * brightness * twinkle * _StarBrightness;
                }
                return result;
            }

            half3 computeGalacticPlane(float3 dir, float time)
            {
                float3 galNorm = normalize(_GalacticNormal.xyz);
                float dist = abs(dot(dir, galNorm));
                float edgeNoise = valueNoise(dir * _GalacticNoiseScale + time * _DriftSpeed * 0.5);
                float width = _GalacticWidth * (1.0 + (edgeNoise - 0.5) * _GalacticNoiseStrength);
                float band = exp(-dist * dist / (2.0 * width * width));
                float detail = fbm3(dir * 10.0 + time * _DriftSpeed * 0.2);
                band *= (0.6 + 0.4 * detail);
                float coreStrip = exp(-dist * dist / (2.0 * (width * 0.3) * (width * 0.3)));
                half3 bandColor = lerp(_GalacticColor.rgb, _GalacticEmission.rgb, coreStrip);
                return band * bandColor * _GalacticBrightness;
            }

            half3 computeNebulae(float3 dir, float time)
            {
                float3 p = dir * _NebulaScale;
                float drift = time * _DriftSpeed;
                float n1 = fbm4(p * 2.0 + float3(drift, 0, 0));
                float n2 = fbm4(p * 2.5 + float3(5.2, 1.3 + drift * 0.7, 9.1));
                float n3 = fbm4(p * 3.0 + float3(3.7, 8.4, 2.6 + drift * 1.3));
                n1 = smoothstep(0.38, 0.78, n1);
                n2 = smoothstep(0.42, 0.82, n2);
                n3 = smoothstep(0.40, 0.80, n3);
                return (n1 * _NebulaColor1.rgb + n2 * _NebulaColor2.rgb + n3 * _NebulaColor3.rgb) * _NebulaStrength;
            }

            float computeDust(float3 dir, float time)
            {
                float3 galNorm = normalize(_GalacticNormal.xyz);
                float3 p = dir * _DustScale;
                float drift = time * _DriftSpeed * 0.3;
                float dust = fbm3(p * 1.8 + float3(0, drift, 0));
                dust = smoothstep(0.3, 0.7, dust);
                float galDist = abs(dot(dir, galNorm));
                float galMask = 1.0 - smoothstep(0.0, _GalacticWidth * 2.5, galDist);
                return max(1.0 - dust * _DustStrength * galMask, 0.25);
            }

            half3 computeCore(float3 dir)
            {
                float3 coreDir = normalize(_CoreDirection.xyz);
                float coreDot = dot(dir, coreDir);
                float inner = smoothstep(1.0 - _CoreSize, 1.0, coreDot);
                inner *= inner;
                float halo = smoothstep(1.0 - _CoreHaloSize, 1.0, coreDot);
                halo = pow(halo, 1.5);
                float rayNoise = valueNoise(dir * 6.0 + coreDir * 3.0);
                halo *= (0.7 + 0.3 * rayNoise);
                return inner * _CoreColor.rgb * _CoreBrightness
                     + halo * _CoreHaloColor.rgb * (_CoreBrightness * 0.25);
            }

            half3 computeAmbient(float3 dir, float time)
            {
                half3 base = _DeepColor.rgb;
                float ambientNoise = valueNoise(dir * 2.5 + time * _DriftSpeed * 1.5);
                half3 ambient = _AmbientColor.rgb * _AmbientStrength * (0.5 + ambientNoise * 0.5);
                float3 galNorm = normalize(_GalacticNormal.xyz);
                float galDist = abs(dot(dir, galNorm));
                float galGlow = (1.0 - galDist) * 0.04;
                return base + ambient + galGlow * _AmbientColor.rgb;
            }

            // ================================================================
            // VERTEX / FRAGMENT
            // ================================================================

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                // Compute view direction from camera to world-space vertex
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.viewDir = worldPos - _WorldSpaceCameraPos;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                float3 dir = normalize(i.viewDir);
                float time = _Time.y;

                half3 color = computeAmbient(dir, time);
                color += computeGalacticPlane(dir, time);
                color += computeStars(dir, time);
                color += computeNebulae(dir, time);
                color *= computeDust(dir, time);
                color += computeCore(dir);

                return half4(color, 1.0);
            }

            ENDCG
        }
    }

    Fallback Off
}
