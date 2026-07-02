# Performance — Ledger, Methodology & Open Opportunities

The single index for performance work in Cosmic Shore. It does three things:

1. **Methodology** — how to find and prove a performance win (the benchmark tool +
   Unity Profiler, used together). Profile first; never guess.
2. **Completed-work ledger** — every performance change that has landed, grouped by
   workstream, with commit evidence and live-code status. This is the "what has
   already been tried" record so the same ground isn't re-dug.
3. **Open opportunities** — the remaining wins, ranked, each with how to verify it.

This doc is a **map, not a duplicate**. The deep dives stay authoritative:

| Topic | Authoritative doc |
|---|---|
| The benchmark tool (4 tabs, HUDs, dev-build capture) | `Assets/_Scripts/Utility/PerformanceBenchmark/BENCHMARK_TOOL.md` |
| Benchmark architecture (collector, schema, analysis) | `Assets/_Scripts/Utility/PerformanceBenchmark/BENCHMARK_ARCHITECTURE.md` |
| Prism system audit (the hot core) | `Assets/_Scripts/Game/Prisms/PRISM_PERFORMANCE_AUDIT.md` |
| Prism spatial index (AOE / occupancy / neighborhood) | `Docs/SPATIAL_INDEX.md` |
| Fauna density-targeting redesign | `Docs/DENSITY_PARTITIONING_AUDIT.md` |
| Threading / main-thread affinity (stability, not throughput) | `Docs/THREADING.md` |
| Collider budget contract | `Docs/ECOSYSTEM_MASTERPLAN.md` §4 |

> **Status note (kept honest):** the live-code status columns below were verified
> against the source on the date of writing, because `PRISM_PERFORMANCE_AUDIT.md`'s
> own "Progress Update (March 2026)" section had gone stale (it lists Rec 1 as
> unimplemented when `PrismEffectsManager` shipped it; Rec 4 and 5 are also done). When
> you land a change here, update both this ledger and the relevant deep-dive doc.

---

## 1. Methodology — benchmark + Profiler, used together

The two tools answer different questions. The **benchmark** scores a run, names the
worst spikes by script self-time, and lets you *prove* a delta (Compare tab). The
**Unity Profiler** drills into *why* a specific marker is hot (Timeline self-time, GPU
module, Memory module). Use the benchmark to find the suspect, the Profiler to convict
it.

### The loop

1. **Pick the heaviest real scenario** for the system under test (see §3 of
   `BENCHMARK_TOOL.md` for tabs). The bottleneck in a 12-player Crystal Capture is not
   the bottleneck in Menu_Main idle — baseline the scenario you actually care about.
2. **Baseline (benchmark).** Runtime Capture tab, *Capture spike breakdowns ON*, enter
   Play Mode, `● Start Recording` → play the scenario → `■ Stop & Analyze` →
   `📋 Copy error log` → **Save** → History → **Tag `baseline`**.
3. **Ground-truth check.** Editor Play-Mode frame time is inflated ~2–3×. Confirm the
   suspect on a **Development Build standalone** (`-csmbench` self-runner, or F7
   DiagnosticsHUD → F5 Run Diagnostic on device). Low-overhead mode + Profiler closed
   gives the truest editor smoothness read.
4. **Drill (Profiler).** Map the benchmark's verdict to the right Profiler module
   (table below). Narrow to the **derived class** — base-class profiling hides the real
   culprit. Use `ProfilerMarker` with `using (marker.Auto())`, never manual
   `Begin/EndSample`.
5. **Fix, then prove (benchmark).** Capture again, same source/scenario → **Compare**
   tab against `baseline`. Same-source deltas only (Compare warns on Editor-vs-DevBuild).
   A green Compare is the deliverable that proves the optimization.

### Benchmark verdict → Profiler module

| If the benchmark blames… | Open this Profiler module | Look for |
|---|---|---|
| GPU-bound / high Draw Calls, SetPass | **GPU** + **Rendering** | Per-object draw calls, SetPass batches, overdraw; Timeline render-thread self-time |
| `Physics.Processing`, broad phase | **Physics** | Active collider count vs the per-cell collider budget |
| GC spikes, `GC.Collect`, memory slope | **Memory** ("GC Allocated In Frame") | Per-frame managed allocs; cross-check the benchmark's collector-overhead self-check (~0 B/frame) |
| `CSM.Net.*`, high RPCs/NetVars/bytes | **CPU Timeline** + benchmark netcode panel | Per-frame RPC count, NetVars-dirty, bytes — needs a real 2-peer / MPPM session |
| Script self-time in a named method | **CPU Timeline** | Self-time vs total; is it a hot per-object loop that should be batched? |

### Rules (from CLAUDE.md)

- **Profile first.** "Do not guess at performance problems."
- **`Debug.Log` is a diagnostic, not a fix.** Don't leave logging as the "solution."
- **Test before and after** — don't assume improvement; the Compare tab is the proof.
- **`sharedMaterial` + MaterialPropertyBlock**, never `renderer.material`.
- New spatial queries against prisms go through `PrismSpatialIndex`, never
  `Physics.OverlapSphere`/`CheckBox` (see `Docs/SPATIAL_INDEX.md`).

---

## 2. Design constraints that bound performance work

Some "obvious" optimizations are **forbidden** here because they violate locked
platform invariants. Know these before proposing a fix, or you'll re-propose a rejected
cheat.

- **Mass is conserved — no prism count caps, TTLs, decay, or idle cullers.** A prism is
  removed only by an *active* force (a vessel ability or fauna consumption). A large
  prism accumulation is a *valid* equilibrium, not a leak to auto-correct. If prism
  count is a perf problem, the levers are **fauna cleanup** (foragers eat trail mass) or
  **pause/throttle the spawner** — never aging mass out. This is why the audit's
  original "global prism budget / recycle oldest prisms" (Rec 8) is **rejected by
  design**, and why the menu trail ring-buffer cap (`64d8f0c8`) was **reverted**. See
  CLAUDE.md "Don't cheat emergence" + `Docs/ECOSYSTEM.md`.
- **Collider budget is the real budget.** Because mass can't be capped, the active
  *collider* count is bounded instead — proximity collider-LOD (`PrismColliderLodManager`),
  not a prism cap. No ecology feature ships without stating its active-collider impact.
- **Universality — one rule set everywhere.** No "it's only the menu / it's cosmetic"
  performance carve-outs. The lava lamp *is* freestyle gameplay; solve its perf with the
  universal systems (fauna, spawner throttle), not a context-local mechanism.
- **Continuity of existence.** Spawns/deaths animate in/out — no instant `Instantiate`-
  then-show or bare `Destroy`. Perf fixes must respect the grow/wither transitions.

---

## 3. Completed-work ledger

Status legend: ✅ shipped · ⚠️ partial / superseded · ❌ rejected by design.

### 3.1 Prism system (the hot core)

The most performance-critical system; most optimization investment lives here. Per-prism
cost is the scaling wall (each prism is a GameObject with 5–6 MonoBehaviours + collider +
renderer). Full analysis: `PRISM_PERFORMANCE_AUDIT.md`.

| Change | Status | Where |
|---|---|---|
| Burst `IJobParallelFor` scale-animation batching (batch 128) | ✅ | `PrismScaleManager` |
| Burst `IJobParallelFor` material color/spread batching | ✅ | `MaterialStateManager` |
| Dynamic frame-skip (1×–12×) under perf pressure | ✅ | `AdaptiveAnimationManager` |
| Centralized timers replace per-prism shield/create coroutines | ✅ | `PrismTimerManager` |
| Burst-jobbed explosion/implosion VFX (`UpdateExplosionsJob`) | ✅ | `PrismEffectsManager` |
| Per-frame VFX spawn cap (`MaxExplosionVFXPerFrame = 64`) | ✅ | `PrismFactory` |
| `sharedMaterial` everywhere (material-clone leaks removed) | ✅ | `MaterialPropertyAnimator` |
| Unified spatial index: Burst AOE + occupancy + neighborhood | ✅ | `PrismSpatialIndex` (`38c16963`) |
| Proximity collider-LOD by focus (bounds active collider count) | ✅ | `PrismColliderLodManager` |
| `EventListenerBase` GC elimination | ✅ | `EventListenerBase` |

These map to audit Recommendations 1, 4, 5, 6, 7 (all shipped). See §4 for the corrected
recommendation status and what remains.

### 3.2 Fauna density-targeting (`Docs/DENSITY_PARTITIONING_AUDIT.md`)

A benchmark-driven redesign of "where is the enemy mass?" for fauna seeking. Phase 1
(`c058663`) + Phase 2 (`9df956f`):

- Grid **sized to the cell** (the shipped 1000m cube saw ~14% of a 2400m cell — root
  cause of fauna ignoring outer-shell mass).
- Separable 3×3×3 box smoothing + sub-voxel **parabolic interpolation** (argmax noise:
  ~100m → ~28m error).
- Adaptive resolution targeting **75m physical voxels** (Nyquist on the 150m kernel).
- **Voxel-weighted mean-shift** centroid refinement (5 iter) in `FindDensestRegionJob`.
- **Result caching** — dirty flag + 0.25s min recompute (was running the full job per
  fauna per query; 100s of fauna × 0.5–2s cadence = 100s of redundant runs/sec).
- Storage hardening: direct `NativeArray` writes (managed mirror + per-query copy gone),
  `ushort` counts (byte overflowed at 255; cells reach 10K+ prisms).
- Tooling: `DensityPartitionBenchmarkRunner` (geometric) + `DensityPartitionTemporalSimRunner`
  (ecology) — Edit-Mode, re-runnable to re-validate any future change.

### 3.3 Ecology / Menu_Main idle

| Change | Status | Commit |
|---|---|---|
| Menu 5fps fix (Blob prism ceiling + fauna caps + headless tuning loop) | ✅ | `6b936df0` |
| Spread initial flora/fauna spawn batches across frames | ✅ | `6b136b3e` |
| Tame the menu food web so gyroids stay sizable | ✅ | `44bc7fea` |
| Fauna reproduction retires the fixed-period spawner | ✅ | `78972ee5` |
| Lava-lamp autopilot trail ring-buffer cap | ❌ reverted (mass-conservation cheat) | `64d8f0c8` |

### 3.4 Targeted hot-path fixes

| Change | Commit |
|---|---|
| HexRace `FindObjectsByType<Crystal>` → live-crystal registry | `68afb42c` |
| Concrete `ImpactCollider` lookup in `OnTriggerEnter` (no per-hit `GetComponent`) | `443c4ffd` |
| Kill SOAP debug-log spam, throttle prism VFX audit, gate spawn logs | `8f84c802` |
| Bake CapsuleMembrane wobble into a reusable preset | `9dbd2f77` (PR #523) |
| ObjectiveIndicator optimization | PR #520 |
| Audio optimization | `a7f426af` |
| Uncap frame rate (`BootstrapConfig` targetFrameRate −1, vSync off) | `67fffa78` |

### 3.4b July 2026 verified-review batches

The July 2026 codebase-wide review (`Docs/PERFORMANCE_REFACTOR_REVIEW.md`) landed four
fix batches plus a Wave-3 extraction sweep re-porting the still-open wins from nine
unmerged optimization branches (`Docs/PERF_BRANCH_MERGE_PLAN.md` has the full audit).
Highlights: `PrismActivationQueue` (spawn-coroutine thundering herd → bounded central
queue), O(1) trail indexing, single turn-monitor driver, allocation-free `GameDataSO`
lookups, Spindle/Crystal material-clone elimination + leak fixes, `DomainVolumeIndicator`
sub-canvas isolation, `GameEventFeed` row pooling, spawnable cache-key correctness fix,
shield-SFX frame coalescing, super-shield 24→8-face mesh, and a menu-UI sweep. This
substantially addresses §5-E (per-frame managed allocations) for the audited paths —
re-profile before chasing more.

### 3.5 Measurement infrastructure (meta-performance)

The whole `Assets/_Scripts/Utility/PerformanceBenchmark/` suite is itself a large
performance investment — you can't optimize what you can't measure. Highlights:

- Editor window rebuilt to Runtime Capture / Sweep / History / Compare tabs (`cbd4726e`),
  later record-only Runtime Capture + live spikes + Copy-for-Claude.
- **Zero-alloc end-of-frame collector**; spike analysis moved **off the game frame** so
  capturing never becomes the spike it measures.
- **Netcode (NGO) instrumentation** — `NetMarkers` (`CSM.Net.*`) at central hot paths +
  RPC/NetVar/bytes counters; report schema + Compare support.
- **Dev-build self-capture** (`-csmbench`) + History import + cross-source guard.
- **DiagnosticsHUD** (F7) in-build overlay with live CPU/GPU **bound verdict** + memory
  detail (`850a3137`, `7b00887e`); standalone **ProfilerCsvLogger** (`09ece59d`).

### 3.6 Threading (stability, not throughput)

`MainThreadDispatcher` + `.AsMainThread()` at every UGS/Netcode `await` resolved the
off-thread SOAP-raise crash cascade. Not a frame-time optimization, but it removed a
class of hangs/crashes under async load. See `Docs/THREADING.md`. Do **not** use
`UniTask.SwitchToMainThread()` / `Yield(PlayerLoopTiming.Update)` as a marshaling fix —
proven unreliable on this UniTask version.

---

## 4. Prism audit recommendation status (corrected)

`PRISM_PERFORMANCE_AUDIT.md` lists 8 recommendations. Verified against live code:

| # | Recommendation | Status | Note |
|---|---|---|---|
| 1 | Batch explosion VFX into a Jobs manager | ✅ done | `PrismEffectsManager` (`UpdateExplosionsJob`, Burst) |
| 2 | **GPU-instanced explosion rendering** | ❌ **open** | Jobs compute positions, but each explosion is still a GameObject → N draw calls + N `transform.position` writes + N `SetPropertyBlock`/frame on the main thread. Biggest un-taken prism win. |
| 3 | **Full DOTS/ECS prism conversion** | ❌ open | Highest payoff, highest risk. Last resort. |
| 4 | Fix material instancing leaks | ✅ done | `sharedMaterial` everywhere in `MaterialPropertyAnimator` |
| 5 | Cap concurrent/per-frame explosion effects | ✅ done | `PrismFactory.MaxExplosionVFXPerFrame = 64` |
| 6 | Replace per-prism coroutines with timers | ✅ done | `PrismTimerManager` |
| 7 | Spatial partitioning for collisions | ✅ done | `PrismSpatialIndex` (AOE) + `PrismColliderLodManager` (collider LOD) |
| 8 | Global prism budget (recycle oldest prisms) | ❌ rejected by design | Conflicts with mass conservation (§2). The collider budget (Rec 7) is the real budget; prism *count* is never capped. |

---

## 5. Open opportunities (ranked)

Each is a hypothesis to confirm with a profile, not a committed task. Profile first.

### A. GPU-instanced explosion/implosion rendering (audit Rec 2) — highest confirmed win
The Jobs *compute* is done; the *rendering* is not. During mass destruction, N explosion
GameObjects = N draw calls + a main-thread loop doing N `transform.position` writes + N
`SetPropertyBlock` calls every frame for up to ~5s each.
- **Fix:** `Graphics.RenderMeshInstanced` with per-instance MPB arrays (position, scale,
  `_ExplosionAmount`, `_Opacity` already computed by the Burst job). Collapses N draw
  calls → 1 and removes the per-object transform/MPB writes. Option B: a VFX Graph burst
  (GPU-simulated, near-zero CPU after spawn) — different visual, lower effort.
- **Verify:** Profiler **GPU** + Rendering (Draw Calls) during a big trail kill; the
  benchmark `boundVerdict` should read GPU-bound. Baseline → change → Compare draw calls.
- **Files:** `PrismExplosion.cs`, `PrismImplosion.cs`, `PrismEffectsManager.cs`,
  `PrismFactory.cs`.

### B. Collider-budget validation under load
`PrismColliderLodManager` bounds active colliders by focus proximity. Confirm it actually
holds the per-cell budget in the worst case (many vessels + projectiles in a dense cell).
- **Verify:** Profiler **Physics** module (`Physics.Processing`, active dynamic bodies)
  in a max-player game in a prism-dense cell. If broad phase is still hot, the focus
  radius or LOD cadence is the knob.

### C. Menu_Main lava-lamp idle (ecology)
Known 5fps history; partially addressed (§3.3). Trail caps are forbidden (§2), so the
levers are fauna cleanup + spawner throttle + **fauna swarm-reach tuning** — the one open
item from `DENSITY_PARTITIONING_AUDIT §7.4–7.5` (fauna idle next to un-eaten shell mass
when swarm reach < cluster σ). Measurable in an afternoon: instrument max pairwise
pack-member distance per density-query tick in Menu_Main.
- **Verify:** Runtime Capture in Menu_Main idle (low-overhead mode); CPU Timeline on
  fauna density queries + prism growth. Watch prism count trend over a few minutes.

### D. Netcode at max players (not yet profiled at scale)
The `CSM.Net.*` instrumentation exists but hasn't been run at 12 players. RPC storms /
NetVar-dirty churn / bytes-per-frame are unmeasured.
- **Verify:** MPPM or two devices, full game, benchmark **netcode panel** + the
  `CSM.Net.*` markers in the Profiler. Look for per-frame RPC spikes and NetVar fan-out.

### E. GC / per-frame allocations
The collector self-checks ~0 B/frame, but gameplay paths may still allocate.
- **Verify:** Profiler **Memory** "GC Allocated In Frame" in a busy scene; chase any
  per-frame managed alloc (closures, boxing, `new` in Update, LINQ in hot paths).

### F. Full DOTS/ECS prism conversion (audit Rec 3) — last resort
Only after A–E are exhausted and the Profiler shows per-prism MonoBehaviour /
managed-to-native marshaling is the wall. Highest payoff (10–50× prism ceiling), highest
risk (hybrid GameObject↔entity bridge for vessels/projectiles). Do it incrementally
(rendering first, then systems) if it's justified at all.

### G. Spatial index Phase 4 — bucket-accelerated AOE (candidate)
`Docs/SPATIAL_INDEX.md` roadmap Phase 4: only if AOE profiling ever demands it. Currently
the linear hot-array scan is fine for the population. Don't pre-optimize.

---

## 6. First action (current plan: profile-first, no target yet)

No single system is committed yet. The agreed next step is a **baseline sweep across the
heavy scenes** to let data pick the bottleneck rather than guessing:

1. Sweep tab (or per-scene Runtime Capture) over the heavy scenarios: a domain minigame
   at max players + AI, and Menu_Main idle. Capture spike breakdowns ON.
2. For each, Copy error log + Save + tag (`baseline-<scene>`).
3. Repeat the worst one in a **Development Build** for ground truth.
4. Read the top spikes → map to a Profiler module (§1) → pick the target from §5 by
   measured impact, not by this list's order.
5. Land the fix, prove it in Compare, update this ledger + the relevant deep-dive doc.
