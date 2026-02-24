Shader "CosmicShore/HyperSeaSkybox"
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

    // ================================================================
    // SHARED HLSL INCLUDE (inlined via CGINCLUDE)
    // Both SubShaders use the same procedural generation logic.
    // ================================================================

    CGINCLUDE

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

    // ---- Structs ----
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
    // Hash Without Sin - robust, portable hash functions
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

    // 3D value noise
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

    // Fractal Brownian Motion - 3 octaves (dust, atmosphere)
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

    // Fractal Brownian Motion - 4 octaves (nebulae)
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
    // STAR COLOR from temperature hash (branchless)
    // Blue-white -> White -> Yellow -> Red-orange
    // ================================================================

    half3 starColor(float temp)
    {
        half3 blue   = half3(0.65, 0.75, 1.0);
        half3 white  = half3(1.0, 0.95, 0.88);
        half3 yellow = half3(1.0, 0.78, 0.4);
        half3 red    = half3(1.0, 0.45, 0.25);

        half3 col = lerp(blue, white, saturate(temp * 2.5));
        col = lerp(col, yellow, saturate((temp - 0.5) * 3.5));
        col = lerp(col, red, saturate((temp - 0.82) * 5.5));
        return col;
    }

    // ================================================================
    // STAR FIELD — Hubble Ultra Deep Field style
    // Each object has unique structure: point stars with diffraction
    // spikes, tiny edge-on/face-on galaxies, faint background smudges
    // ================================================================

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

            // Hash for object existence
            float h = hash31(cellId);

            // Increase probability near galactic plane
            float3 cellDir = normalize((cellId + 0.5) / scale);
            float galDist = abs(dot(cellDir, galNorm));
            float probability = 0.25 + 0.35 * pow(1.0 - galDist, _StarConcentration);

            if (h > probability)
                continue;

            // Object position within cell
            float3 offset = hash33(cellId) - 0.5;
            float3 starPos = neighbor + offset - f;
            float dist = length(starPos);

            // Per-object hashes for type, shape, orientation
            float typeHash = hash31(cellId + 73.7);
            float sizeHash = hash31(cellId + 191.3);
            float temp = hash31(cellId + 127.1);
            float3 orient = normalize(hash33(cellId + 53.0) - 0.5);

            float obj = 0;
            half3 col = half3(0, 0, 0);

            if (typeHash < 0.18)
            {
                // ---- Bright star with diffraction spikes ----
                float tightness = 600.0 + sizeHash * 800.0;
                obj = exp(-dist * dist * tightness);

                // Four-point diffraction cross
                float3 spikeA = normalize(cross(orient, float3(0.17, 1.0, 0.31)));
                float3 spikeB = normalize(cross(orient, spikeA));
                float dA = length(cross(starPos, spikeA));
                float dB = length(cross(starPos, spikeB));
                float spikes = exp(-dA * dA * 3000.0) + exp(-dB * dB * 3000.0);
                spikes *= exp(-dist * 12.0); // fade with distance from center
                obj += spikes * 0.35;

                col = starColor(temp);
            }
            else if (typeHash < 0.42)
            {
                // ---- Tiny edge-on galaxy (elongated streak) ----
                float along = dot(starPos, orient);
                float3 perpVec = starPos - along * orient;
                float perp = length(perpVec);
                float elongation = 3.0 + sizeHash * 5.0;
                float spread = 250.0 + sizeHash * 200.0;
                obj = exp(-(along * along * spread / elongation
                          + perp * perp * spread * elongation));

                // Warmer colors for galaxies
                col = starColor(temp * 0.6 + 0.2);
            }
            else if (typeHash < 0.55)
            {
                // ---- Faint face-on galaxy (soft disk with bright nucleus) ----
                float spread = 150.0 + sizeHash * 150.0;
                float disk = exp(-dist * dist * spread);
                float nucleus = exp(-dist * dist * spread * 6.0);
                obj = disk * 0.4 + nucleus * 0.6;

                col = starColor(temp * 0.5 + 0.25);
            }
            else
            {
                // ---- Point star (varied tightness) ----
                float tightness = 400.0 + sizeHash * 800.0;
                obj = exp(-dist * dist * tightness);

                col = starColor(temp);
            }

            // Twinkle (stars only, galaxies are steady)
            float twinkle = (typeHash < 0.18 || typeHash >= 0.55)
                ? sin(time * _TwinkleSpeed + h * 80.0) * 0.25 + 0.75
                : 1.0;

            // Wide brightness range — most objects are faint
            float brightness = 0.15 + h * h * 3.0;

            result += obj * col * brightness * twinkle * _StarBrightness;
        }

        return result;
    }

    // ================================================================
    // GALACTIC PLANE
    // Bright milky band with noise-modulated edges
    // ================================================================

    half3 computeGalacticPlane(float3 dir, float time)
    {
        float3 galNorm = normalize(_GalacticNormal.xyz);
        float dist = abs(dot(dir, galNorm));

        // Noise-modulated width
        float edgeNoise = valueNoise(dir * _GalacticNoiseScale + time * _DriftSpeed * 0.5);
        float width = _GalacticWidth * (1.0 + (edgeNoise - 0.5) * _GalacticNoiseStrength);

        // Gaussian band
        float band = exp(-dist * dist / (2.0 * width * width));

        // Internal particulate structure
        float detail = fbm3(dir * 10.0 + time * _DriftSpeed * 0.2);
        band *= (0.6 + 0.4 * detail);

        // Brighter core strip within the band
        float coreStrip = exp(-dist * dist / (2.0 * (width * 0.3) * (width * 0.3)));
        half3 bandColor = lerp(_GalacticColor.rgb, _GalacticEmission.rgb, coreStrip);

        return band * bandColor * _GalacticBrightness;
    }

    // ================================================================
    // NEBULAE
    // Three colored FBM clouds like ink diffusing in water
    // ================================================================

    half3 computeNebulae(float3 dir, float time)
    {
        float3 p = dir * _NebulaScale;
        float drift = time * _DriftSpeed;

        // Three independent noise fields
        float n1 = fbm4(p * 2.0 + float3(drift, 0, 0));
        float n2 = fbm4(p * 2.5 + float3(5.2, 1.3 + drift * 0.7, 9.1));
        float n3 = fbm4(p * 3.0 + float3(3.7, 8.4, 2.6 + drift * 1.3));

        // Shape noise into isolated cloud structures
        // Raised thresholds create dark space between structures, less mixing
        float c1 = smoothstep(0.52, 0.78, n1);
        float c2 = smoothstep(0.55, 0.82, n2);
        float c3 = smoothstep(0.53, 0.80, n3);

        half3 color = c1 * _NebulaColor1.rgb
                    + c2 * _NebulaColor2.rgb
                    + c3 * _NebulaColor3.rgb;

        return color * _NebulaStrength;
    }

    // ================================================================
    // DUST LANES
    // Dark filaments that occlude light, concentrated near galactic plane
    // ================================================================

    float computeDust(float3 dir, float time)
    {
        float3 galNorm = normalize(_GalacticNormal.xyz);

        float3 p = dir * _DustScale;
        float drift = time * _DriftSpeed * 0.3;

        float dust = fbm3(p * 1.8 + float3(0, drift, 0));
        dust = smoothstep(0.3, 0.7, dust);

        // Dust concentrates near galactic plane
        float galDist = abs(dot(dir, galNorm));
        float galMask = 1.0 - smoothstep(0.0, _GalacticWidth * 2.5, galDist);

        float occlusion = 1.0 - dust * _DustStrength * galMask;
        return max(occlusion, 0.25);
    }

    // ================================================================
    // GALACTIC CORE
    // Brightness enhancement graded into the galactic plane
    // ================================================================

    half3 computeCore(float3 dir)
    {
        float3 coreDir = normalize(_CoreDirection.xyz);
        float3 galNorm = normalize(_GalacticNormal.xyz);
        float coreDot = dot(dir, coreDir);

        // Core only exists on the galactic plane — not a standalone circle
        float galDist = abs(dot(dir, galNorm));
        float onPlane = exp(-galDist * galDist / (2.0 * _GalacticWidth * _GalacticWidth));

        // Tight inner brightening
        float inner = smoothstep(1.0 - _CoreSize, 1.0, coreDot);
        inner *= inner * onPlane;

        // Broader glow, also constrained to the plane
        float halo = smoothstep(1.0 - _CoreHaloSize, 1.0, coreDot);
        halo *= onPlane;

        // Particulate structure in the halo
        float rayNoise = valueNoise(dir * 8.0 + coreDir * 3.0);
        halo *= (0.6 + 0.4 * rayNoise);

        half3 color = inner * _CoreColor.rgb * _CoreBrightness
                    + halo * _CoreHaloColor.rgb * (_CoreBrightness * 0.25);

        return color;
    }

    // ================================================================
    // AMBIENT ATMOSPHERE
    // Subtle living glow - the "translucent medium" feel
    // ================================================================

    half3 computeAmbient(float3 dir, float time)
    {
        half3 base = _DeepColor.rgb;

        // Gentle animated ambient variation
        float ambientNoise = valueNoise(dir * 2.5 + time * _DriftSpeed * 1.5);
        half3 ambient = _AmbientColor.rgb * _AmbientStrength * (0.5 + ambientNoise * 0.5);

        // Slightly brighter near galactic plane
        float3 galNorm = normalize(_GalacticNormal.xyz);
        float galDist = abs(dot(dir, galNorm));
        float galGlow = (1.0 - galDist) * 0.04;

        return base + ambient + galGlow * _AmbientColor.rgb;
    }

    // ================================================================
    // SHARED FRAGMENT
    // ================================================================

    half4 hyperSeaFrag(float3 viewDir)
    {
        float3 dir = normalize(viewDir);
        float time = _Time.y;

        // 1. Base atmosphere - the living medium
        half3 color = computeAmbient(dir, time);

        // 2. Galactic plane - the bright milky band
        color += computeGalacticPlane(dir, time);

        // 3. Star field - bioluminescent plankton
        color += computeStars(dir, time);

        // 4. Nebulae - ink clouds in the medium
        color += computeNebulae(dir, time);

        // 5. Dust lanes - murky silt (multiplicative)
        color *= computeDust(dir, time);

        // 6. Galactic core - the overwhelming bright center
        color += computeCore(dir);

        return half4(color, 1.0);
    }

    ENDCG

    // ================================================================
    // SUBSHADER 1: Unity Skybox (RenderSettings.skybox)
    // Used when assigned as a skybox material in Lighting settings.
    // Vertex positions ARE the view directions.
    // ================================================================

    SubShader
    {
        Tags
        {
            "Queue"="Background"
            "RenderType"="Background"
            "PreviewType"="Skybox"
            "RenderPipeline"="UniversalPipeline"
        }

        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.viewDir = v.vertex.xyz;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                return hyperSeaFrag(i.viewDir);
            }

            ENDCG
        }
    }

    Fallback Off
}
