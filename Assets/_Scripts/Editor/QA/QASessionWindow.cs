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
            public string branch;         // what git has checked out right now
            public string sessionBranch;  // what the session form says is under test
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
        readonly Dictionary<string, string> _noteEdits = new Dictionary<string, string>();
        readonly Dictionary<string, bool> _instructionsOpen = new Dictionary<string, bool>();
        GUIStyle _bodyStyle, _passHeader, _failHeader, _okBody, _headline, _chip;

        GUIStyle Body => _bodyStyle ?? (_bodyStyle = new GUIStyle(EditorStyles.wordWrappedLabel));
        GUIStyle PassHeader => _passHeader ?? (_passHeader = Tinted(FrogletEditorPalette.Ok));
        GUIStyle FailHeader => _failHeader ?? (_failHeader = Tinted(FrogletEditorPalette.Error));
        GUIStyle OkBody => _okBody ?? (_okBody = TintedWrapped(FrogletEditorPalette.Ok));

        GUIStyle Headline
        {
            get
            {
                if (_headline == null)
                {
                    _headline = new GUIStyle(EditorStyles.boldLabel);
                    _headline.wordWrap = true;
                }
                return _headline;
            }
        }

        GUIStyle Chip
        {
            get
            {
                if (_chip == null)
                {
                    _chip = new GUIStyle(EditorStyles.miniBoldLabel);
                    _chip.alignment = TextAnchor.MiddleCenter;
                    _chip.normal.textColor = new Color(0.1f, 0.1f, 0.1f);
                }
                return _chip;
            }
        }

        static GUIStyle Tinted(Color c)
        {
            var s = new GUIStyle(EditorStyles.miniBoldLabel);
            s.normal.textColor = c;
            return s;
        }

        static GUIStyle TintedWrapped(Color c)
        {
            var s = new GUIStyle(EditorStyles.wordWrappedLabel);
            s.normal.textColor = c;
            return s;
        }

        /// <summary>
        /// A numbered step banner. The window reads as ONE PROCESS — session, build,
        /// editor setup, items, submit — and these are what separate the phases so a
        /// first-time tester always knows where in the process they are standing.
        /// </summary>
        void StepHeader(string number, string title)
        {
            EditorGUILayout.Space(14);
            var accent = FrogletEditorPalette.ColorFor(FrogletToolCategory.Qa);
            using (new EditorGUILayout.HorizontalScope())
            {
                var box = GUILayoutUtility.GetRect(20f, 20f, GUILayout.Width(20f));
                EditorGUI.DrawRect(box, accent);
                GUI.Label(box, number, Chip);
                EditorGUILayout.LabelField(title, FrogletEditorPalette.SectionHeader);
            }
            var line = GUILayoutUtility.GetRect(1f, 2f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(line, new Color(accent.r, accent.g, accent.b, 0.35f));
            EditorGUILayout.Space(4);
        }

        /// <summary>A faint horizontal rule between subsections of one panel.</summary>
        static void Rule()
        {
            EditorGUILayout.Space(4);
            var r = GUILayoutUtility.GetRect(1f, 1f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(r, new Color(0.5f, 0.5f, 0.5f, 0.25f));
            EditorGUILayout.Space(4);
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
                psi.EnvironmentVariables["PYTHONUTF8"] = "1";
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
            StepHeader("1", "Start your session");
            _newTester = EditorGUILayout.TextField("Your name", _newTester);
            EditorGUILayout.LabelField(
                "Creates Docs/QA/RESULTS/<today>-<yourname>.md with the build and Unity " +
                "version filled in. Steps 2 through 5 appear once it exists.",
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
            StepHeader("1", "Your session — " + _state.sessionFile);
            EditorGUILayout.LabelField(
                "Tester " + _state.tester + "   ·   " + _state.date + "   ·   Unity " +
                _state.unity + "   ·   " + _state.platform, EditorStyles.miniLabel);

            StepHeader("2", "Be on the right build");
            DrawBuildStep();

            if (!string.IsNullOrEmpty(_state.preconditions))
            {
                StepHeader("3", "Set up the Unity Editor — once per session");
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    EditorGUILayout.LabelField(_state.preconditions, Body);
            }

            StepHeader("4", "Run your items — read, do, judge, note");
            if (_state.rows.Length == 0)
                EditorGUILayout.HelpBox("No items in your session yet — pick one from " +
                    "the list below to see what it involves.", MessageType.Info);

            foreach (var row in _state.rows) DrawRow(row);

            EditorGUILayout.Space(8);
            DrawAddItem();

            foreach (var p in _state.problems)
            {
                if (p.where == "Commit") continue;  // step 2 renders this one in place
                EditorGUILayout.HelpBox(
                    p.where + ": " + p.what + (string.IsNullOrEmpty(p.fix) ? "" : "\n" + p.fix),
                    p.blocking ? MessageType.Error : MessageType.Warning);
            }
        }

        /// <summary>
        /// The live build check. Every backlog instruction that says "the branch under
        /// test" means the build this form names — an abstraction that confuses a
        /// beginner, so this step makes it concrete: here is that build, here is what
        /// your project is actually on, and here is exactly what to type if they differ.
        /// </summary>
        void DrawBuildStep()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(
                    "Anywhere an instruction says “the branch under test”, it means " +
                    "this exact build, which your form names:",
                    EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.LabelField(
                    "        " + _state.sessionBranch + "   at commit   " + _state.commit,
                    EditorStyles.boldLabel);

                var match = !string.IsNullOrEmpty(_state.commit) &&
                            _state.commit == _state.head;
                if (match)
                {
                    EditorGUILayout.LabelField(
                        "Your Unity project is on that exact commit. Nothing to do here — " +
                        "go to step 3.", OkBody);
                    return;
                }

                EditorGUILayout.LabelField(
                    "Your project is currently on " + _state.head + " — a DIFFERENT build. " +
                    "A verdict recorded against the wrong build sends engineering into the " +
                    "wrong code, so fix this before running anything:", FailBodyStyle());
                EditorGUILayout.LabelField(
                    "In a terminal at the repo root, run the three commands below (the Copy " +
                    "button puts them on your clipboard), then let Unity reimport.",
                    EditorStyles.wordWrappedMiniLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (FrogletEditorPalette.ColorButton("Copy the git commands",
                            FrogletEditorPalette.Info, 170f))
                        EditorGUIUtility.systemCopyBuffer =
                            "git fetch origin\n" +
                            "git checkout " + _state.sessionBranch + "\n" +
                            "git pull origin " + _state.sessionBranch;
                    if (FrogletEditorPalette.ColorButton(
                            "I tested what is checked out — record " + _state.head,
                            FrogletEditorPalette.Warn, 320f, 24f,
                            "Rewrites the form's Commit row to the build Unity currently " +
                            "has open. Only press this if that really is the build you ran " +
                            "the items on."))
                        Run("submit", "--accept-head");
                }
            }
        }

        GUIStyle FailBodyStyle() => _failBody ?? (_failBody = TintedWrapped(FrogletEditorPalette.Error));
        GUIStyle _failBody;

        // A row reads top-to-bottom in the order the work actually happens:
        // headline → the instructions → your verdict → your notes. The verdict
        // control deliberately sits BELOW the instructions, because judging comes
        // after doing.
        void DrawRow(Row row)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var item = ItemFor(row.id);
                using (new EditorGUILayout.HorizontalScope())
                {
                    var headline = row.id + (item == null ? "" : "  —  " + item.title);
                    EditorGUILayout.LabelField(headline, Headline);
                    if (item != null)
                        EditorGUILayout.LabelField(item.priority,
                            EditorStyles.miniBoldLabel, GUILayout.Width(24f));
                    if (!row.frozen &&
                        FrogletEditorPalette.ColorButton("×", FrogletEditorPalette.Error,
                            24f, 18f, "Remove this row from your session"))
                        Run("remove", "--item", row.id);
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
                if (next) DrawInstructions(item);

                if (row.frozen)
                {
                    EditorGUILayout.LabelField(
                        "Verdict: " + row.verdict + "  (published — frozen. A retest is a " +
                        "new session, never an edit.)", EditorStyles.miniBoldLabel);
                }
                else
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField("Your verdict",
                            EditorStyles.miniBoldLabel, GUILayout.Width(80f));
                        var options = BuildVerdictOptions();
                        var current = Mathf.Max(0, System.Array.IndexOf(options, row.verdict));
                        var picked = EditorGUILayout.Popup(current, options, GUILayout.Width(110f));
                        if (picked != current)
                            Run("set", "--item", row.id, "--verdict", options[picked]);

                        GUILayout.FlexibleSpace();
                        if (FrogletEditorPalette.ColorButton("Attach evidence",
                                FrogletEditorPalette.Info, 120f, 18f,
                                "Copy a screenshot/clip/log next to this session and " +
                                "reference it from this item's notes"))
                            Attach(row.id);
                        if (FrogletEditorPalette.ColorButton("Save Console log",
                                FrogletEditorPalette.Muted, 120f, 18f,
                                "Save the current Editor log as evidence for this item"))
                            AttachEditorLog(row.id);
                    }

                    EditorGUILayout.LabelField(
                        "Your notes — required unless PASS. Say which step number and " +
                        "exactly what you saw:", EditorStyles.wordWrappedMiniLabel);

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
            EditorGUILayout.Space(6);
        }

        string[] BuildVerdictOptions()
        {
            var list = new List<string> { "" };
            if (_state != null && _state.verdicts != null) list.AddRange(_state.verdicts);
            return list.ToArray();
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
                var first = true;
                if (!string.IsNullOrEmpty(item.context))
                {
                    first = false;
                    EditorGUILayout.LabelField("Why this is on the list", EditorStyles.miniBoldLabel);
                    EditorGUILayout.LabelField(item.context, Body);
                }
                if (!string.IsNullOrEmpty(item.steps))
                {
                    if (!first) Rule();
                    first = false;
                    EditorGUILayout.LabelField("What to do — in order", EditorStyles.miniBoldLabel);
                    EditorGUILayout.LabelField(item.steps, Body);
                }
                if (!string.IsNullOrEmpty(item.passWhen))
                {
                    if (!first) Rule();
                    first = false;
                    EditorGUILayout.LabelField("PASS when", PassHeader);
                    EditorGUILayout.LabelField(item.passWhen, Body);
                }
                if (!string.IsNullOrEmpty(item.failWhen))
                {
                    if (!first) Rule();
                    first = false;
                    EditorGUILayout.LabelField("FAIL when", FailHeader);
                    EditorGUILayout.LabelField(item.failWhen, Body);
                }
                if (!string.IsNullOrEmpty(item.known))
                {
                    if (!first) Rule();
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

        // Step 5 of the process, pinned to the bottom so it is always visible.
        // The build-mismatch remedy lives in step 2, next to its explanation.
        void DrawFooter()
        {
            EditorGUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("5 · Submit", EditorStyles.boldLabel,
                    GUILayout.Width(70f));
                if (_state.submitted)
                    EditorGUILayout.LabelField("Submitted — nothing new to publish. Keep " +
                        "adding items and submit again any time.", EditorStyles.miniLabel);
                else if (_state.canSubmit)
                    EditorGUILayout.LabelField("Everything checks out — press Submit to " +
                        "publish your verdicts.", EditorStyles.miniLabel);
                else
                    EditorGUILayout.LabelField(
                        _state.blocking + " problem(s) to fix first — each is explained in " +
                        "red next to what it belongs to.", EditorStyles.miniLabel);
                if (HasUnsavedNotes)
                    EditorGUILayout.LabelField("Unsaved notes.", EditorStyles.miniBoldLabel,
                        GUILayout.Width(90f));

                GUILayout.FlexibleSpace();

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
