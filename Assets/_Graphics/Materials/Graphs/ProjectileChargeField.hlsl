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
// ─── ONE ROUND IS ONE STROKE. THE VOLLEY IS THE SPHERE. ─────────────────────────────
//
// A round draws a SINGLE arc: one wobbling bolt lying on one randomly-oriented great
// circle of the shell, striking outward from a point and fading. It is deliberately
// NOT a crackling ball — three simultaneous seeds each throwing five radiating
// filaments, over a lit centre and a standing fresnel rim, averaged into exactly the
// glowing sphere this shell exists to avoid drawing. The sphere is meant to be
// assembled by the PLAYER, not by the fragment: consecutive rounds get different great
// circles (see Phase, below), so a burst lays down stroke after stroke at different
// orientations and the shape accumulates as an after-image. Anything that puts a second
// bright always-on term on this shell — a centre glow, a bright rim, more simultaneous
// arcs — takes that back and hands the sphere to one round again.
//
// The stroke is also what still makes the shell an honest INSTRUMENT for the hit
// volume: it lies exactly on the shell's surface, so it is a curve of exactly the hit
// radius. The rim is kept only as a whisper — enough that a round is never fully dark
// between discharges (continuity of existence), far too dim to read as a boundary.
//
// Inputs:
//   float3 ObjectPosition - object-space fragment position (unit sphere, radius 0.5)
//   float3 ObjectNormal   - object-space normal
//   float3 ViewDirOS      - object-space view direction (camera - fragment)
//   float  Phase          - animation phase in seconds, already carrying this round's
//                           own offset — see ProjectileChargeFieldPhase_float below, which
//                           is where every per-round difference is decided.)
//   float  Charge01       - 0..1, how far this round has charged, from its world radius
//   float  Lateral        - the round's lateral identity, 0..2 (see the phase resolver): it
//                           spins this round's great circle, which is what stops a volley's
//                           two muzzles drawing the same stroke twice
//
// Outputs:
//   float3 EmissionColor  - additive emission RGB
//   float  Alpha          - coverage (the shell is additive; alpha rides along for
//                           anything that wants it)

#ifndef PROJECTILE_CHARGE_FIELD_INCLUDED
#define PROJECTILE_CHARGE_FIELD_INCLUDED

#define PCF_TAU 6.28318530718

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


// ─── Per-round phase ────────────────────────────────────────────────────────
//
// Everything that makes one round's stroke different from another's is decided here, out
// of the shell's own object-to-world matrix and nothing else. It lives in this file rather
// than inline in the vertex shader so the verification harness measures the SHIPPED
// decorrelation instead of a transcription of it.
//
// The gun has TWO muzzles and fires both in the same tick, so a volley's pair shares a
// growth factor, a flight progress and therefore a world radius — EXACTLY. Radius alone
// was the whole per-round signal, so the pair were clones: same great circle, same point
// in the cycle, side by side for their whole flight. Two synchronised strokes read as one
// deliberate shape, which is the opposite of the stochastic fill the shell exists for.
//
// What separates them is the one thing that is different about them by construction: the
// muzzles sit 6.4 units apart across the ship (local x = ±3.2). A round flies along its
// own forward, and `SafeLookRotation` builds its frame with forward as +z — so its two
// LATERAL world coordinates are invariant along the flight while differing between the
// muzzles by that 6.4. Free per-round identity out of a matrix that was already being read.
//
// Two details are load-bearing:
//   * TWO axes, not one. The round's frame comes from `LookRotation(aim)` with WORLD up, so
//     it does not roll with the ship — the muzzle separation lands on +x at zero roll and on
//     +y at 90°. Reading only one axis would resynchronise the pair every quarter roll.
//   * NOISE, not a linear term. Any linear combination `a·latX + b·latY` has a null
//     direction, so there is always a roll angle at which the pair collapses back together.
//     Two independent smooth noises have no such direction, and "the two muzzles get
//     unrelated strokes" is exactly the stochastic claim being made.
//
// The lateral term costs NO extra per-round flicker — it is constant along a straight
// flight — unlike the radius term, which is simultaneously a round's identity and its
// progress. (A vessel drifting hard inherits some lateral velocity into the shot, which
// makes it drift slowly rather than sit still. That is extra variety, not a defect.)
void ProjectileChargeFieldPhase_float(
    float3 AxisX,        // object-to-world column 0 — its LENGTH is the shell's diameter
    float3 AxisY,        // object-to-world column 1
    float3 OriginWS,     // object-to-world column 3 — the round's world position
    float  TimeSeconds,
    out float Phase,
    out float Charge01,
    out float Lateral)
{
    float diameter = length(AxisX);
    float worldRadius = 0.5 * diameter;

    float3 right = AxisX / max(diameter, 1e-6);
    float3 up    = AxisY / max(length(AxisY), 1e-6);

    Lateral = ChargeValueNoise1D(dot(OriginWS, right) * _LateralNoiseScale)
            + ChargeValueNoise1D(dot(OriginWS, up) * _LateralNoiseScale * 1.37 + 19.7);
    float lateral = Lateral;

    Phase = TimeSeconds * _PhaseSpeed
          + worldRadius * _PhaseByRadius
          + lateral * _PhaseByLateral;

    Charge01 = saturate(worldRadius / max(_ChargeReferenceRadius, 1e-3));
}

// ─── Main function ──────────────────────────────────────────────────────────

void ProjectileChargeField_float(
    float3 ObjectPosition,
    float3 ObjectNormal,
    float3 ViewDirOS,
    float  Phase,
    float  Charge01,
    float  Lateral,
    out float3 EmissionColor,
    out float  Alpha)
{
    float3 fragDir = normalize(ObjectPosition);
    float charge = saturate(Charge01);
    float gain = lerp(_ChargeFloor, 1.0, charge);

    // Fresnel rim — deliberately a WHISPER, and the only always-on term. It exists so a
    // round is never *fully* dark between strokes, not so it can be read as a boundary:
    // a bright standing rim is a sphere drawn by one round, which is the thing this pass
    // removed. Raise `_ArcIntensity` if the shell needs to be louder; never this.
    float3 N = normalize(ObjectNormal);
    float3 V = normalize(ViewDirOS);
    float NdotV = saturate(dot(N, V));
    float fresnel = pow(1.0 - NdotV, _FresnelRimPower) * _FresnelRimIntensity * gain;

    int arcCount = (int)_ArcCount;
    float totalContribution = 0.0;
    float3 totalColor = float3(0.0, 0.0, 0.0);

    for (int i = 0; i < 4; i++)
    {
        if (i >= arcCount) break;

        // One discharge CYCLE per arc. The envelope is zero at BOTH ends of a cycle
        // (unlike the skimmer's impact, which starts at full flash because something
        // just hit it) — that is what lets the great circle be re-rolled at the cycle
        // boundary without the stroke visibly teleporting.
        float offset = ChargeHash1(float(i) * 7.3 + 0.5) * 11.0;
        float cycle = Phase * _CrackleRate + offset;
        float life = frac(cycle);
        float idx = floor(cycle);

        // Strike, HOLD, snuff. The hold is what stops the shell twinkling: with a bare
        // strike-and-decay a round spent a third of every cycle below the eye's threshold
        // and the stream read as a third of its rounds being dark. A plateau keeps a bolt
        // on the shell essentially always (measured 84% of the cycle above threshold at the
        // dimmest charge) while still reaching zero at BOTH ends, which is the property that
        // lets the great circle be re-rolled at the boundary without the stroke teleporting.
        float strike01 = saturate(life / max(_StrikeTime, 1e-3));
        float env = smoothstep(0.0, 0.07, life)
                  * pow(smoothstep(1.0, min(_HoldTime, 0.98), life), _FadeShape);
        if (env < 0.002) continue;

        // ── The great circle this stroke is drawn on ──
        // Its pole is uniform on the sphere (z uniform in [-1,1], azimuth uniform), so
        // over a burst the strokes have no preferred plane and the accumulated shape is
        // a sphere rather than a band.
        float h1 = ChargeHash1(idx * 3.7 + float(i) * 11.3);
        float h2 = ChargeHash1(idx * 5.1 + float(i) * 17.9);
        float h3 = ChargeHash1(idx * 9.3 + float(i) * 23.1);

        // `Lateral` SPINS the circle about the shell's z axis, and that — not the phase
        // offset it also feeds — is what actually splits a volley's pair onto different
        // PLANES. Two rounds 0.2 of a cycle apart still share `idx`, so they draw the SAME
        // great circle at two draw stages, which is exactly the twinning being fixed; a
        // spin changes the circle itself no matter how close their cycles are.
        //
        // It enters through cos/sin only, so it is perfectly continuous and wraps for free:
        // a pilot drifting hard inherits lateral velocity into the shot, which makes the
        // stroke's plane WALK slowly around the shell instead of popping — the same reason
        // the envelope goes to zero at both ends of a cycle. A round flying straight has an
        // exactly constant Lateral and therefore an exactly fixed plane.
        float pz = h1 * 2.0 - 1.0;
        float paz = h2 * PCF_TAU + Lateral * _LateralPoleSpin;
        float pr = sqrt(saturate(1.0 - pz * pz));
        float3 pole = float3(pr * cos(paz), pr * sin(paz), pz);

        // Orthonormal basis IN the great-circle plane. The reference vector swaps away
        // from the pole so the cross product never degenerates.
        float3 refv = abs(pole.z) < 0.9 ? float3(0.0, 0.0, 1.0) : float3(1.0, 0.0, 0.0);
        float3 u = normalize(cross(refv, pole));
        float3 v = cross(pole, u);

        // Fragment coordinates on that frame: signed height off the plane (the sine of
        // the angle away from the circle) and the angle along it.
        float height = dot(fragDir, pole);
        float theta = atan2(dot(fragDir, v), dot(fragDir, u));

        // ── The bolt wanders off the exact circle ──
        // Without this the stroke is a drafted arc; with it, it is lightning that happens
        // to be following one.
        float wob = ChargeFBM1D(theta * _ArcWanderScale + idx * 4.7 + float(i) * 3.1, 3)
                  * _ArcWander;
        float d = height - wob;
        float sharp = max(_ArcSharpness, 1e-3);
        float stroke = exp(-(d * d) / (sharp * sharp));
        if (stroke < 0.002) continue;

        // ── The stroke DRAWS ITSELF, outward from a random point on the circle ──
        // Both ways at once, so the segment needs no wrap handling for any span up to the
        // full circle, and so a round caught early in its cycle still reads as a stroke
        // rather than as a dot. Rounds that share a great circle (neighbours in the
        // stream, whose radii are close) are at different points of this draw, so they
        // are never clones of each other.
        float start = h3 * PCF_TAU;
        float dTheta = theta - start;
        dTheta = dTheta - PCF_TAU * round(dTheta / PCF_TAU);   // wrap to [-pi, pi]
        float away = abs(dTheta);

        float halfSpan = _ArcSpan * 0.5;
        float reach = halfSpan * strike01;
        float soften = max(halfSpan * 0.35, 0.08);
        float along = 1.0 - smoothstep(reach - soften, reach, away);

        // The advancing ends are the hot part, while the bolt is still striking.
        float tipWidth = max(soften * 0.5, 0.04);
        float tipDist = away - reach;
        float tip = exp(-(tipDist * tipDist) / (tipWidth * tipWidth))
                  * (1.0 - strike01) * _TipGlow;

        float lit = along + tip;
        float body = stroke * lit;
        float contribution = body * env * gain * _ArcIntensity;
        if (contribution < 0.001) continue;

        // Blue body, DANGER-RED hot core — and the threshold is what keeps them two
        // colours instead of one. A plain lerp between a saturated blue and a saturated
        // red spends most of its range in MAGENTA, which is neither, and at `body^2`
        // that magenta was most of every arc. `_CoreThreshold` confines the red to the
        // hot centreline (and to the striking tips) so the arc reads blue with a red
        // filament inside it.
        float heat = saturate(body);
        float core = smoothstep(_CoreThreshold, 1.0, heat);
        float3 arcColor = lerp(_CrackleColorB.rgb, _CrackleColorA.rgb, core);
        arcColor *= 1.0 + heat * 2.0;

        totalContribution += contribution;
        totalColor += arcColor * contribution;
    }

    totalContribution = saturate(totalContribution);

    Alpha = saturate(totalContribution + fresnel);
    EmissionColor = totalContribution > 0.001
        ? (totalColor / max(totalContribution, 0.001)) * totalContribution + _FresnelRimColor.rgb * fresnel
        : _FresnelRimColor.rgb * fresnel;
}

void ProjectileChargeFieldPhase_half(
    half3 AxisX, half3 AxisY, half3 OriginWS, half TimeSeconds,
    out half Phase, out half Charge01, out half Lateral)
{
    float p, c, l;
    ProjectileChargeFieldPhase_float(AxisX, AxisY, OriginWS, TimeSeconds, p, c, l);
    Phase = (half)p;
    Charge01 = (half)c;
    Lateral = (half)l;
}

void ProjectileChargeField_half(
    half3 ObjectPosition,
    half3 ObjectNormal,
    half3 ViewDirOS,
    half  Phase,
    half  Charge01,
    half  Lateral,
    out half3 EmissionColor,
    out half  Alpha)
{
    float3 emOut;
    float aOut;
    ProjectileChargeField_float(ObjectPosition, ObjectNormal, ViewDirOS, Phase, Charge01, Lateral, emOut, aOut);
    EmissionColor = (half3)emOut;
    Alpha = (half)aOut;
}

#endif // PROJECTILE_CHARGE_FIELD_INCLUDED
