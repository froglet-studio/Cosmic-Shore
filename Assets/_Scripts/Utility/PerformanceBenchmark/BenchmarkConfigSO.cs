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

        [Tooltip("Capture gameplay load (active prisms, explosion/implosion VFX, vessels, players) " +
                 "so frame cost can be read against the on-screen workload.")]
        [SerializeField] private bool captureGameLoadStats = true;

        [Tooltip("Capture Netcode for GameObjects metrics (CSM.Net.* marker time, RPCs/frame, " +
                 "NetVars dirty/frame, bytes/frame). 0 in non-networked scenes.")]
        [SerializeField] private bool captureNetcodeStats = true;

        [Header("Output")]
        [Tooltip("Subfolder inside Application.persistentDataPath for saved reports.")]
        [SerializeField] private string outputFolder = "Benchmarks";

        [Tooltip("Optional label to tag this benchmark run (e.g. 'Demo_Build', 'Squirrel_Race').")]
        [SerializeField] private string benchmarkLabel = "";

        [Header("Load-Time Targets")]
        [Tooltip("Cold boot target in seconds: engine start → main menu ready (menu vessel spawned, " +
                 "splash fade begins), cached sign-in, online. The published number lives in " +
                 "Docs/PERFORMANCE_OPTIMIZATION.md §0.6; this default mirrors it (LoadTimeTargets).")]
        [SerializeField, Min(0f)] private float coldBootToMenuTargetSeconds = LoadTimeTargets.ColdBootToMenuSeconds;

        [Tooltip("Menu → first playable frame target in seconds, for EVERY mode and intensity: arcade " +
                 "launch tap → arena complete (connecting screen done). Published in " +
                 "Docs/PERFORMANCE_OPTIMIZATION.md §0.6; this default mirrors it (LoadTimeTargets).")]
        [SerializeField, Min(0f)] private float menuToPlayableTargetSeconds = LoadTimeTargets.MenuToPlayableSeconds;

        public float WarmupDuration => warmupDuration;
        public float SampleDuration => sampleDuration;
        public bool CaptureRenderingStats => captureRenderingStats;
        public bool CaptureMemoryStats => captureMemoryStats;
        public bool CapturePhysicsStats => capturePhysicsStats;
        public bool CaptureGameLoadStats => captureGameLoadStats;
        public bool CaptureNetcodeStats => captureNetcodeStats;
        public string OutputFolder => outputFolder;
        public string BenchmarkLabel => benchmarkLabel;
        public float ColdBootToMenuTargetSeconds => coldBootToMenuTargetSeconds;
        public float MenuToPlayableTargetSeconds => menuToPlayableTargetSeconds;
    }
}
