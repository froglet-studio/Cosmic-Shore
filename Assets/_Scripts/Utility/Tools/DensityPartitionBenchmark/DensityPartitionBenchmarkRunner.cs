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
    /// The runner never spawns real Prism MonoBehaviours - every algorithm operates
    /// on a synthetic BenchmarkPrism list produced by BenchmarkScenario.Build().
    /// That keeps the harness independent of GameDataSO injection, PrismSpatialIndex
    /// initialization, Cell membrane setup, and the rest of the production
    /// lifecycle that doesn't survive Edit Mode. The trade-off is that integration
    /// bugs in HealthBlockTracker / Cell.AddBlock aren't exercised - those are
    /// simulated via BenchmarkScenario.staleFraction instead.
    ///
    /// Algorithms in the matrix (each adjacent pair isolates one variable):
    ///   - GridArgmax17                      - current Cell.countGrids[D].FindDensestRegion()
    ///   - GridSmoothed17                    - + separable box smoothing (sibling-branch baseline)
    ///   - GridSmoothedInterp17              - + sub-voxel parabolic interpolation
    ///   - GridMassSmoothedInterp17          - + mass-weighted per prism (volume sum)
    ///   - GridMassSmoothedInterp32          - same, finer 32^3 grid (does resolution help?)
    /// Ground truth uses a 64^3 grid with separable smoothing - ~100x faster than the
    /// brute-force candidate-vs-prism scan it replaced.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class DensityPartitionBenchmarkRunner : MonoBehaviour
    {
        // Bumped whenever the built-in scenario set or config defaults change.
        // RunAllAndDump auto-resets a stale serialized component to the current
        // defaults when this doesn't match _configVersion - so pulling a new
        // benchmark iteration "just works" without manually clearing the
        // Scenarios list on the GameObject. (That manual step was forgotten
        // every iteration and silently ran stale scenarios.)
        const int CurrentConfigVersion = 4;

        [SerializeField, HideInInspector] int _configVersion = 0;

        [Header("Scenarios")]
        [Tooltip("Scenarios run in this order. Each scenario produces four queries " +
                 "(anti-Jade, anti-Ruby, anti-Gold, all-domain). Auto-reset to the " +
                 "current built-in set when the benchmark version bumps - clear the " +
                 "list and Run to force a refresh, or use the inspector's Reset button.")]
        public List<BenchmarkScenario> scenarios = new();

        [Header("Ground truth")]
        [Tooltip("Smoothing radius for ground truth, in meters. Represents the swarm-effective " +
                 "consumption volume: consumeRadius (40m) + goalOrbitRadius (60m) + boid " +
                 "separation spread. 150m is a representative value - the algorithm only has " +
                 "to land within this of enemy mass for the swarm to sweep the rest.")]
        [Min(20f)] public float groundTruthSmoothingRadius = 150f;

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
        /// all algorithms - the brute-force scan is the expensive step, so caching
        /// it cuts total runtime by ~Nalgos times.
        /// </summary>
        [ContextMenu("Run All && Dump Report")]
        public string RunAllAndDump()
        {
            EnsureDefaults();

            // Algorithm matrix.
            //   Row 0 - ProductionFixedGrid17 - the LITERAL shipped algorithm: 17³
            //     count grid hard-fixed at ±500m, no adaptation to cell size. At
            //     1200m cell scale this drops every prism in the outer shell.
            //   Rows 1+ - adaptive grids sized to the data. Each adjacent pair
            //     isolates one variable (smoothing / interp / mass / resolution /
            //     centroid / mean-shift).
            // Row 0 vs Row 1 is the headline: how much does the grid-undersizing
            // bug cost vs an otherwise-identical grid that's sized to the cell?
            string[] algoNames = new string[]
            {
                "ProductionFixedGrid17 (LITERAL shipped)",
                "GridArgmax17 (adaptive - same algo, sized to cell)",
                "GridSmoothed17",
                "GridSmoothedInterp17",
                "GridMassSmoothedInterp17",
                "GridMassSmoothedInterp32",
                "GridMassSmoothedInterpCentroid17",
                "GridMassSmoothedInterpMeanShift17",
                "GridMassSmoothedInterpCentroid32",
                "GridMassSmoothedInterpMeanShift32",
                "ProductionV2 (75m voxels + voxel mean-shift - Phase 2 ship)",
            };
            SearchOptions[] algoOpts = new SearchOptions[]
            {
                DensityPartitionBenchmarkAlgorithms.ProductionFixedGrid17(),
                DensityPartitionBenchmarkAlgorithms.GridArgmax17(),
                DensityPartitionBenchmarkAlgorithms.GridSmoothed17(),
                DensityPartitionBenchmarkAlgorithms.GridSmoothedInterp17(),
                DensityPartitionBenchmarkAlgorithms.GridMassSmoothedInterp17(),
                DensityPartitionBenchmarkAlgorithms.GridMassSmoothedInterp32(),
                DensityPartitionBenchmarkAlgorithms.GridMassSmoothedInterpCentroid17(),
                DensityPartitionBenchmarkAlgorithms.GridMassSmoothedInterpMeanShift17(),
                DensityPartitionBenchmarkAlgorithms.GridMassSmoothedInterpCentroid32(),
                DensityPartitionBenchmarkAlgorithms.GridMassSmoothedInterpMeanShift32(),
                DensityPartitionBenchmarkAlgorithms.ProductionV2(),
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
            // Scenario label -> % of prisms inside the production grid's ±500m bounds.
            // <100% means the shipped grid silently drops the rest.
            var gridCoverage = new Dictionary<string, float>();

            float prodExtent = DensityPartitionBenchmarkAlgorithms.ProductionExtent;
            for (int si = 0; si < ordered.Count; si++)
            {
                var scn = ordered[si];
                if (scn == null) { truthList.Add(null); truthData.Add(null); underTestData.Add(null); continue; }
                var truth = scn.Build();
                var underTest = scn.ApplyStaleness(truth);
                truthList.Add(scn);
                truthData.Add(truth);
                underTestData.Add(underTest);

                // Grid-coverage diagnostic: how many truth prisms fall inside the
                // production grid's fixed ±500m box.
                int inside = 0;
                for (int pi = 0; pi < truth.Count; pi++)
                {
                    var pos = truth[pi].Position;
                    if (Mathf.Abs(pos.x) <= prodExtent &&
                        Mathf.Abs(pos.y) <= prodExtent &&
                        Mathf.Abs(pos.z) <= prodExtent)
                        inside++;
                }
                gridCoverage[scn.label] = truth.Count > 0 ? 100f * inside / truth.Count : 0f;

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
                branch, sha, algos, gridCoverage,
                kernelRadiusM: Mathf.RoundToInt(groundTruthSmoothingRadius),
                gridStrideM: Mathf.RoundToInt(DensityPartitionBenchmarkAlgorithms.ProductionStride),
                gridCellsPerAxis: DensityPartitionBenchmarkAlgorithms.ProductionGridCells);

            Debug.Log("[DensityPartitionBenchmark]\n" + lastReport);
            return lastReport;
        }

        /// <summary>
        /// Force a full reset to the current built-in defaults - scenarios + all
        /// config fields. Exposed for the editor inspector's "Reset to Defaults"
        /// button and the context menu.
        /// </summary>
        [ContextMenu("Reset Config && Scenarios to Defaults")]
        public void ResetToDefaults()
        {
            _configVersion = 0;            // force EnsureDefaults to treat this as stale
            if (scenarios != null) scenarios.Clear();
            EnsureDefaults();
        }

        /// <summary>
        /// Ensures the component is on the current built-in defaults. Re-populates
        /// the scenario list AND resets the config fields (kernel radius, etc.)
        /// whenever the serialized _configVersion lags CurrentConfigVersion - so a
        /// component serialized by an older benchmark iteration auto-refreshes on
        /// the next Run instead of silently running stale scenarios. Also fires on
        /// a genuinely empty scenario list (fresh component).
        /// </summary>
        public void EnsureDefaults()
        {
            if (scenarios == null) scenarios = new List<BenchmarkScenario>();

            bool stale = _configVersion != CurrentConfigVersion;
            if (scenarios.Count > 0 && !stale) return;

            if (stale && scenarios.Count > 0)
                Debug.Log($"[DensityPartitionBenchmark] Config v{_configVersion} is stale " +
                          $"(current v{CurrentConfigVersion}) - auto-resetting scenarios + config to defaults.");

            // Reset every code-defaulted field, not just the scenario list - a
            // stale component also carries a stale groundTruthSmoothingRadius etc.
            _configVersion = CurrentConfigVersion;
            groundTruthSmoothingRadius = DensityPartitionBenchmarkAlgorithms.DefaultSmoothingRadiusM;
            groundTruthMassWeighted = false;
            warmupPass = true;
            scenarios.Clear();

            // Iteration 4: re-anchored to the REAL cell scale. The Blob cell's
            // CapsuleMembrane has radius 1200m - a 2400m-diameter sphere. The
            // production BlockDensityGrid is hard-coded to a 1000m cube (±500m),
            // so it can only see ~14% of the cell's volume. Scenarios now place
            // clusters both inside ±500m (production grid sees them) and in the
            // 500-1200m outer shell (production grid silently drops them).
            const float CellRadius = 1200f;

            // ── Inside the production grid (±500m) - control cases ────────────

            scenarios.Add(new BenchmarkScenario
            {
                label = "InnerCluster_r150",
                kind = ScenarioKind.SingleCluster,
                shape = ScenarioShape.Peaked,
                prismCount = 2000,
                seed = 42,
                worldHalfExtent = CellRadius,
                clusterCenter = new Vector3(220f, 110f, -160f), // fully within ±500m
                clusterRadius = 150f,
                jadeFraction = 0.4f, rubyFraction = 0.4f, goldFraction = 0.2f,
            });

            scenarios.Add(new BenchmarkScenario
            {
                label = "TightCluster_Inner_r60",
                kind = ScenarioKind.SingleCluster,
                shape = ScenarioShape.Peaked,
                prismCount = 2000,
                seed = 11,
                worldHalfExtent = CellRadius,
                clusterCenter = new Vector3(173f, 91f, -227f),
                clusterRadius = 60f,
                jadeFraction = 0.4f, rubyFraction = 0.4f, goldFraction = 0.2f,
            });

            // ── In the outer shell (>500m) - the production grid CANNOT see ───

            // A dense cluster entirely outside ±500m. ProductionFixedGrid17 sees
            // zero prisms here and returns Empty; every adaptive variant nails it.
            // This is the direct demonstration of the grid-undersizing bug.
            scenarios.Add(new BenchmarkScenario
            {
                label = "OuterShellCluster_r150",
                kind = ScenarioKind.SingleCluster,
                shape = ScenarioShape.Peaked,
                prismCount = 2000,
                seed = 7,
                worldHalfExtent = CellRadius,
                clusterCenter = new Vector3(780f, 220f, -340f), // ~880m from origin
                clusterRadius = 150f,
                jadeFraction = 0.4f, rubyFraction = 0.4f, goldFraction = 0.2f,
            });

            // ── Spanning both regions - realistic gameplay distribution ───────

            // K=3 clusters spread across ±720m (worldHalfExtent*0.6). Some land
            // inside ±500m, some outside. Production grid finds only the inner
            // ones even when the densest cluster is in the outer shell.
            scenarios.Add(new BenchmarkScenario
            {
                label = "MultiCluster_K3_CellScale",
                kind = ScenarioKind.MultiCluster,
                shape = ScenarioShape.Peaked,
                prismCount = 2000,
                seed = 42,
                worldHalfExtent = CellRadius,
                clusterCount = 3,
                clusterRadius = 160f,
                jadeFraction = 0.3f, rubyFraction = 0.3f, goldFraction = 0.4f,
            });

            // Two clusters - one inner, one outer. The outer one is denser by
            // construction (seed-tuned). Production grid only sees the inner one,
            // so it always picks the WRONG cluster - a clean correctness failure,
            // not just an accuracy one.
            scenarios.Add(new BenchmarkScenario
            {
                label = "TwoClusters_InnerVsOuter",
                kind = ScenarioKind.MultiCluster,
                shape = ScenarioShape.Peaked,
                prismCount = 2000,
                seed = 123,
                worldHalfExtent = CellRadius,
                clusterCount = 2,
                clusterRadius = 160f,
                jadeFraction = 0.4f, rubyFraction = 0.4f, goldFraction = 0.2f,
            });

            scenarios.Add(new BenchmarkScenario
            {
                label = "MultiCluster_HeavyMass_CellScale",
                kind = ScenarioKind.MultiCluster,
                shape = ScenarioShape.Peaked,
                prismCount = 2000,
                seed = 42,
                worldHalfExtent = CellRadius,
                clusterCount = 3,
                clusterRadius = 160f,
                jadeFraction = 0.3f, rubyFraction = 0.3f, goldFraction = 0.4f,
                volumeMin = 0.5f, volumeMax = 4f,
            });

            // DomainSegregated: each cluster ~90% one domain, spread cell-scale.
            // The §2.3.1 staleness test - phantom Blue at the Jade-dominant
            // cluster pollutes anti-Jade's view.
            scenarios.Add(new BenchmarkScenario
            {
                label = "DomainSegregated_K3_clean",
                kind = ScenarioKind.DomainSegregatedClusters,
                shape = ScenarioShape.Peaked,
                prismCount = 2000,
                seed = 42,
                worldHalfExtent = CellRadius,
                clusterCount = 3,
                clusterRadius = 160f,
                segregatedPurity = 0.9f,
                jadeFraction = 0.33f, rubyFraction = 0.33f, goldFraction = 0.34f,
            });

            scenarios.Add(new BenchmarkScenario
            {
                label = "DomainSegregated_K3_stale30",
                kind = ScenarioKind.DomainSegregatedClusters,
                shape = ScenarioShape.Peaked,
                prismCount = 2000,
                seed = 42,
                worldHalfExtent = CellRadius,
                clusterCount = 3,
                clusterRadius = 160f,
                segregatedPurity = 0.9f,
                jadeFraction = 0.33f, rubyFraction = 0.33f, goldFraction = 0.34f,
                staleFraction = 0.3f,
            });

            // ── Diffuse (no peak - diagnostic floor only) ─────────────────────

            scenarios.Add(new BenchmarkScenario
            {
                label = "UniformRandom_CellScale_diag",
                kind = ScenarioKind.UniformRandom,
                shape = ScenarioShape.Diffuse,
                prismCount = 2000,
                seed = 42,
                worldHalfExtent = CellRadius,
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

                    // Time the system query 3 times post-warmup; capture median + worst.
                    // Exposes JIT/pooling instability - a "fast median" with a 5x worst
                    // case is materially different from a stable run.
                    var sys = DensityPartitionBenchmarkAlgorithms.Search(underTest, q.exclude, scenario.worldHalfExtent, opt);
                    double t1 = sys.ElapsedMs;
                    double t2 = DensityPartitionBenchmarkAlgorithms.Search(underTest, q.exclude, scenario.worldHalfExtent, opt).ElapsedMs;
                    double t3 = DensityPartitionBenchmarkAlgorithms.Search(underTest, q.exclude, scenario.worldHalfExtent, opt).ElapsedMs;
                    double minMs = System.Math.Min(t1, System.Math.Min(t2, t3));
                    double worstMs = System.Math.Max(t1, System.Math.Max(t2, t3));
                    double medMs = t1 + t2 + t3 - minMs - worstMs; // median = sum - min - max

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
                        ElapsedMs = medMs,
                        WorstMs = worstMs,
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
            // Box kernel - matches the separable-box smoothing the GroundTruth
            // uses, so mass% is an apples-to-apples ratio. A spherical kernel
            // would have ~47% the volume of the bounding cube, which is exactly
            // why iteration 1's mass% sat at 44-52% even when Δ was small.
            float s = 0f;
            for (int i = 0; i < truth.Count; i++)
            {
                var p = truth[i];
                if (exclude.HasValue && p.Domain == exclude.Value) continue;
                Vector3 diff = p.Position - loc;
                if (Mathf.Abs(diff.x) > radius) continue;
                if (Mathf.Abs(diff.y) > radius) continue;
                if (Mathf.Abs(diff.z) > radius) continue;
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
