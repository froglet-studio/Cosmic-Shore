# Omni Shepard Lab

A browser lab for designing the omni crystal's **Shepard triangles**: the shells of triangle plates that
stream in toward the crystal and land on its body, forever. Sliders for how many shells are in
flight, how many triangles each carries, where they launch, where they land, how fast they travel,
and how they are coloured. Round 3 added 18 alternative styles, a random style generator, a
compare grid and shared likes. A trip chart, a scorecard and a "what it takes to ship" export go with it.

- **Page (source):** `Docs/Studios/OmniShepardLab.html` (open it in Chrome, or use the live page)
- **Live page:** https://claude.ai/artifact/NqWi3Ej4vxU3poSjp2HqG9 (private until shared from its Share menu; decisions recorded there land in its `decisions` log)
- **Shipped numbers + meshes:** baked in by `Tools/Build/omni_shepard_lab_assets.py`
  (`--check` fails when the assets move and the page has not been rebaked; `--self-test`)
- **Verify:** `node .claude/skills/labmaker/verify_lab.cjs Docs/Studios/OmniShepardLab.html`
- **Round-to-round parity:** `Tools/Build/omni_shepard_lab_parity.cjs <old.html> <new.html> [--old-set <recipe>] [--old-style id] [--new-style id]`
- **Shader vs lab:** `Tools/Build/omni_shepard_shader_parity.py` (compiles the shipped HLSL with clang)
- **Space squares (round 6):** `blender -b CrystalsRaw_V2.blend --python Tools/Build/bake_omni_space_squares.py -- Docs/Studios/data/omni_space_squares.json` (the `.blend` is the art team's, not in the repo); the extractor then maps it onto the body FBX
- **What ships (since round 8):** the "Depth ratchet", see Round 8 below and `Docs/PALETTE.md` §2.10 ("Breathe" shipped rounds 4–7); since round 9 the crystal holds still and the body plays the Space squares; since round 10 every crystal takes its own random pose (seeded from its spawn position) and reseats at −0.87
- **Ratchet + pose, headless:** `bash Tools/Build/crystal_depth_harness/run.sh`
- **Shader vs lab (depth prisms):** `Tools/Build/omni_depth_prism_shader_parity.py` (since 2026-10-13 also the hull-fusion identity: undriven, any moved vertex draws where fusion put it)
- **Field scorecard (round 10):** `window.__lab.runFieldBatch()` / `fieldScore(p)` / `fieldMetricsOf(p, kind, dist, subject)`

## How the effect works (measured from the assets)

`Crystal.prefab` carries three `OmniCrystalTriangles` instances, each with its own
`OmniShepardTriangles {0,1,2}.mat` on `OmniShepardFresnelShader`. The shader scales the whole
20-plate triangle mesh about the crystal centre by `s`, where `s` sweeps `_Start → _Stop` once per
`_Period`, and draws it with `alpha = (1.05 − s) × _Opacity`.

| Layer | `_Start` | `_Stop` | `_Period` |
|---|---|---|---|
| OmniShepardTriangles 0 | 1.000 | 0.833 | 3 s |
| OmniShepardTriangles 1 | 0.833 | 0.667 | 3 s |
| OmniShepardTriangles 2 | 0.667 | 0.500 | 3 s |

The three bands are contiguous and share one clock, so when the period wraps, each layer jumps to
exactly where the layer outside it just was. One triangle seems to fly all the way in over
3 × 3 s = 9 s (the thick "baton" line on the trip chart). The one jump that would show, the innermost
layer vanishing at alpha 0.55, happens at `_Stop` 0.5, which puts every plate 0.17 u inside the body
face behind it (plate centroid 1.562 local × 0.892 × 0.5 × 10 = 6.97 u; body face 7.14 u), so the
opaque body hides it.

A fourth shell, `OmniShepardTrianglesRim`, does not scale (`_ScaleDistance` 0) and draws at alpha
0.02–0.07: a faint static outer skin.

## Round 10 (2026-10-13): a field of Mass crystals, per-crystal randomness, and reseats seen from far away

The ask: many Mass crystals in parallel (lifeform hearts, rows of pickups) read like one shared
animation. A crystal's columns are a pure function of the camera direction in its own space, so crystals
that share an orientation and sit far from the camera show identical columns. Give the designer knobs
that break that, each seeded per crystal, each keeping the ratchet's promises, and a number for "how
alike neighbours look".

Opened by reading the shared decision log: still the five entries from rounds 7–8. The designer
confirmed the round-9 ship (round-8 recipe, no turn, squares at 1x) is the decision, so there is
nothing to apply.

**New on the page**
- **View ▸ Field** (Off / Rows / Cluster), count, spacing, field camera distance. Each crystal has its
  own pose, centre and ratchet. The shell shader gained `uOffset` (inert at 0).
- **Vary tab** (lab-only, chip "needs code": C# and profile fields, no shader change):
  - *Random orientation*: a uniformly random pose per crystal, scaled by the knob. The depth rule holds
    exactly, because depth is read in each crystal's own space.
  - *Per-prism depth jitter*: each prism's Top-of-column depth ± the knob, its Sealed-from depth up to
    the knob lower.
  - *Reseat depth jitter*: per crystal, only ever DEEPER than the dial. A shallower reseat is seen.
  - *Reseat fade jitter*: per crystal, ± a fraction.
- Every crystal draws from its own `mulberry32` stream in a fixed order and count, so one knob never
  reshuffles another's. Every knob at 0 is the shipped crystal exactly (0 px, below).
- **Scorecard ▸ Score the field**: 20 crystals, rows and a cluster, the camera orbiting 90° over 20 s
  at 250 u (far) and 60 u (near). Every crystal ratchets at 15 Hz from a fresh state.
  - *Look r*: each crystal rendered alone from its own camera direction (48×48), pixels correlated with
    its two nearest neighbours over the pixels either covers.
  - *Picture r*: the prism heights, matched by view direction.
  - *Motion r*: those heights' change, i.e. moving in step.
  - *Same-prism r*: matched by plate index.
  - *Rises / Reseats seen %*: the promises, worst of the four runs.
- Seven new styles: the four knobs, all three together, reseat −0.87, and random orientation + reseat −0.87.
- The Ship-it tab names the depth profile values and the randomness fields.

**Field scorecard** (`window.__lab.runFieldBatch()`, seed 7):

| Style | Look r far | Look r near | Picture r far | Motion r far | Same-prism r far | Rises seen % | Reseats seen % | Reseats |
|---|---|---|---|---|---|---|---|---|
| **Shipped, every knob 0 (control)** | 0.60 | 0.17 | 0.99 | 0.90 | 0.99 | 0.1 | **73** | 3487 |
| Random orientation | **0.12** | 0.08 | 0.97 | 0.87 | 0.01 | 0.1 | 71.3 | 3341 |
| Per-prism depth jitter 0.15 | 0.50 | 0.15 | 0.99 | 0.89 | 0.99 | 0.2 | 52.9 | 3885 |
| Reseat jitter 0.15, fade ±50% | 0.61 | 0.17 | 0.99 | 0.92 | 0.99 | 0 | 53.4 | 1330 |
| All three | 0.11 | 0.08 | 0.96 | 0.88 | 0.01 | 0 | 28.3 | 1809 |
| Reseat −0.87 | 0.61 | 0.17 | 0.99 | 0.93 | 0.99 | 0 | **0.6** | 668 |
| Random orientation, reseat −0.87 | 0.12 | 0.08 | 0.97 | 0.89 | 0.01 | 0 | 1.8 | 676 |

Noise band: seeds 7, 11 and 23 give look r far 0.58–0.60 for the control and 0.10–0.12 with random
orientation (±0.02).

- **Orientation is the knob that changes what you see**: look r far 0.60 → 0.12, and 0.17 → 0.08 near.
- Per-prism depth jitter moves it a little (0.50). Reseat jitter does not move it at all.
- **Picture r and motion r stay at 0.87–0.99 for every variant.** That is the depth rule doing its
  job: every crystal shows its far side open and its near side sealed, and prisms drop as they come
  toward you. No randomness that keeps the rule can change that, and none should.

**Found**
1. **The shipped reseat (−0.8) is seen once the camera moves round a still crystal**, on both crystals,
   worst from far away. Measured with the field metric and one row of crystals:

   | Reseat at | Omni 34 u | Omni 250 u | Mass 34 u | Mass 250 u |
   |---|---|---|---|---|
   | −0.75 | 25% | 70.5% | 32.9% | 82.8% |
   | **−0.8 (shipped)** | 8.6% | 56.5% | 13.8% | 73% |
   | −0.85 | 0 | 3.2% | 0 | 32.5% |
   | **−0.87** | 0 | 0 | 0 | 0 |
   | −0.9 | 0 | 0 | 0 | 0 |

   Mechanism: a column top sits 2× the plate radius out (`_Reach` 2). Seen nearly orthographically from
   far away, it clears the silhouette whenever √(1−d²) > body / top ≈ 0.5, which is every reseat
   shallower than d ≈ −0.87. Round 7's "cliff near −0.75, 0% at −0.8" was measured at 34 u on a
   crystal turning 15°/s, a different motion from a camera orbiting a still crystal.

   The cost of going deeper: fewer reseats. The omni at 34 u reseats 209 times over the 20 s orbit at
   −0.8 and 42 times at −0.87, so columns stay sealed longer until they pass far enough behind.
2. **The single-crystal scorecard could no longer see a reseat.** Its 26 cameras are fixed, and since
   round 9 the crystal does not turn, so the ratchet never reseats and "Births seen 0%" is true by
   default. The field scorecard moves the camera; it is now the one that tests the promise.
3. **The shader's face-split sign test is gone, in the game and in both lab twins** (`VS_SHELL`,
   `vertexAt`). On the Mass prisms baked thick (§1 of this branch), the test flipped for the outward
   face's deep corners. Every normal of both meshes faces out (min n·(v−c) is 0.038 on the omni and 0.018
   on the Mass; the generator asserts it), so the test was always +1 on the lab's meshes.

**Checked**
- `verify_lab.cjs` PASS (desktop and phone; screenshots read).
- Pixel parity against round 9 (`omni_shepard_lab_parity.cjs`): 0 px at all five times for Shipped,
  Classic, Depth ratchet faces split and Sealed by depth faces split, and for Shipped and faces split on
  the Mass. Negative control: random orientation on the Mass changes 127k pixels.
- Field scorecard deterministic per seed; noise band as above.
- Omni and Mass reseat sweep: `fieldMetricsOf(p, 1, dist, subject)`.

**Decision (designer, 2026-10-13):** random orientation ships, the reseat moves to −0.87, and the omni gets the
same treatment as the Mass (they share one profile).

**What ships**

| Piece | What changed |
|---|---|
| `CrystalPoseJitter.cs` (new, `Utility/`) | Pure maths. `SeedOf(spawn position)` quantizes to 1 cm and murmur-mixes, so every client poses the same pickup the same way with nothing networked (crystals are local and a pickup spawns at the same place everywhere; a lifeform's heart is posed from where its host stands on the frame it enables, so two clients can turn one heart differently, which is cosmetic). `Pose(seed, amount)` is a uniformly random rotation (Shoemake's quaternion from the first three draws of the lab's `mulberry32`, ported bit for bit), scaled toward identity about its own axis. JS twin: `shoemake()` / `poseOfSeed()`. |
| `CrystalDepthPrisms.cs` | Gathers the crystal's models once in Awake: every sibling under the crystal root with a renderer, itself included, never the root (the round-8 free turn's rule). On the first frame after enable, it turns them about the root's origin to the crystal's pose. It waits for that frame because a pooled crystal is enabled before it is moved. The authored pose comes back on disable. The body search for the omni's holes reuses the same list. |
| `CrystalDepthPrismProfileSO` / `.asset` (generated) | `reseatDepth` −0.8 → **−0.87**; new `orientationJitter` **1** (Header "Variety"). `author_omni_crystal_triangles.py` `DEPTH_PROFILE_VALUES`. |
| `DepthRatchetTests.cs` (new) | The three pure ratchet tests moved here from `CrystalDepthPrismTests` (no editor dependencies), plus four new ones (seven methods, ten cases): the pose is identity at 0, the same for the same spawn and different one pickup along; it matches the lab at four seeds; it is uniform (4,000 spawns); and **a row of eight posed crystals under an orbiting camera keeps both promises**, with local depth equal to world depth to 1e-5. |
| `Tools/Build/crystal_depth_harness/` (new) | Compiles the shipped `DepthRatchet.cs`, `CrystalPoseJitter.cs` and `DepthRatchetTests.cs` against a value-type `UnityEngine` stub and runs them with NUnitLite. |
| Lab | `SHIPPED.jitOrient` is read from the profile, and the knob's chip is now "material values" (a profile value). The round-10 styles are re-based on the new Shipped: Every crystal alike, + depth jitter, + reseat jitter, + all jitter, Round 9. The field control is "every knob 0". |

**After the pick** (`runFieldBatch()`, seed 7):

| Style | Look r far | Look r near | Motion r far | Rises seen % | Reseats seen % | Reseats |
|---|---|---|---|---|---|---|
| Every knob 0 (control) | 0.61 | 0.17 | 0.93 | 0 | 0.6 | 668 |
| **Shipped** (random pose, reseat −0.87) | **0.12** | **0.08** | 0.89 | 0 | **1.8** | 676 |
| Round 9 (alike, reseat −0.8) | 0.60 | 0.17 | 0.90 | 0.1 | 73 | 3487 |

The 1.8% is the −0.87 bound's thin margin: 13 of 676 reseats across the four runs, near-range cluster
crystals seen at strong perspective. −0.9 would remove it at the cost of fewer reseats again.

**Checked (ship)**
- Graduation, pixels: the round-10 page with `{jitOrient 1, depthSwap −0.87}` applied equals the new Shipped,
  0 px at all five times on the omni and the Mass. Negative control: the recipe without the pose changes
  127k px. Classic is 0 px.
- `crystal_depth_harness`: 10/10 headless. Mutants each fail: a ratchet that reseats 0.3 shallower (2 tests),
  and a pose with the quaternion sign dropped (the lab-parity test).
- Extractor `--check` OK, `--self-test` 16/16. Omni generator `--check` OK. `verify_lab.cjs` PASS.
- Re-run at ship (2026-10-13, after F1–F4): `runFieldBatch()` reproduces the table above exactly; extractor
  `--check` OK and `--self-test` 17/17 (F3 added one); `verify_lab.cjs` PASS and `--self-test` OK.

**Not modelled:** crystals hiding each other (only their own prisms hide a reseat), fog, LOD, culling,
and a ratchet that skips frames on distant crystals.

## Performance (2026-10-13): the instrument exists, the numbers do not yet

CLAUDE.md: profile first. A cloud session cannot profile, so this branch built the instrument and changed
nothing on speculation. The designer chose to skip profiling for now and put the run on QA's list for Yash
(`Docs/QA/QA_BACKLOG.md`, **QA-P2-BENCH-DEPTH-PRISMS**).

**The instrument:** **FrogletTools ▸ Performance ▸ Crystal Depth Prism Benchmark**
(`Assets/_Scripts/Utility/PerformanceBenchmark/Editor/CrystalDepthPrismBenchmarkWindow.cs`, a READER).
- Spawns N omni and N Mass crystals (N = 10 / 50 / 200; omni only or Mass only for attribution) in a
  ring, stripped to their visuals.
- Orbits `Camera.main` outside the ring, looking in, so the ratchets run.
- Reports median / p95 of the main thread, `CrystalDepthPrisms.LateUpdate`,
  `CrystalSpaceSquares.LateUpdate`, `CrystalFlipWave.LateUpdate`, skinned-mesh update, draw calls,
  SetPass calls, batches, shadow casters and triangles.
- Lists every batch / SRP counter the Unity version exposes.

**Before / after table:** empty until the benchmark runs.

**What each candidate is, read from the code and assets (2026-10-13). Attack them in the order the numbers
rank them:**

| Candidate | Static facts | Blocked on |
|---|---|---|
| One draw per crystal (the property block breaks the SRP batcher) | **Moot as built.** `OmniDepthPrismShader` is `CGPROGRAM` with loose uniforms (no `UnityPerMaterial` CBUFFER) and a `float4 _DepthState[60]` array, so it is not SRP-batcher compatible with or without a block. A per-renderer value that keeps the batcher would first need the shader rewritten to HLSL with a `UnityPerMaterial` CBUFFER and the column state moved out of material properties (a shared `GraphicsBuffer` indexed per crystal). `Renderer.SetShaderUserValue` is not in the 6000.0.75 reference DLLs this session compiles against; the project is on 6000.3.17, which the QA item asks Yash to check. | draw-call and SetPass numbers |
| The leaving pass draws every prism every frame | True. Pass 1 (`UniversalForward`) runs for every visible crystal; a column with no reseat collapses each leaving vertex to a point (alpha 0), so it costs a draw call and a vertex shade of the whole mesh (360 vertices on the omni, 1080 on the Mass). A per-renderer toggle cannot disable one pass of a shared material; the options are a second material without the pass, swapped while no column is reseating, or a single pass that draws the leaving prism only when its column is live. | draw calls / vertex cost |
| Dirty-check the property block | **Not a pure saving.** `Crystal.ClearColorSetTint` writes a null block, which wipes `_DepthDriven` and `_DepthState`. A driver that skipped its write while no column changed would leave a still-camera crystal undriven (snapped sealed) until something moved. It needs a "block was wiped" check that costs less than the write it saves. | `CrystalDepthPrisms.LateUpdate` ms |
| Distance LOD (step the ratchet every k frames, staggered; or sit undriven) | A far crystal stepping every k frames sees k× the dt per step. `DepthRatchet.Step` already takes dt, so the reseat fade stays timed. A crystal sitting undriven shows sealed, which is a visible change at range. | `CrystalDepthPrisms.LateUpdate` ms per crystal |
| Prism shadows | **The prisms cast none:** the shader has no `ShadowCaster` pass. Their renderers still say `m_CastShadows 1` (omni prism prefab, Mass prefabs), so they are culled for the shadow pass for nothing. Setting it to 0 in both generators changes no pixel. | the "Shadow casters" counter |
| Space squares beyond a distance | The body re-skins every visible frame regardless: the Time flip wave turns its bones every frame. Freezing the blend-shape weights saves only the blend-shape pass, not the skinning. The body also has `m_SkinnedMotionVectors 1`, an extra skinned output, and `m_UpdateWhenOffscreen 0` (good). | skinned-mesh ms |
| F2: `ThemeManager` clones 9 unused material instances per domain set | See F2 (§4 of this branch): retired together with the tone/rim materials. | none (memory, not frame time) |

## Round 9 (2026-10-12): no free turn, the ratchet unfrozen, the Space squares ship

After flying round 8, the designer wrote: "lets ditch the autorotation on the whole object for both
crystals. And the depth effect did not make it into unity for the triangles or the face split. both of
those effects were frozen. i have recorded a new decision that includes the space squares."

The new decision is not in the live page's shared `decisions` log; it holds only the five earlier
entries, so it was probably saved locally in that browser. This round ships the round-8 recipe, with
no free turn and the squares on at the art file's speed. One cause is that a decision snapshot did
not carry the VIEW knobs, so a pick made while watching the squares could not have said so. It does
now (Found 3).

**Changes**

| Piece | What changed |
|---|---|
| `CrystalDepthPrisms.cs` | **The free turn is gone.** The crystal's models are no longer turned, and the authored-pose restore went with it. **The ratchet runs against the gameplay camera:** `CameraManager`'s active `CustomCameraController.Camera`, falling back to `Camera.main` only where no controller is active (main menu, test scenes). It is resolved once per frame for every crystal (Found 1). |
| `CrystalDepthPrismProfileSO` / `.asset` | The three turn fields are removed. |
| `CrystalSpaceSquares.cs` + `CrystalSpaceSquaresSO.cs` (new) | **The Space squares in game.** The FBX carries no shape keys, so on the first visible frame the body moves onto a copy of the twin it draws. That copy has the art file's three keys as blend shapes. It is made once per twin and shared, and registered through the new `RhombusSkinBaker.RegisterDerivedTwin` so `SourceOf` still finds the FBX for hull fusion and the morph. The driver then plays the baked weights, with a per-crystal phase. Positions are Blender's exactly. Each key carries four frames (25/50/75/100) whose normals are the authored normal turned with its face, the lab's `squaresAttr` rule. A stacked key (Step2 is relative to Step1) is measured with its base at 1. |
| `OmniSpaceSquares.asset` (new, generated) | `author_omni_crystal_triangles.py` maps `omni_space_squares.json` onto the body FBX's control points: 480 anchors in Unity's space (x negated), 3 keys of 480 offsets, 120 frames × 3 weights, 25 fps, speed 1. It refuses a bake that does not land on the body, and anchors that sit within 10× the runtime match tolerance of another control point. |
| `OmniCrystalBody.prefab` | Carries `CrystalSpaceSquares`. Only `Crystal.prefab` instances it. |
| Lab | `SHIPPED.turn` is 0; turn knobs cost code again. The squares view is on by default, at the asset's speed. Decisions record the view (Found 3). Library styles are rooted on the look they were designed on (Found 2). The export says what to change when the squares are off or at another speed. |

**Checked**
- **C#:** `unity_refcompile` player and editor configs report 0 errors in project code. Negative
  control: one planted error each in `CrystalSpaceSquares.cs` and its test is named.
- **The baker, run headlessly on the shipped code:** `CrystalSpaceSquaresBaker` and
  `CrystalSpaceSquaresSO`, extracted from the source files, ran against faithful `UnityEngine` stubs,
  the real body FBX (imported Unity's way: x negated, split by normal, welded) and the real asset.
  16/16 pass:
  - the moving vertices weld to exactly the 480 square corners (1,440 of 2,880 split vertices);
  - offsets equal the asset's to 6e-8;
  - at every frame of every key, stacked keys included, each normal is its authored normal turned
    with each of its faces, to 2.2e-5;
  - the drawn mesh is never edited, there is one twin per drawn mesh, and a mesh already carrying the
    keys is worn as is.

  Mutants each fail: normals left unturned, stacking ignored, and the snap rule removed (weights off
  by 0.97).
- **Playback = the lab:** `WeightsAt` against the lab's own `squaresWeights()` (extracted from the
  page) over 2,001 samples matches to 6.4e-7.
- **Lab, pixels:**
  - Classic, fill-spin, depth-seal and col-hidden are 0 px against the round-7 page, with the squares
    view off to match it.
  - Round 8 had broken Classic: 245/255 off, 107,926 px.
  - Breathe is 0 px against round 8.
  - Shipped with `turn 15` set back is 0 px against round 8, and its knobs differ only in `turn` and
    `viewSquares`.
- Shader parity: the depth prisms still pass 8/8 and Breathe 7/7. `verify_lab.cjs` PASS; extractor
  `--check` OK and `--self-test` 12/12; omni generator `--check` OK. The Mass generator shows only its
  known shell-material drift.
- **Not verified:** a play session in the Editor. No Unity is available in this session, so
  `/verify-unity` did not run. The checklist entry is in `Docs/UNITY_VERIFICATION_CHECKLIST.md`.

**Found**
1. **A lab with one camera cannot show that the game has several.** `CrystalDepthPrisms` read
   `Camera.main`. In Bootstrap that is the MainCamera-tagged "Camera", the main menu's
   Cinemachine-brain camera with the AudioListener, a child of `CameraManager`. It never follows the
   vessel; "CM PlayerCam", which is untagged and never retagged, is driven by `CustomCameraController`
   and renders the game. So every column's depth was measured from a point that never moved (or, with
   that camera off, the driver found none and left the prisms undriven, sealed and solid). Either way
   the ratchet held every prism still and the face split never opened. That is the "frozen" the designer saw. The precedent was
   already in the tree (`WormholeView.ResolveController`). **Any view-dependent effect must ask
   `CameraManager` for the active controller's camera, not `Camera.main`.**
2. **Rooting every style on Shipped broke the library when Shipped changed.** Round 8 made `SHIPPED`
   the Depth ratchet, and `styleParams` laid every style over it. So a Classic, Columns or Spin style
   (which never set the depth knobs) inherited the ratchet and its prisms: Classic drew 245/255 off its
   round-7 self. A style is now rooted on the look it was designed on: Shipped for `base: 'shipped'`,
   the pinned Breathe look for every other base.
3. **A decision that leaves out the view cannot say what was approved.** `snapshot()` recorded only
   the non-view knobs, so "Space squares on" was invisible in the log. It now records `view` as well.

## Follow-ups F1–F4 closed (2026-10-13)

| # | What was done | What the row got wrong, or what it missed |
|---|---|---|
| F1 | `ThemeManagerDataContainerSO.GetTeamCrystalMaterial(domain, material)` finds the base-set slot holding a model's OWN default material. `Crystal.ResolveActivationMaterial` and `ChangeDomain` use it through `TeamMaterialFor`. A model whose material is not in the set keeps its own. `TeamCrystalMaterialTests` covers a synthetic set, the shipped omni (slots 0 and 1) and the Mass models (−1). | Still latent, re-measured: `crystalPrefab` names only the omni (31 `Crystal.prefab`, 3 `BigCrystalVariant`). `ChangeDomain` also had no null check on the theme data; `TeamMaterialFor` has one. |
| F2 | The eight `OmniShepardTriangles*` tone and rim materials are deleted. The lab pins their values in `RETIRED` (0 px against before on Shipped, Classic, Breathe and Breathe wave). The extractor and the Breathe parity tool read the pin. The generator stops authoring them and leaves `OriginalMaterialSet` slots 2–4 empty. `ThemeManager` clones only the slots that are set. | `OmniShepardTriangles 0`, the three `Inactive` and `RimInactive` were already unreferenced; only slots 2–4 named the other three. |
| F3 | **Retired** (designer's call). The eight Mass shell materials are deleted with `SpawnedSegments.prefab`, and the lab pins them in `RETIRED.massShells` (0 px on the old-shells view and Mass glass). The Mass generator no longer authors them, and its spent exploding-material repoints stand down into an invariant. **Its `--check` exits 0 for the first time since round 8, and a planted change is named DRIFT.** | The drift was not a property re-order: the Editor-saved files carried **2× the generator's HDR colours** and the `_SCALEDISTANCE_ON` keyword, a deliberate brightening. Their "live" consumer `SpawnedSegments.prefab` was an orphan (1,008 baked Mass crystals, 0 references). |
| F4 | `CameraManager.ViewCamera` returns the active controller's camera, else `Camera.main`, resolved once a frame; `CrystalDepthPrisms` uses it too. Five view-dependent reads that had round 9's bug now use it: `CellEcologyLod` (the LOD's camera "pilot"), `ThreatGrove` (its far-pilot sense), `GrizzlyBombVisual` (billboards; it also cached the camera at arm time, so a switch to the death cam left them facing the old one), the Thresher combo label, and `SniperScopeOverlay` (the flight reticle was projected through the menu camera). `MainMenuCameraController`'s comment is corrected. | 52 reads in 32 files, classified. **Already right:** `ObjectiveIndicator`, `WormholeView`, `PreGameCinematicController`, and `ShipAudioController` (FMOD listener first). **Menu-only, where `Camera.main` is the view:** toys, previews, screenshots, `ConnectingArenaPreview`. **Settings / tools:** `OffscreenCameraSetup`, the stress and explosion harnesses, training. **Left as rows:** `GraphicsSettingsApplier` applies anti-aliasing to `Camera.main` only, so "CM PlayerCam" never gets the setting; `CameraManager` paints the background of `Camera.main` on two of its three paths. |

New rows found while closing these:
- **R1:** no material uses `OmniShepardFresnelShader` any more (Prisma has a GLSL translation of it). Retire it
  with `omni_shepard_shader_parity.py`, or keep it as the lab's Breathe reference.
- **R2:** `ThemeManager.prefab` serializes `BaseMaterialSet: GreenMaterialSet` and `ColorSet`, which the script no
  longer has (the live set comes from `ThemeManagerDataContainer.asset`). They are dead overrides.
- **R3:** the anti-aliasing and background-colour reads above (F4, settings side).

## Follow-ups (refactor rows, measured at ship time 2026-10-12, round 9)

Rows only: none of these is fixed on this branch.

| # | Kind | Row | Evidence |
|---|---|---|---|
| F1 | inconsistency (walked past, latent) | `Crystal.ResolveActivationMaterial` / `ChangeDomain` pick the team material by model SLOT INDEX from `OriginalMaterialSet`, whose slot 0 is the omni body. A Mass crystal given a domain would put `OmniCrystalBody` on its depth-prism renderer: wrong shader, prisms frozen. Fix by keying the team material to the model's own default material, not its index. | Not live today: `grep crystalPrefab:` over scenes/prefabs finds 0 references to `CrystalMass` / `ActiveCrystalMass` and 31 to the omni `Crystal.prefab`; a Mass prefab's `ownDomain` is -1. |
| F2 | debt created | `OriginalMaterialSet` slots 2-4 still name retired tone/rim materials (`OmniShepardTriangles 1/2`, `OmniShepardTrianglesRim`), and `ThemeManager` clones all three for every domain: 9 material instances no crystal draws. `author_omni_crystal_triangles.py` keeps authoring the 8 retired tone/rim materials because the lab's Classic and Breathe styles read them. Blocking question: is the lab still deciding anything with them? If not, retire both together. | `OmniShepardTriangles 0` and the `Inactive` tone materials have 0 references outside `.mat` files; `1`, `2` and `Rim` are referenced only by `OriginalMaterialSet.asset`. |
| F3 | inconsistency (made visible) | `author_mass_crystal_look.py --check` reports 8 drifting Mass shell materials (`Active/BlueMassCrystalMaterial[ 1-3]`). The same 8 drift on a clean `bleeding-edge` worktree, so this branch did not cause it: the Editor re-saved them. Two of them are still live in `SpawnedSegments.prefab`. Decide which side wins (re-run the generator or teach it the re-saved form), so its `--check` can fail on a real change again. | `--check` exits 1 on this branch and on base, naming the same 8 files. |
| F4 | inconsistency (walked past, cross-system) | The repo disagrees about which camera `Camera.main` is in flight. `MainMenuCameraController` (~:405) says "CM PlayerCam is Camera.main during freestyle", but in `Bootstrap.unity` "CM PlayerCam" is `Untagged` and nothing retags it. The only MainCamera under `CameraManager` is the Cinemachine-brain "Camera", which never follows the vessel. 52 `Camera.main` reads in 32 runtime files; a view-dependent one among them has this branch's bug. Filed as a suggested task. | `grep -rn "Camera\.main" Assets/_Scripts --include=*.cs` minus `Editor/` and `Tests/`. Bootstrap YAML: `m_TagString` of "CM PlayerCam" vs "Camera". |

## Round 8 (2026-10-12): the Depth ratchet ships

The designer recorded two decisions a minute apart (decision log, 22:00 and 22:01), both on the
**Depth ratchet** with faces split 0.5 and thickness growing inward. The omni pick was thickness 8 and
the Mass pick (Mass + effect) thickness 6.25; both had top of column at −0.88, sealed from 1, a 15°/s
free turn and opaque prisms. They wrote "the omni crystal and the mass crystal are both looking
excellent" and asked to fly with them. This round puts that look in the game.

**What ships** (generators own every asset; `Docs/PALETTE.md` §2.10):

| Piece | What it is |
|---|---|
| `OmniDepthPrismShader.shader` (new) | The body's look (colour formula, `_Spread`, `_Opacity` dissolve) plus the column vertex stage: `k = lerp(_Reach, 1, u)`, size `lerp(_PlateScaleStart, 1, u)`, thickness from the top face inward, faces split by `_FaceSplit × _SplitUnit × (1 − u)`. Two passes: the arriving prism and, during a reseat, the leaving one, dithered on complementary pixels. Undriven, every prism draws sealed and solid. |
| `CrystalDepthPrisms.cs` (new) | Per visible crystal, each frame: turns the crystal's models (never the root), steps every column's ratchet against the main camera (`DepthRatchet.cs`, pure math), and writes `_DepthState[plate]` through a property block that composes with `Crystal`'s tint. On the omni it also cuts the body's 20 triangle plates into holes. |
| `CrystalDepthPrismProfile.asset` (new) | open −0.88, seal 1, power 1, reseat −0.8, fade 0.6 s, turn 15°/s, tilt 25°, wobble 0. Shared by both crystals. |
| `RhombusSkinBaker.TryCutPlates` | The body's holes: a cut twin of the skinned body that keeps every vertex, bone and bindpose, cached per mesh, and resolves to the FBX through `SourceOf`. |
| Omni (`author_omni_crystal_triangles.py`) | `Crystal.prefab`: 2 slots (body, prisms); the second and third tone shells are removed. The prism prefab is now SEATED (scale 0.446 = the crystal's own triangles). TEXCOORD2 = (centre, plate index). `OmniDepthPrisms[Inactive].mat`, thickness 8. `OriginalMaterialSet.CrystalMaterial1` = `OmniDepthPrisms`, so team crystals match. |
| Mass (`author_mass_crystal_look.py`) | `CrystalMass` / `ActiveCrystalMass`: the inner shell becomes the prism renderer (`MassCrystalPrisms.asset`, 60 prisms, seated) and the other three shells are deleted (every consumer references only the root objects; swept). `MassDepthPrisms[Inactive].mat`, thickness 6.25. |

**The lab follows the assets.** The extractor reads the shipped look back: the prism materials, the
profile, the Mass thickness, and the shell scale at s = 1 (the seated prefab × `_Reach` = 0.89219, as
before). `SHIPPED` is now the Depth ratchet. The Breathe look is the pinned base `BREATHE_LOOK`, read
from its still-authored materials. The "Breathe" style and its riffs moved onto it, and team saves from
before round 8 count as Breathe diffs. New knob: **Thickness on the Mass** (6.25). The crystal switch
reads Omni / Mass (old shells) / Mass + effect: with the Shipped style, Mass + effect is the Mass
crystal as it ships.

**Checked**
- **Graduation, knob level:** the round-7 page's Shipped with the recorded recipe applied equals the
  new `SHIPPED` on all 61 knobs. Negative control: dropping one recipe key is named.
- **Graduation, pixels:** 0 px over 2/255 at all five times, for omni and Mass. This is measured in
  the stateless mode (depth 1) on both pages, which exercises everything the assets encode: scale,
  thickness, split, anchor, turn and the Mass thickness.
- **The ratchet itself cannot be pixel-compared across two pages,** because its state depends on what
  was drawn before (see Found 1).
- The Breathe style is 0 px against the round-7 Shipped.
- **Shader vs lab:** `omni_depth_prism_shader_parity.py` compiles the shipped `DepthPrismPosition`
  with clang. On every vertex at five column places it matches the lab's `vertexAt` to 2.1e-5 (omni)
  and 2.4e-5 (Mass); that is the lab mesh's 5-decimal bake, stretched by thickness. Sealed top faces
  are exact (5e-6). Negative controls with inward thickness off or face split off fail by 5e-2–1e-1.
  The Breathe tool (`omni_shepard_shader_parity.py`) still passes 7/7, now on the Breathe style.
- **The whole shader compiles with DXC:** both passes, vertex and fragment. Negative control: a
  missing `_Opacity` is named.
- **C#:**
  - `unity_refcompile` player and editor configs report 0 errors in project code. It found one
    real error first: an unassigned `out` local.
  - Negative control: one planted error each in the component and the test is named.
  - The ratchet tests ran headlessly against the shipped `DepthRatchet.cs` (3/3). A mutant that
    reseats in view fails.
- `verify_lab.cjs` PASS; extractor `--check` OK and `--self-test` 12/12; omni generator `--check` OK.

**Found**
1. **A stateful look cannot be pixel-compared across pages that drew different histories.** The
   first pixel comparison differed by up to 4,924 px, shrinking with time (to 498 px by 7.7 s): the
   ratchet's state carried frames drawn before the manual clock took over. The proof moved to the
   knob level plus the stateless mode. The lab now restarts the ratchet on `reset()` and on any change
   to a dial it reads, so from this round on a run's state depends only on (dials, clock, camera).
2. **The Mass generator's shell materials were already drifting.** The Unity Editor had re-saved them
   in its own property order. That is pre-existing, so this round wrote only its new outputs and left
   those eight files alone; the generator's docstring records it.
3. **The prism prefab had to move to the SEATED scale** (0.446, the crystal's own triangles; it was
   0.892, the top of the column). Two reasons: anything that draws the mesh without the driver (a
   husk, a codex model, a wrong material) shows the crystal at rest instead of a halo twice its size,
   and the culling bounds can be widened by `_Reach` from a true base.

**Not in this round:** the Space squares (lab-only, needs the body re-exported with blend shapes) and
the other depth modes (seal by depth, depth columns), which still need code.

## Round 7 (2026-10-12): sealed by depth

The ask: something like "Hole fill, spin-hidden", but tied to the way the prisms separate with depth.
**The back side should be at the top of its columns and the front should be sealed, and that must
hold at any orientation as the crystal is spun around.**

"Hole fill, spin-hidden" only gets that relation from the camera-locked spin, which runs on a clock.
Turn the crystal any other way and the relation breaks.

**The mechanism (new Depth tab)**
- A prism's place in its column is a function of its **depth** from the camera, not of time:
  d = (plate direction) · (camera direction), in object space. d = +1 faces you, 0 is on the
  silhouette, −1 is straight behind.
- seat = smoothstep(*Top of column at*, *Sealed from*, d)^*Profile curve*. Seat 0 is the top of the
  column; seat 1 is sealed in the hole.
- It is built on Hole fill: the body without its 20 triangle plates, with opaque prisms.
- There is no clock in the rule, so it holds from any side by construction. Spin the crystal, tumble
  it, or orbit the camera: the side you look at is sealed and the far side stands open.
- **Free turn** (Depth tab: rate, axis tilt, axis wobble) turns the whole crystal about a fixed axis,
  **not** camera-locked, so the prisms move.

Three modes:

| Mode | Rule | Ships as |
|---|---|---|
| **Seal by depth** | u = seat(d), always | shader only: the camera position is all it needs |
| **Ratchet** | Coming toward you: ride seat(d) down. Going away: stay sealed. Once behind *Reseat* depth: the sealed prism fades out in its hole while a new one fades in at the top of its column | shader + a small per-plate state component |
| **Depth columns** | Every column runs the hole-fill swap forever, with column length 1 − seat(d): tall behind, zero in front | shader only |

In Ratchet and Depth columns the arriving and leaving prisms dither on **complementary pixels**
(noise vs 1 − noise). A swap in place therefore shows no gap and no doubling, so the front's
zero-length columns swap unseen.

**Scorecard** (new columns, camera-averaged over 26 directions, 30 s each, fresh state per camera)
- **Depth link r:** the correlation between how far behind a prism is (−d) and how far up its column
  it is (1 − u). It measures the ask directly.
- **Rises seen %:** the share of visible travel that goes outward, which is the anti-Shepard motion.

| Style | Depth link r | Rises seen | Births seen | Seals in view /s | Holes open |
|---|---|---|---|---|---|
| **Sealed by depth** | **0.93** | 49.9% | 0% (no births) | 0.63 | 12.3 |
| Sealed by depth, gradient | 0.98 | 49.9% | 0% | 0.49 | 16.6 |
| **Depth ratchet** (reseat −0.8) | 0.71 | **0%** | **0%** | 0.43 | 7.5 |
| Depth ratchet, tumbling | 0.67 | 0% | 0% | 0.60 | 6.6 |
| Depth columns | 0.55 | 15.9% | 43.5% | 0.52 | 7.7 |
| Depth columns, depth off (control) | 0.01 | 2% | 85.8% | 1.16 | 12.7 |
| Hole fill, spin-hidden (round 6, for reference) | — | — | 0% | 2.0 | 12.8 |

- **Sealed by depth is exactly the ask:** r = 0.93. Because the rule has no direction, prisms coming
  toward you drop into their holes and prisms going away lift out. Half of the visible motion is
  outward.
- **The Ratchet keeps the depth relation (r = 0.71) and makes every visible move downward:** 0%
  rises and 0% births seen. That is the Shepard illusion on a crystal you can spin any way.
  Columns that never pass far enough behind stay sealed until they do; the tumbling variant
  brings every face round.
- *Sealed by depth, still* scores 0 motion because the scorecard's cameras do not move. On the page
  the camera orbit (or a drag) moves it.

**Found**
1. **The Ratchet's reseat depth has a cliff at about −0.75.** At −0.7, 98.8% of reseats are seen;
   at −0.8, 0%. The top of a column at the back stays outside the body's shadow until it is almost
   straight behind (about 6.8 u above a 7.1 u body). The default is −0.8. A slower fade (1.5 s)
   only halves the count at −0.7.
2. **The compare grid's 2-px gutters were never cleared.** The canvas keeps its buffer and each
   cell clears only inside its scissor, so the gutters kept the last full view as stray green lines.
   This had been there since the grid was added. The grid now clears the whole canvas first.

**Checked**
- `verify_lab.cjs` PASS.
- Pixel parity vs round 6 is 0 px for Shipped, Classic, Hole fill, Hole fill spin-hidden and
  Spin-hidden columns, so the depth and turn paths are inert at their defaults.
- The depth-off control scores r = 0.01 against 0.93, so the link column measures depth and not
  the scene.
- `omni_shepard_lab_assets.py --check` OK; `omni_shepard_shader_parity.py` PASS.
- No C# or asset changed, so `/verify-unity` has nothing to run.

**Face split (Shape ▸ Face split; four new depth styles)**
- Each triangle is treated as the prism it is. Its five faces (top, bottom and three sides) move apart along their own normals by *Face split* × the plate radius × (1 − u).
- u is the column place, so it is the same gradient. In the depth styles the far side is split open at the top of its columns and the near side is whole and sealed. In the clock-driven styles a prism assembles on its way down.
- `OmniCrystalTriangles.asset` and `MassCrystalExport1` already give each face its own corners, with flat normals (18 corners and 5 faces per prism, normals within 3e-5 of the face normal). The vertex normal therefore is the face normal, and the shader needs nothing new baked.
- Checked:
  - In the JS twin, all five face centroids move out by the same 0.237 mesh units at split 1.
  - Pixel parity is 0 px vs the round-7 page at split 0 (Shipped, Sealed by depth, Depth ratchet, Hole fill spin-hidden).
  - Shader parity is 7 of 7.
- New styles: Sealed by depth, faces split; Depth ratchet, faces split; Depth columns, faces split; Sealed by depth, gradient, faces split.

**Thickness grows inward (Shape ▸ Thickness grows)**
- The shipped shader stretches a plate about its own centre. A thicker prism therefore stands proud of the body when it lands: its top face is at 7.138 u at thickness 1 (exactly flush with the body's own triangle plate, on all 20 slots), 7.18 at 1.25, 7.31 at 2 and 7.65 at 4.
- *Inward* anchors the stretch at the top face instead: local += dir · (dot(local, dir) − topH) · (thickness − 1), where topH is the top face's height above the plate centre. It is the same for every plate of a mesh.
- A seated prism is then flush at any thickness: the top face measures 7.138 at 1, 1.25, 2 and 4, and only the bottom face goes deeper (6.80 → 5.78). On the Mass the prisms stay flush with their neighbours the same way.
- On for the Hole fill and depth styles, where the prisms fill holes. Shipped, Classic and Spin-hidden are unchanged (0 px against the previous page), and shader parity is 7 of 7.

**Also:** the Thickness slider now goes to 8, up from 3. `OmniShepardFresnelShader`'s `_Thickness` inspector range was raised to `Range(0.2, 8)` to match, so a lab pick can be typed into the material. That is a property-range change only: the shipped value stays 1.25 and the vertex code is untouched (shader parity 7 of 7).

**Not modelled / to build:** all lab-side. Seal by depth and Depth columns need only the shader
(the object-space camera direction, the plate centre already baked, and hole fill). The Ratchet also
needs per-plate state written from C# each frame (`MaterialPropertyBlock.SetVectorArray`) and a
plate index baked into the mesh. The Ship-it tab lists each.

## Round 6 (2026-10-12): the Mass crystal, hole fill, and the Space squares

The designer asked for three things:
- **"This is going to affect the mass crystal too."** Bring in the current Mass crystal effect, with
  a simple switch to see the selected effect on the whole Mass crystal, since it is all triangles.
- **"A triangle hole that gets filled by an opaque prism."** The prism that made the hole fades out
  into the prism that fills it, at the top of the column.
- **The Space effect alongside.** Bring in the squares rearranging on the surface by weaving above
  and below the other surface prisms. Two `.blend` files were attached for this.

**Crystal switch (header, and View ▸ Crystal)**
- **Omni** — as before.
- **Mass (shipped)** — `CrystalMass.prefab` exactly as it ships: four shells of the 60 triangular
  prisms of `MassCrystalExport1` (`ActiveMassCrystalMaterial 0–3`). Three shells sweep their bands
  (0.33→0, 0.66→0.33, 1→0.66, period 3 s, `_ScaleDistance 1`), and the outer shell stays static at
  1.03→0.98. Read from the prefab and materials by `omni_shepard_lab_assets.py`, like the omni.
- **Mass + effect** — the selected style on all 60 Mass prisms. Plate centres are computed from the
  Mass mesh the way `OmniCrystalTriangles.asset` bakes them.

The Mass is drawn at the omni body's world size (7.5 u) so the two compare side by side. The compare
grid follows the switch.

**Hole fill (Fill tab, ten styles)**
- Each column's slot is a triangular **hole**. On the omni, the body's own 20 triangle plates are
  removed: the body is drawn from a mask of its 6-corner components.
- The prism in the hole is opaque. It wears the omni body material, with a screen-door dither for
  its fades.
- Each cycle:
  1. The resident prism fades out in the hole while its replacement fades in at the top of the same
     column (*Cross-fade*).
  2. The replacement drops.
  3. It sits in the hole until the next swap (*Sit in hole*).
- Each column draws two instances, the arriving prism and the leaving one.
- *Glass* swaps the opaque prisms back to the Shepard material, for comparison.

| Style (Omni / Mass + effect) | Holes open (avg) | Births seen | Arrivals in view /s |
|---|---|---|---|
| Hole fill | 12.8 / 38.4 | 89.2% / 89.2% | 1.42 / 4.38 |
| **Hole fill, spin-hidden** | 12.8 / 38.5 | **0% / 0%** | **2.0 / 6.0** |
| Hole fill, long wait | 7.8 / 23.4 | 89.2% | 1.42 / 4.38 |
| Spin-hidden columns (glass) on the Mass | — | 100% | 6.0 |
| Shipped (Breathe) on the Mass | — | 0% | 17.1 |

- In plain Hole fill the births are seen by design: the cross-fade at the top of the column is the
  look asked for.
- Spin-hidden glass cannot hide anything on the Mass, because there is no opaque body. Hole fill
  can: the opaque prisms ARE the body.

**Space squares (View ▸ Space squares, Squares speed)**
- Found in `CrystalsRaw_V2.blend`, object `OmniCrystal.001`, action `Action.008`, 25 fps, a
  120-frame (4.8 s) loop. Three shape keys move only the 60 square plates:
  - `SquareRotateAroundPentagon` (frames 1→40, then snaps to 0);
  - `SquareOverTri_Step1/2` (41→80, then held);
  - `SquareOverTri_Step2/2`, stacked on Step 1 (81→120).
- Both snaps land congruent (0 and 2e-5 off-slot), so they cannot be seen.
- `Tools/Build/bake_omni_space_squares.py` (Blender, headless) writes
  `Docs/Studios/data/omni_space_squares.json`: the basis, each key's delta against its relative key,
  and the per-frame weights that Blender's curves produce. It refuses to write unless its replay
  matches Blender's evaluated mesh at all 120 frames; measured 1.15e-6.
- `omni_shepard_lab_assets.py` maps the Blender vertices onto the shipped body FBX. They share axes
  at 2.2417× scale, with a different vertex order. Measured mapping error 1.1e-5; a shifted body is
  refused (new self-test).
- The lab replays it on the body with the authored normals turned by each face's rotation, so frame
  0 is pixel-identical to the static body.
- It is a view toggle: it works with every style and every cell of the compare grid.
- **It is not in the game.** Shipping it needs the body re-exported with blend shapes, plus an
  animator clip. The Ship-it tab says so when it is on.

**Found**
1. **Hole fill, spin-hidden needed NO swirl, not 72°.** The first derivation said a prism landing at
   1 − dwell needs a 180° − 360°·dwell swirl to land in front. The sweep showed swirl 0 gives 0%
   births seen and the most arrivals in view (2.0/s). The swap happens behind the crystal and the
   spin itself brings the hole round, so the derivation answered the wrong question. Set to 0.
2. **The Mass's opaque prisms clipped to yellow.** They used the Mass shell's HDR colours
   (1.98 green). They now wear the omni body material, the one an opaque crystal surface uses.
3. **The HUD read 0 speed for every fill style.** It sampled the descent at the omni's hold point,
   which in Hole fill is the cross-fade. It now samples the fill timeline's drop segment.

**Checked**
- `verify_lab.cjs` PASS (including the WebGL-warning check).
- Pixel parity vs round 5 with the new features off: 0 px over 2/255 for Shipped, Classic,
  Spin-hidden columns and Hidden columns.
- Squares on at t = 0 is 0 px different from squares off.
- `omni_shepard_lab_assets.py --check` OK and `--self-test` 9 of 9.
- `omni_shepard_shader_parity.py` 7 of 7.
- No C# or asset changed this round, so `/verify-unity` has nothing to run.

## Round 5 (2026-10-12): one triangle per column, hidden births

The designer recorded "Use the Shipped (Breathe) style" in the decision log and asked to go back to
the Shepard illusion: **one triangle at a time in each column, each column on its own phase, and the
whole crystal turned with respect to the camera so the side where triangles appear is always hidden
by the rest of the object.** Also: riff more hypnotic options off the saved Breathe.

**The mechanism ("Spin to hide", Hide tab)**
- One layer. Each column's phase is its angle round the spin axis (the plate's longitude), plus a
  small latitude tie-break, because eight pairs of columns on this mesh share a longitude.
- The whole crystal (body and triangles) turns once per trip about the camera's up axis. The turn is
  phase-locked so every column is born exactly as it passes behind the crystal: rotation
  θ = −2π·t/T + offset, derived in the page source at `spinTheta`.
- An opposing 180° swirl in each column means the triangle that was born behind lands on the side
  facing you. Without it, the landing comes round behind too ("Spin, no swirl": 0 arrivals in view).
- The four columns 69° from the spin axis cannot be hidden by any rotation: their far-side births sit
  just above the silhouette. A half-strength column swing over the first 30% of the trip (the
  "polar tuck") pulls them in behind the body.

**Scorecard** (new camera-averaged columns, over 26 camera directions at 34 u; the body is treated
as a sphere of 0.97 × its radius for the occlusion test). A birth is **seen** if the triangle turns
solid (α ≥ 0.25) within the first 10% of its trip at a spot the body does not hide.

| Style | Births seen | Landings seen | Arrivals in view /s | Trip |
|---|---|---|---|---|
| **Spin-hidden columns** (spin + polar tuck) | **0%** | 0% | **2.0** (all 20 per trip) | 10 s |
| Spin only (no tuck), as first asked | 20% (the 4 polar columns) | 0% | 2.0 | 10 s |
| Spin-hidden, spin and tuck off (control) | 86.9% | 0% | 1.14 | 10 s |
| Hidden columns (per-column swing, no spin) | 0% | 0% | 1.42 (only front columns) | 8 s |
| Columns, unhidden (control) | 89.2% | 0% | 1.42 | 8 s |
| Shipped (Breathe) | 0% | 0% | 3.25 | 10.5 s |

Spin-axis tilt only moves the problem: −45° to +45° gives 20–60% births seen without the tuck.

**Also new**
- **Breathe riffs:** Breathe wave (a ripple rolling pole to pole), Breathe spiral, Breathe turning
  (phase-locked to the spin), slow tide, twisting, glowing contact.
- **Column swing family:** the per-column alternative, with no spin.
- **Generators:** "9 spin variants" and "9 hidden variants" seeded random generators.
- **Phase pattern:** an "Even" stagger pattern.
- **Hide tab:** spin, axis tilt, birth point, phase tie-break, column swing, swing mode, swing end.

**Found**
1. **The per-column swing measured its turn from the wrong vector.** It took the angle from the
   column's landing direction but applied it after the swirl, so with any swirl it aimed at the
   wrong place. "Hidden + swirl" read 80% births seen, and spin plus swing read 80%. Now it measures
   from the current direction: 0% for both. Found by the parameter sweep, not by eye.
2. **Mirror mode does not hide.** "Hidden, mirror" shows 78.5% of births. Mirroring only moves
   FRONT columns behind; back columns are born in place at twice the crystal's radius, outside the
   body's shadow cone. Kept as a measured negative.
3. **A Shepard-style fade fights spin hiding:** "Spin-hidden, Shepard fade" shows 20%. The
   triangle only turns solid after it has come round into view. Solid-from-birth (fade-in 4%) is
   what makes hiding work.

**Checked**
- `verify_lab.cjs` PASS.
- Shipped page vs round 4, pixel parity: 0 px over 2/255 at all 5 times. The same for Classic and
  for Swirl, with `--old-style/--new-style` (new flag).
- `omni_shepard_shader_parity.py` 7 of 7: the shipped materials still match the lab.
- Both new controls fail as they must (86.9% and 89.2% births seen).

**Not modelled / to build:** this is all lab-side, and every round-5 option is *needs shader*. The
spin also needs a small C# component that turns the crystal transform to the camera each frame. That
also turns the body, its flip wave and its charge edges. A triangle mid-flight shifts slightly when
the camera moves. In multiplayer every client turns its own crystals to its own camera, which is
purely visual. Pick a look first; the Ship-it tab lists what it would take.

## Round 4 (2026-10-10): Breathe ships

The designer exported this recipe from the lab and pasted it back. That is the decision:

```
{"period":3.5,"direction":2,"sizeStart":0.5,"thickness":1.25,"faceForward":1,"rimShell":0}
```

**What changed in the game** (all through `Tools/Build/author_omni_crystal_triangles.py`, `--check` clean):
- `OmniShepardFresnelShader` gained four opt-in dials, each inert at its default: `_Breathe` (the
  band ping-pongs), `_PhaseOffset` (this layer's share of the clock), `_PlateScaleStart` and
  `_Thickness` (each plate sized and stretched about its own centre).
- `OmniCrystalTriangles.asset` carries each vertex's plate centre in TEXCOORD2 (stride 48 → 60).
- The six `OmniShepardTriangles*` materials now span the whole trip, 1.0 ↔ 0.5, breathing over
  10.5 s (3 shells × 3.5 s), at phases 0, ⅓ and ⅔, with `_PlateScaleStart` 0.5, `_Thickness` 1.25
  and `_FaceForward` 1.
- `Crystal.prefab` lost slot 4, the stationary rim. A breathing shell turns round at alpha 0.05, so
  there is no birth pop left to hide.
- Docs: `Docs/PALETTE.md` §2.10, `Docs/UNITY_VERIFICATION_CHECKLIST.md`, and the comments and
  tooltips that described the rim.

**What changed in the lab**
- It reads the new materials: Shipped is now "Shipped (Breathe)".
- The old look is the **Classic** style. The 17 library styles are built on Classic, so they look
  exactly as they did in round 3.
- Direction, plate size and thickness now read *material values*. The Ship-it tab writes the
  generator's constants, which closes the loop: at the shipped settings it reproduces the six
  materials on disk.
- A new control, "Fade line 1.3", makes a breathing shell pop at the far end.

**Checked**

| Proof | Tool | Result |
|---|---|---|
| The assets encode the exported recipe | `omni_shepard_lab_parity.cjs round3.html new.html --old-set <recipe>` | ≤ 4 of ~66k lit pixels over 2/255, at 5 times |
| Classic is round 3's shipped look | `omni_shepard_lab_parity.cjs round3.html new.html --new-style classic` | 0 pixels over 2/255 |
| Negative control: shipped moved | `omni_shepard_lab_parity.cjs round3.html new.html` | 62k–65k of 82k–90k pixels differ |
| The shader's dials are inert at defaults (Mass crystal unchanged) | `omni_shepard_shader_parity.py` (clang++ `-Wall`, real mesh) | `v · s` to 1.0e-7 with plate centres, 6.3e-8 without |
| The shipped materials draw the lab's Breathe | same, vs `__lab.vertexAt`, 3 layers × 7 times × 360 vertices | max 3.2e-6 units (mesh radius 1.6) |
| Negative controls | same | `_Thickness` 1.25 not inert (8.7e-3); `_Breathe` off does not match (0.37) |
| Gates | `verify_lab.cjs`; `omni_shepard_lab_assets.py --check` / `--self-test`; `author_omni_crystal_triangles.py --check` | PASS; OK / 7 of 7; OK |

**Not verified:** no Unity editor or `unity` CLI was available, so `/verify-unity` did NOT run. Only
the vertex stage was compiled (by clang); Unity has not compiled the ShaderLab wrapper or the
fragment stage. Filed in `Docs/UNITY_VERIFICATION_CHECKLIST.md`. Two more gaps:
- **Draw order:** the three shells keep fixed queues 2999/3000/3001 while they breathe through each
  other. The lab sorts by radius each frame, so overlaps may layer differently in game.
- **Prisma:** the port's hand-translated `OmniShepardFresnelShader.glsl` still draws the falling look
  until an engine session ports the four dials. `Port/` is outside this branch's scope.

**Found**
1. **A lab's export is a build ticket only if it names the build.** Round 3's export pointed at "the
   shader extension the lab prototypes". Round 4's points at the generator's constants, because the
   generator owns these materials and a hand edit to a `.mat` would be reverted by its `--check`.
2. **Moving the shipped look moves every style that was a diff from it.** The library styles were
   stored as diffs from SHIPPED, so shipping Breathe would have quietly changed all 17. They are now
   pinned to a named base (Classic), and saved or liked looks store every knob.
3. **The round-3 parity tool shipped without its `require('playwright')` line.** I assembled it from a
   scratch copy with `sed '1,0d'`, which deleted line 1, and then ran the scratch copy instead of the
   committed file. Fixed, and this round ran the committed file.

## Round 3 (2026-10-10): many more looks

Feedback: "people don't like it, give me many more options". The decision log was empty, so this
round widens the space instead of tuning one look: a new effect model, 18 named styles, an endless
seeded random supply, a 3 × 3 compare grid, and shared likes and saves so the team can converge.

**What changed**
- **One generalised model.** Every shell copy loops over the whole trip and each triangle plate
  gets its own clock, path, spin and size. At the shipped knobs it collapses to exactly the three
  staggered material bands the game draws. Pixel parity against round 2: at most 10 of ~85k lit
  pixels differ by more than 2/255 (`omni_shepard_lab_parity.cjs`). The negative control (Swirl
  270°) changes 26k–80k pixels.
- **About 20 new knobs** in Stream / Motion / Shape / Light: direction (in / out / breathe), hover
  before the dive, stagger and its pattern (random, pole wave, sweep, spiral), swirl, tumble,
  scatter, shrink-with-distance, size at launch, thickness, fade style, blend (glass / glow),
  brightness, hue drift, landing flash, body pulse, trails.
- **Every knob says what it costs to ship:** a chip on each slider and each style reads
  *material values* (edit .mat / .prefab), *needs mesh*, or *needs shader*. The **Ship it** tab
  writes the material values, or the recipe plus what would have to be built.
- **Styles tab:** 18 styles, *Compare 1–9* / *10–18* / *9 random* / *Surprise me*, *Like*
  (shared through the page's `votes` collection), *Save to team gallery* (`styles` collection), and
  a *Most liked* ranking. Click a compare cell to open it in the editor.

**Scorecard** (seed 7, from `runBatch()`; "hidden" = that end of the loop cannot be seen to pop):

| Style | Ships as | Shells | Tris drawn | Trip (s) | Launch (u) | Seen from (u) | Speed in → land (u/s) | Visible tris (avg) | Launch hidden | Landing hidden |
|---|---|---|---|---|---|---|---|---|---|---|
| Shipped | material | 3 | 480 | 9 | 6.8 | 6.8 | 0.77 → 0.77 | 57.5 | yes | yes |
| Swirl | shader | 4 | 640 | 9 | 6.8 | 6.8 | 1.14 → 0.34 | 71.7 | yes | yes |
| Tumbling shards | shader | 3 | 480 | 7.5 | 6.8 | 6.8 | 0.93 → 0.93 | 57.3 | yes | yes |
| Meteor rain | shader | 2 | 1280 | 2.4 | 6.8 | 6.78 | 2.9 → 2.9 | 38.1 | yes | yes |
| Pole wave | shader | 2 | 320 | 5 | 6.8 | 6.8 | 1.39 → 1.39 | 38.2 | yes | yes |
| Lighthouse | shader | 1 | 160 | 4 | 6.8 | 6.8 | 1.74 → 1.74 | 19.2 | yes | yes |
| Breathe | shader | 2 | 320 | 8 | 6.8 | 6.76 | 2.16 → 2.16 | 38.3 | yes | yes |
| Emitter | shader | 4 | 640 | 6 | 6.8 | 6.8 | 1.16 → 1.16 | 76.7 | yes | yes |
| Implosion | shader | 1 | 160 | 3.5 | 11.68 | 11.68 | 7.52 → 7.52 | 19.6 | yes | yes |
| Glow stream | shader | 5 | 800 | 8 | 6.8 | 6.8 | 0.87 → 0.87 | 95.4 | yes | yes |
| Sparkle | shader | 4 | 192 | 4 | 6.8 | 6.76 | 1.74 → 1.74 | 22.8 | yes | yes |
| Assemble | shader | 2 | 320 | 6 | 10.98 | 10.98 | 2.79 → 0.93 | 37.5 | yes | yes |
| Heartbeat | shader | 2 | 320 | 2.8 | 6.8 | 6.8 | 6.49 → 1.81 | 37.5 | yes | yes |
| Petals | shader | 3 | 480 | 10.5 | 6.8 | 6.76 | 0.66 → 0.66 | 57.1 | yes | yes |
| Vortex | shader | 3 | 1920 | 3.6 | 6.8 | 6.8 | 1.94 → 1.94 | 57.5 | yes | yes |
| Slabs | shader | 4 | 640 | 8 | 6.8 | 6.8 | 0.87 → 0.87 | 76.7 | yes | yes |
| Dense stream | material | 6 | 960 | 9 | 6.8 | 6.8 | 0.77 → 0.77 | 115 | yes | yes |
| Glass contrast | material | 3 | 480 | 9 | 6.8 | 6.8 | 0.77 → 0.77 | 57.5 | yes | yes |
| One shell (baseline) | material | 1 | 160 | 9 | 6.8 | 6.8 | 0.77 → 0.77 | 19.2 | yes | yes |
| Lands short (control) | material | 3 | 480 | 9 | 6.8 | 6.8 | 0.62 → 0.62 | 60 | yes | **no** |

**Found**
1. **Only three of the 18 looks ship as material values** (Shipped, Dense stream, Glass contrast).
   Everything that moves triangles independently (stagger, swirl, tumble, scatter, hover, glow,
   trails) needs an `OmniShepardFresnelShader` extension. That extension would ALSO collapse the
   crystal's three layer materials into one material with a copy index.
2. **The scorecard caught two of my own styles popping.** Breathe faded in but never out, so it
   vanished at full opacity at the far point (fixed: the envelope now fades at both ends for
   breathe). Implosion's halo was born at alpha 0.10 (fixed: fade line 1.40, birth alpha 0.05).
   All 18 now hide both ends.
3. Additive "Glow" styles brighten wherever triangles overlap the body. Without bloom the lab
   understates how they would read in game.

**Decision needed (designer / team):** open the page, use Compare, and Like what you would ship.
The next round starts from the *Most liked* list and the decision log. If the winner needs the
shader, that is the build ticket. If not, the Ship it tab has the material values.

**Checked:** `verify_lab.cjs` PASS (desktop + phone); `omni_shepard_lab_assets.py --check` OK;
pixel parity with round 2 at the shipped settings, plus its negative control; compare grids of
styles 1–9, 10–18 and random 1–9 read as screenshots; the scorecard's *Lands short* control still
fails.

## Round 2 (2026-10-10): the shells were never drawn

Reported by the designer on the live page: "zero shells, just the main crystal, and weird
artifacts in the center from misdrawn vertices". Both were lab bugs; the game was not involved.

**Found**
1. **No shell ever rendered in round 1.** The shell shader declared `uS` in both stages, at
   `highp` in the vertex shader (the GLSL ES default) and `mediump` in the fragment shader. WebGL
   refuses to link a program whose shared uniform precisions differ, so every shell draw was
   skipped. Chromium reports that as a console WARNING, and `verify_lab.cjs` failed only on errors,
   so round 1 passed. Its "both screenshots were read" check was also wrong: the crystal in them was
   the body alone. Fix: `precision highp float` in both fragment shaders, and `program()` now throws
   on a failed link, which the page logs as a console error.
2. **The body's centre drew wrong.** The page derived its own face normals and flipped each one
   "outward" by testing it against the face centroid. The body is 30 hollow, bevelled rhombus plates:
   122 inner faces point at the centre and 480 wall faces point sideways (measured from the FBX). The
   flip turned the inner faces around, and on the walls the sign of a near-zero dot product picked
   bright or dark at random. Fix: the baker now carries the FBX's authored per-corner normals
   (`LayerElementNormal`, `ByPolygonVertex` / `IndexToDirect`; Unity imports them because
   `normalImportMode: 0`), and the page uses them as they are.

**Checked**
- `verify_lab.cjs` now fails on WebGL `INVALID_*` / link warnings. Its `--self-test` has a fifth
  plant (a draw with an unlinked program), and all five plants are named.
- Negative controls on this lab: the round-1 page from git now FAILS the gate ("program not linked").
  The fixed page with only the shell precision reverted fails with "Precisions of uniform 'uS'
  differ". The fixed page PASSES.
- Screenshots read at 30 u (shells streaming in around the body), with the body hidden (shells
  only), and at 16 u with the shells off (body plates, walls and gaps clean; no stray triangles).
- `omni_shepard_lab_assets.py --check` OK, and `--self-test` 6/6.
- The round-1 scorecard is unchanged. It is computed from the meshes and the formula and never
  depended on the draw.

## Round 1 (2026-10-10)

**What changed:** the lab exists. Sliders (Shells / Motion / Look / View tabs), six presets, a live
readout, the trip chart, a scorecard, the material-value export, and the decision log.

**Every knob maps to an asset field or says it doesn't:**

| Slider | Field | Ships without code? |
|---|---|---|
| Shells in flight | count of `OmniShepardTriangles` layers on `Crystal.prefab` | Yes, but each new layer is a prefab instance + active/inactive material pair + `crystalModels` entry |
| Triangles per shell | — | No: needs a mesh variant of `OmniCrystalTriangles.asset` (the export lists which plates) |
| Launch height / Landing scale | outermost `_Start` / innermost `_Stop` | Yes |
| Shell size | `OmniCrystalTriangles.prefab` `m_LocalScale` | Yes |
| Period | `_Period` | Yes |
| Landing ease | unequal `_Start`/`_Stop` bands per layer | Yes: the bands stay contiguous, so the shipped shader runs it seamlessly |
| Opacity, Rim power, Face forward | `_Opacity`, `_RimPower`, `_FaceForward` | Yes |
| Fade line | the `1.05` constant in `OmniShepardFresnelShader` | No: shader edit, and it also moves the rim shell and the Mass crystal |

**Scorecard** (seed 7; produced by `window.__lab.runBatch()` / `metrics()` on the page, measured
2026-10-10 through `verify_lab.cjs` and a Playwright probe):

| Variant | Shells | Launch (u above body) | Seen from (u) | Trip (s) | Speed in → land (u/s) | Spacing (u) | Shells seen | Land gap (u) | Landing hidden |
|---|---|---|---|---|---|---|---|---|---|
| Shipped | 3 | 6.80 | 6.80 | 9 | 0.77 → 0.77 | 2.32 | 2.86 | −0.17 | yes |
| One shell (baseline) | 1 | 6.80 | 6.80 | 9 | 0.77 → 0.77 | 6.97 | 0.95 | −0.17 | yes |
| Lands short (control, `_Stop` 0.6) | 3 | 6.80 | 6.80 | 9 | 0.62 → 0.62 | 1.86 | 3.00 | +1.22 | **no** |
| Dense stream (6 shells, 1.5 s) | 6 | 6.80 | 6.80 | 9 | 0.77 → 0.77 | 1.16 | 5.72 | −0.17 | yes |
| Settle in (ease 2, 5 shells, 1.8 s) | 5 | 6.80 | 6.80 | 9 | 1.39 → 0.15 | 2.51 → 0.28 | 3.95 | −0.17 | yes |
| Launch 1.6 (fade unchanged) | 3 | 15.16 | **7.36** | 9 | 1.70 → 1.70 | 5.11 | 1.41 | −0.17 | yes |
| Launch 1.6, fade 1.65 | 3 | 15.16 | 15.16 | 9 | 1.70 → 1.70 | 5.11 | 2.93 | −0.17 | yes |
| Shell size 1.2 | 3 | 11.61 | 11.61 | 9 | 1.04 → 1.04 | 3.12 | 3.00 | +2.23 | **no** |

Landing scale sweep (everything else shipped): `_Stop` 0.45 → gap −0.87 u (hidden), 0.52 → +0.11 u
(hidden, inside the 0.15 u tolerance), 0.55 → +0.53 u (visible pop).

**Checked**
- `verify_lab.cjs`: PASS (no console errors, desktop 1600 × 900 fits with no scroll or clipping,
  iPhone 13 has no sideways scroll and reads as a phone, manual clock advances, `runBatch` is
  deterministic). Stage is WebGL, so the blank check is skipped; both screenshots were read.
- The baked block matches the assets: `omni_shepard_lab_assets.py --check` OK; `--self-test` passes
  its six checks, including two planted defects (a non-Shepard material, a marker named in prose).
- The body mesh's frame matches the triangle mesh's: all 48 signed axis permutations of the FBX were
  scored by how evenly the plates land on the body at `_Stop` 0.5; the identity is the best fit
  (spread 0.011 local units), so the page uses the FBX vertices as-is.
- The negative control reads as a failure: **Lands short** reports a visible pop.

**Found**
1. **Launch height is capped by the shader, not the material.** Above `s ≈ 1.04` a triangle's alpha
   is below the 0.01 clip, so raising `_Start` alone adds invisible travel: at 1.6 the triangles are
   still first seen 7.36 u above the body (shipped: 6.80) while flying 2.2× faster and spaced 2.2×
   wider. A visibly higher launch needs the fade constant raised, which is a shader edit shared with
   the rim shell and the Mass crystal.
2. **Shell size moves the landing too.** `m_LocalScale` 1.2 raises the launch to 11.6 u but lands
   2.2 u short of the body, so the hand-off becomes a visible pop. Raise the launch with `_Start`
   (and the fade line), not with the prefab scale.
3. **The landing window is narrow:** `_Stop` between about 0.45 and 0.52 hides the hand-off; 0.55
   already pops.
4. **Easing is free.** Unequal bands keep the hand-off seamless on the shipped shader, so "fast in,
   settle onto the body" needs only material values (and enough layers to look smooth).

**Decision needed (designer)**
- How dense and how fast should the stream be (shells × period)? Shipped is 3 shells, 9 s trip.
- Should the triangles launch from higher up? If yes, that is a shader edit (Found 1).
- Fewer triangles per shell (a sparser, shard-like stream) needs a new mesh; is that wanted?

Record the choice in the page's **Decisions** tab; the next round starts by reading that log.

## What this lab does not model

Bloom and the rest of URP post-processing; blending happens in sRGB with a 2.2 gamma approximation.
Transparent sort order (Unity sorts these shells by object origin, the same point for all of them).
ThemeManager domain recolours (only the active and inactive materials). The game camera's FOV, the
vessel, the crystal's collect/explode materials, FadeIn on respawn. Frame cost on a real GPU.

## Graduation

Material values come from the **Export** tab. Edit both the active and the `Inactive` material of
each layer. More than three layers means new `OmniShepardTriangles (n)` instances on
`Crystal.prefab` with their own material pairs and `crystalModels` entries; fewer means removing
them. After any asset change, rebake the lab (`python3 Tools/Build/omni_shepard_lab_assets.py`) so
its "shipped" column follows the assets.
