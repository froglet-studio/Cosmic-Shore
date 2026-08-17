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

**Prism animation work has its own locked law:** `Docs/PRISM_ANIMATION.md` — no
prism may need multiframe CPU updates to animate (one stamp → GPU clock → one
scheduled end swap; gameplay state final at start). The CPU animation managers
(`PrismScaleManager` / `MaterialStateManager` / `AdaptiveAnimationManager` and
`PrismEffectsManager`'s animation passes) were DELETED in the D2 pass
(2026-08-02) — historical task entries below that reference them describe a
retired architecture; do not resurrect a per-frame pass to "optimize" anything.

---

## 0. SESSION HANDOFF (2026-07-15) — next session starts here

Branch `claude/domainvolumeindicator-perf-spike-cp32yn` — everything
committed and pushed, tree clean. Session focus: the "10 ms
`DomainVolumeIndicator.Update` spike" from the user's profiler capture —
root-caused, fixed, field-verified across four captures (full per-capture
analyses in the dated sections under §0.1), and closed with the discovery
that **the editor's Burst compilation was disabled the whole time**.

### The one rule this session bought (check FIRST in every future perf session)

**Before trusting any capture, verify `Jobs ▸ Burst ▸ Enable Compilation` is
ON** (and for clean captures: `Jobs ▸ Burst ▸ Safety Checks ▸ Off`,
`Jobs ▸ Jobs Debugger` off, leak detection off). It was OFF on the user's
machine (confirmed 07-15), which silently ran every job as managed IL at
~20× cost and explains all inflated job readings in captures #2–#4
(`CellVolumeSumJob` 6.1–6.5 ms, `LodClassifyJob` 2.0–2.7 ms, and the
`WaitForJobGroupID` 3.83 ms stall — a managed 6 ms job hogging a worker
delays the engine's own culling jobs). The tell in the Hierarchy view: a
job showing as **`ExecuteJobFunction.Invoke() [Invoke]`** is executing
managed; Burst-compiled rows show the job type name.

### Shipped this session (do not re-fix; regression list)

1. **`fb5e6643`** — Cell volume recompute is one Burst `CellVolumeSumJob`
   over a new packed **summation view** in `PrismSpatialIndex`
   (`PrismCellData`: live volume + cell id + live domain slot + env flag);
   the managed 8000-prisms/frame slice and `Cell.massTracked` are DELETED.
   Root cause of the reported spike: `EnsureVolumeFresh` is reader-driven
   and billed the slice to the first volume reader — the domain gauge was
   the messenger, not the culprit. Markers: `Cell.VolumeSum`,
   `DomainVolumeIndicator.Sample`/`.Push` (closes TODO C2). Six edit-mode
   summation-view tests.
2. **`71b51a28`** — Collider-LOD **hysteresis** (`lodExitRadiusMultiplier`,
   default 1.15: far→near inside `lodRadiusMeters`, near→far only beyond
   radius×multiplier — kills moving-focus boundary flapping); drain budget
   charged per re-validated entry (was unbounded transform reads on churny
   queues); `LOD.Sweep`/`LOD.Drain` markers; LOD tick offset half an
   interval from the volume tick.
3. **`4ba827ef`** — Volume sum moved **off the main thread**:
   `PrismSpatialIndex.TryScheduleCellVolumeSum` snapshots
   `_spatial`+`_cellData` (one per-frame-shared memcpy, marker
   `Cell.VolumeSum.Snapshot`) and `Schedule()`s the job to a worker;
   `Cell.EnsureVolumeFresh` harvests with `IsCompleted` on a later read
   (never blocks; readers keep published sums meanwhile — the declared
   0.25 s tolerance). In-flight passes are discarded unpublished on
   reset/disable; buffers complete-then-dispose on teardown. LOD tick
   re-arm made **phase-preserving** (advance from due time — re-arming from
   `Time.time` re-stacked the ticks after any hitch, observed in capture
   #3). Async/sync equivalence test added; sync `SumCellVolumes` kept for
   tests/benchmarks.

Docs commits `27024bab`/`722f37e1`/`3cc29fb8`/`910b0854` hold the
per-capture analyses; `Docs/SPATIAL_INDEX.md` documents the summation view
+ its three freshness streams.

### Field-verified results (capture #4, standalone profiler, ~7000 prisms, crowded view — do not regress)

| Row | Session start | Capture #4 |
|---|---|---|
| `BehaviourUpdate` (entire script tick) | 15.24 ms | **1.71 ms** |
| `DomainVolumeIndicator.Update` | 10.31 ms | **0.00 ms** |
| `Cell.VolumeSum` on the main thread | 6.1–6.5 ms | **absent** (worker thread) |
| `PrismColliderLodManager` managed self | 4.00 ms | 1.37 ms (`LOD.Sweep`), `LOD.Drain` 0.00 |
| Biggest remaining script row | — | `CapsuleMembrane.UpdateMatrices` 0.70 ms |

### TODO NEXT SESSION — in priority order

1. **Burst-ON verification capture** (user just enabled it). Expected:
   `LodClassifyJob` ~0.1–0.3 ms; `CellVolumeSumJob` ~0.15 ms on a worker
   row (Timeline view — it never appears in the main-thread hierarchy
   anymore); `DomainVolumeIndicator.Update` ≤ ~0.5 ms on schedule frames
   (`Cell.VolumeSum.Snapshot` memcpy is the only real cost) and ~0
   between; `LOD.Sweep` sub-ms; `WaitForJobGroupID` under
   `ScriptableRenderContext.Submit` shrinks (worker no longer hogged);
   the two 0.25 s ticks staying 0.125 s apart across hitches.
2. **Functional re-verification of the volume path** (in-editor, from the
   capture-#1 checklist in §0.1): gauge wedges track per-domain mass
   (lay trail / let fauna eat); phase ladder climbs at the same volumes
   (flora freeze at Frenzy, resume after consumption); nucleus claim flips
   only by in-nucleus laid volume (Brood Rush); steals re-attribute wedges
   within ~0.5 s; HexRace Play Again starts with an empty gauge (no ghost
   pre-reset volume — the discard-unpublished path); run the
   `PrismSpatialIndexTests` edit-mode suite (7 new summation-view tests).
3. **Rendering frontier** (capture #4 — now the top of the frame):
   a. Identify the second camera stack entry ("Camera", ~2 ms beside
      CM PlayerCam's ~4 ms) — what does it render, and can its culling
      mask drop the prism layers? BRG culling + emit-draw jobs run once
      PER camera.
   b. Read the DiagnosticsHUD **CPU/GPU split + bound verdict** row in the
      crowded view. Suspicion: GPU-bound via transparent-prism overdraw at
      2M+ verts — if confirmed, the lever is shader/overdraw work, and any
      visual LOD idea must respect continuity (mass never disappears;
      only shading may simplify) and go through `/ecology` discussion.
   c. One **development-build capture** as ground truth — removes
      `EditorLoop` (4.39 ms) + `Profiler.FlushCounters` (2.62 ms), which
      persist even under the standalone profiler (it only moves the
      profiler UI out of process; the game still runs in the editor).
4. **Carried-over backlog** (§0.1 + §4, still open): Task 2 raycast-target
   audit (tool shipped, run pending); Task 3 shader-warmup collection
   (still EMPTY); TODO C1 Task 6 — retire the dormant adaptive
   frame-interval machinery; Task 7 micro-items; Task 8 graphics settings
   hygiene; TODO B pool/creation-tick datums.

### Architecture contracts added this session (for anyone touching them)

- **Summation view** (`PrismCellData` in `PrismSpatialIndex`): CellId/EnvMass
  written ONLY by `Cell.AddBlock/RemoveBlock` (both membership streams
  funnel through them; bulk clears → `ClearAllCellBindings`; BindCell
  passes the slot index explicitly because `prism.SpatialIndexId` isn't
  assigned yet during `Register`). Volume pushed by
  `Prism.RefreshVolumeCache` (O(growing)/frame). Domain slot refreshed by
  `ForwardDomainChangeToCell` on steals — the AOE damage view's
  registration-time Domain stays deliberately stale (known gap, untouched).
  Dual membership (flora host cell vs containing cell): last binder wins —
  a prism sums into exactly ONE cell (the old massTracked could
  double-count membrane-edge flora into two).
- **Async volume sum**: never block the main thread on the handle
  (`IsCompleted` harvest); never publish a pass scheduled before a reset;
  the results NativeArray must be quiescent (Complete) before re-schedule
  or dispose.
- **LOD hysteresis**: enter radius is the safety band (speed × tick
  margin); the exit multiplier only widens the far direction. Reconciles
  classify by the wide radius (collider-on is safe).

### Lessons (append to the standing list)

- An editor with Burst compilation disabled runs every job ~20× slow and
  poisons ALL job-row numbers — and a long managed job on a worker also
  delays the ENGINE's culling jobs (`WaitForJobGroupID`). Check the Jobs
  menu before profiling; recognize managed execution by the
  `ExecuteJobFunction.Invoke()` row name.
- Reader-driven lazy recomputes bill their cost to whichever component
  reads first — profile rows can indict the messenger. Split markers
  around the sample vs the push settled it in one capture.
- Fixed-cadence ticks must re-arm from their DUE time; re-arming from
  `Time.time` lets one hitch permanently re-stack de-phased ticks.
- DiagnosticsHUD "Frame Time" is wall clock (CPU + GPU/present wait +
  editor loop) — the profiler's CPU number will always read lower; use the
  HUD's busy-CPU/GPU split + bound verdict to pick the next lever.
- `List.Remove(this)` on a static enabled-instance registry is an O(n)
  UnityEngine.Object-equality scan, and a pool MISS pays it too: the
  create-then-deactivate cycle runs `OnDisable` for an instance that was never
  added, scanning the WHOLE registry for nothing. Under a mass burst this was
  1,863 ms of one frame (2,408 misses × ~50k live effects, 2026-08-02).
  Registries with no order contract use stored-index swap-remove, O(1).
- When the GPU owns an animation, the GameObject CARRIER becomes the cost:
  a pooled effect object whose only jobs are one stamp and holding a pool
  slot charges Instantiate + registry churn + a timer entry per death,
  orders of magnitude above the entity work it wraps. Batch-instantiate
  entities from the prototype instead (`PrismDebris` pattern: queue → one
  `em.Instantiate(prototype, N)` per frame → sweep-based batch destroy) and
  keep the pooled object only as the no-ECS fallback.

---

## 0.2 PLATFORM NOTE — macOS / Metal (2026-08-03)

Branch `claude/mac-game-unity-performance-p35x60`. Triggered by a report of
**~3 fps in the editor on an Apple Silicon Mac** (native ARM editor, not
Rosetta) against ~26–40 ms frames on the Windows dev machines.

### Read this before blaming the Mac

A sustained ~10× deficit is **not** a platform-cost profile. Retina costs at
most ~4× and only when GPU-bound; shader compilation is transient. A flat 10×
across every frame is the signature of an **editor diagnostic toggle**, and
§0's standing rule already caught this exact class once on Windows (Burst
compilation was off the whole session, running every job managed at ~20×).
Check these **before** taking any Mac capture seriously — all are per-machine
editor state, none are committed, so a new machine starts with none of the
project's history:

| Check | Where | Cost when wrong |
|---|---|---|
| **Deep Profile** off | Profiler window toggle | ~10× whole frame — the single most likely cause of 3 fps |
| **Burst ▸ Enable Compilation** ON | `Jobs ▸ Burst` | ~20× on every job (§0's rule) |
| **Burst ▸ Safety Checks** off | `Jobs ▸ Burst` | large on job-heavy frames |
| **Jobs Debugger** off | `Jobs` menu | large on job-heavy frames |
| **Leak Detection** off / not "with stack trace" | `Jobs` menu | large, allocation-proportional |
| Burst cache warm | first play-mode entry on a new machine compiles from cold | one-off stall, not sustained |

The Hierarchy-view tell for managed jobs is unchanged: a row showing
`ExecuteJobFunction.Invoke() [Invoke]` is executing managed; Burst-compiled
rows show the job type name.

### Shipped this session

1. **`metalAPIValidation: 0`** (`ProjectSettings/ProjectSettings.asset`) — was
   **1**. Metal's API validation layer runs per-draw in the editor on macOS;
   on a frame with this project's draw count it is pure overhead and it
   surfaces as errors things D3D12 silently tolerates. Off by default; turn it
   back on deliberately when debugging a Metal-specific rendering bug.
2. **Mac graphics API pinned to Metal** (same file, `m_BuildTargetGraphicsAPIs`
   gained a `MacStandaloneSupport` entry, `m_APIs: 10000000`,
   `m_Automatic: 0`). There was **no Mac entry at all** — only
   `WindowsStandaloneSupport` pinned to D3D12. Automatic resolves to Metal in
   practice, but pinning removes any chance of an OpenGLCore fallback (a
   deprecated, dramatically slower path) on an older Mac.
3. **`SettingsAutoDetector` is pixel-aware**
   (`Assets/_Scripts/Controller/Settings/SettingsAutoDetector.cs`). The
   heuristic scored cores/RAM/VRAM and **never asked how many pixels it had to
   fill**, then handed `MSAA4x` to anything scoring High or above. A Retina
   MacBook and a 1080p desktop score identically while the Mac renders ~4× the
   pixels — and per capture #4 the frontier here is transparent-prism
   overdraw, so pixel count is a first-order term. Now: a per-tier pixel budget
   drives `RenderScalePercent` (square-rooted, since render scale is linear per
   axis; clamped 50–100, never supersamples), FSR is selected when scaling
   down, and the AA choice reads the **effective** post-render-scale pixel
   count instead of the tier. Helps 4K Windows monitors identically — it is not
   a Mac special case. 13 edit-mode tests in `SettingsAutoDetectorTests`.
4. **`ExclusiveFullScreen` no longer requested on macOS**
   (`GraphicsSettingsApplier.ToFullScreenMode`). Unity only implements it on
   Windows; asking for it on a Mac alongside a Retina `SetResolution` is the
   standard recipe for a black window / wrong backbuffer / offset mouse.
   Runtime platform check, not a compile guard (`Docs/CONDITIONAL_COMPILATION.md`).

**Scope honesty:** items 1–4 are real Mac costs but they do **not** add up to
10×. If 3 fps survives the editor-toggle checklist above, the next step is a
capture on the Mac, not more speculative settings work.

### Known-open macOS gaps (not addressed here)

- **Graphics Jobs are off for Mac only** (`m_BuildTargetGraphicsJobs`:
  `MacStandaloneSupport: 0`, while Windows and Linux are `1`). Left alone
  deliberately — Metal graphics-jobs support in 6000.3 was not verified in this
  session, and flipping it blind risks instability for an unmeasured win. Test
  it explicitly before changing.
- **The shader warmup collection is empty AND would be Windows-recorded**
  (Task 3). `GameplayShaderWarmup.shadervariants` holds 0 variants, so warmup
  is a no-op everywhere. Worth knowing when it does get recorded: a
  ShaderVariantCollection is per-graphics-API, so a collection recorded on
  D3D12 does nothing for Metal. macOS needs its own recording pass.
- **No macOS build target exists.** `CosmicShoreBuildPipeline` builds
  `StandaloneWindows64` only (`ExecutableName = "CosmicShore.exe"`),
  `Tools/Build/build_windows.sh` is the only wrapper, and there is no
  `BurstAotSettings_StandaloneOSX.json`. Editor-on-Mac works; shipping a Mac
  player is unscoped work.
- **Standalone defaults to the lowest quality tier**
  (`QualitySettings.asset` → `m_PerPlatformDefaultQuality: Standalone: 0` =
  Very Low). Affects how a fresh machine *looks* before auto-detect seeds, not
  its framerate. Flagged, untouched.
- **Dual-mouse input does not exist on Mac.** `Win32RawInputMultiMouseProvider`
  is 11 `user32.dll` P/Invokes behind `#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN`,
  and `MultiMouseService` is guarded the same way, so `dualMouseEngaged` is
  permanently false there. The Escape→fullscreen toggle and the
  `!Application.isFocused` input guard are inside the same block
  (`InputController.cs:72-78`). Correctness, not performance.

---

## 0.1 PREVIOUS SESSION HANDOFF (2026-07-09)

Branch `claude/cosmic-shore-perf-opt-g8j6n0` — tracked by
**PR #588** (https://github.com/froglet-studio/Cosmic-Shore/pull/588); pushes
to this branch update that PR. Everything committed and pushed, tree clean. All code below compiled in-editor (Step 0
PASS) but several fixes still await their in-editor measurement. Every
non-trivial commit this session was adversarially re-verified by agent sweeps
BEFORE editor testing; every blocker found was fixed in a follow-up commit
(see §6 changelog for the full record).

### Shipped this session (do not re-fix; regression list)

1. **Growth pass** — dt-linear step calibrated to the real 40 ms fixed
   timestep + sliced under `maxGrowersPerFrame` (`27860eaa`+`4b36b7f8`+`d80e7ee5`).
2. **GC scheduling** — full collect behind every covered transition on every
   peer (`5f6b497a`+`4443df83`); spawn-window `WaitForSeconds` cached (`546e2b98`).
3. **Flora grow-tick pacing** + spindle MPB fades + drain hardening
   (`019eb3c0`+`5da47650`+`fc9c53f3`).
4. **Domain gauge push gating** (`481a7ad8`) — NOTE: follow-up filed, see TODO C2.
5. **Prism render entities**: prototype-instantiate (1 structural op, was ~8) +
   batched visibility flush (`e0735b2c`+`9b891d40`). CONFIRMED: creation tick
   should collapse — measure next session (TODO A1).
6. **Collider LOD**: Burst classification, transitions-only, budgeted cull
   queue with live re-validation (`eaf107e0`+`9b891d40`). CONFIRMED WORKING:
   0.71 ms (was 5.54).
7. **Pools**: async timesliced refills under an inactive incubator, deeper
   prism buffers (120/40/300), `PoolMiss`/`PoolActivate` attribution
   (`75828ff0`+`9b891d40`+`337443f0`).
8. **LightFauna consume pacing** (Task 5) — per-tick meal-plan rebuild,
   budget charged only on real consumes (`337443f0`+`eb707175`).

### TODO A — user measurements next session (in-editor, in this order)

1. **Steps 2–6 of the DOTS-round checklist** (§ "2026-07-09 fixes for the soak
   findings"): `Prism.Create.Visibility` ≈ ~0.05 ms and count-independent (was
   0.35–0.66 ms, scaling); shield-engage morph shows no ghost box; LOD
   gameplay checks (never fly through solid prisms; blip ≤0.5 ms per 0.25 s);
   `PoolMiss.*` only in the first seconds after load, then silent;
   `UniTaskLoopRunnerUpdate` spawn-tick hits gone; 25k re-soak FPS vs the 27
   fps baseline.
2. **Task 5 checks**: `LightFauna.UpdateBehaviorCoroutine` ≤ ~2 ms tick frames
   (was 13.79), small `LightFauna.Update` drain slices after; clusters melt
   over a few frames; fauna feed/starve normally; fauna death mid-graze
   withers cleanly.
3. **Collect the three open datums**: (a) `PoolActivate.<prefab>` — who owns
   the 0.27 ms activations, first-Awake or heavy OnEnable? (b) `PoolRefill.*`
   typical clean ms; (c) whether `GC.Collect` ever fires mid-gameplay now.
4. **Task 2 (user action)**: run `FrogletTools > Interface > Raycast Target
   Audit` on Menu_Main + vessel/UI prefabs (scene edits Undo-able; PREFAB
   edits are NOT — rely on git). Expect `EventSystem.Update` 0.5 → ~0.1 ms.
5. **Task 3 (user action)**: record the shader-variant collection over
   `Assets/_SO_Assets/GameplayShaderWarmup.shadervariants` (procedure in Task
   3 section) — it is still EMPTY, warmup is a no-op until recorded. Needs
   `_verboseLogging=1` on BootstrapConfig to see the confirmation log.
6. **Grab the stack trace** of the one-off
   `ArgumentNullException (Parameter: source)` if it reappears — still
   unattributed.
7. **HexRace full pass** (deferred twice): PoolRefill/PoolMiss under race
   load, `Prism.Create.*` on ring detonations, `PrismScaleManager.Process`
   bounded, growth tempo eyeball, replay/GC behavior, spike-train check.

### TODO B — code work gated on TODO A datums

1. If `PoolActivate` shows deferred first-Awake dominating → Awake-warm at
   refill completion (activate+deactivate once, budgeted, off the hot path).
2. If `PoolRefill` typical is still multi-ms or `InstantiateAsync unavailable`
   warning appears → revisit the async refill path.
3. If `Prism.Create.Visibility` did NOT collapse → expand the row; suspects
   in order: flush marker (`PrismRender.VisibilityFlush`), prototype miss,
   remaining structural op.

### TODO C — code work NOT gated (pick up any time, priority order)

1. **Task 6 — retire the dormant adaptive frame-interval machinery** (§4
   Task 6; decision now unblocked: prefer option (a) retire — the dt-linear +
   sliced scale pass made it redundant, and its capacity-floor curve is
   actively wrong; note `currentFrameInterval` creep also stretches the
   growth-manager tick under load today).
2. **DomainVolumeIndicator per-push cost** — RESOLVED `fb5e6643` (see the
   2026-07-14 section): the multi-ms cost was never the gauge — it was
   `Cell.EnsureVolumeFresh`'s managed volume slice billed to the first
   volume reader of the interval (10.31 ms at high prism counts). Recompute
   is now one Burst `CellVolumeSumJob` (~0.1-0.3 ms, under the new
   `Cell.VolumeSum` marker); the gauge got `DomainVolumeIndicator.Sample`
   / `.Push` split markers for the residual. Verify per the 2026-07-14
   checklist.
3. **Task 7 micro-items** (§4 table): AOE explosion pooling, per-prism spawn
   coroutine → `PrismTimerManager`, PlayerName `FixedString`, StatsManager
   elemental-lambda closures, `MaterialPropertyAnimator` closure.
4. **Task 8 — graphics settings hygiene** (editor actions): delete the two
   dead `Always Included Shaders` entries (indices 9–10); audit the seven
   force-included Shader Graphs once Task 3's warmup collection is recorded.
5. **Cleanup**: 6 unreferenced `_Prefabs/Pools/*PrismPool.prefab` siblings
   carry stale values (unused — delete or sync). Task 9 doc rides are done.
6. **Decision item (ecology, needs a fork discussion)**: LightFauna cadence
   floor is applied BEFORE the aggression multiplier (0.05 × 0.25 = 12.5 ms
   effective floor) — pre-existing; changing it alters ecology cadence, so
   raise via `/ecology` + AskUserQuestion if pursued.

### 2026-07-14 — the "DomainVolumeIndicator 10.31 ms" spike: root cause + fix (SHIPPED `fb5e6643`, verify below)

User capture (Menu_Main-scale scene, 2.17M verts): `DomainVolumeIndicator.Update`
**10.31 ms self, 32.4% of a 31.7 ms frame, 1 call, 0 B GC**, in a periodic spike
train. TODO C2 suspected the gauge's push path (1.22 ms in the 07-09 capture) —
the marker split planned there was pre-empted by reading the call graph:

**Root cause — reader-attributed volume slice, mis-billed to the gauge.**
`Cell.EnsureVolumeFresh` is reader-driven: the first volume reader of a stale
0.25 s interval paid one slice of the managed recompute
(`VolumeSlicePrismsPerFrame = 8000`; per prism: Unity null-check +
`CachedVolume` object-graph read + `trackedBlocks` dictionary lookup +
`transform.position` interop for env prisms in nucleus cells ≈ ~1.3 µs) —
~10 ms per slice frame at high prism counts, billed to the reader's Update
row. The gauge samples on exactly the recompute cadence (0.25 s), so it was
almost always that first reader. Same pathology as the collider-LOD managed
slice fixed in `eaf107e0` (0.7 µs/entry × 8000/frame), and the same fix.

**Fix (`fb5e6643`) — Burst the pass over the index's packed data:**

- `PrismSpatialIndex` gains a packed **cell-volume summation view**
  (`PrismCellData`, 8 B/slot: live volume, cell id, live domain slot,
  env-mass flag) + `CellVolumeSumJob`, one Burst `.Run()` per cell per
  0.25 s producing all the sums the managed pass did (per-domain volume /
  env volume / nucleus env volume + totals, nucleus split included).
  ~0.1–0.3 ms at 25k prisms, under the new **`Cell.VolumeSum`** marker.
- Freshness: `CellId`/`EnvMass` written only by `Cell.AddBlock/RemoveBlock`
  (both membership streams already funnel through them; bulk clears →
  `ClearAllCellBindings`); volume pushed by `Prism.RefreshVolumeCache`
  (O(growing)/frame); domain slot refreshed by `ForwardDomainChangeToCell`
  (AOE damage-view Domain stays deliberately stale — known gap untouched).
- `Cell`'s slice machinery + `massTracked` deleted. Semantics preserved:
  same cadence, atomic publish, live-domain steal re-attribution,
  fauna-body volume-only rule. One deliberate delta: a prism sums into
  exactly ONE cell (last binder wins) — the old dual membership
  (flora host cell + spatially containing cell) could double-count
  membrane-edge flora into two cells' phase ladders.
- Gauge attribution split shipped as `DomainVolumeIndicator.Sample` / `.Push`
  markers (TODO C2's requested split; the residual was already sub-ms).

**Ecology invariants:** accounting change only — volume is still the spine,
mass conservation / continuity / domain symmetry untouched, zero collider or
physics-query impact (strictly removes main-thread cost).

**Verification (in-editor, same scenario as the screenshot):**

1. Profile the same scene: `DomainVolumeIndicator.Update` self ≤ ~0.3 ms on
   every frame (no 0.25 s spike train); new `Cell.VolumeSum` rows ≤ ~0.5 ms
   at ≤ 0.25 s cadence per cell; `DomainVolumeIndicator.Sample`/`.Push`
   both sub-0.2 ms.
2. Gauge sanity: hex wedges still track per-domain mass (lay trail → your
   domain's wedge deepens; fauna eat → it recedes); centre hexagon tints
   the dominant domain; spawn-cycle ring sweeps.
3. Phase ladder unchanged: a filling cell still walks Calm → Restless →
   Frenzy at the same volumes (DiagnosticsHUD or fauna aggression as the
   observable); flora freeze at Frenzy and resume after consumption.
4. Nucleus cells (Brood Rush): nucleus claim still flips by in-nucleus laid
   volume only; exterior grazing never sways control.
5. Steals: convert a patch of enemy trail → within ~0.25 s the wedges
   re-attribute (old domain down, new domain up).
6. Replay reset (HexRace Play Again): post-reset gauge starts empty — no
   ghost volume from the previous round.
7. Edit-mode tests: run `CosmicShore.Tests` → the six new
   `PrismSpatialIndexTests` summation-view tests pass.

### 2026-07-14 capture #2 (post-`fb5e6643`, frame 86145, 40.77 ms CPU) — Burst jobs read ~20× too slow; LOD churn fixed (`71b51a28`)

The `fb5e6643` attribution works: the gauge's cost now shows as
`DomainVolumeIndicator.Sample` → `Cell.VolumeSum` → `ExecuteJobFunction.Invoke`
(`.Push` ≈ 0.00), and the spike moved off the managed slice into the job row.
But the numbers are wrong by a consistent multiplier:

| Row | This capture | Expected (documented) |
|---|---|---|
| `Cell.VolumeSum` job (`CellVolumeSumJob`) | **6.55 ms** | ~0.1–0.3 ms at 25k prisms |
| `LodClassifyJob` (under `PrismColliderLodManager.Update`) | **2.67 ms** | ~0.1–0.3 ms at 25k (`eaf107e0`, confirmed 07-09) |
| `PrismColliderLodManager.Update` managed self | **4.00 ms** | sub-0.5 ms tick |

**Diagnosis — the ~20× multiplier on BOTH Burst jobs is environmental, not
workload.** Two independent Burst linear scans reading 20× their confirmed
cost points at the jobs running as managed IL in the editor (Burst disabled,
still async-compiling after the script reload, or Jobs Debugger + full safety
checks). A real 20× population (~500k prisms) is implausible (memory,
rendering). **User check before any further code on these rows (TODO A0 —
RESOLVED 07-15: Burst WAS disabled; user enabled it, see §0):**
in the editor menu, `Jobs ▸ Burst ▸ Enable Compilation` ON,
`Jobs ▸ Burst ▸ Safety Checks ▸ Off` for the capture, `Jobs ▸ Jobs Debugger`
OFF, leak detection OFF — then re-capture. Expected: both job rows sub-ms.
Player/dev builds always run Burst-compiled — these two rows are near-free in
a build regardless of the editor setting.

**Real-workload findings fixed in `71b51a28` (valid whatever the Burst datum):**

1. **LOD boundary flapping.** Classification had ONE radius for both
   directions, so every prism at the bubble edge of a moving focus (the menu
   autopilot vessels roam continuously) flipped near/far every 0.25 s tick —
   transition floods that the managed side pays for (restore loop, queue
   enqueues, collider re-toggles into PhysX). Fix: hysteresis band in
   `LodClassifyJob` — far→near inside `lodRadiusMeters`, near→far only beyond
   `lodRadiusMeters × lodExitRadiusMultiplier` (new knob, default 1.15),
   annulus keeps prior state. Safety unchanged: the enter radius still
   guarantees speed × tick margin; reconciles classify by the wide radius
   (collider-on is the safe direction).
2. **Unbounded drain re-validation.** `DrainCullQueue`'s budget only counted
   APPLIED culls — entries re-validating as near (a churny queue's common
   case) were free, so one frame could burn thousands of
   `transform.position` + per-focus distance checks (multi-ms self). Budget
   is now charged per entry that reaches re-validation; cancelled/dead
   entries stay uncharged.
3. **Tick stacking.** The LOD sweep and the volume recompute are both 0.25 s
   passes and had locked onto the same frame (6.68 + 6.59 ms in one
   `BehaviourUpdate`). The LOD tick now starts half an interval out of phase.
4. **Attribution:** new `LOD.Sweep` (tick: classify + transitions) and
   `LOD.Drain` (every-frame budgeted culls) markers.

**Verification (next capture, after the Burst settings check):**
`LOD.Sweep` ticks sub-ms with `LodClassifyJob` ~0.1–0.3 ms; `LOD.Drain`
≤ ~0.5 ms every frame; `Cell.VolumeSum` sub-ms on its (now offset) tick
frames; steady-state transition counts near zero when no focus crosses new
territory (watch `becameNear/Far` sizes via a debugger or add temp logging);
vessels never fly through solid prisms (hysteresis touches only the
far-direction).

**Designed escalation (NOT shipped — needs the Burst-on datum first):** if a
capture with Burst confirmed ON still shows `Cell.VolumeSum` > 1 ms (i.e.
the population genuinely is huge), move the summation off the main thread:
index-owned async batch — snapshot `_spatial`+`_cellData` (one memcpy), one
`Schedule()`d job summing ALL cells per 0.25 s, lazily completed next tick,
cells consume the last published batch (the "densest-region async on a
snapshot" pattern, staleness still ≤ interval + a frame). Same escalation
applies to `LodClassifyJob` (schedule → apply next frame) but its flag-bit
read-write makes snapshotting subtler — only with evidence.

**EditorLoop spikes (user question):** `EditorLoop` is everything the Unity
EDITOR itself does outside your game's PlayerLoop — profiler window repaint
(the biggest offender while capturing), scene/inspector/console redraws,
editor-side GC (this capture's sawtooth in the object/memory graph), version
control polling. It does not exist in builds and is not actionable game
code. To keep it out of captures: use the standalone Profiler process
(Profiler window ⋮ menu), close the Console during capture runs (log spam =
editor repaints), don't leave the Game view at full-res alongside Scene
view, and treat a development build as the ground truth for frame times.

### 2026-07-14 capture #3 (standalone profiler, post-`71b51a28`, 28 ms frame) — jobs still managed-speed; volume sum moved off the main thread (`4ba827ef`)

Values (user capture, `LOD.Drain` marker row confirms `71b51a28` running):

| Row | Capture #2 | Capture #3 | Verdict |
|---|---|---|---|
| `Cell.VolumeSum` job | 6.55 ms | **6.11 ms** | unchanged — still managed-execution speed |
| `LodClassifyJob` | 2.67 ms | **2.01 ms** | unchanged class; small drop from hysteresis (fewer flag writes/list appends) |
| `PrismColliderLodManager` managed self | 4.00 ms | **1.37 ms** (`LOD.Sweep` self) | hysteresis + drain budget worked |
| `LOD.Drain` | — | **0.00 ms** | queue churn gone |
| Tick stacking | stacked | **stacked again** | the naive `now + interval` re-arm re-syncs after any hitch > offset — fixed phase-preserving in `4ba827ef` |

**The Burst question is still open but no longer load-bearing.** Two
captures show both jobs at ~20× Burst-expected cost, and `LodClassifyJob`
was CONFIRMED at 0.1–0.3 ms on 07-09 with identical code — so the
environment changed between sessions (the `ExecuteJobFunction.Invoke()`
row name is itself the managed-execution tell; Burst-compiled jobs show
the job type). TODO A0 (check `Jobs ▸ Burst ▸ Enable Compilation`, look
for yellow Burst compile errors in the console) still wants an answer —
it decides whether `LodClassifyJob`'s remaining 2 ms is real on this
machine — but the volume path no longer depends on it
*(RESOLVED 07-15: Burst was indeed disabled — see §0)*:

**Shipped `4ba827ef` — async volume sum (snapshot + worker thread):**
`Cell.EnsureVolumeFresh` now schedules `CellVolumeSumJob` via
`PrismSpatialIndex.TryScheduleCellVolumeSum` — one per-frame-shared
snapshot memcpy of `_spatial`+`_cellData` (marker:
`Cell.VolumeSum.Snapshot`) + `Schedule()` — and harvests with
`IsCompleted` on a later read (never blocks; readers keep published sums
meanwhile, the tolerance the cadence already declares). In-flight passes
are discarded unpublished on reset/disable; buffers complete-then-dispose
on teardown. Main-thread cost per 0.25 s recompute is now the memcpy
(~0.1–0.4 ms) **regardless of Burst, population, or editor settings**.
The sync `SumCellVolumes` stays for tests/benchmarks; an equivalence test
(`TryScheduleCellVolumeSum_MatchesSyncResults`) pins the two paths
together.

**Expected next capture:** `DomainVolumeIndicator.Update` total ≤ ~0.5 ms
on schedule frames (snapshot memcpy) and ~0 between; the job runs on a
worker-thread row (visible in Timeline view, not in the main-thread
hierarchy); `LOD.Sweep` unchanged (~1.4 ms managed self + job — drops to
sub-ms if/when Burst is re-enabled); the two ticks 0.125 s apart and
staying apart. If `LOD.Sweep` becomes the top row after Burst is
confirmed on, the same async treatment applies to `LodClassifyJob` but
needs a flag-bit snapshot design (it read-writes `_spatial`) — evidence
first.

### 2026-07-14 capture #4 (standalone profiler, post-`4ba827ef`, 26.43 ms CPU, ~7000 prisms, crowded view) — script side CONFIRMED FIXED; frontier moves to rendering

**Verified in the wild (do not regress):**

| Row | Before this session | Capture #4 |
|---|---|---|
| `BehaviourUpdate` (whole script tick) | 15.24 ms | **1.71 ms** |
| `DomainVolumeIndicator.Update` | 10.31 ms | **0.00 ms** |
| `Cell.VolumeSum` on the main thread | 6.11 ms | **absent** (worker thread) |
| Biggest script row now | — | `CapsuleMembrane.UpdateMatrices` 0.70 ms |

The `fb5e6643` → `4ba827ef` chain is confirmed working. Scripts are no
longer the frame's problem.

**Where the spike frame actually goes now (26.43 ms main thread):**

- `RenderPlayModeViewCameras` **11.49 ms** — the editor's wrapper around the
  game's real URP render loop (in a build this is the same work minus the
  wrapper). Inside: `Inl_RenderCameraStack` 10.79 ms across **2 camera
  stacks** ("CM PlayerCam" ≈ 4 ms, "Camera" ≈ 2 ms — identify what the
  second camera renders and whether its culling mask needs the prism
  layers; BRG frustum-culling + emit-draw jobs run once PER camera), and
  `WaitForJobGroupID` **3.83 ms** — the main thread stalling on culling
  jobs at Submit. Note the managed `CellVolumeSumJob` now occupies a worker
  for ~6 ms every 0.25 s while Burst is off — another reason to resolve
  TODO A0 (with Burst on it's ~0.15 ms and out of the way).
- `EditorLoop` **4.39 ms** + `Profiler.FlushCounters` **2.62 ms** — editor
  tax. The STANDALONE profiler only moves the profiler UI out of process;
  the game still runs inside the editor, so EditorLoop remains and
  FlushCounters is the cost of shipping profiler data to the other
  process. Only profiling a development BUILD removes these (~7 ms here).
- `UpdateScene` 7.85 ms of which scripts 1.71 ms (rest: animation, physics,
  coroutines, late update — individually small).

**The frame-time discrepancy (user question):** DiagnosticsHUD's "Frame
Time" is wall-clock `Time.unscaledDeltaTime` — CPU main thread + GPU/present
wait + editor loop. 26 ms profiler CPU vs 40–50 ms HUD frame time means
~15–20 ms is GPU/present + editor tax. The HUD already answers which:
expand it and read the **CPU (busy) / GPU split + bound verdict** row
(FrameTimingManager, works on DX12). If it says GPU-bound in crowded views,
the lever is GPU work — transparent-prism overdraw at 2.16M verts — not
CPU.

**Next datums wanted (in order):** (1) TODO A0 — the `Jobs ▸ Burst`
menu state / console Burst errors (decides `LodClassifyJob`'s 2 ms and
frees the worker thread) *(RESOLVED 07-15: it was disabled; enabled — see
§0 for the Burst-ON verification checklist)*; (2) DiagnosticsHUD bound
verdict in the crowded view; (3) what the second camera stack entry
("Camera") is in the scene and its culling mask; (4) a development-build
capture as ground truth (no EditorLoop, no PlayModeView wrapper).

### Session-wide lesson list (for the next agent)

- Never assume Unity's 0.02 default fixed timestep — this project runs 0.04
  (`TimeManager.asset`); tempo constants derive from it.
- Adversarial re-verification before editor testing caught real blockers in
  EVERY round this session (tempo ×1.9, dt-cap tempo sag, flora corpse-drain,
  spindle death-hang, ghost-entity resurrection, meal-queue duplicate
  backlog). Keep doing it.
- The Boid pacing pattern's hidden assumption (queue drains between ticks)
  does NOT transfer to fast-cadence consumers — rebuild-per-tick instead.
- Pool consumption is one-way under mass conservation: buffers delay
  instantiates, never eliminate them; only timeslicing/incubation removes the
  frame cost.
- Structural ECS changes (add/remove component) sync all jobs and scale with
  entity count — prototype+Instantiate+SetComponentData and batch APIs are
  the pattern.

---

## 1. Current state (2026-07-08)

### Measured results after the de-spike pass (PR #573)

| Scenario | Before | After |
|---|---|---|
| Menu_Main (lava lamp) | ~6 fps | ~50 fps |
| HexRace | periodic multi-ms spike train | ~33 ms frames, no spike train |

Remaining known costs as of session end (2026-07-09): `EventSystem.Update`
≈ **0.5 ms** flat UI raycast tax (Task 2 — audit tool shipped, run pending);
first-use shader-compile hitches (Task 3 — plumbing shipped, collection still
EMPTY); `DomainVolumeIndicator` ≈ **1.2 ms** during active feeding (TODO C2 —
resolved `fb5e6643`, was really the Cell volume slice; see 2026-07-14 section);
`GameObject.Activate` ≈ 0.27 ms per pooled activation (TODO A3/B1 datum);
editor-only DiagnosticsHUD ≈ 0.6 ms + stat strings (ships out). Menu was
50–60 fps at session end with the LightFauna fix not yet measured — see §0.

### 2026-07-09 post-DOTS-round capture — Task 5 evidence landed; ECS question answered

User capture after Step 0 (compile: PASS) + Step 1 (menu ~50–60 fps avg; target 70+).
Confirmed working in the wild: `PrismColliderLodManager.Update` **0.71 ms** (was
5.54), animation managers 0.41 ms. Steps 2–6 of the verification checklist are
**pending next session** (creation-tick numbers, LOD gameplay checks, PoolMiss
quieting, 25k re-soak, HexRace).

**New frame driver — `LightFauna.UpdateBehaviorCoroutine` 13.79 ms (9.27 self),
4 ticks, 12.8 KB GC.** This is Task 5's predicted burst, now measured. The cost
is NOT the fauna brain (sensing already rides the Burst spatial index; the
Physics.OverlapSphereNonAlloc inside is deliberately masked to vessels-only and
costs 0.04 ms). It is the **inline consume cascade**: every edible prism in
range is eaten in one tick, and each Consume pays the full death synchronously —
spatial-index removal, pool release (`TransformHandle.SetParent` /
`PrismScaleAnimator.OnDisable` ×16), VFX pool activation (`GameObject.Activate`
×16 ≈ 0.27 ms EACH — attribute next round: likely first-activation Awake of
async-incubated clones and/or heavy VFX OnEnable; if first-Awake, add an
Awake-warm toggle at refill completion), spindle lifecycle, and ~148 small
allocations.

**Decision (recorded): do NOT port fauna to ECS.** ECS/Burst pays off for
thousands of homogeneous things doing simple math with no managed interop —
which is why the render matrices, the AOE scan, and now the LOD classification
live there. Fauna are a handful of heterogeneous agents whose cost is
GameObject-world side effects (pools, spindles, VFX, SOAP); moving the brain
into ECS would not remove one millisecond of that cascade and would cost a huge
integration surface. The fix is **Task 5** (NEXT UP): port the Boid
`_pendingMeals` + `maxConsumesPerFrame` pacing to `LightFauna.UpdateBehavior` —
queue the eligible meals at the tick, drain a few per frame, re-validate at
drain, clear on death. Pacing only, throughput preserved (`/ecology` change,
same invariant statement as `19b7b5a4`). Expected: the 13.79 ms tick spreads to
~1–2 ms/frame at identical eating rate.

**Also on the 70 fps path:** `DomainVolumeIndicator.Update` reads 1.22 ms self
in this capture — the push gate helps static menus but during active feeding
the fills/cycle change every frame, so the per-push cost itself needs a marker
split next round (follow-up filed). `EventSystem.Update` 0.52 ms — the Raycast
Target Audit (Task 2) has still not been run in-editor. `DiagnosticsHUD` 0.64 ms
+ 21.5 KB is editor-only.

### 2026-07-09 fixes for the soak findings (SHIPPED — verify per §5 + the soak section)

All three soak offenders fixed in `e0735b2c` / `eaf107e0` / `75828ff0`:

1. **Creation tick (Task 4 CLOSED):** render entities now clone a cached
   Prefab-tagged prototype (ONE structural change, was ~8: CreateEntity + the
   RenderMeshUtility bundle + per-override AddComponentData + DisableRendering),
   and the lifecycle's show/hide toggles batch into a single LateUpdate flush
   (`PrismRender.VisibilityFlush` marker) using the batch Add/RemoveComponent
   APIs — two structural changes per frame regardless of prism count. Expect
   `Prism.Create.Visibility` to collapse from 0.35–0.66 ms/prism to ~0.05 ms
   and to STOP scaling with entity count. VFX pools and exotic-visual
   hand-offs keep immediate toggles (no double-draw window).
2. **Collider LOD:** classification is one Burst `LodClassifyJob` per 0.25 s
   tick over the packed array (~0.1–0.3 ms at 25k, `.Run()` like every other
   index query — no races), maintaining a per-slot `PrismFlags.LodNear` bit
   and emitting near/far **transitions only**; the manager applies them under
   `maxColliderTogglesPerFrame` (512). Expect `PrismColliderLodManager.Update`
   to fall from 5.54 ms slice-frames to sub-0.5 ms ticks with nothing between
   ticks. Slot-reuse staleness self-heals within one tick (same tolerance
   class as before); reconcile/restore-all/kill-switch semantics unchanged.
3. **Pool refills:** maintenance requests one engine-timesliced
   `InstantiateAsync` batch (integration spread by the engine; clones
   incubate at y = −100 km so nothing can flash in-scene), prism pools
   deepened to 120 buffer / 40 s⁻¹ / 300 max, and Awake prewarms only
   `defaultCapacity` (deep buffers no longer cost scene-load hitches — the
   async loop tops up over the first seconds). On-demand misses now show as
   `PoolMiss.<prefab>` — expect them in the first seconds after load and then
   never again in steady state; the 4.38 ms `UniTaskLoopRunnerUpdate`
   spawn-tick hits should disappear once the buffer holds. `maxSize` is
   clamped ≥ buffer target at Awake so config drift can't create an
   instantiate/destroy churn loop.
   **Instance-preparation contract:** the async batch bypasses the virtual
   `CreateFunc`, so subclass per-instance setup must live in
   `GenericPoolManager.OnInstanceCreated(T)` — invoked on BOTH creation paths
   (sync `CreateFunc` and each async clone as it leaves the incubator).
   `ProjectilePoolManager` does its Reflex `InjectRecursive` there; overriding
   `CreateFunc` for setup is a regression trap (async-refilled Sparrow
   projectiles shipped un-injected → null `AudioSystem` NRE in
   `LaunchProjectile` → dead guns/missiles, fixed in `e146b882`).

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
| `e0735b2c` | Render entities instantiate from Prefab-tagged prototypes (1 structural op, was ~8); prism visibility toggles batched into a LateUpdate flush (2 structural ops/frame total) |
| `eaf107e0` | Collider-LOD classification is one Burst pass emitting transitions via the `LodNear` flag bit; managed cost O(changed) under a 512/frame toggle budget (was 5.5 ms managed slice frames) |
| `75828ff0` | Pool refills via engine-timesliced `InstantiateAsync` batches; `PoolMiss.*` markers on on-demand misses; prism pools 120 buffer / 40 rate / 300 max; prewarm = defaultCapacity only; maxSize clamped ≥ buffer |
| `e146b882` | FIX from Sparrow regression: async refills bypass `CreateFunc`, so subclass instance-prep moved to the new `OnInstanceCreated(T)` hook (fires on both creation paths); `ProjectilePoolManager` DI injection lives there — un-injected async projectiles were NRE-ing on launch (dead Sparrow guns/missiles) |
| `019eb3c0` | Flora grow-tick instantiation paced across frames (decision at tick, drain 1/frame); branch walk de-LINQ'd |
| `5da47650` | Spindle condense/evaporate fades via shared MaterialPropertyBlock (was a Material clone + Destroy per fade) |
| `d4f696ae` | Creation-tick split markers: `Prism.Create.Visibility` / `.SOAPRaise` / `.SpatialBind` (Task 4 attribution) |
| `481a7ad8` | Domain-volume gauge: colors on sample cadence, push gated on real change (was 1.02 ms/frame flat) |
| `fb5e6643` | Cell volume recompute is one Burst `CellVolumeSumJob` over the index's new packed summation view (`PrismCellData`); managed 8000-prism slice deleted (was a ~10 ms reader-attributed spike billed to `DomainVolumeIndicator.Update`); `Cell.VolumeSum` + `DomainVolumeIndicator.Sample`/`.Push` markers (closes TODO C2) |
| `71b51a28` | Collider-LOD hysteresis band (`lodExitRadiusMultiplier`, kills moving-focus boundary flapping); drain budget charged per re-validated entry (was unbounded transform reads on churny queues); `LOD.Sweep`/`LOD.Drain` markers; LOD tick de-phased half an interval from the volume recompute |
| `bec6338c` | `AIPilot` + `AICinematicBehavior` `enabled` mirrors their active state — zero `Update()` dispatch on vessels not actively AI-driven (was every vessel, every frame, early-out); `StartAIPilot` idempotent (repeated activations stacked duplicate ability/seek coroutines forever); `StopAIPilot` uses `StopAllCoroutines` (the old `StopCoroutine(new enumerator)` was a no-op) |
| `6e899993` | Non-local players' `InputController.enabled = false` at pair-init — AI/remote copies no longer poll the physical devices per frame nor raise duplicate global button events |
| `ce11eaf6` | `R_VesselActionHandler` button-event subscription idempotent — init + input-unpause both subscribed, so every button press dispatched its actions (and RPC round-trip) twice |
| `e75569d3` | Drift/AI-pilot trims: `StopAIPilot` early-out when already stopped (per-turn-start native calls skipped); `ToggleAIPilot` cinematic stop via `TryGetComponent` (no component instantiated just to no-op stop it); trigger rest-remap short-circuits to identity on healthy pads; `DriftAudioController` self-disables on class-gate fail (was a permanent early-out `Update`) |
| `f0ddfc21` | Batched pure-entity debris: prism-death explosion VFX spawn as ONE `em.Instantiate(prototype, N)` batch per frame (`PrismDebris` + `PrismRenderService.SpawnExplosionDebrisBatch`), retire via time-ordered sweep into ONE batched destroy — no GameObject/pool/per-effect timer per death, full 5s duration always. Root cause it kills: a lifted-throttle 30³ blast put 2,408 deaths in one frame, all pool misses, `PrismExplosion.OnDisable` alone 1,863 ms (its `EnabledInstances` `List.Remove` O(n) scan — now O(1) swap-remove on both effect classes for the surviving pooled uses). `PrismDebris.Drain`/`.Sweep` markers. Remainder (implosion port, `AOE.ResolveDamage` 0.43 ms/kill self + per-kill `PrismEventData` alloc): `Docs/PRISM_CLOCK_FOLLOWUP_PROMPTS.md` Prompt 9 |

---

## 2. Conventions (locked — do not relitigate)

- **The fix pattern is: slice + per-frame budget + atomic publish.** Spread a
  hot pass across frames with a rotating cursor and a serialized per-frame
  budget; publish results atomically so consumers never see a half-updated
  state. Reference implementations: `Prism.CreateBlockCoroutine`
  (creation-completion budget), `Boid._pendingMeals` (consume queue +
  `maxConsumesPerFrame`). Prefer this over one-frame passes and over
  "adaptive skipping". **Escalation when the pass is a linear scan over
  per-prism data:** a managed slice budget only caps the spike — the
  per-entry interop/object-graph constant (~1 µs) makes even one slice
  multi-ms at an 8000/frame budget. Burst the whole pass over
  `PrismSpatialIndex`'s packed arrays instead and the slicing machinery
  disappears: `PrismColliderLodManager` (`LodClassifyJob`, 5.5 ms slice
  frames → 0.1-0.3 ms) and `Cell.EnsureVolumeFresh` (`CellVolumeSumJob`,
  ~10 ms slice frames → one sub-ms pass per 0.25 s, `fb5e6643`) are the
  reference collapses.
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
- **Markers added this session**: `Prism.Create.Visibility` / `.SOAPRaise` /
  `.SpatialBind` (creation-tick split — Visibility was the verdict);
  `PrismRender.VisibilityFlush` (batched entity toggles, LateUpdate);
  `PoolRefill.<prefab>` (maintenance refills — async request only),
  `PoolMiss.<prefab>` (on-demand Get miss = buffer empty on the caller's
  frame), `PoolActivate.<prefab>` (pooled Get `SetActive(true)` — first-Awake
  vs OnEnable attribution).
- **DiagnosticsHUD**: "Animators" section shows `active / registered` per
  animation manager, live (updates only when the count changes — no GC).
  Console commands: `prisms N` / `prisms off` / `prismcolors`.
- **Collider-LOD telemetry**: `PrismColliderLodManager.LastNearCount` /
  `LastLiveCount`.
- **Shell-contact tier markers**: `ShellContact.Build` (per-frame probe
  rebuild from live collider poses), `ShellContact.Query` (the synchronous
  Burst `ShellContactQueryJob` schedule+complete inside
  `PrismSpatialIndex.CollectShellContacts`), `ShellContact.Dispatch`
  (enter/exit resolution + `AcceptImpactee` effect chains). Per-impactor
  `<Type>.AcceptImpactee` markers cover shell dispatches too (the shell tier
  routes through the same lazy marker). A/B switch:
  `PrismShellContactManager.ForceLegacyBoxInteraction` reverts shielded
  interaction to the authored box trigger (see Docs/SPATIAL_INDEX.md § Shell
  view).
- **Benchmark tool**: `Assets/_Scripts/Utility/PerformanceBenchmark/`
  (`BENCHMARK_TOOL.md` — tabs, score/hints, sweep).
- **Raycast audit tool**: `FrogletTools > Interface > Raycast Target Audit`
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
menu `FrogletTools/Interface/Raycast Target Audit` (line 33). Classifier
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

### Task 5 — LightFauna grazing pacing parity (`/ecology`) ✔ SHIPPED (2026-07-09)

**Status:** SHIPPED — `337443f0`, evidence was the 2026-07-09 capture (13.79 ms
/ 4 ticks, 9.27 self, 12.8 KB, `GameObject.Activate` ×16 in the cascade). The
behavior tick enqueues every edible prism it finds (identical eligibility);
`maxConsumesPerFrame` (serialized on LightFauna, default 8, ≤0 = legacy burst)
drains per frame with the edibility predicate re-checked at drain; queue clears
on death (uneaten meals stay in the world). Predation stays inline. Mechanics
log updated (`Docs/ECOSYSTEM.md` §12 "Consume pacing"). Same commit adds
`PoolActivate.<prefabName>` markers around pooled Get activation — the next
capture names which pool owns the 0.27 ms activations and whether deferred
first-Awake (async incubation) or heavy OnEnable dominates; if first-Awake, the
designed follow-up is an Awake-warm at refill completion.
**Verify (in-editor):** menu with fauna feeding — `LightFauna.UpdateBehaviorCoroutine`
should drop from ~13.8 ms tick-frames to ≤ ~2 ms, with the drain visible as small
`LightFauna.Update` slices on following frames; a grazed cluster melts over a few
frames instead of vanishing in one; fauna still feed (watch starvation — tune
`maxConsumesPerFrame` up if a berserk-cadence fauna ever starves with a full
queue, which the math says cannot happen at default settings); fauna death
mid-graze withers normally.

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
| DomainVolumeIndicator residual | `…/UI/DomainVolumeIndicator.cs`: per-frame `StepTowardTargets` exp-lerp + push, delta-gated downstream (`DomainVolumeHexGraphic.SetState`, ε=0.002); targets sampled at 0.25 s | Sub-ms; now split under `DomainVolumeIndicator.Sample` / `.Push` markers (`fb5e6643`) — the historical multi-ms readings were `Cell.EnsureVolumeFresh` billed to this row (TODO C2, resolved). Revisit only if `.Push` itself reappears in captures. |

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
- `Docs/ECOSYSTEM.md` / `Docs/SPATIAL_INDEX.md`: note the patterns now
  standard — collider-LOD classification and the cell volume recompute are
  Burst passes over the index's packed arrays (transitions-only / atomic
  publish); densest-region job async on a snapshot. (`SPATIAL_INDEX.md`
  documents the summation view as of `fb5e6643`.)

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
| 2026-07-09 (handoff) | Session closed at head `eb707175`, all pushed, tree clean. Added §0 SESSION HANDOFF: shipped list (8 fix groups), TODO A (user measurements: DOTS-round steps 2–6, Task 5 checks, three open datums, raycast audit, shader recording, HexRace pass), TODO B (datum-gated code: Awake-warm, async-refill revisit, Visibility fallback), TODO C (ungated: Task 6 retire, gauge push split, Task 7 micro-items, Task 8 hygiene, stale pool prefabs, cadence-floor decision item), and the session lesson list. Instrumentation inventory updated with the six new marker families. |
| 2026-07-09 (Task 5 round) | Task 5 shipped (`337443f0`) + re-verification caught one substantive issue, fixed in the follow-up commit: LightFauna ticks far faster than Boid (cadence floor 0.05 s × Frenzy ×0.25 — the floor is applied BEFORE the multiplier, a pre-existing quirk worth knowing), so carrying `_pendingMeals` across ticks re-enqueued every still-live prism as a duplicate — a dead-dupe backlog that burned drain budget and could spuriously starve a fauna standing in food. Fix: the queue is REBUILT from the live scan each tick (clearing loses nothing — the scan re-finds anything uneaten), and `EatPrism` returns bool so only actual consumes spend the frame budget. Accepted note (matches Boid, by design): consume radius is not re-checked at drain — a fast fauna can suck in a prism it just passed; visually fine. `PoolActivate.*` marker init order, release-build zero-cost, and death/starvation edges all verified sound. |
| 2026-07-09 (DOTS round) | Shipped the three soak fixes: prototype-instantiated render entities + batched visibility flush (`e0735b2c`), Burst LOD classification with transition-only apply (`eaf107e0`), async timesliced pool refills + `PoolMiss.*` + deeper buffers (`75828ff0`). 4-agent adversarial re-verification found 1 blocker + 9 real/minor findings → fixed in `9b891d40`: immediate SetVisible/Destroy cancel queued toggles (stale queued show = ghost box through a shield morph); pending-visibility map cleared on world invalidation; flush host un-leaked + late execution order; LOD sweeps never blocked by the cull backlog (restores immediate + cancel queued culls; culls drain budgeted with live foci re-validation at apply); `Prism.Restore` clears stale `_lodCulled`; pool clones incubate under an INACTIVE parent (no Awake/OnEnable at integration — a pooled Projectile's OnEnable registers an LOD focus); wall-clock refill timer; narrowed async fallback catch. Accepted notes: `LastNearCount` is approximate telemetry between reconciles; implosion pool fills to 160 async over first seconds (own 64 min-prewarm covers bursts); 6 unreferenced `_Prefabs/Pools/*PrismPool.prefab` siblings still carry old values (unused — candidates for deletion); Entities/UnityEngine API signatures verified from knowledge (PackageCache absent in this checkout) — the first editor compile is the real gate. |
| 2026-07-09 (flora round, re-verified) | Adversarial pass over the four commits. Markers + gauge gating: SOUND. Two real bugs fixed in `fc9c53f3`: (1) the flora drain outlived `LifeForm.Die` (`StopAllCoroutines` killed the old GrowCoroutine spawn site but not `Update`) — a dying flora kept spawning through its wither window (zombie flora / child pop-out); `Die` override clears the queue. (2) Spindle's evaporate renderer-gone early-out could bail before `DisableSpindle`, hanging `DieCoroutine`'s empty-tracker wait — removed. Hardening: grow orders carry `decidedAt` and drop past `ReservationTtlSeconds − 1` (Frenzy holds could outlive the 5 s claim → overlap risk); drain freezes at `timeScale 0` (parity with the old `WaitForSeconds` loop). **Correction of record:** a material clone stays SRP-batchable; an MPB excludes the renderer for the fade duration — the spindle win is zero material create/destroy churn, at the cost of unbatched draws *during* fades. Re-measure in the next capture; fallback is quantized shared fade materials (phase-variant pattern). Accepted nuances: gauge ring can lag ≤1 rebuild-epsilon (~1.4°, under segment granularity); theme-swap colors up to 0.25 s latent; per-tick flora throughput can under-fill only when a parent dies or an assembler fails mid-window (rare, disclosed). |
| 2026-07-09 | 6-agent adversarial re-verification of the five commits (pre-editor-test). **3 blockers found in the fresh Task 1 commits, fixed in `d80e7ee5`:** `dtNominal` 0.02 → 0.04 (project Fixed Timestep is 0.04 — growth was ~1.9× too fast as committed; two agents converged on it independently); fixed 0.5 s dt cap → rotation-scaled cap (fixed cap slowed tempo up to 7× under the target frenzy load); cursor drift on removal bursts corrected. GC coverage gaps (clients + Play Again reloads never took the scheduled collect) fixed in `4443df83` via the two all-peers post-load fade-back handlers. WaitForSeconds cache, pool marker verdicts: SOUND (notes: cache misses on the variable-`waitTime` `waitTillOutsideSkimmer` path — acceptable; prewarm burst unmarked — intentional). Final compile-sanity pass over all six edited files: PASS (arithmetic invariants proven, `MaterialStateManager` independent, no external readers of the sliced behavior). Lesson recorded: never assume Unity's 0.02 default fixed timestep — check `TimeManager.asset`. |
| 2026-07-17 (Sparrow regression) | The `75828ff0` async refill broke Sparrow guns/missiles once merged to bleeding-edge: `InstantiateAsync` bypasses the virtual `CreateFunc`, the only place `ProjectilePoolManager` ran Reflex injection, so async-refilled projectiles carried a null `AudioSystem` and NRE'd in `LaunchProjectile` (stack pool = un-injected instances hand out FIRST; Sparrow missile pool prewarms 5 toward a 20 target, so the pool top went dud within seconds). Fixed in `e146b882`: new `GenericPoolManager.OnInstanceCreated(T)` hook fires on both creation paths; subclass instance-prep (DI injection) must live there, never in a `CreateFunc` override. §1 item 3 updated with the contract. |
| 2026-08-06 (pause + menu-return round) | Two reported UX stalls fixed. **Pause tap hitch:** the pause panel (`R_Pause_Menu_Panel`) starts inactive in every scene, so the FIRST tap paid the whole hierarchy's Awake/OnEnable + layout + TMP mesh generation mid-gameplay. Shipped `PauseMenu.Prewarm()` — the panel is activated invisible (root CanvasGroup alpha 0) for two frames at scene start and deactivated again; called from `MiniGameHUD.Start` (gameplay scenes) and `MenuMiniGameHUD.InstantiatePauseMenu` (menu freestyle). **Game→menu return:** (1) `SceneLoader.ReturnToMainMenu` now unpauses first (mirrors `LaunchGame`) — the pause-menu Main Menu button previously ran the whole transition at `timeScale 0`; (2) connected clients are covered BEFORE the teardown via `MultiplayerMiniGameControllerBase.BroadcastReturnToMenuVeil` → `ShowReturnToMenuVeil_ClientRpc` (mirror of the replay path's `PrepareForSceneReload_ClientRpc`; RPC + despawn messages share the reliable channel, so the veil always lands before vessels pop out — clients previously watched the whole despawn + scene switch uncovered); (3) on a game→menu arrival (previous loaded scene was a gameplay scene — tracked per-peer in `SceneLoader`), the opaque splash is HELD for `menuReturnSettleSeconds` (1.5 s, serialized) after `OnClientReady` so end-of-session cleanup finishes behind the veil instead of visibly clearing after the fade; the all-peers covered `GC.Collect` moved after the settle window so settle churn is collected too. First boot, auth→menu, party join, game launches, and replay reloads are not delayed (flag armed only on game→menu, cleared by `LaunchGame`). Remaining known stall: Menu_Main's synchronous scene-activation Awake/Start cost still freezes the splash spinner for its duration — structural, not addressed this round. |
