// ─────────────────────────────────────────────────────────────────────────────
// IPartySessionService.cs
// Contract for the Relay-backed party session (the actual multiplayer session).
//
// KEY CONSTRAINT: implementors must NOT touch NetworkManager.  Starting and
// stopping Netcode is INetworkTransitionService's job.  This interface only
// manages the UGS session object (create, join, leave, refresh).
// ─────────────────────────────────────────────────────────────────────────────

using System;
using Cysharp.Threading.Tasks;
using Unity.Services.Multiplayer;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Manages the UGS Relay session (the live party session used for actual
    /// multiplayer networking).  Responsible for creating, joining, and leaving
    /// the session - not for Netcode transport.
    ///
    /// Lifetime: extracted from <see cref="HostConnectionService"/> in Phase 9.
    /// Implemented by <c>PartySessionService</c> in the Services folder.
    /// </summary>
    public interface IPartySessionService
    {
        /// <summary>
        /// The active party session, or <c>null</c> if no session has been created
        /// or joined.
        /// </summary>
        ISession ActiveSession { get; }

        /// <summary>
        /// Raised with the leaving player's UGS <c>PlayerId</c> when a player leaves the
        /// active party session.  Re-broadcasts the underlying
        /// <c>ISession.PlayerLeaving</c> event so consumers don't have to touch the raw
        /// session object; the service owns subscribe/unsubscribe at the session
        /// lifecycle chokepoints so handlers never leak across session reassignment.
        /// </summary>
        event Action<string> PlayerLeaving;

        /// <summary>
        /// Returns <c>true</c> once if the UGS SDK has pushed a roster-affecting
        /// session event since the last call, clearing the flag as it reads.
        ///
        /// <para>
        /// The party-session analogue of
        /// <c>IPresenceLobbyService.ConsumeRosterDirty</c>. Drain it from
        /// <c>Update()</c> and re-sync the party members <b>from the SDK's
        /// in-memory roster</b> - do NOT issue a <see cref="RefreshAsync"/> in
        /// response. The SDK has already patched <c>ISession.Players</c> before
        /// it raises, so the push tick needs no network call, and a path with no
        /// read cannot be voided by the stale-index read fault that voids ~32% of
        /// party-session poll reads (<c>Docs/PresenceSystem/BUGS.md</c>).
        /// </para>
        ///
        /// <para>
        /// Set from UGS callbacks on an unspecified thread; consumed on the main
        /// thread. The implementation uses <c>Interlocked</c> and does nothing
        /// else inside the callbacks.
        /// </para>
        /// </summary>
        bool TryConsumeRosterDirty();

        /// <summary>
        /// The wall-clock time (via <c>Time.unscaledTime</c>) when the current
        /// session was created.  Used to enforce the grace period that prevents
        /// premature RefreshAsync failures from nulling a freshly-provisioned session.
        /// </summary>
        float CreatedAtUnscaledTime { get; }

        /// <summary>
        /// Creates a new Relay-backed party session and sets it as <see cref="ActiveSession"/>.
        /// No-op if a session already exists.
        /// </summary>
        /// <param name="maxPlayers">Maximum simultaneous players for this session.</param>
        UniTask CreateAsync(int maxPlayers);

        /// <summary>
        /// Joins an existing session by its UGS session ID.
        /// Sets <see cref="ActiveSession"/> on success.
        /// </summary>
        /// <param name="sessionId">
        /// The UGS session ID published by the host.  Must be the real ID (not PENDING).
        /// </param>
        /// <remarks>
        /// Retries transient join failures (rate-limit / SDK SessionException NRE /
        /// lobby-events 23006) with back-off; non-transient errors throw to the caller.
        /// </remarks>
        UniTask JoinByIdAsync(string sessionId);

        /// <summary>
        /// Leaves the active session gracefully (delete if host, leave if client).
        /// Clears <see cref="ActiveSession"/> to null.
        /// Safe to call even if no session is active.
        /// </summary>
        UniTask LeaveAsync();

        /// <summary>
        /// Refreshes the session's player list from the UGS backend.
        /// Used to detect when new members have joined or left.
        /// </summary>
        /// <remarks>
        /// Only call after the grace period (<see cref="CreatedAtUnscaledTime"/> +
        /// SESSION_CREATION_GRACE_PERIOD_SECONDS) to avoid nulling a freshly-provisioned session.
        /// </remarks>
        UniTask RefreshAsync();

        /// <summary>
        /// Re-publishes the local player's identity properties (displayName /
        /// avatarId) on the ACTIVE session's player record. Session player
        /// properties are otherwise written only at create/join, so a mid-party
        /// profile rename would leave every peer's roster (party slots) showing
        /// the stale name. Called by <c>HostConnectionService</c> on profile
        /// change; no-op when no session is active. Failures are logged and
        /// swallowed - the name self-heals on the next session (re)join.
        /// </summary>
        UniTask UpdateLocalPlayerPropertiesAsync(string displayName, int avatarId);

        /// <summary>
        /// Synchronously clears <see cref="ActiveSession"/> without calling the UGS SDK.
        /// Use for stale-reference cleanup (game→menu transition) or after non-rate-limit
        /// refresh failures.  Call <see cref="LeaveAsync"/> for a graceful leave.
        /// </summary>
        void ClearSession();
    }
}
