#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Prisma.Workspace;

namespace Prisma
{
    /// <summary>
    /// Prisma's task and bug tracker. Items come from the user, from Prisma itself (problems the
    /// tracks found, regressions, missing audio, the next milestone that is ready) and from the
    /// Prisma Agent (prisma_board_suggest). Anything Prisma or the agent proposes starts as
    /// SUGGESTED and only joins the board when the user accepts it. Kept beside the tracks.
    ///
    /// Every item carries an acceptance criterion: the check that proves it done. For a bug Prisma
    /// found in the tracks, the check is the tracks' own: the problem stays away for three runs
    /// through its scene. <see cref="Verify"/> applies it after every ingest, so "fixed" is
    /// evidence, not a claim, and a fixed problem that comes back reopens its card.
    ///
    /// More than one writer shares board.json: Prisma.exe, which keeps the board in memory, and
    /// every prisma-mcp an agent runs. <see cref="Save(IBoardStore)"/> therefore never writes its
    /// own copy blind. It re-reads the file and merges card by card against what it last saw
    /// (<see cref="BoardMerge"/>), so a suggestion an agent made in the meantime survives.
    /// <see cref="Refresh(IBoardStore)"/> pulls such changes in without writing. A file that
    /// cannot be read is copied aside before anything replaces it, and fields or states this
    /// build does not know are carried through unchanged.
    /// The scheduling fields and logic live in <c>Shared/Workspace</c>.
    /// </summary>
    public sealed partial class PrismaBoard
    {
        public enum Kind { Bug = 0, Task = 1 }
        public enum Status { Suggested = 0, Todo = 1, Doing = 2, Done = 3, Dismissed = 4 }

        /// <summary>The file layout this build writes. 2 = stable <see cref="Item.Uid"/>s and the scheduling fields.</summary>
        public const int CurrentSchema = 2;

        public sealed partial class Item
        {
            /// <summary>The short key people read and type (B-3, T-12). Unique on the board; may be renumbered if two writers picked the same one.</summary>
            public string Id { get; set; } = "";
            /// <summary>Stable identity across writers and machines. Merges match cards by this, never by <see cref="Id"/>.</summary>
            public string Uid { get; set; } = "";

            /// <summary>Bug or Task. A kind this build does not know (written by a newer one) reads as Task and is written back unchanged.</summary>
            [JsonIgnore] public Kind Type { get => BoardJson.ParseEnum(TypeName, Kind.Task); set => TypeName = value.ToString(); }
            [JsonPropertyName("Type"), JsonConverter(typeof(LenientStringConverter))]
            public string TypeName { get; set; } = nameof(Kind.Bug);

            public string Title { get; set; } = "";
            public string Detail { get; set; } = "";
            public string Source { get; set; } = "user";   // user, tracks, agent, milestones

            /// <summary>Where the card is. A state this build does not know reads as Todo (so it stays visible) and is written back unchanged until someone moves the card.</summary>
            [JsonIgnore] public Status State { get => BoardJson.ParseEnum(StateName, Status.Todo); set => StateName = value.ToString(); }
            [JsonPropertyName("State"), JsonConverter(typeof(LenientStringConverter))]
            public string StateName { get; set; } = nameof(Status.Todo);
            /// <summary>False when the stored state came from a newer build and is shown as Todo.</summary>
            [JsonIgnore] public bool StateKnown => BoardJson.IsKnown<Status>(StateName);

            public int Priority { get; set; } = 2;           // 1 high, 2 normal, 3 low
            public DateTime Created { get; set; } = DateTime.UtcNow;
            public DateTime Updated { get; set; } = DateTime.UtcNow;
            public string? IssueKey { get; set; }
            public string? Milestone { get; set; }
            public List<string> Notes { get; set; } = new();
            /// <summary>How anyone can tell it is done: a test, a smoke run, a scene that stays clean.</summary>
            public string Criterion { get; set; } = "";
            /// <summary>The run that showed the criterion passing (null until then, and again after a relapse).</summary>
            public DateTime? CriterionMet { get; set; }

            /// <summary>Fields this build does not know (a newer Prisma wrote them): kept and written back as they were.</summary>
            [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
        }

        /// <summary>The criterion of a bug that came from the tracks (the rule that turns an issue Quiet).</summary>
        public static string TracksCriterion(string? scene) =>
            $"Not seen again in 3 runs through {(string.IsNullOrEmpty(scene) ? "any scene" : scene)} (Amoebius checks this after every run)";

        public int Schema { get; set; } = CurrentSchema;
        public List<Item> Items { get; set; } = new();
        public int NextBug { get; set; } = 1;
        public int NextTask { get; set; } = 1;
        [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }

        /// <summary>Set when the stored board could not be read. The file was copied to <see cref="Preserved"/> first, so nothing was lost.</summary>
        [JsonIgnore] public string? LoadError { get; internal set; }
        /// <summary>Where the unreadable board was copied, if it was.</summary>
        [JsonIgnore] public string? Preserved { get; internal set; }

        /// <summary>Each card as the store last held it (Uid -> JSON): the common ancestor of a three-way merge.</summary>
        [JsonIgnore] internal Dictionary<string, string> Base { get; set; } = new();
        /// <summary>The store's change stamp when this board last read or wrote it.</summary>
        [JsonIgnore] internal string? Stamp { get; set; }

        public static string FileIn(string dir) => FileBoardStore.FileIn(dir);

        /// <summary>Loads board.json from <paramref name="dir"/> (default: beside the tracks). Never throws; see <see cref="LoadError"/>.</summary>
        public static PrismaBoard Load(string? dir = null) => Load(new FileBoardStore(dir ?? PrismaTracks.DefaultDir()));

        public static PrismaBoard Load(IBoardStore store)
        {
            var stamp = store.Stamp();
            var read = store.Read();
            if (read.Text == null) return new PrismaBoard { Stamp = stamp };
            if (!BoardJson.TryParse(read.Text, out var board, out var error))
            {
                // Never start empty over a file we could not read: keep a copy before anything can replace it.
                var copy = store.Preserve(read.Text);
                return new PrismaBoard { Stamp = stamp, LoadError = error, Preserved = copy };
            }
            board!.Stamp = stamp;
            board.Base = BoardMerge.Snapshot(board);
            return board;
        }

        /// <summary>Saves to <paramref name="dir"/> (default: beside the tracks), merged with whatever another writer saved since this board last read it.</summary>
        public void Save(string? dir = null) => Save(new FileBoardStore(dir ?? PrismaTracks.DefaultDir()));

        public void Save(IBoardStore store)
        {
            using (store.Lock())
            {
                var read = store.Read();
                if (read.Text != null)
                {
                    if (BoardJson.TryParse(read.Text, out var disk, out _)) BoardMerge.Merge(this, disk!);
                    else Preserved = store.Preserve(read.Text) ?? Preserved;   // unreadable: copied aside, then replaced by this board
                }
                Schema = Math.Max(Schema, CurrentSchema);
                store.Write(BoardJson.Serialize(this));
                Base = BoardMerge.Snapshot(this);
                Stamp = store.Stamp();
            }
        }

        /// <summary>
        /// Pulls in what another writer saved (an agent's suggestion, a move from another Prisma)
        /// when the store changed since this board last read or wrote it. Local changes not yet
        /// saved are kept. Returns true when anything was read. Writes nothing.
        /// </summary>
        public bool Refresh(string? dir = null) => Refresh(new FileBoardStore(dir ?? PrismaTracks.DefaultDir()));

        public bool Refresh(IBoardStore store)
        {
            var stamp = store.Stamp();
            if (stamp == Stamp) return false;
            var read = store.Read();
            Stamp = stamp;
            if (read.Text == null || !BoardJson.TryParse(read.Text, out var disk, out _)) return false; // the next Save deals with it
            BoardMerge.Merge(this, disk!);
            Base = BoardMerge.Snapshot(disk!);   // the store's content is the new ancestor; unsaved local edits still differ from it
            return true;
        }

        public Item Add(Kind type, string title, string detail = "", string source = "user", Status state = Status.Todo,
                        int priority = 2, string? issueKey = null, string? milestone = null, string criterion = "")
        {
            var item = new Item
            {
                Id = NewId(type), Uid = NewUid(),
                Type = type, Title = title.Trim(), Detail = detail.Trim(), Source = source, State = state,
                Priority = Math.Clamp(priority, 1, 3), IssueKey = issueKey, Milestone = milestone, Criterion = criterion.Trim(),
            };
            Items.Add(item);
            return item;
        }

        internal static string NewUid() => Guid.NewGuid().ToString("N");

        /// <summary>The next free key of a kind: past both the counter and every key already on the board.</summary>
        internal string NewId(Kind type)
        {
            if (type == Kind.Bug)
            {
                NextBug = Math.Max(NextBug, BoardMerge.MaxNumber(Items, "B-") + 1);
                return $"B-{NextBug++}";
            }
            NextTask = Math.Max(NextTask, BoardMerge.MaxNumber(Items, "T-") + 1);
            return $"T-{NextTask++}";
        }

        public void Move(Item item, Status to, string? note = null)
        {
            item.State = to;
            item.Updated = DateTime.UtcNow;
            if (to == Status.Done) item.CompletedAt ??= DateTime.UtcNow;
            else item.CompletedAt = null;
            if (note != null) item.Notes.Add($"{DateTime.Now:yyyy-MM-dd HH:mm} {note}");
        }

        /// <summary>
        /// Prisma's own suggestions: a bug for every tracked problem worth fixing that the board
        /// does not know yet, and a task for each milestone checkpoint whose dependencies are done.
        /// Returns what it added (all SUGGESTED).
        /// </summary>
        public List<Item> Suggest(PrismaTracks tracks, IEnumerable<(string id, string title, string status, List<string> deps, string exit)>? checkpoints = null)
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
                    "tracks", Status.Suggested, priority, issueKey: issue.Key, criterion: TracksCriterion(issue.LastScene)));
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
                        "milestones", Status.Suggested, 2, milestone: c.id, criterion: c.exit));
            }
            return added;
        }

        /// <summary>
        /// Applies the tracks' verdict to every card linked to a tracked problem: the criterion is met
        /// when the problem went quiet, and a met card whose problem came back loses it (a DONE card
        /// reopens to TO DO). Returns the cards that changed and how. Moving to DONE stays the user's call.
        /// </summary>
        public List<(Item item, bool met)> Verify(PrismaTracks tracks)
        {
            var changed = new List<(Item, bool)>();
            foreach (var it in Items.Where(i => i.IssueKey != null && i.State is Status.Todo or Status.Doing or Status.Done))
            {
                if (!tracks.Issues.TryGetValue(it.IssueKey!, out var issue)) continue;
                if (issue.State == PrismaTracks.IssueState.Quiet && it.CriterionMet == null)
                {
                    // Stamped with the run that proved it, so "came back" compares run with run.
                    it.CriterionMet = tracks.Runs.Count > 0 ? tracks.Runs[^1].Time : DateTime.UtcNow;
                    it.Updated = DateTime.UtcNow;
                    it.Notes.Add($"{DateTime.Now:yyyy-MM-dd HH:mm} criterion met: not seen since {issue.LastSeen:yyyy-MM-dd HH:mm}" +
                                 (issue.LastScene != null ? $" across 3 runs through {issue.LastScene}" : ""));
                    changed.Add((it, true));
                }
                else if (issue.State is PrismaTracks.IssueState.Open or PrismaTracks.IssueState.Fixing && it.CriterionMet != null)
                {
                    it.CriterionMet = null;
                    it.Updated = DateTime.UtcNow;
                    it.Notes.Add($"{DateTime.Now:yyyy-MM-dd HH:mm} came back on {issue.LastSeen:yyyy-MM-dd HH:mm}");
                    if (it.State == Status.Done) Move(it, Status.Todo, "reopened: the problem came back");
                    changed.Add((it, false));
                }
            }
            return changed;
        }

        static string Short(string m)
        {
            m = m.Replace('\n', ' ').Trim();
            return m.Length > 90 ? m[..89] + "..." : m;
        }
    }
}
