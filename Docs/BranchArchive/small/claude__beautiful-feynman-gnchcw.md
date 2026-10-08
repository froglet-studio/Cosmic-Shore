# Branch archive: `claude/beautiful-feynman-gnchcw`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-06-19 by Claude
- **Unmerged commits:** 1
- **Forked from:** `b89ed52da` (2026-06-18, Merge pull request #559 from froglet-studio/claude/inspiring-franklin-xf2y5o)
- **Tip:** `b397a3607`
- **Files touched (3):**
  - `Assets/_Scripts/Game/Prisms/PRISM_PERFORMANCE_AUDIT.md`
  - `CLAUDE.md`
  - `Docs/PERFORMANCE.md`

### `b397a3607` — docs(perf): add Docs/PERFORMANCE.md ledger + methodology; correct stale audit status

_Claude, 2026-06-19 00:07:25 +0000_

```text
Consolidate the project's scattered performance history into a single index:
profiling methodology (benchmark tool + Unity Profiler, used together), a
completed-work ledger with commit evidence + live-code status, the corrected
prism-audit recommendation status, and ranked open opportunities.

Verified against source and corrected the stale notes in PRISM_PERFORMANCE_AUDIT.md:
- Rec 1 (Jobs explosion manager) shipped via PrismEffectsManager
- Rec 4 (material-clone leaks) resolved — MaterialPropertyAnimator uses sharedMaterial
- Rec 5 (per-frame VFX cap) shipped — PrismFactory.MaxExplosionVFXPerFrame = 64
- Rec 7 (spatial partitioning) shipped — PrismSpatialIndex + PrismColliderLodManager
- Rec 2 (GPU-instanced explosion rendering) and Rec 3 (DOTS) remain the open wins
- Rec 8 (global prism budget) rejected by design — conflicts with mass conservation

Register the new doc in CLAUDE.md's Documentation Index.
```

```text
 Assets/_Scripts/Game/Prisms/PRISM_PERFORMANCE_AUDIT.md |  37 +++++--
 CLAUDE.md                                              |   1 +
 Docs/PERFORMANCE.md                                    | 281 +++++++++++++++++++++++++++++++++++++++++++++++
 3 files changed, 308 insertions(+), 11 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 347 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Prisms/PRISM_PERFORMANCE_AUDIT.md b/Assets/_Scripts/Game/Prisms/PRISM_PERFORMANCE_AUDIT.md
index efd2a8026..8fd13975f 100644
--- a/Assets/_Scripts/Game/Prisms/PRISM_PERFORMANCE_AUDIT.md
+++ b/Assets/_Scripts/Game/Prisms/PRISM_PERFORMANCE_AUDIT.md
@@ -352,17 +352,32 @@ Similarly, replace `CreateBlockCoroutine` with a delayed-activation manager that
 
 ---
 
-## Progress Update (March 2026)
-
-Since the original audit, the following optimizations have been implemented or are in active development:
-
-- **PrismTimerManager** — centralized timer system replacing per-prism coroutines (Recommendation 6)
-- **Per-frame explosion VFX cap** — limits concurrent explosion effects (Recommendation 5)
-- **EventListenerBase GC elimination** — reduced garbage collection from event listener allocations
-- **PrismAOEData** — cache-line-aware data layout with hot/cold splitting and bit-packed flags for AOE queries
-- **Burst-compiled spatial queries** — replaces Physics-based AOE prism damage (partial implementation of Recommendation 7)
-
-Recommendations 1-3 (Jobs-based explosion manager, GPU instanced rendering, full DOTS conversion) remain unimplemented.
+## Progress Update
+
+> The consolidated, continuously-maintained status of every recommendation lives in
+> **`Docs/PERFORMANCE.md` §4** (verified against live code). The summary below is kept
+> in sync with it.
+
+Implemented since the original audit:
+
+- **PrismTimerManager** — centralized timer system replacing per-prism coroutines (Recommendation 6). ✅
+- **PrismEffectsManager** — Burst `IJobParallelFor` explosion/implosion VFX batching (Recommendation 1). ✅
+- **Per-frame explosion VFX cap** — `PrismFactory.MaxExplosionVFXPerFrame = 64` (Recommendation 5). ✅
+- **`sharedMaterial` everywhere** — `MaterialPropertyAnimator` no longer clones materials (Recommendation 4). ✅
+- **EventListenerBase GC elimination** — reduced garbage collection from event listener allocations.
+- **PrismSpatialIndex** (formerly `PrismAOERegistry`) — cache-line-aware hot/cold layout + Burst AOE
+  queries + occupancy reservations + neighborhood views; replaced `Physics.OverlapSphere`/`CheckBox`
+  across assemblers and fauna (Recommendation 7). Plus `PrismColliderLodManager` for collider-LOD. ✅
+
+Still open:
+
+- **Recommendation 2** (GPU-instanced explosion rendering) — the Jobs *compute* landed via
+  `PrismEffectsManager`, but rendering is still one GameObject per explosion (N draw calls + N
+  per-frame `transform`/`SetPropertyBlock` writes). This is the biggest remaining prism win.
+- **Recommendation 3** (full DOTS/ECS conversion) — unimplemented; highest payoff, highest risk.
+- **Recommendation 8** (global prism budget / recycle oldest prisms) — **rejected by design**: it
+  conflicts with the locked mass-conservation invariant (no prism count caps or TTLs). The collider
+  budget (Rec 7) is the real budget; see `Docs/PERFORMANCE.md` §2.
 
 ---
 
diff --git a/CLAUDE.md b/CLAUDE.md
index d7f81e4a2..6d5cc12be 100644
--- a/CLAUDE.md
+++ b/CLAUDE.md
@@ -290,6 +290,7 @@ MiniGameControllerBase (abstract, NetworkBehaviour)
 | `SCENES.md` | `Docs/` | Complete scene inventory, game modes, launch pipeline |
 | `THREADING.md` | `Docs/` | UniTask / SyncContext threading rules, `.AsMainThread()` contract, `MainThreadDispatcher`, canary, history |
 | `SPATIAL_INDEX.md` | `Docs/` | `PrismSpatialIndex` — THE canonical spatial index of prism mass (Burst AOE queries, growth occupancy reservations, bucket grid). **Read before adding any spatial query against prisms.** |
+| `PERFORMANCE.md` | `Docs/` | **The performance index.** Profiling methodology (benchmark tool + Unity Profiler, used together), the completed-work ledger (every perf change with commit + live-code status), corrected prism-audit recommendation status, and ranked open opportunities. **Start here for any performance work.** |
 | `PartySystem/` | `Docs/` | Party (Relay) layer: `ARCHITECTURE.md` (locked design, investigation Q&A, error-handling matrix, exit criteria), `REFACTOR.md` (active backlog + deferred items + per-commit protocol), `BUGS.md`, `TESTS.md`, `TODOS.md`. EAGER per-user Relay session is the locked design. |
 | `PresenceSystem/` | `Docs/` | Presence-lobby (discovery) layer: `ARCHITECTURE.md`, `REFACTOR.md`, `BUGS.md`, `TESTS.md`, `TODOS.md`. Lobby-only UGS session, coexists with NetworkManager. |
 | `NetworkDiagnostics/` | `Docs/` | NetDiag overlay: `ARCHITECTURE.md` (NetworkMonitor + `NetworkDiagnostics` helper, classification rules), `TESTS.md` (Tests A-E), `TODOS.md`. |
diff --git a/Docs/PERFORMANCE.md b/Docs/PERFORMANCE.md
new file mode 100644
index 000000000..7f31e67b9
--- /dev/null
+++ b/Docs/PERFORMANCE.md
@@ -0,0 +1,281 @@
+# Performance — Ledger, Methodology & Open Opportunities
+
+The single index for performance work in Cosmic Shore. It does three things:
+
+1. **Methodology** — how to find and prove a performance win (the benchmark tool +
+   Unity Profiler, used together). Profile first; never guess.
+2. **Completed-work ledger** — every performance change that has landed, grouped by
+   workstream, with commit evidence and live-code status. This is the "what has
+   already been tried" record so the same ground isn't re-dug.
+3. **Open opportunities** — the remaining wins, ranked, each with how to verify it.
+
+This doc is a **map, not a duplicate**. The deep dives stay authoritative:
+
+| Topic | Authoritative doc |
+|---|---|
+| The benchmark tool (4 tabs, HUDs, dev-build capture) | `Assets/_Scripts/Utility/PerformanceBenchmark/BENCHMARK_TOOL.md` |
+| Benchmark architecture (collector, schema, analysis) | `Assets/_Scripts/Utility/PerformanceBenchmark/BENCHMARK_ARCHITECTURE.md` |
+| Prism system audit (the hot core) | `Assets/_Scripts/Game/Prisms/PRISM_PERFORMANCE_AUDIT.md` |
+| Prism spatial index (AOE / occupancy / neighborhood) | `Docs/SPATIAL_INDEX.md` |
+| Fauna density-targeting redesign | `Docs/DENSITY_PARTITIONING_AUDIT.md` |
+| Threading / main-thread affinity (stability, not throughput) | `Docs/THREADING.md` |
+| Collider budget contract | `Docs/ECOSYSTEM_MASTERPLAN.md` §4 |
+
+> **Status note (kept honest):** the live-code status columns below were verified
+> against the source on the date of writing, because `PRISM_PERFORMANCE_AUDIT.md`'s
+> own "Progress Update (March 2026)" section had gone stale (it lists Rec 1 as
+> unimplemented when `PrismEffectsManager` shipped it; Rec 4 and 5 are also done). When
+> you land a change here, update both this ledger and the relevant deep-dive doc.
+
+---
+
+## 1. Methodology — benchmark + Profiler, used together
+
+The two tools answer different questions. The **benchmark** scores a run, names the
+worst spikes by script self-time, and lets you *prove* a delta (Compare tab). The
+**Unity Profiler** drills into *why* a specific marker is hot (Timeline self-time, GPU
+module, Memory module). Use the benchmark to find the suspect, the Profiler to convict
+it.
+
+### The loop
+
+1. **Pick the heaviest real scenario** for the system under test (see §3 of
+   `BENCHMARK_TOOL.md` for tabs). The bottleneck in a 12-player Crystal Capture is not
+   the bottleneck in Menu_Main idle — baseline the scenario you actually care about.
+2. **Baseline (benchmark).** Runtime Capture tab, *Capture spike breakdowns ON*, enter
+   Play Mode, `● Start Recording` → play the scenario → `■ Stop & Analyze` →
+   `📋 Copy error log` → **Save** → History → **Tag `baseline`**.
+3. **Ground-truth check.** Editor Play-Mode frame time is inflated ~2–3×. Confirm the
+   suspect on a **Development Build standalone** (`-csmbench` self-runner, or F7
+   DiagnosticsHUD → F5 Run Diagnostic on device). Low-overhead mode + Profiler closed
+   gives the truest editor smoothness read.
+4. **Drill (Profiler).** Map the benchmark's verdict to the right Profiler module
+   (table below). Narrow to the **derived class** — base-class profiling hides the real
+   culprit. Use `ProfilerMarker` with `using (marker.Auto())`, never manual
+   `Begin/EndSample`.
+5. **Fix, then prove (benchmark).** Capture again, same source/scenario → **Compare**
+   tab against `baseline`. Same-source deltas only (Compare warns on Editor-vs-DevBuild).
+   A green Compare is the deliverable that proves the optimization.
+
+### Benchmark verdict → Profiler module
+
+| If the benchmark blames… | Open this Profiler module | Look for |
+|---|---|---|
+| GPU-bound / high Draw Calls, SetPass | **GPU** + **Rendering** | Per-object draw calls, SetPass batches, overdraw; Timeline render-thread self-time |
+| `Physics.Processing`, broad phase | **Physics** | Active collider count vs the per-cell collider budget |
+| GC spikes, `GC.Collect`, memory slope | **Memory** ("GC Allocated In Frame") | Per-frame managed allocs; cross-check the benchmark's collector-overhead self-check (~0 B/frame) |
+| `CSM.Net.*`, high RPCs/NetVars/bytes | **CPU Timeline** + benchmark netcode panel | Per-frame RPC count, NetVars-dirty, bytes — needs a real 2-peer / MPPM session |
+| Script self-time in a named method | **CPU Timeline** | Self-time vs total; is it a hot per-object loop that should be batched? |
+
+### Rules (from CLAUDE.md)
+
+- **Profile first.** "Do not guess at performance problems."
+- **`Debug.Log` is a diagnostic, not a fix.** Don't leave logging as the "solution."
+- **Test before and after** — don't assume improvement; the Compare tab is the proof.
+- **`sharedMaterial` + MaterialPropertyBlock**, never `renderer.material`.
+- New spatial queries against prisms go through `PrismSpatialIndex`, never
+  `Physics.OverlapSphere`/`CheckBox` (see `Docs/SPATIAL_INDEX.md`).
+
+---
+
+## 2. Design constraints that bound performance work
+
+Some "obvious" optimizations are **forbidden** here because they violate locked
+platform invariants. Know these before proposing a fix, or you'll re-propose a rejected
```

</details>
