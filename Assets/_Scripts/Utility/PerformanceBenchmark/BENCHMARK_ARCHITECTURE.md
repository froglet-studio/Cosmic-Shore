# Performance Benchmark — Architecture Gist (for diagramming)

This is a **structured gist of how the tool is wired**, written so it can be pasted into a chat to
generate an architecture diagram. It lists the components, what each one does, who calls whom, and
the data flow — with extra depth on the **Runtime Capture** and **Sweep** tabs. Plain text + ASCII,
no Mermaid.

The one rule that explains the whole design:

> **The `PerformanceBenchmarkRunner` is the only thing that measures.** Everything else either
> *starts* it, *reads* its output, or is a *separate on-screen readout*. Every tab is a different
> front-end onto that one runtime collector.

---

## 1. Layers (top → bottom)

```
┌─────────────────────────────── EDITOR (UnityEditor, #if UNITY_EDITOR) ───────────────────────────────┐
│                                                                                                       │
│   PerformanceBenchmarkWindow  ──tabs──►  Runtime Capture │ Sweep │ History │ Compare │ Load Insights  │
│        │                                                                              │               │
│        │ starts / reads                         enriches spikes (off game thread)     │ LoadInsightsTab│
│        ▼                                              ▲                               ▼               │
│   SpikeAnalyzer (ProfilerDriver / HierarchyFrameDataView — editor-only marker self-time)             │
│        │                                                                                              │
│   EditorUIStyles · BenchmarkHistory · BenchmarkComparer · BenchmarkAutoStart                          │
└───────────────────────────────────────────────────────────────────────────────────────────────────────┘
                  │ creates GameObjects in Play Mode                 ▲ reads .Spikes / .LastReport
                  ▼                                                  │
┌─────────────────────────────── RUNTIME (MonoBehaviours, exist in Play Mode) ─────────────────────────┐
│                                                                                                       │
│   PerformanceBenchmarkRunner  ◄── the only FRAME measurer (end-of-frame, zero-alloc)                 │
│        ├─ samples FrameSnapshot/frame  ── ProfilerRecorder (Render/Memory/Physics) + FrameTimingMgr   │
│        ├─ GameLoadSampler   ── prisms / VFX / vessels / players from gameplay singletons              │
│        ├─ NetMarkers        ── CSM.Net.* markers + RPC/NetVar/bytes counters (read back as recorders) │
│        └─ on stop ─► BenchmarkStatistics ─► BenchmarkAnalysis (score+grade+hints) ─► BenchmarkReport  │
│                                                                                                       │
│   LoadInsights (static)  ◄── the LOAD-WINDOW measurer: spans from pipeline call sites                 │
│        ├─ armed via PlayerPrefs; BeginLoad at game launch → CompleteLoad at arena complete            │
│        ├─ exact wall-clock attribution (innermost active span wins; sums to 100%) + hot-path          │
│        │  accumulators (per-item stage totals inside big spans) + hints                               │
│        └─ LoadInsightsRuntime (host, editor+dev builds): frame stalls, error capture, netcode scene   │
│           events (client trigger), in-flight snapshot every 5s, abort/timeout rails                   │
│                                                                                                       │
│   ManualSweepSession   ── Sweep companion: error/exception log + F8 marks (near-zero overhead)        │
│                                                                                                       │
│   DiagnosticsHUD (F7)  ── auto-spawn uGUI overlay, editor + DEV builds, own Run-Diagnostic export     │
│   BenchmarkHUDOverlay (F9) ── editor IMGUI eyeball overlay                                            │
│   ProfilerCsvLogger    ── standalone per-frame CSV dump (menu-driven)                                 │
│   BenchmarkBuildAutoRunner ── headless dev-build self-runner (-csmbench)                              │
└───────────────────────────────────────────────────────────────────────────────────────────────────────┘
                  │ writes
                  ▼
┌─────────────────────────────── STORAGE (plain JSON / text on disk) ──────────────────────────────────┐
│   persistentDataPath/Benchmarks/*.json   (+ benchmark_index.json, _collect_lastrun, _sweep_lastrun)   │
│   persistentDataPath/Benchmarks/LoadInsights/load_*.json + .txt (+ _loadinsights_inflight.json)       │
│   persistentDataPath/PerfRuns/*.json     (dev-build self-capture, origin=DevBuild)                    │
│   persistentDataPath/ProfilerCaptures/*.csv (+ _summary.txt)                                          │
│   Documents/CosmicShore Diagnostics/diag_*.json (+ .txt)  (DiagnosticsHUD)                            │
└───────────────────────────────────────────────────────────────────────────────────────────────────────┘
```

> The "one measurer" rule now has a deliberate second instance: `PerformanceBenchmarkRunner` owns
> **steady-state frame cost**, `LoadInsights` owns the **load window** (launch → playable). They
> never overlap in responsibility: the runner samples frames forever-shaped, the load recorder
> attributes a bounded window to causes. Both follow the same pattern — gameplay code writes
> markers/spans, the tool reads and persists reports, the window is a front-end.

---

## 2. The measurer — `PerformanceBenchmarkRunner` (runtime)

A MonoBehaviour created in Play Mode. Samples **once per frame at end-of-frame**, into a
pre-allocated ring of `FrameSnapshot`s (so steady-state allocation ≈ 0 — verified by the
"collector overhead" self-check).

- **Inputs per frame:** `Time.unscaledDeltaTime`; `ProfilerRecorder`s for Render (Draw Calls,
  SetPass, Batches, Triangles, Vertices), Memory (GC Allocated In Frame, reserved/used), Physics
  (active rigidbodies); `FrameTimingManager` for CPU/GPU split; `GameLoadSampler` for gameplay
  counts; `NetMarkers` counters for netcode.
- **Two run shapes** (`StartBenchmark(freeForm)`):
  - **free-form (Runtime Capture, Manual Sweep):** *skip warmup, sample immediately, record until
    Stop* (or until the frame buffer fills, `MaxFreeFormSeconds`).
  - **fixed (automatic sweep, dev-build):** *warmup `Config.WarmupDuration` → sample
    `Config.SampleDuration` → auto-finish*.
- **Spikes (free-form):** a frame over threshold is recorded as a lightweight `SpikeEntry`
  (`frameIndex`, `frameTimeMs`, CPU/GPU, and crucially a `profilerFrameIndex`). The **expensive
  script breakdown is NOT done here** — only the cheap frame index is stored, so capturing a spike
  costs ~microseconds (this is the fix for the old "capture storm").
- **On stop → `FinishRun`:** aggregates into `BenchmarkStatistics`, runs `BenchmarkAnalysis`
  (score + grade + hints), assembles a `BenchmarkReport`, exposes `.LastReport` / `.LastReportPath`.
- **Public surface the window reads:** `IsRunning`, `IsFreeForm`, `FramesCaptured`, `Spikes`,
  `LastReport`, `LastReportPath`, `AutoSave`, `Configure(config, data, gameData, hintRules)`,
  `StartBenchmark(bool)`, `StopBenchmark()`.

---

## 3. Tab front-ends (editor)

### 3a. Runtime Capture  (internal enum `Tab.Collect`, displayed "Runtime Capture")

The free-play recorder. Flow and ownership:

```
[Setup foldout]  Config / HintRules / GameData / "Capture spike breakdowns" toggle / Spawn F9 overlay
        │
   ● Start Recording ──► StartFreeFormInCurrentPlay()
        │                   ├─ find or create PerformanceBenchmarkRunner GameObject
        │                   ├─ if breakdowns on: SpikeAnalyzer.SetProfilerEnabled(true)
        │                   ├─ ClearRecent()  (discard previous unsaved run + cache)
        │                   ├─ Configure(...) ; AutoSave=false
        │                   └─ StartBenchmark(freeForm:true)
        │
   while running ─► DrawRecordingStatus()      (live: frames · spikes · Live Spikes list + filters)
        │            window.Update() ─► EnrichPendingSpikes()  ◄── KEY: spike breakdown happens HERE,
        │                                  rate-limited (0.35s), worst-first, off the game frame,
        │                                  via SpikeAnalyzer.TryGetTopMarkers(profilerFrameIndex)
        │
   ■ Stop & Analyze ─► runner.StopBenchmark() ─► runner.LastReport
        │
   adopt report ─► cache to Benchmarks/_collect_lastrun.json  (survives leaving Play Mode)
        │
   [📋 Copy error log]  BuildClaudeReportText(report) → system clipboard  (stats+spikes+hints, Claude-ready)
   [Save]               report.SaveToFile + BenchmarkHistory.AddToHistory
   [Clear Recent]       discard report + delete cache
```

- **Live Spikes filters:** `spikeScriptsOnly` · `spikeShowCount` (5/10/20/All) · `spikeSearch` text.
- **Low-overhead mode** = "Capture spike breakdowns" OFF → no Profiler enable, no hierarchy walks →
  records frame time / fps / stability only (true smoothness read).
- **State-driven UI:** while recording it shows only the live status + spikes; idle shows one
  state-appropriate primary button (Enter Play / Start Recording) + the Copy/Save/Clear row.

### 3b. Sweep  (`Tab.Sweep`) — two modes

**Manual Session (primary).** Two runtime objects run together:

```
● Start Session ──► StartManualSweep()
        ├─ PerformanceBenchmarkRunner (free-form, low overhead — no profiler walks)   → frame stats
        └─ ManualSweepSession.StartSession()                                          → errors + marks
                ├─ Application.logMessageReceived → captures Error/Exception/Assert (timestamp+msg)
                └─ F8 / Mark button → SweepMark(timestamp, fps, label)
        │
   live: "● Session — N frames · E errors · M marks"  + live Error list + Mark list
        │
   ■ Stop & Save ──► StopManualSweep()
        ├─ runner.StopBenchmark() → LastReport (stats)
        ├─ session.FillReport(report) → copies errors[] + marks[] INTO the same report
        └─ cache to Benchmarks/_sweep_lastrun.json
        │
   results: stat foldouts (DrawResults) + Errors foldout + Marks foldout
   [📋 Copy error log]  BuildSweepLogText(report)   [Save]   [Clear Recent]
```

> The Manual Session is the only place two runtime collectors compose: the **Runner** owns frame
> stats, the **ManualSweepSession** owns the error log + marks, and `FillReport` merges them into one
> `BenchmarkReport` before save.

**Automatic (multi-scene) — experimental, foldout.** `BenchmarkSweepRunner.StartSweep(scenes, ...)`
iterates selected Build-Settings scenes:
- **Full sweep:** load → fixed benchmark → next; results to History (A–F grade per scene).
- **Errors only (fast scan):** load briefly → catch errors → next; per-scene OK/ERR badge,
  expandable messages. Networked scenes load uninitialized (no host/players).

### 3c. History (`Tab.History`)

Reads `BenchmarkHistory` (disk index). Per entry: score badge, FPS/frame stats, origin badge
(Editor/DevBuild/Legacy), git branch/commit, tag. Actions: set Baseline/Current → Compare, Tag,
reveal JSON, Delete, Rebuild Index, **Import External Run** (dev-build JSON from a device).

### 3d. Compare (`Tab.Compare`)

`BenchmarkComparer.Compare(baseline, current)` → per-metric diff (FPS, frame time, render, memory,
netcode) with better/same/worse verdicts + counts banner + non-scored game-load context. **Cross-
source guard**: warns when origin/platform differ (only same-source deltas are valid). Copy as text.

---

## 4. On-screen overlays (independent of the window)

### DiagnosticsHUD (F7) — the on-device tester overlay
- `[RuntimeInitializeOnLoadMethod]` **auto-spawns** in Editor + Development builds (`#if
  UNITY_EDITOR || DEVELOPMENT_BUILD`), DontDestroyOnLoad. Pure **uGUI** (own Canvas + EventSystem).
- **Keys:** F7 toggle · F6 Advanced/Simple · F5 Run Diagnostic. On-screen buttons mirror these +
  `–/+` duration.
- **Two side-by-side blocks**, color-coded by health:
  - Left = local frame cost: **FPS, Frame Time, CPU (busy), GPU, Bound** (live verdict via
    `FrameBoundness`), **CPU / GPU** thread breakdown (advanced: Total/Main/Wait/Render Thread),
    **Render** (Draw/Batches/SetPass/Tris/Verts), **Memory** (GC KB/frame, Managed, Unity Alloc,
    Reserved % of device RAM, Gfx Driver, Device RAM/VRAM).
  - Right = connection: **Network** (Ping=UTP RTT, NetVars, RPCs, Bytes/f), **Region** (OS region +
    UTC offset).
- **Run Diagnostic (F5):** records N seconds, flags spikes (`> max(33.3ms, 1.75×mean)`), writes
  `Documents/CosmicShore Diagnostics/diag_<scene>_<ts>.json` + `.txt` — incl. avg CPU/busy/GPU ms,
  bound verdict, and alloc/reserved memory. Works in editor and build.
- Reads the **same** `ProfilerRecorder`s + `NetMarkers` counters as the Runner, and the same
  `FrameBoundness` classification as `BenchmarkAnalysis.boundVerdict` → numbers and verdicts match.

### BenchmarkHUDOverlay (F9) — editor IMGUI eyeball
- Spawned from Runtime Capture's *Spawn Live HUD Overlay* (or drop the component). `OnGUI` text:
  FPS / frame (avg+max) / CPU busy+total / GPU / bound verdict / Draw / SetPass / Tris / GC /
  memory (alloc, reserved, gfx driver, device caps), plus optional game-load counts via GameDataSO.

---

## 5. Shared data model

```
FrameSnapshot   (one per frame: ms, cpu, gpu, draws, setpass, batches, tris, verts, gcAlloc,
                 mem, rigidbodies, + game-load counts, + netcode counters)
        │ aggregated by
BenchmarkStatistics  (avg/p95/p99/max frame ms, avg/p1 fps, stddev, avg draws/tris,
                      total GC, CPU/GPU avgs, netcode share, collector overhead)
        │ scored by
BenchmarkAnalysis    (0–100 score, A–F grade via BenchmarkGrade, boundVerdict CPU/GPU,
                      hints[] from BenchmarkHintRulesSO rules)
        │ assembled into
BenchmarkReport      (sceneName, timestamp, source{origin,platform,git}, statistics, spikes[]
                      (+ topMarkers[] {name, ms, isScript}), analysis, errors[], marks[])
        │ persisted as
JSON on disk  ── indexed by BenchmarkHistory ── diffed by BenchmarkComparer
```

`SpikeEntry` carries `profilerFrameIndex` so the **editor** can fetch `topMarkers` lazily via
`SpikeAnalyzer` after the fact. `SweepError{timeSeconds,type,message}` and `SweepMark{timeSeconds,
fps,label}` come from `ManualSweepSession`.

---

## 6. Who-calls-what (one-liners for arrows in a diagram)

- `PerformanceBenchmarkWindow` → **creates/finds** `PerformanceBenchmarkRunner` (and, for Manual
  Sweep, `ManualSweepSession`) in Play Mode.
- `PerformanceBenchmarkWindow.Update()` → `EnrichPendingSpikes()` → `SpikeAnalyzer.TryGetTopMarkers`
  (off the game thread).
- `PerformanceBenchmarkRunner` → `ProfilerRecorder` / `FrameTimingManager` / `GameLoadSampler` /
  `NetMarkers` (read) → `BenchmarkStatistics` → `BenchmarkAnalysis` → `BenchmarkReport`.
- Gameplay/netcode code → `NetMarkers.*` (write markers + counters) ← read back by both the Runner
  and `DiagnosticsHUD`.
- `ManualSweepSession` → `Application.logMessageReceived` (errors) + Input System F8 (marks) →
  `FillReport(report)`.
- `BenchmarkReport.SaveToFile` → disk → `BenchmarkHistory` (index) → `BenchmarkComparer` (diff).
- `DiagnosticsHUD` → own `ProfilerRecorder`s + `NetMarkers` + UTP RTT → `Documents/…/diag_*.json`.
- `BenchmarkBuildAutoRunner` (`-csmbench`) → `PerformanceBenchmarkRunner` (fixed run) →
  `PerfRuns/*.json` → History *Import External Run*.
- Pipeline code (`GameDataSO.InvokeGameLaunch/InvokeClientReady`, `SceneLoader`, vessel/AI
  initializers, `Cell`, life/segment/crystal spawners, `SpawnableBase`, `PrismTrailBuilder.LayOne`
  stage accumulators, pools) →
  `LoadInsights.BeginLoad / Measure / Mark / Count / AccumulateSample / CompleteLoad` (write) ←
  read by `LoadInsightsTab` (live status + report) and persisted as `LoadInsights/load_*.json + .txt`.
- `LoadInsightsRuntime` → Unity + Netcode scene events (client-side BeginLoad trigger, marks),
  `Application.logMessageReceived` (errors), per-frame stall feed, in-flight snapshot/recovery.

---

## 7. Suggested diagram framing

- **Three swim-lanes:** Editor · Runtime · Storage (as in §1).
- **Center of gravity:** `PerformanceBenchmarkRunner`. Draw every tab as an arrow *into* it (start)
  and an arrow *out* of it (read report).
- **Call out the two "off-thread / low-overhead" design decisions** as annotations: (a) spikes store
  only a frame index, breakdown is enriched later in the editor; (b) Manual Sweep + DiagnosticsHUD
  run with the Profiler off for near-zero perturbation.
- **Show the two overlays as independent runtime nodes** (not children of the window) — DiagnosticsHUD
  is the one that ships in dev builds and writes to Documents.
