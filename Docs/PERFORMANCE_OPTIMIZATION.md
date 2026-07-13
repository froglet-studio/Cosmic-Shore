# Performance Optimization — Log & Backlog

Living document for the frame-cost optimization effort. It records what already
shipped (so nobody re-fixes or regresses it), the locked conventions every perf
fix follows, the instrumentation available, and the **prioritized backlog** with
root-cause analysis and designed fixes. All file/line references below were
verified against the code on 2026-07-08 (branch `claude/cosmic-shore-perf-opt-g8j6n0`,
post PR #573 merge).

**How to use this doc:** before touching anything performance-related, read
§2 (conventions) and §3 (instrumentation). Pick work from §4 top-down — tasks
are ordered by value-for-cost. After every fix, run the §5 verification
protocol and update this doc (status + measured numbers).

---

## 1. Current state (2026-07-08)

### Measured results after the de-spike pass (PR #573)

| Scenario | Before | After |
|---|---|---|
| Menu_Main (lava lamp) | ~6 fps | ~50 fps |
| HexRace | periodic multi-ms spike train | ~33 ms frames, no spike train |

Remaining known costs: `EventSystem.Update` ≈ **0.5 ms** flat UI raycast tax
(Task 2); first-use shader-compile hitches (Task 3 — plumbing shipped,
collection not yet recorded).

### 2026-07-09 soak capture (Menu_Main, ~25,000 prisms) — the population-scaling rows

Long lava-lamp soak: **25,483 registered prisms** (PrismScaleManager HUD;
27,030 in MaterialStateManager), 27 fps, CPU-busy 25.1 ms, GPU 10.9 ms. This
population is a **valid designed state**, not a leak: the menu vessel is Jade,
fauna spawn in the cell's controlling color and eat only *opposing* mass —
a single-domain cloud has no predator (territorial permanence), so it grows
until an active force consumes it. The perf contract is therefore that every
per-frame system must be **O(near/active), not O(population)**. Two rows
violate that today; the creation-tick verdict also landed.

1. **`Prism.Create.Visibility` VERDICT (Task 4 closed):** 90%+ of the
   creation tick (0.72/0.80 ms at 11k prisms; 1.32/1.46 ms at 25k — the cost
   *scales with entity count*), with `CompleteAllJobs` / `InvalidateArrays`
   children — the ECS structural-change signature of the `DisableRendering`
   add/remove. `SpatialBind` (0.04) and `SOAPRaise` (0.03, 384 B — PrismStats
   payload, Task 7 micro-item) are exonerated. **Fix (designed, next up):**
   make prism render visibility non-structural — create the render entity
   visible at the creation tick instead of born-hidden-then-revealed, and
   batch the hide-side toggles (ECB / batched RemoveComponent) so per-prism
   sync points disappear.
2. **`PrismColliderLodManager.Update` = 5.54 ms on slice frames.** The sweep
   is budgeted (`maxPrismChecksPerFrameSlice = 8000`) but the slice scan
   (`PrismSpatialIndex.QueryUnionOfSpheresSlice`) is a **managed** loop —
   per entry: NativeArray indexer (editor safety checks), flag test,
   per-focus `distancesq`, managed ref + Unity-null check, `List.Add` ≈
   0.7 µs/entry. At 25k prisms a sweep = 4 slice-frames × 5.5 ms every
   0.25 s. **Fix (designed):** Burst the classification — add an LOD-near
   flag bit to `PrismSpatialData`, run one Burst job per sweep over the
   packed array emitting `becameNear`/`becameFar` transition index lists,
   complete async (schedule → apply next frame, the densest-region pattern),
   managed side applies **transitions only** (typically a handful per sweep).
   Managed cost collapses from O(population) to O(transitions). Interim knob
   if needed before the job lands: lower `maxPrismChecksPerFrameSlice` to
   ~2500 (≈1.7 ms/frame, sweep stretches to ~11 frames — still inside the
   radius margin at menu speeds).
3. **`UniTaskLoopRunnerUpdate` = 4.38 ms + 3.4 KB on trail-spawn ticks.**
   The trail spawner (`VesselPrismController` UniTask loop) calls
   `CreateBlock` → `PrismFactory` → pool `Get()`; with the buffer drained
   (long-soak consumption outruns the 20/s maintenance), `ObjectPool.Get`
   falls through to `CreateFunc` = **synchronous prism-prefab Instantiate
   inline on the spawn tick** (2 blocks for a gapped trail ≈ 2 × ~2 ms).
   These on-demand misses are NOT covered by the `PoolRefill.*` marker
   (maintenance-only). **Fix path:** (a) instrument the miss —
   marker around `CreateFunc` itself (`PoolMiss.<prefabName>`) to count
   misses vs buffered hits; (b) the now-evidence-backed escalation:
   maintenance refills via Unity 6 `InstantiateAsync` + deeper
   `bufferSizeTarget`/`maxInstantiateRate` on the vessel prism pools so the
   buffer never empties and every instantiate is engine-timesliced.
4. **Open item:** console shows a one-off
   `ArgumentNullException: Value cannot be null. Parameter name: source`
   (a LINQ call on a null collection somewhere) — grab the stack trace next
   time it appears; not yet attributed.

Post-fix rows confirmed healthy at 25k: `PrismScaleManager.Process`
invisible (68 active growers), creation budget holding (2/frame),
`CoroutinesDelayedCalls` 1.61 ms / 472 B, physics 1.62 ms, GPU 10.9 ms
(instancing coping with 25k).

### 2026-07-09 capture (frame 33237, 32.76 ms) — the flora growth pipeline

Post-slicing capture. The animation managers are bounded (1.41 ms for BOTH,
was 5.55) and the frame driver moved to `CoroutinesDelayedCalls` = 9.88 ms
(30%) + 43 KB GC — the **flora growth pipeline**, three linked costs:

1. **`AssembledFlora.GrowCoroutine` 4.92 ms + 36.4 KB, one call** — one
   flora's grow tick: up to `itemsPerGrow` (5) unpooled `Instantiate`s of
   HealthPrism + Spindle prefabs inline in one frame, every `growPeriod`
   (3 s) per flora → the red spike train. Plus `activeBranches.ElementAt(i)`
   (LINQ, O(n²)/tick + enumerator allocs). **Fixed `019eb3c0`:** grow tick
   decides + claims sites, per-frame drain (`maxSpawnsPerFrame`, default 1)
   instantiates — pacing only; de-LINQ'd.
2. **`HealthPrism.CreateBlockCoroutine` 2.38 ms / 5 calls (~0.47 ms per
   creation tick)** — Task 4 now measured. **Split markers shipped
   `d4f696ae`** (`Prism.Create.Visibility` / `.SOAPRaise` / `.SpatialBind`);
   next capture names the dominant part, then fix it (structural-change →
   enableable component is the favorite).
3. **`Spindle` fades cloned a Material per condense/evaporate** (49
   condensing in-capture) — the banned `renderer.material` pattern. **Fixed
   `5da47650`:** `_DeathAnimation` via shared MaterialPropertyBlock, cleared
   on completion to restore SRP batching.

Secondary: `DomainVolumeIndicator.Update` 1.02 ms flat — **fixed
`481a7ad8`** (colors on sample cadence, push gated on convergence/cycle
epsilon/fresh sample). Editor-only rows to ignore in captures:
`DiagnosticsHUD.Update` 0.43 ms + 7.8 KB/frame, animation-manager stat
strings 2.2 KB (both compiled out of release).

Deliberately NOT done: no change to flora growth cadence, item counts, or
any ecosystem tuning — the regrowth behavior is working as designed; every
fix spreads or de-allocates the same work.

### 2026-07-08 capture (HexRace, frame 2721, 71.28 ms) — two new findings

1. **Pool-refill Instantiate + full GC (35 ms).** `UniTaskLoopRunnerEarlyUpdate
   → Instantiate.Produce` (one call, 14.30 ms self — a prism prefab, per the
   child component ctors) with a **20.75 ms `GC.Collect`** inside it. Root
   cause is structural: **conserved prisms make pool consumption one-way** —
   `Prism.ReturnToPool()` has no gameplay caller (destroyed prisms persist as
   restorable wrecks, by design), so after the first ~30 buffered instances
   every trail prism laid = one runtime Instantiate of a heavy 7-component
   prefab, all session. Pool config is consistent (no maxSize churn bug:
   vessel pools 10/100/20 capacity/max/buffer; `PrismExplosionPool`
   1500/1500/1500). The full GC is the heap-growth clock expiring mid-race.
   **Shipped mitigations:** full `GC.Collect()` behind the scene-load splash
   (`SceneLoader.LoadSceneAsync`); the per-spawn `WaitForSeconds` alloc in
   `Prism.CreateBlockCoroutine` cached (one alloc per prism ever laid,
   eliminated); `PoolRefill.<prefabName>` ProfilerMarker around maintenance
   refills for per-pool attribution. **Escalation (needs the marker datum):**
   if `PoolRefill.*` shows multi-ms *typical* (not GC-polluted) unit cost,
   move maintenance refills to Unity 6 `Object.InstantiateAsync` so the
   engine time-slices the instantiate — deliberate integration risk, only
   with evidence.
2. **Task 1's missing datum arrived: 4350 active / 11,400 registered**
   growers (Animators HUD) during HexRace — 4× the design estimate. Source
   shortlist (verified callers): **Boid grazing** calls `Grow(±1)` on prey +
   own health prism per bite (`Boid.cs:297-298, 476`) — sustained grazing
   keeps whole clusters legitimately re-animating; assembler bonding
   (`GyroidAssembler.cs:372`, `WallAssembler.cs:313`, one-shot per bond);
   `GunVesselTransformer.cs:100`; creation blooms. This is re-triggered
   animation from real gameplay, **not a leak** (disabled/destroyed animators
   unregister via `OnDisable`/`OnDestroy`) — so the fix is the Task 1
   slice-and-budget, which shipped (below).

### Shipped optimizations (do not regress)

| Commit | What |
|---|---|
| `311f554d` | Settled octahedron shields render instanced + batch shared meshes |
| `44429079` | Kill diag audit scene scans; instrument impact + membrane hot paths |
| `350caa10` | Time-slice cytoplasm shard reorientation off the pickup frame |
| `0b3854e3` | Pool crystal-ring prisms through `PrismFactory` |
| `ead0fcd4` | Pool spent-crystal husks; kill per-pickup material clones |
| `3f2e4f65` | Triple the crystal-ring pool buffer |
| `2e14555a` | Warm recorded shader variants behind the splash (Task 3 plumbing) |
| `1f6b69b6` | Stamp fauna body-prism ownership; desync boid ticks; deepen implosion pool |
| `19b7b5a4` | Pace boid consume cascades across frames (`Boid._pendingMeals`), throughput preserved |
| `be8b1d14` | Run the densest-region job async instead of stalling the frame |
| `4b29ad50` | Slice the collider-LOD sweep across frames with prism stamps |
| `f2b9eb3e` | Slice the cell volume recompute; publish sums atomically |
| `b0373252` | Fuse the animation-manager passes; drop the job round-trips |
| `a372782f` | Cadence-gate membrane matrices; de-LINQ crystal lookups |
| `660854e7` | Budget prism creation completions per frame (6/frame, `Prism.cs`) |
| `0caca23a` | Attribute animation-manager cost (`PrismScaleManager.Process` marker); cache color endpoints |
| `669b5ef8` | Raycast-target audit editor tool (Task 2 tooling) |
| `27860eaa` | Growth step made dt-linear, tempo-preserved (Task 1 commit 1) |
| `4b36b7f8` | Growth pass sliced under a per-frame budget, true-dt stepping (Task 1 commit 2) |
| `5f6b497a` | Full GC behind the scene-load splash |
| `546e2b98` | Spawn-window `WaitForSeconds` cached (kills one alloc per prism laid) |
| `e04d5a72` | `PoolRefill.<prefabName>` markers on pool buffer refills |
| `d80e7ee5` | FIX from re-verification: growth tempo calibrated to the real 40 ms tick (`dtNominal = 0.04`); slice dt cap scaled to rotation period; cursor drift corrected |
| `4443df83` | GC on every peer behind the post-load fade (clients + Play Again reloads were uncovered) |
| `019eb3c0` | Flora grow-tick instantiation paced across frames (decision at tick, drain 1/frame); branch walk de-LINQ'd |
| `5da47650` | Spindle condense/evaporate fades via shared MaterialPropertyBlock (was a Material clone + Destroy per fade) |
| `d4f696ae` | Creation-tick split markers: `Prism.Create.Visibility` / `.SOAPRaise` / `.SpatialBind` (Task 4 attribution) |
| `481a7ad8` | Domain-volume gauge: colors on sample cadence, push gated on real change (was 1.02 ms/frame flat) |

---

## 2. Conventions (locked — do not relitigate)

- **The fix pattern is: slice + per-frame budget + atomic publish.** Spread a
  hot pass across frames with a rotating cursor and a serialized per-frame
  budget; publish results atomically so consumers never see a half-updated
  state. Reference implementations: `PrismColliderLodManager` (sliced sweep +
  prism stamps), `Cell.EnsureVolumeFresh` (sliced recompute + atomic sum
  publish), `Prism.CreateBlockCoroutine` (creation-completion budget),
  `Boid._pendingMeals` (consume queue + `maxConsumesPerFrame`). Prefer this
  over one-frame passes and over "adaptive skipping".
- **Any change touching fauna/flora/cells/crystals/spawning goes through the
  `/ecology` skill protocol**: restate the invariants touched, state the
  active-collider-budget impact, hand back exact in-editor verification steps.
  Mass is conserved; no decay/TTL/despawn timers, ever — pacing (spreading
  work over frames, throughput preserved) is fine, capping (dropping work) is
  not.
- **Profile first.** No fix lands without a marker/capture naming the cost,
  and a capture after confirming the improvement (`Debugging Methodology` in
  CLAUDE.md).
- Conventional commits (`perf(scope): summary`); commit identity
  `Claude <noreply@anthropic.com>`; push with exponential backoff to the
  session branch only.

---

## 3. Instrumentation inventory

- **Profiler markers**: `PrismScaleManager.Process`,
  `MaterialStateManager.Process`, `CapsuleMembrane.UpdateMatrices`,
  `CapsuleMembrane.RenderMeshInstanced`. (The generic
  `AdaptiveAnimationManager.Update` row collapses all manager instances — the
  per-manager markers are what attribute cost.)
- **DiagnosticsHUD**: "Animators" section shows `active / registered` per
  animation manager, live (updates only when the count changes — no GC).
  Console commands: `prisms N` / `prisms off` / `prismcolors`.
- **Collider-LOD telemetry**: `PrismColliderLodManager.LastNearCount` /
  `LastLiveCount`.
- **Benchmark tool**: `Assets/_Scripts/Utility/PerformanceBenchmark/`
  (`BENCHMARK_TOOL.md` — tabs, score/hints, sweep).
- **Raycast audit tool**: `Tools > Cosmic Shore > UI > Raycast Target Audit`
  (`Assets/_Scripts/Editor/RaycastTargetAuditTool.cs`).

---

## 4. Backlog (priority order = value for cost)

### Task 1 — PrismScaleManager growth-wave cost ✔ SHIPPED (2026-07-08)

**Status:** SHIPPED — `27860eaa` (dt-linear step) + `4b36b7f8` (sliced pass,
`maxGrowersPerFrame = 300` serialized on `PrismScaleManager`, per-animator
`LastStepTime` true-dt stepping) **+ `d80e7ee5`**, which fixed two tempo
blockers that a 6-agent adversarial re-verification caught in those commits
before any build: (a) `dtNominal` was 0.02 but the project's Fixed Timestep —
the cadence the old 5%-per-tick fraction was calibrated against — is **0.04**
(`ProjectSettings/TimeManager.asset`), so growth ran ~1.9× too fast as first
committed; now 0.04 → k ∈ [1.25, 2.5] s⁻¹, per-tick step within ~5% of
historical across the whole GrowthRate range. (b) The fixed 0.5 s catch-up
cap clamped budget-induced inter-step delay under exactly the target load
(4350/300 = 15 ticks × 40 ms = 0.6 s rotation), silently slowing tempo — the
cap now scales to 2 rotations (floored at 0.5 s), so slicing delay always
integrates fully and only genuine stalls clamp. Same commit corrects cursor
drift on removal bursts (advance compensates for in-window prunes +
completions). The spike source datum arrived (see §1): 4350 active growers
during HexRace, driven by Boid grazing `Grow(±1)` per bite — legitimate
re-animation, not a leak. **In-editor verification pending (see §5 + the
per-fix checks below).** Design record below updated to the corrected math.
**Value:** highest — at 4350 active the unsliced pass walked every grower's
native interop each 40 ms tick; the budget caps it at 300/tick (~1/14th).

**Accepted behavior notes (from re-verification, no code change):**
- Completion detection (and thus `ExecuteOnScaleComplete`: shield activation,
  `onPrismVolumeModified` stat credit) can lag by up to one rotation
  (~0.6 s at the captured worst case) during a frenzy — acceptable; watch it
  in-editor.
- Under adaptive frame-interval creep the OLD code visibly slowed growth
  (fraction-per-tick at long ticks); the dt-linear step now holds authored
  tempo under load. Growth under heavy load therefore looks *faster than the
  old build* — that is the fix working, not a regression.
- Elapsed time beyond the (rotation-scaled) cap is dropped by design: a
  genuine multi-second stall does not fast-forward growth afterwards, which
  matches the old accumulator's spike clamp and preserves continuity.

**Symptom.** `PrismScaleManager.Process` = 5.55 ms when active growers spike
to ~1000 (baseline < 200; spikes a few times per session). Confirmed via the
`PrismScaleManager.Process` marker + Animators HUD.

**Root cause (verified).** Per-grower *native interop* × N in one processed
frame — the math is trivial, the engine boundary is the cost. Per grower per
tick (`Assets/_Scripts/Controller/Managers/PrismScaleManager.cs:58-79`):
`transform.localScale` read + write, `OwnerPrism.SyncRenderTransform()`
(entity LocalToWorld write), `RefreshVolumeCache()` → `transform.lossyScale`
(a parent-chain walk).

**Why it can't just be cadence-skipped (verified).** The step at
`PrismScaleManager.cs:61` is
`lerpSpeed = clamp(GrowthRate · dt, 0.05, 0.1)` — a clamped **lerp fraction
per processed tick**, not a rate. The manager feeds it an effectively fixed
dt: `AdaptiveAnimationManager.Update` uses
`updateInterval = Time.fixedDeltaTime × currentFrameInterval` and passes
`effectiveDeltaTime = updateInterval`
(`Assets/_Scripts/Controller/Managers/AdaptiveAnimationManager.cs:203-208`),
i.e. **40 ms** at interval 1 (the project Fixed Timestep is 0.04 —
`ProjectSettings/TimeManager.asset`; do NOT assume Unity's 0.02 default).
Worse: `PrismScaleAnimator.GrowthRate` defaults to
**0.01** (`Assets/_Scripts/Controller/Environment/Prisms/PrismScaleAnimator.cs:26`),
so `GrowthRate·dt = 0.0002` — *every* grower clamps up to the 0.05 floor.
Growth tempo is therefore 100% cadence-defined: process a grower half as
often and it visibly grows half as fast. Any slicing/cadence change is unsafe
until the step is dt-linear.

**Fix — two commits:**

1. `perf(prisms): make the growth step dt-linear, tempo-preserved`
   Replace the step in `PrismScaleManager.ProcessAnimationFrame` with
   exponential easing (constants as CORRECTED by `d80e7ee5`):
   ```
   dtNominal = 0.04f   // MUST equal the project Fixed Timestep (TimeManager.asset)
   k     = clamp(GrowthRate * dtNominal, 0.05f, 0.1f) / dtNominal   // 1.25–2.5 s⁻¹
   alpha = 1 - exp(-k * deltaTime)
   ```
   At the nominal cadence (dt = 0.04) this gives `1 − exp(−0.05) ≈ 0.0488`
   vs the old 0.05 — visually identical (−2.4% per step), and within ~5% of
   the historical per-tick fraction across the whole GrowthRate range
   (rate 2 → 0.0769 vs 0.08; rate ≥ 2.5 → 0.0952 vs 0.10) — but now *correct
   at any dt*. Do not read `Time.fixedDeltaTime` live (AstroLeague's hitstop
   rescales it transiently). Keep `COMPLETION_THRESHOLD_SQR` snap semantics
   unchanged.
2. `perf(prisms): slice the growth pass`
   Serialized budget (`[Min(50)] int maxGrowersPerFrame = 300`), rotating
   cursor over the snapshot list (advance compensated for in-window
   removals), and a per-animator `LastStepTime` stamp (float on
   `PrismScaleAnimator`) so each grower steps with its **true elapsed dt**
   when its slice comes up. The catch-up dt cap MUST scale with the rotation
   period (`max(0.5s, 2 × tickDt × ceil(count/budget))`) — a fixed cap
   silently slows tempo whenever a rotation exceeds it, which is exactly the
   frenzy case. `SyncRenderTransform` / `RefreshVolumeCache` then run at 1/K
   rate automatically — safe because the volume consumer (`Cell`'s per-domain
   aggregation) is itself on a 0.25 s sliced cadence, and the render-matrix
   lag is bounded by the slice period.

**Bonus datum (parallel, user-driven):** observe what's on screen when the
count spikes to ~1000 — tadpole swarm melting a trail cluster? flora regrow
pulse? gyroid assembly? If it's the flora regrowth pulse, note
`Docs/ECOSYSTEM.md` already flags that pulse for retirement — raise it under
`/ecology`, don't tune it casually. If a single caller restarts growth on
already-settled prisms every tick, kill it at the source (bigger win than
slicing).

**Verify (in-editor, pending):** same HexRace scenario;
`PrismScaleManager.Process` bounded (~≤ 2 ms even at 4350 active); growth
blooms visually unchanged at low counts (rings, flora, trails — tempo
identical); during a heavy frenzy, blooms step coarser but at the same
overall speed — if visibly chunky, raise `maxGrowersPerFrame` on the
`PrismScaleManager` component (PrismManagers prefab) and re-check the marker;
Animators HUD count unchanged — slicing must not change WHO animates, only
when they step. Confirm no prism ever pops instantly to full size (the
unseeded-stamp fallback + dt cap guard this).

---

### Task 2 — Run the Raycast Target Audit (in-editor, tool shipped)

**Status:** tooling shipped (`669b5ef8`); execution is a user-driven editor
pass. **Value:** ~0.4 ms every frame, near-zero effort.

`EventSystem.Update` ≈ 0.5 ms — pure UI raycast volume. Counted:
`Menu_Main.unity` ~859 enabled raycast targets, `Squirrel.prefab` ~148 (the
default menu vessel — rides into every scene), `GameCanvas-HexRace.prefab`
~115, `ArcadeGameConfigureModal` ~107, `GameCanvas` ~71, `R_GameOverPanel`
~57. Unity ships every Image/TMP with `raycastTarget` on; almost all are pure
display. Real Selectables are interleaved (the Squirrel HUD holds 5), so a
blind batch edit was rejected.

**Tool behavior (verified,** `Assets/_Scripts/Editor/RaycastTargetAuditTool.cs`**):**
menu `Tools/Cosmic Shore/UI/Raycast Target Audit` (line 33). Classifier
(`NeedsRaycast`, lines 163-178) keeps any Graphic with a `Selectable`
(`GetComponentInParent`, inactive included) or an `IEventSystemHandler` on
self-or-ancestor; everything else is a disable candidate. Scan scope: Project
selection of prefabs/folders (recursive) takes precedence, else all roots of
the open scene. Disabling is button-driven, not automatic on scan.
**Caveat:** the *scene* path uses `Undo.RecordObject` (undo-able); the
*prefab* path saves via `SaveAsPrefabAsset` and is **NOT undo-able** — rely
on git for prefab edits.

**Run on:** Menu_Main scene, `_Prefabs/Spacevessels`, `_Prefabs/UI Elements`,
the GameCanvas prefabs, the modals. Then click through the full menu + one
race. **Verify:** `EventSystem.Update` ~0.1 ms; every button still works. If
the tool misclassifies anything, fix the classifier — don't hand-edit prefabs.

---

### Task 3 — Record the shader warm-up collection (user action + verify)

**Status:** plumbing shipped (`2e14555a`) but the collection is **empty** —
warmup is currently a no-op. **Value:** kills the one first-use hitch class
pooling can't fix (first pickup / first mine / first ring of a session).

**Verified plumbing:** `AppManager.WarmUpShaders()`
(`Assets/_Scripts/System/AppManager.cs:274`), called once in
`RunBootstrapAsync` (`:220`) *before* the minimum-splash wait so the cost
hides behind the opaque splash. Config: `BootstrapConfigSO._shaderWarmupCollections`
(an **array**, `Assets/_Scripts/System/Bootstrap/BootstrapConfigSO.cs:32`);
`BootstrapConfig.asset` wires exactly one entry →
`Assets/_SO_Assets/GameplayShaderWarmup.shadervariants`, whose content today
is `m_Shaders: {}` (0 variants).

**Recording procedure (editor):** Project Settings > Graphics > Shader
Loading → "Currently tracked" → **Clear** → play a representative session
(menu, freestyle, pickups, HexRace, Joust rings, a mine) → **Save to
asset…** OVER `Assets/_SO_Assets/GameplayShaderWarmup.shadervariants` (same
path preserves the GUID and the BootstrapConfig wiring). Re-record after big
material/shader changes.

**Verify:** enable `_verboseLogging` on `BootstrapConfig.asset` (currently
**0** — nothing logs until flipped); console shows
`[AppManager] Shader warmup: N variants in Xms` (`AppManager.cs:295`) with
N > 0. **Escalation:** if DX12 hitches survive (`Shader.CreateGPUProgram` /
PSO rows on first use), the deeper fix is Unity 6 `GraphicsStateCollection`
trace/replay — only with profiler evidence.

---

### Task 4 — CreateBlockCoroutine per-tick attribution (conditional)

**Status:** conditional — only if the budgeted row still shows in captures.
The ×12-per-frame burst was fixed by the completion budget
(`MaxCreationCompletionsPerFrame = 6`, `Assets/_Scripts/Controller/Vessel/Prism.cs:429`);
a single creation tick still costs ~0.25–0.3 ms and nobody knows which part
dominates.

**Verified cost candidates in the completion tick:**
1. `SetRenderVisible(true)` (`Prism.cs:457`) → `PrismRenderService.SetVisible`
   (`Assets/_Scripts/Controller/ECS/Rendering/PrismRenderService.cs:346-355`)
   — **adds/removes the `DisableRendering` tag component = ECS structural
   change per prism** (entities are born hidden via `AddComponent` at `:339`;
   `ApplyRenderPath` may even lazily create the entity in the same tick).
2. `_onTrailBlockCreatedEventChannel.Raise` (`Prism.cs:467-471`) — SOAP
   listeners run inline.
3. `PrismSpatialIndex.Register` (`Prism.cs:485-487`) → `Register`
   (`PrismSpatialIndex.cs:662`) → `BindCell` (`:527`): `Cell.FindCellContaining`
   + `cell.AddBlock` density-grid writes. Also in the same tick:
   `RefreshVolumeCache` (`:483`) and `PrismColliderLodManager.NotifyPrismActivated`
   (`:492`).

**Fix:** add split ProfilerMarkers around the three candidates, capture, fix
the winner. If the structural change wins: make prism visibility an
`IEnableableComponent` flag flip instead of add/remove `DisableRendering` —
non-structural, no sync point.

---

### Task 5 — LightFauna grazing pacing parity (conditional, `/ecology`)

**Status:** conditional — currently cheap (0.72 ms / 16 calls); act only if a
capture shows the LightFauna tick spiking.

**Verified.** LightFauna's tick is `UpdateBehavior()`
(`Assets/_Scripts/Controller/Environment/FloraAndFauna/LightFauna.cs:228`,
driven by `UpdateBehaviorCoroutine` `:157-168` — note: `CalculateBehavior` is
the **Boid** method name, not LightFauna's). Inside the spatial-index prism
loop (`:328-397`) herbivores consume **every** edible prism in range inline —
`Consume` at `:384` and `:394`, predation at `:362` — no queue, no budget: a
brittlestar swarm in dense prey can burst exactly like boids did.

**Fix (port the Boid pattern, verified refs):** `Boid.maxConsumesPerFrame`
(default 8, ≤0 = legacy burst; `Boid.cs:75`), `_pendingMeals` (`:80`),
enqueue (`:308-311`), `DrainPendingMeals` (`:361-366`) called from the tick
(`:322`) then `Update` (`:497-498`), re-validation at drain time in
`EatPrism` (`:374-398`: destroyed / shielded / fauna-body re-checks), queue
cleared on death (`OnDeath :406` — uneaten prey stays in the world, mass
conserved). It's an `/ecology` change: **pacing only, throughput preserved**
— same invariant statement as commit `19b7b5a4`.

**Profiler attribution:** steady-state cost appears under
`LightFauna.UpdateBehaviorCoroutine`; `WitherCoroutine` is death-only; the
base-class `Fauna.UpdateGoalCoroutine` profiles under `Fauna`, not
`LightFauna`.

---

### Task 6 — Adaptive frame-interval machinery: retire or retune

**Status:** decide after Task 1 ships. **Verified:** the designed 1×–12×
frame-skipping (`AdaptiveAnimationManager.UpdateFrameInterval`) is dormant —
its only call site is `EnsureCapacity` (`AdaptiveAnimationManager.cs:117`),
i.e. it runs only on array high-water growth, never in steady state.

**Why it was NOT simply wired on:** its curve floors the interval at
`capacityFactor = capacity / 50` (`:151`, `:158`, `:166`) — at 500 animators
that forces 10–12× skipping even at 60 fps → visibly chunky animation. And
after Task 1 the scale manager is dt-correct + budget-bounded anyway.

**Options:** (a) retire the dead machinery (`frameTimeHistory`,
`UpdateFrameInterval`, `currentFrameInterval` → fixed cadence) for clarity —
**preferred**; or (b) re-tune pressure-driven only (frame-time ratio, no
capacity floor) and call it from `Update` (it already self-gates at 0.5 s).
Only pick (b) if captures show sustained overload that slicing doesn't bound.
Note Task 1's dt-linear step is what makes (b) *safe* at all.

---

### Task 7 — Deferred micro-items (only with profiler evidence)

| Item | Verified refs | Notes |
|---|---|---|
| AOE explosion Instantiate per pickup | `ExplosionHelper.CreateExplosion` (`…/EffectsSO/Helpers/ExplosionHelper.cs:13/:42`), `Object.Instantiate` in `SpawnAllAndDetonate` (`:77`); triggered per crystal impact by `VesselExplosionByCrystalEffectSO.Execute` (`:57`, 0.15 s per-impactor cooldown `:34`) | Self-destroy contract: `AOEExplosion.ExplodeAsync` (`…/Projectiles/AOEExplosion.cs:174`) — delay 0.2 s + duration **2–4 s** (prefab values; code default 2; SkyBurst=50), `Destroy` at `:267`. Lifetime is ~2.2–4.2 s, **not** 0.6 s (0.6 s is the prism collider window — different system). Pooling it touches ALL AOE users (Joust, mines, skimmer overtakes) — deferred deliberately. ~0.1–0.3 ms/pickup. |
| Per-prism spawn-window coroutine | `Prism.cs:346` (StartCoroutine), `:435` (`new WaitForSeconds(waitTime)` alloc per spawn), `waitTime = 0.6f` field `:30` | Centralize into `PrismTimerManager` (`…/Managers/PrismTimerManager.cs:20`) if `CoroutinesDelayedCalls` shows. Caveat: the manager currently serves only shield timers (`TimerAction.DeactivateShield`) — needs a new `TimerAction`. |
| PlayerName string alloc per pickup | `Crystal.ExplodeParams.PlayerName` is already `FixedString64Bytes` (`…/FlowField/Crystal.cs:88`) but `.ToString()` runs per explosion (`:169`) | Plumb `FixedString64Bytes` through attribution if GC matters. Multiplayer round-trips via `NetworkExplodeParams` (`NetworkCrystalManager.cs:362-391`). |
| StatsManager closure per collect | `StatsManager.CrystalCollected` (`…/Managers/StatsManager.cs:108`); the four **elemental** lambdas (`:127/:135/:143/:151`) capture `crystalStats` → closure alloc per collect; the Omni lambda (`:120`) is captureless → compiler-cached, no alloc | Cache delegates or switch to direct increments. `UpdateStatForPlayer` at `:325`. |
| MaterialPropertyAnimator closure per re-target | `UpdateMaterial` (`…/Environment/Prisms/MaterialPropertyAnimator.cs:159`), `OnAnimationComplete = () => {…}` at `:208-222` | One closure per team-change re-target; invoked/nulled by `MaterialStateManager` (`:127/:133`). Minor. |
| DomainVolumeIndicator residual | `…/UI/DomainVolumeIndicator.cs`: per-frame `StepTowardTargets` exp-lerp (`:229-245`) + `PushState` (`:247-258`, resolves theme colors each frame); targets sampled at 0.25 s (`:168`); mesh rebuild delta-gated downstream (`DomainVolumeHexGraphic.SetState :98`, ε=0.002) | Sub-ms; mesh rebuilds every frame only *during* lerp convergence. Revisit only if it reappears in captures. |

---

### Task 8 — Graphics settings hygiene (flagged, untouched)

**Verified** (`ProjectSettings/GraphicsSettings.asset:30-44`, 15 entries):
`m_AlwaysIncludedShaders` indices **9 and 10** (lines 39-40, guids
`a9fd020fb5d57034ab1c751f7cca8a1c` / `393648d8ac8900a4098c6d07a7bb3bf2`) are
**dead references** — neither guid resolves to any asset in the repo. Delete
them (Project Settings > Graphics > Always Included Shaders).

Seven gameplay Shader Graphs are force-included (indices 6-8, 11-14):
`BlockGraph`, `CageGraph`, `DandruffGraph`, `CrystalGraph`, `VesselGraph`,
`SnowGraph`, `VelocityGraphOld` (all in `Assets/_Graphics/Materials/Graphs/`).
Always Included force-packs **every variant** of these into the build (size +
memory smell). Audit whether they're needed there at all once the Task 3
warmup collection covers runtime compilation — `VelocityGraphOld` in
particular looks like a leftover.

---

### Task 9 — Docs updates (small, ride along with any ecology commit)

- `Docs/ECOSYSTEM.md` mechanics log: add the consume-pacing entry — Boid
  `_pendingMeals` / `maxConsumesPerFrame` is the `ReproductionCooldownSeconds`
  (birth-burst throttle) pattern applied to the consumption side; **pacing
  only, throughput preserved**. Prevents the next person mistaking the queue
  for a grazing cap. Extend it when Task 5 lands for LightFauna.
- `Docs/ECOSYSTEM.md` / `Docs/SPATIAL_INDEX.md`: note the sliced patterns now
  standard — collider-LOD sweep slices + prism stamps; cell volume recompute
  slices + atomic publish; densest-region job async on a snapshot.

---

## 5. Standing verification protocol (run after each fix)

Same HexRace scenario, Deep Profile **off**:

1. No row in `BehaviourUpdate` > ~2 ms.
2. No periodic spike train in the CPU graph.
3. GC alloc/frame near zero outside UI.
4. Gameplay sanity: pickups (rings bloom once, husks drift), fauna
   graze/starve/never eat fauna bodies, growth tempo unchanged, membrane
   wobble smooth, crystal respawn fade correct, replay/reset reads an empty
   cell immediately.

First capture of any session: confirm the compile is clean (the
bleeding-edge + PR-base merges `393cf914` / `dd012b66` were verified by grep,
not compiler, at the time of the merge).

---

## 6. Doc changelog

| Date | Change |
|---|---|
| 2026-07-08 | Created. All task file/line claims verified against code (6-agent sweep). Corrections found: LightFauna's tick is `UpdateBehavior` (not `CalculateBehavior` — that's Boid's); AOE explosion lifetime is ~2.2–4.2 s (not 0.6 s); audit tool's prefab path is not Undo-able; StatsManager's Omni lambda is captureless (no alloc) — only the four elemental lambdas allocate; shader-warmup log additionally requires `_verboseLogging=1` on `BootstrapConfig.asset`. Task 1 marked NEXT UP. |
| 2026-07-08 (later) | HexRace capture analyzed (71 ms frame): pool-refill Instantiate + 20.75 ms mid-race `GC.Collect` on EarlyUpdate, and the Task 1 datum landed — 4350/11,400 growers, source = Boid grazing re-animation (legit, not a leak). Shipped: Task 1 both commits (`27860eaa`, `4b36b7f8`), GC behind splash (`5f6b497a`), spawn-window WaitForSeconds cache (`546e2b98`), `PoolRefill.*` markers (`e04d5a72`). Ecology protocol run: pacing/attribution only, zero collider impact, tempo + continuity preserved by construction. Escalation recorded: `InstantiateAsync` refills only if `PoolRefill.*` shows multi-ms typical unit cost. |
| 2026-07-09 (flora round) | New capture (32.76 ms frame) analyzed: frame driver = flora growth pipeline (`CoroutinesDelayedCalls` 9.88 ms + 43 KB GC). Shipped under `/ecology`: grow-tick pacing `019eb3c0`, spindle MPB fades `5da47650`, creation-tick split markers `d4f696ae`, gauge push gating `481a7ad8`. Animation managers confirmed bounded post-slice (1.41 ms both, was 5.55). Verify in-editor: flora canopy shape/density unchanged over a full regrow cycle (children appear over ≤5 frames instead of one burst — invisible at 3 s cadence); spindle condense/evaporate fades look identical; gauge fills/ring/dominant tint behave identically; next capture reads `Prism.Create.*` split + `PoolRefill.*`. |
| 2026-07-09 (soak round) | 25k-prism menu soak analyzed (user capture). Task 4 verdict: `Prism.Create.Visibility` dominates (90%+, scales with entity count, `CompleteAllJobs` signature) → non-structural visibility fix is next. Two new O(population) offenders found + fixes designed: collider-LOD slice scan is managed (5.54 ms/slice-frame at 25k → Burst transition-list job), and trail-spawn pool misses Instantiate inline under `UniTaskLoopRunnerUpdate` (4.38 ms — invisible to `PoolRefill.*`; add `PoolMiss.*` marker, then `InstantiateAsync` + deeper buffers). `SOAPRaise` 384 B ruled a Task 7 micro-item. Open: unattributed one-off `ArgumentNullException (source)` in console. HexRace pass deferred by user. |
| 2026-07-09 (flora round, re-verified) | Adversarial pass over the four commits. Markers + gauge gating: SOUND. Two real bugs fixed in `fc9c53f3`: (1) the flora drain outlived `LifeForm.Die` (`StopAllCoroutines` killed the old GrowCoroutine spawn site but not `Update`) — a dying flora kept spawning through its wither window (zombie flora / child pop-out); `Die` override clears the queue. (2) Spindle's evaporate renderer-gone early-out could bail before `DisableSpindle`, hanging `DieCoroutine`'s empty-tracker wait — removed. Hardening: grow orders carry `decidedAt` and drop past `ReservationTtlSeconds − 1` (Frenzy holds could outlive the 5 s claim → overlap risk); drain freezes at `timeScale 0` (parity with the old `WaitForSeconds` loop). **Correction of record:** a material clone stays SRP-batchable; an MPB excludes the renderer for the fade duration — the spindle win is zero material create/destroy churn, at the cost of unbatched draws *during* fades. Re-measure in the next capture; fallback is quantized shared fade materials (phase-variant pattern). Accepted nuances: gauge ring can lag ≤1 rebuild-epsilon (~1.4°, under segment granularity); theme-swap colors up to 0.25 s latent; per-tick flora throughput can under-fill only when a parent dies or an assembler fails mid-window (rare, disclosed). |
| 2026-07-09 | 6-agent adversarial re-verification of the five commits (pre-editor-test). **3 blockers found in the fresh Task 1 commits, fixed in `d80e7ee5`:** `dtNominal` 0.02 → 0.04 (project Fixed Timestep is 0.04 — growth was ~1.9× too fast as committed; two agents converged on it independently); fixed 0.5 s dt cap → rotation-scaled cap (fixed cap slowed tempo up to 7× under the target frenzy load); cursor drift on removal bursts corrected. GC coverage gaps (clients + Play Again reloads never took the scheduled collect) fixed in `4443df83` via the two all-peers post-load fade-back handlers. WaitForSeconds cache, pool marker verdicts: SOUND (notes: cache misses on the variable-`waitTime` `waitTillOutsideSkimmer` path — acceptable; prewarm burst unmarked — intentional). Final compile-sanity pass over all six edited files: PASS (arithmetic invariants proven, `MaterialStateManager` independent, no external readers of the sliced behavior). Lesson recorded: never assume Unity's 0.02 default fixed timestep — check `TimeManager.asset`. |
