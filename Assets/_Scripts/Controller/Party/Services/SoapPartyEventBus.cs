// ─────────────────────────────────────────────────────────────────────────────
// SoapPartyEventBus.cs
// Single place that calls connectionData.OnXxx.Raise(...) for every party event.
//
// WHY this class exists:
//   Before extraction, HostConnectionService and PartyInviteController each
//   called connectionData.OnXxx?.Raise(...) directly in a dozen locations.  A
//   mistyped field name or a forgotten null-check was invisible to the compiler
//   because SOAP event fields are optional inspector references.  Centralising
//   every Raise call here means:
//     1. The null-guard pattern lives in exactly one place per event type.
//     2. Log lines are automatic - every event is visible in the console
//        timeline without per-call Debug.Log boilerplate.
//     3. Tests can swap in a mock bus (Phase 14 / future) without touching
//        game logic at all.
//
// OWNERSHIP:
//   Only this class may call .Raise() on HostConnectionDataSO events.
//   Callers (HostConnectionService, PartyInviteController) call the typed
//   methods below - never access connectionData events directly.
//
// LIFETIME:
//   Pure C# - no MonoBehaviour.  Instantiated in HostConnectionService.Awake()
//   for Phases 4-11.  Phase 12 registers it in Reflex DI.
//
// THREAD SAFETY:
//   Main-thread only.  All Raise calls must occur on Unity's main thread
//   because SOAP ScriptableEvent.Raise() notifies MonoBehaviour listeners.
// ─────────────────────────────────────────────────────────────────────────────

using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Centralises every SOAP event raise for the party system.
    ///
    /// Each method guards against null event references (inspector might not have
    /// wired every event asset) and emits a structured <c>[SoapPartyEventBus]</c>
    /// log so the console timeline shows exactly when each event fires and why.
    ///
    /// Lifetime: pure C# - no MonoBehaviour.  Created as a field on
    /// <see cref="HostConnectionService"/>; will be DI-registered in Phase 12.
    /// Thread-safety: main-thread only.
    /// </summary>
    public sealed class SoapPartyEventBus
    {
        // ─────────────────────────────────────────────────────────────────────
        // Private state
        // ─────────────────────────────────────────────────────────────────────

        private readonly HostConnectionDataSO _data;

        // ─────────────────────────────────────────────────────────────────────
        // Construction
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Creates the event bus backed by <paramref name="data"/>.
        /// </summary>
        /// <param name="data">
        /// The project-level <see cref="HostConnectionDataSO"/> asset that owns
        /// all SOAP event references.  Must not be null.
        /// </param>
        public SoapPartyEventBus(HostConnectionDataSO data)
        {
            _data = data;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Connection events
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Raises <see cref="HostConnectionDataSO.OnHostConnectionEstablished"/>.
        /// Called once the local player has successfully joined or created the
        /// presence lobby and all identity properties have been saved.
        /// </summary>
        public void RaiseHostConnectionEstablished()
        {
            Debug.Log("[SoapPartyEventBus] RaiseHostConnectionEstablished");
            _data.OnHostConnectionEstablished?.Raise();
        }

        /// <summary>
        /// Raises <see cref="HostConnectionDataSO.OnHostConnectionLost"/>.
        /// Called on sign-out or fatal lobby disconnection.
        /// </summary>
        public void RaiseHostConnectionLost()
        {
            Debug.Log("[SoapPartyEventBus] RaiseHostConnectionLost");
            _data.OnHostConnectionLost?.Raise();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Invite events
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Raises <see cref="HostConnectionDataSO.OnInviteSent"/> carrying the
        /// invited player's data.  Called after the invite payload has been
        /// persisted to the presence lobby.
        /// </summary>
        /// <param name="invitedPlayer">
        /// Identity of the player who was invited - used by UI to show the sent row.
        /// </param>
        public void RaiseInviteSent(PartyPlayerData invitedPlayer)
        {
            Debug.Log($"[SoapPartyEventBus] RaiseInviteSent → {invitedPlayer.DisplayName} ({invitedPlayer.PlayerId})");
            _data.OnInviteSent?.Raise(invitedPlayer);
        }

        /// <summary>
        /// Raises <see cref="HostConnectionDataSO.OnInviteReceived"/> carrying the
        /// invite payload.  Called when <c>RefreshAsync</c> detects a pending
        /// invite addressed to the local player.
        /// </summary>
        /// <param name="invite">
        /// Full invite payload: sender identity + session id.
        /// </param>
        public void RaiseInviteReceived(PartyInviteData invite)
        {
            Debug.Log($"[SoapPartyEventBus] RaiseInviteReceived from {invite.HostDisplayName} ({invite.HostPlayerId})");
            _data.OnInviteReceived?.Raise(invite);
        }

        /// <summary>
        /// Raises <see cref="HostConnectionDataSO.OnInviteResolved"/>.
        /// Called the moment the local user accepts, declines, or leaves -
        /// any action that terminates the pending invite's UI state.
        /// UI panels listen to dismiss stale invite rows.
        /// </summary>
        public void RaiseInviteResolved()
        {
            Debug.Log("[SoapPartyEventBus] RaiseInviteResolved");
            _data.OnInviteResolved?.Raise();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Party member events
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Raises <see cref="HostConnectionDataSO.OnPartyMemberJoined"/> carrying
        /// the new member's data.
        /// </summary>
        /// <param name="member">
        /// Identity of the player who joined - used by <c>PartySlotView</c> to
        /// populate a slot.
        /// </param>
        public void RaisePartyMemberJoined(PartyPlayerData member)
        {
            Debug.Log($"[SoapPartyEventBus] RaisePartyMemberJoined → {member.DisplayName} ({member.PlayerId})");
            _data.OnPartyMemberJoined?.Raise(member);
        }

        /// <summary>
        /// Raises <see cref="HostConnectionDataSO.OnPartyMemberLeft"/> carrying the
        /// departing member's data.
        /// </summary>
        /// <param name="member">
        /// Identity of the player who left - used by <c>PartySlotView</c> to clear
        /// a slot.
        /// </param>
        public void RaisePartyMemberLeft(PartyPlayerData member)
        {
            Debug.Log($"[SoapPartyEventBus] RaisePartyMemberLeft → {member.DisplayName} ({member.PlayerId})");
            _data.OnPartyMemberLeft?.Raise(member);
        }

        /// <summary>
        /// Raises <see cref="HostConnectionDataSO.OnPartyMemberKicked"/> carrying
        /// the kicked member's data.
        /// <para>
        /// Note: <see cref="HostConnectionDataSO.RemovePartyMember"/> already
        /// raises <c>OnPartyMemberKicked</c> internally.  Call this method only
        /// from paths that bypass <c>RemovePartyMember</c> and need to fire the
        /// event directly.
        /// </para>
        /// </summary>
        /// <param name="member">Identity of the kicked player.</param>
        public void RaisePartyMemberKicked(PartyPlayerData member)
        {
            Debug.Log($"[SoapPartyEventBus] RaisePartyMemberKicked → {member.DisplayName} ({member.PlayerId})");
            _data.OnPartyMemberKicked?.Raise(member);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Join-completed event
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Raises <see cref="HostConnectionDataSO.OnPartyJoinCompleted"/>.
        /// Called by <see cref="PartyInviteController"/> once the accepting client
        /// has a live Netcode connection and the scene has synced - the party is
        /// fully established.
        /// </summary>
        public void RaisePartyJoinCompleted()
        {
            Debug.Log("[SoapPartyEventBus] RaisePartyJoinCompleted");
            _data.OnPartyJoinCompleted?.Raise();
        }
    }
}
