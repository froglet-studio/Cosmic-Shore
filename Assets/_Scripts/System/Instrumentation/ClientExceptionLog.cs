using System;
using System.Collections.Generic;

namespace CosmicShore.Core
{
    /// <summary>
    /// The errors and exceptions one analytics session has seen, for <c>client_exception</c>
    /// (Shombith 2026-11-07, the Errors page). Fed from <c>Application.logMessageReceivedThreaded</c>,
    /// so <see cref="Observe"/> may run on ANY thread: every member that touches state takes the lock.
    ///
    /// <para><b>No message text ever leaves this class.</b> A log line can hold a player's name, a
    /// session id or a path. Only its type, its first project frame and a short hash of the message
    /// with the digits taken out are kept, so the same fault with a different id or count folds
    /// into one row.</para>
    ///
    /// <para>Throttle: one event per distinct hash per session, at most <see cref="MaxPerSession"/>
    /// per session. A hash is HELD from its first sighting until the facade sends it (a short fold
    /// window, or pause/quit), and repeats in that time are folded into its count.</para>
    ///
    /// Pure string work and a lock; no Unity calls, so the edit-mode tests drive it directly.
    /// </summary>
    public sealed class ClientExceptionLog
    {
        /// <summary>Distinct faults sent per session at most.</summary>
        public const int MaxPerSession = 20;

        /// <summary><c>exception_type</c> of an error logged with Debug.LogError (no exception object).</summary>
        public const string LogErrorType = "LogError";

        /// <summary>What a field says when the log carried nothing to read it from.</summary>
        public const string Unknown = "unknown";

        /// <summary>One fault as sent: the counts are a snapshot taken when it left the hold.</summary>
        public readonly struct Entry
        {
            public readonly string ExceptionType;
            public readonly string TopFrame;
            public readonly string Scene;
            public readonly string MessageHash;
            public readonly int Count;

            public Entry(string exceptionType, string topFrame, string scene, string messageHash, int count)
            {
                ExceptionType = exceptionType;
                TopFrame = topFrame;
                Scene = scene;
                MessageHash = messageHash;
                Count = count;
            }
        }

        sealed class Held
        {
            public string ExceptionType;
            public string TopFrame;
            public string Scene;
            public string MessageHash;
            public int Count;
        }

        readonly object _lock = new();
        readonly Dictionary<string, Held> _byHash = new(StringComparer.Ordinal);
        readonly List<Held> _held = new();

        /// <summary>
        /// Records one error or exception. Returns true when it is a NEW fault this session that is
        /// now held for sending (the caller schedules the send); false for a repeat (folded into its
        /// count) or once <see cref="MaxPerSession"/> faults have been taken.
        /// </summary>
        public bool Observe(string exceptionType, string topFrame, string scene, string messageHash)
        {
            lock (_lock)
            {
                if (_byHash.TryGetValue(messageHash, out var seen))
                {
                    seen.Count++;
                    return false;
                }

                if (_byHash.Count >= MaxPerSession)
                    return false;

                var held = new Held
                {
                    ExceptionType = exceptionType,
                    TopFrame = topFrame,
                    Scene = scene,
                    MessageHash = messageHash,
                    Count = 1
                };
                _byHash.Add(messageHash, held);
                _held.Add(held);
                return true;
            }
        }

        /// <summary>The faults waiting to be sent, with their counts so far; the hold is emptied.</summary>
        public List<Entry> TakeHeld()
        {
            lock (_lock)
            {
                var entries = new List<Entry>(_held.Count);
                foreach (var h in _held)
                    entries.Add(new Entry(h.ExceptionType, h.TopFrame, h.Scene, h.MessageHash, h.Count));
                _held.Clear();
                return entries;
            }
        }

        /// <summary>A new session: every fault may be sent once more.</summary>
        public void Reset()
        {
            lock (_lock)
            {
                _byHash.Clear();
                _held.Clear();
            }
        }

        // ── Reading a log line (pure, any thread) ──────────────────────────────

        /// <summary>
        /// The type of an exception log ("NullReferenceException: Object reference..." gives
        /// "NullReferenceException"), or <see cref="LogErrorType"/> for a Debug.LogError line.
        /// Only a name made of identifier characters is taken, so no message text can leak through.
        /// </summary>
        public static string ExceptionTypeOf(string condition, bool isException)
        {
            if (!isException)
                return LogErrorType;

            if (string.IsNullOrEmpty(condition))
                return "Exception";

            int colon = condition.IndexOf(':');
            string head = colon > 0 ? condition.Substring(0, colon) : condition;
            return IsQualifiedIdentifier(head) ? head : "Exception";
        }

        /// <summary>
        /// The first PROJECT frame of a Unity stack trace as "Namespace.Class.Method": the first line
        /// in a <c>CosmicShore.</c> namespace that is not the logging helper itself. Falls back to the
        /// first frame of any kind, then <see cref="Unknown"/>. Reads both of Unity's shapes:
        /// "CosmicShore.X.Y:Method (args) (at path:line)" (a logged error) and
        /// "CosmicShore.X.Y.Method (args) [0x00000] in path:line" (an exception).
        /// </summary>
        public static string TopFrameOf(string stackTrace)
        {
            if (string.IsNullOrEmpty(stackTrace))
                return Unknown;

            string first = null;
            foreach (var raw in stackTrace.Split('\n'))
            {
                string frame = FrameName(raw);
                if (frame == null)
                    continue;

                first ??= frame;
                if (frame.StartsWith("CosmicShore.", StringComparison.Ordinal) && !IsLoggingFrame(frame))
                    return frame;
            }

            return first ?? Unknown;
        }

        /// <summary>"Namespace.Class.Method" from one stack-trace line, or null when the line is not a frame.</summary>
        static string FrameName(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return null;

            string s = line.Trim();
            if (s.StartsWith("at ", StringComparison.Ordinal))
                s = s.Substring(3).TrimStart();

            int paren = s.IndexOf('(');
            if (paren > 0)
                s = s.Substring(0, paren);

            s = s.Trim().Replace(':', '.');
            return s.Length > 0 && IsQualifiedIdentifier(s.Replace("<", "").Replace(">", "").Replace("`", "").Replace("[", "").Replace("]", "").Replace(",", ""))
                ? s
                : null;
        }

        static bool IsLoggingFrame(string frame) =>
            frame.StartsWith("CosmicShore.Utility.CSDebug.", StringComparison.Ordinal) ||
            frame.StartsWith("CosmicShore.Core.CSDebug.", StringComparison.Ordinal) ||
            frame.StartsWith("CosmicShore.CSDebug.", StringComparison.Ordinal);

        static bool IsQualifiedIdentifier(string s)
        {
            if (string.IsNullOrEmpty(s) || s.Length > 256)
                return false;

            foreach (char c in s)
                if (!(char.IsLetterOrDigit(c) || c == '_' || c == '.' || c == '+'))
                    return false;

            return char.IsLetter(s[0]) || s[0] == '_';
        }

        /// <summary>
        /// A stable 12-hex-digit hash (FNV-1a 64) of the message's first line with every digit taken
        /// out, so "took 312 ms" and "took 2041 ms" are one fault. Stable across runs, platforms and
        /// builds (no string.GetHashCode, which .NET randomises per process).
        /// </summary>
        public static string MessageHashOf(string condition)
        {
            string line = condition ?? string.Empty;
            int newline = line.IndexOf('\n');
            if (newline >= 0)
                line = line.Substring(0, newline);

            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            ulong hash = offset;
            foreach (char c in line.TrimEnd('\r', ' ', '\t'))
            {
                if (char.IsDigit(c))
                    continue;

                hash ^= c;
                hash *= prime;
            }

            return (hash & 0xFFFFFFFFFFFFUL).ToString("x12");
        }
    }
}
