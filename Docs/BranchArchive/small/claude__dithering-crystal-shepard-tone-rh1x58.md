# Branch archive: `claude/dithering-crystal-shepard-tone-rh1x58`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-08-12 by Claude
- **Unmerged commits:** 1
- **Forked from:** `0a94cb38b` (2026-08-11, Merge pull request #707 from froglet-studio/claude/gold-shielded-prism-contras)
- **Tip:** `4f558961b`
- **Files touched (20):**
  - `Assets/_Graphics/Materials/CrystalMaterials/ActiveMassCrystalMaterial 1.mat`
  - `Assets/_Graphics/Materials/CrystalMaterials/ActiveMassCrystalMaterial 2.mat`
  - `Assets/_Graphics/Materials/CrystalMaterials/ActiveMassCrystalMaterial 3.mat`
  - `Assets/_Graphics/Materials/CrystalMaterials/ActiveMassCrystalMaterial.mat`
  - `Assets/_Graphics/Materials/CrystalMaterials/BlueMassCrystalMaterial 1.mat`
  - `Assets/_Graphics/Materials/CrystalMaterials/BlueMassCrystalMaterial 2.mat`
  - `Assets/_Graphics/Materials/CrystalMaterials/BlueMassCrystalMaterial 3.mat`
  - `Assets/_Graphics/Materials/CrystalMaterials/BlueMassCrystalMaterial.mat`
  - `Assets/_Graphics/Materials/Graphs/ShepardGraph.shadergraph`
  - `Assets/_Graphics/Materials/Graphs/ShepardToneDither.hlsl`
  - `Assets/_Graphics/Materials/Graphs/ShepardToneDither.hlsl.meta`
  - `Assets/_Scripts/Editor/ShepardToneDitherValidator.cs`
  - `Assets/_Scripts/Editor/ShepardToneDitherValidator.cs.meta`
  - `Assets/_Scripts/Tests/Editor/ShepardToneDitherTests.cs`
  - `Assets/_Scripts/Tests/Editor/ShepardToneDitherTests.cs.meta`
  - `CLAUDE.md`
  - `Docs/SHEPARD_TONE.md`
  - `Tools/Shaders/enable_shepard_alpha_clip.py`
  - `Tools/Shaders/fit_shepard_dither_cdf.py`
  - `Tools/Shaders/wire_shepard_tone_dither.py`

### `4f558961b` — feat(crystal): sell the Shepard tone with a screen door instead of transparency

_Claude, 2026-08-12 04:52:21 +0000_

```text
The mass crystal is four nested shells on ShepardGraph, each contracting
through its own window of a shared 3s period and handing its exact
(radius, alpha) state to its inner neighbour — every partial finite, the
ensemble endless. Alpha blending was undermining that illusion:

  * four SrcAlpha shells with ZWrite off composite into one soft ball, so
    no shell has an edge for the eye to track — and tracking an individual
    partial is the entire mechanism of a Shepard tone;
  * ZWrite off plus fixed queue offsets means the shells never occlude each
    other, in an effect that is entirely about nested depth;
  * the outermost travelling shell bottoms out at alpha 0.05 and is simply
    invisible when blended, so the widest visible thing is the shell at
    travel 1.0..0.66 and the silhouette reads as a 34% sawtooth PULSE — the
    one cue that survives blending is the one that exposes the loop.

Replaced with a screen-door dither (ShepardToneDither.hlsl): a
distance-to-owner cellular fill with an octahedral unit shape, anchored to
the crystal's own object-space DIRECTION and remapped through a fitted CDF.
Coverage drops instead of intensity, so every surviving fragment keeps full
colour, full fresnel and a hard edge; opaque + ZWrite turns the nesting into
real parallax (you see inner shells through the holes in outer ones); and the
5% shell finally reads as a present skeleton at the true outer radius, masking
the sawtooth it always existed to mask.

Three properties of the anchor are load-bearing: object-space so the pattern
is glued to the mesh and never crawls, scale-invariant so a shell's shards
ACCRETE into a solid nugget rather than reshuffling as it collapses, and
distance-to-owner rather than crack planes — the successor direction the
prism SHATTER3D rejection explicitly noted, since closed level sets can never
lie flat against a facet. Each shell seeds its lattice from its own
[Start, Stop] so four concentric shells cannot punch holes along the same
rays; measured pairwise agreement 0.60-0.62 against 0.58 for independence.

The Shepard maths is untouched. All eight materials flipped to opaque +
_ALPHATEST_ON (parents take the full float flip; the Blue variants inherit
their floats and take the keyword/tag half only).

Verified out of editor: the graph splice is validated in memory before
writing and re-validated after; the HLSL compiles clean under clang -Wall in
all three gauge branches; coverage measured THROUGH that build at 0.0094 mean
/ 0.0151 max (ensemble), which is time-domain only here because alpha is a
per-shell uniform with no spatial gradient band.

Docs/SHEPARD_TONE.md; gates are ShepardToneDitherTests + FrogletTools >
Ecology > Prism Animation > Validate Shepard Tone Dither, because every
failure mode in this system is silent.
```

```text
 .../Materials/CrystalMaterials/ActiveMassCrystalMaterial 1.mat        |  15 +-
 .../Materials/CrystalMaterials/ActiveMassCrystalMaterial 2.mat        |  17 +-
 .../Materials/CrystalMaterials/ActiveMassCrystalMaterial 3.mat        |  17 +-
 .../Materials/CrystalMaterials/ActiveMassCrystalMaterial.mat          |  17 +-
 .../Materials/CrystalMaterials/BlueMassCrystalMaterial 1.mat          |   3 +-
 .../Materials/CrystalMaterials/BlueMassCrystalMaterial 2.mat          |   3 +-
 .../Materials/CrystalMaterials/BlueMassCrystalMaterial 3.mat          |   3 +-
 .../_Graphics/Materials/CrystalMaterials/BlueMassCrystalMaterial.mat  |   3 +-
 Assets/_Graphics/Materials/Graphs/ShepardGraph.shadergraph            | 505 ++++++++++++++++++++++++++++----
 Assets/_Graphics/Materials/Graphs/ShepardToneDither.hlsl              | 294 +++++++++++++++++++
 Assets/_Graphics/Materials/Graphs/ShepardToneDither.hlsl.meta         |   7 +
 Assets/_Scripts/Editor/ShepardToneDitherValidator.cs                  | 168 +++++++++++
 Assets/_Scripts/Editor/ShepardToneDitherValidator.cs.meta             |  11 +
 Assets/_Scripts/Tests/Editor/ShepardToneDitherTests.cs                | 122 ++++++++
 Assets/_Scripts/Tests/Editor/ShepardToneDitherTests.cs.meta           |  11 +
 CLAUDE.md                                                             |   2 +
 Docs/SHEPARD_TONE.md                                                  | 254 ++++++++++++++++
 Tools/Shaders/enable_shepard_alpha_clip.py                            | 167 +++++++++++
 Tools/Shaders/fit_shepard_dither_cdf.py                               | 222 ++++++++++++++
 Tools/Shaders/wire_shepard_tone_dither.py                             | 383 ++++++++++++++++++++++++
 20 files changed, 2125 insertions(+), 99 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 1672 lines)</summary>

```diff
diff --git a/Assets/_Graphics/Materials/Graphs/ShepardToneDither.hlsl b/Assets/_Graphics/Materials/Graphs/ShepardToneDither.hlsl
new file mode 100644
index 000000000..19a3973ca
--- /dev/null
+++ b/Assets/_Graphics/Materials/Graphs/ShepardToneDither.hlsl
@@ -0,0 +1,294 @@
+#ifndef SHEPARD_TONE_DITHER_INCLUDED
+#define SHEPARD_TONE_DITHER_INCLUDED
+
+// =============================================================================
+// SHEPARD-TONE DITHER — the mass crystal's transparency, as coverage instead of blending.
+//
+// WHAT THE CRYSTAL IS. ShepardGraph draws four nested copies of the same crystal mesh
+// (CrystalMass / ActiveCrystalMass: innerShell, secondShell, easedShell, outerShell), each
+// on its own material with its own [Stop, Start] window over a shared 3 s period. Traced
+// out of the graph, every shell runs the same two lines:
+//
+//     travel = hi - (hi - lo) * frac(Time / Period)        // hi/lo = max/min(Start, Stop)
+//     alpha  = (1.05 - travel) * Opacity
+//     vertex = travel * P                                   // when ScaleDistance
+//
+// so each shell CONTRACTS from hi to lo while growing more opaque, and at the wrap it
+// jumps back out to hi — landing exactly where its neighbour was, at exactly its
+// neighbour's alpha (0.66@0.39 -> 0.33@0.72 -> 0.0@1.05 tiles seamlessly). That is a
+// textbook Shepard tone: every partial is individually finite, the ENSEMBLE is endless.
+// A crystal of mass, collapsing inward forever.
+//
+// WHY BLENDING UNDERSOLD IT. The illusion needs the eye to TRACK an individual partial —
+// a Shepard tone works precisely because each sine keeps its own timbre while losing
+// amplitude. Alpha blending destroys that:
+//
+//   * Four SrcAlpha/OneMinusSrcAlpha shells with ZWrite off composite into one soft ball.
+//     No shell has an edge of its own, so there is nothing for the eye to follow, and the
+//     inner shells only TINT the disc rather than reading as surfaces behind it.
+//   * The outermost travelling shell bottoms out at alpha 0.05. At 5% blended it is not
+//     visible at all — so the widest thing on screen is the shell at travel 1.0 .. 0.66,
+//     and the silhouette reads as a 34% SAWTOOTH PULSE. That is the opposite of a Shepard
+//     tone: the one cue that survives blending is the one cue that exposes the loop.
+//   * Fresnel is multiplied by alpha, so the far shells lose the rim that says "hard
+//     crystal facet" exactly when they most need to look like one.
+//   * ZWrite off + fixed queue offsets means the shells never occlude each other — no
+//     depth cue at all in an effect that is entirely about nested depth.
+//
+// WHY COVERAGE SELLS IT. A screen door drops FRAGMENTS, not intensity, so every surviving
+// fragment keeps full colour, full fresnel and a hard edge. The consequences are the whole
+// point of this file:
+//
+//   * A shell fading out becomes SPARSER, not dimmer — it stays a legible crystal surface
+//     down to a handful of shards, so the eye can lock onto it and follow it inward. The
+//     partial keeps its timbre.
+//   * Opaque + ZWrite gives real depth: an outer shell genuinely occludes the ones behind
+//     it, and you see them THROUGH its holes. The nesting becomes parallax instead of tint.
+//   * The 5% outer shell now reads as a present, full-intensity skeleton at the true outer
+//     radius, so it masks the silhouette sawtooth it was always meant to mask.
+//   * It is the same visual language as every other transparency in the game — the prism
+//     occlusion corridor, the debris erosion, the cloak family (Docs/PRISM_ANIMATION.md
+//     §4.7). One HyperSea, one rule set.
+//
+// THE ANCHOR IS THE CRYSTAL'S OWN DIRECTION, and that choice is doing three jobs at once.
+// The threshold field is evaluated at `normalize(PositionOS)` — the object-space direction
+// of the fragment:
+//
+//   1. GLUED TO THE MESH. It is not screen-anchored, so it does not crawl when the camera
+//      or the crystal moves. The dissolve is something happening to the crystal, not to
+//      the image (the same reason PrismErosionFade anchors to UV0).
+//   2. SCALE-INVARIANT. Every shell is the same mesh under a uniform scale about the
+//      origin, so the direction is unchanged by `travel`. One shell's pattern is therefore
+//      identical at every point in its journey: as its alpha rises, its shards GROW from
+//      their own centres and coalesce into a solid nugget, rather than reshuffling. That
+//      accretion read IS the "mass condensing" story, and it comes free from the anchor.
+//   3. NO LAYERED BEAT. The prism corridor's worst artefact — two surfaces stacked along
+//      one camera ray reading the same screen-space threshold and moiré-beating — cannot
+//      occur here: a shell's front and back faces lie in different DIRECTIONS from the
+//      origin, so they sample different cells by construction. Two-sided rendering
+//      (`_Cull: 0`) stays safe with no back-face fade needed.
+//
+// THE TRADE, STATED. Object anchoring is the opposite trade from the corridor's screen
+// anchoring, and it costs what that buys: the shard's SCREEN size scales with the crystal's
+// screen size. Up close the shards are large and legible (which is what you want — that is
+// where the effect is being looked at); far away they shrink toward the pixel and the
+// dissolve degrades into shimmer, since a screen door has no mip chain and MSAA is off
+// (`_AlphaToMask: 0`, deliberately — alpha-to-coverage would resolve the door back into
+// smooth alpha and undo the whole thing). A crystal is a pickup rather than a wall of mass,
+// so it is small on screen exactly when it matters least. If distant crystals ever read as
+// noisy, the lever is SHEPARD_DITHER_CELLS, not a switch back to blending.
+//
+// THE SHELLS MUST NOT SHARE A PATTERN. Four concentric shells sampling one direction field
+// would punch their holes along the same rays, and the crystal would look like it had
+// fixed windows drilled through it. Each shell therefore offsets the hash by a seed
+// DERIVED FROM ITS OWN [Start, Stop] WINDOW — the one thing that already differs between
+// them — so nothing has to be authored and a fifth shell decorrelates itself. Measured
+// pairwise agreement at alpha 0.3 is 0.60-0.62 against 0.58 for true independence.
+// The seed enters the HASH ONLY, never the lattice coordinate: adding it to `q` would
+// shift the cells (fine) but also push the Hoskins hash's input up, and that hash loses
+// uniformity as its argument grows (a seed of 25 doubled the coverage error).
+//
+// =============================================================================
+
+// Cells per unit of DIRECTION, i.e. across the crystal's own unit sphere. 6.0 puts about
+// 450 cells on the sphere against the mesh's 480 triangles, so a shard is roughly a facet
+// and the crystal comes apart along something that looks like its own structure. This is
+// the ONE dial worth turning, and it was chosen by rendering it: 3 reads as debris
+// (chunks far larger than a facet), 10+ reads as noise (the crystalline motif is lost and
+// it becomes generic stipple), 5-7 is the window. The CDF fit below is scale-invariant —
+// the same constants serve any density — so this may be retuned freely.
+static const float SHEPARD_DITHER_CELLS = 6.0;
+
+// Per-shell hash offset span. Keep SMALL: see the seed note above.
+static const float SHEPARD_DITHER_SEED_SPAN = 2.0;
+
+// -----------------------------------------------------------------------------
+// THE UNIT SHAPE. Same discipline as the prism dither's SHARD kernel: change the METRIC,
+// keep the arrangement. The lattice, the jitter, the 3x3x3 search and the CDF remap are
+// identical across all three; only the level-set shape of the distance-to-owner fill
+// changes, from spheres to octahedra to cubes.
+//
+// OCTA ships. The house motif is soft-hard-soft, and a sphere is soft with a soft gradient
+// either side of it — the same argument that retired round Worley flecks from the prism
+// corridor. An octahedral gauge cuts a facet along STRAIGHT lines, so the crystal breaks
+// up into crystal-shaped pieces. It is also the cheapest of the three (two adds and an
+// abs; no sqrt at all, because a gauge is homogeneous of degree 1 and `min` may be taken
+// on it directly).
+//
+// THE VOLUME NORMALISATION IS LOAD-BEARING, exactly as the prism SHARD's area
+// normalisation is. {L1 <= r} is an octahedron of volume (4/3)r^3 and {Linf <= r} a cube
+// of volume 8r^3, against (4/3)pi*d^3 for the sphere; scaling each gauge to the
+// EQUAL-VOLUME sphere radius means the shards occupy the same ink at the same threshold
+// AND the distance distribution lands back on the sphere's own measured CDF. That is why
+// one fit below serves all three, and why retuning a constant here means refitting.
+//     octa: pi^(-1/3)     = 0.68278406
+//     cube: (6/pi)^(1/3)  = 1.24070098
+// -----------------------------------------------------------------------------
+#define SHEPARD_DITHER_GAUGE_SPHERE 0  // round holes — soft-SOFT-soft, off-motif, kept as the reference
+#define SHEPARD_DITHER_GAUGE_OCTA   1  // octahedral holes — straight-edged shards, SHIPPED
+#define SHEPARD_DITHER_GAUGE_CUBE   2  // cubic holes — straight-edged but axis-aligned and repetitive
+
+#define SHEPARD_DITHER_GAUGE SHEPARD_DITHER_GAUGE_OCTA
+
+static const float SHEPARD_DITHER_OCTA_NORM = 0.68278406;
+static const float SHEPARD_DITHER_CUBE_NORM = 1.24070098;
+
+// Smoothstep remap fitted to the measured CDF of the gauge distance over the DIRECTION
+// SPHERE, pooled across the four shipped shell windows (Tools/Shaders/fit_shepard_dither_cdf.py
+// — rerun it if CELLS, the gauge normalisation, the seed derivation or the hash change).
+//
+// WITHOUT THE REMAP the raw cell distance clusters hard around its mean and coverage
+// tracks alpha badly. WITH it, |coverage - alpha| measures 0.0094 mean / 0.0151 max over
+// the ensemble and 0.0117 mean on the worst single shell — measured THROUGH A CLANG BUILD
+// OF THIS FILE (/asset-surgery §4.5c), not through a reimplementation of it.
+//
```

</details>
