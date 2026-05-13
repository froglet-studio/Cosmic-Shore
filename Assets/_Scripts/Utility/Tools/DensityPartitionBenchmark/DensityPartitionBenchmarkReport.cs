using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Utility.Tools.DensityPartitionBenchmark
{
    /// <summary>
    /// Builds the text report. Format is intentionally diff-friendly: scenarios are
    /// sorted by label, queries appear in a fixed order (anti-Jade, anti-Ruby,
    /// anti-Gold, all-domain), every number has units, every metric has a target.
    ///
    /// Two consecutive runs with the same scenario list and the same algorithms
    /// should produce identical reports modulo wall-clock time, so a textual diff
    /// surfaces real changes.
    /// </summary>
    public static class DensityPartitionBenchmarkReport
    {
        public const string Version = "1.1";

        // ------------------------------------------------------------------
        // Per-query record. Each scenario produces 4 rows (anti-J/R/G/all).
        // ------------------------------------------------------------------
        public struct QueryRow
        {
            public string Scenario;
            public ScenarioShape Shape;     // Peaked vs Diffuse — controls which median bucket
            public string Query;             // "anti-Jade", "all-domain", etc.
            public Vector3 GroundTruth;
            public float GroundTruthDensity;
            public Vector3 SystemAnswer;
            public float SystemDensity;
            public float DistanceErrorM;
            public float MassFoundPercent;   // ScoreLocation(sys.Loc, truth) / gt.Density * 100
            public double ElapsedMs;
            public bool GroundTruthEmpty;
            public bool SystemEmpty;
        }

        // ------------------------------------------------------------------
        // Top-level report: one algorithm × all scenarios.
        // ------------------------------------------------------------------
        public struct AlgorithmReport
        {
            public string AlgorithmName;
            public List<QueryRow> Rows;
            public double TotalElapsedMs;
        }

        // ==================================================================
        // Standard queries — plain struct array, no value tuples
        // ==================================================================

        public struct Query
        {
            public string label;
            public Domains? exclude;
        }

        static readonly Query[] _standardQueries = new Query[]
        {
            new Query { label = "anti-Jade",  exclude = Domains.Jade },
            new Query { label = "anti-Ruby",  exclude = Domains.Ruby },
            new Query { label = "anti-Gold",  exclude = Domains.Gold },
            new Query { label = "all-domain", exclude = null },
        };

        public static Query[] StandardQueries() => _standardQueries;

        // ==================================================================
        // Public render entry point
        // ==================================================================

        public static string Render(
            string branch,
            string commitSha,
            IReadOnlyList<AlgorithmReport> algos,
            int kernelRadiusM,
            int gridStrideM,
            int gridCellsPerAxis)
        {
            var sb = new StringBuilder(8192);
            string dateUtc = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture);

            sb.AppendLine($"=== DensityPartitionBenchmark v{Version} ===");
            sb.AppendLine($"Branch: {branch}   Commit: {commitSha}   Date: {dateUtc}");
            sb.AppendLine($"Kernel: {kernelRadiusM}m   ProductionGrid: {gridStrideM}m x {gridCellsPerAxis}^3");
            sb.AppendLine($"Distance units: meters.  Density units: prism count (or mass-weighted for Mass* variants).");
            sb.AppendLine($"Headline targets (on Peaked scenarios only):");
            sb.AppendLine($"  median Δ < 30m (= half the production grid stride)");
            sb.AppendLine($"  median mass-found > 90%");
            sb.AppendLine($"  median recompute < 1.0ms at N=2000");
            sb.AppendLine($"Notes:");
            sb.AppendLine($"  - 'density sys=' is the algorithm's own internal score over its (possibly stale) input.");
            sb.AppendLine($"  - 'mass=' is the corrected score — system answer rescored against the clean truth tape,");
            sb.AppendLine($"    divided by ground truth.");
            sb.AppendLine($"  - Diffuse scenarios (UniformRandom, Gradient) have no peak to find. They appear in the");
            sb.AppendLine($"    per-algorithm sections for diagnostic purposes but do NOT count toward the headline.");
            sb.AppendLine();

            foreach (var algo in algos)
                AppendAlgorithm(sb, algo);

            AppendCrossAlgorithmSummary(sb, algos);
            AppendPerScenarioWinners(sb, algos);

            return sb.ToString();
        }

        // ==================================================================
        // Per-algorithm section
        // ==================================================================

        static void AppendAlgorithm(StringBuilder sb, AlgorithmReport algo)
        {
            sb.AppendLine($"--- Algorithm: {algo.AlgorithmName} ---");

            string lastScenario = null;
            foreach (var row in algo.Rows)
            {
                if (row.Scenario != lastScenario)
                {
                    if (lastScenario != null) sb.AppendLine();
                    string tag = row.Shape == ScenarioShape.Diffuse ? "  [DIFFUSE — diagnostic only]" : "";
                    sb.AppendLine($"Scenario: {row.Scenario}{tag}");
                    lastScenario = row.Scenario;
                }

                if (row.GroundTruthEmpty)
                {
                    sb.AppendLine($"  {row.Query,-12} gt=(EMPTY) sys=(skipped)");
                    continue;
                }

                string sysLabel = row.SystemEmpty ? "(EMPTY)" : Fmt(row.SystemAnswer);
                sb.AppendLine(
                    $"  {row.Query,-12} gt={Fmt(row.GroundTruth)} sys={sysLabel} " +
                    $"Δ={row.DistanceErrorM,7:F1}m  density gt={row.GroundTruthDensity,6:F1} " +
                    $"sys={row.SystemDensity,7:F1} mass={row.MassFoundPercent,5:F0}%   " +
                    $"t={row.ElapsedMs:F2}ms");
            }

            sb.AppendLine();
            sb.AppendLine($"  total query time: {algo.TotalElapsedMs:F1}ms across {algo.Rows.Count} queries");
            sb.AppendLine();
        }

        // ==================================================================
        // Cross-algorithm summary — Peaked headline + Diffuse diagnostic
        // ==================================================================

        static void AppendCrossAlgorithmSummary(StringBuilder sb, IReadOnlyList<AlgorithmReport> algos)
        {
            sb.AppendLine("=== Summary (Peaked scenarios — headline ranking) ===");
            sb.AppendLine(string.Format("{0,-36} {1,12} {2,12} {3,12}",
                "Algorithm", "medianΔ(m)", "medianMass%", "medianMs"));

            // Track best on each axis for the * marker
            float bestDist = float.MaxValue;
            float bestMass = -1f;
            double bestMs = double.MaxValue;
            string bestDistAlgo = null, bestMassAlgo = null, bestMsAlgo = null;

            var cached = new List<(string name, float d, float m, double t)>();
            foreach (var algo in algos)
            {
                if (!ComputeMedians(algo.Rows, ScenarioShape.Peaked, out var d, out var m, out var t)) continue;
                cached.Add((algo.AlgorithmName, d, m, t));
                if (d < bestDist) { bestDist = d; bestDistAlgo = algo.AlgorithmName; }
                if (m > bestMass) { bestMass = m; bestMassAlgo = algo.AlgorithmName; }
                if (t < bestMs)   { bestMs = t;   bestMsAlgo = algo.AlgorithmName; }
            }

            foreach (var c in cached)
            {
                string mark(string algoName, string axisBest) => algoName == axisBest ? "*" : " ";
                sb.AppendLine(string.Format("{0,-36} {1,11:F1}{2} {3,10:F0}%{4} {5,11:F2}{6}",
                    c.name,
                    c.d, mark(c.name, bestDistAlgo),
                    c.m, mark(c.name, bestMassAlgo),
                    c.t, mark(c.name, bestMsAlgo)));
            }

            sb.AppendLine();
            sb.AppendLine("  *  marks the best algorithm per column. medianΔ lower-is-better, medianMass% higher-is-better, medianMs lower-is-better.");
            sb.AppendLine();

            // Diffuse diagnostic section
            sb.AppendLine("=== Summary (Diffuse scenarios — diagnostic only, no peak exists) ===");
            sb.AppendLine(string.Format("{0,-36} {1,12} {2,12} {3,12}",
                "Algorithm", "medianΔ(m)", "medianMass%", "medianMs"));
            foreach (var algo in algos)
            {
                if (!ComputeMedians(algo.Rows, ScenarioShape.Diffuse, out var d, out var m, out var t))
                {
                    sb.AppendLine(string.Format("{0,-36} {1,12} {2,12} {3,12}",
                        algo.AlgorithmName, "(no data)", "(no data)", "(no data)"));
                    continue;
                }
                sb.AppendLine(string.Format("{0,-36} {1,12:F1} {2,11:F0}% {3,12:F2}",
                    algo.AlgorithmName, d, m, t));
            }
            sb.AppendLine();
        }

        // ==================================================================
        // Per-scenario winners — which algorithm got closest, by scenario
        // ==================================================================

        static void AppendPerScenarioWinners(StringBuilder sb, IReadOnlyList<AlgorithmReport> algos)
        {
            if (algos.Count == 0) return;

            // Collect unique scenarios in deterministic order (use first algorithm's row order)
            var scenarios = new List<string>();
            var seen = new HashSet<string>();
            foreach (var row in algos[0].Rows)
            {
                if (seen.Add(row.Scenario)) scenarios.Add(row.Scenario);
            }

            sb.AppendLine("=== Per-scenario winners + stability (Peaked only) ===");
            sb.AppendLine(string.Format("{0,-34} {1,-36} {2,12} {3,12}",
                "Scenario", "Winner (smallest medianΔ)", "winnerΔ(m)", "winnerStab(m)"));

            foreach (var scn in scenarios)
            {
                ScenarioShape shape = ScenarioShape.Peaked;
                foreach (var algo in algos)
                {
                    bool found = false;
                    foreach (var row in algo.Rows)
                    {
                        if (row.Scenario == scn) { shape = row.Shape; found = true; break; }
                    }
                    if (found) break;
                }
                if (shape != ScenarioShape.Peaked) continue;

                string bestAlgo = null;
                float bestMedianD = float.MaxValue;
                float bestStability = 0f;
                foreach (var algo in algos)
                {
                    var dists = new List<float>();
                    var locs = new List<Vector3>();
                    foreach (var row in algo.Rows)
                    {
                        if (row.Scenario != scn) continue;
                        if (row.GroundTruthEmpty || row.SystemEmpty) continue;
                        dists.Add(row.DistanceErrorM);
                        locs.Add(row.SystemAnswer);
                    }
                    if (dists.Count == 0) continue;
                    dists.Sort();
                    float med = Median(dists);
                    if (med < bestMedianD)
                    {
                        bestMedianD = med;
                        bestAlgo = algo.AlgorithmName;
                        bestStability = PairwiseMedianDistance(locs);
                    }
                }

                if (bestAlgo != null)
                    sb.AppendLine(string.Format("{0,-34} {1,-36} {2,11:F1} {3,11:F1}",
                        scn, bestAlgo, bestMedianD, bestStability));
            }

            sb.AppendLine();
            sb.AppendLine("Stability = median pairwise distance between the algorithm's 4 query answers");
            sb.AppendLine("on the same scenario. A scenario with one dominant cluster should produce a low");
            sb.AppendLine("stability number (answers cluster together). High stability + low Δ together =");
            sb.AppendLine("'algorithm flips between equivalent answers across queries' (TwoEqualClusters at");
            sb.AppendLine("low grid resolution is the canonical example).");
            sb.AppendLine();
            sb.AppendLine("=== Cross-algorithm stability on each Peaked scenario ===");
            sb.AppendLine("(median pairwise distance between the 4 query answers — lower = more stable)");
            sb.AppendLine();

            // Header row of algorithm names
            sb.Append(string.Format("{0,-34}", "Scenario"));
            foreach (var algo in algos)
                sb.Append(string.Format(" {0,-22}", Truncate(algo.AlgorithmName, 22)));
            sb.AppendLine();

            foreach (var scn in scenarios)
            {
                ScenarioShape shape = ScenarioShape.Peaked;
                foreach (var algo in algos)
                {
                    bool found = false;
                    foreach (var row in algo.Rows)
                    {
                        if (row.Scenario == scn) { shape = row.Shape; found = true; break; }
                    }
                    if (found) break;
                }
                if (shape != ScenarioShape.Peaked) continue;

                sb.Append(string.Format("{0,-34}", scn));
                foreach (var algo in algos)
                {
                    var locs = new List<Vector3>();
                    foreach (var row in algo.Rows)
                    {
                        if (row.Scenario != scn) continue;
                        if (row.SystemEmpty) continue;
                        locs.Add(row.SystemAnswer);
                    }
                    float stab = PairwiseMedianDistance(locs);
                    sb.Append(string.Format(" {0,22}", string.Format("{0:F1}m", stab)));
                }
                sb.AppendLine();
            }

            sb.AppendLine();
            sb.AppendLine("Interpretation:");
            sb.AppendLine("  Headline rank is medianΔ on Peaked scenarios — that's the only metric");
            sb.AppendLine("  where 'algorithm accuracy' is meaningful. Diffuse scenarios have no peak");
            sb.AppendLine("  and exist only to surface noise-floor behavior.");
            sb.AppendLine();
            sb.AppendLine("  Use this report as the falsifiable contract for any redesign. A candidate");
            sb.AppendLine("  algorithm must beat the current production row (GridArgmax17) on at least");
            sb.AppendLine("  one of medianΔ / medianMass% / medianMs without regressing the other two.");
        }

        static float PairwiseMedianDistance(List<Vector3> locs)
        {
            if (locs.Count < 2) return 0f;
            var dists = new List<float>(locs.Count * (locs.Count - 1) / 2);
            for (int i = 0; i < locs.Count; i++)
            for (int j = i + 1; j < locs.Count; j++)
                dists.Add(Vector3.Distance(locs[i], locs[j]));
            dists.Sort();
            return Median(dists);
        }

        static string Truncate(string s, int max) =>
            s.Length <= max ? s : s.Substring(0, max);

        // ==================================================================
        // Helpers
        // ==================================================================

        static bool ComputeMedians(List<QueryRow> rows, ScenarioShape onlyShape,
                                   out float medianDist, out float medianMass, out double medianMs)
        {
            var dists = new List<float>();
            var masses = new List<float>();
            var times = new List<double>();
            foreach (var r in rows)
            {
                if (r.Shape != onlyShape) continue;
                if (r.GroundTruthEmpty || r.SystemEmpty) continue;
                dists.Add(r.DistanceErrorM);
                masses.Add(r.MassFoundPercent);
                times.Add(r.ElapsedMs);
            }
            dists.Sort(); masses.Sort(); times.Sort();
            medianDist = Median(dists);
            medianMass = Median(masses);
            medianMs = MedianD(times);
            return dists.Count > 0;
        }

        static string Fmt(Vector3 v) =>
            string.Format(CultureInfo.InvariantCulture, "({0,5:F0},{1,5:F0},{2,5:F0})", v.x, v.y, v.z);

        static float Median(List<float> xs)
        {
            if (xs.Count == 0) return 0f;
            int m = xs.Count / 2;
            return xs.Count % 2 == 1 ? xs[m] : (xs[m - 1] + xs[m]) * 0.5f;
        }

        static double MedianD(List<double> xs)
        {
            if (xs.Count == 0) return 0.0;
            int m = xs.Count / 2;
            return xs.Count % 2 == 1 ? xs[m] : (xs[m - 1] + xs[m]) * 0.5;
        }
    }
}
