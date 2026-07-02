# Performance Refactor Review — Verified Candidates (July 2026)

> **Execution status (updated as fixes land on this branch):**
> - **DONE — Phase 1:** dead-code purge (`CurrentScore`, `CellControlTurnMonitor`,
>   `BoidSimulationController` + metas); D1 (`GetBlockIndex`), D2 (single turn-monitor
>   driver + de-LINQ), D3 (allocation-free `GameDataSO` lookups — loop form; the
>   dictionary variant deferred), D4 (`Cell` static domain arrays), D8 (HexRace score
>   throttle 10 Hz), D10 (`ResourceSystem` editor guards + cached gain tick), D11 (Boid
>   cached wait), D12 (`VesselController` change-checked writes + honest benchmark
>   counter), D13 (`LightFauna` wither de-LINQ).
> - **DONE — Phase 2:** B3 (`DomainVolumeIndicator` sub-canvas + invisible-gauge gate +
>   ring quantization); B2 (`Spindle` MPB death animation — clone path deleted;
>   `Crystal` double-clone + per-collection leak fixed); B1 partial
>   (`SpaceCrystalAnimator` visibility gating + `CrystalSpace.prefab`
>   `UpdateWhenOffscreen: 0` — **verify in editor**: culling pop risk if baked bounds
>   are tight; full shader-driven conversion still open).
> - **DONE — Phase 2/3 batch 2:** `Trail.LookAhead` non-alloc overload (+ Skimmer and
>   SkimmerAlign callers on scratch buffers; `Skimmer.DrawCircle` computes one shared
>   prism list per circle instead of per segment); `Branch` IEquatable + HashSet→List
>   in AssembledFlora/BranchingFlora (kills reflection-boxing + the `ElementAt` O(n²));
>   `WallAssembler` mate-candidate cap (32 — bounds the 1 Hz sweep AND the
>   conversion blast radius; Gyroid precedent); `GameEventFeed` row pooling
>   (recycle instead of Instantiate/Destroy per event); `MaterialBlendUtility`
>   overlay tracking (fixes one-material-instance-leak-per-blend + array growth on
>   pooled prisms), shared MPB, cached shader IDs; `SkimmerOverchargeCollect` cached
>   layer mask + in-place sort.
> - **OPEN:** A1 (prism ECS/instancing), A2 (AOE explosion pooling + batched
>   grow + projectile movement manager), B1 full shader-driven crystal animation, B4
>   (fauna scheduler — gated on ecology scaling).

**What this is.** A codebase-wide review answering: *which systems would produce the most
performance gain if completely refactored?* Six subsystem audits (ecosystem, vessel core, UI,
projectiles/FX/assemblers, AI/arcade, codebase-wide anti-pattern sweep) produced 18 candidate
findings; every finding was then **independently adversarially verified** against the actual
code — line citations, throttle/gating checks, and scene/prefab GUID wiring checks. Several
plausible-looking "hot paths" turned out to be dead code or already-mitigated; several others
turned out worse than first reported. Only verified findings appear below.

**Companion docs:** `Assets/_Scripts/Game/Prisms/PRISM_PERFORMANCE_AUDIT.md` (prism system),
`Docs/SPATIAL_INDEX.md` (spatial queries), `Docs/ECOSYSTEM_MASTERPLAN.md` §4 (collider budget).

---

## The headline: this codebase is past the easy wins

The historically hottest per-entity paths have already been refactored to the target
architecture and should be **left alone**:

- **`PrismSpatialIndex`** — Burst AOE/occupancy/proximity queries, no physics. AOE damage is
  fully migrated (`AOEExplosion` batches via `ExplosionImpactor.ProcessBatchFrame`, prism layer
  excluded from its collider). Assemblers migrated off `Physics.CheckBox`/`OverlapSphere`.
- **`PrismEffectsManager`** — explosion/implosion animation is Burst-jobbed and batch-applied
  (audit rec #1 **shipped**; `PrismExplosion` only registers state, no per-effect loop).
- **`MaterialPropertyAnimator`** — now uses `sharedMaterial` (audit rec #4 **resolved**; the
  in-repo audit doc is stale on this).
- **`PrismScaleManager` / `MaterialStateManager` / `AdaptiveAnimationManager` /
  `PrismTimerManager` / `PrismColliderLodManager`** — Jobs+Burst batching, frame-skipping,
  centralized timers, proximity collider culling.
- **Fauna sensing** — goals come from the cached Burst `BlockDensityGrid`, consumption from
  `PrismSpatialIndex.QuerySphere`; the only remaining fauna physics probe is a masked
  vessel-only `OverlapSphereNonAlloc`.
- **HUD event plumbing** — per-vessel HUD views are event-driven (no polling);
  `ObjectiveIndicator` is the model UI widget (own sub-canvas, string caching, alpha-only pulse).
- **Textbook anti-patterns are absent from hot paths**: zero `SendMessage`, no
  `FindObjectOfType`/`Camera.main`/LINQ in any per-frame `Update` (verified sweep), projectiles
  and prisms pooled.

What remains splits into: **(A) two structural refactors with the largest headroom**,
**(B) three bounded medium refactors that are live costs today**, **(C) a dead-code purge that
removes latent perf traps**, and **(D) verified spot fixes**. And, just as important, **(E) a
list of refuted findings** so nobody re-chases them.

---

## A. Structural refactors — largest headroom

### A1. Prism GameObject architecture → incremental DOTS/ECS + instanced rendering *(largest ceiling, already roadmapped)*

Still the single biggest headroom in the project, unchanged from
`PRISM_PERFORMANCE_AUDIT.md` recs #2/#3/#7: each prism is a full GameObject with 5–6
MonoBehaviours + BoxCollider + MeshRenderer (~12,000 MonoBehaviours + 2,000 colliders at 2,000
prisms; production cells reach 10,000+). The animation *compute* is now batched (Burst), but
the *object model* is not: rendering is one MeshRenderer per prism (no
`RenderMeshInstanced`/Entities Graphics), and PhysX still broad-phases every non-culled
collider. The packages (`com.unity.entities` 1.4.2) are installed.

- **Refactor shape:** exactly the audit's phased plan — instanced/ECS rendering first, then
  spatial-hash collision replacing colliders (extending `PrismSpatialIndex`, which already owns
  occupancy), leaving GameObjects only as thin interaction proxies. Coordinate with
  `ECOSYSTEM_MASTERPLAN.md` §4 (collider budget / collider-LOD by phase) — same end state.
- **Expected gain (from the audit, still valid):** prism system cost ~15–25 ms/frame →
  ~2–5 ms/frame at 2k prisms; 10–50× capacity ceiling.
- **Status:** audit recs #1 (Jobs explosion manager) and #6 (timers) are DONE; #2 (instanced
  explosion rendering), #3 (ECS), #7-remainder (collider replacement beyond LOD culling) are
  NOT. The audit doc's "recs 1-3 unimplemented" line is stale on #1.

### A2. AOE explosion lifecycle — pool detonations, batch per-block growth *(hot today, clear pattern to copy)*

The one *live* subsystem still doing per-event object churn at combat rates. Verified:

- **Every detonation is `Instantiate` + reflection DI + `Destroy`:**
  `ExplosionHelper.SpawnAllAndDetonate`
  (`Assets/_Scripts/Controller/ImpactEffects/EffectsSO/Helpers/ExplosionHelper.cs:77-81`) does
  `Object.Instantiate(prefab)` + `GameObjectInjector.InjectRecursive` (reflection walk) per
  explosion; `AOEExplosion.ExplodeAsync` ends with `Destroy(gameObject)` (`AOEExplosion.cs:267`).
  The same pattern repeats at `ProjectileDetonatorSO.cs:90-92` — **invoked per projectile end
  on Sparrow full-auto** (via `DetonateSparrowProjectileEndEffect`), i.e. at fire-rate
  frequency — and at `VesselDangerBlockFormationBySkimmerEffectSO.cs:64-67`.
- **Per-block frame-yielding UniTasks:** `AOERadialBlocks.cs:185` spawns one `GrowToScale`
  task per block (wired: `AOEConicSkyBurst.prefab` = 72 blocks/burst, ~200 frames each) — and
  the Prism's own scale-grow runs in parallel with it; `AOEDangerHemisphereBlocks.cs:203`
  spawns `MakeDangerousAsync` per block (wired config = **144 blocks per explosion**), each
  doing `GetComponentsInChildren<MeshRenderer>(true)` and stomping the shared prism material.
- **Per-launch allocations:** `Projectile.cs:144-146` allocates a linked
  `CancellationTokenSource` pair per shot and runs a per-projectile UniTask yielding every
  frame (`:171-205`). Projectiles are pooled; the tasks/CTS are not.

**Refactor shape:** pool `AOEExplosion` instances (they already have clean
Initialize/Detonate/cancel lifecycles; inject once at pool-fill, not per detonation); route
AOE block grow-in and danger-conversion through the existing `PrismEffectsManager` batch
(same shape as explosions — it is the proven in-repo pattern); centralize projectile motion in
a manager (SoA position/velocity/elapsed, one update or Burst job, shared cancellation).
**Verified severity: medium, hot path** — this is the largest *live* per-event churn left.

---

## B. Bounded refactors — live costs today, medium severity (all verified)

### B1. Crystal idle animation: always-on SkinnedMeshRenderers, no culling

`SpaceCrystalAnimator` (`Assets/_Scripts/Controller/Environment/Crystals/SpaceCrystalAnimator.cs:31-52`)
runs `FixedUpdate` per instance driving `SkinnedMeshRenderer.SetBlendShapeWeight`. Verified
wiring makes the scale much larger than "capped crystals": `CrystalSpace.prefab` is nested in
**12 living flora/fauna prefabs** (active for the lifeform's whole life), and the four
Mass-lifeform prefabs each add **4 more** SMR+animator pairs. Dozens of concurrent skinned
meshes in ecology scenes — and `CrystalSpace.prefab` sets **`m_UpdateWhenOffscreen: 1`**, so
those skin every rendered frame even off-camera. On mobile this is real GPU/CPU skinning load.

**Refactor shape:** bake the pulse into a vertex shader driven by a material param (or a
single manager lerping an MPB float), drop the per-instance MonoBehaviours, set
`updateWhenOffscreen = 0` and let bounds handle it. No gameplay surface.

**Also found (flag, do not act):** the "crystals are capped 16/cell" statement in
`ECOSYSTEM_MASTERPLAN.md` §4 has **no implementing code** (no cap logic in
`CrystalManager`/`NetworkCrystalManager`/`CellRuntimeDataSO`/`Cell`). Doc/code drift — the
ecology owner should reconcile.

### B2. Spindle / Crystal material churn (flora grow-in + crystal collect)

- Every spindle `Start()` runs `CondenseCoroutine` → `UseTemporaryMaterial()` →
  `new Material(originalMaterial)` (`Spindle.cs:70, 137-141, 184`) — one material instance +
  broken batch slot per condensing spindle, per-frame `SetFloat` on the unique material for the
  ~1 s window, then destroyed. Spindle count ≈ prism count in flora-dense cells; worst case is
  `LifeForm.Die` → `ForceWitherAll` cloning **one material per spindle in a single frame**
  (hundreds+). Verified leak: a spindle dying mid-condense overwrites `temporaryMaterial`
  without destroying the condense clone.
- `Crystal.cs:247` is a genuine **double clone** (`new Material(renderer.material)` — getter
  clone + explicit clone, the getter clone leaks); `Crystal.cs:176` clones per model per
  `Explode` into a single `tempMaterial` field that is overwritten per iteration and never
  destroyed — **leaks per collection**.

**Refactor shape:** Spindle already drives `_Phase` via MPB (`Spindle.cs:62-67`) — extend the
MPB to the condense/evaporate params and delete the clone path entirely; fix the two Crystal
leaks with MPB or explicit lifetime. Event-driven, bounded, no design surface.

### B3. `DomainVolumeIndicator` hex gauge re-batches the shared canvas — including the whole Menu_Main UI

Verified: the gauge is runtime-attached to the "Volume / Pause Button" with **no sub-canvas**
(`DomainVolumeIndicator.cs:119-129`), so its `SetVerticesDirty` cadence re-batches the whole
root canvas — the **entire menu UI canvas in Menu_Main** (parent chain → `UI_Refactored`) and
the shared game HUD canvas in gameplay scenes. `FaunaSpawnCycleFraction` advances continuously
(`Cell.cs:350-357`; the spawner records a pulse every period regardless of seeding), so with
the 0.002 epsilon the mesh dirties at ~42 Hz in Menu_Main (12 s spawn period) — and menu-mode
"hide" is CanvasGroup-alpha-only, so **the rebuild continues while the gauge is invisible**
during all normal menu browsing. Short-period gameplay cells (Skim Race 15 s) are similar;
long-period cells (60–300 s) are 8–1.7 Hz.

**Fix shape (small, high leverage):** copy `ObjectiveIndicator.CreateRuntime`'s sub-canvas
isolation (`ObjectiveIndicator.cs:299-329` — its comment literally calls this "the single
biggest perf win for this widget on a shared HUD canvas"), and/or quantize the spawn-cycle
input to the ring's visible segment count; skip `PushState` entirely while the owning
CanvasGroup alpha is 0.

### B4. Fauna behavior ticking — refactor **when** the ecosystem scales, not before

Verified reality check: fauna counts today are **tens, not hundreds** (spawn profile caps;
heaviest cell ≈ 58), and the per-tick `new WaitForSeconds` churn across ~60 fauna × 2
coroutines is only ~3 KB/s of GC noise. **Not a current hot path.** But the masterplan
explicitly targets "100s of fauna", and the per-creature architecture (2 coroutines +
per-tick allocations + per-neighbor `GetComponentInParent` walks + per-frame body-prism index
sync) scales linearly with population. The refactor — a centralized fauna tick scheduler, then
Jobs/Burst steering over SoA reading the existing `PrismSpatialIndex` buckets — is the right
Phase-gate companion to the masterplan's collider budget, and unlocks the sensing layer
already built for it. Sequence it with ecology scaling (`/ecology` protocol; state
collider-budget impact), not as a standalone perf PR.

Cheap enablers that are safe now: cache the `Fauna` back-reference on body `HealthPrism`s
(kills the `GetComponentInParent` walks at `LightFauna.cs:350`, `Boid.cs:215,247` — predator/
HealthPrism-gated, so low frequency today); cache `Boid`'s constant-interval `WaitForSeconds`
(`Boid.cs:160`; `Fauna`/`LightFauna` intervals vary per tick, so those need an accumulator
pattern instead); drop the redundant inherited goal coroutine for `LightFauna` (it recomputes
`Goal` at `LightFauna.cs:249-257`, clobbering the base coroutine's write before every use).

---

## C. Dead code that looks hot — delete it (verified dead via code + GUID wiring greps)

These carry the *worst-looking* patterns in the codebase and cost nothing today — which makes
them perf traps for whoever wires them next. Deleting them is the cheapest "refactor":

| Dead code | Location | The trap it carries |
|---|---|---|
| `CurrentScore` | `Assets/_Scripts/UI/CurrentScore.cs` (GUID referenced by zero scenes/prefabs) | Per-frame LINQ sort + list + closure + string alloc + unconditional TMP write |
| `TrailFollower.RideTheTrail` + `Trail.Project` + `TrailFollower.Attach/Detach` | `TrailFollower.cs:60-122`, `Trail.cs:104-147` (component sits idle on Urchin; `IsAttached` permanently false) | Heaviest trail-walk pattern in repo (alloc + double walk + `Project`) |
| `CellControlTurnMonitor` | `TurnMonitors/CellControlTurnMonitor.cs` (zero scene/prefab refs) | Per-frame `OrderByDescending().FirstOrDefault()` via `GameDataSO.cs:664-671` |
| `BoidSimulationController` | `Environment/FloraAndFauna/BoidSimulationController.cs` (zero refs) | **Synchronous GPU readback every Update** (`readBuffer.GetData`, line 139) + ComputeBuffer realloc per spawn |
| `Skimmer.DrawCircle` marker path | `Skimmer.cs:150-194` (early-outs on `nudgeShardPoolManager`, which is null in every prefab incl. Squirrel's explicit `{fileID: 0}` override) | ≤4 coroutines × ≤360 segments of pool-Get + `GetComponent` + double list alloc per skim |
| `Boid.AddToMoundCoroutine` + `NewBlock` | `Boid.cs:371-414` (reachable only via unshipped Termite's drone; would NRE on stale serialization anyway) | Per-frame **allocating** `Physics.OverlapSphere` poll + unpooled `Instantiate`/`AddComponent` |
| `Gun.FireSpherical` / `LoadedGun` spherical path | `Gun.cs:96-116` (executor hardcodes `FiringPatterns.Default`; `LoadedGun` prefabs unreferenced; sole caller commented out) | 2×(energy+3) projectile volleys that never fire |
| `AIGunner` | `Controller/AI/AIGunner.cs` (all logic commented out) | Dead stub |

If any of these is intended future work (Termite, spherical fire, nudge shards), keep the
asset but fix the pattern *before* wiring, and say so in a comment.

---

## D. Verified spot fixes (small, real, no architecture change)

Ordered roughly by value:

1. **`VesselPrismController.cs:262`** — replace `trail.TrailList.IndexOf(prism)` (O(n) scan
   per spawned prism; trails grow monotonically all turn, indefinitely in the lava lamp) with
   `trail.GetBlockIndex(prism)` — the O(1) dictionary **already exists** (`Trail.cs:48-52`).
   Note: `Count - 1` is *not* safe (duplicate-add early-return path, `Trail.cs:27-31`).
2. **Turn monitors: pick one driver.** `TurnMonitor.Update` (`TurnMonitor.cs:40-46`) and
   `TurnMonitorController.Update` (`TurnMonitorController.cs:68-82`) both run
   `CheckForEndOfTurn` every frame; only the controller's result is used (base `OnTurnEnded`
   is empty, no overrides). Delete the monitor-side check; replace the controller's
   `monitors.Any(...)` (boxed-enumerator alloc/frame) with a `for` loop. Better: evaluate
   `IsObjectiveReached` inside the `OnCrystalsCollectedChanged`/`OnJoustCollisionChanged`
   handlers the network monitors already subscribe to, and keep only the time-based monitors
   polling. (Absolute cost is small — this is architectural hygiene + the alloc.)
3. **`GameDataSO`: name→stats dictionary.** `FindByName` (`GameDataSO.cs:876-883`) allocates
   closure+delegate+enumerator per lookup; `StatsManager`
   (`Controller/Managers/StatsManager.cs` — note: *not* under UI) does 1–2 lookups per prism
   created/destroyed/stolen, server-only. Steady drip + ~600-lookup bursts on AOE frames.
   Maintain a `Dictionary<string, IRoundStats>` on add/remove.
4. **`Cell.AddBlock`/`RemoveBlock`/`NotifyBlockDomainChanged`/`DominantDomain`
   (`Cell.cs:918, 942, 962-963, 186`)** — hoist the per-call `Domains[]` arrays to
   `static readonly`. Runs per prism register/unregister (via `PrismSpatialIndex.BindCell`).
5. **`AssembledFlora`/`BranchingFlora` `Branch` struct** — implement `IEquatable<Branch>`
   (currently reflection-boxes on every `HashSet` op), replace `ElementAt(i)`-in-a-loop
   (`AssembledFlora.cs:127`, accidental O(n²) + enumerator alloc) with a `List<Branch>`,
   reuse the two per-`Grow` scratch lists.
6. **`Trail.LookAhead` non-allocating overload** (`Trail.cs:71` allocates per call; called per
   skim trigger-enter via `SkimmerAlignPrismEffectSO`) — fill a caller-owned buffer.
7. **`WallAssembler.FindClosestMate` (`WallAssembler.cs:404-424`)** — the *live* assembler
   cost (Serpent): a 1 Hz **uncapped** 40 u prism sweep that `AddComponent`s a `WallAssembler`
   onto every candidate lacking one — permanent component accumulation on pooled prisms. Cap
   candidates (Gyroid caps at 10) and pre-add/pool the component. (The per-frame steering
   coroutines are fine at reachable scale — single digits to low dozens.)
8. **`HexRaceScoreTracker.Update` (`HexRaceScoreTracker.cs:77-83`)** — throttle the
   elapsed-time `Score` write to ~5–10 Hz (today it dirties `n_Score` every frame; NGO
   coalesces to tick rate, so cost is small — one float per tick to each client + ~30 Hz HUD
   events on clients — but it's free to fix).
9. **`GameEventFeed`** — pool rows (it builds a TMP GameObject per event via
   `GameFeedEntry.CreateEntry` — the prefab field is null in both GameCanvases — and
   `ForceRebuildLayoutImmediate`s a VLG+ContentSizeFitter container, `GameEventFeed.cs:185-214`).
   Bounded (≤6 rows, source-throttled) but a spike source on joust-heavy frames in the four
   domain-game scenes.
10. **`ResourceSystem`** — move the editor test-harness branches (`ResourceSystem.cs:62-65`)
    behind `#if UNITY_EDITOR` and compute effective levels on write instead of per-frame
    polling. (Micro-cleanup — see E1 before spending more than that.)
11. **`Boid.cs:160`** — cache the constant `WaitForSeconds`.
12. **`VesselController.cs:88`** — `CountNetVarDirty(3)` is unconditional: the benchmark
    over-reports dirty NetVars (counts at frame rate even when setters early-out; serialization
    is 30 Hz). Fix the instrumentation, not the replication (see E4).
13. **`LightFauna.WitherCoroutine` (`LightFauna.cs:122-145`)** — death-path LINQ chains; reuse
    the cached body-prism list and sort in place (matters only in Frenzy die-off frames).
14. **`MaterialBlendUtility.BeginBlend`** — pools nothing (`AddComponent` + material clone +
    `renderer.materials` array round-trip per blend); pool the MPB and skip the array swap.

---

## E. Refuted / downgraded — do not re-chase these

The adversarial pass exists so these don't come back as "findings" every audit:

1. **"`ResourceSystem.Update` is the largest per-vessel cost" — REFUTED.** Steady-state is ~8
   `TryGetValue` on ≤4-entry dictionaries (~0.1–0.5 µs/vessel); `EmitElementLevel` dedups on
   integer level steps, so the claimed per-frame `ElementalFloat` fan-out doesn't happen
   (bounded ≤~20 emits per element per effect). The genuinely heavy per-frame vessel components
   are `VesselTransformer.Update`, `ShipAudioController.Update`, `Skimmer.Update` — none of
   which showed a defect. A full event-driven rewrite is disproportionate (and temporary
   effects decay continuously, so some periodic advancement is required regardless).
2. **"Assembler coroutine swarm dominates frame time" — REFUTED at reachable scale.** Every
   Gyroid bonding initiator is dead or unshipped (verified via GUID wiring); the live path
   (Serpent wall) runs single-digit-to-low-dozens steering coroutines, each ~µs, and
   `NotifyPositionChanged` only rebuckets on bucket-key change. The real cost there is the
   1 Hz uncapped sweep (D7). A centralized `BondSteeringManager` is not warranted.
3. **"Fauna coroutines are a top refactor now" — DOWNGRADED.** Tens of fauna, ~3 KB/s GC.
   Right refactor, wrong trigger — gate it on ecology scaling (B4).
4. **"`VesselController` writes NetworkVariables at render rate" — DOWNGRADED to
   instrumentation bug.** NGO 2.5's setter early-outs on equal values and serializes dirty
   vars only at tick boundaries (TickRate 30 in the NetworkManager prefab). Per-frame cost is a
   memcmp + dirty flag. Same for the HexRace score drip (one float @ 30 Hz, host-only).
5. **"Turn-monitor double evaluation is expensive" — DOWNGRADED.** Real and redundant, but
   dozens of field reads + one ~40 B enumerator box per frame, server-only for the network
   monitors (`if (!IsServer) return false`). Hygiene fix (D2), not a hot path.
6. **"`Skimmer.DrawCircle` allocation storm per skim" — REFUTED as live cost.** The entire
   marker path is unreachable: `nudgeShardPoolManager` is null in every prefab (Squirrel
   explicitly overrides it to null despite owning the pool component). Latent trap → C-list.
7. **"`renderer.material` setter clones in AOE danger blocks" — mechanism wrong.** The
   *setter* doesn't clone (getter-only behavior); the real per-block costs there are the
   `GetComponentsInChildren` allocation, the per-block task, and stomping the shared material
   reference (covered in A2).
8. **"StatsManager is in `_Scripts/UI/`" — wrong path** (it's
   `Controller/Managers/StatsManager.cs`), and it's gated `_allowRecord = server/host only`.
   The dictionary fix (D3) stands; the "event storm" framing mostly doesn't — setters gate C#
   raises on `!IsSpawned` and replicate via NetworkVariable callbacks by design, with no
   double-raise.

---

## Suggested sequencing

| Phase | Work | Why this order |
|---|---|---|
| 1 (days) | C dead-code purge + D1–D5, D7, D11–D12 | Zero-risk, removes traps, kills the only verified O(n²) and the pooled-prism component accumulation |
| 2 (days) | B3 sub-canvas + B2 spindle/crystal MPB + D8–D9 | Biggest *visible-now* wins: Menu_Main canvas rebuild at ~42 Hz, flora material churn, feed spikes |
| 3 (1–2 wks) | A2 AOE pooling + batched grow + projectile manager | Largest live per-event churn; reuses `PrismEffectsManager` pattern |
| 4 (1–2 wks) | B1 crystal animator → shader-driven | Mobile skinning load; no design surface |
| 5 (roadmap) | A1 prism ECS/instancing (with masterplan §4) + B4 fauna scheduler (with ecology scaling) | Structural; sequence with the ecosystem phase gates that already own these constraints |

**Doc fixes to land alongside:** update `PRISM_PERFORMANCE_AUDIT.md` (rec #1 shipped via
`PrismEffectsManager`; rec #4 resolved — `MaterialPropertyAnimator` uses `sharedMaterial`);
reconcile the masterplan's "crystals capped 16/cell" claim with code (no cap exists).
