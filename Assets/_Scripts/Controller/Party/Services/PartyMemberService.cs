// ─────────────────────────────────────────────────────────────────────────────
// PartyMemberService.cs
// Owns the party member list: diff, seed, repopulate, and clear.
//
// WHY this class exists:
//   The member-list mutation logic was scattered across four methods in
//   HostConnectionService: RefreshPartyMembersAsync (diff), RepopulatePartyMembers
//   FromSession (scene-reload rebuild), ApplyPostLobbyJoinState (seed), and
//   DeclineInviteAsync / KickPartyMemberAsync (clear-with-events).  Extracting
//   it here gives a single, auditable home for every write to
//   HostConnectionDataSO.PartyMembers and every raise of OnPartyMemberJoined /
//   OnPartyMemberLeft.
//
// KEY CONSTRAINT: this service does NOT touch NetworkManager, the UGS lobby SDK,
//   or the UGS session SDK.  It only reads from ISession.Players (already
//   fetched by the caller) and writes to the SOAP data container.
//
// LIFETIME:
//   Pure C# - no MonoBehaviour.  Instantiated as a field on
//   HostConnectionService for Phases 10-11.  Phase 12 registers it in Reflex DI.
//
// THREAD SAFETY:
//   Main-thread only.
// ─────────────────────────────────────────────────────────────────────────────

using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Manages the <see cref="HostConnectionDataSO.PartyMembers"/> SOAP list:
    /// diffs it against the live Relay session, seeds it on party create/join,
    /// and fires <c>OnPartyMemberJoined</c> / <c>OnPartyMemberLeft</c> SOAP events
    /// to keep party-slot UI in sync.
    ///
    /// <para>
    /// Does NOT call any UGS SDK or touch NetworkManager - it only reads from
    /// <c>ISession.Players</c> (already refreshed by <see cref="IPartySessionService"/>)
    /// and mutates the <see cref="HostConnectionDataSO.PartyMembers"/> list.
    /// </para>
    ///
    /// Lifetime: pure C# - no MonoBehaviour.  Created as a field on
    /// <see cref="HostConnectionService"/>; will be DI-registered in Phase 12.
    /// Thread-safety: main-thread only.
    /// </summary>
    public sealed class PartyMemberService : IPartyMemberService
    {
        // ─────────────────────────────────────────────────────────────────────
        // Constants
        // ─────────────────────────────────────────────────────────────────────

        // Property key names mirror the values written by PresenceLobbyService
        // and PartySessionService so that player-property reads are consistent.
        private const string DISPLAY_NAME_KEY = "displayName";
        private const string AVATAR_ID_KEY    = "avatarId";

        // ─────────────────────────────────────────────────────────────────────
        // Dependencies
        // ─────────────────────────────────────────────────────────────────────

        private readonly HostConnectionDataSO _connectionData;
        private readonly SoapPartyEventBus    _eventBus;

        // ─────────────────────────────────────────────────────────────────────
        // Construction
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Creates the party member service.
        /// </summary>
        /// <param name="connectionData">
        /// Shared SOAP data container.  The service reads <c>LocalPlayerData</c>,
        /// <c>LocalPlayerId</c>, and writes to <c>PartyMembers</c>.
        /// </param>
        /// <param name="eventBus">
        /// SOAP event bus used to raise <c>OnPartyMemberJoined</c> and
        /// <c>OnPartyMemberLeft</c>.
        /// </param>
        public PartyMemberService(HostConnectionDataSO connectionData, SoapPartyEventBus eventBus)
        {
            _connectionData = connectionData;
            _eventBus       = eventBus;
        }

        // ─────────────────────────────────────────────────────────────────────
        // IPartyMemberService
        // ─────────────────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public void SeedLocalPlayer(bool clearFirst = true)
        {
            if (clearFirst)
                _connectionData.PartyMembers?.Clear();

            if (!string.IsNullOrEmpty(_connectionData.LocalPlayerData.PlayerId))
                _connectionData.PartyMembers?.Add(_connectionData.LocalPlayerData);

            Debug.Log("[PartyMemberService] Seeded PartyMembers with local player.");
        }

        /// <inheritdoc/>
        public IReadOnlyList<string> SyncFromSession(ISession session, string localPlayerId)
        {
            if (_connectionData.PartyMembers == null) return System.Array.Empty<string>();

            // Build a fast-lookup set of current session player IDs.
            var sessionPlayerIds = new HashSet<string>();
            foreach (var p in session.Players)
                sessionPlayerIds.Add(p.Id);

            // Add players that are in the session but not yet in the SOAP list;
            // refresh identity (displayName/avatarId) on members already present.
            var joinedPlayerIds = new List<string>();
            foreach (var p in session.Players)
            {
                if (string.IsNullOrEmpty(p.Id) || p.Id == localPlayerId) continue;

                var memberData = ReadMemberData(p);

                int existingIdx = -1;
                for (int i = 0; i < _connectionData.PartyMembers.Count; i++)
                {
                    if (_connectionData.PartyMembers[i].PlayerId == p.Id) { existingIdx = i; break; }
                }

                if (existingIdx < 0)
                {
                    _connectionData.PartyMembers.Add(memberData);
                    _eventBus.RaisePartyMemberJoined(memberData);
                    joinedPlayerIds.Add(p.Id);
                    Debug.Log($"[PartyMemberService] Member joined: {memberData.DisplayName} ({p.Id})");
                }
                else
                {
                    var existing = _connectionData.PartyMembers[existingIdx];
                    if (existing.DisplayName != memberData.DisplayName ||
                        existing.AvatarId    != memberData.AvatarId)
                    {
                        // Identity refresh (mid-party rename), NOT a membership
                        // change: RemoveAt + Insert fires the list's item events
                        // (party slot UI repaints) WITHOUT raising the SOAP
                        // member-joined/left events, so no invite-clear or
                        // state-machine side effects trigger. Same pattern as
                        // HostConnectionService.RefreshOnlinePlayersDiff.
                        _connectionData.PartyMembers.RemoveAt(existingIdx);
                        _connectionData.PartyMembers.Insert(existingIdx, memberData);
                        Debug.Log($"[PartyMemberService] Member identity refreshed: '{existing.DisplayName}' -> '{memberData.DisplayName}' ({p.Id})");
                    }
                }
            }

            // Remove players that are in the SOAP list but no longer in the session.
            for (int i = _connectionData.PartyMembers.Count - 1; i >= 0; i--)
            {
                var member = _connectionData.PartyMembers[i];
                if (member.PlayerId == localPlayerId) continue;

                if (!sessionPlayerIds.Contains(member.PlayerId))
                {
                    _connectionData.PartyMembers.RemoveAt(i);
                    _eventBus.RaisePartyMemberLeft(member);
                    Debug.Log($"[PartyMemberService] Member left: {member.DisplayName} ({member.PlayerId})");
                }
            }

            return joinedPlayerIds;
        }

        /// <inheritdoc/>
        public void ClearSilent()
        {
            _connectionData.PartyMembers?.Clear();
            Debug.Log("[PartyMemberService] Party members cleared (silent).");
        }

        /// <inheritdoc/>
        public void ClearWithEvents(string localPlayerId)
        {
            if (_connectionData.PartyMembers == null) return;

            for (int i = _connectionData.PartyMembers.Count - 1; i >= 0; i--)
            {
                var member = _connectionData.PartyMembers[i];
                if (member.PlayerId == localPlayerId) continue;
                _connectionData.PartyMembers.RemoveAt(i);
                _eventBus.RaisePartyMemberLeft(member);
            }

            Debug.Log("[PartyMemberService] Party members cleared with Left events.");
        }

        /// <inheritdoc/>
        public PartyPlayerData ReadMemberData(IReadOnlyPlayer p)
        {
            string displayName = "Unknown Pilot";
            int    avatarId    = 0;

            if (p.Properties.TryGetValue(DISPLAY_NAME_KEY, out var dn) &&
                !string.IsNullOrEmpty(dn.Value))
                displayName = dn.Value;

            if (p.Properties.TryGetValue(AVATAR_ID_KEY, out var av) &&
                int.TryParse(av.Value, out int parsed))
                avatarId = parsed;

            return new PartyPlayerData(p.Id, displayName, avatarId);
        }
    }
}
