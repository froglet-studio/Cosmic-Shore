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
// arcs radiate from impact points a controller pushes in EVERY FRAME through a
// MaterialPropertyBlock — correct for one skimmer, ruinous for the ~54 rounds a single
// Sparrow has in the air at 90 volleys/s. Here an arc is a function of TIME, the shell's
// OWN object-to-world matrix, and ONE float stamped per SHOT — so there is no per-frame
// CPU write at all, and the material is GPU-INSTANCED: every shell shares mesh and
// material and they batch into one instanced draw, with that float as per-instance data.
// (It used to claim "no property block, SRP-batched". That was true and it was also why
// every round in a burst drew the identical stroke — see the phase resolver below.)
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
//   float  Seed           - this round's identity, 0..1, stamped per SHOT through the GPU
//                           instancing buffer. It picks the circle's angle and tilt, the bolt's
//                           jaggedness and the round's point in its own discharge cycle — see
//                           the phase resolver for why nothing implicit could do that job.
//   float3 ViewAxisOS     - object-space direction from the round to the CAMERA, per OBJECT
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
// FILE SCOPE, which would collide with this shader's UnityPerMaterial cbuffer.

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
// A round's identity is an explicit per-instance SEED, stamped once at launch by
// `Projectile.StampChargeFieldSeed` and delivered through the GPU-instancing buffer. It is
// there because the shell has NO implicit signal that can tell two rounds apart, and that is
// arithmetic rather than opinion:
//
//   * WORLD RADIUS changes by 0.0183 u between consecutive volleys (90 volleys/s over a 0.3 s
//     flight). Turning that into half a discharge cycle needs `_PhaseByRadius * _CrackleRate`
//     ~= 27, which makes ONE round discharge at ~159 Hz — thirty bolts over its own flight.
//   * TIME is identical for every round alive at a given instant.
//   * LATERAL POSITION is identical for every round from one muzzle while the ship flies
//     straight, which is most of the time it is firing.
//
// So rounds fired 11 ms apart were, to this shader, the same round — and at 90 volleys/s that
// is every round in the stream, not just the volley's pair. Three passes tried to derive
// identity from the geometry (radius, then a lateral read, then a lateral read spinning the
// circle) and all three were measured decorrelated and still read as identical, because what
// they decorrelated was a difference the signal did not actually carry.
//
// The cost is one `SetPropertyBlock` per SHOT — not per frame — and the material moves from
// SRP-batched to GPU-INSTANCED. That is the right trade for ~54 identical spheres: they still
// batch, and now they can differ. The previous "no per-instance write" claim was defending a
// batching strategy that had made the effect impossible.
void ProjectileChargeFieldPhase_float(
    float3 AxisX,        // object-to-world column 0 — its LENGTH is the shell's diameter
    float  Seed,         // per-round, 0..1
    float  TimeSeconds,
    out float Phase,
    out float Charge01)
{
    float worldRadius = 0.5 * length(AxisX);

    // The seed spans several discharge cycles, so every round is at its own point in its own
    // cycle. The radius term is what makes a single round EVOLVE across its flight; the seed is
    // what makes it different from its neighbours.
    Phase = TimeSeconds * _PhaseSpeed
          + worldRadius * _PhaseByRadius
          + Seed * _PhaseBySeed;

    Charge01 = saturate(worldRadius / max(_ChargeReferenceRadius, 1e-3));
}

void ProjectileChargeFieldPhase_half(
    half3 AxisX, half Seed, half TimeSeconds, out half Phase, out half Charge01)
{
    float p, c;
    ProjectileChargeFieldPhase_float(AxisX, Seed, TimeSeconds, p, c);
    Phase = (half)p;
    Charge01 = (half)c;
}

// ─── Main function ──────────────────────────────────────────────────────────

void ProjectileChargeField_float(
    float3 ObjectPosition,
    float3 ObjectNormal,
    float3 ViewDirOS,
    float  Phase,
    float  Charge01,
    float  Seed,
    float3 ViewAxisOS,
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

        // ── The circle is built around the VIEW AXIS, and that is the difference between
        //    an effect and an invisible one. ──
        // `Cull Back` draws only the FRONT hemisphere, so a great circle at a uniformly random
        // pole spends most of its length behind the round. Rendered at true 1080p pixel density
        // (Tools/Shaders/render_projectile_charge_field.py) most rounds past ~40 units showed no
        // stroke at all and collapsed to a plain dark disc — and every plain dark disc looks
        // exactly like every other one, which is the whole of the "they still read as identical"
        // report. Three passes of measurement (planarity, lit-set overlap, per-round brightness)
        // all said the pair were decorrelated and all of them were answering the wrong question,
        // because they counted samples over the sphere instead of asking what is on the screen.
        //
        // Anchoring the pole near the plane PERPENDICULAR to the view puts the stroke across the
        // visible face every time, so a round always shows a slash at some angle — the one
        // feature still resolvable when the whole round is 30 px wide. The per-round `Seed` sets that
        // angle and its tilt, so neighbouring rounds differ in the thing the eye can actually see.
        float3 va = normalize(ViewAxisOS);
        float3 refv = abs(va.z) < 0.9 ? float3(0.0, 0.0, 1.0) : float3(1.0, 0.0, 0.0);
        float3 e1 = normalize(cross(va, refv));
        float3 e2 = cross(va, e1);

        float ang  = h2 * PCF_TAU + Seed * _SeedSpin;
        float tilt = _ArcTiltRange * sin(h1 * PCF_TAU + Seed * _SeedTilt);
        float3 pole = normalize(cos(tilt) * (cos(ang) * e1 + sin(ang) * e2) + sin(tilt) * va);

        // With the pole perpendicular to the view, `v` comes out exactly along the view axis,
        // so theta = 0 is the point of the circle facing the camera. That is what lets the
        // stroke's centre be biased toward the visible face below.
        float3 u = normalize(cross(va, pole));
        float3 v = cross(pole, u);

        // Fragment coordinates on that frame: signed height off the plane (the sine of
        // the angle away from the circle) and the angle along it.
        float height = dot(fragDir, pole);
        float theta = atan2(dot(fragDir, v), dot(fragDir, u));

        // ── The bolt wanders off the exact circle ──
        // Without this the stroke is a drafted arc; with it, it is lightning that happens
        // to be following one.
        // The bolt's JAGGEDNESS is seeded per round too, not just per discharge — otherwise
        // neighbouring rounds draw the *same* squiggle at two angles, which at 30 px reads as
        // one shape repeated. The seed enters as a SHIFT of the noise input rather than as a
        // hash of it, so it stays continuous.
        float wob = ChargeFBM1D(theta * _ArcWanderScale + idx * 4.7 + float(i) * 3.1
                                + Seed * _SeedWobble, 3)
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
        // Centred NEAR the camera-facing point (theta = 0) rather than anywhere on the circle:
        // a stroke centred on the back half is culled down to two slivers at the limb, which is
        // the plain-disc failure again. `_ArcStartSpread` is how far it may wander.
        float start = (h3 * 2.0 - 1.0) * _ArcStartSpread;
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

void ProjectileChargeField_half(
    half3 ObjectPosition,
    half3 ObjectNormal,
    half3 ViewDirOS,
    half  Phase,
    half  Charge01,
    half  Seed,
    half3 ViewAxisOS,
    out half3 EmissionColor,
    out half  Alpha)
{
    float3 emOut;
    float aOut;
    ProjectileChargeField_float(ObjectPosition, ObjectNormal, ViewDirOS, Phase, Charge01, Seed, ViewAxisOS, emOut, aOut);
    EmissionColor = (half3)emOut;
    Alpha = (half)aOut;
}

#endif // PROJECTILE_CHARGE_FIELD_INCLUDED
