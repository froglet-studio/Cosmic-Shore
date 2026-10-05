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
    /// long, frame-time percentiles, every distinct error/warning/exception with its count, and the
    /// crash if there was one. The launcher keeps these and hands them to Claude ("analyse my last
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
            int bucket = Math.Min(s_buckets.Length - 1, (int)(ms * 2));
            s_buckets[bucket]++;
            if (s_frames > 30) // per scene, after the first frames' loading
            {
                if (!s_sceneBuckets.TryGetValue(s_currentScene, out var sb)) s_sceneBuckets[s_currentScene] = sb = new int[201];
                sb[bucket]++;
            }
            if (ms > s_worstMs && s_frames > 30) s_worstMs = ms; // the first frames are loading
        }

        static double Percentile(double p) => Percentile(s_buckets, p);

        static double Percentile(int[] buckets, double p)
        {
            var s_buckets = buckets;
            int total = s_buckets.Sum();
            if (total == 0) return 0;
            int want = (int)Math.Ceiling(total * p), seen = 0;
            for (int i = 0; i < s_buckets.Length; i++) if ((seen += s_buckets[i]) >= want) return i / 2.0;
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
