using System.Collections.Generic;
using CosmicShore.Utility;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;

namespace CosmicShore.Utility.PerformanceBenchmark
{
    /// <summary>
    /// Runtime component that captures per-frame performance data for a configured
    /// duration and produces a <see cref="BenchmarkReport"/>.
    ///
    /// All state is written into the <see cref="BenchmarkDataSO"/> container and
    /// lifecycle transitions are broadcast via SOAP events, keeping this runner fully
    /// decoupled from any UI or tooling consumers.
    ///
    /// Usage:
    ///   1. Attach to a GameObject in the scene you want to benchmark.
    ///   2. Assign a <see cref="BenchmarkConfigSO"/> and a <see cref="BenchmarkDataSO"/>.
    ///   3. Call <see cref="StartBenchmark"/> (or check autoStartOnEnable).
    ///   4. Consumers subscribe to events on the BenchmarkDataSO asset.
    /// </summary>
    public class PerformanceBenchmarkRunner : MonoBehaviour
    {
        static readonly ProfilerMarker s_benchmarkMarker = new("CosmicShore.BenchmarkCapture");

        // ── Custom Profiler Counters ─────────────────────
        // These show up in Unity's Profiler window under the "CosmicShore" module,
        // giving real-time visibility into benchmark metrics without the editor window.
        static readonly ProfilerCategory s_cosmicCategory = ProfilerCategory.Scripts;

        static readonly ProfilerCounterValue<float> s_counterFps =
            new(s_cosmicCategory, "Benchmark FPS", ProfilerMarkerDataUnit.Count,
                ProfilerCounterOptions.FlushOnEndOfFrame);

        static readonly ProfilerCounterValue<float> s_counterFrameTimeMs =
            new(s_cosmicCategory, "Benchmark Frame Time (ms)", ProfilerMarkerDataUnit.TimeNanoseconds,
                ProfilerCounterOptions.FlushOnEndOfFrame);

        static readonly ProfilerCounterValue<int> s_counterDrawCalls =
            new(s_cosmicCategory, "Benchmark Draw Calls", ProfilerMarkerDataUnit.Count,
                ProfilerCounterOptions.FlushOnEndOfFrame);

        static readonly ProfilerCounterValue<int> s_counterFramesCaptured =
            new(s_cosmicCategory, "Benchmark Frames Captured", ProfilerMarkerDataUnit.Count,
                ProfilerCounterOptions.FlushOnEndOfFrame);

        [Header("Configuration")]
        [SerializeField] private BenchmarkConfigSO config;

        [Header("SOAP Data Container")]
        [Tooltip("Central data container that holds runtime state and events. " +
                 "Wire the same asset into any UI or system that needs to react to benchmark lifecycle.")]
        [SerializeField] private BenchmarkDataSO benchmarkData;

        [Header("Automation")]
        [Tooltip("Automatically start the benchmark when this component is enabled.")]
        [SerializeField] private bool autoStartOnEnable;

        enum State { Idle, WarmingUp, Sampling, Done }

        [SerializeField, HideInInspector] private State state = State.Idle;

        float stateTimer;
        float progressUpdateInterval = 0.5f;
        float nextProgressUpdate;
        int frameCounter;
        List<FrameSnapshot> snapshots;
        BenchmarkReport currentReport;

        // Running averages for live progress reporting
        float runningFpsSum;
        float runningFrameTimeMs;

        // Cached config flags — avoid SO property getter per frame
        bool cachedCaptureRendering;
        bool cachedCaptureMemory;
        bool cachedCapturePhysics;

        // Profiler recorders for rendering stats
        ProfilerRecorder drawCallsRecorder;
        ProfilerRecorder batchesRecorder;
        ProfilerRecorder setPassRecorder;
        ProfilerRecorder trianglesRecorder;
        ProfilerRecorder verticesRecorder;

        // Profiler recorders for memory and physics — zero-allocation alternatives
        ProfilerRecorder gcAllocRecorder;
        ProfilerRecorder activeBodiesRecorder;

        public bool IsRunning => state == State.WarmingUp || state == State.Sampling;
        public float Progress
        {
            get
            {
                if (config == null) return 0;
                return state switch
                {
                    State.WarmingUp => stateTimer / config.WarmupDuration * 0.1f,
                    State.Sampling => 0.1f + stateTimer / config.SampleDuration * 0.9f,
                    State.Done => 1f,
                    _ => 0f
                };
            }
        }

        void OnEnable()
        {
            if (autoStartOnEnable && config != null)
                StartBenchmark();
        }

        void OnDisable()
        {
            DisposeRecorders();
        }

        public void StartBenchmark()
        {
            if (config == null)
            {
                CSDebug.LogError("[Benchmark] No BenchmarkConfigSO assigned.");
                return;
            }

            if (IsRunning)
            {
                CSDebug.LogWarning("[Benchmark] Already running — ignoring StartBenchmark call.");
                return;
            }

            // Cache config flags to avoid SO property getter overhead per frame
            cachedCaptureRendering = config.CaptureRenderingStats;
            cachedCaptureMemory = config.CaptureMemoryStats;
            cachedCapturePhysics = config.CapturePhysicsStats;

            int estimatedFrames = Mathf.CeilToInt(config.SampleDuration * 120);
            snapshots = new List<FrameSnapshot>(estimatedFrames);
            frameCounter = 0;
            runningFpsSum = 0;
            runningFrameTimeMs = 0;

            currentReport = new BenchmarkReport
            {
                label = config.BenchmarkLabel,
                warmupDuration = config.WarmupDuration,
                sampleDuration = config.SampleDuration
            };
            currentReport.PopulateEnvironment();

            StartRecorders();

            stateTimer = 0;
            nextProgressUpdate = 0;
            state = State.WarmingUp;

            // Update SOAP data container
            if (benchmarkData != null)
            {
                benchmarkData.IsRunning = true;
                benchmarkData.IsSampling = false;
                benchmarkData.Progress = 0f;
                benchmarkData.FramesCaptured = 0;
                benchmarkData.ActiveLabel = config.BenchmarkLabel;
                benchmarkData.LastReportPath = string.Empty;
                benchmarkData.OnBenchmarkStarted?.Raise();
            }

            CSDebug.Log($"[Benchmark] Started — warming up for {config.WarmupDuration}s, then sampling for {config.SampleDuration}s.");
        }

        public void StopBenchmark()
        {
            if (!IsRunning) return;
            FinishRun(wasStopped: true);
        }

        void Update()
        {
            switch (state)
            {
                case State.WarmingUp:
                    stateTimer += Time.unscaledDeltaTime;
                    UpdateDataContainerProgress();
                    if (stateTimer >= config.WarmupDuration)
                    {
                        stateTimer = 0;
                        state = State.Sampling;

                        if (benchmarkData != null)
                        {
                            benchmarkData.IsSampling = true;
                            benchmarkData.OnSamplingStarted?.Raise();
                        }

                        CSDebug.Log("[Benchmark] Warmup complete — sampling started.");
                    }
                    break;

                case State.Sampling:
                    CaptureFrame();
                    stateTimer += Time.unscaledDeltaTime;
                    UpdateDataContainerProgress();
                    BroadcastProgressIfDue();
                    if (stateTimer >= config.SampleDuration)
                    {
                        FinishRun(wasStopped: false);
                    }
                    break;
            }
        }

        void CaptureFrame()
        {
            using (s_benchmarkMarker.Auto())
            {
                float dt = Time.unscaledDeltaTime;
                float frameTimeMs = dt * 1000f;
                float fps = 1f / Mathf.Max(dt, 0.0001f);

                var snapshot = new FrameSnapshot
                {
                    frameIndex = frameCounter++,
                    deltaTimeMs = frameTimeMs,
                    fps = fps
                };

                runningFpsSum += fps;
                runningFrameTimeMs += frameTimeMs;

                if (cachedCaptureRendering)
                {
                    snapshot.drawCalls = GetRecorderValue(drawCallsRecorder);
                    snapshot.batches = GetRecorderValue(batchesRecorder);
                    snapshot.setPassCalls = GetRecorderValue(setPassRecorder);
                    snapshot.triangles = GetRecorderValue(trianglesRecorder);
                    snapshot.vertices = GetRecorderValue(verticesRecorder);
                }

                if (cachedCaptureMemory)
                {
                    snapshot.totalAllocatedMemory = Profiler.GetTotalAllocatedMemoryLong();
                    snapshot.totalReservedMemory = Profiler.GetTotalReservedMemoryLong();
                    // Use ProfilerRecorder for actual per-frame GC allocation instead of
                    // GetMonoUsedSizeLong() which returns cumulative heap usage.
                    snapshot.gcAllocatedPerFrame = GetRecorderValueLong(gcAllocRecorder);
                }

                if (cachedCapturePhysics)
                {
                    // Use ProfilerRecorder instead of FindObjectsByType<Rigidbody>() which
                    // scans the scene hierarchy and allocates a managed array every frame.
                    snapshot.activeRigidbodies = GetRecorderValue(activeBodiesRecorder);
                }

                snapshots.Add(snapshot);

                // Write to custom profiler counters — visible in Unity Profiler window
                s_counterFps.Value = fps;
                s_counterFrameTimeMs.Value = frameTimeMs;
                s_counterDrawCalls.Value = snapshot.drawCalls;
                s_counterFramesCaptured.Value = frameCounter;

                if (benchmarkData != null)
                    benchmarkData.FramesCaptured = frameCounter;
            }
        }

        void FinishRun(bool wasStopped)
        {
            state = State.Done;
            DisposeRecorders();

            currentReport.snapshots = snapshots;
            currentReport.ComputeStatistics();

            string filePath = currentReport.SaveToFile(config.OutputFolder);

            // Auto-index in history so every run is retrievable for comparison
            BenchmarkHistory.AddToHistory(currentReport, filePath, config.OutputFolder);

            // Update SOAP data container
            if (benchmarkData != null)
            {
                benchmarkData.IsRunning = false;
                benchmarkData.IsSampling = false;
                benchmarkData.Progress = 1f;
                benchmarkData.LastReportPath = filePath;

                var stateData = BuildStateData(filePath);

                if (wasStopped)
                    benchmarkData.OnBenchmarkStopped?.Raise(stateData);
                else
                    benchmarkData.OnBenchmarkCompleted?.Raise(stateData);
            }

            CSDebug.Log($"[Benchmark] {(wasStopped ? "Stopped early" : "Complete")} — {snapshots.Count} frames captured. Report saved to:\n{filePath}");
            LogSummary(currentReport.statistics);
        }

        // ── SOAP Progress Broadcasting ──────────────────

        void UpdateDataContainerProgress()
        {
            if (benchmarkData != null)
                benchmarkData.Progress = Progress;
        }

        void BroadcastProgressIfDue()
        {
            if (benchmarkData?.OnProgressUpdated == null) return;
            if (stateTimer < nextProgressUpdate) return;

            nextProgressUpdate = stateTimer + progressUpdateInterval;
            benchmarkData.OnProgressUpdated.Raise(BuildStateData(string.Empty));
        }

        BenchmarkStateData BuildStateData(string reportFilePath)
        {
            float avgFps = frameCounter > 0 ? runningFpsSum / frameCounter : 0;
            float avgFrameTime = frameCounter > 0 ? runningFrameTimeMs / frameCounter : 0;
            float p99 = currentReport?.statistics?.p99FrameTimeMs ?? 0;

            return new BenchmarkStateData(
                label: config.BenchmarkLabel,
                sceneName: currentReport?.sceneName ?? "",
                gitCommitHash: currentReport?.gitCommitHash ?? "",
                progress: Progress,
                framesCaptured: frameCounter,
                avgFps: avgFps,
                avgFrameTimeMs: avgFrameTime,
                p99FrameTimeMs: p99,
                reportFilePath: reportFilePath
            );
        }

        // ── Profiler Recorders ──────────────────────────

        void StartRecorders()
        {
            if (cachedCaptureRendering)
            {
                drawCallsRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
                batchesRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
                setPassRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
                trianglesRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
                verticesRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Vertices Count");
            }

            if (cachedCaptureMemory)
            {
                gcAllocRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            }

            if (cachedCapturePhysics)
            {
                activeBodiesRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Physics, "Active Dynamic Bodies");
            }
        }

        void DisposeRecorders()
        {
            drawCallsRecorder.Dispose();
            batchesRecorder.Dispose();
            setPassRecorder.Dispose();
            trianglesRecorder.Dispose();
            verticesRecorder.Dispose();
            gcAllocRecorder.Dispose();
            activeBodiesRecorder.Dispose();
        }

        static int GetRecorderValue(ProfilerRecorder recorder)
        {
            return recorder.Valid && recorder.Count > 0 ? (int)recorder.LastValue : 0;
        }

        static long GetRecorderValueLong(ProfilerRecorder recorder)
        {
            return recorder.Valid && recorder.Count > 0 ? recorder.LastValue : 0;
        }

        // ── Logging ─────────────────────────────────────

        static void LogSummary(BenchmarkStatistics s)
        {
            CSDebug.Log(
                $"[Benchmark Summary]\n" +
                $"  Frames: {s.totalFrames} over {s.durationSeconds:F1}s\n" +
                $"  FPS — avg: {s.avgFps:F1}, min: {s.minFps:F1}, p1: {s.p1Fps:F1}, p5: {s.p5Fps:F1}\n" +
                $"  Frame Time — avg: {s.avgFrameTimeMs:F2}ms, p95: {s.p95FrameTimeMs:F2}ms, p99: {s.p99FrameTimeMs:F2}ms, max: {s.maxFrameTimeMs:F2}ms\n" +
                $"  Draw Calls: {s.avgDrawCalls:F0}, Batches: {s.avgBatches:F0}, Tris: {s.avgTriangles:F0}\n" +
                $"  Memory Peak: {s.peakAllocatedMemory / (1024f * 1024f):F1} MB, GC Total: {s.totalGcAllocated / (1024f * 1024f):F1} MB");
        }
    }
}
