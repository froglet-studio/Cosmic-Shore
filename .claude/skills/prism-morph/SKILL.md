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
   inside a radius around a small number of sources and ranked by **where the field actually
   lives** (§4.2). The cost is `O(sources)`, never `O(prisms)`.

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
- **Sort, then the budget — and the SORT KEY is a claim about where your field lives.**
  `PrismSpatialIndex.QuerySphere` is **unordered**, so without a sort the dense mesh goes to
  whichever prisms the bucket walk happened to reach. But the budget *truncates* the query
  volume, so the key decides WHICH PART of it gets the dense mesh, and there are two answers:
  a field that **decays with distance** from its source (the cradle) ranks nearest-to-source,
  because those prisms move most and will still be moving next frame; a field that lives in a
  **moving shell or front** ranks by distance to that front, `abs(|p - U| - c)`, because
  nearest-to-source there is exactly the mass the effect is LEAVING. **The two are identical in
  code and the wrong one is invisible until the support is small relative to the volume the
  budget truncates** — the retired ripple (§13) inherited the cradle's key, passed a playtest on
  a carrier whose reach was small (where the two agree), and then could not be seen AT ALL on one
  whose reach was 95 units. Precompute the key per candidate into a small struct rather than
  measuring it inside the comparator: one square root per candidate instead of two per comparison.
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
- If it comes back **"too fast and too subtle"**, a morph has exactly THREE budgets and they are
  not interchangeable: *how big* (amplitude and support), *how long* (the carrier's clock), and
  *how much of that time it spends at full size* (the strength envelope). Find out which ones are
  already spent before proposing one. The retired ripple (§13) had amplitude and shell thickness ON
  their structural ceilings and its LENGTH owned by a gameplay guarantee — the warhead's
  `ExplosionDuration` is capped by a shipped capture test, and the value being asked for had
  already been rejected once for that exact reason — so the envelope was the only budget left, and
  a plateau bought 3× the frames at full amplitude for free. **General rule: when an effect's
  length is owned by something other than the effect, the envelope's SHAPE is the budget** — and
  changing the peak is what a no-fold proof is stated against, while changing when the peak
  happens is not.
- And **check whose number it is before you spend it.** A duration on a gameplay prefab is a
  weapon's tuning, not a visual's; the honest move is to state what buying more would cost (in
  that case a bigger blast or a closer fuze, a buff or a nerf) and let the person decide, rather
  than failing a test somebody wrote a justification for.

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
  over, outside rises to meet. **It is the family's only member, and the only consumer of the
  high-poly swap, the residency contract and the global-uniform bank.**

**A neighbour, not a member: the Rhino sword's SLICE** (`Docs/PRISM_ANIMATION.md` §4.10). It is
the admission test's question 1 answered "no" and still wanting the dense mesh — every input (the
cut plane, the side, the blade speed) is known at the instant of the kill, so it is a §1 STAMP on
two pure render entities per prism, not a global, and it shares only `HighPolyPrismMesh` with this
family (no residency swap: the halves are BORN on the dense mesh, so there is nothing to hide). It
is worth reading before a new member for two findings that transfer: **a vertex map that folds one
part of a surface onto another is exact only up to the straddling row of triangles**, so the mesh
density IS the precision — and what is left is best closed in the fragment stage (clip on the
interpolated REST value, which is exact within a triangle, and let back faces shade as the surface
the clip opened onto); and **a central projection from an interior point is the one way to flatten
the far side of a convex solid onto a plane EXACTLY** (a bijection onto the true cross-section),
where the obvious orthogonal projection overhangs every face slanted to the plane. Its harness
(`verify_prism_slice.py`) adds a second proof tier this family lacks: glslang front-end-compiles
every pass of a hand-written `.shader`, plain and DOTS-instanced, against a URP mock.

**Built and NOT shipped — a retirement record.** One other member reached playtest and was removed
with everything that carried it. It is written down because the family inherits its findings, not
because any of it is in the tree: **no code, asset, graph node, config, tool or gate refers to it**,
so do not cite a file from it as evidence and do not try to revive it as a drop-in.

- **THE RIPPLE** (2026-09-23 → 2026-09-25) — *travelling shell · one whole cycle passing · four
  carriers, all rejected*. A thin shell of rippled prisms sweeping outward through the mass around a
  source. It was carried in turn by **every vessel**, the **Scarab's ball**, the Sparrow's heavy
  **skyburst round in flight**, and finally that round's **warhead blast** at detonation. Six
  playtests; the last two said *"i could not detect the shockwave at all"* and then *"i see it now.
  it is very fast and very subtle"*. It was cut with the branch that scoped down to the paradigm
  itself. **Eleven things it established that the next member inherits:**
  (a) **A member need not belong to a vessel, and this one was taken OFF the fleet and then off two
  more carriers.** It shipped on every vessel, bound where the platform laws bind, and the verdict
  was *"awesome effect, but it will be overused as a wake on every vessel"*: **an effect strong
  enough to be an EVENT stops being one the moment it is continuous, and no measurement will tell
  you.** The ball went the same way one step down (in play for a WHOLE MATCH), and the round in
  flight went on a playtest that APPROVED it — *"I could see the pulses as a wake for the travelling
  heavy prism"* — where the word was the verdict, because a front trailing a travelling object is a
  wake and a wake is a texture. So ask **who the sentence is FOR, and is it on all the time**,
  before reaching for a carrier. See (h), the same finding from the budget's side.
  (b) **Pick the frame from what the effect is ABOUT — the frame follows the FORCE.** It was
  cylindrical about a path while it was a wake and spherical about a point once it was a blast front,
  because a blast's force is radial and has a RADIUS the gameplay already authors. Changing what the
  effect IS changes the frame, and every frame-specific piece goes with it: an axis array, a
  Jacobian shear term, a second residency filter.
  (c) **An amplitude is either a STRAIN or a LENGTH, and which one is a claim about scale.** Scaling
  a coordinate (`r → r(1+E)`) makes the origin a fixed point, is singularity-free, and bounds the
  no-fold condition with one dimensionless number — but the displacement then GROWS with the
  coordinate, which was right at a 30 u reach and would have moved a 95 u reach's outer mass 40 u.
  A bounded offset (`r → r + σAwP`) keeps the fixed point *by construction of the support* — the
  shell never reaches the centre — and bounds the displacement instead. Pick by asking what the
  effect's own scale is.
  (d) **DERIVE the no-fold bound from the shape's own parameters rather than measuring a constant.**
  For a windowed sinusoid `max|P′| = 2πQ` exactly, so `A < 1/(2πQ)` and the config COMPUTED the
  clamp from the bandwidth — raise the bandwidth and the allowed amplitude falls on its own, where a
  literal clamp silently becomes wrong. And look for a SECOND bound you can make FOLLOW rather than
  assert: there `b > 0` followed from `a > 0` plus the publisher's guarantee that the shell is born
  at `c = σ`, which made that birth radius half the proof rather than a taste decision.
  (e) **The Jacobian's shape follows the frame.** A purely radial map has a diagonal differential
  and therefore no shear; a map whose field travels along an axis while displacing along a radius
  has an off-diagonal term. Whichever term carries the derivative of the *interesting* factor is
  the one a "close enough" normal omits — make THAT the `#ifndef` dial and let the negative control
  prove it is load-bearing.
  (f) **Splice ORDER between two morphs is a real decision and nothing on screen reports it**: it
  ran BEFORE the cradle, because the cradle closes mass onto a hull resting on it and a ripple
  applied after would re-open the hole. Assert both edges.
  (g) **An ABSOLUTE window is a claim about the fleet's real numbers, and the right answer may be to
  have no window at all.** Its engage speed shipped at 150 u/s against a Squirrel that cruises at
  54, so on the hull the mode flies it never ran — and the report was *"too subtle"*, because **a
  window that never opens is indistinguishable on screen from an effect that is too weak.**
  Re-authoring it from measured speeds was a fix; deleting it was the real one, because a blast's
  criterion was never a speed. Every member should still ship a one-line-per-second verbose report
  on `CSLogChannel.PrismRuntime` naming its live slots, their derived geometry and the resident
  count, **including the idle case with its reason** — the family's failure modes all render as
  "nothing is happening" and nothing else separates them.
  (h) **THE RESIDENCY BUDGET IS SHARED, so granting a member to N things DIVIDES it rather than
  multiplying the cost** — and that is a different failure from a slow frame. Past a headcount every
  instance is back on the authored 24-triangle prism and the effect is silently gone from ALL of
  them at once, with nothing in a profile to show for it. "A few things that earn it" beats
  "everything, cheaply" for a family whose whole premise is that only a handful of prisms can be
  smooth at a time.
  (i) **Find the GAMEPLAY QUANTITY that already discriminates, rather than authoring a flag.** The
  Sparrow's base and heavy rockets are ONE prefab and ONE pool told apart by a per-shot payload, so
  the first cut's `leavesWake` prefab bool could not tell them apart at all — while the heavy's
  warhead multiplier is already zero on the base rocket and on every non-skyburst prefab. Gating on
  the weapon cost no authored field and could not drift.
  (j) **A member whose strength passes through zero must not have its slot dropped there**, and the
  publisher is what has to know: at both ends of that ripple's life the strength was zero in value
  AND slope, and a strength-based slot drop would have released and re-acquired every high-poly
  override mid-effect — the exact pop §4.2 exists to prevent. The SOURCE owns the decision to stop.
  And **residency is the effect's REACH, not its current support**: a prism the front has not
  arrived at yet must already be carrying the dense mesh when it does. Note the margin that covers
  the overshoot has to be written in the SAME units as the overshoot — an absolute margin against a
  support that is a FRACTION of the reach agreed by 0.2 of a unit at one tuning and broke by 19 at
  the next.
  (k) **Residency has a COUNT and a SUBDIVISION and the GPU only prices their product.** 160 prisms
  at `s9` (155,520 triangles) is *dearer by 1.25%* than 128 at `s10` (153,600) and puts a quarter
  more prisms in motion, which is what "too subtle" actually wanted. So the budget can be RE-SPENT
  rather than raised — but the count needs its own ceiling and the subdivision its own floor, or the
  triangle bound alone admits a thousand prisms at `s2`, i.e. the authored prism with none of the
  smoothness the family exists for.

**Candidates — each needs design sign-off before it is built.** These are illustrative shapes
the machinery already supports, not an approved roster; do not build one because it is listed.
- **TRAIL RIPPLE (1D)** — *a point on a trail · travelling ripple · BOTH WAYS along the chain*.
  **Saved by request (2026-09-25), not approved and not built.** A ripple that runs along a
  ONE-DIMENSIONAL prismscape — a trail — from a point on it, in **both directions**, to carry an
  effect or an interaction to a **new location further along that trail**. It is the family's first
  member whose field is ORDERED rather than spatial, and that changes three of the four parts:
  * **Source** is a point ON a `Trail` (`PrismscapeDimension.Trail`, resolved by
    `PrismscapeTopology.DimensionOf`), so a slot carries a trail identity and an index or arc
    length along it, not a world sphere. Two fronts per slot, one each way; a chain END is where
    a front stops, which is also what makes a loop behave differently from an open ribbon — the
    Urchin's launch already turns on exactly that distinction (`URCHIN_TRAIL_RIDER.md`).
  * **Residency** is a WALK along the trail's own ordering, not a `QuerySphere`. That is cheaper
    and more exact than the spatial index for this shape — a trail already knows its neighbours —
    and the §4.2 sort-key rule applies unchanged in its 1D form: the field lives in a travelling
    front, so residency ranks by distance to the FRONT along the chain, never by distance to the
    source. The budget then truncates the two fronts, not the middle.
  * **Map** is a displacement transverse to the ribbon as a function of arc-length offset from a
    front — the same windowed pulse shape, with `s` measured along the chain instead of radially,
    so (c), (d) and (e) above transfer directly and the Jacobian is again diagonal in the
    ribbon's own frame.
  **And it is the family's first member that proposes to DELIVER something**, which is the part
  that needs the design conversation rather than the engineering one: a morph is photons only
  (§3, question 5), so *"sends an effect or interaction to a new location"* means some OTHER
  system acts when the front arrives, and that system's authority, replication and timing are its
  own problem — the ripple can be the CLOCK a peer watches, never the thing that resolves the
  outcome, because a morph's geometry is owner-local and tick-gated (`Docs/LIT.md` makes the same
  ruling about lit volumes). Decide what arrives, and who says so, before drawing anything.
- **PUCKER** — *mouth · pucker · proximity*. The smooth cousin of consumption: the neighbouring
  prisms' surfaces draw toward a feeding creature's mouth for the moment before the prism goes.
  **Touches the ecology — route through `/ecology` as well as this skill**, and note that
  consumption is an active force that removes mass while a morph must not.
- **BLAST BULGE** — *blast front · swell · proximity*. Mass an explosion is about to take swells
  outward along the blast's own axis in the frame before it goes. Must not outlive its prism;
  the destruction is already a state change and owns the timing. The retired ripple above was
  effectively this shape built about a warhead, so read its eleven findings first — and note it
  would be a map KIND on the existing node rather than a second node (rule 1 below).
- **SHIELD SWELL** — *shielding prism · bulge · state flag*. The octahedron engage is already a
  morph of its own mesh; this family's contribution would be the NEIGHBOURS bulging as armour
  engages. Note the ownership interaction in §4.2 — a shielding prism is exactly the case where
  the override slot changes hands.

**Three family-wide rules to carry into any of them:**

1. **One node, many map kinds** — prefer extending the existing morph node with a map kind over
   splicing a second node into the vertex chain (§5, last bullet). The retired ripple is the one
   member that ever took the exception, and it showed the cost: a second node is a second slot
   loop, a second no-fold proof, an explicit order to assert, and — see 3 — a sibling-wirer break.
2. **The family shares the mesh, the residency contract and the bank shape.** If a member needs
   to break one of those, that is a fundamentals conversation (`CLAUDE.md ▸ Design Philosophy`),
   not a local exception.
3. **Identify a morph STRUCTURALLY, never by name.** `Tools/Shaders/prism_vertex_chain.py` is
   the shared definition — a Custom Function node with exactly the four correctly-directed
   Vector3 slots `Position`/`Normal`/`OutPosition`/`OutNormal` — and `walk_past_morphs` is how
   every non-morph wirer reaches the vertex blocks past however many morphs are in the chain.
   Adding a second morph broke three sibling wirers that each walked past ONE hard-coded name; a
   name list needs one edit per wirer per morph forever, which is a defect amplifier rather than a
   fix. **A new member must use that walk and must not add its name anywhere.**
