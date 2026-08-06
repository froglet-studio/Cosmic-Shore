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
// A kernel must also EARN ITS SHAPE. The 2026-08-06 hard-edge pass measured two more
// polygonal candidates that pass the fidelity bar and were still rejected, on the look:
// a triangular TESSELLATION (simplex grid, per-facet phase, facets filling as nested
// triangles — 0.0009 / 0.0056) and Voronoi SHATTER (irregular polygons filling between
// parallel straight lines — 0.0009 / 0.0034). Both dissolve into thin strokes at mid
// alpha and read as scratchy crosshatch rather than as polygons, and the unstaggered
// tessellation (0.16) is the literal wallpaper the Bayer grid was dropped for. Passing
// the number is necessary, not sufficient.
// -----------------------------------------------------------------------------
#define PRISM_OCCLUSION_KERNEL_IGN 0     // screen-space noise — reads as a DISSOLVE
#define PRISM_OCCLUSION_KERNEL_SPIRAL 1  // corridor-relative — reads as an IRIS
#define PRISM_OCCLUSION_KERNEL_WORLEY 2  // screen-space cells — reads as ROUND flecking
#define PRISM_OCCLUSION_KERNEL_SHARD 3   // screen-space cells — reads as TRIANGULAR flecking

#define PRISM_OCCLUSION_KERNEL PRISM_OCCLUSION_KERNEL_SHARD

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
// frame. SHARD keeps Worley's arrangement exactly (same lattice, same jitter, same
// orbit, same remap) and changes only the METRIC, so the flecks become equilateral
// triangles of the same area: hard polygonal unit shape, ambiguous placement, still
// feathered by the corridor's own soft profile. Hard shape, soft sandwich.
//
// Kernel 2 is kept, not deleted — it is the calibration reference every fidelity number
// in this file is quoted against, and it is one #define away if the triangles ever want
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
static const float PRISM_OCCLUSION_MORPH_RATE = 0.12;   // cycles/sec; 0 = frozen

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
        radialRatio * PRISM_OCCLUSION_SPIRAL_RINGS
        + angleTurns * PRISM_OCCLUSION_SPIRAL_ARMS
        + time * PRISM_OCCLUSION_MORPH_RATE));
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
static const float PRISM_OCCLUSION_CELL_CDF_HI = 0.873;   // — do not retune independently

// The fit is named CELL, not WORLEY, because BOTH cellular kernels use it: SHARD's
// triangle gauge is area-normalised against the circle (see kernel D), which lands its
// distance distribution on the same CDF. Re-measured under the triangle metric the
// independent best fit is 0.0118 / 0.8775 — within noise of these two, and the shipped
// pair measures 0.0074 uniform on it. One fit, two metrics; that is the payoff for
// normalising by area rather than by extent.

float2 PrismOcclusionHash2(float2 cell)
{
    float3 p3 = frac(cell.xyx * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.xx + p3.yz) * p3.zy);
}

float PrismOcclusionWorley(float2 pixel, float time)
{
    float2 p = pixel / PRISM_OCCLUSION_CELL_SIZE;
    float2 base = floor(p);
    float phase = time * PRISM_OCCLUSION_MORPH_RATE * 6.28318530718;

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
// Kernel D — screen-space SHARD (triangular cells). CURRENT.
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
    float2 p = pixel / PRISM_OCCLUSION_CELL_SIZE;
    float2 base = floor(p);
    float phase = time * PRISM_OCCLUSION_MORPH_RATE * 6.28318530718;

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

#if PRISM_OCCLUSION_SHARD_ORIENT == PRISM_OCCLUSION_SHARD_FLIP
            // A triangle's point reflection is the opposite-pointing triangle, so half the
            // cells can be flipped for one multiply — no second gauge, no branch.
            q *= (h.x < 0.5) ? -1.0 : 1.0;
#elif PRISM_OCCLUSION_SHARD_ORIENT == PRISM_OCCLUSION_SHARD_SPIN
            // wave.y is already sin(2pi*h.y + phase) from the orbit above, so a full
            // per-cell rotation costs exactly one cos.
            float c = cos(6.28318530718 * h.y + phase);
            q = float2(q.x * c - q.y * wave.y, q.x * wave.y + q.y * c);
#endif

            best = min(best, PrismOcclusionTriangleGauge(q));
        }
    }

    return PrismOcclusionSafeThreshold(smoothstep(
        PRISM_OCCLUSION_CELL_CDF_LO, PRISM_OCCLUSION_CELL_CDF_HI, best));
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
// ClipThreshold — 0 outside the corridor (never discards); the kernel's threshold inside
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
    // the perpendicular distance to the axis. The VECTOR is kept, not just its length —
    // the spiral kernel needs its direction for the corridor-relative angle, and taking
    // it here means the kernel adds no geometry work of its own.
    float3 perp = rel - axis * t;
    float distanceToAxis = length(perp);

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
    // `_Time.y` (UnityInput.hlsl) — seconds since level load. Drives the morph for the two
    // continuous kernels; IGN ignores it (see the morph-rate note at the top of the file).
    float time = _Time.y;

#if PRISM_OCCLUSION_KERNEL == PRISM_OCCLUSION_KERNEL_SPIRAL
    // Corridor-relative polar coordinates. The radial ratio is 0 on the axis and 1 at the
    // cone wall — it tracks the taper, so the spiral's bands are nested CONES and hold a
    // constant angular width at every depth, exactly like the profile they dither.
    float radialRatio = distanceToAxis / max(outerAtT, 1e-4);

    // The angle is measured in the CAMERA's right/up frame rather than in a basis derived
    // from the axis. Any basis built from the axis alone has to pick a reference vector,
    // and it flips — with the whole spiral visibly snapping around — the moment the axis
    // swings past it. The camera's frame has no such degeneracy here (the corridor points
    // away from the lens by construction), and it makes the spiral roll with the camera,
    // which reads as the pattern belonging to the view rather than to the world.
    float3 cameraRight = UNITY_MATRIX_V[0].xyz;
    float3 cameraUp = UNITY_MATRIX_V[1].xyz;
    float angleTurns = atan2(dot(perp, cameraUp), dot(perp, cameraRight)) * (1.0 / 6.28318530718);

    ClipThreshold = PrismOcclusionSpiral(radialRatio, angleTurns, time);
#else
    // Screen pixel coordinates, reconstructed from the same world position the
    // rasterizer used. Avoids a Screen Position node (and its varying) entirely.
    // Shared by every screen-anchored kernel.
    float4 positionCS = TransformWorldToHClip(PositionWS);
    float2 ndc = positionCS.xy / max(abs(positionCS.w), 1e-6);
    float2 pixel = (ndc * 0.5 + 0.5) * _ScreenParams.xy;

#if PRISM_OCCLUSION_KERNEL == PRISM_OCCLUSION_KERNEL_SHARD
    ClipThreshold = PrismOcclusionShard(pixel, time);
#elif PRISM_OCCLUSION_KERNEL == PRISM_OCCLUSION_KERNEL_WORLEY
    ClipThreshold = PrismOcclusionWorley(pixel, time);
#else
    ClipThreshold = PrismOcclusionMotley(pixel);
#endif
#endif
#endif
}

#endif // PRISM_OCCLUSION_CORRIDOR_INCLUDED
