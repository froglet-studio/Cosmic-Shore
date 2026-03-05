// ForcefieldCrackle.hlsl
// Custom Function for Shader Graph — renders impact crackles on a sphere surface.
//
// Usage in Shader Graph:
//   Custom Function node → File mode → this file
//   Function name: ForcefieldCrackle
//
// Inputs:
//   float3 ObjectPosition  — object-space vertex/fragment position (Position node, Object space)
//   float3 ObjectNormal    — object-space normal
//
// Outputs:
//   float3 EmissionColor   — additive emission RGB
//   float  Alpha           — transparency (0 = fully transparent, 1 = fully opaque)
//
// Properties set via MaterialPropertyBlock from ForcefieldCrackleController.cs:
//
// Impact data (per-frame):
//   float4 _ImpactPositions[16]  — xyz = unit-sphere direction, w = elapsed time
//   float4 _ImpactParams[16]     — x = intensity, y = angular radius, z = max lifetime
//   int    _ImpactCount           — active impact count (early-out optimization)
//
// Visual params (set from SO config, updated on change):
//   float4 _CrackleColorA        — primary crackle line color
//   float4 _CrackleColorB        — secondary / highlight color
//   float4 _FresnelRimColor      — ambient rim glow color
//   float  _VoronoiCellSize      — voronoi cell density (2–40, default 12)
//   float  _EdgeSharpness        — voronoi edge threshold (0.01–0.5, default 0.15)
//   float  _RingThickness        — ring width factor (0.05–1, default 0.4)
//   float  _CenterFillAmount     — center fill ratio (0–1, default 0.3)
//   float  _RippleSpeed          — expansion speed multiplier (0.2–3, default 1)
//   float  _FresnelRimIntensity  — rim glow strength (0–0.5, default 0.08)
//   float  _FresnelRimPower      — fresnel exponent (1–8, default 3)

#ifndef FORCEFIELD_CRACKLE_INCLUDED
#define FORCEFIELD_CRACKLE_INCLUDED

// Impact data — set from C# via MaterialPropertyBlock
float4 _ImpactPositions[16];
float4 _ImpactParams[16];
int _ImpactCount;

// Visual params — set from C# via MaterialPropertyBlock (sourced from SO config)
float4 _CrackleColorA;       // primary crackle line color (RGB)
float4 _CrackleColorB;       // secondary / highlight color (RGB)
float4 _FresnelRimColor;     // ambient rim glow color (RGB)
float _VoronoiCellSize;      // density of crackle cells (default 12)
float _EdgeSharpness;        // voronoi edge threshold (default 0.15)
float _RingThickness;        // ring width relative to angular radius (default 0.4)
float _CenterFillAmount;     // center fill vs. ring-only (default 0.3)
float _RippleSpeed;          // expansion speed multiplier (default 1)
float _FresnelRimIntensity;  // ambient rim glow strength (default 0.08)
float _FresnelRimPower;      // fresnel exponent (default 3)

// ─── Procedural noise helpers ───────────────────────────────────────────────

// Hash function for pseudo-random values
float3 Hash3(float3 p)
{
    p = float3(
        dot(p, float3(127.1, 311.7, 74.7)),
        dot(p, float3(269.5, 183.3, 246.1)),
        dot(p, float3(113.5, 271.9, 124.6))
    );
    return frac(sin(p) * 43758.5453123);
}

// 3D Voronoi — returns (F1 distance, F2 distance, cell ID hash)
// Used to create the branching/crackle pattern
float3 Voronoi3D(float3 p)
{
    float3 cellBase = floor(p);
    float3 cellFrac = frac(p);

    float f1 = 8.0;
    float f2 = 8.0;
    float cellId = 0.0;

    for (int x = -1; x <= 1; x++)
    for (int y = -1; y <= 1; y++)
    for (int z = -1; z <= 1; z++)
    {
        float3 offset = float3(x, y, z);
        float3 randomOffset = Hash3(cellBase + offset);
        float3 diff = offset + randomOffset - cellFrac;
        float dist = dot(diff, diff);

        if (dist < f1)
        {
            f2 = f1;
            f1 = dist;
            cellId = dot(cellBase + offset, float3(1.0, 57.0, 113.0));
        }
        else if (dist < f2)
        {
            f2 = dist;
        }
    }

    f1 = sqrt(f1);
    f2 = sqrt(f2);
    return float3(f1, f2, frac(sin(cellId) * 43758.5453));
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

    // Normalize the fragment's position on the unit sphere
    float3 fragDir = normalize(ObjectPosition);

    float totalContribution = 0;
    float3 totalColor = float3(0, 0, 0);

    for (int i = 0; i < 16; i++)
    {
        float4 impactPos = _ImpactPositions[i];
        float4 impactParam = _ImpactParams[i];

        float maxLifetime = impactParam.z;
        if (maxLifetime <= 0) continue; // empty slot

        float intensity = impactParam.x;
        float angularRadius = impactParam.y;
        float elapsed = impactPos.w;

        // Lifetime ratio (0 = just born, 1 = expired)
        float lifeRatio = saturate(elapsed / maxLifetime);

        // Fade out: sharp attack, smooth decay
        float timeFade = 1.0 - lifeRatio * lifeRatio;

        // Great-circle (geodesic) distance on the unit sphere
        float3 impactDir = normalize(impactPos.xyz);
        float cosAngle = dot(fragDir, impactDir);
        float angle = acos(saturate(cosAngle)); // [0, PI]

        // Expanding ring edge — the crackle wavefront expands outward over time
        // _RippleSpeed controls how fast the ring expands (1 = reaches max radius at expiry)
        float expandedLife = saturate(lifeRatio * _RippleSpeed);
        float maxAngle = angularRadius * 3.14159 * expandedLife;
        float ringWidth = angularRadius * _RingThickness;

        // Distance from the expanding ring edge
        float distFromRing = abs(angle - maxAngle);

        // Also add a filled contribution near the impact center that fades with time
        float centerFill = smoothstep(angularRadius * 3.14159 * _CenterFillAmount, 0, angle) * (1.0 - lifeRatio);

        // Ring contribution — sharp at the wavefront
        float ringContrib = smoothstep(ringWidth, ringWidth * 0.1, distFromRing);
        ringContrib *= step(angle, angularRadius * 3.14159 + ringWidth); // clip beyond max radius

        // Combine ring and center fill
        float spatialFalloff = max(ringContrib, centerFill);

        if (spatialFalloff < 0.001) continue;

        // Crackle pattern — voronoi cell edges create branching fracture lines
        // Scale the voronoi based on angular position relative to impact point
        // Use spherical coordinates relative to impact direction for consistent scaling
        float3 tangent = normalize(cross(impactDir, float3(0.123, 0.456, 0.789)));
        float3 bitangent = cross(impactDir, tangent);
        float2 localUV = float2(dot(fragDir, tangent), dot(fragDir, bitangent));

        float3 voronoiInput = float3(localUV * _VoronoiCellSize, angle * _VoronoiCellSize * 0.5);
        float3 vor = Voronoi3D(voronoiInput);

        // Edge detection: difference between F2 and F1 highlights cell boundaries
        float edgeFactor = vor.y - vor.x;
        float crackle = smoothstep(0.0, _EdgeSharpness, edgeFactor); // 0 at edges, 1 in cell interiors
        crackle = 1.0 - crackle; // invert: 1 at edges (the crackle lines)

        // Combine everything
        float contribution = spatialFalloff * crackle * timeFade * intensity;

        // Color: lerp between the two configurable crackle colors
        float hueShift = frac(vor.z + i * 0.137);
        float3 crackleColor = lerp(
            _CrackleColorA.rgb,
            _CrackleColorB.rgb,
            crackle * 0.6 + hueShift * 0.2
        );

        totalContribution += contribution;
        totalColor += crackleColor * contribution;
    }

    totalContribution = saturate(totalContribution);

    // Fresnel rim boost — makes the sphere edge more visible even without impacts
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
