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
// COST CONTRACT. A fully opaque fragment outside the corridor executes: one compare
// (radius > 0), one segment-distance evaluation (~10 ALU), two compares, then returns
// the alpha it was given and a clip threshold of 0 — no dither, no texture, no extra
// varying beyond world position, and `clip(alpha - 0)` with alpha >= 1 never discards.
// The kernel is paid only by fragments whose FINAL alpha is fractional — inside the
// corridor's gradient shell, or carrying a sub-1 alpha of their own (a fading debris
// prism, a cloaked prism). Both branches are uniform across a prism and near-uniform
// across a screen tile, so they are coherent. Nothing here changes the render queue,
// the batch, or the draw call count: every prism stays in the same instanced batch.
//
// WHY DITHER AND NOT BLENDING — and why, since 2026-08-10, this is THE prism
// transparency mechanism, not just the corridor's. The environment must stay CHEAP
// OPAQUE prisms — the transparent queue (sorting + blend + no depth write) is exactly
// the cost this feature exists to avoid. Screen-door alpha-to-clip keeps every prism
// in the opaque queue, needs no sorting, and is order-independent by construction.
// Originally the exploding debris and the cloak family still blended in the transparent
// queue for their fades; now the threshold engages for ANY fractional final alpha, so
// those fades ride the same screen door (and the same back-face separation), every prism
// material is opaque + _ALPHATEST_ON, and NO prism renders in the transparent queue at
// all. The effects compose in coverage — a debris prism fading inside the corridor is
// one pattern at the product alpha, not two stacked transparencies. The trade is
// stated in the doc: it makes the prism materials alpha-tested.

#ifndef PRISM_OCCLUSION_CORRIDOR_INCLUDED
#define PRISM_OCCLUSION_CORRIDOR_INCLUDED

// -----------------------------------------------------------------------------
// THE KERNEL SWITCH.
//
// The dither kernel is the one part of this file that is a LOOK decision rather than a
// correctness one, so the surviving candidates are carried side by side while the look
// is being chosen. All are procedural — no texture, no sampler, no asset — and all cost
// less than the corridor test itself. Point PRISM_OCCLUSION_KERNEL at one to A/B them;
// nothing else in the file, the graph, or any material changes.
//
// ADMISSION RULE. A kernel earns a slot here by holding |coverage − alpha| under ~0.01
// (see FIDELITY on each). That number is what lets the SHORT gradient band below read as
// a fade instead of an edge, and it is the reason the other nine candidates rendered on
// 2026-08-04 (concentric rings 0.21, quasicrystal 0.13, halftone 0.10, hex 0.10, perlin
// 0.04, …) are not here: they buy their look by trading it away.
//
// A kernel must also EARN ITS SHAPE — passing the number is necessary, not sufficient.
// The 2026-08-06 hard-edge pass measured two more polygonal candidates. Voronoi SHATTER
// passed both bars and is carried as kernel 4. The triangular TESSELLATION (simplex grid,
// per-facet phase, facets filling as nested triangles) passed the number at 0.0009 / 0.0056
// and is NOT here: it dissolves into thin strokes at mid alpha and reads as scratchy
// crosshatch rather than as facets, and with the per-facet stagger removed it measures
// 0.16 and is the literal wallpaper the Bayer grid was dropped for.
// -----------------------------------------------------------------------------
#define PRISM_OCCLUSION_KERNEL_IGN 0       // screen-space noise — reads as a DISSOLVE
#define PRISM_OCCLUSION_KERNEL_SPIRAL 1    // corridor-relative — reads as an IRIS
#define PRISM_OCCLUSION_KERNEL_WORLEY 2    // screen-space cells — reads as ROUND flecking
#define PRISM_OCCLUSION_KERNEL_SHARD 3     // screen-space cells — reads as TRIANGULAR flecking
#define PRISM_OCCLUSION_KERNEL_SHATTER 4   // screen-space cells — reads as a CRACKED LATTICE
#define PRISM_OCCLUSION_KERNEL_SHATTER3D 5 // WORLD-space cells — a VOLUMETRIC cracked lattice
#define PRISM_OCCLUSION_KERNEL_SHARD3D 6   // WORLD-space cells — distance-to-owner fill (Prompt 16)

#define PRISM_OCCLUSION_KERNEL PRISM_OCCLUSION_KERNEL_SHATTER

// -----------------------------------------------------------------------------
// LIVE TUNING — the design-mode gate (FrogletTools > Ecology > Prism Animation >
// Occlusion Dither Lab).
//
// Every dial below is a compile-time constant, which is the right shape for shipping and
// a terrible one for CHOOSING a look: you cannot slide a #define while flying. So the
// whole dial set can be promoted to two global uniforms — the same O(1)-per-frame shape
// the corridor's own params already use, published by the Lab window — and the kernel
// choice becomes a runtime branch.
//
//   1 = DESIGN MODE. Dials are live; the Lab drives them while the game runs.
//   0 = SHIPPED. This file compiles EXACTLY as it would have without any of this: the
//       macros below expand to the constants themselves, the #if picks one kernel, and
//       the other six unused kernels plus the branch and the uniforms are not in the shader at all.
//
// It is not free, which is why it is a gate rather than a permanent feature: design mode
// compiles every carried kernel into every prism shader and allocates registers for the
// largest, which costs occupancy on tile-based GPUs — on the one draw class this game has
// most of. The Lab's **Bake to Source** button writes the chosen values into the constants
// and flips this to 0, so the cost lasts exactly as long as the design session.
//
// FAIL-SAFE. `_PrismOcclusionDitherA.x` is the master: it holds kernel+1, so an
// unpublished global (all zeros — a player build, or the editor before the Lab is opened)
// reads as 0 and EVERY dial falls back to its compile-time constant. Design mode with
// nobody driving it looks exactly like shipped mode.
// -----------------------------------------------------------------------------
#define PRISM_OCCLUSION_LIVE_TUNING 0

#if PRISM_OCCLUSION_LIVE_TUNING
float4 _PrismOcclusionDitherA;  // (kernel + 1, cellSize, shardOrient, morphRate)
float4 _PrismOcclusionDitherB;  // (shatterCell, shatterWall, spiralRings, spiralArms)
float4 _PrismOcclusionDitherC;  // (shatterDepthPhase, backFacePower, shatter3dCell, shatter3dWall)

#define PRISM_OCCLUSION_TUNING_ON (_PrismOcclusionDitherA.x > 0.5)
#define PRISM_OCCLUSION_DIAL(live, fallback) (PRISM_OCCLUSION_TUNING_ON ? (live) : (fallback))
#else
#define PRISM_OCCLUSION_DIAL(live, fallback) (fallback)
#endif

// -----------------------------------------------------------------------------
// THE SHAPE RULE — why the current kernel is SHARD and not WORLEY (2026-08-06).
//
// The unit shape of the dither is a design surface, not just a dither detail: it is
// the smallest piece of the game the player sees, repeated thousands of times right
// next to their ship. Cosmic Shore's motif is SOFT-HARD-SOFT — bloom (soft) around
// low-poly prisms (hard) drawn along a smooth flight curve (soft); the UI borders do
// the same thing, grading out at both ends while taking hard turns in their pathing.
// Rigid geometry sandwiched between the ambiguous.
//
// A CIRCLE breaks that. It is a soft shape with a soft gradient on either side of it —
// soft-SOFT-soft — so Worley's round flecks read as foam against everything else in the
// frame. Two kernels answer it, and both are carried: SHARD keeps Worley's arrangement
// exactly and changes only the METRIC, so the flecks become equilateral triangles of the
// same area; SHATTER abandons the fleck and makes the NEGATIVE space the motif, filling
// each Voronoi polygon between straight lines so the lattice reads as cracked walls.
//
// SHATTER IS WHAT SHIPPED (2026-08-06), chosen in motion in the Occlusion Dither Lab
// rather than from stills, at polygon 16.26 px / wall 20 px. Both candidates are hard-
// edged and both sit inside the admission rule; the call between them was a look call and
// could only be made against real trail mass at speed. SHARD stays one #define away.
//
// Kernel 2 is kept, not deleted — it is the calibration reference every fidelity number
// in this file is quoted against, and it is one #define away if the round flecks ever want
// re-judging side by side.
// -----------------------------------------------------------------------------

// -----------------------------------------------------------------------------
// THE MORPH RATE — how fast the pattern evolves, in full pattern cycles per second.
// A cycle is "the pattern has returned to itself", so 0.12 is one cycle per ~8 seconds:
// legibly alive without ever drawing the eye off the ship. Set to 0 for a frozen pattern;
// nothing else needs to change.
//
// This is an AXIS, not another kernel — each kernel interprets it in its own natural terms
// (the cellular kernels orbit their feature points, the spiral drifts its phase), and each
// states the interpretation at its own definition. Time is `_Time.y`, a URP built-in, so
// morphing costs one MAD per fragment and ZERO CPU: no per-prism state, no publisher
// change, no extra uniform. That is the same shape the clock-material law asks for
// everywhere else — initial conditions plus a clock, evaluated on the GPU.
//
// WHY IT IS SAFE. The pattern is only visible where alpha is strictly between 0 and 1 —
// the narrow gradient shell — because the core clips regardless of threshold and the
// exterior clips nothing. So an evolving threshold can only flip pixels inside that band.
// At this rate 0.69% of band pixels change state per 60fps frame, which reads as the
// pattern FLOWING rather than flickering; past roughly 0.25 cycles/sec (1.45%) it starts
// to read as noise instead, so treat that as the ceiling. Coverage fidelity is INDEPENDENT
// of the rate — measured 0.0065-0.0070 across 0.04 through 0.25 — so the rate is purely a
// motion dial and moving it cannot break the fade.
//
// IGN IGNORES THIS. It is a hash, not a field: it has no continuity in any input, so
// advancing it does not morph the pattern, it resamples it — every pixel independently,
// every frame. That is full-amplitude shimmer, not motion. Only the two kernels that are
// continuous functions of position can be continuous functions of time as well.
// -----------------------------------------------------------------------------
static const float PRISM_OCCLUSION_MORPH_RATE = 0.3256;   // cycles/sec; 0 = frozen

// Every dial below is read through one of these accessors rather than named directly, so
// that design mode and shipped mode differ in exactly one place each. Under
// PRISM_OCCLUSION_LIVE_TUNING 0 each one collapses to its constant and the compiler folds
// it away — the generated code is identical to naming the constant inline.
float PrismOcclusionMorphRate()
{
    return PRISM_OCCLUSION_DIAL(_PrismOcclusionDitherA.w, PRISM_OCCLUSION_MORPH_RATE);
}

// -----------------------------------------------------------------------------
// THE LAYERED BEAT — the problem, and the two dials that answer it (2026-08-11).
//
// Every screen-anchored kernel is a pure function of the screen pixel, so two surfaces
// stacked along one camera ray — a prism's own back face showing through its clipped
// front face, or two parallel walls of trail mass — read the IDENTICAL threshold at
// every pixel. Their alphas differ only slightly (two nearby depths on the same
// profile), so the front layer's hole boundary {threshold = alphaFront} and the back
// layer's survival boundary {threshold = alphaBack} are near-identical contours offset
// by Δalpha/|∇threshold| — a pixel or two. Two copies of the same line set offset by a
// hair is the textbook moiré condition, and because the alpha field rides the GEOMETRY
// while the threshold rides the SCREEN, camera motion slides the pair at slightly
// different rates: the interference beats, and it beats worst on SHATTER, whose level
// sets are parallel straight walls with the shallowest gradient in the file.
//
// REJECTED (2026-08-10, reverted 2026-08-11): SHEARING THE WHOLE DITHER DOMAIN by view
// depth (`pixel += depth * gain * dir`). It decorrelated the layers as designed, but it
// translates the ENTIRE lattice, so the pattern's screen velocity is gain × (depth
// change per frame) — at flight speed that is tens of pixels per frame of coherent
// crawl, and coherent motion is the most salient thing the eye can be shown. It read as
// a LARGER flicker than the beat it fixed. The lesson generalises: a fix that moves the
// pattern globally cannot win against speed, because the eye tracks global motion.
//
// What is carried instead are two LOCAL answers, both independently switchable:
//
//   1. DEPTH BAND PHASE (SHATTER only; `PRISM_OCCLUSION_SHATTER_DEPTH_PHASE`). Add the
//      depth term inside the kernel's final frac() instead of to its domain. The
//      Voronoi lattice stays exactly where it is — cells do not move at all, so there
//      is no global crawl to track — and only each cell's WALL slides within its own
//      cell. Layers land at uncorrelated wall phases, so the parallel-line coincidence
//      is broken while the crack lattice stays put and reads as one shattered medium.
//      Coverage-neutral for the frac-of-uniform reason: the phase is added inside a
//      frac() of an already-uniform quantity and is independent of the cell hash.
//
//   2. BACK-FACE ALPHA SEPARATION (`PrismBackFaceFade`, at the bottom of this file).
//      Attack the OTHER precondition instead of the pattern: a beat needs both layers
//      simultaneously in the gradient band. Sharpening the far surface's alpha pushes
//      the interior out of the band while the exterior is still mid-fade, so only one
//      dithered layer is live at a time and there is nothing to interfere with.
//
// The two compose and are orthogonal — (1) decorrelates what remains overlapping,
// (2) reduces how much overlaps — so tune them independently in the Lab.
//
// The SPIRAL ignores both — it is corridor-anchored, not screen-anchored. Note it has
// the SAME layered-beat failure for its own reason (its polar coordinates are constant
// along a camera ray, so stacked layers read identical thresholds there too); if it is
// ever revived, give it a depth term of its own rather than exempting it.
// -----------------------------------------------------------------------------

// The clip threshold must land STRICTLY inside (0,1). frac() can return exactly 0, and a
// 0 threshold against a 0 alpha is `clip(0)` — which KEEPS the fragment on the URP
// variants that clip directly rather than through AlphaDiscard's epsilon. That would
// leave a sparse confetti of survivors in a core that is supposed to be fully gone.
float PrismOcclusionSafeThreshold(float n)
{
    return n * 0.998 + 0.001;
}

// -----------------------------------------------------------------------------
// Kernel A — the corridor-relative SPIRAL.
//
// An Archimedean spiral in the corridor's OWN polar frame: `rings` bands across the
// cone's radius, sheared by `arms` full turns per revolution. Both coordinates are
// already paid for — u is the radial ratio the profile below computes anyway, and the
// angle comes from the perpendicular vector the distance came from — so this is the
// cheapest kernel of the set: a dot, a dot, an atan2 and a frac, with no hash at all.
//
// WHY IT LOOKS DIFFERENT FROM A SCREEN-SPACE DITHER. The pattern is anchored to the
// corridor rather than to the screen, so it does not slide when the camera moves: the
// world travels through a standing spiral centred on the ship. That is an iris/portal
// read rather than a dissolve read — a deliberate choice, not a side effect.
//
// FIDELITY. Measured in situ (kept-fraction vs alpha, per alpha bin, over a rendered
// prism wall) the spiral averages |coverage − alpha| = 0.0042, against 0.0021 for IGN
// and 0.10–0.21 for the other structured kernels (rings, halftone, hex, quasicrystal).
// It is the only structured pattern that keeps a smooth fade in a SHORT gradient band;
// the rest trade that away for their look, which is why they are not carried here.
//
// ARMS MUST STAY AN INTEGER. atan2 has a seam at ±pi where the angle jumps by exactly
// one turn. An integer arm count makes the spiral's phase jump by an integer there too,
// which frac() erases — a fractional count would leave a visible radial scar down one
// side of the corridor.
// -----------------------------------------------------------------------------
// MORPH: the rate is added straight to the band phase, so one cycle drifts the pattern by
// exactly one band. Because an Archimedean spiral is sheared, a radial phase drift IS a
// rotation — the iris turns slowly rather than pulsing. Coverage is untouched: the phase
// is inside a frac() of an already-uniform quantity, so shifting it cannot change the
// threshold's distribution at all. This is the one kernel whose morph is provably free.
static const float PRISM_OCCLUSION_SPIRAL_RINGS = 9.0;  // bands across the cone radius
static const float PRISM_OCCLUSION_SPIRAL_ARMS = 3.0;   // turns per revolution — INTEGER

float PrismOcclusionSpiral(float radialRatio, float angleTurns, float time)
{
    return PrismOcclusionSafeThreshold(frac(
        radialRatio * PRISM_OCCLUSION_DIAL(_PrismOcclusionDitherB.z, PRISM_OCCLUSION_SPIRAL_RINGS)
        + angleTurns * PRISM_OCCLUSION_DIAL(_PrismOcclusionDitherB.w, PRISM_OCCLUSION_SPIRAL_ARMS)
        + time * PrismOcclusionMorphRate()));
}

// -----------------------------------------------------------------------------
// Kernel B — the screen-space MOTLEY (interleaved gradient noise).
//
// A low-discrepancy screen-space hash with no repeating tile. The ordered 4×4 Bayer
// matrix this replaced (2026-08-04) read as exactly what its name says: a regular grid,
// a literal screen door, legible as structure rather than as transparency.
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
    return PrismOcclusionSafeThreshold(
        frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715)))));
}

// -----------------------------------------------------------------------------
// Kernel C — screen-space WORLEY (cellular / Voronoi).
//
// Distance to the nearest jittered lattice point, searched over the 3×3 neighbourhood
// that can possibly contain it. Reads as organic flecking — irregular blobs with visible
// cell structure — rather than the even stipple of IGN or the standing bands of the
// spiral. Screen-anchored like IGN, so prisms dissolve through it.
//
// THE REMAP IS NOT OPTIONAL. Raw F1 cell distance is badly non-uniform: over the shipped
// lattice it clusters around 0.43 with almost nothing near either extreme, so a straight
// `F1 / maxDistance` threshold gives |coverage − alpha| = 0.1401 — worse than halftone,
// and far outside the admission rule above. The distance is therefore pushed through a
// smoothstep fitted to its own measured CDF, which lands it at 0.0048 on a uniform alpha
// sweep and 0.0074 measured across the real corridor cross-section (against 0.0017 for
// the spiral and 0.0034 for IGN on the same measurement) — a 19× improvement that costs
// one instruction. The remap is MONOTONIC, so the cell boundaries and the whole visual
// character are untouched; only the RATE at which cells fill in as alpha sweeps changes,
// and it changes to the correct one. Same argument as picking IGN over white noise:
// irregular AND even is the combination that works.
//
// COST. The most expensive kernel carried here — 9 cells × one 2D hash each, ~18 hashes,
// against IGN's one frac-chain and the spiral's zero. Still per-fragment ALU only, still
// no texture or sampler, and still confined to fragments INSIDE the corridor cone (the
// whole kernel is past the early-out), so it is paid on a small fraction of the screen.
//
// The hash is float-only (Hoskins hash22) rather than integer-op, so it behaves
// identically on GLES/mobile targets where integer throughput is poor.
// -----------------------------------------------------------------------------
// MORPH: each feature point ORBITS inside its own cell — `0.5 + 0.5*sin(2pi*hash + t)`
// per axis — so the cells breathe, drift, merge and split continuously with no pop. The
// orbit is bounded to the unit cell, which is what keeps the 3×3 search exhaustive; a
// `frac(hash + t)` drift would be cheaper and WRONG, because the point teleports from one
// cell edge to the other every cycle.
//
// The jitter is this sin-orbit at EVERY rate, including 0. That is deliberate: the orbit's
// marginal distribution is arcsine rather than uniform, which shifts the F1 CDF, so a
// static raw-hash jitter and a moving sin jitter would need two different remaps. Using
// one jitter function means ONE fit covers both — verified phase-stable at 0.0068 from
// rate 0 through t = 400s. (Feeding the shipped-static constants 0.02/0.83 to the moving
// points measured 0.0238, i.e. straight back out of the admission rule, which is exactly
// the failure mode the warning below is about.)
static const float PRISM_OCCLUSION_CELL_SIZE = 6.0;       // pixels per lattice cell
static const float PRISM_OCCLUSION_CELL_CDF_LO = 0.011;   // fitted to the measured F1 CDF
static const float PRISM_OCCLUSION_CELL_CDF_HI = 0.873;   // — see THE SIZE WINDOW below

// The fit is named CELL, not WORLEY, because BOTH cellular kernels use it: SHARD's
// triangle gauge is area-normalised against the circle (see kernel D), which lands its
// distance distribution on the same CDF. Re-measured under the triangle metric the
// independent best fit is 0.0118 / 0.8775 — within noise of these two, and the shipped
// pair measures 0.0074 uniform on it. One fit, two metrics; that is the payoff for
// normalising by area rather than by extent.
//
// -----------------------------------------------------------------------------
// THE SIZE WINDOW — CELL_SIZE is a free dial inside a measured band (2026-08-06).
//
// CORRECTION to what this file and the checklist used to say. The fit is NOT bound to
// the pitch: the distance is measured in CELL units, so the distribution does not move
// when the lattice does. Refitting at every pitch from 3 to 15 px lands within noise of
// the two constants above (lo 0.009–0.020, hi 0.859–0.878) and buys nothing measurable —
// at 15 px a bespoke refit takes the sweep from 0.0062 to 0.0059 and leaves the corridor
// error at 0.026 untouched. The "~19× degradation" this note used to threaten is what you
// get from dropping the remap ENTIRELY (raw F1 = 0.140), not from moving the pitch.
//
// What actually bounds the pitch is SAMPLING at both ends, and neither end is fittable:
//
//   3.0 px   the shape falls under the pixel floor — quantisation, 0.013 either way
//   4.5 px   fine grain; triangles present but not readable at 1:1
//   6.0 px   SHIPPED. 0.0074 / 0.0145 (SHARD/FIXED); ~9 px tall triangles
//   8.0 px   0.0060 / 0.0146 — the same fidelity, and the most legible AS a triangle
//  11.0 px   0.0059 / 0.0193 — bold; too few cells now span the gradient band
//  15.0 px   0.0060 / 0.0248 — BREAKS THE FADE. The band reads as chunky edge, not fade.
//
// So: 4.5–11 px is the usable window and 6–8 px is the sweet spot. Move it inside that
// band freely; past 11 px the corridor error is a SPATIAL sampling failure and there is
// no constant anywhere in this file that will buy it back.
// -----------------------------------------------------------------------------
float PrismOcclusionCellSize()
{
    return PRISM_OCCLUSION_DIAL(_PrismOcclusionDitherA.y, PRISM_OCCLUSION_CELL_SIZE);
}

float2 PrismOcclusionHash2(float2 cell)
{
    float3 p3 = frac(cell.xyx * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.xx + p3.yz) * p3.zy);
}

// 3D-input variants of the same float-only Hoskins family, for the volumetric kernels
// and the object-space erosion. Same rationale as Hash2: no integer ops, so identical
// behaviour on GLES/mobile targets.
float3 PrismOcclusionHash3(float3 p3)
{
    p3 = frac(p3 * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yxz + 33.33);
    return frac((p3.xxy + p3.yxx) * p3.zyx);
}

float PrismOcclusionHash1(float3 p3)
{
    p3 = frac(p3 * 0.1031);
    p3 += dot(p3, p3.zyx + 31.32);
    return frac((p3.x + p3.y) * p3.z);
}

float PrismOcclusionWorley(float2 pixel, float time)
{
    float2 p = pixel / PrismOcclusionCellSize();
    float2 base = floor(p);
    float phase = time * PrismOcclusionMorphRate() * 6.28318530718;

    // Squared distance while searching — the sqrt is paid once, at the end.
    float best = 8.0;
    [unroll]
    for (int y = -1; y <= 1; ++y)
    {
        [unroll]
        for (int x = -1; x <= 1; ++x)
        {
            float2 cell = base + float2(x, y);
            float2 orbit = 0.5 + 0.5 * sin(6.28318530718 * PrismOcclusionHash2(cell) + phase);
            float2 offset = (cell + orbit) - p;
            best = min(best, dot(offset, offset));
        }
    }

    return PrismOcclusionSafeThreshold(smoothstep(
        PRISM_OCCLUSION_CELL_CDF_LO, PRISM_OCCLUSION_CELL_CDF_HI, sqrt(best)));
}

// -----------------------------------------------------------------------------
// Kernel D — screen-space SHARD (triangular cells).
//
// Worley with one line changed. Same lattice, same Hoskins hash, same orbiting feature
// points, same 3×3 search, same CDF remap — only the METRIC differs, from Euclidean
// distance (whose level sets are circles) to the gauge of an equilateral triangle (whose
// level sets are equilateral triangles). The arrangement the eye reads as "organic
// flecking" is therefore untouched; only the unit shape's EDGES change, from curved to
// three straight ones. That is the whole point: see THE SHAPE RULE at the top.
//
// THE GAUGE. A convex polygon's gauge is the max of its edges' half-plane functions. For
// an equilateral triangle with normals 120° apart, two of the three collapse into one
// abs():
//
//     g(q) = max(q.y, 0.86602540*|q.x| - 0.5*q.y)
//
// {g ≤ r} is the equilateral triangle of INRADIUS r (all three edges are exactly r from
// the origin; the vertices are at 2r, since an equilateral triangle's circumradius is
// twice its inradius).
//
// IT IS A GAUGE, NOT A SQUARED DISTANCE — homogeneous of degree 1, so `min` may be taken
// on it directly and there is no final sqrt. SHARD is therefore very slightly CHEAPER
// than Worley: same nine hashes and nine sines, one abs/mul/max per cell instead of a
// mul/add, and one sqrt saved at the end.
//
// THE AREA NORMALISATION IS LOAD-BEARING — it is not a size preference. A circle of
// radius d has area πd², the triangle at g=r has area 3√3·r², so the two carry the same
// visual weight when r = d·√(π/3√3) — i.e. when the gauge is scaled by 1/0.77756 =
// 1.28607 to read as an "equivalent circle radius". Two things follow, and both are why
// the constant may not be casually retuned: the triangles occupy exactly the ink the
// circles did at the same threshold (so the dissolve's density is unchanged, which is
// what "triangles of the same size" means), AND the distance distribution lands on
// Worley's own measured CDF, so the fitted remap above serves both kernels unchanged.
// Change PRISM_OCCLUSION_SHARD_AREA and you must refit PRISM_OCCLUSION_CELL_CDF_*.
//
// FIDELITY. Measured in the same harness as every number quoted here (which reads the
// shipped Worley at 0.0073 on a uniform alpha sweep and 0.0117 across the corridor
// cross-section — ~1.55× stricter than the original in-situ pass that produced this
// file's 0.0048/0.0074, so compare within the harness, not across): SHARD lands at
// **0.0074 / 0.0145** with FIXED orientation, 0.0066 / 0.0126 with FLIP, 0.0070 / 0.0129
// with SPIN. All three sit inside the admission rule. FIXED pays about 24% more corridor
// error than Worley (0.0145 vs 0.0117) for the shape — stated rather than rounded away —
// and it is bought back by the fit, not by the band: phase-stable at 0.0073–0.0074 across
// t = 0 … 400s, exactly like Worley.
//
// THE 3×3 SEARCH IS STILL EXHAUSTIVE ENOUGH, but for a weaker reason than Worley's, so it
// is measured rather than argued: because the circumradius is twice the inradius, a
// feature point outside the neighbourhood can in principle beat one inside it, which
// Euclidean distance forbids. Against an exhaustive 5×5 search, 0.216% of pixels differ
// at all and the MEAN threshold delta is 1.5e-5 — invisible in a dither, and the same
// class of approximation Worley already ships. A 5×5 would triple the hash count to buy
// that back; it is not worth one part in 10⁵.
// -----------------------------------------------------------------------------
// MORPH: identical to Worley's — the feature points orbit inside their own cells, so the
// triangles drift, merge and split continuously. Under SPIN they also rotate, because the
// orientation is read from the same orbit phase (the sine is already computed for the
// jitter, so spinning costs exactly one cos per cell). Measured 0.64% of band pixels
// changing state per 60fps frame at the default rate, against Worley's 0.50% and the
// ~1.45% ceiling above which a morphing dither reads as noise.
#define PRISM_OCCLUSION_SHARD_FIXED 0  // every triangle points the same way — most legible
#define PRISM_OCCLUSION_SHARD_FLIP 1   // up/down, via a free negation of the offset
#define PRISM_OCCLUSION_SHARD_SPIN 2   // per-cell rotation off the orbit phase — shards

// FIXED is the default because it is the most legible AS A TRIANGLE at 1:1 — the shape
// the pattern is made of should be nameable at a glance, which is the entire ask. FLIP
// and SPIN scatter the orientation and read progressively more as generic angular
// splinters; both are one edit away and both measure marginally BETTER (a uniformly
// oriented gauge is more spatially correlated), so this is a look call, not a numbers one.
#define PRISM_OCCLUSION_SHARD_ORIENT PRISM_OCCLUSION_SHARD_FIXED

static const float PRISM_OCCLUSION_SHARD_AREA = 1.28607;  // equal-area vs the circle

// The gauge of the equilateral triangle, expressed as an equivalent circle radius.
// Always >= 0: if q.y < 0 the second term is >= -0.5*q.y > 0, and if q.y >= 0 the first
// term already is — so the smoothstep below is never fed a negative distance.
float PrismOcclusionTriangleGauge(float2 q)
{
    return max(q.y, 0.86602540 * abs(q.x) - 0.5 * q.y) * PRISM_OCCLUSION_SHARD_AREA;
}

float PrismOcclusionShard(float2 pixel, float time)
{
    float2 p = pixel / PrismOcclusionCellSize();
    float2 base = floor(p);
    float phase = time * PrismOcclusionMorphRate() * 6.28318530718;

    float best = 8.0;
    [unroll]
    for (int y = -1; y <= 1; ++y)
    {
        [unroll]
        for (int x = -1; x <= 1; ++x)
        {
            float2 cell = base + float2(x, y);
            float2 h = PrismOcclusionHash2(cell);
            float2 wave = sin(6.28318530718 * h + phase);
            float2 orbit = 0.5 + 0.5 * wave;

            // Offset FROM the feature point TO the pixel. The direction matters here in a
            // way it never did for Worley: the gauge is not radially symmetric, so
            // reversing it would flip every triangle. Keep it this way round.
            float2 q = p - (cell + orbit);

            // FLIP: a triangle's point reflection is the opposite-pointing triangle, so
            //       half the cells flip for one multiply — no second gauge, no branch.
            // SPIN: wave.y is already sin(2pi*h.y + phase) from the orbit above, so a full
            //       per-cell rotation costs exactly one cos.
            // Under design mode both are compiled in and selected at runtime; under
            // PRISM_OCCLUSION_LIVE_TUNING 0 the #if keeps exactly one, as before.
#if PRISM_OCCLUSION_LIVE_TUNING
            int orient = (int)PRISM_OCCLUSION_DIAL(_PrismOcclusionDitherA.z,
                                                   (float)PRISM_OCCLUSION_SHARD_ORIENT);
            if (orient == PRISM_OCCLUSION_SHARD_FLIP)
            {
                q *= (h.x < 0.5) ? -1.0 : 1.0;
            }
            else if (orient == PRISM_OCCLUSION_SHARD_SPIN)
            {
                float c = cos(6.28318530718 * h.y + phase);
                q = float2(q.x * c - q.y * wave.y, q.x * wave.y + q.y * c);
            }
#elif PRISM_OCCLUSION_SHARD_ORIENT == PRISM_OCCLUSION_SHARD_FLIP
            q *= (h.x < 0.5) ? -1.0 : 1.0;
#elif PRISM_OCCLUSION_SHARD_ORIENT == PRISM_OCCLUSION_SHARD_SPIN
            float c = cos(6.28318530718 * h.y + phase);
            q = float2(q.x * c - q.y * wave.y, q.x * wave.y + q.y * c);
#endif

            best = min(best, PrismOcclusionTriangleGauge(q));
        }
    }

    return PrismOcclusionSafeThreshold(smoothstep(
        PRISM_OCCLUSION_CELL_CDF_LO, PRISM_OCCLUSION_CELL_CDF_HI, best));
}

// -----------------------------------------------------------------------------
// Kernel E — screen-space SHATTER (a cracked lattice of walls). CURRENT.
//
// The other way to make a hard-edged unit shape: instead of growing a polygon around a
// point, take the VORONOI CELL itself — an irregular convex polygon with nothing but
// straight edges — and fill it between two parallel straight lines. Each cell gets a
// hashed phase and a hashed band direction, and `frac(phase + ramp)` sweeps a band across
// it. Neighbouring cells are independent, so the cell boundaries are always visible: the
// pattern reads as a cracked lattice / labyrinth of WALLS rather than as scattered flecks.
//
// It is a different design proposition from SHARD, not a variant of it. SHARD keeps
// Worley's arrangement and hardens the shape; SHATTER abandons the fleck entirely and
// makes the NEGATIVE space the motif. Both are legitimately soft-hard-soft; which one
// belongs next to the ship is a look call that can only be made in motion, which is why
// this is carried rather than described.
//
// TWO DIALS, and they are independent — this is the one kernel here where the wall
// thickness is authorable separately from the cell size:
//   CELL — the polygon size in pixels.
//   WALL — the band repeat in pixels. At alpha a the dark wall is (1−a)·WALL wide, so
//          this is literally "how thick the walls get as the corridor closes".
//
// FIDELITY, and the window (same harness as everywhere in this file; shipped Worley reads
// 0.0073 / 0.0117 on it). Fidelity is exact by construction in the large — `frac` of a
// hash is uniform, so there is no CDF to fit and no remap to keep in sync — and what
// bounds the dials is again SAMPLING:
//
//   polygon  5 px / wall  9 px   0.0258 / 0.0240   BREAKS — polygons under the wall period
//   polygon  8 px / wall  9 px   0.0027 / 0.0065
//   polygon 12 px / wall  9 px   0.0009 / 0.0070
//   polygon 18 px / wall  9 px   0.0007 / 0.0068
//   polygon 11 px / wall  4 px   0.0007 / 0.0029   fine crazing
//   polygon 11 px / wall  7 px   0.0010 / 0.0052
//   polygon 11 px / wall 11 px   0.0013 / 0.0092
//   polygon 11 px / wall 18 px   0.0197 / 0.0223   BREAKS — 1.64x its own polygon
//   polygon 16 px / wall 20 px   0.0051 / 0.0102   SHIPPED SETTING (1.23x)
//
// THE WALL WINDOW IS RELATIVE, NOT ABSOLUTE — corrected 2026-08-06, and the correction
// came from a setting chosen by eye that the first window wrongly called a failure. Every
// row above except the last was swept at a FIXED 11 px polygon, which made a flat "wall
// 4-11 px" look like the rule; it is not. What fails is a wall wide relative to ITS OWN
// polygon, because there is no lattice left to crack: 0.75x -> 0.0063, 1.00x -> 0.0094,
// 1.23x -> 0.0102, 1.30x -> 0.0162, 1.64x -> 0.0173. Read it as **polygon 8-20 px, wall
// up to ~1.25x the polygon**, and measure past that rather than assuming either way.
//
// The shipped 16.26 / 20 holds 0.0102-0.0128 across t = 0…400s — at or inside the Worley
// baseline, and better than SHARD's 0.0145.
//
// COST. The most expensive kernel in the file: Worley's nine hashes and nine sines, plus
// a tenth hash for the owning cell and one sin/cos pair for the band direction. Still
// ALU-only, still no texture or sampler, still paid only on corridor fragments.
// -----------------------------------------------------------------------------
// MORPH: both halves move. The sites orbit exactly as in Worley (so the polygons drift,
// and the walls re-draw themselves as cells trade territory), and the band phase advances
// at the same rate (so each wall slides across its own cell). The phase term sits inside
// the frac() of an already-uniform quantity, so — like the spiral — its contribution to
// coverage is provably nil.
static const float PRISM_OCCLUSION_SHATTER_CELL = 16.26;  // polygon size, px  (8-20)
static const float PRISM_OCCLUSION_SHATTER_WALL = 20.0;   // band repeat, px   (<= 1.25x cell)

// DEPTH BAND PHASE — wall-period cycles per world unit of view depth.
// SHIPPED AT 0 (off). Implemented, measured, and defaulted off because the measurement
// says it cannot do the job; kept as a Lab dial because it is one MAD and provably
// coverage-neutral, so exploring it costs nothing.
//
// It is added HERE, inside the kernel's final frac() alongside the morph phase, rather
// than to the sampling domain — the lattice must not move (see THE LAYERED BEAT). That
// fixes the "global crawl" half of the rejected shear. What it does NOT fix is the
// underlying conflict, which is arithmetic and applies to ANY depth-driven term:
//
//   * DECORRELATION needs the phase to change a LOT across a small depth step. Two faces
//     of one prism are ~2 units apart, so meaningful separation there needs ~0.075+.
//   * SPEED needs the phase to change LITTLE across a frame. At 300 u/s a surface's depth
//     moves 5 units per 60fps frame — 2.5x a whole prism thickness — so the same term
//     that separates the two faces necessarily churns that pixel every frame.
//
// Measured through a clang build of this file (rate | delta at 2u | delta at 12u |
// band pixels flipping per frame at 300 u/s; 0.25 delta = fully decorrelated, and the
// morph note above puts the flicker ceiling near 1.45%):
//
//     0.002 | 0.004 | 0.024 |  2.0%      negligible help, already at the ceiling
//     0.005 | 0.010 | 0.060 |  4.9%
//     0.010 | 0.020 | 0.120 |  9.5%
//     0.020 | 0.040 | 0.240 | 17.9%      12x the ceiling, still only 16% decorrelated at 2u
//     0.050 | 0.100 | 0.400 | 37.2%
//
// The two requirements are ~50x apart: there is no rate that helps the near case without
// reintroducing exactly the flicker that got the domain shear rejected. Hence 0, and
// hence the back-face separation below — which attacks the beat's other precondition and
// has NO temporal cost at all, because it does not depend on depth.
static const float PRISM_OCCLUSION_SHATTER_DEPTH_PHASE = 0.0;

float PrismOcclusionShatterDepthPhase()
{
    return PRISM_OCCLUSION_DIAL(_PrismOcclusionDitherC.x, PRISM_OCCLUSION_SHATTER_DEPTH_PHASE);
}

float PrismOcclusionShatter(float2 pixel, float viewDepth, float time)
{
    float shatterCell = PRISM_OCCLUSION_DIAL(_PrismOcclusionDitherB.x, PRISM_OCCLUSION_SHATTER_CELL);
    float shatterWall = PRISM_OCCLUSION_DIAL(_PrismOcclusionDitherB.y, PRISM_OCCLUSION_SHATTER_WALL);
    float2 p = pixel / shatterCell;
    float2 base = floor(p);
    float phase = time * PrismOcclusionMorphRate() * 6.28318530718;

    // F1 as usual, but the ANSWER is which cell won, not how far away it was.
    float best = 8.0;
    float2 owner = base;
    [unroll]
    for (int y = -1; y <= 1; ++y)
    {
        [unroll]
        for (int x = -1; x <= 1; ++x)
        {
            float2 cell = base + float2(x, y);
            float2 orbit = 0.5 + 0.5 * sin(6.28318530718 * PrismOcclusionHash2(cell) + phase);
            float2 offset = (cell + orbit) - p;
            float d = dot(offset, offset);
            if (d < best)
            {
                best = d;
                owner = cell;
            }
        }
    }

    // The band is measured from the owning cell's INDEX, not from its jittered site, so an
    // orbiting site slides its walls instead of rotating them about a moving centre.
    float2 h = PrismOcclusionHash2(owner);
    float ang = 6.28318530718 * h.y;
    float ramp = dot(p - owner, float2(cos(ang), sin(ang)))
               * (shatterCell / shatterWall);

    // The depth term rides HERE — inside the frac, alongside the morph phase, both of
    // them independent of h.x — so the cells hold still and only their walls shift.
    return PrismOcclusionSafeThreshold(
        frac(h.x + ramp + time * PrismOcclusionMorphRate()
             + viewDepth * PrismOcclusionShatterDepthPhase()));
}

// -----------------------------------------------------------------------------
// Kernel F — WORLD-SPACE SHATTER3D (a volumetric cracked lattice).
// CARRIED — REJECTED ON LOOK, 2026-08-10, the same day it shipped.
//
// On real trail mass it reads as GLITCHY CLIPPING in a ring around the vessel. The
// failure is geometric and the flat measurements were structurally blind to it: a
// volumetric crack PLANE that happens to lie nearly parallel to a viewed SURFACE
// intersects it in a region whose ramp is nearly constant, so a face-sized plate
// shares one threshold and flips at one alpha — a plate-flash, not a dither. The 2D
// kernel cannot produce this (its band direction always lies in the screen plane),
// and neither the uniform sweep (0.0006), the in-situ corridor bin (0.0031) nor the
// flat z=0 preview slice can see it, because all three sample the field off
// surface-glancing geometry. Passing the number is necessary, not sufficient — the
// tessellation candidate's lesson, paid a second time.
//
// Kept carried because the anchoring insight is real and still wanted. The
// glancing-plane failure is solved by kernel 6 (SHARD3D): filling polyhedra by
// Euclidean distance to the owner site — level sets are spheres, closed surfaces
// that cannot lie flat against a face over a whole plate — instead of by parallel
// planar cuts. Do not re-ship THIS kernel as-is. SHARD3D is the Lab candidate;
// it is not the shipped kernel until it earns its look on real mass at speed.
//
// The original design rationale, kept for that successor: the same proposition as
// SHATTER — Voronoi cells filled between parallel straight cuts so the negative
// space is the motif — lifted from the screen into the WORLD: the cells become
// polyhedra, the walls become crack planes, and the pattern BELONGS TO THE WORLD
// instead of to the screen. Three things fall out at once:
//
//   * TRUE PARALLAX. Surfaces stacked along one camera ray occupy different world
//     positions, so they sample decorrelated regions of the field by construction —
//     the layered moiré-beat cannot form, and the depth-parallax shear (which only
//     screen-anchored kernels need) is irrelevant here.
//   * NO STROBE AT SPEED. A screen-anchored pattern slides over fast-moving geometry,
//     flashing the bright prism face against its dark interior at the slide rate. A
//     world-anchored pattern MOVES WITH the geometry: its optical flow is the scene's
//     own optical flow, so high speed reads as motion, not flicker.
//   * VOLUMETRIC READ. The corridor visibly carves a hole through a standing crystal
//     lattice — the "less 2D" feel, delivered literally.
//
// THE OCTAVE LADDER — how a world-anchored pattern holds the SCREEN-pixel fidelity
// window. A fixed world cell size only holds the measured 8–20 px window over a ~2.4×
// depth range, and the corridor spans more. Scaling the lattice continuously with
// distance is NOT an option — a lattice anchored at the world origin that rescales
// per frame sweeps hundreds of cells past any distant fragment per frame (full-screen
// shimmer), and anchoring the scale at the camera collapses the field to angular
// coordinates, which is exactly the screen anchoring we are escaping. So the cell
// size is snapped to a POWER-OF-TWO ladder of world sizes: within one rung the
// lattice is a fixed world lattice (zero swim, perfect parallax), and a fragment
// picks the rung nearest its ideal angular size, so cells render at cellPx × [0.7,
// 1.4] — inside the window. The rung boundary is a camera-centred shell; it is
// JITTERED per fixed 8-unit world chunk (one extra hash) so it reads as a ragged
// chunk-scale mix instead of a legible sphere, and a world point only crosses it
// once per halving of its distance — a chunk re-seeds its cracks once or twice
// during an entire high-speed approach, against the screen kernels' continuous
// slide.
//
// COVERAGE IS EXACT BY CONSTRUCTION, like 2D SHATTER's: the threshold is
// frac(h.x + ramp(...)) with h.x uniform and INDEPENDENT of everything in the ramp —
// the band phase comes from h.x while the cut-plane normal is built from h.y/h.z
// alone (uniform on the sphere via azimuth + cos-latitude). Do not "simplify" the
// normal to normalize(h - 0.5): that correlates the plane with the phase through
// h.x, and frac(X + g(X)) is not uniform. Octave choice is a per-chunk MIXTURE of
// uniform thresholds, and a mixture of uniforms is uniform — the ladder cannot bend
// coverage either. No CDF, nothing to refit; what bounds the dials is sampling, same
// as 2D (cell 8–20 px equivalent, wall ≤ ~1.25× the cell) — re-measured in the Lab,
// not derived.
//
// COST. One jitter hash + an 8-cell octant search (2×2×2: the eight cells whose
// centres are nearest the fragment) + one band hash ≈ the 2D kernel's ten hashes,
// plus a log2/exp2 and a sphere-direction sincos. The octant search CAN misattribute
// an owner near an equidistant boundary (the exhaustive answer is 3×3×3 = 27); for
// SHATTER that displaces a crack by a sliver and cannot touch coverage — every
// cell's phase is uniform — which is why the 3.4× search cost buys nothing a dither
// can show.
//
// MORPH: identical to the 2D cellular kernels — sites orbit inside their own cells
// (bounded, so the octant search stays valid), and the band phase advances at the
// morph rate; both sit inside frac() of a uniform, so the motion is coverage-free.
// -----------------------------------------------------------------------------
static const float PRISM_OCCLUSION_SHATTER3D_CELL = 12.0;  // ideal cell size on screen, px (8-20)
static const float PRISM_OCCLUSION_SHATTER3D_WALL = 1.2;   // wall period as a RATIO of the cell (<= ~1.25)

float PrismOcclusionShatter3DCell()
{
    return PRISM_OCCLUSION_DIAL(_PrismOcclusionDitherC.z, PRISM_OCCLUSION_SHATTER3D_CELL);
}

float PrismOcclusionShatter3DWall()
{
    return PRISM_OCCLUSION_DIAL(_PrismOcclusionDitherC.w, PRISM_OCCLUSION_SHATTER3D_WALL);
}

// Shared by SHATTER3D and SHARD3D: nearest power-of-two world cell size to the
// ideal angular size, rung boundary jittered per 8-world-unit chunk so adjacent
// fragments at the same depth do not all flip rungs together.
float PrismOcclusionOctaveCellWorld(float3 positionWS, float angularScale, float targetPx)
{
    float targetWorld = max(targetPx * angularScale, 1e-5);
    float jitter = PrismOcclusionHash1(floor(positionWS * 0.125)) - 0.5;
    float rung = floor(log2(targetWorld) + 0.5 + 0.35 * jitter);
    return exp2(rung);
}

// The 2×2×2 octant search: base = floor(q - 0.5) makes base + {0,1}³ exactly the
// eight cells whose centres are nearest q. Shared by SHATTER3D and SHARD3D.
void PrismOcclusionOwner3D(float3 q, float phase, out float3 owner, out float bestSq)
{
    float3 base = floor(q - 0.5);
    bestSq = 1e9;
    owner = base;
    [unroll]
    for (int z = 0; z <= 1; ++z)
    {
        [unroll]
        for (int y = 0; y <= 1; ++y)
        {
            [unroll]
            for (int x = 0; x <= 1; ++x)
            {
                float3 cell = base + float3(x, y, z);
                float3 orbit = 0.5 + 0.5 * sin(6.28318530718 * PrismOcclusionHash3(cell) + phase);
                float3 offset = (cell + orbit) - q;
                float dd = dot(offset, offset);
                if (dd < bestSq)
                {
                    bestSq = dd;
                    owner = cell;
                }
            }
        }
    }
}

// angularScale = world units per screen pixel at the fragment's distance
// (radial distance / focal length in px) — computed by the caller, which owns the
// camera; the preview shader passes 1 so preview pixels ARE world units.
float PrismOcclusionShatter3D(float3 positionWS, float angularScale, float time)
{
    float phase = time * PrismOcclusionMorphRate() * 6.28318530718;
    float cellWorld = PrismOcclusionOctaveCellWorld(positionWS, angularScale, PrismOcclusionShatter3DCell());
    float3 q = positionWS / cellWorld;
    float3 owner;
    float bestSq;
    PrismOcclusionOwner3D(q, phase, owner, bestSq);

    // Band phase from h.x; cut-plane normal from h.y/h.z ONLY (uniform on the sphere:
    // uniform azimuth + uniform cos-latitude). Independence of phase and normal is
    // what keeps frac() uniform — see the header note.
    float3 h = PrismOcclusionHash3(owner + 61.0);
    float az = 6.28318530718 * h.y;
    float cz = 2.0 * h.z - 1.0;
    float sz = sqrt(max(1.0 - cz * cz, 0.0));
    float3 dir = float3(sz * cos(az), sz * sin(az), cz);

    // Wall period is authored RELATIVE to the cell (q is already in cell units).
    float ramp = dot(q - owner, dir) / max(PrismOcclusionShatter3DWall(), 1e-3);

    return PrismOcclusionSafeThreshold(
        frac(h.x + ramp + time * PrismOcclusionMorphRate()));
}

// -----------------------------------------------------------------------------
// Kernel G — WORLD-SPACE SHARD3D (Prompt 16, 2026-08-25).
// Lab candidate. NOT the shipped kernel. Coverage proven offline; look on real
// mass at speed is the remaining gate. Do not Bake as CURRENT until that look
// is earned — SHATTER3D passed every fidelity number and was REJECTED ON LOOK
// the day it shipped.
//
// THE FAILURE THIS REPLACES. SHATTER3D fills Voronoi polyhedra with planar
// cuts (`dot(q - owner, dir)`). A plane is constant along every direction
// perpendicular to `dir`, so a crack plane lying near-parallel to a viewed
// face paints a face-sized plate at one threshold — a flash, not a dither.
// Uniform sweep, in-situ bin, and the Lab's flat z=0 preview are all
// structurally blind to that (none samples the field off surface-glancing
// geometry).
//
// THE SHAPE. Same world frame, same octave ladder, same 2×2×2 owner search,
// same morph (sites orbit inside their cells). The fill is Euclidean
// distance-to-owner (3D Worley F1). Level sets of distance-from-a-point are
// SPHERES — closed surfaces — so they cannot lie flat against a plane over a
// whole plate: distance from a point to points on a plane varies across that
// plane unless the plane is a single point, which it is not. A polyhedral
// gauge (cube / tetrahedron / octahedron) COULD plate-flash if a facet were
// parallel to the viewed face; Euclidean is the geometrically safe metric,
// especially for axis-aligned environment plates.
//
// WHAT IT KEEPS from SHATTER3D (the anchoring insight that stayed right):
// true parallax between stacked layers, no strobe at speed (optical flow IS
// the scene's), screen-pixel fidelity via the octave ladder. A fix that
// MOVES THE PATTERN GLOBALLY cannot win against speed — this one does not
// move it; it belongs to the world.
//
// WALL is meaningless for a distance fill (there is no band). The Lab hides
// it. CELL is shared with SHATTER3D (`PRISM_OCCLUSION_SHATTER3D_CELL`) so
// both volumetric kernels A/B at the same angular size.
//
// CDF. 3D F1 is not uniform; the 2D CELL_CDF pair (0.011 / 0.873) is a 2D
// Worley/SHARD fit and must not be reused. Fitted by
// Tools/Shaders/verify_prism_shard3d.py against a clang build of THIS file
// (LO=0.155 HI=0.915, compiled |coverage−alpha|=0.00783 on n=8000).
// -----------------------------------------------------------------------------
static const float PRISM_OCCLUSION_SHARD3D_CDF_LO = 0.155;  // verify_prism_shard3d.py --bake
static const float PRISM_OCCLUSION_SHARD3D_CDF_HI = 0.915;

float PrismOcclusionShard3D(float3 positionWS, float angularScale, float time)
{
    float phase = time * PrismOcclusionMorphRate() * 6.28318530718;
    float cellWorld = PrismOcclusionOctaveCellWorld(positionWS, angularScale, PrismOcclusionShatter3DCell());
    float3 q = positionWS / cellWorld;
    float3 owner;
    float bestSq;
    PrismOcclusionOwner3D(q, phase, owner, bestSq);
    return PrismOcclusionSafeThreshold(smoothstep(
        PRISM_OCCLUSION_SHARD3D_CDF_LO, PRISM_OCCLUSION_SHARD3D_CDF_HI, sqrt(bestSq)));
}

// -----------------------------------------------------------------------------
// THE DISPATCH — the single point at which a kernel is chosen.
//
// It takes EVERY parameterisation because the kernels do not share one: the screen-
// anchored kernels want pixel coordinates, the spiral wants the corridor's own polar
// frame, and SHATTER3D wants the world position plus the angular scale (world units per
// screen pixel at the fragment's distance). Passing all of them keeps the selection in
// one function instead of duplicating it at every call site, which matters because there
// are two call sites — the corridor itself, and the Occlusion Dither Lab's preview
// shader. The preview is therefore not a reimplementation of the look: it is literally
// this function, so a preview cannot drift from what the game draws (it passes
// positionWS = (pixel, 0) and angularScale = 1, so preview pixels ARE world units and
// both volumetric kernels (SHATTER3D and SHARD3D) show their z = 0 slice at 1:1).
//
// PolarValid says whether (radialRatio, angleTurns) describe a real corridor frame. Since
// the dither became THE prism transparency mechanism (fade-outs, cloak, authored sub-1
// alpha — see the corridor test below), a fragment can need a threshold OUTSIDE the
// corridor, where no polar frame exists. The four screen-anchored kernels don't care; the
// SPIRAL is corridor-anchored and would read frozen zeros there — the whole prism popping
// at one alpha — so it falls back to IGN for out-of-corridor fades. The preview always
// passes true (it synthesizes a valid frame in every mode).
//
// Under PRISM_OCCLUSION_LIVE_TUNING 0 the #if chain leaves exactly one call and the other
// kernels are dead-stripped, so the shipped shader is what it always was.
// -----------------------------------------------------------------------------
float PrismOcclusionDitherThreshold(float2 pixel, float radialRatio, float angleTurns, bool polarValid,
    float3 positionWS, float angularScale, float viewDepth, float time)
{
#if PRISM_OCCLUSION_LIVE_TUNING
    // The branch is on a GLOBAL, so it is uniform across the entire frame — fully
    // coherent, never divergent. The cost of design mode is the six unused kernels
    // sitting in the shader, not this compare.
    int kernel = PRISM_OCCLUSION_TUNING_ON
        ? (int)(_PrismOcclusionDitherA.x - 1.0)
        : PRISM_OCCLUSION_KERNEL;

    if (kernel == PRISM_OCCLUSION_KERNEL_SHATTER3D) return PrismOcclusionShatter3D(positionWS, angularScale, time);
    if (kernel == PRISM_OCCLUSION_KERNEL_SHARD3D)   return PrismOcclusionShard3D(positionWS, angularScale, time);
    if (kernel == PRISM_OCCLUSION_KERNEL_SHARD)     return PrismOcclusionShard(pixel, time);
    if (kernel == PRISM_OCCLUSION_KERNEL_SHATTER)   return PrismOcclusionShatter(pixel, viewDepth, time);
    if (kernel == PRISM_OCCLUSION_KERNEL_WORLEY)    return PrismOcclusionWorley(pixel, time);
    if (kernel == PRISM_OCCLUSION_KERNEL_SPIRAL)
        return polarValid ? PrismOcclusionSpiral(radialRatio, angleTurns, time)
                          : PrismOcclusionMotley(pixel);
    return PrismOcclusionMotley(pixel);
#elif PRISM_OCCLUSION_KERNEL == PRISM_OCCLUSION_KERNEL_SHATTER3D
    return PrismOcclusionShatter3D(positionWS, angularScale, time);
#elif PRISM_OCCLUSION_KERNEL == PRISM_OCCLUSION_KERNEL_SHARD3D
    return PrismOcclusionShard3D(positionWS, angularScale, time);
#elif PRISM_OCCLUSION_KERNEL == PRISM_OCCLUSION_KERNEL_SHARD
    return PrismOcclusionShard(pixel, time);
#elif PRISM_OCCLUSION_KERNEL == PRISM_OCCLUSION_KERNEL_SHATTER
    return PrismOcclusionShatter(pixel, viewDepth, time);
#elif PRISM_OCCLUSION_KERNEL == PRISM_OCCLUSION_KERNEL_WORLEY
    return PrismOcclusionWorley(pixel, time);
#elif PRISM_OCCLUSION_KERNEL == PRISM_OCCLUSION_KERNEL_SPIRAL
    return polarValid ? PrismOcclusionSpiral(radialRatio, angleTurns, time)
                      : PrismOcclusionMotley(pixel);
#else
    return PrismOcclusionMotley(pixel);
#endif
}

// Does the selected kernel need the corridor's polar frame? Only the spiral does, and
// working it out costs an atan2 — so in shipped mode this folds to a compile-time
// constant and the atan2 disappears entirely for the other six. (There is no NEEDS_PIXEL
// counterpart any more: every kernel selection needs the pixel now, because even a spiral
// build dithers out-of-corridor fades through IGN, which reads pixels.)
#if PRISM_OCCLUSION_LIVE_TUNING
#define PRISM_OCCLUSION_NEEDS_POLAR 1
#elif PRISM_OCCLUSION_KERNEL == PRISM_OCCLUSION_KERNEL_SPIRAL
#define PRISM_OCCLUSION_NEEDS_POLAR 1
#else
#define PRISM_OCCLUSION_NEEDS_POLAR 0
#endif

// -----------------------------------------------------------------------------
// THE NOSE CLEARANCE — where the corridor STOPS, in multiples of the vessel's own
// circumscribing hull radius (2026-08-11).
//
// The cone used to run all the way to the vessel's ORIGIN plane, with the axial
// gradient still in progress when it got there. A prism the ship is flying into is
// therefore still PARTLY DEMATERIALISED at the moment of contact, and an impact you
// cannot see land does not read as an impact — the collision is with something the
// corridor already half-erased.
//
// So the fade now has to be COMPLETE this far short of the vessel plane, leaving a
// fully solid buffer that the ship's whole nose sits inside. Measured in hull radii
// because that is the one length the corridor already knows and the one that scales
// across the fleet with nothing authored: the hull radius bounds every part of the
// ship about its origin, so a clearance of 1 means "solid from a ship's-length out,
// all the way through the nose and past it".
//
// THE TRADE, STATED. This is the corridor giving ground on its own job: mass inside
// the buffer is solid, so a prism nearly touching the ship CAN occlude it there. That
// is the point — you are trading a sliver of see-through for the impact reading — but
// it is a real trade, and it is why this is a dial. Lower it toward 0.5 if prisms
// start hiding the ship at contact range; 0 restores the old flush-to-the-plane
// behaviour exactly.
//
// DEGENERATE CASE: a camera closer to the ship than the clearance leaves no corridor
// at all (tSolid <= 0 below). That is correct rather than dangerous — inside one hull
// radius there is no room for occluding mass to hide behind anyway.
// -----------------------------------------------------------------------------
static const float PRISM_OCCLUSION_NOSE_CLEARANCE = 1.0;

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
// The corridor test — and, since 2026-08-10, THE prism transparency mechanism.
//
// The dither is no longer corridor-only: the threshold engages for ANY fragment whose
// final alpha lands below 1, wherever it is. That one rule is what lets every prism
// transparency effect ride the same screen door — the corridor's fade, the exploding
// debris fade-out (PrismExplosionClock's Opacity), the cloak family's authored
// near-zero alpha — composing in COVERAGE (alphas multiply before one threshold
// compare), so a fading prism inside the corridor shows one consistent pattern, not
// two stacked effects. It is also what abolished the transparent queue for prisms:
// every prism material is OPAQUE + _ALPHATEST_ON now (enable_prism_alpha_clip.py
// enforces it; PrismOcclusionDiagnostics screams at a transparent one), so no prism
// pays sorting or blend overdraw, and depth writes stay on everywhere.
//
// PositionWS  — the fragment's world position (Shader Graph Position node, World).
//               It is the POST-vertex-animation position, so a prism still blooming
//               on the grow clock is tested where it actually rasterizes.
// Target      — _PrismOcclusionTarget (vessel world position).
// Params      — _PrismOcclusionParams = (outerRadius, innerRadius, coreAlpha).
// BaseAlpha   — whatever fed SurfaceDescription.Alpha before this node (BlockGraph's
//               _Alpha, ExplodingBlockGraph's clock Opacity). Multiplying rather than
//               replacing is what makes the graph's own alpha a first-class dither
//               input: authored sub-1 alpha and clock fades render as coverage.
//
// Alpha         — BaseAlpha scaled by the corridor fade.
// ClipThreshold — 0 when the final alpha is >= 1 (never discards); the kernel's
//                 threshold otherwise, so the material dissolves as a screen door
//                 instead of popping — in the corridor, mid-explosion, or cloaked.
// -----------------------------------------------------------------------------
void PrismOcclusionFade_float(float3 PositionWS, float3 Target, float3 Params, float BaseAlpha,
    out float Alpha, out float ClipThreshold)
{
    Alpha = BaseAlpha;
    ClipThreshold = 0.0;

    // Fast out for fully-dead fragments (an eroded debris chunk, a cloak-invisible
    // prism): nothing survives a threshold of 1 against an alpha of 0, and no kernel
    // needs evaluating to know it. (`clip(0 - 1)` discards on every URP variant; the
    // strictly-inside-(0,1) rule below is for computed thresholds against LIVE alphas.)
    if (BaseAlpha <= 0.0)
    {
        Alpha = 0.0;
        ClipThreshold = 1.0;
        return;
    }

    // ---- Stage 1: the corridor's contribution to alpha. Structured as nested tests
    // rather than the early returns this function used to have, because a fragment
    // OUTSIDE the corridor can still need the dither (any BaseAlpha < 1). The cheap-exit
    // shape survives where it matters: a fully opaque fragment outside the cone runs the
    // same handful of compares it always did and falls through to the alpha gate below.
    float outerRadius = Params.x;
    bool insideCorridor = false;
    float radialRatio = 0.0;
    float3 perp = float3(0.0, 0.0, 0.0);

    // _WorldSpaceCameraPos (UnityInput.hlsl, included by every URP pass) rather than a
    // published uniform: the near end of the corridor is then ALWAYS exactly the camera
    // that is rendering — game view, scene view, any split — with nothing to resolve on
    // the CPU and nothing to keep in sync.
#if defined(SHADERGRAPH_PREVIEW)
    float3 cameraWS = float3(0.0, 0.0, 0.0);
#else
    float3 cameraWS = _WorldSpaceCameraPos.xyz;
#endif

    // outerRadius <= 0 means "corridor off" (no local vessel, or disabled in config) —
    // the corridor contributes nothing, but a fading prism still dithers below.
    if (outerRadius > 0.0)
    {
        float3 axis = Target - cameraWS;
        float3 rel = PositionWS - cameraWS;
        float axisLenSq = dot(axis, axis);

        // axisLenSq ~ 0: camera sitting on the vessel — no axis, no cone.
        if (axisLenSq > 1e-6)
        {
            // t is UNCLAMPED, and the cone is bounded by rejecting t outside (0, tSolid). This
            // makes it a BARE cone: it ends flat at the NOSE CLEARANCE plane — one hull
            // radius short of the vessel's own plane, see the constant above — with no
            // spherical cap past the base and none behind the camera. Mass level with or
            // behind the ship cannot be in front of it, so clearing any of it would be more
            // than the corridor needs. (Saturating t instead would pin the closest point to
            // the vessel past t = 1, and the metric there becomes distance-to-the-ship-point
            // — that is exactly the hemispherical cap this rejection removes.)
            float t = dot(rel, axis) / axisLenSq;

            // Where the fade must be finished — short of the vessel plane by the nose
            // clearance, so the ship and the mass it is about to hit sit in solid air.
            // saturate: a camera inside the clearance yields 0 and switches the corridor
            // off, which is the correct degenerate behaviour (see the constant's note).
            float axisLen = sqrt(axisLenSq);
            float tSolid = saturate(1.0 - (outerRadius * PRISM_OCCLUSION_NOSE_CLEARANCE) / axisLen);

            if (t > 0.0 && t < tSolid)
            {
                // Within (0,1) the closest point on the segment IS the perpendicular foot,
                // so this is the perpendicular distance to the axis. The VECTOR is kept,
                // not just its length — the spiral kernel needs its direction for the
                // corridor-relative angle, and taking it here means the kernel adds no
                // geometry work of its own.
                perp = rel - axis * t;
                float distanceToAxis = length(perp);

                // THE RADIUS TAPERS WITH t — this one multiply is what makes the corridor
                // a CONE rather than a capsule, and it is the whole shape argument. The
                // volume that can actually hide the ship is the eye->silhouette cone: it
                // is a point at the lens and only reaches the hull's radius at the hull.
                // A constant radius (the capsule the retired ClearPrisms CapsuleCollider
                // imposed, carried over into the first shader version) massively
                // over-clears near the camera, where a fixed world radius subtends a huge
                // solid angle. Tapering makes the cleared region a CONSTANT ANGULAR SIZE —
                // exactly the ship's own silhouette, at every depth — so the corridor
                // never dissolves a single prism more than it must.
                float outerAtT = outerRadius * t;
                if (distanceToAxis < outerAtT)
                {
                    insideCorridor = true;

                    // The profile: EXACTLY coreAlpha (0 by default — fully tapered to
                    // nothing, no residual ghost anywhere the ship can be) inside the
                    // inner cone, EXACTLY 1 at and beyond the outer cone, C2-smooth in
                    // between.
                    //
                    // The band is deliberately SHORT so the world snaps back to opaque as
                    // soon as you move off — only a thin shell is ever in transition.
                    // Short and smooth are in tension, which is why the easing is quintic
                    // and the dither is low-discrepancy: both exist to keep a narrow band
                    // from reading as an edge.
                    float innerRadius = min(Params.y, outerRadius);
                    float innerAtT = innerRadius * t;

                    // Radial clearance: 1 inside the inner cone, 0 at the outer surface.
                    float clearRadial = 1.0 - PrismOcclusionSmootherStep(
                        (distanceToAxis - innerAtT) / max(outerAtT - innerAtT, 1e-4));

                    // Axial clearance: 1 up to the base band, 0 at tSolid — the nose
                    // clearance plane, NOT the vessel plane. This grades the BASE.
                    // Without it the cone ended in a hard cut — a prism spanning that
                    // plane was faded on the camera side and solid on the far side, which
                    // reads as a crisp semicircular edge on any large plate at that depth.
                    //
                    // The band's thickness is DERIVED, not authored: it is the radial
                    // shell's own world thickness (outerRadius - innerRadius) expressed in
                    // units of t. That makes the gradient shell ISOTROPIC — the same
                    // thickness across the base as around the sides — so the corridor's
                    // whole boundary fades at one rate and there is no seam anywhere on
                    // it. It also self-scales: a long corridor gets a proportionally short
                    // axial band, a short one a longer band, with nothing to tune. Clamped
                    // to 1 for the degenerate case where the camera is closer to the ship
                    // than the shell is thick.
                    float baseBand = clamp((outerRadius - innerRadius) / axisLen, 1e-4, 1.0);
                    float clearAxial = 1.0 - PrismOcclusionSmootherStep((t - (tSolid - baseBand)) / baseBand);

                    // PRODUCT, not min(): a fragment is cleared only where it is inside
                    // the cone AND before the base, and multiplying two C2 curves stays
                    // C2 — min() would crease wherever the two cross, which is exactly the
                    // artefact this pass exists to remove.
                    float fade = lerp(1.0, Params.z, clearRadial * clearAxial);

                    Alpha = BaseAlpha * fade;

                    // Corridor-relative radial ratio: 0 on the axis, 1 at the cone wall —
                    // it tracks the taper, so the spiral's bands are nested CONES and hold
                    // a constant angular width at every depth, exactly like the profile
                    // they dither.
                    radialRatio = distanceToAxis / max(outerAtT, 1e-4);
                }
            }
        }
    }

    // ---- Stage 2: the dither. One gate for every transparency source: a fragment whose
    // final alpha is fractional dissolves through the screen door; a fully opaque one
    // exits here with threshold 0 (`clip(alpha - 0)` with alpha >= 1 never discards), so
    // solid mass outside the corridor pays no kernel — the same cost contract as ever.
    if (Alpha >= 1.0)
        return;

#if !defined(SHADERGRAPH_PREVIEW)
    // `_Time.y` (UnityInput.hlsl) — seconds since level load. Drives the morph for the
    // continuous kernels; IGN ignores it (see the morph-rate note at the top of the file).
    float time = _Time.y;

    float angleTurns = 0.0;
#if PRISM_OCCLUSION_NEEDS_POLAR
    // The corridor-relative angle exists only INSIDE the corridor (radialRatio was
    // computed with it above); an out-of-corridor fade passes polarValid = false and the
    // dispatch swaps the spiral for IGN there.
    //
    // The angle is measured in the CAMERA's right/up frame rather than in a basis derived
    // from the axis. Any basis built from the axis alone has to pick a reference vector,
    // and it flips — with the whole spiral visibly snapping around — the moment the axis
    // swings past it. The camera's frame has no such degeneracy here (the corridor points
    // away from the lens by construction), and it makes the spiral roll with the camera,
    // which reads as the pattern belonging to the view rather than to the world.
    if (insideCorridor)
    {
        float3 cameraRight = UNITY_MATRIX_V[0].xyz;
        float3 cameraUp = UNITY_MATRIX_V[1].xyz;
        angleTurns = atan2(dot(perp, cameraUp), dot(perp, cameraRight)) * (1.0 / 6.28318530718);
    }
#endif

    // Screen pixel coordinates, reconstructed from the same world position the
    // rasterizer used. Avoids a Screen Position node (and its varying) entirely.
    // Needed by every kernel selection now (even a spiral build falls back to IGN for
    // out-of-corridor fades), and only ever computed for fragments already past the
    // alpha gate — solid mass never reaches this.
    float4 positionCS = TransformWorldToHClip(PositionWS);
    float2 ndc = positionCS.xy / max(abs(positionCS.w), 1e-6);
    float2 pixel = (ndc * 0.5 + 0.5) * _ScreenParams.xy;

    // Linear view depth, handed to the kernels rather than applied to the pixel: the
    // rejected shear moved the whole domain and crawled at speed (see THE LAYERED BEAT).
    // positionCS.w IS linear view depth under a perspective projection, and 1 under an
    // orthographic one — where a constant depth simply disables the term, correctly.
    float viewDepth = abs(positionCS.w);

    // Both volumetric kernels' frame (SHATTER3D and SHARD3D): world units per screen
    // pixel at the fragment's
    // RADIAL distance (radial, not view-depth, so the mapping is invariant under
    // camera roll/turn — turning the camera cannot re-scale the lattice). The focal
    // length in pixels comes off the live projection matrix, so FOV changes (the speed
    // tunnel) re-pick octaves instead of silently shrinking cells out of the fidelity
    // window.
    float focalPx = 0.5 * _ScreenParams.y * max(UNITY_MATRIX_P._m11, 1e-3);
    float angularScale = length(PositionWS - cameraWS) / focalPx;

    ClipThreshold = PrismOcclusionDitherThreshold(pixel, radialRatio, angleTurns, insideCorridor,
                                                  PositionWS, angularScale, viewDepth, time);
#endif
}

// -----------------------------------------------------------------------------
// THE EROSION — the exploding prism's OWN dither, anchored to the prism itself:
// ONE WIPE PER FACE (2026-08-10; reshaped 2026-08-11 onto UV anchoring — the first
// version carved the body into Voronoi chunks and read as the prism being EATEN from
// many points per face; the second anchored to body POSITION and its face
// classification broke under the shatter spin).
//
// The fade-out of exploding debris must not be a function of the VIEW: a screen- or
// world-anchored pattern crawls across a flying, tumbling chunk as the camera and the
// chunk move, so the dissolve reads as something happening TO the image rather than
// to the prism. Each of the box's six faces gets ONE erosion front — a wipe in a
// hashed direction with a gently jagged edge — that sweeps across the face as the
// clock Opacity runs 1 -> 0. Rotate the camera, tumble the prism: nothing about the
// wipe changes. One front per face, a hard jagged line with a dithered dissolve
// fringe ahead of it — soft-hard-soft, not confetti and not a bare cut.
//
// WHERE IT SITS. ExplodingBlockGraph splices this between the explosion clock and the
// corridor node: Opacity -> EROSION -> Survival (0..1; fractional only in the narrow
// front fringe) -> PrismOcclusionFade.BaseAlpha. So the erosion owns the FADE
// (angle-free, body-anchored) while the corridor keeps owning OCCLUSION (a view
// effect by definition), and when a fading prism is also inside the corridor the two
// screen doors compose in coverage. A dead fragment takes the corridor function's
// alpha<=0 fast out (threshold 1, no kernel).
//
// LIVE PRISMS ON THIS GRAPH ARE EXACT PASS-THROUGHS. With no explosion stamped, the
// clock's legacy fallback hands _Opacity through: MazeDangerBlockMateral rests at 1
// (Survival 1 everywhere — fully solid, no pattern, no cost beyond one compare) and
// TransparentPrismMaterial rests at 0 (Survival 0 — cloak-invisible). The early outs
// make both cases exact, not approximate.
//
// THE ANCHOR IS UV0, and that choice is what makes the wipe spin-proof. The first cut
// anchored to the body POSITION and classified faces by dominant axis — but the
// per-face shatter spin migrates fragments across dominance boundaries as pieces
// rotate, so wipes jumped between face frames mid-tumble (reported as "the normals
// stop updating as the pieces spin"). UVs are MESH ATTRIBUTES: no vertex animation —
// flight, spin, scale — can move them, so the front is glued to the face under any
// motion, and the whole flight-undo matrix ride is deleted with the problem. The
// built-in Cube maps every face to UV [0,1]; faces share the wipe's UV-space
// direction and phase, but each face's UV frame is ORIENTED differently on the box,
// so in world space the fronts still run in different directions per face.
//
// THE EDGE IS HARD (2026-08-11). It briefly carried a dithered FRINGE — fractional
// survival just ahead of the front, rendered by the corridor stage as screen-door
// speckle — on the reading that soft-hard-soft wanted a soft trailing component. In
// motion that was wrong: the debris edge then dissolved in the SAME visual language as
// the corridor it flies through, and the two effects read as one confused surface
// rather than as "a prism breaking up" inside "the world going see-through". The
// motif's soft component here is the unbroken face the front eats into and the
// irregular JAG of the front itself; the front line stays hard so the event stays
// legible as its own. See PRISM_EROSION_FRINGE.
//
// THE WIPE FINISHES EARLY BY DESIGN. Thresholds are compressed above END_MARGIN, so
// every fragment is gone by alpha = END_MARGIN — 15% of the fade before the entity
// retires (and the fade itself was extended 1.5×, PrismExplosion.DefaultDuration).
// Without the margin the last slivers ride alpha all the way to ~0.001 and the batch
// retirement beats the wipe to them — the "pieces vanish before the wipe finishes"
// bug, structurally closed instead of tuned around.
//
// COVERAGE. The wipe coordinate's distribution over a square face is trapezoidal, not
// uniform, so the raw threshold is pushed through a smoothstep fitted to its measured
// CDF over CUBE FACES (Tools/Shaders/fit_prism_erosion_cdf.py — rerun it if WIGGLE or
// the wiggle frequency move). Coverage error here is TIME-domain — a slight bend in
// the fade curve, never a spatial artefact.
// -----------------------------------------------------------------------------
static const float PRISM_EROSION_WIGGLE = 0.12;      // jagged-front amplitude, in wipe units
static const float PRISM_EROSION_WIGGLE_FREQ = 2.5;  // jags across one face width
static const float PRISM_EROSION_END_MARGIN = 0.15;  // wipe completes by alpha = this (15% early)
// Dithered dissolve band leading the front, in alpha units. SHIPPED AT 0 = HARD EDGE.
//
// A fringe returns FRACTIONAL survival just ahead of the wipe, which the corridor stage
// then renders as screen-door speckle — and that is precisely the problem: the debris
// edge dissolved in the same visual language as the tunnel it flies through, so the two
// effects read as one confused surface instead of "a prism breaking up" plus "the world
// going see-through". The erosion's job is to be legible as its own event, and the
// motif's hard component is what carries that: a hard jagged front, whose organic
// quality comes from the value-noise JAG rather than from a gradient.
//
// So the fade now reads solid face -> hard irregular front -> gone, with the only dither
// on debris being the corridor's own when a chunk flies through the cone — which is
// correct, because there it IS the tunnel acting on it.
//
// Non-zero re-enables the graded edge; the branch below is on a compile-time constant,
// so the unused side folds away and the hard path costs one compare.
static const float PRISM_EROSION_FRINGE = 0.0;
static const float PRISM_EROSION_CDF_LO = -0.02;     // fitted to the measured raw-threshold CDF
static const float PRISM_EROSION_CDF_HI = 1.02;      // (Monte-Carlo over the UV square) — see
                                                     // fit_prism_erosion_cdf.py

void PrismErosionFade_float(float3 UV, float3 Velocity, float BaseOpacity,
    out float Survival)
{
    // Exact ends — these are what make the live-material pass-throughs exact and the
    // debris fade's first frame clean.
    if (BaseOpacity >= 1.0) { Survival = 1.0; return; }
    if (BaseOpacity <= 0.0) { Survival = 0.0; return; }

    // Face-local frame from UV0, centred: [-1, 1] across the face.
    float2 uv = UV.xy * 2.0 - 1.0;

    // Per-prism identity off the stamped flight vector: wipe direction (h.x) and jag
    // seed (h.z) — every debris chunk peels its own way, no new property, no CPU.
    float3 e = PrismOcclusionHash3(Velocity);
    float3 h = PrismOcclusionHash3(e * 64.0 + 17.0);
    float ang = 6.28318530718 * h.x;
    float2 dir = float2(cos(ang), sin(ang));

    // The wipe coordinate, normalized so it spans EXACTLY [0, 1] over the face
    // regardless of direction (max |dot(uv, dir)| over the square is |dx| + |dy|).
    float w01 = dot(uv, dir) / (abs(dir.x) + abs(dir.y)) * 0.5 + 0.5;

    // The jagged edge: cheap 1D value noise along the cross-front coordinate, so the
    // front reads as an erosion line rather than a ruler cut.
    float c = dot(uv, float2(-dir.y, dir.x)) * PRISM_EROSION_WIGGLE_FREQ + h.z * 64.0;
    float ci = floor(c);
    float cf = c - ci;
    cf = cf * cf * (3.0 - 2.0 * cf);
    float jag = lerp(PrismOcclusionHash1(float3(ci, h.y * 64.0, e.z * 64.0)),
                     PrismOcclusionHash1(float3(ci + 1.0, h.y * 64.0, e.z * 64.0)), cf);
    w01 = saturate(w01 + (jag - 0.5) * PRISM_EROSION_WIGGLE);

    // CDF-linearised, then compressed above END_MARGIN so the whole wipe lands inside
    // the fade with room to spare. As Opacity falls, the highest thresholds die
    // first — the front enters from one edge and crosses to the other.
    float threshold = PrismOcclusionSafeThreshold(
        PRISM_EROSION_END_MARGIN +
        smoothstep(PRISM_EROSION_CDF_LO, PRISM_EROSION_CDF_HI, w01) * (1.0 - PRISM_EROSION_END_MARGIN));

    // The fringe: fractional survival in a narrow band ahead of the front. The
    // corridor stage renders any fractional alpha as screen-door coverage, so this
    // costs nothing extra and reads as the face dissolving at the front line.
    // Hard edge by default. The guard is not decoration: a zero fringe in the divide
    // would be inf for a survivor, -inf for a dead fragment and NaN exactly ON the
    // front — and saturate(NaN) is undefined, so the front line itself would be the
    // one place with unspecified behaviour.
    Survival = PRISM_EROSION_FRINGE > 0.0
        ? saturate((BaseOpacity - threshold) / PRISM_EROSION_FRINGE)
        : (BaseOpacity >= threshold ? 1.0 : 0.0);
}

// -----------------------------------------------------------------------------
// THE BACK-FACE SEPARATION — attack the beat's OTHER precondition (2026-08-11).
//
// A layered beat needs TWO things: two surfaces sharing a screen pixel, AND both of
// them sitting at similar mid-band alpha at the same moment. Every pattern-side fix
// (shear, depth phase, world anchoring) attacks the first. This attacks the second,
// and it is the only one that removes the interference rather than scrambling it: if
// the far surface is already fully clipped while the near one is mid-fade, there is
// nothing left to interfere with, whatever the pattern does.
//
// Prisms render TWO-SIDED (`_Cull: 0`, RenderFace Both), which is why the beat's usual
// second layer is the prism's OWN interior — seen through the holes the front face's
// dither just punched, one prism-thickness away and therefore at nearly the same
// corridor alpha. Sharpening the BACK face's alpha (alpha^power, power > 1) drops the
// interior out of the band early while the exterior is still dissolving.
//
// FACING WITHOUT A NEW SEMANTIC. Shader Graph can only expose SV_IsFrontFace through an
// Is Front Face node, and this project has no such node to donor-clone. It has 36
// NormalVector nodes, so the test is done from geometry instead: the interpolated
// normal is the geometric OUTWARD normal on both sides of a two-sided draw (nothing
// here flips it), so `dot(N, camera - position) < 0` means we are looking at the far
// side of that surface. Same answer, ordinary data, no new varying.
//
// WHAT IT COSTS, STATED. This is a LOOK change, not a free win: interiors vanish
// earlier, so a mid-fade prism reads as a thinner shell than it used to. That is the
// trade the dial buys, and POWER = 1 disables it exactly (alpha^1), so it can be
// switched off without touching the graph.
//
// COVERAGE. Untouched as a mechanism — this scales the ALPHA, not the threshold
// distribution, so the screen door still reproduces whatever alpha it is handed. What
// changes is which alpha a back face is handed, which is the entire intent.
//
// WHERE IT SITS. Spliced AFTER PrismOcclusionFade's Alpha output, before
// SurfaceDescription.Alpha — it has to be after, because in the corridor's gradient
// band the graph's own alpha is 1 and only the corridor's fade is fractional, so
// sharpening earlier would square a 1 and do nothing. The clip threshold computed by
// the corridor is unaffected and still compares against this sharpened alpha, which is
// exactly the desired "the interior clips out sooner".
// -----------------------------------------------------------------------------
// alpha^power on far-facing surfaces; 1 = off (exact no-op).
//
// The beat needs BOTH layers in the gradient band at once, so what this dial buys is
// measured as how much of the alpha range still has both in band. Measured through a
// clang build of this file (band taken as alpha in [0.08, 0.92]):
//
//     power | both-in-band over | interior fully gone by alpha
//       1.0 |  0.09 - 0.92      |  0.08      (off: the whole band overlaps)
//       2.0 |  0.28 - 0.92      |  0.28
//       3.0 |  0.44 - 0.92      |  0.43      SHIPPED — removes the lower half
//       4.0 |  0.54 - 0.92      |  0.53
//       6.0 |  0.66 - 0.92      |  0.65
//
// 3.0 ships because it removes half the overlapping range, which is a large enough
// change to be judged in one playtest — a subtler default would leave "did it help?"
// and "is it under-dialled?" indistinguishable. The upper end stays 0.92 by nature:
// near alpha 1 both layers are almost solid and the pattern is sparse holes, which is
// where a beat is least visible anyway. Drop toward 2.0 if interiors read too thin;
// 1.0 disables it without touching the graph.
static const float PRISM_BACKFACE_POWER = 3.0;

float PrismBackFacePower()
{
    return PRISM_OCCLUSION_DIAL(_PrismOcclusionDitherC.y, PRISM_BACKFACE_POWER);
}

void PrismBackFaceFade_float(float3 PositionWS, float3 NormalWS, float BaseAlpha,
    out float Alpha)
{
    Alpha = BaseAlpha;

    // Solid and fully-dead fragments are already unambiguous; only the band can beat.
    if (BaseAlpha >= 1.0 || BaseAlpha <= 0.0) return;

#if !defined(SHADERGRAPH_PREVIEW)
    // Facing test in world space. A degenerate normal (zero-length, un-authored) dots to
    // 0 and takes the front-face branch — the no-op — so bad data can only ever leave
    // the look unchanged, never clip a surface away.
    if (dot(NormalWS, _WorldSpaceCameraPos.xyz - PositionWS) < 0.0)
        Alpha = pow(BaseAlpha, PrismBackFacePower());
#endif
}

#endif // PRISM_OCCLUSION_CORRIDOR_INCLUDED
