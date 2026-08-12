using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using CosmicShore.Utility;
using UnityEditor;
using UnityEngine;
using CosmicShore.Editor.Froglet;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Aggregates every PrismExplosionBenchmark run JSON in
    /// <c>BenchmarkResults/PrismExplosion/</c> (project root, branch-independent)
    /// into a legacy-cpu vs gpu-clock comparison: per-run and per-variant summary
    /// stats, per-phase means, and a time-binned FPS envelope (mean/min/max per
    /// 100 ms bin across all runs) written as <c>report.md</c> + <c>curves.csv</c>.
    /// Run each branch's series first (same scene, same grid), then this.
    /// FrogletTools ▸ Performance ▸ Prism Grid Benchmark▸ Generate Comparison Report.
    /// </summary>
    public static class PrismExplosionBenchmarkReport
    {
        const float BinSeconds = 0.1f;

        [MenuItem("FrogletTools/Performance/Prism Grid Benchmark/Generate Comparison Report")]
        [FrogletTool(FrogletToolCategory.Performance, Importance = 4,
            Description = "Diff prism-grid benchmark runs into a comparison report.")]
        public static void Generate()
        {
            string dir = PrismExplosionBenchmark.OutputDirectory;
            if (!Directory.Exists(dir))
            {
                Debug.LogWarning($"[PrismBench] No results folder at {dir} — run a series first (Bench button / 'bench' command in the Prism Grid scene).");
                return;
            }

            var runs = new List<PrismExplosionBenchmark.RunResult>();
            foreach (var file in Directory.GetFiles(dir, "*.json"))
            {
                try
                {
                    var r = JsonUtility.FromJson<PrismExplosionBenchmark.RunResult>(File.ReadAllText(file));
                    if (r == null || r.deltaTimes == null || r.deltaTimes.Length <= r.preRollFrames)
                        Debug.LogWarning($"[PrismBench] Skipping malformed run file {Path.GetFileName(file)}");
                    else if (r.prismTotal <= 0)
                        Debug.LogWarning($"[PrismBench] Skipping EMPTY-lattice run {Path.GetFileName(file)} " +
                                         "(recorded before the empty-lattice guard existed — delete it)");
                    else
                        runs.Add(r);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[PrismBench] Skipping unreadable {Path.GetFileName(file)}: {e.Message}");
                }
            }

            if (runs.Count == 0)
            {
                Debug.LogWarning($"[PrismBench] No valid run files in {dir}.");
                return;
            }

            var variants = runs.GroupBy(r => r.variant).OrderBy(g => g.Key).ToList();

            var md = new StringBuilder();
            md.AppendLine("# Prism Grid Explosion — FPS Envelope Comparison");
            md.AppendLine();
            md.AppendLine($"Generated {DateTime.UtcNow:o}. Bin width {BinSeconds * 1000:F0} ms. " +
                          "t = 0 is the detonation frame; negative t is the resting-lattice pre-roll baseline.");
            md.AppendLine();

            // ── Setup comparability check ────────────────────────────────────
            var setups = runs.Select(r => (r.prismTotal, r.countsX, r.countsY, r.countsZ,
                    r.gapX, r.gapY, r.gapZ, r.blastRadius, r.explosionDuration, r.throttlesLifted))
                .Distinct().ToList();
            if (setups.Count > 1)
            {
                md.AppendLine("> ⚠ **Runs use differing setups** — the comparison is only apples-to-apples per setup:");
                foreach (var s in setups)
                    md.AppendLine($"> - {s.countsX}×{s.countsY}×{s.countsZ} gaps {s.gapX:F1}/{s.gapY:F1}/{s.gapZ:F1} " +
                                  $"→ {s.prismTotal:N0} prisms, blast r {s.blastRadius:F0} in {s.explosionDuration:F1}s, " +
                                  $"throttles {(s.throttlesLifted ? "lifted" : "default")}");
                md.AppendLine();
            }
            if (runs.Select(r => r.throttlesLifted).Distinct().Count() > 1)
            {
                md.AppendLine("> ⚠ **Runs MIX lifted and default safety throttles** — throttled runs " +
                              "spread destruction over ~19s (48/frame) while lifted runs land it inside the " +
                              "wavefront; their envelopes measure different workloads. Re-run one side.");
                md.AppendLine();
            }

            // ── Per-variant sections ─────────────────────────────────────────
            md.AppendLine("## Summary");
            md.AppendLine();
            md.AppendLine("| variant | branch | runs | prisms | baseline FPS | mean FPS | ±sd | p50 FPS | 1% low | min FPS |");
            md.AppendLine("|---|---|---|---|---|---|---|---|---|---|");

            var variantAgg = new Dictionary<string, (double mean, double onePct, double baseline)>();

            foreach (var g in variants)
            {
                var stats = g.Select(RunStats).ToList();
                double meanOfMeans = stats.Average(s => s.meanFps);
                double sd = Math.Sqrt(stats.Average(s => (s.meanFps - meanOfMeans) * (s.meanFps - meanOfMeans)));
                double p50 = stats.Average(s => s.p50Fps);
                double onePct = stats.Average(s => s.onePctLowFps);
                double minFps = stats.Min(s => s.minFps);
                double baseline = stats.Average(s => s.baselineFps);
                string branch = string.Join(", ", g.Select(r => r.branch).Distinct());
                string prisms = string.Join(", ", g.Select(r => r.prismTotal).Distinct().Select(p => p.ToString("N0")));

                variantAgg[g.Key] = (meanOfMeans, onePct, baseline);
                md.AppendLine($"| {g.Key} | {branch} | {g.Count()} | {prisms} | {baseline:F1} | " +
                              $"**{meanOfMeans:F1}** | {sd:F1} | {p50:F1} | {onePct:F1} | {minFps:F1} |");
            }
            md.AppendLine();

            // ── Delta section (exactly two variants) ─────────────────────────
            if (variantAgg.Count == 2)
            {
                var a = variantAgg.First(); // alphabetical: gpu-clock before legacy-cpu
                var b = variantAgg.Last();
                // Present as new-vs-old regardless of alphabetical order.
                var newer = a.Key == "gpu-clock" ? a : b;
                var older = a.Key == "gpu-clock" ? b : a;
                md.AppendLine("## Delta (gpu-clock vs legacy-cpu)");
                md.AppendLine();
                md.AppendLine($"- Explosion-window mean FPS: **{newer.Value.mean:F1} vs {older.Value.mean:F1}** " +
                              $"({Pct(newer.Value.mean, older.Value.mean)})");
                md.AppendLine($"- 1% low: **{newer.Value.onePct:F1} vs {older.Value.onePct:F1}** " +
                              $"({Pct(newer.Value.onePct, older.Value.onePct)})");
                md.AppendLine($"- Pre-roll baseline: {newer.Value.baseline:F1} vs {older.Value.baseline:F1} " +
                              $"({Pct(newer.Value.baseline, older.Value.baseline)}) — a baseline gap means the " +
                              "difference is not explosion-specific; judge the explosion cost as (baseline − window).");
                md.AppendLine();
            }

            // ── Phase table ──────────────────────────────────────────────────
            (float lo, float hi, string label)[] phases =
            {
                (-99f, 0f,  "pre-roll (baseline)"),
                (0f,   1f,  "0–1 s (blast + debris launch)"),
                (1f,   3f,  "1–3 s (mid-flight)"),
                (3f,   99f, "3 s–end (fade tail)"),
            };
            md.AppendLine("## Phase means (FPS)");
            md.AppendLine();
            md.Append("| phase |");
            foreach (var g in variants) md.Append($" {g.Key} |");
            md.AppendLine();
            md.Append("|---|");
            foreach (var _ in variants) md.Append("---|");
            md.AppendLine();
            foreach (var (lo, hi, label) in phases)
            {
                md.Append($"| {label} |");
                foreach (var g in variants)
                {
                    double frames = 0, seconds = 0;
                    foreach (var r in g)
                        foreach (var (t, dt) in Samples(r))
                            if (t >= lo && t < hi) { frames += 1; seconds += dt; }
                    md.Append(seconds > 0 ? $" {frames / seconds:F1} |" : " — |");
                }
                md.AppendLine();
            }
            md.AppendLine();

            // ── Per-run table ────────────────────────────────────────────────
            md.AppendLine("## Runs");
            md.AppendLine();
            md.AppendLine("| variant | series | run | frames | baseline FPS | mean FPS | p50 | 1% low | min |");
            md.AppendLine("|---|---|---|---|---|---|---|---|---|");
            foreach (var r in runs.OrderBy(r => r.variant).ThenBy(r => r.series).ThenBy(r => r.runIndex))
            {
                var s = RunStats(r);
                md.AppendLine($"| {r.variant} | {r.series} | {r.runIndex} | {s.frames} | {s.baselineFps:F1} | " +
                              $"{s.meanFps:F1} | {s.p50Fps:F1} | {s.onePctLowFps:F1} | {s.minFps:F1} |");
            }
            md.AppendLine();
            md.AppendLine("Full envelope: `curves.csv` (per-variant mean/min/max FPS per 100 ms bin — " +
                          "plot t on X, FPS on Y; the min/max band across repeated runs IS the envelope).");

            // ── Binned envelope CSV ──────────────────────────────────────────
            float tMin = -runs.Max(r => r.preRollSeconds);
            float tMax = runs.Max(r => r.windowSeconds);
            int binCount = Mathf.CeilToInt((tMax - tMin) / BinSeconds);
            var csv = new StringBuilder();
            csv.Append("binStartSeconds");
            foreach (var g in variants) csv.Append($",{g.Key}_meanFps,{g.Key}_minFps,{g.Key}_maxFps,{g.Key}_samples");
            csv.AppendLine();

            var perVariantBins = variants.ToDictionary(g => g.Key, g =>
            {
                var bins = new List<float>[binCount];
                for (int i = 0; i < binCount; i++) bins[i] = new List<float>();
                foreach (var r in g)
                    foreach (var (t, dt) in Samples(r))
                    {
                        int bin = Mathf.FloorToInt((t - tMin) / BinSeconds);
                        if (bin >= 0 && bin < binCount && dt > 0f) bins[bin].Add(1f / dt);
                    }
                return bins;
            });

            for (int i = 0; i < binCount; i++)
            {
                csv.Append((tMin + i * BinSeconds).ToString("F2", CultureInfo.InvariantCulture));
                foreach (var g in variants)
                {
                    var bin = perVariantBins[g.Key][i];
                    csv.Append(bin.Count > 0
                        ? $",{bin.Average():F2},{bin.Min():F2},{bin.Max():F2},{bin.Count}"
                        : ",,,,0");
                }
                csv.AppendLine();
            }

            string reportPath = Path.Combine(dir, "report.md");
            File.WriteAllText(reportPath, md.ToString());
            File.WriteAllText(Path.Combine(dir, "curves.csv"), csv.ToString());

            Debug.Log($"[PrismBench] Report written: {reportPath} ({runs.Count} runs, " +
                      $"{variants.Count} variant(s): {string.Join(", ", variants.Select(g => $"{g.Key}×{g.Count()}"))})");
            EditorUtility.RevealInFinder(reportPath);
        }

        [MenuItem("FrogletTools/Performance/Prism Grid Benchmark/Open Results Folder")]
        [FrogletTool(FrogletToolCategory.Performance, Importance = 2,
            Description = "Open the benchmark results directory.")]
        public static void OpenFolder()
        {
            Directory.CreateDirectory(PrismExplosionBenchmark.OutputDirectory);
            EditorUtility.RevealInFinder(PrismExplosionBenchmark.OutputDirectory);
        }

        static string Pct(double a, double b) =>
            b <= 0 ? "n/a" : $"{(a / b - 1.0) * 100.0:+0.0;-0.0}%";

        /// <summary>(time-relative-to-detonation, deltaTime) per frame. Pre-roll frames
        /// have negative time; times use the run's OWN measured pre-roll duration.</summary>
        static IEnumerable<(float t, float dt)> Samples(PrismExplosionBenchmark.RunResult r)
        {
            float preSum = 0f;
            for (int i = 0; i < r.preRollFrames; i++) preSum += r.deltaTimes[i];
            float cum = 0f;
            for (int i = 0; i < r.deltaTimes.Length; i++)
            {
                yield return (cum - preSum, r.deltaTimes[i]);
                cum += r.deltaTimes[i];
            }
        }

        struct Stats
        {
            public int frames;
            public double meanFps, p50Fps, onePctLowFps, minFps, baselineFps;
        }

        static Stats RunStats(PrismExplosionBenchmark.RunResult r)
        {
            var window = new List<float>(r.deltaTimes.Length);
            double windowSum = 0, preSum = 0;
            int preFrames = r.preRollFrames;
            for (int i = 0; i < r.deltaTimes.Length; i++)
            {
                if (i < preFrames) preSum += r.deltaTimes[i];
                else { window.Add(r.deltaTimes[i]); windowSum += r.deltaTimes[i]; }
            }

            var sorted = window.OrderByDescending(dt => dt).ToList(); // worst (longest) first
            int onePctCount = Mathf.Max(1, sorted.Count / 100);
            double onePctMeanDt = sorted.Take(onePctCount).Average();
            double medianDt = sorted[sorted.Count / 2];

            return new Stats
            {
                frames = window.Count,
                meanFps = windowSum > 0 ? window.Count / windowSum : 0,
                p50Fps = medianDt > 0 ? 1.0 / medianDt : 0,
                onePctLowFps = onePctMeanDt > 0 ? 1.0 / onePctMeanDt : 0,
                minFps = sorted.Count > 0 && sorted[0] > 0 ? 1.0 / sorted[0] : 0,
                baselineFps = preSum > 0 ? preFrames / preSum : 0,
            };
        }
    }
}
