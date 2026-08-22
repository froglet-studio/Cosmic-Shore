using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using CosmicShore.Utility;
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

        const int MaxArchivedIssues = 300;

        public static string RootDir { get; private set; }
        /// <summary>Machine-local live store (gitignored via BugLedger/.gitignore). ALL day-to-day
        /// writes land here, so the ledger never dirties version control on its own — an issue
        /// reaches git only when the human stages and pushes it (Stage &amp; Push tab).</summary>
        public static string LocalDir { get; private set; }
        public static string IssuesDir { get; private set; }
        public static string ResolvedDir { get; private set; }
        /// <summary>The tracked, published set — written ONLY by <see cref="ApplyStagedChanges"/>
        /// during a commit-and-push, read to sync teammates' issues down.</summary>
        public static string SharedIssuesDir { get; private set; }

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
            LocalDir = Path.Combine(RootDir, "local");
            IssuesDir = Path.Combine(LocalDir, "issues");
            ResolvedDir = Path.Combine(LocalDir, "resolved");
            SharedIssuesDir = Path.Combine(RootDir, "shared", "issues");

            MigrateLegacyLayout();
            EnsureStoreGitignore();
            ApplySettings(BugLedgerSettings.instance);
            lock (StoreLock)
            {
                LoadAllLocked();
                SyncFromSharedLocked();
            }

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
                string id = BugSignature.ErrorId(entry.Condition, entry.Stack, entry.Type, out string signature);

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
                foreach (var issue in Issues.Values)
                {
                    if (!issue.IsValidating || issue.ValidationPaused || !issue.HasSignature) continue;
                    if (issue.Scope != scope) continue;
                    if (seenIds.Contains(issue.Id)) continue;   // recurrence already reopened it

                    issue.CleanSessions++;
                    if (issue.CleanSessions >= issue.CleanSessionsRequired)
                        (resolved ??= new List<BugLedgerIssue>()).Add(issue);
                    else
                        WriteLocked(issue);
                }

                if (resolved != null)
                    foreach (var issue in resolved)
                        ResolveLocked(issue, $"validated — silent across {issue.CleanSessionsRequired} clean {scope} sessions");
            }

            if (resolved == null) return;
            foreach (var issue in resolved)
                Debug.Log($"[BugLedger] Validated & closed: \"{issue.Title}\" — silent across {issue.CleanSessionsRequired} clean {scope} sessions (last: {why}). Archived to BugLedger/resolved/.");
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

        /// <summary>Re-scans the local store AND syncs teammates' shared issues down — picks up a
        /// pull, a push, hand edits.</summary>
        public static void RefreshFromDisk()
        {
            lock (StoreLock)
            {
                LoadAllLocked();
                SyncFromSharedLocked();
            }
            Bump();
        }

        /// <summary>For out-of-band mutations (the publisher finishing a push on its worker
        /// thread) — makes every open window recompute.</summary>
        public static void NotifyExternalChange() => Bump();

        public static string IssuePath(string id)
            => IssuesDir == null || string.IsNullOrEmpty(id) ? null : Path.Combine(IssuesDir, id + ".bug.json");

        /// <summary>Files a custom (human-authored) bug. Returns its id.</summary>
        public static string ReportCustom(string title, string notes,
                                          string severity = BugLedgerIssueSeverity.Major)
        {
            var nowIso = Iso(DateTime.UtcNow);
            var id = "C-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)
                          + "-" + RandomSuffix();
            var issue = new BugLedgerIssue
            {
                Id = id,
                Kind = BugLedgerIssueKind.Custom,
                State = BugLedgerIssueState.Open,
                Severity = severity,
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

        /// <summary>
        /// Files (or refreshes) ONE finding from an editor tool — an auditor failure, a validator
        /// hit, the crash detector's File Bug. The id derives from (tool, normalized title) via
        /// <see cref="BugSignature.ToolId"/>, so reporting the same finding twice updates one
        /// issue instead of minting a duplicate. Tool issues carry scope "Tool" and take part in
        /// NO session-based validation — their validator is the tool itself, via
        /// <see cref="ReportToolFindings"/>. Returns the issue id.
        /// </summary>
        public static string ReportFromTool(string toolName, string title, string notes,
                                            string severity = BugLedgerIssueSeverity.Major)
        {
            string id = BugSignature.ToolId(toolName, title, out var signature);
            var nowIso = Iso(DateTime.UtcNow);
            lock (StoreLock)
            {
                if (Issues.TryGetValue(id, out var existing))
                {
                    existing.TimesSeen++;
                    existing.LastSeenUtc = nowIso;
                    if (existing.IsValidating && !existing.ValidationPaused)
                    {
                        existing.State = BugLedgerIssueState.Open;
                        existing.Regressions++;
                        existing.CleanSessions = 0;
                        Debug.LogWarning($"[BugLedger] Regression: \"{existing.Title}\" reported again by {toolName} while validating — reopened (regression #{existing.Regressions}).");
                    }
                    WriteLocked(existing);
                    return id;
                }

                var issue = new BugLedgerIssue
                {
                    Id = id,
                    Kind = BugLedgerIssueKind.Tool,
                    State = BugLedgerIssueState.Open,
                    Severity = severity,
                    Title = FirstLine(title, MaxTitleChars),
                    Notes = notes ?? "",
                    Signature = signature,
                    Scope = "Tool",
                    LogType = toolName,
                    Reporter = Environment.UserName,
                    Machine = Environment.MachineName,
                    CreatedUtc = nowIso,
                    LastSeenUtc = nowIso,
                    TimesSeen = 1,
                    // A deterministic tool re-run that stops reporting a finding IS the proof —
                    // one clean run closes it (see ReportToolFindings).
                    CleanSessionsRequired = 1,
                };
                Issues[id] = issue;
                WriteLocked(issue);
                return id;
            }
        }

        /// <summary>
        /// A tool ran to completion and THESE are all the findings it has (possibly none). Each is
        /// filed/refreshed via <see cref="ReportFromTool"/>; then every VALIDATING issue this tool
        /// owns that the run did NOT re-report is resolved — the tool that filed a finding is the
        /// one authority on whether it is gone, and a full clean re-run is stronger evidence than
        /// any number of play sessions. Only call this after a FULL run (a partial sweep would
        /// resolve findings it simply never looked at).
        /// </summary>
        public static void ReportToolFindings(string toolName,
                                              IReadOnlyList<(string title, string notes)> findings,
                                              string severity = BugLedgerIssueSeverity.Major)
        {
            var reported = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (findings != null)
                foreach (var (title, notes) in findings)
                    reported.Add(ReportFromTool(toolName, title, notes, severity));

            var prefix = $"Tool|{toolName}|";
            List<BugLedgerIssue> resolved = null;
            lock (StoreLock)
            {
                foreach (var issue in Issues.Values)
                {
                    if (!issue.IsValidating || issue.ValidationPaused) continue;
                    if (!issue.Signature.StartsWith(prefix, StringComparison.Ordinal)) continue;
                    if (reported.Contains(issue.Id)) continue;
                    (resolved ??= new List<BugLedgerIssue>()).Add(issue);
                }
                if (resolved != null)
                    foreach (var issue in resolved)
                        ResolveLocked(issue, $"validated — a full {toolName} run no longer reports it");
            }

            if (resolved == null) return;
            foreach (var issue in resolved)
                Debug.Log($"[BugLedger] Validated & closed: \"{issue.Title}\" — a full {toolName} run no longer reports it. Archived to BugLedger/resolved/.");
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

        /// <summary>Closes the issue, bypassing validation. The live file is removed (archived
        /// under <c>BugLedger/resolved/</c> unless the archive is off).</summary>
        public static void ResolveNow(string id)
        {
            lock (StoreLock)
            {
                if (Issues.TryGetValue(id, out var issue))
                    ResolveLocked(issue, "resolved by hand");
            }
        }

        public static void SetSeverity(string id, string severity)
            => Mutate(id, issue => issue.Severity = severity);

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

        /// <summary>Discards the issue (no validation). The window confirms first. Internally this
        /// still stamps a tombstone into the local archive — without one, a copy a teammate
        /// already pushed would re-import on the next sync and the bug would "resurrect".</summary>
        public static void Delete(string id)
        {
            lock (StoreLock)
            {
                if (Issues.TryGetValue(id, out var issue))
                    ResolveLocked(issue, "deleted by hand");
            }
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

        /// <summary>
        /// Adopts teammates' PUBLISHED issues into the local live store: every file under
        /// <c>shared/issues/</c> that is neither live locally nor tombstoned (resolved/deleted
        /// here) is imported. Local always wins for an issue that exists on both sides — the
        /// difference is what the Stage &amp; Push tab shows as a pending MODIFY.
        /// </summary>
        static void SyncFromSharedLocked()
        {
            try
            {
                if (SharedIssuesDir == null || !Directory.Exists(SharedIssuesDir)) return;
                foreach (var path in Directory.GetFiles(SharedIssuesDir, "*.bug.json"))
                {
                    var id = Path.GetFileName(path);
                    id = id[..^".bug.json".Length];
                    if (Issues.ContainsKey(id)) continue;
                    if (File.Exists(TombstonePath(id))) continue;

                    var issue = BugLedgerIssue.FromJson(File.ReadAllText(path));
                    if (issue == null || !string.Equals(issue.Id, id, StringComparison.OrdinalIgnoreCase)) continue;
                    Issues[issue.Id] = issue;
                    WriteLocked(issue);
                }
            }
            catch { }
        }

        /// <summary>Pre-split layouts wrote the live store directly under <c>BugLedger/issues|resolved</c>
        /// (tracked — which made every ledger heartbeat dirty git). Move any such files into the
        /// gitignored <c>local/</c> area once.</summary>
        static void MigrateLegacyLayout()
        {
            try
            {
                MigrateFolder(Path.Combine(RootDir, "issues"), IssuesDir);
                MigrateFolder(Path.Combine(RootDir, "resolved"), ResolvedDir);
            }
            catch { }
        }

        static void MigrateFolder(string oldDir, string newDir)
        {
            if (!Directory.Exists(oldDir)) return;
            Directory.CreateDirectory(newDir);
            foreach (var file in Directory.GetFiles(oldDir, "*.bug.json"))
            {
                var target = Path.Combine(newDir, Path.GetFileName(file));
                try
                {
                    if (File.Exists(target)) File.Delete(file);
                    else File.Move(file, target);
                }
                catch { }
            }
            try { if (Directory.GetFileSystemEntries(oldDir).Length == 0) Directory.Delete(oldDir); }
            catch { }
        }

        /// <summary>Self-heals <c>BugLedger/.gitignore</c> (a committed file) so the live store can
        /// never dirty version control even on a checkout that predates it.</summary>
        static void EnsureStoreGitignore()
        {
            try
            {
                var path = Path.Combine(RootDir, ".gitignore");
                if (File.Exists(path)) return;
                Directory.CreateDirectory(RootDir);
                File.WriteAllText(path,
                    "# Live ledger data is machine-local; only shared/ (published through the\n" +
                    "# Stage & Push tab of FrogletTools > Diagnostics > Bug Ledger) is tracked.\n" +
                    "local/\n");
            }
            catch { }
        }

        // ── Publishing (the Stage & Push tab's data source) ──────────────────────────────────────

        public enum BugLedgerChangeKind { Add = 0, Modify = 1, Remove = 2 }

        /// <summary>One difference between the local live store and the published shared set.</summary>
        public sealed class BugLedgerPendingChange
        {
            public string Id;
            public string Title;
            public BugLedgerChangeKind Kind;
        }

        /// <summary>
        /// Diffs local against shared: live issues missing from shared are ADDs, differing ones
        /// are MODIFYs, and shared issues with no live copy (resolved/deleted here) are REMOVEs.
        /// Pure computation — nothing is written, and git sees nothing until
        /// <see cref="ApplyStagedChanges"/> runs inside a publish.
        /// </summary>
        public static List<BugLedgerPendingChange> ComputePendingChanges()
        {
            var pending = new List<BugLedgerPendingChange>();
            lock (StoreLock)
            {
                var shared = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    if (SharedIssuesDir != null && Directory.Exists(SharedIssuesDir))
                        foreach (var path in Directory.GetFiles(SharedIssuesDir, "*.bug.json"))
                        {
                            var name = Path.GetFileName(path);
                            shared[name[..^".bug.json".Length]] = File.ReadAllText(path);
                        }
                }
                catch { }

                foreach (var issue in Issues.Values)
                {
                    if (!shared.TryGetValue(issue.Id, out var publishedJson))
                        pending.Add(new BugLedgerPendingChange { Id = issue.Id, Title = issue.Title, Kind = BugLedgerChangeKind.Add });
                    else if (!string.Equals(publishedJson, issue.ToJson(), StringComparison.Ordinal))
                        pending.Add(new BugLedgerPendingChange { Id = issue.Id, Title = issue.Title, Kind = BugLedgerChangeKind.Modify });
                }

                foreach (var kv in shared)
                {
                    if (Issues.ContainsKey(kv.Key)) continue;
                    var parsed = BugLedgerIssue.FromJson(kv.Value);
                    pending.Add(new BugLedgerPendingChange
                    {
                        Id = kv.Key,
                        Title = parsed?.Title ?? kv.Key,
                        Kind = BugLedgerChangeKind.Remove,
                    });
                }
            }

            pending.Sort((a, b) => a.Kind != b.Kind
                ? ((int)a.Kind).CompareTo((int)b.Kind)
                : string.CompareOrdinal(a.Id, b.Id));
            return pending;
        }

        /// <summary>
        /// Materializes the STAGED ids into <c>shared/</c> — copy for Add/Modify, delete for
        /// Remove — and returns the absolute paths touched (for a scoped <c>git add</c>). Called
        /// only from inside a publish operation (any thread; takes the store lock), never from a
        /// [+] click: files must not dirty git while the human is still choosing.
        /// </summary>
        public static List<string> ApplyStagedChanges(ICollection<string> stagedIds)
        {
            var touched = new List<string>();
            if (stagedIds == null || stagedIds.Count == 0) return touched;

            lock (StoreLock)
            {
                Directory.CreateDirectory(SharedIssuesDir);
                foreach (var id in stagedIds)
                {
                    var sharedPath = Path.Combine(SharedIssuesDir, id + ".bug.json");
                    try
                    {
                        if (Issues.TryGetValue(id, out var issue))
                        {
                            File.WriteAllText(sharedPath, issue.ToJson());
                            touched.Add(sharedPath);
                        }
                        else if (File.Exists(sharedPath))
                        {
                            File.Delete(sharedPath);
                            touched.Add(sharedPath);   // git add stages the deletion of a tracked file
                        }
                    }
                    catch { }
                }
            }
            Bump();
            return touched;
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

        /// <summary>Closes an issue: stamps a resolved copy into the local archive
        /// (<c>BugLedger/local/resolved/</c>), prunes it, and removes the live file. The archive is
        /// ALSO the tombstone — <see cref="SyncFromSharedLocked"/> uses it to keep a resolved or
        /// deleted issue from re-importing off a teammate's still-shared copy — which is why the
        /// stamp is unconditional. If the issue was published, its shared copy shows up as a
        /// REMOVE in the Stage &amp; Push tab.</summary>
        static void ResolveLocked(BugLedgerIssue issue, string reason)
        {
            try
            {
                issue.State = "resolved";
                issue.ResolvedUtc = Iso(DateTime.UtcNow);
                issue.Resolution = reason;
                Directory.CreateDirectory(ResolvedDir);
                File.WriteAllText(TombstonePath(issue.Id), issue.ToJson());
                PruneArchive();
            }
            catch { }
            DeleteLocked(issue.Id);
        }

        static string TombstonePath(string id) => Path.Combine(ResolvedDir, id + ".bug.json");

        static void PruneArchive()
        {
            try
            {
                var files = Directory.GetFiles(ResolvedDir, "*.bug.json");
                if (files.Length <= MaxArchivedIssues) return;
                Array.Sort(files, (a, b) =>
                    File.GetLastWriteTimeUtc(a).CompareTo(File.GetLastWriteTimeUtc(b)));
                for (int i = 0; i < files.Length - MaxArchivedIssues; i++)
                    File.Delete(files[i]);
            }
            catch { }
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

        // ── Helpers ──────────────────────────────────────────────────────────────────────────────
        // (Signature normalization/hashing lives in CosmicShore.Utility.BugSignature — the
        // runtime-safe shared core the future in-game reporter will use too.)

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
