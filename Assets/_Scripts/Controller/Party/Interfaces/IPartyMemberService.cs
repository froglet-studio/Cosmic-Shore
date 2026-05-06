// ─────────────────────────────────────────────────────────────────────────────
// IPartyMemberService.cs
// Contract for syncing the party member list and firing the SOAP events that
// keep the UI (PartySlotView, PartyAreaPanel) up to date.
//
// WHY a separate service?
//   The member-list diff logic (add new members, remove departed ones, fire
//   OnPartyMemberJoined / OnPartyMemberLeft) is currently scattered across
//   RefreshPartyMembersAsync, ScanPresenceForJoinedPartyMembers, and
//   AcceptInviteAsync in HostConnectionService.  Centralising it here means
//   one place to audit and test member-list correctness.
// ─────────────────────────────────────────────────────────────────────────────

using Cysharp.Threading.Tasks;
using Unity.Services.Multiplayer;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Synchronises the <see cref="HostConnectionDataSO.PartyMembers"/> SOAP list
    /// against the live party session and fires the appropriate SOAP events.
    ///
    /// Lifetime: extracted from <see cref="HostConnectionService"/> in Phase 10.
    /// Implemented by <c>PartyMemberService</c> in the Services folder.
    /// </summary>
    public interface IPartyMemberService
    {
        /// <summary>
        /// Adds the local player as the sole member when first entering a party
        /// (host create) or joining a session (client accept).
        /// Clears the list before seeding if <paramref name="clearFirst"/> is true.
        /// </summary>
        /// <param name="playerId">Local player's UGS ID.</param>
        /// <param name="displayName">Local player's display name.</param>
        /// <param name="avatarId">Local player's avatar ID.</param>
        /// <param name="clearFirst">True to clear the SOAP list before adding.</param>
        void SeedLocalPlayer(string playerId, string displayName, int avatarId, bool clearFirst = true);

        /// <summary>
        /// Diffs the <paramref name="session"/>'s player list against the current
        /// <see cref="HostConnectionDataSO.PartyMembers"/> SOAP list and:
        /// <list type="bullet">
        ///   <item>Adds players that appeared — fires <c>OnPartyMemberJoined</c>.</item>
        ///   <item>Removes players that left — fires <c>OnPartyMemberLeft</c>.</item>
        /// </list>
        /// Returns the list of player IDs that were newly added (for outgoing-invite
        /// cleanup after a member joins).
        /// </summary>
        /// <param name="session">
        /// The live party session.  Must not be null.
        /// </param>
        /// <param name="localPlayerId">
        /// The local player's ID — excluded from the diff (always present).
        /// </param>
        UniTask<System.Collections.Generic.List<string>> SyncFromSessionAsync(
            ISession session,
            string   localPlayerId);

        /// <summary>
        /// Clears the SOAP party member list and fires <c>OnPartyMemberLeft</c>
        /// for every non-local member.  Called on leave/kick.
        /// </summary>
        /// <param name="localPlayerId">
        /// Local player's ID — not evicted from the list, not fired as Left.
        /// </param>
        void Clear(string localPlayerId);
    }
}
