## 4.5 Technique: offline simulation of a deterministic generator

Origin: the Caldera/Ourobor cell rework (2026-08). §6 below used to list
"play-mode measurements (baselines)" as editor-only, with the workflow being
"human runs the measurer, pastes the output, you author from it." For a
**deterministic** generator you can skip the human entirely — port it and
measure offline.

When it applies: the generator's output is a pure function of serialized inputs
(a seed + authored constants), which is exactly the contract
`CellEnvironmentSpawnableBase` already enforces so clients agree without seed
sync. Anything driven by play-mode state (physics, timing, live gameplay) is
NOT this and still needs the editor.

**The method — and the step that makes it trustworthy:**

1. Port the generator to Python (or any host), mirroring emit ORDER exactly.
2. **Validate the port against a KNOWN-GOOD baseline before trusting it for a
   new one.** The shipped Caldera's authored `PhaseThresholds` encoded its
   baseline (thresholds = baseline + fixed deltas), so the port had a target to
   hit: it reproduced 31,194 prisms / 430,691 volume **to the unit**. Only then
   was it used to author new numbers. Without this step you have a plausible
   number, not a measured one — which is worse than no number.
3. Measure whatever the authored asset needs (count, volume, per-kind and
   per-domain splits, min/max radius, per-family breakdown for tuning).
4. Iterate design changes against the sim, then hand-sync to the C#.
5. **Cross-check the sync mechanically.** After hand-syncing, extract every
   numeric literal of a given class from BOTH files and compare as multisets
   (e.g. every `new Vector3(x,y,z)` / `Plate(x,y,z)` scale tuple). A silent
   divergence between generator and sim means wrong authored numbers with no
   symptom until someone re-measures in-engine.
6. **Render the point cloud** (matplotlib, three orthographic projections,
   coloured by domain). This catches geometry errors no assertion will — a pass
   that measured fine read as squat blobs rather than mountains until it was
   plotted, and the fix was an aspect-ratio change, not a count change.

Also: assert the spatial invariants the design claims. "Nothing inside the
nucleus control radius" is one line over the emitted points, and it caught that
the SHIPPED build had 89% of its mass in there.

**Trap: a validated SIMULATION does not validate the SHIPPED CONSTANTS — verify
the file, and never derive one row from another.** A measured table can be proven
end-to-end offline and still ship wrong, because between the measurement and the
asset there is a TRANSCRIPTION, and that step is invisible to both the sim and
code review. The gyroid octagon tables (2026-08-16) were validated as rotation
*matrices* — 273-plant colony, zero overlaps, bijective on the reference lattice —
then hand-carried into C# as *quaternions* with only the first of four block types
pasted from the emit; the other three were filled in by a plausible mirror-symmetry
ansatz. The conjugation does not act on `LookRotation` frames that way, so 12 of 16
seed rotations were wrong by up to 179°, and it cost five playtests, because each
plant was internally perfect and only the JOINS were wrong.

Two rules fall out, and they generalise to any generated-constant pipeline:

1. **Write a verifier that parses the SHIPPED artifact** (not the intermediate
   JSON, not the sim's own state) and re-proves it against a fresh reference walk —
   including the representation you converted into. Yes, this means re-implementing
   the engine's quaternion convention; that is the point, since the conversion is
   exactly what was never checked. `Tools/Build/verify_gyroid_octagon_tables.py` is
   the worked example, and it turned a five-playtest hunt into one command.
2. **Delete the transcription step.** Make the measuring tool emit the finished
   target-language block, ready to paste verbatim, and assert per-entry
   self-consistency as it prints (each emitted pose must reproduce the quantity it
   is supposed to encode). Emit exact measured samples, never an *average* of
   rotations — an averaged-then-orthonormalised matrix is where a det −1 reflection
   hides.

Symmetry shortcuts are the specific temptation: they look like insight, they halve
the work, and a chiral structure punishes them silently.

## 4.5b Technique: offline simulation of a FRAGMENT SHADER

Origin: the prism occlusion corridor (2026-08-04). §4.5 covers deterministic
*generators*; the same move works on a fragment shader, and it answers questions
the editor is slow at: what shape is this field, does this gradient read as an
edge, which of twelve dither kernels looks right.

**Port the SHIPPED HLSL, not an idealized version of it.** Every constant, every
early-out, every clamp — including the ugly ones (the strictly-inside-(0,1) nudge,
the epsilon in a divide). A sim of what you *meant* to write validates nothing. When
the port and the HLSL disagree later, that divergence is the finding.

The method:
1. Port the fade/threshold function to numpy, vectorized over a pixel grid.
2. Add a trivial ray-cast rasterizer (AABB slabs + a depth buffer, ~40 lines) so you
   can render the effect *in situ* — on a wall of boxes, at real hull scale — not
   just as an abstract ramp. In-situ and abstract disagree: a kernel measured at a
   fixed radius made corridor-relative patterns look perfect and screen-space ones
   look bad, exactly backwards from how they render.
3. Rasterize only inside each box's projected screen bbox. A full-image raycast per
   box is what turns a 20-second render into a 10-minute one that times out.
4. Render candidate sheets and **look at them** before implementing anything.
5. Re-render from the shipped code after implementing, and confirm it matches.

**Measure coverage fidelity for any dithered/screen-door effect.** The number that
decides whether a short gradient reads as a fade or as a hard edge is
|kept-fraction − alpha|, binned by alpha over a real render. Under ~0.01 reads
smooth; 0.1+ reads as banding. Measure it, don't eyeball it — of twelve kernels
tried, the three that survived were not the three that looked best on a ramp.

**A bad metric is usually fixable by remapping it through its own CDF.** Raw Worley
cell-distance clusters around 0.43 with nothing at the extremes, so it scored 0.140 —
unusable. Fitting a `smoothstep(a,b,·)` to its measured CDF took it to 0.0048, a 19×
improvement for ONE instruction, and because the remap is monotonic the pattern's
shape is completely unchanged: only the rate at which it fills in as alpha sweeps.
Two consequences worth internalising: any monotone threshold field can be made
coverage-correct this way, and **the fit is tied to the field's parameters** — change
the cell size, the jitter, or add animation, and the constants must be re-fitted or
the error silently returns. Verify the fit across the whole range you intend to use
(here: rate 0 through t=400s).

## 4.5b-geo Technique: prove GENERATED GEOMETRY against a closed form

Origin: the Dolphin's `BlastProfileGraphic` and `EchoSightHalo` (2026-08-17). §4.5b samples a
fragment function to judge a LOOK. This is its vertex-side sibling: when you generate a mesh, a
UI outline, or a screen-space size in code you cannot run, transcribe the *same arithmetic* into
Python and assert the properties the shape must have. It catches a class §4.5b cannot, because a
wrong outline does not look noisy — **it renders a plausible WRONG SHAPE**, and reviewing the code
that produced it tends to re-confirm the author's own mental model.

The assertions that actually earn their keep, in the order they catch things:

| Property | How to assert it | What it catches |
|---|---|---|
| **Simple, non-self-intersecting loop** | sign of `cross(p[i], p[i+1], p[i+2])` constant around the ring | an outline walked in the wrong ORDER — the killer, because a centre-fan over a mis-ordered loop draws a bowtie with hollow wedges and no error |
| **Area vs the exact formula** | shoelace vs the closed form (a stadium is `πR² + 4LR`) | a whole dimension dropped or doubled; polygonal under-approximation shows as a clean few-% deficit that shrinks with segment count |
| **Max step between consecutive vertices** | should equal a KNOWN edge of the shape | a jump across the interior — the direct signature of the ordering bug above |
| **Aspect / units** | for screen-space math, assert x and y offsets subtend EQUAL PIXELS | an ellipse where a circle was intended (NDC x and y both span −1..1 over unequal pixel counts) |
| **The regime table** | tabulate the output across the input range | a `max()`/`min()` crossover in the wrong place, or a floor that never engages |

A worked instance: the stadium outline swept both end caps from the ACROSS basis vector instead
of ALONG, which left cap one ending at the far tip and cap two starting near the middle. Convexity
and max-step both failed instantly; area was 2% under the closed form at 10 segments per cap after
the fix, which is exactly the expected polygonal deficit. The same script then tabulated the halo's
angular size against depth and confirmed the constant-size floor engaged where intended.

Cheap to write (~30 lines), and the table it prints is evidence you can paste straight into the
doc and the PR.

## 4.5c Technique: COMPILE the shipped HLSL with clang (stronger than porting it)

Origin: the occlusion corridor's triangle/shatter kernels (2026-08-06). §4.5b ports a
shader to numpy to judge its LOOK. This compiles the **actual file from the repo** and
runs it, which answers a different and harder question: *does the source I am about to
commit compile, and does it do what my measurements say?*

A numpy port validates the design. Only compiling the real file validates the FILE — a
port cannot catch a typo, an unbalanced brace, a wrong swizzle, or a `#if` that excludes
the wrong block.

```python
# Read the shader from the repo, apply a SHORT, LISTED set of mechanical substitutions,
# #include it from a C++ shim, compile with -Wall, run it, diff against the numpy port.
SUBS = [(r"\[unroll\]", ""),          # HLSL loop attribute
        (r"\bout float\b", "float&"), # HLSL out-param -> C++ reference
        (r"\bfloat2\(", "mk2(")]      # vector constructor spelling
```

- **Rewrite `out` params to C++ references in the EXTRACT, never with a `#define out`.**
  An empty `#define out` compiles clean and silently makes every out-param pass by VALUE,
  so the harness runs, prints, and reports `0.000` for every result — which reads as a
  logic bug in the shader and sends you debugging correct code. `out float3 X` →
  `float3 &X` as a substitution on the extracted text (the SUBS list above already has the
  scalar form; the vector forms need the same). A harness whose output is uniformly the
  zero value is a harness bug until proven otherwise.
- **`__attribute__((ext_vector_type(N)))` is the whole trick.** clang's vector types give
  you elementwise arithmetic and *arbitrary swizzles* (`.xyx`, `.yzx`, `.zy`) for free, so
  hash functions written for HLSL compile unmodified. Only the `floatN(a,b)` constructor
  spelling needs substituting.
- **Keep the substitution list short, listed, and auditable.** Every constant and every
  expression must pass through untouched — those are what you are verifying. If the list
  starts growing, you are rewriting the shader, not testing it.
- **Build with `-Wall` and treat every warning as a finding, because C++'s scalar
  overloads are not HLSL's.** `abs()` on a scalar float resolves to C's INTEGER `abs`
  unless you supply a float overload, so the harness silently truncates `abs(0.004)` to
  `0` and you go tuning a shader against a render that never ran your math. Same class:
  `std::min`/`std::max` are type-strict and reject `max(someFloat, 1e-8)` outright (that
  one at least fails loudly). Define HLSL-shaped `abs`/`min`/`max` in the shim, and
  `#undef`/restore them after the `#include` so the harness's own C++ still compiles.
- **Rasterize the REAL geometry, not a test surface.** §4.5b's advice to render "in situ"
  goes further for a shader bound to one authored model: parse the source mesh (trap
  above), run the actual bake the runtime will run, and feed the shipped entry point per
  fragment through a ~60-line triangle rasterizer (project, screen bbox, 2D barycentrics,
  1/w perspective correction, depth buffer). Then a claim like "the arcs only run along
  crease edges" is something you LOOK at, and a claim like "it is not blown out" is a
  census (mean linear output, % of covered pixels over 1.0/2.0/4.0) rather than an
  opinion. Re-render after any later edit and `cmp` the output: byte-identical proves the
  edit was surface-neutral, which is exactly what you want to assert about a refactor.
- **Stub the URP built-ins** (`_WorldSpaceCameraPos`, `_ScreenParams`, `_Time`,
  `UNITY_MATRIX_V`, `TransformWorldToHClip`) as file-scope globals in the shim. Then the
  entry point compiles too, not just the leaf functions.
- **Compile EVERY `#if` branch.** A gate like `#define X_LIVE_TUNING 1|0` has two shapes;
  build both and diff their output. That is how you prove a "design mode" is genuinely
  free when it is off, instead of asserting it.
- **Then assert the dials actually DRIVE.** Set the globals from the shim and check the
  output changes — over a POPULATION of pixels, not one. A single sample matching proves
  nothing (a flip that affects half the cells legitimately leaves any given pixel alone).

This also verifies a source-rewriting tool end to end: bake values with the tool's own
regexes, compile the result, and confirm the round-trip back to the original values is
byte-identical.

**Use the harness to MEASURE a bound the CPU then has to encode, instead of guessing it.**
Compiling for correctness is the obvious use; the higher-value one is deriving a number that
must live on the other side of the CPU/GPU boundary. A vertex-displacing effect needs a
`RenderBounds` envelope, and the padding is whatever the shader can actually displace — so
sweep the shipped entry point over the real geometry and every t in the animation window, and
report peak displacement as a RATIO of the quantity the CPU already has (`radius × amplitude`
measured at 0.991, so 1.25 ships with headroom). The constant then arrives with its
derivation attached, and re-running the harness after any shader edit re-checks it. Assert the
ratio in the harness, so a later change to the motion that widens the envelope fails there
rather than as prisms popping at the screen edge.

**Prove a shader REFACTOR behaviour-neutral by compiling BOTH revisions into one binary.**
Factoring a term out of a shipped function (2026-10: `ChargeCrystalDischarge` out of
`ChargeCrystalSurface`) is the change most likely to be "obviously the same" and least likely to be
checked. `git show HEAD:<file>.hlsl > old.hlsl`, translate old and new through the same `SUBS`, wrap
them in `namespace OLD { … }` / `namespace NEW { … }` (strip the include guard first, or the second
copy compiles to nothing), and `memcmp` the two entry points over a few hundred thousand random
inputs — include the degenerate ones (here: an all-zero barycentric, the unbaked fail-safe). Then
assert the extracted term equals the old function with the rest zeroed out, and negative-control
by perturbing one expression in `NEW` (it must report thousands of mismatches). Zero bitwise
mismatches is a stronger claim than "it compiles and looks right", and costs ten minutes.

**A NEW URP `.shader` can be front-end compiled with the repo's own glslang mock.**
`Tools/Shaders/verify_prism_slice.py` exports `URP_MOCK` and `glslang_compile(work, include,
program, stage, entry, defines)`; import it, append the few URP functions your pass calls that the
mock lacks (`TransformObjectToHClip`, `ComputeFogFactor`, `MixFogColor`, `#define half float`), copy
any `Assets/...` include into the work dir at the same relative path, and compile every
`HLSLPROGRAM` vertex+fragment with and without `INSTANCING_ON`. It proves the shader's OWN code
type-checks, nothing about URP; negative-control it (drop an argument from a call) before quoting.

**A hand-written URP `.shader` can be compiled by a REAL HLSL compiler — Microsoft's DXC — with
no substitutions at all.** The clang shim above rewrites the language (`out` params, constructors,
scalar overloads) and the glslang mock compiles a GLSL translation; DXC compiles the HLSL as written,
so `out` params, swizzles, `clip`, `Texture2DArray` and an `#include` of a shared `.hlsl` all behave
exactly as on device. The Linux release downloads and runs in this container (2026-10, wormhole
corridor change):
`curl -sSL -o dxc.tgz https://github.com/microsoft/DirectXShaderCompiler/releases/download/v1.8.2407/linux_dxc_2024_07_31.x86_64.tar.gz`,
untar into scratch, run as `LD_LIBRARY_PATH=<dir>/lib <dir>/bin/dxc -T ps_6_0 -E frag -HV 2018 -I <stubdir>
-I Assets/_Graphics/Materials/Graphs pass0.hlsl -Fo /dev/null` (the `bin/dxc` binary needs `lib/` on the
loader path or it fails to start). Build each pass's file as `HLSLINCLUDE` + that pass's `HLSLPROGRAM`
with the `#pragma` lines dropped (entry names come from `#pragma vertex/fragment`), and swap the URP
`Core.hlsl` include for a ~15-line stub: `CBUFFER_START/END`, `TEXTURE2D[_ARRAY]`, `SAMPLER`,
`SAMPLE_TEXTURE2D[_ARRAY]`, `_WorldSpaceCameraPos`, `_ScreenParams`, `_ProjectionParams`, `_Time`,
`UNITY_MATRIX_V/P`, `TransformObjectToWorld`, `TransformWorldToHClip`. Compile vertex AND fragment of
every pass (`vs_6_0`/`ps_6_0`), then negative-control it (delete one global the pass reads — DXC must
name it). Like the glslang route it proves the shader's own code and its includes, nothing about URP.

**Reuse an existing harness's shim for a DIFFERENT function in the same file — do not write a
second one.** `Tools/Shaders/verify_prism_shard3d.py` exposes `SHIM`, `translate()` and
`clang_cmd()` as module members, so a scratch script can `import verify_prism_shard3d as H`,
write its own three-line `extern "C"` ABI around any function in `PrismOcclusionCorridor.hlsl`
(the 2026-09-15 near-circle change did this for `PrismOcclusionFade_float`) and set a file-scope
global directly (`_PrismOcclusionNearRadius = n`) — no copy of the substitution list, so a fix to
the harness reaches both. Then diff the compiled function against a Python reference over random
samples; 40k samples at 2.8e-6 max deviation is a real proof, "I read the lerp" is not.

**A new per-frame scalar for a Custom Function node is cheaper as a FILE-SCOPE HLSL global than as
a new node input.** Widening `float3 Params` to `float4` costs a `.shadergraph` edit on every graph
the function is spliced into (two here), a property retype and a re-run of the wiring script; a
`float _Foo;` declared beside the file's other globals and driven by `Shader.SetGlobalFloat` costs
none of that, reaches every graph the file is included in, and reads 0 until published — which is
the old behaviour by construction. The corridor's `_PrismOcclusionNearRadius` and the Lab's dither
dials are the shape.

## 4.5c-r Technique: RASTERIZE the shipped shader — the rung above compiling it

§4.5c proves a shader *computes* what you think. It cannot tell you the effect is **invisible**,
and that is a different failure with the same symptom: everything passes and the human says it
still looks wrong.

Compiling the HLSL gets you a function. Wrap that function in a ~60-line rasterizer and you get
the **picture**: a perspective camera, a ray-primitive intersection per pixel, the shipped
fragment entry point evaluated at the hit, the pass's real blend mode accumulated over black, a
tonemap, and a PNG written with `zlib` + `struct` (no image library needed).

```
ray from camera -> intersect the primitive -> object-space pos/normal/viewdir
   -> call the SHIPPED fragment function -> accumulate per the pass's Blend
   -> tonemap -> PNG
```

Three things to get right or the render lies:

- **Match the pass's render state**, not just its maths. `Cull Back` means you take the NEAR
  intersection only; `Blend One One` means you sum, never lerp. A rasterizer that draws both
  hemispheres will show you an effect the player never sees.
- **Render at the game's PIXEL DENSITY, not at a comfortable panel size.** A 300 px panel at a
  60° FOV is 3.6× coarser than a 1080p frame, which flatters every fine feature. Pick the panel's
  FOV so it is a 1:1 crop: `fov = 2·atan(tan(fov_game/2)·panel_px / frame_px)`.
- **Render the real POPULATION, not one instance.** Place the objects where the game puts them, at
  the density the game produces (fire rate × flight time), each with its own per-instance data.

> **Trap: a per-instance metric is structurally blind to the sum over N instances.** A projectile
> shell measured 2.5% of one sphere lit, one planar curve, a quarter of the light of the effect it
> replaced — and a burst of 54 of them emitted **2.11×** that baseline, because the pass is
> additive and N is set by the fire rate. When an effect appears many times at once, total the
> LINEAR emission over a rendered frame and treat THAT as the tuning surface. Per-instance numbers
> answer a question nobody asked.

> **Trap: a metric can only decorrelate a difference the signal actually carries.** Three separate
> passes derived per-instance identity from geometry (a radius, then a lateral position, then that
> position spinning a plane), each measured well-decorrelated, each still read as identical on
> screen — because the underlying quantities differ by ~0.02 units between instances. Before
> tuning a decorrelation, print the raw spread of the signal across the instances you need to tell
> apart. If it is near zero, no function of it will help; you need real per-instance data.

> **Trap: `floor(x)` keying.** When an effect picks its identity with `floor(x)` and its timing
> with `frac(x)`, a sub-unit offset in `x` changes WHEN, never WHICH. Two instances a fifth of a
> cycle apart are the SAME choice at two stages. Decorrelating "which" means reaching the argument
> of the `floor`, not adding to `x`.

> **Trap: a ceiling the rejected version would pass is not a gate.** When a tuning budget comes out
> of a playtest rejection, set the threshold tight enough to FAIL the value that was rejected —
> even if that makes it an odd number. A round-number ceiling picked for tidiness is how the
> regression walks straight back in.

Worked example: `Tools/Shaders/render_projectile_charge_field.py` (isolated instances and the live
stream, both revisions side by side) with `verify_projectile_charge_field.py` owning the numeric
gates including the frame's light budget.

## 4.5d Technique: offline simulation of a CONTROL LOOP (steering, pursuit, any feedback law)

§4.5 simulates a *generator* — deterministic, one pass, compare the output. A **control
loop** is the harder cousin: the thing you are changing feeds back into its own input, so a
change that looks locally sensible can be globally worse and neither review nor a unit test
will say so. Simulate it the same way, and let the simulation pick the tuning.

The shape:

1. **Extract the loop's pure math into a static class with no `UnityEngine` in it**
   (`PursuitReachability`: turning radius, the reachability test, the escape direction, the
   orbit detector). That class is what the shipped `MonoBehaviour` calls AND what the harness
   calls, so the tested path is the shipped path. This is the same discipline as §4.7's
   "extract the shipped block" — here it also buys the design a seam.
2. **Write a plant** — a few dozen lines integrating heading and position under a max turn
   rate. It does not need to be the engine's integrator; it needs the same *constraint*
   (bounded turn rate, constant-ish speed), because that constraint is what the law is
   fighting.
3. **Score over a randomized ensemble, not a scenario.** Hundreds of (start pose, objective)
   draws, fixed seed. Report reached/total and mean time-to-objective. One hand-picked
   scenario proves nothing about a feedback law.
4. **Measure candidate fixes against each other before shipping one.** Two plausible
   remedies for a late swerve were each simulated and REJECTED on the numbers (a commit-range
   gate traded swerves for orbits, 400→377 reached; a look-ahead factor multiplied break-offs
   28→247 at double the mean time) — which is what forced the search to continue until the
   real cause turned up. Rejecting on data is cheaper than shipping and re-playtesting.
5. **Sweep every dial you are about to author and report the curve, not the pick.** The
   away-bias in this session turned out to be a *dead dial* — 0…1.5 all reached 400/400
   within 0.06 s — so it shipped at its midpoint with that fact recorded, instead of being
   defended as a tuned value.

Two things the harness catches that reading cannot: a **ratcheting accumulator** (a
running-minimum "best distance so far" silently degrades a progress gate to "constant range
only"; visible instantly as a detector that never fires on an approach), and a **wrong
comparison of derived quantities** — see the squared-vs-linear trap in §5.

**Two more, both learned shipping a mouse-flight control law (2026-08-26), and both invisible to
the obvious tests:**

- **A control curve is a claim about the STEADY STATE under continuous input, so a test that only
  pokes the law with an impulse is structurally blind to it.** The first cut of a mouse→stick
  integrator sprang back to centre only on frames where the mouse was STILL. Every plausible
  assertion passed — it integrates, it clamps, it returns to zero, it is frame-rate independent —
  and it was unflyable, because the spring was off *whenever the player was actually steering*, so
  any drag at all wound up pinned at full deflection and no stable partial turn existed anywhere.
  The test that finds it is one line and nothing like the others: hold a constant input for
  several time constants and assert the settled output, at several input magnitudes. Write the
  closed form too (`v·k/spring`) and assert the integrator MATCHES it — then the number a tuner
  reasons about is the number the code produces, which is a second bug class closed for free.
- **A dead zone applied to an integrator's STATE is a RATCHET, not a filter.** Snapping the
  accumulator to zero below a threshold means any input whose per-frame contribution is smaller
  than the threshold is erased every frame and can never accumulate — so slow, careful input does
  literally nothing, and the speed needed to escape scales with FRAME RATE. Measured here: at
  60 fps the law ignored every drag under ~110 px/s, which is precisely the aiming range. The
  state must stay honest; apply the dead zone to the reported OUTPUT. Same shape as the
  running-minimum ratchet above — an accumulator that is allowed to forget cannot integrate.

**A third, learned re-tuning that same mouse-flight law (2026-08-27) — the exact mirror of the
first, and made by someone who had just written the first down:**

- **A control curve is a claim about the steady state; a FLICK is a claim about the transient, and
  a player judges responsiveness by the transient.** Adding a hold band to the integrator came with
  a re-tune (gain 0.011 → 0.0045, spring 3.5 → 1.5) justified by the SUSTAINED curve, which was
  near enough identical (318 vs 333 px/s for full deflection). The impulse response was never
  measured, and it fell by four times: a 100 px / 0.15 s flick went from 0.86 deflection to 0.40 —
  67 °/s to 17 on the vessel — and the scheme was reported as *not working at all*. **Measure both
  before either number moves.** A flick harness is four lines (spread N pixels over M frames, then
  stop) and it is the assertion that would have caught it: `Flick(100px, 0.15s).magnitude > 0.75`.
- **Technique — when a change is meant to be ADDITIVE, diff it against the implementation it
  replaced, under a realistic input stream.** Compile the PRE-BRANCH version of the pure class
  alongside the shipped one (`git show <merge-base>:<path>`, paste it into the harness as
  `OrigStep`), drive both from the same pseudo-random "hand" (bursts of movement, pauses, occasional
  hard sweeps, jittered frame times), and report **worst divergence and the state at which it first
  diverges**. "It only affects the top of the range" is a claim, and the range is where the player
  spends their time: this reported worst divergence 0.9995 of a stick unit first appearing at frame
  37 of 20,000 — the two were simply different controls — and it is what found the real defect after
  two wrong hypotheses. After the fix the same harness reported 0.035, all of it inside the band the
  feature owns, which is the shape a genuinely additive change has.

Limits, state them: the plant is not the engine, so the simulation bounds *behaviour of the
law*, never feel. Frame timing, replication, and the vessel's real thrust/grip model are out
of scope, and the human still playtests.

## 4.5e Technique: PHOTOGRAPH a generator — compile + run the shipped arena, rasterize what it lays

When the deliverable is a PICTURE of something the game builds (a card background, a thumbnail, a
proof-of-look), do not ask for a screenshot: compile the SHIPPED generator in a Roslyn harness,
run it with every field read off its prefab, and rasterize the lay list. Worked example and
reusable machinery: `Tools/Build/card_art_harness/` driven by `render_card_backgrounds.py` (the
`/cardart` skill) - 12 generators and 5 course files compiled unmodified, ~1 s per picture,
byte-deterministic so a `--check` re-renders and compares. Four rules it paid for:

- **Two kinds of shim, held to two standards.** GEOMETRY (Vector3/Quaternion/Mathf) must be
  FAITHFUL - the Cleave harness's identity `LookRotation` is fine for COUNTING and photographs an
  axis-aligned world. ENGINE-OBJECT stand-ins on the lay path (`PrismTrailBuilder`, `Instantiate`,
  `SpawnPrismTrail`) must be LOUD: log an error, and the harness refuses to write the picture.
- **Prove fidelity by COUNT against a number someone else wrote down.** The Swell's 14,277, the
  Switchyard's 3,978 and the concentric shells' 24,966 all reproduce to the prism; a count that
  disagrees is a shim bug.
- **A generator that lays in `Spawn()` gets read through its PREVIEW API** (`GetPreviewBlocks`),
  never by stubbing enough of the lay path for it to run.
- **Serialized data has two shapes a line parser gets wrong**: a primitive array is ONE hex blob
  (keep the raw token; decode by the C# field's type), and a value that looks like it lives on a
  transform may be a component FIELD (`CapsuleMembrane.radius`, not the membrane's scale).

