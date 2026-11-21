using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Amoebius;
using CosmicShore.Engine;
using CosmicShore.Utility;

namespace CosmicShore.Player
{
    /// <summary>
    /// The logs of an ONLINE test in Amoebius, written by the game itself so nobody pastes a console again
    /// (Yash, 2026-10-15: "auto collect the logs whenever we test the game, just like bug hunt and
    /// performance"). The launcher passes <c>COSMIC_SHORE_NET_LOG_DIR</c> to START ONLINE and + PLAYER 2
    /// (and <c>COSMIC_SHORE_NET_PEER</c>: <c>main</c>, <c>p2</c>); when the game closes it writes ONE file
    /// there, <c>net_&lt;utc&gt;_&lt;peer&gt;.json</c>: its <see cref="NetSessionRecord"/> (role, counters,
    /// verdict, the lifecycle timeline) plus its console lines, in the format the Unity editor writes at Play
    /// stop (<see cref="NetTestLogFormat"/>). The launcher's after-run notification offers SEND TO CLAUDE.
    ///
    /// <para>What counts as a multiplayer test is the editor's rule: a second player (any peer but
    /// <c>main</c>) always; <c>main</c> when its record shows another player. A solo online session writes
    /// nothing.</para>
    /// </summary>
    public static class NetTestLog
    {
        const int MaxLines = 5000;
        const int MaxMessageChars = 1000;
        const int MaxFrameChars = 300;

        static readonly object s_gate = new();
        static readonly Queue<NetTestLogFormat.Line> s_lines = new();
        static readonly System.Diagnostics.Stopwatch s_clock = new();
        static int s_dropped, s_errors, s_warnings;
        static string s_dir, s_peer;
        static DateTime s_started;
        static bool s_written;

        /// <summary>Starts keeping the console when the launcher asked for a log and the run is online. Before the game boots.</summary>
        public static void Begin(LogCounter log)
        {
            var dir = Environment.GetEnvironmentVariable("COSMIC_SHORE_NET_LOG_DIR");
            if (string.IsNullOrWhiteSpace(dir) || Environment.GetEnvironmentVariable("COSMIC_SHORE_NET") == "off") return;
            s_dir = Path.GetFullPath(dir);
            s_peer = NetTestLogFormat.PeerName(Environment.GetEnvironmentVariable("COSMIC_SHORE_NET_PEER"));
            s_started = DateTime.UtcNow;
            s_clock.Restart();
            log.Tap = Capture;
        }

        /// <summary>Any thread. Never throws: a log hook that throws would recurse into the console.</summary>
        static void Capture(LogType type, string message, Exception exception)
        {
            try
            {
                var at = type == LogType.Log ? null
                    : NetTestLogFormat.Clip(exception?.StackTrace?.Split('\n').Select(f => f.Trim()).FirstOrDefault(f => f.Length > 0), MaxFrameChars);
                var msg = exception != null && string.IsNullOrEmpty(message) ? exception.GetType().Name + ": " + exception.Message : message;
                var line = new NetTestLogFormat.Line((float)s_clock.Elapsed.TotalSeconds, TypeName(type), NetTestLogFormat.Clip(msg ?? "", MaxMessageChars), at);
                lock (s_gate)
                {
                    if (s_written) return;
                    if (type == LogType.Warning) s_warnings++;
                    else if (type != LogType.Log) s_errors++;
                    s_lines.Enqueue(line);
                    while (s_lines.Count > MaxLines) { s_lines.Dequeue(); s_dropped++; }
                }
            }
            catch { }
        }

        /// <summary>
        /// The game is closing (before its quit sequence tears the network down, so the role still answers) or
        /// crashed: write this peer's file if this was a multiplayer test. Once per run; returns the path or null.
        /// </summary>
        public static string Write(string exit, Exception crash = null)
        {
            if (s_dir == null) return null;
            NetTestLogFormat.Line[] lines;
            int dropped, errors, warnings;
            lock (s_gate)
            {
                if (s_written) return null;
                s_written = true;
                lines = s_lines.ToArray();
                s_lines.Clear();
                (dropped, errors, warnings) = (s_dropped, s_errors, s_warnings);
            }
            try
            {
                var record = NetSessionRecorder.BuildRecord();
                string why = s_peer != "main"
                    ? "second player (" + s_peer + ")"
                    : NetTestLogFormat.MultiplayerEvidence(record.role, record.lifecycle?.Select(ev => ((string)ev.e, (string)ev.detail)));
                if (why == null) return null;   // solo online play: nothing to collect
                var info = new NetTestLogFormat.Info
                {
                    Peer = s_peer, Why = why, Exit = exit, Crash = crash?.ToString(), User = Environment.UserName,
                    Branch = Environment.GetEnvironmentVariable("COSMIC_SHORE_BRANCH"),
                    Commit = Environment.GetEnvironmentVariable("COSMIC_SHORE_COMMIT"),
                    Started = s_started, Ended = DateTime.UtcNow,
                };
                string json = NetTestLogFormat.Build(JsonUtility.ToJson(record, true), info, lines, dropped, errors, warnings);
                Directory.CreateDirectory(s_dir);
                string path = Path.Combine(s_dir, NetTestLogFormat.FileName(s_started, s_peer));
                File.WriteAllText(path, json, new UTF8Encoding(false));
                Console.WriteLine($"[net-log] multiplayer test log written: {path}");
                return path;
            }
            catch (Exception e)
            {
                // Diagnostics never take the game down with them; the line says why there is no file.
                Console.WriteLine($"[net-log] could not write the multiplayer test log: {e.GetType().Name}: {e.Message}");
                return null;
            }
        }

        static string TypeName(LogType t) => t switch
        {
            LogType.Error => "error",
            LogType.Assert => "assert",
            LogType.Warning => "warning",
            LogType.Exception => "exception",
            _ => "log",
        };
    }
}
