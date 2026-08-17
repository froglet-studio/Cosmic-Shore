// ─────────────────────────────────────────────────────────────────────────────
// AcceptanceSignalService.cs
// Orchestrates the PENDING-sentinel three-phase acceptance protocol.
//
// WHY this class exists:
//   The accept handshake lives entirely in lobby player-properties - no server
//   side, no Relay required - yet it spans four distinct operations that were
//   previously scattered as private methods on HostConnectionService:
//     ScanForAcceptanceSignalsAsync, PublishAcceptanceSignalAsync,
//     WaitForRealSessionIdAsync, RepublishOutgoingInvitesWithRealSessionIdAsync.
//   Extracting them here gives the protocol a single, documentable home and
//   makes each phase independently testable without a live MonoBehaviour.
//
// PROTOCOL OVERVIEW:
//   Phase 1 - Sender writes invite payloads with PENDING as session id.
//   Phase 2 - Recipient calls PublishSignalAsync to write
//              accepted_invite = hostPlayerId to their own player property.
//   Phase 3 - Sender's refresh calls ScanForSignals; on detection it creates
//              the Relay session (outside this service) then calls
//              RepublishWithRealIdAsync to replace PENDING with the real id.
//   Phase 4 - Recipient's WaitForRealSessionIdAsync polls until the real id
//              appears, then joins the Relay session (outside this service).
//
// USAGE:
//   HostConnectionService creates one instance in Awake and owns the
//   session-creation and state-transition logic that wraps these calls.
//   All methods are stateless - the service holds no mutable fields.
//
// LIFETIME:
//   Pure C# - no MonoBehaviour.  Instantiated as a field on
//   HostConnectionService for Phases 8-11.  Phase 12 registers it in Reflex DI.
//
// THREAD SAFETY:
//   Main-thread only.
// ─────────────────────────────────────────────────────────────────────────────

using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Orchestrates the PENDING-sentinel three-phase acceptance handshake that
    /// lets a party-invite recipient and host agree on a Relay session id through
    /// lobby player properties alone - no Relay required until the host creates
    /// the session on acceptance detection.
    ///
    /// <para>
    /// This service is stateless: all mutable state lives in the lobby session
    /// objects and in <see cref="InviteService"/>, which are passed as parameters.
    /// HostConnectionService owns the session-creation and state-transition logic
    /// that wraps these calls.
    /// </para>
    ///
    /// Lifetime: pure C# - no MonoBehaviour.  Created as a field on
    /// <see cref="HostConnectionService"/>; will be DI-registered in Phase 12.
    /// Thread-safety: main-thread only.
    /// </summary>
    public sealed class AcceptanceSignalService
    {
        // ─────────────────────────────────────────────────────────────────────
        // Constants - lobby player-property keys
        // ─────────────────────────────────────────────────────────────────────

        private const string ACCEPTED_INVITE_KEY = PartyLobbyKeys.AcceptedInvite;
        private const string INVITE_PAYLOADS_KEY = PartyLobbyKeys.InvitePayloads;

        // ─────────────────────────────────────────────────────────────────────
        // Public API - host-side (scan + republish)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Scans all lobby players for an acceptance signal directed at the local
        /// player.  Returns the accepting player's id, or <c>null</c> if no signal
        /// is present.
        ///
        /// <para>
        /// A "signal" is a lobby player whose <c>accepted_invite</c> property value
        /// equals <paramref name="localPlayerId"/> AND whose player id appears in
        /// <paramref name="invitedPlayerIds"/> (we only care about people we invited).
        /// </para>
        /// </summary>
        /// <param name="lobby">The live presence lobby session.  Must not be null.</param>
        /// <param name="localPlayerId">
        /// The local player's UGS player id.  Used both to skip self and to verify
        /// the acceptance is directed at us.
        /// </param>
        /// <param name="invitedPlayerIds">
        /// The set of player ids to whom the local player has sent outstanding invites
        /// (typically <see cref="InviteService.OutgoingTargets"/>).
        /// </param>
        /// <returns>
        /// The accepting player's id, or <c>null</c> if no signal is found.
        /// </returns>
        public string ScanForSignals(
            ISession lobby,
            string   localPlayerId,
            IReadOnlyCollection<string> invitedPlayerIds)
        {
            foreach (var p in lobby.Players)
            {
                if (p.Id == localPlayerId) continue;
                if (!invitedPlayerIds.Contains(p.Id)) continue;
                if (!p.Properties.TryGetValue(ACCEPTED_INVITE_KEY, out var prop)) continue;

                string value = prop?.Value ?? string.Empty;
                if (value != localPlayerId)
                {
                    if (!string.IsNullOrEmpty(value))
                        Debug.Log($"[AcceptanceSignalService] Player {p.Id} accepted_invite='{value}' - not us ('{localPlayerId}'), skipping.");
                    continue;
                }

                Debug.Log($"[AcceptanceSignalService] Acceptance signal from {p.Id}.");
                return p.Id;
            }
            return null;
        }

        /// <summary>
        /// After the host has created the Relay session, updates all outgoing invite
        /// payloads from PENDING to <paramref name="realSessionId"/>, pre-refreshes
        /// the lobby (to avoid stale player-data writes), sets the composite payload
        /// property on the current player, and saves with retry.
        ///
        /// <para>
        /// Caller must ensure <paramref name="invites"/> has been updated via
        /// <see cref="InviteService.UpdatePayloadsWithRealSessionId"/> BEFORE calling
        /// this method, or call this method which handles that update internally.
        /// </para>
        /// </summary>
        /// <param name="lobbyService">
        /// The active presence lobby service.  Used for pre-save refresh and to
        /// obtain the current-player reference for property writes.
        /// </param>
        /// <param name="realSessionId">The Relay session id that replaces PENDING.</param>
        /// <param name="invites">
        /// The invite service that holds the outgoing payload dictionary.  Its
        /// <see cref="InviteService.UpdatePayloadsWithRealSessionId"/> is called here
        /// and its <see cref="InviteService.SerializeAll"/> produces the composite value.
        /// </param>
        /// <param name="writer">
        /// Property writer used for the save-with-retry pattern.
        /// </param>
        public async UniTask RepublishWithRealIdAsync(
            IPresenceLobbyService lobbyService,
            string                realSessionId,
            InviteService         invites,
            LobbyPropertyWriter   writer)
        {
            invites.UpdatePayloadsWithRealSessionId(realSessionId);

            // Party session creation can take 2-3s (NM shutdown + Relay alloc),
            // so the SDK's internal player index may be stale.  Refresh before
            // saving to avoid writing to a stale CurrentPlayer reference.
            try { await lobbyService.RefreshAsync(); }
            catch (Exception e)
            {
                Debug.LogWarning($"[AcceptanceSignalService] Pre-save refresh failed (non-fatal) ({e.GetType().Name}): {e}");
            }

            var lobby = lobbyService.ActiveLobby;
            if (lobby == null)
            {
                Debug.LogWarning("[AcceptanceSignalService] RepublishWithRealId: lobby became null after refresh - skipping save.");
                return;
            }

            string composite = invites.SerializeAll();
            lobby.CurrentPlayer.SetProperty(INVITE_PAYLOADS_KEY,
                new PlayerProperty(composite, VisibilityPropertyOptions.Public));

            await writer.SaveWithRetryAsync(lobby);
            Debug.Log($"[AcceptanceSignalService] Republished {invites.OutgoingCount} invite(s) with real session id {realSessionId}.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // Public API - recipient-side (publish signal + poll for real id)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Writes the local player's acceptance signal to the presence lobby:
        /// <c>accepted_invite = hostPlayerId</c>.  The host's next refresh cycle
        /// will detect this signal via <see cref="ScanForSignals"/> and create the
        /// Relay session.
        /// </summary>
        /// <param name="lobby">The live presence lobby session.  Must not be null.</param>
        /// <param name="hostPlayerId">
        /// The UGS player id of the inviting host.  Written as the property value so
        /// the host can verify the acceptance is directed at them.
        /// </param>
        /// <param name="writer">
        /// Property writer used for the mutex + refresh + save-with-retry pattern.
        /// Do NOT call while already holding <c>_lobbyMutex</c>.
        /// </param>
        public async UniTask PublishSignalAsync(
            ISession            lobby,
            string              hostPlayerId,
            LobbyPropertyWriter writer)
        {
            await writer.WriteAsync(
                lobby,
                () =>
                {
                    lobby.CurrentPlayer.SetProperty(ACCEPTED_INVITE_KEY,
                        new PlayerProperty(hostPlayerId, VisibilityPropertyOptions.Public));
                    Debug.Log($"[AcceptanceSignalService] Published acceptance signal to host {hostPlayerId}.");
                },
                "PublishAcceptanceSignal");
        }

        /// <summary>
        /// Polls the presence lobby until the host republishes the invite payload with
        /// a real (non-PENDING) session id for the local player, then returns that id.
        ///
        /// <para>
        /// UGS lobby refresh propagates in ~750ms.  The poll interval is 400ms so the
        /// recipient sees the real id within 1-2 polls of the host republishing.
        /// </para>
        /// </summary>
        /// <param name="lobbyService">
        /// The active lobby service.  Used to get the current lobby on each poll
        /// tick, since the lobby reference may be reset mid-wait.
        /// </param>
        /// <param name="hostPlayerId">The UGS player id of the inviting host.</param>
        /// <param name="localPlayerId">
        /// The local player's UGS player id.  Used to find the invite line
        /// targeting us in the host's composite payload property.
        /// </param>
        /// <param name="timeoutSeconds">
        /// Maximum wait time.  Default 7s covers the host's session-creation
        /// round-trip (NM shutdown + Relay allocation + lobby republish).
        /// </param>
        /// <returns>The real Relay session id.</returns>
        /// <exception cref="TimeoutException">
        /// Thrown if the real session id does not appear within
        /// <paramref name="timeoutSeconds"/>.
        /// </exception>
        public async UniTask<string> WaitForRealSessionIdAsync(
            IPresenceLobbyService lobbyService,
            string                hostPlayerId,
            string                localPlayerId,
            float                 timeoutSeconds = 7f)
        {
            float deadline   = UnityEngine.Time.unscaledTime + timeoutSeconds;
            int   pollCount  = 0;

            while (UnityEngine.Time.unscaledTime < deadline)
            {
                await UniTask.Delay(400);
                pollCount++;

                var lobby = lobbyService.ActiveLobby;
                if (lobby == null)
                {
                    Debug.LogWarning("[AcceptanceSignalService] WaitForRealSessionId: lobby reset - retrying next poll.");
                    continue;
                }

                bool hostFound = false;
                foreach (var p in lobby.Players)
                {
                    if (p.Id != hostPlayerId) continue;
                    hostFound = true;

                    if (!p.Properties.TryGetValue(INVITE_PAYLOADS_KEY, out var prop))
                    {
                        if (pollCount % 5 == 1)
                            Debug.Log($"[AcceptanceSignalService] WaitForRealSessionId poll#{pollCount}: host found but no {INVITE_PAYLOADS_KEY} yet.");
                        break;
                    }
                    if (string.IsNullOrEmpty(prop?.Value))
                    {
                        if (pollCount % 5 == 1)
                            Debug.Log($"[AcceptanceSignalService] WaitForRealSessionId poll#{pollCount}: {INVITE_PAYLOADS_KEY} is empty.");
                        break;
                    }

                    bool foundForUs = false;
                    foreach (var line in prop.Value.Split(InviteService.LINE_SEPARATOR))
                    {
                        // Parse uses InviteService's static ParseLine which matches format
                        // targetId|senderId|sessionId|displayName|avatarId
                        var parsed = InviteService.ParseLine(line);
                        if (!parsed.HasValue) continue;
                        if (parsed.Value.targetId != localPlayerId) continue;
                        foundForUs = true;

                        var sid = parsed.Value.invite.PartySessionId;
                        if (!string.IsNullOrEmpty(sid) && sid != InviteService.PENDING_SESSION_ID)
                        {
                            Debug.Log($"[AcceptanceSignalService] WaitForRealSessionId resolved after {pollCount} polls: {sid}");
                            return sid;
                        }
                        if (pollCount % 5 == 1)
                            Debug.Log($"[AcceptanceSignalService] WaitForRealSessionId poll#{pollCount}: session id still PENDING.");
                    }
                    if (!foundForUs && pollCount % 5 == 1)
                        Debug.Log($"[AcceptanceSignalService] WaitForRealSessionId poll#{pollCount}: payload present but no line targeting us.");
                    break;
                }

                if (!hostFound && pollCount % 5 == 1)
                    Debug.Log($"[AcceptanceSignalService] WaitForRealSessionId poll#{pollCount}: host {hostPlayerId} not in lobby ({lobby.Players.Count} players).");
            }

            throw new TimeoutException(
                $"[AcceptanceSignalService] Host {hostPlayerId} did not publish a real session id " +
                $"within {timeoutSeconds}s (after {pollCount} polls).");
        }
    }
}
