#ifndef SPINDLE_SWAY_INCLUDED
#define SPINDLE_SWAY_INCLUDED

// Spindle sway — the GPU half of "a spindle is a living limb, not a rod".
//
// Docs/PRISM_ANIMATION.md's clock-material law in its purest form: ZERO per-frame
// CPU. Everything here is a function of (object-space position, the platform clock,
// the per-spindle phase Spindle.cs already stamps, two per-material constants), so a
// thousand swaying spindles cost exactly what a thousand still ones did.
//
// THE BEND IS A SHEAR, AND THAT IS WHAT MAKES IT UNIT-FREE.
// One material (SpindleMaterial) is shared by twelve prefabs whose meshes disagree
// about scale by three orders of magnitude — a gyroid branch spans ~1 object unit,
// the QuadFish body spans 349. A displacement authored in absolute units would be
// invisible on one and catastrophic on the other. So the lateral offset is a
// FRACTION OF THE DISTANCE ALONG THE SPINDLE'S OWN AXIS:
//
//     offset.x = Amplitude * PositionOS.z * sin(...)
//
// which is first-order bending. Three properties fall out for free and none of them
// had to be authored: it is exactly zero at the root (z = 0), so a spindle can never
// tear away from whatever it is attached to; it grows toward the tip, which is what
// a frond, a fin and a tail all do; and `Amplitude` is a dimensionless SLOPE, so the
// same number means the same visual bend on every mesh that shares the material.
// (atan(0.12) ≈ 6.8° of tip deflection.)
//
// +Z IS THE SPINDLE'S LENGTH. This is the platform's prism/spindle pose convention —
// `SpawnPoint.LookRotation(fwd, up)` puts local +Z on the branch direction, and every
// shipped spindle prefab is scaled on z to match (Branch 6.2, TadpoleSpindle 3.0).
//
// WHY TWO WAVES AND NOT ONE. A purely planar swish disappears when you view it
// edge-on, which for a forest of branches means a third of them look dead from any
// given camera. The secondary term rides a different axis, a subordinate weight and
// an incommensurable frequency ratio, so the tip traces a slow open Lissajous that
// never repeats and is never still from any angle — while staying clearly lateral, so
// a fish still reads as swishing rather than corkscrewing.
//
// Amplitude = 0 (the graph default) makes this an exact no-op: both sine terms are
// multiplied by it, so a material that does not author the property is bit-identical
// to the un-swayed shader. That is what keeps BranchingMembraneMaterial and
// SparrowExhaustProjectile — the two non-spindle materials on this graph — unchanged.

// The secondary axis's share of the primary amplitude. Subordinate on purpose: at 1.0
// the motion is a circle (a corkscrew), at 0 it is planar (invisible edge-on).
#define SPINDLE_SWAY_SECONDARY_WEIGHT 0.45

// Frequency ratio between the two axes. Irrational-ish so the pair never re-phases
// into a repeating figure — a rational ratio would settle into a visible loop.
#define SPINDLE_SWAY_SECONDARY_RATIO 0.73

void SpindleSway_float(
    float3 PositionOS,   // object-space vertex position; +Z runs the spindle's length
    float  Clock,        // _PrismClock — the platform clock (PrismClock.Now)
    float  Phase,        // _Phase — Spindle.cs's per-spindle desync, 0..2pi
    float  Amplitude,    // dimensionless bend slope; 0 = no sway at all
    float  Frequency,    // radians per second
    out float3 Out)
{
    float t = Clock * Frequency + Phase;

    // Span along the spindle. Signed, so a mesh whose origin sits mid-body (the
    // QuadFish: nose at +132, tail at -217) pivots about its middle and the longer
    // end travels further — a fish's tail out-swinging its head, for free.
    float span = PositionOS.z * Amplitude;

    float3 p = PositionOS;
    p.x += span * sin(t);
    p.y += span * sin(t * SPINDLE_SWAY_SECONDARY_RATIO + Phase + 1.5707963)
                * SPINDLE_SWAY_SECONDARY_WEIGHT;

    Out = p;
}

#endif
