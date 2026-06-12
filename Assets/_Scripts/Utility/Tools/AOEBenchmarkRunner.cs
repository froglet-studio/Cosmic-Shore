#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System.Collections.Generic;
using System.Diagnostics;
using Cysharp.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;
using CosmicShore.Core;
using CosmicShore.ECS;
using CosmicShore.Game;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;

namespace CosmicShore.Utility.Tools
{
    /// <summary>
    /// Automated AOE benchmark. Spawns synthetic prisms, runs explosions across
    /// Physics / Burst Legacy / Burst ECS modes (each twice), and prints a report.
    ///
    /// Usage: Add to an empty scene or attach to any GameObject, enter Play Mode.
    /// The benchmark runs automatically on Start and logs results to Console.
    /// Also accessible via FrogletTools > Run AOE Benchmark.
    /// </summary>
    public class AOEBenchmarkRunner : MonoBehaviour
    {
        [Header("Test Parameters")]
        [Tooltip("Number of synthetic prisms to register")]
        [SerializeField] private int prismCount = 3000;

        [Tooltip("Explosion max radius — controls how many prisms fall within the AOE")]
        [SerializeField] private float explosionRadius = 50f;

        [Tooltip("How many frames to run each explosion test")]
        [SerializeField] private int framesPerTest = 60;

        [Tooltip("How many times to repeat each mode (for consistency)")]
        [SerializeField] private int runsPerMode = 2;

        [Tooltip("Spread radius for prism placement")]
        [SerializeField] private float prismSpreadRadius = 100f;

        private readonly List<BenchmarkResult> _results = new();

        private struct BenchmarkResult
        {
            public string ModeName;
            public int Run;
            public int PrismCount;
            public double TotalMs;
            public double AvgFrameMs;
            public double MinFrameMs;
            public double MaxFrameMs;
            public int TotalHits;
            public int FrameCount;
        }

        private async void Start()
        {
            // Wait a frame for singletons to initialize
            await UniTask.Yield();
            await RunFullBenchmark();
        }

        public async UniTask RunFullBenchmark()
        {
            Debug.Log("=== AOE BENCHMARK START ===");
            Debug.Log($"Config: {prismCount} prisms, radius={explosionRadius}, " +
                      $"{framesPerTest} frames/test, {runsPerMode} runs/mode, " +
                      $"spread={prismSpreadRadius}");

            _results.Clear();

            // --- Mode 1: Physics (ForceLegacyPhysics = true) ---
            for (int run = 0; run < runsPerMode; run++)
            {
                ExplosionImpactor.ForceLegacyPhysics = true;
                PrismEntityBridge.UseECS = false;
                await RunSingleTest("Physics", run + 1);
            }

            // --- Mode 2: Burst Legacy ---
            for (int run = 0; run < runsPerMode; run++)
            {
                ExplosionImpactor.ForceLegacyPhysics = false;
                PrismEntityBridge.UseECS = false;
                await RunSingleTest("Burst Legacy", run + 1);
            }

            // --- Mode 3: Burst ECS ---
            for (int run = 0; run < runsPerMode; run++)
            {
                ExplosionImpactor.ForceLegacyPhysics = false;
                PrismEntityBridge.UseECS = true;
                await RunSingleTest("Burst ECS", run + 1);
            }

            // Reset to default
            ExplosionImpactor.ForceLegacyPhysics = false;
            PrismEntityBridge.UseECS = false;

            PrintReport();
            Debug.Log("=== AOE BENCHMARK COMPLETE ===");
        }

        private async UniTask RunSingleTest(string modeName, int runNumber)
        {
            Debug.Log($"  [{modeName}] Run {runNumber}/{runsPerMode} — registering {prismCount} prisms...");

            // Ensure registry
            var registry = PrismAOERegistry.EnsureInstance();
            if (registry == null || !registry.IsAvailable)
            {
                Debug.LogError("  PrismAOERegistry unavailable — cannot run benchmark");
                return;
            }

            // Clear previous data
            registry.ClearAll();

            // Register synthetic prisms in a sphere around origin
            for (int i = 0; i < prismCount; i++)
            {
                float3 pos = (float3)(Random.insideUnitSphere * prismSpreadRadius);
                registry.RegisterSynthetic(
                    pos,
                    PrismFlags.IsActive,
                    volume: 1f,
                    domain: (int)Domains.Jade);
            }

            Debug.Log($"  [{modeName}] Run {runNumber} — registered {registry.HighWaterMark} prisms, " +
                      $"running {framesPerTest} explosion frames...");

            // Wait a frame for state to settle
            await UniTask.Yield();

            // Run the explosion simulation
            var alreadyHit = new HashSet<int>(256);
            var frameTimes = new double[framesPerTest];
            int totalHits = 0;
            var sw = new Stopwatch();

            float explosionDuration = 2f;
            float speed = explosionRadius / explosionDuration;

            for (int frame = 0; frame < framesPerTest; frame++)
            {
                // Simulate growing radius (sine easing, matching AOEExplosion)
                float t = (float)(frame + 1) / framesPerTest;
                float ease = Mathf.Sin(t * Mathf.PI / 2f);
                float currentRadius = explosionRadius * ease;

                int hitsBefore = alreadyHit.Count;

                sw.Restart();

                // This is the actual hot path being benchmarked
                registry.ProcessExplosionFrame(
                    center: Vector3.zero,
                    radius: currentRadius,
                    speed: speed,
                    inertia: 1f,
                    explosionDomain: Domains.Ruby,    // different from prisms (Jade) → destructive
                    affectSelf: false,
                    destructive: true,
                    devastating: false,
                    shielding: false,
                    anonymous: true,
                    vessel: null,
                    alreadyHit: alreadyHit);

                sw.Stop();
                frameTimes[frame] = sw.Elapsed.TotalMilliseconds;
                totalHits += alreadyHit.Count - hitsBefore;

                await UniTask.Yield();
            }

            // Compute stats
            double totalMs = 0, minMs = double.MaxValue, maxMs = 0;
            for (int i = 0; i < framesPerTest; i++)
            {
                totalMs += frameTimes[i];
                if (frameTimes[i] < minMs) minMs = frameTimes[i];
                if (frameTimes[i] > maxMs) maxMs = frameTimes[i];
            }

            _results.Add(new BenchmarkResult
            {
                ModeName = modeName,
                Run = runNumber,
                PrismCount = prismCount,
                TotalMs = totalMs,
                AvgFrameMs = totalMs / framesPerTest,
                MinFrameMs = minMs,
                MaxFrameMs = maxMs,
                TotalHits = totalHits,
                FrameCount = framesPerTest
            });

            Debug.Log($"  [{modeName}] Run {runNumber} — avg={totalMs / framesPerTest:F3}ms, " +
                      $"total={totalMs:F1}ms, hits={totalHits}");

            // Cleanup
            registry.ClearAll();
            await UniTask.Yield();
        }

        private void PrintReport()
        {
            Debug.Log("\n" +
                "╔══════════════════════════════════════════════════════════════════════════╗\n" +
                "║                        AOE BENCHMARK REPORT                             ║\n" +
                "╚══════════════════════════════════════════════════════════════════════════╝");

            Debug.Log($"  Prisms: {prismCount}  |  Radius: {explosionRadius}  |  " +
                      $"Frames/test: {framesPerTest}  |  Runs/mode: {runsPerMode}\n");

            Debug.Log(
                $"  {"Mode",-16} {"Run",4} {"Avg(ms)",10} {"Min(ms)",10} {"Max(ms)",10} " +
                $"{"Total(ms)",11} {"Hits",6} {"Frames",7}");
            Debug.Log(
                $"  {"────────────────",-16} {"────",4} {"──────────",10} {"──────────",10} {"──────────",10} " +
                $"{"───────────",11} {"──────",6} {"───────",7}");

            // Group by mode for averaging
            var modeAverages = new Dictionary<string, (double sum, int count)>();

            foreach (var r in _results)
            {
                Debug.Log(
                    $"  {r.ModeName,-16} {r.Run,4} {r.AvgFrameMs,10:F3} {r.MinFrameMs,10:F3} " +
                    $"{r.MaxFrameMs,10:F3} {r.TotalMs,11:F1} {r.TotalHits,6} {r.FrameCount,7}");

                if (!modeAverages.ContainsKey(r.ModeName))
                    modeAverages[r.ModeName] = (0, 0);
                var (sum, count) = modeAverages[r.ModeName];
                modeAverages[r.ModeName] = (sum + r.AvgFrameMs, count + 1);
            }

            Debug.Log("");
            Debug.Log("  ── Summary (avg across runs) ──");

            double baselineMs = 0;
            foreach (var kvp in modeAverages)
            {
                double avg = kvp.Value.sum / kvp.Value.count;
                if (baselineMs == 0) baselineMs = avg; // first mode is baseline
                string speedup = baselineMs > 0 && avg > 0
                    ? $"{baselineMs / avg:F1}x"
                    : "—";
                Debug.Log($"  {kvp.Key,-16}  {avg,8:F3} ms/frame  (vs baseline: {speedup})");
            }

            Debug.Log("");
        }
    }
}
#endif
