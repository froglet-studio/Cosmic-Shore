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
        _StarBaseProb ("Star Base Probability", Range(0, 1)) = 0.25
        _StarGalacticBoost ("Star Galactic Plane Boost", Range(0, 1)) = 0.35
        _StarConcentration ("Star Galactic Concentration", Range(0, 100)) = 1.2
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

        [Header(Andromeda)]
        _AndromedaDirection ("Andromeda Direction", Vector) = (-0.5, 0.35, 0.8, 0)
        [HDR]_AndromedaDiskColor ("Disk Color", Color) = (0.3, 0.35, 0.55, 1)
        [HDR]_AndromedaNucleusColor ("Nucleus Color", Color) = (0.6, 0.55, 0.4, 1)
        _AndromedaBrightness ("Brightness", Range(0, 3)) = 1.0
        _AndromedaSize ("Angular Size", Range(0.02, 0.4)) = 0.1

        [Header(Cellular Overlay)]
        _CellOverlayStrength ("Cell Overlay Strength", Range(0, 0.5)) = 0.12
        _CellOverlayScale ("Cell Overlay Scale", Range(1, 40)) = 8.0
        _CellOverlayColor ("Cell Overlay Color", Color) = (0.14, 0.21, 0.62, 1)
        _CellEdgeSharpness ("Cell Edge Sharpness", Range(1, 20)) = 8.0

        [Header(Membrane Atmosphere Bridge)]
        _AtmosphereColor ("Atmosphere Color", Color) = (0.02, 0.04, 0.18, 1)
        _AtmosphereStrength ("Atmosphere Strength", Range(0, 1)) = 0.25
        _AtmosphereHeight ("Atmosphere Height Bias", Range(-1, 1)) = -0.3
        _AtmosphereFalloff ("Atmosphere Falloff", Range(0.5, 8)) = 2.5

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
    half _StarBaseProb;
    half _StarGalacticBoost;
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

    float4 _AndromedaDirection;
    half4 _AndromedaDiskColor;
    half4 _AndromedaNucleusColor;
    half _AndromedaBrightness;
    half _AndromedaSize;

    half _CellOverlayStrength;
    half _CellOverlayScale;
    half4 _CellOverlayColor;
    half _CellEdgeSharpness;

    half4 _AtmosphereColor;
    half _AtmosphereStrength;
    half _AtmosphereHeight;
    half _AtmosphereFalloff;

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
    // Every object has unique structure: no plain points.
    // Foreground stars with spikes, edge-on/face-on/irregular galaxies
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
            float probability = _StarBaseProb + _StarGalacticBoost * pow(1.0 - galDist, _StarConcentration);

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

            // Decompose starPos into components along orient axis
            float along = dot(starPos, orient);
            float3 perpVec = starPos - along * orient;
            float perp = length(perpVec);

            if (typeHash < 0.15)
            {
                // ---- Foreground star with diffraction spikes ----
                float tightness = 600.0 + sizeHash * 800.0;
                obj = exp(-dist * dist * tightness);

                // Four-point diffraction cross
                float3 spikeA = normalize(cross(orient, float3(0.17, 1.0, 0.31)));
                float3 spikeB = normalize(cross(orient, spikeA));
                float dA = length(cross(starPos, spikeA));
                float dB = length(cross(starPos, spikeB));
                float spikes = exp(-dA * dA * 3000.0) + exp(-dB * dB * 3000.0);
                spikes *= exp(-dist * 12.0);
                obj += spikes * 0.35;

                col = starColor(temp);
            }
            else if (typeHash < 0.40)
            {
                // ---- Edge-on galaxy (elongated streak with central bulge) ----
                float elongation = 3.0 + sizeHash * 5.0;
                float spread = 10.0 + sizeHash * 8.0;
                float streak = exp(-(along * along * spread / elongation
                                   + perp * perp * spread * elongation));
                // Central bulge brighter than the arms
                float bulge = exp(-dist * dist * spread * 2.0) * 0.5;
                obj = streak + bulge;

                col = starColor(temp * 0.6 + 0.2);
            }
            else if (typeHash < 0.60)
            {
                // ---- Face-on galaxy (disk + bright nucleus + arm hint) ----
                float spread = 6.0 + sizeHash * 6.0;
                float diskFalloff = exp(-dist * dist * spread);
                float nucleus = exp(-dist * dist * spread * 8.0);

                // Hint of spiral structure via angular variation
                float3 perpDir = normalize(perpVec + 0.001);
                float3 secondAxis = normalize(cross(orient, perpDir));
                float armAngle = atan2(dot(starPos, secondAxis), dot(starPos, perpDir));
                float armPattern = sin(armAngle * 2.0 + dist * spread * 0.8) * 0.3 + 0.7;

                obj = diskFalloff * armPattern * 0.4 + nucleus * 0.7;

                col = starColor(temp * 0.5 + 0.25);
            }
            else if (typeHash < 0.78)
            {
                // ---- Irregular / interacting galaxy (asymmetric blob) ----
                // Offset the center slightly for asymmetry
                float3 asymOffset = (hash33(cellId + 97.3) - 0.5) * 0.15;
                float3 asymPos = starPos + asymOffset;
                float asymDist = length(asymPos);

                float spread = 8.0 + sizeHash * 12.0;
                float blob = exp(-asymDist * asymDist * spread);

                // Secondary knot (interacting companion)
                float3 knot = starPos - asymOffset * 2.0;
                float knotDist = length(knot);
                blob += exp(-knotDist * knotDist * spread * 3.0) * 0.4;

                col = starColor(temp * 0.7 + 0.15);
                obj = blob;
            }
            else
            {
                // ---- Faint elongated smudge (distant unresolved galaxy) ----
                float elongation = 1.5 + sizeHash * 3.0;
                float spread = 12.0 + sizeHash * 16.0;
                obj = exp(-(along * along * spread / elongation
                          + perp * perp * spread * elongation));

                // Slight central brightening
                obj += exp(-dist * dist * spread * 3.0) * 0.3;

                col = starColor(temp * 0.4 + 0.3);
            }

            // Twinkle only for foreground stars — galaxies are steady
            float twinkle = (typeHash < 0.15)
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
    // Domain-warped clouds with large-scale directional variation
    // Each direction in the sky has unique character
    // ================================================================

    half3 computeNebulae(float3 dir, float time)
    {
        float3 p = dir * _NebulaScale;
        float drift = time * _DriftSpeed;

        // Domain warping — organic flowing shapes instead of blobby camo
        float3 warp = float3(
            valueNoise(p * 1.5 + float3(drift * 0.2, 0, 0)),
            valueNoise(p * 1.5 + float3(5.2, 1.3 + drift * 0.15, 0)),
            valueNoise(p * 1.5 + float3(2.1, 0, 7.8))
        );
        float3 wp = p + (warp - 0.5) * 1.4;

        // Large-scale directional mask — breaks uniform tiling,
        // creates nebula-rich regions and vast dark voids
        float regionNoise = valueNoise(dir * 1.3 + float3(42.0, 17.0, 91.0));
        float regionMask = smoothstep(0.3, 0.7, regionNoise);

        // Three warped noise fields at different scales & offsets
        float n1 = fbm4(wp * 2.0 + float3(drift, 0, 0));
        float n2 = fbm4(wp * 2.5 + float3(5.2, 1.3 + drift * 0.7, 9.1));
        float n3 = fbm4(wp * 3.0 + float3(3.7, 8.4, 2.6 + drift * 1.3));

        // Wide smoothstep + square curve: dense bright cores with long
        // wispy tails that gradually fade into darkness (no hard edges)
        float c1 = smoothstep(0.28, 0.88, n1);
        c1 *= c1;
        float c2 = smoothstep(0.30, 0.90, n2);
        c2 *= c2;
        float c3 = smoothstep(0.29, 0.89, n3);
        c3 *= c3;

        // Brightness variation within each cloud — creates illusion of
        // variable depth: bright hot-spots read as closer/denser,
        // dim regions recede into the background
        float depth1 = valueNoise(wp * 4.5 + float3(11.3, 0, drift * 0.3));
        float depth2 = valueNoise(wp * 5.0 + float3(0, 13.7, drift * 0.25));
        float depth3 = valueNoise(wp * 5.5 + float3(0, drift * 0.2, 15.1));
        c1 *= 0.3 + depth1 * 1.0;
        c2 *= 0.3 + depth2 * 1.0;
        c3 *= 0.3 + depth3 * 1.0;

        half3 color = c1 * _NebulaColor1.rgb
                    + c2 * _NebulaColor2.rgb
                    + c3 * _NebulaColor3.rgb;

        // Apply large-scale modulation — some sky regions are rich, others void
        color *= regionMask;

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
    // ANDROMEDA GALAXY — computed inline
    // Inclined elliptical disk with spiral arms and dust lane.
    // ================================================================

    half3 computeAndromeda(float3 dir)
    {
        float3 androDir = normalize(_AndromedaDirection.xyz);
        float3 up = normalize(cross(androDir, float3(0.13, 1.0, 0.24)));
        float3 right = normalize(cross(up, androDir));

        float u = dot(dir, right);
        float v = dot(dir, up);
        float w = dot(dir, androDir);

        // Only compute for pixels roughly facing Andromeda
        float facing = saturate((w - 0.7) * 5.0);
        if (facing < 0.001) return 0;

        // Inclined elliptical disk (~77 deg tilt)
        float eu = u;
        float ev = v * 3.2;
        float r2 = eu * eu + ev * ev;
        float size2 = _AndromedaSize * _AndromedaSize;

        // Outer disk with smooth falloff
        float disk = exp(-r2 / (size2 * 0.25));

        // Bright compact nucleus
        float nucleus = exp(-r2 / (size2 * 0.008));

        // Spiral arms via log-spiral coordinates
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

        return color * _AndromedaBrightness * facing;
    }

    // ================================================================
    // CELLULAR OVERLAY
    // Voronoi cell pattern projected onto the sky sphere — echoes the
    // membrane's faceted geometric language at low opacity.
    // Uses 3D Voronoi so the pattern is seamless on the sphere.
    // ================================================================

    half3 computeCellOverlay(float3 dir, float time)
    {
        if (_CellOverlayStrength < 0.001) return 0;

        float3 p = dir * _CellOverlayScale;
        float3 drift = float3(time * _DriftSpeed * 0.3, 0, time * _DriftSpeed * 0.2);
        p += drift;

        float3 i = floor(p);
        float3 f = frac(p);

        float minDist1 = 10.0;
        float minDist2 = 10.0;

        // 3D Voronoi — find two nearest cell centers
        for (int x = -1; x <= 1; x++)
        for (int y = -1; y <= 1; y++)
        for (int z = -1; z <= 1; z++)
        {
            float3 neighbor = float3(x, y, z);
            float3 cellCenter = hash33(i + neighbor);
            float3 diff = neighbor + cellCenter - f;
            float d = dot(diff, diff);

            if (d < minDist1)
            {
                minDist2 = minDist1;
                minDist1 = d;
            }
            else if (d < minDist2)
            {
                minDist2 = d;
            }
        }

        minDist1 = sqrt(minDist1);
        minDist2 = sqrt(minDist2);

        // Edge detection: where two cells are nearly equidistant
        float edge = 1.0 - smoothstep(0.0, 1.0 / _CellEdgeSharpness, minDist2 - minDist1);

        // Subtle interior gradient for depth
        float interior = smoothstep(0.0, 0.5, minDist1) * 0.15;

        float pattern = edge + interior;
        return pattern * _CellOverlayColor.rgb * _CellOverlayStrength;
    }

    // ================================================================
    // MEMBRANE ATMOSPHERE BRIDGE
    // A directional atmospheric haze that uses the membrane's palette
    // to create visual continuity between the geometric cell boundary
    // and the photorealistic deep space.
    // ================================================================

    half3 computeAtmosphereBridge(float3 dir)
    {
        if (_AtmosphereStrength < 0.001) return 0;

        // Hemisphere bias — thicker atmosphere in one direction
        // (typically below the galactic plane, toward where the membrane sits)
        float heightFactor = dot(dir, float3(0, 1, 0));
        float biasedHeight = heightFactor - _AtmosphereHeight;

        // Exponential falloff from the bias direction
        float atmo = exp(-abs(biasedHeight) * _AtmosphereFalloff);

        // Add noise so it feels organic, not a clean gradient
        float noiseVal = valueNoise(dir * 3.0 + float3(17.3, 0, 41.7));
        atmo *= (0.7 + 0.3 * noiseVal);

        return _AtmosphereColor.rgb * atmo * _AtmosphereStrength;
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
    // SHARED FRAGMENT (full quality)
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

        // 6. Galactic core - brightness enhancement graded into the plane
        color += computeCore(dir);

        // 7. Andromeda - inline procedural computation
        color += computeAndromeda(dir);

        // 8. Cellular overlay — geometric Voronoi pattern bridging membrane aesthetic
        color += computeCellOverlay(dir, time);

        // 9. Atmosphere bridge — directional haze in membrane palette
        color += computeAtmosphereBridge(dir);

        return half4(color, 1.0);
    }

    // ================================================================
    // MOBILE FRAGMENT (reduced quality)
    // Skips: nebulae (3x fbm4), dust lanes (fbm3), Voronoi cells (27-tap),
    // Andromeda (spiral arms). Keeps: atmosphere, galactic plane, stars, core.
    // ================================================================

    half4 hyperSeaFragMobile(float3 viewDir)
    {
        float3 dir = normalize(viewDir);
        float time = _Time.y;

        half3 color = computeAmbient(dir, time);
        color += computeGalacticPlane(dir, time);
        color += computeStars(dir, time);
        color += computeCore(dir);
        color += computeAtmosphereBridge(dir);

        return half4(color, 1.0);
    }

    ENDCG

    // ================================================================
    // SUBSHADER 1: Mobile (reduced quality)
    // Selected first on mobile GPUs (OpenGL ES 3.0 / Vulkan mobile).
    // Drops nebulae, dust, Voronoi, Andromeda to save ALU.
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

        // LOD 100 ensures desktop (LOD 200+) prefers the full SubShader below
        LOD 100

        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma only_renderers gles3 vulkan metal

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.viewDir = v.vertex.xyz;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                return hyperSeaFragMobile(i.viewDir);
            }

            ENDCG
        }
    }

    // ================================================================
    // SUBSHADER 2: Desktop / Full Quality
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

        LOD 200

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
