using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using CosmicShore.Editor.Froglet;
using CosmicShore.Utility;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// The Launch Console in the editor: "can we land, and what is in the way?" in five seconds,
    /// and one button per launch action. The twin of the claude.ai Launch Console artifact; both
    /// read the launch-status branch's <c>status.json</c> (<c>Docs/LAUNCH/STATUS_CONTRACT.md</c>).
    /// Designed on Frogma, Launch Console tab, Rev 58 (<c>Docs/MENU_REDESIGN_B.md</c> §132): the
    /// docked layout, and below <see cref="NarrowBelow"/> the narrow one.
    ///
    /// READER tool: it writes no assets, records nothing in the tool-change ledger and draws no ship
    /// panel. Its only side effect is git's own fetch bookkeeping. Every action button opens a page
    /// with <see cref="Application.OpenURL"/>: no API call, no token. The fetch runs on the thread
    /// pool (<see cref="LaunchStatusFetcher"/>); OnGUI only draws what the last fetch left.
    /// Configuration (milestone, platforms, links, the audit's verdicts) is <see cref="LaunchConsoleConfigSO"/>.
    /// </summary>
    public sealed class LaunchConsoleWindow : EditorWindow
    {
        const float NarrowBelow = 640f;
        const float Pad = 10f;
        const float Gap = 8f;
        const double RefetchOnFocusMinutes = 10;

        [MenuItem("FrogletTools/Launch/Launch Console", false, 40)]
        [FrogletTool(FrogletToolCategory.Build, Importance = 4,
            Description = "Can we land? Each launch platform's go / no-go from the launch-status branch, its blockers, and one button per launch action.",
            DocPath = "Docs/LAUNCH/STATUS_CONTRACT.md#the-editor-window")]
        public static void Open()
        {
            var w = GetWindow<LaunchConsoleWindow>();
            w.titleContent = new GUIContent("Launch Console");
            w.minSize = new Vector2(360f, 420f);
            w.Show();
        }

        [SerializeField] string _selected = "windows";
        [SerializeField] bool _gatesOpen;
        [SerializeField] bool _humansOpen;

        LaunchConsoleConfigSO _config;
        LaunchStatusBoard _board;
        string _boardCommit;
        bool _boardFromCache;
        DateTime? _fetchedUtc;
        string _fetchError;
        string _parseError;
        Task<LaunchFetchResult> _pending;
        Vector2 _scroll;
        Styles _styles;

        // ── Lifecycle ────────────────────────────────────────────────────────────

        void OnEnable()
        {
            LoadConfig();
            StartFetch();
        }

        void OnDisable() => EditorApplication.update -= Poll;

        void OnFocus()
        {
            if (_pending == null && _fetchedUtc is DateTime t && (DateTime.UtcNow - t).TotalMinutes > RefetchOnFocusMinutes)
                StartFetch();
        }

        void LoadConfig()
        {
            _config = AssetDatabase.LoadAssetAtPath<LaunchConsoleConfigSO>(LaunchConsoleConfigSO.AssetPath);
            if (_config != null) return;
            var guids = AssetDatabase.FindAssets("t:" + nameof(LaunchConsoleConfigSO));
            if (guids.Length > 0)
                _config = AssetDatabase.LoadAssetAtPath<LaunchConsoleConfigSO>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        void StartFetch()
        {
            if (_pending != null || _config == null) return;
            // Captured here, on the main thread: RepoRoot reads Application.dataPath.
            var root = FrogletGit.RepoRoot;
            _pending = LaunchStatusFetcher.FetchAsync(root, _config.statusBranch, _config.statusFile);
            EditorApplication.update -= Poll;
            EditorApplication.update += Poll;
        }

        void Poll()
        {
            if (_pending == null) { EditorApplication.update -= Poll; return; }
            if (!_pending.IsCompleted) return;
            EditorApplication.update -= Poll;
            var task = _pending;
            _pending = null;
            if (task.IsFaulted || task.IsCanceled)
            {
                _fetchedUtc = DateTime.UtcNow;
                _fetchError = "The fetch stopped: " + (task.Exception?.GetBaseException().Message ?? "cancelled");
            }
            else Apply(task.Result);
            Repaint();
        }

        void Apply(LaunchFetchResult r)
        {
            _fetchedUtc = r.FinishedUtc;
            _fetchError = r.Error;
            if (r.Json == null) return;
            var board = LaunchStatusBoard.Parse(r.Json, out var err);
            if (board == null)
            {
                // A file the publisher's contract should never let through: a real fault, said once.
                if (_parseError != err) CSDebug.LogWarning("[LaunchConsole] " + err);
                _parseError = err;
                return;
            }
            _parseError = null;
            _board = board;
            _boardCommit = r.Commit;
            _boardFromCache = r.FromCache;
        }

        // ── Drawing ──────────────────────────────────────────────────────────────

        readonly struct Verdict
        {
            public readonly LaunchPlatformEntry Platform;
            public readonly LaunchState State;
            public readonly string Why;
            public Verdict(LaunchPlatformEntry p, LaunchState s, string why) { Platform = p; State = s; Why = why; }
        }

        void OnGUI()
        {
            _styles ??= new Styles();
            _styles.Refresh();

            var cfg = _config;
            if (cfg == null)
            {
                FrogletEditorPalette.Banner("Launch Console", "No launch configuration found.",
                    FrogletEditorPalette.ColorFor(FrogletToolCategory.Build));
                EditorGUILayout.HelpBox("The window reads " + LaunchConsoleConfigSO.AssetPath +
                    " (a LaunchConsoleConfigSO). It is not in this checkout.", MessageType.Warning);
                if (GUILayout.Button("Look again", GUILayout.Width(120))) { LoadConfig(); StartFetch(); }
                return;
            }

            var now = DateTime.UtcNow;
            bool narrow = position.width < NarrowBelow;
            var verdicts = new List<Verdict>(cfg.platforms.Count);
            foreach (var p in cfg.platforms)
            {
                if (p == null) continue;
                var s = LaunchRules.PlatformState(LaunchConsoleConfigSO.IdsFor(p, _board), _board,
                    p.auditVerdict, p.auditWhy, now, cfg.staleHours, out var why);
                verdicts.Add(new Verdict(p, s, why));
            }

            DrawHeader(cfg, now, narrow);
            DrawToolbar(cfg, now, narrow);

            float width = Mathf.Max(200f, position.width - 2f * Pad - 14f);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            GUILayout.BeginHorizontal();
            GUILayout.Space(Pad);
            GUILayout.BeginVertical(GUILayout.Width(width));

            DrawVerdict(verdicts, width);
            DrawPills(verdicts, width, narrow);
            DrawSourceLine(cfg, now, width);

            var selected = verdicts.Find(v => v.Platform.key == _selected);
            if (selected.Platform == null && verdicts.Count > 0) selected = verdicts[0];
            if (selected.Platform != null) DrawPanel(cfg, selected, now, width, narrow);

            if (narrow) DrawFolded(cfg, now, width);
            else DrawGatesAndHumans(cfg, now, width);

            GUILayout.Space(Pad);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();
        }

        void DrawHeader(LaunchConsoleConfigSO cfg, DateTime now, bool narrow)
        {
            var days = cfg.MilestoneUtc is DateTime m ? LaunchRules.DaysLeft(m, now) : (int?)null;
            var daysLine = days is int d
                ? d.ToString(CultureInfo.InvariantCulture) + (d == 1 ? " day" : " days") + " to " + cfg.MilestoneLabel
                : "no milestone date set";
            FrogletEditorPalette.Banner(cfg.milestoneName, "Launch Console · " + daysLine,
                FrogletEditorPalette.ColorFor(FrogletToolCategory.Build));
            if (narrow || days == null) return;
            var r = GUILayoutUtility.GetLastRect();
            GUI.Label(new Rect(r.xMax - 260f, r.y + 6f, 246f, 40f), days.Value.ToString(CultureInfo.InvariantCulture), _styles.BigNumber);
        }

        void DrawToolbar(LaunchConsoleConfigSO cfg, DateTime now, bool narrow)
        {
            var row = GUILayoutUtility.GetRect(0f, 32f, GUILayout.ExpandWidth(true));
            float x = row.x + Pad, y = row.y + 5f;
            bool busy = _pending != null;
            if (FrogletEditorPalette.FamilyButton(new Rect(x, y, 84f, 22f), busy ? "Fetching…" : "Refresh", false, false,
                    "Fetch " + cfg.statusBranch + " and read " + cfg.statusFile, !busy))
                StartFetch();
            x += 84f + Gap;

            var consoleLabel = narrow ? "Checklist and Needs a human ↗" : "Checklist and Needs a human: open the console ↗";
            float cw = _styles.Button.CalcSize(new GUIContent(consoleLabel)).x + 24f;
            var consoleUrl = LaunchRules.SafeUrl(cfg.consoleUrl);
            if (FrogletEditorPalette.FamilyButton(new Rect(x, y, cw, 22f), consoleLabel, false, false,
                    "The live checklist lives in the console artifact's store, which this window cannot read.", consoleUrl != null))
                Application.OpenURL(consoleUrl);
            x += cw + Gap;

            var status = busy ? "Fetching " + cfg.statusBranch + "…"
                : _fetchedUtc is DateTime f ? "Fetched " + LaunchRules.AgeText(now - f) : "Not fetched yet";
            var style = _fetchError != null || _parseError != null ? _styles.Danger : _styles.Muted;
            var text = _parseError ?? (_fetchError != null ? _fetchError : status);
            GUI.Label(new Rect(x, y + 3f, Mathf.Max(0f, row.xMax - x - Pad), 18f), new GUIContent(text, text), style);
        }

        void DrawVerdict(List<Verdict> verdicts, float width)
        {
            int nogo = 0, risk = 0, go = 0;
            foreach (var v in verdicts)
            {
                if (v.State == LaunchState.NoGo) nogo++;
                else if (v.State == LaunchState.AtRisk) risk++;
                else if (v.State == LaunchState.Go) go++;
            }
            LaunchState overall = nogo > 0 ? LaunchState.NoGo : risk > 0 ? LaunchState.AtRisk
                : go == verdicts.Count && go > 0 ? LaunchState.Go : LaunchState.Unknown;
            string line = overall switch
            {
                LaunchState.NoGo => nogo + " of " + verdicts.Count + " no-go, " + risk + " at risk. Nothing lands until every pill reads Go.",
                LaunchState.AtRisk => risk + " of " + verdicts.Count + " at risk.",
                LaunchState.Go => "Every platform is green and fresh.",
                _ => "Not every platform has a record or a verdict yet.",
            };
            GUILayout.Space(6f);
            var word = LaunchRules.Word(overall).ToUpperInvariant();
            float ww = _styles.Verdict.CalcSize(new GUIContent(word)).x;
            float lh = Mathf.Max(22f, _styles.HeaderWrap.CalcHeight(new GUIContent(line), width - ww - Gap));
            var r = GUILayoutUtility.GetRect(width, lh);
            _styles.Verdict.normal.textColor = StateColor(overall);
            GUI.Label(new Rect(r.x, r.y, ww, 22f), word, _styles.Verdict);
            GUI.Label(new Rect(r.x + ww + Gap, r.y + 3f, width - ww - Gap, lh), line, _styles.HeaderWrap);
            GUILayout.Space(4f);
        }

        void DrawPills(List<Verdict> verdicts, float width, bool narrow)
        {
            if (verdicts.Count == 0) return;
            if (narrow)
            {
                const float rowH = 28f;
                var area = GUILayoutUtility.GetRect(width, verdicts.Count * (rowH + 4f));
                for (int i = 0; i < verdicts.Count; i++)
                {
                    var v = verdicts[i];
                    var r = new Rect(area.x, area.y + i * (rowH + 4f), width, rowH);
                    PillFrame(r, v.State, v.Platform.key == _selected, vertical: true);
                    GUI.Label(new Rect(r.x + 10f, r.y, 112f, rowH), v.Platform.displayName, _styles.Name);
                    StateWord(new Rect(r.x + 126f, r.y + 6f, 90f, 16f), v.State);
                    GUI.Label(new Rect(r.x + 220f, r.y, r.width - 228f, rowH), v.Why, _styles.Muted);
                    PillClick(r, v);
                }
                GUILayout.Space(4f);
                return;
            }

            const float minW = 120f, pillH = 64f;
            int cols = Mathf.Clamp(Mathf.FloorToInt((width + Gap) / (minW + Gap)), 1, verdicts.Count);
            int rows = Mathf.CeilToInt(verdicts.Count / (float)cols);
            float pw = (width - (cols - 1) * Gap) / cols;
            var grid = GUILayoutUtility.GetRect(width, rows * pillH + (rows - 1) * Gap);
            for (int i = 0; i < verdicts.Count; i++)
            {
                var v = verdicts[i];
                var r = new Rect(grid.x + (i % cols) * (pw + Gap), grid.y + (i / cols) * (pillH + Gap), pw, pillH);
                PillFrame(r, v.State, v.Platform.key == _selected, vertical: false);
                GUI.Label(new Rect(r.x + 10f, r.y + 6f, r.width - 18f, 16f), v.Platform.displayName, _styles.Name);
                StateWord(new Rect(r.x + 10f, r.y + 24f, r.width - 18f, 14f), v.State);
                GUI.Label(new Rect(r.x + 10f, r.y + 38f, r.width - 18f, 24f), v.Why, _styles.SmallWrap);
                PillClick(r, v);
            }
            GUILayout.Space(4f);
        }

        void PillFrame(Rect r, LaunchState state, bool selected, bool vertical)
        {
            var border = state == LaunchState.Unknown ? FrogletEditorPalette.Muted.WithAlpha(0.5f) : StateColor(state);
            FrogletEditorPalette.DrawSliver(r, FrogletEditorPalette.Surface, border, vertical ? 6f : 10f,
                vertical ? 1f : 2f, dashed: state == LaunchState.AtRisk);
            if (!selected) return;
            // The pick is one line in the selection colour, inside the pill (an outline would be clipped by the sliver).
            var sel = FrogletEditorPalette.Families.Selection;
            if (vertical) FrogletEditorPalette.DrawRect(new Rect(r.x + 2f, r.y + 6f, 3f, r.height - 8f), sel);
            else FrogletEditorPalette.DrawRect(new Rect(r.x + 3f, r.yMax - 5f, r.width - 16f, 3f), sel);
        }

        void PillClick(Rect r, Verdict v)
        {
            if (GUI.Button(r, new GUIContent("", v.Platform.displayName + ": " + LaunchRules.Word(v.State) + ", " + v.Why), GUIStyle.none))
            {
                _selected = v.Platform.key;
                GUI.FocusControl(null);
            }
            EditorGUIUtility.AddCursorRect(r, MouseCursor.Link);
        }

        void StateWord(Rect r, LaunchState state)
        {
            var col = StateColor(state);
            var dot = new Rect(r.x, r.y + (r.height - 8f) * 0.5f, 8f, 8f);
            if (state == LaunchState.AtRisk)
            {
                FrogletEditorPalette.DrawCard(dot, Color.clear, col);
                FrogletEditorPalette.DrawRect(new Rect(dot.x, dot.y, 4f, 8f), col);
            }
            else FrogletEditorPalette.DrawRect(dot, col);
            _styles.State.normal.textColor = col;
            GUI.Label(new Rect(r.x + 13f, r.y, r.width - 13f, r.height), LaunchRules.Word(state).ToUpperInvariant(), _styles.State);
        }

        void DrawSourceLine(LaunchConsoleConfigSO cfg, DateTime now, float width)
        {
            string pipelines;
            if (_board == null) pipelines = "Pipelines: no status read yet from " + cfg.statusBranch + ".";
            else
            {
                pipelines = "Pipelines: " + cfg.statusBranch + (string.IsNullOrEmpty(_boardCommit) ? "" : " @ " + _boardCommit);
                if (_board.GeneratedAt is DateTime g)
                    pipelines += ", generated " + g.ToString("d MMM HH:mm", CultureInfo.InvariantCulture) + " UTC (" + LaunchRules.AgeText(now - g) + ")";
                if (_boardFromCache) pipelines += ", the last copy this checkout had (the fetch failed)";
                pipelines += ".";
            }
            var text = pipelines + " Verdicts and blockers: " + cfg.auditSource + ", measured " + cfg.auditDate +
                ". Ages count from a record's event date when it carries one.";
            var h = _styles.SmallWrap.CalcHeight(new GUIContent(text), width);
            GUI.Label(GUILayoutUtility.GetRect(width, h), text, _styles.SmallWrap);
            GUILayout.Space(Gap);
        }

        // The selected platform: its records, its actions, its blockers. Measured, then drawn.
        void DrawPanel(LaunchConsoleConfigSO cfg, Verdict v, DateTime now, float width, bool narrow)
        {
            float rightW = narrow ? 0f : Mathf.Clamp(width * 0.36f, 260f, 440f);
            float leftW = narrow ? width - 24f : width - rightW - 24f - 16f;
            float leftH = PanelLeft(cfg, v, now, new Rect(0f, 0f, leftW, 0f), narrow, draw: false);
            float rightH = narrow ? 0f : PanelBlockers(cfg, v.Platform, new Rect(0f, 0f, rightW, 0f), narrow, draw: false);
            float blockersBelow = narrow ? PanelBlockers(cfg, v.Platform, new Rect(0f, 0f, leftW, 0f), narrow, draw: false) : 0f;
            float h = Mathf.Max(leftH + blockersBelow, rightH) + 24f;

            var area = GUILayoutUtility.GetRect(width, h);
            FrogletEditorPalette.DrawSliver(area, FrogletEditorPalette.Surface, FrogletEditorPalette.Muted.WithAlpha(0.45f), 14f);
            var left = new Rect(area.x + 12f, area.y + 12f, leftW, leftH);
            PanelLeft(cfg, v, now, left, narrow, draw: true);
            if (narrow)
                PanelBlockers(cfg, v.Platform, new Rect(left.x, left.yMax, leftW, blockersBelow), narrow, draw: true);
            else
            {
                var line = new Rect(left.xMax + 8f, area.y + 12f, 1f, h - 24f);
                FrogletEditorPalette.DrawRect(line, FrogletEditorPalette.Muted.WithAlpha(0.35f));
                PanelBlockers(cfg, v.Platform, new Rect(line.x + 16f, area.y + 12f, rightW, rightH), narrow, draw: true);
            }
            GUILayout.Space(Gap);
        }

        float PanelLeft(LaunchConsoleConfigSO cfg, Verdict v, DateTime now, Rect a, bool narrow, bool draw)
        {
            var p = v.Platform;
            float y = a.y;

            // Name, state, why.
            if (draw)
            {
                float nw = _styles.PanelTitle.CalcSize(new GUIContent(p.displayName)).x;
                GUI.Label(new Rect(a.x, y, nw, 20f), p.displayName, _styles.PanelTitle);
                StateWord(new Rect(a.x + nw + 10f, y + 3f, 80f, 14f), v.State);
                if (!narrow) GUI.Label(new Rect(a.x + nw + 96f, y + 2f, a.width - nw - 96f, 16f), v.Why, _styles.Muted);
            }
            y += 22f;
            if (narrow)
            {
                float wh = _styles.SmallWrap.CalcHeight(new GUIContent(v.Why), a.width);
                if (draw) GUI.Label(new Rect(a.x, y, a.width, wh), v.Why, _styles.SmallWrap);
                y += wh + 2f;
            }

            var rows = new List<(string label, string id)>();
            foreach (var c in p.channels) if (c != null) rows.Add((c.label, c.id));
            if (!string.IsNullOrEmpty(p.buildId)) rows.Add(("Last build", p.buildId));
            bool gatesHere = !string.IsNullOrEmpty(p.extraIdPrefix);
            if (gatesHere && _board != null)
                foreach (var id in LaunchConsoleConfigSO.IdsFor(p, _board))
                    if (id.StartsWith(p.extraIdPrefix, StringComparison.Ordinal)) rows.Add((id, id));

            y += 4f;
            var head = rows.Count == 0 ? "NO CHANNEL OF ITS OWN" : gatesHere ? "GATES AND THE LAST RUN" : "CHANNELS AND THE LAST RUN";
            if (draw) GUI.Label(new Rect(a.x, y, a.width, 14f), head, _styles.Cap);
            y += 16f;

            if (rows.Count > 0 && !narrow)
            {
                if (draw)
                {
                    Columns(a, out var c0, out var c1, out var c2, out var c3);
                    GUI.Label(new Rect(c0.x, y, c0.width, 14f), gatesHere ? "GATE" : "CHANNEL", _styles.Cap);
                    GUI.Label(new Rect(c1.x, y, c1.width, 14f), "VERSION", _styles.Cap);
                    GUI.Label(new Rect(c2.x, y, c2.width, 14f), "RESULT", _styles.Cap);
                    GUI.Label(new Rect(c3.x, y, c3.width, 14f), "AGE · SOURCE", _styles.Cap);
                    FrogletEditorPalette.DrawRect(new Rect(a.x, y + 16f, a.width, 1f), FrogletEditorPalette.Muted.WithAlpha(0.35f));
                }
                y += 20f;
            }
            foreach (var (label, id) in rows)
                y += RecordRow(cfg, label, id, now, new Rect(a.x, y, a.width, 0f), narrow, draw);

            y += 8f;
            if (draw) GUI.Label(new Rect(a.x, y, a.width, 14f), "START HERE", _styles.Cap);
            y += 16f;
            y += Actions(p, new Rect(a.x, y, a.width, 0f), narrow, draw) + 4f;

            const string caption = "Each button opens the workflow page or dashboard where the action runs. Production steps wait for a reviewer there; this window holds no tokens.";
            float ch = _styles.SmallWrap.CalcHeight(new GUIContent(caption), a.width);
            if (draw) GUI.Label(new Rect(a.x, y, a.width, ch), caption, _styles.SmallWrap);
            y += ch + 4f;

            if (!string.IsNullOrEmpty(p.rollback))
            {
                var rb = "Roll back by: " + p.rollback;
                float rh = _styles.BodyWrap.CalcHeight(new GUIContent(rb), a.width);
                if (draw) GUI.Label(new Rect(a.x, y, a.width, rh), rb, _styles.BodyWrap);
                y += rh + 4f;
            }
            return y - a.y;
        }

        static void Columns(Rect a, out Rect c0, out Rect c1, out Rect c2, out Rect c3)
        {
            float w0 = 130f, w1 = 110f, w2 = Mathf.Max(140f, (a.width - w0 - w1) * 0.5f);
            c0 = new Rect(a.x, 0f, w0 - 8f, 0f);
            c1 = new Rect(a.x + w0, 0f, w1 - 8f, 0f);
            c2 = new Rect(a.x + w0 + w1, 0f, w2 - 8f, 0f);
            c3 = new Rect(a.x + w0 + w1 + w2, 0f, Mathf.Max(60f, a.width - w0 - w1 - w2), 0f);
        }

        float RecordRow(LaunchConsoleConfigSO cfg, string label, string id, DateTime now, Rect a, bool narrow, bool draw)
        {
            var r = _board?.Get(id);
            float y = a.y;
            if (narrow)
            {
                if (draw)
                {
                    float lw = Mathf.Min(_styles.Name.CalcSize(new GUIContent(label)).x, a.width * 0.5f);
                    GUI.Label(new Rect(a.x, y, lw, 16f), new GUIContent(label, id), _styles.Name);
                    float x = a.x + lw + 8f;
                    if (r != null) x += Chip(new Rect(x, y + 1f, 0f, 14f), r.Result) + 8f;
                    GUI.Label(new Rect(x, y, a.xMax - x, 16f), r == null ? "No run recorded" : r.VersionLine, r == null ? _styles.Muted : _styles.Body);
                }
                y += 18f;
                if (r != null)
                {
                    if (draw) GUI.Label(new Rect(a.x, y, a.width, 14f), new GUIContent(r.Summary ?? "", r.Summary), _styles.Small);
                    y += 15f;
                    if (draw) AgeSource(r, now, cfg, new Rect(a.x, y, a.width, 14f));
                    y += 15f;
                }
                if (draw) FrogletEditorPalette.DrawRect(new Rect(a.x, y + 3f, a.width, 1f), FrogletEditorPalette.SurfaceRaised);
                return y + 7f - a.y;
            }

            Columns(a, out var c0, out var c1, out var c2, out var c3);
            if (draw)
            {
                GUI.Label(new Rect(c0.x, y, c0.width, 16f), new GUIContent(label, id), _styles.Name);
                if (r == null)
                    GUI.Label(new Rect(c1.x, y, a.xMax - c1.x, 16f), "No run recorded", _styles.Muted);
                else
                {
                    GUI.Label(new Rect(c1.x, y, c1.width, 16f), r.VersionLine, _styles.Body);
                    Chip(new Rect(c2.x, y + 1f, 0f, 14f), r.Result);
                    GUI.Label(new Rect(c2.x, y + 17f, c2.width, 14f), new GUIContent(r.Summary ?? "", r.Summary), _styles.Small);
                    AgeSource(r, now, cfg, new Rect(c3.x, y, c3.width, 14f));
                }
            }
            y += r == null ? 20f : 34f;
            if (draw) FrogletEditorPalette.DrawRect(new Rect(a.x, y, a.width, 1f), FrogletEditorPalette.SurfaceRaised);
            return y + 5f - a.y;
        }

        float Chip(Rect r, string result)
        {
            var text = result ?? "?";
            float w = _styles.Chip.CalcSize(new GUIContent(text)).x + 10f;
            var col = result == "success" ? FrogletEditorPalette.Families.Cta
                : result == "failure" ? FrogletEditorPalette.Families.Danger : FrogletEditorPalette.Muted;
            var rect = new Rect(r.x, r.y, w, r.height);
            FrogletEditorPalette.DrawCard(rect, Color.clear, col);
            _styles.Chip.normal.textColor = col;
            GUI.Label(rect, text, _styles.Chip);
            return w;
        }

        void AgeSource(LaunchRecord r, DateTime now, LaunchConsoleConfigSO cfg, Rect a)
        {
            var age = LaunchRules.Age(r, now);
            var ageText = age is TimeSpan t ? LaunchRules.AgeText(t) : "age unknown";
            bool stale = LaunchRules.IsStale(r, now, cfg.staleHours) || age == null;
            float aw = _styles.Small.CalcSize(new GUIContent(ageText)).x;
            var tip = (r.EventDate is DateTime ev ? "Event " + ev.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "; " : "") +
                      (r.UpdatedAt is DateTime up ? "recorded " + up.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " UTC" : "no publish time");
            GUI.Label(new Rect(a.x, a.y, aw, a.height), new GUIContent(ageText, tip), stale ? _styles.SmallDanger : _styles.Small);
            var url = r.OpenUrl;
            var src = "· " + (r.Source ?? "unknown source") + (url != null ? " ↗" : "");
            var sr = new Rect(a.x + aw + 4f, a.y, Mathf.Max(0f, a.width - aw - 4f), a.height);
            GUI.Label(sr, new GUIContent(src, url ?? r.Source), _styles.SmallMuted);
            if (url != null)
            {
                EditorGUIUtility.AddCursorRect(sr, MouseCursor.Link);
                if (GUI.Button(sr, GUIContent.none, GUIStyle.none)) Application.OpenURL(url);
            }
        }

        float Actions(LaunchPlatformEntry p, Rect a, bool narrow, bool draw)
        {
            const float h = 24f;
            if (p.actions.Count == 0) return 0f;
            if (narrow)
            {
                float bw = (a.width - Gap) * 0.5f;
                for (int i = 0; i < p.actions.Count; i++)
                {
                    var r = new Rect(a.x + (i % 2) * (bw + Gap), a.y + (i / 2) * (h + 6f), bw, h);
                    if (draw) ActionButton(p.actions[i], r);
                }
                return Mathf.CeilToInt(p.actions.Count / 2f) * (h + 6f);
            }
            float x = a.x, y = a.y;
            foreach (var act in p.actions)
            {
                if (act == null) continue;
                float w = _styles.Button.CalcSize(new GUIContent(act.label + " ↗")).x + 26f;
                if (x > a.x && x + w > a.xMax) { x = a.x; y += h + 6f; }
                if (draw) ActionButton(act, new Rect(x, y, w, h));
                x += w + Gap;
            }
            return y + h + 2f - a.y;
        }

        static void ActionButton(LaunchActionEntry act, Rect r)
        {
            var url = LaunchRules.SafeUrl(act.url);
            if (FrogletEditorPalette.FamilyButton(r, act.label + " ↗",
                    act.kind == LaunchActionKind.CallToAction, act.kind == LaunchActionKind.Commit,
                    url != null ? "Opens " + url : "No https link set for this action", url != null))
                Application.OpenURL(url);
        }

        float PanelBlockers(LaunchConsoleConfigSO cfg, LaunchPlatformEntry p, Rect a, bool narrow, bool draw)
        {
            float y = a.y + (narrow ? 8f : 0f);
            var head = narrow
                ? "OPEN BLOCKERS (" + p.blockerRanks.Count + "), " + (cfg.auditSource ?? "").ToUpperInvariant()
                : "Open blockers (" + p.blockerRanks.Count + ")";
            if (draw)
            {
                GUI.Label(new Rect(a.x, y, a.width, 16f), head, narrow ? _styles.Cap : _styles.Name);
                if (!narrow) GUI.Label(new Rect(a.x, y, a.width, 16f), cfg.auditSource + ", " + cfg.auditDate, _styles.SmallMutedRight);
            }
            y += 20f;
            if (p.blockerRanks.Count == 0)
            {
                if (draw) GUI.Label(new Rect(a.x, y, a.width, 16f), "None in the audit for this platform.", _styles.Muted);
                y += 18f;
            }
            foreach (var rank in p.blockerRanks)
            {
                var b = cfg.Blocker(rank);
                var title = b != null ? b.title : "(rank " + rank + " is not in the config)";
                var who = b != null ? b.decider : "";
                bool human = b != null && b.needsHuman;
                var tip = "#" + rank + " " + title + (b != null ? ". Decides: " + who : "") + (human ? ". Needs a human." : "");
                if (draw)
                {
                    GUI.Label(new Rect(a.x, y, 30f, 16f), "#" + rank, _styles.Muted);
                    if (narrow)
                    {
                        GUI.Label(new Rect(a.x + 32f, y, a.width - 32f, 16f), new GUIContent(title, tip), _styles.Name);
                        float ww = _styles.Small.CalcSize(new GUIContent(who)).x;
                        GUI.Label(new Rect(a.x + 32f, y + 16f, ww, 14f), who, _styles.SmallMuted);
                        if (human) GUI.Label(new Rect(a.x + 40f + ww, y + 16f, 80f, 14f), "HUMAN", _styles.SmallDanger);
                    }
                    else
                    {
                        float tagW = human ? 52f : 0f;
                        float ww = Mathf.Min(_styles.Small.CalcSize(new GUIContent(who)).x, a.width * 0.4f);
                        GUI.Label(new Rect(a.x + 32f, y, a.width - 32f - ww - tagW - 8f, 16f), new GUIContent(title, tip), _styles.Name);
                        GUI.Label(new Rect(a.xMax - ww - tagW, y + 1f, ww, 14f), new GUIContent(who, tip), _styles.SmallMuted);
                        if (human) GUI.Label(new Rect(a.xMax - tagW + 6f, y + 1f, tagW, 14f), "HUMAN", _styles.SmallDanger);
                    }
                    FrogletEditorPalette.DrawRect(new Rect(a.x, y + (narrow ? 33f : 21f), a.width, 1f), FrogletEditorPalette.SurfaceRaised);
                }
                y += narrow ? 36f : 24f;
            }
            const string note = "Who takes a blocker, and its live status, are in the console's checklist, which this window cannot read.";
            float nh = _styles.SmallWrap.CalcHeight(new GUIContent(note), a.width);
            if (draw) GUI.Label(new Rect(a.x, y + 4f, a.width, nh), note, _styles.SmallWrap);
            y += nh + 6f;
            return y - a.y;
        }

        void DrawGatesAndHumans(LaunchConsoleConfigSO cfg, DateTime now, float width)
        {
            float colW = (width - Gap) * 0.5f;
            float gh = Gates(cfg, now, new Rect(0f, 0f, colW - 24f, 0f), draw: false);
            float hh = Humans(cfg, new Rect(0f, 0f, colW - 24f, 0f), draw: false);
            float h = Mathf.Max(gh, hh) + 24f;
            var area = GUILayoutUtility.GetRect(width, h);
            var l = new Rect(area.x, area.y, colW, h);
            var r = new Rect(area.x + colW + Gap, area.y, colW, h);
            var edge = FrogletEditorPalette.Muted.WithAlpha(0.45f);
            FrogletEditorPalette.DrawSliver(l, FrogletEditorPalette.Surface, edge, 14f);
            FrogletEditorPalette.DrawSliver(r, FrogletEditorPalette.Surface, edge, 14f);
            Gates(cfg, now, new Rect(l.x + 12f, l.y + 12f, colW - 24f, 0f), draw: true);
            Humans(cfg, new Rect(r.x + 12f, r.y + 12f, colW - 24f, 0f), draw: true);
        }

        void DrawFolded(LaunchConsoleConfigSO cfg, DateTime now, float width)
        {
            int failed = 0;
            foreach (var g in GateRows(cfg)) if (_board?.Get(g.id)?.Result == "failure") failed++;
            if (Fold(GUILayoutUtility.GetRect(width, 26f), "Quality gates", ref _gatesOpen, failed > 0 ? failed + " failed" : "", failed > 0))
            {
                float gh = Gates(cfg, now, new Rect(0f, 0f, width - 8f, 0f), draw: false, titled: false);
                var gr = GUILayoutUtility.GetRect(width, gh + 8f);
                Gates(cfg, now, new Rect(gr.x + 4f, gr.y + 4f, width - 8f, gh), draw: true, titled: false);
            }
            GUILayout.Space(4f);
            if (Fold(GUILayoutUtility.GetRect(width, 26f), "Needs a human, in order", ref _humansOpen, cfg.humanSteps.Count + " steps", false))
            {
                float hh = Humans(cfg, new Rect(0f, 0f, width - 8f, 0f), draw: false, titled: false);
                var hr = GUILayoutUtility.GetRect(width, hh + 8f);
                Humans(cfg, new Rect(hr.x + 4f, hr.y + 4f, width - 8f, hh), draw: true, titled: false);
            }
        }

        bool Fold(Rect r, string label, ref bool open, string note, bool danger)
        {
            FrogletEditorPalette.DrawSliver(r, FrogletEditorPalette.SurfaceRaised, FrogletEditorPalette.Muted.WithAlpha(0.5f), 6f);
            GUI.Label(new Rect(r.x + 8f, r.y + 5f, 14f, 16f), open ? "▾" : "▸", _styles.Muted);
            GUI.Label(new Rect(r.x + 24f, r.y + 5f, r.width - 120f, 16f), label, _styles.Name);
            GUI.Label(new Rect(r.xMax - 108f, r.y + 5f, 100f, 16f), note, danger ? _styles.SmallDangerRight : _styles.SmallMutedRight);
            if (GUI.Button(r, GUIContent.none, GUIStyle.none)) open = !open;
            EditorGUIUtility.AddCursorRect(r, MouseCursor.Link);
            return open;
        }

        List<(string label, string id)> GateRows(LaunchConsoleConfigSO cfg)
        {
            var rows = new List<(string, string)>();
            foreach (var g in cfg.gates) if (g != null) rows.Add((g.label, g.id));
            if (_board != null && !string.IsNullOrEmpty(cfg.smokePrefix))
            {
                var smoke = new List<string>();
                foreach (var id in _board.Records.Keys)
                    if (id.StartsWith(cfg.smokePrefix, StringComparison.Ordinal)) smoke.Add(id);
                smoke.Sort(StringComparer.Ordinal);
                foreach (var id in smoke) rows.Add(("Smoke: " + id.Substring(cfg.smokePrefix.Length), id));
            }
            return rows;
        }

        float Gates(LaunchConsoleConfigSO cfg, DateTime now, Rect a, bool draw, bool titled = true)
        {
            float y = a.y;
            if (titled)
            {
                if (draw) GUI.Label(new Rect(a.x, y, a.width, 16f), "Quality gates", _styles.Name);
                y += 20f;
            }
            var rows = GateRows(cfg);
            bool anySmoke = false;
            foreach (var (label, id) in rows)
            {
                anySmoke |= !string.IsNullOrEmpty(cfg.smokePrefix) && id.StartsWith(cfg.smokePrefix, StringComparison.Ordinal);
                var r = _board?.Get(id);
                if (draw)
                {
                    GUI.Label(new Rect(a.x, y, 160f, 16f), new GUIContent(label, id), _styles.Name);
                    if (r == null) GUI.Label(new Rect(a.x + 168f, y, a.width - 168f, 16f), "No run recorded", _styles.Muted);
                    else
                    {
                        float cw = Chip(new Rect(a.x + 168f, y + 1f, 0f, 14f), r.Result);
                        float x = a.x + 168f + Mathf.Max(cw, 60f) + 8f;
                        GUI.Label(new Rect(x, y, a.xMax - x, 14f), new GUIContent(r.Summary ?? "", r.Summary), _styles.Small);
                        AgeSource(r, now, cfg, new Rect(x, y + 16f, a.xMax - x, 14f));
                    }
                    FrogletEditorPalette.DrawRect(new Rect(a.x, y + (r == null ? 19f : 33f), a.width, 1f), FrogletEditorPalette.SurfaceRaised);
                }
                y += r == null ? 24f : 38f;
            }
            if (!anySmoke)
            {
                if (draw)
                {
                    GUI.Label(new Rect(a.x, y, 160f, 16f), "Smoke per mode", _styles.Name);
                    GUI.Label(new Rect(a.x + 168f, y, a.width - 168f, 16f), "No smoke run recorded", _styles.Muted);
                }
                y += 24f;
            }
            foreach (var note in cfg.qualityNotes)
            {
                if (string.IsNullOrEmpty(note)) continue;
                var text = "Audit, " + cfg.auditDate + ": " + note;
                float h = _styles.BodyWrap.CalcHeight(new GUIContent(text), a.width);
                if (draw) GUI.Label(new Rect(a.x, y, a.width, h), text, _styles.BodyWrap);
                y += h + 2f;
            }
            return y - a.y;
        }

        float Humans(LaunchConsoleConfigSO cfg, Rect a, bool draw, bool titled = true)
        {
            float y = a.y;
            if (titled)
            {
                if (draw)
                {
                    GUI.Label(new Rect(a.x, y, a.width, 16f), "Needs a human, in order", _styles.Name);
                    GUI.Label(new Rect(a.x, y, a.width, 16f), cfg.auditSource + ", held in the config", _styles.SmallMutedRight);
                }
                y += 20f;
            }
            for (int i = 0; i < cfg.humanSteps.Count; i++)
            {
                var s = cfg.humanSteps[i];
                if (s == null) continue;
                if (draw)
                {
                    float ww = Mathf.Min(_styles.Small.CalcSize(new GUIContent(s.who ?? "")).x, a.width * 0.3f);
                    GUI.Label(new Rect(a.x, y, 22f, 16f), (i + 1).ToString(CultureInfo.InvariantCulture), _styles.Muted);
                    GUI.Label(new Rect(a.x + 24f, y, a.width - 24f - ww - 8f, 16f), new GUIContent(s.text, s.text), _styles.Body);
                    GUI.Label(new Rect(a.xMax - ww, y + 1f, ww, 14f), s.who, _styles.SmallMuted);
                }
                y += 20f;
            }
            return y - a.y;
        }

        static Color StateColor(LaunchState s) => s switch
        {
            LaunchState.Go => FrogletEditorPalette.Families.Cta,
            LaunchState.AtRisk => FrogletEditorPalette.Families.Danger,
            LaunchState.NoGo => FrogletEditorPalette.Families.Danger,
            _ => FrogletEditorPalette.Muted,
        };

        /// <summary>The window's text styles, built from the palette's and rebuilt when the skin changes.</summary>
        sealed class Styles
        {
            bool _dark;
            bool _built;
            public GUIStyle Name, Body, BodyWrap, Muted, Small, SmallWrap, SmallMuted, SmallMutedRight, SmallDanger,
                SmallDangerRight, Danger, Cap, State, Chip, Verdict, HeaderWrap, PanelTitle, BigNumber, Button;

            public void Refresh()
            {
                if (_built && _dark == FrogletEditorPalette.IsDark) return;
                _built = true;
                _dark = FrogletEditorPalette.IsDark;
                var header = FrogletEditorPalette.HeaderText;
                var body = FrogletEditorPalette.BodyText;
                var muted = FrogletEditorPalette.Muted;
                var danger = FrogletEditorPalette.Families.Danger;

                Name = Make(FrogletEditorPalette.CardTitle, header, 12, false);
                Body = Make(EditorStyles.label, body, 11, false);
                BodyWrap = Make(EditorStyles.label, body, 11, true);
                Muted = Make(EditorStyles.label, muted, 11, false);
                Small = Make(EditorStyles.label, body, 10, false);
                SmallWrap = Make(EditorStyles.label, muted, 10, true);
                SmallMuted = Make(EditorStyles.label, muted, 10, false);
                SmallMutedRight = Make(EditorStyles.label, muted, 10, false);
                SmallMutedRight.alignment = TextAnchor.UpperRight;
                SmallDanger = Make(EditorStyles.boldLabel, danger, 10, false);
                SmallDangerRight = Make(EditorStyles.boldLabel, danger, 10, false);
                SmallDangerRight.alignment = TextAnchor.UpperRight;
                Danger = Make(EditorStyles.label, danger, 11, false);
                Cap = Make(EditorStyles.boldLabel, muted, 9, false);
                State = Make(EditorStyles.boldLabel, header, 10, false);
                Chip = Make(EditorStyles.label, header, 10, false);
                Chip.alignment = TextAnchor.MiddleCenter;
                Verdict = Make(EditorStyles.boldLabel, header, 15, false);
                HeaderWrap = Make(EditorStyles.boldLabel, header, 12, true);
                PanelTitle = Make(EditorStyles.boldLabel, header, 14, false);
                BigNumber = Make(EditorStyles.boldLabel, header, 26, false);
                BigNumber.alignment = TextAnchor.MiddleRight;
                Button = Make(EditorStyles.boldLabel, header, 11, false);
            }

            static GUIStyle Make(GUIStyle from, Color text, int size, bool wrap) => new GUIStyle(from)
            {
                fontSize = size,
                wordWrap = wrap,
                richText = false,
                clipping = TextClipping.Clip,
                alignment = TextAnchor.UpperLeft,
                normal = { textColor = text },
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
            };
        }
    }
}
