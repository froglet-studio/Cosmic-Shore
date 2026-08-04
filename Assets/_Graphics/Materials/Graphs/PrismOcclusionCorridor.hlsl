// PrismOcclusionCorridor.hlsl — the GPU side of the camera↔vessel occlusion corridor
// (Docs/PRISM_ANIMATION.md §3 C1 / §5 C1, the "moving-target exception" class of §1).
//
// PURPOSE. Prisms that sit between the player's camera and the player's vessel must
// not hide the ship. The corridor is a BARE CONE from the camera to the vessel — a
// point at the lens, widening to the circle that circumscribes the hull, ending at the
// vessel's plane with no cap at either end. That is the minimal volume able to occlude
// the ship: nothing outside the eye->silhouette cone can be in front of it, and nothing
// at or past the vessel's own depth can either. A fragment inside fades out; a fragment
// outside is untouched, and the ENTIRE boundary — sides and base alike — is one
// gradient shell of uniform thickness, so the shape has no seam anywhere on it.
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
//   float3 _PrismOcclusionParams  — (outerRadius, innerRadius, coreAlpha).
//                                   outerRadius <= 0 means "corridor off" — the very
//                                   first branch below returns the untouched alpha.
//
// THE PROFILE. Both radii TAPER with distance along the axis, so they describe two
// nested cones. Inside the inner cone the alpha is EXACTLY coreAlpha (0 by default:
// fully tapered to nothing, so no dithered ghost survives anywhere the ship can be); at
// and beyond the outer cone it is EXACTLY 1. Because the radius grows in proportion to
// depth, the cleared region has a CONSTANT ANGULAR size — the ship's own silhouette —
// rather than a constant world size. The inner cone is deliberately much narrower than
// the outer one (a quarter of it by default), so most of the corridor's cross-section
// is gradient rather than hard clearance and the dissolve reads as a soft column with a
// small solid-clear centre. The BASE is graded on the same shell thickness (see
// clearAxial below), so the corridor closes toward the vessel as softly as it feathers
// outward.
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
// it per-prism would mean a per-prism material swap. Screen-door alpha-to-clip keeps
// every prism in the opaque queue, needs no sorting, and is order-independent by
// construction. The trade is stated in the doc: it makes the prism materials
// alpha-tested.

#ifndef PRISM_OCCLUSION_CORRIDOR_INCLUDED
#define PRISM_OCCLUSION_CORRIDOR_INCLUDED

// -----------------------------------------------------------------------------
// The screen door: a MOTLEY, not a matrix.
//
// Interleaved gradient noise — a low-discrepancy screen-space hash with no repeating
// tile. The ordered 4×4 Bayer matrix this replaced (2026-08-04) read as exactly what
// its name says: a regular grid, a literal screen door, legible as structure rather
// than as transparency.
//
// IGN was picked over plain white noise (an integer hash), which is motlier still but
// CLUMPS: measured over the shipped ramp, |coverage − alpha| averages 0.0001 for IGN
// against 0.0017 for a hash and 0.0100 for Bayer. That fidelity is what lets the SHORT
// gradient below still read as a smooth fade instead of a ragged edge — the stipple
// density tracks the alpha almost exactly at every point in the band. Irregular AND
// even is the combination that works; irregular and blotchy is not.
//
// Screen-space and static, so the pattern does not crawl: prisms slide through it as
// the camera moves, which is what makes it read as a dissolve.
// -----------------------------------------------------------------------------
float PrismOcclusionMotley(float2 pixel)
{
    float n = frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
    // Nudged STRICTLY inside (0,1). frac() can return exactly 0, and a 0 threshold
    // against a 0 alpha is `clip(0)` — which KEEPS the fragment on the URP variants
    // that clip directly rather than through AlphaDiscard's epsilon. That would leave a
    // sparse confetti of survivors in a core that is supposed to be fully gone.
    return n * 0.998 + 0.001;
}

// Quintic smootherstep — C2 continuous: value, FIRST and SECOND derivatives are all
// zero at both ends. smoothstep (cubic) only zeroes the first, which leaves a faint
// crease where the band begins and ends. That crease is what you notice when the band
// is short, so the shorter the gradient the more the extra continuity earns its two MADs.
float PrismOcclusionSmootherStep(float t)
{
    t = saturate(t);
    return t * t * t * (t * (t * 6.0 - 15.0) + 10.0);
}

// -----------------------------------------------------------------------------
// The corridor test.
//
// PositionWS  — the fragment's world position (Shader Graph Position node, World).
//               It is the POST-vertex-animation position, so a prism still blooming
//               on the grow clock is tested where it actually rasterizes.
// Target      — _PrismOcclusionTarget (vessel world position).
// Params      — _PrismOcclusionParams = (outerRadius, innerRadius, coreAlpha).
// BaseAlpha   — whatever fed SurfaceDescription.Alpha before this node (_Alpha).
//               Multiplying rather than replacing keeps the graph's transparent
//               materials (cloak / transparent shielded / transparent danger) honest:
//               their authored alpha still applies, the corridor only scales it.
//
// Alpha         — BaseAlpha scaled by the corridor fade.
// ClipThreshold — 0 outside the corridor (never discards); the motley threshold inside
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

    float3 axis = Target - cameraWS;
    float3 rel = PositionWS - cameraWS;
    float axisLenSq = dot(axis, axis);
    if (axisLenSq <= 1e-6)
        return; // camera sitting on the vessel: no axis, no cone

    // t is UNCLAMPED, and the cone is bounded by rejecting t outside (0,1). This makes it
    // a BARE cone: it ends flat at the vessel's plane, with no spherical cap past the base
    // and none behind the camera. Mass level with or behind the ship cannot be in front of
    // it, so clearing any of it would be more than the corridor needs. (Saturating t
    // instead would pin the closest point to the vessel past t = 1, and the metric there
    // becomes distance-to-the-ship-point — that is exactly the hemispherical cap this
    // rejection removes.)
    float t = dot(rel, axis) / axisLenSq;
    if (t <= 0.0 || t >= 1.0)
        return; // behind the camera, or at/past the vessel — outside the cone entirely

    // Within (0,1) the closest point on the segment IS the perpendicular foot, so this is
    // the perpendicular distance to the axis.
    float distanceToAxis = distance(rel, axis * t);

    // THE RADIUS TAPERS WITH t — this one multiply is what makes the corridor a CONE
    // rather than a capsule, and it is the whole shape argument. The volume that can
    // actually hide the ship is the eye->silhouette cone: it is a point at the lens and
    // only reaches the hull's radius at the hull. A constant radius (the capsule the
    // retired ClearPrisms CapsuleCollider imposed, carried over into the first shader
    // version) massively over-clears near the camera, where a fixed world radius
    // subtends a huge solid angle. Tapering makes the cleared region a CONSTANT ANGULAR
    // SIZE — exactly the ship's own silhouette, at every depth — so the corridor never
    // dissolves a single prism more than it must.
    float outerAtT = outerRadius * t;

    if (distanceToAxis >= outerAtT)
        return; // outside the cone: costs nothing beyond the tests above

    // The profile: EXACTLY coreAlpha (0 by default — fully tapered to nothing, no
    // residual ghost anywhere the ship can be) inside the inner cone, EXACTLY 1 at and
    // beyond the outer cone, C2-smooth in between.
    //
    // The band is deliberately SHORT so the world snaps back to opaque as soon as you
    // move off — only a thin shell is ever in transition. Short and smooth are in
    // tension, which is why the easing is quintic and the dither is low-discrepancy:
    // both exist to keep a narrow band from reading as an edge.
    float innerRadius = min(Params.y, outerRadius);
    float innerAtT = innerRadius * t;

    // Radial clearance: 1 inside the inner cone, 0 at the outer cone's surface.
    float clearRadial = 1.0 - PrismOcclusionSmootherStep(
        (distanceToAxis - innerAtT) / max(outerAtT - innerAtT, 1e-4));

    // Axial clearance: 1 up to the base band, 0 at the vessel's plane. This grades the
    // BASE. Without it the cone ended in a hard cut — a prism spanning the vessel's plane
    // was faded on the camera side and solid on the far side, which reads as a crisp
    // semicircular edge on any large plate at that depth.
    //
    // The band's thickness is DERIVED, not authored: it is the radial shell's own world
    // thickness (outerRadius - innerRadius) expressed in units of t. That makes the
    // gradient shell ISOTROPIC — the same thickness across the base as around the sides —
    // so the corridor's whole boundary fades at one rate and there is no seam anywhere on
    // it. It also self-scales: a long corridor gets a proportionally short axial band, a
    // short one a longer band, with nothing to tune. Clamped to 1 for the degenerate case
    // where the camera is closer to the ship than the shell is thick.
    float baseBand = clamp((outerRadius - innerRadius) / sqrt(axisLenSq), 1e-4, 1.0);
    float clearAxial = 1.0 - PrismOcclusionSmootherStep((t - (1.0 - baseBand)) / baseBand);

    // PRODUCT, not min(): a fragment is cleared only where it is inside the cone AND
    // before the base, and multiplying two C2 curves stays C2 — min() would crease
    // wherever the two cross, which is exactly the artefact this pass exists to remove.
    float fade = lerp(1.0, Params.z, clearRadial * clearAxial);

    Alpha = BaseAlpha * fade;

#if !defined(SHADERGRAPH_PREVIEW)
    // Screen pixel coordinates, reconstructed from the same world position the
    // rasterizer used. Avoids a Screen Position node (and its varying) entirely.
    float4 positionCS = TransformWorldToHClip(PositionWS);
    float2 ndc = positionCS.xy / max(abs(positionCS.w), 1e-6);
    float2 pixel = (ndc * 0.5 + 0.5) * _ScreenParams.xy;
    ClipThreshold = PrismOcclusionMotley(pixel);
#endif
}

#endif // PRISM_OCCLUSION_CORRIDOR_INCLUDED
