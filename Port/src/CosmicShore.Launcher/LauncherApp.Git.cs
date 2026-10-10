using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using ImGuiNET;

namespace CosmicShore.Launcher
{
    /// <summary>
    /// The GIT page: how an agent's edits (or your own) get from Prisma's workspace to GitHub.
    /// Left: where the workspace is, every change with which chat made it, and SAVE (commit to a
    /// branch, push, pull request). Right: the selected file's diff, then the recent history.
    /// </summary>
    public sealed partial class LauncherApp
    {
        readonly SourceControl _git;
        SourceControl.Change? _gitSel;
        List<string> _gitDiff = new();
        string _gitBranch = "", _gitMessage = "", _gitName = "", _gitEmail = "";
        string? _gitNote;
        bool _gitNoteBad, _gitIdentityRead, _gitRefreshing, _gitPushed;
        DateTime _gitPolled;
        string? _gitConfirm; // the button waiting for its second click

        void RefreshGit()
        {
            if (_gitRefreshing) return;
            _gitRefreshing = true;
            Task.Run(async () =>
            {
                try
                {
                    var st = await _git.Refresh();
                    if (st != null && _gitSel != null)
                    {
                        var same = st.Changes.FirstOrDefault(c => c.Path == _gitSel.Path);
                        if (same == null) { _gitSel = null; _gitDiff = new(); }
                        else if (same != _gitSel) { _gitSel = same; _gitDiff = await _git.Diff(same); }
                    }
                }
                finally { _gitRefreshing = false; }
            });
        }

        /// <summary>A chat run ended: the GIT page re-reads the workspace, and edits made off-page get a notification.</summary>
        void OnChatRunEnded(ClaudeChat c)
        {
            RefreshGit();
            if (c.LastRunEdits > 0 && _page != Page.Git)
                Notify($"The agent changed {c.LastRunEdits} file{(c.LastRunEdits == 1 ? "" : "s")}",
                    $"In \"{Trim(c.Title, 50)}\". They stay in Amoebius's workspace until you commit them.", NoteKind.Info,
                    ("REVIEW", () => _page = Page.Git));
        }

        void GitDone(string? error, string ok)
        {
            _gitNote = error ?? ok;
            _gitNoteBad = error != null;
            if (error != null) _jobs.Log.Add(LogKind.Error, "GIT: " + error);
        }

        string SuggestedBranch()
        {
            var title = _chats.All.FirstOrDefault(c => !c.Empty && c.CurrentScope == ClaudeChat.Scope.Game)?.Title ?? "";
            return "prisma/" + SourceControl.Slug(title.Length > 0 ? title : "changes");
        }

        string SuggestedMessage(SourceControl.State st)
        {
            var chats = st.Changes.SelectMany(c => _chats.EditorsOf(System.IO.Path.Combine(_ws.Dir, c.Path))).Distinct().ToList();
            var first = chats.FirstOrDefault()?.Title;
            bool engine = st.Changes.Any(c => c.Path.StartsWith("Port/"));
            string head = first != null ? (engine ? "feat(prisma): " : "fix: ") + first : (engine ? "chore(prisma): " : "chore: ") + $"{st.Changes.Count} changes from Amoebius";
            var body = string.Join("\n", st.Changes.Take(12).Select(c => $"- {c.Path}"));
            return head + "\n\n" + body + (st.Changes.Count > 12 ? $"\n- ... and {st.Changes.Count - 12} more" : "");
        }

        bool TwoClick(string id, string label, string confirm, float w, bool enabled)
        {
            bool armed = _gitConfirm == id;
            if (!SmallButton(armed ? confirm : label, w, enabled)) return false;
            if (armed) { _gitConfirm = null; return true; }
            _gitConfirm = id;
            return false;
        }

        void DrawGit(Vector2 a, Vector2 b)
        {
            var dl = ImGui.GetWindowDrawList();
            PageHeader(a, "GIT", _ws.Exists
                ? $"Amoebius's workspace  ·  {Trim(_ws.Dir, 70)}  ·  {(_s.Workspace == WorkspaceMode.WorktreeOfMyClone ? "a worktree of your clone" : "a clone Amoebius manages")}"
                : "No workspace yet: press START on PLAY once.");
            if (!_ws.Exists || _tools.Git == null) return;
            if ((DateTime.Now - _gitPolled).TotalSeconds > 4 && !_git.Busy) { _gitPolled = DateTime.Now; RefreshGit(); }
            if (!_gitIdentityRead) { _gitIdentityRead = true; Task.Run(() => { var (n, e) = _git.Identity(); if (_gitName.Length == 0) _gitName = n; if (_gitEmail.Length == 0) _gitEmail = e; }); }
            var st = _git.Last;
            ImGui.SetCursorScreenPos(new Vector2(b.X - 250, a.Y + 6));
            if (SmallButton(_gitRefreshing ? "READING" : "REFRESH", 110, !_gitRefreshing)) RefreshGit();
            ImGui.SameLine(0, 8);
            if (SmallButton("FOLDER", 110, true)) SourceControl.OpenUrl(_ws.Dir);
            Neon.Tooltip("Open the workspace folder.");
            if (st == null) { dl.AddText(Neon.Small, 14, a + new Vector2(0, 80), Neon.U(Neon.Dim), "Reading the workspace..."); return; }

            float split = a.X + (b.X - a.X) * 0.46f;
            var la = new Vector2(a.X, a.Y + 70);

            // ---- where the workspace is
            Card(dl, la, new Vector2(split - 10, la.Y + 74));
            string where = st.Detached ? $"Detached at {st.Head}" : $"On branch {st.Branch}";
            dl.AddText(Neon.Strong, 16, la + new Vector2(18, 12), Neon.U(Neon.Ink), Trim(where, 60));
            string sub = st.Detached
                ? $"START put {_s.Branch} here. Commit to give these changes a branch of their own."
                : st.Ahead > 0 ? $"{st.Ahead} commit{(st.Ahead == 1 ? "" : "s")} not on GitHub yet" + (st.Upstream != null ? $"  ·  tracks {st.Upstream}" : "")
                : st.Upstream != null ? $"In step with {st.Upstream}" : "Not pushed yet";
            dl.AddText(Neon.Small, 13, la + new Vector2(18, 40), Neon.U(st.Ahead > 0 ? Neon.Amber : Neon.Dim), Trim(sub, 80));

            // ---- changes
            float ly = la.Y + 88;
            float saveH = 250;
            var ca = new Vector2(a.X, ly);
            var cb = new Vector2(split - 10, b.Y - saveH - 14);
            Card(dl, ca, cb);
            dl.AddText(Neon.Strong, 15, ca + new Vector2(18, 12), Neon.U(Neon.Ink), st.Changes.Count == 0 ? "No changes" : $"{st.Changes.Count} changed file{(st.Changes.Count == 1 ? "" : "s")}");
            if (st.Changes.Count > 0)
            {
                int add = st.Changes.Sum(c => c.Added), del = st.Changes.Sum(c => c.Removed);
                dl.AddText(Neon.Small, 13, ca + new Vector2(190, 14), Neon.U(Neon.Dim), $"+{add}  -{del}");
                ImGui.SetCursorScreenPos(new Vector2(cb.X - 150, ca.Y + 6));
                if (TwoClick("discardall", "DISCARD ALL", "SURE? CLICK", 140, !_git.Busy))
                    Task.Run(async () => GitDone(await _git.DiscardAll(_jobs.Log), "Every change was discarded."));
                Neon.Tooltip("Throws away every uncommitted change in the workspace. Click twice.");
            }
            else
                dl.AddText(Neon.Small, 13, ca + new Vector2(18, 40), Neon.U(Neon.Dim), "When the agent edits a file, it shows up here with which chat changed it.");
            ImGui.SetCursorScreenPos(ca + new Vector2(8, 48));
            ImGui.BeginChild("##gitchanges", cb - ca - new Vector2(16, 56));
            float rw = ImGui.GetContentRegionAvail().X;
            foreach (var c in st.Changes)
            {
                var p = ImGui.GetCursorScreenPos();
                ImGui.PushID(c.Path);
                bool sel = _gitSel?.Path == c.Path;
                if (ImGui.InvisibleButton("r", new Vector2(rw, 40)))
                {
                    _gitSel = c;
                    Task.Run(async () => _gitDiff = await _git.Diff(c));
                }
                bool hov = ImGui.IsItemHovered();
                var d = ImGui.GetWindowDrawList();
                if (sel || hov) d.AddRectFilled(p, p + new Vector2(rw, 40), Neon.U(sel ? Neon.Cyan : Neon.Ink, sel ? 0.10f : 0.05f), 6);
                var lc = c.Letter switch { 'A' => Neon.Lime, 'D' => Neon.Red, 'R' => Neon.Violet, _ => Neon.Amber };
                d.AddRectFilled(p + new Vector2(8, 10), p + new Vector2(26, 28), Neon.U(lc, 0.18f), 4);
                d.AddText(Neon.Mono, 14, p + new Vector2(12, 11), Neon.U(lc), c.Letter.ToString());
                string file = System.IO.Path.GetFileName(c.Path), dir = System.IO.Path.GetDirectoryName(c.Path)?.Replace('\\', '/') ?? "";
                d.AddText(Neon.Small, 14, p + new Vector2(36, 3), Neon.U(Neon.Ink), Trim(file, (int)(rw / 9) - 10));
                var by = _chats.EditorsOf(System.IO.Path.Combine(_ws.Dir, c.Path));
                string meta = Trim(dir, 46) + (by.Count > 0 ? "  ·  by " + Trim(Glyphs(by[0].Title.Length > 0 ? by[0].Title : "a chat"), 34) : "");
                d.AddText(Neon.Small, 12, p + new Vector2(36, 21), Neon.U(by.Count > 0 ? Neon.Mix(Neon.Dim, Neon.Cyan, 0.4f) : Neon.Dim), meta);
                string counts = c.Binary ? "bin" : $"+{c.Added} -{c.Removed}";
                d.AddText(Neon.Small, 12, p + new Vector2(rw - 140, 12), Neon.U(Neon.Dim), counts);
                if (hov || _gitConfirm == "rev" + c.Path)
                {
                    ImGui.SetCursorScreenPos(p + new Vector2(rw - 86, 1));
                    if (TwoClick("rev" + c.Path, "REVERT", "SURE?", 80, !_git.Busy))
                        Task.Run(async () => GitDone(await _git.Revert(c, _jobs.Log), $"{file} is back as it was."));
                }
                ImGui.PopID();
                ImGui.SetCursorScreenPos(p + new Vector2(0, 44));
            }
            ImGui.Dummy(Vector2.Zero);
            ImGui.EndChild();

            // ---- save: commit to a branch, push, pull request
            var sa = new Vector2(a.X, b.Y - saveH);
            var sb = new Vector2(split - 10, b.Y);
            Card(dl, sa, sb);
            dl.AddText(Neon.Strong, 15, sa + new Vector2(18, 12), Neon.U(Neon.Ink), "SAVE TO GITHUB");
            if (_gitBranch.Length == 0) _gitBranch = st.Branch ?? SuggestedBranch();
            if (_gitMessage.Length == 0 && st.Changes.Count > 0) _gitMessage = SuggestedMessage(st);
            float fw = sb.X - sa.X - 36;
            ImGui.SetCursorScreenPos(sa + new Vector2(18, 40));
            ImGui.PushItemWidth(fw);
            ImGui.InputTextWithHint("##gbranch", "branch, e.g. prisma/fix-score", ref _gitBranch, 120);
            ImGui.PopItemWidth();
            Neon.Tooltip(SourceControl.ValidBranch(_gitBranch.Trim())
                ? "The branch the commit goes on. A new name makes a new branch from where the workspace is."
                : "Not a branch name git accepts (no spaces, '..', or a trailing '/').");
            if (!SourceControl.ValidBranch(_gitBranch.Trim())) dl.AddText(Neon.Small, 12, sa + new Vector2(fw - 140, 18), Neon.U(Neon.Red), "invalid branch name");
            ImGui.SetCursorScreenPos(sa + new Vector2(18, 76));
            ImGui.PushItemWidth(fw * 0.5f - 4);
            ImGui.InputTextWithHint("##gname", "author name", ref _gitName, 80);
            Neon.Tooltip("The commit's author, read from your git config (GitHub Desktop sets it).");
            ImGui.SameLine(0, 8);
            ImGui.InputTextWithHint("##gemail", "author email", ref _gitEmail, 120);
            Neon.Tooltip("The commit's author email, read from your git config (GitHub Desktop sets it).");
            ImGui.PopItemWidth();
            ImGui.SetCursorScreenPos(sa + new Vector2(18, 108));
            ImGui.InputTextMultiline("##gmsg", ref _gitMessage, 4000, new Vector2(fw, 70));
            bool canCommit = st.Changes.Count > 0 && !_git.Busy && SourceControl.ValidBranch(_gitBranch.Trim()) && _gitMessage.Trim().Length > 0;
            ImGui.SetCursorScreenPos(sa + new Vector2(18, 188));
            if (SmallButton("COMMIT", 110, canCommit))
            {
                string br = _gitBranch.Trim(), msg = _gitMessage;
                Task.Run(async () => { var e = await _git.Commit(br, msg, _gitName, _gitEmail, _jobs.Log); GitDone(e, $"Committed to {br}. PUSH sends it to GitHub."); if (e == null) _gitMessage = ""; });
            }
            Neon.Tooltip("Commits every change to the branch above, on this PC only.");
            ImGui.SameLine(0, 8);
            bool canPush = !_git.Busy && (canCommit || (st.Branch != null && (st.Ahead > 0 || st.Upstream == null)));
            if (SmallButton(st.Changes.Count > 0 ? "COMMIT + PUSH" : "PUSH", 150, canPush))
            {
                string br = _gitBranch.Trim(), msg = _gitMessage;
                bool commit = st.Changes.Count > 0;
                Task.Run(async () =>
                {
                    var e = commit ? await _git.Commit(br, msg, _gitName, _gitEmail, _jobs.Log) : null;
                    if (e == null) e = await _git.Push(_jobs.Log);
                    GitDone(e, $"{br} is on GitHub. OPEN PR to ask for it to be merged into {_s.Branch}.");
                    if (e == null) { _gitMessage = ""; _gitPushed = true; }
                });
            }
            Neon.Tooltip("Commits (if there are changes) and pushes the branch to GitHub.");
            ImGui.SameLine(0, 8);
            bool canPr = st.Branch != null && st.Upstream != null && st.Branch != _s.Branch && _git.WebUrl != null;
            if (SmallButton("OPEN PR", 110, canPr)) SourceControl.OpenUrl(_git.PullRequestUrl(st.Branch!, _s.Branch)!);
            Neon.Tooltip($"Opens GitHub's pull request page: {st.Branch ?? "your branch"} into {_s.Branch}.");
            string hint = _gitNote ?? (_s.Workspace == WorkspaceMode.WorktreeOfMyClone
                ? "This workspace shares your clone: a commit here shows up in GitHub Desktop as a local branch right away."
                : _gitPushed || st.Upstream != null ? "In GitHub Desktop: Fetch origin, then pick the branch to see it."
                : "Commit, then push. In GitHub Desktop: Fetch origin and pick the branch.");
            dl.AddText(Neon.Small, 12, sa + new Vector2(18, 232), Neon.U(_gitNote == null ? Neon.Dim : _gitNoteBad ? Neon.Red : Neon.Lime), Trim(hint, (int)(fw / 6.2f)));

            // ---- right: the diff, then the history
            var da = new Vector2(split + 10, a.Y + 70);
            float histH = 270;
            var db = new Vector2(b.X, b.Y - histH - 14);
            Card(dl, da, db);
            dl.AddText(Neon.Strong, 15, da + new Vector2(18, 12), Neon.U(Neon.Ink), _gitSel != null ? Trim(_gitSel.Path, 70) : "Diff");
            ImGui.SetCursorScreenPos(da + new Vector2(10, 42));
            ImGui.BeginChild("##gitdiff", db - da - new Vector2(20, 50), ImGuiChildFlags.None, ImGuiWindowFlags.HorizontalScrollbar);
            if (_gitSel == null)
            {
                ImGui.PushFont(Neon.Small); ImGui.TextColored(Neon.Dim, "Pick a file on the left to see what changed in it."); ImGui.PopFont();
            }
            else
            {
                ImGui.PushFont(Neon.Mono);
                foreach (var line in _gitDiff.ToList())
                {
                    var col = line.StartsWith("@@") ? Neon.Violet : line.StartsWith('+') ? Neon.Lime : line.StartsWith('-') ? Neon.Red : Neon.Dim;
                    if (line.StartsWith('+') || line.StartsWith('-'))
                    {
                        var p = ImGui.GetCursorScreenPos();
                        ImGui.GetWindowDrawList().AddRectFilled(p, p + new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetTextLineHeight()), Neon.U(col, 0.07f));
                    }
                    ImGui.TextColored(col, line.Length == 0 ? " " : Glyphs(line.Replace("\t", "    ")));
                }
                ImGui.PopFont();
            }
            ImGui.EndChild();

            var ha = new Vector2(split + 10, b.Y - histH);
            Card(dl, ha, b);
            dl.AddText(Neon.Strong, 15, ha + new Vector2(18, 12), Neon.U(Neon.Ink), "HISTORY");
            dl.AddText(Neon.Small, 12, ha + new Vector2(100, 15), Neon.U(Neon.Dim), "amber = on this PC only, not on GitHub yet  ·  click to open on GitHub");
            ImGui.SetCursorScreenPos(ha + new Vector2(8, 40));
            ImGui.BeginChild("##githist", b - ha - new Vector2(16, 48));
            float hw = ImGui.GetContentRegionAvail().X;
            foreach (var e in st.Log)
            {
                var p = ImGui.GetCursorScreenPos();
                ImGui.PushID(e.Sha);
                if (ImGui.InvisibleButton("h", new Vector2(hw, 26)) && !e.Unpushed && _git.CommitUrl(e.Sha) is { } u) SourceControl.OpenUrl(u);
                var d = ImGui.GetWindowDrawList();
                if (ImGui.IsItemHovered()) d.AddRectFilled(p, p + new Vector2(hw, 26), Neon.U(Neon.Ink, 0.05f), 5);
                d.AddCircleFilled(p + new Vector2(12, 13), 4, Neon.U(e.Unpushed ? Neon.Amber : Neon.Cyan));
                d.AddText(Neon.Mono, 13, p + new Vector2(24, 5), Neon.U(e.Unpushed ? Neon.Amber : Neon.Dim), e.Sha);
                d.AddText(Neon.Small, 13, p + new Vector2(96, 5), Neon.U(Neon.Ink), Trim(Glyphs(e.Subject), (int)((hw - 290) / 6.8f)));
                d.AddText(Neon.Small, 12, p + new Vector2(hw - 180, 6), Neon.U(Neon.Dim), Trim($"{e.Author}  ·  {e.When}", 30));
                ImGui.PopID();
                ImGui.SetCursorScreenPos(p + new Vector2(0, 28));
            }
            ImGui.Dummy(Vector2.Zero);
            ImGui.EndChild();
        }
    }
}
