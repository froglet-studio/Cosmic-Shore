using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using ImGuiNET;
using Prisma;

namespace CosmicShore.Launcher
{
    /// <summary>
    /// BRANCHES: the repo's inactive branches, sorted by how much unmerged work each holds, with a
    /// checkbox per deletable branch. Uses the same GitHub sign-in as the iOS build. Every deleted branch
    /// is first saved as tag archive/&lt;branch&gt;; trunk/pipeline branches, branches with an open PR or a
    /// recent commit, and (unless allowed) LARGE branches have no checkbox. Rules: <see cref="BranchCleanup"/>.
    /// </summary>
    public sealed partial class LauncherApp
    {
        static readonly (BranchCleanup.Group g, string name, Vector4 col)[] BranchGroups =
        {
            (BranchCleanup.Group.Merged, "MERGED", Neon.Lime),
            (BranchCleanup.Group.Small, "SMALL", Neon.Cyan),
            (BranchCleanup.Group.Medium, "MEDIUM", Neon.Amber),
            (BranchCleanup.Group.Large, "LARGE", Neon.Red),
            (BranchCleanup.Group.Locked, "LOCKED", Neon.Dim),
            (BranchCleanup.Group.Active, "ACTIVE", Neon.Violet),
        };

        readonly object _brLock = new();
        List<BranchCleanup.Branch> _br = new();
        readonly HashSet<string> _brTicked = new();
        BranchCleanup? _brClient;
        BranchCleanup.Policy _brPolicy = BranchCleanup.Policy.Defaults;
        int _brTab;
        string _brFilter = "";
        bool _brAllowLarge, _brConfirm, _brBusy;
        string _brStatus = "";
        DateTime? _brLoadedAt;
        CancellationTokenSource? _brCts;

        void Reclassify()
        {
            var now = DateTime.UtcNow;
            foreach (var b in _br) (b.Group, b.Lock) = BranchCleanup.Classify(b, _brPolicy, now, _brAllowLarge);
            _brTicked.RemoveWhere(n => _br.FirstOrDefault(x => x.Name == n) is not { Lock: null, Result: null });
        }

        void LoadRepoBranches()
        {
            if (_brBusy) return;
            _brBusy = true; _brConfirm = false; _brStatus = "Signing in to GitHub";
            _brCts = new CancellationTokenSource();
            var ct = _brCts.Token;
            Task.Run(async () =>
            {
                try
                {
                    if (_tools.Git == null) _tools.Detect(_s);
                    var token = _ws.GitHubApiToken();
                    if (token == null) { _brStatus = "No GitHub sign-in. Sign in with GitHub Desktop, or paste a token in SETTINGS > Source (Contents: read & write)."; return; }
                    var client = new BranchCleanup(_s.RemoteUrl, token);
                    var policy = BranchCleanup.Policy.Load(_ws.Exists ? _ws.Dir : null);
                    _brStatus = "Listing branches";
                    var list = await client.List(ct);
                    var now = DateTime.UtcNow;
                    // Only inactive, unlocked branches need measuring; the rest are locked whatever they hold.
                    var toMeasure = list.Where(b => BranchCleanup.Classify(b, policy, now, true).group is not (BranchCleanup.Group.Locked or BranchCleanup.Group.Active)).ToList();
                    await client.Measure(toMeasure, policy, n => _brStatus = $"Measuring {n} / {toMeasure.Count} inactive branches", ct);
                    lock (_brLock)
                    {
                        _brClient = client; _brPolicy = policy;
                        _br = list.OrderBy(b => b.Date).ToList();
                        _brTicked.Clear();
                        Reclassify();
                        _brLoadedAt = DateTime.Now;
                    }
                    _brStatus = $"Loaded {list.Count} branches from {client.Repository}.";
                }
                catch (OperationCanceledException) { _brStatus = "Cancelled."; }
                catch (Exception ex) { _brStatus = "Could not load: " + ex.Message; }
                finally { _brBusy = false; }
            });
        }

        void DeleteTickedBranches()
        {
            if (_brBusy || _brClient == null) return;
            List<BranchCleanup.Branch> targets;
            lock (_brLock) targets = _br.Where(b => _brTicked.Contains(b.Name) && b.Lock == null && b.Result == null).ToList();
            if (targets.Count == 0) return;
            _brBusy = true; _brConfirm = false;
            _brCts = new CancellationTokenSource();
            var ct = _brCts.Token;
            var client = _brClient;
            Task.Run(async () =>
            {
                int done = 0, failed = 0;
                try
                {
                    foreach (var b in targets)
                    {
                        ct.ThrowIfCancellationRequested();
                        _brStatus = $"Deleting {done + failed + 1} / {targets.Count}: {b.Name}";
                        bool ok;
                        try { ok = await client.ArchiveAndDelete(b, ct); }
                        catch (Exception ex) when (ex is not OperationCanceledException) { b.Result = "failed: " + ex.Message; ok = false; }
                        if (ok) done++; else failed++;
                        lock (_brLock) _brTicked.Remove(b.Name);
                        _jobs.Log.Add(ok ? LogKind.Success : LogKind.Warn, $"branches> {b.Name}: {b.Result}");
                    }
                    _brStatus = $"Finished: {done} deleted (each saved as archive/<branch>), {failed} skipped or failed. Details in CONSOLE.";
                }
                catch (OperationCanceledException) { _brStatus = $"Stopped after {done} deleted."; }
                finally { _brBusy = false; }
            });
        }

        void DrawBranches(Vector2 a, Vector2 b)
        {
            var dl = ImGui.GetWindowDrawList();
            List<BranchCleanup.Branch> all;
            lock (_brLock) all = _br.ToList();
            int inactive = all.Count(x => x.Group is not (BranchCleanup.Group.Locked or BranchCleanup.Group.Active));
            PageHeader(a, "BRANCHES", _brLoadedAt == null
                ? "Find inactive branches and delete them. Each one is saved as an archive/ tag first."
                : $"{_brClient?.Repository}  ·  {all.Count} branches  ·  {inactive} inactive  ·  loaded {_brLoadedAt:HH:mm}");
            ImGui.SetCursorScreenPos(new Vector2(b.X - 150, a.Y + 6));
            if (_brBusy ? SmallButton("CANCEL", 140, true) : SmallButton(_brLoadedAt == null ? "LOAD" : "REFRESH", 140, true))
            {
                if (_brBusy) _brCts?.Cancel(); else LoadRepoBranches();
            }

            float y = a.Y + 66;
            if (_brStatus.Length > 0)
            {
                bool bad = _brStatus.StartsWith("Could not") || _brStatus.StartsWith("No GitHub");
                dl.AddText(Neon.Small, 14, new Vector2(a.X, y), Neon.U(bad ? Neon.Red : _brBusy ? Neon.Amber : Neon.Dim), Trim(_brStatus, 150));
                y += 26;
            }

            if (all.Count == 0)
            {
                var ca = new Vector2(a.X, y + 8);
                Card(dl, ca, ca + new Vector2(Math.Min(760, b.X - a.X), 170));
                string[] lines =
                {
                    "1.  Press LOAD. Prisma reads every branch from GitHub with your git or GitHub Desktop sign-in",
                    "    (or the token in SETTINGS > Source; it needs Contents: read & write to delete).",
                    "2.  Inactive branches are sorted by how much unmerged work they hold: MERGED holds none.",
                    "3.  Tick branches, press DELETE, then CONFIRM. Each is saved as tag archive/<branch> first,",
                    "    so  git checkout -b <branch> archive/<branch>  brings it back.",
                    "Never offered: master, development, build/*, branches with an open PR or a commit this month.",
                };
                for (int i = 0; i < lines.Length; i++)
                    dl.AddText(Neon.Small, 14, ca + new Vector2(18, 16 + i * 24), Neon.U(i == lines.Length - 1 ? Neon.Dim : Neon.Ink), lines[i]);
                return;
            }

            // tiles: one per group, click to show that group
            float tw = (b.X - a.X - 5 * 10) / 6;
            for (int i = 0; i < BranchGroups.Length; i++)
            {
                var (g, name, col) = BranchGroups[i];
                var tp = new Vector2(a.X + i * (tw + 10), y);
                Tile(tp, tw, name, all.Count(x => x.Group == g).ToString(), col);
                ImGui.SetCursorScreenPos(tp);
                if (ImGui.InvisibleButton("brtile" + i, new Vector2(tw, 86))) { _brTab = i; _brConfirm = false; }
                if (ImGui.IsItemHovered()) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                if (i == _brTab) dl.AddRect(tp, tp + new Vector2(tw, 86), Neon.U(col, 0.6f), 12, ImDrawFlags.None, 1.5f);
            }
            y += 100;

            // tools row
            var (tabGroup, tabName, tabCol) = BranchGroups[_brTab];
            ImGui.SetCursorScreenPos(new Vector2(a.X, y));
            ImGui.PushItemWidth(260);
            ImGui.InputTextWithHint("##brfilter", "Filter by name, author or message", ref _brFilter, 200);
            ImGui.PopItemWidth();
            ImGui.SameLine(0, 16);
            Toggle("Allow LARGE", () => _brAllowLarge, v => { _brAllowLarge = v; lock (_brLock) Reclassify(); },
                "LARGE branches hold 11+ commits that are in no trunk. Read Docs/BranchArchive/large/ first.");
            var q = _brFilter.Trim().ToLowerInvariant();
            var rows = all.Where(x => x.Group == tabGroup && (q.Length == 0 || (x.Name + " " + x.Author + " " + x.Message).ToLowerInvariant().Contains(q))).ToList();
            ImGui.SetCursorScreenPos(new Vector2(b.X - 330, y - 4));
            if (SmallButton("TICK ALL", 150, !_brBusy && rows.Any(x => x.Lock == null && x.Result == null)))
                lock (_brLock) foreach (var x in rows.Where(x => x.Lock == null && x.Result == null)) _brTicked.Add(x.Name);
            ImGui.SameLine(0, 10);
            if (SmallButton("CLEAR", 150, _brTicked.Count > 0)) lock (_brLock) { _brTicked.Clear(); _brConfirm = false; }
            y += 48;

            // list
            const float footH = 58;
            var la = new Vector2(a.X, y);
            ImGui.SetCursorScreenPos(la);
            ImGui.BeginChild("##branches", new Vector2(b.X - a.X, b.Y - y - footH));
            var cdl = ImGui.GetWindowDrawList();
            float w = ImGui.GetContentRegionAvail().X;
            if (rows.Count == 0) ImGui.TextColored(Neon.Dim, $"No {tabName.ToLowerInvariant()} branches" + (q.Length > 0 ? " match the filter." : "."));
            foreach (var br in rows)
            {
                var p = ImGui.GetCursorScreenPos();
                bool ticked = _brTicked.Contains(br.Name);
                if (ticked) cdl.AddRectFilled(p, p + new Vector2(w, 32), Neon.U(Neon.Red, 0.10f), 6);
                ImGui.PushID(br.Name);
                ImGui.SetCursorScreenPos(p + new Vector2(6, 5));
                if (br.Lock == null && br.Result == null)
                {
                    if (ImGui.Checkbox("##tick", ref ticked)) lock (_brLock) { if (ticked) _brTicked.Add(br.Name); else _brTicked.Remove(br.Name); _brConfirm = false; }
                }
                else cdl.AddText(Neon.Small, 12, p + new Vector2(8, 9), Neon.U(Neon.Dim), br.Result != null ? "done" : "lock");
                ImGui.PopID();
                cdl.AddText(Neon.Mono, 14, p + new Vector2(44, 8), Neon.U(Neon.Ink), Trim(br.Name, (int)(w * 0.36f / 8)));
                string uniq = br.Unique?.ToString() ?? (br.Measured ? "?" : "");
                cdl.AddText(Neon.Small, 13, p + new Vector2(w * 0.40f, 9), Neon.U(tabCol), uniq.Length > 0 ? uniq + " unmerged" : "");
                cdl.AddText(Neon.Small, 13, p + new Vector2(w * 0.50f, 9), Neon.U(Neon.Dim), br.Date.ToString("yyyy-MM-dd") + "  " + Trim(br.Author, 18));
                string note = br.Result ?? br.Lock ?? br.Message;
                var noteCol = br.Result == null ? Neon.Dim : br.Result.StartsWith("deleted") ? Neon.Lime : Neon.Amber;
                cdl.AddText(Neon.Small, 13, p + new Vector2(w * 0.68f, 9), Neon.U(noteCol), Trim(note, (int)(w * 0.32f / 7)));
                ImGui.SetCursorScreenPos(p + new Vector2(0, 34));
            }
            ImGui.Dummy(new Vector2(0, 6));
            ImGui.EndChild();

            // footer: two-step delete
            var fy = b.Y - footH + 14;
            int n = _brTicked.Count;
            dl.AddText(Neon.Strong, 17, new Vector2(a.X, fy + 8), Neon.U(n > 0 ? Neon.Ink : Neon.Dim), n == 0 ? "Nothing ticked" : $"{n} ticked for deletion");
            ImGui.SetCursorScreenPos(new Vector2(b.X - (_brConfirm ? 470 : 200), fy));
            if (!_brConfirm)
            {
                if (Neon.Button("brdelete", $"DELETE {n}...", new Vector2(200, 40), Neon.Red, Neon.Small, 15, n > 0 && !_brBusy)) _brConfirm = true;
            }
            else
            {
                if (Neon.Button("brconfirm", $"CONFIRM: DELETE {n}", new Vector2(300, 40), Neon.Red, Neon.Small, 15, !_brBusy)) DeleteTickedBranches();
                ImGui.SameLine(0, 10);
                if (SmallButton("BACK", 150, true)) _brConfirm = false;
            }
        }
    }
}
