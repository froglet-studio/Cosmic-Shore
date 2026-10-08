# Branch archive: `claude/merge-bleeding-edge-conflicts-7iqozq`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-07-16 by Claude
- **Unmerged commits:** 1
- **Forked from:** `21c3307d1` (2026-07-16, merge: bleeding-edge into Ys-bleeding-edge — keep Ys perf code, adopt em-das)
- **Tip:** `1bb3ced08`
- **Files touched (2):**
  - `Assets/_Scripts/Utility/PerformanceBenchmark/BENCHMARK_TEST_PROCEDURE.md`
  - `Assets/_Scripts/Utility/PerformanceBenchmark/BENCHMARK_TOOL.md`

### `1bb3ced08` — docs(benchmark): align procedure doc with the real tool UI and test inventory

_Claude, 2026-07-16 00:34:39 +0000_

```text
Post-merge verification found the bleeding-edge update to
BENCHMARK_TEST_PROCEDURE.md fixed only the Quick Start, leaving the body
contradicting it and the actual window:

- Config menu path: Create > ScriptableObjects > Tools > Benchmark Config
  (was CosmicShore > Tools, which doesn't exist)
- Tab/button labels match PerformanceBenchmarkWindow.cs: Runtime Capture tab,
  Start Recording, Stop & Analyze / Stop & Save, Copy Text
- Test inventory: 8 classes / 88 edit-mode tests (BenchmarkAnalysisTests and
  FrameBoundnessTests were missing; per-class counts refreshed)
- Removed nonexistent CosmicShore.PerformanceBenchmark.Tests.asmdef and
  CosmicShore.Runtime.asmref rows; tests compile in the default editor
  assembly under the CosmicShore.Utility.PerformanceBenchmark.Tests namespace
  (BENCHMARK_TOOL.md row updated to match)
```

```text
 .../_Scripts/Utility/PerformanceBenchmark/BENCHMARK_TEST_PROCEDURE.md | 36 ++++++++++++++++-----------------
 Assets/_Scripts/Utility/PerformanceBenchmark/BENCHMARK_TOOL.md        |  2 +-
 2 files changed, 19 insertions(+), 19 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Utility/PerformanceBenchmark/BENCHMARK_TEST_PROCEDURE.md b/Assets/_Scripts/Utility/PerformanceBenchmark/BENCHMARK_TEST_PROCEDURE.md
index c58150ece..bc0328f53 100644
--- a/Assets/_Scripts/Utility/PerformanceBenchmark/BENCHMARK_TEST_PROCEDURE.md
+++ b/Assets/_Scripts/Utility/PerformanceBenchmark/BENCHMARK_TEST_PROCEDURE.md
@@ -19,7 +19,7 @@ Every run is saved to disk. You can compare any two runs in the History tab.
 
 ### Required: BenchmarkConfigSO
 
-Create via `Create > CosmicShore > Tools > Benchmark Config`. Configure:
+Create via `Create > ScriptableObjects > Tools > Benchmark Config`. Configure:
 
 | Setting | Default | Purpose |
 |---|---|---|
@@ -46,10 +46,10 @@ The editor window auto-creates a runner GameObject if none exists. If you want p
 ### From the Editor Window
 
 1. Open `FrogletTools > Performance Benchmark`
-2. Go to the **Run** tab
+2. Go to the **Runtime Capture** tab
 3. Assign your `BenchmarkConfigSO`
 4. Enter Play Mode
-5. Click **Start Benchmark**
+5. Click **Start Recording**
 6. Wait for the progress bar to complete (warmup → sampling)
 7. Results appear below with a health grade (A–F) and stat summary
 
@@ -61,7 +61,7 @@ The editor window auto-creates a runner GameObject if none exists. If you want p
 
 ### Stopping early
 
-Click **Stop Early** during a run. Captured frames up to that point are still saved and indexed.
+Click **Stop & Analyze** during a run (or **Stop & Save** during a manual sweep). Captured frames up to that point are still saved and indexed.
 
 ---
 
@@ -179,7 +179,7 @@ The summary badges show total counts: "3 Improved", "2 Unchanged", "1 Regressed"
 
 ### Copy to Clipboard
 
-Click **Copy Text Report** to copy the full comparison as formatted ASCII text — useful for pasting into PRs, Slack, or commit messages.
+Click **Copy Text** to copy the full comparison as formatted ASCII text — useful for pasting into PRs, Slack, or commit messages.
 
 ---
 
@@ -187,27 +187,29 @@ Click **Copy Text Report** to copy the full comparison as formatted ASCII text 
 
 ### Test Assembly
 
-Tests live in `Assets/_Scripts/Utility/PerformanceBenchmark/Tests/Editor/` under the `CosmicShore.PerformanceBenchmark.Tests` assembly definition (editor-only, NUnit).
+Tests live in `Assets/_Scripts/Utility/PerformanceBenchmark/Tests/Editor/` in the `CosmicShore.Utility.PerformanceBenchmark.Tests` namespace. There is no dedicated `.asmdef` — like most first-party test folders they compile into Unity's default editor assembly (see `Assets/_Scripts/Tests/UNIT_TESTING_GUIDE.md`).
 
 ### Running in Unity
 
 1. Open `Window > General > Test Runner`
 2. Select the **EditMode** tab
-3. Expand **CosmicShore.PerformanceBenchmark.Tests**
+3. Expand the default editor assembly and find the `CosmicShore.Utility.PerformanceBenchmark.Tests` namespace
 4. Click **Run All** or run individual test classes
 
 ### Test Coverage
 
 | Test Class | Tests | What It Covers |
 |---|---|---|
-| **BenchmarkStatisticsTests** | 10 | Null/empty input, single frame, multi-frame averages, median, stddev, rendering stats, memory tracking, percentiles |
-| **MetricDeltaTests** | 10 | Verdict classification, neutral threshold logic, edge cases (zero baseline, identical values, percent calculation) |
+| **BenchmarkStatisticsTests** | 15 | Null/empty input, single frame, multi-frame averages, median, stddev, rendering stats, memory tracking, percentiles |
+| **MetricDeltaTests** | 11 | Verdict classification, neutral threshold logic, edge cases (zero baseline, identical values, percent calculation) |
 | **BenchmarkComparerTests** | 9 | Identical reports, better/worse FPS detection, delta count validation, custom threshold, text formatting |
-| **BenchmarkReportTests** | 4 | ComputeStatistics, save/load round-trip, snapshot preservation |
-| **BenchmarkConfigSOTests** | 8 | Default values, positive durations, enabled captures, SerializedObject field access |
-| **BenchmarkHistoryTests** | 12 | Add/deduplicate/ordering, GetAll/GetLatest, tagging, GetByTag, GetByScene, RemoveEntry, RebuildIndex, GetTrendSummary, LoadReport |
+| **BenchmarkReportTests** | 8 | ComputeStatistics, save/load round-trip, snapshot preservation |
+| **BenchmarkConfigSOTests** | 13 | Default values, positive durations, enabled captures, SerializedObject field access |
+| **BenchmarkHistoryTests** | 14 | Add/deduplicate/ordering, GetAll/GetLatest, tagging, GetByTag, GetByScene, RemoveEntry, RebuildIndex, GetTrendSummary, LoadReport |
+| **BenchmarkAnalysisTests** | 9 | Score/health-grade analysis of captured runs |
+| **FrameBoundnessTests** | 9 | CPU/GPU frame-boundness classification |
 
-**Total: 53 tests**
+**Total: 88 tests**
 
 ### Test Isolation
 
@@ -228,10 +230,8 @@ Tests live in `Assets/_Scripts/Utility/PerformanceBenchmark/Tests/Editor/` under
 | `BenchmarkComparison.cs` | MetricDelta + BenchmarkComparer for side-by-side run comparison |
 | `BenchmarkHistory.cs` | On-disk index manager for persistent snapshot tracking |
 | `FrameSnapshot.cs` | Per-frame data struct (timing, rendering, memory, physics) |
-| `Editor/PerformanceBenchmarkWindow.cs` | Editor window with Run, History, and Compare tabs |
-| `CosmicShore.Runtime.asmref` | Assembly reference to compile with CosmicShore.Runtime |
-| `Tests/Editor/*.cs` | 53 NUnit edit-mode tests |
-| `Tests/Editor/CosmicShore.PerformanceBenchmark.Tests.asmdef` | Test assembly definition |
+| `Editor/PerformanceBenchmarkWindow.cs` | Editor window with Runtime Capture, Sweep, History, and Compare tabs |
+| `Tests/Editor/*.cs` | 88 NUnit edit-mode tests (no `.asmdef` — default editor assembly) |
 
 ---
 
@@ -239,7 +239,7 @@ Tests live in `Assets/_Scripts/Utility/PerformanceBenchmark/Tests/Editor/` under
 
 | Problem | Solution |
 |---|---|
-| "Start Benchmark" button is grayed out | Enter Play Mode first |
+| "Start Recording" button is grayed out | Enter Play Mode first |
 | No config slot visible | Create a `BenchmarkConfigSO` asset and assign it |
 | History shows 0 snapshots | Check that the output folder matches your config. Click "Rebuild Index" |
 | Rendering stats are all zero | Enable "Capture Rendering Stats" in the config. Some stats may not be available on all platforms |
diff --git a/Assets/_Scripts/Utility/PerformanceBenchmark/BENCHMARK_TOOL.md b/Assets/_Scripts/Utility/PerformanceBenchmark/BENCHMARK_TOOL.md
index baa313836..022fadc1f 100644
--- a/Assets/_Scripts/Utility/PerformanceBenchmark/BENCHMARK_TOOL.md
+++ b/Assets/_Scripts/Utility/PerformanceBenchmark/BENCHMARK_TOOL.md
@@ -272,7 +272,7 @@ Reports include per-frame snapshots, aggregated statistics, spikes (with markers
 | Config | `BenchmarkConfigSO.cs` (default asset: `_SO_Assets/Benchmark/BenchmarkConfig.asset`) |
 | Auto-enter-Play hook | `Editor/BenchmarkAutoStart.cs` |
 | Pastel UI helpers | `Editor/EditorUIStyles.cs` |
-| Tests | `Tests/Editor/*` (`CosmicShore.PerformanceBenchmark.Tests`) |
+| Tests | `Tests/Editor/*` (`CosmicShore.Utility.PerformanceBenchmark.Tests` namespace, no `.asmdef`) |
 
 Most live under `Assets/_Scripts/Utility/PerformanceBenchmark/`. `NetMarkers` placement lives in the
 gameplay/netcode code (e.g. `VesselController`, `R_VesselActionHandler`, `Player`, the vessel
```

</details>
