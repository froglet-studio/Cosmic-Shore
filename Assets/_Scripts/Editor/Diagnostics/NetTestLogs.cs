using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using CosmicShore.Editor.Froglet;
using CosmicShore.Utility;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// The logs of a MULTIPLAYER play test, written by themselves so nobody pastes a console again
    /// (Yash, 2026-10-15: "auto collect the logs whenever we test the game, just like bug hunt and
    /// performance"). Every editor taking part - the main editor and each MPPM virtual player - keeps its
    /// own console for the play session and, when Play stops, writes ONE file:
    /// <c>Logs/NetSessions/net_&lt;utc&gt;_&lt;peer&gt;.json</c> in the MAIN project (a virtual player runs
    /// from <c>Library/VP/&lt;id&gt;/</c> and writes into its parent project, so one folder holds the whole
    /// test). The file is that peer's <see cref="NetSessionRecord"/> (role, counters, verdict, the
    /// lifecycle timeline) plus its console lines.
    ///
    /// <para><see cref="BugHuntReports"/> picks the files up as the <c>net</c> kind and the after-play
    /// question (<see cref="BugHuntAutoSend"/>) sends them to <c>bugledger/&lt;user&gt;</c> with the errors;
    /// Claude reads them with <c>Tools/Build/net_logs.py</c>, which lines the peers of one test up into one
    /// timeline. No window, no setting.</para>
    ///
    /// <para>What counts as a multiplayer test: a virtual player always; the main editor when its record
    /// shows another player (a client approved other than its own, a client left, a leaver handed to the
    /// AI, a join refused or bounced, the role client or spectator) or when a virtual player's editor is
    /// running. A solo online session (presence lobby only) writes nothing.</para>
    /// </summary>
    [InitializeOnLoad]
    public static class NetTestLogs
    {
        public const string FolderName = "NetSessions";
        public const string Schema = "cosmicshore.nettestlog.v1";

        const int MaxLines = 5000;
        const int MaxMessageChars = 1000;
        const int MaxFrameChars = 300;
        const int MaxFileBytes = 450 * 1024;      // under the sender's 512 KB per-file cap
        const string WaitKey = "NetTestLogs.AskNotBefore";
        const string WroteKey = "NetTestLogs.WroteThisPlay";

        /// <summary>How long the main editor waits for the virtual players' files before asking.</summary>
        public const double PeerGraceSeconds = 4.0;

        struct Line
        {
            public float t;
            public LogType type;
            public string msg;
            public string at;
        }

        static readonly object Gate = new();
        static readonly Queue<Line> Lines = new();
        static int _dropped, _errors, _warnings;
        static bool _capturing;
        static DateTime _startedUtc;
        static readonly System.Diagnostics.Stopwatch Clock = new();

        static NetTestLogs()
        {
            if (Application.isBatchMode) return;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            // With domain reload on entering Play, this static constructor runs again inside the play
            // domain, before the first scene's Awake: start there so nothing early is lost.
            if (EditorApplication.isPlayingOrWillChangePlaymode && !_capturing) Begin();
        }

        static void OnPlayModeChanged(PlayModeStateChange change)
        {
            try
            {
                if (change == PlayModeStateChange.ExitingEditMode) Begin();
                else if (change == PlayModeStateChange.EnteredPlayMode && !_capturing) Begin();
                else if (change == PlayModeStateChange.ExitingPlayMode) Finish();
            }
            catch (Exception e)
            {
                // Diagnostics must never break Play (the ledger's own doctrine).
                Debug.LogWarning($"[NetTestLogs] {change} failed: {e.GetType().Name}: {e.Message}");
            }
        }

        static void Begin()
        {
            lock (Gate)
            {
                Lines.Clear();
                _dropped = _errors = _warnings = 0;
                _startedUtc = DateTime.UtcNow;
                Clock.Restart();
                if (!_capturing) Application.logMessageReceivedThreaded += OnLog;
                _capturing = true;
            }
            SessionState.SetBool(WroteKey, false);
        }

        // Any thread. Allocation-light, never throws.
        static void OnLog(string message, string stack, LogType type)
        {
            try
            {
                var line = new Line
                {
                    t = (float)Clock.Elapsed.TotalSeconds,
                    type = type,
                    msg = Clip(message, MaxMessageChars),
                    at = type == LogType.Log ? null : Clip(FirstGameFrame(stack), MaxFrameChars),
                };
                lock (Gate)
                {
                    if (!_capturing) return;
                    if (type == LogType.Warning) _warnings++;
                    else if (type != LogType.Log) _errors++;
                    Lines.Enqueue(line);
                    while (Lines.Count > MaxLines) { Lines.Dequeue(); _dropped++; }
                }
            }
            catch { /* a log hook that throws would recurse into the console */ }
        }

        /// <summary>
        /// Play is stopping: the scene, the NetworkManager and the record's providers still answer. Decide
        /// whether this was a multiplayer test and, if so, write this peer's file.
        /// </summary>
        static void Finish()
        {
            Line[] lines;
            int dropped, errors, warnings;
            lock (Gate)
            {
                if (!_capturing) return;
                _capturing = false;
                Application.logMessageReceivedThreaded -= OnLog;
                lines = Lines.ToArray();
                Lines.Clear();
                dropped = _dropped; errors = _errors; warnings = _warnings;
            }

            var record = NetSessionRecorder.BuildRecord();
            bool clone = IsVirtualPlayer(Application.dataPath, out var vpId);
            string why = clone ? "MPPM virtual player" : MultiplayerEvidence(record);
            if (why == null && AnyVirtualPlayerRunning(MainProjectRoot(Application.dataPath)))
                why = "MPPM virtual players running";
            if (why == null) return;   // solo play: nothing to collect

            string peer = clone ? PeerLabel(vpId) : "main";
            string json = Build(record, peer, clone, why, lines, dropped, errors, warnings);
            string dir = Path.Combine(MainProjectRoot(Application.dataPath), "Logs", FolderName);
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, $"net_{_startedUtc:yyyyMMdd_HHmmss}_{BugHuntReports.Slug(peer)}.json");
            File.WriteAllText(path, json, new UTF8Encoding(false));

            if (!clone)
            {
                // Hold the after-play question until the virtual players' files have landed (they stop
                // Play a moment after the main editor), so the question and the send see the whole test.
                SessionState.SetBool(WroteKey, true);
                SessionState.SetFloat(WaitKey, (float)(EditorApplication.timeSinceStartup + PeerGraceSeconds));
            }
        }

        /// <summary>True when the main editor wrote a multiplayer log for the play test that just ended.</summary>
        public static bool WroteThisPlay => SessionState.GetBool(WroteKey, false);

        /// <summary>Editor time before which the after-play question should wait for peer files.</summary>
        public static double AskNotBefore => SessionState.GetFloat(WaitKey, 0f);

        /// <summary>Whether this editor is an MPPM virtual player (it runs from the main project's <c>Library/VP/&lt;id&gt;/</c>).</summary>
        public static bool IsClone => IsVirtualPlayer(Application.dataPath, out _);

        // ── pure helpers (EditMode-testable) ──────────────────────────────────────────────

        /// <summary>
        /// Why <paramref name="record"/> is a multiplayer session, or null. Only evidence of ANOTHER player
        /// counts: the host approving its own client 0 and party transitions happen in solo online play too.
        /// </summary>
        public static string MultiplayerEvidence(NetSessionRecord record)
        {
            if (record == null) return null;
            if (record.role == "client" || record.role == "spectator") return "role " + record.role;
            if (record.lifecycle == null) return null;
            foreach (var ev in record.lifecycle)
            {
                switch (ev.e)
                {
                    case "clientApproved":
                        var id = (ev.detail ?? "").Split(' ')[0];
                        if (id.Length > 0 && id != "0") return "client " + id + " approved";
                        break;
                    case "clientLeft":
                    case "leaverToAI":
                    case "refused":
                    case "bounce":
                        return ev.e;
                }
            }
            return null;
        }

        /// <summary>The project a virtual player belongs to: the part of its data path before <c>/Library/VP/</c>.</summary>
        public static string MainProjectRoot(string dataPath)
        {
            var p = (dataPath ?? "").Replace('\\', '/');
            int at = p.IndexOf("/Library/VP/", StringComparison.OrdinalIgnoreCase);
            if (at > 0) return p.Substring(0, at);
            var parent = Path.GetDirectoryName(p.TrimEnd('/'));
            return string.IsNullOrEmpty(parent) ? p : parent.Replace('\\', '/');
        }

        public static bool IsVirtualPlayer(string dataPath, out string vpId)
        {
            vpId = null;
            var p = (dataPath ?? "").Replace('\\', '/');
            int at = p.IndexOf("/Library/VP/", StringComparison.OrdinalIgnoreCase);
            if (at < 0) return false;
            var rest = p.Substring(at + "/Library/VP/".Length);
            int slash = rest.IndexOf('/');
            vpId = slash > 0 ? rest.Substring(0, slash) : rest;
            return vpId.Length > 0;
        }

        /// <summary>
        /// The virtual player's MPPM tags (P2, P3 ...) when the engine exposes them, else its VP folder.
        /// Read by reflection: <c>Unity.Multiplayer.PlayMode.CurrentPlayer</c> moved into the engine in MPPM 2.0,
        /// and diagnostics must not stop compiling on an editor that lacks it.
        /// </summary>
        static string PeerLabel(string vpId)
        {
            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var t = asm.GetType("Unity.Multiplayer.PlayMode.CurrentPlayer", false);
                    if (t == null) continue;
                    var tags = t.GetProperty("Tags")?.GetValue(null) as System.Collections.IEnumerable
                               ?? t.GetMethod("ReadOnlyTags")?.Invoke(null, null) as System.Collections.IEnumerable;
                    if (tags == null) break;
                    var parts = new List<string>();
                    foreach (var tag in tags) if (tag != null) parts.Add(tag.ToString());
                    if (parts.Count > 0) return string.Join("-", parts);
                    break;
                }
            }
            catch { /* the folder name is good enough */ }
            return "vp-" + vpId;
        }

        /// <summary>A virtual player's editor holds <c>Temp/UnityLockfile</c> in its VP folder while it runs.</summary>
        static bool AnyVirtualPlayerRunning(string mainRoot)
        {
            try
            {
                var vp = Path.Combine(mainRoot, "Library", "VP");
                if (!Directory.Exists(vp)) return false;
                foreach (var dir in Directory.GetDirectories(vp))
                    if (File.Exists(Path.Combine(dir, "Temp", "UnityLockfile"))) return true;
            }
            catch { /* unreadable: not a multiplayer test as far as we can tell */ }
            return false;
        }

        /// <summary>The first stack frame that is not Unity's logging plumbing.</summary>
        static string FirstGameFrame(string stack)
        {
            if (string.IsNullOrEmpty(stack)) return null;
            foreach (var raw in stack.Split('\n'))
            {
                var f = raw.Trim();
                if (f.Length == 0) continue;
                if (f.StartsWith("UnityEngine.Debug", StringComparison.Ordinal) ||
                    f.StartsWith("UnityEngine.Logger", StringComparison.Ordinal) ||
                    f.StartsWith("UnityEngine.DebugLogHandler", StringComparison.Ordinal) ||
                    f.StartsWith("CosmicShore.Utility.CSDebug", StringComparison.Ordinal) ||
                    f.Contains("StackTraceUtility")) continue;
                return f;
            }
            return null;
        }

        static string Clip(string s, int max) => s == null ? null : s.Length <= max ? s : s.Substring(0, max) + "...";

        static string TypeName(LogType t) => t switch
        {
            LogType.Error => "error",
            LogType.Assert => "assert",
            LogType.Warning => "warning",
            LogType.Exception => "exception",
            _ => "log",
        };

        static string Build(NetSessionRecord record, string peer, bool clone, string why, Line[] lines,
                            int dropped, int errors, int warnings)
        {
            string recordJson = JsonUtility.ToJson(record, true);
            var (branch, commit) = GitHead(MainProjectRoot(Application.dataPath));
            // Newest lines win: the end of a session is where the failure is. Drop the oldest until it fits.
            int first = 0;
            string text;
            while (true)
            {
                text = Compose(recordJson, peer, clone, why, branch, commit, lines, first, dropped + first, errors, warnings);
                if (Encoding.UTF8.GetByteCount(text) <= MaxFileBytes || first >= lines.Length) return text;
                first += Math.Max(50, (lines.Length - first) / 5);
                if (first > lines.Length) first = lines.Length;
            }
        }

        static string Compose(string recordJson, string peer, bool clone, string why, string branch, string commit,
                              Line[] lines, int first, int dropped, int errors, int warnings)
        {
            var sb = new StringBuilder(64 * 1024);
            sb.Append("{\n");
            Field(sb, "schema", Schema); Field(sb, "engine", "unity"); Field(sb, "peer", peer);
            sb.Append("  \"isVirtualPlayer\": ").Append(clone ? "true" : "false").Append(",\n");
            Field(sb, "why", why); Field(sb, "user", Environment.UserName);
            Field(sb, "unityVersion", Application.unityVersion);
            Field(sb, "branch", branch); Field(sb, "commit", commit);
            Field(sb, "playStartedUtc", _startedUtc.ToString("o", CultureInfo.InvariantCulture));
            Field(sb, "playEndedUtc", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
            sb.Append("  \"record\": ").Append(recordJson).Append(",\n");
            sb.Append("  \"console\": {\n");
            sb.Append("    \"errors\": ").Append(errors).Append(",\n");
            sb.Append("    \"warnings\": ").Append(warnings).Append(",\n");
            sb.Append("    \"dropped\": ").Append(dropped).Append(",\n");
            sb.Append("    \"lines\": [");
            for (int i = first; i < lines.Length; i++)
            {
                var l = lines[i];
                sb.Append(i == first ? "\n      " : ",\n      ");
                sb.Append("{\"t\": ").Append(l.t.ToString("0.###", CultureInfo.InvariantCulture));
                sb.Append(", \"type\": \"").Append(TypeName(l.type)).Append('"');
                sb.Append(", \"msg\": ").Append(Quote(l.msg));
                if (l.at != null) sb.Append(", \"at\": ").Append(Quote(l.at));
                sb.Append('}');
            }
            sb.Append("\n    ]\n  }\n}\n");
            return sb.ToString();
        }

        static void Field(StringBuilder sb, string key, string value) =>
            sb.Append("  \"").Append(key).Append("\": ").Append(Quote(value)).Append(",\n");

        static string Quote(string s)
        {
            if (s == null) return "null";
            var sb = new StringBuilder(s.Length + 2);
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.Append('"').ToString();
        }

        /// <summary>The branch and commit the test ran, from the main project's git; nulls when git is absent.</summary>
        static (string branch, string commit) GitHead(string root)
        {
            try
            {
                var branch = FrogletGit.RunIn(root, "rev-parse", "--abbrev-ref", "HEAD");
                var commit = FrogletGit.RunIn(root, "rev-parse", "--short", "HEAD");
                return (branch.Ok ? branch.StdOut.Trim() : null, commit.Ok ? commit.StdOut.Trim() : null);
            }
            catch { return (null, null); }
        }
    }
}
