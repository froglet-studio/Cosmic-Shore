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
        private const string DISPLAY_NAME_KEY = PartyLobbyKeys.DisplayName;
        private const string AVATAR_ID_KEY    = PartyLobbyKeys.AvatarId;

        // ─────────────────────────────────────────────────────────────────────
        // Dependencies
        // ─────────────────────────────────────────────────────────────────────

        private readonly HostConnectionDataSO _connectionData;
        private readonly SoapPartyEventBus    _eventBus;

        /// <summary>
        /// Consecutive session reads in which a member must be absent before we
        /// treat them as gone.
        ///
        /// <para>
        /// One absence is not evidence of departure. The session read can come
        /// back short for reasons that have nothing to do with the member: the
        /// UGS SDK's documented stale-cache defect
        /// (<c>Docs/PresenceSystem/BUGS.md</c> B1/B6), or a refresh landing
        /// mid-mutation. Acting on a single short read raised
        /// <c>OnPartyMemberLeft</c>, which flips Friends presence and forces a
        /// full <c>FriendsListPanel</c> rebuild - so a transient blip presented
        /// as a party member vanishing and then reappearing.
        /// </para>
        ///
        /// <para>
        /// The cost of waiting is one extra refresh interval on a genuine
        /// departure, and only for the cases with no push signal - an explicit
        /// leave arrives via <c>ISession.PlayerLeaving</c> and the host's
        /// <c>OnClientDisconnect</c> backstop routes through
        /// <c>ReconcilePartyMembersNow</c>, neither of which waits on this.
        /// </para>
        /// </summary>
        private const int MISSED_READS_BEFORE_REMOVAL = 2;

        /// <summary>
        /// Consecutive absent-read counts keyed by PlayerId. Cleared the moment a
        /// member is seen again, so an intermittent connection cannot accumulate
        /// strikes across unrelated blips.
        /// </summary>
        private readonly Dictionary<string, int> _missedReads = new();

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
            bool changed = false;

            if (clearFirst && _connectionData.PartyMembers is { Count: > 0 })
            {
                _connectionData.PartyMembers.Clear();
                changed = true;
            }

            if (string.IsNullOrEmpty(_connectionData.LocalPlayerData.PlayerId))
            {
                Debug.Log("[PartyMemberService] Seeded PartyMembers with local player.");
                if (changed) _eventBus.RequestPartyRosterChanged();
                return;
            }

            // Idempotent: with clearFirst == false the list may already hold our
            // row, and PartyPlayerData's Equals/GetHashCode key on PlayerId only,
            // so a blind Add would leave two entries for the same player - which
            // ArcadeLobbyList renders as the local player occupying two slots.
            // Required now that the presence-reconnect path seeds without
            // clearing (see HostConnectionService.ApplyPostLobbyJoinState).
            if (_connectionData.PartyMembers != null &&
                !_connectionData.PartyMembers.Contains(_connectionData.LocalPlayerData))
            {
                _connectionData.PartyMembers.Add(_connectionData.LocalPlayerData);
                changed = true;
            }

            Debug.Log("[PartyMemberService] Seeded PartyMembers with local player.");
            if (changed) _eventBus.RequestPartyRosterChanged();
        }

        /// <inheritdoc/>
        public IReadOnlyList<string> SyncFromSession(ISession session, string localPlayerId)
        {
            if (_connectionData.PartyMembers == null) return System.Array.Empty<string>();

            // Build a fast-lookup set of current session player IDs.
            var sessionPlayerIds = new HashSet<string>();
            foreach (var p in session.Players)
                sessionPlayerIds.Add(p.Id);

            // Tracks whether this sync moved the roster at all. A steady-state
            // poll tick changes nothing, and raising OnPartyRosterChanged on
            // every one of those would make a coalesced repaint signal fire at
            // poll cadence forever - which is precisely the per-tick churn the
            // channel exists to replace.
            bool rosterChanged = false;

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
                    rosterChanged = true;
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
                        // A rename is not a membership change - hence no
                        // joined/left raise above - but it IS a repaint, which
                        // is exactly what OnPartyRosterChanged signals.
                        rosterChanged = true;
                        Debug.Log($"[PartyMemberService] Member identity refreshed: '{existing.DisplayName}' -> '{memberData.DisplayName}' ({p.Id})");
                    }
                }
            }

            // Remove players that are in the SOAP list but no longer in the
            // session - but only after MISSED_READS_BEFORE_REMOVAL consecutive
            // absences. See the field doc on _missedReads for why one absence is
            // not enough evidence.
            for (int i = _connectionData.PartyMembers.Count - 1; i >= 0; i--)
            {
                var member = _connectionData.PartyMembers[i];
                if (member.PlayerId == localPlayerId) continue;

                if (sessionPlayerIds.Contains(member.PlayerId))
                {
                    _missedReads.Remove(member.PlayerId);
                    continue;
                }

                _missedReads.TryGetValue(member.PlayerId, out int misses);
                misses++;
                _missedReads[member.PlayerId] = misses;

                if (misses < MISSED_READS_BEFORE_REMOVAL)
                {
                    Debug.Log(
                        $"[PartyMemberService] Member {member.DisplayName} ({member.PlayerId}) absent from " +
                        $"session read {misses}/{MISSED_READS_BEFORE_REMOVAL} - holding the slot.");
                    continue;
                }

                _missedReads.Remove(member.PlayerId);
                _connectionData.PartyMembers.RemoveAt(i);
                _eventBus.RaisePartyMemberLeft(member);
                rosterChanged = true;
                Debug.Log($"[PartyMemberService] Member left: {member.DisplayName} ({member.PlayerId})");
            }

            // ONE raise for the whole sync, after the list has settled. A sync
            // that adds three members costs one repaint here, where the
            // per-member events above cost three. Raising inside the loops would
            // also let a listener observe a half-applied roster.
            if (rosterChanged) _eventBus.RequestPartyRosterChanged();

            return joinedPlayerIds;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// "Silent" means no per-member <c>OnPartyMemberLeft</c> raises - those
        /// carry side effects (invite-row clearing, Friends presence writes)
        /// that a wholesale teardown must not trigger. It does NOT mean the
        /// roster moved invisibly: anything rendering party size still has to
        /// repaint, so the coalesced roster signal is raised.
        /// </remarks>
        public void ClearSilent()
        {
            bool changed = _connectionData.PartyMembers is { Count: > 0 };
            _connectionData.PartyMembers?.Clear();
            Debug.Log("[PartyMemberService] Party members cleared (silent).");
            if (changed) _eventBus.RequestPartyRosterChanged();
        }

        /// <inheritdoc/>
        public void ClearWithEvents(string localPlayerId)
        {
            if (_connectionData.PartyMembers == null) return;

            bool changed = false;
            for (int i = _connectionData.PartyMembers.Count - 1; i >= 0; i--)
            {
                var member = _connectionData.PartyMembers[i];
                if (member.PlayerId == localPlayerId) continue;
                _connectionData.PartyMembers.RemoveAt(i);
                _eventBus.RaisePartyMemberLeft(member);
                changed = true;
            }

            Debug.Log("[PartyMemberService] Party members cleared with Left events.");
            if (changed) _eventBus.RequestPartyRosterChanged();
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
