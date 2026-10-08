# Branch archive: `claude/tender-pasteur-mxw3t4`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-06-11 by Claude
- **Unmerged commits:** 1
- **Forked from:** `b0e8033fd` (2026-06-12, Merge branch 'bleeding-edge' into claude/tender-pasteur-mxw3t4)
- **Tip:** `ef2048146`
- **Files touched (10):**
  - `Assets/_Scripts/Utility/PerformanceBenchmark/BENCHMARK_ARCHITECTURE.md`
  - `Assets/_Scripts/Utility/PerformanceBenchmark/BENCHMARK_TOOL.md`
  - `Assets/_Scripts/Utility/PerformanceBenchmark/BenchmarkAnalysis.cs`
  - `Assets/_Scripts/Utility/PerformanceBenchmark/BenchmarkComparison.cs`
  - `Assets/_Scripts/Utility/PerformanceBenchmark/BenchmarkReport.cs`
  - `Assets/_Scripts/Utility/PerformanceBenchmark/BenchmarkStatistics.cs`
  - `Assets/_Scripts/Utility/PerformanceBenchmark/Editor/PerformanceBenchmarkWindow.cs`
  - `Assets/_Scripts/Utility/PerformanceBenchmark/FrameSnapshot.cs`
  - `Assets/_Scripts/Utility/PerformanceBenchmark/PerformanceBenchmarkRunner.cs`
  - `Assets/_Scripts/Utility/PerformanceBenchmark/Tests/Editor/BenchmarkStatisticsTests.cs`

### `ef2048146` — feat(benchmark): schema v2 — CPU thread breakdown + physics time in captures

_Claude, 2026-06-11 21:17:41 +0000_

```text
FrameSnapshot gains cpuMainThread/cpuPresentWait/cpuRenderThread (from the
FrameTiming already fetched per frame) and physicsTimeMs (Physics.Processing
marker recorder). Statistics aggregate them plus busy-CPU (wait-for-present
removed) and physicsSharePercent; boundVerdict and the CpuBound/GpuBound hint
rules now compare GPU against busy CPU so frame-capped captures aren't
misread as CPU-bound (pre-v2 reports behave identically — busy falls back to
the raw total). Compare tab gains Busy CPU / Avg GPU / Physics Share deltas;
the History tab's copy-as-text export includes the thread/physics/netcode
breakdown. Schema version bumped 1 → 2; docs + stats tests updated.
```

```text
 .../_Scripts/Utility/PerformanceBenchmark/BENCHMARK_ARCHITECTURE.md   |  8 +++--
 Assets/_Scripts/Utility/PerformanceBenchmark/BENCHMARK_TOOL.md        | 10 +++++-
 Assets/_Scripts/Utility/PerformanceBenchmark/BenchmarkAnalysis.cs     | 14 +++++----
 Assets/_Scripts/Utility/PerformanceBenchmark/BenchmarkComparison.cs   |  5 +++
 Assets/_Scripts/Utility/PerformanceBenchmark/BenchmarkReport.cs       |  4 ++-
 Assets/_Scripts/Utility/PerformanceBenchmark/BenchmarkStatistics.cs   | 41 ++++++++++++++++++++++++
 .../Utility/PerformanceBenchmark/Editor/PerformanceBenchmarkWindow.cs |  5 ++-
 Assets/_Scripts/Utility/PerformanceBenchmark/FrameSnapshot.cs         |  8 +++++
 .../Utility/PerformanceBenchmark/PerformanceBenchmarkRunner.cs        | 12 +++++++
 .../PerformanceBenchmark/Tests/Editor/BenchmarkStatisticsTests.cs     | 56 +++++++++++++++++++++++++++++++++
 10 files changed, 151 insertions(+), 12 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 352 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Utility/PerformanceBenchmark/BENCHMARK_ARCHITECTURE.md b/Assets/_Scripts/Utility/PerformanceBenchmark/BENCHMARK_ARCHITECTURE.md
index 429141de5..dffeae5da 100644
--- a/Assets/_Scripts/Utility/PerformanceBenchmark/BENCHMARK_ARCHITECTURE.md
+++ b/Assets/_Scripts/Utility/PerformanceBenchmark/BENCHMARK_ARCHITECTURE.md
@@ -194,11 +194,13 @@ source guard**: warns when origin/platform differ (only same-source deltas are v
 ## 5. Shared data model
 
 ```
-FrameSnapshot   (one per frame: ms, cpu, gpu, draws, setpass, batches, tris, verts, gcAlloc,
-                 mem, rigidbodies, + game-load counts, + netcode counters)
+FrameSnapshot   (one per frame: ms, cpu, gpu, cpu main/wait/render thread (v2), draws,
+                 setpass, batches, tris, verts, gcAlloc, mem, rigidbodies, physics ms (v2),
+                 + game-load counts, + netcode counters)
         │ aggregated by
 BenchmarkStatistics  (avg/p95/p99/max frame ms, avg/p1 fps, stddev, avg draws/tris,
-                      total GC, CPU/GPU avgs, netcode share, collector overhead)
+                      total GC, CPU/GPU avgs + thread breakdown + busy-CPU (v2),
+                      physics share (v2), netcode share, collector overhead)
         │ scored by
 BenchmarkAnalysis    (0–100 score, A–F grade via BenchmarkGrade, boundVerdict CPU/GPU,
                       hints[] from BenchmarkHintRulesSO rules)
diff --git a/Assets/_Scripts/Utility/PerformanceBenchmark/BENCHMARK_TOOL.md b/Assets/_Scripts/Utility/PerformanceBenchmark/BENCHMARK_TOOL.md
index baa313836..0c4a87ee1 100644
--- a/Assets/_Scripts/Utility/PerformanceBenchmark/BENCHMARK_TOOL.md
+++ b/Assets/_Scripts/Utility/PerformanceBenchmark/BENCHMARK_TOOL.md
@@ -209,7 +209,15 @@ instancing, `sharedMaterial` + `MaterialPropertyBlock`, SOAP over `Find*`).
 
 Rule types: `GcPerFrameKb`, `MemorySlopeKbPerFrame` (leak), `AvgDrawCalls`, `GpuBound`, `CpuBound`,
 `FrameInstability`, `SpikeMarkerName`, `NetcodeSharePercent`, `RpcsPerFrame`. Severities: `Info`,
-`Warning`, `Blocker`.
+`Warning`, `Blocker`. The `GpuBound`/`CpuBound` rules and the report's `boundVerdict` compare GPU
+time against **busy CPU** (`EffectiveCpuTimeMs` — wait-for-present removed) on schema-v2 captures,
+so frame-capped runs aren't misread as CPU-bound.
+
+Schema v2 (see `BenchmarkReport.CurrentSchemaVersion`) adds to every `FrameSnapshot`: CPU
+main-thread / present-wait / render-thread times and `physicsTimeMs` (the `Physics.Processing`
+marker), and to `statistics`: their averages plus `avgCpuBusyTimeMs` / `maxCpuBusyTimeMs` /
+`physicsSharePercent`. Pre-v2 reports load fine — the new fields read as 0 and verdicts fall back
+to the raw CPU total.
 
 ---
 
diff --git a/Assets/_Scripts/Utility/PerformanceBenchmark/BenchmarkAnalysis.cs b/Assets/_Scripts/Utility/PerformanceBenchmark/BenchmarkAnalysis.cs
index 875d6c9d3..3580bf6d3 100644
--- a/Assets/_Scripts/Utility/PerformanceBenchmark/BenchmarkAnalysis.cs
+++ b/Assets/_Scripts/Utility/PerformanceBenchmark/BenchmarkAnalysis.cs
@@ -138,8 +138,10 @@ namespace CosmicShore.Utility.PerformanceBenchmark
             return Math.Max(0, Math.Min(100, score));
         }
 
+        // Busy CPU (wait-for-present removed) when the capture has thread times (schema v2),
+        // else the raw total — identical to pre-v2 behavior on old reports.
         static string BoundVerdict(BenchmarkStatistics s) =>
-            FrameBoundness.Classify(s.avgCpuFrameTimeMs, s.avgGpuFrameTimeMs);
+            FrameBoundness.Classify(s.EffectiveCpuTimeMs, s.avgGpuFrameTimeMs);
 
         static bool TryEvaluate(HintRule rule, BenchmarkStatistics s, List<SpikeEntry> spikes, out BenchmarkHint hint)
         {
@@ -172,15 +174,15 @@ namespace CosmicShore.Utility.PerformanceBenchmark
                 case HintRuleType.GpuBound:
                 {
                     if (s.avgGpuFrameTimeMs <= 0.001f) return false;
-                    if (s.avgGpuFrameTimeMs <= s.avgCpuFrameTimeMs * rule.threshold) return false;
-                    finding = $"GPU {s.avgGpuFrameTimeMs:F1} ms vs CPU {s.avgCpuFrameTimeMs:F1} ms — GPU-bound.";
+                    if (s.avgGpuFrameTimeMs <= s.EffectiveCpuTimeMs * rule.threshold) return false;
+                    finding = $"GPU {s.avgGpuFrameTimeMs:F1} ms vs busy CPU {s.EffectiveCpuTimeMs:F1} ms — GPU-bound.";
                     break;
                 }
                 case HintRuleType.CpuBound:
                 {
-                    if (s.avgCpuFrameTimeMs <= 0.001f) return false;
-                    if (s.avgCpuFrameTimeMs <= s.avgGpuFrameTimeMs * rule.threshold) return false;
-                    finding = $"CPU {s.avgCpuFrameTimeMs:F1} ms vs GPU {s.avgGpuFrameTimeMs:F1} ms — CPU-bound.";
+                    if (s.EffectiveCpuTimeMs <= 0.001f) return false;
+                    if (s.EffectiveCpuTimeMs <= s.avgGpuFrameTimeMs * rule.threshold) return false;
+                    finding = $"Busy CPU {s.EffectiveCpuTimeMs:F1} ms vs GPU {s.avgGpuFrameTimeMs:F1} ms — CPU-bound.";
                     break;
                 }
                 case HintRuleType.FrameInstability:
diff --git a/Assets/_Scripts/Utility/PerformanceBenchmark/BenchmarkComparison.cs b/Assets/_Scripts/Utility/PerformanceBenchmark/BenchmarkComparison.cs
index 5044f6791..2c8662468 100644
--- a/Assets/_Scripts/Utility/PerformanceBenchmark/BenchmarkComparison.cs
+++ b/Assets/_Scripts/Utility/PerformanceBenchmark/BenchmarkComparison.cs
@@ -75,6 +75,11 @@ namespace CosmicShore.Utility.PerformanceBenchmark
                 new MetricDelta("P99 Frame Time (ms)", bStats.p99FrameTimeMs, cStats.p99FrameTimeMs, true, neutralThresholdPercent),
                 new MetricDelta("StdDev Frame Time (ms)", bStats.stdDevFrameTimeMs, cStats.stdDevFrameTimeMs, true, neutralThresholdPercent),
 
+                // CPU / GPU / physics (lower is better; 0 on both sides reads as neutral)
+                new MetricDelta("Busy CPU (ms)", bStats.EffectiveCpuTimeMs, cStats.EffectiveCpuTimeMs, true, neutralThresholdPercent),
+                new MetricDelta("Avg GPU (ms)", bStats.avgGpuFrameTimeMs, cStats.avgGpuFrameTimeMs, true, neutralThresholdPercent),
+                new MetricDelta("Physics Share (%)", bStats.physicsSharePercent, cStats.physicsSharePercent, true, neutralThresholdPercent),
+
                 // Rendering (lower is better)
                 new MetricDelta("Avg Draw Calls", bStats.avgDrawCalls, cStats.avgDrawCalls, true, neutralThresholdPercent),
                 new MetricDelta("Avg Batches", bStats.avgBatches, cStats.avgBatches, true, neutralThresholdPercent),
diff --git a/Assets/_Scripts/Utility/PerformanceBenchmark/BenchmarkReport.cs b/Assets/_Scripts/Utility/PerformanceBenchmark/BenchmarkReport.cs
index 96e422884..675e0ec3b 100644
--- a/Assets/_Scripts/Utility/PerformanceBenchmark/BenchmarkReport.cs
+++ b/Assets/_Scripts/Utility/PerformanceBenchmark/BenchmarkReport.cs
@@ -46,7 +46,9 @@ namespace CosmicShore.Utility.PerformanceBenchmark
         // Bump CurrentSchemaVersion on any field change; readers must tolerate older/unknown
         // versions. Defaults to 0 (no initializer) so legacy JSON lacking the key reads as 0
         // = "legacy/unversioned"; PopulateEnvironment stamps the current version on new runs.
-        public const int CurrentSchemaVersion = 1;
+        // v2: FrameSnapshot gains cpuMainThread/cpuPresentWait/cpuRenderThread/physicsTimeMs;
+        //     statistics gain thread-breakdown averages, busy-CPU, and physics time/share.
+        public const int CurrentSchemaVersion = 2;
         public int schemaVersion;
         public SourceInfo source = new();
 
diff --git a/Assets/_Scripts/Utility/PerformanceBenchmark/BenchmarkStatistics.cs b/Assets/_Scripts/Utility/PerformanceBenchmark/BenchmarkStatistics.cs
index e337d436a..7942e9e84 100644
--- a/Assets/_Scripts/Utility/PerformanceBenchmark/BenchmarkStatistics.cs
+++ b/Assets/_Scripts/Utility/PerformanceBenchmark/BenchmarkStatistics.cs
@@ -27,6 +27,22 @@ namespace CosmicShore.Utility.PerformanceBenchmark
         public float avgGpuFrameTimeMs;
         public float maxGpuFrameTimeMs;
 
+        // CPU thread breakdown (ms, schema v2) — 0 on pre-v2 reports / unsupported platforms.
+        public float avgCpuMainThreadTimeMs;
+        public float avgCpuPresentWaitTimeMs;
+        public float avgCpuRenderThreadTimeMs;
+
+        // Busy CPU = max(main thread − present wait, render thread) per frame — actual CPU
+        // work with the vsync/targetFrameRate idle removed. Equals the raw total on pre-v2
+        // data (no thread times), so verdicts stay backward-compatible.
+        public float avgCpuBusyTimeMs;
+        public float maxCpuBusyTimeMs;
+
+        /// <summary>CPU number the bound verdict compares against GPU: busy time when
+        /// available, else the raw total. Not serialized (derived).</summary>
+        public float EffectiveCpuTimeMs =>
+            avgCpuBusyTimeMs > 0.001f ? avgCpuBusyTimeMs : avgCpuFrameTimeMs;
+
         // FPS
         public float avgFps;
         public float minFps;
@@ -57,6 +73,9 @@ namespace CosmicShore.Utility.PerformanceBenchmark
 
         // Physics
         public float avgActiveRigidbodies;
+        public float avgPhysicsTimeMs;       // Physics.Processing (schema v2)
+        public float maxPhysicsTimeMs;
+        public float physicsSharePercent;    // avg physics time as a % of avg frame time
 
         // Netcode (NGO)
         public float avgNetcodeTimeMs;
@@ -107,6 +126,11 @@ namespace CosmicShore.Utility.PerformanceBenchmark
             long sumPlayers = 0;
             float sumCpu = 0;
```

</details>
