// ForcefieldCrackle.hlsl
// Renders electrical arc / charge discharge effects on a sphere surface.
// Arcs radiate from impact points and expand outward as jagged lightning bolts.
//
// Inputs:
//   float3 ObjectPosition  - object-space vertex/fragment position
//   float3 ObjectNormal    - object-space normal
//   float3 ViewDirOS       - object-space view direction (camera - vertex)
//
// Outputs:
//   float3 EmissionColor   - additive emission RGB
//   float  Alpha           - transparency
//
// Properties set via MaterialPropertyBlock from ForcefieldCrackleController.cs:
//
// Impact data (per-frame):
//   float4 _ImpactPositions[16]  - xyz = unit-sphere direction, w = elapsed time
//   float4 _ImpactParams[16]     - x = intensity, y = angular radius, z = max lifetime
//   int    _ImpactCount           - active impact count
//
// Visual params (set from controller serialized fields):
//   float4 _CrackleColorA        - core arc color (hot center)
//   float4 _CrackleColorB        - outer glow color (halo)
//   float4 _FresnelRimColor      - ambient rim glow color
//   float  _ArcDensity           - number of arc branches (1–20, default 8)
//   float  _ArcSharpness         - how thin/sharp the arcs are (0.01–0.5, default 0.06)
//   float  _RingThickness        - wavefront band width (0.05–1, default 0.4)
//   float  _CenterFillAmount     - center glow amount (0–1, default 0.15)
//   float  _RippleSpeed          - expansion speed multiplier (0.2–3, default 1)
//   float  _FresnelRimIntensity  - rim glow strength (0–0.5, default 0.08)
//   float  _FresnelRimPower      - fresnel exponent (1–8, default 3)

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

float Hash1(float n)
{
    return frac(sin(n) * 43758.5453123);
}

float ValueNoise1D(float x)
{
    float i = floor(x);
    float f = frac(x);
    f = f * f * (3.0 - 2.0 * f);
    return lerp(Hash1(i), Hash1(i + 1.0), f);
}

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
    float3 ViewDirOS,
    out float3 EmissionColor,
    out float Alpha)
{
    EmissionColor = float3(0, 0, 0);
    Alpha = 0;

    float3 fragDir = normalize(ObjectPosition);

    // Fresnel rim - proper view-dependent calculation
    float3 N = normalize(ObjectNormal);
    float3 V = normalize(ViewDirOS);
    float NdotV = saturate(dot(N, V));
    float fresnel = pow(1.0 - NdotV, _FresnelRimPower) * _FresnelRimIntensity;

    // Always show fresnel rim, even with no impacts
    Alpha = fresnel;
    EmissionColor = _FresnelRimColor.rgb * fresnel;

    if (_ImpactCount <= 0) return;

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

        // Azimuthal angle around the impact point
        float2 localUV = float2(dot(fragDir, tangent), dot(fragDir, bitangent));
        float azimuth = atan2(localUV.y, localUV.x);

        // ── Expanding wavefront ──
        float expandedLife = saturate(lifeRatio * _RippleSpeed);
        float wavefrontAngle = angularRadius * 3.14159 * expandedLife;
        float ringWidth = angularRadius * _RingThickness;

        // Wavefront band: strongest at the leading edge, fading behind
        float distBehindFront = wavefrontAngle - angle;
        float waveBand = smoothstep(-ringWidth * 0.1, 0.0, distBehindFront)
                       * smoothstep(ringWidth, 0.0, distBehindFront);

        // Clip beyond the wavefront + margin
        waveBand *= step(angle, wavefrontAngle + ringWidth * 0.2);

        // Center glow - bright flash at impact origin
        float centerGlow = smoothstep(angularRadius * 3.14159 * _CenterFillAmount, 0, angle)
                         * (1.0 - lifeRatio * lifeRatio);

        float spatialEnvelope = max(waveBand, centerGlow);
        if (spatialEnvelope < 0.001) continue;

        // ── Electrical arc pattern ──
        int arcCount = (int)_ArcDensity;
        float arcContrib = 0.0;
        float arcHeat = 0.0;

        for (int a = 0; a < arcCount; a++)
        {
            if (a >= (int)_ArcDensity) break;

            float baseAngle = (float(a) / float(arcCount)) * 6.28318 + Hash1(float(i) * 7.3 + 0.5) * 6.28318;

            float dAzimuth = azimuth - baseAngle;
            dAzimuth = dAzimuth - 6.28318 * round(dAzimuth / 6.28318);

            float noiseInput = angle * 15.0 + float(a) * 13.7 + float(i) * 5.3;
            float wobble = FBM1D(noiseInput, 4) * 0.3 * (angle + 0.1);

            float subBranch = FBM1D(noiseInput * 2.3 + 100.0, 3) * 0.15 * angle;

            float arcDist = abs(dAzimuth - wobble);
            float arcDistSub = abs(dAzimuth - wobble - subBranch);

            float arcLine = exp(-arcDist * arcDist / (_ArcSharpness * _ArcSharpness));
            float subLine = exp(-arcDistSub * arcDistSub / (_ArcSharpness * _ArcSharpness * 4.0)) * 0.4;

            float thisArc = max(arcLine, subLine);
            thisArc *= smoothstep(0.0, 0.05, angle);

            arcContrib = max(arcContrib, thisArc);
            arcHeat = max(arcHeat, arcLine);
        }

        float contribution = spatialEnvelope * arcContrib * timeFade * intensity;

        // Color: hot core at arc center, outer glow at edges
        float3 arcColor = lerp(
            _CrackleColorB.rgb,
            _CrackleColorA.rgb,
            arcHeat * arcHeat
        );

        arcColor *= 1.0 + arcHeat * 2.0;

        totalContribution += contribution;
        totalColor += arcColor * contribution;
    }

    totalContribution = saturate(totalContribution);

    // Combine impacts with fresnel rim
    Alpha = saturate(totalContribution + fresnel);
    EmissionColor = totalContribution > 0.001
        ? (totalColor / max(totalContribution, 0.001)) * totalContribution + _FresnelRimColor.rgb * fresnel
        : _FresnelRimColor.rgb * fresnel;
}

// Half-precision variant for mobile
void ForcefieldCrackle_half(
    half3 ObjectPosition,
    half3 ObjectNormal,
    half3 ViewDirOS,
    out half3 EmissionColor,
    out half Alpha)
{
    float3 emOut;
    float aOut;
    ForcefieldCrackle_float(ObjectPosition, ObjectNormal, ViewDirOS, emOut, aOut);
    EmissionColor = (half3)emOut;
    Alpha = (half)aOut;
}

// ─── Capsule variant (the Rhino energy-sword blade) ─────────────────────────
//
// The sphere function parameterizes the surface by unit DIRECTION and measures
// ripples as great-circle ANGLES — on the blade's stretched capsule (built-in
// capsule mesh, object y ∈ [-1, 1], under a strongly non-uniform ~(1.5, 30, 4.8)
// scale) that collapses the whole blade length into the two poles. This variant
// measures in WORLD UNITS instead:
//
//   • _ImpactPositions[i].xyz = the impact's OBJECT-SPACE POSITION (not direction),
//     stored by ForcefieldCrackleController in Capsule surface mode, so arcs stay
//     glued to the blade through swings and stretch with it as energy grows it.
//   • _ImpactParams[i].y      = the ripple's REACH in world units.
//
// ScaleOS is the object→world scale per axis (from the vertex shader's model
// matrix); multiplying object positions by it component-wise yields world-metric
// distances without caring about the blade's world rotation. All visual params
// (_ArcDensity, _RingThickness, _CenterFillAmount, colors, fresnel) keep the same
// meaning and proportions as the sphere version — ring width and center fill are
// fractions of the reach, arcs radiate in a surface-tangent frame at the impact.
void ForcefieldCrackleCapsule_float(
    float3 ObjectPosition,
    float3 ObjectNormal,
    float3 ViewDirOS,
    float3 ScaleOS,
    out float3 EmissionColor,
    out float Alpha)
{
    EmissionColor = float3(0, 0, 0);
    Alpha = 0;

    // Fresnel rim - same view-dependent rim as the sphere version.
    float3 N = normalize(ObjectNormal);
    float3 V = normalize(ViewDirOS);
    float NdotV = saturate(dot(N, V));
    float fresnel = pow(1.0 - NdotV, _FresnelRimPower) * _FresnelRimIntensity;

    Alpha = fresnel;
    EmissionColor = _FresnelRimColor.rgb * fresnel;

    if (_ImpactCount <= 0) return;

    // World-metric position on the capsule (rotation dropped: it preserves distance).
    float3 fragSC = ObjectPosition * ScaleOS;

    float totalContribution = 0;
    float3 totalColor = float3(0, 0, 0);

    for (int i = 0; i < 16; i++)
    {
        float4 impactPos = _ImpactPositions[i];
        float4 impactParam = _ImpactParams[i];

        float maxLifetime = impactParam.z;
        if (maxLifetime <= 0) continue;

        float intensity = impactParam.x;
        float reach = max(impactParam.y, 0.01); // world units
        float elapsed = impactPos.w;

        float lifeRatio = saturate(elapsed / maxLifetime);
        float timeFade = pow(1.0 - lifeRatio, 1.5);

        float3 impSC = impactPos.xyz * ScaleOS;
        float3 d = fragSC - impSC;

        // Normalized surface distance: 0 at the impact, 1 at full reach — the direct
        // analog of the sphere version's angle / (angularRadius * PI).
        float nd = length(d) / reach;

        // ── Expanding wavefront (fractions of the reach; 1/PI keeps the sphere
        //    version's ring-width : reach proportion for the same _RingThickness) ──
        float front = saturate(lifeRatio * _RippleSpeed);
        float ringW = _RingThickness * 0.3183;

        float distBehindFront = front - nd;
        float waveBand = smoothstep(-ringW * 0.1, 0.0, distBehindFront)
                       * smoothstep(ringW, 0.0, distBehindFront);
        waveBand *= step(nd, front + ringW * 0.2);

        float centerGlow = smoothstep(_CenterFillAmount, 0, nd)
                         * (1.0 - lifeRatio * lifeRatio);

        float spatialEnvelope = max(waveBand, centerGlow);
        if (spatialEnvelope < 0.001) continue;

        // ── Surface-tangent frame at the impact: outward = capsule normal direction
        //    (built-in capsule: cylinder body spans object y ∈ [-0.5, 0.5], caps beyond,
        //    so the scaled body half-height is 0.5 * ScaleOS.y) ──
        float segHalf = 0.5 * ScaleOS.y;
        float3 axisPoint = float3(0.0, clamp(impSC.y, -segHalf, segHalf), 0.0);
        float3 outward = normalize(impSC - axisPoint + float3(1e-4, 0.0, 0.0));
        float3 tangent = normalize(cross(outward, float3(0.123, 0.456, 0.789)));
        float3 bitangent = cross(outward, tangent);

        float azimuth = atan2(dot(d, bitangent), dot(d, tangent));

        // ── Electrical arc pattern (identical structure; nd*PI stands in for the
        //    sphere's great-circle angle so the FBM frequencies carry over) ──
        float ndAngle = nd * 3.14159;
        int arcCount = (int)_ArcDensity;
        float arcContrib = 0.0;
        float arcHeat = 0.0;

        for (int a = 0; a < arcCount; a++)
        {
            if (a >= (int)_ArcDensity) break;

            float baseAngle = (float(a) / float(arcCount)) * 6.28318 + Hash1(float(i) * 7.3 + 0.5) * 6.28318;

            float dAzimuth = azimuth - baseAngle;
            dAzimuth = dAzimuth - 6.28318 * round(dAzimuth / 6.28318);

            float noiseInput = ndAngle * 15.0 + float(a) * 13.7 + float(i) * 5.3;
            float wobble = FBM1D(noiseInput, 4) * 0.3 * (ndAngle + 0.1);

            float subBranch = FBM1D(noiseInput * 2.3 + 100.0, 3) * 0.15 * ndAngle;

            float arcDist = abs(dAzimuth - wobble);
            float arcDistSub = abs(dAzimuth - wobble - subBranch);

            float arcLine = exp(-arcDist * arcDist / (_ArcSharpness * _ArcSharpness));
            float subLine = exp(-arcDistSub * arcDistSub / (_ArcSharpness * _ArcSharpness * 4.0)) * 0.4;

            float thisArc = max(arcLine, subLine);
            thisArc *= smoothstep(0.0, 0.016, nd);

            arcContrib = max(arcContrib, thisArc);
            arcHeat = max(arcHeat, arcLine);
        }

        float contribution = spatialEnvelope * arcContrib * timeFade * intensity;

        float3 arcColor = lerp(
            _CrackleColorB.rgb,
            _CrackleColorA.rgb,
            arcHeat * arcHeat
        );

        arcColor *= 1.0 + arcHeat * 2.0;

        totalContribution += contribution;
        totalColor += arcColor * contribution;
    }

    totalContribution = saturate(totalContribution);

    Alpha = saturate(totalContribution + fresnel);
    EmissionColor = totalContribution > 0.001
        ? (totalColor / max(totalContribution, 0.001)) * totalContribution + _FresnelRimColor.rgb * fresnel
        : _FresnelRimColor.rgb * fresnel;
}

void ForcefieldCrackleCapsule_half(
    half3 ObjectPosition,
    half3 ObjectNormal,
    half3 ViewDirOS,
    half3 ScaleOS,
    out half3 EmissionColor,
    out half Alpha)
{
    float3 emOut;
    float aOut;
    ForcefieldCrackleCapsule_float(ObjectPosition, ObjectNormal, ViewDirOS, ScaleOS, emOut, aOut);
    EmissionColor = (half3)emOut;
    Alpha = (half)aOut;
}

#endif // FORCEFIELD_CRACKLE_INCLUDED
