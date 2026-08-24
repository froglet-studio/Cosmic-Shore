using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Always-on editor crash detector.
    ///
    /// <para><b>How it works.</b> On editor load it writes a session sentinel
    /// (<c>Logs/CrashDetector/session.state</c>) and starts one background thread that (a) drains a
    /// concurrent queue of captured errors/exceptions into an append-only journal, flushing after
    /// every drain so the tail survives a hard death, and (b) re-stamps the sentinel with a
    /// heartbeat + the editor's current state. Errors are captured via
    /// <see cref="Application.logMessageReceivedThreaded"/>, so capture works from ANY thread and
    /// nothing here depends on the main thread staying responsive — which is the point: when the
    /// main thread is the thing dying (or hanging), the journal and heartbeat keep landing.</para>
    ///
    /// <para><b>Crash detection.</b> A clean exit (<see cref="EditorApplication.quitting"/>) marks
    /// the sentinel clean; a domain reload re-adopts the same session (same pid). If the next
    /// editor launch finds a sentinel that is neither clean nor owned by a live Unity process, the
    /// previous session ended abnormally — Unity crash, PC fault, force-kill, hang-then-kill — and
    /// a <c>Crash-*.log</c> report is written from the stale sentinel, the captured error journal,
    /// and the tail of Unity's own <c>Editor-prev.log</c>. A main-thread liveness stamp in the
    /// sentinel lets the report distinguish "died abruptly" from "was hung for ~N s, then killed".</para>
    ///
    /// <para><b>READER tool</b> per Docs/TOOLING.md: writes only machine-local files under
    /// <c>Logs/CrashDetector/</c> and <c>UserSettings/</c> (both gitignored), never assets — no
    /// ledger, no ship panel. UI: FrogletTools ▸ Diagnostics ▸ Crash Detector
    /// (<see cref="DiagnosticsWindow"/>). Its sibling, <see cref="BugLedger"/>, files ISSUES from
    /// the same error stream — this class stays purely about the editor process dying.</para>
    /// </summary>
    [InitializeOnLoad]
    public static class CrashDetectorMonitor
    {
        // ── Session verdict keys (SessionState: survives domain reloads, dies with the editor) ──
        const string VerdictKey = "CrashDetector.LastSessionVerdict"; // "", "clean", "crashed"
        const string ReportKey = "CrashDetector.LastCrashReport";

        const int MaxEntriesPerSecond = 20;      // error-storm cap; excess collapses to one count
        const int ReportJournalBytes = 256 * 1024;
        const int ReportEditorLogBytes = 64 * 1024;

        // ── Paths (resolved on the main thread at boot; background thread reads the fields) ─────
        public static string RootDir { get; private set; }
        public static string SentinelPath { get; private set; }
        public static string JournalPath { get; private set; }

        // ── Live session state ───────────────────────────────────────────────────────────────────
        static readonly ConcurrentQueue<string> Queue = new();
        static readonly AutoResetEvent Signal = new(false);
        static readonly object SentinelLock = new();
        static readonly object RateLock = new();

        static Thread _writer;
        static volatile bool _running;
        static volatile bool _hooked;
        static volatile string _state = "EditMode";
        static string _sessionId = "";
        static string _unityVersion = "";
        static int _pid;
        static DateTime _sessionStartUtc;
        static long _mainAliveTicks;             // stamped by EditorApplication.update — hang detection
        static long _journalBytes;
        static volatile bool _capNoted;

        // Settings snapshot — plain fields the background thread may read; the SO itself is
        // main-thread-only, so values are copied here at boot and on every settings change.
        static volatile int _heartbeatSeconds = 5;
        static volatile bool _captureWarnings;
        static volatile int _stackLines = 8;
        static volatile int _maxReports = 10;
        static long _maxJournalBytes = 4 * 1024 * 1024;

        // ── Hang dump (writer thread; Windows only) ──────────────────────────────────────────────
        // A hang the user kills from Task Manager leaves no stack anywhere — the 2026-08-21
        // 'Run managed callbacks' freeze reports could say WHEN the main thread stopped but
        // never WHERE it was stuck. So when the writer thread sees the main thread stale past
        // the threshold it writes a minidump of the live process (thread stacks + module list,
        // a few MB): open it in Visual Studio / WinDbg and the main thread's stack names the
        // deadlock. One attempt per session; a stall that RECOVERS is journaled instead, so the
        // next report can tell slow from hung.
        static volatile int _hangDumpSeconds = 45;   // 0 = off
        static bool _hangDumpWritten;                // writer thread only
        static bool _mainWasStale;                   // writer thread only
        static long _staleSinceTicks;                // writer thread only
        static bool _isWindowsEditor;                // cached on the main thread at Boot

        [DllImport("DbgHelp.dll", SetLastError = true)]
        static extern bool MiniDumpWriteDump(IntPtr hProcess, uint processId,
            Microsoft.Win32.SafeHandles.SafeFileHandle hFile, int dumpType,
            IntPtr exceptionParam, IntPtr userStreamParam, IntPtr callbackParam);

        // rate limiting (any thread)
        static long _rateSecond;
        static int _rateCount;
        static int _suppressed;

        public static bool IsMonitoring => _running;
        public static string SessionId => _sessionId;
        public static DateTime SessionStartUtc => _sessionStartUtc;
        public static string CurrentState => _state;
        public static string LastSessionVerdict => SessionState.GetString(VerdictKey, "");
        public static string LastCrashReportPath => SessionState.GetString(ReportKey, "");

        static CrashDetectorMonitor()
        {
            // The detector must never take the editor down — a watchdog that crashes the patient
            // is worse than none.
            try { Boot(); }
            catch (Exception e)
            {
                Debug.LogWarning($"[CrashDetector] Boot failed; crash detection is off this session. {e.GetType().Name}: {e.Message}");
            }
        }

        // ── Boot / lifecycle ─────────────────────────────────────────────────────────────────────

        static void Boot()
        {
            EnsurePaths();
            var settings = CrashDetectorSettings.instance;
            ApplySettings(settings);
            _unityVersion = Application.unityVersion;
            _isWindowsEditor = Application.platform == RuntimePlatform.WindowsEditor;
            Directory.CreateDirectory(RootDir);

            var previous = SessionSentinel.TryRead(SentinelPath);
            int pid = Process.GetCurrentProcess().Id;
            bool sameProcess = previous != null && previous.pid == pid && !previous.cleanExit;

            if (!sameProcess)
            {
                // Fresh editor launch: judge the previous session before touching anything.
                if (previous == null)
                {
                    // First run (or logs were cleared) — nothing to judge.
                }
                else if (previous.cleanExit)
                {
                    SessionState.SetString(VerdictKey, "clean");
                }
                else if (IsUnityProcessAlive(previous.pid))
                {
                    // Ambiguous — the sentinel's pid is a live Unity (pid reuse, or an unusual
                    // multi-instance situation). Never cry crash on a maybe.
                }
                else
                {
                    WriteCrashReport(previous);
                }
            }

            if (!settings.Enabled)
            {
                // Nothing will be watching, so a stale sentinel must not misreport later.
                TryDelete(SentinelPath);
                return;
            }

            if (sameProcess) AdoptSession(previous, pid);
            else StartNewSession(pid);

            Hook();
            StartWriter();
        }

        /// <summary>Runtime toggle from the window. Persists the setting and starts/stops the watch.</summary>
        public static void SetEnabled(bool on)
        {
            var settings = CrashDetectorSettings.instance;
            settings.Enabled = on;
            settings.SaveNow();

            if (on && !_running)
            {
                try
                {
                    EnsurePaths();
                    ApplySettings(settings);
                    _unityVersion = Application.unityVersion;
                    Directory.CreateDirectory(RootDir);
                    StartNewSession(Process.GetCurrentProcess().Id);
                    Hook();
                    StartWriter();
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[CrashDetector] Could not start: {e.GetType().Name}: {e.Message}");
                }
            }
            else if (!on && _running)
            {
                Enqueue(Marker("crash detection disabled by user"));
                Shutdown("Disabled", cleanExit: true);
                TryDelete(SentinelPath);
            }
        }

        /// <summary>Copies settings into the plain fields the background thread reads.</summary>
        public static void ApplySettings(CrashDetectorSettings s)
        {
            _heartbeatSeconds = s.HeartbeatSeconds;
            _captureWarnings = s.CaptureWarnings;
            _stackLines = s.StackTraceLines;
            _maxReports = s.MaxReportsKept;
            _hangDumpSeconds = s.HangDumpSeconds;
            Interlocked.Exchange(ref _maxJournalBytes, s.MaxJournalMB * 1024L * 1024L);
        }

        static void EnsurePaths()
        {
            if (RootDir != null) return;
            var projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            RootDir = Path.Combine(projectRoot, "Logs", "CrashDetector");
            SentinelPath = Path.Combine(RootDir, "session.state");
            JournalPath = Path.Combine(RootDir, "session.journal.log");
        }

        static void StartNewSession(int pid)
        {
            _sessionId = DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff", CultureInfo.InvariantCulture);
            _sessionStartUtc = DateTime.UtcNow;
            _pid = pid;
            _state = EditorApplication.isPlayingOrWillChangePlaymode ? "PlayMode" : "EditMode";
            _capNoted = false;
            Interlocked.Exchange(ref _mainAliveTicks, DateTime.UtcNow.Ticks);

            var header =
                "==== Cosmic Shore crash-detector journal ====\n" +
                $"session: {_sessionId}\n" +
                $"started: {Iso(_sessionStartUtc)} UTC\n" +
                $"unity:   {_unityVersion}\n" +
                $"pid:     {pid}\n" +
                "Errors, exceptions and asserts land here as they happen; a background thread\n" +
                "flushes after every entry so the tail survives a hard crash.\n\n";
            try
            {
                File.WriteAllText(JournalPath, header);
                _journalBytes = header.Length;
            }
            catch { _journalBytes = 0; }

            WriteSentinel(cleanExit: false);
        }

        static void AdoptSession(SessionSentinel previous, int pid)
        {
            _sessionId = string.IsNullOrEmpty(previous.sessionId) ? "unknown" : previous.sessionId;
            _sessionStartUtc = TryParseIso(previous.startedUtc, out var started) ? started : DateTime.UtcNow;
            _pid = pid;
            _state = EditorApplication.isPlayingOrWillChangePlaymode ? "PlayMode" : "EditMode";
            _capNoted = false;
            Interlocked.Exchange(ref _mainAliveTicks, DateTime.UtcNow.Ticks);

            try { _journalBytes = File.Exists(JournalPath) ? new FileInfo(JournalPath).Length : 0L; }
            catch { _journalBytes = 0; }

            Enqueue(Marker("domain reload complete — session re-adopted"));
            WriteSentinel(cleanExit: false);
        }

        static void Hook()
        {
            if (_hooked) return;
            _hooked = true;
            Application.logMessageReceivedThreaded += OnLogMessage;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            EditorApplication.quitting += OnQuitting;
            EditorApplication.update += OnEditorUpdate;
        }

        static void Unhook()
        {
            if (!_hooked) return;
            _hooked = false;
            Application.logMessageReceivedThreaded -= OnLogMessage;
            AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            EditorApplication.quitting -= OnQuitting;
            EditorApplication.update -= OnEditorUpdate;
        }

        static void OnBeforeAssemblyReload()
        {
            Enqueue(Marker("domain reload begins (script compilation / play-mode transition)"));
            // A crash DURING the reload then reads as "last state: DomainReload" in the report.
            _state = "DomainReload";
            WriteSentinel(cleanExit: false);
            // The writer thread deliberately KEEPS RUNNING through the managed-callbacks phase:
            // the 2026-08-21 'Run managed callbacks' hangs lived exactly here (a LATER
            // beforeAssemblyReload handler blocking forever), and a watchdog that stands down
            // at reload-begin can never dump the deadlock it exists to name. The domain unload
            // aborts the thread — WriterLoop catches ThreadAbortException and flushes in its
            // finally — so heartbeats and the hang-dump check simply run until the domain
            // actually goes down. (The full Shutdown still runs on editor quit.)
        }

        static void OnQuitting()
        {
            Enqueue(Marker("editor quitting (clean exit)"));
            Shutdown("Quit", cleanExit: true);
        }

        /// <summary>Stops the writer (draining the queue), stamps the sentinel, releases hooks.</summary>
        static void Shutdown(string finalState, bool cleanExit)
        {
            _state = finalState;
            _running = false;
            Signal.Set();
            try { _writer?.Join(2000); } catch { }
            _writer = null;
            WriteSentinel(cleanExit);
            Unhook();
        }

        // ── Capture (any thread — must stay allocation-light and must never throw) ───────────────

        static void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            if (!_running) return;
            if (type == LogType.Log) return;
            if (type == LogType.Warning && !_captureWarnings) return;

            try
            {
                lock (RateLock)
                {
                    long second = DateTime.UtcNow.Ticks / TimeSpan.TicksPerSecond;
                    if (second != _rateSecond)
                    {
                        if (_suppressed > 0)
                        {
                            Queue.Enqueue($"        ... {_suppressed} further entries suppressed (rate cap {MaxEntriesPerSecond}/s)");
                            _suppressed = 0;
                        }
                        _rateSecond = second;
                        _rateCount = 0;
                    }
                    if (++_rateCount > MaxEntriesPerSecond)
                    {
                        _suppressed++;
                        return;
                    }
                }

                Enqueue(FormatEntry(condition, stackTrace, type));
            }
            catch { /* never throw back into the log pipeline */ }
        }

        static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                var ex = e.ExceptionObject as Exception;
                var text = ex == null
                    ? $"UNHANDLED APPDOMAIN EXCEPTION: {e.ExceptionObject}"
                    : $"UNHANDLED APPDOMAIN EXCEPTION: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}";
                Enqueue(FormatRaw(text));
            }
            catch { }
        }

        static void OnPlayModeChanged(PlayModeStateChange change)
        {
            switch (change)
            {
                case PlayModeStateChange.ExitingEditMode:
                    Enqueue(Marker("entering play mode"));
                    break;
                case PlayModeStateChange.EnteredPlayMode:
                    _state = "PlayMode";
                    Enqueue(Marker("play mode running"));
                    break;
                case PlayModeStateChange.ExitingPlayMode:
                    Enqueue(Marker("leaving play mode"));
                    break;
                case PlayModeStateChange.EnteredEditMode:
                    _state = "EditMode";
                    Enqueue(Marker("edit mode restored"));
                    break;
            }
        }

        static void OnEditorUpdate()
            => Interlocked.Exchange(ref _mainAliveTicks, DateTime.UtcNow.Ticks);

        static void Enqueue(string line)
        {
            Queue.Enqueue(line);
            Signal.Set();
        }

        static string FormatEntry(string condition, string stackTrace, LogType type)
        {
            var sb = new StringBuilder(192);
            sb.Append('[').Append(DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture))
              .Append("] [").Append(_state).Append("] ").Append(type).Append(": ").Append(condition);

            int keep = _stackLines;
            if (keep > 0 && !string.IsNullOrEmpty(stackTrace))
            {
                var lines = stackTrace.Split('\n');
                int written = 0;
                for (int i = 0; i < lines.Length && written < keep; i++)
                {
                    var line = lines[i].TrimEnd();
                    if (line.Length == 0) continue;
                    sb.Append("\n        ").Append(line);
                    written++;
                }
                if (lines.Length > keep) sb.Append("\n        ...");
            }
            return sb.ToString();
        }

        static string FormatRaw(string text)
            => $"[{DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture)}] [{_state}] {text}";

        static string Marker(string text)
            => $"[{DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture)}] ---- {text} ----";

        // ── The writer thread ────────────────────────────────────────────────────────────────────

        static void StartWriter()
        {
            _running = true;
            _writer = new Thread(WriterLoop)
            {
                Name = "CrashDetector.Writer",
                IsBackground = true,
                Priority = System.Threading.ThreadPriority.BelowNormal,
            };
            _writer.Start();
        }

        static void WriterLoop()
        {
            StreamWriter journal = null;
            try
            {
                journal = OpenJournal();
                long lastBeatTicks = 0;
                while (_running)
                {
                    Signal.WaitOne(500);
                    Drain(journal);

                    long now = DateTime.UtcNow.Ticks;
                    if (now - lastBeatTicks >= _heartbeatSeconds * TimeSpan.TicksPerSecond)
                    {
                        WriteSentinel(cleanExit: false);
                        lastBeatTicks = now;
                    }

                    CheckMainThreadStall(now);
                }
                Drain(journal);
            }
            catch (ThreadAbortException) { }
            catch { /* IO trouble must never surface as an editor exception */ }
            finally
            {
                try { journal?.Flush(); journal?.Dispose(); } catch { }
            }
        }

        static StreamWriter OpenJournal()
        {
            // Shared so a human can open (or even delete) the live journal without wedging us.
            var fs = new FileStream(JournalPath, FileMode.Append, FileAccess.Write,
                                    FileShare.ReadWrite | FileShare.Delete);
            return new StreamWriter(fs, new UTF8Encoding(false));
        }

        // ── Hang dump (writer thread) ────────────────────────────────────────────────────────────

        static void CheckMainThreadStall(long nowTicks)
        {
            int threshold = _hangDumpSeconds;
            if (threshold <= 0) return;

            long aliveTicks = Interlocked.Read(ref _mainAliveTicks);
            double staleSec = (nowTicks - aliveTicks) / (double)TimeSpan.TicksPerSecond;

            if (staleSec >= threshold)
            {
                if (!_mainWasStale)
                {
                    _mainWasStale = true;
                    _staleSinceTicks = aliveTicks;
                }
                if (!_hangDumpWritten && _isWindowsEditor)
                {
                    _hangDumpWritten = true; // one attempt per session, success or not
                    TryWriteHangDump(staleSec);
                }
            }
            else if (_mainWasStale)
            {
                _mainWasStale = false;
                double total = (aliveTicks - _staleSinceTicks) / (double)TimeSpan.TicksPerSecond;
                Enqueue(Marker($"main thread stalled ~{total:F0}s and RECOVERED — slow, not hung"));
            }
        }

        /// <summary>
        /// Writes a minidump of the live, hung editor from this background thread. Thread
        /// stacks + module list (typically a few MB) — enough to name the module the main
        /// thread is stuck in, which is the one fact a Task-Manager kill destroys. Best-effort
        /// by design: a watchdog that crashes the patient is worse than none.
        /// </summary>
        static void TryWriteHangDump(double staleSeconds)
        {
            try
            {
                var path = Path.Combine(RootDir, $"HangDump-{_sessionId}.dmp");
                using var proc = Process.GetCurrentProcess();
                using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
                // 0x0 MiniDumpNormal | 0x100 WithProcessThreadData | 0x1000 WithThreadInfo.
                const int dumpType = 0x0 | 0x100 | 0x1000;
                bool ok = MiniDumpWriteDump(proc.Handle, (uint)_pid, fs.SafeFileHandle, dumpType,
                                            IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                Enqueue(ok
                    ? Marker($"main thread unresponsive ~{staleSeconds:F0}s — wrote hang dump: {path} " +
                             "(open in Visual Studio or WinDbg; the main thread's stack names the deadlock)")
                    : Marker($"main thread unresponsive ~{staleSeconds:F0}s — MiniDumpWriteDump failed " +
                             $"(Win32 error {Marshal.GetLastWin32Error()})"));
            }
            catch (Exception e)
            {
                Enqueue(Marker($"hang dump attempt failed: {e.GetType().Name}: {e.Message}"));
            }
        }

        static void Drain(StreamWriter journal)
        {
            bool wrote = false;
            long cap = Interlocked.Read(ref _maxJournalBytes);
            while (Queue.TryDequeue(out var line))
            {
                if (_journalBytes < cap)
                {
                    journal.WriteLine(line);
                    _journalBytes += line.Length + 1;
                    wrote = true;
                }
                else if (!_capNoted)
                {
                    journal.WriteLine(Marker("journal size cap reached — further entries dropped"));
                    _capNoted = true;
                    wrote = true;
                }
            }
            if (wrote) journal.Flush();
        }

        // ── Sentinel ─────────────────────────────────────────────────────────────────────────────

        static void WriteSentinel(bool cleanExit)
        {
            var mainAlive = new DateTime(Interlocked.Read(ref _mainAliveTicks), DateTimeKind.Utc);
            var text =
                $"sessionId={_sessionId}\n" +
                $"pid={_pid}\n" +
                $"unity={_unityVersion}\n" +
                $"startedUtc={Iso(_sessionStartUtc)}\n" +
                $"lastHeartbeatUtc={Iso(DateTime.UtcNow)}\n" +
                $"mainThreadUtc={Iso(mainAlive)}\n" +
                $"state={_state}\n" +
                $"cleanExit={(cleanExit ? "true" : "false")}\n";
            lock (SentinelLock)
            {
                try { File.WriteAllText(SentinelPath, text); } catch { }
            }
        }

        /// <summary>
        /// The sentinel on disk, parsed leniently: a torn or half-written file is itself evidence
        /// of an abnormal end, so unparseable fields degrade to defaults instead of failing.
        /// </summary>
        sealed class SessionSentinel
        {
            public string sessionId = "";
            public int pid = -1;
            public string unity = "";
            public string startedUtc = "";
            public string lastHeartbeatUtc = "";
            public string mainThreadUtc = "";
            public string state = "unknown";
            public bool cleanExit;

            public static SessionSentinel TryRead(string path)
            {
                try
                {
                    if (!File.Exists(path)) return null;
                    var s = new SessionSentinel();
                    foreach (var line in File.ReadAllLines(path))
                    {
                        int eq = line.IndexOf('=');
                        if (eq <= 0) continue;
                        var key = line[..eq];
                        var value = line[(eq + 1)..].Trim();
                        switch (key)
                        {
                            case "sessionId": s.sessionId = value; break;
                            case "pid": int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out s.pid); break;
                            case "unity": s.unity = value; break;
                            case "startedUtc": s.startedUtc = value; break;
                            case "lastHeartbeatUtc": s.lastHeartbeatUtc = value; break;
                            case "mainThreadUtc": s.mainThreadUtc = value; break;
                            case "state": s.state = value; break;
                            case "cleanExit": s.cleanExit = value == "true"; break;
                        }
                    }
                    return s;
                }
                catch { return null; }
            }
        }

        // ── Crash report ─────────────────────────────────────────────────────────────────────────

        static void WriteCrashReport(SessionSentinel previous)
        {
            var reportPath = Path.Combine(RootDir,
                $"Crash-{DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}.log");

            var sb = new StringBuilder(96 * 1024);
            sb.AppendLine("==== Cosmic Shore editor crash report ====");
            sb.AppendLine($"written:        {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)} (next editor launch after the abnormal exit)");
            sb.AppendLine($"session:        {previous.sessionId}  (pid {previous.pid}, Unity {previous.unity})");
            sb.AppendLine($"started:        {previous.startedUtc} UTC");
            sb.AppendLine($"last heartbeat: {previous.lastHeartbeatUtc} UTC");
            sb.AppendLine($"last state:     {previous.state}");

            if (TryParseIso(previous.lastHeartbeatUtc, out var beat) && TryParseIso(previous.mainThreadUtc, out var main))
            {
                double gap = (beat - main).TotalSeconds;
                sb.AppendLine(gap > 5
                    ? $"main thread:    unresponsive for ~{gap:F0}s at the last heartbeat — likely a hang / freeze that was then killed"
                    : "main thread:    responsive at the last heartbeat — the process ended abruptly (hard crash or kill)");
            }

            var dumpPath = Path.Combine(RootDir, $"HangDump-{previous.sessionId}.dmp");
            if (File.Exists(dumpPath))
                sb.AppendLine($"hang dump:      {dumpPath} — open in Visual Studio / WinDbg; the main thread's stack names the deadlock");

            sb.AppendLine();
            sb.AppendLine("---- Captured errors (crash-detector journal) ----");
            var journalTail = TailOfFile(JournalPath, ReportJournalBytes);
            sb.AppendLine(journalTail ?? "(journal missing)");

            sb.AppendLine("---- Tail of Unity's own log (Editor-prev.log) ----");
            var prevLog = Path.Combine(UnityEditorLogDir(), "Editor-prev.log");
            var logTail = TailOfFile(prevLog, ReportEditorLogBytes);
            sb.AppendLine(logTail ?? $"(not found at {prevLog})");

            try
            {
                File.WriteAllText(reportPath, sb.ToString());
                PruneReports();
                SessionState.SetString(VerdictKey, "crashed");
                SessionState.SetString(ReportKey, reportPath);
                Debug.LogWarning($"[CrashDetector] The previous editor session ended abnormally (last state: {previous.state}). Report: {reportPath}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CrashDetector] Detected an abnormal previous exit but could not write the report: {e.Message}");
            }
        }

        static void PruneReports()
        {
            try
            {
                var files = Directory.GetFiles(RootDir, "Crash-*.log");
                Array.Sort(files, StringComparer.OrdinalIgnoreCase); // timestamped names sort chronologically
                for (int i = 0; i < files.Length - _maxReports; i++)
                    File.Delete(files[i]);

                // Hang dumps ride the same retention — they are MBs each, and a dump whose
                // report has been pruned has lost its context anyway.
                var dumps = Directory.GetFiles(RootDir, "HangDump-*.dmp");
                Array.Sort(dumps, StringComparer.OrdinalIgnoreCase); // session ids sort chronologically
                for (int i = 0; i < dumps.Length - _maxReports; i++)
                    File.Delete(dumps[i]);
            }
            catch { }
        }

        /// <summary>Crash reports on disk, newest first. For the window.</summary>
        public static string[] ListReports()
        {
            try
            {
                if (RootDir == null || !Directory.Exists(RootDir)) return Array.Empty<string>();
                var files = Directory.GetFiles(RootDir, "Crash-*.log");
                Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                Array.Reverse(files);
                return files;
            }
            catch { return Array.Empty<string>(); }
        }

        // ── Helpers ──────────────────────────────────────────────────────────────────────────────

        static string Iso(DateTime utc)
            => utc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);

        static bool TryParseIso(string text, out DateTime utc)
            => DateTime.TryParse(text, CultureInfo.InvariantCulture,
                                 DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out utc);

        static bool IsUnityProcessAlive(int pid)
        {
            if (pid <= 0) return false;
            try
            {
                var p = Process.GetProcessById(pid);
                if (p.HasExited) return false;
                return p.ProcessName.IndexOf("unity", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch { return false; }
        }

        /// <summary>Last <paramref name="maxBytes"/> of a file, trimmed to whole lines. Null if unreadable.</summary>
        static string TailOfFile(string path, int maxBytes)
        {
            try
            {
                if (!File.Exists(path)) return null;
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read,
                                              FileShare.ReadWrite | FileShare.Delete);
                bool truncated = fs.Length > maxBytes;
                if (truncated) fs.Seek(-maxBytes, SeekOrigin.End);
                using var reader = new StreamReader(fs, Encoding.UTF8);
                var text = reader.ReadToEnd();
                if (truncated)
                {
                    int firstNewline = text.IndexOf('\n');
                    if (firstNewline >= 0) text = "(...truncated...)\n" + text[(firstNewline + 1)..];
                }
                return text;
            }
            catch { return null; }
        }

        static string UnityEditorLogDir()
        {
#if UNITY_EDITOR_WIN
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                "Unity", "Editor");
#elif UNITY_EDITOR_OSX
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal),
                                "Library", "Logs", "Unity");
#else
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal),
                                ".config", "unity3d");
#endif
        }

        static void TryDelete(string path)
        {
            try { if (path != null && File.Exists(path)) File.Delete(path); } catch { }
        }
    }
}
