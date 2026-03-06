// ForcefieldCrackle.hlsl
// Renders electrical arc / charge discharge effects on a sphere surface.
// Arcs radiate from impact points and expand outward as jagged lightning bolts.
//
// Usage in Shader Graph:
//   Custom Function node → File mode → this file
//   Function name: ForcefieldCrackle
//
// Inputs:
//   float3 ObjectPosition  — object-space vertex/fragment position
//   float3 ObjectNormal    — object-space normal
//
// Outputs:
//   float3 EmissionColor   — additive emission RGB
//   float  Alpha           — transparency
//
// Properties set via MaterialPropertyBlock from ForcefieldCrackleController.cs:
//
// Impact data (per-frame):
//   float4 _ImpactPositions[16]  — xyz = unit-sphere direction, w = elapsed time
//   float4 _ImpactParams[16]     — x = intensity, y = angular radius, z = max lifetime
//   int    _ImpactCount           — active impact count
//
// Visual params (set from SO config, updated on change):
//   float4 _CrackleColorA        — core arc color (hot center)
//   float4 _CrackleColorB        — outer glow color (halo)
//   float4 _FresnelRimColor      — ambient rim glow color
//   float  _ArcDensity           — number of arc branches (4–20, default 8)
//   float  _ArcSharpness         — how thin/sharp the arcs are (0.01–0.5, default 0.06)
//   float  _RingThickness        — wavefront band width (0.05–1, default 0.4)
//   float  _CenterFillAmount     — center glow amount (0–1, default 0.15)
//   float  _RippleSpeed          — expansion speed multiplier (0.2–3, default 1)
//   float  _FresnelRimIntensity  — rim glow strength (0–0.5, default 0.08)
//   float  _FresnelRimPower      — fresnel exponent (1–8, default 3)

#ifndef FORCEFIELD_CRACKLE_INCLUDED
#define FORCEFIELD_CRACKLE_INCLUDED

// Impact data
float4 _ImpactPositions[16];
float4 _ImpactParams[16];
int _ImpactCount;

// Visual params
float4 _CrackleColorA;
float4 _CrackleColorB;
float4 _FresnelRimColor;
float _ArcDensity;
float _ArcSharpness;
float _RingThickness;
float _CenterFillAmount;
float _RippleSpeed;
float _FresnelRimIntensity;
float _FresnelRimPower;

// ─── Noise helpers ──────────────────────────────────────────────────────────

// Simple 1D hash
float Hash1(float n)
{
    return frac(sin(n) * 43758.5453123);
}

// 2D hash → float
float Hash12(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

// Value noise 1D — smooth random wobble along a line
float ValueNoise1D(float x)
{
    float i = floor(x);
    float f = frac(x);
    f = f * f * (3.0 - 2.0 * f); // smoothstep
    return lerp(Hash1(i), Hash1(i + 1.0), f);
}

// FBM (fractal brownian motion) 1D — multi-octave jagged noise
float FBM1D(float x, int octaves)
{
    float value = 0.0;
    float amplitude = 0.5;
    float frequency = 1.0;

    for (int o = 0; o < octaves; o++)
    {
        value += amplitude * (ValueNoise1D(x * frequency) * 2.0 - 1.0);
        frequency *= 2.17;
        amplitude *= 0.5;
    }
    return value;
}

// ─── Main function ──────────────────────────────────────────────────────────

void ForcefieldCrackle_float(
    float3 ObjectPosition,
    float3 ObjectNormal,
    out float3 EmissionColor,
    out float Alpha)
{
    EmissionColor = float3(0, 0, 0);
    Alpha = 0;

    if (_ImpactCount <= 0) return;

    float3 fragDir = normalize(ObjectPosition);

    float totalContribution = 0;
    float3 totalColor = float3(0, 0, 0);

    for (int i = 0; i < 16; i++)
    {
        float4 impactPos = _ImpactPositions[i];
        float4 impactParam = _ImpactParams[i];

        float maxLifetime = impactParam.z;
        if (maxLifetime <= 0) continue;

        float intensity = impactParam.x;
        float angularRadius = impactParam.y;
        float elapsed = impactPos.w;

        float lifeRatio = saturate(elapsed / maxLifetime);

        // Fade: sharp flash then decay
        float timeFade = pow(1.0 - lifeRatio, 1.5);

        // Great-circle distance on the unit sphere
        float3 impactDir = normalize(impactPos.xyz);
        float cosAngle = dot(fragDir, impactDir);
        float angle = acos(clamp(cosAngle, -1.0, 1.0));

        // Build a local coordinate frame on the sphere around the impact point
        float3 tangent = normalize(cross(impactDir, float3(0.123, 0.456, 0.789)));
        float3 bitangent = cross(impactDir, tangent);

        // Azimuthal angle around the impact point (0 to 2*PI)
        float2 localUV = float2(dot(fragDir, tangent), dot(fragDir, bitangent));
        float azimuth = atan2(localUV.y, localUV.x); // [-PI, PI]

        // ── Expanding wavefront ──
        float expandedLife = saturate(lifeRatio * _RippleSpeed);
        float wavefrontAngle = angularRadius * 3.14159 * expandedLife;
        float ringWidth = angularRadius * _RingThickness;

        // Wavefront band: strongest at the leading edge, fading behind
        float distBehindFront = wavefrontAngle - angle;
        float waveBand = smoothstep(-ringWidth * 0.1, 0.0, distBehindFront)
                       * smoothstep(ringWidth, 0.0, distBehindFront);

        // Clip anything beyond the wavefront + margin
        waveBand *= step(angle, wavefrontAngle + ringWidth * 0.2);

        // Center glow — bright flash at impact origin, fading quickly
        float centerGlow = smoothstep(angularRadius * 3.14159 * _CenterFillAmount, 0, angle)
                         * (1.0 - lifeRatio * lifeRatio);

        float spatialEnvelope = max(waveBand, centerGlow);
        if (spatialEnvelope < 0.001) continue;

        // ── Electrical arc pattern ──
        // Create multiple arc "spokes" radiating from the impact point
        // Each spoke is a thin line whose radial position wobbles via FBM noise
        int arcCount = (int)_ArcDensity;
        float arcContrib = 0.0;
        float arcHeat = 0.0; // tracks how close we are to an arc core (for color)

        for (int a = 0; a < arcCount; a++)
        {
            if (a >= (int)_ArcDensity) break; // dynamic loop guard

            // Each arc has a base azimuthal angle, evenly spaced + per-impact offset
            float baseAngle = (float(a) / float(arcCount)) * 6.28318 + Hash1(float(i) * 7.3 + 0.5) * 6.28318;

            // Azimuthal distance from this arc spoke
            float dAzimuth = azimuth - baseAngle;
            // Wrap to [-PI, PI]
            dAzimuth = dAzimuth - 6.28318 * round(dAzimuth / 6.28318);

            // The arc wobbles sideways as it travels outward (jagged lightning path)
            // FBM displacement grows with distance from impact — farther = more chaotic
            float noiseInput = angle * 15.0 + float(a) * 13.7 + float(i) * 5.3;
            float wobble = FBM1D(noiseInput, 4) * 0.3 * (angle + 0.1);

            // Secondary sub-branch jitter
            float subBranch = FBM1D(noiseInput * 2.3 + 100.0, 3) * 0.15 * angle;

            // Distance from the wobbling arc line
            float arcDist = abs(dAzimuth - wobble);
            float arcDistSub = abs(dAzimuth - wobble - subBranch);

            // Sharp falloff — creates thin bright lines
            float arcLine = exp(-arcDist * arcDist / (_ArcSharpness * _ArcSharpness));
            float subLine = exp(-arcDistSub * arcDistSub / (_ArcSharpness * _ArcSharpness * 4.0)) * 0.4;

            float thisArc = max(arcLine, subLine);

            // Arcs fade at the very center (nothing to arc from) and at the outer edge
            thisArc *= smoothstep(0.0, 0.05, angle);

            arcContrib = max(arcContrib, thisArc);
            arcHeat = max(arcHeat, arcLine);
        }

        // Combine spatial envelope with arc pattern
        float contribution = spatialEnvelope * arcContrib * timeFade * intensity;

        // Color: hot core color at arc center, outer glow color at edges
        float3 arcColor = lerp(
            _CrackleColorB.rgb,    // outer glow
            _CrackleColorA.rgb,    // hot core
            arcHeat * arcHeat      // squared for sharper color transition at arc cores
        );

        // Add a brightness boost at arc cores (HDR bloom-friendly)
        arcColor *= 1.0 + arcHeat * 2.0;

        totalContribution += contribution;
        totalColor += arcColor * contribution;
    }

    totalContribution = saturate(totalContribution);

    // Fresnel rim — subtle sphere outline even without impacts
    float fresnel = 1.0 - abs(dot(normalize(ObjectNormal), normalize(ObjectPosition)));
    float rimBoost = pow(fresnel, _FresnelRimPower) * _FresnelRimIntensity;

    Alpha = saturate(totalContribution + rimBoost);
    EmissionColor = totalContribution > 0.001
        ? (totalColor / max(totalContribution, 0.001)) * Alpha
        : _FresnelRimColor.rgb * rimBoost;
}

// Half-precision variant for mobile
void ForcefieldCrackle_half(
    half3 ObjectPosition,
    half3 ObjectNormal,
    out half3 EmissionColor,
    out half Alpha)
{
    float3 emOut;
    float aOut;
    ForcefieldCrackle_float(ObjectPosition, ObjectNormal, emOut, aOut);
    EmissionColor = (half3)emOut;
    Alpha = (half)aOut;
}

#endif // FORCEFIELD_CRACKLE_INCLUDED
