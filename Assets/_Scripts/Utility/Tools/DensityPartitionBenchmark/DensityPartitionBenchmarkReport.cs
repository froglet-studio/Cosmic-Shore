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
        public const string Version = "1.0";

        // ------------------------------------------------------------------
        // Per-query record. Each scenario produces 4 rows (anti-J/R/G/all).
        // ------------------------------------------------------------------
        public struct QueryRow
        {
            public string Scenario;
            public string Query;          // "anti-Jade", "all-domain", etc.
            public Vector3 GroundTruth;
            public float GroundTruthDensity;
            public Vector3 SystemAnswer;
            public float SystemDensity;
            public float DistanceErrorM;
            public float MassFoundPercent; // system_density / gt_density * 100
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
        // Public API
        // ==================================================================

        /// <summary>
        /// Renders a full report comparing one or more algorithms across the given
        /// scenario reports. Returns plain text suitable for the Unity Console, the
        /// system clipboard, or a .txt file.
        /// </summary>
        public static string Render(
            string branch,
            string commitSha,
            IReadOnlyList<AlgorithmReport> algos,
            int kernelRadiusM,
            int gridStrideM,
            int gridCellsPerAxis)
        {
            var sb = new StringBuilder(4096);
            string dateUtc = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture);

            sb.AppendLine($"=== DensityPartitionBenchmark v{Version} ===");
            sb.AppendLine($"Branch: {branch}   Commit: {commitSha}   Date: {dateUtc}");
            sb.AppendLine($"Kernel: {kernelRadiusM}m   ProductionGrid: {gridStrideM}m x {gridCellsPerAxis}^3");
            sb.AppendLine($"Distance units: meters.  Density units: prism count (or mass-weighted for MassHistogram*).");
            sb.AppendLine($"Targets: median distance error < 30m (= production grid stride / 2).");
            sb.AppendLine($"         median mass-found > 90%.");
            sb.AppendLine($"         median recompute time < 1.0ms at N=2000.");
            sb.AppendLine($"Note: 'density sys=' is the algorithm's own internal score over its (possibly stale) input.");
            sb.AppendLine($"      'mass=' is the corrected score — system answer rescored against the clean truth tape,");
            sb.AppendLine($"      divided by ground truth. Stale scenarios show sys high but mass low.");
            sb.AppendLine();

            foreach (var algo in algos)
                AppendAlgorithm(sb, algo);

            AppendCrossAlgorithmSummary(sb, algos);

            return sb.ToString();
        }

        // ==================================================================
        // Helpers
        // ==================================================================

        static void AppendAlgorithm(StringBuilder sb, AlgorithmReport algo)
        {
            sb.AppendLine($"--- Algorithm: {algo.AlgorithmName} ---");

            // Per-scenario, in deterministic order (already pre-sorted).
            string lastScenario = null;
            foreach (var row in algo.Rows)
            {
                if (row.Scenario != lastScenario)
                {
                    if (lastScenario != null) sb.AppendLine();
                    sb.AppendLine($"Scenario: {row.Scenario}");
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
                    $"sys={row.SystemDensity,6:F1} mass={row.MassFoundPercent,5:F0}%   " +
                    $"t={row.ElapsedMs:F2}ms");
            }

            sb.AppendLine();
            sb.AppendLine($"  total query time: {algo.TotalElapsedMs:F1}ms across {algo.Rows.Count} queries");
            sb.AppendLine();
        }

        static void AppendCrossAlgorithmSummary(StringBuilder sb, IReadOnlyList<AlgorithmReport> algos)
        {
            sb.AppendLine("=== Summary ===");
            sb.AppendLine(string.Format("{0,-28} {1,12} {2,12} {3,12}",
                "Algorithm", "medianΔ(m)", "medianMass%", "medianMs"));

            foreach (var algo in algos)
            {
                var dists = new List<float>(algo.Rows.Count);
                var masses = new List<float>(algo.Rows.Count);
                var times = new List<double>(algo.Rows.Count);
                foreach (var r in algo.Rows)
                {
                    if (r.GroundTruthEmpty || r.SystemEmpty) continue;
                    dists.Add(r.DistanceErrorM);
                    masses.Add(r.MassFoundPercent);
                    times.Add(r.ElapsedMs);
                }
                dists.Sort();
                masses.Sort();
                times.Sort();

                float md = Median(dists);
                float mm = Median(masses);
                double mt = MedianD(times);

                sb.AppendLine(string.Format("{0,-28} {1,12:F1} {2,11:F0}% {3,12:F2}",
                    algo.AlgorithmName, md, mm, mt));
            }

            sb.AppendLine();
            sb.AppendLine("Interpretation:");
            sb.AppendLine("  medianΔ — half of queries land within this distance of ground truth. Lower is better.");
            sb.AppendLine("  medianMass% — half of queries find at least this fraction of ground-truth density. Higher is better.");
            sb.AppendLine("  medianMs — median per-query wall-clock cost. Lower is better.");
            sb.AppendLine();
            sb.AppendLine("Use this report as the falsifiable contract for any redesign. If a candidate");
            sb.AppendLine("algorithm regresses on any of the three columns vs. the current GridArgmax row,");
            sb.AppendLine("it has to clear a higher bar than 'looks right in Menu_Main' before shipping.");
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

        // ------------------------------------------------------------------
        // Query iteration helper. Returns the four standard queries in order:
        //   anti-Jade, anti-Ruby, anti-Gold, all-domain.
        // ------------------------------------------------------------------
        public static IEnumerable<(string label, Domains? exclude)> StandardQueries()
        {
            yield return ("anti-Jade", Domains.Jade);
            yield return ("anti-Ruby", Domains.Ruby);
            yield return ("anti-Gold", Domains.Gold);
            yield return ("all-domain", null);
        }
    }
}
