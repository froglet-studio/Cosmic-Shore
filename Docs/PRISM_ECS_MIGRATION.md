# Prism ECS Migration — Assessment & Plan

**Status:** Assessment complete; Phase R is the launch-blocking work item.
**Context:** A full 3-player Skim Race (HexRace, mode 33) round ends in single-digit FPS.
Target: sustained 60 fps. This doc evaluates the ECS exploration on
`claude/ecs-migration-guide-Db42i` and lays out the migration plan.
**Companion docs:** `Assets/_Scripts/Game/Prisms/PRISM_PERFORMANCE_AUDIT.md` (original audit),
`Docs/SPATIAL_INDEX.md`, `Docs/ECOSYSTEM_MASTERPLAN.md` (scale ambitions this must serve),
`Docs/PRISM_ANIMATION.md` (**the clock-material law, LOCKED** — prism animation is one
initial-conditions stamp + GPU clock + one scheduled end swap; the per-instance
override components in §7 are the stamp's carrier, and the CPU animation managers this
doc Burst-batched are scheduled for retirement under that law).

---

## 1. Diagnosis — where the frames actually go

A Skim Race round accumulates trail prisms continuously: spawn interval is
`wavelength / speed` (`VesselPrismController.cs:167`, wavelength 4, clamp 0–3 s) →
~12–20 prisms/sec/vessel at race speeds. Three vessels over a 30–90 s round ≈
**3,600–4,000+ live prisms at round end** (the Skim Race cell's forager food web —
tadpoles + brittlestars, `Skim Race Cell Config.asset` — grazes some of it back, but
consumption lags spawning during a race). FPS decays monotonically with that count,
so the bottleneck is whatever scales **linearly per live prism per frame**.

Walking the per-prism cost structure on this branch:

| Cost | Scales with live count? | State |
|---|---|---|
| Scale / material / VFX animation | No — only *animating* subset | ✅ Burst-batched (`PrismScaleManager`, `MaterialStateManager`, `PrismEffectsManager`, batch 128) |
| Shield timers | No — ≤64 active | ✅ Centralized (`PrismTimerManager`) |
| Physics broadphase | No — bounded | ✅ `PrismColliderLodManager` culls colliders >200 m from any focus (commit `9cc209257`) |
| AOE damage queries | Yes, but Burst | ✅ `PrismSpatialIndex` 16 B hot array, SIMD scan, 48-hit/frame cap |
| Per-prism `Update()` loops | — | ✅ None exist (audit recs 1, 4, 5, 6 all landed) |
| Material clones / GC | — | ✅ Fixed — `sharedMaterial` everywhere |
| **Rendering** | **Yes — every frame** | ❌ **~1 draw call + SetPass per prism** |

**The unfixed linear cost is rendering** (audit recs 2 & 3, explicitly noted
"remain unimplemented"). The chain:

1. All prisms of a domain share one material (`ThemeManager.GenerateDomainMaterialSet`),
   which *should* SRP-batch — but every prism gets a per-renderer
   `MaterialPropertyBlock` (`MaterialPropertyAnimator.cs`, applied at
   `MaterialStateManager.cs:139`), and **any renderer with an MPB is excluded from the
   SRP Batcher**.
2. The materials have `m_EnableInstancingVariants: 1` but nothing draws instanced —
   zero `RenderMeshInstanced`/BRG usage for prisms (the only precedent in the repo is
   `CapsuleMembrane.cs:166`).
3. Net: **N live prisms ≈ N draw calls + N SetPass per frame**. At 4,000 prisms the
   main-thread/render-thread submission cost alone explains single digits, before
   counting transparent-prism overdraw and the heavyweight `UnstablePrismGraph`
   ShaderGraph. Explosion/implosion VFX add up to 64 more full-mesh draws
   (`PrismFactory.cs:29-30` caps).

The CPU simulation side is already effectively data-oriented: NativeArray hot/cold
layouts, Burst jobs, centralized lifecycles. **What ECS buys us is not faster
simulation — it's the rendering model (BatchRendererGroup via Entities Graphics) and,
later, removal of per-prism GameObject overhead.** Any plan that doesn't put the
render path first is treating the wrong organ.

---

## 2. Review of `claude/ecs-migration-guide-Db42i`

What's on the branch: `Docs/ECS-Migration-Guide-Prisms.md` (phased plan),
`PrismEntityBridge.cs`, `AOEComponents.cs`, `PrismComponents.cs`, and a modified
`PrismAOERegistry` with a `UseECS` toggle. Phase 0 ("completed" there) gives each
prism a companion entity mirroring the AOE registry's spatial/damage data; the ECS
query path does `ToComponentDataArray` → `Reinterpret` → the *same* Burst job.

**Keep (conceptually sound):**
- The phase framing: hybrid bridge → animation systems → effects → full entity → networking.
- The "what NOT to migrate" list (vessels, UI, camera, SOAP, scoring) — exactly right.
- The networking call: prisms are **not networked** (verified: no `NetworkObject` on
  any prism prefab; trails spawn locally and deterministically per client from
  replicated vessel motion). Netcode is a non-issue for this migration.
- The `IEnableableComponent` component shapes — already ported to this branch as
  `Assets/_Scripts/Controller/ECS/Components/PrismComponents.cs`.

**Reject (do not merge the branch):**
1. **It's stale by two generations.** Based on the old `development` line: references
   `PrismAOERegistry` (now `PrismSpatialIndex`, heavily evolved: occupancy
   reservations, bucket grid, cell binding, `CopyLivePrisms`), VContainer (now
   Reflex), pre-reorg `_Scripts/Game/` paths, and predates `PrismEffectsManager`,
   `PrismTimerManager`, and the collider-LOD work. The code does not rebase; only the
   ideas transfer.
2. **Phase 0 is a perf regression, not a win.** It double-books every prism (legacy
   arrays + entities, both always updated), and its query path *copies* entity data
   into arrays the registry already holds natively. The AOE path was never the
   bottleneck — it's already a 16 B-packed Burst scan.
3. **It defers rendering to late phases.** The actual fire is draw submission;
   the guide's Phase 0–1 deliver no frame-time improvement for Skim Race.

---

## 3. Risks & benefits of the ECS approach (done right)

### Benefits

| Benefit | Magnitude | When |
|---|---|---|
| Draw calls: ~4,000 → ≤~56 instanced batches (7 prism meshes × 4 domains × opaque/transparent; realistically far fewer live at once) | **The fix.** Render submission from tens of ms to ~1 ms; SetPass churn collapses | Phase R |
| Per-instance properties (`_BrightColor`, `_DarkColor`, `_Spread`, explosion params) move from per-renderer MPB writes to persistent GPU instance buffers | Removes the thing that breaks batching *and* the per-animating-prism `SetPropertyBlock` main-thread loop | Phase R |
| Explosion/implosion VFX ride the same instanced path | 64 draws → same batch | Phase R |
| Existing Burst jobs port nearly line-for-line to `IJobEntity` (the guide is right that the codebase is "~60% DOTS-native") | Consolidation; deletes bespoke manager plumbing | Phase 2 |
| Pure-entity prisms: ~90% memory reduction, no GameObject spawn/SetActive churn, 10–50k prism ceiling | Strategic — this is what makes `ECOSYSTEM_MASTERPLAN` scale (credible artificial life needs population headroom) | Phase 3 |

### Risks

| Risk | Severity | Mitigation |
|---|---|---|
| **BRG requires Vulkan/Metal — no GLES3.** Android is configured Vulkan + GLES3 fallback (`ProjectSettings.asset`, automatic APIs) | **Launch-gating product decision** | Either raise min-spec to Vulkan (the overwhelming majority of active Android devices) or keep the legacy MeshRenderer path as a GLES3 fallback (the A/B toggle gives us this for free). Decide in Phase 0. |
| ShaderGraph properties must be DOTS-instancing compatible for material overrides | Medium | `UnstablePrismGraph` uses default property declarations; modern ShaderGraph (URP 17 / Unity 6000.0.62f1) auto-generates DOTS-instanced variants. **Verify in-editor first** — if it fails, the fallback is a hand-written DOTS-instanced HLSL port of the prism shader (contained, known work). |
| `public struct Entity` in `CosmicShore.Gameplay` (`BoidSimulationController.cs:12`) collides with `Unity.Entities.Entity` | Low (compile-breaking but trivial) | Rename to `BoidEntity` before any system code lands. |
| Hybrid dual-source-of-truth drift (GameObject state vs entity state) | Medium | One-way sync only (GameObject → entity), event-driven at the existing manager choke points (spawn, growth tick, settle, state change, destroy). Never read gameplay state back from entities in Phases R–2. |
| **Continuity-of-existence law** — bloom/wither/explode/implode/shield visuals must be pixel-faithful through the cutover (platform-wide law, no exceptions) | High if fumbled | All these are already driven by shader params + scale, which the instanced path carries per-instance. Build a side-by-side visual parity checklist into Phase R exit criteria. |
| Phase 3 (pure entity, no GameObject) touches an enormous gameplay surface: `PrismImpactor` pipeline, skimmer triggers, fauna grazing (`Fauna` eats `Prism`), `TrailFollower` (walks `List<Prism>` — logical, not physical, verified), `PrismOctahedronShield` collider swaps, `LifeFormCrystal` | **High — months, regression-prone** | **Do not gate launch on it.** Phases R–2 don't need it. Phase 3 is post-launch, justified only by ecosystem scale targets. |
| Entities source-gen on the default assembly (no runtime asmdefs) | Low | Works; adds compile time. Optional later: asmdef carve-out for `Controller/ECS/`. |
| Team debuggability/iteration in ECS | Low for R–2 | Entities are created at runtime (no baking/subscenes); Entities hierarchy/inspector windows cover debugging. Gameplay code stays MonoBehaviour until Phase 3. |

---

## 4. The plan

Sequenced so each phase pays for itself and launch never depends on the long tail.

### Phase 0 — Measure & de-risk (2–3 days)

1. Run the Performance Benchmark tool (`BENCHMARK_TOOL.md`) through a full 3-player
   Skim Race round: record frame time, draw calls, SetPass, and live-prism count
   (GameLoadSampler) over time. Expected: draws ≈ live prisms, frame time tracking
   draws. This is the baseline chart every later phase is judged against.
2. Repeat on a representative Vulkan Android device. Note GPU-bound vs CPU-bound split
   (transparent overdraw may add a GPU floor that instancing alone won't fix — if so,
   that's a separate, smaller workstream: opaque-at-distance for prism transparency).
3. **Decide the GLES3 policy** (min-spec Vulkan vs. keep legacy path as fallback).
4. Pre-work commits: rename `BoidSimulationController.Entity` → `BoidEntity`;
   in-editor spike proving `UnstablePrismGraph` properties override per-instance under
   Entities Graphics (one cube, one entity, one `[MaterialProperty("_BrightColor")]`
   component).

**Exit:** baseline curve + shader spike green + GLES3 decision recorded here.

### Phase R — Rendering cutover via Entities Graphics (2–4 weeks) ⟵ **the launch item**

Companion entity per prism that owns **rendering only**. Gameplay, physics, triggers,
trails, ecosystem — all stay exactly as they are on GameObjects.

- On `Prism.Initialize`/pool-checkout: create entity with `LocalToWorld`,
  `MaterialMeshInfo`/`RenderMeshArray`, and material-override components
  (`_BrightColor`, `_DarkColor`, `_Spread`, opacity/explosion params); disable the
  GameObject's `MeshRenderer`. On pool-return/destroy: destroy/disable entity.
- Transform sync is **event-driven, not per-frame**: prisms are static after growth.
  `PrismScaleManager` writes `LocalToWorld` while a prism animates; settled prisms
  cost zero. (`PrismSpatialIndex.UpdatePosition` call sites are the complete list of
  movers — gyroid steering, fauna bodies.)
- `MaterialStateManager` writes color-override components instead of
  `SetPropertyBlock` (same Burst job output, different sink). `PrismEffectsManager`
  feeds explosion/implosion entities the same way; the per-effect GameObjects stop
  rendering.
- Keep the legacy MeshRenderer path behind a config toggle (A/B in the benchmark, and
  the GLES3 fallback if Phase 0 chose to keep one).

**Exit criteria:** full 3-player Skim Race round holds ≥60 fps on the target device;
benchmark shows draws decoupled from prism count; side-by-side visual parity for
bloom-in, wither, explode, implode, shield engage/shatter, domain re-theme, danger
state. No gameplay code paths changed.

### Phase 2 — Lifecycle systems become ECS systems (2–3 weeks; stretch goal for launch)

Port `PrismScaleManager` / `MaterialStateManager` / `PrismEffectsManager` /
`PrismTimerManager` job bodies to `IJobEntity` systems over the Phase R entities
(`ScaleAnimation`, `MaterialAnimation`, `ShieldTimer` enableable components already
scaffolded in `Controller/ECS/Components/PrismComponents.cs`). The per-prism
MonoBehaviours (`PrismScaleAnimator`, `MaterialPropertyAnimator`) shrink to thin
registration shells. Mechanical, per the exploration guide — its Phase 1–2 mapping
table is accurate and reusable here.

### Phase 3 — Pure-entity prisms (post-launch; 1–2 quarters honest)

Remove the prism GameObject. Contact detection moves from PhysX triggers to
`PrismSpatialIndex` queries (it is already THE canonical store — skimmer becomes a
`QuerySphere`, impactor dispatch becomes an adapter over hit indices); `Trail` becomes
an entity-reference list; `PrismOctahedronShield`, fauna grazing, and the impactor
matrix get entity-facing adapters. Vessels stay hybrid GameObjects (exploration
guide's Phase 4 option 1 — correct). **Trigger:** only when ecosystem population
targets (10k+ prisms) or spawn-churn costs demand it — not for the 60 fps goal.

### Branch disposition

- **Do not merge** `claude/ecs-migration-guide-Db42i`. Keep it as a reference;
  its guide's phase tables inform Phases 2–3. Its Phase 0 implementation
  (AOE-mirror companion entities, dual bookkeeping) is superseded by Phase R above.
- `PrismComponents.cs` already lives on this line at
  `Assets/_Scripts/Controller/ECS/Components/` — extend it in place.

---

## 5. Implementation log (this branch)

Direction per product owner: PC-first, full conversion on this branch, targeting
10x–1000x prism counts. If it works it merges; if not, nothing ships from here.

### Checkpoint A — instanced rendering cutover (DONE, needs in-editor verification)

What landed:

| Piece | File |
|---|---|
| Per-instance shader overrides (`_BrightColor` f4 / `_DarkColor` f4 / `_Spread` f3, matched to UnstablePrismGraph declarations) | `Controller/ECS/Rendering/PrismRenderProperties.cs` |
| Bridge service: companion entity per prism, mesh/material registration, visibility, transform/color/material sync, epoch-validated handles, master toggle | `Controller/ECS/Rendering/PrismRenderService.cs` |
| Config toggle asset (optional, Resources/PrismRenderConfig; defaults ON) | `ScriptableObjects/PrismRenderConfigSO.cs` |
| Max-prism stress harness: N pure render entities, churn knobs, fps overlay | `Controller/ECS/Rendering/PrismRenderStressTest.cs` |
| Visibility router (`SetRenderVisible`/`ApplyRenderPath`), entity lifecycle, mover sync | `Controller/Vessel/Prism.cs` |
| Growth animation → entity matrix sync | `Controller/Managers/PrismScaleManager.cs` (+ `OwnerPrism` on `PrismScaleAnimator`) |
| Animated colors → entity override sink + tracked current colors (replaces MPB read-back) | `Controller/Managers/MaterialStateManager.cs`, `Controller/Environment/Prisms/MaterialPropertyAnimator.cs` |
| Octahedron shield ↔ instanced path handoff (morph mesh = GameObject renderer while engaged) | `Controller/Vessel/PrismOctahedronShield.cs` |
| `Entity` name-clash fix: boid struct renamed `BoidEntity` | `Controller/Environment/FloraAndFauna/BoidSimulationController.cs` |

Design rules encoded:
- **One visibility truth.** All former `meshRenderer.enabled` writes route through
  `Prism.SetRenderVisible`; entity and MeshRenderer can never both draw.
- **GameObject fallback is always whole.** Toggle off (config asset or
  `PrismRenderService.SetRuntimeOverride(false)`) or any missing ECS prerequisite
  → identical legacy behavior, per prism.
- **Exotic geometry falls back per prism.** Octahedron morph/shatter renders via
  the GameObject; everything else batches.
- **Movers stay honest** via the existing `NotifyPositionChanged` contract (fauna
  bodies, gyroid steering) — same hook the spatial index already requires.

In-editor verification protocol (Checkpoint A):
1. Open any gameplay scene (MinigameHexRace), enter play, fly and lay trail.
   Expected: prisms render identically (bloom-in growth, domain colors, theme
   transitions, shield engage/shatter, danger tint, destruction hiding the block).
2. Frame Debugger / Stats: SetPass + draw calls must NOT grow with trail length —
   prism draws collapse to one instanced batch per (mesh × material). Compare by
   flipping `PrismRenderService.SetRuntimeOverride(false)` (legacy) vs `(true)`.
3. Entities Hierarchy (Window > Entities): live entity count ≈ live prism count.
4. Watch for `[MaterialProperty]` size-mismatch errors on first prism spawn — if
   `_Spread` errors, the SG declaration changed from Vector3 and
   `PrismSpreadOverride` must match.
5. Octahedron: super-shield a prism (collide crystal) — bloom morph plays on the
   GameObject, returns to batched rendering after shatter.
6. Stress harness: empty scene + camera + `PrismRenderStressTest` (assign prism
   mesh/material from a prism prefab) — 100k static entities should hold 60+ fps
   on a mid PC; raise until it breaks to find the ceiling.
7. Known API to double-check on first compile: `RenderMeshUtility.AddComponents(
   entity, em, in RenderMeshDescription, MaterialMeshInfo)` — if EG 1.4.15 only
   ships the `RenderMeshArray` overload, swap to it with a single-element array
   (one-line change in `PrismRenderService.Create` / stress test).

Caveats (accepted for this checkpoint):
- Transparent prisms: BRG sorts transparency at coarser granularity than
  per-renderer — possible minor sort-order differences in dense overlap.
- Entities Graphics requires Vulkan/Metal/DX — PC targets fine; the Android GLES3
  decision from §3 still stands for mobile.

### Checkpoint B — explosion/implosion VFX on entities (DONE, needs in-editor verification)

`PrismEffectsManager` already computed everything in Burst; its per-renderer
`GetPropertyBlock`/`SetPropertyBlock` sinks now route into companion-entity
overrides, so a 64-effect swarm-eat storm renders as one instanced batch per
effect material instead of 64 draws + SetPass.

| Piece | File |
|---|---|
| Effect override comps: `_Velocity` f3 / `_ExplosionAmount` f1 / `_Opacity` f1 (ExplodingBlockGraph), `_State` f1 / `_Location` f3 (SuctionGraph) — sizes verified against both graphs | `Controller/ECS/Rendering/PrismRenderProperties.cs` |
| `PrismRenderOverrideSet` on Create + `SetTeamColors` / `SetExplosionParams` / `SetImplosionParams` | `Controller/ECS/Rendering/PrismRenderService.cs` |
| Companion entity per pooled effect, pending team colors, no-flash first-frame show (`EnableVisual`), hide on complete/disable, destroy with GameObject | `Utility/Effects/PrismExplosion.cs`, `Utility/Effects/PrismImplosion.cs` |
| Per-frame param routing (entity vs legacy MPB), entity-aware zombie audit | `Controller/Managers/PrismEffectsManager.cs` |
| `ConfigureForTeam` hands the palette to the effect (path-agnostic) instead of writing MPB directly | `Controller/Prisms/PrismFactory.cs` |

Verification additions:
8. Destroy a wall of prisms (AOE) — explosions fly/fade identically, implosions
   suck toward the consumer identically; Frame Debugger shows effect draws
   collapsed into ≤2 instanced batches. Zombie audit lines stay quiet.
9. `EcosystemPerfProbe` now logs `ents=` (live companion entities) next to
   `prisms=` — ents≈prisms confirms the batched path is carrying the population;
   ents=0 means legacy fallback engaged.

### Post-review hardening (DONE)

A 7-angle adversarial review of Checkpoints A+B confirmed four issues, all
fixed: orphaned visible entity on pooled release of a shielded prism
(undefined OnDisable order — `ApplyRenderPath` now never shows an entity on an
inactive GameObject); handoff color continuity in both directions
(`FlushDisplayedColorsToRenderer` / `SyncRenderColorsFromAnimator`);
late-material entity-creation retry in `SyncRenderMaterial`; effect
`_hasPendingTeamColors` cleared on `OnDisable` (pool-reuse parity with the MPB
clear). Refuted (no change): stale-epoch color suppression, runtime-override
stranding, watchdog/manager desync.

### Checkpoint C — population-independent collider-LOD sweep (DONE)

The previous sweep was O(N): `CopyLivePrisms` over the whole population +
per-prism `SetColliderCulledByLod` every 0.25 s — invisible at 4k prisms, a
recurring multi-ms main-thread spike at 100k. Now transition-based:

- `PrismSpatialIndex.LiveCount` — O(1) live counter maintained by the four
  lifecycle methods (mirrors `CopyLivePrisms`' JobPassValue semantics).
- `PrismColliderLodManager.Sweep` touches only near/far TRANSITIONS
  (double-buffered near sets); one O(N) reconciliation runs when LOD (re)gains
  an opinion (first sweep, foci returning after idle, re-enable).
- `PrismColliderLodManager.NotifyPrismActivated` (called from the spawn-window
  end and `Prism.Restore`) classifies colliders that come online between
  ticks — O(foci), no population walk.

Collider-budget impact: none — the same prisms end in the same collider states;
only the cost of computing them changed (O(near + transitions) steady state).

Verification addition:
10. In a long Skim Race round, the `[ECOSIM] colliders=near/live` line must show
    the same near counts as before this change, and the profiler must show no
    0.25 s-cadence spike from `PrismColliderLodManager.Sweep` as live counts
    grow.

### Checkpoint D — pure-entity bulk mass (NEXT, design locked, do after A–C verify in-editor)

The remaining ceiling at 100k+ is GameObject existence itself: ~2–4 KB and a
pooled instantiation per prism. Design (Phase 3 of §4, sequenced):

1. **Spawn-side split.** `PrismFactory` gains a bulk tier: trail/track prisms
   spawn as pure entities (render entity + `PrismSpatialIndex` entry + a
   compact state record) with NO GameObject. The spatial index already carries
   position/flags/volume/domain — it becomes the single gameplay truth for
   bulk prisms (it already is for AOE, occupancy, fauna senses, cell grids).
2. **Contact via the index, not PhysX.** Skimmer/hull/projectile contact with
   bulk prisms uses `QuerySphere` against the index (the canonical pattern per
   Docs/SPATIAL_INDEX.md) instead of trigger colliders. The impactor pipeline
   gets an index-hit adapter that synthesizes the same effect-SO dispatch.
3. **Promote on touch.** Interactions that need the full MonoBehaviour
   (TrailFollower attach, octahedron shield, steal/restore mechanics) promote
   a bulk prism to a pooled GameObject on first contact — the existing pool
   provides the body; the entity + index entry persist across the promotion,
   so continuity-of-existence is never violated.
4. **Trail holds handles, not MonoBehaviours.** `Trail`'s `List<Prism>` becomes
   a list of stable prism handles (index slot + entity) with a thin accessor
   that promotes on demand for the few blocks actually being ridden.

Gate: only start after Checkpoints A–C hold 60 fps in a full 3-player Skim
Race on PC and the stress harness ceiling is measured — if 100k-with-
GameObjects already exceeds the design target, D is optional headroom.

---

## 7. Root cause of the in-editor breakage (colors mixed up / explosions frozen) — FIXED

**Symptom:** with the instanced path on, prism colors looked uniform/mixed and
explosions rendered as a static fully-exploded blob instead of animating.

**Root cause:** the per-instance `[MaterialProperty]` overrides were not reaching
the shaders. Entities Graphics only drives a ShaderGraph property per-instance when
that property is declared **Hybrid Per Instance** (`hlslDeclarationOverride: 3`).
An audit found that across the four prism shaders, ONLY `ExplodingBlockGraph._Velocity`
was marked Hybrid Per Instance — every animated property
(`_BrightColor`/`_DarkColor`/`_Spread`/`_ExplosionAmount`/`_Opacity`/`_State`/`_Location`)
was `Default`. So each entity read the *shared material constant*: explosions showed
`ExplodingBlockMaterial`'s baked `_ExplosionAmount: 20.7` (frozen), and prisms lost
per-instance color variation (mixed up). This was the day-one risk flagged in §3.

It also corrected a wrong shader map from the initial scaffold: the main prism block
materials use **`BlockGraph`** (`bf8c159f…`), not `UnstablePrismGraph`. Explosions use
`ExplodingBlockGraph`, implosions `SuctionGraph`.

**Fix applied (this commit):** set Hybrid Per Instance on the driven properties in all
three shaders, pattern-matched to the already-working `_Velocity` and to the repo's
existing `CrystalGraph`/`SkimmerGraph` precedent:
- `BlockGraph`: `_BrightColor`, `_DarkColor`, `_Spread`
- `ExplodingBlockGraph`: `_BrightColor`, `_DarkColor`, `_ExplosionAmount`, `_Opacity`, `_Spread`
- `SuctionGraph`: `_BrightColor`, `_DarkColor`, `_State`, `_Location`, `_Spread`

Hybrid Per Instance is **additive** — it adds a `DOTS_INSTANCING_ON` variant used only
under BatchRendererGroup; the normal MeshRenderer + MaterialPropertyBlock path is
unchanged. So these shader edits are safe for the legacy path too.

**Path is now opt-in (default OFF).** `PrismRenderConfigSO.useInstancedRendering`
defaults false and `PrismRenderService` defaults off, so the proven legacy path is the
baseline and a build is never broken by default. The fixed ECS path is one flag away.

### In-editor verification loop (do these in order)

1. **Reimport shaders.** Focus the editor (or right-click the three `.shadergraph`
   files → Reimport) so the `DOTS_INSTANCING_ON` variants compile.
2. **Isolated shader check FIRST.** Empty scene + camera + `PrismRenderStressTest`
   (assign a prism mesh + a `BlockGraph`-based material). Play. If instances show
   *varied* colors → per-instance overrides reach the shader (shader fix worked). If
   all identical → the shader still isn't honoring per-instance; recheck the reimport
   and the `hlslDeclarationOverride: 3` flags. This isolates shader-vs-integration.
3. **Enable the path.** Create a `PrismRenderConfig` asset in a `Resources/` folder
   (Create ▸ ScriptableObjects ▸ Rendering ▸ Prism Render Config), tick
   *Use Instanced Rendering*. Or call `PrismRenderService.SetRuntimeOverride(true)`.
   On first active frame the console logs `[PrismRenderService] … ACTIVE`.
4. **Gameplay check.** MinigameHexRace: fly, lay trail, destroy prisms. Colors,
   bloom-in, theme transitions, shield engage/shatter, explosions (animating now),
   implosions must match the legacy path. A/B with the toggle.
5. **Residual risk to watch — Vector3 override size.** `_Spread`/`_Location`/`_Velocity`
   are float3 components against Hybrid-Per-Instance Vector3 props (matches the working
   `_Velocity`). If the console logs a `MaterialProperty` size mismatch for one of them,
   change that override struct in `PrismRenderProperties.cs` from `float3` to `float4`
   (set `.w = 0`). Colors (float4) and scalars (float) are unaffected.

## 6. Decision summary

| Question | Call |
|---|---|
| Is ECS the right direction? | **Yes** — but as the vehicle for instanced rendering first, simulation consolidation second, pure entities last. The simulation is already data-oriented; rendering is the fire. |
| Merge the exploration branch? | No. Stale base, perf-neutral-at-best Phase 0. Salvage the guide's framing. |
| What gates launch? | Phase 0 + Phase R only. Phase 2 if schedule allows; Phase 3 explicitly post-launch. |
| Biggest open product decision | Android GLES3: raise min-spec to Vulkan, or keep the legacy render path as fallback (the A/B toggle makes "both" cheap). |
| Biggest technical unknown | ShaderGraph → DOTS-instanced property overrides on `UnstablePrismGraph` (Phase 0 spike, day one). |
