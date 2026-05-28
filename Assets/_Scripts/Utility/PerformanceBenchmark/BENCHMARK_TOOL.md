# Performance Benchmark Tool

**Open it:** `FrogletTools > Performance Benchmark` (Unity menu bar).

It measures the **running game** frame-by-frame, scores it, points out what's slow and how to
fix it, saves runs you can compare, and can sweep multiple scenes for errors. Performance only
exists at runtime, so it runs in Play Mode — but you don't enter Play yourself; the tool does.

---

## The four tabs

### Collect — capture one run
1. Pick a **Config** (click **Create Default Config** if the slot is empty).
2. Choose a **Scene to capture** and optionally tick **Boot from Bootstrap first** (on = the game
   boots through Bootstrap so networked scenes initialize; off = the scene is loaded directly —
   best for self-contained scenes).
3. Click **▶ Start Capture** → it enters Play, warms up, samples, and shows live progress + frames.
4. On finish you get a **results panel**: score bar, grade, CPU/GPU split, draw calls / memory /
   game-load, **hints**, and **top spikes**.
5. Click **Save to History**, then **Tag** it (e.g. `baseline`).
   ⚠️ Save before leaving Play Mode — unsaved runs are lost when Play exits (domain reload).

### Sweep — many scenes / error scan
Select scenes (from Build Settings) and run. Two modes:
- **Full sweep** — benchmarks each scene; results go into History tagged together.
- **Errors only (fast scan)** — skips benchmarking, loads each scene briefly and **catches
  errors/exceptions/asserts**. Each scene gets a green/red badge; click the ⚠ count to expand the
  messages.

### History — saved runs
Every saved run with a **score badge**, FPS/frame stats, and git branch/commit. Tag, delete, open
the raw JSON, or send a run to Compare. Use **Rebuild Index** if JSON files were added/removed
manually.

### Compare — before / after
Pick a **baseline** and a **current** run → a 17-metric diff with better / same / worse verdicts,
plus a non-scored "game load" context block so you can confirm both runs faced a similar workload.
This is how you *prove* an optimization worked: capture → tag `baseline` → make the change →
capture → Compare.

---

## What you get per run

| Output | Meaning |
|---|---|
| **Score (0–100)** | Blends avg FPS, P99 frame time, stability, and GC into one number. |
| **Grade A–F** | Quick health read (or **"No data captured"** if the run was empty/interrupted). |
| **CPU vs GPU** | Which is the bottleneck, via `FrameTimingManager`. |
| **Top spikes** | The worst frames, each blamed on the **profiler markers** that cost the most (e.g. `GC.Collect`, `Physics.Processing`, `Canvas.SendWillRenderCanvases`). |
| **Hints** | Actionable findings with concrete **fix advice** and a severity. `Blocker` flags the run as failing. |

### Config settings (`BenchmarkConfigSO`)

| Setting | Default | Purpose |
|---|---|---|
| Warmup Duration | 3s | Settle time before recording (skips first-frame hitches). |
| Sample Duration | 10s | How long to record after warmup. |
| Capture Rendering | on | Draw calls, batches, SetPass, triangles, vertices. |
| Capture Memory | on | Allocated/reserved memory + GC per frame. |
| Capture Physics | on | Active rigidbodies. |
| Capture Game Load | on | Active prisms, explosion/implosion VFX, vessels, players. |
| Output Folder | `Benchmarks` | Subfolder under `Application.persistentDataPath` for JSON + index. |
| Benchmark Label | — | Name stamped on the run. |

---

## Customizing the hints

Hints are produced by a rule set. Create one via
`Create > ScriptableObjects > Tools > Benchmark Hint Rules`, assign it in the Collect tab's
**Hint Rules** slot, and tune each rule's **threshold, advice text, severity**, or add a
**profiler-marker rule** (fires when a spike's top marker name matches a pattern). Leave the slot
empty to use the built-in defaults, which are grounded in the project's anti-patterns (pooling,
GPU instancing, `sharedMaterial` + `MaterialPropertyBlock`, SOAP over `Find*`).

Rule types: `GcPerFrameKb`, `MemorySlopeKbPerFrame` (leak), `AvgDrawCalls`, `GpuBound`, `CpuBound`,
`FrameInstability`, `SpikeMarkerName`. Severities: `Info`, `Warning`, `Blocker`.

---

## Live HUD overlay

In Collect, **Spawn Live HUD Overlay** drops an on-screen readout (FPS / frame time / draw calls /
GC / game load) you toggle in the Game view with **F9** — independent of a benchmark run. You can
also drop the `BenchmarkHUDOverlay` component on any GameObject yourself.

---

## Output & storage

Each saved run is a JSON file in `{Application.persistentDataPath}/{OutputFolder}/`, plus a
lightweight `benchmark_index.json` for fast listing. Reports include per-frame snapshots,
aggregated statistics, spikes (with markers), and the analysis (score + hints).

---

## Key files

| Role | File |
|---|---|
| Per-frame capture (runtime) | `PerformanceBenchmarkRunner.cs` |
| Score + rule-based hint engine | `BenchmarkAnalysis.cs` |
| Customizable hint rules (SO) | `BenchmarkHintRulesSO.cs` |
| Spike marker attribution (editor-only) | `SpikeAnalyzer.cs` |
| Multi-scene + error sweep | `BenchmarkSweepRunner.cs` |
| Live on-screen overlay | `BenchmarkHUDOverlay.cs` |
| Data model | `FrameSnapshot.cs`, `BenchmarkStatistics.cs`, `BenchmarkReport.cs` |
| A–F grade | `BenchmarkGrade.cs` |
| Disk history + index | `BenchmarkHistory.cs` |
| A/B comparison | `BenchmarkComparison.cs` |
| Config | `BenchmarkConfigSO.cs` |
| Editor window | `Editor/PerformanceBenchmarkWindow.cs` |
| Auto-enter-Play hook | `Editor/BenchmarkAutoStart.cs` |
| Pastel UI helpers | `Editor/EditorUIStyles.cs` |
| Tests | `Tests/Editor/*` (`CosmicShore.PerformanceBenchmark.Tests`) |

All under `Assets/_Scripts/Utility/PerformanceBenchmark/`. SOAP plumbing
(`BenchmarkDataSO` + events) lives in `Assets/_Scripts/Utility/DataContainers/` and
`Assets/_Scripts/Utility/SOAP/ScriptableBenchmarkData/` — optional, for external listeners.

---

## Limitations

- **CPU/GPU split** needs "Frame Timing Stats" enabled in Player Settings — **Start** enables it for
  you; the first run after enabling may report GPU time as 0 on some platforms.
- **Spike markers** need the Unity Profiler enabled — **Start** enables it; if it was off, spikes are
  still recorded but without marker attribution.
- **Directly-loaded networked scenes** are uninitialized (no host/players) unless you use
  **Boot from Bootstrap**; the same applies to the Sweep.
- A dedicated **benchmark scene that recommends player graphics settings** is future work — the tool
  currently measures whatever scene you point it at.
