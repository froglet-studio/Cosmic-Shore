#ifndef PRISM_SWAY_INCLUDED
#define PRISM_SWAY_INCLUDED

// Prism sway — a LIVING health prism rides its lifeform's sway.
//
// §44 gave every spindle a GPU bend and left the conserved mass bolted to it rigid, so
// a Clawfish's fin bent away from the four ribs lying on it and the lockup that reads as
// ONE creature came apart the moment the sway was visible. This is the other half.
//
// THE SWAY IS A PROPERTY OF THE LIMB, NOT OF THE MESH. `SpindleSway` displaces a vertex
// by a pure SHEAR along the limb's own +z:
//
//     offset = z_limb * (Amplitude * sin(t),  Amplitude * W * sin(t'),  0)      [limb space]
//
// and the key property is that the offset is a function of z ALONE — every point at the
// same height on the limb moves identically, whatever its x and y. So a prism bolted to
// the limb does not need to know where around it it sits; it needs its own z, and the
// limb's axes. Evaluate the SAME field at the prism's own vertices and the prism and the
// limb surface move together EXACTLY, not approximately. That exactness is the whole
// point: an approximation here reads as the rib sliding on the fin.
//
// WHAT IS BAKED, AND WHY IT IS BAKED. All four inputs are constants of the prism's
// attachment, computed once when it is bound (Docs/PRISM_ANIMATION.md's clock-material
// law: stamp the initial conditions, then the GPU runs the course). A prism does not move
// relative to the limb it is part of, so nothing here is ever re-computed:
//
//   SpanX / SpanY  the limb's +x / +y, expressed in THIS PRISM'S object space and
//                  premultiplied by the limb's sway Amplitude. The change of basis is
//                  what makes a rib pitched 17.7 degrees off the fin bend with the fin
//                  rather than across it, and it carries the prism's own leaf scale, so
//                  a non-uniformly-scaled prism is handled for free.
//   Axis           the limb's +z as a linear FUNCTIONAL on this prism's object space, so
//                  `dot(PositionOS, Axis)` is that vertex's height up the limb measured
//                  from the prism's own origin.
//   Timing         (Frequency rad/s, Phase rad, Z0) — the limb's own two wave constants
//                  plus the prism ORIGIN's height up the limb. Three scalars in one
//                  float3 because the prism graphs carry Vector1 and Vector3 property
//                  donors and no Vector4 one (same ruling as `_JiggleParams`).
//
// SpanX = SpanY = 0 is an exact, bit-identical no-op, and it is the DEFAULT: a vessel's
// trail prism, a cell's authored environment and the skeleton left behind by a dead
// lifeform all stay perfectly still. That is not just a safe default, it is the feature —
// **living mass is the mass that moves**, and a player can tell a plant that is alive
// from the husk of one that is not without being told.
//
// The two shared wave constants come from SpindleSway.hlsl itself rather than being
// copied, because a prism that swayed at a different ratio or weight from the limb it is
// bolted to would drift across it over exactly the timescale the ratio was chosen to make
// non-repeating — i.e. the failure would be invisible for the first few seconds.
#include "SpindleSway.hlsl"

void PrismSway_float(
    float3 PositionOS,   // object-space vertex position
    float  Clock,        // _PrismClock — the platform clock (PrismClock.Now)
    float3 SpanX,        // limb +x in prism object space, x the limb's Amplitude
    float3 SpanY,        // limb +y in prism object space, x the limb's Amplitude
    float3 Axis,         // limb +z as a functional on prism object space
    float3 Timing,       // (Frequency rad/s, Phase rad, Z0 = the prism origin's limb height)
    out float3 Out)
{
    float freq  = Timing.x;
    float phase = Timing.y;
    float t = Clock * freq + phase;

    // This vertex's height up the limb: the prism origin's, plus how far this vertex
    // reaches along the limb's axis. For a vertex of the LIMB'S OWN mesh (Axis = +z,
    // Z0 = 0) this is exactly PositionOS.z, which is what makes the two shaders one
    // field rather than two that happen to agree.
    float zl = Timing.z + dot(PositionOS, Axis);

    // Identical wave pair to SpindleSway: same primary sine, same secondary axis on the
    // shared subordinate weight and incommensurable ratio, same quarter-turn offset.
    //
    // THE HEIGHT IS FOLDED IN FIRST, and the order is load-bearing rather than tidy.
    // SpindleSway computes `span = PositionOS.z * Amplitude` and only then multiplies by
    // the sine; `(Amplitude*height) * sin` and `Amplitude * (height*sin)` are the SAME
    // real number and different float32s. Scaling the span first makes the two shaders
    // agree to the last bit rather than to a tolerance, which is what lets the verifier
    // assert one field instead of two that look alike (verify_prism_sway.py T3).
    float3 spanX = SpanX * zl;
    float3 spanY = SpanY * zl;

    Out = PositionOS
        + spanX * sin(t)
        + spanY * sin(t * SPINDLE_SWAY_SECONDARY_RATIO + phase + 1.5707963)
                * SPINDLE_SWAY_SECONDARY_WEIGHT;
}

#endif
