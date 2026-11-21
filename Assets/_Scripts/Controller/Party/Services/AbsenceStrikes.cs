// ─────────────────────────────────────────────────────────────────────────────
// AbsenceStrikes.cs
// Two absent reads before a player is removed from a roster.
//
// WHY (Block 5 step 7, Docs/PartySystem/BRANCH_AUDIT_2026-10.md §2; the August presence work's
// c9c6db1772 on Ys-rescue-2026-08-24):
//   Both roster diffs (the online list in HostConnectionService.RefreshOnlinePlayersDiff and the
//   party list in PartyMemberService.SyncFromSession) removed a player the moment one read came
//   back without them. One absence is not evidence of departure: the SDK's stale-index fault voids
//   12-43% of reads and can leave a short roster behind, a read can land mid-mutation, and the
//   presence converge can swap to a lobby whose roster has not filled yet. The online panel emptied
//   and refilled a tick later, and party members vanished and came back (OnPartyMemberLeft flips
//   Friends presence and rebuilds the friends panel).
//
// THE RULE:
//   - A player absent from a SERVER read gets a strike; ReadsBeforeRemoval (2) in a row removes them.
//   - A player seen again loses their strikes, so blips far apart never add up.
//   - A player the service said left (its PlayerLeaving / PlayerHasLeft push, collected by
//     DepartureReports) is removed at once: that departure is stated, not inferred.
//   - Re-reading the SDK's local copy (a push tick, a party tick between GETs) is not a read: it
//     would only see the same short roster again, so it can remove stated departures but never
//     adds a strike.
//
// THREAD SAFETY: main thread only. Push handlers write to DepartureReports, never here.
// ─────────────────────────────────────────────────────────────────────────────

using System.Collections.Generic;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Counts consecutive absent reads per player and says when an absent player should go.
    /// </summary>
    public sealed class AbsenceStrikes
    {
        /// <summary>Consecutive absent server reads before a player is removed.</summary>
        public const int ReadsBeforeRemoval = 2;

        readonly Dictionary<string, int> _misses = new();

        /// <summary>The player is in this read: forget any strikes.</summary>
        public void Seen(string playerId) => _misses.Remove(playerId);

        /// <summary>
        /// The player is missing from this read. True when they should be removed now: at once when
        /// <paramref name="departureStated"/>, otherwise on the <see cref="ReadsBeforeRemoval"/>th
        /// consecutive absence among reads with <paramref name="serverRead"/> set.
        /// </summary>
        public bool Absent(string playerId, bool departureStated, bool serverRead)
        {
            if (departureStated)
            {
                _misses.Remove(playerId);
                return true;
            }
            if (!serverRead) return false;

            _misses.TryGetValue(playerId, out int misses);
            if (++misses < ReadsBeforeRemoval)
            {
                _misses[playerId] = misses;
                return false;
            }
            _misses.Remove(playerId);
            return true;
        }

        /// <summary>Consecutive absent reads counted for the player so far.</summary>
        public int StrikesFor(string playerId) => _misses.TryGetValue(playerId, out int n) ? n : 0;

        /// <summary>Forgets every strike (the roster was cleared).</summary>
        public void Clear() => _misses.Clear();
    }

    /// <summary>
    /// The players a session's push said left, collected from whatever thread the SDK raises on and
    /// taken by the main thread before each roster diff.
    /// </summary>
    public sealed class DepartureReports
    {
        readonly object _gate = new();
        readonly HashSet<string> _reported = new();

        /// <summary>Any thread: the service said this player left.</summary>
        public void Report(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return;
            lock (_gate) _reported.Add(playerId);
        }

        /// <summary>
        /// Main thread: moves every report into <paramref name="into"/> (cleared first). A report is
        /// taken once; one for a player the next read still holds is simply dropped.
        /// </summary>
        public void TakeInto(ISet<string> into)
        {
            into.Clear();
            lock (_gate)
            {
                into.UnionWith(_reported);
                _reported.Clear();
            }
        }

        /// <summary>Drops every report (the session was let go).</summary>
        public void Clear()
        {
            lock (_gate) _reported.Clear();
        }
    }
}
