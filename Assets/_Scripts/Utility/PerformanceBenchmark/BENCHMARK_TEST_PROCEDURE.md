# Performance Benchmark Tool — Test Procedure

## Quick Start

1. **Create a config asset**: Right-click in the Project window → Create → CosmicShore → Tools → Benchmark Config
2. **Open the tool**: Menu bar → FrogletTools → Performance Benchmark
3. **Drag the config** into the "Config" slot
4. **Enter Play Mode** in the scene you want to measure
5. **Click "Start Benchmark"** — results appear automatically when it finishes

Every run is saved to disk. You can compare any two runs in the History tab.

---

## Setup

### Required: BenchmarkConfigSO

Create via `Create > CosmicShore > Tools > Benchmark Config`. Configure:

| Setting | Default | Purpose |
|---|---|---|
| Warmup Duration | 3s | Scene stabilization time before recording begins |
| Sample Duration | 10s | How long to capture frame data |
| Capture Rendering Stats | true | Draw calls, batches, triangles, vertices, SetPass calls |
| Capture Memory Stats | true | Heap size, GC allocations per frame |
| Capture Physics Stats | true | Active rigidbody count |
| Output Folder | `Benchmarks` | Subfolder in `Application.persistentDataPath` |
| Benchmark Label | (empty) | Human-readable tag for this run (e.g., "GDC_Demo", "Squirrel_Race") |

### Optional: BenchmarkDataSO

If you want real-time progress events in custom UI or other systems, assign a `BenchmarkDataSO` asset. This is the SOAP data container that broadcasts lifecycle events (`OnBenchmarkStarted`, `OnSamplingStarted`, `OnBenchmarkCompleted`, `OnProgressUpdated`). The editor window works without it, but assigning one enables the frames-captured counter during runs.

### Optional: PerformanceBenchmarkRunner in scene

The editor window auto-creates a runner GameObject if none exists. If you want persistent config, add a `PerformanceBenchmarkRunner` component to a GameObject in your scene and assign the config + data container in the inspector. Check `Auto Start On Enable` to benchmark every time you enter Play Mode.

---

## Running a Benchmark

### From the Editor Window

1. Open `FrogletTools > Performance Benchmark`
2. Go to the **Run** tab
3. Assign your `BenchmarkConfigSO`
4. Enter Play Mode
5. Click **Start Benchmark**
6. Wait for the progress bar to complete (warmup → sampling)
7. Results appear below with a health grade (A–F) and stat summary

### What happens during a run

1. **Warmup phase** (default 3s) — the scene runs but no data is recorded. This lets shaders compile, objects spawn, and physics settle.
2. **Sampling phase** (default 10s) — every frame is captured as a `FrameSnapshot` containing frame time, FPS, and optionally rendering/memory/physics stats.
3. **Completion** — statistics are computed, the report is saved to JSON, and the run is indexed in the history.

### Stopping early

Click **Stop Early** during a run. Captured frames up to that point are still saved and indexed.

---

## Reading Results

### Health Grade

| Grade | Criteria | Meaning |
|---|---|---|
| **A** | Avg FPS ≥ 55, P99 frame time < 25ms, StdDev < 5ms | Excellent — smooth and stable |
| **B** | Avg FPS ≥ 45, P99 frame time < 35ms | Good — playable with minor hitches |
| **C** | Avg FPS ≥ 30, P99 frame time < 50ms | Acceptable — noticeable frame drops |
| **D** | Avg FPS ≥ 20 | Poor — frequent stutters |
| **F** | Below all thresholds | Critical — not playable |

### Key Metrics Explained

| Metric | What It Means | Target |
|---|---|---|
| **Avg FPS** | Average frames per second | ≥ 60 for mobile |
| **Worst 1% FPS (P1)** | FPS during the worst spike frames | ≥ 30 |
| **Avg Frame Time** | Time per frame in milliseconds (16.7ms = 60fps) | ≤ 16.7ms |
| **Worst 1% Frame Time (P99)** | Frame time during the worst spikes | < 25ms |
| **Stability (StdDev)** | How consistent frame times are. Lower = smoother | < 5ms |
| **Draw Calls** | GPU commands per frame | Lower is better |
| **Batches** | Grouped draw calls | Lower = better batching |
| **Triangles** | Scene geometry per frame | Lower is better |
| **Peak Memory** | Maximum memory used during the run | Monitor for growth |
| **GC Allocations** | Total garbage created (causes stutter when collected) | Minimize |

---

## Unity Profiler Integration

The benchmark writes custom profiler counters visible in Unity's Profiler window while a run is active:

| Counter | Module | Description |
|---|---|---|
| Benchmark FPS | Scripts | Current FPS as measured by the benchmark |
| Benchmark Frame Time (ms) | Scripts | Current frame time |
| Benchmark Draw Calls | Scripts | Draw calls captured this frame |
| Benchmark Frames Captured | Scripts | Total frames captured so far |

To view these:
1. Open `Window > Analysis > Profiler`
2. Start a benchmark
3. The counters appear under the **Scripts** module
4. You can add them to the Profiler chart by clicking the module dropdown

The runner also wraps its capture in a `ProfilerMarker` named `CosmicShore.BenchmarkCapture`, visible in the Timeline view.

---

## Managing History

### History Tab

Every benchmark run is automatically indexed. The History tab shows all saved snapshots with:
- Label, tag, git branch/commit
- Date, scene name, frame count
- Key stats: Avg FPS, P1 FPS, Avg frame time, P99 frame time

### Tagging Snapshots

Click **Tag** on any entry to label it (e.g., "baseline", "after-optimization", "GDC-build"). Tags help identify important runs when comparing.

### Rebuilding the Index

If report JSON files were added/removed manually or the index gets corrupted, click **Rebuild Index**. This scans the output folder and reconstructs the index from all `.json` report files.

### Deleting Snapshots

Click **Del** on any entry. Confirmation dialog appears. Deletes both the index entry and the JSON file.

### Accessing Raw JSON

Click **JSON** on any entry to open the file in your system's file browser. Reports are saved at:
```
{Application.persistentDataPath}/{outputFolder}/{label}_{timestamp}_{reportId}.json
```

---

## Comparing Runs

### Typical Workflow

1. Run a benchmark **before** making changes → tag it "baseline"
2. Make your optimization changes
3. Run another benchmark
4. Go to the **Compare** tab
5. Select the baseline and current runs from the dropdowns
6. Click **Compare**

### Shortcut: From History Tab

Click **Baseline** on any entry to set it as the baseline, or **Current** to set it as the current run. The tool automatically switches to the Compare tab and runs the comparison.

### Reading the Comparison

- **Green rows** = improved (better than baseline)
- **Red rows** = regressed (worse than baseline)
- **No highlight** = unchanged (within ±2% threshold)

The summary badges show total counts: "3 Improved", "2 Unchanged", "1 Regressed".

### 17 Metrics Compared

| Category | Metrics |
|---|---|
| FPS (higher is better) | Avg, Min, P1, P5, Median |
| Frame Time (lower is better) | Avg, Max, P95, P99, StdDev |
| Rendering (lower is better) | Draw Calls, Batches, SetPass Calls, Triangles |
| Memory (lower is better) | Peak Allocated MB, Avg Allocated MB, Total GC MB |

### Copy to Clipboard

Click **Copy Text Report** to copy the full comparison as formatted ASCII text — useful for pasting into PRs, Slack, or commit messages.

---

## Running Unit Tests

### Test Assembly

Tests live in `Assets/_Scripts/Utility/PerformanceBenchmark/Tests/Editor/` under the `CosmicShore.PerformanceBenchmark.Tests` assembly definition (editor-only, NUnit).

### Running in Unity

1. Open `Window > General > Test Runner`
2. Select the **EditMode** tab
3. Expand **CosmicShore.PerformanceBenchmark.Tests**
4. Click **Run All** or run individual test classes

### Test Coverage

| Test Class | Tests | What It Covers |
|---|---|---|
| **BenchmarkStatisticsTests** | 10 | Null/empty input, single frame, multi-frame averages, median, stddev, rendering stats, memory tracking, percentiles |
| **MetricDeltaTests** | 10 | Verdict classification, neutral threshold logic, edge cases (zero baseline, identical values, percent calculation) |
| **BenchmarkComparerTests** | 9 | Identical reports, better/worse FPS detection, delta count validation, custom threshold, text formatting |
| **BenchmarkReportTests** | 4 | ComputeStatistics, save/load round-trip, snapshot preservation |
| **BenchmarkConfigSOTests** | 8 | Default values, positive durations, enabled captures, SerializedObject field access |
| **BenchmarkHistoryTests** | 12 | Add/deduplicate/ordering, GetAll/GetLatest, tagging, GetByTag, GetByScene, RemoveEntry, RebuildIndex, GetTrendSummary, LoadReport |

**Total: 53 tests**

### Test Isolation

- `BenchmarkHistoryTests` create a unique temporary folder per test via `Guid` and clean up in `TearDown`
- `BenchmarkConfigSOTests` create/destroy ScriptableObject instances in `SetUp`/`TearDown`
- All tests are purely edit-mode — no Play Mode or scene loading required

---

## File Reference

| File | Purpose |
|---|---|
| `PerformanceBenchmarkRunner.cs` | MonoBehaviour that captures per-frame data during Play Mode |
| `BenchmarkConfigSO.cs` | ScriptableObject configuration (timing, captures, output) |
| `BenchmarkReport.cs` | Serializable report with environment info, snapshots, and statistics |
| `BenchmarkStatistics.cs` | Pure computation: percentiles, averages, stddev from frame data |
| `BenchmarkComparison.cs` | MetricDelta + BenchmarkComparer for side-by-side run comparison |
| `BenchmarkHistory.cs` | On-disk index manager for persistent snapshot tracking |
| `FrameSnapshot.cs` | Per-frame data struct (timing, rendering, memory, physics) |
| `Editor/PerformanceBenchmarkWindow.cs` | Editor window with Run, History, and Compare tabs |
| `CosmicShore.Runtime.asmref` | Assembly reference to compile with CosmicShore.Runtime |
| `Tests/Editor/*.cs` | 53 NUnit edit-mode tests |
| `Tests/Editor/CosmicShore.PerformanceBenchmark.Tests.asmdef` | Test assembly definition |

---

## Troubleshooting

| Problem | Solution |
|---|---|
| "Start Benchmark" button is grayed out | Enter Play Mode first |
| No config slot visible | Create a `BenchmarkConfigSO` asset and assign it |
| History shows 0 snapshots | Check that the output folder matches your config. Click "Rebuild Index" |
| Rendering stats are all zero | Enable "Capture Rendering Stats" in the config. Some stats may not be available on all platforms |
| GC Allocations look wrong | The tool uses `ProfilerRecorder("GC Allocated In Frame")` which requires Unity 2021+. Verify your Unity version |
| Physics stats missing | Enable "Capture Physics Stats" in the config. The recorder uses `"Active Dynamic Bodies"` which requires Unity's physics profiler module |
| Reports not saving | Check that `Application.persistentDataPath` is writable. Look in the Console for `[Benchmark]` log messages |
| Profiler counters not visible | Open Unity Profiler, look under the "Scripts" module. Counters only update while a benchmark is actively running |
