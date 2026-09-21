using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Names the bug behind an error STORM — a fault that repeats every frame until the console is
    /// unusable and the frame rate is gone.
    ///
    /// <para>The reason this exists is that a storm destroys its own evidence. One exception per
    /// frame per object is thousands of identical console entries, and the one line that says
    /// WHICH object and WHICH call site is somewhere in the middle of them with nothing to mark
    /// it out; Unity's own Collapse only helps if you already know what you are collapsing, and it
    /// hides the rate, which is the thing that tells you a storm from a one-off. Meanwhile every
    /// entry costs a stack-trace capture, so the game slows down in proportion to how badly it is
    /// broken — which is exactly when a player is least able to describe what happened.</para>
    ///
    /// <para><b>It suppresses nothing.</b> Every error still reaches the console in full; this is
    /// fail-loud with an index, not a filter. What it adds is ONE periodic summary that ranks the
    /// distinct faults by how often they fired and quotes each one's first full stack — so a
    /// report stops being "tons of NullReferenceExceptions" and becomes a file and a line.</para>
    ///
    /// <para>Faults are keyed with <see cref="BugSignature.ErrorId"/>, the same fingerprint the
    /// editor Bug Ledger files under (<c>Docs/DIAGNOSTICS.md</c>), so a storm reported here and an
    /// issue filed there are the same id and merge rather than becoming two bugs.</para>
    ///
    /// <para>Editor and development builds only, and gated at RUNTIME rather than with
    /// <c>#if</c>: per <c>Docs/CONDITIONAL_COMPILATION.md</c> a guard has to cover a
    /// self-consistent unit, and the cheapest way to be sure of that is not to write one. A
    /// release player never subscribes, so it costs nothing there.</para>
    /// </summary>
    public static class ErrorStormReporter
    {
        /// Errors within one window before the window is called a storm and summarised. A handful
        /// of errors during a scene load is ordinary; a per-frame fault clears this in well under
        /// a second.
        const int StormThreshold = 40;

        /// How long a window is. Long enough that a burst at a scene boundary settles on its own,
        /// short enough that a running storm is named while the tester is still looking at it.
        const float WindowSeconds = 5f;

        /// Distinct faults quoted per summary, most frequent first. A storm is almost always one
        /// or two signatures; more than this and the list stops being readable.
        const int TopOffenders = 5;

        /// Marks this reporter's own output so the handler can never recurse on it.
        const string Tag = "[ErrorStorm]";

        sealed class Fault
        {
            public string Id;
            public string Message;
            public string FirstStack;
            public int Count;
        }

        static readonly Dictionary<string, Fault> _faults = new();
        static readonly object _gate = new();
        static float _windowStart;
        static int _windowCount;
        static bool _installed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Install()
        {
            // SubsystemRegistration runs before every play session, including with domain reload
            // disabled - so the subscription and the counters are reset together and a previous
            // session's storm cannot be attributed to this one.
            Reset();

            if (!Application.isEditor && !Debug.isDebugBuild) return;
            if (_installed) return;

            Application.logMessageReceived += OnLog;
            _installed = true;
        }

        static void Reset()
        {
            lock (_gate)
            {
                _faults.Clear();
                _windowCount = 0;
                _windowStart = Time.realtimeSinceStartup;
            }
        }

        static void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            // Never account for our own summary, or a storm would feed itself.
            if (condition != null && condition.StartsWith(Tag, StringComparison.Ordinal)) return;

            string id = BugSignature.ErrorId(condition, stackTrace, type, out _);

            string report = null;
            lock (_gate)
            {
                if (!_faults.TryGetValue(id, out var fault))
                {
                    // The FIRST stack is kept rather than the last: later frames of a storm are
                    // the same fault seen from a degraded state, and the first one is the closest
                    // thing to the cause.
                    fault = new Fault { Id = id, Message = condition, FirstStack = stackTrace };
                    _faults[id] = fault;
                }
                fault.Count++;
                _windowCount++;

                float now = Time.realtimeSinceStartup;
                if (now - _windowStart < WindowSeconds) return;

                if (_windowCount >= StormThreshold) report = BuildReport(now - _windowStart);

                _faults.Clear();
                _windowCount = 0;
                _windowStart = now;
            }

            // Outside the lock: logging re-enters this handler synchronously.
            if (report != null) Debug.LogError(report);
        }

        static string BuildReport(float elapsed)
        {
            var ranked = new List<Fault>(_faults.Values);
            ranked.Sort((a, b) => b.Count.CompareTo(a.Count));

            var sb = new StringBuilder(1024);
            sb.Append(Tag).Append(' ')
              .Append(_windowCount).Append(" errors in ").Append(elapsed.ToString("F1"))
              .Append("s (").Append((_windowCount / Mathf.Max(0.01f, elapsed)).ToString("F0"))
              .Append("/s) from ").Append(ranked.Count)
              .Append(ranked.Count == 1 ? " distinct fault." : " distinct faults.")
              .Append(" Every one is also in the console in full; this is the index.\n");

            int shown = Mathf.Min(TopOffenders, ranked.Count);
            for (int i = 0; i < shown; i++)
            {
                var f = ranked[i];
                sb.Append("\n  ").Append(i + 1).Append(". x").Append(f.Count)
                  .Append("  ").Append(f.Id).Append("  ")
                  .Append(BugSignature.NormalizeText(f.Message, 200)).Append('\n')
                  .Append(FirstUserFrames(f.FirstStack, 4));
            }

            if (ranked.Count > shown)
                sb.Append("\n  ... and ").Append(ranked.Count - shown).Append(" more.\n");

            sb.Append("\nFile these with FrogletTools > Diagnostics > Bug Ledger (same ids).");
            return sb.ToString();
        }

        /// <summary>
        /// The first few frames of a stack, indented. Trimmed rather than quoted whole because a
        /// storm summary that carries five full traces is as unreadable as the storm was.
        /// </summary>
        static string FirstUserFrames(string stack, int maxFrames)
        {
            if (string.IsNullOrEmpty(stack)) return "       (no stack)\n";

            var sb = new StringBuilder(256);
            int taken = 0;
            foreach (var raw in stack.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0) continue;
                sb.Append("       ").Append(line).Append('\n');
                if (++taken >= maxFrames) break;
            }
            return sb.ToString();
        }
    }
}
