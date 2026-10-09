using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using CosmicShore.Engine.Profiling;

namespace CosmicShore.Player
{
    /// <summary>
    /// The <c>markers</c> script / control verb: the game's own ProfilerMarker timings as this engine
    /// measures them (<see cref="MarkerCollector"/>), over a window the caller opens with
    /// <c>markers reset</c>. It is the in-engine counterpart of Unity's <c>diag</c>: same marker
    /// names, plus the bytes each one allocates.
    ///
    ///   markers reset          start a new window
    ///   markers [N]            print the top N (default 25) by ms per frame
    ///   markers json PATH      write the whole window as JSON
    /// </summary>
    public static class MarkerReport
    {
        public static void Run(string arg)
        {
            var words = arg.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length > 0 && words[0] == "reset")
            {
                MarkerCollector.ResetTotals();
                Console.WriteLine("[markers] window reset");
                return;
            }
            if (words.Length > 1 && words[0] == "json")
            {
                Write(words[1]);
                return;
            }
            int top = words.Length > 0 && int.TryParse(words[0], out int n) ? n : 25;
            var all = MarkerCollector.Summarize();
            Console.WriteLine($"[markers] {all.Count} markers over {MarkerCollector.TotalFrames} frames (ms per frame over every frame; p50/p95/max over the frames each ran)");
            Console.WriteLine($"[markers] {"name",-48} {"avgMs",8} {"p50",7} {"p95",7} {"max",8} {"calls/f",8} {"KB/f",8}");
            foreach (var m in all.Take(top))
                Console.WriteLine($"[markers] {Trim(m.Name, 48),-48} {m.AvgMsPerFrame,8:0.0000} {m.P50Ms,7:0.00} {m.P95Ms,7:0.00} {m.MaxMs,8:0.000} {m.CallsPerFrame,8:0.00} {m.KBPerFrame,8:0.000}");
        }

        public static void Write(string path)
        {
            var all = MarkerCollector.Summarize();
            var doc = new
            {
                kind = "prisma-markers",
                version = 1,
                engine = "prisma",
                scene = CosmicShore.Engine.SceneManagement.SceneManager.GetActiveScene().name,
                frames = MarkerCollector.TotalFrames,
                markers = all.Select(m => new
                {
                    name = m.Name, category = m.Category,
                    avgMsPerFrame = Math.Round(m.AvgMsPerFrame, 4), p50Ms = m.P50Ms, p95Ms = m.P95Ms, maxMs = Math.Round(m.MaxMs, 3),
                    callsPerFrame = Math.Round(m.CallsPerFrame, 3), kbPerFrame = Math.Round(m.KBPerFrame, 3), activeFrames = m.ActiveFrames,
                }).ToList(),
            };
            path = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"[markers] {all.Count} markers, {MarkerCollector.TotalFrames} frames -> {path}");
        }

        static string Trim(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "~";
    }
}
