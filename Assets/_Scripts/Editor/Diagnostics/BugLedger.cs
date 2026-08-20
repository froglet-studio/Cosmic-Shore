using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace CosmicShore.Editor
{
    /// <summary>
    /// The shared, committable bug ledger — FrogletTools ▸ Diagnostics ▸ Bug Ledger.
    ///
    /// <para><b>The store.</b> One JSON file per issue under <c>BugLedger/issues/</c> at the
    /// PROJECT ROOT (outside Assets/, so Unity never imports it and no .meta churn exists). The
    /// folder is committable: pushing an issue shares it with the team, and because every issue is
    /// its own small file, two people working on different bugs can never conflict and a reviewer
    /// only ever sees the issues a branch actually touched. An auto-filed issue's id derives from
    /// its error SIGNATURE, so the same bug filed on two machines lands in the SAME file.</para>
    ///
    /// <para><b>Auto-capture.</b> Every error / exception / assert (never plain logs; warnings
    /// excluded) is captured via <see cref="Application.logMessageReceivedThreaded"/> — any thread,
    /// no dependence on a responsive main thread — normalized into a stable signature (digits, hex
    /// and machine-local path prefixes collapsed), and filed or updated by a background worker so
    /// the log path never blocks on file IO. One distinct signature = one issue, however many
    /// thousand times it fires.</para>
    ///
    /// <para><b>The lifecycle.</b> Open → (dev clicks Mark Fixed) → Validating → auto-resolved.
    /// While Validating, the fix is only believed when the game proves it: each qualifying session
    /// (a play run for PlayMode-scoped issues, a full editor session for EditMode-scoped ones)
    /// where the signature stays silent counts one clean session; at the issue's own
    /// <c>cleanSessionsRequired</c> the issue is closed and its file DELETED. If the signature
    /// recurs while Validating, the issue flips back to Open with a regression count — a fix that
    /// didn't fix is surfaced, loudly. Per issue, validation can be paused (nothing counts either
    /// way), the issue can be Ignored (parked; matching errors never reopen it, and its presence
    /// keeps auto-capture from re-filing the same signature), resolved outright, or deleted.</para>
    ///
    /// <para><b>READER tool</b> per Docs/TOOLING.md in the asset sense — it writes no Assets/, no
    /// ledger, no ship panel. Its output (<c>BugLedger/</c>) is ordinary committable project data
    /// the DEVELOPER chooses to push, exactly like editing a doc. Settings are machine-local
    /// (<see cref="BugLedgerSettings"/>, UserSettings/).</para>
    /// </summary>
    [InitializeOnLoad]
    public static class BugLedger
    {
        // ── SessionState keys (survive domain reloads, die with the editor) ─────────────────────
        const string SeenPlayKey = "BugLedger.SeenThisPlay";
        const string SeenEditorKey = "BugLedger.SeenThisEditorSession";
        const string TouchedKey = "BugLedger.TouchedThisEditorSession";
        const string PlayStartKey = "BugLedger.PlayStartUtc";
        const string EditorStartKey = "BugLedger.EditorStartUtc";

        const int MaxSampleChars = 500;
        const int MaxTitleChars = 120;
        const int MaxStackLines = 10;
        const int MaxStackChars = 1500;
        const int MaxQueuedEntries = 512;      // storm guard — dedupe makes drops harmless
        const int MaxPersistedSetEntries = 800;

        public static string RootDir { get; private set; }
        public static string IssuesDir { get; private set; }

        // ── Store (issues by id). Every reader/writer takes StoreLock. ──────────────────────────
        static readonly object StoreLock = new();
        static readonly Dictionary<string, BugLedgerIssue> Issues = new(StringComparer.OrdinalIgnoreCase);

        // ── Capture pipeline ─────────────────────────────────────────────────────────────────────
        readonly struct Captured
        {
            public readonly string Condition;
            public readonly string Stack;
            public readonly LogType Type;
            public readonly bool InPlayMode;

            public Captured(string condition, string stack, LogType type, bool inPlayMode)
            {
                Condition = condition;
                Stack = stack;
                Type = type;
                InPlayMode = inPlayMode;
            }
        }

        static readonly ConcurrentQueue<Captured> Queue = new();
        static readonly AutoResetEvent Signal = new(false);
        static Thread _worker;
        static volatile bool _running;
        static volatile bool _hooked;
        static volatile bool _inPlayMode;
        static volatile bool _capWarned;
        static DateTime _playStartUtc;         // main thread only
        static int _changeStamp;

        // ── Session evidence (which signatures fired when). Guarded by SeenLock. ─────────────────
        static readonly object SeenLock = new();
        static readonly HashSet<string> SeenThisPlay = new(StringComparer.OrdinalIgnoreCase);
        static readonly HashSet<string> SeenThisEditorSession = new(StringComparer.OrdinalIgnoreCase);
        static readonly HashSet<string> TouchedThisEditorSession = new(StringComparer.OrdinalIgnoreCase);

        // Settings snapshot — plain fields any thread may read; the SO itself is main-thread-only.
        static volatile bool _autoCapture = true;
        static volatile int _defaultCleanRequired = 2;
        static volatile int _minPlaySeconds = 15;
        static volatile int _minEditorMinutes = 10;
        static volatile int _maxAutoIssues = 150;

        /// <summary>Bumped on every mutation; the window polls it to know when to repaint.</summary>
        public static int ChangeStamp => _changeStamp;

        public static bool AutoCaptureEnabled => _autoCapture;

        static BugLedger()
        {
            // Same doctrine as the crash detector: the ledger must never take the editor down.
            try { Boot(); }
            catch (Exception e)
            {
                Debug.LogWarning($"[BugLedger] Boot failed; bug capture is off this session. {e.GetType().Name}: {e.Message}");
            }
        }

        // ── Boot / lifecycle ─────────────────────────────────────────────────────────────────────

        static void Boot()
        {
            var projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            RootDir = Path.Combine(projectRoot, "BugLedger");
            IssuesDir = Path.Combine(RootDir, "issues");

            ApplySettings(BugLedgerSettings.instance);
            LoadAllLocked();

            if (string.IsNullOrEmpty(SessionState.GetString(EditorStartKey, "")))
                SessionState.SetString(EditorStartKey, Iso(DateTime.UtcNow));

            _inPlayMode = EditorApplication.isPlayingOrWillChangePlaymode;
            if (_inPlayMode && TryParseIso(SessionState.GetString(PlayStartKey, ""), out var playStart))
                _playStartUtc = playStart;
            else
                _playStartUtc = DateTime.UtcNow;

            RestoreSet(SeenPlayKey, SeenThisPlay);
            RestoreSet(SeenEditorKey, SeenThisEditorSession);
            RestoreSet(TouchedKey, TouchedThisEditorSession);

            Hook();
            StartWorker();
        }

        /// <summary>Copies settings into the plain fields the background thread reads.</summary>
        public static void ApplySettings(BugLedgerSettings s)
        {
            _autoCapture = s.AutoCaptureEnabled;
            _defaultCleanRequired = s.DefaultCleanSessionsRequired;
            _minPlaySeconds = s.MinValidationPlaySeconds;
            _minEditorMinutes = s.MinEditorSessionMinutes;
            _maxAutoIssues = s.MaxAutoIssues;
        }

        public static void SetAutoCaptureEnabled(bool on)
        {
            var settings = BugLedgerSettings.instance;
            settings.AutoCaptureEnabled = on;
            settings.SaveNow();
            ApplySettings(settings);
            Bump();
        }

        static void Hook()
        {
            if (_hooked) return;
            _hooked = true;
            Application.logMessageReceivedThreaded += OnLogMessage;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            EditorApplication.quitting += OnQuitting;
        }

        static void Unhook()
        {
            if (!_hooked) return;
            _hooked = false;
            Application.logMessageReceivedThreaded -= OnLogMessage;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            EditorApplication.quitting -= OnQuitting;
        }

        static void StartWorker()
        {
            _running = true;
            _worker = new Thread(WorkerLoop)
            {
                Name = "BugLedger.Worker",
                IsBackground = true,
                Priority = System.Threading.ThreadPriority.BelowNormal,
            };
            _worker.Start();
        }

        /// <summary>Stops the worker after it drains everything already captured.</summary>
        static void StopWorker()
        {
            _running = false;
            Signal.Set();
            try { _worker?.Join(2000); } catch { }
            _worker = null;
        }

        static void OnBeforeAssemblyReload()
        {
            Unhook();
            StopWorker();
            lock (SeenLock)
            {
                PersistSet(SeenPlayKey, SeenThisPlay);
                PersistSet(SeenEditorKey, SeenThisEditorSession);
                PersistSet(TouchedKey, TouchedThisEditorSession);
            }
        }

        static void OnQuitting()
        {
            Unhook();
            StopWorker();
            CreditEditorSessionEnd();
        }

        // ── Capture (any thread — allocation-light, never throws) ────────────────────────────────

        static void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            if (!_running || !_autoCapture) return;
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            if (condition != null &&
                (condition.StartsWith("[BugLedger]", StringComparison.Ordinal) ||
                 condition.StartsWith("[CrashDetector]", StringComparison.Ordinal))) return;

            try
            {
                if (Queue.Count >= MaxQueuedEntries) return;   // storm guard; dedupe makes drops harmless
                Queue.Enqueue(new Captured(condition, stackTrace, type, _inPlayMode));
                Signal.Set();
            }
            catch { /* never throw back into the log pipeline */ }
        }

        static void WorkerLoop()
        {
            try
            {
                while (_running)
                {
                    Signal.WaitOne(500);
                    while (Queue.TryDequeue(out var entry)) Process(entry);
                }
                while (Queue.TryDequeue(out var entry)) Process(entry);
            }
            catch (ThreadAbortException) { }
            catch { /* the ledger must never surface as an editor exception */ }
        }

        static void Process(Captured entry)
        {
            try
            {
                string id = SignatureId(entry.Condition, entry.Stack, entry.Type, out string signature);

                bool firstThisSession;
                lock (SeenLock)
                {
                    if (entry.InPlayMode) SeenThisPlay.Add(id);
                    SeenThisEditorSession.Add(id);
                    firstThisSession = TouchedThisEditorSession.Add(id);
                }

                var nowIso = Iso(DateTime.UtcNow);
                lock (StoreLock)
                {
                    if (Issues.TryGetValue(id, out var issue))
                    {
                        issue.TimesSeen++;
                        issue.LastSeenUtc = nowIso;
                        if (entry.InPlayMode && issue.Scope != "PlayMode") issue.Scope = "PlayMode";

                        if (issue.IsValidating && !issue.ValidationPaused)
                        {
                            // The fix did not fix. Reopen loudly — this is the ledger's whole point.
                            issue.State = BugLedgerIssueState.Open;
                            issue.Regressions++;
                            issue.CleanSessions = 0;
                            WriteLocked(issue);
                            Debug.LogWarning($"[BugLedger] Regression: \"{issue.Title}\" recurred while validating — reopened (regression #{issue.Regressions}).");
                        }
                        else if (firstThisSession)
                        {
                            // One occurrence write per issue per editor session keeps the committed
                            // files quiet; later hits this session count in memory only.
                            WriteLocked(issue);
                        }
                        return;
                    }

                    int autoCount = 0;
                    foreach (var existing in Issues.Values)
                        if (existing.Kind == BugLedgerIssueKind.Auto) autoCount++;
                    if (autoCount >= _maxAutoIssues)
                    {
                        if (!_capWarned)
                        {
                            _capWarned = true;
                            Debug.LogWarning($"[BugLedger] Auto-issue cap reached ({_maxAutoIssues}) — new error signatures are no longer being filed. Triage the ledger.");
                        }
                        return;
                    }

                    var created = new BugLedgerIssue
                    {
                        Id = id,
                        Kind = BugLedgerIssueKind.Auto,
                        State = BugLedgerIssueState.Open,
                        Title = FirstLine(entry.Condition, MaxTitleChars),
                        Signature = signature,
                        Sample = Truncate(entry.Condition, MaxSampleChars),
                        Stack = StackExcerpt(entry.Stack),
                        Scope = entry.InPlayMode ? "PlayMode" : "EditMode",
                        LogType = entry.Type.ToString(),
                        Reporter = Environment.UserName,
                        Machine = Environment.MachineName,
                        CreatedUtc = nowIso,
                        LastSeenUtc = nowIso,
                        TimesSeen = 1,
                        CleanSessionsRequired = _defaultCleanRequired,
                    };
                    Issues[id] = created;
                    WriteLocked(created);
                    // No console line: the error itself is already red in the console, and a second
                    // line per distinct error is exactly the noise the tooling contract forbids.
                }
            }
            catch { /* one bad entry must not kill the worker */ }
        }

        // ── Session tracking + auto-validation ───────────────────────────────────────────────────

        static void OnPlayModeChanged(PlayModeStateChange change)
        {
            switch (change)
            {
                case PlayModeStateChange.EnteredPlayMode:
                    _inPlayMode = true;
                    _playStartUtc = DateTime.UtcNow;
                    SessionState.SetString(PlayStartKey, Iso(_playStartUtc));
                    lock (SeenLock) SeenThisPlay.Clear();
                    break;

                case PlayModeStateChange.ExitingPlayMode:
                    _inPlayMode = false;
                    // Let the worker land anything captured this play run before judging it —
                    // an entry still in the queue is not yet in the seen set, and crediting a
                    // clean session over it would validate a fix the session just disproved.
                    DrainCapturesBriefly();
                    double seconds = (DateTime.UtcNow - _playStartUtc).TotalSeconds;
                    HashSet<string> seen;
                    lock (SeenLock)
                    {
                        seen = new HashSet<string>(SeenThisPlay, StringComparer.OrdinalIgnoreCase);
                        SeenThisPlay.Clear();
                    }
                    SessionState.SetString(PlayStartKey, "");
                    CreditCleanSessions("PlayMode", seen, qualifies: seconds >= _minPlaySeconds,
                        why: $"{Math.Max(1, (int)seconds)}s play run");
                    break;
            }
        }

        /// <summary>Bounded wait for the worker to finish the already-captured backlog. Only ever
        /// called on the main thread at a session boundary, so 400 ms is the worst-case cost of a
        /// storm that is literally still in flight.</summary>
        static void DrainCapturesBriefly()
        {
            Signal.Set();
            var deadline = DateTime.UtcNow.AddMilliseconds(400);
            while (!Queue.IsEmpty && DateTime.UtcNow < deadline)
                Thread.Sleep(5);
        }

        static void CreditEditorSessionEnd()
        {
            if (!TryParseIso(SessionState.GetString(EditorStartKey, ""), out var started)) return;
            double minutes = (DateTime.UtcNow - started).TotalMinutes;
            HashSet<string> seen;
            lock (SeenLock) seen = new HashSet<string>(SeenThisEditorSession, StringComparer.OrdinalIgnoreCase);
            CreditCleanSessions("EditMode", seen, qualifies: minutes >= _minEditorMinutes,
                why: $"{Math.Max(1, (int)minutes)}min editor session");
        }

        /// <summary>
        /// One qualifying session ended without the given signatures firing — credit every
        /// unpaused Validating issue of that scope, and close the ones that reached their quota.
        /// Only counts while auto-capture is on: a clean session is only evidence when something
        /// was listening.
        /// </summary>
        static void CreditCleanSessions(string scope, HashSet<string> seenIds, bool qualifies, string why)
        {
            if (!_autoCapture || !qualifies) return;

            List<BugLedgerIssue> resolved = null;
            lock (StoreLock)
            {
                List<string> toRemove = null;
                foreach (var issue in Issues.Values)
                {
                    if (!issue.IsValidating || issue.ValidationPaused || !issue.HasSignature) continue;
                    if (issue.Scope != scope) continue;
                    if (seenIds.Contains(issue.Id)) continue;   // recurrence already reopened it

                    issue.CleanSessions++;
                    if (issue.CleanSessions >= issue.CleanSessionsRequired)
                    {
                        (toRemove ??= new List<string>()).Add(issue.Id);
                        (resolved ??= new List<BugLedgerIssue>()).Add(issue);
                    }
                    else
                    {
                        WriteLocked(issue);
                    }
                }

                if (toRemove != null)
                    foreach (var id in toRemove)
                        DeleteLocked(id);
            }

            if (resolved == null) return;
            foreach (var issue in resolved)
                Debug.Log($"[BugLedger] Validated & closed: \"{issue.Title}\" — silent across {issue.CleanSessionsRequired} clean {scope} sessions (last: {why}). Ledger entry deleted.");
        }

        // ── Public API (main thread; the window and other tools) ─────────────────────────────────

        /// <summary>Stable copy of the current issue list, for UI. Field-level tearing while the
        /// worker writes is possible and harmless — every field is display-only here.</summary>
        public static List<BugLedgerIssue> Snapshot()
        {
            lock (StoreLock) return new List<BugLedgerIssue>(Issues.Values);
        }

        public static void CountsByState(out int open, out int validating, out int ignored)
        {
            open = validating = ignored = 0;
            lock (StoreLock)
            {
                foreach (var issue in Issues.Values)
                {
                    if (issue.IsOpen) open++;
                    else if (issue.IsValidating) validating++;
                    else if (issue.IsIgnored) ignored++;
                }
            }
        }

        /// <summary>Re-scans the issues folder — picks up a pull, a teammate's push, hand edits.</summary>
        public static void RefreshFromDisk()
        {
            lock (StoreLock) LoadAllLocked();
            Bump();
        }

        public static string IssuePath(string id)
            => IssuesDir == null || string.IsNullOrEmpty(id) ? null : Path.Combine(IssuesDir, id + ".bug.json");

        /// <summary>Files a custom (human-authored) bug. Returns its id.</summary>
        public static string ReportCustom(string title, string notes)
        {
            var nowIso = Iso(DateTime.UtcNow);
            var id = "C-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)
                          + "-" + RandomSuffix();
            var issue = new BugLedgerIssue
            {
                Id = id,
                Kind = BugLedgerIssueKind.Custom,
                State = BugLedgerIssueState.Open,
                Title = FirstLine(title, MaxTitleChars),
                Notes = notes ?? "",
                Reporter = Environment.UserName,
                Machine = Environment.MachineName,
                CreatedUtc = nowIso,
                LastSeenUtc = nowIso,
                TimesSeen = 1,
                CleanSessionsRequired = _defaultCleanRequired,
            };
            lock (StoreLock)
            {
                Issues[id] = issue;
                WriteLocked(issue);
            }
            return id;
        }

        /// <summary>Open → Validating. Only meaningful for issues with a signature; the window
        /// routes signatureless issues to <see cref="ResolveNow"/> after a confirm.</summary>
        public static void MarkFixed(string id)
            => Mutate(id, issue =>
            {
                issue.State = BugLedgerIssueState.Validating;
                issue.CleanSessions = 0;
                issue.ValidationPaused = false;
                issue.FixedBy = Environment.UserName;
                issue.FixedUtc = Iso(DateTime.UtcNow);
            });

        /// <summary>Closes the issue and deletes its file, bypassing validation.</summary>
        public static void ResolveNow(string id)
        {
            lock (StoreLock) DeleteLocked(id);
        }

        public static void Ignore(string id)
            => Mutate(id, issue => issue.State = BugLedgerIssueState.Ignored);

        public static void Reopen(string id)
            => Mutate(id, issue =>
            {
                issue.State = BugLedgerIssueState.Open;
                issue.CleanSessions = 0;
            });

        public static void SetValidationPaused(string id, bool paused)
            => Mutate(id, issue => issue.ValidationPaused = paused);

        /// <summary>Deletes the issue outright (no validation, no trace). The window confirms first.</summary>
        public static void Delete(string id)
        {
            lock (StoreLock) DeleteLocked(id);
        }

        public static void SaveNotes(string id, string notes)
            => Mutate(id, issue => issue.Notes = notes ?? "");

        static void Mutate(string id, Action<BugLedgerIssue> edit)
        {
            lock (StoreLock)
            {
                if (!Issues.TryGetValue(id, out var issue)) return;
                edit(issue);
                WriteLocked(issue);
            }
        }

        // ── Store IO (call sites hold StoreLock) ─────────────────────────────────────────────────

        static void LoadAllLocked()
        {
            Issues.Clear();
            try
            {
                if (IssuesDir == null || !Directory.Exists(IssuesDir)) return;
                foreach (var path in Directory.GetFiles(IssuesDir, "*.bug.json"))
                {
                    var issue = BugLedgerIssue.FromJson(File.ReadAllText(path));
                    if (issue != null) Issues[issue.Id] = issue;
                }
            }
            catch { /* an unreadable folder degrades to an empty ledger, never an exception */ }
        }

        static void WriteLocked(BugLedgerIssue issue)
        {
            try
            {
                Directory.CreateDirectory(IssuesDir);
                File.WriteAllText(IssuePath(issue.Id), issue.ToJson());
            }
            catch { }
            Bump();
        }

        static void DeleteLocked(string id)
        {
            Issues.Remove(id);
            try
            {
                var path = IssuePath(id);
                if (path != null && File.Exists(path)) File.Delete(path);
            }
            catch { }
            Bump();
        }

        static void Bump() => Interlocked.Increment(ref _changeStamp);

        // ── Signatures ───────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Deterministic id for an error: hash of (log type | normalized message | normalized top
        /// user frame). Digits, hex runs and machine-local path prefixes are collapsed so counts,
        /// instance ids, positions and checkout locations don't split one bug into many — and so
        /// the same bug hashes identically on every machine.
        /// </summary>
        internal static string SignatureId(string condition, string stack, LogType type, out string signature)
        {
            var message = NormalizeText(condition, 300);
            var frame = TopUserFrame(stack);
            signature = $"{type}|{message}|{frame}";

            using var md5 = MD5.Create();
            var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(signature));
            var sb = new StringBuilder(12);
            for (int i = 0; i < 5; i++) sb.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
            return "E-" + sb;
        }

        /// <summary>First line, trimmed, hex runs → <c>0x#</c>, digit runs → <c>#</c>.</summary>
        internal static string NormalizeText(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            int newline = s.IndexOf('\n');
            if (newline >= 0) s = s[..newline];
            s = s.Trim();
            if (s.Length > max) s = s[..max];

            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '0' && i + 1 < s.Length && (s[i + 1] == 'x' || s[i + 1] == 'X'))
                {
                    int j = i + 2;
                    while (j < s.Length && Uri.IsHexDigit(s[j])) j++;
                    if (j > i + 2)
                    {
                        sb.Append("0x#");
                        i = j - 1;
                        continue;
                    }
                }
                if (char.IsDigit(c))
                {
                    while (i + 1 < s.Length && char.IsDigit(s[i + 1])) i++;
                    sb.Append('#');
                    continue;
                }
                sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>
        /// The first CosmicShore frame of a stack (else the first frame at all), with any
        /// machine-local absolute path stripped back to <c>Assets/…</c> — a signature must hash the
        /// same on every checkout.
        /// </summary>
        internal static string TopUserFrame(string stack)
        {
            if (string.IsNullOrEmpty(stack)) return "";
            string firstNonEmpty = null;
            string pick = null;
            foreach (var raw in stack.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0) continue;
                firstNonEmpty ??= line;
                if (line.Contains("CosmicShore", StringComparison.Ordinal) &&
                    !line.Contains("BugLedger", StringComparison.Ordinal) &&
                    !line.Contains("CrashDetector", StringComparison.Ordinal))
                {
                    pick = line;
                    break;
                }
            }
            pick ??= firstNonEmpty;
            if (pick == null) return "";

            pick = pick.Replace('\\', '/');
            // Both frame formats carry a source location: mono-style "… in <path>:line" and
            // unity-style "… (at <path>:line)". Either may be absolute on one machine and
            // repo-relative on another, so both are cut back to "Assets/…".
            pick = StripPathAfterMarker(pick, " in ");
            pick = StripPathAfterMarker(pick, "(at ");
            return NormalizeText(pick, 240);
        }

        static string StripPathAfterMarker(string line, string marker)
        {
            int idx = line.LastIndexOf(marker, StringComparison.Ordinal);
            if (idx < 0) return line;
            int pathStart = idx + marker.Length;
            var tail = line[pathStart..];
            int assetsIdx = tail.IndexOf("Assets/", StringComparison.OrdinalIgnoreCase);
            if (assetsIdx == 0) return line;                              // already repo-relative
            if (assetsIdx > 0) return line[..pathStart] + tail[assetsIdx..];
            return line[..idx];   // no Assets/ segment (packages, il2cpp) — drop the alien path
        }

        // ── Helpers ──────────────────────────────────────────────────────────────────────────────

        static string FirstLine(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "(no message)";
            int newline = s.IndexOf('\n');
            if (newline >= 0) s = s[..newline];
            s = s.Trim();
            return s.Length > max ? s[..max] : s;
        }

        static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Trim();
            return s.Length > max ? s[..max] + "…" : s;
        }

        static string StackExcerpt(string stack)
        {
            if (string.IsNullOrEmpty(stack)) return "";
            var sb = new StringBuilder(256);
            int lines = 0;
            foreach (var raw in stack.Split('\n'))
            {
                var line = raw.TrimEnd();
                if (line.Length == 0) continue;
                if (lines > 0) sb.Append('\n');
                sb.Append(line);
                if (++lines >= MaxStackLines || sb.Length >= MaxStackChars) break;
            }
            var result = sb.ToString();
            return result.Length > MaxStackChars ? result[..MaxStackChars] : result;
        }

        static string RandomSuffix()
        {
            var bytes = new byte[2];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            return bytes[0].ToString("x2", CultureInfo.InvariantCulture) +
                   bytes[1].ToString("x2", CultureInfo.InvariantCulture);
        }

        static void PersistSet(string key, HashSet<string> set)
        {
            var sb = new StringBuilder(set.Count * 13);
            int written = 0;
            foreach (var id in set)
            {
                if (written++ >= MaxPersistedSetEntries) break;
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(id);
            }
            SessionState.SetString(key, sb.ToString());
        }

        static void RestoreSet(string key, HashSet<string> set)
        {
            var stored = SessionState.GetString(key, "");
            if (string.IsNullOrEmpty(stored)) return;
            foreach (var id in stored.Split('\n'))
                if (id.Length > 0) set.Add(id);
        }

        static string Iso(DateTime utc)
            => utc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);

        static bool TryParseIso(string text, out DateTime utc)
            => DateTime.TryParse(text, CultureInfo.InvariantCulture,
                                 DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out utc);
    }
}
