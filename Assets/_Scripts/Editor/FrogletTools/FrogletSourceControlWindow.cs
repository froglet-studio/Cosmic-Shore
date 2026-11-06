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
            w.minSize = new Vector2(760f, 460f);
            w.Show();
        }

        void OnEnable()
        {
            // Resolved on the main thread: FrogletGit reads Application.dataPath, which a worker may not.
            _ = FrogletGit.RepoRoot;
            _ = FrogletGit.TopLevel;
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

        // ── Drawing ──────────────────────────────────────────────────────────────

        void OnGUI()
        {
            FrogletEditorPalette.Banner("Source Control",
                "Commit, pull, push and open a pull request without leaving Unity; the /ship checks run before every commit and push.",
                FrogletEditorPalette.ColorFor(FrogletToolCategory.Build));

            if (!FrogletGit.IsAvailable)
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.HelpBox(
                    "No git found: not on PATH, not inside GitHub Desktop, not in Program Files. Install Git " +
                    "(https://git-scm.com/downloads) or GitHub Desktop, then reopen this window.", MessageType.Warning);
                return;
            }

            DrawTopBar();
            DrawStatusLine();
            _tab = GUILayout.Toolbar(_tab, new[]
            {
                $"Changes ({_rows.Count})", "History", "Branches",
                _report == null ? "Checks" : $"Checks ({_report.findings.Count(f => !string.Equals(f.severity, "info"))})",
            }, GUILayout.Height(24));
            EditorGUILayout.Space(4);

            switch (_tab)
            {
                case 0: DrawChanges(); break;
                case 1: DrawHistory(); break;
                case 2: DrawBranches(); break;
                default: DrawChecks(); break;
            }

            if (_deferred != null && Event.current.type == EventType.Repaint)
            {
                var d = _deferred;
                _deferred = null;
                EditorApplication.delayCall += () => d();
            }
        }

        void DrawTopBar()
        {
            EditorGUILayout.Space(6);
            using (new EditorGUILayout.HorizontalScope())
            {
                var protectedBranch = FrogletGit.IsProtectedBranch(_branch);
                var pill = GUILayoutUtility.GetRect(220, 22, GUILayout.Width(220), GUILayout.Height(22));
                FrogletEditorPalette.StatusPill(pill, "BRANCH  " + (string.IsNullOrEmpty(_branch) ? "?" : _branch),
                    protectedBranch ? FrogletEditorPalette.Error : FrogletEditorPalette.Info);

                var sync = _upstream == null ? "not on GitHub yet" : $"↓{_behind}  ↑{_ahead}";
                var syncRect = GUILayoutUtility.GetRect(130, 22, GUILayout.Width(130), GUILayout.Height(22));
                FrogletEditorPalette.StatusPill(syncRect, sync,
                    _behind > 0 ? FrogletEditorPalette.Warn : _ahead > 0 ? FrogletEditorPalette.Info : FrogletEditorPalette.Ok);

                if (_baseKnown)
                {
                    var b = GUILayoutUtility.GetRect(200, 22, GUILayout.Width(200), GUILayout.Height(22));
                    FrogletEditorPalette.StatusPill(b, _baseMerges == 0 ? "UP TO DATE WITH BLEEDING-EDGE" : $"{_baseMerges} PRs BEHIND BLEEDING-EDGE",
                        _baseMerges == 0 ? FrogletEditorPalette.Ok : _baseMerges >= 10 ? FrogletEditorPalette.Warn : FrogletEditorPalette.Muted);
                }

                GUILayout.FlexibleSpace();
                if (_busy) GUILayout.Label(_busyLabel + "…", FrogletEditorPalette.Subtitle);
            }

            EditorGUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope())
            {
                var idle = !_busy;
                var accent = FrogletEditorPalette.ColorFor(FrogletToolCategory.Build);
                if (FrogletEditorPalette.ColorButton("Refresh", accent, 80, 26, "Re-read the working tree.", idle, outline: true))
                    _deferred = RefreshAll;
                if (FrogletEditorPalette.ColorButton("Fetch", accent, 70, 26, "Ask GitHub what changed. Changes no file.", idle, outline: true))
                    _deferred = DoFetch;
                if (FrogletEditorPalette.ColorButton(_behind > 0 ? $"Pull ↓{_behind}" : "Pull", accent, 80, 26,
                        "Bring this branch's new commits from GitHub.", idle && _upstream != null, outline: _behind == 0))
                    _deferred = DoPull;
                if (FrogletEditorPalette.ColorButton(_upstream == null ? "Publish branch" : _ahead > 0 ? $"Push ↑{_ahead}" : "Push",
                        FrogletEditorPalette.Ok, 120, 26, "Run the checks, then send your commits to GitHub.",
                        idle && (_ahead > 0 || _upstream == null), outline: _ahead == 0 && _upstream != null))
                    _deferred = DoPush;
                if (FrogletEditorPalette.ColorButton("Update from bleeding-edge", FrogletEditorPalette.Warn, 180, 26,
                        "Merge the latest bleeding-edge into this branch. Conflicts are undone and named.",
                        idle && _baseKnown && _baseCommits > 0, outline: _baseMerges < 10))
                    _deferred = DoUpdateFromBase;
                if (FrogletEditorPalette.ColorButton("Open pull request", FrogletEditorPalette.Info, 130, 26,
                        "GitHub's pull request page for this branch, filled from the template.",
                        idle && _upstream != null && !FrogletGit.IsProtectedBranch(_branch), outline: true))
                    _deferred = DoOpenPullRequest;
                GUILayout.FlexibleSpace();
            }
        }

        void DrawStatusLine()
        {
            if (string.IsNullOrEmpty(_status1)) { EditorGUILayout.Space(4); return; }
            EditorGUILayout.Space(4);
            var r = GUILayoutUtility.GetRect(0, 1, GUILayout.ExpandWidth(true));
            var height = FrogletEditorPalette.CardBodyWrapped.CalcHeight(new GUIContent(_status1), Mathf.Max(100, r.width - 20)) + 8;
            var box = GUILayoutUtility.GetRect(0, height, GUILayout.ExpandWidth(true));
            FrogletEditorPalette.DrawCard(box, FrogletEditorPalette.SurfaceRaised,
                (_statusIsError ? FrogletEditorPalette.Error : FrogletEditorPalette.Ok).WithAlpha(0.6f));
            FrogletEditorPalette.DrawAccentStripe(box, _statusIsError ? FrogletEditorPalette.Error : FrogletEditorPalette.Ok);
            GUI.Label(new Rect(box.x + 10, box.y + 4, box.width - 16, box.height - 8), _status1, FrogletEditorPalette.CardBodyWrapped);
            EditorGUILayout.Space(4);
        }

        // ── Changes ──────────────────────────────────────────────────────────────

        void DrawChanges()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(Mathf.Max(300, position.width * 0.42f))))
                {
                    DrawChangeList();
                    DrawCommitBox();
                }
                using (new EditorGUILayout.VerticalScope())
                    DrawDiff();
            }
        }

        void DrawChangeList()
        {
            var ticked = _rows.Count(r => !_unticked.Contains(r.Path));
            using (new EditorGUILayout.HorizontalScope())
            {
                var all = ticked == _rows.Count && _rows.Count > 0;
                var now = EditorGUILayout.ToggleLeft($"{ticked} of {_rows.Count} file(s) ticked", all, FrogletEditorPalette.SectionLabel);
                if (now != all)
                {
                    if (now) _unticked.Clear();
                    else foreach (var r in _rows) _unticked.Add(r.Path);
                }
                GUILayout.FlexibleSpace();
                if (FrogletEditorPalette.ColorButton("Discard…", FrogletEditorPalette.Error, 80, 20,
                        "Throw away the ticked changes (asks first).", !_busy && ticked > 0, outline: true))
                    _deferred = DoDiscardTicked;
            }

            _listScroll = EditorGUILayout.BeginScrollView(_listScroll, GUILayout.ExpandHeight(true));
            if (_rows.Count == 0)
                GUILayout.Label("No changes. Everything is committed.", FrogletEditorPalette.CardBody);
            foreach (var row in _rows)
            {
                var r = GUILayoutUtility.GetRect(0, 20, GUILayout.ExpandWidth(true));
                var selected = row.Path == _selectedRow;
                if (selected) FrogletEditorPalette.DrawRect(r, FrogletEditorPalette.Info.WithAlpha(0.18f));
                var c = row.Change;
                FrogletEditorPalette.DrawAccentStripe(r, c.IsConflicted ? FrogletEditorPalette.Error
                    : c.Kind == '+' ? FrogletEditorPalette.Ok : c.Kind == '-' ? FrogletEditorPalette.Error : FrogletEditorPalette.Warn);

                var tick = new Rect(r.x + 6, r.y + 2, 16, 16);
                var was = !_unticked.Contains(row.Path);
                var now = EditorGUI.Toggle(tick, was);
                if (now != was) { if (now) _unticked.Remove(row.Path); else _unticked.Add(row.Path); }

                var label = c.Kind + "  " + row.Path + (row.Meta != null ? "  (+ .meta)" : string.Empty) +
                            (c.OriginalPath != null ? "   ← " + c.OriginalPath : string.Empty);
                var labelRect = new Rect(r.x + 26, r.y + 2, r.width - 30, 16);
                GUI.Label(labelRect, new GUIContent(label, row.Path), FrogletEditorPalette.CardBody);
                if (Event.current.type == EventType.MouseDown && labelRect.Contains(Event.current.mousePosition))
                {
                    if (Event.current.clickCount == 2) Ping(row.Path);
                    SelectRow(row);
                    Event.current.Use();
                }
            }
            EditorGUILayout.EndScrollView();
        }

        void SelectRow(ScRow row)
        {
            _selectedRow = row.Path;
            _diff = "…";
            Task.Run(() =>
            {
                var d = FrogletSourceControl.Diff(row);
                _main.Enqueue(() => { if (_selectedRow == row.Path) _diff = d; });
            });
        }

        void DrawCommitBox()
        {
            FrogletEditorPalette.HorizontalRule(4);
            GUILayout.Label("Summary (required)", FrogletEditorPalette.SectionLabel);
            _summary = EditorGUILayout.TextField(_summary);
            GUILayout.Label("Description", FrogletEditorPalette.SectionLabel);
            _description = EditorGUILayout.TextArea(_description, GUILayout.MinHeight(48));
            var ticked = _rows.Count(r => !_unticked.Contains(r.Path));
            var can = !_busy && ticked > 0 && _summary.Trim().Length > 0;
            EditorGUILayout.Space(4);
            var label = FrogletGit.IsProtectedBranch(_branch) ? $"Commit to {_branch} (protected: make a branch)" : $"Commit {ticked} file(s) to {_branch}";
            var r = GUILayoutUtility.GetRect(0, 30, GUILayout.ExpandWidth(true));
            if (FrogletEditorPalette.ColorButton(r, label, FrogletEditorPalette.Ok,
                    "Saves open scenes and assets, runs the checks on the ticked files, then commits them.",
                    can && !FrogletGit.IsProtectedBranch(_branch)))
                _deferred = DoCommit;
        }

        void DrawDiff()
        {
            GUILayout.Label(_selectedRow ?? "Select a file to see what changed", FrogletEditorPalette.SectionLabel);
            if (_selectedRow != null)
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (FrogletEditorPalette.ColorButton("Show in folder", FrogletEditorPalette.Slate, 110, 20, null, true, outline: true))
                        EditorUtility.RevealInFinder(FrogletGit.ToAbsolute(_selectedRow));
                    if (_selectedRow.StartsWith("Assets/", StringComparison.Ordinal) &&
                        FrogletEditorPalette.ColorButton("Select in Project", FrogletEditorPalette.Slate, 120, 20, null, true, outline: true))
                        Ping(_selectedRow);
                    GUILayout.FlexibleSpace();
                }

            var lines = _diff.Split('\n');
            const float lineH = 15f;
            _diffScroll = EditorGUILayout.BeginScrollView(_diffScroll, GUILayout.ExpandHeight(true));
            var area = GUILayoutUtility.GetRect(0, lines.Length * lineH + 4, GUILayout.ExpandWidth(true));
            var first = Mathf.Max(0, (int)(_diffScroll.y / lineH) - 2);
            var last = Mathf.Min(lines.Length, first + (int)(position.height / lineH) + 4);
            for (var i = first; i < last; i++)
            {
                var l = lines[i];
                var rr = new Rect(area.x + 4, area.y + i * lineH, Mathf.Max(area.width, 2000), lineH);
                if (l.StartsWith("+") && !l.StartsWith("+++"))
                    FrogletEditorPalette.DrawRect(rr, FrogletEditorPalette.Ok.WithAlpha(0.12f));
                else if (l.StartsWith("-") && !l.StartsWith("---"))
                    FrogletEditorPalette.DrawRect(rr, FrogletEditorPalette.Error.WithAlpha(0.12f));
                else if (l.StartsWith("@@"))
                    FrogletEditorPalette.DrawRect(rr, FrogletEditorPalette.Info.WithAlpha(0.10f));
                GUI.Label(rr, l, FrogletEditorPalette.CardBody);
            }
            EditorGUILayout.EndScrollView();
        }

        static void Ping(string repoRelative)
        {
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(repoRelative.Substring(FrogletGit.ProjectPrefix.Length));
            if (asset != null) { Selection.activeObject = asset; EditorGUIUtility.PingObject(asset); }
        }

        // ── History ──────────────────────────────────────────────────────────────

        void DrawHistory()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                _pageScroll = EditorGUILayout.BeginScrollView(_pageScroll, GUILayout.Width(Mathf.Max(360, position.width * 0.55f)));
                foreach (var c in _history)
                {
                    var r = GUILayoutUtility.GetRect(0, 22, GUILayout.ExpandWidth(true));
                    if (c.Sha == _selectedCommit) FrogletEditorPalette.DrawRect(r, FrogletEditorPalette.Info.WithAlpha(0.18f));
                    if (c.Unpushed)
                    {
                        FrogletEditorPalette.DrawAccentStripe(r, FrogletEditorPalette.Ok);
                        FrogletEditorPalette.StatusPill(new Rect(r.x + 6, r.y + 3, 54, 16), "UNPUSHED", FrogletEditorPalette.Ok);
                    }
                    GUI.Label(new Rect(r.x + 66, r.y + 3, r.width - 70, 16),
                        $"{c.ShortSha}  {c.Date}  {c.Author}:  {c.Subject}", FrogletEditorPalette.CardBody);
                    if (Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition))
                    {
                        _selectedCommit = c.Sha;
                        _commitFiles = new List<string> { "…" };
                        var sha = c.Sha;
                        Task.Run(() =>
                        {
                            var files = FrogletSourceControl.CommitFiles(sha);
                            _main.Enqueue(() => { if (_selectedCommit == sha) _commitFiles = files; });
                        });
                        Event.current.Use();
                    }
                }
                EditorGUILayout.EndScrollView();

                using (new EditorGUILayout.VerticalScope())
                {
                    GUILayout.Label(_selectedCommit == null ? "Select a commit" : $"{_commitFiles.Count} file(s)", FrogletEditorPalette.SectionLabel);
                    _sideScroll = EditorGUILayout.BeginScrollView(_sideScroll);
                    foreach (var f in _commitFiles) GUILayout.Label(f, FrogletEditorPalette.CardBody);
                    EditorGUILayout.EndScrollView();
                }
            }
        }

        // ── Branches ─────────────────────────────────────────────────────────────

        void DrawBranches()
        {
            GUILayout.Label("New branch", FrogletEditorPalette.SectionHeader);
            using (new EditorGUILayout.HorizontalScope())
            {
                _newBranch = EditorGUILayout.TextField(_newBranch, GUILayout.MinWidth(260));
                var valid = FrogletSourceControl.IsValidBranchName(_newBranch.Trim(), out var why);
                if (FrogletEditorPalette.ColorButton("Create from latest bleeding-edge", FrogletEditorPalette.Ok, 230, 22,
                        "Fetches, then starts the branch from origin/bleeding-edge and switches to it. Your uncommitted changes come along.",
                        !_busy && valid))
                    _deferred = () => DoCreateBranch(_newBranch.Trim());
                GUILayout.FlexibleSpace();
                if (!valid && _newBranch.Length > 0) GUILayout.Label(why, FrogletEditorPalette.Subtitle);
            }
            GUILayout.Label("Name it yourname/what-it-does, e.g. charles/thresher-audio.", FrogletEditorPalette.Subtitle);

            FrogletEditorPalette.HorizontalRule();
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Switch branch", FrogletEditorPalette.SectionHeader);
                GUILayout.FlexibleSpace();
                GUILayout.Label("Filter", GUILayout.Width(36));
                _branchFilter = EditorGUILayout.TextField(_branchFilter, GUILayout.Width(220));
            }

            var locals = new HashSet<string>(_branches.Where(b => !b.IsRemote).Select(b => b.Name), StringComparer.Ordinal);
            _pageScroll = EditorGUILayout.BeginScrollView(_pageScroll);
            var shown = 0;
            foreach (var b in _branches)
            {
                if (b.IsRemote && locals.Contains(b.Name)) continue; // the local one stands for it
                if (_branchFilter.Length > 0 && b.Name.IndexOf(_branchFilter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (++shown > 150) { GUILayout.Label("… type in the filter to find older branches", FrogletEditorPalette.Subtitle); break; }
                var r = GUILayoutUtility.GetRect(0, 24, GUILayout.ExpandWidth(true));
                var current = !b.IsRemote && b.Name == _branch;
                if (current) FrogletEditorPalette.DrawRect(r, FrogletEditorPalette.Info.WithAlpha(0.18f));
                FrogletEditorPalette.DrawAccentStripe(r, b.IsRemote ? FrogletEditorPalette.Slate : FrogletEditorPalette.Info);
                GUI.Label(new Rect(r.x + 10, r.y + 4, r.width - 140, 16),
                    $"{b.Name}{(b.IsRemote ? "  (on GitHub)" : "")}   {b.Date}  {b.Subject}", FrogletEditorPalette.CardBody);
                if (current)
                    FrogletEditorPalette.StatusPill(new Rect(r.xMax - 110, r.y + 3, 100, 18), "CURRENT", FrogletEditorPalette.Info);
                else if (FrogletEditorPalette.ColorButton(new Rect(r.xMax - 110, r.y + 2, 100, 20), "Switch", FrogletEditorPalette.Info,
                             null, !_busy, outline: true))
                {
                    var target = b;
                    _deferred = () => DoSwitch(target);
                }
            }
            EditorGUILayout.EndScrollView();
        }

        // ── Checks ───────────────────────────────────────────────────────────────

        void DrawChecks()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (FrogletEditorPalette.ColorButton("Run push checks", FrogletEditorPalette.Info, 140, 26,
                        "Check the ticked files and the unpushed commits (Tools/Build/push_checks.py).", !_busy && FrogletPython.IsAvailable))
                    _deferred = () => RunChecks(TickedPaths(), "the ticked files and unpushed commits", null);
                if (FrogletEditorPalette.ColorButton("Run /ship gates", FrogletEditorPalette.Warn, 140, 26,
                        "Every generator --check and static check /ship runs (Tools/Build/ship_gates.py, ~2 min).", !_busy && FrogletPython.IsAvailable))
                    _deferred = DoGates;
                if (FrogletEditorPalette.ColorButton("Ask Claude to ship this", FrogletEditorPalette.Violet, 170, 26,
                        "Copies a /ship request for this branch and opens Claude Code: review, conflicts, the pull request.", true, outline: true))
                    _deferred = DoAskClaude;
                GUILayout.FlexibleSpace();
            }
            if (!FrogletPython.IsAvailable)
            {
                EditorGUILayout.HelpBox("The checks are python (Tools/Build/push_checks.py, ship_gates.py). Install Python 3 from " +
                                        FrogletPython.DownloadUrl + " and reopen this window.", MessageType.Warning);
                if (GUILayout.Button("I installed it: look again", GUILayout.Width(200))) FrogletPython.Reset();
            }

            _pageScroll = EditorGUILayout.BeginScrollView(_pageScroll);
            DrawEditorFindings();
            if (_report != null)
            {
                GUILayout.Label($"Push checks on {_reportFor}: {_report.@checked} path(s)" +
                                (_report.behind != null && _report.behind.commits > 0 ? $", {_report.behind.merges} PRs behind bleeding-edge" : ""),
                                FrogletEditorPalette.SectionHeader);
                if (_report.findings.Length == 0) GUILayout.Label("Nothing to flag.", FrogletEditorPalette.CardBody);
                foreach (var f in _report.findings) DrawFinding(f);
            }
            if (_gateLines.Count > 0 || _gateVerdict != null)
            {
                FrogletEditorPalette.HorizontalRule();
                GUILayout.Label("/ship gates" + (_gateVerdict != null ? ": " + _gateVerdict : " (running)"), FrogletEditorPalette.SectionHeader);
                foreach (var l in _gateLines)
                {
                    if (l.StartsWith("ok    ")) continue; // only what needs reading
                    var r = GUILayoutUtility.GetRect(new GUIContent(l), FrogletEditorPalette.CardBodyWrapped, GUILayout.ExpandWidth(true));
                    FrogletEditorPalette.DrawAccentStripe(r, l.StartsWith("FAIL") ? FrogletEditorPalette.Error : l.StartsWith("GATES: ALL OK")
                        ? FrogletEditorPalette.Ok : FrogletEditorPalette.Muted);
                    GUI.Label(new Rect(r.x + 8, r.y, r.width - 8, r.height), l, FrogletEditorPalette.CardBodyWrapped);
                }
                GUILayout.Label($"{_gateLines.Count(l => l.StartsWith("ok    "))} gate(s) passed.", FrogletEditorPalette.Subtitle);
            }
            EditorGUILayout.EndScrollView();
        }

        /// <summary>What only the editor knows: a failed compile, unsaved tool output.</summary>
        void DrawEditorFindings()
        {
            if (EditorUtility.scriptCompilationFailed)
                DrawFinding(new ScFinding
                {
                    id = "compile", severity = "block", title = "The project does not compile",
                    detail = "The Console has compile errors. A push now breaks the build for everyone who pulls it.",
                    paths = new string[0],
                });
            var tools = FrogletToolChangeLedger.Tools();
            if (tools.Count > 0)
            {
                DrawFinding(new ScFinding
                {
                    id = "tool-output", severity = "info", title = $"{tools.Count} editor tool(s) recorded output",
                    detail = "Their files are in your changes; FrogletTools > Build > Pending Tool Changes shows which tool wrote what.",
                    paths = new string[0],
                });
            }
        }

        void DrawFinding(ScFinding f)
        {
            var accent = f.IsBlock ? FrogletEditorPalette.Error : f.IsWarn ? FrogletEditorPalette.Warn : FrogletEditorPalette.Info;
            var paths = f.paths ?? new string[0];
            var body = (f.detail ?? string.Empty).Trim();
            var shownPaths = paths.Take(12).ToList();
            if (paths.Length > 12) shownPaths.Add($"… and {paths.Length - 12} more");
            var text = body + (shownPaths.Count > 0 ? "\n" + string.Join("\n", shownPaths.Select(p => "  • " + p)) : string.Empty);
            var width = Mathf.Max(200, position.width - 150);
            var h = 26 + FrogletEditorPalette.CardBodyWrapped.CalcHeight(new GUIContent(text), width) + 8;
            var r = GUILayoutUtility.GetRect(0, h, GUILayout.ExpandWidth(true));
            FrogletEditorPalette.DrawCard(r, FrogletEditorPalette.SurfaceRaised, accent.WithAlpha(0.5f));
            FrogletEditorPalette.DrawAccentStripe(r, accent);
            FrogletEditorPalette.StatusPill(new Rect(r.x + 10, r.y + 5, 56, 16), (f.severity ?? "").ToUpperInvariant(), accent);
            GUI.Label(new Rect(r.x + 74, r.y + 4, r.width - 200, 18), f.title, FrogletEditorPalette.CardTitle);
            GUI.Label(new Rect(r.x + 12, r.y + 26, width, h - 28), text, FrogletEditorPalette.CardBodyWrapped);
            if (f.fix == "discard" && paths.Length > 0 &&
                FrogletEditorPalette.ColorButton(new Rect(r.xMax - 120, r.y + 4, 110, 20), "Discard these", FrogletEditorPalette.Error,
                    "Throw these files' changes away (asks first).", !_busy, outline: true))
            {
                var p = paths;
                _deferred = () => DoDiscardPaths(p, f.title);
            }
            EditorGUILayout.Space(4);
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
