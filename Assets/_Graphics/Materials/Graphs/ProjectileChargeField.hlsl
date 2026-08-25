// ProjectileChargeField.hlsl
//
// The SELF-DRIVEN member of the forcefield-crackle family: a charge shell that
// crackles on its own instead of being told where it was hit.
//
// Why it exists: the Sparrow's rounds SWELL as they fly (MASS in-flight growth,
// `Projectile.ApplyFlightGrowth`). Growing the tracer MODEL to sell that read as a
// small ship firing cannonballs, so the model now stays the size it left the muzzle
// and this shell draws the hit volume instead — see-through, so a pilot can still
// see the arena through their own enormous bullets.
//
// The difference from `ForcefieldCrackle.hlsl` is the DRIVER, not the language. There,
// arcs radiate from impact points a controller pushes in every frame through a
// MaterialPropertyBlock — correct for one skimmer, ruinous for the ~54 rounds a single
// Sparrow has in the air at 90 volleys/s (a per-renderer property block is also a per-
// renderer draw call). Here every arc is a function of TIME and the shell's OWN
// object-to-world matrix, so there is no per-frame CPU write, no property block, and
// every round in the match batches through one material.
//
// Inputs:
//   float3 ObjectPosition - object-space fragment position (unit sphere, radius 0.5)
//   float3 ObjectNormal   - object-space normal
//   float3 ViewDirOS      - object-space view direction (camera - fragment)
//   float  Phase          - animation phase in seconds, already carrying this round's
//                           own offset (see the vertex shader: the shell's world radius
//                           is what decorrelates rounds, so two shots fired 11 ms apart
//                           are at different sizes and therefore different phases —
//                           a stable per-round offset that drifts CONTINUOUSLY as the
//                           round grows, so nothing ever pops. A world-POSITION hash
//                           would re-roll every frame; a constant would strobe the
//                           whole volley in unison.)
//   float  Charge01       - 0..1, how far this round has charged, from its world radius
//
// Outputs:
//   float3 EmissionColor  - additive emission RGB
//   float  Alpha          - coverage (the shell is additive; alpha rides along for
//                           anything that wants it)

#ifndef PROJECTILE_CHARGE_FIELD_INCLUDED
#define PROJECTILE_CHARGE_FIELD_INCLUDED

// ─── Noise helpers ──────────────────────────────────────────────────────────
// Deliberately the same functions ForcefieldCrackle.hlsl uses, duplicated rather
// than shared: that file declares its impact arrays and every visual property at
// FILE SCOPE, which would collide with this shader's UnityPerMaterial cbuffer and
// cost it SRP Batcher compatibility — the one thing that makes 54 simultaneous
// shells affordable.

float ChargeHash1(float n)
{
    return frac(sin(n) * 43758.5453123);
}

float ChargeValueNoise1D(float x)
{
    float i = floor(x);
    float f = frac(x);
    f = f * f * (3.0 - 2.0 * f);
    return lerp(ChargeHash1(i), ChargeHash1(i + 1.0), f);
}

float ChargeFBM1D(float x, int octaves)
{
    float value = 0.0;
    float amplitude = 0.5;
    float frequency = 1.0;

    for (int o = 0; o < octaves; o++)
    {
        value += amplitude * (ChargeValueNoise1D(x * frequency) * 2.0 - 1.0);
        frequency *= 2.17;
        amplitude *= 0.5;
    }
    return value;
}

// ─── Main function ──────────────────────────────────────────────────────────

void ProjectileChargeField_float(
    float3 ObjectPosition,
    float3 ObjectNormal,
    float3 ViewDirOS,
    float  Phase,
    float  Charge01,
    out float3 EmissionColor,
    out float  Alpha)
{
    float3 fragDir = normalize(ObjectPosition);

    // Fresnel rim — the shell's only always-on term, and what makes an otherwise
    // near-empty sphere read as a boundary at all. Scaled by charge so a round that
    // has barely grown is a hint and a fully-charged one is a lantern.
    float3 N = normalize(ObjectNormal);
    float3 V = normalize(ViewDirOS);
    float NdotV = saturate(dot(N, V));
    float charge = saturate(Charge01);
    float fresnel = pow(1.0 - NdotV, _FresnelRimPower) * _FresnelRimIntensity
                  * lerp(_ChargeFloor, 1.0, charge);

    Alpha = fresnel;
    EmissionColor = _FresnelRimColor.rgb * fresnel;

    int seedCount = (int)_ArcSeeds;
    float arcGain = lerp(_ChargeFloor, 1.0, charge);

    float totalContribution = 0;
    float3 totalColor = float3(0, 0, 0);

    for (int i = 0; i < 6; i++)
    {
        if (i >= seedCount) break;

        // One discharge CYCLE per seed. The envelope is zero at BOTH ends of a cycle
        // (unlike the skimmer's impact, which starts at full flash because something
        // just hit it) — that is what lets the seed point be re-rolled at the cycle
        // boundary without the arcs visibly teleporting.
        float offset = ChargeHash1(float(i) * 7.3 + 0.5) * 11.0;
        float cycle = Phase * _CrackleRate + offset;
        float life = frac(cycle);
        float idx = floor(cycle);

        float env = smoothstep(0.0, 0.15, life) * pow(1.0 - life, 1.5);
        if (env < 0.001) continue;

        // Seed direction, uniform on the sphere (z uniform in [-1,1], azimuth uniform).
        float h1 = ChargeHash1(idx * 3.7 + float(i) * 11.3);
        float h2 = ChargeHash1(idx * 5.1 + float(i) * 17.9);
        float z = h1 * 2.0 - 1.0;
        float az = h2 * 6.28318;
        float rr = sqrt(saturate(1.0 - z * z));
        float3 seedDir = float3(rr * cos(az), rr * sin(az), z);

        // Great-circle distance from the seed point.
        float angle = acos(clamp(dot(fragDir, seedDir), -1.0, 1.0));

        // Local frame on the sphere around the seed, for the azimuthal arc pattern.
        float3 tangent = normalize(cross(seedDir, float3(0.123, 0.456, 0.789)));
        float3 bitangent = cross(seedDir, tangent);
        float azimuth = atan2(dot(fragDir, bitangent), dot(fragDir, tangent));

        // ── Expanding wavefront ──
        float wavefrontAngle = _ArcReach * 3.14159 * saturate(life * _RippleSpeed);
        float ringWidth = _ArcReach * _RingThickness;

        float distBehindFront = wavefrontAngle - angle;
        float waveBand = smoothstep(-ringWidth * 0.1, 0.0, distBehindFront)
                       * smoothstep(ringWidth, 0.0, distBehindFront);
        waveBand *= step(angle, wavefrontAngle + ringWidth * 0.2);

        float centerGlow = smoothstep(_ArcReach * 3.14159 * _CenterFillAmount, 0.0, angle)
                         * (1.0 - life * life);

        float spatialEnvelope = max(waveBand, centerGlow);
        if (spatialEnvelope < 0.001) continue;

        // ── Electrical arcs radiating from the seed ──
        int arcCount = (int)_ArcDensity;
        float arcContrib = 0.0;
        float arcHeat = 0.0;

        for (int a = 0; a < 6; a++)
        {
            if (a >= arcCount) break;

            float baseAngle = (float(a) / float(arcCount)) * 6.28318
                            + ChargeHash1(idx * 2.9 + float(i) * 7.3) * 6.28318;

            float dAzimuth = azimuth - baseAngle;
            dAzimuth = dAzimuth - 6.28318 * round(dAzimuth / 6.28318);

            float noiseInput = angle * 15.0 + float(a) * 13.7 + float(i) * 5.3;
            float wobble = ChargeFBM1D(noiseInput, 3) * 0.3 * (angle + 0.1);

            float arcDist = abs(dAzimuth - wobble);
            float arcLine = exp(-arcDist * arcDist / (_ArcSharpness * _ArcSharpness));

            arcLine *= smoothstep(0.0, 0.05, angle);

            arcContrib = max(arcContrib, arcLine);
            arcHeat = max(arcHeat, arcLine);
        }

        float contribution = spatialEnvelope * arcContrib * env * arcGain * _ArcIntensity;

        // Blue body, DANGER-RED hot core — and the threshold is what keeps them two
        // colours instead of one. A plain lerp between a saturated blue and a saturated
        // red spends most of its range in MAGENTA, which is neither, and at `arcHeat^2`
        // that magenta was most of every arc. `_CoreThreshold` confines the red to the
        // hot centreline so the arc reads blue with a red filament inside it.
        float core = smoothstep(_CoreThreshold, 1.0, arcHeat);
        float3 arcColor = lerp(_CrackleColorB.rgb, _CrackleColorA.rgb, core);
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

void ProjectileChargeField_half(
    half3 ObjectPosition,
    half3 ObjectNormal,
    half3 ViewDirOS,
    half  Phase,
    half  Charge01,
    out half3 EmissionColor,
    out half  Alpha)
{
    float3 emOut;
    float aOut;
    ProjectileChargeField_float(ObjectPosition, ObjectNormal, ViewDirOS, Phase, Charge01, emOut, aOut);
    EmissionColor = (half3)emOut;
    Alpha = (half)aOut;
}

#endif // PROJECTILE_CHARGE_FIELD_INCLUDED
