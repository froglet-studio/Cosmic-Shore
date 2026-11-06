using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CosmicShore.Editor.Froglet
{
    /// <summary>
    /// FrogletTools ▸ Build ▸ <b>Source Control</b> — GitHub Desktop's day-to-day work inside the editor, plus the
    /// checks Desktop never runs (Docs/SOURCE_CONTROL.md).
    ///
    /// <para>Changes (tick, diff, discard, commit), History, Branches (switch, new from bleeding-edge), Fetch,
    /// Pull, Push, Update from bleeding-edge, and Open pull request (GitHub's own page, filled from the repo's
    /// template). Before a commit and before a push it runs <c>Tools/Build/push_checks.py</c> — the stale base,
    /// FMOD events saved nowhere, Unity re-saves that collide with branches in flight, junk files, build-scene and
    /// define changes, .meta pairs — and the Checks tab runs the /ship gates (<c>Tools/Build/ship_gates.py</c>).
    /// Rules live in those python files; this window draws their answer and runs git.</para>
    ///
    /// <para>A git client, not an asset tool: it writes no assets of its own (it commits and discards the
    /// person's), so it records nothing on the tool ledger and draws no ship panel. Every git and python call
    /// runs on a worker thread; only the continuation touches Unity.</para>
    /// </summary>
    public sealed class FrogletSourceControlWindow : EditorWindow
    {
        const string FetchMinutesPref = "Froglet.SourceControl.FetchMinutes";
        const string ClaudeUrl = "https://claude.ai/code";
        static readonly string[] Tabs = { "Changes", "History", "Branches", "Checks" };

        // ── State (written only on the main thread) ──────────────────────────────
        int _tab;
        string _branch = string.Empty;
        string _upstream;
        int _ahead, _behind, _baseMerges, _baseCommits;
        bool _baseKnown;
        List<ScChange> _status = new();
        List<ScRow> _rows = new();
        readonly HashSet<string> _unticked = new(StringComparer.Ordinal);
        string _selectedRow;
        string _diff = string.Empty;
        List<ScCommit> _history = new();
        string _selectedCommit;
        List<string> _commitFiles = new();
        List<ScBranch> _branches = new();
        string _branchFilter = string.Empty;
        string _newBranch = string.Empty;
        string _summary = string.Empty;
        string _description = string.Empty;
        ScCheckReport _report;
        string _reportFor = string.Empty;
        readonly List<string> _gateLines = new();
        string _gateVerdict;
        string _status1 = string.Empty;
        bool _statusIsError;
        DateTime _lastFetch = DateTime.MinValue;
        Vector2 _listScroll, _diffScroll, _pageScroll, _sideScroll;

        // ── Background work ──────────────────────────────────────────────────────
        bool _busy;
        string _busyLabel = string.Empty;
        readonly ConcurrentQueue<Action> _main = new();
        readonly ConcurrentQueue<string> _gateStream = new();

        /// <summary>Set by a button, run after the GUI pass — mutating state mid-layout throws.</summary>
        Action _deferred;

        [MenuItem("FrogletTools/Build/Source Control", false, 130)]
        [FrogletTool(FrogletToolCategory.Build, Importance = 5,
            Description = "GitHub Desktop inside Unity: commit, pull, push, branches, a pull request, and the /ship checks before every push.",
            DocPath = "Docs/SOURCE_CONTROL.md")]
        public static void Open()
        {
            var w = GetWindow<FrogletSourceControlWindow>("Source Control");
            w.minSize = new Vector2(820f, 560f);
            w.Show();
        }

        void OnEnable()
        {
            // Resolved on the main thread: FrogletGit reads Application.dataPath, which a worker may not.
            _ = FrogletGit.RepoRoot;
            _ = FrogletGit.TopLevel;
            wantsMouseMove = true;
            EditorApplication.update += Pump;
            RefreshAll();
        }

        void OnDisable() => EditorApplication.update -= Pump;

        void OnFocus()
        {
            if (!_busy) RefreshAll();
        }

        static int FetchMinutes
        {
            get => EditorPrefs.GetInt(FetchMinutesPref, 5);
            set => EditorPrefs.SetInt(FetchMinutesPref, Mathf.Clamp(value, 0, 120));
        }

        void Pump()
        {
            var any = false;
            while (_main.TryDequeue(out var a))
            {
                try { a(); }
                catch (Exception e) { SetStatus(e.Message, true); }
                any = true;
            }
            while (_gateStream.TryDequeue(out var line)) { _gateLines.Add(line); any = true; }
            if (any) Repaint();
            Animate();

            var minutes = FetchMinutes;
            if (minutes > 0 && !_busy && !EditorApplication.isPlayingOrWillChangePlaymode && FrogletGit.IsAvailable &&
                (DateTime.UtcNow - _lastFetch).TotalMinutes >= minutes)
            {
                _lastFetch = DateTime.UtcNow;
                Run("Fetching", () =>
                {
                    var r = FrogletSourceControl.Fetch();
                    var snap = Snapshot();
                    return () => { Apply(snap); if (!r.Ok) SetStatus("Fetch failed: " + FirstLine(r.Text), true); };
                }, quiet: true);
            }
        }

        /// <summary>Run <paramref name="work"/> on a worker thread; it returns what to do on the main thread.</summary>
        void Run(string label, Func<Action> work, bool quiet = false)
        {
            if (_busy) return;
            _busy = true;
            _busyLabel = label;
            if (!quiet) SetStatus(label + "…", false);
            Task.Run(() =>
            {
                Action then;
                try { then = work(); }
                catch (Exception e) { then = () => SetStatus(label + " failed: " + e.Message, true); }
                _main.Enqueue(() =>
                {
                    _busy = false;
                    _busyLabel = string.Empty;
                    then?.Invoke();
                });
            });
            Repaint();
        }

        void SetStatus(string s, bool error)
        {
            _status1 = s ?? string.Empty;
            _statusIsError = error;
        }

        static string FirstLine(string s)
        {
            s = (s ?? string.Empty).Trim();
            var i = s.IndexOf('\n');
            return i < 0 ? s : s.Substring(0, i);
        }

        // ── Snapshot: everything a refresh reads, gathered on the worker ─────────

        sealed class Snap
        {
            public string Branch, Upstream;
            public int Ahead, Behind, BaseMerges, BaseCommits;
            public bool BaseKnown;
            public List<ScChange> Status;
            public List<ScCommit> History;
            public List<ScBranch> Branches;
        }

        static Snap Snapshot()
        {
            var s = new Snap
            {
                Branch = FrogletGit.CurrentBranch(),
                Upstream = FrogletSourceControl.Upstream(),
                Status = FrogletSourceControl.Status(),
                History = FrogletSourceControl.History(),
                Branches = FrogletSourceControl.Branches(),
            };
            FrogletSourceControl.AheadBehind(s.Upstream, out s.Behind, out s.Ahead);
            s.BaseKnown = FrogletSourceControl.BehindBase(out s.BaseMerges, out s.BaseCommits);
            return s;
        }

        void Apply(Snap s)
        {
            _branch = s.Branch;
            _upstream = s.Upstream;
            _ahead = s.Ahead;
            _behind = s.Behind;
            _baseKnown = s.BaseKnown;
            _baseMerges = s.BaseMerges;
            _baseCommits = s.BaseCommits;
            _status = s.Status;
            _rows = FrogletSourceControl.GroupWithMetas(s.Status);
            _history = s.History;
            _branches = s.Branches;
            var live = new HashSet<string>(_rows.Select(r => r.Path), StringComparer.Ordinal);
            _unticked.RemoveWhere(p => !live.Contains(p));
            if (_selectedRow != null && !live.Contains(_selectedRow)) { _selectedRow = null; _diff = string.Empty; }
        }

        void RefreshAll()
        {
            if (!FrogletGit.IsAvailable) return;
            Run("Reading the working tree", () =>
            {
                var snap = Snapshot();
                return () => { Apply(snap); if (_status1.EndsWith("…")) SetStatus(string.Empty, false); };
            }, quiet: true);
        }

        // ── Unity-side guards ────────────────────────────────────────────────────

        /// <summary>Save what the editor holds in memory, so git sees it. False when the person cancelled.</summary>
        static bool SaveEverything()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
            AssetDatabase.SaveAssets();
            return true;
        }

        /// <summary>Operations that rewrite files under the editor's feet are refused while the game runs.</summary>
        bool RefuseWhilePlaying(string what)
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode) return false;
            SetStatus("Stop Play mode before you " + what + ": files would change under the running game.", true);
            return true;
        }

        bool HasTrackedChanges => _status.Any(c => !c.IsUntracked);

        static void AfterFilesChanged() => AssetDatabase.Refresh();
        // ── Drawing (FrogletSeaTheme: the game's colour set over the menu's sea lines) ──

        const float Pad = 16f;
        const float RowH = 28f;
        const string StillSeaPref = "Froglet.SourceControl.StillSea";
        double _lastPaint;

        static bool StillSea
        {
            get => EditorPrefs.GetBool(StillSeaPref, false);
            set => EditorPrefs.SetBool(StillSeaPref, value);
        }

        double SeaTime => StillSea ? 0.0 : EditorApplication.timeSinceStartup;

        /// <summary>The sea drifts while the window is looked at (about 15 frames a second); otherwise it holds.</summary>
        void Animate()
        {
            var now = EditorApplication.timeSinceStartup;
            var watched = focusedWindow == this || mouseOverWindow == this;
            if ((watched && !StillSea || _busy) && now - _lastPaint > 1.0 / 15.0) Repaint();
        }

        void OnGUI()
        {
            FrogletSeaTheme.BeginFrame();
            if (Event.current.type == EventType.Repaint) _lastPaint = EditorApplication.timeSinceStartup;
            if (Event.current.type == EventType.MouseMove) Repaint(); // hover states
            var p = FrogletSeaTheme.Current;
            var w = position.width;
            var h = position.height;
            FrogletSeaTheme.Sea(new Rect(0, 0, w, h), SeaTime);

            var y = DrawHeader(w);
            if (!FrogletGit.IsAvailable)
            {
                var r = new Rect(Pad, y, w - 2 * Pad, 90);
                FrogletSeaTheme.Panel(r);
                FrogletSeaTheme.Stripe(r, p.Danger);
                GUI.Label(new Rect(r.x + 16, r.y + 12, r.width - 32, r.height - 24),
                    "No git found: not on PATH, not inside GitHub Desktop, not in Program Files. Install Git " +
                    "(https://git-scm.com/downloads) or GitHub Desktop, then reopen this window.", FrogletSeaTheme.BodyWrapped(p.Text));
                return;
            }

            y = DrawStatusRow(y, w);
            y = DrawActions(y, w);
            y = DrawMessage(y, w);

            var labels = new[]
            {
                _rows.Count > 0 ? $"Changes  {_rows.Count}" : "Changes", "History", "Branches",
                _report == null ? "Checks" : $"Checks  {_report.findings.Count(f => f.severity != "info")}",
            };
            var tab = FrogletSeaTheme.Tabs(new Rect(Pad, y, w - 2 * Pad, 32), labels, _tab);
            if (tab != _tab) { _tab = tab; _pageScroll = Vector2.zero; }
            y += 42;

            var body = new Rect(Pad, y, w - 2 * Pad, Mathf.Max(120, h - y - Pad));
            switch (_tab)
            {
                case 0: DrawChanges(body); break;
                case 1: DrawHistory(body); break;
                case 2: DrawBranches(body); break;
                default: DrawChecks(body); break;
            }

            if (_deferred != null && Event.current.type == EventType.Repaint)
            {
                var d = _deferred;
                _deferred = null;
                EditorApplication.delayCall += () => d();
            }
        }

        float DrawHeader(float w)
        {
            var p = FrogletSeaTheme.Current;
            var y = 14f;
            GUI.Label(new Rect(Pad, y, w - 2 * Pad - 190, 26), "SOURCE CONTROL", FrogletSeaTheme.Title);
            var themeLabel = "Theme  " + p.Name;
            var tw = FrogletSeaTheme.ButtonWidth(themeLabel);
            if (FrogletSeaTheme.Button(new Rect(w - Pad - tw, y + 1, tw, 26), themeLabel, FrogletSeaTheme.ButtonKind.Quiet, true,
                    "The game's colour sets, as the window's theme. " + p.Tagline))
                ThemeMenu();
            y += 30;
            GUI.Label(new Rect(Pad, y, w - 2 * Pad, 16),
                "Commit, pull and push without leaving Unity. The /ship checks run before every commit and push.", FrogletSeaTheme.Subtitle);
            return y + 26;
        }

        void ThemeMenu()
        {
            var menu = new GenericMenu();
            foreach (var t in FrogletSeaTheme.Themes)
            {
                var id = t.Id;
                menu.AddItem(new GUIContent(t.Name), FrogletSeaTheme.Current.Id == id, () => { FrogletSeaTheme.SetTheme(id); Repaint(); });
            }
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Still sea (no motion)"), StillSea, () => { StillSea = !StillSea; Repaint(); });
            menu.ShowAsContext();
        }

        float DrawStatusRow(float y, float w)
        {
            var p = FrogletSeaTheme.Current;
            var x = Pad;
            var protectedBranch = FrogletGit.IsProtectedBranch(_branch);

            void Chip(string label, Color c, bool filled = false)
            {
                var cw = FrogletSeaTheme.PillWidth(label);
                FrogletSeaTheme.Pill(new Rect(x, y, cw, 22), label, c, filled);
                x += cw + 8;
            }

            Chip("Branch  " + (string.IsNullOrEmpty(_branch) ? "?" : _branch), protectedBranch ? p.Danger : p.Selection);
            Chip(_upstream == null ? "Not on GitHub yet" : $"{_behind} to pull · {_ahead} to push", p.Data);
            if (_baseKnown)
                Chip(_baseMerges == 0 ? "Up to date with bleeding-edge" : $"{_baseMerges} PRs behind bleeding-edge",
                     _baseMerges >= 10 ? p.Danger : p.Data);
            if (_busy)
                GUI.Label(new Rect(x + 4, y, w - x - Pad, 22), _busyLabel + "…", FrogletSeaTheme.BodySoft);
            y += 30;
            if (_busy) FrogletSeaTheme.Working(new Rect(Pad, y - 4, w - 2 * Pad, 2), EditorApplication.timeSinceStartup);
            return y + 4;
        }

        float DrawActions(float y, float w)
        {
            var idle = !_busy;
            var buttons = new List<(string label, FrogletSeaTheme.ButtonKind kind, bool enabled, string tip, Action act)>
            {
                ("Refresh", FrogletSeaTheme.ButtonKind.Quiet, idle, "Read the working tree again.", RefreshAll),
                ("Fetch", FrogletSeaTheme.ButtonKind.Quiet, idle, "Ask GitHub what changed. Changes no file.", DoFetch),
                (_behind > 0 ? $"Pull {_behind}" : "Pull", _behind > 0 ? FrogletSeaTheme.ButtonKind.Primary : FrogletSeaTheme.ButtonKind.Quiet,
                    idle && _upstream != null, "Bring this branch's new commits from GitHub.", DoPull),
                (_upstream == null ? "Publish branch" : _ahead > 0 ? $"Push {_ahead}" : "Push",
                    _ahead > 0 || _upstream == null ? FrogletSeaTheme.ButtonKind.Cta : FrogletSeaTheme.ButtonKind.Quiet,
                    idle && (_ahead > 0 || _upstream == null) && !FrogletGit.IsProtectedBranch(_branch),
                    "Run the checks, then send your commits to GitHub.", DoPush),
                ("Update from bleeding-edge", _baseMerges > 0 ? FrogletSeaTheme.ButtonKind.Primary : FrogletSeaTheme.ButtonKind.Quiet,
                    idle && _baseKnown && _baseCommits > 0, "Merge the latest bleeding-edge into this branch. Conflicts are undone and named.",
                    DoUpdateFromBase),
                ("Open pull request", FrogletSeaTheme.ButtonKind.Primary, idle && _upstream != null && !FrogletGit.IsProtectedBranch(_branch),
                    "GitHub's pull request page for this branch, filled from the template.", DoOpenPullRequest),
            };
            var x = Pad;
            foreach (var b in buttons)
            {
                var bw = FrogletSeaTheme.ButtonWidth(b.label, 76f);
                if (x + bw > w - Pad && x > Pad) { x = Pad; y += 38; }
                if (FrogletSeaTheme.Button(new Rect(x, y, bw, 30), b.label, b.kind, b.enabled, b.tip))
                    _deferred = b.act;
                x += bw + 8;
            }
            return y + 42;
        }

        float DrawMessage(float y, float w)
        {
            if (string.IsNullOrEmpty(_status1)) return y;
            var p = FrogletSeaTheme.Current;
            var style = FrogletSeaTheme.BodyWrapped(p.Text);
            var textW = w - 2 * Pad - 64;
            var th = Mathf.Max(18f, style.CalcHeight(new GUIContent(_status1), textW));
            var r = new Rect(Pad, y, w - 2 * Pad, th + 20);
            FrogletSeaTheme.Panel(r, FrogletSeaTheme.SliverSmall);
            FrogletSeaTheme.Stripe(r, _statusIsError ? p.Danger : p.Selection);
            GUI.Label(new Rect(r.x + 16, r.y + 10, textW, th), _status1, style);
            if (FrogletSeaTheme.Button(new Rect(r.xMax - 38, r.y + 8, 28, 22), "×", FrogletSeaTheme.ButtonKind.Quiet, true, "Dismiss"))
                _deferred = () => SetStatus(string.Empty, false);
            return r.yMax + 12;
        }

        // ── Changes ──────────────────────────────────────────────────────────────

        void DrawChanges(Rect body)
        {
            var leftW = Mathf.Clamp(body.width * 0.42f, Mathf.Min(320f, body.width - 260f), body.width - 260f);
            var left = new Rect(body.x, body.y, leftW, body.height);
            var right = new Rect(left.xMax + 12, body.y, body.width - leftW - 12, body.height);
            FrogletSeaTheme.Panel(left);
            FrogletSeaTheme.Panel(right);

            const float commitH = 196f;
            var list = new Rect(left.x, left.y, left.width, left.height - commitH);
            DrawChangeList(list);
            DrawCommitBox(new Rect(left.x, list.yMax, left.width, commitH));
            DrawDiff(right);
        }

        void DrawChangeList(Rect r)
        {
            var p = FrogletSeaTheme.Current;
            var ticked = _rows.Count(row => !_unticked.Contains(row.Path));
            var head = new Rect(r.x + 14, r.y + 12, r.width - 28, 22);
            var all = ticked == _rows.Count && _rows.Count > 0;
            var some = ticked > 0 && !all;
            var now = FrogletSeaTheme.Check(new Rect(head.x, head.y + 4, 15, 15), all, some);
            if (now != all)
            {
                if (now) _unticked.Clear();
                else foreach (var row in _rows) _unticked.Add(row.Path);
            }
            GUI.Label(new Rect(head.x + 24, head.y, 220, 22), $"{ticked} OF {_rows.Count} TICKED", FrogletSeaTheme.Label);
            if (FrogletSeaTheme.Button(new Rect(head.xMax - 92, head.y - 1, 92, 24), "Discard", FrogletSeaTheme.ButtonKind.Danger,
                    !_busy && ticked > 0, "Throw away the ticked changes (asks first)."))
                _deferred = DoDiscardTicked;

            var view = new Rect(r.x + 4, head.yMax + 8, r.width - 8, r.height - (head.yMax + 8 - r.y) - 4);
            FrogletSeaTheme.Rule(new Rect(r.x + 14, view.y - 4, r.width - 28, 1));
            if (_rows.Count == 0)
            {
                GUI.Label(new Rect(view.x + 12, view.y + 10, view.width - 24, 20), "No changes. Everything is committed.", FrogletSeaTheme.BodySoft);
                return;
            }
            var content = new Rect(0, 0, view.width - 14, _rows.Count * RowH);
            _listScroll = FrogletSeaTheme.BeginScroll(view, _listScroll, content);
            var first = Mathf.Max(0, (int)(_listScroll.y / RowH));
            var last = Mathf.Min(_rows.Count, first + (int)(view.height / RowH) + 2);
            for (var i = first; i < last; i++)
                DrawChangeRow(new Rect(0, i * RowH, content.width, RowH), _rows[i], p);
            FrogletSeaTheme.EndScroll();
        }

        void DrawChangeRow(Rect r, ScRow row, FrogletSeaTheme.Palette p)
        {
            var e = Event.current;
            var selected = row.Path == _selectedRow;
            if (selected)
            {
                FrogletSeaTheme.Fill(r, FrogletSeaTheme.WithAlpha(p.Selection, 0.13f));
                FrogletSeaTheme.Stripe(r, p.Selection);
            }
            else if (r.Contains(e.mousePosition))
                FrogletSeaTheme.Fill(r, FrogletSeaTheme.WithAlpha(p.SurfaceRaised, 0.25f));

            var was = !_unticked.Contains(row.Path);
            var now = FrogletSeaTheme.Check(new Rect(r.x + 10, r.y + 6.5f, 15, 15), was);
            if (now != was) { if (now) _unticked.Remove(row.Path); else _unticked.Add(row.Path); }

            var c = row.Change;
            var (glyph, color) = c.IsConflicted ? ("!", p.Danger) : c.Kind == '+' ? ("+", p.Data) : c.Kind == '-' ? ("-", p.Danger)
                               : c.Kind == '>' ? (">", p.Data) : ("~", p.TextSoft);
            var g = FrogletSeaTheme.LabelStrong;
            var prev = g.normal.textColor;
            g.normal.textColor = color;
            if (e.type == EventType.Repaint) GUI.Label(new Rect(r.x + 32, r.y, 14, r.height), glyph, g);
            g.normal.textColor = prev;

            var slash = row.Path.LastIndexOf('/');
            var name = slash < 0 ? row.Path : row.Path.Substring(slash + 1);
            var folder = slash < 0 ? string.Empty : row.Path.Substring(0, slash);
            var metaW = row.Meta != null ? 44f : 0f;
            var textR = new Rect(r.x + 50, r.y, r.width - 58 - metaW, r.height);
            var nameW = Mathf.Min(FrogletSeaTheme.Body.CalcSize(new GUIContent(name)).x, textR.width);
            if (e.type == EventType.Repaint)
            {
                GUI.Label(new Rect(textR.x, r.y, nameW, r.height), new GUIContent(name, row.Path), FrogletSeaTheme.Body);
                if (folder.Length > 0 && textR.width - nameW > 30)
                    GUI.Label(new Rect(textR.x + nameW + 8, r.y + 1, textR.width - nameW - 8, r.height), folder, FrogletSeaTheme.BodySoft);
            }
            if (row.Meta != null)
                FrogletSeaTheme.Pill(new Rect(r.xMax - metaW - 4, r.y + 6, metaW, 16), "meta", p.TextSoft);

            var hit = new Rect(r.x + 30, r.y, r.width - 30, r.height);
            if (e.type == EventType.MouseDown && e.button == 0 && hit.Contains(e.mousePosition))
            {
                if (e.clickCount == 2) Ping(row.Path);
                SelectRow(row);
                e.Use();
            }
        }

        void SelectRow(ScRow row)
        {
            _selectedRow = row.Path;
            _diff = "…";
            _diffScroll = Vector2.zero;
            Task.Run(() =>
            {
                var d = FrogletSourceControl.Diff(row);
                _main.Enqueue(() => { if (_selectedRow == row.Path) _diff = d; });
            });
        }

        void DrawCommitBox(Rect r)
        {
            var p = FrogletSeaTheme.Current;
            FrogletSeaTheme.Rule(new Rect(r.x + 14, r.y, r.width - 28, 1));
            var x = r.x + 14;
            var cw = r.width - 28;
            var y = r.y + 12;
            GUI.Label(new Rect(x, y, cw, 16), "SUMMARY", FrogletSeaTheme.Label);
            y += 18;
            _summary = EditorGUI.TextField(new Rect(x, y, cw, 24), _summary, FrogletSeaTheme.Field);
            y += 32;
            GUI.Label(new Rect(x, y, cw, 16), "DESCRIPTION", FrogletSeaTheme.Label);
            y += 18;
            _description = EditorGUI.TextArea(new Rect(x, y, cw, 46), _description, FrogletSeaTheme.Area);
            y += 56;
            var ticked = _rows.Count(row => !_unticked.Contains(row.Path));
            var isProtected = FrogletGit.IsProtectedBranch(_branch);
            var label = isProtected ? $"{_branch} is protected: make a branch" : $"Commit {ticked} to {_branch}";
            var can = !_busy && ticked > 0 && _summary.Trim().Length > 0 && !isProtected;
            if (FrogletSeaTheme.Button(new Rect(x, y, cw, 34), label, FrogletSeaTheme.ButtonKind.Cta, can,
                    "Saves open scenes and assets, runs the checks on the ticked files, then commits them."))
                _deferred = DoCommit;
            if (isProtected) FrogletSeaTheme.Stripe(new Rect(x, y, cw, 34), p.Danger);
        }

        void DrawDiff(Rect r)
        {
            var p = FrogletSeaTheme.Current;
            var head = new Rect(r.x + 16, r.y + 12, r.width - 32, 22);
            if (_selectedRow == null)
            {
                GUI.Label(head, "SELECT A FILE TO SEE WHAT CHANGED", FrogletSeaTheme.Label);
                return;
            }
            var bx = head.xMax;
            if (_selectedRow.StartsWith("Assets/", StringComparison.Ordinal) || FrogletGit.ProjectPrefix.Length > 0)
            {
                var pw = FrogletSeaTheme.ButtonWidth("Select in Project");
                bx -= pw;
                if (FrogletSeaTheme.Button(new Rect(bx, head.y - 1, pw, 24), "Select in Project", FrogletSeaTheme.ButtonKind.Quiet))
                    Ping(_selectedRow);
                bx -= 8;
            }
            var sw = FrogletSeaTheme.ButtonWidth("Show in folder");
            bx -= sw;
            if (FrogletSeaTheme.Button(new Rect(bx, head.y - 1, sw, 24), "Show in folder", FrogletSeaTheme.ButtonKind.Quiet))
                EditorUtility.RevealInFinder(FrogletGit.ToAbsolute(_selectedRow));
            GUI.Label(new Rect(head.x, head.y, bx - head.x - 10, 22), new GUIContent(_selectedRow, _selectedRow), FrogletSeaTheme.LabelStrong);

            var view = new Rect(r.x + 4, head.yMax + 10, r.width - 8, r.height - (head.yMax + 10 - r.y) - 6);
            FrogletSeaTheme.Rule(new Rect(r.x + 16, view.y - 5, r.width - 32, 1));
            var lines = _diff.Split('\n');
            const float lineH = 16f;
            var mono = FrogletSeaTheme.Mono;
            var longest = 0;
            foreach (var l in lines) longest = Mathf.Max(longest, l.Length);
            var charW = mono.CalcSize(new GUIContent("M")).x;
            var content = new Rect(0, 0, Mathf.Max(view.width - 14, longest * charW + 24), lines.Length * lineH + 8);
            _diffScroll = FrogletSeaTheme.BeginScroll(view, _diffScroll, content);
            var first = Mathf.Max(0, (int)(_diffScroll.y / lineH) - 1);
            var last = Mathf.Min(lines.Length, first + (int)(view.height / lineH) + 3);
            var prev = mono.normal.textColor;
            for (var i = first; i < last; i++)
            {
                var l = lines[i];
                var rr = new Rect(0, 4 + i * lineH, content.width, lineH);
                Color text = p.Text;
                if (l.StartsWith("+") && !l.StartsWith("+++")) { FrogletSeaTheme.Fill(rr, FrogletSeaTheme.WithAlpha(p.Data, 0.14f)); text = p.DataText; }
                else if (l.StartsWith("-") && !l.StartsWith("---")) { FrogletSeaTheme.Fill(rr, FrogletSeaTheme.WithAlpha(p.Danger, 0.13f)); text = p.Danger; }
                else if (l.StartsWith("@@")) { FrogletSeaTheme.Fill(rr, FrogletSeaTheme.WithAlpha(p.Selection, 0.08f)); text = p.Selection; }
                else if (l.StartsWith("diff ") || l.StartsWith("index ") || l.StartsWith("+++") || l.StartsWith("---")) text = p.TextSoft;
                if (Event.current.type == EventType.Repaint)
                {
                    mono.normal.textColor = text;
                    GUI.Label(new Rect(12, rr.y, rr.width - 12, lineH), l, mono);
                }
            }
            mono.normal.textColor = prev;
            FrogletSeaTheme.EndScroll();
        }

        static void Ping(string repoRelative)
        {
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(repoRelative.Substring(FrogletGit.ProjectPrefix.Length));
            if (asset != null) { Selection.activeObject = asset; EditorGUIUtility.PingObject(asset); }
        }

        // ── History ──────────────────────────────────────────────────────────────

        void DrawHistory(Rect body)
        {
            var p = FrogletSeaTheme.Current;
            var leftW = Mathf.Clamp(body.width * 0.58f, Mathf.Min(360f, body.width - 220f), body.width - 220f);
            var left = new Rect(body.x, body.y, leftW, body.height);
            var right = new Rect(left.xMax + 12, body.y, body.width - leftW - 12, body.height);
            FrogletSeaTheme.Panel(left);
            FrogletSeaTheme.Panel(right);

            var unpushed = _history.Count(c => c.Unpushed);
            GUI.Label(new Rect(left.x + 14, left.y + 12, left.width - 28, 22),
                unpushed > 0 ? $"LAST {_history.Count} COMMITS · {unpushed} NOT ON GITHUB YET" : $"LAST {_history.Count} COMMITS", FrogletSeaTheme.Label);
            var view = new Rect(left.x + 4, left.y + 42, left.width - 8, left.height - 46);
            FrogletSeaTheme.Rule(new Rect(left.x + 14, view.y - 4, left.width - 28, 1));
            var content = new Rect(0, 0, view.width - 14, _history.Count * 44f);
            _pageScroll = FrogletSeaTheme.BeginScroll(view, _pageScroll, content);
            var e = Event.current;
            for (var i = 0; i < _history.Count; i++)
            {
                var c = _history[i];
                var r = new Rect(0, i * 44f, content.width, 44f);
                if (r.yMax < _pageScroll.y || r.y > _pageScroll.y + view.height) continue;
                var selected = c.Sha == _selectedCommit;
                if (selected) { FrogletSeaTheme.Fill(r, FrogletSeaTheme.WithAlpha(p.Selection, 0.13f)); FrogletSeaTheme.Stripe(r, p.Selection); }
                else if (r.Contains(e.mousePosition)) FrogletSeaTheme.Fill(r, FrogletSeaTheme.WithAlpha(p.SurfaceRaised, 0.25f));
                var x = r.x + 14;
                if (c.Unpushed)
                {
                    FrogletSeaTheme.Pill(new Rect(x, r.y + 6, 74, 16), "unpushed", p.Cta);
                    x += 82;
                }
                if (e.type == EventType.Repaint)
                {
                    GUI.Label(new Rect(x, r.y + 4, r.xMax - x - 12, 20), c.Subject, FrogletSeaTheme.Body);
                    GUI.Label(new Rect(r.x + 14, r.y + 22, r.width - 26, 18), $"{c.ShortSha}   {c.Author}   {c.Date}", FrogletSeaTheme.BodySoft);
                }
                if (e.type == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition))
                {
                    _selectedCommit = c.Sha;
                    _commitFiles = new List<string> { "…" };
                    var sha = c.Sha;
                    Task.Run(() =>
                    {
                        var files = FrogletSourceControl.CommitFiles(sha);
                        _main.Enqueue(() => { if (_selectedCommit == sha) _commitFiles = files; });
                    });
                    e.Use();
                }
            }
            FrogletSeaTheme.EndScroll();

            GUI.Label(new Rect(right.x + 14, right.y + 12, right.width - 28, 22),
                _selectedCommit == null ? "SELECT A COMMIT" : $"{_commitFiles.Count} FILE(S)", FrogletSeaTheme.Label);
            var fview = new Rect(right.x + 4, right.y + 42, right.width - 8, right.height - 46);
            FrogletSeaTheme.Rule(new Rect(right.x + 14, fview.y - 4, right.width - 28, 1));
            var fcontent = new Rect(0, 0, fview.width - 14, _commitFiles.Count * 22f);
            _sideScroll = FrogletSeaTheme.BeginScroll(fview, _sideScroll, fcontent);
            for (var i = 0; i < _commitFiles.Count; i++)
            {
                var line = _commitFiles[i];
                var code = line.Length > 0 ? line[0] : ' ';
                var color = code == 'A' ? p.Data : code == 'D' ? p.Danger : code == 'R' ? p.Data : p.TextSoft;
                var g = FrogletSeaTheme.LabelStrong;
                var prev = g.normal.textColor;
                g.normal.textColor = color;
                if (e.type == EventType.Repaint)
                {
                    GUI.Label(new Rect(12, i * 22f, 18, 22), code.ToString(), g);
                    GUI.Label(new Rect(32, i * 22f, fcontent.width - 36, 22), line.Length > 2 ? line.Substring(2).Trim() : line, FrogletSeaTheme.Body);
                }
                g.normal.textColor = prev;
            }
            FrogletSeaTheme.EndScroll();
        }

        // ── Branches ─────────────────────────────────────────────────────────────

        void DrawBranches(Rect body)
        {
            var p = FrogletSeaTheme.Current;
            var top = new Rect(body.x, body.y, body.width, 104);
            FrogletSeaTheme.Panel(top);
            GUI.Label(new Rect(top.x + 16, top.y + 12, 300, 18), "NEW BRANCH", FrogletSeaTheme.Label);
            const string create = "Create from latest bleeding-edge";
            var cw = FrogletSeaTheme.ButtonWidth(create);
            var fieldR = new Rect(top.x + 16, top.y + 36, Mathf.Max(160, top.width - 48 - cw), 28);
            _newBranch = EditorGUI.TextField(fieldR, _newBranch, FrogletSeaTheme.Field);
            var valid = FrogletSourceControl.IsValidBranchName(_newBranch.Trim(), out var why);
            if (FrogletSeaTheme.Button(new Rect(fieldR.xMax + 16, fieldR.y - 1, cw, 30), create, FrogletSeaTheme.ButtonKind.Cta, !_busy && valid,
                    "Fetches, then starts the branch from origin/bleeding-edge and switches to it. Your uncommitted changes come along."))
                _deferred = () => DoCreateBranch(_newBranch.Trim());
            GUI.Label(new Rect(top.x + 16, top.y + 72, top.width - 32, 20),
                !valid && _newBranch.Length > 0 ? why : "Name it yourname/what-it-does, for example charles/thresher-audio.",
                !valid && _newBranch.Length > 0 ? FrogletSeaTheme.BodyWrapped(p.Danger) : FrogletSeaTheme.BodySoft);

            var list = new Rect(body.x, top.yMax + 12, body.width, body.height - top.height - 12);
            FrogletSeaTheme.Panel(list);
            GUI.Label(new Rect(list.x + 16, list.y + 14, 200, 18), "SWITCH BRANCH", FrogletSeaTheme.Label);
            GUI.Label(new Rect(list.xMax - 16 - 240 - 54, list.y + 14, 50, 18), "FILTER", FrogletSeaTheme.Label);
            _branchFilter = EditorGUI.TextField(new Rect(list.xMax - 16 - 240, list.y + 10, 240, 24), _branchFilter, FrogletSeaTheme.Field);

            var locals = new HashSet<string>(_branches.Where(b => !b.IsRemote).Select(b => b.Name), StringComparer.Ordinal);
            var shown = _branches.Where(b => !(b.IsRemote && locals.Contains(b.Name)))
                                 .Where(b => _branchFilter.Length == 0 || b.Name.IndexOf(_branchFilter, StringComparison.OrdinalIgnoreCase) >= 0)
                                 .Take(200).ToList();
            var view = new Rect(list.x + 4, list.y + 46, list.width - 8, list.height - 50);
            FrogletSeaTheme.Rule(new Rect(list.x + 16, view.y - 4, list.width - 32, 1));
            const float bh = 44f;
            var content = new Rect(0, 0, view.width - 14, shown.Count * bh + (shown.Count == 200 ? 24 : 0));
            _pageScroll = FrogletSeaTheme.BeginScroll(view, _pageScroll, content);
            var e = Event.current;
            for (var i = 0; i < shown.Count; i++)
            {
                var b = shown[i];
                var r = new Rect(0, i * bh, content.width, bh);
                if (r.yMax < _pageScroll.y || r.y > _pageScroll.y + view.height) continue;
                var current = !b.IsRemote && b.Name == _branch;
                if (current) { FrogletSeaTheme.Fill(r, FrogletSeaTheme.WithAlpha(p.Selection, 0.13f)); FrogletSeaTheme.Stripe(r, p.Selection); }
                else if (r.Contains(e.mousePosition)) FrogletSeaTheme.Fill(r, FrogletSeaTheme.WithAlpha(p.SurfaceRaised, 0.25f));
                if (e.type == EventType.Repaint)
                {
                    GUI.Label(new Rect(r.x + 14, r.y + 4, r.width - 150, 20), b.Name, FrogletSeaTheme.Body);
                    GUI.Label(new Rect(r.x + 14, r.y + 22, r.width - 150, 18),
                        (b.IsRemote ? "on GitHub only   " : "") + b.Date + "   " + b.Subject, FrogletSeaTheme.BodySoft);
                }
                var br = new Rect(r.xMax - 108, r.y + 9, 96, 26);
                if (current) FrogletSeaTheme.Pill(new Rect(br.x + 6, br.y + 4, 84, 18), "current", p.Selection, true);
                else if (FrogletSeaTheme.Button(br, "Switch", FrogletSeaTheme.ButtonKind.Primary, !_busy))
                {
                    var target = b;
                    _deferred = () => DoSwitch(target);
                }
            }
            if (shown.Count == 200)
                GUI.Label(new Rect(14, shown.Count * bh + 2, content.width - 28, 20), "Type in the filter to find older branches.", FrogletSeaTheme.BodySoft);
            FrogletSeaTheme.EndScroll();
        }

        // ── Checks ───────────────────────────────────────────────────────────────

        void DrawChecks(Rect body)
        {
            var p = FrogletSeaTheme.Current;
            var x = body.x;
            var buttons = new (string label, FrogletSeaTheme.ButtonKind kind, bool enabled, string tip, Action act)[]
            {
                ("Run push checks", FrogletSeaTheme.ButtonKind.Primary, !_busy && FrogletPython.IsAvailable,
                    "Check the ticked files and the unpushed commits (Tools/Build/push_checks.py).",
                    () => RunChecks(TickedPaths(), "the ticked files and unpushed commits", null)),
                ("Run /ship gates", FrogletSeaTheme.ButtonKind.Primary, !_busy && FrogletPython.IsAvailable,
                    "Every generator --check and static check /ship runs (Tools/Build/ship_gates.py, about 2 minutes).", DoGates),
                ("Ask Claude to ship this", FrogletSeaTheme.ButtonKind.Quiet, true,
                    "Copies a /ship request for this branch and opens Claude Code: review, conflicts, the pull request.", DoAskClaude),
            };
            foreach (var b in buttons)
            {
                var bw = FrogletSeaTheme.ButtonWidth(b.label);
                if (FrogletSeaTheme.Button(new Rect(x, body.y, bw, 30), b.label, b.kind, b.enabled, b.tip)) _deferred = b.act;
                x += bw + 8;
            }

            var panel = new Rect(body.x, body.y + 42, body.width, body.height - 42);
            FrogletSeaTheme.Panel(panel);
            var view = new Rect(panel.x + 4, panel.y + 8, panel.width - 8, panel.height - 12);
            var cards = new List<ScFinding>();
            if (!FrogletPython.IsAvailable)
                cards.Add(new ScFinding
                {
                    severity = "warn", title = "Python 3 is not installed",
                    detail = "The checks and gates are python (Tools/Build/push_checks.py, ship_gates.py). Install Python 3 from " +
                             FrogletPython.DownloadUrl + " and reopen this window.", paths = new string[0],
                });
            if (EditorUtility.scriptCompilationFailed)
                cards.Add(new ScFinding
                {
                    id = "compile", severity = "block", title = "The project does not compile",
                    detail = "The Console has compile errors. A push now breaks the build for everyone who pulls it.", paths = new string[0],
                });
            var tools = FrogletToolChangeLedger.Tools();
            if (tools.Count > 0)
                cards.Add(new ScFinding
                {
                    id = "tool-output", severity = "info", title = $"{tools.Count} editor tool(s) recorded output",
                    detail = "Their files are in your changes; FrogletTools > Build > Pending Tool Changes shows which tool wrote what.",
                    paths = new string[0],
                });
            if (_report != null) cards.AddRange(_report.findings);

            var cardW = view.width - 14 - 24;
            var heights = cards.Select(f => FindingHeight(f, cardW)).ToList();
            var gateLines = _gateLines.Where(l => !l.StartsWith("ok    ")).ToList();
            var gatesH = _gateLines.Count > 0 || _gateVerdict != null ? 48 + gateLines.Count * 20 : 0;
            var reportH = _report != null ? 30 : 0;
            var total = 12 + reportH + heights.Sum() + heights.Count * 10 + gatesH + 12;
            var content = new Rect(0, 0, view.width - 14, Mathf.Max(total, view.height - 4));
            _pageScroll = FrogletSeaTheme.BeginScroll(view, _pageScroll, content);
            var y = 12f;
            if (_report != null)
            {
                var behind = _report.behind != null && _report.behind.commits > 0 ? $" · {_report.behind.merges} PRs behind bleeding-edge" : "";
                GUI.Label(new Rect(12, y, content.width - 24, 20), $"PUSH CHECKS ON {_reportFor.ToUpperInvariant()} · {_report.@checked} PATH(S){behind.ToUpperInvariant()}",
                    FrogletSeaTheme.Label);
                y += reportH;
            }
            if (cards.Count == 0 && _report != null)
            {
                GUI.Label(new Rect(12, y, content.width - 24, 20), "Nothing to flag.", FrogletSeaTheme.Body);
                y += 28;
            }
            else if (cards.Count == 0 && gatesH == 0)
                GUI.Label(new Rect(12, y, content.width - 24, 40),
                    "Run the push checks, or let a commit or push run them. The /ship gates take about two minutes.", FrogletSeaTheme.BodyWrapped(p.TextSoft));
            for (var i = 0; i < cards.Count; i++)
            {
                DrawFinding(new Rect(12, y, cardW, heights[i]), cards[i]);
                y += heights[i] + 10;
            }
            if (gatesH > 0)
            {
                y += 6;
                var passed = _gateLines.Count(l => l.StartsWith("ok    "));
                GUI.Label(new Rect(12, y, content.width - 24, 20),
                    "/SHIP GATES · " + (_gateVerdict != null ? _gateVerdict : "RUNNING") + $" · {passed} PASSED", FrogletSeaTheme.Label);
                y += 26;
                foreach (var l in gateLines)
                {
                    var c = l.StartsWith("FAIL") ? p.Danger : l.StartsWith("GATES: ALL OK") ? p.Cta : p.TextSoft;
                    FrogletSeaTheme.Stripe(new Rect(12, y + 2, 3, 16), c);
                    GUI.Label(new Rect(24, y, content.width - 36, 20), l, FrogletSeaTheme.Mono);
                    y += 20;
                }
            }
            FrogletSeaTheme.EndScroll();
        }

        static float FindingHeight(ScFinding f, float width)
        {
            var text = FindingText(f);
            var style = FrogletSeaTheme.BodyWrapped(FrogletSeaTheme.Current.TextSoft);
            return 44 + (text.Length == 0 ? 0 : style.CalcHeight(new GUIContent(text), width - 32)) + 12;
        }

        static string FindingText(ScFinding f)
        {
            var paths = f.paths ?? new string[0];
            var shownPaths = paths.Take(12).Select(x => "•  " + x).ToList();
            if (paths.Length > 12) shownPaths.Add($"… and {paths.Length - 12} more");
            var body = (f.detail ?? string.Empty).Trim();
            return body + (shownPaths.Count > 0 ? (body.Length > 0 ? "\n" : "") + string.Join("\n", shownPaths) : string.Empty);
        }

        void DrawFinding(Rect r, ScFinding f)
        {
            var p = FrogletSeaTheme.Current;
            var accent = f.IsBlock ? p.Danger : f.IsWarn ? p.Danger : p.Data;
            FrogletSeaTheme.Sliver(r, FrogletSeaTheme.WithAlpha(p.GroundDeep, 0.55f), FrogletSeaTheme.WithAlpha(accent, f.IsBlock ? 0.9f : 0.5f),
                                   FrogletSeaTheme.SliverSmall);
            FrogletSeaTheme.Stripe(new Rect(r.x, r.y + 8, 3, r.height - 16), accent);
            var label = f.IsBlock ? "stop" : f.IsWarn ? "warning" : "note";
            var pw = FrogletSeaTheme.PillWidth(label);
            FrogletSeaTheme.Pill(new Rect(r.x + 16, r.y + 12, pw, 18), label, accent, f.IsBlock);
            var paths = f.paths ?? new string[0];
            var hasFix = f.fix == "discard" && paths.Length > 0;
            GUI.Label(new Rect(r.x + 24 + pw, r.y + 10, r.width - pw - (hasFix ? 160 : 40), 22), f.title, FrogletSeaTheme.LabelStrong);
            var text = FindingText(f);
            if (text.Length > 0)
                GUI.Label(new Rect(r.x + 16, r.y + 40, r.width - 32, r.height - 48), text, FrogletSeaTheme.BodyWrapped(p.TextSoft));
            if (hasFix && FrogletSeaTheme.Button(new Rect(r.xMax - 130, r.y + 8, 118, 26), "Discard these", FrogletSeaTheme.ButtonKind.Danger,
                    !_busy, "Throw these files' changes away (asks first)."))
            {
                var title = f.title;
                _deferred = () => DoDiscardPaths(paths, title);
            }
        }

        // ── Actions ──────────────────────────────────────────────────────────────

        List<string> TickedPaths() => FrogletSourceControl.PathsOf(_rows.Where(r => !_unticked.Contains(r.Path)));

        void DoFetch()
        {
            if (_busy) return;
            _lastFetch = DateTime.UtcNow;
            Run("Fetching", () =>
            {
                var r = FrogletSourceControl.Fetch();
                var snap = Snapshot();
                return () => { Apply(snap); SetStatus(r.Ok ? "Fetched." : "Fetch failed: " + r.Text, !r.Ok); };
            });
        }

        void DoPull()
        {
            if (RefuseWhilePlaying("pull")) return;
            if (!SaveEverything()) return;
            if (HasTrackedChanges)
            {
                SetStatus("Commit or discard your changes first: a pull must not mix with uncommitted edits.", true);
                return;
            }
            Run("Pulling", () =>
            {
                var r = FrogletSourceControl.Pull();
                var snap = Snapshot();
                return () =>
                {
                    AfterFilesChanged();
                    Apply(snap);
                    SetStatus(r.Ok ? "Pulled." : r.ExitCode == 2 ? r.StdErr + "\nAsk Claude to resolve them (Checks tab)." : "Pull failed: " + r.Text, !r.Ok);
                };
            });
        }

        void DoUpdateFromBase()
        {
            if (RefuseWhilePlaying("update from bleeding-edge")) return;
            if (!SaveEverything()) return;
            if (HasTrackedChanges)
            {
                SetStatus("Commit or discard your changes first, then update from bleeding-edge.", true);
                return;
            }
            Run("Merging bleeding-edge in", () =>
            {
                var f = FrogletSourceControl.Fetch();
                var r = f.Ok ? FrogletSourceControl.UpdateFromBase() : f;
                var snap = Snapshot();
                return () =>
                {
                    AfterFilesChanged();
                    Apply(snap);
                    if (r.Ok) SetStatus("Bleeding-edge merged in. Push to send the merge.", false);
                    else if (r.ExitCode == 2)
                        SetStatus(r.StdErr + "\nThese need resolving object by object (/ship §1): use Ask Claude to ship this.", true);
                    else SetStatus("Update failed: " + r.Text, true);
                };
            });
        }

        void DoOpenPullRequest()
        {
            Run("Finding the GitHub page", () =>
            {
                var url = FrogletSourceControl.CompareUrl(FrogletSourceControl.OriginUrl(), _branch);
                return () =>
                {
                    if (url == null) { SetStatus("origin is not a GitHub repository, so there is no pull request page.", true); return; }
                    if (_ahead > 0) SetStatus("You have unpushed commits: push first, or the pull request will not have them.", true);
                    Application.OpenURL(url);
                };
            }, quiet: true);
        }

        void DoCommit()
        {
            if (!SaveEverything()) return;
            var summary = _summary.Trim();
            var description = _description;
            var branch = _branch;
            // Saving can change files: read the tree again, then check and commit what is ticked now.
            Run("Checking the ticked files", () =>
            {
                var snap = Snapshot();
                var rows = FrogletSourceControl.GroupWithMetas(snap.Status);
                return () =>
                {
                    Apply(snap);
                    var paths = FrogletSourceControl.PathsOf(rows.Where(r => !_unticked.Contains(r.Path)));
                    if (paths.Count == 0) { SetStatus("Nothing ticked to commit.", true); return; }
                    RunChecks(paths, "the files you are committing", ok =>
                    {
                        if (!ok) return;
                        Run("Committing", () =>
                        {
                            var r = FrogletSourceControl.Commit(paths, summary, description);
                            var after = Snapshot();
                            return () =>
                            {
                                Apply(after);
                                if (r.Ok)
                                {
                                    _summary = _description = string.Empty;
                                    SetStatus($"Committed {paths.Count} file(s) to {branch}. Push when you are ready.", false);
                                }
                                else SetStatus("Commit failed: " + r.Text, true);
                            };
                        });
                    });
                };
            });
        }

        void DoPush()
        {
            if (EditorUtility.scriptCompilationFailed)
            {
                _tab = 3;
                SetStatus("The project has compile errors; fix them before you push.", true);
                return;
            }
            var branch = _branch;
            var setUpstream = _upstream == null;
            RunChecks(new List<string>(), "your unpushed commits", ok =>
            {
                if (!ok) return;
                Run("Pushing", () =>
                {
                    var r = FrogletSourceControl.Push(branch, setUpstream);
                    var snap = Snapshot();
                    return () =>
                    {
                        Apply(snap);
                        if (r.Ok) SetStatus($"Pushed {branch}. Open pull request when it is ready for review.", false);
                        else if (r.Text.IndexOf("rejected", StringComparison.OrdinalIgnoreCase) >= 0)
                            SetStatus("GitHub has commits you do not: Pull first, then push.\n" + r.Text, true);
                        else if (r.Text.IndexOf("Authentication", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 r.Text.IndexOf("could not read Username", StringComparison.OrdinalIgnoreCase) >= 0)
                            SetStatus("GitHub did not accept your sign-in. Push once from GitHub Desktop (or a terminal) to store it, then try again.\n" + r.Text, true);
                        else SetStatus("Push failed: " + r.Text, true);
                    };
                });
            });
        }

        /// <summary>Run push_checks.py; a block stops, a warning asks, then <paramref name="then"/>(true) continues.</summary>
        void RunChecks(List<string> paths, string what, Action<bool> then)
        {
            if (!FrogletPython.IsAvailable)
            {
                var go = then != null && EditorUtility.DisplayDialog("No Python, no checks",
                    "The push checks need Python 3 (" + FrogletPython.DownloadUrl + "). Continue without them?",
                    "Continue unchecked", "Cancel");
                then?.Invoke(go);
                return;
            }
            Run("Running the push checks", () =>
            {
                var report = FrogletSourceControl.RunPushChecks(paths, out var error);
                return () =>
                {
                    _report = report;
                    _reportFor = what;
                    if (report == null)
                    {
                        _tab = 3;
                        SetStatus("The push checks did not run: " + error, true);
                        then?.Invoke(false);
                        return;
                    }
                    var blocks = report.findings.Where(f => f.IsBlock).ToList();
                    var warns = report.findings.Where(f => f.IsWarn).ToList();
                    if (then == null)
                    {
                        SetStatus(blocks.Count + warns.Count == 0 ? "Checks: nothing to flag." : $"Checks: {blocks.Count} stop(s), {warns.Count} warning(s).",
                                  blocks.Count > 0);
                        return;
                    }
                    if (blocks.Count > 0)
                    {
                        _tab = 3;
                        SetStatus($"Stopped: {blocks[0].title}. See the Checks tab.", true);
                        then(false);
                        return;
                    }
                    if (warns.Count > 0)
                    {
                        var msg = new StringBuilder();
                        foreach (var w in warns.Take(6)) msg.Append("• ").Append(w.title).Append('\n');
                        var choice = EditorUtility.DisplayDialogComplex("Before you go on", msg + "\nThe Checks tab explains each one and can discard the files it names.",
                                                                        "Go on anyway", "Cancel", "Show me");
                        if (choice != 0)
                        {
                            if (choice == 2) _tab = 3;
                            SetStatus("Cancelled: " + warns.Count + " warning(s) to look at in the Checks tab.", false);
                            then(false);
                            return;
                        }
                    }
                    then(true);
                };
            });
        }

        void DoGates()
        {
            _gateLines.Clear();
            _gateVerdict = null;
            _tab = 3;
            Run("Running the /ship gates", () =>
            {
                var r = FrogletPython.RunScript(FrogletSourceControl.ShipGatesScript, new[] { "--no-refcompile" }, 30 * 60 * 1000,
                                                line => _gateStream.Enqueue(line));
                return () =>
                {
                    _gateVerdict = r.Ok ? "ALL OK" : r.Started ? "FAILED" : "did not run";
                    SetStatus(r.Ok ? "/ship gates: all passed." : r.Started ? "/ship gates failed: see the Checks tab." : "The gates did not run: " + r.StdErr,
                              !r.Ok);
                };
            });
        }

        void DoAskClaude()
        {
            var prompt = $"/ship the branch {_branch} of froglet-studio/cosmic-shore (opened from Unity's Source Control window). " +
                         "Merge bleeding-edge in and resolve any conflicts, run the gates, review, and open the pull request.";
            if (_report != null && _report.findings.Length > 0)
                prompt += " The push checks flagged: " + string.Join("; ", _report.findings.Select(f => f.title)) + ".";
            EditorGUIUtility.systemCopyBuffer = prompt;
            Application.OpenURL(ClaudeUrl);
            SetStatus("Copied a /ship request for " + _branch + " — paste it into the Claude Code session that opened.", false);
        }

        void DoDiscardTicked()
        {
            var rows = _rows.Where(r => !_unticked.Contains(r.Path)).ToList();
            if (rows.Count == 0 || RefuseWhilePlaying("discard")) return;
            var newFiles = rows.Count(r => r.Change.IsUntracked);
            if (!EditorUtility.DisplayDialog("Discard changes?",
                    $"Throw away the changes in {rows.Count} file(s)" + (newFiles > 0 ? $", and DELETE {newFiles} new file(s)" : "") +
                    "? This cannot be undone.", "Discard", "Cancel"))
                return;
            Run("Discarding", () =>
            {
                var r = FrogletSourceControl.Discard(rows);
                var snap = Snapshot();
                return () => { AfterFilesChanged(); Apply(snap); SetStatus(r.Ok ? $"Discarded {rows.Count} file(s)." : "Discard failed: " + r.Text, !r.Ok); };
            });
        }

        void DoDiscardPaths(string[] paths, string title)
        {
            if (RefuseWhilePlaying("discard")) return;
            var inTree = paths.Where(p => _status.Any(c => c.Path == p)).ToList();
            if (inTree.Count == 0)
            {
                SetStatus("Those files are in a commit, not in your working tree. Ask Claude to take them out of the commit before you push.", true);
                return;
            }
            if (!EditorUtility.DisplayDialog("Discard changes?", $"{title}\n\nThrow away the changes in {inTree.Count} file(s)? This cannot be undone.",
                    "Discard", "Cancel"))
                return;
            var status = _status;
            Run("Discarding", () =>
            {
                var r = FrogletSourceControl.DiscardPaths(inTree, status);
                var snap = Snapshot();
                return () =>
                {
                    AfterFilesChanged();
                    Apply(snap);
                    if (_report != null)
                        _report.findings = _report.findings.Where(f => f.paths == null || !f.paths.Intersect(inTree).Any()).ToArray();
                    SetStatus(r.Ok ? $"Discarded {inTree.Count} file(s)." : "Discard failed: " + r.Text, !r.Ok);
                };
            });
        }

        void DoSwitch(ScBranch target)
        {
            if (RefuseWhilePlaying("switch branch")) return;
            if (!SaveEverything()) return;
            if (HasTrackedChanges && !EditorUtility.DisplayDialog("Switch with uncommitted changes?",
                    $"You have {_rows.Count} changed file(s). They come with you to {target.Name} if git can carry them; " +
                    "if a file differs on both branches git refuses and nothing changes. Commit first to be safe.",
                    "Switch anyway", "Cancel"))
                return;
            Run("Switching to " + target.Name, () =>
            {
                var r = FrogletSourceControl.Switch(target);
                var snap = Snapshot();
                return () =>
                {
                    AfterFilesChanged();
                    Apply(snap);
                    SetStatus(r.Ok ? "On " + snap.Branch + ". Unity is re-importing what changed." : "Switch failed: " + r.Text, !r.Ok);
                };
            });
        }

        void DoCreateBranch(string name)
        {
            if (RefuseWhilePlaying("create a branch")) return;
            if (!SaveEverything()) return;
            Run("Creating " + name, () =>
            {
                var f = FrogletSourceControl.Fetch();
                var r = FrogletSourceControl.CreateBranch(name, fromBase: true);
                var snap = Snapshot();
                return () =>
                {
                    AfterFilesChanged();
                    Apply(snap);
                    if (r.Ok) { _newBranch = string.Empty; _tab = 0; }
                    SetStatus(r.Ok ? $"On the new branch {name}, started from the latest bleeding-edge" + (f.Ok ? "." : " (fetch failed: it starts from the last one fetched).")
                                   : "Could not create the branch: " + r.Text, !r.Ok);
                };
            });
        }
    }
}
