using UnityEngine;

namespace CosmicShore.Utility.PerformanceBenchmark
{
    [CreateAssetMenu(
        fileName = "BenchmarkConfig",
        menuName = "ScriptableObjects/Tools/Benchmark Config",
        order = 100)]
    public class BenchmarkConfigSO : ScriptableObject
    {
        [Header("Timing")]
        [Tooltip("Seconds to wait before recording begins. Lets the scene stabilize.")]
        [SerializeField] private float warmupDuration = 3f;

        [Tooltip("Seconds of actual measurement after warmup completes.")]
        [SerializeField] private float sampleDuration = 10f;

        [Header("Capture Settings")]
        [Tooltip("Capture rendering stats (draw calls, batches, triangles, etc.).")]
        [SerializeField] private bool captureRenderingStats = true;

        [Tooltip("Capture memory stats (heap size, allocated, GC count).")]
        [SerializeField] private bool captureMemoryStats = true;

        [Tooltip("Capture physics stats (active rigidbodies, contacts).")]
        [SerializeField] private bool capturePhysicsStats = true;

        [Header("Output")]
        [Tooltip("Subfolder inside Application.persistentDataPath for saved reports.")]
        [SerializeField] private string outputFolder = "Benchmarks";

        [Tooltip("Optional label to tag this benchmark run (e.g. 'GDC_Demo', 'Squirrel_Race').")]
        [SerializeField] private string benchmarkLabel = "";

        public float WarmupDuration => warmupDuration;
        public float SampleDuration => sampleDuration;
        public bool CaptureRenderingStats => captureRenderingStats;
        public bool CaptureMemoryStats => captureMemoryStats;
        public bool CapturePhysicsStats => capturePhysicsStats;
        public string OutputFolder => outputFolder;
        public string BenchmarkLabel => benchmarkLabel;
    }
}
