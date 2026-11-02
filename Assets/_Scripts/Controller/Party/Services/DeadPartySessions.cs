// ─────────────────────────────────────────────────────────────────────────────
// DeadPartySessions.cs
// Party sessions this machine has PROVEN dead by trying to join them.
//
// WHY (the four-player test of 2026-10-30): Garrett's host froze for 82 s in a
// Maelstrom cell swap, Relay dropped him, and his lobby and his presence row
// outlived the allocation. Every other machine kept offering JOIN, SPECTATE and
// REJOIN into that session; five joins over 4.5 minutes each tore down the
// player's own party, failed "join code not found" and bounced. A presence row
// cannot know its host is dead until the host itself writes again, but the first
// join that fails "Gone" can: that machine stops offering the session at once.
//
// A session is remembered dead for a few minutes (a revived host makes a NEW
// session, so nothing it does is hidden), and the memory is per Play.
// ─────────────────────────────────────────────────────────────────────────────

using System;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>Party session ids a join on this machine found dead. See the file header.</summary>
    public static class DeadPartySessions
    {
        /// <summary>How long a session stays dead here. Longer than any freeze a host comes back from with the same session.</summary>
        public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

        static readonly Dictionary<string, DateTime> s_deadUntilUtc = new();

        /// <summary>Remember <paramref name="sessionId"/> as dead (a join into it failed "Gone").</summary>
        public static void Mark(string sessionId) => Mark(sessionId, DateTime.UtcNow);

        public static void Mark(string sessionId, DateTime nowUtc)
        {
            if (string.IsNullOrEmpty(sessionId)) return;
            s_deadUntilUtc[sessionId] = nowUtc + Lifetime;
        }

        /// <summary>True when a join on this machine found <paramref name="sessionId"/> dead within <see cref="Lifetime"/>.</summary>
        public static bool IsDead(string sessionId) => IsDead(sessionId, DateTime.UtcNow);

        public static bool IsDead(string sessionId, DateTime nowUtc)
        {
            if (string.IsNullOrEmpty(sessionId) || !s_deadUntilUtc.TryGetValue(sessionId, out var until)) return false;
            if (nowUtc < until) return true;
            s_deadUntilUtc.Remove(sessionId);
            return false;
        }

        /// <summary>The id to advertise for a row: empty when the session is dead, so the row offers no Join, Spectate or Rejoin.</summary>
        public static string Live(string sessionId) => IsDead(sessionId) ? string.Empty : sessionId;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForPlay() => s_deadUntilUtc.Clear();
    }
}
