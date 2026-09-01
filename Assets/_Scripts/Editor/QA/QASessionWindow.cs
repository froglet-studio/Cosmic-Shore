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
    /// WRITES NOTHING UNDER Assets/ — no ship panel (Docs/TOOLING.md §6). Its output is
    /// the tester's own session file under <c>Docs/QA/RESULTS/</c> plus the ledger, which
    /// are ordinary committable project data, exactly like the Bug Ledger's store. The
    /// python tools own every one of those writes.
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

            /// <summary>
            /// "" when nobody has reported this yet; otherwise e.g. "PASS by Caleb
            /// (2026-08-14)" or "already passed". Derived by session.py from submitted
            /// sessions + the archive, because QA_BACKLOG.md is stale between central
            /// apply_results runs and would otherwise hand out finished work again.
            /// </summary>
            public string answeredBy;
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
        string _activeSession = "";
        bool _startingNew;
        Vector2 _scroll;
        int _addIndex;
        bool _busy;
        readonly Dictionary<string, string> _noteEdits = new Dictionary<string, string>();
        readonly Dictionary<string, bool> _instructionsOpen = new Dictionary<string, bool>();
        readonly Dictionary<string, bool> _helpOpen = new Dictionary<string, bool>();

        /// <summary>Set by a button, run after the GUI pass — mutating mid-layout throws.</summary>
        System.Action _deferred;

        static Color Accent => FrogletEditorPalette.ColorFor(FrogletToolCategory.Qa);

        // Every style and colour comes from FrogletEditorPalette (Docs/TOOLING.md §3).
        static GUIStyle Body => FrogletEditorPalette.CardBodyWrapped;

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
            EditorGUILayout.Space(12);
            var open = help != null && _helpOpen.TryGetValue(number, out var h) && h;
            using (new EditorGUILayout.HorizontalScope())
            {
                var pill = GUILayoutUtility.GetRect(22f, 18f, GUILayout.Width(22f));
                FrogletEditorPalette.StatusPill(pill, number, Accent);
                GUILayout.Space(6f);
                // ExpandWidth, not FlexibleSpace: a LabelField sized by the layout's
                // leftovers gets squeezed by anything after it, which silently CLIPPED
                // every header ("…do this once, at the s"). Let the title take the row
                // and the button keep its fixed width.
                EditorGUILayout.LabelField(title, FrogletEditorPalette.SectionHeader,
                    GUILayout.ExpandWidth(true));
                if (help != null &&
                    FrogletEditorPalette.ColorButton(open ? "×" : "?",
                        FrogletEditorPalette.Muted, 22f, 18f, help, outline: true))
                    _helpOpen[number] = !open;
            }
            var line = GUILayoutUtility.GetRect(0f, 2f, GUILayout.ExpandWidth(true));
            FrogletEditorPalette.DrawRect(line, Accent.WithAlpha(0.35f));
            EditorGUILayout.Space(4);
            if (open)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    EditorGUILayout.LabelField(help, Body);
                EditorGUILayout.Space(2);
            }
        }

        /// <summary>A palette pill used as a labelled subsection heading.</summary>
        static void PillHeader(string label, Color accent, float width = 88f)
        {
            var r = GUILayoutUtility.GetRect(width, 16f, GUILayout.Width(width));
            FrogletEditorPalette.StatusPill(r, label, accent);
        }

        /// <summary>
        /// Queue a state change for after the GUI pass (Docs/TOOLING.md §4). Every
        /// button here re-runs session.py and REPLACES _state, so acting inline would
        /// change the control set between a layout and repaint pass mid-frame.
        /// </summary>
        void Defer(System.Action action) => _deferred = action;

        void RunDeferred()
        {
            if (_deferred == null) return;
            var action = _deferred;
            _deferred = null;
            EditorApplication.delayCall += () =>
            {
                action();
                if (this != null) Repaint();
            };
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
            Description = "Run the untested-development backlog and submit verdicts.",
            DocPath = "Docs/QA/README.md#for-qa--how-to-run-a-session")]
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

                // ALWAYS name the file. Without --file the CLI falls back to the NEWEST
                // results file, so a tester viewing one session could have their verdict
                // written into somebody else's — and on a fresh clone the window adopted
                // whichever session happened to sort last (which is how a finished
                // session pinned to a deleted branch became everyone's default view).
                if (!string.IsNullOrEmpty(_activeSession) && args.Length > 0 && args[0] != "new")
                    sb.Append(" --file ").Append(Quote(_activeSession));

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

        // ── Finishing a session ───────────────────────────────────────────────────

        /// <summary>
        /// The whole end of the process behind ONE button: validate + publish the
        /// verdicts, hand the file to git, say what happened, and offer the next test.
        ///
        /// Deliberately NOT included: <c>apply_results.py</c>. That rewrites the three
        /// SHARED generated files (QA_BACKLOG / ARCHIVE / DEV_TASKS), so running it per
        /// tester would have every session racing the same three files, and a FAIL would
        /// land as a bare dev task — the `/qa-backlog` skill enriches each one with its
        /// source PR and likely files, which is the difference between a task and a
        /// research assignment. Applying stays a central batch step; the tester's job
        /// ends when their results are pushed.
        /// </summary>
        void SubmitAndFinish()
        {
            var offered = 0;
            foreach (var row in _state.rows)
                if (!row.frozen && !string.IsNullOrWhiteSpace(row.verdict)) offered++;

            Run("submit");

            if (_state == null || !_state.submitted)
            {
                EditorUtility.DisplayDialog("Not sent yet",
                    "Your results were NOT sent — something still needs fixing.\n\n" +
                    "Each problem is written in red next to the thing it belongs to. " +
                    "Fix them and press Submit again.\n\nNothing you have typed is lost.",
                    "Back to the form");
                return;
            }

            var publish = PublishSession();
            var next = NextBacklogItemId();

            // offered == 0 is the re-press case: everything in this session was already
            // sent. Harmless, and worth keeping honest rather than claiming "0 sent" —
            // it is also the safest possible rehearsal of the git path below.
            string message;
            if (offered == 0)
                message = "Nothing new to send — everything in this session had already " +
                          "been submitted.";
            else if (offered == 1)
                message = "1 verdict sent. Thank you — that is one more thing that is " +
                          "no longer untested.";
            else
                message = offered + " verdicts sent. Thank you — that is " + offered +
                          " more things that are no longer untested.";
            message += "\n\n" + publish;

            if (string.IsNullOrEmpty(next))
            {
                EditorUtility.DisplayDialog("Results submitted", message +
                    "\n\nThere are no more tests on the list. Nothing else to do.", "Close");
                Close();
                return;
            }

            var title = TitleOf(next);
            if (EditorUtility.DisplayDialog("Results submitted",
                    message + "\n\nWould you like to run the next test?\n\n" + next +
                    (string.IsNullOrEmpty(title) ? "" : "\n" + title),
                    "Yes — start the next test", "No — I am done for now"))
            {
                Run("set", "--item", next, "--verdict", "");
                _instructionsOpen[next] = true;
                _scroll = Vector2.zero;
            }
            else
            {
                Close();
            }
        }

        /// <summary>
        /// Commit and push the session file, its evidence and the ledger — and NOTHING
        /// else (FrogletGit's pathspec keeps the tester's own staged work untouched).
        ///
        /// Never switches branch: a checkout mid-session is the one git operation that
        /// could disturb the build being tested. On a protected branch (bleeding-edge)
        /// it pushes to a `qa/results-…` branch via a refspec instead, which needs no
        /// local checkout at all.
        /// </summary>
        string PublishSession()
        {
            var stem = _state.sessionFile.EndsWith(".md")
                ? _state.sessionFile.Substring(0, _state.sessionFile.Length - 3)
                : _state.sessionFile;
            var paths = new List<string>
            {
                "Docs/QA/RESULTS/" + _state.sessionFile,
                "Docs/QA/.applied.json",
            };
            var evidence = Path.Combine(RepoRoot, "Docs", "QA", "RESULTS", "evidence", stem);
            if (Directory.Exists(evidence)) paths.Add("Docs/QA/RESULTS/evidence/" + stem);

            var manual = "Your results are saved in the file, but they are NOT on the " +
                         "server yet. In GitHub Desktop, commit and push these:\n" +
                         "    Docs/QA/RESULTS/" + _state.sessionFile + "\n" +
                         "    Docs/QA/.applied.json";

            if (!FrogletGit.IsAvailable) return manual;

            try
            {
                var branch = FrogletGit.CurrentBranch();
                if (FrogletGit.IsProtectedBranch(branch))
                    return PublishFromProtectedBranch(stem, paths, manual);

                EditorUtility.DisplayProgressBar("Sending your results", "Saving to git…", 0.3f);
                var add = FrogletGit.Add(paths);
                if (!add.Ok) return manual + "\n\n(git add failed: " + add.Text.Trim() + ")";

                var commit = FrogletGit.Commit(CommitMessage(), paths);
                if (!commit.Ok && !FrogletGit.NothingToCommit(commit))
                    return manual + "\n\n(git commit failed: " + commit.Text.Trim() + ")";

                EditorUtility.DisplayProgressBar("Sending your results", "Uploading…", 0.7f);
                var pushed = FrogletGit.PushWithRetry(branch);
                return pushed.Ok
                    ? "Sent to the server on branch " + branch + " — nothing more for you to do."
                    : manual + "\n\n(git push failed: " + pushed.Text.Trim() + ")";
            }
            catch (System.Exception e)
            {
                return manual + "\n\n(" + e.GetType().Name + ": " + e.Message + ")";
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        string CommitMessage() =>
            "docs(qa): " + _state.tester + " session results, " + _state.date;

        /// <summary>
        /// Publish from a SHARED branch without moving anything the tester owns.
        ///
        /// The obvious implementation — commit locally, then push that commit to a QA
        /// ref — leaves their `bleeding-edge` one commit ahead of origin forever, with
        /// a Push button in GitHub Desktop that either errors or, if the branch is not
        /// protected server-side, lands a QA commit straight on it. So no local commit
        /// is made at all: the commit object is assembled with plumbing and pushed
        /// directly, leaving HEAD, the branch, the index and the working tree untouched.
        ///
        ///     read-tree HEAD  →  add PATHS  →  write-tree  →  commit-tree  →  push
        ///
        /// all against a THROWAWAY index (GIT_INDEX_FILE), which is what keeps it scoped:
        /// whatever else the tester has dirty or staged is invisible to it.
        ///
        /// `refs/qa-published/&lt;stem&gt;` records what was last published for this session
        /// and parents the next publish, so repeated submits FAST-FORWARD the QA branch
        /// rather than being rejected as unrelated histories. It also keeps that commit
        /// reachable so git cannot garbage-collect it between sessions, and it sits
        /// outside refs/heads so it never shows up as a branch.
        /// </summary>
        string PublishFromProtectedBranch(string stem, List<string> paths, string manual)
        {
            var qaRef = "refs/qa-published/" + stem;
            var index = Path.Combine(Path.GetTempPath(),
                                     "qa-index-" + System.Guid.NewGuid().ToString("N"));
            var env = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("GIT_INDEX_FILE", index),
            };

            try
            {
                EditorUtility.DisplayProgressBar("Sending your results", "Preparing…", 0.3f);

                var prior = FrogletGit.Run("rev-parse", "--verify", "-q", qaRef);
                var parent = prior.Ok ? prior.StdOut.Trim() : "HEAD";

                var read = FrogletGit.RunWithEnv(env, "read-tree", "HEAD");
                if (!read.Ok) return manual + "\n\n(git read-tree failed: " + read.Text.Trim() + ")";

                var args = new List<string> { "add", "--" };
                args.AddRange(paths);
                var staged = FrogletGit.RunWithEnv(env, args.ToArray());
                if (!staged.Ok) return manual + "\n\n(git add failed: " + staged.Text.Trim() + ")";

                var tree = FrogletGit.RunWithEnv(env, "write-tree");
                if (!tree.Ok) return manual + "\n\n(git write-tree failed: " + tree.Text.Trim() + ")";

                var made = FrogletGit.Run("commit-tree", tree.StdOut.Trim(),
                                          "-p", parent, "-m", CommitMessage());
                if (!made.Ok) return manual + "\n\n(git commit-tree failed: " + made.Text.Trim() + ")";
                var sha = made.StdOut.Trim();

                EditorUtility.DisplayProgressBar("Sending your results", "Uploading…", 0.7f);
                var qa = "qa/results-" + stem;
                // No -u: with a refspec it sets the upstream of the SOURCE ref — the
                // tester's own branch — re-pointing local bleeding-edge at the QA branch,
                // so their next Pull would fetch it into the build under test.
                var push = FrogletGit.Run("push", "origin", sha + ":refs/heads/" + qa);
                if (!push.Ok) return manual + "\n\n(git push failed: " + push.Text.Trim() + ")";

                FrogletGit.Run("update-ref", qaRef, sha);
                return "Sent to the server on branch " + qa + ". Engineering picks it up " +
                       "from there — nothing more for you to do.";
            }
            finally
            {
                try { if (File.Exists(index)) File.Delete(index); }
                catch (System.Exception) { /* a temp file we could not delete is harmless */ }
            }
        }

        /// <summary>
        /// Top of the backlog that is not in this session AND that nobody has already
        /// reported. The second half matters because QA_BACKLOG.md only loses a passed
        /// item when the central apply_results step runs — without it, tomorrow's
        /// session (or a second tester) is handed work that is already done.
        /// </summary>
        string NextBacklogItemId()
        {
            if (_state?.backlog == null) return null;
            var present = new HashSet<string>();
            foreach (var r in _state.rows) present.Add(r.id);
            foreach (var b in _state.backlog)
                if (!present.Contains(b.id) && string.IsNullOrEmpty(b.answeredBy))
                    return b.id;
            return null;
        }

        string TitleOf(string id)
        {
            var item = ItemFor(id);
            return item == null ? "" : item.priority + " — " + item.title;
        }

        /// <summary>
        /// " — see step 2" / " — see the test(s) marked in red". A blocked tester must
        /// be told WHERE to look, not just that some number of things are wrong.
        /// </summary>
        string BlockingHint()
        {
            var step2 = false;
            var inTests = false;
            foreach (var p in _state.problems)
                if (p.blocking && p.where == "Commit") step2 = true;
            foreach (var r in _state.rows)
                if (r.problemBlocking) inTests = true;

            if (step2 && inTests) return " — see step 2 and the test(s) marked in red";
            if (step2) return " — see step 2 above";
            if (inTests) return " — see the test(s) marked in red above";
            return " — each one is explained in red where it belongs, above";
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

        const string TesterPrefKey = "CosmicShore.QA.Tester";
        const string SessionPrefKey = "CosmicShore.QA.ActiveSession";

        void Refresh()
        {
            if (!FindPython(out _python, out _pythonPrefix, out _pythonVersion))
            {
                _python = null;
                return;
            }

            _newTester = EditorPrefs.GetString(TesterPrefKey, _newTester ?? "");
            _activeSession = EditorPrefs.GetString(SessionPrefKey, "");
            Run("state");

            // A remembered session that no longer exists (a fresh clone, someone else's
            // machine) must not pin the window to nothing. Prefer this tester's own file;
            // otherwise show the create screen rather than adopting a stranger's session.
            if (_state != null && !SessionExists(_activeSession))
            {
                _activeSession = MineOrNone();
                EditorPrefs.SetString(SessionPrefKey, _activeSession ?? "");
                Run("state");
            }
        }

        bool SessionExists(string file)
        {
            if (string.IsNullOrEmpty(file) || _state?.sessions == null) return false;
            foreach (var s in _state.sessions) if (s == file) return true;
            return false;
        }

        /// <summary>Newest session belonging to this tester, or null — never someone else's.</summary>
        string MineOrNone()
        {
            if (_state?.sessions == null || string.IsNullOrWhiteSpace(_newTester)) return null;
            var slug = Slug(_newTester);
            string best = null;
            foreach (var s in _state.sessions)
                if (s.IndexOf("-" + slug, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    best = s;   // sessions[] is sorted, so the last match is the newest
            return best;
        }

        static string Slug(string name)
        {
            var sb = new StringBuilder();
            foreach (var c in (name ?? "").Trim().ToLowerInvariant())
                sb.Append(char.IsLetterOrDigit(c) ? c : '-');
            return sb.ToString().Trim('-');
        }

        void SetActiveSession(string file)
        {
            _activeSession = file;
            EditorPrefs.SetString(SessionPrefKey, file ?? "");
            _noteEdits.Clear();
            _instructionsOpen.Clear();
            _scroll = Vector2.zero;
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
                else if (!_state.hasSession || _startingNew) DrawNoSession();
                else DrawSession();
                EditorGUILayout.EndScrollView();
                if (_state != null && _state.hasSession) DrawFooter();
            }

            RunDeferred();
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
                "Creates Docs/QA/RESULTS/<today>-<yourname>.md against the build you have " +
                "open right now. Steps 2 through 5 appear once it exists.",
                EditorStyles.wordWrappedMiniLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (FrogletEditorPalette.ColorButton("Create session",
                        FrogletEditorPalette.Ok, 140f, 24f, null,
                        enabled: !string.IsNullOrWhiteSpace(_newTester)))
                {
                    var who = _newTester;
                    EditorPrefs.SetString(TesterPrefKey, who);
                    _startingNew = false;
                    Defer(() =>
                    {
                        _activeSession = "";      // let `new` choose the filename
                        Run("new", "--tester", who,
                            "--unity", Application.unityVersion,
                            "--platform", "Editor (" + Application.platform + ")");
                        if (_state != null && !string.IsNullOrEmpty(_state.sessionFile))
                            SetActiveSession(_state.sessionFile);
                    });
                }

                if (_startingNew &&
                    FrogletEditorPalette.ColorButton("Cancel", FrogletEditorPalette.Muted, 90f))
                    _startingNew = false;
            }

            if (_state != null && _state.sessions != null && _state.sessions.Length > 0)
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("…or reopen an existing session:",
                    EditorStyles.miniBoldLabel);
                DrawSessionPicker();
            }
        }

        /// <summary>
        /// Which session file this window is working on.
        ///
        /// It exists because the window used to just open whichever results file sorted
        /// LAST. With one file in the repo that meant every tester on every machine
        /// inherited someone else's finished session — pinned to their branch (so step 2
        /// sent you to check out a branch that no longer exists) and with every row
        /// frozen (so no verdict control was drawn at all, and there was no way out
        /// because the create screen only appeared when NO session existed).
        /// </summary>
        void DrawSessionPicker()
        {
            var names = _state.sessions;
            var current = System.Array.IndexOf(names, _state.sessionFile);
            using (new EditorGUILayout.HorizontalScope())
            {
                var picked = EditorGUILayout.Popup(Mathf.Max(0, current), names);
                if (picked != current && picked >= 0 && picked < names.Length)
                {
                    var file = names[picked];
                    Defer(() => { _startingNew = false; SetActiveSession(file); });
                }
                if (!_startingNew &&
                    FrogletEditorPalette.ColorButton("New session…",
                        FrogletEditorPalette.Info, 120f, 18f,
                        "Start a fresh session — a retest, a different build, or another tester"))
                    _startingNew = true;
            }
        }

        void DrawSession()
        {
            StepHeader("1", "Your session",
                "This is your results file. It lives in Docs/QA/RESULTS/ and it is " +
                "yours alone — nothing in it reaches anyone else until you press " +
                "Submit in step 5. You can leave it half-finished for days and come " +
                "back to it; the window reopens whatever you were last working on.");
            EditorGUILayout.LabelField(_state.sessionFile, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Tester " + _state.tester + "   ·   " + _state.date + "   ·   Unity " +
                _state.unity + "   ·   " + _state.platform, EditorStyles.miniLabel);
            EditorGUILayout.Space(4);
            DrawSessionPicker();

            // Someone else's session is not yours to add to: its verdicts are frozen and
            // its build is theirs. Say so, rather than letting a tester quietly type into it.
            if (!string.IsNullOrWhiteSpace(_newTester) &&
                !string.Equals(_state.tester?.Trim(), _newTester.Trim(),
                               System.StringComparison.OrdinalIgnoreCase))
            {
                EditorGUILayout.HelpBox(
                    "This session belongs to " + _state.tester + ", not you. Its verdicts " +
                    "are theirs and its build may not be the one you have open. Press " +
                    "\"New session…\" to start your own.", MessageType.Warning);
            }

            StepHeader("2", "Check you have the right build",
                "Two names describe the code you are testing. The BRANCH is the line " +
                "of work — the tests in your list are about one particular branch. The " +
                "VERSION (engineers call it a \"commit\") is a short code, like a " +
                "receipt number, for one exact snapshot of that branch.\n\n" +
                "Your session recorded the version you had when you started. Branches " +
                "move on as people push work, so if you update the project, that " +
                "recorded version goes out of date — which is fine, as long as the " +
                "record ends up matching what you really tested. That is all this step " +
                "does.\n\n" +
                "It matters because a result filed against the wrong version sends " +
                "engineering hunting through the wrong code. When an instruction later " +
                "says \"the branch under test\", it means the branch named here.");
            DrawBuildStep();

            if (!string.IsNullOrEmpty(_state.preconditions))
            {
                StepHeader("3", "Set up Unity — once per session",
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
        /// The build check, made concrete and — importantly — honest about what the
        /// form's Commit row IS.
        ///
        /// It is stamped from HEAD when the session is created (session.py create), so
        /// it records "the version I started testing on", NOT a target engineering
        /// handed down. An earlier draft of this step read it as a target and told the
        /// tester to fetch/checkout/pull to reach it — which lands on the branch TIP
        /// and therefore can never reach a stale recorded commit when the branch name
        /// is the same. That was a dead end: the mismatch could not be cleared by
        /// following the instructions.
        ///
        /// So the two mismatch cases are separated by what actually differs. Wrong
        /// BRANCH is a real navigation problem, fixed in GitHub Desktop. Same branch,
        /// moved on is the ordinary case — the project advanced under an open session
        /// — and the fix is to record what you are really testing.
        /// </summary>
        void DrawBuildStep()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var match = !string.IsNullOrEmpty(_state.commit) &&
                            _state.commit == _state.head;
                var sameBranch = !string.IsNullOrEmpty(_state.sessionBranch) &&
                                 _state.sessionBranch == _state.branch;

                EditorGUILayout.LabelField("Your session says you are testing:",
                    EditorStyles.miniBoldLabel);
                EditorGUILayout.LabelField("      " + _state.sessionBranch +
                    "      version " + _state.commit, EditorStyles.boldLabel);
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("Unity actually has open:",
                    EditorStyles.miniBoldLabel);
                EditorGUILayout.LabelField("      " + _state.branch +
                    "      version " + _state.head, EditorStyles.boldLabel);

                if (match)
                {
                    FrogletEditorPalette.HorizontalRule(4f);
                    PillHeader("MATCH", FrogletEditorPalette.Ok, 62f);
                    EditorGUILayout.LabelField("Nothing to do here — go on to step 3.", Body);
                    return;
                }

                FrogletEditorPalette.HorizontalRule(4f);

                if (!sameBranch)
                {
                    EditorGUILayout.LabelField(
                        "You are on the wrong branch, so this is not the code your " +
                        "tests are about. Switch before you run anything:",
                        Body);
                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField("In GitHub Desktop:",
                        EditorStyles.miniBoldLabel);
                    EditorGUILayout.LabelField(
                        "1.  Click  Fetch origin  (top bar).\n" +
                        "2.  Click  Current Branch  (top bar) and choose  " +
                        _state.sessionBranch + "\n" +
                        "3.  Click  Pull origin  (top bar). If it is not offered, you " +
                        "are already up to date.\n" +
                        "4.  Come back to Unity, wait for it to finish importing, then " +
                        "press Refresh at the top of this window.", Body);
                    FrogletEditorPalette.HorizontalRule(4f);
                }
                else
                {
                    EditorGUILayout.LabelField(
                        "Same branch, newer code: the project has moved on since your " +
                        "session was created — someone pushed work, or you pulled it. " +
                        "That is normal and nothing is broken.", Body);
                    EditorGUILayout.Space(4);
                }

                EditorGUILayout.LabelField(
                    "Testing the version Unity has open now? Say so, and this step is " +
                    "done:", EditorStyles.miniBoldLabel);
                if (FrogletEditorPalette.ColorButton(
                        "Yes — I am testing version " + _state.head,
                        FrogletEditorPalette.Warn, 300f, 24f,
                        "Records " + _state.head + " as the version this session was " +
                        "run on. It only updates your own notes — it does not change " +
                        "any code. Press it only if EVERY test in this session was run " +
                        "on the version Unity has open right now."))
                    Defer(() => Run("submit", "--accept-head"));
                EditorGUILayout.LabelField(
                    "Only press that if every test in this session was run on it. If " +
                    "you were told to test one specific older version, ask whoever set " +
                    "up your session — do not guess.",
                    EditorStyles.wordWrappedMiniLabel);
            }
        }


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
                    EditorGUILayout.LabelField(headline, FrogletEditorPalette.CardTitle,
                        GUILayout.ExpandWidth(true));
                    if (item != null)
                        EditorGUILayout.LabelField(item.priority,
                            EditorStyles.miniBoldLabel, GUILayout.Width(24f));
                    if (!row.frozen &&
                        FrogletEditorPalette.ColorButton("×", FrogletEditorPalette.Error,
                            24f, 18f, "Remove this row from your session"))
                        Defer(() => Run("remove", "--item", row.id));
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
                        {
                            var chosen = options[picked];
                            Defer(() => Run("set", "--item", row.id, "--verdict", chosen));
                        }

                        GUILayout.FlexibleSpace();
                        if (FrogletEditorPalette.ColorButton("Attach evidence",
                                FrogletEditorPalette.Info, 120f, 18f,
                                "Copy a screenshot/clip/log next to this session and " +
                                "reference it from this item's notes"))
                            Defer(() => Attach(row.id));
                        if (FrogletEditorPalette.ColorButton("Save Console log",
                                FrogletEditorPalette.Muted, 120f, 18f,
                                "Save the current Editor log as evidence for this item"))
                            Defer(() => AttachEditorLog(row.id));
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
                                var note = typed;
                                Defer(() => Run("set", "--item", row.id, "--notes", note));
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
                    if (!first) FrogletEditorPalette.HorizontalRule(4f);
                    first = false;
                    EditorGUILayout.LabelField("What to do — in order", EditorStyles.miniBoldLabel);
                    EditorGUILayout.LabelField(item.steps, Body);
                }
                if (!string.IsNullOrEmpty(item.passWhen))
                {
                    if (!first) FrogletEditorPalette.HorizontalRule(4f);
                    first = false;
                    PillHeader("PASS WHEN", FrogletEditorPalette.Ok);
                    EditorGUILayout.LabelField(item.passWhen, Body);
                }
                if (!string.IsNullOrEmpty(item.failWhen))
                {
                    if (!first) FrogletEditorPalette.HorizontalRule(4f);
                    first = false;
                    PillHeader("FAIL WHEN", FrogletEditorPalette.Error);
                    EditorGUILayout.LabelField(item.failWhen, Body);
                }
                if (!string.IsNullOrEmpty(item.known))
                {
                    if (!first) FrogletEditorPalette.HorizontalRule(4f);
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
            // Already-reported items are listed LAST and labelled with who answered
            // them, rather than hidden: re-running one is occasionally right (a
            // second opinion, a retest after a fix), but it must be a deliberate
            // choice rather than the accident of a stale backlog file.
            var open = new List<string> { "Pick another test…" };
            var ids = new List<string> { null };
            var done = new List<string>();
            var doneIds = new List<string>();
            var present = new HashSet<string>();
            var remaining = 0;
            foreach (var r in _state.rows) present.Add(r.id);
            foreach (var b in _state.backlog)
            {
                if (present.Contains(b.id)) continue;
                if (string.IsNullOrEmpty(b.answeredBy))
                {
                    open.Add(b.priority + "  " + b.id + " — " + b.title);
                    ids.Add(b.id);
                    remaining++;
                }
                else
                {
                    done.Add("(done: " + b.answeredBy + ")  " + b.id + " — " + b.title);
                    doneIds.Add(b.id);
                }
            }
            open.AddRange(done);
            ids.AddRange(doneIds);

            EditorGUILayout.LabelField(
                remaining == 0
                    ? "Every test on the list has been reported. Nothing left to pick."
                    : remaining + " test(s) still waiting for someone to run them.",
                EditorStyles.miniLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                _addIndex = EditorGUILayout.Popup(_addIndex, open.ToArray());
                if (FrogletEditorPalette.ColorButton("Add", FrogletEditorPalette.Ok, 60f) &&
                    _addIndex > 0 && _addIndex < ids.Count)
                {
                    var picked = ids[_addIndex];
                    _addIndex = 0;
                    Defer(() => Run("set", "--item", picked, "--verdict", ""));
                }
            }

            // Show the selected item's instructions BEFORE it is added, so picking an
            // item is an informed choice, not a leap.
            if (_addIndex > 0 && _addIndex < ids.Count)
            {
                var chosen = ItemFor(ids[_addIndex]);
                if (chosen != null && !string.IsNullOrEmpty(chosen.answeredBy))
                    EditorGUILayout.HelpBox(
                        "Someone has already reported this one — " + chosen.answeredBy +
                        ". You can still run it (a retest after a fix, or a second " +
                        "opinion), but it is not work that is waiting for you.",
                        MessageType.Warning);
                EditorGUILayout.LabelField(
                    "Here is what that one involves. Press Add to put it in your list:",
                    EditorStyles.wordWrappedMiniLabel);
                DrawInstructions(chosen);
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
            // Drawn as the same numbered step as 1-4 — a pill, the title, the accent
            // rule — rather than as a bespoke strip, so the process reads unbroken to
            // its last line. It sits outside the scroll view so it is always reachable.
            var help =
                "Submit hands your finished verdicts to engineering. It checks your " +
                "form first and refuses if anything is missing, telling you exactly " +
                "what to fix — so pressing it can never lose your work.\n\n" +
                "You can submit as often as you like; each time, only the tests you " +
                "have newly finished are sent. Once a verdict is sent it is locked: if " +
                "that test gets fixed and you run it again later, that is a fresh " +
                "session, not an edit to this one.\n\n" +
                "You do not have to update the backlog afterwards — this window already " +
                "knows what has been reported and stops offering it. Folding results " +
                "into QA_BACKLOG.md and opening dev tasks for failures is a separate " +
                "step someone runs periodically (the /qa-backlog skill); it needs the " +
                "whole picture, so it is not yours to run.";

            // A BLOCKING PROBLEM OUTRANKS EVERYTHING, including "Sent". Checking
            // `submitted` first hid the reason the button was dead: a finished session
            // showed "Sent. Keep testing…" beside a greyed-out Submit and no way to
            // learn why. A disabled control must always be able to say what would
            // re-enable it.
            string status;
            Color tone;
            if (!_state.canSubmit)
            {
                status = _state.blocking + " thing(s) to fix before you can submit" +
                         BlockingHint() + ".";
                tone = FrogletEditorPalette.Error;
            }
            else if (HasUnsavedNotes)
            {
                status = "Save your edited note first — the Save note button is on that test.";
                tone = FrogletEditorPalette.Warn;
            }
            else if (_state.submitted)
            {
                status = "Sent. Keep testing and press Submit again whenever you finish more.";
                tone = FrogletEditorPalette.Ok;
            }
            else
            {
                status = "Everything checks out — press Submit to send your results.";
                tone = FrogletEditorPalette.Ok;
            }

            StepHeader("5", "Submit", help);
            using (new EditorGUILayout.HorizontalScope())
            {
                // The status wraps and takes the row; the button keeps its fixed width.
                // A fixed-width label here is what clipped "…press Submit again".
                EditorGUILayout.LabelField(status, Body, GUILayout.ExpandWidth(true));

                // enabled:, NOT a DisabledScope. A scope fades the fill and its white
                // label together, which is what made this button unreadable; the
                // palette's own disabled state is a legible Surface/Muted pair.
                if (FrogletEditorPalette.ColorButton("Submit session", tone, 150f, 26f,
                        HasUnsavedNotes
                            ? "Save your edited notes first"
                            : "Sends your verdicts, pushes them, and offers the next test",
                        enabled: _state.canSubmit && !HasUnsavedNotes))
                    Defer(SubmitAndFinish);
            }
            EditorGUILayout.Space(4);
        }
    }
}
