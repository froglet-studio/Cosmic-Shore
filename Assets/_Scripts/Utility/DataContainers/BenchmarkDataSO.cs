using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.Soap
{
    /// <summary>
    /// Central SOAP data container for the performance benchmark system.
    /// Holds runtime state + SOAP events that decouple the benchmark runner from all consumers.
    /// Create one asset and wire it into PerformanceBenchmarkRunner, any UI overlays, or
    /// CI tooling that needs to react to benchmark lifecycle.
    /// </summary>
    [CreateAssetMenu(
        fileName = "BenchmarkData",
        menuName = "ScriptableObjects/DataContainers/Benchmark Data")]
    public class BenchmarkDataSO : ScriptableObject
    {
        // ─────────────────────────────────────────────────────────────────────
        // Lifecycle Events
        // ─────────────────────────────────────────────────────────────────────

        [Header("Lifecycle Events")]
        [Tooltip("Raised when a benchmark run begins (after warmup starts).")]
        public ScriptableEventNoParam OnBenchmarkStarted;

        [Tooltip("Raised when warmup finishes and actual frame sampling begins.")]
        public ScriptableEventNoParam OnSamplingStarted;

        [Tooltip("Raised when the benchmark run completes and the report is saved.")]
        public ScriptableEventBenchmarkStateData OnBenchmarkCompleted;

        [Tooltip("Raised when a benchmark is stopped early before completion.")]
        public ScriptableEventBenchmarkStateData OnBenchmarkStopped;

        // ─────────────────────────────────────────────────────────────────────
        // Progress Updates
        // ─────────────────────────────────────────────────────────────────────

        [Header("Progress")]
        [Tooltip("Raised periodically during sampling with current progress and live stats.")]
        public ScriptableEventBenchmarkStateData OnProgressUpdated;

        // ─────────────────────────────────────────────────────────────────────
        // Runtime State (written by runner, read by consumers)
        // ─────────────────────────────────────────────────────────────────────

        [Header("Runtime State")]
        [HideInInspector] public bool IsRunning;
        [HideInInspector] public bool IsSampling;
        [HideInInspector] public float Progress;
        [HideInInspector] public int FramesCaptured;
        [HideInInspector] public string ActiveLabel;
        [HideInInspector] public string LastReportPath;

        // ─────────────────────────────────────────────────────────────────────
        // Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        public void ResetRuntimeData()
        {
            IsRunning = false;
            IsSampling = false;
            Progress = 0f;
            FramesCaptured = 0;
            ActiveLabel = string.Empty;
            LastReportPath = string.Empty;
        }
    }
}
