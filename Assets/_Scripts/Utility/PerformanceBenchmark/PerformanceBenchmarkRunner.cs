using System.Collections.Generic;
using CosmicShore.Utility;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;

// GameDataSO lives in CosmicShore.Utility (DataContainers); referenced here for
// optional vessel/player load counts.

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

        [Header("Game Load (optional)")]
        [Tooltip("Optional GameDataSO — when assigned, vessel/player counts are recorded. " +
                 "Prism and VFX counts are read from their manager singletons regardless.")]
        [SerializeField] private GameDataSO gameData;

        [Header("Hint Rules (optional)")]
        [Tooltip("Customizable rule set for the actionable hint engine. Falls back to built-in defaults when null.")]
        [SerializeField] private BenchmarkHintRulesSO hintRules;

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
        bool cachedCaptureGameLoad;

        // Profiler recorders for rendering stats
        ProfilerRecorder drawCallsRecorder;
        ProfilerRecorder batchesRecorder;
        ProfilerRecorder setPassRecorder;
        ProfilerRecorder trianglesRecorder;
        ProfilerRecorder verticesRecorder;

        // Profiler recorders for memory and physics — zero-allocation alternatives
        ProfilerRecorder gcAllocRecorder;
        ProfilerRecorder activeBodiesRecorder;

        // CPU/GPU split via FrameTimingManager (reused single-element buffer).
        readonly FrameTiming[] _frameTimings = new FrameTiming[1];

        // Spike capture
        List<SpikeEntry> spikes;
        readonly List<MarkerSample> _markerScratch = new(8);
        float runningSampleSum;   // sum of sampled frame times → running mean for the spike threshold
        int runningSampleCount;
        const float SpikeMultiplier = 1.75f;      // a frame is a spike when > multiplier × running mean…
        const float SpikeFloorMs = 1000f / 45f;   // …and above this absolute floor (~22 ms)
        const int MaxSpikes = 48;
        const int TopMarkersPerSpike = 5;

        public bool IsRunning => state == State.WarmingUp || state == State.Sampling;

        /// <summary>When true (default) the run auto-saves + indexes on finish. The Collect tab sets this false and saves explicitly.</summary>
        public bool AutoSave { get; set; } = true;

        /// <summary>Frames captured so far this run (live, for editor feedback without a Data Container).</summary>
        public int FramesCaptured => frameCounter;

        public bool IsWarmingUp => state == State.WarmingUp;
        public bool IsSampling => state == State.Sampling;

        /// <summary>Path of the most recently saved report (set when a run is saved). Empty until then.</summary>
        public string LastReportPath { get; private set; } = string.Empty;

        /// <summary>The most recently completed report. Null until a run finishes.</summary>
        public BenchmarkReport LastReport { get; private set; }

        /// <summary>
        /// Assigns config / data / game-data at runtime. Used by tooling (sweep runner,
        /// editor window) that spawns the runner programmatically, avoiding editor-only
        /// SerializedObject wiring.
        /// </summary>
        public void Configure(BenchmarkConfigSO benchmarkConfig, BenchmarkDataSO data = null,
            GameDataSO gameDataContainer = null, BenchmarkHintRulesSO rules = null)
        {
            config = benchmarkConfig;
            benchmarkData = data;
            if (gameDataContainer != null) gameData = gameDataContainer;
            if (rules != null) hintRules = rules;
        }

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
            cachedCaptureGameLoad = config.CaptureGameLoadStats;

            int estimatedFrames = Mathf.CeilToInt(config.SampleDuration * 120);
            snapshots = new List<FrameSnapshot>(estimatedFrames);
            spikes = new List<SpikeEntry>();
            frameCounter = 0;
            runningFpsSum = 0;
            runningFrameTimeMs = 0;
            runningSampleSum = 0;
            runningSampleCount = 0;

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

                // CPU/GPU split. Editor side enables FrameTimingManager; if it's off this
                // returns 0 frames and the fields stay 0 (harmless).
                FrameTimingManager.CaptureFrameTimings();
                if (FrameTimingManager.GetLatestTimings(1, _frameTimings) > 0)
                {
                    snapshot.cpuFrameTimeMs = (float)_frameTimings[0].cpuFrameTime;
                    snapshot.gpuFrameTimeMs = (float)_frameTimings[0].gpuFrameTime;
                }

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

                if (cachedCaptureGameLoad)
                {
                    var load = GameLoadSampler.Sample(gameData);
                    snapshot.activePrisms = load.activePrisms;
                    snapshot.activeExplosions = load.activeExplosions;
                    snapshot.activeImplosions = load.activeImplosions;
                    snapshot.activeVessels = load.activeVessels;
                    snapshot.activePlayers = load.activePlayers;
                }

                snapshots.Add(snapshot);
                DetectSpike(snapshot, frameTimeMs);

                // Write to custom profiler counters — visible in Unity Profiler window
                s_counterFps.Value = fps;
                s_counterFrameTimeMs.Value = frameTimeMs;
                s_counterDrawCalls.Value = snapshot.drawCalls;
                s_counterFramesCaptured.Value = frameCounter;

                if (benchmarkData != null)
                    benchmarkData.FramesCaptured = frameCounter;
            }
        }

        // When a frame's time exceeds the running threshold, record it as a spike and (in the
        // editor) attribute the most expensive markers from the profiler's matching frame.
        void DetectSpike(in FrameSnapshot snapshot, float frameTimeMs)
        {
            runningSampleSum += frameTimeMs;
            runningSampleCount++;
            if (spikes.Count >= MaxSpikes) return;

            float mean = runningSampleCount > 0 ? runningSampleSum / runningSampleCount : 0f;
            float threshold = Mathf.Max(SpikeFloorMs, SpikeMultiplier * mean);
            if (frameTimeMs < threshold) return;

            var spike = new SpikeEntry
            {
                frameIndex = snapshot.frameIndex,
                frameTimeMs = frameTimeMs,
                cpuFrameTimeMs = snapshot.cpuFrameTimeMs,
                gpuFrameTimeMs = snapshot.gpuFrameTimeMs
            };

#if UNITY_EDITOR
            if (SpikeAnalyzer.TryGetTopMarkers(SpikeAnalyzer.LastFrameIndex, TopMarkersPerSpike, _markerScratch))
            {
                for (int i = 0; i < _markerScratch.Count; i++)
                    spike.topMarkers.Add(_markerScratch[i]);
            }
#endif
            spikes.Add(spike);
        }

        void FinishRun(bool wasStopped)
        {
            state = State.Done;
            DisposeRecorders();

            currentReport.snapshots = snapshots;
            currentReport.spikes = spikes ?? new List<SpikeEntry>();
            currentReport.ComputeStatistics();
            currentReport.analysis = BenchmarkAnalysis.Analyze(
                currentReport, hintRules != null ? hintRules.Resolve() : null);

            LastReport = currentReport;

            // Collect mode saves explicitly (AutoSave=false); Sweep and direct runs auto-save.
            string filePath = string.Empty;
            if (AutoSave)
            {
                filePath = currentReport.SaveToFile(config.OutputFolder);
                LastReportPath = filePath;
                BenchmarkHistory.AddToHistory(currentReport, filePath, config.OutputFolder);
            }

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

            string savedNote = AutoSave ? $"Report saved to:\n{filePath}" : "Held for explicit Save.";
            CSDebug.Log($"[Benchmark] {(wasStopped ? "Stopped early" : "Complete")} — {snapshots.Count} frames captured. {savedNote}");
            LogSummary(currentReport.statistics);
        }

        /// <summary>
        /// Explicitly saves the most recently completed report to disk and indexes it in History.
        /// Used by the Collect tab's Save button (which runs with AutoSave=false). No-op if there's
        /// no report or it was already saved. Returns the saved path (or existing one).
        /// </summary>
        public string SaveLastReport()
        {
            if (LastReport == null || config == null) return LastReportPath;
            if (!string.IsNullOrEmpty(LastReportPath)) return LastReportPath; // already saved

            string filePath = LastReport.SaveToFile(config.OutputFolder);
            LastReportPath = filePath;
            BenchmarkHistory.AddToHistory(LastReport, filePath, config.OutputFolder);
            return filePath;
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
                $"  Memory Peak: {s.peakAllocatedMemory / (1024f * 1024f):F1} MB, GC Total: {s.totalGcAllocated / (1024f * 1024f):F1} MB\n" +
                $"  Load — prisms avg: {s.avgActivePrisms:F0} (peak {s.peakActivePrisms}), " +
                $"explosions peak: {s.peakActiveExplosions}, implosions peak: {s.peakActiveImplosions}, " +
                $"vessels avg: {s.avgActiveVessels:F1}");
        }
    }
}
