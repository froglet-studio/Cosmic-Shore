using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using ImGuiNET;
using Prisma;

namespace CosmicShore.Launcher
{
    /// <summary>
    /// macOS-style notifications (banners top right, a notification center under the bell), the
    /// clean-ups Prisma offers but never does unasked (a stale git lock, stale build outputs, low
    /// disk, errors from the last run), and the first-run tour.
    /// </summary>
    public sealed partial class LauncherApp
    {
        public enum NoteKind { Info, Success, Warning, Error }

        sealed class Note
        {
            public string Title = "", Body = "";
            public NoteKind Kind;
            public List<(string label, Action act)> Actions = new();
            public DateTime Time = DateTime.Now;
            public float Age, Out = -1;
            public bool Seen, Handled, Banner = true;
        }

        readonly List<Note> _notes = new();
        bool _centerOpen;

        void Notify(string title, string body, NoteKind kind, params (string, Action)[] actions)
        {
            lock (_notes)
            {
                _notes.Add(new Note { Title = title, Body = body, Kind = kind, Actions = actions.ToList() });
                if (_notes.Count > 40) _notes.RemoveAt(0);
            }
        }

        void NotifyRun(PrismaTracks.IngestResult r)
        {
            var run = r.Run;
            if (run.Crashed)
            {
                var crash = _tracks.Issues.Values.Where(i => i.Kind == "crash").OrderByDescending(i => i.LastSeen).FirstOrDefault();
                Notify($"Cosmic Shore crashed{(run.Scenes.Count > 0 ? " in " + run.Scenes[^1] : "")}", crash?.Message ?? "See the run in TRACKS.", NoteKind.Error,
                    ("INVESTIGATE", () => FixWithAgent(crash?.Message ?? run.Report, "crash", 1)), ("NOT NOW", () => { }));
                return;
            }
            var problems = r.New.Concat(r.Back).ToList();
            if (problems.Count > 0)
            {
                Notify($"{problems.Count} new problem{(problems.Count == 1 ? "" : "s")} in your last run",
                    string.Join("\n", problems.Take(2).Select(i => Trim(i.Message.Replace('\n', ' '), 70))), NoteKind.Warning,
                    ("CLEAN UP", () =>
                    {
                        _chats.New();
                        _page = Page.Chat;
                        SendChat("Clean up the new problems from my last play run (prisma_tracks has them with counts):\n" +
                                 string.Join("\n", problems.Take(8).Select(i => $"- [{i.Kind}] {i.Message}")) +
                                 "\nFix the ones whose cause is in the game, prove each fix, and suggest the rest as board items.", (ClaudeChat.Mode)_s.ChatMode);
                    }),
                    ("REVIEW", () => { _page = Page.Tracks; _tracksTab = 0; }));
            }
            foreach (var reg in r.Regressions.Take(1))
                Notify("Slower than usual", reg.Message, NoteKind.Warning, ("SEE TRACKS", () => { _page = Page.Tracks; _tracksTab = 1; }));
            if (problems.Count == 0 && r.Regressions.Count == 0)
                Notify("Clean run", $"{run.Seconds / 60:0.0} min, {run.Exceptions + run.Errors} errors, p95 {run.P95:0.0} ms. Recorded in TRACKS.", NoteKind.Success);
        }

        // ------------------------------------------------------------------ doctor

        bool? _lastOkSeen;

        /// <summary>Looks for things Prisma can clean up. It only ever offers; the user decides.</summary>
        void RunDoctor()
        {
            try
            {
                // 1. a git lock left behind by a crashed git blocks every fetch
                if (_ws.Exists && _tools.Git != null)
                {
                    var rel = ProcessRunner.Capture(_tools.Git, "-C", _ws.Dir, "rev-parse", "--git-path", "index.lock");
                    var lockFile = rel == null ? null : Path.IsPathRooted(rel) ? rel : Path.Combine(_ws.Dir, rel);
                    if (lockFile != null && File.Exists(lockFile) && DateTime.Now - File.GetLastWriteTime(lockFile) > TimeSpan.FromMinutes(2))
                        Notify("A stale git lock is blocking updates", "A git process ended without removing its lock file.", NoteKind.Warning,
                            ("CLEAN UP", () => { try { File.Delete(lockFile); Notify("Lock removed", "Updates work again.", NoteKind.Success); } catch (Exception e) { Notify("Could not remove the lock", e.Message, NoteKind.Error); } }),
                            ("NOT NOW", () => { }));
                }
                // 2. low disk where Prisma keeps its workspace, versions and builds
                var drive = new DriveInfo(Path.GetPathRoot(LauncherSettings.DataDir)!);
                if (drive.IsReady && drive.AvailableFreeSpace < 3L << 30)
                    Notify($"Low disk space: {drive.AvailableFreeSpace / (double)(1L << 30):0.0} GB free",
                        "Prisma can remove old launcher versions (keeping the newest three), build caches and session reports older than 30 days.", NoteKind.Warning,
                        ("CLEAN UP", () => Task.Run(FreeSpace)), ("NOT NOW", () => { }));
            }
            catch { /* the doctor never gets in the way */ }
        }

        void FreeSpace()
        {
            long freed = 0;
            long Size(string d) { try { return new DirectoryInfo(d).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length); } catch { return 0; } }
            void Remove(string d) { freed += Size(d); try { Directory.Delete(d, true); } catch { } }
            foreach (var v in LauncherUpdater.Installed().Where(v => v.Commit != LauncherUpdater.Commit).Skip(3)) Remove(v.Dir);
            var build = Path.Combine(LauncherSettings.DataDir, "build");
            if (Directory.Exists(build)) Remove(build);
            foreach (var f in LauncherJobs.Sessions().Where(f => DateTime.Now - f.LastWriteTime > TimeSpan.FromDays(30)))
                try { freed += f.Length; f.Delete(); } catch { }
            Notify("Cleaned up", $"Freed {freed / (double)(1L << 30):0.00} GB.", NoteKind.Success);
        }

        /// <summary>Called every frame: a build that just failed gets the stale-outputs offer.</summary>
        void WatchJobs()
        {
            var ok = _jobs.LastOk;
            if (ok == _lastOkSeen) return;
            bool failedNow = ok == false && _lastOkSeen != false;
            _lastOkSeen = ok;
            if (!failedNow || !_ws.Exists) return;
            var last = string.Join("\n", _jobs.Log.Tail(80));
            bool buildFail = last.Contains("error CS", StringComparison.Ordinal) || last.Contains("MSB", StringComparison.Ordinal) || last.Contains("being used by another process");
            if (!buildFail) return;
            Notify("The build failed", "If the outputs are stale, a clean build fixes it. Prisma can delete Port's build folders and try again.", NoteKind.Error,
                ("CLEAN & RETRY", () => Task.Run(() =>
                {
                    foreach (var proj in Directory.GetDirectories(Path.Combine(_ws.Dir, "Port", "src")))
                        foreach (var sub in new[] { "obj", "bin" })
                            try { var d = Path.Combine(proj, sub); if (Directory.Exists(d)) Directory.Delete(d, true); } catch { }
                    _jobs.Play();
                })),
                ("SHOW LOG", () => _page = Page.Console));
        }

        // ------------------------------------------------------------------ drawing

        static Vector4 NoteColor(NoteKind k) => k switch { NoteKind.Success => Neon.Lime, NoteKind.Warning => Neon.Amber, NoteKind.Error => Neon.Red, _ => Neon.Cyan };

        List<string> Wrap(string text, float width, ImFontPtr font, float size)
        {
            var lines = new List<string>();
            ImGui.PushFont(font);
            foreach (var para in text.Split('\n'))
            {
                var line = "";
                foreach (var word in para.Split(' '))
                {
                    var test = line.Length == 0 ? word : line + " " + word;
                    if (ImGui.CalcTextSize(test).X * size / font.FontSize > width && line.Length > 0) { lines.Add(line); line = word; }
                    else line = test;
                }
                lines.Add(line);
            }
            ImGui.PopFont();
            return lines;
        }

        void DrawNotifications(Vector2 size, float dt)
        {
            List<Note> live;
            lock (_notes) live = _notes.Where(n => n.Banner).ToList();
            const float W = 360;
            float y = TitleH + 12;
            int shown = 0;
            foreach (var n in live.AsEnumerable().Reverse())
            {
                if (shown == 3) break;
                bool sticky = n.Actions.Count > 0 && !n.Handled;
                n.Age += Math.Min(dt, 0.05f);
                var body = Wrap(n.Body, W - 76, Neon.Small, 13).Take(3).ToList();
                float h = 58 + body.Count * 17 + (n.Actions.Count > 0 ? 40 : 0);
                if (!sticky && n.Age > 6.5f && n.Out < 0) n.Out = 0;
                if (n.Out >= 0) { n.Out += Math.Min(dt, 0.05f); if (n.Out > 0.3f) { n.Banner = false; continue; } }
                float slideIn = 1 - MathF.Pow(1 - Math.Clamp(n.Age / 0.35f, 0, 1), 3);
                float slideOut = n.Out >= 0 ? MathF.Pow(n.Out / 0.3f, 2) : 0;
                var a = new Vector2(size.X - W - 16 + (1 - slideIn + slideOut) * (W + 30), y);
                DrawNoteCard(n, a, W, h, body, banner: true);
                y += h + 10;
                shown++;
            }
            if (_centerOpen) DrawCenter(size);
        }

        void DrawNoteCard(Note n, Vector2 a, float w, float h, List<string> body, bool banner)
        {
            ImGui.SetNextWindowPos(a);
            ImGui.SetNextWindowSize(new Vector2(w, h));
            ImGui.Begin("##note" + n.GetHashCode() + (banner ? "b" : "c"), ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoFocusOnAppearing);
            var dl = ImGui.GetWindowDrawList();
            var b = a + new Vector2(w, h);
            for (int i = 4; i >= 1; i--) dl.AddRectFilled(a + new Vector2(-i, i * 1.5f - 2), b + new Vector2(i, i * 2f), Neon.U(new Vector4(0, 0, 0, 1), 0.06f), 18);
            dl.AddRectFilled(a, b, Neon.U(new Vector4(0.13f, 0.14f, 0.20f, 1f), 0.94f), 16);
            dl.AddRect(a, b, Neon.U(Neon.Ink, 0.12f), 16);
            dl.AddRectFilled(a + new Vector2(14, 16), a + new Vector2(42, 44), Neon.U(new Vector4(0.06f, 0.07f, 0.12f, 1f)), 8);
            Neon.PrismIcon(dl, a + new Vector2(28, 31), 16, 1f, Neon.Time);
            dl.AddCircleFilled(a + new Vector2(40, 18), 4, Neon.U(NoteColor(n.Kind)));
            dl.AddText(Neon.Small, 12, a + new Vector2(54, 12), Neon.U(Neon.Dim), "PRISMA");
            var ago = DateTime.Now - n.Time;
            var when = ago.TotalMinutes < 1 ? "now" : ago.TotalHours < 1 ? $"{(int)ago.TotalMinutes}m ago" : n.Time.ToString("HH:mm");
            dl.AddText(Neon.Small, 12, new Vector2(b.X - 14 - when.Length * 6.2f, a.Y + 12), Neon.U(Neon.Dim), when);
            dl.AddText(Neon.Strong, 15, a + new Vector2(54, 28), Neon.U(Neon.Ink), Trim(n.Title, 40));
            for (int i = 0; i < body.Count; i++) dl.AddText(Neon.Small, 13, a + new Vector2(54, 50 + i * 17), Neon.U(Neon.Mix(Neon.Ink, Neon.Dim, 0.4f)), body[i]);
            // hover: the close button macOS shows on a banner's corner
            bool hover = ImGui.IsMouseHoveringRect(a, b);
            if (hover) { n.Age = Math.Min(n.Age, 5f); n.Seen = true; }
            if (hover && banner)
            {
                var cc = a + new Vector2(4, 4);
                dl.AddCircleFilled(cc, 9, Neon.U(new Vector4(0.25f, 0.26f, 0.32f, 1f)), 16);
                dl.AddLine(cc - new Vector2(3.5f), cc + new Vector2(3.5f), Neon.U(Neon.Ink), 1.5f);
                dl.AddLine(cc + new Vector2(-3.5f, 3.5f), cc + new Vector2(3.5f, -3.5f), Neon.U(Neon.Ink), 1.5f);
                ImGui.SetCursorScreenPos(cc - new Vector2(9));
                if (ImGui.InvisibleButton("close", new Vector2(18))) { n.Out = 0; n.Seen = true; }
            }
            if (n.Actions.Count > 0)
            {
                float bx = a.X + 54, by = b.Y - 38;
                foreach (var (label, act) in n.Actions)
                {
                    ImGui.PushFont(Neon.Small);
                    float bw = ImGui.CalcTextSize(label).X + 26;
                    ImGui.PopFont();
                    var pa = new Vector2(bx, by); var pb = pa + new Vector2(bw, 26);
                    ImGui.SetCursorScreenPos(pa);
                    bool click = ImGui.InvisibleButton(label, pb - pa) && !n.Handled;
                    bool hov = ImGui.IsItemHovered();
                    bool primary = label == n.Actions[0].label;
                    dl.AddRectFilled(pa, pb, Neon.U(primary ? Neon.Mix(new Vector4(0.25f, 0.27f, 0.36f, 1f), NoteColor(n.Kind), 0.45f) : new Vector4(0.25f, 0.27f, 0.36f, 1f), n.Handled ? 0.4f : hov ? 1f : 0.85f), 7);
                    dl.AddText(Neon.Small, 13, pa + new Vector2(13, 5), Neon.U(Neon.Ink, n.Handled ? 0.4f : 1f), label);
                    if (click) { n.Handled = true; n.Seen = true; n.Out = 0; try { act(); } catch (Exception e) { Notify("That did not work", e.Message, NoteKind.Error); } }
                    bx += bw + 8;
                }
            }
            ImGui.End();
        }

        void DrawCenter(Vector2 size)
        {
            const float W = 380;
            var a = new Vector2(size.X - W - 8, TitleH + 6); var b = new Vector2(size.X - 8, size.Y - 48);
            ImGui.SetNextWindowPos(a);
            ImGui.SetNextWindowSize(b - a);
            ImGui.Begin("##center", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoMove);
            var dl = ImGui.GetWindowDrawList();
            dl.AddRectFilled(a, b, Neon.U(new Vector4(0.08f, 0.09f, 0.14f, 1f), 0.96f), 16);
            dl.AddRect(a, b, Neon.U(Neon.Ink, 0.1f), 16);
            dl.AddText(Neon.Strong, 17, a + new Vector2(18, 16), Neon.U(Neon.Ink), "Notifications");
            ImGui.SetCursorScreenPos(new Vector2(b.X - 96, a.Y + 10));
            if (SmallButton("CLEAR", 80, _notes.Count > 0)) lock (_notes) _notes.Clear();
            List<Note> all; lock (_notes) all = _notes.AsEnumerable().Reverse().ToList();
            float y = a.Y + 54;
            if (all.Count == 0) dl.AddText(Neon.Small, 14, a + new Vector2(18, 56), Neon.U(Neon.Dim), "Nothing yet.");
            foreach (var n in all)
            {
                var body = Wrap(n.Body, W - 30 - 76, Neon.Small, 13).Take(3).ToList();
                float h = 58 + body.Count * 17 + (n.Actions.Count > 0 ? 40 : 0);
                if (y + h > b.Y - 10) break;
                n.Seen = true;
                DrawNoteCard(n, new Vector2(a.X + 14, y), W - 28, h, body, banner: false);
                y += h + 8;
            }
            ImGui.End();
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !ImGui.IsMouseHoveringRect(a, b) &&
                !(_titleRects.TryGetValue("tbbell", out var bell) && ImGui.IsMouseHoveringRect(bell.a, bell.b)))
                _centerOpen = false;
        }

        // ------------------------------------------------------------------ first-run tour

        int _tour = -1;

        sealed record TourStep(string Title, string Body, Func<(Vector2 a, Vector2 b)?> Target, Page? Show = null);

        TourStep[] Tour => new[]
        {
            new TourStep("Welcome to Prisma",
                "Froglet's own engine for Cosmic Shore. It builds the game from source, runs it, records every run, and has an agent powered by Claude. Here is a one-minute tour.",
                () => null),
            new TourStep("Play", "Pick a branch and press START: Prisma fetches it, builds it and runs the game. Every play session is recorded when you close the game.", () => Rail(Page.Play), Page.Play),
            new TourStep("Build", "Phone builds: an Android APK, or an iOS .ipa built on GitHub's Mac for Sideloadly.", () => Rail(Page.Build), Page.Build),
            new TourStep("Project", "Prisma's own Player, Scenes and Quality settings. Empty fields follow Unity's.", () => Rail(Page.Project), Page.Project),
            new TourStep("Prisma Agent", "Powered by Claude. It works on the game, starting from what your last runs recorded. Sign in with your Claude plan, pick a model, plan first or let it edit.", () => Rail(Page.Chat), Page.Chat),
            new TourStep("Tracks", "Every run's performance per scene, the modes and vessels you used, audio, and every problem with when it was first and last seen.", () => Rail(Page.Tracks), Page.Tracks),
            new TourStep("Board", "Your bugs and tasks. Prisma suggests new ones from your runs; nothing joins the board until you accept it.", () => Rail(Page.Board), Page.Board),
            new TourStep("Notifications", "Prisma tells you what each run found and offers clean-ups - it never cleans up without asking.", () => Title("tbbell")),
            new TourStep("Settings", "Looks and themes, the Claude account, updates and every installed version. Press ? in the top bar to see this tour again.", () => Rail(Page.Options), Page.Options),
        };

        (Vector2, Vector2)? Rail(Page p) => _railRects.TryGetValue(p, out var r) ? r : null;
        (Vector2, Vector2)? Title(string id) => _titleRects.TryGetValue(id, out var r) ? r : null;

        void StartTour() { _tour = 0; _centerOpen = false; }

        void DrawTour(Vector2 size)
        {
            if (_tour < 0) return;
            var steps = Tour;
            if (_tour >= steps.Length) { _tour = -1; _s.TourDone = true; _dirty = true; return; }
            var step = steps[_tour];
            if (step.Show is { } page) _page = page;
            // One full-screen window above the app: the dim with a hole for the target, then the card.
            ImGui.SetNextWindowPos(Vector2.Zero);
            ImGui.SetNextWindowSize(size);
            ImGui.SetNextWindowFocus();
            ImGui.Begin("##tour", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoMove);
            var fg = ImGui.GetWindowDrawList();
            var target = step.Target();
            float pulse = 0.5f + 0.5f * MathF.Sin(Neon.Time * 4);
            if (target is { } t)
            {
                var (ta, tb) = (t.a - new Vector2(6), t.b + new Vector2(6));
                uint dim = Neon.U(Neon.Space0, 0.72f);
                fg.AddRectFilled(Vector2.Zero, new Vector2(size.X, ta.Y), dim);
                fg.AddRectFilled(new Vector2(0, tb.Y), size, dim);
                fg.AddRectFilled(new Vector2(0, ta.Y), new Vector2(ta.X, tb.Y), dim);
                fg.AddRectFilled(new Vector2(tb.X, ta.Y), new Vector2(size.X, tb.Y), dim);
                fg.AddRect(ta, tb, Neon.U(Neon.Cyan, 0.6f + 0.4f * pulse), 12, ImDrawFlags.None, 2.5f);
                fg.AddRect(ta - new Vector2(4), tb + new Vector2(4), Neon.U(Neon.Cyan, 0.2f * pulse), 15, ImDrawFlags.None, 2f);
            }
            else fg.AddRectFilled(Vector2.Zero, size, Neon.U(Neon.Space0, 0.78f));

            const float W = 380;
            var body = Wrap(step.Body, W - 40, Neon.Body, 15);
            float h = 92 + body.Count * 21;
            Vector2 a;
            if (target is { } tt)
            {
                bool right = tt.b.X + W + 30 < size.X;
                a = right ? new Vector2(tt.b.X + 22, Math.Clamp((tt.a.Y + tt.b.Y) * 0.5f - h * 0.5f, TitleH + 8, size.Y - h - 50))
                          : new Vector2(tt.a.X - W, tt.b.Y + 16);
                a.X = Math.Clamp(a.X, 8, size.X - W - 8);
            }
            else a = (size - new Vector2(W, h)) * 0.5f;

            var dl = fg;
            var b = a + new Vector2(W, h);
            dl.AddRectFilled(a, b, Neon.U(new Vector4(0.12f, 0.13f, 0.20f, 1f), 0.97f), 16);
            dl.AddRect(a, b, Neon.U(Neon.Cyan, 0.35f), 16);
            if (_tour == 0) Neon.PrismIcon(dl, a + new Vector2(W - 44, 34), 34, 1f, Neon.Time);
            dl.AddText(Neon.Heading, 22, a + new Vector2(20, 18), Neon.U(Neon.Cyan), step.Title);
            for (int i = 0; i < body.Count; i++) dl.AddText(Neon.Body, 15, a + new Vector2(20, 52 + i * 21), Neon.U(Neon.Ink), body[i]);
            for (int i = 0; i < steps.Length; i++)
                dl.AddCircleFilled(new Vector2(a.X + 24 + i * 12, b.Y - 22), 3, Neon.U(i == _tour ? Neon.Cyan : Neon.Dim, i == _tour ? 1f : 0.5f));
            ImGui.SetCursorScreenPos(new Vector2(b.X - (_tour > 0 ? 250 : 176), b.Y - 38));
            if (SmallButton("SKIP", 70, true)) { _tour = steps.Length; }
            ImGui.SameLine(0, 6);
            if (_tour > 0) { if (SmallButton("BACK", 70, true)) _tour--; ImGui.SameLine(0, 6); }
            if (SmallButton(_tour == 0 ? "START" : _tour == steps.Length - 1 ? "DONE" : "NEXT", 90, true)) _tour++;
            ImGui.End();
        }
    }
}
