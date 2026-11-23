// ─────────────────────────────────────────────────────────────────────────────
// PresenceWatchDemand.cs
// "Someone is looking at the online list right now." A counted demand the friends panel raises while it
// is visible; LobbyRefreshScheduler shortens the presence safety poll (WATCHED_POLL_SECONDS) while it is
// held, so a player who quit without a leave drops off the list in seconds rather than a minute.
// Counted (not a bool) so two panels open at once do not cancel each other on close.
// ─────────────────────────────────────────────────────────────────────────────

using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A counted "the online list is on screen" demand. <see cref="Acquire"/> on show,
    /// <see cref="Release"/> on hide, always paired. Main-thread only.
    /// </summary>
    public static class PresenceWatchDemand
    {
        static int _holders;

        /// <summary>True while at least one view holds the demand.</summary>
        public static bool IsWatched => _holders > 0;

        /// <summary>How many views hold it (diagnostics).</summary>
        public static int Holders => _holders;

        public static void Acquire() => _holders++;

        public static void Release()
        {
            if (_holders > 0) _holders--;
        }

        /// <summary>Tests and Play start: nothing holds it. The Editor runs without a domain reload, so a
        /// static would otherwise carry the last Play's count into the next.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetForPlay() => _holders = 0;
    }
}
