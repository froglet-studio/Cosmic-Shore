using System;
using System.Collections.Generic;
using System.Diagnostics;
using CosmicShore.Data;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace CosmicShore.Utility.Tools.DensityPartitionBenchmark
{
    /// <summary>
    /// Edit-Mode-capable harness for testing density-partition algorithms against a
    /// deterministic ground truth. Drop this on a GameObject in
    /// Assets/_Scenes/Game_TestDesign/DensityPartitionBenchmark.unity (or any other
    /// scene); configure the scenario list; press "Run All & Dump Report" in the
    /// inspector or invoke RunAllAndDump() from a Toolbox button.
    ///
    /// The runner never spawns real Prism MonoBehaviours — every algorithm operates
    /// on a synthetic BenchmarkPrism list produced by BenchmarkScenario.Build().
    /// That keeps the harness independent of GameDataSO injection, PrismAOERegistry
    /// initialization, Cell membrane setup, and the rest of the production
    /// lifecycle that doesn't survive Edit Mode. The trade-off is that integration
    /// bugs in HealthBlockTracker / Cell.AddBlock aren't exercised — those are
    /// simulated via BenchmarkScenario.staleFraction instead.
    ///
    /// Algorithms included:
    ///   - GroundTruth                       — slow brute-force, used as the oracle
    ///   - GridArgmax                         — current Cell.countGrids[D].FindDensestRegion()
    ///   - GridSmoothed                       — current + 3x3x3 smoothing (sibling-branch baseline)
    ///   - GridCentroid                       — alternative reduction over the same grid
    ///   - MassHistogramArgmax                — recommended (§3.7 in audit), volume-weighted
    ///   - MassHistogramSmoothed              — recommended + 3x3x3 smoothing (default reduction)
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class DensityPartitionBenchmarkRunner : MonoBehaviour
    {
        [Header("Scenarios")]
        [Tooltip("Scenarios run in this order. Each scenario produces four queries " +
                 "(anti-Jade, anti-Ruby, anti-Gold, all-domain). Reorder via the +/- " +
                 "controls. The default list reproduces the audit's failure modes.")]
        public List<BenchmarkScenario> scenarios = new();

        [Header("Ground truth")]
        [Tooltip("Smoothing radius for ground truth, in meters. Each ground-truth scan picks " +
                 "the candidate center with the highest prism count (or mass) inside this radius. " +
                 "Smaller = finer peak; larger = more lenient.")]
        [Min(20f)] public float groundTruthSmoothingRadius = 90f;

        [Tooltip("If true, ground truth weights each prism by Volume instead of counting 1. " +
                 "Keep false to grade against the current production semantics (count-only).")]
        public bool groundTruthMassWeighted = false;

        [Header("Run controls")]
        [Tooltip("Run a warm-up pass (results discarded) before timing the real pass. " +
                 "Reduces JIT startup bias for short scenarios.")]
        public bool warmupPass = true;

        [Header("Output")]
        [Tooltip("Last rendered report. Populated by RunAllAndDump(). Editor 'Copy to Clipboard' " +
                 "button reads from here.")]
        [TextArea(8, 40)] public string lastReport = "";

        // ------------------------------------------------------------------
        // Public API
        // ------------------------------------------------------------------

        /// <summary>
        /// Run every scenario against every algorithm, render the report, and dump
        /// it to the Console. The report is also stored in lastReport for clipboard
        /// access. Returns the rendered text for caller convenience.
        ///
        /// Ground truth is computed once per (scenario, query) pair and reused for
        /// all algorithms — the brute-force scan is the expensive step, so caching
        /// it cuts total runtime by ~Nalgos times.
        /// </summary>
        [ContextMenu("Run All && Dump Report")]
        public string RunAllAndDump()
        {
            EnsureDefaultScenarios();

            var named = new (string name, AlgorithmFn fn)[]
            {
                ("GridArgmax (current)",            DensityPartitionBenchmarkAlgorithms.GridArgmax),
                ("GridSmoothed (current+smooth)",   DensityPartitionBenchmarkAlgorithms.GridSmoothed),
                ("GridCentroid (baseline)",         DensityPartitionBenchmarkAlgorithms.GridCentroid),
                ("MassHistogramArgmax (rec)",       DensityPartitionBenchmarkAlgorithms.MassHistogramArgmax),
                ("MassHistogramSmoothed (rec+smooth)", DensityPartitionBenchmarkAlgorithms.MassHistogramSmoothed),
            };

            // Sort scenarios deterministically by label so the report diffs cleanly.
            var ordered = new List<BenchmarkScenario>(scenarios);
            ordered.Sort((a, b) => string.CompareOrdinal(a?.label ?? "", b?.label ?? ""));

            // Pre-build truth tapes + ground-truth answers once per (scenario, query).
            // Brute-force ground truth is the expensive step; caching it cuts total
            // time by ~Nalgos times.
            var truths = new List<(BenchmarkScenario scn, List<BenchmarkPrism> truth, List<BenchmarkPrism> underTest)>(ordered.Count);
            var gtAnswers = new Dictionary<(int scnIdx, string query), BenchmarkResult>();
            for (int si = 0; si < ordered.Count; si++)
            {
                var scn = ordered[si];
                if (scn == null) { truths.Add((null, null, null)); continue; }
                var truth = scn.Build();
                var underTest = scn.ApplyStaleness(truth);
                truths.Add((scn, truth, underTest));

                foreach (var (label, excl) in DensityPartitionBenchmarkReport.StandardQueries())
                {
                    var gt = DensityPartitionBenchmarkAlgorithms.GroundTruth(
                        truth, excl, scn.worldHalfExtent,
                        groundTruthSmoothingRadius, groundTruthMassWeighted);
                    gtAnswers[(si, label)] = gt;
                }
            }

            // Run every algorithm over the pre-built tapes and pre-computed ground truth.
            var algos = new List<DensityPartitionBenchmarkReport.AlgorithmReport>(named.Length);
            foreach (var (algoName, fn) in named)
                algos.Add(RunOneFromCache(algoName, fn, truths, gtAnswers));

            string branch = ReadEnvOr("GIT_BRANCH", "(unknown)");
            string sha = ReadEnvOr("GIT_SHA", "(unknown)");

            lastReport = DensityPartitionBenchmarkReport.Render(
                branch, sha, algos,
                kernelRadiusM: Mathf.RoundToInt(groundTruthSmoothingRadius),
                gridStrideM: Mathf.RoundToInt(DensityPartitionBenchmarkAlgorithms.ProductionStride),
                gridCellsPerAxis: DensityPartitionBenchmarkAlgorithms.ProductionGridCells);

            Debug.Log("[DensityPartitionBenchmark]\n" + lastReport);
            return lastReport;
        }

        /// <summary>
        /// Re-populate the scenario list with the audit's recommended default set if
        /// the list is empty. Lets the user run a useful benchmark on a fresh
        /// component without inspector configuration.
        /// </summary>
        public void EnsureDefaultScenarios()
        {
            if (scenarios == null) scenarios = new List<BenchmarkScenario>();
            if (scenarios.Count > 0) return;

            scenarios.Add(new BenchmarkScenario
            {
                label = "UniformRandom_2000",
                kind = ScenarioKind.UniformRandom,
                prismCount = 2000,
                seed = 42,
                worldHalfExtent = 500f,
                jadeFraction = 0.4f, rubyFraction = 0.4f, goldFraction = 0.2f,
            });

            scenarios.Add(new BenchmarkScenario
            {
                label = "SingleCluster_2000_r100",
                kind = ScenarioKind.SingleCluster,
                prismCount = 2000,
                seed = 42,
                worldHalfExtent = 500f,
                clusterCenter = new Vector3(150f, 80f, -120f),
                clusterRadius = 100f,
                jadeFraction = 0.4f, rubyFraction = 0.4f, goldFraction = 0.2f,
            });

            scenarios.Add(new BenchmarkScenario
            {
                label = "MultiCluster_2000_K3",
                kind = ScenarioKind.MultiCluster,
                prismCount = 2000,
                seed = 42,
                worldHalfExtent = 500f,
                clusterCount = 3,
                clusterRadius = 80f,
                jadeFraction = 0.3f, rubyFraction = 0.3f, goldFraction = 0.4f,
            });

            scenarios.Add(new BenchmarkScenario
            {
                label = "Gradient_2000_X",
                kind = ScenarioKind.Gradient,
                prismCount = 2000,
                seed = 42,
                worldHalfExtent = 500f,
                gradientAxis = Vector3.right,
                jadeFraction = 0.4f, rubyFraction = 0.4f, goldFraction = 0.2f,
            });

            // Staleness reproductions — same shape as above but with 30% of prisms
            // mis-tagged as Blue, matching the §2.3.1 ordering bug. Run alongside
            // the clean versions to make the "anti-domain drifts away from truth"
            // failure mode quantitative.
            scenarios.Add(new BenchmarkScenario
            {
                label = "MultiCluster_2000_K3_stale30",
                kind = ScenarioKind.MultiCluster,
                prismCount = 2000,
                seed = 42,
                worldHalfExtent = 500f,
                clusterCount = 3,
                clusterRadius = 80f,
                jadeFraction = 0.3f, rubyFraction = 0.3f, goldFraction = 0.4f,
                staleFraction = 0.3f,
            });

            scenarios.Add(new BenchmarkScenario
            {
                label = "MultiCluster_HeavyMass_2000_K3",
                kind = ScenarioKind.MultiCluster,
                prismCount = 2000,
                seed = 42,
                worldHalfExtent = 500f,
                clusterCount = 3,
                clusterRadius = 80f,
                jadeFraction = 0.3f, rubyFraction = 0.3f, goldFraction = 0.4f,
                volumeMin = 0.5f, volumeMax = 4f,
            });
        }

        // ------------------------------------------------------------------
        // Internals
        // ------------------------------------------------------------------

        delegate BenchmarkResult AlgorithmFn(IReadOnlyList<BenchmarkPrism> prisms, Domains? exclude, float worldHalfExtent);

        DensityPartitionBenchmarkReport.AlgorithmReport RunOneFromCache(
            string name,
            AlgorithmFn fn,
            List<(BenchmarkScenario scn, List<BenchmarkPrism> truth, List<BenchmarkPrism> underTest)> truths,
            Dictionary<(int scnIdx, string query), BenchmarkResult> gtAnswers)
        {
            var rows = new List<DensityPartitionBenchmarkReport.QueryRow>(truths.Count * 4);
            var totalSw = Stopwatch.StartNew();

            for (int si = 0; si < truths.Count; si++)
            {
                var (scenario, truth, underTest) = truths[si];
                if (scenario == null) continue;

                if (warmupPass)
                {
                    // Warm-up: run each algorithm once on this scenario and discard.
                    foreach (var (_, excl) in DensityPartitionBenchmarkReport.StandardQueries())
                        fn(underTest, excl, scenario.worldHalfExtent);
                }

                foreach (var (label, excl) in DensityPartitionBenchmarkReport.StandardQueries())
                {
                    var gt = gtAnswers[(si, label)];
                    var sys = fn(underTest, excl, scenario.worldHalfExtent);

                    float dist = (gt.Empty || sys.Empty)
                        ? 0f
                        : Vector3.Distance(gt.Location, sys.Location);

                    float massPct = 0f;
                    if (!gt.Empty && gt.Density > 0f)
                    {
                        // Compare densities in a comparable space: rescore the system's
                        // chosen point under the ground-truth metric (count of true-domain
                        // prisms inside the smoothing radius). Avoids comparing apples
                        // (system-count over a corrupted tape) to oranges (ground-truth
                        // count over the clean tape).
                        float scored = ScoreLocation(sys.Location, truth, excl,
                            groundTruthSmoothingRadius, groundTruthMassWeighted);
                        massPct = 100f * scored / gt.Density;
                    }

                    rows.Add(new DensityPartitionBenchmarkReport.QueryRow
                    {
                        Scenario = scenario.label,
                        Query = label,
                        GroundTruth = gt.Location,
                        GroundTruthDensity = gt.Density,
                        SystemAnswer = sys.Location,
                        SystemDensity = sys.Density,
                        DistanceErrorM = dist,
                        MassFoundPercent = massPct,
                        ElapsedMs = sys.ElapsedMs,
                        GroundTruthEmpty = gt.Empty,
                        SystemEmpty = sys.Empty,
                    });
                }
            }

            totalSw.Stop();
            return new DensityPartitionBenchmarkReport.AlgorithmReport
            {
                AlgorithmName = name,
                Rows = rows,
                TotalElapsedMs = totalSw.Elapsed.TotalMilliseconds,
            };
        }

        static float ScoreLocation(
            Vector3 loc,
            IReadOnlyList<BenchmarkPrism> truth,
            Domains? exclude,
            float radius,
            bool massWeighted)
        {
            float r2 = radius * radius;
            float s = 0f;
            for (int i = 0; i < truth.Count; i++)
            {
                var p = truth[i];
                if (exclude.HasValue && p.Domain == exclude.Value) continue;
                if ((p.Position - loc).sqrMagnitude > r2) continue;
                s += massWeighted ? p.Volume : 1f;
            }
            return s;
        }

        static string ReadEnvOr(string key, string fallback)
        {
            try
            {
                var v = Environment.GetEnvironmentVariable(key);
                return string.IsNullOrEmpty(v) ? fallback : v;
            }
            catch { return fallback; }
        }
    }
}
