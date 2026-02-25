using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;

namespace CosmicShore.Utility.PerformanceBenchmark
{
    /// <summary>
    /// Runtime component that captures per-frame performance data for a configured
    /// duration and produces a <see cref="BenchmarkReport"/>.
    ///
    /// Usage:
    ///   1. Attach to a GameObject in the scene you want to benchmark.
    ///   2. Assign a <see cref="BenchmarkConfigSO"/>.
    ///   3. Call <see cref="StartBenchmark"/> (or check autoStartOnEnable).
    ///   4. When finished, <see cref="OnBenchmarkComplete"/> fires with the saved report path.
    /// </summary>
    public class PerformanceBenchmarkRunner : MonoBehaviour
    {
        static readonly ProfilerMarker s_benchmarkMarker = new("CosmicShore.BenchmarkCapture");

        [Header("Configuration")]
        [SerializeField] private BenchmarkConfigSO config;

        [Header("Automation")]
        [Tooltip("Automatically start the benchmark when this component is enabled.")]
        [SerializeField] private bool autoStartOnEnable;

        /// <summary>Fires when a benchmark run finishes. Payload is the saved JSON file path.</summary>
        public event Action<string> OnBenchmarkComplete;

        enum State { Idle, WarmingUp, Sampling, Done }

        [SerializeField, HideInInspector] private State state = State.Idle;

        float stateTimer;
        int frameCounter;
        List<FrameSnapshot> snapshots;
        BenchmarkReport currentReport;

        // Profiler recorders for rendering stats (Unity 2020.2+)
        ProfilerRecorder drawCallsRecorder;
        ProfilerRecorder batchesRecorder;
        ProfilerRecorder setPassRecorder;
        ProfilerRecorder trianglesRecorder;
        ProfilerRecorder verticesRecorder;

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

            int estimatedFrames = Mathf.CeilToInt(config.SampleDuration * 120); // conservative estimate
            snapshots = new List<FrameSnapshot>(estimatedFrames);
            frameCounter = 0;

            currentReport = new BenchmarkReport
            {
                label = config.BenchmarkLabel,
                warmupDuration = config.WarmupDuration,
                sampleDuration = config.SampleDuration
            };
            currentReport.PopulateEnvironment();

            StartRecorders();

            stateTimer = 0;
            state = State.WarmingUp;

            CSDebug.Log($"[Benchmark] Started — warming up for {config.WarmupDuration}s, then sampling for {config.SampleDuration}s.");
        }

        public void StopBenchmark()
        {
            if (!IsRunning) return;
            FinishRun();
        }

        void Update()
        {
            switch (state)
            {
                case State.WarmingUp:
                    stateTimer += Time.unscaledDeltaTime;
                    if (stateTimer >= config.WarmupDuration)
                    {
                        stateTimer = 0;
                        state = State.Sampling;
                        CSDebug.Log("[Benchmark] Warmup complete — sampling started.");
                    }
                    break;

                case State.Sampling:
                    CaptureFrame();
                    stateTimer += Time.unscaledDeltaTime;
                    if (stateTimer >= config.SampleDuration)
                    {
                        FinishRun();
                    }
                    break;
            }
        }

        void CaptureFrame()
        {
            using (s_benchmarkMarker.Auto())
            {
                var snapshot = new FrameSnapshot
                {
                    frameIndex = frameCounter++,
                    deltaTimeMs = Time.unscaledDeltaTime * 1000f,
                    fps = 1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f)
                };

                if (config.CaptureRenderingStats)
                {
                    snapshot.drawCalls = GetRecorderValue(drawCallsRecorder);
                    snapshot.batches = GetRecorderValue(batchesRecorder);
                    snapshot.setPassCalls = GetRecorderValue(setPassRecorder);
                    snapshot.triangles = GetRecorderValue(trianglesRecorder);
                    snapshot.vertices = GetRecorderValue(verticesRecorder);
                }

                if (config.CaptureMemoryStats)
                {
                    snapshot.totalAllocatedMemory = Profiler.GetTotalAllocatedMemoryLong();
                    snapshot.totalReservedMemory = Profiler.GetTotalReservedMemoryLong();
                    snapshot.gcAllocatedPerFrame = Profiler.GetMonoUsedSizeLong();
                }

                if (config.CapturePhysicsStats)
                {
                    // Physics stats from the profiler — available per frame
                    snapshot.activeRigidbodies = Physics.simulationMode != SimulationMode.Script
                        ? FindObjectsByType<Rigidbody>(FindObjectsSortMode.None).Length
                        : 0;
                }

                snapshots.Add(snapshot);
            }
        }

        void FinishRun()
        {
            state = State.Done;
            DisposeRecorders();

            currentReport.snapshots = snapshots;
            currentReport.ComputeStatistics();

            string filePath = currentReport.SaveToFile(config.OutputFolder);

            CSDebug.Log($"[Benchmark] Complete — {snapshots.Count} frames captured. Report saved to:\n{filePath}");
            LogSummary(currentReport.statistics);

            OnBenchmarkComplete?.Invoke(filePath);
        }

        // ── Profiler Recorders ──────────────────────────

        void StartRecorders()
        {
            if (!config.CaptureRenderingStats) return;

            drawCallsRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            batchesRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            setPassRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            trianglesRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            verticesRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Vertices Count");
        }

        void DisposeRecorders()
        {
            drawCallsRecorder.Dispose();
            batchesRecorder.Dispose();
            setPassRecorder.Dispose();
            trianglesRecorder.Dispose();
            verticesRecorder.Dispose();
        }

        static int GetRecorderValue(ProfilerRecorder recorder)
        {
            return recorder.Valid && recorder.Count > 0 ? (int)recorder.LastValue : 0;
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
