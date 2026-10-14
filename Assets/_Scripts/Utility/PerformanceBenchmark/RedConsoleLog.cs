using UnityEngine;
#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !COSMICSHORE_NO_RED_CONSOLE_LOG
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
#endif

namespace CosmicShore.Utility.PerformanceBenchmark
{
    /// <summary>
    /// Always-on "red console" recorder: every Error, Exception and Assert the game logs while it
    /// plays lands in ONE plain-text file per session — no capture window, nothing to start. Each
    /// distinct message is written once with its time, frame, scene and stack trace; repeats are
    /// counted and summarized at the end of the file (and on every <c>redlog</c> query), so a spam
    /// loop can't bury the first occurrence or fill the disk.
    ///
    /// <para><b>Where.</b> Editor: <c>&lt;project&gt;/Logs/RedConsole/redconsole_*.log</c>. Development
    /// build: <c>persistentDataPath/RedConsole/</c>. The newest 20 sessions are kept.</para>
    ///
    /// <para><b>What it sees.</b> <see cref="Application.logMessageReceivedThreaded"/>, so errors from
    /// any thread are caught. Logs that <c>BenignLobbyLogFilter</c> suppresses never reach Unity's
    /// log callbacks, so they are not recorded either — the file matches the red entries the
    /// Console shows. Writes are queued and flushed by a background thread (every 0.5 s) and on
    /// quit, so the logging path never blocks on disk.</para>
    ///
    /// <para><b>Turning it off.</b> HUD console <c>redlog off</c> (remembered in PlayerPrefs, takes
    /// effect immediately), or the scripting define <c>COSMICSHORE_NO_RED_CONSOLE_LOG</c> to compile
    /// it out. Editor + Development builds only, like the rest of the DiagnosticsHUD tooling.</para>
    ///
    /// HUD console: <c>redlog</c> (status + path), <c>redlog open</c>, <c>redlog copy</c>,
    /// <c>redlog clear</c>, <c>redlog on</c> / <c>redlog off</c>, <c>redlog folder</c>.
    /// </summary>
    public static class RedConsoleLog
    {
        public const string CommandName = "redlog";

#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !COSMICSHORE_NO_RED_CONSOLE_LOG
        const string EnabledPrefKey = "CosmicShore.RedConsoleLog.Enabled";
        const int MaxStackChars = 4000;
        const int MaxKeyChars = 300;
        const int MaxDistinct = 2000;      // past this, new messages are only counted
        const int KeepSessions = 20;

        static readonly object Lock = new();
        static readonly Dictionary<string, Entry> Seen = new(StringComparer.Ordinal);
        static readonly List<string> Order = new();
        static readonly StringBuilder Pending = new(16 * 1024);

        static Thread _writer;
        static volatile bool _running;
        static bool _hooked, _enabled;
        static int _total, _dropped;
        static int _mainThreadId;

        // Main-thread values mirrored for the threaded callback (Time / SceneManager are main-thread only).
        static volatile int _frame;
        static volatile string _scene = "";

        sealed class Entry { public string Type; public int Count; public string First; public string Last; }

        /// <summary>This session's file (null until the first start).</summary>
        public static string CurrentPath { get; private set; }
        public static string Folder { get; private set; }
        public static bool Enabled => _enabled;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            // Domain reload may be off in the editor: start each play session fresh.
            Unhook();
            lock (Lock) { Seen.Clear(); Order.Clear(); Pending.Clear(); _total = 0; _dropped = 0; }
            CurrentPath = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
            DiagnosticsHUD.RegisterCommand(CommandName, HandleCommand);
            Application.quitting -= OnQuit;
            Application.quitting += OnQuit;
            UnityEngine.SceneManagement.SceneManager.activeSceneChanged -= OnSceneChanged;
            UnityEngine.SceneManagement.SceneManager.activeSceneChanged += OnSceneChanged;
            _scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (PlayerPrefs.GetInt(EnabledPrefKey, 1) == 1) Start();
        }

        static void OnSceneChanged(UnityEngine.SceneManagement.Scene _, UnityEngine.SceneManagement.Scene next) => _scene = next.name;

        static void Start()
        {
            _enabled = true;
            try
            {
#if UNITY_EDITOR
                Folder = Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, "Logs", "RedConsole");
#else
                Folder = Path.Combine(Application.persistentDataPath, "RedConsole");
#endif
                Directory.CreateDirectory(Folder);
                PruneOldSessions();
                if (CurrentPath == null)
                {
                    string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
                    CurrentPath = Path.Combine(Folder, $"redconsole_{stamp}.log");
                    File.WriteAllText(CurrentPath,
                        $"# Cosmic Shore red console log — {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}\n" +
                        $"# {Application.productName} {Application.version} · Unity {Application.unityVersion} · {Application.platform} · start scene '{_scene}'\n" +
                        "# Errors, exceptions and asserts only. Each distinct message once; repeat counts in the summary.\n\n");
                }
            }
            catch (Exception e)
            {
                _enabled = false;
                Debug.LogWarning($"[RedConsoleLog] Could not open the log file: {e.Message}");
                return;
            }

            if (!_hooked) { Application.logMessageReceivedThreaded += OnLog; _hooked = true; }
            if (_writer == null)
            {
                _running = true;
                _writer = new Thread(WriterLoop) { IsBackground = true, Name = "RedConsoleLog" };
                _writer.Start();
            }
            EnsureFrameTicker();
        }

        static void Stop()
        {
            _enabled = false;
            Unhook();
        }

        static void Unhook()
        {
            if (_hooked) { Application.logMessageReceivedThreaded -= OnLog; _hooked = false; }
            _running = false;
            _writer = null;   // the old thread exits on its next tick
            Flush(writeSummary: true);
        }

        static void OnQuit()
        {
            Unhook();
        }

        static void OnLog(string condition, string stackTrace, LogType type)
        {
            if (!_enabled) return;
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;

            condition ??= "";
            string key = type + ": " + (condition.Length > MaxKeyChars ? condition.Substring(0, MaxKeyChars) : condition);
            string now = DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
            bool main = Thread.CurrentThread.ManagedThreadId == _mainThreadId;
            string where = $"frame {_frame} · scene '{_scene}'{(main ? "" : " · thread " + Thread.CurrentThread.ManagedThreadId)}";

            lock (Lock)
            {
                _total++;
                if (Seen.TryGetValue(key, out Entry e)) { e.Count++; e.Last = now; return; }
                if (Order.Count >= MaxDistinct) { _dropped++; return; }
                Seen[key] = new Entry { Type = type.ToString(), Count = 1, First = now, Last = now };
                Order.Add(key);

                string stack = string.IsNullOrEmpty(stackTrace) ? "(no stack trace)" :
                    stackTrace.Length > MaxStackChars ? stackTrace.Substring(0, MaxStackChars) + "\n…" : stackTrace.TrimEnd();
                Pending.Append('[').Append(now).Append("] ").Append(type.ToString().ToUpperInvariant())
                       .Append(" #").Append(Order.Count).Append(" · ").Append(where).Append('\n')
                       .Append(condition.TrimEnd()).Append('\n')
                       .Append(stack).Append("\n\n");
            }
        }

        static void WriterLoop()
        {
            while (_running)
            {
                Thread.Sleep(500);
                Flush(writeSummary: false);
            }
        }

        static void Flush(bool writeSummary)
        {
            string text;
            lock (Lock)
            {
                if (writeSummary && Order.Count > 0) Pending.Append(Summary());
                if (Pending.Length == 0 || CurrentPath == null) return;
                text = Pending.ToString();
                Pending.Clear();
                try { File.AppendAllText(CurrentPath, text); }
                catch { /* never log from here: it would feed back into OnLog */ }
            }
        }

        static string Summary()
        {
            var sb = new StringBuilder();
            sb.Append("── summary @ ").Append(DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture))
              .Append(": ").Append(_total).Append(" red entries, ").Append(Order.Count).Append(" distinct");
            if (_dropped > 0) sb.Append(", ").Append(_dropped).Append(" past the distinct cap (counted, not written)");
            sb.Append(" ──\n");
            for (int i = 0; i < Order.Count; i++)
            {
                Entry e = Seen[Order[i]];
                sb.Append("  #").Append(i + 1).Append(" ×").Append(e.Count).Append("  first ").Append(e.First)
                  .Append(" last ").Append(e.Last).Append("  ").Append(FirstLine(Order[i])).Append('\n');
            }
            return sb.Append('\n').ToString();
        }

        static string FirstLine(string s)
        {
            int n = s.IndexOf('\n');
            return n < 0 ? s : s.Substring(0, n);
        }

        static void PruneOldSessions()
        {
            try
            {
                var files = new List<FileInfo>(new DirectoryInfo(Folder).GetFiles("redconsole_*.log"));
                files.Sort((a, b) => b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc));
                for (int i = KeepSessions - 1; i < files.Count; i++) files[i].Delete();
            }
            catch { /* best effort */ }
        }

        // ── frame mirror: a tiny hidden component so the threaded callback can stamp the frame ──
        static void EnsureFrameTicker()
        {
            if (FrameTicker.Instance != null) return;
            var go = new GameObject("[RedConsoleLog]") { hideFlags = HideFlags.HideInHierarchy };
            UnityEngine.Object.DontDestroyOnLoad(go);
            FrameTicker.Instance = go.AddComponent<FrameTicker>();
        }

        sealed class FrameTicker : MonoBehaviour
        {
            public static FrameTicker Instance;
            void Update() => _frame = Time.frameCount;
            void OnDestroy() { if (Instance == this) Instance = null; }
        }

        // ── HUD console ──
        static string HandleCommand(string[] args)
        {
            string sub = args is { Length: > 0 } ? args[0].ToLowerInvariant() : "";
            switch (sub)
            {
                case "":
                case "status":
                case "path":
                {
                    int total, distinct;
                    lock (Lock) { total = _total; distinct = Order.Count; }
                    return $"redlog {(_enabled ? "ON" : "OFF")}: {total} red entries ({distinct} distinct) → {CurrentPath ?? "(no file yet)"}  · redlog open|copy|clear|folder|on|off";
                }
                case "on":
                    PlayerPrefs.SetInt(EnabledPrefKey, 1); PlayerPrefs.Save();
                    if (!_enabled) Start();
                    return _enabled ? $"redlog ON → {CurrentPath}" : "redlog could not start (see warning)";
                case "off":
                    PlayerPrefs.SetInt(EnabledPrefKey, 0); PlayerPrefs.Save();
                    Stop();
                    return "redlog OFF (remembered; 'redlog on' to resume)";
                case "copy":
                    Flush(writeSummary: true);
                    if (CurrentPath == null || !File.Exists(CurrentPath)) return "no red console file yet";
                    GUIUtility.systemCopyBuffer = File.ReadAllText(CurrentPath);
                    return $"copied {Path.GetFileName(CurrentPath)} ({new FileInfo(CurrentPath).Length / 1024} KB) to the clipboard";
                case "open":
                    Flush(writeSummary: true);
                    if (CurrentPath == null || !File.Exists(CurrentPath)) return "no red console file yet";
                    Application.OpenURL("file:///" + CurrentPath.Replace('\\', '/'));
                    return "opened " + CurrentPath;
                case "folder":
                    if (Folder == null) return "no folder yet";
                    Application.OpenURL("file:///" + Folder.Replace('\\', '/'));
                    return "opened " + Folder;
                case "clear":
                    lock (Lock)
                    {
                        Seen.Clear(); Order.Clear(); Pending.Clear(); _total = 0; _dropped = 0;
                        try { if (CurrentPath != null) File.WriteAllText(CurrentPath, $"# cleared {DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture)}\n\n"); }
                        catch (Exception e) { return "clear failed: " + e.Message; }
                    }
                    return "redlog cleared → " + CurrentPath;
                default:
                    return "redlog [status|open|copy|clear|folder|on|off]";
            }
        }
#endif
    }
}
