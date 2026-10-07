// ─────────────────────────────────────────────────────────────────────────────
// UgsRequestTelemetry.cs
// Rolling one-minute counters for the UGS request layer: how many calls, how many
// lobby reads, how many 429s, how many retries, how many presence resets and offline
// fallbacks. Pure observability - a call site that counts never changes behaviour.
//
// WHY (Docs/MultiplayerArchitecture/REVIEW_INVITE_AND_RESILIENCE.md §5.9):
//   Fifteen party fixes were judged by "it felt smoother". The review's Phase 1 acceptance is
//   a NUMBER - zero 429s and zero ForceResets across a four-player, ten-minute MPPM run - and a
//   number needs a counter. NetworkDiagnostics.GetSnapshot() appends Describe() to every NetDiag
//   log line, so the counts ride the lines the party layer already prints on every fault.
//
// THREAD SAFETY: main-thread only. The clock is a settable delegate so tests drive the window.
// ─────────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>What <see cref="UgsRequestTelemetry"/> counts. Numeric values are fixed; the enum is also the array index.</summary>
    public enum UgsRequestCounter
    {
        /// <summary>Every attempt <see cref="UgsRequestPolicy.ExecuteAsync{T}"/> made (first tries and retries alike).</summary>
        Requests           = 0,
        /// <summary>Lobby / session GETs issued outside the policy: the presence and party refresh ticks and the writer's pre/post-save refreshes.</summary>
        LobbyReads         = 1,
        Retries            = 2,
        RateLimited        = 3,
        Transient          = 4,
        Benign             = 5,
        Conflict           = 6,
        Full               = 7,
        Gone               = 8,
        Fatal              = 9,
        /// <summary>A caller got an in-flight task back instead of a second request.</summary>
        Coalesced          = 10,
        /// <summary>A retry was refused because the per-minute budget was spent.</summary>
        BudgetExhausted    = 11,
        /// <summary>HostConnectionService rebuilt the presence layer after consecutive refresh errors.</summary>
        PresenceForceReset = 12,
        /// <summary>Boot gave up on the networked menu and started the offline local host.</summary>
        OfflineFallback    = 13,
    }

    public static class UgsRequestTelemetry
    {
        /// <summary>Length of the rolling window every per-minute figure is measured over.</summary>
        public const int WindowSeconds = 60;

        private static readonly int s_counterCount = Enum.GetValues(typeof(UgsRequestCounter)).Length;
        private static readonly Queue<double>[] s_windows = NewWindows();
        private static readonly long[] s_totals = new long[s_counterCount];

        /// <summary>Seconds source. Defaults to <see cref="Time.realtimeSinceStartupAsDouble"/>; tests substitute a manual clock.</summary>
        public static Func<double> Now { get; set; } = () => Time.realtimeSinceStartupAsDouble;

        private static Queue<double>[] NewWindows()
        {
            var windows = new Queue<double>[Enum.GetValues(typeof(UgsRequestCounter)).Length];
            for (int i = 0; i < windows.Length; i++) windows[i] = new Queue<double>();
            return windows;
        }

        public static void Count(UgsRequestCounter counter)
        {
            int i = (int)counter;
            if (i < 0 || i >= s_counterCount) return;
            s_totals[i]++;
            s_windows[i].Enqueue(Now());
        }

        /// <summary>Counts a classified failure under its class's counter.</summary>
        public static void CountFailure(UgsFailureClass cls)
        {
            switch (cls)
            {
                case UgsFailureClass.RateLimited: Count(UgsRequestCounter.RateLimited); break;
                case UgsFailureClass.Transient:   Count(UgsRequestCounter.Transient);   break;
                case UgsFailureClass.Benign:      Count(UgsRequestCounter.Benign);      break;
                case UgsFailureClass.Conflict:    Count(UgsRequestCounter.Conflict);    break;
                case UgsFailureClass.Full:        Count(UgsRequestCounter.Full);        break;
                case UgsFailureClass.Gone:        Count(UgsRequestCounter.Gone);        break;
                case UgsFailureClass.Fatal:       Count(UgsRequestCounter.Fatal);       break;
                // Cancelled is the caller's choice, not a fault: deliberately uncounted.
            }
        }

        /// <summary>Occurrences inside the trailing <see cref="WindowSeconds"/>.</summary>
        public static int InLastMinute(UgsRequestCounter counter)
        {
            int i = (int)counter;
            if (i < 0 || i >= s_counterCount) return 0;
            Prune(s_windows[i]);
            return s_windows[i].Count;
        }

        /// <summary>Occurrences since the process started (or the last <see cref="Reset"/>).</summary>
        public static long Total(UgsRequestCounter counter)
        {
            int i = (int)counter;
            return i < 0 || i >= s_counterCount ? 0 : s_totals[i];
        }

        /// <summary>
        /// One compact field for a log line, per-minute figures first:
        /// <c>ugs[req/min=12 reads/min=40 429/min=0 retry/min=1 coalesced/min=0 budget-out/min=0 reset=0 offline=0]</c>.
        /// The last two are lifetime totals - a reset or an offline fallback is rare enough that
        /// "how many ever" is the figure worth reading.
        /// </summary>
        public static string Describe()
        {
            var sb = new StringBuilder(128);
            sb.Append("ugs[req/min=").Append(InLastMinute(UgsRequestCounter.Requests));
            sb.Append(" reads/min=").Append(InLastMinute(UgsRequestCounter.LobbyReads));
            sb.Append(" 429/min=").Append(InLastMinute(UgsRequestCounter.RateLimited));
            sb.Append(" retry/min=").Append(InLastMinute(UgsRequestCounter.Retries));
            sb.Append(" coalesced/min=").Append(InLastMinute(UgsRequestCounter.Coalesced));
            sb.Append(" budget-out/min=").Append(InLastMinute(UgsRequestCounter.BudgetExhausted));
            sb.Append(" reset=").Append(Total(UgsRequestCounter.PresenceForceReset));
            sb.Append(" offline=").Append(Total(UgsRequestCounter.OfflineFallback));
            sb.Append(']');
            return sb.ToString();
        }

        /// <summary>Clears every window and total. For tests and for a fresh MPPM measurement.</summary>
        public static void Reset()
        {
            for (int i = 0; i < s_counterCount; i++)
            {
                s_windows[i].Clear();
                s_totals[i] = 0;
            }
        }

        private static void Prune(Queue<double> window)
        {
            double cutoff = Now() - WindowSeconds;
            while (window.Count > 0 && window.Peek() < cutoff)
                window.Dequeue();
        }
    }
}
