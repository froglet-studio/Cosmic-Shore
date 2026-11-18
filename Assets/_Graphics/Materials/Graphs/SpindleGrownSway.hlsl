#ifndef SPINDLE_GROWN_SWAY_INCLUDED
#define SPINDLE_GROWN_SWAY_INCLUDED

// Grown sway: the per-vertex wave of a GROWN creature (Tools/Build/fauna_kit: the Ray and its kin), and how
// that body answers its own turning.
//
// THE WAVE. SpindleSway.hlsl bends a spindle as ONE shear along its own z. That is right for a branch, a fin, a
// fish. A grown body is many limbs that each wave their own way (a wing ripples from shoulder to tip, its hind
// edge a beat behind its leading edge; a sleeve hangs from that wing and drifts with it), and the kit already
// knows, for every vertex, the wave it rides: an offset that is
//
//     SwaySin * sin(t) + SwayCos * cos(t)          t = Clock * Frequency + Phase
//
// (a sine part and a cosine part, so a limb's own wave ADDS to the wave of the limb it grows from and every
// vertex keeps its own amplitude, direction and lag with no per-vertex phase to evaluate). The kit bakes the two
// vectors into the mesh (fauna_kit/unity_port.py: TEXCOORD1.xyz and TEXCOORD2.xyz, in the body's own space and
// units), and this adds them. Zero per-frame CPU.
//
// THE TURN (Garrett, 2026-11-18: "its rotations just happen ... i want the wings to take on chiral movements
// that match their rotations"). A body that only rotates is a model on a stick. Three terms make the two sides
// of the body do DIFFERENT, mirrored things in a turn, each driven by the body's own angular velocity, which
// GrownLimbDriver measures, springs and hands to the renderer (per creature, a MaterialPropertyBlock):
//
//   Stroke   the OUTSIDE of a turn beats harder and the inside quieter: the wave is scaled by
//            1 + dot(Stroke, position.xy). The body rows itself round.
//   Twist    in a roll the hind edges deflect like ailerons, one side up and the other down:
//            a rotation about the body's x by Twist * position.x.
//   Bend     everything far from the heart LAGS the rotation and springs back: a rotation by Bend, scaled by
//            the distance from the heart (a wingtip by the full angle, a long tail by more, the heart not at all).
//
// The driver premultiplies all three by the body's size, so nothing here needs to know it. The position is the
// BODY's own space: unity_port.py keeps every cut piece's mesh in it.
//
// Strength = 0 (the graph default) makes this an exact no-op: the whole offset is multiplied by it. That is
// what keeps every other mesh on SpindleGraph unchanged, whatever its TEXCOORD1 and TEXCOORD2 hold. And with the
// six turn inputs at 0 (their defaults, and what every renderer without a driver has) it is the wave alone.

void SpindleGrownSway_float(
    float3 PositionOS,   // object-space vertex position, after SpindleSway
    float3 SwaySin,      // TEXCOORD1.xyz - the sine part of this vertex's wave, object units
    float3 SwayCos,      // TEXCOORD2.xyz - the cosine part
    float  Clock,        // _PrismClock - the platform clock (PrismClock.Now)
    float  Phase,        // _Phase - Spindle.cs's per-spindle desync, 0..2pi
    float  Strength,     // _GrownSway - 0 = no grown sway at all, 1 = as authored
    float  Frequency,    // _GrownFrequency - radians per second
    float  BendX,        // _GrownBendX/Y/Z - the lag rotation (radians at the wingtip), over the body's reach
    float  BendY,
    float  BendZ,
    float  StrokeX,      // _GrownStrokeX/Y - how much harder the outside of the turn beats, over the body's reach
    float  StrokeY,
    float  Twist,        // _GrownTwist - the aileron deflection (radians at the wingtip), over the body's reach
    out float3 Out)
{
    float t = Clock * Frequency + Phase;
    float3 p = PositionOS;

    float3 wave = (SwaySin * sin(t) + SwayCos * cos(t)) * max(0.0, 1.0 + StrokeX * p.x + StrokeY * p.y);
    float3 lag = cross(float3(BendX, BendY, BendZ), p) * length(p);
    float3 aileron = cross(float3(Twist * p.x, 0.0, 0.0), p);

    Out = p + Strength * (wave + lag + aileron);
}

#endif
