using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CosmicShore.Engine;
using CosmicShore.Engine.SceneManagement;

namespace CosmicShore.Player
{
    /// <summary>
    /// <c>--session-report PATH</c> (or COSMIC_SHORE_SESSION_REPORT): when the player closes - or
    /// crashes - it writes one JSON file describing the session: how long, which scenes and for how
    /// long, frame-time percentiles (overall and per scene), simulation and render CPU percentiles,
    /// the average cost of each loop phase, allocations and GC (collections, pause time), audio
    /// usage, every distinct error/warning/exception with its count, and the crash if there was one. The launcher keeps these and hands them to Claude ("analyse my last
    /// session"), so a play test becomes data an agent can act on.
    /// </summary>
    public static class SessionReport
    {
        static string s_path;
        static DateTime s_start;
        static readonly List<(string scene, int frame, double seconds)> s_scenes = new();
        static readonly int[] s_buckets = new int[201]; // frame ms histogram, 0.5 ms buckets to 100 ms
        static int s_frames;
        static readonly Dictionary<string, int[]> s_sceneBuckets = new();
        static readonly int[] s_simBuckets = new int[201], s_renderBuckets = new int[201];
        static long s_alloc0, s_allocLast, s_worstAlloc;
        static readonly int[] s_allocBuckets = new int[1025]; // per-frame allocation, 8 KB buckets to 8 MB
        static int s_gc0, s_gc1, s_gc2;
        static TimeSpan s_pause0, s_steadyPause0;
        static DateTime s_steady0;
        static int s_steadyGc0, s_steadyGc1, s_steadyGc2;
        static string s_currentScene = "";
        static double s_worstMs;
        static bool s_written;

        public static LogCounter Log;
        public static Func<int> Frame = () => s_frames;

        public static bool Enabled => s_path != null;

        public static void Begin(string path)
        {
            path ??= Environment.GetEnvironmentVariable("COSMIC_SHORE_SESSION_REPORT");
            if (string.IsNullOrWhiteSpace(path)) return;
            s_path = Path.GetFullPath(path);
            s_start = DateTime.UtcNow;
            s_alloc0 = s_allocLast = GC.GetTotalAllocatedBytes(false);
            (s_gc0, s_gc1, s_gc2, s_pause0) = (GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2), GC.GetTotalPauseDuration());
            CosmicShore.Engine.GameLoop.PhaseTiming = true; // cheap: a timestamp per loop phase
            SceneManager.activeSceneChanged += (_, next) =>
            {
                s_currentScene = next?.name ?? "";
                s_scenes.Add((s_currentScene, Frame(), (DateTime.UtcNow - s_start).TotalSeconds));
            };
            AppDomain.CurrentDomain.ProcessExit += (_, _) => Write("process exit");
        }

        /// <summary>One presented frame's wall time.</summary>
        public static void FrameTime(double ms)
        {
            if (s_path == null || ms <= 0) return;
            s_frames++;
            long alloc = GC.GetTotalAllocatedBytes(false);
            if (s_frames == 30) // steady state starts after the first frames' loading
                (s_steady0, s_steadyPause0, s_steadyGc0, s_steadyGc1, s_steadyGc2) =
                    (DateTime.UtcNow, GC.GetTotalPauseDuration(), GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2));
            if (s_frames > 30)
            {
                s_worstAlloc = Math.Max(s_worstAlloc, alloc - s_allocLast);
                s_allocBuckets[Math.Min(s_allocBuckets.Length - 1, (int)((alloc - s_allocLast) / 8192))]++;
            }
            s_allocLast = alloc;
            int bucket = Math.Min(s_buckets.Length - 1, (int)(ms * 2));
            s_buckets[bucket]++;
            if (s_frames > 30) // per scene, after the first frames' loading
            {
                if (!s_sceneBuckets.TryGetValue(s_currentScene, out var sb)) s_sceneBuckets[s_currentScene] = sb = new int[201];
                sb[bucket]++;
            }
            if (ms > s_worstMs && s_frames > 30) s_worstMs = ms; // the first frames are loading
        }

        /// <summary>One frame's simulation (engine tick) CPU time.</summary>
        public static void SimTime(double ms) { if (s_path != null && ms > 0) s_simBuckets[Math.Min(200, (int)(ms * 2))]++; }

        /// <summary>One frame's render CPU time (collect, draw submission, post, UI, present).</summary>
        public static void RenderTime(double ms) { if (s_path != null && ms > 0) s_renderBuckets[Math.Min(200, (int)(ms * 2))]++; }

        static object Cpu()
        {
            var loop = CosmicShore.Engine.GameLoop.Current;
            int frames = Math.Max(1, loop?.TimedFrames ?? 0);
            return new
            {
                simP50Ms = Percentile(s_simBuckets, 0.50), simP95Ms = Percentile(s_simBuckets, 0.95),
                renderP50Ms = Percentile(s_renderBuckets, 0.50), renderP95Ms = Percentile(s_renderBuckets, 0.95),
                phaseAvgMs = loop == null ? new Dictionary<string, double>() : loop.PhaseTotals.ToDictionary(
                    kv => kv.Key, kv => Math.Round(kv.Value * 1000.0 / System.Diagnostics.Stopwatch.Frequency / frames, 3)),
            };
        }

        /// <summary>
        /// Allocation and GC. Whole-run totals include loading; the steady-state figures start at
        /// frame 30 and are what a player feels (a gen0/gen1 collection is a blocking pause).
        /// </summary>
        static object Memory()
        {
            long allocated = GC.GetTotalAllocatedBytes(false) - s_alloc0;
            var loop = CosmicShore.Engine.GameLoop.Current;
            int timed = Math.Max(1, loop?.TimedFrames ?? 0);
            bool steady = s_frames > 30;
            double steadyMinutes = steady ? Math.Max(1.0 / 60, (DateTime.UtcNow - s_steady0).TotalMinutes) : 1;
            int steadyFrames = Math.Max(1, s_frames - 30);
            int g0 = GC.CollectionCount(0), g1 = GC.CollectionCount(1), g2 = GC.CollectionCount(2);
            var pause = GC.GetTotalPauseDuration();
            return new
            {
                allocatedMB = Math.Round(allocated / 1048576.0, 1),
                kbPerFrameP50 = Percentile(s_allocBuckets, 0.50) * 16, // bucket index / 2 * 8 KB
                kbPerFrameP95 = Percentile(s_allocBuckets, 0.95) * 16,
                worstFrameKB = Math.Round(s_worstAlloc / 1024.0, 1),
                heapMB = Math.Round(GC.GetTotalMemory(false) / 1048576.0, 1),
                gcGen0 = g0 - s_gc0, gcGen1 = g1 - s_gc1, gcGen2 = g2 - s_gc2,
                gcPauseMs = Math.Round((pause - s_pause0).TotalMilliseconds, 1),
                steadyGcPerMin = steady ? Math.Round((g0 - s_steadyGc0) / steadyMinutes, 1) : 0,
                steadyGen2PerMin = steady ? Math.Round((g2 - s_steadyGc2) / steadyMinutes, 1) : 0,
                steadyGcPauseMsPerFrame = steady ? Math.Round((pause - s_steadyPause0).TotalMilliseconds / steadyFrames, 2) : 0,
                // Which loop phase allocates (the loop thread only; the rest is other threads and rendering).
                phaseAvgKB = loop == null ? new Dictionary<string, double>() : loop.PhaseAllocations.ToDictionary(
                    kv => kv.Key, kv => Math.Round(kv.Value / 1024.0 / timed, 1)),
            };
        }

        static double Percentile(double p) => Percentile(s_buckets, p);

        static double Percentile(int[] buckets, double p)
        {
            int total = buckets.Sum();
            if (total == 0) return 0;
            int want = (int)Math.Ceiling(total * p), seen = 0;
            for (int i = 0; i < buckets.Length; i++) if ((seen += buckets[i]) >= want) return i / 2.0;
            return 100;
        }

        static object Audio()
        {
            var events = CosmicShore.Engine.Audio.Fmod.AudioStats.Snapshot();
            return new
            {
                backend = CosmicShore.Engine.Audio.Fmod.FmodBackend.Current != null ? "fmod" : "off",
                instances = CosmicShore.Engine.Audio.Fmod.AudioStats.Instances,
                distinctEvents = events.Count,
                topEvents = events.OrderByDescending(e => e.Value).Take(12).Select(e => new { e.Key, e.Value }).ToList(),
                missingEvents = CosmicShore.Engine.Audio.Fmod.AudioStats.Missing.ToList(),
                unwiredOneShots = CosmicShore.Engine.Audio.Fmod.AudioStats.UnwiredOneShots,
            };
        }

        /// <summary>The vessel classes flying at the end of the run (by their root object's name).</summary>
        static List<string> Vessels()
        {
            try
            {
                return CosmicShore.Engine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                    .Where(b => b.GetType().Name == "VesselStatus")
                    .Select(b => b.transform.root.name.Replace("(Clone)", "").Trim()).Distinct().Take(16).ToList();
            }
            catch { return new List<string>(); }
        }

        public static void Write(string exit, Exception crash = null)
        {
            if (s_path == null || s_written) return;
            s_written = true;
            try
            {
                double total = (DateTime.UtcNow - s_start).TotalSeconds;
                var scenes = new List<object>();
                for (int i = 0; i < s_scenes.Count; i++)
                {
                    double end = i + 1 < s_scenes.Count ? s_scenes[i + 1].seconds : total;
                    scenes.Add(new { name = s_scenes[i].scene, enteredAtSecond = Math.Round(s_scenes[i].seconds, 1), seconds = Math.Round(end - s_scenes[i].seconds, 1) });
                }
                object Problems(string prefix) => (Log?.Unique ?? new Dictionary<string, int>())
                    .Where(kv => kv.Key.StartsWith(prefix, StringComparison.Ordinal))
                    .OrderByDescending(kv => kv.Value).Take(60)
                    .Select(kv => new { count = kv.Value, message = kv.Key[(prefix.Length + 2)..].Trim() }).ToList();
                var report = new
                {
                    kind = "prisma-session",
                    version = 1,
                    startedUtc = s_start.ToString("O"),
                    seconds = Math.Round(total, 1),
                    exit,
                    crash = crash?.ToString(),
                    branch = Environment.GetEnvironmentVariable("COSMIC_SHORE_BRANCH"),
                    commit = Environment.GetEnvironmentVariable("COSMIC_SHORE_COMMIT"),
                    machine = new
                    {
                        os = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
                        cpus = Environment.ProcessorCount,
                        gpu = CosmicShore.Render.GlCaps.Renderer,
                        screen = $"{Screen.width}x{Screen.height}",
                        msaa = CosmicShore.Render.RenderQuality.Msaa,
                        renderScale = CosmicShore.Render.RenderQuality.RenderScale,
                    },
                    frames = new
                    {
                        presented = s_frames,
                        p50Ms = Percentile(0.50),
                        p95Ms = Percentile(0.95),
                        p99Ms = Percentile(0.99),
                        worstMs = Math.Round(s_worstMs, 1),
                        over33Ms = s_buckets.Skip(66).Sum(),
                    },
                    cpu = Cpu(),
                    memory = Memory(),
                    scenes,
                    perScene = s_sceneBuckets.Where(kv => kv.Value.Sum() > 0).Select(kv => new
                    {
                        scene = kv.Key,
                        frames = kv.Value.Sum(),
                        p50Ms = Percentile(kv.Value, 0.50),
                        p95Ms = Percentile(kv.Value, 0.95),
                        over33Ms = kv.Value.Skip(66).Sum(),
                    }).ToList(),
                    modes = s_scenes.Select(x => x.scene).Where(n => n.StartsWith("Minigame", StringComparison.Ordinal)).Distinct().ToList(),
                    vessels = Vessels(),
                    audio = Audio(),
                    counts = new { errors = Log?.Errors ?? 0, exceptions = Log?.Exceptions ?? 0, warnings = Log?.Warnings ?? 0 },
                    exceptions = Problems("Exception"),
                    errors = Problems("Error"),
                    asserts = Problems("Assert"),
                    warnings = Problems("Warning"),
                };
                Directory.CreateDirectory(Path.GetDirectoryName(s_path)!);
                File.WriteAllText(s_path, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
                Console.WriteLine($"[player] session report -> {s_path}");
            }
            catch (Exception e) { Console.WriteLine("[player] could not write the session report: " + e.Message); }
        }
    }
}
