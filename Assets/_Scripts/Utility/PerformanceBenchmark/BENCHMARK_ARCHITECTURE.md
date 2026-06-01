# Performance Benchmark Tool — Architecture

A plain map of how the tool is wired. Mermaid blocks render in Notion and GitHub.

> One-line summary: an **editor window** drives a **runtime capturer** that fills a
> per-frame buffer + spike list, which is folded into a **report (JSON)**, **scored +
> hinted**, **persisted to history**, and **drawn back in the window**.

---

## 1. Components at a glance

| Layer | File(s) | Role |
|---|---|---|
| **Config** | `BenchmarkConfigSO` | Warmup/sample durations, capture toggles, output folder, label. *(default asset lives at `Assets/_SO_Assets/Benchmark/`)* |
| | `BenchmarkHintRulesSO` | Optional custom hint rules (else built-in defaults). |
| **Runtime capture** | `PerformanceBenchmarkRunner` | MonoBehaviour. End-of-frame coroutine fills a pre-sized `FrameSnapshot[]`; detects spikes. Zero-alloc steady state. |
| | `GameLoadSampler` | Reads gameplay load (prisms, VFX, vessels, players) from `GameDataSO`. |
| | `NetMarkers` | `ProfilerMarker`/counters for the netcode hot paths (`CSM.Net.*`). |
| **Standalone runtime** | `ProfilerCsvLogger` | Per-frame CSV/summary logger (long sessions). Menu + component. |
| | `BenchmarkHUDOverlay` | F9 live on-screen HUD (fps/frame/draws/GC/load). |
| **Data model** | `FrameSnapshot` | One frame: timing, render, memory, physics, netcode, load. |
| | `SpikeEntry` + `MarkerSample` | A spike frame + its top self-time markers (`isScript`-tagged). |
| | `BenchmarkStatistics` | Aggregates (avg/p95/p99/stddev, slopes, shares). |
| | `BenchmarkReport` | The whole run (snapshots + spikes + stats + analysis). Serializes to JSON. |
| **Analysis** | `BenchmarkAnalysis` | Score (0-100) + rule-based hints; CPU/GPU verdict. |
| | `BenchmarkGrade` | Letter grade from stats. |
| | `SpikeAnalyzer` | **Editor-only** profiler bridge: reads a frame's top self-time samples via `HierarchyFrameDataView`, drops editor/engine noise, tags scripts. |
| **Persistence** | `BenchmarkReport.SaveToFile` | Writes `persistentDataPath/Benchmarks/*.json`. |
| | `BenchmarkHistory` | Index + per-run JSON; tagging. |
| **Editor UI** | `PerformanceBenchmarkWindow` | The window. Tabs: **Collect · Sweep · History · Compare** (+ **Spikes**, in progress). |
| | `EditorUIStyles` | Pastel section headers, badges, score bars. |
| | `BenchmarkAutoStart` | Enters Play (optionally via Bootstrap), enables Profiler, spawns the runner. |
| | `BenchmarkSweepRunner` | Runs the capture across multiple scenes. |
| | `BenchmarkComparer` / `BenchmarkComparison` | Diffs two runs (baseline vs current). |
| **Build** | `BenchmarkBuildAutoRunner` | Dev-build self-capture → JSON to `persistentDataPath/PerfRuns/`. |

---

## 2. Component map

```mermaid
flowchart TB
    subgraph EDITOR["Editor (UnityEditor)"]
        W["PerformanceBenchmarkWindow<br/>(Collect · Sweep · History · Compare · Spikes)"]
        AS["BenchmarkAutoStart<br/>enter Play + enable Profiler"]
        SW["BenchmarkSweepRunner"]
        CMP["BenchmarkComparer"]
        SA["SpikeAnalyzer<br/>HierarchyFrameDataView<br/>(noise filter + isScript)"]
        UI["EditorUIStyles"]
        W --> AS
        W --> SW
        W --> CMP
        W -.uses.-> UI
    end

    subgraph RUNTIME["Runtime (Play Mode / Build)"]
        R["PerformanceBenchmarkRunner<br/>end-of-frame coroutine"]
        GLS["GameLoadSampler"]
        NM["NetMarkers (CSM.Net.*)"]
        HUD["BenchmarkHUDOverlay (F9)"]
        CSV["ProfilerCsvLogger"]
        R --> GLS
        R -.reads.-> NM
    end

    subgraph DATA["Data + Analysis"]
        CFG["BenchmarkConfigSO"]
        FS["FrameSnapshot[]"]
        SP["SpikeEntry / MarkerSample"]
        REP["BenchmarkReport (JSON)"]
        STAT["BenchmarkStatistics"]
        AN["BenchmarkAnalysis<br/>score + hints"]
        HIST["BenchmarkHistory"]
    end

    AS --> R
    SW --> R
    CFG --> R
    R --> FS
    R --> SP
    SA -. editor-only marker pull .-> SP
    FS --> REP
    SP --> REP
    REP --> STAT
    REP --> AN
    REP --> HIST
    REP --> W
    HIST --> W
```

---

## 3. Capture flow (one Collect run)

```mermaid
sequenceDiagram
    participant U as You
    participant W as Window (Collect)
    participant AS as BenchmarkAutoStart
    participant R as Runner
    participant SA as SpikeAnalyzer (editor)
    participant REP as Report
    participant H as History

    U->>W: pick Config + scene, press Start
    W->>AS: RequestCaptureOnPlay(config, scene)
    AS->>AS: enable Profiler, enter Play
    AS->>R: spawn + Configure + StartBenchmark
    loop every frame (end of frame)
        R->>R: Warmup → then Sampling: fill FrameSnapshot
        alt frame > spike threshold (max 22.2ms, 1.75×mean)
            R->>SA: TryGetTopMarkers(lastFrame)
            SA-->>R: top self-time samples (noise dropped, isScript tagged)
            R->>R: add SpikeEntry
        end
    end
    R->>REP: assemble snapshots + spikes → ComputeStatistics
    REP->>REP: BenchmarkAnalysis → score + hints + verdict
    REP->>H: SaveToFile (JSON) + AddToHistory
    REP-->>W: draw Results / Hints / Spikes
```

---

## 4. Where the data lives

```mermaid
flowchart LR
    A["Collect run"] --> B["persistentDataPath/Benchmarks/*.json<br/>(report + _collect_lastrun cache)"]
    A --> C["benchmark_index.json<br/>(History list)"]
    D["Dev build self-capture"] --> E["persistentDataPath/PerfRuns/*.json"]
    F["ProfilerCsvLogger"] --> G["persistentDataPath/ProfilerCaptures/*.csv + _summary.txt"]
```

- **Editor (Mac):** `~/Library/Application Support/<company>/<product>/...`
- All three are plain text/JSON — readable by Claude. The window's **History/Compare** tabs read the `Benchmarks/` JSON.

---

## 5. The spike-attribution path (what replaces screenshots)

This is the part that turns a hitch into a script breakdown without the Profiler window:

```mermaid
flowchart TB
    F["Spike frame detected<br/>(frameTimeMs > threshold)"] --> SA
    SA["SpikeAnalyzer.TryGetTopMarkers"] --> HV["HierarchyFrameDataView<br/>main thread, sort by Self Time"]
    HV --> WALK["walk samples"]
    WALK --> NOISE{"editor/engine noise?<br/>(GUI, Gfx waits, JIT,<br/>loading, PlayerLoop…)"}
    NOISE -- yes --> DROP["drop"]
    NOISE -- no --> KEEP["keep + tag isScript<br/>(Type.Method() / coroutine / CS.*)"]
    KEEP --> TOPN["top N by self ms"]
    TOPN --> SPIKE["SpikeEntry.topMarkers"]
    SPIKE --> REPORT["report JSON"]
    SPIKE --> COPY["(planned) 'Copy for Claude' button"]
```

**Status:** noise filter + `isScript` tagging are done (commit `06d41cc`). Still to land:
the **default config asset auto-load**, the dedicated **Spikes tab**, the **live** spike
list during recording, and the **Copy-for-Claude** text export.
