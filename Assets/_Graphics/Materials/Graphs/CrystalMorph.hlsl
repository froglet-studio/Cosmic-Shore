// CrystalMorph.hlsl — a crystal's body carried onto another shape, on the clock.
//
// The GPU half of the Squirrel's omni-crystal morph
// (_Scripts/Controller/Vessel/R_VesselActions/SQUIRREL_CRYSTAL_MORPH.md). The CPU bakes a
// per-vertex TARGET into TEXCOORD2 and stamps three numbers once; the vertex stage runs
// the whole animation from there, so it costs nothing per frame and nothing per vertex on
// the CPU — the same contract Docs/PRISM_ANIMATION.md §4 puts on every prism visual.
//
// Shader Graph usage: Custom Function node, Source = this file, function name WITHOUT the
// _float suffix. Wire the UNEXPOSED _PrismClock property (published once per frame by
// PrismClock's publisher, from the SAME value the stamp uses) into Clock — never a Time
// node, which is a different clock domain and renders every stamp pre-finished.
//
// There are TWO functions here and they are one animation: `CrystalMorph` moves the vertex and
// `CrystalMorphNormal` turns its normal, both off the same stamp and the same per-face phase.
// Splice both or neither — a shape that arrives without its shading is the seam this exists to
// remove, wearing a different costume.
//
// Spliced onto the very END of the vertex-position chain, so `Position` arrives with every
// other vertex effect already applied (on ShepardGraph, the shell's outward displacement).
// That placement is load-bearing in BOTH directions: at t = 0 the output is that position
// verbatim, so the morph starts EXACTLY as the crystal was drawing — displacement, shell
// band and all — and at t = 1 it is the bare target, so the displacement is gone and the
// shape lands on the geometry it was fitted to instead of hovering a shell above it.

#ifndef CRYSTAL_MORPH_INCLUDED
#define CRYSTAL_MORPH_INCLUDED

// -----------------------------------------------------------------------------
// Position -> Target, eased, with a per-face stagger.
//
//   Position  object-space vertex position, post every other vertex effect
//   Target    xyz = this vertex's destination (object space), w = its face's PHASE [0,1]
//   Clock     _PrismClock
//   Morph     x = stamped start time, y = duration (seconds), z = stagger [0,1)
//
// PHASE is what lets one mesh carry two different jobs at once: the crystal's strut faces
// are stamped phase 0 and are absorbed FIRST, while the panels that become the octahedra's
// faces are stamped late and land LAST — so the leftovers are already gone by the time the
// shape they were absorbed into is finished. Stagger 0 collapses that to one synchronised
// move.
//
// Duration <= 0 means UNSTAMPED and returns Position untouched. Every crystal material in
// the project carries this node with (0, 0, 0) and is therefore bit-identical to before it
// existed; only an object the morph has stamped moves.
// -----------------------------------------------------------------------------
void CrystalMorph_float(float3 Position, float4 Target, float Clock, float3 Morph,
    out float3 Out)
{
    float duration = Morph.y;
    if (duration <= 0.0)
    {
        Out = Position;
        return;
    }

    float t = saturate((Clock - Morph.x) / duration);

    // Each face gets the same LENGTH of travel, offset by its phase, so a staggered face
    // is not also a faster one. span is what is left of the window after the stagger.
    float stagger = saturate(Morph.z);
    float span = max(1e-4, 1.0 - stagger);
    float e = saturate((t - saturate(Target.w) * stagger) / span);

    e = e * e * (3.0 - 2.0 * e);   // smoothstep: zero end tangents, so it settles rather than arrives
    Out = lerp(Position, Target.xyz, e);
}

// -----------------------------------------------------------------------------
// Normal -> Target normal, on EXACTLY the same schedule as the position above.
//
//   Normal    object-space vertex normal, post every other vertex effect
//   Target    xyz = this vertex's destination NORMAL, w = its face's PHASE (the same
//             value baked into the position target, duplicated so the two cannot drift)
//   Clock     _PrismClock
//   Morph     the same (start, duration, stagger) the position node is given
//
// This is not a nicety. Both shaders involved in the hand-off derive their base colour from
// `(1 - N.V)^4` (the shared FresnelColors subgraph), so a morph that carried only POSITION
// would land the crystal cage's normals on the octahedron's faces: the right shape wearing
// the wrong surface, which reads as a lighting pop at the exact instant the animation is
// supposed to be invisible.
//
// The lerp is between two unit vectors and is renormalised, which is a chord rather than an
// arc — it is not slerp, and near-antipodal endpoints pass close to zero. That is acceptable
// here and deliberately not "fixed": a face's own normal and its target face's normal are
// close for most of the assignment (the panel was chosen by angular fit), the traversal is a
// fraction of a second, and slerp on a per-vertex path would cost a trig pair per vertex for
// a difference nobody can see. The degenerate case is guarded, not ignored.
//
// Duration <= 0 means UNSTAMPED and returns Normal untouched, exactly like the position node.
// -----------------------------------------------------------------------------
void CrystalMorphNormal_float(float3 Normal, float4 Target, float Clock, float3 Morph,
    out float3 Out)
{
    float duration = Morph.y;
    if (duration <= 0.0)
    {
        Out = Normal;
        return;
    }

    float t = saturate((Clock - Morph.x) / duration);

    float stagger = saturate(Morph.z);
    float span = max(1e-4, 1.0 - stagger);
    float e = saturate((t - saturate(Target.w) * stagger) / span);

    e = e * e * (3.0 - 2.0 * e);
    float3 n = lerp(Normal, Target.xyz, e);
    float len = length(n);
    Out = len > 1e-5 ? n / len : Target.xyz;
}

#endif // CRYSTAL_MORPH_INCLUDED
