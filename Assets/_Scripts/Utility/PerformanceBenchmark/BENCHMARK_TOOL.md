# Performance Benchmark Tool

**Open the window:** `FrogletTools > Performance Benchmark` (Unity menu bar).

The toolset measures the **running game** frame-by-frame, scores it, names what's slow and how to
fix it, saves runs you can compare, sweeps scenes for errors, and gives you an on-screen readout
that works in a real device build. Performance only exists at runtime, so everything runs in Play
Mode.

It has three faces:

| Piece | Where it lives | Use it for |
|---|---|---|
| **Benchmark Window** (4 tabs) | Editor only (`FrogletTools` menu) | Recording, scoring, sweeping, comparing — the analysis cockpit. |
| **DiagnosticsHUD** (F7) | Editor **and** dev builds (auto-spawns) | On-screen live readout (FPS, CPU/GPU split + bound verdict, memory) + "Run Diagnostic" spike capture to Documents. The overlay testers use on a real device. |
| **BenchmarkHUDOverlay** (F9) | Editor (spawned from the window) | A quick IMGUI eyeball of FPS/CPU-GPU/draws/GC/memory while you play. |

There's also a standalone **ProfilerCsvLogger** (per-frame CSV dump) and a **dev-build self-runner**
(`-csmbench`) covered further down.

---

## The four tabs

```
┌──────────── Performance Benchmark Window ────────────┐
│ Runtime Capture │ Sweep │ History (n) │ Compare       │
└──────────────────────────────────────────────────────┘
```

### 1 · Runtime Capture — free-play recording with live spike breakdown ⭐

The day-to-day tab. You **play freely** and Start/Stop a recording; every frame that spikes is
broken down into the **script methods** that caused it, with editor noise filtered out.

1. Pick a **Config** in the collapsible **Setup** (auto-loads the repo default; click **Create
   Default Config** if the slot is empty). Optional: a **Hint Rules** SO and a **Game Data** SO
   (adds prism/VFX/vessel counts to the report).
2. **Enter Play Mode** and get to the moment you want to profile.
3. Press **● Start Recording**. The tab collapses to a live status: `● Recording — N frames · M
   spikes`, with a **Live Spikes** list that updates as you play.
4. Press **■ Stop & Analyze**. Results appear immediately (they don't need a fixed window — it
   records until you stop, or until the frame buffer fills).
5. **📋 Copy error log** puts a clean, Claude-ready text block on the clipboard (stats + top spikes
   with script self-times + hints) — no screenshots. **Save** commits it to History; **Clear
   Recent** discards it.

**Live Spikes filters** (toolbar above the list): **Scripts only** (hide engine/editor markers),
**Show** 5/10/20/All, and a **search box** to filter marker names. Each spike row is tinted by
severity (red ≥100 ms, peach ≥50 ms) and shows `⚡ Frame N — X ms  CPU/GPU`, then its top markers
(`▸` = script, `·` = engine) with self-time.

**Capture spike breakdowns** toggle (Setup): when **on**, the Profiler is enabled and each spike
gets its script breakdown — what you need to find a culprit. When **off** (low-overhead mode), it
records **frame time / fps / stability only** with near-zero perturbation — use this for a *true*
smoothness read, and also close the Profiler window for the cleanest number. The honest ground
truth is still a **Development Build run standalone** (the editor inflates frame time ~2–3×).

> The recorded report **persists across leaving Play Mode** — it's cached to disk
> (`Benchmarks/_collect_lastrun.json`) and reloaded, so you can still Copy/Save after Play exits.
> Spike breakdowns are enriched **off the game thread** in the editor window (rate-limited,
> worst-spike-first), so the act of capturing never becomes the spike it's measuring.

### 2 · Sweep — a data set, two modes

**Manual Session (primary).** Start a session, then **play the game yourself**. It records in the
background with **minimal FPS disturbance** (low-overhead — no profiler walks): frame stats, a
**timestamped error / exception / assert log**, and the **moments you mark** with **F8** (or the
*Mark* button, optionally labelled). Live status shows `● Session — N frames · E errors · M marks`.
Press **■ Stop & Save**; results reuse the stat foldouts plus an **Errors** list and a **Marks**
list. **📋 Copy error log** exports the session as text. Great for "I played the boss fight and it
felt bad at the 40s mark — here's the error log + my marks".

**Automatic (multi-scene) — experimental, in a foldout.** Select scenes from Build Settings and run:
- **Full sweep** — benchmarks each scene in turn; results go to History tagged together, each with
  an A–F grade badge and `fps (p99 ms)`.
- **Errors only (fast scan)** — skips benchmarking, loads each scene briefly and catches
  errors/exceptions/asserts. Each scene gets a green **OK** / red **ERR** badge; click the **⚠ count**
  to expand the messages. Networked game scenes that need the Bootstrap→host pipeline sweep in an
  *uninitialized* state (no host/players) — that's expected.

Sweep results also **persist** across leaving Play Mode.

### 3 · History — saved runs

Every saved run, newest first, with a **score badge**, FPS/frame stats, an **origin badge**
(Editor / DevBuild / Legacy), git branch/commit, and any **tag**. Per-entry: set as **Baseline** or
**Current** for Compare, **Tag** it (e.g. `baseline`, `after-trail-fix`), reveal the raw **JSON**,
or **Delete**. **Rebuild Index** re-scans the folder; **Import External Run** pulls in a report JSON
captured on a device (see Dev-build capture).

### 4 · Compare — before / after

Pick a **baseline** and a **current** run → a metric diff (FPS, frame time, rendering, memory,
**netcode**) with better / same / worse verdicts and a counts banner (`X better · Y same · Z
worse`), plus a non-scored "game load" context block so you can confirm both runs faced a similar
workload. This is how you *prove* an optimization worked: capture → tag `baseline` → make the change
→ capture → Compare. If the two runs come from different sources (Editor vs DevBuild) or platforms,
a **cross-source warning** flags that only same-source deltas are meaningful.

---

## What you get per run

| Output | Meaning |
|---|---|
| **Score (0–100)** | Blends avg FPS, P99 frame time, stability, and GC into one number. |
| **Grade A–F** | Quick health read (or **"No data captured"** if the run was empty/interrupted). |
| **CPU vs GPU** | Which is the bottleneck, via `FrameTimingManager`. |
| **Top spikes** | The worst frames, each blamed on the **profiler markers** that cost the most (e.g. `GC.Collect`, `Physics.Processing`, `Canvas.SendWillRenderCanvases`, `CSM.Net.*`), editor noise filtered, scripts flagged. |
| **Netcode** | NGO cost the stock Profiler hides: netcode share of frame, RPCs/frame, NetVars-dirty/frame, bytes/frame, network tick rate. |
| **Hints** | Actionable findings with concrete **fix advice** and a severity. `Blocker` flags the run as failing. |
| **Game load** | Active prisms, explosion/implosion VFX, vessels, players — so frame cost ties to on-screen workload. |
| **Collector overhead** | The collector's own allocation per frame — a self-check; should read ~0 B/frame. |

### Config settings (`BenchmarkConfigSO`)

| Setting | Default | Purpose |
|---|---|---|
| Warmup Duration | 3s | Settle time before recording (fixed/auto runs only — Runtime Capture skips warmup). |
| Sample Duration | 10s | Fixed-window length for auto/dev-build runs (Runtime Capture records until you Stop). |
| Capture Rendering | on | Draw calls, batches, SetPass, triangles, vertices. |
| Capture Memory | on | Allocated/reserved memory + GC per frame. |
| Capture Physics | on | Active rigidbodies. |
| Capture Game Load | on | Active prisms, explosion/implosion VFX, vessels, players. |
| Capture Netcode | on | `CSM.Net.*` markers + RPC/NetVar/bytes counters. |
| Output Folder | `Benchmarks` | Subfolder under `Application.persistentDataPath` for JSON + index. |
| Benchmark Label | — | Name stamped on the run. |

Netcode cost is instrumented in the gameplay/netcode code via `NetMarkers` — markers
`CSM.Net.Tick / Serialize / Deserialize / SpawnDespawn / RpcDispatch` plus per-frame counters
`CSM RPCs Sent`, `CSM NetVars Dirty`, `CSM Bytes Sent`, seeded at the central NGO hot paths and
extensible.

---

## DiagnosticsHUD — the in-build on-screen overlay (F7)

A uGUI overlay that **auto-spawns in the Editor and Development builds** (stripped from Release via
`#if UNITY_EDITOR || DEVELOPMENT_BUILD`). This is the readout testers use on a real device — it
needs no editor window and writes its own reports.

**Controls:** **F7** toggles the whole overlay · **F6** Advanced/Simple · **F5** Run Diagnostic.
On-screen buttons mirror these (**Advanced/Simple**, **Run Ns**, **– / +** to set the duration).

**Layout** (top-left, two side-by-side blocks, color-coded by health — green/amber/red):

- **Simple:** `FPS`, `Frame Time (ms)`, `CPU (busy)`, `GPU`, and `Bound` — the live CPU-vs-GPU
  dependence readout (see "Reading CPU vs GPU bound" below).
- **Advanced:** left block = local frame cost — **CPU / GPU** (CPU Total, Main Thread,
  Wait (present), Render Thread), **Render** (Draw Calls, Batches, SetPass, Triangles, Vertices)
  and **Memory** (GC KB/frame, Managed heap, Unity Alloc, Reserved with % of device RAM,
  Gfx Driver, Device RAM/VRAM caps); right block = connection — **Network** (Ping, NetVars,
  RPCs, Bytes/f) and **Region** (Location + UTC offset). The panel auto-resizes to fit.

> **Ping** is round-trip time to the host (UnityTransport RTT). **Region** is the local client's OS
> region + UTC offset — UGS auto-picks the Relay region and doesn't surface it, so for
> cross-continent testing you read the local region and let Ping tell you the latency to host.

### Reading CPU vs GPU bound

The split comes from `FrameTimingManager` (always active in the Editor and Development builds —
exactly where these overlays exist; release builds would additionally need "Frame Timing Stats"
in Player Settings). Shared classification lives in `FrameBoundness` so the live overlays and the
benchmark's `boundVerdict` agree:

- **CPU (busy)** = `max(main thread − wait-for-present, render thread)` — actual CPU work. The
  raw `cpuFrameTime` *includes* the idle wait for present, so under vsync or
  `Application.targetFrameRate` it always fills the frame budget and would misread as
  "CPU-bound"; the busy number doesn't.
- **GPU** = `gpuFrameTime`. Shows `n/a` on graphics APIs without GPU timestamps (e.g. some GLES
  devices) — there the Bound verdict falls back to what's measurable.
- **Bound** = `GPU-bound` / `CPU-bound` (one side >10% over the other) / `Balanced`, or
  **`Capped @N`** when fps sits at the vsync/targetFrameRate cap — then the cap is the limiter
  and neither processor verdict applies (that's the healthy state: both have headroom).
- **Memory** answers the "is it RAM?" question: **Reserved** (Unity's total footprint) is shown
  against the device's physical RAM — that percentage is what predicts OS kills on mobile;
  **Gfx Driver** approximates GPU-side memory (textures/meshes/RTs) against the VRAM cap.

**Run Diagnostic (F5 / button):** records frames for the selected seconds (default 10s, `±` adjusts),
flags spikes (frames over `max(33.3 ms, 1.75 × running mean)`), and writes a report to
`Documents/CosmicShore Diagnostics/diag_<scene>_<timestamp>.json` (+ a readable `.txt`). Works in the
editor and in a build. Each spike records time, ms, fps, draws, tris, GC KB, CPU/GPU ms, and ping;
the report header adds avg CPU (total + busy), avg GPU, the bound verdict, and allocated/reserved
memory vs device RAM.

The HUD reads the same `ProfilerRecorder`s and `NetMarkers` counters as the window, so its numbers
line up with a Runtime Capture.

---

## Overlays at a glance

| Overlay | Key | Where | Tech | Extras |
|---|---|---|---|---|
| **DiagnosticsHUD** | F7 (F6 advanced · F5 diagnostic) | Editor + Dev builds (auto) | uGUI | CPU/GPU split + Bound verdict, full memory block, Ping, Region, **Run Diagnostic → Documents** |
| **BenchmarkHUDOverlay** | F9 | Editor (spawn from Runtime Capture's *Spawn Live HUD Overlay*, or drop the component) | IMGUI | FPS/frame, CPU/GPU + Bound verdict, Draw/SetPass/Tris/GC, memory, optional game-load counts |

---

## Customizing the hints

Hints are produced by a rule set. Create one via
`Create > ScriptableObjects > Tools > Benchmark Hint Rules`, assign it in the Runtime Capture tab's
**Hint Rules** slot, and tune each rule's **threshold, advice text, severity**, or add a
**profiler-marker rule** (fires when a spike's top marker name matches a pattern). Leave the slot
empty to use the built-in defaults, grounded in the project's anti-patterns (pooling, GPU
instancing, `sharedMaterial` + `MaterialPropertyBlock`, SOAP over `Find*`).

Rule types: `GcPerFrameKb`, `MemorySlopeKbPerFrame` (leak), `AvgDrawCalls`, `GpuBound`, `CpuBound`,
`FrameInstability`, `SpikeMarkerName`, `NetcodeSharePercent`, `RpcsPerFrame`. Severities: `Info`,
`Warning`, `Blocker`.

---

## Dev-build capture (verify editor numbers against a real build)

Editor Play-Mode numbers are inflated (~2–3×) and subsystem costs shift versus a real build. A
development build can self-run the same capture:

1. Make a **Development Build** (enable "Frame Timing Stats" in Player Settings for the CPU/GPU split).
2. Launch it with the `-csmbench` command-line argument.
3. It runs warmup → sample → analyze unattended and writes a report JSON (origin `DevBuild`) to
   `persistentDataPath/PerfRuns/`.
4. Pull that JSON off the device and use History → **Import External Run**.
5. Compare against an editor run — the cross-source warning reminds you only deltas (not absolute
   numbers) compare across sources.

Strictly gated: the auto-runner requires both a dev-build/editor compile and the `-csmbench` arg, so
it never runs in a normal session (`BenchmarkBuildAutoRunner`). This is separate from the
DiagnosticsHUD's manual **Run Diagnostic**, which any tester can trigger in any dev build.

---

## Output & storage

| Source | Path |
|---|---|
| Runtime Capture / Sweep runs | `{persistentDataPath}/Benchmarks/*.json` (+ `benchmark_index.json`, `_collect_lastrun.json`, `_sweep_lastrun.json`) |
| DiagnosticsHUD diagnostics | `Documents/CosmicShore Diagnostics/diag_*.json` (+ `.txt`) |
| Dev-build self-capture | `{persistentDataPath}/PerfRuns/*.json` |
| ProfilerCsvLogger | `{persistentDataPath}/ProfilerCaptures/*.csv` (+ `_summary.txt`) |

On Mac (editor), `persistentDataPath` is `~/Library/Application Support/<company>/<product>/…`.
Reports include per-frame snapshots, aggregated statistics, spikes (with markers), and the analysis
(score + hints) — all plain text/JSON.

---

## Key files

| Role | File |
|---|---|
| Editor window (all 4 tabs) | `Editor/PerformanceBenchmarkWindow.cs` |
| Per-frame capture (runtime, end-of-frame, zero-alloc) | `PerformanceBenchmarkRunner.cs` |
| Manual-session error log + F8 marks (runtime) | `ManualSweepSession.cs` |
| In-build overlay + Run Diagnostic (F7) | `DiagnosticsHUD.cs` |
| Editor live overlay (F9) | `BenchmarkHUDOverlay.cs` |
| Spike marker attribution (editor-only) | `SpikeAnalyzer.cs` |
| Score + rule-based hint engine | `BenchmarkAnalysis.cs` |
| Shared CPU/GPU bound classification (busy CPU, fps-cap detection) | `FrameBoundness.cs` |
| Customizable hint rules (SO) | `BenchmarkHintRulesSO.cs` |
| Netcode (NGO) markers + counters | `NetMarkers.cs` |
| Game-load counters (prisms/VFX/vessels) | `GameLoadSampler.cs` |
| Multi-scene + error sweep | `BenchmarkSweepRunner.cs` |
| Dev-build self-runner (`-csmbench`) | `BenchmarkBuildAutoRunner.cs` |
| Standalone per-frame CSV logger | `ProfilerCsvLogger.cs` |
| Data model | `FrameSnapshot.cs`, `BenchmarkStatistics.cs`, `BenchmarkReport.cs` (schema + source) |
| A–F grade | `BenchmarkGrade.cs` |
| Disk history + index | `BenchmarkHistory.cs` |
| A/B comparison | `BenchmarkComparison.cs` |
| Config | `BenchmarkConfigSO.cs` (default asset: `_SO_Assets/Benchmark/BenchmarkConfig.asset`) |
| Auto-enter-Play hook | `Editor/BenchmarkAutoStart.cs` |
| Pastel UI helpers | `Editor/EditorUIStyles.cs` |
| Tests | `Tests/Editor/*` (`CosmicShore.PerformanceBenchmark.Tests`) |

Most live under `Assets/_Scripts/Utility/PerformanceBenchmark/`. `NetMarkers` placement lives in the
gameplay/netcode code (e.g. `VesselController`, `R_VesselActionHandler`, `Player`, the vessel
initializers, `INetworkSerializable` structs).

---

## Limitations

- **CPU/GPU split** needs "Frame Timing Stats" in Player Settings — the window enables it for you;
  the first run after enabling may report GPU time as 0 on some platforms.
- **Spike markers** need the Unity Profiler enabled (the window enables it when *Capture spike
  breakdowns* is on); with it off, frames are still recorded but without marker attribution, and
  marker-level attribution is **editor-only** (dev-build/HUD spikes have frame time but no script
  breakdown).
- **Editor frame time is inflated ~2–3×** — use low-overhead mode (close the Profiler) for a true
  smoothness read, and a **Development Build** for ground truth.
- **Directly-loaded networked scenes** are uninitialized (no host/players) in the automatic sweep —
  expected.
- **Netcode metrics** are only as complete as the `NetMarkers` placement.
- **Cross-source runs** (Editor vs DevBuild, or different platforms) aren't comparable on absolute
  numbers — only same-source before/after deltas are meaningful (Compare warns).
