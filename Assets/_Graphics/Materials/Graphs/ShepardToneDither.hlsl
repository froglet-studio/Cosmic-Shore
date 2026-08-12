#ifndef SHEPARD_TONE_DITHER_INCLUDED
#define SHEPARD_TONE_DITHER_INCLUDED

// =============================================================================
// SHEPARD-TONE DITHER — the mass crystal's transparency, as coverage instead of blending.
//
// WHAT THE CRYSTAL IS. ShepardGraph draws four nested copies of the same crystal mesh
// (CrystalMass / ActiveCrystalMass: innerShell, secondShell, easedShell, outerShell), each
// on its own material with its own [Stop, Start] window over a shared 3 s period. Traced
// out of the graph, every shell runs the same two lines:
//
//     travel = hi - (hi - lo) * frac(Time / Period)        // hi/lo = max/min(Start, Stop)
//     alpha  = (1.05 - travel) * Opacity
//     vertex = travel * P                                   // when ScaleDistance
//
// so each shell CONTRACTS from hi to lo while growing more opaque, and at the wrap it
// jumps back out to hi — landing exactly where its neighbour was, at exactly its
// neighbour's alpha (0.66@0.39 -> 0.33@0.72 -> 0.0@1.05 tiles seamlessly). That is a
// textbook Shepard tone: every partial is individually finite, the ENSEMBLE is endless.
// A crystal of mass, collapsing inward forever.
//
// WHY BLENDING UNDERSOLD IT. The illusion needs the eye to TRACK an individual partial —
// a Shepard tone works precisely because each sine keeps its own timbre while losing
// amplitude. Alpha blending destroys that:
//
//   * Four SrcAlpha/OneMinusSrcAlpha shells with ZWrite off composite into one soft ball.
//     No shell has an edge of its own, so there is nothing for the eye to follow, and the
//     inner shells only TINT the disc rather than reading as surfaces behind it.
//   * The outermost travelling shell bottoms out at alpha 0.05. At 5% blended it is not
//     visible at all — so the widest thing on screen is the shell at travel 1.0 .. 0.66,
//     and the silhouette reads as a 34% SAWTOOTH PULSE. That is the opposite of a Shepard
//     tone: the one cue that survives blending is the one cue that exposes the loop.
//   * Fresnel is multiplied by alpha, so the far shells lose the rim that says "hard
//     crystal facet" exactly when they most need to look like one.
//   * ZWrite off + fixed queue offsets means the shells never occlude each other — no
//     depth cue at all in an effect that is entirely about nested depth.
//
// WHY COVERAGE SELLS IT. A screen door drops FRAGMENTS, not intensity, so every surviving
// fragment keeps full colour, full fresnel and a hard edge. The consequences are the whole
// point of this file:
//
//   * A shell fading out becomes SPARSER, not dimmer — it stays a legible crystal surface
//     down to a handful of shards, so the eye can lock onto it and follow it inward. The
//     partial keeps its timbre.
//   * Opaque + ZWrite gives real depth: an outer shell genuinely occludes the ones behind
//     it, and you see them THROUGH its holes. The nesting becomes parallax instead of tint.
//   * The 5% outer shell now reads as a present, full-intensity skeleton at the true outer
//     radius, so it masks the silhouette sawtooth it was always meant to mask.
//   * It is the same visual language as every other transparency in the game — the prism
//     occlusion corridor, the debris erosion, the cloak family (Docs/PRISM_ANIMATION.md
//     §4.7). One HyperSea, one rule set.
//
// THE ANCHOR IS THE CRYSTAL'S OWN DIRECTION, and that choice is doing three jobs at once.
// The threshold field is evaluated at `normalize(PositionOS)` — the object-space direction
// of the fragment:
//
//   1. GLUED TO THE MESH. It is not screen-anchored, so it does not crawl when the camera
//      or the crystal moves. The dissolve is something happening to the crystal, not to
//      the image (the same reason PrismErosionFade anchors to UV0).
//   2. SCALE-INVARIANT. Every shell is the same mesh under a uniform scale about the
//      origin, so the direction is unchanged by `travel`. One shell's pattern is therefore
//      identical at every point in its journey: as its alpha rises, its shards GROW from
//      their own centres and coalesce into a solid nugget, rather than reshuffling. That
//      accretion read IS the "mass condensing" story, and it comes free from the anchor.
//   3. NO LAYERED BEAT. The prism corridor's worst artefact — two surfaces stacked along
//      one camera ray reading the same screen-space threshold and moiré-beating — cannot
//      occur here: a shell's front and back faces lie in different DIRECTIONS from the
//      origin, so they sample different cells by construction. Two-sided rendering
//      (`_Cull: 0`) stays safe with no back-face fade needed.
//
// THE TRADE, STATED. Object anchoring is the opposite trade from the corridor's screen
// anchoring, and it costs what that buys: the shard's SCREEN size scales with the crystal's
// screen size. Up close the shards are large and legible (which is what you want — that is
// where the effect is being looked at); far away they shrink toward the pixel and the
// dissolve degrades into shimmer, since a screen door has no mip chain and MSAA is off
// (`_AlphaToMask: 0`, deliberately — alpha-to-coverage would resolve the door back into
// smooth alpha and undo the whole thing). A crystal is a pickup rather than a wall of mass,
// so it is small on screen exactly when it matters least. If distant crystals ever read as
// noisy, the lever is SHEPARD_DITHER_CELLS, not a switch back to blending.
//
// THE SHELLS MUST NOT SHARE A PATTERN. Four concentric shells sampling one direction field
// would punch their holes along the same rays, and the crystal would look like it had
// fixed windows drilled through it. Each shell therefore offsets the hash by a seed
// DERIVED FROM ITS OWN [Start, Stop] WINDOW — the one thing that already differs between
// them — so nothing has to be authored and a fifth shell decorrelates itself. Measured
// pairwise agreement at alpha 0.3 is 0.60-0.62 against 0.58 for true independence.
// The seed enters the HASH ONLY, never the lattice coordinate: adding it to `q` would
// shift the cells (fine) but also push the Hoskins hash's input up, and that hash loses
// uniformity as its argument grows (a seed of 25 doubled the coverage error).
//
// =============================================================================

// Cells per unit of DIRECTION, i.e. across the crystal's own unit sphere. 6.0 puts about
// 450 cells on the sphere against the mesh's 480 triangles, so a shard is roughly a facet
// and the crystal comes apart along something that looks like its own structure. This is
// the ONE dial worth turning, and it was chosen by rendering it: 3 reads as debris
// (chunks far larger than a facet), 10+ reads as noise (the crystalline motif is lost and
// it becomes generic stipple), 5-7 is the window. The CDF fit below is scale-invariant —
// the same constants serve any density — so this may be retuned freely.
static const float SHEPARD_DITHER_CELLS = 6.0;

// Per-shell hash offset span. Keep SMALL: see the seed note above.
static const float SHEPARD_DITHER_SEED_SPAN = 2.0;

// -----------------------------------------------------------------------------
// THE UNIT SHAPE. Same discipline as the prism dither's SHARD kernel: change the METRIC,
// keep the arrangement. The lattice, the jitter, the 3x3x3 search and the CDF remap are
// identical across all three; only the level-set shape of the distance-to-owner fill
// changes, from spheres to octahedra to cubes.
//
// OCTA ships. The house motif is soft-hard-soft, and a sphere is soft with a soft gradient
// either side of it — the same argument that retired round Worley flecks from the prism
// corridor. An octahedral gauge cuts a facet along STRAIGHT lines, so the crystal breaks
// up into crystal-shaped pieces. It is also the cheapest of the three (two adds and an
// abs; no sqrt at all, because a gauge is homogeneous of degree 1 and `min` may be taken
// on it directly).
//
// THE VOLUME NORMALISATION IS LOAD-BEARING, exactly as the prism SHARD's area
// normalisation is. {L1 <= r} is an octahedron of volume (4/3)r^3 and {Linf <= r} a cube
// of volume 8r^3, against (4/3)pi*d^3 for the sphere; scaling each gauge to the
// EQUAL-VOLUME sphere radius means the shards occupy the same ink at the same threshold
// AND the distance distribution lands back on the sphere's own measured CDF. That is why
// one fit below serves all three, and why retuning a constant here means refitting.
//     octa: pi^(-1/3)     = 0.68278406
//     cube: (6/pi)^(1/3)  = 1.24070098
// -----------------------------------------------------------------------------
#define SHEPARD_DITHER_GAUGE_SPHERE 0  // round holes — soft-SOFT-soft, off-motif, kept as the reference
#define SHEPARD_DITHER_GAUGE_OCTA   1  // octahedral holes — straight-edged shards, SHIPPED
#define SHEPARD_DITHER_GAUGE_CUBE   2  // cubic holes — straight-edged but axis-aligned and repetitive

#define SHEPARD_DITHER_GAUGE SHEPARD_DITHER_GAUGE_OCTA

static const float SHEPARD_DITHER_OCTA_NORM = 0.68278406;
static const float SHEPARD_DITHER_CUBE_NORM = 1.24070098;

// Smoothstep remap fitted to the measured CDF of the gauge distance over the DIRECTION
// SPHERE, pooled across the four shipped shell windows (Tools/Shaders/fit_shepard_dither_cdf.py
// — rerun it if CELLS, the gauge normalisation, the seed derivation or the hash change).
//
// WITHOUT THE REMAP the raw cell distance clusters hard around its mean and coverage
// tracks alpha badly. WITH it, |coverage - alpha| measures 0.0094 mean / 0.0151 max over
// the ensemble and 0.0117 mean on the worst single shell — measured THROUGH A CLANG BUILD
// OF THIS FILE (/asset-surgery §4.5c), not through a reimplementation of it.
//
// THE PATTERN IS PRECISION-CHAOTIC AND THAT IS FINE. hash3 folds a ~1e4 magnitude through
// frac(), so its low bits do not survive float32 rounding: a float64 mirror of this kernel
// produces a statistically identical but pointwise DIFFERENT pattern (KS distance 0.006
// against the compiled build; 24% of samples land in a different cell). Nothing in
// gameplay reads the pattern, so only the distribution has to match — which is why
// fit_shepard_dither_cdf.py validates by distribution rather than pointwise, and why these
// constants are a fit to the FAMILY rather than to one realisation.
//
// WHY THAT NUMBER IS COMFORTABLE HERE, when the prism corridor holds itself to ~0.003:
// alpha on this shader is a UNIFORM — one value for the whole shell, recomputed per frame
// from Time. There is no spatial gradient band anywhere in the effect, so a coverage error
// cannot produce a spatial artefact the way it does across the corridor's short fade. It
// is purely TIME-domain: a ~1% bend in how fast a shell thins out. (Same reasoning
// PrismErosionFade records for its own trapezoidal fit.)
static const float SHEPARD_DITHER_CDF_LO = 0.14264;
static const float SHEPARD_DITHER_CDF_HI = 0.90153;

// The clip threshold must land STRICTLY inside (0,1). `frac` can return exactly 0, and a
// 0 threshold against a 0 alpha is `clip(0)` — which KEEPS the fragment on the URP variants
// that clip directly rather than through AlphaDiscard's epsilon. Verbatim from
// PrismOcclusionCorridor.hlsl, for the same reason.
float ShepardToneSafeThreshold(float n)
{
    return n * 0.998 + 0.001;
}

// A copy of PrismOcclusionHash3 (the float-only Hoskins family — no integer ops, so
// identical behaviour on GLES/mobile). Copied rather than #included: pulling in the
// corridor would couple the crystal's look to the corridor's compile-time kernel switch,
// and the corridor's kernels are screen-anchored, which is the one thing this effect must
// not be. Six lines is the cheaper coupling.
float3 ShepardToneHash3(float3 p3)
{
    p3 = frac(p3 * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yxz + 33.33);
    return frac((p3.xxy + p3.yxx) * p3.zyx);
}

// Each shell's lattice offset, derived from the window it already owns. See the seed note.
float3 ShepardToneDitherSeed(float start, float stop)
{
    return frac(float3(start * 17.0 + stop *  7.0,
                       start * 23.0 + stop * 13.0,
                       start * 29.0 + stop * 19.0)) * SHEPARD_DITHER_SEED_SPAN;
}

// Distance-to-owner over a jittered lattice, restricted to the direction sphere.
//
// DISTANCE-TO-OWNER, NOT CRACK PLANES, and that is a direct consequence of the prism
// SHATTER3D rejection (Docs/PRISM_ANIMATION.md §4.7, 2026-08-10): a volumetric lattice cut
// by PLANES puts a face-sized plate at one threshold whenever a crack lies near-parallel
// to the surface, so a whole facet flashes at one alpha. Every level set of a
// distance-to-owner fill is a CLOSED convex surface around its seed, which can never lie
// flat against a facet — the successor direction that rejection explicitly noted.
//
// The 3x3x3 search is what 3D costs: 27 hashes against the corridor's 9. It is paid only
// by fragments with fractional alpha (the solid core early-outs below), on an object that
// is a pickup rather than a wall of mass.
float ShepardToneDitherField(float3 dir, float3 seed)
{
    float3 q = dir * SHEPARD_DITHER_CELLS;
    float3 origin = floor(q);
    float best = 8.0;

    [unroll]
    for (int z = -1; z <= 1; ++z)
    {
        [unroll]
        for (int y = -1; y <= 1; ++y)
        {
            [unroll]
            for (int x = -1; x <= 1; ++x)
            {
                float3 cell = origin + float3(x, y, z);
                float3 offset = (cell + ShepardToneHash3(cell + seed)) - q;

#if SHEPARD_DITHER_GAUGE == SHEPARD_DITHER_GAUGE_OCTA
                best = min(best, (abs(offset.x) + abs(offset.y) + abs(offset.z)) * SHEPARD_DITHER_OCTA_NORM);
#elif SHEPARD_DITHER_GAUGE == SHEPARD_DITHER_GAUGE_CUBE
                best = min(best, max(abs(offset.x), max(abs(offset.y), abs(offset.z))) * SHEPARD_DITHER_CUBE_NORM);
#else
                best = min(best, dot(offset, offset));   // squared while searching
#endif
            }
        }
    }

#if SHEPARD_DITHER_GAUGE == SHEPARD_DITHER_GAUGE_SPHERE
    best = sqrt(best);
#endif
    return best;
}

// -----------------------------------------------------------------------------
// The Custom Function entry point. Spliced into ShepardGraph by
// Tools/Shaders/wire_shepard_tone_dither.py:
//
//   BEFORE:  Multiply(1.05 - travel, Opacity) -----------------> SurfaceDescription.Alpha
//                                                                SurfaceDescription.AlphaClipThreshold = 0.01
//   AFTER:   Multiply -> DITHER.BaseAlpha
//            Position(Object) -> DITHER.PositionOS
//            _Start / _Stop ---> DITHER.Start / .Stop
//            DITHER.Alpha ------------------------------------> SurfaceDescription.Alpha
//            DITHER.ClipThreshold ----------------------------> SurfaceDescription.AlphaClipThreshold
//
// Alpha is passed through untouched and the threshold does all the work, which is the same
// contract PrismOcclusionFade uses: on an opaque alpha-tested URP pass the surface alpha is
// only ever compared against the cutoff, never blended.
//
// NOTE ON `_velocity`: a non-zero velocity adds a tangential twist to the vertex
// displacement, which would rotate directions and therefore slide the pattern over the
// mesh. Every shipped mass-crystal material authors velocity (0,0,0), so the anchor is
// exact today; if a twist is ever authored, the dissolve will drift with it — which is
// arguably correct (the pattern stays glued to the material, not to the rest pose), but it
// is a behaviour change worth knowing about rather than discovering.
// -----------------------------------------------------------------------------
void ShepardToneDither_float(float3 PositionOS, float BaseAlpha, float Start, float Stop,
    out float Alpha, out float ClipThreshold)
{
    Alpha = BaseAlpha;
    ClipThreshold = 0.0;

    // The solid core. The innermost shell reaches alpha 1.05 as it collapses, and a fully
    // opaque fragment has nothing to dither — skipping the kernel here is what keeps the
    // brightest, largest-on-screen part of the crystal free.
    if (BaseAlpha >= 1.0)
    {
        return;
    }

    // Fully dead: nothing survives a threshold of 1 against an alpha of 0, and no kernel
    // needs evaluating to know it. (`clip(0 - 1)` discards on every URP variant.)
    if (BaseAlpha <= 0.0)
    {
        Alpha = 0.0;
        ClipThreshold = 1.0;
        return;
    }

    // The scale-invariant anchor. No shipped crystal vertex sits at the mesh origin (the
    // mesh is a shell of uniform radius), but a degenerate shell scaled to zero would put
    // every fragment there, so the divide is guarded rather than assumed.
    float3 dir = PositionOS / max(length(PositionOS), 1e-5);

    float raw = ShepardToneDitherField(dir, ShepardToneDitherSeed(Start, Stop));
    ClipThreshold = ShepardToneSafeThreshold(
        smoothstep(SHEPARD_DITHER_CDF_LO, SHEPARD_DITHER_CDF_HI, raw));
}

#endif // SHEPARD_TONE_DITHER_INCLUDED
