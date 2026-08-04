// PrismOcclusionCorridor.hlsl — the GPU side of the camera↔vessel occlusion corridor
// (Docs/PRISM_ANIMATION.md §3 C1 / §5 C1, the "moving-target exception" class of §1).
//
// PURPOSE. Prisms that sit between the player's camera and the player's vessel must
// not hide the ship. The corridor is the capsule swept from the camera position to
// the vessel position; a fragment inside it fades out, a fragment outside it is
// untouched.
//
// WHY IT LIVES HERE AND NOT ON THE CPU. Occlusion is camera-relative LIVE data — it
// can never be a per-prism stamp, because the answer changes every frame for every
// prism as the camera and the ship move. The law's escape hatch for exactly this
// case (PRISM_ANIMATION.md §1, "animation vs. live gameplay data") is a GLOBAL
// uniform: ONE O(1) write per frame that every prism reads, with zero per-prism CPU
// work, zero material swaps, and zero per-instance overrides. That is what this file
// consumes. The previous implementation (ClearPrisms.cs, deleted) did the opposite —
// a physics capsule trigger per vessel, a per-prism sharedMaterial swap on enter/exit
// and a per-physics-tick MaterialPropertyBlock write per tracked prism — and it was
// also structurally dead, because prisms draw through companion entities and a
// GameObject MaterialPropertyBlock never reaches the instanced batch.
//
// THE UNIFORMS (published by PrismOcclusionCorridor.cs once per frame):
//   float3 _PrismOcclusionTarget  — the vessel's world position (the far end of the
//                                   corridor; the near end is the camera, read on the
//                                   GPU so it is always exactly the rendering camera).
//   float3 _PrismOcclusionParams  — (outerRadius, innerRadius, minAlpha).
//                                   outerRadius <= 0 means "corridor off" — the very
//                                   first branch below returns the untouched alpha.
//
// COST CONTRACT. A fragment outside the corridor executes: one compare (radius > 0),
// one segment-distance evaluation (~10 ALU), one compare, then returns the alpha it
// was given and a clip threshold of 0 — no dither, no texture, no extra varying beyond
// world position, and `clip(alpha - 0)` with alpha >= 1 never discards. Both branches
// are uniform across a prism and near-uniform across a screen tile, so they are
// coherent. Nothing here changes the render queue, the batch, or the draw call count:
// corridor prisms stay in the same instanced batch as every other prism.
//
// WHY DITHER AND NOT BLENDING. The environment must stay CHEAP OPAQUE prisms — moving
// them into the transparent queue (sorting + blend + no depth write) for a corridor
// that changes every frame is exactly the cost this feature exists to avoid, and doing
// it per-prism would mean a per-prism material swap. Screen-door transparency
// (ordered 4×4 Bayer alpha-to-clip) keeps every prism in the opaque queue, needs no
// sorting, and is order-independent by construction. The trade is stated in the doc:
// it makes the prism materials alpha-tested.

#ifndef PRISM_OCCLUSION_CORRIDOR_INCLUDED
#define PRISM_OCCLUSION_CORRIDOR_INCLUDED

// Canonical 4×4 ordered Bayer index, built from the recursive construction
//   M4(x,y) = 4·M2(x&1, y&1) + M2(x>>1, y>>1),  M2(x,y) = 2·((x^y)&1) + (y&1)
// which reproduces
//    0  8  2 10
//   12  4 14  6
//    3 11  1  9
//   15  7 13  5
// exactly, with no constant array (dynamic indexing of a constant matrix compiles to
// a select chain on some targets; this is pure ALU).
float PrismOcclusionBayer4x4(uint2 p)
{
    uint xl = p.x & 1u, yl = p.y & 1u;
    uint xh = (p.x >> 1) & 1u, yh = (p.y >> 1) & 1u;
    uint m2Low = 2u * ((xl ^ yl) & 1u) + yl;
    uint m2High = 2u * ((xh ^ yh) & 1u) + yh;
    uint index = 4u * m2Low + m2High;
    // (index + 0.5)/16 keeps the threshold strictly inside (0,1): alpha 1 is never
    // clipped and alpha 0 always is.
    return (index + 0.5) * 0.0625;
}

// -----------------------------------------------------------------------------
// The corridor test.
//
// PositionWS  — the fragment's world position (Shader Graph Position node, World).
//               It is the POST-vertex-animation position, so a prism still blooming
//               on the grow clock is tested where it actually rasterizes.
// Target      — _PrismOcclusionTarget (vessel world position).
// Params      — _PrismOcclusionParams = (outerRadius, innerRadius, minAlpha).
// BaseAlpha   — whatever fed SurfaceDescription.Alpha before this node (_Alpha).
//               Multiplying rather than replacing keeps the graph's transparent
//               materials (cloak / transparent shielded / transparent danger) honest:
//               their authored alpha still applies, the corridor only scales it.
//
// Alpha         — BaseAlpha scaled by the corridor fade.
// ClipThreshold — 0 outside the corridor (never discards); the Bayer threshold inside
//                 it, so an opaque alpha-tested material dissolves smoothly instead of
//                 popping. Transparent materials ignore this output entirely (they do
//                 not enable _ALPHATEST_ON) and simply blend the reduced alpha.
// -----------------------------------------------------------------------------
void PrismOcclusionFade_float(float3 PositionWS, float3 Target, float3 Params, float BaseAlpha,
    out float Alpha, out float ClipThreshold)
{
    Alpha = BaseAlpha;
    ClipThreshold = 0.0;

    float outerRadius = Params.x;
    if (outerRadius <= 0.0)
        return; // corridor off: no local vessel, or disabled in config

    // _WorldSpaceCameraPos (UnityInput.hlsl, included by every URP pass) rather than a
    // published uniform: the near end of the corridor is then ALWAYS exactly the camera
    // that is rendering — game view, scene view, any split — with nothing to resolve on
    // the CPU and nothing to keep in sync.
#if defined(SHADERGRAPH_PREVIEW)
    float3 cameraWS = float3(0.0, 0.0, 0.0);
#else
    float3 cameraWS = _WorldSpaceCameraPos.xyz;
#endif

    // Distance from the fragment to the camera->vessel SEGMENT (not the infinite line):
    // clamping t to [0,1] is what keeps prisms BEHIND the ship, and prisms behind the
    // camera, fully opaque.
    float3 axis = Target - cameraWS;
    float3 rel = PositionWS - cameraWS;
    float axisLenSq = dot(axis, axis);
    float t = (axisLenSq > 1e-6) ? saturate(dot(rel, axis) / axisLenSq) : 0.0;
    float distanceToAxis = distance(rel, axis * t);

    if (distanceToAxis >= outerRadius)
        return; // outside the corridor: costs nothing beyond the test above

    // innerRadius..outerRadius is the feather. Inside innerRadius the fade is at its
    // floor (minAlpha); at outerRadius it is 1. Smoothstep matches the easing every
    // other prism transition uses (PrismColorLerp).
    float innerRadius = min(Params.y, outerRadius);
    float k = saturate((distanceToAxis - innerRadius) / max(outerRadius - innerRadius, 1e-4));
    k = k * k * (3.0 - 2.0 * k);
    float fade = lerp(Params.z, 1.0, k);

    Alpha = BaseAlpha * fade;

#if !defined(SHADERGRAPH_PREVIEW)
    // Screen pixel coordinates, reconstructed from the same world position the
    // rasterizer used. Avoids a Screen Position node (and its varying) entirely.
    float4 positionCS = TransformWorldToHClip(PositionWS);
    float2 ndc = positionCS.xy / max(abs(positionCS.w), 1e-6);
    float2 pixel = (ndc * 0.5 + 0.5) * _ScreenParams.xy;
    ClipThreshold = PrismOcclusionBayer4x4((uint2)abs(pixel));
#endif
}

#endif // PRISM_OCCLUSION_CORRIDOR_INCLUDED
