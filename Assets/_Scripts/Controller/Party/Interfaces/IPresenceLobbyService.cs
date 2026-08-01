// ─────────────────────────────────────────────────────────────────────────────
// IPresenceLobbyService.cs
// Contract for the presence lobby - the global player-discovery session.
//
// KEY CONSTRAINT: implementors must NOT start a NetworkManager or create a
// Relay allocation.  The presence lobby is a lobby-only UGS session that
// coexists safely with any active NetworkManager state.  Relay is
// IPartySessionService's responsibility.
// ─────────────────────────────────────────────────────────────────────────────

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Unity.Services.Multiplayer;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Manages the UGS lobby-only session used for player discovery and invite
    /// property exchange.  No Relay, no NetworkManager interaction.
    ///
    /// Lifetime: extracted from <see cref="HostConnectionService"/> in Phase 7.
    /// Implemented by <c>PresenceLobbyService</c> in the Services folder.
    /// </summary>
    public interface IPresenceLobbyService
    {
        /// <summary>
        /// The active presence lobby session, or <c>null</c> if not yet joined.
        /// </summary>
        ISession ActiveLobby { get; }

        /// <summary>
        /// Optional source of LIVE values for the stateful player properties
        /// (invite_payloads, joined_party, matchName, ...). When set, every
        /// lobby (re)join - initial join, reconnect, and the periodic
        /// <see cref="ConvergeToCanonicalAsync"/> migration - publishes these
        /// values instead of resetting the keys to empty. Without it, a rejoin
        /// wipes in-flight invites and a guest's joined_party advertisement
        /// (Docs/PresenceSystem/BUGS.md B4). Set once by
        /// <c>HostConnectionService</c>, the single writer of those values.
        /// </summary>
        Func<IReadOnlyDictionary<string, string>> LivePropertySource { get; set; }

        /// <summary>
        /// Joins an existing presence lobby or creates one if none exists.
        /// Safe to call multiple times - returns early if already joined.
        /// </summary>
        /// <param name="maxPlayers">
        /// Maximum simultaneous players in the global lobby (typically 100).
        /// </param>
        UniTask JoinOrCreateAsync(int maxPlayers);

        /// <summary>
        /// Leaves the presence lobby gracefully (delete if host, leave if client).
        /// Safe to call even if not currently in a lobby - returns immediately.
        /// </summary>
        UniTask LeaveAsync();

        /// <summary>
        /// Refreshes the lobby's player list and properties from the UGS backend.
        /// Must be called on a timer to detect new players and incoming invites.
        /// </summary>
        /// <remarks>
        /// Rate-limited by UGS to approximately 1 call per second per client.
        /// The refresh scheduler (LobbyRefreshScheduler) manages the interval.
        /// </remarks>
        UniTask RefreshAsync();

        /// <summary>
        /// Writes the given set of player properties to the local player's lobby
        /// record.  Acquires the lobby mutex internally - do NOT call while already
        /// holding the mutex.
        /// </summary>
        /// <param name="properties">
        /// Key-value pairs to write (e.g. displayName, invite_payloads).
        /// </param>
        /// <param name="operationName">
        /// Human-readable name for this write - appears in log output for diagnostics.
        /// </param>
        UniTask SavePropertiesAsync(
            Dictionary<string, PlayerProperty> properties,
            string operationName);

        /// <summary>
        /// Synchronously clears the <see cref="ActiveLobby"/> reference without
        /// calling the UGS SDK.  Use before a reconnect attempt to let
        /// <see cref="JoinOrCreateAsync"/> proceed rather than return early.
        ///
        /// <para>
        /// This is appropriate when consecutive refresh errors indicate the
        /// connection is already broken - calling Leave/Delete would just add more
        /// failures.  The reconnect path calls <see cref="JoinOrCreateAsync"/>
        /// immediately after to re-establish the lobby.
        /// </para>
        /// </summary>
        void ForceReset();

        /// <summary>
        /// Converges every client onto a single canonical presence lobby - the one
        /// with the lexicographically smallest session id - healing a
        /// simultaneous-create split where two clients each created their own lobby
        /// at the same instant (MPPM / near-simultaneous launches) and UGS session
        /// indexing lag let both miss each other on the initial query.
        ///
        /// <para>
        /// Idempotent and swap-free: the smallest-id holder stays put; everyone else
        /// leaves their lobby and joins the canonical one.  A no-op once the set has
        /// converged, so it is safe to call on a timer to self-heal late splits.
        /// Must NOT touch NetworkManager or Relay (presence lobby is lobby-only).
        /// </para>
        /// </summary>
        /// <param name="maxPlayers">Capacity to use if a (re)join is required.</param>
        UniTask ConvergeToCanonicalAsync(int maxPlayers);

        /// <summary>
        /// Returns <c>true</c> (and clears the flag) when the UGS session has
        /// pushed a roster-affecting event since the last call - a player joined,
        /// a player is leaving or has left, player properties changed, or a
        /// generic lobby delta arrived.
        ///
        /// <para>
        /// This is the PUSH half of presence discovery. The presence lobby has
        /// always been an <see cref="ISession"/>, and this codebase already
        /// consumes <c>ISession</c> events on the PARTY session
        /// (<c>PartySessionService</c> subscribes <c>PlayerLeaving</c>) - the
        /// presence lobby was simply never subscribed, so every arrival and
        /// departure anywhere in the world was discovered only by the next poll
        /// tick. Callers should treat a <c>true</c> return as "refresh now"
        /// while keeping their periodic poll as a safety net.
        /// </para>
        ///
        /// <para>
        /// Deliberately a polled flag rather than a C# event: the SDK raises its
        /// callbacks from its own dispatch context, and raising a SOAP event or
        /// touching a <c>UnityEngine.Object</c> from there would run listeners
        /// inline off the main thread (see <c>Docs/THREADING.md</c>). Draining
        /// the flag from <c>Update()</c> is both the main-thread guarantee and a
        /// per-frame coalescer - a four-player join burst produces one refresh,
        /// not four.
        /// </para>
        /// </summary>
        bool ConsumeRosterDirty();

        /// <summary>
        /// Returns <c>true</c> (and clears the flag) when UGS has pushed a
        /// DEFINITE membership-loss signal since the last call - either
        /// <c>RemovedFromSession</c> (we were removed) or <c>Deleted</c> (the
        /// lobby is gone).
        ///
        /// <para>
        /// Distinct from <see cref="ConsumeRosterDirty"/> because the recovery is
        /// different: a roster change means re-read, a membership loss means
        /// rejoin. This is an authoritative statement from the service, not the
        /// error-count heuristic
        /// (<c>MAX_REFRESH_ERRORS_BEFORE_RECONNECT</c>) the reconnect path
        /// otherwise relies on - so it can be acted on without waiting for the
        /// NetDiag data that <c>Docs/PresenceSystem/REFACTOR.md</c>'s
        /// <c>LobbyMembershipMonitor</c> extraction is gated on.
        /// </para>
        /// </summary>
        bool ConsumeMembershipLost();
    }
}
