using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using ImGuiNET;
using Prisma;

namespace CosmicShore.Launcher
{
    /// <summary>
    /// Prisma's own pages: TRACKS (what every play run recorded), BOARD (the task and bug tracker,
    /// with Prisma's suggestions) and MILESTONES (the roadmap's checkpoints, each run as an engine
    /// session), plus the macOS-style title bar they sit under.
    /// </summary>
    public sealed partial class LauncherApp
    {
        const float TitleH = 44;
        readonly Dictionary<Page, (Vector2 a, Vector2 b)> _railRects = new();
        static string TracksDir => Path.Combine(LauncherSettings.DataDir, "tracks");

        PrismaTracks _tracks = PrismaTracks.Load(TracksDir);
        PrismaBoard _board = PrismaBoard.Load(TracksDir);
        readonly object _dataLock = new();
        int _tracksTab, _boardTab;
        string _newItem = "", _newCriterion = "";
        int _newItemType;
        string? _openCard;

        // ------------------------------------------------------------------ data

        /// <summary>Folds every session report not yet in the tracks, then lets Prisma suggest board items.</summary>
        void IngestSessions(bool notify)
        {
            var results = new List<PrismaTracks.IngestResult>();
            List<PrismaBoard.Item> suggested;
            List<(PrismaBoard.Item item, bool met)> verified;
            lock (_dataLock)
            {
                foreach (var f in LauncherJobs.Sessions().OrderBy(f => f.LastWriteTimeUtc))
                    if (_tracks.Ingest(f.FullName) is { } r) results.Add(r);
                suggested = _board.Suggest(_tracks, Checkpoints());
                verified = _board.Verify(_tracks);
                if (results.Count > 0) _tracks.Save(TracksDir);
                if (suggested.Count > 0 || results.Count > 0 || verified.Count > 0) _board.Save(TracksDir);
            }
            if (!notify) return;
            foreach (var r in results) NotifyRun(r);
            foreach (var (it, met) in verified)
            {
                var card = it;
                if (met)
                    Notify($"{card.Id} looks fixed", $"{Trim(card.Title, 70)}\nIts acceptance check passed: not seen again in 3 runs.", NoteKind.Success,
                        card.State == PrismaBoard.Status.Done ? Array.Empty<(string, Action)>() : new (string, Action)[]
                        {
                            ("MARK DONE", () => { MoveCard(card, PrismaBoard.Status.Done, "done: acceptance check passed"); }),
                            ("SHOW", () => { _page = Page.Board; _openCard = card.Id; }),
                        });
                else
                    Notify($"{card.Id} came back", Trim(card.Title, 80), NoteKind.Warning, ("SHOW", () => { _page = Page.Board; _openCard = card.Id; }));
            }
            if (suggested.Count > 0)
                Notify($"{suggested.Count} new suggestion{(suggested.Count == 1 ? "" : "s")}",
                    string.Join("\n", suggested.Take(3).Select(i => i.Title)), NoteKind.Info,
                    ("REVIEW", () => { _page = Page.Board; _boardTab = 0; }));
        }

        void SaveBoard() { lock (_dataLock) _board.Save(TracksDir); }

        /// <summary>
        /// Moves a card and keeps its tracked problem in step: a bug in DOING marks the problem as
        /// being fixed (the agent's brief says so); anywhere else it is just open again.
        /// </summary>
        void MoveCard(PrismaBoard.Item it, PrismaBoard.Status to, string? note = null)
        {
            lock (_dataLock)
            {
                _board.Move(it, to, note);
                if (it.IssueKey != null && _tracks.Issues.TryGetValue(it.IssueKey, out var issue)
                    && issue.State is PrismaTracks.IssueState.Open or PrismaTracks.IssueState.Fixing)
                {
                    issue.State = to == PrismaBoard.Status.Doing ? PrismaTracks.IssueState.Fixing : PrismaTracks.IssueState.Open;
                    _tracks.Save(TracksDir);
                }
                _board.Save(TracksDir);
            }
        }

        // milestones.json lives in the workspace (Port/docs), so progress is committed with the branch
        string MilestonesFile => Path.Combine(_ws.Dir, "Port", "docs", "milestones.json");
        JsonObject? _milestones;
        DateTime _milestonesRead;

        JsonObject? Milestones()
        {
            try
            {
                if (!File.Exists(MilestonesFile)) return _milestones = null;
                var t = File.GetLastWriteTimeUtc(MilestonesFile);
                if (_milestones == null || t != _milestonesRead) { _milestones = JsonNode.Parse(File.ReadAllText(MilestonesFile))!.AsObject(); _milestonesRead = t; }
            }
            catch { _milestones = null; }
            return _milestones;
        }

        IEnumerable<(string id, string title, string status, List<string> deps, string exit)> Checkpoints()
        {
            var m = Milestones();
            if (m?["checkpoints"] is not JsonArray arr) yield break;
            foreach (var c in arr.OfType<JsonObject>())
                yield return (c["id"]?.ToString() ?? "", c["title"]?.ToString() ?? "", c["status"]?.ToString() ?? "todo",
                              (c["dependsOn"] as JsonArray)?.Select(x => x?.ToString() ?? "").ToList() ?? new List<string>(),
                              c["exit"]?.ToString() ?? "");
        }

        // The file is read by people and committed: keep ' ` > and non-ASCII as written, not \u-escaped.
        static readonly JsonSerializerOptions MilestonesJson = new()
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        void SetMilestoneStatus(JsonObject c, string status)
        {
            c["status"] = status;
            (c["notes"] as JsonArray ?? (JsonArray)(c["notes"] = new JsonArray())).Add($"{DateTime.Now:yyyy-MM-dd} status -> {status} (Prisma)");
            File.WriteAllText(MilestonesFile, _milestones!.ToJsonString(MilestonesJson));
            _milestonesRead = File.GetLastWriteTimeUtc(MilestonesFile);
        }

        /// <summary>START / CONTINUE (and --auto milestone:ID): an engine session for the checkpoint, in PLAN mode with its prompt.</summary>
        void StartMilestone(string id)
        {
            if (Milestones()?["checkpoints"] is not JsonArray arr) return;
            if (arr.OfType<JsonObject>().FirstOrDefault(x => x["id"]?.ToString() == id) is not { } c) return;
            if ((c["status"]?.ToString() ?? "todo") == "todo") SetMilestoneStatus(c, "in-progress");
            var chat = _chats.ForMilestone(id, c["title"]?.ToString() ?? "");
            _page = Page.Chat;
            if (chat.Busy) return; // already running: just show it
            _s.ChatMode = 0; _dirty = true;
            SendChat(c["prompt"]?.ToString() ?? $"Plan checkpoint {id}.", ClaudeChat.Mode.Plan);
        }

        void AddMilestoneNote(string id, string note)
        {
            if (Milestones()?["checkpoints"] is not JsonArray arr) return;
            if (arr.OfType<JsonObject>().FirstOrDefault(c => c["id"]?.ToString() == id) is not { } c) return;
            (c["notes"] as JsonArray ?? (JsonArray)(c["notes"] = new JsonArray())).Add($"{DateTime.Now:yyyy-MM-dd} {note}");
            File.WriteAllText(MilestonesFile, _milestones!.ToJsonString(MilestonesJson));
            _milestonesRead = File.GetLastWriteTimeUtc(MilestonesFile);
        }

        /// <summary>
        /// A milestone run stopped short (a budget limit or a failure): what it tried goes on the board
        /// as a suggestion with the checkpoint's exit criterion, the checkpoint gets a dated note, and
        /// a notification offers to continue the same conversation with a fresh budget.
        /// </summary>
        void OnMilestoneStopped(ClaudeChat.SessionStop stop)
        {
            var exit = Checkpoints().FirstOrDefault(c => c.id == stop.Milestone).exit ?? "";
            PrismaBoard.Item item;
            lock (_dataLock)
            {
                item = _board.Add(PrismaBoard.Kind.Task, $"Milestone {stop.Milestone} stopped {stop.Reason}",
                    $"{stop.Title}\nWhat the run tried:\n{stop.Tried}" + (stop.LastWords.Length > 0 ? $"\nIts last words:\n{stop.LastWords}" : ""),
                    "milestones", PrismaBoard.Status.Suggested, 1, milestone: stop.Milestone, criterion: exit);
                _board.Save(TracksDir);
            }
            AddMilestoneNote(stop.Milestone, $"run stopped {stop.Reason}; what it tried is board item {item.Id} (Prisma)");
            Notify($"Milestone {stop.Milestone} stopped", $"Stopped {stop.Reason}. What it tried is on the BOARD as {item.Id}.", NoteKind.Warning,
                ("CONTINUE", () =>
                {
                    // Same conversation when it is still open; otherwise a fresh one told what was tried.
                    var chat = _chats.ForMilestone(stop.Milestone, stop.Title);
                    bool same = !chat.Empty;
                    _page = Page.Chat;
                    if (chat.Busy) return;
                    SendChat("Continue where the last run stopped. Check what is already done before redoing anything, and keep to the exit criterion." +
                             (same ? "" : $"\nThe last run stopped {stop.Reason}. What it tried:\n{stop.Tried}"), (ClaudeChat.Mode)_s.ChatMode);
                }),
                ("BOARD", () => { _page = Page.Board; _openCard = item.Id; }));
        }

        int RailBadge(Page p) => p switch
        {
            Page.Board => _board.Items.Count(i => i.State == PrismaBoard.Status.Suggested),
            Page.Tracks => _tracks.Open.Count(i => i.Kind is "crash" or "exception"),
            Page.Git => _git.Last?.Changes.Count ?? 0,
            _ => 0,
        };

        // ------------------------------------------------------------------ title bar

        void DrawTitleBar(Vector2 size)
        {
            var dl = ImGui.GetWindowDrawList();
            var a = new Vector2(RailW + 1, 0); var b = new Vector2(size.X, TitleH);
            dl.AddRectFilled(a, b, Neon.U(Neon.Space0, 0.55f));
            dl.AddLine(new Vector2(a.X, b.Y), b, Neon.U(Neon.Ink, 0.07f));
            string title = "Prisma  ·  Cosmic Shore";
            ImGui.PushFont(Neon.Strong);
            var ts = ImGui.CalcTextSize(title);
            ImGui.PopFont();
            dl.AddText(Neon.Strong, 15, new Vector2((a.X + b.X - ts.X * 15f / 16f) * 0.5f, 13), Neon.U(Neon.Ink, 0.85f), title);

            // right: branch, agent, notifications, help
            float x = b.X - 16;
            void IconAt(string id, float w, Action<ImDrawListPtr, Vector2, uint> icon, string tip, Action click, int badge = 0)
            {
                x -= w;
                var p = new Vector2(x, 7);
                ImGui.SetCursorScreenPos(p);
                if (ImGui.InvisibleButton(id, new Vector2(w - 4, 30))) click();
                bool hov = ImGui.IsItemHovered();
                if (hov) { ImGui.SetMouseCursor(ImGuiMouseCursor.Hand); dl.AddRectFilled(p, p + new Vector2(w - 4, 30), Neon.U(Neon.Ink, 0.07f), 7); }
                Neon.Tooltip(tip);
                icon(dl, p + new Vector2((w - 4) * 0.5f, 15), Neon.U(hov ? Neon.Ink : Neon.Dim));
                if (badge > 0)
                {
                    dl.AddCircleFilled(p + new Vector2(w - 10, 7), 7, Neon.U(Neon.Red), 16);
                    var t = badge > 9 ? "9+" : badge.ToString();
                    dl.AddText(Neon.Small, 11, p + new Vector2(w - 10 - t.Length * 3f, 0.5f), Neon.U(new Vector4(1, 1, 1, 1)), t);
                }
                _titleRects[id] = (p, p + new Vector2(w - 4, 30));
            }
            IconAt("tbhelp", 40, IconHelp, "Take the tour", () => StartTour());
            IconAt("tbbell", 40, IconBell, "Notifications", () => _centerOpen = !_centerOpen, _notes.Count(n => !n.Seen));
            int running = _chats.Running;
            IconAt("tbagent", 40, (d, c, col) => { d.AddCircleFilled(c, 5, Neon.U(running > 0 ? Neon.Amber : _chat.SignedIn == true || !string.IsNullOrWhiteSpace(_s.AnthropicApiKey) ? Neon.Lime : Neon.Dim)); },
                running > 0 ? $"Prisma Agent: {running} chat{(running == 1 ? " is" : "s are")} working" : "Prisma Agent (powered by Claude)", () => _page = Page.Chat);
            // branch chip
            var branch = Trim(_s.Branch, 34);
            ImGui.PushFont(Neon.Small);
            float bw = ImGui.CalcTextSize(branch).X + 34;
            ImGui.PopFont();
            x -= bw + 8;
            var ca = new Vector2(x, 9); var cb = ca + new Vector2(bw, 26);
            dl.AddRectFilled(ca, cb, Neon.U(Neon.Ink, 0.06f), 13);
            IconBranch(dl, ca + new Vector2(14, 13), Neon.U(Neon.Cyan));
            dl.AddText(Neon.Small, 13, ca + new Vector2(26, 5), Neon.U(Neon.Ink, 0.85f), branch);
        }

        readonly Dictionary<string, (Vector2 a, Vector2 b)> _titleRects = new();

        // ------------------------------------------------------------------ TRACKS

        void DrawTracks(Vector2 a, Vector2 b)
        {
            var dl = ImGui.GetWindowDrawList();
            PrismaTracks t; lock (_dataLock) t = _tracks;
            PageHeader(a, "TRACKS", $"{t.Runs.Count} runs recorded  ·  {t.Open.Count()} open problems  ·  every play session is folded in when the game closes");
            ImGui.SetCursorScreenPos(new Vector2(b.X - 470, a.Y + 6));
            Segmented("ttab", new[] { "OVERVIEW", "PERFORMANCE", "AUDIO", "FEATURES", "RUNS" }, _tracksTab, i => _tracksTab = i, Neon.Cyan);
            var ca = new Vector2(a.X, a.Y + 70);
            if (t.Runs.Count == 0)
            {
                Card(dl, ca, new Vector2(b.X, ca.Y + 120));
                dl.AddText(Neon.Strong, 16, ca + new Vector2(22, 22), Neon.U(Neon.Ink), "No runs yet");
                dl.AddText(Neon.Small, 14, ca + new Vector2(22, 50), Neon.U(Neon.Dim), "Press START on PLAY, play, and close the game. Prisma records the run here:");
                dl.AddText(Neon.Small, 14, ca + new Vector2(22, 72), Neon.U(Neon.Dim), "performance per scene, the modes and vessels you used, audio, and every problem.");
                return;
            }
            ImGui.SetCursorScreenPos(ca);
            ImGui.BeginChild("##tracks", b - ca);
            switch (_tracksTab)
            {
                case 0: TracksOverview(t); break;
                case 1: TracksPerf(t); break;
                case 2: TracksAudio(t); break;
                case 3: TracksFeatures(t); break;
                default: TracksRuns(t); break;
            }
            ImGui.EndChild();
        }

        static void Card(ImDrawListPtr dl, Vector2 a, Vector2 b, Vector4? tint = null)
        {
            dl.AddRectFilled(a + new Vector2(0, 2), b + new Vector2(0, 2), Neon.U(new Vector4(0, 0, 0, 1), 0.25f), 12);
            dl.AddRectFilled(a, b, Neon.U(tint ?? new Vector4(0.07f, 0.08f, 0.14f, 1f), 0.86f), 12);
            dl.AddRect(a, b, Neon.U(Neon.Ink, 0.08f), 12);
        }

        void Tile(Vector2 p, float w, string label, string value, Vector4 col)
        {
            var dl = ImGui.GetWindowDrawList();
            Card(dl, p, p + new Vector2(w, 86));
            dl.AddText(Neon.Small, 13, p + new Vector2(16, 14), Neon.U(Neon.Dim), label);
            dl.AddText(Neon.Heading, 30, p + new Vector2(16, 36), Neon.U(col), value);
        }

        static void Spark(ImDrawListPtr dl, Vector2 a, Vector2 size, IList<double> values, Vector4 col, double budget = 0)
        {
            if (values.Count == 0) return;
            double max = Math.Max(values.Max(), budget) * 1.1;
            if (max <= 0) max = 1;
            float w = size.X / Math.Max(values.Count, 1);
            if (budget > 0)
            {
                float by = a.Y + size.Y - (float)(budget / max) * size.Y;
                dl.AddLine(new Vector2(a.X, by), new Vector2(a.X + size.X, by), Neon.U(Neon.Amber, 0.35f));
            }
            for (int i = 0; i < values.Count; i++)
            {
                float h = Math.Max(2, (float)(values[i] / max) * size.Y);
                var c = budget > 0 && values[i] > budget ? Neon.Red : col;
                dl.AddRectFilled(new Vector2(a.X + i * w + 1, a.Y + size.Y - h), new Vector2(a.X + (i + 1) * w - 1, a.Y + size.Y), Neon.U(c, 0.85f), 2);
            }
        }

        static Vector4 KindColor(string kind) => kind switch
        {
            "crash" => Neon.Red, "exception" => Neon.Red, "error" => Neon.Amber, "perf" => Neon.Violet, "audio" => Neon.Cyan, _ => Neon.Dim,
        };

        void TracksOverview(PrismaTracks t)
        {
            var dl = ImGui.GetWindowDrawList();
            var p = ImGui.GetCursorScreenPos();
            float w = (ImGui.GetContentRegionAvail().X - 36) / 4;
            var recent = t.Runs.TakeLast(10).ToList();
            double crashFree = 100.0 * t.Runs.Count(r => !r.Crashed) / t.Runs.Count;
            var p95s = recent.Where(r => r.P95 > 0).Select(r => r.P95).OrderBy(x => x).ToList();
            Tile(p, w, "RUNS", t.Runs.Count.ToString(), Neon.Ink);
            Tile(p + new Vector2(w + 12, 0), w, "CRASH-FREE", $"{crashFree:0}%", crashFree >= 99 ? Neon.Lime : Neon.Amber);
            Tile(p + new Vector2((w + 12) * 2, 0), w, "MEDIAN p95 (10 RUNS)", p95s.Count > 0 ? $"{p95s[p95s.Count / 2]:0.0} ms" : "-", Neon.Cyan);
            Tile(p + new Vector2((w + 12) * 3, 0), w, "OPEN PROBLEMS", t.Open.Count().ToString(), t.Open.Any(i => i.Kind is "crash" or "exception") ? Neon.Red : Neon.Ink);
            ImGui.SetCursorScreenPos(p + new Vector2(0, 104));
            ImGui.PushFont(Neon.Strong); ImGui.TextColored(Neon.Ink, "Open problems"); ImGui.PopFont();
            foreach (var issue in t.Open.Take(12)) IssueRow(issue);
        }

        void IssueRow(PrismaTracks.Issue issue)
        {
            var dl = ImGui.GetWindowDrawList();
            var p = ImGui.GetCursorScreenPos();
            float w = ImGui.GetContentRegionAvail().X;
            Card(dl, p, p + new Vector2(w, 54));
            dl.AddCircleFilled(p + new Vector2(18, 27), 5, Neon.U(KindColor(issue.Kind)));
            dl.AddText(Neon.Small, 12, p + new Vector2(32, 8), Neon.U(KindColor(issue.Kind)), issue.Kind.ToUpperInvariant() + (issue.Area.Length > 0 ? "  ·  " + issue.Area : ""));
            dl.AddText(Neon.Body, 15, p + new Vector2(32, 26), Neon.U(Neon.Ink), Trim(issue.Message.Replace('\n', ' '), 96));
            dl.AddText(Neon.Small, 12, p + new Vector2(w - 330, 8), Neon.U(Neon.Dim), $"{issue.Runs} run{(issue.Runs == 1 ? "" : "s")}  ·  last {issue.LastSeen.ToLocalTime():MMM d HH:mm}");
            ImGui.PushID(issue.Key);
            ImGui.SetCursorScreenPos(p + new Vector2(w - 190, 12));
            if (SmallButton("FIX", 70, true)) FixWithAgent(issue.Message, issue.Kind, issue.Runs);
            ImGui.SameLine(0, 6);
            bool tracked = _board.Items.Any(i => i.IssueKey == issue.Key && i.State is not PrismaBoard.Status.Suggested and not PrismaBoard.Status.Dismissed);
            if (SmallButton(tracked ? "ON BOARD" : "TRACK", 100, !tracked))
            {
                lock (_dataLock)
                {
                    var existing = _board.Items.FirstOrDefault(i => i.IssueKey == issue.Key);
                    if (existing != null) _board.Move(existing, PrismaBoard.Status.Todo, "accepted from TRACKS");
                    else _board.Add(PrismaBoard.Kind.Bug, Trim(issue.Message, 90), issue.Message, "tracks", PrismaBoard.Status.Todo,
                                    issue.Kind is "crash" or "exception" ? 1 : 2, issue.Key, criterion: PrismaBoard.TracksCriterion(issue.LastScene));
                }
                SaveBoard();
            }
            ImGui.PopID();
            ImGui.SetCursorScreenPos(p + new Vector2(0, 62));
        }

        void TracksPerf(PrismaTracks t)
        {
            var dl = ImGui.GetWindowDrawList();
            var timed = t.Runs.Where(r => r.SimP95 > 0 || r.AllocKBPerFrame > 0).TakeLast(40).ToList();
            if (timed.Count > 0)
            {
                // Where a frame goes: simulation vs render CPU, and the garbage collector's share.
                var p = ImGui.GetCursorScreenPos();
                float w = ImGui.GetContentRegionAvail().X;
                var last = timed[^1];
                Card(dl, p, p + new Vector2(w, 70));
                dl.AddText(Neon.Strong, 16, p + new Vector2(18, 12), Neon.U(Neon.Ink), "Frame budget");
                dl.AddText(Neon.Small, 13, p + new Vector2(18, 38), Neon.U(Neon.Dim),
                    $"p95  sim {last.SimP95:0.0} ms  ·  render {last.RenderP95:0.0} ms  ·  {last.AllocKBPerFrame:0} KB allocated per frame  ·  GC {last.GcPauseMsPerFrame:0.00} ms/frame");
                Spark(dl, p + new Vector2(w - 420, 12), new Vector2(400, 46), timed.Select(r => r.GcPauseMsPerFrame).ToList(), Neon.Violet, 1);
                ImGui.SetCursorScreenPos(p + new Vector2(0, 78));
            }
            foreach (var scene in t.Runs.SelectMany(r => r.SceneP95.Keys).Distinct().OrderBy(s => s))
            {
                var xs = t.Runs.Where(r => r.SceneP95.ContainsKey(scene)).Select(r => r.SceneP95[scene]).TakeLast(40).ToList();
                var p = ImGui.GetCursorScreenPos();
                float w = ImGui.GetContentRegionAvail().X;
                Card(dl, p, p + new Vector2(w, 70));
                dl.AddText(Neon.Strong, 16, p + new Vector2(18, 12), Neon.U(Neon.Ink), scene);
                var sorted = xs.TakeLast(5).OrderBy(x => x).ToList();
                dl.AddText(Neon.Small, 13, p + new Vector2(18, 38), Neon.U(Neon.Dim), $"p95  median {sorted[sorted.Count / 2]:0.0} ms  ·  latest {xs[^1]:0.0} ms  ·  {xs.Count} runs");
                Spark(dl, p + new Vector2(w - 420, 12), new Vector2(400, 46), xs, Neon.Cyan, 16.7);
                ImGui.SetCursorScreenPos(p + new Vector2(0, 78));
            }
            ImGui.PushFont(Neon.Small);
            ImGui.TextColored(Neon.Dim, "Bars are each run's 95th-percentile frame time; the amber line is 60 fps (16.7 ms), red bars are over it. Frame budget bars are GC pause per frame; over 1 ms is a visible hitch rate.");
            ImGui.PopFont();
        }

        void TracksAudio(PrismaTracks t)
        {
            var dl = ImGui.GetWindowDrawList();
            var p = ImGui.GetCursorScreenPos();
            float w = ImGui.GetContentRegionAvail().X;
            var runs = t.Runs.TakeLast(40).ToList();
            Card(dl, p, p + new Vector2(w, 110));
            dl.AddText(Neon.Strong, 16, p + new Vector2(18, 12), Neon.U(Neon.Ink), "Sound events per run");
            dl.AddText(Neon.Small, 13, p + new Vector2(18, 36), Neon.U(Neon.Dim),
                $"latest: {runs[^1].AudioInstances} instances of {runs[^1].AudioDistinct} events  ·  {runs[^1].AudioMissing} missing from the banks  ·  {runs[^1].AudioUnwired} unwired one-shots");
            Spark(dl, p + new Vector2(18, 60), new Vector2(w - 36, 40), runs.Select(r => (double)r.AudioInstances).ToList(), Neon.Violet);
            ImGui.SetCursorScreenPos(p + new Vector2(0, 122));
            ImGui.PushFont(Neon.Strong); ImGui.TextColored(Neon.Ink, "Audio problems"); ImGui.PopFont();
            var audio = t.Open.Where(i => i.Kind == "audio").ToList();
            if (audio.Count == 0) { ImGui.PushFont(Neon.Small); ImGui.TextColored(Neon.Dim, "None open."); ImGui.PopFont(); }
            foreach (var i in audio) IssueRow(i);
        }

        void TracksFeatures(PrismaTracks t)
        {
            var dl = ImGui.GetWindowDrawList();
            void Column(string title, IEnumerable<string> values, Vector2 p, float w)
            {
                var counts = values.GroupBy(v => v).Select(g => (g.Key, n: g.Count())).OrderByDescending(x => x.n).Take(12).ToList();
                Card(dl, p, p + new Vector2(w, 46 + Math.Max(1, counts.Count) * 26));
                dl.AddText(Neon.Strong, 16, p + new Vector2(16, 12), Neon.U(Neon.Ink), title);
                int max = counts.Count > 0 ? counts.Max(c => c.n) : 1;
                for (int i = 0; i < counts.Count; i++)
                {
                    var y = p.Y + 44 + i * 26;
                    float bw = (w - 32) * counts[i].n / max;
                    dl.AddRectFilled(new Vector2(p.X + 16, y), new Vector2(p.X + 16 + bw, y + 20), Neon.U(Neon.Cyan, 0.16f), 5);
                    dl.AddText(Neon.Small, 13, new Vector2(p.X + 24, y + 3), Neon.U(Neon.Ink), Trim(counts[i].Key, 34));
                    dl.AddText(Neon.Small, 13, new Vector2(p.X + w - 46, y + 3), Neon.U(Neon.Dim), counts[i].n.ToString());
                }
                if (counts.Count == 0) dl.AddText(Neon.Small, 13, p + new Vector2(16, 44), Neon.U(Neon.Dim), "none recorded yet");
            }
            var p0 = ImGui.GetCursorScreenPos();
            float cw = (ImGui.GetContentRegionAvail().X - 24) / 3;
            Column("Game modes played", t.Runs.SelectMany(r => r.Modes.Select(PrettyScene)), p0, cw);
            Column("Vessels flown", t.Runs.SelectMany(r => r.Vessels), p0 + new Vector2(cw + 12, 0), cw);
            Column("Scenes visited", t.Runs.SelectMany(r => r.Scenes), p0 + new Vector2((cw + 12) * 2, 0), cw);
        }

        void TracksRuns(PrismaTracks t)
        {
            var dl = ImGui.GetWindowDrawList();
            foreach (var r in t.Runs.AsEnumerable().Reverse().Take(60))
            {
                var p = ImGui.GetCursorScreenPos();
                float w = ImGui.GetContentRegionAvail().X;
                Card(dl, p, p + new Vector2(w, 56), r.Crashed ? new Vector4(0.25f, 0.05f, 0.08f, 1f) : null);
                dl.AddText(Neon.Strong, 15, p + new Vector2(16, 9), Neon.U(Neon.Ink), $"{r.Time.ToLocalTime():MMM d  HH:mm}   {Trim(r.Branch ?? "", 30)}{(string.IsNullOrEmpty(r.Commit) ? "" : "@" + r.Commit)}");
                dl.AddText(Neon.Small, 13, p + new Vector2(16, 31), Neon.U(Neon.Dim),
                    Trim($"{r.Seconds / 60:0.0} min  ·  {string.Join(" > ", r.Scenes.Take(5))}  ·  p95 {r.P95:0.0} ms  ·  {r.Exceptions} exc, {r.Errors} err, {r.Warnings} warn" + (r.Crashed ? "  ·  CRASHED" : ""), 130));
                ImGui.PushID(r.Id);
                ImGui.SetCursorScreenPos(p + new Vector2(w - 112, 10));
                if (SmallButton("ANALYSE", 100, true))
                {
                    _chats.New();
                    _page = Page.Chat;
                    SendChat($"Analyse this play run in Prisma: {r.Report}. Compare it with the tracks (prisma_tracks), rank what needs fixing in the game, and suggest the fixes as board items (prisma_board_suggest).",
                        ClaudeChat.Mode.Plan);
                }
                ImGui.PopID();
                ImGui.SetCursorScreenPos(p + new Vector2(0, 62));
            }
        }

        void FixWithAgent(string message, string kind, int runs)
        {
            _chats.New();
            _page = Page.Chat;
            SendChat($"Fix this {kind} in Cosmic Shore - Prisma has seen it in {runs} run(s):\n{message}\n\nFind it in the tracks (prisma_tracks) and the game's code, reproduce it, fix it in the game, and prove the fix with the prisma tools. If its cause is in Prisma itself, say which milestone it belongs to instead.",
                (ClaudeChat.Mode)_s.ChatMode);
        }

        static string PrettyScene(string s) => s.StartsWith("Minigame") ? System.Text.RegularExpressions.Regex.Replace(s["Minigame".Length..].Replace("_Gameplay", ""), "([a-z])([A-Z])", "$1 $2") : s;

        // ------------------------------------------------------------------ BOARD

        void DrawBoard(Vector2 a, Vector2 b)
        {
            var dl = ImGui.GetWindowDrawList();
            List<PrismaBoard.Item> items; lock (_dataLock) items = _board.Items.ToList();
            var suggested = items.Where(i => i.State == PrismaBoard.Status.Suggested).ToList();
            PageHeader(a, "BOARD", $"{items.Count(i => i.Type == PrismaBoard.Kind.Bug && i.State is PrismaBoard.Status.Todo or PrismaBoard.Status.Doing)} open bugs  ·  " +
                                   $"{items.Count(i => i.Type == PrismaBoard.Kind.Task && i.State is PrismaBoard.Status.Todo or PrismaBoard.Status.Doing)} open tasks  ·  {suggested.Count} suggested");
            ImGui.SetCursorScreenPos(new Vector2(b.X - 260, a.Y + 6));
            Segmented("btab", new[] { "ALL", "BUGS", "TASKS" }, _boardTab, i => _boardTab = i, Neon.Cyan);
            bool Want(PrismaBoard.Item i) => _boardTab == 0 || (_boardTab == 1) == (i.Type == PrismaBoard.Kind.Bug);

            // add a new item
            var addP = new Vector2(a.X, a.Y + 66);
            ImGui.SetCursorScreenPos(addP);
            Segmented("ntype", new[] { "BUG", "TASK" }, _newItemType, i => _newItemType = i, Neon.Magenta);
            ImGui.SameLine(0, 10);
            float inputs = b.X - a.X - 300;
            ImGui.PushItemWidth(inputs * 0.55f);
            bool enter = ImGui.InputTextWithHint("##newitem", "Add a bug or task, then Enter", ref _newItem, 300, ImGuiInputTextFlags.EnterReturnsTrue);
            ImGui.PopItemWidth();
            ImGui.SameLine(0, 8);
            ImGui.PushItemWidth(inputs * 0.45f - 8);
            enter |= ImGui.InputTextWithHint("##newcrit", "Done when... (how to check it)", ref _newCriterion, 300, ImGuiInputTextFlags.EnterReturnsTrue);
            ImGui.PopItemWidth();
            ImGui.SameLine(0, 8);
            if ((SmallButton("ADD", 80, _newItem.Trim().Length > 0) || enter) && _newItem.Trim().Length > 0)
            {
                lock (_dataLock) _board.Add(_newItemType == 0 ? PrismaBoard.Kind.Bug : PrismaBoard.Kind.Task, _newItem, criterion: _newCriterion);
                SaveBoard();
                _newItem = _newCriterion = "";
            }

            var ca = new Vector2(a.X, addP.Y + 52);
            ImGui.SetCursorScreenPos(ca);
            ImGui.BeginChild("##board", b - ca);
            var sug = suggested.Where(Want).ToList();
            if (sug.Count > 0)
            {
                ImGui.PushFont(Neon.Strong); ImGui.TextColored(Neon.Magenta, $"Suggested by Prisma  ({sug.Count})"); ImGui.PopFont();
                foreach (var it in sug.Take(8)) SuggestionCard(it);
                ImGui.Dummy(new Vector2(0, 8));
            }
            var cols = new[] { (PrismaBoard.Status.Todo, "TO DO"), (PrismaBoard.Status.Doing, "DOING"), (PrismaBoard.Status.Done, "DONE") };
            float cw = (ImGui.GetContentRegionAvail().X - 24) / 3;
            var top = ImGui.GetCursorScreenPos();
            float maxY = top.Y;
            for (int c = 0; c < 3; c++)
            {
                var (state, name) = cols[c];
                var colItems = items.Where(i => i.State == state && Want(i)).OrderBy(i => i.Priority).ThenByDescending(i => i.Updated).Take(state == PrismaBoard.Status.Done ? 12 : 40).ToList();
                var x = top.X + c * (cw + 12);
                dl.AddText(Neon.Strong, 15, new Vector2(x + 4, top.Y), Neon.U(Neon.Dim), $"{name}  {colItems.Count}");
                float y = top.Y + 28;
                foreach (var it in colItems) y = BoardCard(it, new Vector2(x, y), cw) + 8;
                maxY = Math.Max(maxY, y);
            }
            ImGui.SetCursorScreenPos(new Vector2(top.X, maxY));
            ImGui.Dummy(new Vector2(0, 10));
            ImGui.EndChild();
        }

        void SuggestionCard(PrismaBoard.Item it)
        {
            var dl = ImGui.GetWindowDrawList();
            var p = ImGui.GetCursorScreenPos();
            float w = ImGui.GetContentRegionAvail().X;
            Card(dl, p, p + new Vector2(w, 54), new Vector4(0.13f, 0.06f, 0.16f, 1f));
            dl.AddRect(p, p + new Vector2(w, 54), Neon.U(Neon.Magenta, 0.35f), 12);
            dl.AddText(Neon.Small, 12, p + new Vector2(16, 8), Neon.U(Neon.Magenta), $"{it.Id}  ·  {it.Type.ToString().ToUpperInvariant()}  ·  from {it.Source}");
            dl.AddText(Neon.Body, 15, p + new Vector2(16, 26), Neon.U(Neon.Ink), Trim(it.Title, 100));
            float cx = Math.Min(w * 0.5f, 560);
            int room = (int)((w - 230 - cx) / 6.6f) - 11; // stop short of ACCEPT / DISMISS
            if (it.Criterion.Length > 0 && room > 12)
                dl.AddText(Neon.Small, 12, p + new Vector2(cx, 8), Neon.U(Neon.Dim), "done when: " + Trim(it.Criterion, room));
            ImGui.PushID(it.Id);
            ImGui.SetCursorScreenPos(p + new Vector2(w - 210, 12));
            if (SmallButton("ACCEPT", 100, true)) { lock (_dataLock) _board.Move(it, PrismaBoard.Status.Todo, "accepted"); SaveBoard(); }
            ImGui.SameLine(0, 6);
            if (SmallButton("DISMISS", 96, true)) { lock (_dataLock) _board.Move(it, PrismaBoard.Status.Dismissed, "dismissed"); SaveBoard(); }
            ImGui.PopID();
            ImGui.SetCursorScreenPos(p + new Vector2(0, 62));
        }

        /// <summary>One card in a column; returns its bottom edge. Click to open; open cards show the detail and actions.</summary>
        float BoardCard(PrismaBoard.Item it, Vector2 p, float w)
        {
            var dl = ImGui.GetWindowDrawList();
            bool open = _openCard == it.Id;
            var lines = open ? (it.Detail.Length > 0 ? it.Detail.Split('\n').Take(6).ToList() : new List<string>()) : new List<string>();
            if (open && it.Criterion.Length > 0) lines.Add("DONE WHEN: " + it.Criterion);
            float h = 58 + (open ? 22 * lines.Count + 46 : 0);
            Card(dl, p, p + new Vector2(w, h));
            var pc = it.Priority == 1 ? Neon.Red : it.Priority == 2 ? Neon.Amber : Neon.Dim;
            dl.AddRectFilled(p + new Vector2(0, 10), p + new Vector2(3, h - 10), Neon.U(pc), 2);
            dl.AddText(Neon.Small, 12, p + new Vector2(14, 8), Neon.U(it.Type == PrismaBoard.Kind.Bug ? Neon.Red : Neon.Cyan),
                $"{it.Id}  ·  {(it.Type == PrismaBoard.Kind.Bug ? "BUG" : "TASK")}  ·  P{it.Priority}" + (it.Source != "user" ? "  ·  " + it.Source : ""));
            dl.AddText(Neon.Body, 15, p + new Vector2(14, 28), Neon.U(Neon.Ink), Trim(it.Title, (int)(w / 8.2f)));
            if (it.CriterionMet != null)
            {
                // The acceptance check passed: evidence, not a claim.
                var pill = p + new Vector2(w - 62, 8);
                dl.AddRectFilled(pill, pill + new Vector2(50, 18), Neon.U(Neon.Lime, 0.18f), 9);
                dl.AddText(Neon.Small, 11, pill + new Vector2(12, 2), Neon.U(Neon.Lime), "MET");
            }
            ImGui.PushID(it.Id);
            ImGui.SetCursorScreenPos(p);
            if (ImGui.InvisibleButton("card", new Vector2(w, 54))) _openCard = open ? null : it.Id;
            if (ImGui.IsItemHovered()) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (open)
            {
                for (int i = 0; i < lines.Count; i++)
                    dl.AddText(Neon.Small, 13, p + new Vector2(14, 58 + i * 22), Neon.U(lines[i].StartsWith("DONE WHEN: ") ? (it.CriterionMet != null ? Neon.Lime : Neon.Cyan) : Neon.Dim), Trim(lines[i], (int)(w / 7.2f)));
                ImGui.SetCursorScreenPos(p + new Vector2(12, h - 42));
                if (it.State != PrismaBoard.Status.Todo && SmallButton("< BACK", 80, true)) MoveCard(it, it.State - 1);
                ImGui.SameLine(0, 6);
                if (it.State != PrismaBoard.Status.Done && SmallButton(it.State == PrismaBoard.Status.Todo ? "START >" : "DONE >", 86, true))
                    MoveCard(it, it.State + 1);
                ImGui.SameLine(0, 6);
                if (it.Milestone == null && SmallButton("AGENT", 76, true))
                {
                    if (it.State == PrismaBoard.Status.Todo) MoveCard(it, PrismaBoard.Status.Doing, "handed to the Prisma Agent");
                    _chats.New();
                    _page = Page.Chat;
                    SendChat($"Work on board item {it.Id} ({it.Type}): {it.Title}\n{it.Detail}\n\n" +
                             (it.Criterion.Length > 0 ? $"Acceptance criterion: {it.Criterion}\nRun that check (engine_smoke, game_* or a test) and show its result before saying the work is done. " : "Say how you proved it before saying it is done. ") +
                             "The user moves the card.", (ClaudeChat.Mode)_s.ChatMode);
                }
                if (it.Milestone != null)
                {
                    ImGui.SameLine(0, 6);
                    if (SmallButton("OPEN", 70, true)) _page = Page.Milestones;
                }
            }
            ImGui.PopID();
            return p.Y + h;
        }

        // ------------------------------------------------------------------ MILESTONES

        void DrawMilestones(Vector2 a, Vector2 b)
        {
            var dl = ImGui.GetWindowDrawList();
            var m = Milestones();
            var cps = (m?["checkpoints"] as JsonArray)?.OfType<JsonObject>().ToList() ?? new List<JsonObject>();
            int done = cps.Count(c => c["status"]?.ToString() == "done");
            PageHeader(a, "MILESTONES", m == null ? "This branch has no Port/docs/milestones.json yet" :
                $"{done} of {cps.Count} checkpoints done  ·  engine work toward the roadmap runs here, as its own session");
            if (m == null) return;
            var ca = new Vector2(a.X, a.Y + 66);
            ImGui.SetCursorScreenPos(ca);
            ImGui.BeginChild("##ms", b - ca);
            var doneIds = new HashSet<string>(cps.Where(c => c["status"]?.ToString() == "done").Select(c => c["id"]!.ToString()));
            foreach (var group in new[] { "M1", "M2" })
            {
                var gs = cps.Where(c => c["milestone"]?.ToString() == group).ToList();
                int gd = gs.Count(c => c["status"]?.ToString() == "done");
                var p = ImGui.GetCursorScreenPos();
                float w = ImGui.GetContentRegionAvail().X;
                dl.AddText(Neon.Heading, 22, p, Neon.U(Neon.Cyan), group);
                dl.AddText(Neon.Small, 14, p + new Vector2(48, 6), Neon.U(Neon.Dim), Trim(m["milestones"]?[group]?.ToString() ?? "", 120));
                // progress
                var pa = p + new Vector2(0, 32); var pb = pa + new Vector2(w, 6);
                dl.AddRectFilled(pa, pb, Neon.U(Neon.Ink, 0.08f), 3);
                if (gs.Count > 0) dl.AddRectFilled(pa, new Vector2(pa.X + w * gd / gs.Count, pb.Y), Neon.U(Neon.Lime), 3);
                ImGui.SetCursorScreenPos(p + new Vector2(0, 50));
                foreach (var c in gs) MilestoneRow(c, doneIds);
                ImGui.Dummy(new Vector2(0, 12));
            }
            ImGui.EndChild();
        }

        void MilestoneRow(JsonObject c, HashSet<string> doneIds)
        {
            var dl = ImGui.GetWindowDrawList();
            string id = c["id"]!.ToString(), title = c["title"]?.ToString() ?? "", status = c["status"]?.ToString() ?? "todo";
            var deps = (c["dependsOn"] as JsonArray)?.Select(x => x!.ToString()).ToList() ?? new();
            bool ready = deps.All(doneIds.Contains);
            var p = ImGui.GetCursorScreenPos();
            float w = ImGui.GetContentRegionAvail().X;
            Card(dl, p, p + new Vector2(w, 66));
            var sc = status == "done" ? Neon.Lime : status == "in-progress" ? Neon.Amber : ready ? Neon.Cyan : Neon.Dim;
            dl.AddCircleFilled(p + new Vector2(22, 33), 9, Neon.U(sc, status == "todo" ? 0.25f : 0.9f), 20);
            if (status == "done") { dl.AddLine(p + new Vector2(17, 33), p + new Vector2(21, 37), Neon.U(Neon.Space0), 2); dl.AddLine(p + new Vector2(21, 37), p + new Vector2(28, 29), Neon.U(Neon.Space0), 2); }
            dl.AddText(Neon.Strong, 16, p + new Vector2(42, 10), Neon.U(Neon.Ink), $"{id}  {title}");
            dl.AddText(Neon.Small, 13, p + new Vector2(42, 36), Neon.U(Neon.Dim),
                Trim($"{c["weeks"]} week{(c["weeks"]?.ToString() == "1" ? "" : "s")}  ·  {(deps.Count > 0 ? "needs " + string.Join(", ", deps) : "no dependencies")}  ·  {c["exit"]}", (int)((w - 360) / 6.6f)));
            ImGui.PushID(id);
            ImGui.SetCursorScreenPos(p + new Vector2(w - 300, 16));
            bool running = _chats.All.Any(x => x.Milestone == id && x.Busy);
            if (status != "done" && SmallButton(running ? "OPEN" : status == "in-progress" ? "CONTINUE" : "START", 110, true))
                StartMilestone(id);
            Neon.Tooltip(ready ? "Opens an engine session for this checkpoint in PLAN mode with its prompt." : "Its dependencies are not done yet - you can still start it.");
            ImGui.SameLine(0, 6);
            int idx = status switch { "in-progress" => 1, "done" => 2, _ => 0 };
            if (SmallButton(new[] { "MARK DOING", "MARK DONE", "REOPEN" }[idx], 130, true))
                SetMilestoneStatus(c, new[] { "in-progress", "done", "todo" }[idx]);
            ImGui.PopID();
            ImGui.SetCursorScreenPos(p + new Vector2(0, 74));
        }

        // ------------------------------------------------------------------ icons

        static void IconTracks(ImDrawListPtr dl, Vector2 c, uint col)
        {
            dl.AddLine(c + new Vector2(-10, 8), c + new Vector2(10, 8), col, 1.6f);
            dl.PathLineTo(c + new Vector2(-9, 4)); dl.PathLineTo(c + new Vector2(-3, -3)); dl.PathLineTo(c + new Vector2(2, 1)); dl.PathLineTo(c + new Vector2(9, -8));
            dl.PathStroke(col, ImDrawFlags.None, 2f);
        }

        static void IconBoard(ImDrawListPtr dl, Vector2 c, uint col)
        {
            for (int i = 0; i < 3; i++)
            {
                var x = c.X - 10 + i * 7.5f;
                dl.AddRect(new Vector2(x, c.Y - 9), new Vector2(x + 5.5f, c.Y + 9 - i * 4), col, 1.5f, ImDrawFlags.None, 1.5f);
            }
        }

        static void IconFlag(ImDrawListPtr dl, Vector2 c, uint col)
        {
            dl.AddLine(c + new Vector2(-7, -10), c + new Vector2(-7, 10), col, 1.8f);
            dl.AddTriangleFilled(c + new Vector2(-6, -10), c + new Vector2(9, -5), c + new Vector2(-6, 0), col);
        }

        static void IconBell(ImDrawListPtr dl, Vector2 c, uint col)
        {
            dl.PathArcTo(c + new Vector2(0, -1), 6, MathF.PI, MathF.Tau, 12);
            dl.PathLineTo(c + new Vector2(7, 5)); dl.PathLineTo(c + new Vector2(-7, 5));
            dl.PathFillConvex(col);
            dl.AddCircleFilled(c + new Vector2(0, 8), 2, col);
        }

        static void IconHelp(ImDrawListPtr dl, Vector2 c, uint col)
        {
            dl.AddCircle(c, 9, col, 20, 1.6f);
            dl.AddText(Neon.Strong, 14, c - new Vector2(4, 8), col, "?");
        }

        static void IconBranch(ImDrawListPtr dl, Vector2 c, uint col)
        {
            dl.AddCircle(c + new Vector2(-3, -5), 2, col); dl.AddCircle(c + new Vector2(-3, 5), 2, col); dl.AddCircle(c + new Vector2(4, -2), 2, col);
            dl.AddLine(c + new Vector2(-3, -3), c + new Vector2(-3, 3), col, 1.3f);
            dl.AddLine(c + new Vector2(4, 0), c + new Vector2(-2, 4), col, 1.3f);
        }
    }
}
