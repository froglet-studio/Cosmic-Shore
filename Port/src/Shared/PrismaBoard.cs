#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Prisma
{
    /// <summary>
    /// Prisma's task and bug tracker. Items come from the user, from Prisma itself (problems the
    /// tracks found, regressions, missing audio, the next milestone that is ready) and from the
    /// Prisma Agent (prisma_board_suggest). Anything Prisma or the agent proposes starts as
    /// SUGGESTED and only joins the board when the user accepts it. Kept beside the tracks.
    /// </summary>
    public sealed class PrismaBoard
    {
        public enum Kind { Bug = 0, Task = 1 }
        public enum Status { Suggested = 0, Todo = 1, Doing = 2, Done = 3, Dismissed = 4 }

        public sealed class Item
        {
            public string Id { get; set; } = "";
            public Kind Type { get; set; }
            public string Title { get; set; } = "";
            public string Detail { get; set; } = "";
            public string Source { get; set; } = "user";   // user, tracks, agent, milestones
            public Status State { get; set; } = Status.Todo;
            public int Priority { get; set; } = 2;           // 1 high, 2 normal, 3 low
            public DateTime Created { get; set; } = DateTime.UtcNow;
            public DateTime Updated { get; set; } = DateTime.UtcNow;
            public string? IssueKey { get; set; }
            public string? Milestone { get; set; }
            public List<string> Notes { get; set; } = new();
        }

        public List<Item> Items { get; set; } = new();
        public int NextBug { get; set; } = 1;
        public int NextTask { get; set; } = 1;

        static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };

        public static string FileIn(string dir) => Path.Combine(dir, "board.json");

        public static PrismaBoard Load(string? dir = null)
        {
            dir ??= PrismaTracks.DefaultDir();
            try { if (File.Exists(FileIn(dir))) return JsonSerializer.Deserialize<PrismaBoard>(File.ReadAllText(FileIn(dir)), Json) ?? new(); }
            catch (Exception) { }
            return new();
        }

        public void Save(string? dir = null)
        {
            dir ??= PrismaTracks.DefaultDir();
            Directory.CreateDirectory(dir);
            var tmp = FileIn(dir) + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(this, Json));
            File.Move(tmp, FileIn(dir), overwrite: true);
        }

        public Item Add(Kind type, string title, string detail = "", string source = "user", Status state = Status.Todo,
                        int priority = 2, string? issueKey = null, string? milestone = null)
        {
            var item = new Item
            {
                Id = type == Kind.Bug ? $"B-{NextBug++}" : $"T-{NextTask++}",
                Type = type, Title = title.Trim(), Detail = detail.Trim(), Source = source, State = state,
                Priority = Math.Clamp(priority, 1, 3), IssueKey = issueKey, Milestone = milestone,
            };
            Items.Add(item);
            return item;
        }

        public void Move(Item item, Status to, string? note = null)
        {
            item.State = to;
            item.Updated = DateTime.UtcNow;
            if (note != null) item.Notes.Add($"{DateTime.Now:yyyy-MM-dd HH:mm} {note}");
        }

        /// <summary>
        /// Prisma's own suggestions: a bug for every tracked problem worth fixing that the board
        /// does not know yet, and a task for each milestone checkpoint whose dependencies are done.
        /// Returns what it added (all SUGGESTED).
        /// </summary>
        public List<Item> Suggest(PrismaTracks tracks, IEnumerable<(string id, string title, string status, List<string> deps)>? checkpoints = null)
        {
            var added = new List<Item>();
            var known = new HashSet<string>(Items.Where(i => i.IssueKey != null).Select(i => i.IssueKey!));
            foreach (var issue in tracks.Open.Where(i => i.Kind != "warning" || i.Runs >= 5))
            {
                if (known.Contains(issue.Key)) continue;
                int priority = issue.Kind is "crash" or "exception" ? 1 : issue.Kind is "error" or "perf" ? 2 : 3;
                string title = issue.Kind switch
                {
                    "crash" => "Crash: " + Short(issue.Message),
                    "perf" => "Slow: " + Short(issue.Message),
                    "audio" => "Audio: " + Short(issue.Message),
                    _ => Short(issue.Message),
                };
                added.Add(Add(Kind.Bug, title,
                    $"{issue.Kind} seen in {issue.Runs} run(s), {issue.Count} time(s); first {issue.FirstSeen:yyyy-MM-dd}, last {issue.LastSeen:yyyy-MM-dd HH:mm}" +
                    (issue.LastScene != null ? $", last in {issue.LastScene}" : "") + ".\n" + issue.Message,
                    "tracks", Status.Suggested, priority, issueKey: issue.Key));
            }
            if (checkpoints != null)
            {
                var all = checkpoints.ToList();
                var done = new HashSet<string>(all.Where(c => c.status == "done").Select(c => c.id));
                var mentioned = new HashSet<string>(Items.Where(i => i.Milestone != null).Select(i => i.Milestone!));
                // At most two milestone suggestions open at once: the next steps, not the whole roadmap.
                int open = Items.Count(i => i.Milestone != null && i.State is Status.Suggested or Status.Todo or Status.Doing);
                foreach (var c in all.Where(c => c.status == "todo" && c.deps.All(done.Contains) && !mentioned.Contains(c.id)).Take(Math.Max(0, 2 - open)))
                    added.Add(Add(Kind.Task, $"Start milestone {c.id}: {c.title}", "Its dependencies are done. START it from MILESTONES.",
                        "milestones", Status.Suggested, 2, milestone: c.id));
            }
            return added;
        }

        static string Short(string m)
        {
            m = m.Replace('\n', ' ').Trim();
            return m.Length > 90 ? m[..89] + "..." : m;
        }
    }
}
