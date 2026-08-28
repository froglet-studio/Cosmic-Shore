// ─────────────────────────────────────────────────────────────────────────────
// IPartyMemberService.cs
// Contract for syncing the party member list and firing the SOAP events that
// keep the UI (PartySlotView, PartyAreaPanel) up to date.
//
// WHY a separate service?
//   The member-list diff logic (add new members, remove departed ones, fire
//   OnPartyMemberJoined / OnPartyMemberLeft) was scattered across
//   RefreshPartyMembersAsync, ScanPresenceForJoinedPartyMembers, and
//   AcceptInviteAsync in HostConnectionService.  Centralising it here means
//   one place to audit and test member-list correctness.
// ─────────────────────────────────────────────────────────────────────────────

using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using Unity.Services.Multiplayer;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Synchronises the <see cref="HostConnectionDataSO.PartyMembers"/> SOAP list
    /// against the live party session and fires the appropriate SOAP events.
    ///
    /// <para>
    /// Implementors must NOT start a NetworkManager or touch the UGS session SDK
    /// directly - those are <see cref="INetworkTransitionService"/>'s and
    /// <see cref="IPartySessionService"/>'s jobs respectively.
    /// </para>
    ///
    /// Lifetime: extracted from <see cref="HostConnectionService"/> in Phase 10.
    /// Implemented by <c>PartyMemberService</c> in the Services folder.
    /// Thread-safety: main-thread only.
    /// </summary>
    public interface IPartyMemberService
    {
        /// <summary>
        /// Seeds the <see cref="HostConnectionDataSO.PartyMembers"/> list with only
        /// the local player's <see cref="HostConnectionDataSO.LocalPlayerData"/>.
        /// Call when the local player first enters a party (host-create path).
        /// </summary>
        /// <param name="clearFirst">
        /// When <c>true</c> (default) the list is cleared before adding the local
        /// player.  Set <c>false</c> only if the list is already empty.
        /// </param>
        void SeedLocalPlayer(bool clearFirst = true);

        /// <summary>
        /// Diffs <paramref name="session"/>.Players against the current
        /// <see cref="HostConnectionDataSO.PartyMembers"/> list:
        /// <list type="bullet">
        ///   <item>Adds players that appeared and fires <c>OnPartyMemberJoined</c>.</item>
        ///   <item>Removes players that left and fires <c>OnPartyMemberLeft</c>.</item>
        /// </list>
        /// Returns the player IDs that were newly added so the caller can do
        /// post-join processing (invite cleanup, state-machine transition, etc.).
        /// </summary>
        /// <param name="session">
        /// The live Relay party session.  Must not be null.
        /// </param>
        /// <param name="localPlayerId">
        /// The local player's UGS ID.  Excluded from the diff - always retained.
        /// </param>
        IReadOnlyList<string> SyncFromSession(ISession session, string localPlayerId);

        /// <summary>
        /// Silently clears <see cref="HostConnectionDataSO.PartyMembers"/> with no
        /// SOAP events raised.  Use in error paths and stale-session cleanup where
        /// the UI will be reset independently.
        /// </summary>
        void ClearSilent();

        /// <summary>
        /// Clears <see cref="HostConnectionDataSO.PartyMembers"/> and fires
        /// <c>OnPartyMemberLeft</c> for every non-local member before removing them.
        /// Use when the party disbands gracefully (leave, kick all, decline).
        /// </summary>
        /// <param name="localPlayerId">
        /// The local player's ID - retained in the list, not fired as Left.
        /// </param>
        void ClearWithEvents(string localPlayerId);

        /// <summary>
        /// Reads the <c>displayName</c> and <c>avatarId</c> player properties from
        /// <paramref name="p"/> and constructs a <see cref="PartyPlayerData"/> value.
        /// Works for both presence-lobby players and Relay session players.
        /// </summary>
        /// <param name="p">A player whose Properties contain the standard keys.</param>
        PartyPlayerData ReadMemberData(IReadOnlyPlayer p);
    }
}
