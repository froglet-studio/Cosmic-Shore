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
        readonly Dictionary<string, bool> _helpOpen = new Dictionary<string, bool>();
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
        ///
        /// <paramref name="help"/> is the section's explanation. It is NOT drawn by
        /// default: a wall of prose reads as work before any has been done, so the
        /// reasoning hides behind the "?" and the step keeps only what to DO. The
        /// text also rides the button's tooltip, so hovering reveals it without a click.
        /// </summary>
        void StepHeader(string number, string title, string help = null)
        {
            EditorGUILayout.Space(14);
            var accent = FrogletEditorPalette.ColorFor(FrogletToolCategory.Qa);
            var open = help != null && _helpOpen.TryGetValue(number, out var h) && h;
            using (new EditorGUILayout.HorizontalScope())
            {
                var box = GUILayoutUtility.GetRect(20f, 20f, GUILayout.Width(20f));
                EditorGUI.DrawRect(box, accent);
                GUI.Label(box, number, Chip);
                EditorGUILayout.LabelField(title, FrogletEditorPalette.SectionHeader);
                if (help != null)
                {
                    GUILayout.FlexibleSpace();
                    if (FrogletEditorPalette.ColorButton(open ? "×" : "?",
                            FrogletEditorPalette.Muted, 22f, 18f, help))
                        _helpOpen[number] = !open;
                }
            }
            var line = GUILayoutUtility.GetRect(1f, 2f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(line, new Color(accent.r, accent.g, accent.b, 0.35f));
            EditorGUILayout.Space(4);
            if (open)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    EditorGUILayout.LabelField(help, Body);
                EditorGUILayout.Space(2);
            }
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
            StepHeader("1", "Your session — " + _state.sessionFile,
                "This is your results file. It lives in Docs/QA/RESULTS/ and it is " +
                "yours alone — nothing in it reaches anyone else until you press " +
                "Submit in step 5. You can leave it half-finished for days and come " +
                "back to it; the window reopens whatever you were last working on.");
            EditorGUILayout.LabelField(
                "Tester " + _state.tester + "   ·   " + _state.date + "   ·   Unity " +
                _state.unity + "   ·   " + _state.platform, EditorStyles.miniLabel);

            StepHeader("2", "Check you have the right version of the game",
                "Different versions of the game are called \"builds\". Each one has a " +
                "short code — a \"commit\" — like a receipt number, and the tests you " +
                "are about to run were written against one specific build.\n\n" +
                "Test the wrong build and your results point engineering at the wrong " +
                "code, which costs more time than not testing at all. When an " +
                "instruction later says \"the branch under test\", it means the build " +
                "named in this step.");
            DrawBuildStep();

            if (!string.IsNullOrEmpty(_state.preconditions))
            {
                StepHeader("3", "Set up Unity — do this once, at the start",
                    "These three settings stop the two most common false alarms: " +
                    "judging the game before Unity has finished loading the new files, " +
                    "and losing the error messages you are supposed to be reading " +
                    "because the game cleared or paused on them.");
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    EditorGUILayout.LabelField(_state.preconditions, Body);
            }

            StepHeader("4", "Run your tests",
                "Each box below is one test. Work through it top to bottom: read what " +
                "to check, go and do it in the game, come back and pick your verdict, " +
                "then write down what you saw.\n\n" +
                "Some words you will meet in the instructions:\n" +
                "•  Console — Unity's message window (Window ▸ General ▸ Console). It " +
                "lists errors as the game runs.\n" +
                "•  Freestyle — flying the ship yourself from the main menu: click the " +
                "centre of the screen, or press Y on a gamepad.\n" +
                "•  Reimport — Unity re-reading the game's files. Assets ▸ Reimport All " +
                "forces it, and it can take a while.\n" +
                "•  MPPM — a Unity feature that runs several copies of the game at once " +
                "so you can test multiplayer alone.\n\n" +
                "You do not have to run every test, or run them in one sitting. If you " +
                "cannot judge one, that is a real answer — mark it BLOCKED and say why.");
            if (_state.rows.Length == 0)
                EditorGUILayout.HelpBox("No tests picked yet — choose one from the " +
                    "list at the bottom to see what it involves.", MessageType.Info);

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
        /// The live build check, made concrete. "The branch under test" is an
        /// abstraction that loses a beginner, so this step names the build, says what
        /// the project is actually on, and — when they differ — gives the fix as
        /// GitHub Desktop clicks (no typing) with the typed commands as a fallback.
        /// </summary>
        void DrawBuildStep()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var match = !string.IsNullOrEmpty(_state.commit) &&
                            _state.commit == _state.head;

                EditorGUILayout.LabelField("The build these tests were written for:",
                    EditorStyles.miniBoldLabel);
                EditorGUILayout.LabelField("      " + _state.sessionBranch +
                    "      commit " + _state.commit, EditorStyles.boldLabel);
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("The build your Unity project has open:",
                    EditorStyles.miniBoldLabel);
                EditorGUILayout.LabelField("      " + _state.branch +
                    "      commit " + _state.head, EditorStyles.boldLabel);

                if (match)
                {
                    Rule();
                    EditorGUILayout.LabelField("These match. Nothing to do here — " +
                        "go on to step 3.", OkBody);
                    return;
                }

                Rule();
                EditorGUILayout.LabelField(
                    "These do not match, so you are about to test the wrong version of " +
                    "the game. Switch to the right one before you run anything:",
                    FailBodyStyle());
                EditorGUILayout.Space(4);

                EditorGUILayout.LabelField("In GitHub Desktop:", EditorStyles.miniBoldLabel);
                EditorGUILayout.LabelField(
                    "1.  Click  Fetch origin  (top bar).\n" +
                    "2.  Click  Current Branch  (top bar) and choose  " +
                    _state.sessionBranch + "\n" +
                    "3.  Click  Pull origin  (top bar). If it is not offered, you are " +
                    "already up to date.", Body);
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField(
                    "Then switch back to Unity, wait for it to finish importing, and " +
                    "press Refresh at the top of this window.", Body);

                Rule();
                EditorGUILayout.LabelField("Prefer to type it? These are the same three " +
                    "steps as commands:", EditorStyles.miniBoldLabel);
                // Selectable so it can be read and copied by hand as well as by button —
                // and shown literally, because "run the commands below" must have the
                // commands directly below it.
                EditorGUILayout.SelectableLabel(GitCommands(), EditorStyles.textArea,
                    GUILayout.Height(52f));
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (FrogletEditorPalette.ColorButton("Copy these commands",
                            FrogletEditorPalette.Info, 160f))
                        EditorGUIUtility.systemCopyBuffer = GitCommands();
                    GUILayout.FlexibleSpace();
                    if (FrogletEditorPalette.ColorButton(
                            "I already tested this version — use " + _state.head,
                            FrogletEditorPalette.Warn, 300f, 24f,
                            "Only press this if the build Unity has open right now IS " +
                            "the one you ran the tests on. It rewrites your form to say " +
                            "so — it does not change any code."))
                        Run("submit", "--accept-head");
                }
            }
        }

        string GitCommands() =>
            "git fetch origin\n" +
            "git checkout " + _state.sessionBranch + "\n" +
            "git pull origin " + _state.sessionBranch;

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
                var whyKey = "why:" + row.id;
                var why = _helpOpen.TryGetValue(whyKey, out var w) && w;
                using (new EditorGUILayout.HorizontalScope())
                {
                    var next = EditorGUILayout.Foldout(open,
                        "What to check — steps and PASS/FAIL", true);
                    if (next != open) _instructionsOpen[row.id] = next;
                    open = next;
                    if (open && item != null && !string.IsNullOrEmpty(item.context))
                    {
                        GUILayout.FlexibleSpace();
                        if (FrogletEditorPalette.ColorButton(why ? "×" : "?",
                                FrogletEditorPalette.Muted, 22f, 16f,
                                "Why this test exists:\n\n" + item.context))
                            _helpOpen[whyKey] = !why;
                    }
                }
                if (open) DrawInstructions(item, why);

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
                        "What you saw — needed for anything except PASS. Name the step " +
                        "number, and copy any error message in full:",
                        EditorStyles.wordWrappedMiniLabel);

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
        void DrawInstructions(BacklogItem item, bool showWhy = true)
        {
            if (item == null) return;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var first = true;
                if (showWhy && !string.IsNullOrEmpty(item.context))
                {
                    first = false;
                    EditorGUILayout.LabelField("Why this test exists", EditorStyles.miniBoldLabel);
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
                    "Here is what that one involves. Press Add to put it in your list:",
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
                EditorGUILayout.LabelField(new GUIContent("5 · Submit",
                        "Submit hands your finished verdicts to engineering. It checks " +
                        "your form first and refuses if anything is missing, telling you " +
                        "exactly what to fix — so pressing it can never lose your work.\n\n" +
                        "You can submit as often as you like; each time, only the tests " +
                        "you have newly finished are sent. Once a verdict is sent it is " +
                        "locked: if that test gets fixed and you run it again later, that " +
                        "is a fresh session, not an edit to this one."),
                    EditorStyles.boldLabel, GUILayout.Width(70f));
                if (_state.submitted)
                    EditorGUILayout.LabelField("Sent. Keep testing and press Submit again " +
                        "whenever you finish more.", EditorStyles.miniLabel);
                else if (_state.canSubmit)
                    EditorGUILayout.LabelField("Everything checks out — press Submit to " +
                        "send your results.", EditorStyles.miniLabel);
                else
                    EditorGUILayout.LabelField(
                        _state.blocking + " thing(s) to fix first — each one is explained " +
                        "in red where it belongs, above.", EditorStyles.miniLabel);
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
