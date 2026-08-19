using System.Collections.Generic;
using System.IO;
using System.Text;
using CosmicShore.Editor.Froglet;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor.QA
{
    /// <summary>
    /// The QA session form — fill in verdicts for backlog items and press Submit.
    ///
    /// This window owns PIXELS ONLY. It never parses or writes a results file and never
    /// re-implements a validation rule; every read and every write goes through
    /// <c>Tools/QA/session.py</c>, which self-tests. Two reasons that split is deliberate:
    /// the rules would silently drift if they existed twice, and a duplicate in here would
    /// be the copy nobody can prove correct. Anything this window gets wrong is a layout
    /// bug you can see, not a lost result.
    ///
    /// Requires python3 on PATH. If it is missing the window says so and offers the
    /// download page rather than half-working.
    /// </summary>
    public class QASessionWindow : EditorWindow
    {
        // ── Payload from `session.py state --json` ────────────────────────────────
        // Field names and shape are fixed by JsonUtility: no dictionaries, and every
        // key must be a valid C# identifier. session.py emits exactly this.

        [System.Serializable]
        public class BacklogItem
        {
            public string id;
            public string priority;
            public string status;
            public string title;
            public string context;   // why this item exists (Source / Why P0)
            public string steps;     // numbered, one per line — what the tester DOES
            public string passWhen;  // the PASS definition, verbatim from the backlog
            public string failWhen;  // the FAIL definition
            public string known;     // pre-existing defects that are NOT a failure
        }

        [System.Serializable]
        public class Row
        {
            public string id;
            public string verdict;
            public string notes;
            public bool frozen;
            public string problem;
            public bool problemBlocking;
        }

        [System.Serializable]
        public class Problem
        {
            public bool blocking;
            public string where;
            public string what;
            public string fix;
        }

        [System.Serializable]
        public class SessionState
        {
            public string root;
            public string head;
            public string branch;
            public string preconditions;
            public bool hasSession;
            public string sessionFile;
            public string sessionPath;
            public string tester;
            public string date;
            public string commit;
            public string unity;
            public string platform;
            public bool submitted;
            public bool canSubmit;
            public int blocking;
            public BacklogItem[] backlog = new BacklogItem[0];
            public Row[] rows = new Row[0];
            public Problem[] problems = new Problem[0];
            public string[] sessions = new string[0];
            public string[] verdicts = new string[0];
        }

        const string DownloadUrl = "https://www.python.org/downloads/";

        SessionState _state;
        string _python;
        string _pythonPrefix;
        string _pythonVersion;
        string _lastError;
        string _newTester = "";
        Vector2 _scroll;
        int _addIndex;
        bool _busy;
        bool _preconditionsOpen = true;
        readonly Dictionary<string, string> _noteEdits = new Dictionary<string, string>();
        readonly Dictionary<string, bool> _instructionsOpen = new Dictionary<string, bool>();
        GUIStyle _bodyStyle, _passHeader, _failHeader;

        GUIStyle Body => _bodyStyle ?? (_bodyStyle = new GUIStyle(EditorStyles.wordWrappedLabel));
        GUIStyle PassHeader => _passHeader ?? (_passHeader = Tinted(FrogletEditorPalette.Ok));
        GUIStyle FailHeader => _failHeader ?? (_failHeader = Tinted(FrogletEditorPalette.Error));

        static GUIStyle Tinted(Color c)
        {
            var s = new GUIStyle(EditorStyles.miniBoldLabel);
            s.normal.textColor = c;
            return s;
        }

        bool HasUnsavedNotes
        {
            get
            {
                if (_state == null) return false;
                foreach (var row in _state.rows)
                {
                    if (_noteEdits.TryGetValue(row.id, out var pending) &&
                        pending != (row.notes ?? "")) return true;
                }
                return false;
            }
        }

        [MenuItem("FrogletTools/QA/QA Session", false, 10)]
        [FrogletTool(FrogletToolCategory.Qa, Importance = 5,
            DisplayName = "QA Session",
            Description = "Record verdicts for backlog items and submit a QA session.")]
        public static void Open()
        {
            var w = GetWindow<QASessionWindow>("QA Session");
            w.minSize = new Vector2(620f, 420f);
            w.Refresh();
            w.Show();
        }

        // ── Talking to Python ─────────────────────────────────────────────────────

        static string RepoRoot
        {
            get
            {
                var parent = Directory.GetParent(Application.dataPath);
                return parent != null ? parent.FullName : Application.dataPath;
            }
        }

        /// <summary>
        /// First interpreter on PATH that is REALLY Python 3, or null.
        ///
        /// "Did the process start" is not good enough on Windows: it ships stub
        /// python.exe / python3.exe App Execution Aliases in WindowsApps that launch
        /// happily and only print "Python was not found; run without arguments to
        /// install from the Microsoft Store". So a candidate must exit 0 AND identify
        /// itself as Python 3 before we believe it.
        /// </summary>
        static bool FindPython(out string exe, out string prefix, out string version)
        {
            exe = prefix = version = null;
            foreach (var candidate in new[] { "python3|", "python|", "py|-3" })
            {
                var parts = candidate.Split('|');
                var args = (parts[1] + " --version").Trim();
                if (!TryRun(parts[0], args, out var so, out var se, out var code)) continue;
                if (code != 0) continue;

                var reported = (so + " " + se).Trim();
                if (!reported.StartsWith("Python 3.")) continue;

                exe = parts[0];
                prefix = parts[1];
                version = reported.Split('\n')[0].Trim();
                return true;
            }
            return false;
        }

        static bool TryRun(string exe, string args, out string stdout, out string stderr,
                           out int exitCode)
        {
            stdout = stderr = "";
            exitCode = -1;
            try
            {
                // Fully qualified: `using System.Diagnostics` would make `Debug` ambiguous
                // against UnityEngine.Debug in this file.
                var psi = new System.Diagnostics.ProcessStartInfo(exe, args)
                {
                    WorkingDirectory = RepoRoot,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    // Both ends of the pipe must agree on UTF-8. Left to defaults,
                    // Python on Windows emits the ANSI codepage into a redirected
                    // pipe and .NET reads it as the console codepage — the backlog's
                    // em-dashes and status glyphs arrive as mojibake ("â€œ â€").
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                };
                psi.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
                using (var p = System.Diagnostics.Process.Start(psi))
                {
                    if (p == null) return false;

                    // Drain stderr on its own thread. Reading both streams to end in
                    // sequence deadlocks if the one you are NOT reading fills its pipe
                    // buffer (4 KB on Windows) — a Python traceback can do that.
                    var err = new StringBuilder();
                    p.ErrorDataReceived += (_, e) =>
                    {
                        if (e.Data != null) lock (err) err.AppendLine(e.Data);
                    };
                    p.BeginErrorReadLine();

                    stdout = p.StandardOutput.ReadToEnd();
                    p.WaitForExit(60000);
                    lock (err) stderr = err.ToString();
                    exitCode = p.HasExited ? p.ExitCode : -1;
                    return true;
                }
            }
            catch (System.Exception e)
            {
                stderr = e.Message;
                return false;
            }
        }

        /// <summary>Run a session.py command and re-read state from its JSON.</summary>
        void Run(params string[] args)
        {
            if (string.IsNullOrEmpty(_python)) return;
            _busy = true;
            try
            {
var sb = new StringBuilder();
                if (!string.IsNullOrEmpty(_pythonPrefix)) sb.Append(_pythonPrefix).Append(' ');
                sb.Append("Tools/QA/session.py");
                foreach (var a in args) sb.Append(' ').Append(Quote(a));
                sb.Append(" --json");

                if (!TryRun(_python, sb.ToString(), out var stdout, out var stderr, out _))
                {
                    _lastError = "Could not run " + _python + ": " + stderr;
                    return;
                }

                var brace = stdout.IndexOf('{');
                if (brace < 0)
                {
                    _lastError = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
                    return;
                }

                _lastError = null;
                var parsed = JsonUtility.FromJson<SessionState>(stdout.Substring(brace));
                if (parsed != null)
                {
                    _state = parsed;
                    PruneNoteEdits();
                }
            }
            finally
            {
                _busy = false;
                Repaint();
            }
        }

        /// <summary>Drop pending edits that the file now agrees with.</summary>
        void PruneNoteEdits()
        {
            var stale = new List<string>();
            foreach (var pair in _noteEdits)
            {
                var row = System.Array.Find(_state.rows, r => r.id == pair.Key);
                if (row == null || (row.notes ?? "") == pair.Value) stale.Add(pair.Key);
            }
            foreach (var key in stale) _noteEdits.Remove(key);
        }

        static string Quote(string s) => "\"" + (s ?? "").Replace("\"", "\\\"") + "\"";

        void Refresh()
        {
            if (!FindPython(out _python, out _pythonPrefix, out _pythonVersion))
            {
                _python = null;
                return;
            }
            Run("state");
        }

        // ── Drawing ───────────────────────────────────────────────────────────────

        void OnGUI()
        {
            FrogletEditorPalette.Banner(
                "QA Session",
                "Record verdicts, then Submit. Nothing is published until you do.",
                FrogletEditorPalette.ColorFor(FrogletToolCategory.Qa));

            if (string.IsNullOrEmpty(_python)) { DrawNoPython(); return; }

            using (new EditorGUI.DisabledScope(_busy))
            {
                _scroll = EditorGUILayout.BeginScrollView(_scroll);
                DrawHeader();
                if (_state == null)
                    EditorGUILayout.HelpBox("No state yet — press Refresh.", MessageType.Info);
                else if (!_state.hasSession) DrawNoSession();
                else DrawSession();
                EditorGUILayout.EndScrollView();
                if (_state != null && _state.hasSession) DrawFooter();
            }
        }

        void DrawNoPython()
        {
            var windows = Application.platform == RuntimePlatform.WindowsEditor;
            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox(
                "This window needs Python 3, and no real one was found on PATH.\n\n" +
                "The project already uses Python for its build checks, so this is the " +
                "same dependency — it just is not installed on this machine yet.",
                MessageType.Error);

            if (windows)
            {
                EditorGUILayout.HelpBox(
                    "On Windows there is a second trap. Windows ships stub python.exe / " +
                    "python3.exe \"App Execution Aliases\" that only advertise the " +
                    "Microsoft Store — if you saw \"Python was not found; run without " +
                    "arguments to install from the Microsoft Store\", that was the stub, " +
                    "not Python.\n\n" +
                    "1. Install Python 3 (tick \"Add python.exe to PATH\" in the installer).\n" +
                    "2. Settings ▸ Apps ▸ Advanced app settings ▸ App execution aliases — " +
                    "turn OFF python.exe and python3.exe.\n" +
                    "3. RESTART UNITY. A running process keeps the PATH it started with, " +
                    "so Unity cannot see a newly installed Python until it restarts.",
                    MessageType.Info);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (FrogletEditorPalette.ColorButton("Open python.org downloads",
                        FrogletEditorPalette.Info, 210f))
                    Application.OpenURL(DownloadUrl);
                if (FrogletEditorPalette.ColorButton("Copy install command",
                        FrogletEditorPalette.Muted, 170f))
                    EditorGUIUtility.systemCopyBuffer = windows
                        ? "winget install Python.Python.3.12"
                        : "brew install python";
                if (windows && FrogletEditorPalette.ColorButton("Open alias settings",
                        FrogletEditorPalette.Muted, 150f))
                    Application.OpenURL("ms-settings:advanced-apps");
                if (FrogletEditorPalette.ColorButton("Recheck", FrogletEditorPalette.Ok, 90f))
                    Refresh();
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(
                "You can always fall back to editing the results markdown by hand — see " +
                "Docs/QA/README.md.", EditorStyles.wordWrappedMiniLabel);
        }

        void DrawHeader()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var build = _state == null ? "?" : _state.branch + " @ " + _state.head;
                EditorGUILayout.LabelField("Build  " + build, EditorStyles.miniLabel,
                    GUILayout.MinWidth(260f));
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField(_pythonVersion ?? "", EditorStyles.miniLabel,
                    GUILayout.Width(110f));
                if (FrogletEditorPalette.ColorButton("Refresh", FrogletEditorPalette.Info, 80f))
                    Refresh();
            }
            if (!string.IsNullOrEmpty(_lastError))
                EditorGUILayout.HelpBox(_lastError, MessageType.Error);
            EditorGUILayout.Space(4);
        }

        void DrawNoSession()
        {
            EditorGUILayout.LabelField("Start a session", FrogletEditorPalette.SectionHeader);
            _newTester = EditorGUILayout.TextField("Your name", _newTester);
            EditorGUILayout.LabelField(
                "Creates Docs/QA/RESULTS/<today>-<yourname>.md with the build and Unity " +
                "version filled in. Add items to it below once it exists.",
                EditorStyles.wordWrappedMiniLabel);
            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_newTester)))
            {
                if (FrogletEditorPalette.ColorButton("Create session",
                        FrogletEditorPalette.Ok, 140f))
                    Run("new", "--tester", _newTester,
                        "--unity", Application.unityVersion,
                        "--platform", "Editor (" + Application.platform + ")");
            }
        }

        void DrawSession()
        {
            EditorGUILayout.LabelField(_state.sessionFile, FrogletEditorPalette.SectionHeader);
            EditorGUILayout.LabelField(
                "Tester " + _state.tester + "   ·   " + _state.date + "   ·   Unity " +
                _state.unity + "   ·   " + _state.platform, EditorStyles.miniLabel);
            EditorGUILayout.Space(6);

            if (!string.IsNullOrEmpty(_state.preconditions))
            {
                _preconditionsOpen = EditorGUILayout.Foldout(_preconditionsOpen,
                    "Before you start — once per session", true);
                if (_preconditionsOpen)
                {
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                        EditorGUILayout.LabelField(_state.preconditions, Body);
                }
                EditorGUILayout.Space(4);
            }

            if (_state.rows.Length == 0)
                EditorGUILayout.HelpBox("No items yet — add one below.", MessageType.Info);

            foreach (var row in _state.rows) DrawRow(row);

            EditorGUILayout.Space(8);
            DrawAddItem();

            foreach (var p in _state.problems)
            {
                EditorGUILayout.HelpBox(
                    p.where + ": " + p.what + (string.IsNullOrEmpty(p.fix) ? "" : "\n" + p.fix),
                    p.blocking ? MessageType.Error : MessageType.Warning);
            }
        }

        void DrawRow(Row row)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(new GUIContent(row.id, TitleFor(row.id)),
                        EditorStyles.boldLabel, GUILayout.Width(230f));

                    if (row.frozen)
                    {
                        EditorGUILayout.LabelField(
                            row.verdict + "  (published — frozen)",
                            EditorStyles.miniLabel);
                    }
                    else
                    {
                        var options = BuildVerdictOptions();
                        var current = Mathf.Max(0, System.Array.IndexOf(options, row.verdict));
                        var picked = EditorGUILayout.Popup(current, options, GUILayout.Width(110f));
                        if (picked != current)
                            Run("set", "--item", row.id, "--verdict", options[picked]);

                        if (FrogletEditorPalette.ColorButton("Attach",
                                FrogletEditorPalette.Info, 70f, 18f))
                            Attach(row.id);
                        if (FrogletEditorPalette.ColorButton("Console",
                                FrogletEditorPalette.Muted, 74f, 18f,
                                "Save the current Editor log as evidence for this item"))
                            AttachEditorLog(row.id);
                        if (FrogletEditorPalette.ColorButton("×",
                                FrogletEditorPalette.Error, 24f, 18f, "Remove this row"))
                            Run("remove", "--item", row.id);
                    }
                }

                // The item's instructions live INSIDE its row: open by default until a
                // verdict is recorded (you are presumably still running it), folded away
                // once one is — but always one click from coming back.
                var open = _instructionsOpen.TryGetValue(row.id, out var o)
                    ? o
                    : !row.frozen && string.IsNullOrEmpty((row.verdict ?? "").Trim());
                var next = EditorGUILayout.Foldout(open,
                    "What to check — steps and PASS/FAIL", true);
                if (next != open) _instructionsOpen[row.id] = next;
                if (next) DrawInstructions(ItemFor(row.id));

                if (!row.frozen)
                {
                    // Notes commit on an explicit press rather than on focus loss. Focus
                    // tracking in IMGUI is subtle, and this window is the half that cannot
                    // be compile-tested — a visible button cannot silently drop a note.
                    var saved = row.notes ?? "";
                    var edited = _noteEdits.TryGetValue(row.id, out var pending) ? pending : saved;
                    var typed = EditorGUILayout.TextArea(edited, EditorStyles.textArea,
                        GUILayout.MinHeight(34f));
                    if (typed != edited) _noteEdits[row.id] = typed;

                    if (typed != saved)
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField("Unsaved note",
                                EditorStyles.miniBoldLabel, GUILayout.Width(90f));
                            if (FrogletEditorPalette.ColorButton("Save note",
                                    FrogletEditorPalette.Ok, 90f, 18f))
                            {
                                _noteEdits.Remove(row.id);
                                Run("set", "--item", row.id, "--notes", typed);
                            }
                            if (FrogletEditorPalette.ColorButton("Revert",
                                    FrogletEditorPalette.Muted, 70f, 18f))
                            {
                                _noteEdits.Remove(row.id);
                                GUI.FocusControl(null);
                            }
                        }
                    }
                }

                if (!string.IsNullOrEmpty(row.problem))
                {
                    EditorGUILayout.HelpBox(row.problem,
                        row.problemBlocking ? MessageType.Error : MessageType.Warning);
                }
            }
        }

        string[] BuildVerdictOptions()
        {
            var list = new List<string> { "" };
            if (_state != null && _state.verdicts != null) list.AddRange(_state.verdicts);
            return list.ToArray();
        }

        string TitleFor(string id)
        {
            var b = ItemFor(id);
            return b == null ? id : b.priority + " — " + b.title;
        }

        BacklogItem ItemFor(string id)
        {
            if (_state?.backlog == null) return null;
            foreach (var b in _state.backlog)
                if (b.id == id) return b;
            return null;
        }

        /// <summary>
        /// The item's full tester-facing instructions — context, numbered steps, the
        /// PASS/FAIL definitions and known exceptions — parsed out of QA_BACKLOG.md by
        /// session.py. This panel is what lets someone run an item without ever opening
        /// the backlog file: everything they need to DO and to JUDGE is on screen.
        /// </summary>
        void DrawInstructions(BacklogItem item)
        {
            if (item == null) return;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (!string.IsNullOrEmpty(item.context))
                {
                    EditorGUILayout.LabelField("Why this is on the list", EditorStyles.miniBoldLabel);
                    EditorGUILayout.LabelField(item.context, Body);
                    EditorGUILayout.Space(4);
                }
                if (!string.IsNullOrEmpty(item.steps))
                {
                    EditorGUILayout.LabelField("What to do — in order", EditorStyles.miniBoldLabel);
                    EditorGUILayout.LabelField(item.steps, Body);
                    EditorGUILayout.Space(4);
                }
                if (!string.IsNullOrEmpty(item.passWhen))
                {
                    EditorGUILayout.LabelField("PASS when", PassHeader);
                    EditorGUILayout.LabelField(item.passWhen, Body);
                    EditorGUILayout.Space(4);
                }
                if (!string.IsNullOrEmpty(item.failWhen))
                {
                    EditorGUILayout.LabelField("FAIL when", FailHeader);
                    EditorGUILayout.LabelField(item.failWhen, Body);
                    EditorGUILayout.Space(4);
                }
                if (!string.IsNullOrEmpty(item.known))
                {
                    EditorGUILayout.LabelField("Known already — do NOT fail on these",
                        EditorStyles.miniBoldLabel);
                    EditorGUILayout.LabelField(item.known, Body);
                }
                if (string.IsNullOrEmpty(item.steps) && string.IsNullOrEmpty(item.passWhen))
                    EditorGUILayout.LabelField(
                        "This item's instructions could not be read out of QA_BACKLOG.md — " +
                        "open Docs/QA/QA_BACKLOG.md and find " + item.id + " for the steps.",
                        EditorStyles.wordWrappedMiniLabel);
            }
        }

        void DrawAddItem()
        {
            var open = new List<string> { "Add an item…" };
            var ids = new List<string> { null };
            var present = new HashSet<string>();
            foreach (var r in _state.rows) present.Add(r.id);
            foreach (var b in _state.backlog)
            {
                if (present.Contains(b.id)) continue;
                open.Add(b.priority + "  " + b.id + " — " + b.title);
                ids.Add(b.id);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                _addIndex = EditorGUILayout.Popup(_addIndex, open.ToArray());
                if (FrogletEditorPalette.ColorButton("Add", FrogletEditorPalette.Ok, 60f) &&
                    _addIndex > 0 && _addIndex < ids.Count)
                {
                    Run("set", "--item", ids[_addIndex], "--verdict", "");
                    _addIndex = 0;
                }
            }

            // Show the selected item's instructions BEFORE it is added, so picking an
            // item is an informed choice, not a leap.
            if (_addIndex > 0 && _addIndex < ids.Count)
            {
                EditorGUILayout.LabelField(
                    "Read what this involves, then press Add to put it in your session:",
                    EditorStyles.wordWrappedMiniLabel);
                DrawInstructions(ItemFor(ids[_addIndex]));
            }
        }

        void Attach(string itemId)
        {
            var picked = EditorUtility.OpenFilePanel("Attach evidence for " + itemId, "", "");
            if (!string.IsNullOrEmpty(picked)) Run("attach", "--item", itemId, "--src", picked);
        }

        /// <summary>Copy the Editor log next to the session so a FAIL carries its console.</summary>
        void AttachEditorLog(string itemId)
        {
            var log = EditorLogPath();
            if (string.IsNullOrEmpty(log) || !File.Exists(log))
            {
                _lastError = "Could not find the Editor log at " + (log ?? "(unknown path)") +
                             ". Attach a screenshot instead.";
                return;
            }
            var tmp = Path.Combine(Path.GetTempPath(), itemId + "-Editor.log");
            try
            {
                // The live log is held open by the Editor — copy through a shared read.
                using (var src = new FileStream(log, FileMode.Open, FileAccess.Read,
                                                FileShare.ReadWrite))
                using (var dst = new FileStream(tmp, FileMode.Create, FileAccess.Write))
                    src.CopyTo(dst);
                Run("attach", "--item", itemId, "--src", tmp);
            }
            catch (System.Exception e)
            {
                _lastError = "Could not copy the Editor log: " + e.Message;
            }
        }

        static string EditorLogPath()
        {
            var home = System.Environment.GetFolderPath(
                System.Environment.SpecialFolder.UserProfile);
            switch (Application.platform)
            {
                case RuntimePlatform.WindowsEditor:
                    var local = System.Environment.GetFolderPath(
                        System.Environment.SpecialFolder.LocalApplicationData);
                    return Path.Combine(local, "Unity", "Editor", "Editor.log");
                case RuntimePlatform.OSXEditor:
                    return Path.Combine(home, "Library", "Logs", "Unity", "Editor.log");
                default:
                    return Path.Combine(home, ".config", "unity3d", "Editor.log");
            }
        }

        void DrawFooter()
        {
            EditorGUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                if (_state.submitted)
                    EditorGUILayout.LabelField("Submitted — nothing new to publish.",
                        EditorStyles.miniLabel);
                else if (_state.canSubmit)
                    EditorGUILayout.LabelField("Ready to submit.", EditorStyles.miniLabel);
                else
                    EditorGUILayout.LabelField(
                        _state.blocking + " problem(s) to fix before you can submit.",
                        EditorStyles.miniLabel);
                if (HasUnsavedNotes)
                    EditorGUILayout.LabelField("Unsaved notes.", EditorStyles.miniBoldLabel,
                        GUILayout.Width(90f));

                GUILayout.FlexibleSpace();

                if (!string.IsNullOrEmpty(_state.commit) &&
                    !string.IsNullOrEmpty(_state.head) &&
                    _state.commit != _state.head)
                {
                    if (FrogletEditorPalette.ColorButton("Use checked-out build",
                            FrogletEditorPalette.Warn, 170f, 24f,
                            "Record " + _state.head + " as the build you tested. Only if true."))
                        Run("submit", "--accept-head");
                }

                using (new EditorGUI.DisabledScope(!_state.canSubmit || HasUnsavedNotes))
                {
                    if (FrogletEditorPalette.ColorButton("Submit session",
                            FrogletEditorPalette.Ok, 150f, 24f,
                            HasUnsavedNotes ? "Save your edited notes first" : null))
                        Run("submit");
                }
            }
        }
    }
}
