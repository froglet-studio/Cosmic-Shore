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
    /// Algorithms in the matrix (each adjacent pair isolates one variable):
    ///   - GridArgmax17                      — current Cell.countGrids[D].FindDensestRegion()
    ///   - GridSmoothed17                    — + separable box smoothing (sibling-branch baseline)
    ///   - GridSmoothedInterp17              — + sub-voxel parabolic interpolation
    ///   - GridMassSmoothedInterp17          — + mass-weighted per prism (volume sum)
    ///   - GridMassSmoothedInterp32          — same, finer 32^3 grid (does resolution help?)
    /// Ground truth uses a 64^3 grid with separable smoothing — ~100x faster than the
    /// brute-force candidate-vs-prism scan it replaced.
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

            // Algorithm matrix. Each adjacent pair isolates one variable so the
            // report attributes wins/losses to a specific knob:
            //   1 vs 2: smoothing on/off
            //   2 vs 3: sub-voxel interpolation on/off
            //   3 vs 4: mass-weighting on/off
            //   4 vs 5: grid resolution 17 vs 32
            string[] algoNames = new string[]
            {
                "GridArgmax17 (current production)",
                "GridSmoothed17 (sibling baseline)",
                "GridSmoothedInterp17",
                "GridMassSmoothedInterp17",
                "GridMassSmoothedInterp32",
            };
            SearchOptions[] algoOpts = new SearchOptions[]
            {
                DensityPartitionBenchmarkAlgorithms.GridArgmax17(),
                DensityPartitionBenchmarkAlgorithms.GridSmoothed17(),
                DensityPartitionBenchmarkAlgorithms.GridSmoothedInterp17(),
                DensityPartitionBenchmarkAlgorithms.GridMassSmoothedInterp17(),
                DensityPartitionBenchmarkAlgorithms.GridMassSmoothedInterp32(),
            };

            // Sort scenarios deterministically by label so the report diffs cleanly.
            var ordered = new List<BenchmarkScenario>(scenarios);
            ordered.Sort((a, b) => string.CompareOrdinal(a?.label ?? "", b?.label ?? ""));

            // Pre-build truth tapes + ground-truth answers once per (scenario, query).
            // Brute-force ground truth is the expensive step; caching it cuts total
            // time by ~Nalgos times.
            var truthList = new List<BenchmarkScenario>(ordered.Count);
            var truthData = new List<List<BenchmarkPrism>>(ordered.Count);
            var underTestData = new List<List<BenchmarkPrism>>(ordered.Count);
            var gtAnswers = new Dictionary<string, BenchmarkResult>();

            for (int si = 0; si < ordered.Count; si++)
            {
                var scn = ordered[si];
                if (scn == null) { truthList.Add(null); truthData.Add(null); underTestData.Add(null); continue; }
                var truth = scn.Build();
                var underTest = scn.ApplyStaleness(truth);
                truthList.Add(scn);
                truthData.Add(truth);
                underTestData.Add(underTest);

                foreach (var q in DensityPartitionBenchmarkReport.StandardQueries())
                {
                    var gt = DensityPartitionBenchmarkAlgorithms.GroundTruth(
                        truth, q.exclude, scn.worldHalfExtent,
                        groundTruthSmoothingRadius, groundTruthMassWeighted);
                    gtAnswers[si + "|" + q.label] = gt;
                }
            }

            // Run every algorithm over the pre-built tapes and pre-computed ground truth.
            var algos = new List<DensityPartitionBenchmarkReport.AlgorithmReport>(algoNames.Length);
            for (int ai = 0; ai < algoNames.Length; ai++)
                algos.Add(RunOneFromCache(algoNames[ai], algoOpts[ai], truthList, truthData, underTestData, gtAnswers));

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

            // ── Peaked scenarios (the algorithms' real job) ───────────────────

            // Cluster centered ON 60m grid lines — measures "best case" for grid
            // algorithms, since quantization error can hit 0 by accident.
            scenarios.Add(new BenchmarkScenario
            {
                label = "SingleCluster_OnGrid_r80",
                kind = ScenarioKind.SingleCluster,
                shape = ScenarioShape.Peaked,
                prismCount = 2000,
                seed = 42,
                worldHalfExtent = 500f,
                clusterCenter = new Vector3(120f, 60f, -180f),
                clusterRadius = 80f,
                jadeFraction = 0.4f, rubyFraction = 0.4f, goldFraction = 0.2f,
            });

            // Cluster center deliberately OFF 60m grid lines — exposes the real
            // grid quantization error floor. With 17^3 production grid the
            // theoretical lower bound for argmax is half-stride / sqrt(3) ≈ 17m.
            // Sub-voxel interp should drop this below 10m.
            scenarios.Add(new BenchmarkScenario
            {
                label = "SingleCluster_OffGrid_r80",
                kind = ScenarioKind.SingleCluster,
                shape = ScenarioShape.Peaked,
                prismCount = 2000,
                seed = 7,
                worldHalfExtent = 500f,
                clusterCenter = new Vector3(73f, 49f, -127f), // none on 60m lines
                clusterRadius = 80f,
                jadeFraction = 0.4f, rubyFraction = 0.4f, goldFraction = 0.2f,
            });

            // Tight cluster — kernel sees a sharp peak, sub-voxel interp can shine.
            scenarios.Add(new BenchmarkScenario
            {
                label = "TightCluster_OffGrid_r30",
                kind = ScenarioKind.SingleCluster,
                shape = ScenarioShape.Peaked,
                prismCount = 2000,
                seed = 11,
                worldHalfExtent = 500f,
                clusterCenter = new Vector3(173f, 49f, -227f),
                clusterRadius = 30f,
                jadeFraction = 0.4f, rubyFraction = 0.4f, goldFraction = 0.2f,
            });

            // Three Gaussian clusters — the realistic gameplay distribution.
            scenarios.Add(new BenchmarkScenario
            {
                label = "MultiCluster_2000_K3",
                kind = ScenarioKind.MultiCluster,
                shape = ScenarioShape.Peaked,
                prismCount = 2000,
                seed = 42,
                worldHalfExtent = 500f,
                clusterCount = 3,
                clusterRadius = 80f,
                jadeFraction = 0.3f, rubyFraction = 0.3f, goldFraction = 0.4f,
            });

            // Two equal-mass clusters — tests stability. Algorithm should pick one
            // consistently across queries (otherwise it'd thrash mid-game).
            scenarios.Add(new BenchmarkScenario
            {
                label = "TwoEqualClusters_2000",
                kind = ScenarioKind.MultiCluster,
                shape = ScenarioShape.Peaked,
                prismCount = 2000,
                seed = 123,
                worldHalfExtent = 500f,
                clusterCount = 2,
                clusterRadius = 80f,
                jadeFraction = 0.4f, rubyFraction = 0.4f, goldFraction = 0.2f,
            });

            // Heavy mass variance — only mass-weighted algorithms should benefit.
            scenarios.Add(new BenchmarkScenario
            {
                label = "MultiCluster_HeavyMass_K3",
                kind = ScenarioKind.MultiCluster,
                shape = ScenarioShape.Peaked,
                prismCount = 2000,
                seed = 42,
                worldHalfExtent = 500f,
                clusterCount = 3,
                clusterRadius = 80f,
                jadeFraction = 0.3f, rubyFraction = 0.3f, goldFraction = 0.4f,
                volumeMin = 0.5f, volumeMax = 4f,
            });

            // Staleness reproduction (static approximation of §2.3.1; see audit §6).
            // Doesn't fully model the dynamic Add/Remove cycle but keeps the slot
            // open — the static version is identical to clean for the anti-D
            // query (any non-X prism counts regardless of tag), which is itself a
            // useful finding to document in the report.
            scenarios.Add(new BenchmarkScenario
            {
                label = "MultiCluster_K3_stale30_static",
                kind = ScenarioKind.MultiCluster,
                shape = ScenarioShape.Peaked,
                prismCount = 2000,
                seed = 42,
                worldHalfExtent = 500f,
                clusterCount = 3,
                clusterRadius = 80f,
                jadeFraction = 0.3f, rubyFraction = 0.3f, goldFraction = 0.4f,
                staleFraction = 0.3f,
            });

            // ── Diffuse scenarios (no peak — diagnostic floor only) ───────────

            scenarios.Add(new BenchmarkScenario
            {
                label = "UniformRandom_2000_diag",
                kind = ScenarioKind.UniformRandom,
                shape = ScenarioShape.Diffuse,
                prismCount = 2000,
                seed = 42,
                worldHalfExtent = 500f,
                jadeFraction = 0.4f, rubyFraction = 0.4f, goldFraction = 0.2f,
            });

            scenarios.Add(new BenchmarkScenario
            {
                label = "Gradient_2000_X_diag",
                kind = ScenarioKind.Gradient,
                shape = ScenarioShape.Diffuse,
                prismCount = 2000,
                seed = 42,
                worldHalfExtent = 500f,
                gradientAxis = Vector3.right,
                jadeFraction = 0.4f, rubyFraction = 0.4f, goldFraction = 0.2f,
            });
        }

        // ------------------------------------------------------------------
        // Internals
        // ------------------------------------------------------------------

        DensityPartitionBenchmarkReport.AlgorithmReport RunOneFromCache(
            string name,
            SearchOptions opt,
            List<BenchmarkScenario> truthList,
            List<List<BenchmarkPrism>> truthData,
            List<List<BenchmarkPrism>> underTestData,
            Dictionary<string, BenchmarkResult> gtAnswers)
        {
            var rows = new List<DensityPartitionBenchmarkReport.QueryRow>(truthList.Count * 4);
            var totalSw = Stopwatch.StartNew();

            for (int si = 0; si < truthList.Count; si++)
            {
                var scenario = truthList[si];
                if (scenario == null) continue;
                var truth = truthData[si];
                var underTest = underTestData[si];

                if (warmupPass)
                {
                    // Warm-up: run each algorithm once on this scenario and discard.
                    foreach (var q in DensityPartitionBenchmarkReport.StandardQueries())
                        DensityPartitionBenchmarkAlgorithms.Search(underTest, q.exclude, scenario.worldHalfExtent, opt);
                }

                foreach (var q in DensityPartitionBenchmarkReport.StandardQueries())
                {
                    var gt = gtAnswers[si + "|" + q.label];
                    var sys = DensityPartitionBenchmarkAlgorithms.Search(underTest, q.exclude, scenario.worldHalfExtent, opt);

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
                        float scored = ScoreLocation(sys.Location, truth, q.exclude,
                            groundTruthSmoothingRadius, groundTruthMassWeighted);
                        massPct = 100f * scored / gt.Density;
                    }

                    rows.Add(new DensityPartitionBenchmarkReport.QueryRow
                    {
                        Scenario = scenario.label,
                        Shape = scenario.shape,
                        Query = q.label,
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
