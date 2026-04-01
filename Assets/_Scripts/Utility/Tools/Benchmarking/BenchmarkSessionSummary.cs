using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CosmicShore.Utility.Tools.Benchmarking
{
    /// <summary>
    /// Statistical summary across multiple benchmark iterations.
    /// Used to assess reproducibility via Coefficient of Variation (CoV)
    /// and min/max spread analysis.
    /// </summary>
    [Serializable]
    public class BenchmarkSessionSummary
    {
        // ── Metadata ────────────────────────────────────────────────────────
        public string Label;
        public string SceneName;
        public int IterationCount;
        public float DurationPerIteration;
        public bool Deterministic;
        public int Seed;

        // ── Avg Frame Time (ms) ─────────────────────────────────────────────
        public float AvgFrameTimeMean;
        public float AvgFrameTimeMin;
        public float AvgFrameTimeMax;
        public float AvgFrameTimeStdDev;
        public float AvgFrameTimeCoV;
        public float AvgFrameTimeSpread;
        public float AvgFrameTimeSpreadPercent;

        // ── Avg FPS ─────────────────────────────────────────────────────────
        public float AvgFpsMean;
        public float AvgFpsMin;
        public float AvgFpsMax;
        public float AvgFpsStdDev;
        public float AvgFpsCoV;
        public float AvgFpsSpread;
        public float AvgFpsSpreadPercent;

        // ── P99 Frame Time (ms) ─────────────────────────────────────────────
        public float P99FrameTimeMean;
        public float P99FrameTimeMin;
        public float P99FrameTimeMax;
        public float P99FrameTimeCoV;

        // ── Jank ────────────────────────────────────────────────────────────
        public float JankPercentMean;
        public float JankPercentMin;
        public float JankPercentMax;

        // ── GC ──────────────────────────────────────────────────────────────
        public float GcAllocMeanKB;
        public float GcAllocMinKB;
        public float GcAllocMaxKB;

        // ── Draw Calls ──────────────────────────────────────────────────────
        public float DrawCallsMean;
        public float DrawCallsMin;
        public float DrawCallsMax;

        // ── Construction ────────────────────────────────────────────────────

        public static BenchmarkSessionSummary Build(
            List<BenchmarkReport> reports,
            BenchmarkSessionConfig config)
        {
            if (reports == null || reports.Count < 2) return null;

            var avgFrameTimes = reports.Select(r => r.AvgFrameTimeMs).ToList();
            var avgFps = reports.Select(r => r.AvgFps).ToList();
            var p99FrameTimes = reports.Select(r => r.P99FrameTimeMs).ToList();
            var jankPercents = reports.Select(r => r.JankPercent).ToList();
            var gcAllocs = reports.Select(r => r.TotalGcAllocBytes / 1024f).ToList();
            var drawCalls = reports.Select(r => (float)r.AvgDrawCalls).Where(d => d >= 0).ToList();

            var summary = new BenchmarkSessionSummary
            {
                Label = config.Label,
                SceneName = reports[0].SceneName,
                IterationCount = reports.Count,
                DurationPerIteration = config.DurationSeconds,
                Deterministic = config.Deterministic,
                Seed = config.Seed,

                AvgFrameTimeMean = Mean(avgFrameTimes),
                AvgFrameTimeMin = avgFrameTimes.Min(),
                AvgFrameTimeMax = avgFrameTimes.Max(),
                AvgFrameTimeStdDev = StdDev(avgFrameTimes),
                AvgFrameTimeCoV = CoV(avgFrameTimes),
                AvgFrameTimeSpread = avgFrameTimes.Max() - avgFrameTimes.Min(),

                AvgFpsMean = Mean(avgFps),
                AvgFpsMin = avgFps.Min(),
                AvgFpsMax = avgFps.Max(),
                AvgFpsStdDev = StdDev(avgFps),
                AvgFpsCoV = CoV(avgFps),
                AvgFpsSpread = avgFps.Max() - avgFps.Min(),

                P99FrameTimeMean = Mean(p99FrameTimes),
                P99FrameTimeMin = p99FrameTimes.Min(),
                P99FrameTimeMax = p99FrameTimes.Max(),
                P99FrameTimeCoV = CoV(p99FrameTimes),

                JankPercentMean = Mean(jankPercents),
                JankPercentMin = jankPercents.Min(),
                JankPercentMax = jankPercents.Max(),

                GcAllocMeanKB = Mean(gcAllocs),
                GcAllocMinKB = gcAllocs.Min(),
                GcAllocMaxKB = gcAllocs.Max(),
            };

            summary.AvgFrameTimeSpreadPercent = summary.AvgFrameTimeMean > 0
                ? (summary.AvgFrameTimeSpread / summary.AvgFrameTimeMean) * 100f : 0f;
            summary.AvgFpsSpreadPercent = summary.AvgFpsMean > 0
                ? (summary.AvgFpsSpread / summary.AvgFpsMean) * 100f : 0f;

            if (drawCalls.Count > 0)
            {
                summary.DrawCallsMean = Mean(drawCalls);
                summary.DrawCallsMin = drawCalls.Min();
                summary.DrawCallsMax = drawCalls.Max();
            }

            return summary;
        }

        // ── Stats helpers ───────────────────────────────────────────────────

        static float Mean(List<float> values)
        {
            return values.Count == 0 ? 0f : values.Average();
        }

        static float StdDev(List<float> values)
        {
            if (values.Count < 2) return 0f;
            float mean = values.Average();
            float variance = values.Sum(v => (v - mean) * (v - mean)) / values.Count;
            return Mathf.Sqrt(variance);
        }

        /// <summary>
        /// Coefficient of Variation = StdDev / Mean.
        /// Lower is better for reproducibility. < 0.02 (2%) is excellent.
        /// </summary>
        static float CoV(List<float> values)
        {
            if (values.Count < 2) return 0f;
            float mean = values.Average();
            if (Mathf.Approximately(mean, 0f)) return 0f;
            return StdDev(values) / Mathf.Abs(mean);
        }
    }
}
