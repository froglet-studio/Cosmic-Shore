#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Prisma
{
    /// <summary>
    /// Prisma's memory of every play run: each session report the player writes is folded into
    /// tracks - performance per scene over time, the features (modes, vessels, scenes) each run
    /// touched, audio activity, and every distinct problem with when it was first and last seen.
    /// The Prisma Agent gets <see cref="Memory"/> with every prompt, so it starts from what the
    /// last runs showed instead of rediscovering it. Linked into the launcher and the MCP server.
    /// </summary>
    public sealed class PrismaTracks
    {
        public sealed class Run
        {
            public string Id { get; set; } = "";
            public DateTime Time { get; set; }
            public string? Branch { get; set; }
            public string? Commit { get; set; }
            public double Seconds { get; set; }
            public string Exit { get; set; } = "";
            public bool Crashed { get; set; }
            public double P50 { get; set; }
            public double P95 { get; set; }
            public int Errors { get; set; }
            public int Exceptions { get; set; }
            public int Warnings { get; set; }
            public List<string> Scenes { get; set; } = new();
            public List<string> Modes { get; set; } = new();
            public List<string> Vessels { get; set; } = new();
            public Dictionary<string, double> SceneP95 { get; set; } = new();
            public int AudioInstances { get; set; }
            public int AudioDistinct { get; set; }
            public int AudioMissing { get; set; }
            public int AudioUnwired { get; set; }
            public string Report { get; set; } = "";
        }

        public enum IssueState { Open = 0, Quiet = 1, Ignored = 2, Fixing = 3 }

        public sealed class Issue
        {
            public string Key { get; set; } = "";
            public string Kind { get; set; } = "";     // crash, exception, error, warning, perf, audio
            public string Area { get; set; } = "";     // [Tag] from the message, the scene, or "audio"
            public string Message { get; set; } = "";
            public DateTime FirstSeen { get; set; }
            public DateTime LastSeen { get; set; }
            public int Runs { get; set; }
            public int Count { get; set; }
            public IssueState State { get; set; }
            public string? LastScene { get; set; }
        }

        public List<Run> Runs { get; set; } = new();
        public Dictionary<string, Issue> Issues { get; set; } = new();

        static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };

        public static string DefaultDir()
        {
            var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrEmpty(baseDir))
                baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
            return Path.Combine(baseDir, "Prisma", "tracks");
        }

        public static string FileIn(string dir) => Path.Combine(dir, "tracks.json");

        public static PrismaTracks Load(string? dir = null)
        {
            dir ??= DefaultDir();
            try
            {
                var f = FileIn(dir);
                if (File.Exists(f)) return JsonSerializer.Deserialize<PrismaTracks>(File.ReadAllText(f), Json) ?? new();
            }
            catch (Exception) { }
            return new();
        }

        public void Save(string? dir = null)
        {
            dir ??= DefaultDir();
            Directory.CreateDirectory(dir);
            var tmp = FileIn(dir) + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(this, Json));
            File.Move(tmp, FileIn(dir), overwrite: true);
            File.WriteAllText(Path.Combine(dir, "MEMORY.md"), Memory(12000));
        }

        public sealed record IngestResult(Run Run, List<Issue> New, List<Issue> Back, List<Issue> Regressions);

        /// <summary>Folds one session report into the tracks. Returns what is new since the last runs.</summary>
        public IngestResult? Ingest(string reportPath)
        {
            if (Runs.Any(r => r.Report == reportPath)) return null;
            JsonElement d;
            try { d = JsonDocument.Parse(File.ReadAllText(reportPath)).RootElement; } catch { return null; }
            if (!d.TryGetProperty("kind", out var k) || k.GetString() != "froglet-engine-session" && k.GetString() != "prisma-session") return null;

            string S(JsonElement e, string p) => e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
            double D(JsonElement e, string p) => e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : 0;
            int I(JsonElement e, string p) => e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : 0;
            List<string> L(JsonElement e, string p) => e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.Array
                ? v.EnumerateArray().Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() ?? "" : x.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "").Where(x => x.Length > 0).ToList()
                : new();

            var frames = d.TryGetProperty("frames", out var fr) ? fr : default;
            var counts = d.TryGetProperty("counts", out var cn) ? cn : default;
            var audio = d.TryGetProperty("audio", out var au) && au.ValueKind == JsonValueKind.Object ? au : default;
            DateTime.TryParse(S(d, "startedUtc"), null, System.Globalization.DateTimeStyles.RoundtripKind, out var time);
            var run = new Run
            {
                Id = Path.GetFileNameWithoutExtension(reportPath),
                Time = time == default ? File.GetLastWriteTimeUtc(reportPath) : time,
                Branch = S(d, "branch"), Commit = S(d, "commit"),
                Seconds = D(d, "seconds"), Exit = S(d, "exit"),
                Crashed = d.TryGetProperty("crash", out var cr) && cr.ValueKind == JsonValueKind.String,
                P50 = frames.ValueKind == JsonValueKind.Object ? D(frames, "p50Ms") : 0,
                P95 = frames.ValueKind == JsonValueKind.Object ? D(frames, "p95Ms") : 0,
                Errors = counts.ValueKind == JsonValueKind.Object ? I(counts, "errors") : 0,
                Exceptions = counts.ValueKind == JsonValueKind.Object ? I(counts, "exceptions") : 0,
                Warnings = counts.ValueKind == JsonValueKind.Object ? I(counts, "warnings") : 0,
                Scenes = L(d, "scenes"), Modes = L(d, "modes"), Vessels = L(d, "vessels"),
                AudioInstances = audio.ValueKind == JsonValueKind.Object ? I(audio, "instances") : 0,
                AudioDistinct = audio.ValueKind == JsonValueKind.Object ? I(audio, "distinctEvents") : 0,
                AudioMissing = audio.ValueKind == JsonValueKind.Object ? L(audio, "missingEvents").Count : 0,
                AudioUnwired = audio.ValueKind == JsonValueKind.Object ? I(audio, "unwiredOneShots") : 0,
                Report = reportPath,
            };
            if (d.TryGetProperty("perScene", out var ps) && ps.ValueKind == JsonValueKind.Array)
                foreach (var s in ps.EnumerateArray())
                    if (I(s, "frames") >= 120) run.SceneP95[S(s, "scene")] = D(s, "p95Ms");

            var fresh = new List<Issue>(); var back = new List<Issue>(); var regress = new List<Issue>();
            Issue Note(string kind, string message, int count, string? area = null)
            {
                string key = kind + ":" + Normalise(message);
                if (!Issues.TryGetValue(key, out var i))
                {
                    Issues[key] = i = new Issue { Key = key, Kind = kind, Message = message.Length > 400 ? message[..400] : message, FirstSeen = run.Time, Area = area ?? AreaOf(message) };
                    fresh.Add(i);
                }
                else if (i.State == IssueState.Quiet) { i.State = IssueState.Open; back.Add(i); }
                i.LastSeen = run.Time; i.Runs++; i.Count += Math.Max(1, count);
                i.LastScene = run.Scenes.LastOrDefault();
                return i;
            }
            if (run.Crashed) Note("crash", S(d, "crash").Split('\n')[0], 1, "crash");
            foreach (var (prop, kind) in new[] { ("exceptions", "exception"), ("errors", "error"), ("asserts", "error"), ("warnings", "warning") })
                if (d.TryGetProperty(prop, out var arr) && arr.ValueKind == JsonValueKind.Array)
                    foreach (var e in arr.EnumerateArray()) Note(kind, S(e, "message"), I(e, "count"));
            if (audio.ValueKind == JsonValueKind.Object)
            {
                foreach (var m in L(audio, "missingEvents")) Note("audio", "FMOD event not in the loaded banks: " + m, 1, "audio");
                if (run.AudioUnwired > 0) Note("audio", "one-shots fired with an unwired (empty) EventReference", run.AudioUnwired, "audio");
            }
            // Performance: over budget, or 25% worse than the scene's recent median.
            foreach (var (scene, p95) in run.SceneP95)
            {
                var history = Runs.Where(r => r.SceneP95.ContainsKey(scene)).TakeLast(5).Select(r => r.SceneP95[scene]).OrderBy(x => x).ToList();
                double median = history.Count > 0 ? history[history.Count / 2] : 0;
                if (p95 > 33.4) Note("perf", $"{scene}: p95 frame {p95:0.0} ms (over the 33 ms budget)", 1, scene);
                if (median > 0 && p95 > median * 1.25 && p95 - median > 2)
                    regress.Add(Note("perf", $"{scene}: p95 regressed {median:0.0} -> {p95:0.0} ms", 1, scene));
            }
            // Problems that stayed away for three runs through their scene go quiet.
            foreach (var i in Issues.Values.Where(i => i.State == IssueState.Open && i.LastSeen < run.Time))
            {
                int since = Runs.Count(r => r.Time > i.LastSeen && (i.LastScene == null || r.Scenes.Contains(i.LastScene))) + (i.LastScene == null || run.Scenes.Contains(i.LastScene) ? 1 : 0);
                if (since >= 3) i.State = IssueState.Quiet;
            }
            Runs.Add(run);
            if (Runs.Count > 500) Runs.RemoveRange(0, Runs.Count - 500);
            return new IngestResult(run, fresh.Where(i => i.Kind != "warning").ToList(), back, regress);
        }

        /// <summary>"[Tag] Something 42 at 0x1f..." -> a key that groups the same problem across runs.</summary>
        static string Normalise(string m)
        {
            m = Regex.Replace(m, @"0x[0-9a-fA-F]+|\b[0-9a-f]{8}-[0-9a-f-]{27,}\b", "#");
            m = Regex.Replace(m, @"-?\d+(\.\d+)?", "#");
            m = Regex.Replace(m, @"\s+", " ").Trim();
            return m.Length > 200 ? m[..200] : m;
        }

        static string AreaOf(string m)
        {
            var t = Regex.Match(m, @"^\[([^\]]{2,40})\]");
            return t.Success ? t.Groups[1].Value : "";
        }

        public IEnumerable<Issue> Open => Issues.Values.Where(i => i.State is IssueState.Open or IssueState.Fixing)
            .OrderByDescending(i => Rank(i.Kind)).ThenByDescending(i => i.Runs).ThenByDescending(i => i.LastSeen);

        static int Rank(string kind) => kind switch { "crash" => 6, "exception" => 5, "error" => 4, "audio" => 3, "perf" => 2, _ => 1 };

        /// <summary>A compact brief for the agent: the last runs, open problems, performance and audio trends.</summary>
        public string Memory(int maxChars = 6000)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# Prisma tracks ({Runs.Count} runs recorded, {Open.Count()} open problems)");
            if (Runs.Count == 0) { sb.AppendLine("No play runs recorded yet."); return sb.ToString(); }
            sb.AppendLine();
            sb.AppendLine("## Last runs (newest first)");
            foreach (var r in Runs.AsEnumerable().Reverse().Take(6))
                sb.AppendLine($"- {r.Time:yyyy-MM-dd HH:mm} {r.Branch}@{r.Commit} {r.Seconds:0}s{(r.Crashed ? " CRASHED" : "")}: " +
                              $"{string.Join(" > ", r.Scenes.Take(6))}; p50 {r.P50:0.0} / p95 {r.P95:0.0} ms; {r.Exceptions} exc, {r.Errors} err, {r.Warnings} warn; " +
                              $"audio {r.AudioInstances} instances ({r.AudioDistinct} events, {r.AudioMissing} missing, {r.AudioUnwired} unwired)" +
                              (r.Modes.Count > 0 ? "; modes " + string.Join(",", r.Modes) : "") + (r.Vessels.Count > 0 ? "; vessels " + string.Join(",", r.Vessels) : ""));
            sb.AppendLine();
            sb.AppendLine("## Open problems (worst first: kind, area, runs seen, last seen, message)");
            foreach (var i in Open.Take(25))
                sb.AppendLine($"- [{i.Kind}] {(i.Area.Length > 0 ? i.Area + " - " : "")}{i.Runs} runs, last {i.LastSeen:MM-dd HH:mm}{(i.State == IssueState.Fixing ? " (a fix is in progress)" : "")}: {i.Message}");
            sb.AppendLine();
            sb.AppendLine("## Performance by scene (median p95 of the last 5 runs -> latest)");
            foreach (var scene in Runs.SelectMany(r => r.SceneP95.Keys).Distinct().OrderBy(s => s))
            {
                var xs = Runs.Where(r => r.SceneP95.ContainsKey(scene)).Select(r => r.SceneP95[scene]).ToList();
                var last5 = xs.TakeLast(5).OrderBy(x => x).ToList();
                sb.AppendLine($"- {scene}: {last5[last5.Count / 2]:0.0} ms -> {xs[^1]:0.0} ms over {xs.Count} runs");
            }
            var quiet = Issues.Values.Where(i => i.State == IssueState.Quiet).OrderByDescending(i => i.LastSeen).Take(8).ToList();
            if (quiet.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("## Recently quiet (not seen in 3+ runs through their scene - probably fixed)");
                foreach (var i in quiet) sb.AppendLine($"- [{i.Kind}] {i.Message}");
            }
            var s = sb.ToString();
            return s.Length > maxChars ? s[..maxChars] + "\n... (tracks.json has the rest)" : s;
        }
    }
}
