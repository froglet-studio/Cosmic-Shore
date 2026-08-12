# SHEPARD_TONE.md — the mass crystal's endless collapse

**Read before touching `ShepardGraph`, any `*MassCrystalMaterial`, or the
`CrystalMass` / `ActiveCrystalMass` prefabs.**

The mass crystal is the game's one **Shepard tone**: an animation that reads as
collapsing inward *forever*, with no start, no end, and no perceptible loop. This
document records what the effect actually is, why alpha blending was undermining it,
and the screen-door mechanism that replaced it (shipped 2026-08-12).

---

## 1. What the effect is

`CrystalMass.prefab` / `ActiveCrystalMass.prefab` are **four nested copies of one crystal
mesh** (`MassCrystalExport1_8-21-25.fbx`, a 480-triangle faceted shell of unit radius),
each with its own material on `ShepardGraph.shadergraph`:

| GameObject | material (parent) | Start | Stop | ScaleDistance |
|---|---|---|---|---|
| `innerShell` | `ActiveMassCrystalMaterial` | 0.33 | 0.0 | on |
| `secondShell` | `ActiveMassCrystalMaterial 1` | 0.66 | 0.33 | on |
| `easedShell` | `ActiveMassCrystalMaterial 2` | 1.0 | 0.66 | on |
| `outerShell` | `ActiveMassCrystalMaterial 3` | 1.03 | 0.98 | **off** |

(The `BlueMassCrystalMaterial*` set are **material variants** of those four — they
override only colour, and inherit every number below. The GameObject names do not line up
with the window order; read the table, not the names.)

Traced out of the graph's edge list, every shell runs the same three lines:

```
travel = hi - (hi - lo) * frac(Time / Period)     // hi/lo = max/min(Start, Stop); Period = 3 s
alpha  = (1.05 - travel) * Opacity
vertex = travel * P                                // when ScaleDistance; otherwise P
```

So each shell **contracts** from `hi` to `lo` while growing more opaque, and at the wrap
it jumps straight back out to `hi`. The jump is invisible because the ensemble tiles:

```
t = 1⁻  { 0.66 @ α0.39,  0.33 @ α0.72,  0.00 @ α1.05 }
t = 0   { 1.00 @ α0.05,  0.66 @ α0.39,  0.33 @ α0.72 }
```

Every shell hands its exact (radius, alpha) state to its inner neighbour, the innermost
vanishes at zero size and full opacity, and a new one is born at the rim at ~zero alpha.
That is a textbook Shepard construction: **each partial is finite, the ensemble is
endless.** `outerShell` does not scale at all — it is a constant faint skin at the true
outer radius whose job is to mask the silhouette while the travelling shells cycle.

---

## 2. Why alpha blending undersold it

A Shepard tone works because the ear can **track an individual partial** — each sine keeps
its own timbre while losing amplitude. The visual analogue needs the eye to track an
individual shell. Blended (`SrcAlpha`/`OneMinusSrcAlpha`, `_ZWrite 0`, queue offsets
−1/0/+1/+1) it cannot:

- **No per-shell edge.** Four translucent shells composite into one soft ball. The inner
  shells only *tint* the disc; none of them has a silhouette of its own to follow.
- **No depth.** `ZWrite` off plus fixed queue offsets means the shells never occlude each
  other, in an effect that is entirely about nested depth. Order is authored, not spatial,
  so there is no parallax cue at all.
- **The outermost travelling shell is invisible.** Its alpha bottoms out at **0.05**. At 5%
  blended it contributes nothing, so the widest visible thing is the shell at
  `travel 1.0 … 0.66` — and the silhouette therefore reads as a **34% sawtooth pulse**. The
  one cue that survives blending is the one cue that exposes the loop.
- **Fresnel is scaled away.** `BaseColor` is `lerp(DullCrystalColor, BrightCrystalColor,
  fresnel⁴)`, multiplied by alpha at composite time — so the far shells lose the rim that
  says "hard crystal facet" exactly when they need it most.

Verified by offline simulation before anything was changed: the four shells were
raytraced and composited, and the result is a flat green ball with a slightly brighter
middle. See §6.

---

## 3. What replaced it — coverage, not intensity

`ShepardToneDither.hlsl` → `ShepardToneDither_float`, spliced into `ShepardGraph` by
`Tools/Shaders/wire_shepard_tone_dither.py`:

```
Multiply(1.05 - travel, _Opacity) -> DITHER.BaseAlpha
Position(Object) -----------------> DITHER.PositionOS
_Start / _Stop -------------------> DITHER.Start / .Stop
DITHER.Alpha ---------------------> SurfaceDescription.Alpha
DITHER.ClipThreshold -------------> SurfaceDescription.AlphaClipThreshold
```

and every `ShepardGraph` material flipped to **opaque + `_ALPHATEST_ON`**
(`Tools/Shaders/enable_shepard_alpha_clip.py`). A screen door drops **fragments**, not
intensity, so:

- **A fading shell becomes sparser, not dimmer.** Every surviving fragment keeps full
  colour, full fresnel and a hard edge, so the shell stays a legible crystal surface down
  to a handful of shards. The partial keeps its timbre — the eye can lock on and follow it
  inward.
- **The nesting becomes parallax.** Opaque + `ZWrite` means an outer shell genuinely
  occludes the ones behind it, and you see them **through the holes it punches**.
- **The 5% shell finally reads.** Full-intensity sparse shards at the true outer radius are
  visible where a 5% wash was not, so the skin masks the silhouette sawtooth it always
  existed to mask.
- **It is the platform's existing visual language** — the prism occlusion corridor, the
  debris erosion and the cloak family all already dissolve by screen door
  (`Docs/PRISM_ANIMATION.md` §4.7). One HyperSea, one rule set.

### 3.1 The kernel

Distance-to-owner over a jittered lattice, **evaluated on the crystal's own direction
sphere** (`normalize(PositionOS)`), remapped through a fitted CDF, nudged strictly inside
(0,1). Three properties are load-bearing and none of them is a preference:

1. **Object-anchored, so it does not crawl.** The dissolve is something happening to the
   crystal, not to the image — the same reason `PrismErosionFade` anchors to UV0.
2. **Scale-invariant, so it accretes.** Every shell is the same mesh under a uniform scale
   about the origin, so the direction is unchanged by `travel`. A shell's pattern is
   therefore *identical* at every point in its journey: as its alpha rises, its shards
   **grow from their own centres and coalesce into a solid nugget** rather than
   reshuffling. That accretion read is the "mass condensing" story, and it is free.
3. **Distance-to-owner, not crack planes.** This is the successor direction the prism
   `SHATTER3D` rejection explicitly noted (`Docs/PRISM_ANIMATION.md` §4.7, 2026-08-10): a
   volumetric lattice cut by *planes* puts a face-sized plate at one threshold whenever a
   crack lies near-parallel to the surface, and the whole facet flashes at one alpha. Every
   level set of a distance-to-owner fill is a **closed convex surface** around its seed,
   which can never lie flat against a facet.

**The unit shape is an octahedron** (`SHEPARD_DITHER_GAUGE_OCTA`), because the house motif
is soft-hard-soft and a sphere is soft with a soft gradient either side of it — the same
argument that retired round Worley flecks from the prism corridor. It is also the cheapest
of the three carried gauges. Sphere and cube are carried behind the same `#define`; all
three are **volume-normalised to the equal-volume sphere radius**, which is why one CDF fit
serves all three and why retuning a normalisation constant means refitting.

**Each shell seeds its own lattice from its own `[Start, Stop]` window.** Four concentric
shells sampling one direction field would punch their holes along the same rays and the
crystal would look like it had fixed windows drilled through it. Deriving the seed from the
one thing that already differs means nothing is authored and a fifth shell decorrelates
itself. Measured pairwise agreement at α 0.3 is 0.60–0.62 against 0.58 for true
independence. The seed enters the **hash only**, never the lattice coordinate — adding it
to `q` also pushes the Hoskins hash's argument up, and that hash loses uniformity as its
argument grows (a seed of 25 doubled the coverage error).

### 3.2 Tuning

| constant | value | note |
|---|---|---|
| `SHEPARD_DITHER_CELLS` | 6.0 | cells per unit of direction. ~450 cells against the mesh's 480 triangles, so a shard is roughly a facet. **The one dial worth turning**: 3 reads as debris, 10+ as generic noise, 5–7 is the window. The CDF fit is scale-invariant, so this is free to retune. |
| `SHEPARD_DITHER_SEED_SPAN` | 2.0 | per-shell hash offset. Keep small. |
| `SHEPARD_DITHER_GAUGE` | OCTA | SPHERE / OCTA / CUBE. |
| `SHEPARD_DITHER_CDF_LO/HI` | 0.14264 / 0.90153 | fitted; re-run the fitter after changing anything above. |

The Shepard maths itself is untouched — `1.05 - travel`, the 3 s period, the four windows
are all exactly as they were. If the outer shell wants to read denser, the lever is the
material's `_Opacity`, not the dither.

---

## 4. Measured fidelity, and why the bar is different here

|coverage − alpha|, measured **through a clang build of the shipped HLSL**
(`/asset-surgery` §4.5c), 40 000 samples per window:

| window | mean | max |
|---|---|---|
| (0.33, 0.0) | 0.0113 | 0.0282 |
| (0.66, 0.33) | 0.0117 | 0.0233 |
| (1.0, 0.66) | 0.0097 | 0.0227 |
| (1.03, 0.98) | 0.0106 | 0.0226 |
| **ensemble** | **0.0094** | **0.0151** |

The prism corridor holds itself to ~0.003, and that is not the same bar. **Alpha on this
shader is a uniform** — one value for the whole shell, recomputed per frame from `Time` —
so there is no spatial gradient band anywhere in the effect and a coverage error cannot
become a spatial artefact the way it does across the corridor's short fade. It is purely
time-domain: a ~1% bend in how fast a shell thins out. (Same reasoning
`fit_prism_erosion_cdf.py` records for its own trapezoidal fit.)

**The pattern is precision-chaotic and that is fine.** `hash3` folds a ~1e4 magnitude
through `frac()`, so its low bits do not survive float32 rounding: a float64 mirror of the
kernel produces a statistically identical but pointwise *different* pattern (KS distance
0.006 against the compiled build; 24% of samples land in a different cell). Nothing in
gameplay reads the pattern, so only the distribution has to match — which is why the fitter
validates by distribution rather than pointwise.

---

## 5. The stated trade, and one deliberate non-change

**Object anchoring is the opposite trade from the corridor's screen anchoring**, and it
costs what that buys: the shard's *screen* size scales with the crystal's screen size. Up
close the shards are large and legible — which is where the effect is being looked at. Far
away they shrink toward the pixel and the dissolve degrades toward shimmer, because a
screen door has no mip chain and MSAA is off. A crystal is a pickup rather than a wall of
mass, so it is small on screen exactly when it matters least. If distant crystals ever read
as noisy, **the lever is `SHEPARD_DITHER_CELLS`, not a switch back to blending.**

`_AlphaToMask` stays **0**, deliberately: alpha-to-coverage would resolve the screen door
back into smooth alpha and undo the entire change.

`disabledShaderPasses` (`SHADOWCASTER`, `DepthOnly`, `MOTIONVECTORS`) is **left alone**. The
crystal is an unlit, non-shadow-casting pickup (`m_CastShadows: false` on the graph target),
and the depth ordering this change is after comes from `ZWrite` in the **forward** pass, not
from the depth prepass. Re-enabling `DepthOnly` would put the crystal into the camera depth
texture and change every depth-sampling effect in the scene — a separate decision, not a
side effect of this one.

---

## 6. Files, tools and gates

| role | path |
|---|---|
| The kernel | `Assets/_Graphics/Materials/Graphs/ShepardToneDither.hlsl` |
| The graph it splices into | `Assets/_Graphics/Materials/Graphs/ShepardGraph.shadergraph` |
| Graph surgery (idempotent) | `Tools/Shaders/wire_shepard_tone_dither.py` |
| Material opaque+clip contract (idempotent) | `Tools/Shaders/enable_shepard_alpha_clip.py` |
| CDF re-fit + `--bake` | `Tools/Shaders/fit_shepard_dither_cdf.py` |
| Editor gate | FrogletTools > Ecology > Prism Animation > **Validate Shepard Tone Dither** |
| CI gate | `Assets/_Scripts/Tests/Editor/ShepardToneDitherTests.cs` |

Both wiring tools are **idempotent** and print "already wired" / "ok" on a no-op, so they
are also the merge-conflict resolver for this graph: take one side whole, re-run both,
confirm they report no-ops, then dump the resolved edge list (`/asset-surgery` §2a) rather
than trusting the validators individually.

**Every failure mode here is silent**, which is why there are two gates rather than a
comment. Revert the graph and the crystal goes back to a blended ball that still looks like
"a crystal" in a screenshot. Leave one material transparent and it gets the stipple with
none of the depth ordering — worse than what it replaced, and equally quiet. Disable
`_ALPHATEST_ON` and URP compiles the `Alpha` output away entirely on an opaque surface, so
the shells stop thinning at all and the Shepard tone simply stops happening. None of those
produce a console message.

### In-editor verification

1. Open `Menu_Main` (or any scene with a mass crystal) and let Unity reimport the graph.
   If the crystal renders **magenta**, `git checkout` the graph and re-run the wirer.
2. Run **FrogletTools > Ecology > Prism Animation > Validate Shepard Tone Dither** — expect
   `RESULT: ✅ PASS` with 8 materials on contract.
3. Enter play mode and watch one 3 s cycle. What to look for, in order of what would prove
   the change: the crystal has a **solid core** with a **sparse shard rim**; individual
   shards **grow and coalesce** as a shell collapses (they do not reshuffle); you can see
   inner shells **through the holes** in outer ones; and the outer silhouette no longer
   pulses on the loop.
4. Fly past one at speed and look for crawl. There should be none — the pattern is glued to
   the mesh. Crawl means the anchor stopped being object-space.

Renders in this document's §2/§3 claims were produced by an offline raytrace of the four
shells with thresholds taken from a clang build of the shipped HLSL. **That is a
simulation, not the engine** — it settles the mechanism and the cell-size choice; the
in-editor pass above is what settles the look.
