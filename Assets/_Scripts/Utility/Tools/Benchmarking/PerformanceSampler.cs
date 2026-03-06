using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CosmicShore.Utility.Tools.Benchmarking
{
    /// <summary>
    /// Attach to any GameObject (or auto-created via BenchmarkWindow) to capture
    /// per-frame performance metrics during play mode. When sampling completes,
    /// produces a <see cref="BenchmarkReport"/>.
    /// </summary>
    public class PerformanceSampler : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField, Tooltip("Seconds to wait before sampling begins (lets the scene stabilise).")]
        float _warmupSeconds = 2f;

        [SerializeField, Tooltip("How many seconds to sample after warmup. 0 = manual stop.")]
        float _sampleDurationSeconds = 10f;

        [SerializeField, Tooltip("Label for the generated report.")]
        string _label = "Benchmark";

        // ── State ───────────────────────────────────────────────────────────
        bool _isWarming;
        bool _isSampling;
        float _warmupTimer;
        float _sampleTimer;
        int _gcCountAtStart;

        // ── Collected data ──────────────────────────────────────────────────
        readonly List<float> _frameTimesMs = new();
        readonly List<float> _gpuTimesMs = new();
        readonly List<long> _drawCalls = new();
        readonly List<long> _triangles = new();
        readonly List<long> _vertices = new();
        readonly List<long> _setPassCalls = new();
        long _gcAllocAccum;
        long _peakUsedMemory;

        // ── Profiler recorders ──────────────────────────────────────────────
        ProfilerRecorder _gcAllocRecorder;
        ProfilerRecorder _drawCallRecorder;
        ProfilerRecorder _triangleRecorder;
        ProfilerRecorder _vertexRecorder;
        ProfilerRecorder _setPassRecorder;

        // ── FrameTiming ─────────────────────────────────────────────────────
        FrameTiming[] _frameTimings = new FrameTiming[1];

        // ── Events ──────────────────────────────────────────────────────────
        public event Action<BenchmarkReport> OnSamplingComplete;

        // ── Public API ──────────────────────────────────────────────────────

        public bool IsSampling => _isSampling;
        public bool IsWarming => _isWarming;
        public float ElapsedSampleTime => _sampleTimer;
        public float WarmupRemaining => Mathf.Max(0f, _warmupSeconds - _warmupTimer);
        public int FramesCaptured => _frameTimesMs.Count;

        public void Configure(string label, float warmup, float duration)
        {
            _label = label;
            _warmupSeconds = warmup;
            _sampleDurationSeconds = duration;
        }

        public void StartSampling()
        {
            if (_isSampling || _isWarming) return;

            _frameTimesMs.Clear();
            _gpuTimesMs.Clear();
            _drawCalls.Clear();
            _triangles.Clear();
            _vertices.Clear();
            _setPassCalls.Clear();
            _gcAllocAccum = 0;
            _peakUsedMemory = 0;
            _warmupTimer = 0f;
            _sampleTimer = 0f;

            StartRecorders();

            if (_warmupSeconds > 0f)
                _isWarming = true;
            else
                BeginCapture();
        }

        public BenchmarkReport StopSampling()
        {
            if (!_isSampling && !_isWarming) return null;

            _isWarming = false;
            _isSampling = false;
            StopRecorders();

            return BuildReport();
        }

        // ── MonoBehaviour ───────────────────────────────────────────────────

        void OnEnable()
        {
            FrameTimingManager.CaptureFrameTimings();
        }

        void OnDisable()
        {
            StopRecorders();
        }

        void Update()
        {
            if (_isWarming)
            {
                _warmupTimer += Time.unscaledDeltaTime;
                if (_warmupTimer >= _warmupSeconds)
                {
                    _isWarming = false;
                    BeginCapture();
                }
                return;
            }

            if (!_isSampling) return;

            CaptureFrame();

            _sampleTimer += Time.unscaledDeltaTime;
            if (_sampleDurationSeconds > 0f && _sampleTimer >= _sampleDurationSeconds)
            {
                var report = StopSampling();
                OnSamplingComplete?.Invoke(report);
            }
        }

        // ── Internals ───────────────────────────────────────────────────────

        void BeginCapture()
        {
            _isSampling = true;
            _gcCountAtStart = GC.CollectionCount(0);
        }

        void CaptureFrame()
        {
            // Frame time from Time.unscaledDeltaTime (most reliable cross-platform)
            _frameTimesMs.Add(Time.unscaledDeltaTime * 1000f);

            // GPU time via FrameTimingManager (may not be available on all platforms)
            FrameTimingManager.CaptureFrameTimings();
            uint count = FrameTimingManager.GetLatestTimings(1, _frameTimings);
            if (count > 0 && _frameTimings[0].gpuFrameTime > 0)
                _gpuTimesMs.Add((float)_frameTimings[0].gpuFrameTime);

            // GC allocation tracking
            if (_gcAllocRecorder.Valid)
                _gcAllocAccum += _gcAllocRecorder.LastValue;

            // Render stats
            if (_drawCallRecorder.Valid)
                _drawCalls.Add(_drawCallRecorder.LastValue);
            if (_triangleRecorder.Valid)
                _triangles.Add(_triangleRecorder.LastValue);
            if (_vertexRecorder.Valid)
                _vertices.Add(_vertexRecorder.LastValue);
            if (_setPassRecorder.Valid)
                _setPassCalls.Add(_setPassRecorder.LastValue);

            // Peak memory
            long used = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
            if (used > _peakUsedMemory)
                _peakUsedMemory = used;
        }

        BenchmarkReport BuildReport()
        {
            int gcCollections = GC.CollectionCount(0) - _gcCountAtStart;
            return BenchmarkReport.Build(
                _label,
                SceneManager.GetActiveScene().name,
                _sampleTimer,
                _frameTimesMs,
                _gpuTimesMs,
                _gcAllocAccum,
                gcCollections,
                _peakUsedMemory,
                _drawCalls,
                _triangles,
                _vertices,
                _setPassCalls
            );
        }

        void StartRecorders()
        {
            _gcAllocRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc");
            _drawCallRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            _triangleRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            _vertexRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Vertices Count");
            _setPassRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
        }

        void StopRecorders()
        {
            _gcAllocRecorder.Dispose();
            _drawCallRecorder.Dispose();
            _triangleRecorder.Dispose();
            _vertexRecorder.Dispose();
            _setPassRecorder.Dispose();
        }
    }
}
