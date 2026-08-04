using System;
using System.Collections;
using System.Collections.Generic;
using CosmicShore.Utility;
using Unity.Netcode;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;

// GameDataSO lives in CosmicShore.Utility (DataContainers); referenced here for
// optional vessel/player load counts.

namespace CosmicShore.Utility.PerformanceBenchmark
{
    /// <summary>
    /// Runtime component that captures per-frame performance data for a configured duration and
    /// produces a <see cref="BenchmarkReport"/>.
    ///
    /// Sampling runs at end-of-frame (after render submission) so GPU/render-thread numbers for
    /// the frame are settled. The steady-state capture path is allocation-free: a pre-allocated
    /// flat <see cref="FrameSnapshot"/> buffer is filled in place, and all string formatting,
    /// scoring, hint evaluation and report assembly happen only when the capture ends. The
    /// collector measures its own per-frame allocation and logs it as a self-check.
    ///
    /// Lifecycle state is also mirrored into an optional <see cref="BenchmarkDataSO"/> for SOAP
    /// listeners, but the tool no longer depends on one - consumers can poll this component.
    /// </summary>
    public class PerformanceBenchmarkRunner : MonoBehaviour
    {
        static readonly ProfilerMarker s_benchmarkMarker = new("CosmicShore.BenchmarkCapture");

        // ── Custom Profiler Counters ─────────────────────
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

        [Header("SOAP Data Container (optional)")]
        [SerializeField] private BenchmarkDataSO benchmarkData;

        [Header("Game Load (optional)")]
        [SerializeField] private GameDataSO gameData;

        [Header("Hint Rules (optional)")]
        [SerializeField] private BenchmarkHintRulesSO hintRules;

        [Header("Automation")]
        [SerializeField] private bool autoStartOnEnable;

        enum State { Idle, WarmingUp, Sampling, Done }

        [SerializeField, HideInInspector] private State state = State.Idle;

        // Frame buffer sized generously and filled in place - no per-frame growth/alloc.
        const int MaxAssumedFps = 360;
        FrameSnapshot[] _frameBuffer;
        int _frameCount;
        bool _bufferFullWarned;

        float stateTimer;
        float progressUpdateInterval = 0.5f;
        float nextProgressUpdate;
        BenchmarkReport currentReport;
        Coroutine _loop;
        WaitForEndOfFrame _endOfFrame;

        // Running sums for live progress + spike threshold.
        float runningFpsSum;
        float runningFrameTimeMs;
        float runningSampleSum;
        int runningSampleCount;

        // Collector self-check: bytes the collector itself allocates per sampled frame.
        long _collectorAllocBytes;

        bool cachedCaptureRendering;
        bool cachedCaptureMemory;
        bool cachedCapturePhysics;
        bool cachedCaptureGameLoad;
        bool cachedCaptureNetcode;

        ProfilerRecorder drawCallsRecorder;
        ProfilerRecorder batchesRecorder;
        ProfilerRecorder setPassRecorder;
        ProfilerRecorder trianglesRecorder;
        ProfilerRecorder verticesRecorder;
        ProfilerRecorder gcAllocRecorder;
        ProfilerRecorder activeBodiesRecorder;

        // Netcode (NGO) marker + counter recorders
        ProfilerRecorder netTickRecorder, netSerializeRecorder, netDeserializeRecorder,
            netSpawnRecorder, netRpcRecorder, rpcsSentRecorder, netVarsDirtyRecorder, netBytesRecorder;

        // CPU/GPU split via FrameTimingManager (reused single-element buffer).
        readonly FrameTiming[] _frameTimings = new FrameTiming[1];

        // Spike capture (only allocates on actual spikes, which are bounded).
        List<SpikeEntry> spikes;
        const float SpikeMultiplier = 1.75f;
        const float SpikeFloorMs = 1000f / 45f;
        const int MaxSpikes = 256;

        // Free-form ("record until stopped") capture, used by the Runtime Capture tab.
        bool _freeForm;
        const int MaxFreeFormSeconds = 300;

        public bool IsRunning => state == State.WarmingUp || state == State.Sampling;

        /// <summary>True while a free-form (record-until-stopped) capture is active.</summary>
        public bool IsFreeForm => _freeForm;

        /// <summary>Live spike list for the Runtime Capture tab (read-only; updated as spikes occur).</summary>
        public IReadOnlyList<SpikeEntry> Spikes => spikes;

        /// <summary>When true (default) the run auto-saves + indexes on finish. The Collect tab sets this false and saves explicitly.</summary>
        public bool AutoSave { get; set; } = true;

        /// <summary>Frames captured so far this run (live).</summary>
        public int FramesCaptured => _frameCount;

        public bool IsWarmingUp => state == State.WarmingUp;
        public bool IsSampling => state == State.Sampling;

        /// <summary>Path of the most recently saved report (set when a run is saved). Empty until then.</summary>
        public string LastReportPath { get; private set; } = string.Empty;

        /// <summary>The most recently completed report. Null until a run finishes.</summary>
        public BenchmarkReport LastReport { get; private set; }

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
            if (_loop != null) { StopCoroutine(_loop); _loop = null; }
            DisposeRecorders();
        }

        public void StartBenchmark() => StartBenchmark(false);

        /// <summary>
        /// Starts a capture. When <paramref name="freeForm"/> is true the run skips warmup and
        /// samples until <see cref="StopBenchmark"/> is called (the Runtime Capture tab) instead
        /// of stopping after <see cref="BenchmarkConfigSO.SampleDuration"/>.
        /// </summary>
        public void StartBenchmark(bool freeForm)
        {
            if (config == null)
            {
                CSDebug.LogError("[Benchmark] No BenchmarkConfigSO assigned.");
                return;
            }
            if (IsRunning)
            {
                CSDebug.LogWarning("[Benchmark] Already running - ignoring StartBenchmark call.");
                return;
            }

            _freeForm = freeForm;

            cachedCaptureRendering = config.CaptureRenderingStats;
            cachedCaptureMemory = config.CaptureMemoryStats;
            cachedCapturePhysics = config.CapturePhysicsStats;
            cachedCaptureGameLoad = config.CaptureGameLoadStats;
            cachedCaptureNetcode = config.CaptureNetcodeStats;

            // Pre-size the flat frame buffer once (no per-frame allocation thereafter).
            float budgetSeconds = freeForm ? MaxFreeFormSeconds : config.SampleDuration;
            int capacity = Mathf.Max(64, Mathf.CeilToInt(budgetSeconds * MaxAssumedFps));
            if (_frameBuffer == null || _frameBuffer.Length < capacity)
                _frameBuffer = new FrameSnapshot[capacity];
            _frameCount = 0;
            _bufferFullWarned = false;

            spikes = new List<SpikeEntry>();
            runningFpsSum = 0;
            runningFrameTimeMs = 0;
            runningSampleSum = 0;
            runningSampleCount = 0;
            _collectorAllocBytes = 0;

            currentReport = new BenchmarkReport
            {
                label = config.BenchmarkLabel,
                warmupDuration = config.WarmupDuration,
                sampleDuration = config.SampleDuration
            };
            currentReport.PopulateEnvironment();
            currentReport.networkTickRate = ResolveNetworkTickRate();

            StartRecorders();

            stateTimer = 0;
            nextProgressUpdate = 0;
            // Free-form skips warmup and samples immediately (you're already playing).
            state = _freeForm ? State.Sampling : State.WarmingUp;

            if (benchmarkData != null)
            {
                benchmarkData.IsRunning = true;
                benchmarkData.IsSampling = _freeForm;
                benchmarkData.Progress = 0f;
                benchmarkData.FramesCaptured = 0;
                benchmarkData.ActiveLabel = config.BenchmarkLabel;
                benchmarkData.LastReportPath = string.Empty;
                benchmarkData.OnBenchmarkStarted?.Raise();
                if (_freeForm) benchmarkData.OnSamplingStarted?.Raise();
            }

            _endOfFrame ??= new WaitForEndOfFrame();
            _loop = StartCoroutine(RunLoop());

            CSDebug.Log(_freeForm
                ? "[Benchmark] Free-form capture started - recording until stopped."
                : $"[Benchmark] Started - warming up for {config.WarmupDuration}s, then sampling for {config.SampleDuration}s.");
        }

        public void StopBenchmark()
        {
            if (!IsRunning) return;
            if (_loop != null) { StopCoroutine(_loop); _loop = null; }
            FinishRun(wasStopped: true);
        }

        // End-of-frame driven so render/GPU timings for the frame are settled before we read them.
        IEnumerator RunLoop()
        {
            while (state == State.WarmingUp)
            {
                yield return _endOfFrame;
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
                    CSDebug.Log("[Benchmark] Warmup complete - sampling started.");
                }
            }

            while (state == State.Sampling)
            {
                yield return _endOfFrame;
                CaptureFrame();
                stateTimer += Time.unscaledDeltaTime;
                UpdateDataContainerProgress();
                BroadcastProgressIfDue();
                // Free-form runs until StopBenchmark(); fixed runs end after SampleDuration.
                if (!_freeForm && stateTimer >= config.SampleDuration)
                {
                    _loop = null;
                    FinishRun(wasStopped: false);
                    yield break;
                }
                // Safety: free-form can't outgrow its buffer - auto-finish if it fills.
                if (_freeForm && _frameCount >= _frameBuffer.Length)
                {
                    _loop = null;
                    FinishRun(wasStopped: true);
                    yield break;
                }
            }
        }

        void CaptureFrame()
        {
            using (s_benchmarkMarker.Auto())
            {
                // Measure the collector's own per-frame allocation (main-thread) so we can prove
                // the steady-state path is zero-alloc.
                long allocBefore = GC.GetAllocatedBytesForCurrentThread();

                float dt = Time.unscaledDeltaTime;
                float frameTimeMs = dt * 1000f;
                float fps = 1f / Mathf.Max(dt, 0.0001f);

                if (_frameCount >= _frameBuffer.Length)
                {
                    if (!_bufferFullWarned)
                    {
                        CSDebug.LogWarning("[Benchmark] Frame buffer full - extra frames dropped (raise sample budget if this recurs).");
                        _bufferFullWarned = true;
                    }
                    return;
                }

                // Fill the snapshot in place (struct in a pre-allocated array - no heap alloc).
                ref FrameSnapshot snapshot = ref _frameBuffer[_frameCount];
                snapshot = default;
                snapshot.frameIndex = _frameCount;
                snapshot.deltaTimeMs = frameTimeMs;
                snapshot.fps = fps;

                runningFpsSum += fps;
                runningFrameTimeMs += frameTimeMs;

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
                    snapshot.gcAllocatedPerFrame = GetRecorderValueLong(gcAllocRecorder);
                }

                if (cachedCapturePhysics)
                    snapshot.activeRigidbodies = GetRecorderValue(activeBodiesRecorder);

                if (cachedCaptureNetcode)
                {
                    // Marker recorders report time in nanoseconds; sum and convert to ms.
                    long netNs = GetRecorderValueLong(netTickRecorder)
                               + GetRecorderValueLong(netSerializeRecorder)
                               + GetRecorderValueLong(netDeserializeRecorder)
                               + GetRecorderValueLong(netSpawnRecorder)
                               + GetRecorderValueLong(netRpcRecorder);
                    snapshot.netcodeTimeMs = netNs / 1_000_000f;
                    snapshot.rpcsSent = GetRecorderValue(rpcsSentRecorder);
                    snapshot.netVarsDirty = GetRecorderValue(netVarsDirtyRecorder);
                    snapshot.netBytesSent = GetRecorderValueLong(netBytesRecorder);
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

                int idx = _frameCount;
                _frameCount++;
                DetectSpike(idx, frameTimeMs, snapshot.cpuFrameTimeMs, snapshot.gpuFrameTimeMs);

                s_counterFps.Value = fps;
                s_counterFrameTimeMs.Value = frameTimeMs;
                s_counterDrawCalls.Value = snapshot.drawCalls;
                s_counterFramesCaptured.Value = _frameCount;

                if (benchmarkData != null)
                    benchmarkData.FramesCaptured = _frameCount;

                _collectorAllocBytes += GC.GetAllocatedBytesForCurrentThread() - allocBefore;
            }
        }

        // Spike detection records the frame and (in-editor) attributes the costliest markers.
        void DetectSpike(int frameIndex, float frameTimeMs, float cpuMs, float gpuMs)
        {
            runningSampleSum += frameTimeMs;
            runningSampleCount++;
            if (spikes.Count >= MaxSpikes) return;

            float mean = runningSampleCount > 0 ? runningSampleSum / runningSampleCount : 0f;
            float threshold = Mathf.Max(SpikeFloorMs, SpikeMultiplier * mean);
            if (frameTimeMs < threshold) return;

            var spike = new SpikeEntry
            {
                frameIndex = frameIndex,
                frameTimeMs = frameTimeMs,
                cpuFrameTimeMs = cpuMs,
                gpuFrameTimeMs = gpuMs
            };

#if UNITY_EDITOR
            // Record WHICH profiler frame to analyze, but don't walk it here. The walk costs
            // 70-90ms and, run inline on every spike, cascaded into a spike storm of the tool's
            // own making. The editor window enriches topMarkers off the game thread instead.
            spike.profilerFrameIndex = SpikeAnalyzer.LastFrameIndex;
#endif
            spikes.Add(spike);
        }

        void FinishRun(bool wasStopped)
        {
            if (state == State.Done) return;
            state = State.Done;
            DisposeRecorders();

            // One-time report assembly (end of capture only - never per frame).
            var list = new List<FrameSnapshot>(_frameCount);
            for (int i = 0; i < _frameCount; i++) list.Add(_frameBuffer[i]);
            currentReport.snapshots = list;
            currentReport.spikes = spikes ?? new List<SpikeEntry>();
            currentReport.ComputeStatistics();
            if (currentReport.statistics != null)
                currentReport.statistics.collectorAllocBytesPerFrame =
                    _frameCount > 0 ? (float)_collectorAllocBytes / _frameCount : 0f;
            currentReport.analysis = BenchmarkAnalysis.Analyze(
                currentReport, hintRules != null ? hintRules.Resolve() : null);

            LastReport = currentReport;

            string filePath = string.Empty;
            if (AutoSave)
            {
                filePath = currentReport.SaveToFile(config.OutputFolder);
                LastReportPath = filePath;
                BenchmarkHistory.AddToHistory(currentReport, filePath, config.OutputFolder);
            }

            if (benchmarkData != null)
            {
                benchmarkData.IsRunning = false;
                benchmarkData.IsSampling = false;
                benchmarkData.Progress = 1f;
                benchmarkData.LastReportPath = filePath;

                var stateData = BuildStateData(filePath);
                if (wasStopped) benchmarkData.OnBenchmarkStopped?.Raise(stateData);
                else benchmarkData.OnBenchmarkCompleted?.Raise(stateData);
            }

            string savedNote = AutoSave ? $"Report saved to:\n{filePath}" : "Held for explicit Save.";
            CSDebug.Log($"[Benchmark] {(wasStopped ? "Stopped early" : "Complete")} - {_frameCount} frames captured. {savedNote}");
            CSDebug.Log($"[Benchmark] Collector steady-state alloc: {currentReport.statistics?.collectorAllocBytesPerFrame ?? 0:F1} B/frame (target ~0).");
            LogSummary(currentReport.statistics);
        }

        public string SaveLastReport()
        {
            if (LastReport == null || config == null) return LastReportPath;
            if (!string.IsNullOrEmpty(LastReportPath)) return LastReportPath;

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
            float avgFps = _frameCount > 0 ? runningFpsSum / _frameCount : 0;
            float avgFrameTime = _frameCount > 0 ? runningFrameTimeMs / _frameCount : 0;
            float p99 = currentReport?.statistics?.p99FrameTimeMs ?? 0;

            return new BenchmarkStateData(
                label: config.BenchmarkLabel,
                sceneName: currentReport?.sceneName ?? "",
                gitCommitHash: currentReport?.gitCommitHash ?? "",
                progress: Progress,
                framesCaptured: _frameCount,
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
                gcAllocRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            if (cachedCapturePhysics)
                activeBodiesRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Physics, "Active Dynamic Bodies");
            if (cachedCaptureNetcode)
            {
                netTickRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Network, "CSM.Net.Tick");
                netSerializeRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Network, "CSM.Net.Serialize");
                netDeserializeRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Network, "CSM.Net.Deserialize");
                netSpawnRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Network, "CSM.Net.SpawnDespawn");
                netRpcRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Network, "CSM.Net.RpcDispatch");
                rpcsSentRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Network, "CSM RPCs Sent");
                netVarsDirtyRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Network, "CSM NetVars Dirty");
                netBytesRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Network, "CSM Bytes Sent");
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
            netTickRecorder.Dispose();
            netSerializeRecorder.Dispose();
            netDeserializeRecorder.Dispose();
            netSpawnRecorder.Dispose();
            netRpcRecorder.Dispose();
            rpcsSentRecorder.Dispose();
            netVarsDirtyRecorder.Dispose();
            netBytesRecorder.Dispose();
        }

        static int ResolveNetworkTickRate()
        {
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsListening && nm.NetworkTickSystem != null)
                return (int)nm.NetworkTickSystem.TickRate;
            return 0;
        }

        static int GetRecorderValue(ProfilerRecorder recorder) =>
            recorder.Valid && recorder.Count > 0 ? (int)recorder.LastValue : 0;

        static long GetRecorderValueLong(ProfilerRecorder recorder) =>
            recorder.Valid && recorder.Count > 0 ? recorder.LastValue : 0;

        // ── Logging ─────────────────────────────────────

        static void LogSummary(BenchmarkStatistics s)
        {
            if (s == null) return;
            CSDebug.Log(
                $"[Benchmark Summary]\n" +
                $"  Frames: {s.totalFrames} over {s.durationSeconds:F1}s\n" +
                $"  FPS - avg: {s.avgFps:F1}, min: {s.minFps:F1}, p1: {s.p1Fps:F1}, p5: {s.p5Fps:F1}\n" +
                $"  Frame Time - avg: {s.avgFrameTimeMs:F2}ms, p95: {s.p95FrameTimeMs:F2}ms, p99: {s.p99FrameTimeMs:F2}ms, max: {s.maxFrameTimeMs:F2}ms\n" +
                $"  Draw Calls: {s.avgDrawCalls:F0}, Batches: {s.avgBatches:F0}, Tris: {s.avgTriangles:F0}\n" +
                $"  Memory Peak: {s.peakAllocatedMemory / (1024f * 1024f):F1} MB, GC Total: {s.totalGcAllocated / (1024f * 1024f):F1} MB");
        }
    }
}
