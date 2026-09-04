using System;
using System.Collections.Generic;

namespace CosmicShore.Utility.PerformanceBenchmark
{
    public enum HintSeverity { Info = 0, Warning = 1, Blocker = 2 }

    /// <summary>One sampled profiler marker on a spike frame.</summary>
    [Serializable]
    public struct MarkerSample
    {
        public string name;
        public float ms;        // self time (ms) on the main thread
        public bool isScript;   // true when the sample is a gameplay script method / coroutine / CS marker
    }

    /// <summary>A frame whose time exceeded the spike threshold, with the markers that cost the most.</summary>
    [Serializable]
    public class SpikeEntry
    {
        public int frameIndex;
        public float frameTimeMs;
        public float cpuFrameTimeMs;
        public float gpuFrameTimeMs;
        public List<MarkerSample> topMarkers = new();

        // The profiler frame to read the script breakdown from. The runtime collector records
        // this cheaply; the editor window enriches topMarkers off the game thread so the
        // hierarchy walk (70-90ms) never inflates the captured frame. -1 = unknown / no profiler.
        public int profilerFrameIndex = -1;
    }

    /// <summary>An actionable finding: what's wrong, how bad, and what to do about it.</summary>
    [Serializable]
    public class BenchmarkHint
    {
        public string id;
        public HintSeverity severity;
        public string title;
        public string finding;     // measured, run-specific ("GC ≈ 12.3 KB/frame")
        public string fixAdvice;   // what to do about it
        public string matchedMarker; // profiler marker that triggered a marker-rule (optional)
    }

    public enum HintRuleType
    {
        GcPerFrameKb = 0,        // (totalGc/frames)/1024 > threshold
        MemorySlopeKbPerFrame = 1, // leak heuristic: slope/1024 > threshold
        AvgDrawCalls = 2,        // avgDrawCalls > threshold
        GpuBound = 3,            // avgGpu > avgCpu * threshold (and gpu measured)
        CpuBound = 4,            // avgCpu > avgGpu * threshold (and cpu measured)
        FrameInstability = 5,    // stdDevFrameTimeMs > threshold
        SpikeMarkerName = 6,     // any spike top-marker name contains markerPattern
        NetcodeSharePercent = 7, // netcodeSharePercent > threshold
        RpcsPerFrame = 8         // avgRpcsSent > threshold
    }

    /// <summary>
    /// A single customizable hint rule. Plain serializable so it backs both the
    /// <see cref="BenchmarkHintRulesSO"/> asset and the built-in defaults.
    /// </summary>
    [Serializable]
    public class HintRule
    {
        public bool enabled = true;
        public string id;
        public HintRuleType type;
        public float threshold;
        public string markerPattern = "";
        public HintSeverity severity = HintSeverity.Warning;
        public string title;
        public string fixAdvice;
    }

    [Serializable]
    public class BenchmarkAnalysisResult
    {
        public int score;        // 0-100
        public bool isBlocked;   // any Blocker-severity hint fired
        public string boundVerdict; // "CPU-bound" / "GPU-bound" / "Balanced" / "Unknown"
        public List<BenchmarkHint> hints = new();
    }

    /// <summary>
    /// Runtime-safe scoring + rule-based hint engine. Evaluates a run's statistics and
    /// captured spikes against a set of <see cref="HintRule"/>s (customizable via
    /// <see cref="BenchmarkHintRulesSO"/>, falling back to <see cref="DefaultRules"/>).
    /// </summary>
    public static class BenchmarkAnalysis
    {
        // Score targets (mobile-first). Kept here as the single tuning point.
        const float TargetFps = 60f;
        const float TargetP99Ms = 33.3f;
        const float TargetStdDevMs = 6f;
        const float GcBudgetKbPerFrame = 4f;

        public static BenchmarkAnalysisResult Analyze(BenchmarkReport report, IList<HintRule> rules)
        {
            var result = new BenchmarkAnalysisResult();
            var stats = report?.statistics;

            if (stats == null || stats.totalFrames == 0)
            {
                result.score = 0;
                result.boundVerdict = "Unknown";
                return result;
            }

            result.score = ComputeScore(stats);
            result.boundVerdict = BoundVerdict(stats);

            var spikes = report.spikes ?? new List<SpikeEntry>();
            foreach (var rule in (rules != null && rules.Count > 0) ? rules : DefaultRules())
            {
                if (rule == null || !rule.enabled) continue;
                if (TryEvaluate(rule, stats, spikes, out var hint))
                {
                    result.hints.Add(hint);
                    if (hint.severity == HintSeverity.Blocker) result.isBlocked = true;
                }
            }

            return result;
        }

        public static int ComputeScore(BenchmarkStatistics s)
        {
            if (s == null || s.totalFrames == 0) return 0;

            float fpsScore = Clamp01(s.avgFps / TargetFps);
            float p99Score = Clamp01(TargetP99Ms / Math.Max(s.p99FrameTimeMs, 0.001f));
            float stabScore = Clamp01(1f - s.stdDevFrameTimeMs / (TargetStdDevMs * 3f));
            float gcKb = GcPerFrameKb(s);
            float gcScore = Clamp01(1f - gcKb / (GcBudgetKbPerFrame * 4f));

            float blended = 0.40f * fpsScore + 0.25f * p99Score + 0.20f * stabScore + 0.15f * gcScore;
            int score = (int)Math.Round(blended * 100f);
            return Math.Max(0, Math.Min(100, score));
        }

        static string BoundVerdict(BenchmarkStatistics s) =>
            FrameBoundness.Classify(s.avgCpuFrameTimeMs, s.avgGpuFrameTimeMs);

        static bool TryEvaluate(HintRule rule, BenchmarkStatistics s, List<SpikeEntry> spikes, out BenchmarkHint hint)
        {
            hint = null;
            string finding = null;
            string matchedMarker = null;

            switch (rule.type)
            {
                case HintRuleType.GcPerFrameKb:
                {
                    float kb = GcPerFrameKb(s);
                    if (kb <= rule.threshold) return false;
                    finding = $"GC ≈ {kb:F1} KB/frame (budget {rule.threshold:F0} KB).";
                    break;
                }
                case HintRuleType.MemorySlopeKbPerFrame:
                {
                    float kb = s.memorySlopeBytesPerFrame / 1024f;
                    if (kb <= rule.threshold) return false;
                    finding = $"Allocated memory rising ≈ {kb:F1} KB/frame across the sample - possible leak.";
                    break;
                }
                case HintRuleType.AvgDrawCalls:
                {
                    if (s.avgDrawCalls <= rule.threshold) return false;
                    finding = $"{s.avgDrawCalls:F0} draw calls/frame (threshold {rule.threshold:F0}).";
                    break;
                }
                case HintRuleType.GpuBound:
                {
                    if (s.avgGpuFrameTimeMs <= 0.001f) return false;
                    if (s.avgGpuFrameTimeMs <= s.avgCpuFrameTimeMs * rule.threshold) return false;
                    finding = $"GPU {s.avgGpuFrameTimeMs:F1} ms vs CPU {s.avgCpuFrameTimeMs:F1} ms - GPU-bound.";
                    break;
                }
                case HintRuleType.CpuBound:
                {
                    if (s.avgCpuFrameTimeMs <= 0.001f) return false;
                    if (s.avgCpuFrameTimeMs <= s.avgGpuFrameTimeMs * rule.threshold) return false;
                    finding = $"CPU {s.avgCpuFrameTimeMs:F1} ms vs GPU {s.avgGpuFrameTimeMs:F1} ms - CPU-bound.";
                    break;
                }
                case HintRuleType.FrameInstability:
                {
                    if (s.stdDevFrameTimeMs <= rule.threshold) return false;
                    finding = $"Frame-time std-dev {s.stdDevFrameTimeMs:F1} ms (threshold {rule.threshold:F0} ms) - hitching.";
                    break;
                }
                case HintRuleType.NetcodeSharePercent:
                {
                    if (s.netcodeSharePercent <= rule.threshold) return false;
                    finding = $"Netcode is {s.netcodeSharePercent:F0}% of frame time ({s.avgNetcodeTimeMs:F2} ms/frame).";
                    break;
                }
                case HintRuleType.RpcsPerFrame:
                {
                    if (s.avgRpcsSent <= rule.threshold) return false;
                    finding = $"{s.avgRpcsSent:F0} RPCs/frame (threshold {rule.threshold:F0}); NetVars dirty {s.avgNetVarsDirty:F0}/frame.";
                    break;
                }
                case HintRuleType.SpikeMarkerName:
                {
                    if (string.IsNullOrEmpty(rule.markerPattern)) return false;
                    float worstMs = 0f;
                    foreach (var spike in spikes)
                    {
                        if (spike?.topMarkers == null) continue;
                        foreach (var m in spike.topMarkers)
                        {
                            if (!string.IsNullOrEmpty(m.name) &&
                                m.name.IndexOf(rule.markerPattern, StringComparison.OrdinalIgnoreCase) >= 0 &&
                                m.ms > worstMs)
                            {
                                worstMs = m.ms;
                                matchedMarker = m.name;
                            }
                        }
                    }
                    if (matchedMarker == null) return false;
                    finding = $"Spike marker '{matchedMarker}' cost up to {worstMs:F1} ms.";
                    break;
                }
                default:
                    return false;
            }

            hint = new BenchmarkHint
            {
                id = rule.id,
                severity = rule.severity,
                title = rule.title,
                finding = finding,
                fixAdvice = rule.fixAdvice,
                matchedMarker = matchedMarker
            };
            return true;
        }

        static float GcPerFrameKb(BenchmarkStatistics s) =>
            s.totalFrames > 0 ? (s.totalGcAllocated / (float)s.totalFrames) / 1024f : 0f;

        static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

        /// <summary>
        /// Built-in defaults, grounded in this project's documented anti-patterns. Used when no
        /// <see cref="BenchmarkHintRulesSO"/> is supplied. Mirror this list when authoring the asset.
        /// </summary>
        public static List<HintRule> DefaultRules() => new()
        {
            new HintRule
            {
                id = "gc-pressure", type = HintRuleType.GcPerFrameKb, threshold = 4f, severity = HintSeverity.Warning,
                title = "GC pressure",
                fixAdvice = "Per-frame managed allocations are feeding the GC. Pool objects, avoid LINQ / new collections / " +
                            "string concatenation in Update, and prefer sharedMaterial + MaterialPropertyBlock over .material."
            },
            new HintRule
            {
                id = "memory-leak", type = HintRuleType.MemorySlopeKbPerFrame, threshold = 8f, severity = HintSeverity.Blocker,
                title = "Possible memory leak",
                fixAdvice = "Allocated memory climbs steadily through the run. Check for unbounded lists/trails, Instantiate " +
                            "without pooling/Destroy, un-disposed NativeArrays, or event handlers that never unsubscribe."
            },
            new HintRule
            {
                id = "draw-calls", type = HintRuleType.AvgDrawCalls, threshold = 1500f, severity = HintSeverity.Warning,
                title = "High draw calls",
                fixAdvice = "Reduce unique materials, enable GPU instancing / static batching, and use MaterialPropertyBlock " +
                            "instead of per-object material clones so renderers batch."
            },
            new HintRule
            {
                id = "gpu-bound", type = HintRuleType.GpuBound, threshold = 1.25f, severity = HintSeverity.Info,
                title = "GPU-bound",
                fixAdvice = "GPU time dominates. Look at overdraw/transparency, shader complexity, render-target resolution, " +
                            "and post-processing before optimizing CPU code."
            },
            new HintRule
            {
                id = "cpu-bound", type = HintRuleType.CpuBound, threshold = 1.25f, severity = HintSeverity.Info,
                title = "CPU-bound",
                fixAdvice = "CPU time dominates. Profile scripts, physics, and per-frame Find/GetComponent; move heavy work to " +
                            "Jobs/Burst or amortize across frames."
            },
            new HintRule
            {
                id = "instability", type = HintRuleType.FrameInstability, threshold = 6f, severity = HintSeverity.Warning,
                title = "Frame-time instability",
                fixAdvice = "Frame time varies a lot (visible hitching). Hunt periodic spikes - GC collections, synchronous " +
                            "loads, pool growth, or burst spawns - and spread or pre-warm them."
            },
            new HintRule
            {
                id = "netcode-share", type = HintRuleType.NetcodeSharePercent, threshold = 15f, severity = HintSeverity.Warning,
                title = "Netcode-heavy frame",
                fixAdvice = "Netcode markers dominate a notable share of the frame. Reduce per-frame NetworkVariable writes " +
                            "(send on change / at the network tick, not every frame), trim OnSerialize payloads, and avoid " +
                            "per-input RPCs - batch or rate-limit them."
            },
            new HintRule
            {
                id = "rpc-flood", type = HintRuleType.RpcsPerFrame, threshold = 20f, severity = HintSeverity.Warning,
                title = "High RPC volume",
                fixAdvice = "Many RPCs per frame. Coalesce input/state RPCs, send at the network tick rate rather than per " +
                            "frame, and prefer NetworkVariables (delta-compressed) over RPCs for continuous state."
            },
            new HintRule
            {
                id = "gc-collect-spike", type = HintRuleType.SpikeMarkerName, markerPattern = "GC.Collect", severity = HintSeverity.Warning,
                title = "GC collection spike",
                fixAdvice = "A garbage collection ran mid-frame. Eliminate per-frame allocations so the GC has nothing to collect " +
                            "during gameplay (pooling, no LINQ/new in hot paths)."
            },
            new HintRule
            {
                id = "physics-spike", type = HintRuleType.SpikeMarkerName, markerPattern = "Physics", severity = HintSeverity.Warning,
                title = "Physics spike",
                fixAdvice = "Physics stepped expensively this frame. Reduce active rigidbodies/colliders, simplify collision " +
                            "shapes, or replace broad-phase queries with a spatial hash (see prism AOE registry)."
            },
            new HintRule
            {
                id = "canvas-rebuild-spike", type = HintRuleType.SpikeMarkerName, markerPattern = "Canvas", severity = HintSeverity.Warning,
                title = "UI canvas rebuild spike",
                fixAdvice = "A Canvas rebuilt mid-frame. Split static and dynamic UI onto separate canvases and avoid changing " +
                            "layout/text every frame."
            },
            new HintRule
            {
                id = "netcode-spike", type = HintRuleType.SpikeMarkerName, markerPattern = "CSM.Net", severity = HintSeverity.Warning,
                title = "Netcode spike",
                fixAdvice = "A netcode marker spiked this frame - usually a burst of spawns/despawns or a large serialize. " +
                            "Spread spawns across frames, pool networked objects, and shrink serialized payloads."
            },
        };
    }
}
