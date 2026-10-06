// GLSL translation of the project's own procedural skybox,
// Assets/_Graphics/Materials/Shaders/HyperSeaSkybox.shader (CosmicShore/HyperSeaSkybox).
// Mechanical HLSL -> GLSL mapping (half/float3 -> vec3, frac -> fract, lerp -> mix,
// atan2 -> atan, saturate -> sat); the algorithm is unchanged. Regenerate rather than
// hand-edit if the source shader changes.
namespace CosmicShore.Render
{
    static class HyperSeaSkyboxGlsl
    {
        public const string Fragment = @"#version 330 core
in vec2 vNdc;
uniform mat4 uInvViewProj;
uniform float uTime;
out vec4 frag;
float sat(float x){return clamp(x,0.0,1.0);}
uniform vec4 _DeepColor;
uniform vec4 _AmbientColor;
uniform float _AmbientStrength;
uniform vec4 _GalacticNormal;
uniform vec4 _GalacticColor;
uniform vec4 _GalacticEmission;
uniform float _GalacticBrightness;
uniform float _GalacticWidth;
uniform float _GalacticNoiseScale;
uniform float _GalacticNoiseStrength;
uniform float _StarDensity;
uniform float _StarBrightness;
uniform float _StarBaseProb;
uniform float _StarGalacticBoost;
uniform float _StarConcentration;
uniform float _TwinkleSpeed;
uniform vec4 _NebulaColor1;
uniform vec4 _NebulaColor2;
uniform vec4 _NebulaColor3;
uniform float _NebulaStrength;
uniform float _NebulaScale;
uniform float _DustStrength;
uniform float _DustScale;
uniform vec4 _CoreDirection;
uniform vec4 _CoreColor;
uniform float _CoreBrightness;
uniform float _CoreSize;
uniform float _CoreHaloSize;
uniform vec4 _CoreHaloColor;
uniform vec4 _AndromedaDirection;
uniform vec4 _AndromedaDiskColor;
uniform vec4 _AndromedaNucleusColor;
uniform float _AndromedaBrightness;
uniform float _AndromedaSize;
uniform float _CellOverlayStrength;
uniform float _CellOverlayScale;
uniform vec4 _CellOverlayColor;
uniform float _CellEdgeSharpness;
uniform vec4 _AtmosphereColor;
uniform float _AtmosphereStrength;
uniform float _AtmosphereHeight;
uniform float _AtmosphereFalloff;
uniform float _DriftSpeed;
    float hash31(vec3 p)
    {
        p = fract(p * vec3(0.1031, 0.1030, 0.0973));
        p += dot(p, p.yxz + 33.33);
        return fract((p.x + p.y) * p.z);
    }

    vec3 hash33(vec3 p)
    {
        p = fract(p * vec3(0.1031, 0.1030, 0.0973));
        p += dot(p, p.yxz + 33.33);
        return fract(vec3(
            (p.x + p.y) * p.z,
            (p.x + p.z) * p.y,
            (p.y + p.z) * p.x
        ));
    }

    // 3D value noise
    float valueNoise(vec3 p)
    {
        vec3 i = floor(p);
        vec3 f = fract(p);
        f = f * f * (3.0 - 2.0 * f);

        float n000 = hash31(i);
        float n100 = hash31(i + vec3(1, 0, 0));
        float n010 = hash31(i + vec3(0, 1, 0));
        float n110 = hash31(i + vec3(1, 1, 0));
        float n001 = hash31(i + vec3(0, 0, 1));
        float n101 = hash31(i + vec3(1, 0, 1));
        float n011 = hash31(i + vec3(0, 1, 1));
        float n111 = hash31(i + vec3(1, 1, 1));

        return mix(
            mix(mix(n000, n100, f.x), mix(n010, n110, f.x), f.y),
            mix(mix(n001, n101, f.x), mix(n011, n111, f.x), f.y),
            f.z
        );
    }

    // Fractal Brownian Motion - 3 octaves (dust, atmosphere)
    float fbm3(vec3 p)
    {
        float v = 0.0;
        float a = 0.5;
        vec3 shift = vec3(100.0, 0.0, 0.0);

        for (int idx = 0; idx < 3; idx++)
        {
            v += a * valueNoise(p);
            p = p * 2.0 + shift;
            a *= 0.5;
        }
        return v;
    }

    // Fractal Brownian Motion - 4 octaves (nebulae)
    float fbm4(vec3 p)
    {
        float v = 0.0;
        float a = 0.5;
        vec3 shift = vec3(100.0, 0.0, 0.0);

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

    vec3 starColor(float temp)
    {
        vec3 blue   = vec3(0.65, 0.75, 1.0);
        vec3 white  = vec3(1.0, 0.95, 0.88);
        vec3 yellow = vec3(1.0, 0.78, 0.4);
        vec3 red    = vec3(1.0, 0.45, 0.25);

        vec3 col = mix(blue, white, sat(temp * 2.5));
        col = mix(col, yellow, sat((temp - 0.5) * 3.5));
        col = mix(col, red, sat((temp - 0.82) * 5.5));
        return col;
    }

    // ================================================================
    // STAR FIELD - Hubble Ultra Deep Field style
    // Every object has unique structure: no plain points.
    // Foreground stars with spikes, edge-on/face-on/irregular galaxies
    // ================================================================

    vec3 computeStars(vec3 dir, float time)
    {
        vec3 result = vec3(0.0);
        vec3 galNorm = normalize(_GalacticNormal.xyz);

        float scale = _StarDensity;
        vec3 p = dir * scale;
        vec3 id = floor(p);
        vec3 f = fract(p) - 0.5;

        for (int x = -1; x <= 1; x++)
        for (int y = -1; y <= 1; y++)
        for (int z = -1; z <= 1; z++)
        {
            vec3 neighbor = vec3(x, y, z);
            vec3 cellId = id + neighbor;

            // Hash for object existence
            float h = hash31(cellId);

            // Increase probability near galactic plane
            vec3 cellDir = normalize((cellId + 0.5) / scale);
            float galDist = abs(dot(cellDir, galNorm));
            float probability = _StarBaseProb + _StarGalacticBoost * pow(1.0 - galDist, _StarConcentration);

            if (h > probability)
                continue;

            // Object position within cell
            vec3 offset = hash33(cellId) - 0.5;
            vec3 starPos = neighbor + offset - f;
            float dist = length(starPos);

            // Per-object hashes for type, shape, orientation
            float typeHash = hash31(cellId + 73.7);
            float sizeHash = hash31(cellId + 191.3);
            float temp = hash31(cellId + 127.1);
            vec3 orient = normalize(hash33(cellId + 53.0) - 0.5);

            float obj = 0.0;
            vec3 col = vec3(0, 0, 0);

            // Decompose starPos into components along orient axis
            float along = dot(starPos, orient);
            vec3 perpVec = starPos - along * orient;
            float perp = length(perpVec);

            if (typeHash < 0.15)
            {
                // ---- Foreground star with diffraction spikes ----
                float tightness = 600.0 + sizeHash * 800.0;
                obj = exp(-dist * dist * tightness);

                // Four-point diffraction cross
                vec3 spikeA = normalize(cross(orient, vec3(0.17, 1.0, 0.31)));
                vec3 spikeB = normalize(cross(orient, spikeA));
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
                vec3 perpDir = normalize(perpVec + 0.001);
                vec3 secondAxis = normalize(cross(orient, perpDir));
                float armAngle = atan(dot(starPos, secondAxis), dot(starPos, perpDir));
                float armPattern = sin(armAngle * 2.0 + dist * spread * 0.8) * 0.3 + 0.7;

                obj = diskFalloff * armPattern * 0.4 + nucleus * 0.7;

                col = starColor(temp * 0.5 + 0.25);
            }
            else if (typeHash < 0.78)
            {
                // ---- Irregular / interacting galaxy (asymmetric blob) ----
                // Offset the center slightly for asymmetry
                vec3 asymOffset = (hash33(cellId + 97.3) - 0.5) * 0.15;
                vec3 asymPos = starPos + asymOffset;
                float asymDist = length(asymPos);

                float spread = 8.0 + sizeHash * 12.0;
                float blob = exp(-asymDist * asymDist * spread);

                // Secondary knot (interacting companion)
                vec3 knot = starPos - asymOffset * 2.0;
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

            // Twinkle only for foreground stars - galaxies are steady
            float twinkle = (typeHash < 0.15)
                ? sin(time * _TwinkleSpeed + h * 80.0) * 0.25 + 0.75
                : 1.0;

            // Wide brightness range - most objects are faint
            float brightness = 0.15 + h * h * 3.0;

            result += obj * col * brightness * twinkle * _StarBrightness;
        }

        return result;
    }

    // ================================================================
    // GALACTIC PLANE
    // Bright milky band with noise-modulated edges
    // ================================================================

    vec3 computeGalacticPlane(vec3 dir, float time)
    {
        vec3 galNorm = normalize(_GalacticNormal.xyz);
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
        vec3 bandColor = mix(_GalacticColor.rgb, _GalacticEmission.rgb, coreStrip);

        return band * bandColor * _GalacticBrightness;
    }

    // ================================================================
    // NEBULAE
    // Domain-warped clouds with large-scale directional variation
    // Each direction in the sky has unique character
    // ================================================================

    vec3 computeNebulae(vec3 dir, float time)
    {
        vec3 p = dir * _NebulaScale;
        float drift = time * _DriftSpeed;

        // Domain warping - organic flowing shapes instead of blobby camo
        vec3 warp = vec3(
            valueNoise(p * 1.5 + vec3(drift * 0.2, 0, 0)),
            valueNoise(p * 1.5 + vec3(5.2, 1.3 + drift * 0.15, 0)),
            valueNoise(p * 1.5 + vec3(2.1, 0, 7.8))
        );
        vec3 wp = p + (warp - 0.5) * 1.4;

        // Large-scale directional mask - breaks uniform tiling,
        // creates nebula-rich regions and vast dark voids
        float regionNoise = valueNoise(dir * 1.3 + vec3(42.0, 17.0, 91.0));
        float regionMask = smoothstep(0.3, 0.7, regionNoise);

        // Three warped noise fields at different scales & offsets
        float n1 = fbm4(wp * 2.0 + vec3(drift, 0, 0));
        float n2 = fbm4(wp * 2.5 + vec3(5.2, 1.3 + drift * 0.7, 9.1));
        float n3 = fbm4(wp * 3.0 + vec3(3.7, 8.4, 2.6 + drift * 1.3));

        // Wide smoothstep + square curve: dense bright cores with long
        // wispy tails that gradually fade into darkness (no hard edges)
        float c1 = smoothstep(0.28, 0.88, n1);
        c1 *= c1;
        float c2 = smoothstep(0.30, 0.90, n2);
        c2 *= c2;
        float c3 = smoothstep(0.29, 0.89, n3);
        c3 *= c3;

        // Brightness variation within each cloud - creates illusion of
        // variable depth: bright hot-spots read as closer/denser,
        // dim regions recede into the background
        float depth1 = valueNoise(wp * 4.5 + vec3(11.3, 0, drift * 0.3));
        float depth2 = valueNoise(wp * 5.0 + vec3(0, 13.7, drift * 0.25));
        float depth3 = valueNoise(wp * 5.5 + vec3(0, drift * 0.2, 15.1));
        c1 *= 0.3 + depth1 * 1.0;
        c2 *= 0.3 + depth2 * 1.0;
        c3 *= 0.3 + depth3 * 1.0;

        vec3 color = c1 * _NebulaColor1.rgb
                    + c2 * _NebulaColor2.rgb
                    + c3 * _NebulaColor3.rgb;

        // Apply large-scale modulation - some sky regions are rich, others void
        color *= regionMask;

        return color * _NebulaStrength;
    }

    // ================================================================
    // DUST LANES
    // Dark filaments that occlude light, concentrated near galactic plane
    // ================================================================

    float computeDust(vec3 dir, float time)
    {
        vec3 galNorm = normalize(_GalacticNormal.xyz);

        vec3 p = dir * _DustScale;
        float drift = time * _DriftSpeed * 0.3;

        float dust = fbm3(p * 1.8 + vec3(0, drift, 0));
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

    vec3 computeCore(vec3 dir)
    {
        vec3 coreDir = normalize(_CoreDirection.xyz);
        vec3 galNorm = normalize(_GalacticNormal.xyz);
        float coreDot = dot(dir, coreDir);

        // Core only exists on the galactic plane - not a standalone circle
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

        vec3 color = inner * _CoreColor.rgb * _CoreBrightness
                    + halo * _CoreHaloColor.rgb * (_CoreBrightness * 0.25);

        return color;
    }

    // ================================================================
    // ANDROMEDA GALAXY - computed inline
    // Inclined elliptical disk with spiral arms and dust lane.
    // ================================================================

    vec3 computeAndromeda(vec3 dir)
    {
        vec3 androDir = normalize(_AndromedaDirection.xyz);
        vec3 up = normalize(cross(androDir, vec3(0.13, 1.0, 0.24)));
        vec3 right = normalize(cross(up, androDir));

        float u = dot(dir, right);
        float v = dot(dir, up);
        float w = dot(dir, androDir);

        // Only compute for pixels roughly facing Andromeda
        float facing = sat((w - 0.7) * 5.0);
        if (facing < 0.001) return vec3(0.0);

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
        float spiralPhase = atan(ev, eu) * 2.0 + r * 15.0 / _AndromedaSize;
        float spiral = valueNoise(vec3(
            sin(spiralPhase),
            cos(spiralPhase),
            r * 10.0 / _AndromedaSize + 3.7
        ));
        disk *= (0.4 + spiral * 0.6);

        // Dust lane across the minor axis
        float dustLane = 1.0 - 0.35 * exp(-ev * ev / (size2 * 0.003));
        disk *= dustLane;

        vec3 color = disk * _AndromedaDiskColor.rgb
                    + nucleus * _AndromedaNucleusColor.rgb;

        return color * _AndromedaBrightness * facing;
    }

    // ================================================================
    // CELLULAR OVERLAY
    // Voronoi cell pattern projected onto the sky sphere - echoes the
    // membrane's faceted geometric language at low opacity.
    // Uses 3D Voronoi so the pattern is seamless on the sphere.
    // ================================================================

    vec3 computeCellOverlay(vec3 dir, float time)
    {
        if (_CellOverlayStrength < 0.001) return vec3(0.0);

        vec3 p = dir * _CellOverlayScale;
        vec3 drift = vec3(time * _DriftSpeed * 0.3, 0, time * _DriftSpeed * 0.2);
        p += drift;

        vec3 i = floor(p);
        vec3 f = fract(p);

        float minDist1 = 10.0;
        float minDist2 = 10.0;

        // 3D Voronoi - find two nearest cell centers
        for (int x = -1; x <= 1; x++)
        for (int y = -1; y <= 1; y++)
        for (int z = -1; z <= 1; z++)
        {
            vec3 neighbor = vec3(x, y, z);
            vec3 cellCenter = hash33(i + neighbor);
            vec3 diff = neighbor + cellCenter - f;
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

    vec3 computeAtmosphereBridge(vec3 dir)
    {
        if (_AtmosphereStrength < 0.001) return vec3(0.0);

        // Hemisphere bias - thicker atmosphere in one direction
        // (typically below the galactic plane, toward where the membrane sits)
        float heightFactor = dot(dir, vec3(0, 1, 0));
        float biasedHeight = heightFactor - _AtmosphereHeight;

        // Exponential falloff from the bias direction
        float atmo = exp(-abs(biasedHeight) * _AtmosphereFalloff);

        // Add noise so it feels organic, not a clean gradient
        float noiseVal = valueNoise(dir * 3.0 + vec3(17.3, 0, 41.7));
        atmo *= (0.7 + 0.3 * noiseVal);

        return _AtmosphereColor.rgb * atmo * _AtmosphereStrength;
    }

    // ================================================================
    // AMBIENT ATMOSPHERE
    // Subtle living glow - the ""translucent medium"" feel
    // ================================================================

    vec3 computeAmbient(vec3 dir, float time)
    {
        vec3 base = _DeepColor.rgb;

        // Gentle animated ambient variation
        float ambientNoise = valueNoise(dir * 2.5 + time * _DriftSpeed * 1.5);
        vec3 ambient = _AmbientColor.rgb * _AmbientStrength * (0.5 + ambientNoise * 0.5);

        // Slightly brighter near galactic plane
        vec3 galNorm = normalize(_GalacticNormal.xyz);
        float galDist = abs(dot(dir, galNorm));
        float galGlow = (1.0 - galDist) * 0.04;

        return base + ambient + galGlow * _AmbientColor.rgb;
    }

    // ================================================================
    // SHARED FRAGMENT
    // ================================================================

    vec4 hyperSeaFrag(vec3 viewDir)
    {
        vec3 dir = normalize(viewDir);
        float time = uTime;

        // 1. Base atmosphere - the living medium
        vec3 color = computeAmbient(dir, time);

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

        // 8. Cellular overlay - geometric Voronoi pattern bridging membrane aesthetic
        color += computeCellOverlay(dir, time);

        // 9. Atmosphere bridge - directional haze in membrane palette
        color += computeAtmosphereBridge(dir);

        return vec4(color, 1.0);
    }

    
void main(){
  vec4 p = uInvViewProj * vec4(vNdc, 1.0, 1.0);
  frag = hyperSeaFrag(p.xyz / p.w);
}
";
    }
}
