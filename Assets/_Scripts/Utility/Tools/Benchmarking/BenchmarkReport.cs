using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace CosmicShore.Utility.Tools.Benchmarking
{
    /// <summary>
    /// Immutable snapshot of a benchmark run. Serializable to JSON for persistence.
    /// </summary>
    [Serializable]
    public class BenchmarkReport
    {
        // ── Metadata ────────────────────────────────────────────────────────
        public string Label;
        public string GitCommit;
        public string SceneName;
        public string Timestamp;
        public float DurationSeconds;
        public int TotalFrames;

        // ── Deterministic settings ────────────────────────────────────────
        public bool Deterministic;
        public int DeterministicSeed;

        // ── Frame Time (ms) ─────────────────────────────────────────────────
        public float AvgFrameTimeMs;
        public float MinFrameTimeMs;
        public float MaxFrameTimeMs;
        public float MedianFrameTimeMs;
        public float P1FrameTimeMs;
        public float P5FrameTimeMs;
        public float P95FrameTimeMs;
        public float P99FrameTimeMs;
        public float StdDevFrameTimeMs;

        // ── FPS (derived) ───────────────────────────────────────────────────
        public float AvgFps;
        public float P1Fps;
        public float P5Fps;

        // ── GPU (when available via FrameTimingManager) ─────────────────────
        public float AvgGpuTimeMs;

        // ── Memory / GC ─────────────────────────────────────────────────────
        public long TotalGcAllocBytes;
        public int GcCollectCount;
        public long PeakUsedMemoryBytes;

        // ── Render stats ────────────────────────────────────────────────────
        public long AvgDrawCalls;
        public long AvgTriangles;
        public long AvgVertices;
        public long AvgSetPassCalls;

        // ── Raw frame times (for histogram / custom analysis) ───────────────
        public List<float> FrameTimeSamples = new();

        // ── Jank metric ─────────────────────────────────────────────────────
        /// <summary>Percentage of frames that exceeded 2x the average frame time.</summary>
        public float JankPercent;

        // ── Construction ────────────────────────────────────────────────────

        public static BenchmarkReport Build(
            string label,
            string sceneName,
            float duration,
            List<float> frameTimesMs,
            List<float> gpuTimesMs,
            long gcAllocBytes,
            int gcCollectCount,
            long peakMemory,
            List<long> drawCalls,
            List<long> triangles,
            List<long> vertices,
            List<long> setPassCalls,
            bool deterministic = false,
            int deterministicSeed = 0)
        {
            var sorted = frameTimesMs.OrderBy(t => t).ToList();
            int count = sorted.Count;
            if (count == 0) return null;

            float avg = sorted.Average();
            float variance = sorted.Sum(t => (t - avg) * (t - avg)) / count;

            var report = new BenchmarkReport
            {
                Label = label,
                SceneName = sceneName,
                Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                DurationSeconds = duration,
                TotalFrames = count,
                AvgFrameTimeMs = avg,
                MinFrameTimeMs = sorted[0],
                MaxFrameTimeMs = sorted[count - 1],
                MedianFrameTimeMs = Percentile(sorted, 50f),
                P1FrameTimeMs = Percentile(sorted, 1f),
                P5FrameTimeMs = Percentile(sorted, 5f),
                P95FrameTimeMs = Percentile(sorted, 95f),
                P99FrameTimeMs = Percentile(sorted, 99f),
                StdDevFrameTimeMs = Mathf.Sqrt(variance),
                AvgFps = 1000f / avg,
                P1Fps = 1000f / Percentile(sorted, 99f),   // P1 FPS = worst 1% frame time
                P5Fps = 1000f / Percentile(sorted, 95f),
                AvgGpuTimeMs = gpuTimesMs is { Count: > 0 } ? gpuTimesMs.Average() : -1f,
                TotalGcAllocBytes = gcAllocBytes,
                GcCollectCount = gcCollectCount,
                PeakUsedMemoryBytes = peakMemory,
                AvgDrawCalls = drawCalls is { Count: > 0 } ? (long)drawCalls.Average() : -1,
                AvgTriangles = triangles is { Count: > 0 } ? (long)triangles.Average() : -1,
                AvgVertices = vertices is { Count: > 0 } ? (long)vertices.Average() : -1,
                AvgSetPassCalls = setPassCalls is { Count: > 0 } ? (long)setPassCalls.Average() : -1,
                FrameTimeSamples = frameTimesMs,
                JankPercent = 100f * sorted.Count(t => t > avg * 2f) / count,
            };

            // Deterministic metadata
            report.Deterministic = deterministic;
            report.DeterministicSeed = deterministicSeed;

            // Try to grab git commit hash
            report.GitCommit = GetGitCommit();

            return report;
        }

        // ── Persistence ─────────────────────────────────────────────────────

        static readonly string ReportsDir = Path.Combine(Application.dataPath, "..", "BenchmarkReports");

        public string Save()
        {
            Directory.CreateDirectory(ReportsDir);
            string safeName = Label.Replace(" ", "_").Replace("/", "-");
            string filename = $"{safeName}_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            string path = Path.Combine(ReportsDir, filename);
            File.WriteAllText(path, JsonUtility.ToJson(this, true));
            return path;
        }

        public static BenchmarkReport Load(string path)
        {
            if (!File.Exists(path)) return null;
            return JsonUtility.FromJson<BenchmarkReport>(File.ReadAllText(path));
        }

        public static string[] GetSavedReportPaths()
        {
            if (!Directory.Exists(ReportsDir)) return Array.Empty<string>();
            return Directory.GetFiles(ReportsDir, "*.json");
        }

        // ── Comparison ──────────────────────────────────────────────────────

        /// <summary>
        /// Returns a list of human-readable comparison lines between two reports.
        /// Positive delta = regression, negative = improvement (for frame time metrics).
        /// </summary>
        public static List<ComparisonLine> Compare(BenchmarkReport baseline, BenchmarkReport current)
        {
            var lines = new List<ComparisonLine>();

            AddLine(lines, "Avg Frame Time", "ms", baseline.AvgFrameTimeMs, current.AvgFrameTimeMs, lowerIsBetter: true);
            AddLine(lines, "Median Frame Time", "ms", baseline.MedianFrameTimeMs, current.MedianFrameTimeMs, lowerIsBetter: true);
            AddLine(lines, "P95 Frame Time", "ms", baseline.P95FrameTimeMs, current.P95FrameTimeMs, lowerIsBetter: true);
            AddLine(lines, "P99 Frame Time", "ms", baseline.P99FrameTimeMs, current.P99FrameTimeMs, lowerIsBetter: true);
            AddLine(lines, "Std Dev", "ms", baseline.StdDevFrameTimeMs, current.StdDevFrameTimeMs, lowerIsBetter: true);
            AddLine(lines, "Avg FPS", "", baseline.AvgFps, current.AvgFps, lowerIsBetter: false);
            AddLine(lines, "P1 FPS", "", baseline.P1Fps, current.P1Fps, lowerIsBetter: false);
            AddLine(lines, "P5 FPS", "", baseline.P5Fps, current.P5Fps, lowerIsBetter: false);
            AddLine(lines, "Jank %", "%", baseline.JankPercent, current.JankPercent, lowerIsBetter: true);

            if (baseline.AvgGpuTimeMs >= 0 && current.AvgGpuTimeMs >= 0)
                AddLine(lines, "Avg GPU Time", "ms", baseline.AvgGpuTimeMs, current.AvgGpuTimeMs, lowerIsBetter: true);

            AddLine(lines, "GC Alloc", "KB", baseline.TotalGcAllocBytes / 1024f, current.TotalGcAllocBytes / 1024f, lowerIsBetter: true);
            AddLine(lines, "GC Collections", "", baseline.GcCollectCount, current.GcCollectCount, lowerIsBetter: true);
            AddLine(lines, "Peak Memory", "MB", baseline.PeakUsedMemoryBytes / (1024f * 1024f), current.PeakUsedMemoryBytes / (1024f * 1024f), lowerIsBetter: true);

            if (baseline.AvgDrawCalls >= 0 && current.AvgDrawCalls >= 0)
                AddLine(lines, "Draw Calls", "", baseline.AvgDrawCalls, current.AvgDrawCalls, lowerIsBetter: true);
            if (baseline.AvgSetPassCalls >= 0 && current.AvgSetPassCalls >= 0)
                AddLine(lines, "SetPass Calls", "", baseline.AvgSetPassCalls, current.AvgSetPassCalls, lowerIsBetter: true);
            if (baseline.AvgTriangles >= 0 && current.AvgTriangles >= 0)
                AddLine(lines, "Triangles", "K", baseline.AvgTriangles / 1000f, current.AvgTriangles / 1000f, lowerIsBetter: true);

            return lines;
        }

        // ── Helpers ─────────────────────────────────────────────────────────

        static float Percentile(List<float> sorted, float p)
        {
            float idx = (p / 100f) * (sorted.Count - 1);
            int lower = Mathf.FloorToInt(idx);
            int upper = Mathf.CeilToInt(idx);
            if (lower == upper) return sorted[lower];
            float frac = idx - lower;
            return sorted[lower] * (1f - frac) + sorted[upper] * frac;
        }

        static void AddLine(List<ComparisonLine> lines, string label, string unit, float baseline, float current, bool lowerIsBetter)
        {
            float delta = current - baseline;
            float pct = baseline != 0 ? (delta / baseline) * 100f : 0f;
            bool improved = lowerIsBetter ? delta < 0 : delta > 0;
            bool regressed = lowerIsBetter ? delta > 0 : delta < 0;

            lines.Add(new ComparisonLine
            {
                Label = label,
                Unit = unit,
                Baseline = baseline,
                Current = current,
                Delta = delta,
                DeltaPercent = pct,
                Improved = improved,
                Regressed = regressed
            });
        }

        static string GetGitCommit()
        {
            try
            {
                var proc = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "git",
                        Arguments = "rev-parse --short HEAD",
                        WorkingDirectory = Application.dataPath,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };
                proc.Start();
                string output = proc.StandardOutput.ReadToEnd().Trim();
                proc.WaitForExit();
                return output;
            }
            catch
            {
                return "unknown";
            }
        }
    }

    [Serializable]
    public struct ComparisonLine
    {
        public string Label;
        public string Unit;
        public float Baseline;
        public float Current;
        public float Delta;
        public float DeltaPercent;
        public bool Improved;
        public bool Regressed;
    }
}
