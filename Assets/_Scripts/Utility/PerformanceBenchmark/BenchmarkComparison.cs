using System;
using System.Text;

namespace CosmicShore.Utility.PerformanceBenchmark
{
    /// <summary>
    /// Holds the delta between two <see cref="BenchmarkReport"/>s (baseline vs current)
    /// with absolute and percentage changes, plus a regression/improvement verdict.
    /// </summary>
    [Serializable]
    public class MetricDelta
    {
        public string metricName;
        public float baselineValue;
        public float currentValue;
        public float absoluteDelta;
        public float percentDelta;
        /// <summary>True when higher is worse (e.g. frame time, draw calls). False when higher is better (e.g. FPS).</summary>
        public bool higherIsWorse;
        public Verdict verdict;

        public enum Verdict { Improved, Neutral, Regressed }

        public MetricDelta(string name, float baseline, float current, bool higherIsWorse, float neutralThresholdPercent = 2f)
        {
            metricName = name;
            baselineValue = baseline;
            currentValue = current;
            absoluteDelta = current - baseline;
            percentDelta = baseline != 0 ? (absoluteDelta / Math.Abs(baseline)) * 100f : 0;
            this.higherIsWorse = higherIsWorse;

            if (Math.Abs(percentDelta) <= neutralThresholdPercent)
                verdict = Verdict.Neutral;
            else if (higherIsWorse)
                verdict = absoluteDelta > 0 ? Verdict.Regressed : Verdict.Improved;
            else
                verdict = absoluteDelta > 0 ? Verdict.Improved : Verdict.Regressed;
        }
    }

    /// <summary>
    /// Compares two benchmark reports and produces a structured comparison summary.
    /// </summary>
    public static class BenchmarkComparer
    {
        public class ComparisonResult
        {
            public BenchmarkReport baseline;
            public BenchmarkReport current;
            public MetricDelta[] deltas;
            public int improvements;
            public int regressions;
            public int neutral;
        }

        public static ComparisonResult Compare(BenchmarkReport baseline, BenchmarkReport current, float neutralThresholdPercent = 2f)
        {
            var bStats = baseline.statistics;
            var cStats = current.statistics;

            var deltas = new[]
            {
                // FPS (higher is better)
                new MetricDelta("Avg FPS", bStats.avgFps, cStats.avgFps, false, neutralThresholdPercent),
                new MetricDelta("Min FPS", bStats.minFps, cStats.minFps, false, neutralThresholdPercent),
                new MetricDelta("P1 FPS", bStats.p1Fps, cStats.p1Fps, false, neutralThresholdPercent),
                new MetricDelta("P5 FPS", bStats.p5Fps, cStats.p5Fps, false, neutralThresholdPercent),
                new MetricDelta("Median FPS", bStats.medianFps, cStats.medianFps, false, neutralThresholdPercent),

                // Frame time (lower is better)
                new MetricDelta("Avg Frame Time (ms)", bStats.avgFrameTimeMs, cStats.avgFrameTimeMs, true, neutralThresholdPercent),
                new MetricDelta("Max Frame Time (ms)", bStats.maxFrameTimeMs, cStats.maxFrameTimeMs, true, neutralThresholdPercent),
                new MetricDelta("P95 Frame Time (ms)", bStats.p95FrameTimeMs, cStats.p95FrameTimeMs, true, neutralThresholdPercent),
                new MetricDelta("P99 Frame Time (ms)", bStats.p99FrameTimeMs, cStats.p99FrameTimeMs, true, neutralThresholdPercent),
                new MetricDelta("StdDev Frame Time (ms)", bStats.stdDevFrameTimeMs, cStats.stdDevFrameTimeMs, true, neutralThresholdPercent),

                // Rendering (lower is better)
                new MetricDelta("Avg Draw Calls", bStats.avgDrawCalls, cStats.avgDrawCalls, true, neutralThresholdPercent),
                new MetricDelta("Avg Batches", bStats.avgBatches, cStats.avgBatches, true, neutralThresholdPercent),
                new MetricDelta("Avg SetPass Calls", bStats.avgSetPassCalls, cStats.avgSetPassCalls, true, neutralThresholdPercent),
                new MetricDelta("Avg Triangles", bStats.avgTriangles, cStats.avgTriangles, true, neutralThresholdPercent),

                // Memory (lower is better)
                new MetricDelta("Peak Allocated (MB)", bStats.peakAllocatedMemory / (1024f * 1024f), cStats.peakAllocatedMemory / (1024f * 1024f), true, neutralThresholdPercent),
                new MetricDelta("Avg Allocated (MB)", bStats.avgAllocatedMemory / (1024f * 1024f), cStats.avgAllocatedMemory / (1024f * 1024f), true, neutralThresholdPercent),
                new MetricDelta("Total GC (MB)", bStats.totalGcAllocated / (1024f * 1024f), cStats.totalGcAllocated / (1024f * 1024f), true, neutralThresholdPercent),
            };

            int improvements = 0, regressions = 0, neutralCount = 0;
            foreach (var d in deltas)
            {
                switch (d.verdict)
                {
                    case MetricDelta.Verdict.Improved: improvements++; break;
                    case MetricDelta.Verdict.Regressed: regressions++; break;
                    default: neutralCount++; break;
                }
            }

            return new ComparisonResult
            {
                baseline = baseline,
                current = current,
                deltas = deltas,
                improvements = improvements,
                regressions = regressions,
                neutral = neutralCount
            };
        }

        public static string FormatAsText(ComparisonResult result)
        {
            var sb = new StringBuilder();
            sb.AppendLine("╔══════════════════════════════════════════════════════════════════╗");
            sb.AppendLine("║              PERFORMANCE BENCHMARK COMPARISON                   ║");
            sb.AppendLine("╚══════════════════════════════════════════════════════════════════╝");
            sb.AppendLine();
            sb.AppendLine($"  Baseline: {result.baseline.label} @ {result.baseline.gitBranch}/{result.baseline.gitCommitHash} ({result.baseline.timestamp})");
            sb.AppendLine($"  Current:  {result.current.label} @ {result.current.gitBranch}/{result.current.gitCommitHash} ({result.current.timestamp})");
            sb.AppendLine($"  Scene:    {result.baseline.sceneName} → {result.current.sceneName}");
            sb.AppendLine($"  Frames:   {result.baseline.statistics.totalFrames} → {result.current.statistics.totalFrames}");
            sb.AppendLine();
            sb.AppendLine($"  {"Metric",-28} {"Baseline",10} {"Current",10} {"Delta",10} {"%",7}  Verdict");
            sb.AppendLine($"  {"─".PadRight(28, '─')} {"─".PadRight(10, '─')} {"─".PadRight(10, '─')} {"─".PadRight(10, '─')} {"─".PadRight(7, '─')}  {"─".PadRight(10, '─')}");

            foreach (var d in result.deltas)
            {
                string verdictStr = d.verdict switch
                {
                    MetricDelta.Verdict.Improved => "[BETTER]",
                    MetricDelta.Verdict.Regressed => "[WORSE]",
                    _ => "[~]"
                };

                string sign = d.absoluteDelta >= 0 ? "+" : "";
                sb.AppendLine($"  {d.metricName,-28} {d.baselineValue,10:F2} {d.currentValue,10:F2} {sign + d.absoluteDelta.ToString("F2"),10} {sign + d.percentDelta.ToString("F1") + "%",7}  {verdictStr}");
            }

            sb.AppendLine();
            sb.AppendLine($"  Summary: {result.improvements} improved, {result.regressions} regressed, {result.neutral} unchanged");
            sb.AppendLine();

            return sb.ToString();
        }
    }
}
