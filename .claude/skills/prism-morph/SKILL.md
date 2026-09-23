---
name: prism-morph
description: Use for ANY Cosmic Shore effect that DEFORMS prism geometry to live gameplay data — a liquid/silky/fabric-like morph of the prism surface itself (drape, wrap, ripple, bulge, pucker, melt), built on the seamless high-poly mesh swap plus a GPU vertex map driven by a per-frame global-uniform bank. Loads the four-part recipe (mesh, residency, bank, map), the clock-material-law argument that makes it legal, the ownership and invisible-swap contracts, the analytic-normal rules, the wirer + clang++ proof-by-convergence harness, and the collider/triangle budget gate. Trigger when editing Assets/_Graphics/Materials/Graphs/PrismCradle.hlsl or any sibling vertex-deformation HLSL, Assets/_Scripts/Utility/PrismCradle.cs, Assets/_Scripts/Utility/HighPolyPrismMesh.cs, Prism.SetRenderMeshOverride / RenderMeshOverride, Tools/Shaders/wire_prism_*.py or verify_prism_*.py, or Docs/PRISM_ANIMATION.md §4.7.
---

# High-Poly Prism Morph Protocol

You are building a member of the **morph family** — an effect in which the *surface of a prism
bends* in response to something happening in the world right now: mass draping over a hull,
puckering toward a mouth, swelling before a blast, rippling behind a ship. The family exists
because of one fact and survives because of three:

> **A deformation is only as smooth as the surface it moves, and a prism has 24 triangles.**
> So the handful of prisms an effect can actually reach are swapped to a high-poly copy of the
> *identical solid*, and the deformation is a smooth field evaluated entirely on the GPU.

That is the whole idea. Everything below is how to land it without re-paying the costs the
first member already paid.

**The reference implementation is the Urchin's CRADLE.** Read it before you write anything:
`Assets/_Graphics/Materials/Graphs/PrismCradle.hlsl`, `Assets/_Scripts/Utility/PrismCradle.cs`,
`Assets/_Scripts/Utility/HighPolyPrismMesh.cs`, `Docs/PRISM_ANIMATION.md` §4.7.2. Its history
is the reason this skill is worth its weight: **two rounds of rigid per-facet motion were
rejected on look** (a whole face, then a per-wedge cut), a 10× tone-down made a small glitch
instead of a big one, and the branch went stale. The redesign — same spirit, more surface,
smoother map — playtested as *"easily one of the greatest things we have ever made."* The
difference was not tuning.

---

## 1. Read the canon first

- `Docs/PRISM_ANIMATION.md` — **the clock-material law (LOCKED, STRICT)**. §4.7 is the one
  sanctioned shape for a prism visual that depends on live gameplay data; §4.7.2 is the cradle.
  §5 is the migration tracker (your effect gets a row).
- `CLAUDE.md ▸ Prism lifecycle / Prism performance / the §4.7 index line` — the invariants and
  the anti-pattern list (especially *"Swapping a prism's MeshFilter mesh directly renders
  NOTHING"*).
- `Docs/SPATIAL_INDEX.md` — `PrismSpatialIndex` is THE spatial index of prism mass. Your
  residency query goes through it and nowhere else.
- `.claude/skills/asset-surgery/SKILL.md` — the out-of-editor ShaderGraph JSON and clang++
  harness protocols this skill depends on.

---

## 2. The legality argument — state it before you build

The clock-material law says **no multiframe CPU update may animate a prism**. A morph appears
to violate it twice (it swaps a mesh, and it moves every frame). It does not, and you must be
able to say why in two lines, because a reviewer will ask:

1. **The mesh swap is a STATE CHANGE, not an animation.** It is final at the instant it is
   applied, exactly like a shield engaging. Nothing interpolates, nothing ticks, and the swap
   is performed where it is provably invisible (§4.2).
2. **The animation is `f(global uniform)` with ZERO per-prism CPU per frame.** O(1) writes per
   frame (`Shader.SetGlobalVectorArray` ×2, `SetGlobalVector` ×1) that every prism reads. This
   is §4.7's shape, the same one the occlusion corridor and the Dolphin's Echo Sight use — the
   cradle is simply the first citizen that moves **vertices** rather than coverage or colour.

And the third claim, which is what makes it *affordable*:

3. **Residency is bounded.** A handful of prisms carry the dense mesh at any instant, chosen
   nearest-first inside a radius around a small number of sources. The cost is `O(sources)`,
   never `O(prisms)`.

**If your effect cannot make all three claims, it is not a member of this family.** See §3.

---

## 3. Admission test — five questions, before any code

Answer all five in the branch description. A "no" is not necessarily fatal, but it changes
what you should be building.

1. **Does it depend on data the GPU could not have known when the prism was stamped?**
   *(e.g. "where is the hull relative to this prism", which changes every frame as the ship
   slides.)*
   **No** → it is a §1 **STAMP** (one-shot initial conditions + the shader clock), not a §4.7
   global. Build that instead; it is cheaper and it batches unconditionally.

2. **Is it bounded to a handful of prisms around a small number of sources?**
   **No** → you cannot afford the mesh. An effect that reaches every prism in the arena must
   stay a **shading** change (coverage, colour) on the prism's own 24-triangle box — that is
   what the corridor and the Echo Sight are. Never widen residency to make a global-reach
   effect work; lower the reach or change the effect.

3. **Is the intended motion SMOOTH over the surface?**
   **No** → the high-poly mesh buys you nothing and the effect will read as facets hinging.
   This is exactly what the cradle's first two rounds were, and no amount of tuning fixed it.
   A per-facet, per-wedge or otherwise piecewise-rigid design does not belong in this family.

4. **Can you write the map in closed form with an analytic derivative?**
   **No** → you cannot light it. A deformed surface with the *undeformed* normal reads as a
   flat sticker sliding over geometry. Redesign the map until you can differentiate it (§5).

5. **Does any GAMEPLAY state change?**
   **Yes** → stop. **A morph is photons only.** The collider, the `PrismSpatialIndex` entry,
   the volume, the domain and every state flag are untouched, and the prism is exactly where
   it says it is for every gameplay query. Mass that *looks* like it moved and did not is a
   deliberate, stated cost of the family (a hull can visually sink into a prism it is not
   colliding with); mass that actually moves is a different feature and a different
   conversation.

---

## 4. The four parts, in build order

### 4.1 The MESH — `HighPolyPrismMesh`

`HighPolyPrismMesh.Get(subdivision)` returns the **shared, cached, identical solid** at higher
density: a unit cube of `HalfExtent 0.5` with per-face grids (hard edges), `subdivision ∈
[2, 32]`, `6 · 2 · s²` triangles. At the shipped `s = 16` that is 3,072 triangles against the
prism's 24.

- **It is SHARED. Never mutate what `Get()` returns.** A write changes it for every prism in
  the game, forever, and there is nothing to report it.
- **Shared is also why it batches.** Every resident prism draws the same mesh, so the residents
  stay in ONE instanced batch. A per-prism unique mesh would mint a `BatchMeshID` each and is
  the thing `CLAUDE.md` already forbids.
- **The attribute contract mirrors the shield generators** — UV0 face-local, TEXCOORD1 the face
  centroid (constant per face), tangents from the face basis, `IndexFormat.UInt32` above 65,000
  verts, `RecalculateBounds()` but never `RecalculateNormals()` (hard per-face normals are
  authored, and recalculating would smooth the cube's edges into a ball).
- **Identity, not shape**: `HighPolyPrismMesh.IsHighPoly(mesh)` answers "is this one of mine",
  by cache identity. Never test geometry to decide ownership.
- A new family member should **reuse this mesh**. If it genuinely needs different topology,
  generate it in the same file under the same attribute contract so both remain one batch per
  subdivision — and say in the PR why the existing one would not do.

### 4.2 The RESIDENCY — a state change, performed where it cannot be seen

The residency pass runs once per frame beside the bank flush and does something categorically
different from it: it changes prism STATE. Five rules, each of which has a failure mode:

- **Swap through `Prism.SetRenderMeshOverride(shared)` / `ClearRenderMeshOverride()`.**
  Prisms draw through an instanced companion entity (`PrismRenderService`); a bare `MeshFilter`
  write **renders nothing** — the companion keeps drawing the box while your mesh sits on a
  renderer that is not drawing. This is how the stellated super-shield first shipped invisible.
- **Ownership is a question, then an answer.** `Prism.RenderMeshOverride` is the question — it
  is deliberately a getter with no setter behind it. **Take the slot only if it is null; clear
  it only if it is still yours.** The slot has other legitimate claimants (a settled shield
  octahedron is the live one). A prism shielded mid-effect has legitimately lost the slot to
  the shield, and clearing there drops the octahedron and renders armour as a box.
- **The swap must be INVISIBLE.** Perform it strictly outside the volume the deformation can
  move anything: query radius = `sourceRadius + reach + residencyMargin`, with the margin > 0
  and asserted in an edit-mode test. If a swap can occur where a vertex is already displaced,
  the prism pops — and continuity of existence is platform-wide.
- **Nearest-first, then the budget.** `PrismSpatialIndex.QuerySphere` is **unordered**, so
  without a sort the dense mesh goes to whichever prisms the bucket walk happened to reach,
  which in a crowded trail is not the ones wrapped around the source. Sort by squared distance
  to the source, then take `maxResidentPrisms`.
- **Never `Physics.OverlapSphere`.** Prism colliders are disabled for the first 0.6 s after
  spawn, so physics queries are structurally blind to fresh prisms — exactly the prisms a
  fast-moving source is passing through. `PrismSpatialIndex` is the only spatial index of prism
  mass.

**Release on every exit, and know which exit you are in:**

| Exit | Prisms alive? | Do |
|---|---|---|
| Source stops / moves away | yes | evict from the resident set, clear the override if still yours |
| Driver `OnDisable` / teardown | yes | `ReleaseAllResidents()` — hand every prism its own mesh back |
| `RuntimeInitializeOnLoadMethod` reset (play-mode exit) | **no** | drop the bookkeeping only. **Do not touch the prisms** — they are destroyed, and touching one is the failure the guard exists to avoid |
| Prism returns to the pool | n/a | already handled — `Prism`'s own `OnDisable` clears the override. You inherit this; do not duplicate it |

Mutating a `HashSet` while walking it throws, so evictions are collected into a scratch list
first. All scratch collections are static and cleared, never allocated per frame.

### 4.3 The BANK — one global uniform, published once per frame

- **Declare the arrays at HLSL FILE SCOPE, outside every CBUFFER.** Shader Graph has no array
  property type, so this is the only way to have one — and it means the graph needs **one new
  node and no new properties**. `Shader.SetGlobalVectorArray` reaches file-scope globals with
  no property surgery. Inside a CBUFFER instead, SRP batching breaks.
- **A master sentinel carries the LIVE SLOT COUNT**, and an unpublished global reads zero —
  so **zero must mean "the loop does not execute"**. Write the loop so the cost of a match with
  no source in it is exactly one comparison.
- **Publish the OFF state at `BeforeSceneLoad`.** Shader globals survive play-mode exit in the
  editor; without this, a source left live when play stopped keeps deforming mass around a hull
  that no longer exists. The corridor and the Echo Sight install the same guard.
- **One writer.** A single hidden `DontDestroyOnLoad` driver flushes from `LateUpdate`. N
  per-source writers stomp each other; this is the un-ref-counted-global lesson the speed tunnel
  already records.
- **Frame-stamp the registry.** Sources publish into a dictionary keyed by instance id with the
  current frame; the flush sweeps stale entries. A despawned source can never burn in.
- **Ease strength in and out** at the source, not in the shader — attach and detach are events,
  and a morph that switches on at full weight pops.
- A bounded bank of N slots is still O(1) in prisms. Keep N small (the cradle ships 4) and pick
  ONE authority per vertex rather than summing (§5).

### 4.4 The MAP — the deformation itself

Object space in, object space out; the field lives in **world** space. Transform the position
by `M` and the normal by `Minv` transposed on the way in, apply the field, transform back.
Guard every matrix use for non-finite values — a prism mid-pool-return can present garbage.

The shipped cradle's whole map is one line:

```
s   = d - R                       // distance from the hull's SURFACE
f   = d - s·k(s)·w                // slide along the vertex's own radius
p'  = U + dir·f
```

with `d = |p - U|`, `dir = (p - U)/d`, `k` the falloff and `w` the eased strength. Aim for that
kind of brevity: **a map you can write in three lines is a map you can differentiate, prove and
explain.**

---

## 5. Design the map — the rules that decide whether it reads or glitches

These are not stylistic. Each one is a specific artefact somebody saw on screen.

- **C1 at BOTH ends.** A falloff that reaches zero with a non-zero derivative leaves a crease at
  the reach, and the *normal* crease is far more visible than the position one. Use
  `k = pow(1 - smoothstep(0,1,t), e)`, and **return `k` and `dk` from the same function** so the
  value and its derivative can never drift apart (`PrismCradleFalloff`). Displacement, first
  derivative and normal correction all vanish together at the reach, so there is no seam.

- **The normal is the map's DERIVATIVE, computed analytically. No shortcuts.**
  The tempting cheap version — lerp the normal toward `dir · sign(n·dir)` — was built and
  **rejected**: it pops where `n·dir` crosses zero, which is a visible line down the middle of
  every side face of the prism you are standing on. For a radial map the Jacobian is
  `diag(a, b, b)` in the radial/tangential frame with `a = f'(d)` and `b = f(d)/d`, so the
  inverse-transpose is:
  ```
  n' = normalize( dir·(n·dir)/a + (n - dir·(n·dir))/b )
  ```
  Clamp `a` and `b` away from zero with one named constant (`PRISM_CRADLE_MIN_RADIAL`) — and
  make that constant an `#ifndef` dial, because it doubles as the harness's negative control.

- **The map must never FOLD.** `f` strictly increasing in `d`, or two vertices at different
  radii swap order and the prism turns inside out. Prove it in the harness (§7), not by
  inspection.

- **When slots overlap, pick ONE authority — do not sum.** The cradle picks the dominant slot
  by `k·w`. Summing two radial maps does not produce a radial map, and the moment it stops
  being one, the analytic normal stops being the map's derivative and the surface lights wrong.

- **Guard the degenerate inputs explicitly**: zero-length normal, vertex at the source centre,
  zero reach, non-finite matrix. Each guard returns pass-through, which is always a legal frame.

- **Composition across two morph nodes is legal but is not free.** Normals compose correctly by
  the chain rule (each node applies its own inverse-transpose to the normal it received), and
  positions compose as fields applied in sequence. But **the no-fold guarantee is per-node and
  does not compose**, and each node costs its own slot loop. **Prefer adding a MAP KIND to the
  existing node over adding a second node.** If you do add one, re-prove no-fold on the
  composite and re-run every sibling wirer.

---

## 6. Wire it — the ShaderGraph splice

Follow `/asset-surgery`: parse the whole file, clone same-file donors so the schema is exact by
construction, rebuild in memory, assert every invariant, then write. Idempotent, `--check`,
exit 1 when not wired.

- **Coverage is a census you state and justify.** A live prism can render with **BlockGraph** or
  **ExplodingBlockGraph** (transparent live prisms rest on the latter), so both get the node.
  **SuctionGraph is excluded** from the cradle because it renders mass being *consumed*, which
  is not mass a hull rests on. Your effect's census may differ — say which graphs and why.
- **Splice LAST on the vertex chain**, so the morph operates on the position every earlier stage
  has already produced.
- **The two vertex BLOCKS are the anchors**, which is what lets one splice rule cover both
  graphs whatever feeds them today. Assert the actual feeders; never assume them.
- **Write the migration against slot DIRECTIONS, not against a table of known past signatures**
  (inputs ascending → Position, Normal; outputs ascending → OutPosition, OutNormal). It then
  runs in both directions and a future signature change needs no new case. It is also the
  resolver for a `.shadergraph` merge conflict: take one side whole, re-run every wirer, confirm
  each reports "already wired".
- **Sweep ORPHANS.** Unsplicing an old node can leave a feeder node (an object-space Tangent
  Vector, say) with zero edges. Remove any node the migration left unreferenced.

**⚠ The trap that costs an hour every time: sibling wirers pin slot INDICES.** Changing your
node's slot count renumbers ids inside the graph, and every other wirer that names a slot by
number — `wire_prism_flight_clock.py`, `wire_prism_jiggle_clock.py`,
`wire_prism_suction_clock.py` are the three that have had to move — starts silently looking at
the wrong socket. **After any signature change, run every `Tools/Shaders/wire_prism_*.py` and
`wire_*.py` with `--check` and confirm all of them pass.**

---

## 7. Prove it — the clang++ harness (HARD GATE)

Build `Tools/Shaders/verify_<effect>.py` on the `verify_prism_cradle.py` /
`verify_prism_sight_composition.py` shape: the **shipped HLSL is translated mechanically (HLSL →
C++ spelling only) and EXECUTED**. Nothing in the harness may be a re-implementation of the
shader — a transcription proves the transcription.

Properties the cradle harness proves, and the shape to copy:

1. **Identity with no live slot** — bit-identical pass-through.
2. **Identity beyond the reach** — bit-identical.
3. **The map's defining behaviour at full weight** (for the cradle: a vertex inside the hull
   lands exactly on its surface, along the outward radial).
4. **Monotonicity / no fold.**
5. **Purity of the displacement direction** — what makes the analytic normal derivable at all.
6. **The normal is the derivative** — see §7.1 below.
7. **No seam at the far edge** — sweep a vertex through `s = reach`; neither position nor normal
   jumps. The falloff is C1 at both ends by construction; this is what measures it.
8. **Partial weight** — the map is linear in the weight at fixed geometry, so an eased engage is
   a blend of the MAP and never a differently-shaped one.
9. **Slot authority** — with two sources live, exactly one of them drapes the vertex.
10. **The negative control** (below).

**Always carry a NEGATIVE CONTROL**, driven by a `-D` override of the file's own `#ifndef`
dial, and assert that it FAILS. A gate nobody has watched fail is a gate nobody should trust.

### 7.1 Prove a derivative by CONVERGENCE RATE, not by a tolerance

This is the single most reusable thing in the family.

> **Halving the test patch must QUARTER the error. A wrong Jacobian converges to a CONSTANT.**

A tolerance cannot tell a correct derivative from a wrong one at any single patch size, because
you can always pick a size where a wrong one passes and a right one fails. The cradle's harness
failed at 0.71 with an **exact** Jacobian, purely because a fixed object-space patch was ~15% of
the local curvature radius once the prism's `(3,1,6)` scale amplified it. Size the patch as a
FRACTION of the local scale and assert the rate:

```
0.02·d : 0.323   0.01·d : 0.103   0.005·d : 0.0262   0.0025·d : 0.00581     (quarters — PASS)
control: 0.761        0.751         0.746            0.744                  (plateaus — FIRES)
```

**And the meta-lesson: when a numeric test fails, ask whether the TEST is wrong before assuming
the code is.** Probe first. An area-ratio guard unchanged across six thresholds ruled out patch
degeneracy; an epsilon sweep showed clean O(ε²); only then was it clear the Jacobian was exact
and the test was measuring curvature.

---

## 8. Budget it (HARD GATE — state all three numbers in the PR)

| Cost | Formula | Cradle as shipped |
|---|---|---|
| Triangles | `residents × 6 × 2 × s²` | 24 × 3,072 = **73,728** |
| **Always-on colliders** | — | **ZERO** |
| Extra draw calls | — | **ZERO** (shared mesh, one batch) |

- **A mesh override swaps the render mesh and NEVER the collider** — the same rule a shield
  already obeys. If your effect would need a collider to change, it is not a morph (§3, Q5).
- Per-frame CPU: `SetGlobalVectorArray` ×2 + `SetGlobalVector` ×1 + one `QuerySphere` per live
  slot. Nothing scales with total prism count.
- The two dials that move the triangle bill are `subdivision` (quadratic) and
  `maxResidentPrisms` (linear). Both live in the config SO; neither is a literal in code.

---

## 9. Config SO, tests and the gates

**Config SO** in `Assets/Resources/`, resolved once and cached, with `InvalidateConfig()` for
the editor: reach, falloff exponent, max strength, subdivision, max residents, residency margin,
engage/release seconds — plus an `IsSane` property that both an edit-mode test and any validator
call, so there is exactly one predicate.

**Edit-mode tests** (`Assets/_Scripts/Tests/Editor/`), the cradle's set as the template:

- `Config_IsSaneWhenAuthored` — the shipped asset satisfies its own predicate.
- `Config_ResidencySwapIsInvisible` — margin > 0, budget in range, triangle ceiling respected.
- `HighPolyPrismMesh_IsTheSameSolidAtHigherDensity` — extents, vertex/triangle counts, hard
  normals on the six axis directions, `dot(vert, normal) == HalfExtent`, outward winding,
  shared-cache identity. *It is the same solid* is the claim the whole swap rests on; assert it.
- Slot-index and HLSL-signature assertions, so a signature change fails a test rather than
  silently breaking a sibling wirer.

**Before handing back, all of these must be clean:**

```
python3 Tools/Shaders/verify_<effect>.py              # + its negative control
for w in Tools/Shaders/wire_*.py; do python3 $w --check; done
python3 Tools/Build/check_conditional_compilation.py
python3 Tools/Build/check_console_logging.py
python3 Tools/Build/check_enum_member_references.py
python3 Tools/Build/check_switch_label_collisions.py
python3 Tools/Build/check_self_referential_locals.py
python3 Tools/Build/check_using_directives.py
```

…plus a Roslyn stub-harness compile of every changed C# file (`/asset-surgery` §4). Remember
what that compile does and does not prove: it is a **real type check** against the stubs you
transcribed, and it is blind to anything whose base type is not in the stub set.

---

## 10. Hand back verification — you cannot run Unity, and the numbers cannot tell you it looks good

State the scene, the hull or source, the exact thing to look at, and the knobs to tune. Then
state plainly that **you did not run it**.

**This family is judged on LOOK and only on look.** The cradle passed every measurement it had,
twice, while reading as a glitch on screen — the measurements were correct and the design was
wrong. Two things follow:

- Never describe a morph as working because a harness is green. Say what was proven (the map is
  the map, the normal is its derivative, nothing folds, nothing pops) and say that whether it
  *reads* is the playtest's answer.
- If the report comes back "this looks terrible", **do not reach for the tuning dials first.**
  Ask whether the motion is smooth enough to be a surface at all. A 10× tone-down of a hinging
  facet is a smaller hinging facet.

---

## 11. Docs you must update

| File | What |
|---|---|
| `Docs/PRISM_ANIMATION.md` | A new §4.7.x for the effect: what it is, why it is a global and not a stamp, the map, the residency contract, the budget — and the **§5 migration-tracker row** |
| The owning system doc | `R_VesselActions/<ABILITY>.md` for a vessel effect, the mode doc for a mode effect: what the pilot sees and why |
| `CLAUDE.md` | The owner's row (vessel table / mode paragraph) and the §4.7 index line — one sentence each, naming the law it obeys |

Write the **rejected** alternatives down. The cheap-normal shortcut and the per-facet motion are
both worth more as records than the code that replaced them, because both will be proposed again.

---

## 12. Commit

One coherent step per commit; conventional-commit message; develop on the feature branch (never
`bleeding-edge`); open a PR only when asked. Say in the commit body what was proven offline and
that nothing was run in the editor.

---

## 13. The family

The family is defined by four axes. A new member is a choice on each:

| Axis | Choices |
|---|---|
| **Source** — what publishes a slot | a vessel hull · a projectile · a blast front · a creature's mouth · a crystal · a placed structure |
| **Map** — what the field does | drape / wrap onto a surface · bulge or swell · travelling ripple · pucker toward a point · shear along a direction · sag |
| **Trigger** — the residency predicate | riding / attached · proximity · contact · a held ability · a state flag |
| **Budget** | subdivision × max residents, priced by §8 |

**Shipped:**

- **CRADLE** (Urchin, `Docs/PRISM_ANIMATION.md` §4.7.2) — *hull · drape · riding · 24 × s16*.
  Mass around a riding hull slides along its own radius onto the hull's sphere; inside closes
  over, outside rises to meet.
- **WAKE** (`Docs/PRISM_ANIMATION.md` §4.7.3) — *carrier · travelling ripple · at speed ·
  96 × s12*. A fast-moving CARRIER — the Sparrow's skyburst missile, the Scarab's ball — drags a
  ripple through the mass around its recent path. **Eight things it established that the next
  member inherits:**
  (a) **a member need not belong to a vessel at all, and this one was taken OFF the fleet.** It
  shipped on every vessel, bound where the platform laws bind, and the playtest verdict was
  *"awesome effect, but it will be overused as a wake on every vessel"* — so ask **who the effect
  is FOR and how OFTEN it happens** before reaching for a per-vessel gate. See (h), which is the
  same finding from the budget's side. (b) **Pick the frame from what
  the effect is ABOUT** — cylindrical about the path, not spherical about the hull, because the
  trail is laid on that line. (c) **Prefer a dimensionless STRAIN to a displacement**: scaling a
  coordinate makes its origin a fixed point, so the map is singularity-free and the no-fold bound
  becomes one number with no geometry in it (`A·(1 + max|t·K'(t)|) < 1`) that a retune of the
  reach or the wavelength cannot invalidate. (d) A map whose field TRAVELS has an off-diagonal
  **shear** in its Jacobian that a static map does not — it is the term a "close enough" normal
  omits, so make it the `#ifndef` dial and let the negative control prove it is load-bearing.
  (e) **Splice ORDER between two morphs is a real decision and nothing on screen reports it**:
  the wake runs BEFORE the cradle, because the cradle closes mass onto a hull resting on it and a
  wake applied after would re-open the hole. Assert both edges.
  (f) **A strain's fixed point is part of the design, so say where the mass you are aiming at
  actually IS.** The wake's header claimed it was most visible on the trail, and the trail is laid
  essentially along the axis — where displacement `r·E` is smallest. The Squirrel happens to lay
  **two** rails ±9.6 u out, so it works; a single-rail hull would have been in the dead zone, and
  the reach (`hullRadius × 3` as first authored) could put even those rails outside the falloff
  entirely. Measure the geometry the effect is aimed at before authoring a reach in hull radii.
  (g) **An ABSOLUTE window is a claim about the fleet's real numbers, so author it from
  measurements** (and from the CARRIER'S, once there is one — the shipped window is the ball's own
  `ballRestSpeed`). The wake's engage speed shipped at 150 u/s against a Squirrel that cruises at 54
  and tops out at 300, so on the hull the mode flies the effect never ran — and the playtest
  report was *"too subtle"*, because **a window that never opens is indistinguishable on screen
  from an effect that is too weak.** Which is why every member of this family should ship with a
  one-line-per-second verbose report on `CSLogChannel.PrismRuntime` naming its live slots, their
  derived geometry and the resident count, **including the idle case with its reason** — the
  family's failure modes all render as "nothing is happening" and nothing else separates them.
  (h) **THE RESIDENCY BUDGET IS SHARED, so granting a member to N things DIVIDES it rather than
  multiplying the cost** — and that is a different failure from a slow frame. Past a certain
  headcount every instance is back on the authored 24-triangle prism and the effect is silently
  gone from ALL of them at once, with nothing in a profile to show for it. So the answer to "who
  carries this?" is a design decision the budget also has a vote in, and "a few things that earn
  it" beats "everything, cheaply" for a family whose whole premise is that only a handful of
  prisms can be smooth at a time.

**Candidates — each needs design sign-off before it is built.** These are illustrative shapes
the machinery already supports, not an approved roster; do not build one because it is listed.
- **PUCKER** — *mouth · pucker · proximity*. The smooth cousin of consumption: the neighbouring
  prisms' surfaces draw toward a feeding creature's mouth for the moment before the prism goes.
  **Touches the ecology — route through `/ecology` as well as this skill**, and note that
  consumption is an active force that removes mass while a morph must not.
- **BLAST BULGE** — *blast front · swell · proximity*. Mass an explosion is about to take swells
  outward along the blast's own axis in the frame before it goes. Must not outlive its prism;
  the destruction is already a state change and owns the timing.
- **SHIELD SWELL** — *shielding prism · bulge · state flag*. The octahedron engage is already a
  morph of its own mesh; this family's contribution would be the NEIGHBOURS bulging as armour
  engages. Note the ownership interaction in §4.2 — a shielding prism is exactly the case where
  the override slot changes hands.

**Three family-wide rules to carry into any of them:**

1. **One node, many map kinds** — prefer extending the existing morph node with a map kind over
   splicing a second node into the vertex chain (§5, last bullet). The wake is the sanctioned
   exception and it shows the cost: a second node is a second slot loop, a second no-fold proof,
   an explicit order to assert, and — see 3 — a sibling-wirer break.
2. **The family shares the mesh, the residency contract and the bank shape.** If a member needs
   to break one of those, that is a fundamentals conversation (`CLAUDE.md ▸ Design Philosophy`),
   not a local exception.
3. **Identify a morph STRUCTURALLY, never by name.** `Tools/Shaders/prism_vertex_chain.py` is
   the shared definition — a Custom Function node with exactly the four correctly-directed
   Vector3 slots `Position`/`Normal`/`OutPosition`/`OutNormal` — and `walk_past_morphs` is how
   every non-morph wirer reaches the vertex blocks past however many morphs are in the chain.
   Adding the wake broke three sibling wirers that each walked past ONE hard-coded name; a name
   list needs one edit per wirer per morph forever, which is a defect amplifier rather than a
   fix. **A new member must use that walk and must not add its name anywhere.**
