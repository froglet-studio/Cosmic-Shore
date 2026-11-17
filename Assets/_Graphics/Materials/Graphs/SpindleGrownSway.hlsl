#ifndef SPINDLE_GROWN_SWAY_INCLUDED
#define SPINDLE_GROWN_SWAY_INCLUDED

// Grown sway: the per-vertex wave of a GROWN creature (Tools/Build/fauna_kit: the Ray and its kin).
//
// SpindleSway.hlsl bends a spindle as ONE shear along its own z. That is right for a branch, a fin, a
// fish. A grown body is many limbs that each wave their own way (a wing ripples from shoulder to tip,
// its hind edge a beat behind its leading edge; a sleeve hangs from that wing and drifts with it), and
// the kit already knows, for every vertex, the wave it rides: an offset that is
//
//     SwaySin * sin(t) + SwayCos * cos(t)          t = Clock * Frequency + Phase
//
// (a sine part and a cosine part, so a limb's own wave ADDS to the wave of the limb it grows from and
// every vertex keeps its own amplitude, direction and lag with no per-vertex phase to evaluate). The
// kit bakes the two vectors into the mesh (fauna_kit/unity_port.py: TEXCOORD1.xyz and TEXCOORD2.xyz,
// in the body's own space and units), and this adds them. Zero per-frame CPU, like every clock
// material (Docs/PRISM_ANIMATION.md); the body's prisms ride the same wave from the same clock on the
// CPU (GrownLimbDriver), because a prism is a collider and must be where it is drawn.
//
// Strength = 0 (the graph default) makes this an exact no-op: the whole offset is multiplied by it.
// That is what keeps every other mesh on SpindleGraph unchanged, whatever its TEXCOORD1 and TEXCOORD2
// hold (the braid's LOD meshes use them for something else; most meshes have none).

void SpindleGrownSway_float(
    float3 PositionOS,   // object-space vertex position, after SpindleSway
    float3 SwaySin,      // TEXCOORD1.xyz - the sine part of this vertex's wave, object units
    float3 SwayCos,      // TEXCOORD2.xyz - the cosine part
    float  Clock,        // _PrismClock - the platform clock (PrismClock.Now)
    float  Phase,        // _Phase - Spindle.cs's per-spindle desync, 0..2pi
    float  Strength,     // _GrownSway - 0 = no grown sway at all, 1 = the wave as authored
    float  Frequency,    // _GrownFrequency - radians per second
    out float3 Out)
{
    float t = Clock * Frequency + Phase;
    Out = PositionOS + Strength * (SwaySin * sin(t) + SwayCos * cos(t));
}

#endif
