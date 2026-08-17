// ─────────────────────────────────────────────────────────────────────────────
// IPartyRoster.cs
// Read-only query surface over the LOCAL party roster.
//
// WHY this interface exists:
//   "Session is authoritative over presence - the lobby is a hint, never the
//   source of truth" is a locked invariant (Docs/MultiplayerArchitecture/
//   ROADMAP.md), and PartySystem/ARCHITECTURE.md exit criterion 3 requires that
//   the host's view of party membership match every client's within one refresh
//   tick.  Both were violated by a single line: FriendsListPanel rendered
//   "IN YOUR PARTY {n}/4" from the REMOTE peer's self-published `partyCount`
//   presence property instead of the local roster.
//
//   With N party members that is N independently-published scalars and
//   N*(N-1) independently-lossy read edges, so a three-player party could show
//   2/4, 1/4 and 3/4 on three different screens at the same instant.  No poll
//   cadence can fix that - it is a wrong-source-of-truth bug, not a latency bug.
//
//   This interface is the enforcement.  A consumer holding an IPartyRoster can
//   answer "how big is MY party?" and "is this player in MY party?" from local
//   state with zero latency, and never needs to reach for a presence property
//   to do it.
//
// THE TWO-TIER RULE:
//   * "How big is MY party?"      -> IPartyRoster (local, authoritative, 0 latency)
//   * "How big is THEIR party?"   -> PartyPlayerData.AdvertisedPartyMemberCount
//                                    (presence lobby, a hint, poll latency)
//   Never answer the first question with the second's data.
//
// NOT ON THIS INTERFACE:
//   The active Relay session id lives on IPartyStateQuery.ActivePartySessionId
//   (implemented by HostConnectionService, which owns the session reference).
//   Do not mirror it here - one owner per fact.
//
// LIFETIME / THREAD SAFETY:
//   Implemented by HostConnectionDataSO (a plain SO, no new writer introduced).
//   Main-thread only, like every other reader of the SOAP lists.
// ─────────────────────────────────────────────────────────────────────────────

namespace CosmicShore.Utility
{
    /// <summary>
    /// Read-only view of the LOCAL party roster - the authoritative answer to
    /// "who is in my party and how many of us are there".
    ///
    /// <para>
    /// Implemented by <see cref="HostConnectionDataSO"/>, which is already wired
    /// into every party-aware UI component, so consumers need no extra
    /// inspector reference or DI registration to depend on this instead of on
    /// the concrete SO.
    /// </para>
    ///
    /// <para>
    /// Backed by the party <b>session</b> roster (via
    /// <c>PartyMemberService.SyncFromSession</c>), never by presence-lobby
    /// properties. See the file header for why that distinction is load-bearing.
    /// </para>
    /// </summary>
    public interface IPartyRoster
    {
        /// <summary>
        /// Players in the local player's party, <b>including the local player</b>.
        /// This is the number a "IN YOUR PARTY n/4" label must render.
        /// Zero only when the roster list is unwired.
        /// </summary>
        int MemberCount { get; }

        /// <summary>Maximum party slots, including the local player (4 by design).</summary>
        int MaxSlots { get; }

        /// <summary>True while the party can accept at least one more member.</summary>
        bool HasOpenSlots { get; }

        /// <summary>
        /// True when <paramref name="playerId"/> is a member of the local
        /// player's party. The authoritative membership test - do not
        /// re-implement it against presence properties.
        /// </summary>
        bool Contains(string playerId);

        /// <summary>
        /// True when the local player hosts the active Relay-backed party
        /// session. Gates kick affordances and game-launch authority.
        /// </summary>
        bool IsHost { get; }
    }
}
