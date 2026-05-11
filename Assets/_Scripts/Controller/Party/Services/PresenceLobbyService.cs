// ─────────────────────────────────────────────────────────────────────────────
// PresenceLobbyService.cs
// Manages the UGS lobby-only session used for player discovery and invite
// property exchange.
//
// WHY this class exists:
//   Before extraction, HostConnectionService owned the full lifecycle of the
//   presence lobby: the _presenceLobby field, JoinPresenceLobbyAsync,
//   TryQueryAndJoinLobbyAsync, CreatePresenceLobbyAsync, DeleteOwnLobbyQuietly,
//   LeavePresenceLobbyAsync, and BuildLocalPlayerProperties.  All of these are
//   pure UGS session mechanics — none of them touch NetworkManager, invite
//   payloads, or member lists.  Extracting them here means:
//     1. HostConnectionService can hold the session reference through the
//        interface (IPresenceLobbyService.ActiveLobby) without owning it.
//     2. The join/create/race-condition logic is testable in isolation.
//     3. The player-property dict format is defined in one place.
//
// KEY CONSTRAINT:
//   This service must NOT touch NetworkManager, Relay allocations, or party
//   session logic.  Those are IPartySessionService's domain.  The presence
//   lobby is a lobby-only UGS session (no Relay) that coexists with any NM.
//
// LIFETIME:
//   Pure C# — no MonoBehaviour.  Instantiated as a field on
//   HostConnectionService for Phases 7-11.  Phase 12 registers it in Reflex DI.
//
// THREAD SAFETY:
//   Main-thread only.
// ─────────────────────────────────────────────────────────────────────────────

using System;
using System.Collections.Generic;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Manages the UGS lobby-only session used for player discovery and invite
    /// property exchange.  Handles the join-or-create flow (including the
    /// simultaneous-create race condition), graceful leave/delete, refresh, and
    /// property writes.  Does NOT touch NetworkManager or Relay.
    ///
    /// <para>
    /// The active session is exposed via <see cref="ActiveLobby"/>.  Call
    /// <see cref="JoinOrCreateAsync"/> once on auth sign-in.  Call
    /// <see cref="ForceReset"/> before attempting a reconnect (sets
    /// <see cref="ActiveLobby"/> to <c>null</c> so <see cref="JoinOrCreateAsync"/>
    /// will proceed rather than returning early).
    /// </para>
    ///
    /// Lifetime: pure C# — no MonoBehaviour.  Created as a field on
    /// <see cref="HostConnectionService"/>; will be DI-registered in Phase 12.
    /// Thread-safety: main-thread only.
    /// </summary>
    public sealed class PresenceLobbyService : IPresenceLobbyService
    {
        // ─────────────────────────────────────────────────────────────────────
        // Constants — keys and tuning
        // ─────────────────────────────────────────────────────────────────────

        private const string PRESENCE_LOBBY_GAME_MODE = "PRESENCE_LOBBY";
        private const string DISPLAY_NAME_KEY         = "displayName";
        private const string AVATAR_ID_KEY            = "avatarId";
        private const string PARTY_COUNT_KEY          = "partyCount";
        private const string PARTY_MAX_KEY            = "partyMax";
        private const string MATCH_NAME_KEY           = "matchName";
        private const string INVITE_PAYLOADS_KEY      = "invite_payloads";
        private const string JOINED_PARTY_KEY         = "joined_party";
        private const string ACCEPTED_INVITE_KEY      = "accepted_invite";

        /// <summary>
        /// After creating a lobby, wait this long before re-querying to detect
        /// a simultaneous-creation race (two clients both create a lobby at the
        /// same millisecond in MPPM or near-simultaneous launches).
        /// </summary>
        private const int LOBBY_RACE_SETTLE_MS   = 1500;
        private const int RATE_LIMIT_MAX_RETRIES = 3;
        private const int RATE_LIMIT_BASE_DELAY_MS = 2000;

        // ─────────────────────────────────────────────────────────────────────
        // Private state
        // ─────────────────────────────────────────────────────────────────────

        private readonly HostConnectionDataSO _connectionData;
        private readonly LobbyPropertyWriter  _propertyWriter;
        private ISession _activeLobby;
        private bool     _leaving;

        // ─────────────────────────────────────────────────────────────────────
        // Construction
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Creates the service.
        /// </summary>
        /// <param name="connectionData">
        /// Source of local player identity fields used when building player
        /// properties (LocalDisplayName, LocalAvatarId, PartyMembers, etc.).
        /// </param>
        /// <param name="propertyWriter">
        /// Owns the lobby mutex and SaveWithRetry pattern; used by
        /// <see cref="SavePropertiesAsync"/> to write player properties safely.
        /// </param>
        public PresenceLobbyService(HostConnectionDataSO connectionData, LobbyPropertyWriter propertyWriter)
        {
            _connectionData = connectionData;
            _propertyWriter = propertyWriter;
        }

        // ─────────────────────────────────────────────────────────────────────
        // IPresenceLobbyService — public API
        // ─────────────────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public ISession ActiveLobby => _activeLobby;

        /// <inheritdoc/>
        /// <remarks>
        /// Three-step algorithm:
        /// <list type="number">
        ///   <item>Query UGS for an existing presence lobby.</item>
        ///   <item>If none, create one; then re-query after a settling delay to detect
        ///         a simultaneous creation by another client (MPPM / near-simultaneous
        ///         launch). If a rival lobby is found, delete own and merge into it.</item>
        ///   <item>On any exception, fall back to creating a new lobby.</item>
        /// </list>
        /// </remarks>
        public async UniTask JoinOrCreateAsync(int maxPlayers)
        {
            if (_activeLobby != null) return;

            Debug.Log("[PresenceLobbyService] JoinOrCreateAsync — joining presence lobby...");
            try
            {
                _activeLobby = await TryQueryAndJoinAsync(maxPlayers);

                if (_activeLobby == null)
                {
                    await CreateAsync(maxPlayers);

                    // Re-query after a settling delay.  If a rival lobby appeared at
                    // the same instant (MPPM or near-simultaneous launch), join theirs
                    // and delete ours to avoid lobby fragmentation.
                    await UniTask.Delay(LOBBY_RACE_SETTLE_MS);
                    var rival = await TryQueryAndJoinAsync(maxPlayers);
                    if (rival != null)
                    {
                        Debug.Log("[PresenceLobbyService] Race detected — merging into existing lobby.");
                        await DeleteOwnLobbyQuietlyAsync();
                        _activeLobby = rival;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PresenceLobbyService] Join failed ({e.Message}) — creating new lobby as fallback.");
                if (_activeLobby == null)
                    await CreateAsync(maxPlayers);
            }

            Debug.Log($"[PresenceLobbyService] JoinOrCreateAsync complete — lobby: {_activeLobby?.Id ?? "NULL"}");
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Gracefully leaves (or deletes if host) the active lobby.  Exceptions
        /// from the UGS SDK are caught and swallowed so callers always reach the
        /// <c>finally</c> cleanup even on broken connections.
        /// </remarks>
        public async UniTask LeaveAsync()
        {
            if (_activeLobby == null || _leaving) return;
            _leaving = true;
            Debug.Log($"[PresenceLobbyService] LeaveAsync — lobby: {_activeLobby.Id}, IsHost: {_activeLobby.IsHost}");
            try
            {
                if (_activeLobby.IsHost)
                    await _activeLobby.AsHost().DeleteAsync();
                else
                    await _activeLobby.LeaveAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PresenceLobbyService] Leave error (session may already be gone): {e.Message}");
            }
            finally
            {
                _activeLobby = null;
                _leaving     = false;
            }
        }

        /// <inheritdoc/>
        public async UniTask RefreshAsync()
        {
            if (_activeLobby == null) return;
            await _activeLobby.RefreshAsync();
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Acquires the lobby mutex via <see cref="LobbyPropertyWriter.WriteAsync"/>;
        /// do NOT call while already holding the mutex (e.g. from inside
        /// <see cref="HostConnectionService"/>'s RefreshAsync cycle).  For in-mutex
        /// writes, set properties directly then call
        /// <see cref="LobbyPropertyWriter.SaveWithRetryAsync"/> explicitly.
        /// </remarks>
        public async UniTask SavePropertiesAsync(
            Dictionary<string, PlayerProperty> properties,
            string operationName)
        {
            if (_activeLobby == null || properties == null || properties.Count == 0) return;

            // Capture the lobby reference so the lambda doesn't re-evaluate the
            // property on each call — the session reference is stable during a write.
            var lobby = _activeLobby;
            await _propertyWriter.WriteAsync(
                lobby,
                () =>
                {
                    foreach (var kv in properties)
                        lobby.CurrentPlayer.SetProperty(kv.Key, kv.Value);
                },
                operationName);

            Debug.Log($"[PresenceLobbyService] SavePropertiesAsync({operationName}) — {properties.Count} props written.");
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Does NOT call the UGS SDK — just clears the internal reference so
        /// <see cref="JoinOrCreateAsync"/> will proceed rather than returning early.
        /// Call this when a broken connection is detected before triggering a
        /// reconnect via <see cref="JoinOrCreateAsync"/>.
        /// </remarks>
        public void ForceReset()
        {
            Debug.Log("[PresenceLobbyService] ForceReset — clearing active lobby reference for reconnect.");
            _activeLobby = null;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Internal — player property builder
        //
        // Kept internal (not on the interface) because it is also called from
        // HostConnectionService for party session joins.  Phase 9 (PartySessionService)
        // will absorb those call sites, at which point this method becomes private.
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Builds the standard player-property dictionary used when joining or
        /// creating UGS sessions (both presence lobby and party session).
        ///
        /// All 8 properties are always present so no key is missing on the first
        /// refresh; absent keys in UGS look the same as empty-value keys, which
        /// causes "invite_payloads not found" false negatives.
        /// </summary>
        internal Dictionary<string, PlayerProperty> BuildLocalPlayerProperties()
        {
            int partyCount = _connectionData.PartyMembers != null ? _connectionData.PartyMembers.Count : 0;
            int partyMax   = _connectionData.MaxPartySlots;

            return new Dictionary<string, PlayerProperty>
            {
                { DISPLAY_NAME_KEY,    new PlayerProperty(_connectionData.LocalDisplayName ?? "Pilot", VisibilityPropertyOptions.Public) },
                { AVATAR_ID_KEY,       new PlayerProperty(_connectionData.LocalAvatarId.ToString(),    VisibilityPropertyOptions.Public) },
                { PARTY_COUNT_KEY,     new PlayerProperty(partyCount.ToString(), VisibilityPropertyOptions.Public) },
                { PARTY_MAX_KEY,       new PlayerProperty(partyMax.ToString(),   VisibilityPropertyOptions.Public) },
                { MATCH_NAME_KEY,      new PlayerProperty(string.Empty,          VisibilityPropertyOptions.Public) },
                { JOINED_PARTY_KEY,    new PlayerProperty(string.Empty,          VisibilityPropertyOptions.Public) },
                { INVITE_PAYLOADS_KEY, new PlayerProperty(string.Empty,          VisibilityPropertyOptions.Public) },
                { ACCEPTED_INVITE_KEY, new PlayerProperty(string.Empty,          VisibilityPropertyOptions.Public) },
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // Private helpers
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Queries the UGS session list for an existing presence lobby and joins the
        /// first available one.  Returns <c>null</c> if no lobby exists or all join
        /// attempts fail.
        /// </summary>
        private async UniTask<ISession> TryQueryAndJoinAsync(int maxPlayers)
        {
            await UniTask.SwitchToMainThread();
            var queryOptions = new QuerySessionsOptions();
            queryOptions.FilterOptions.Add(
                new FilterOption(FilterField.StringIndex1, PRESENCE_LOBBY_GAME_MODE, FilterOperation.Equal));

            IList<ISessionInfo> sessions = null;
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    var results = await MultiplayerService.Instance.QuerySessionsAsync(queryOptions);
                    sessions = results.Sessions;
                    break;
                }
                catch (Exception qe) when (attempt < RATE_LIMIT_MAX_RETRIES && IsRateLimitException(qe))
                {
                    int delay = RATE_LIMIT_BASE_DELAY_MS * (1 << attempt);
                    Debug.LogWarning($"[PresenceLobbyService] Rate limited querying lobby — retry {attempt + 1}/{RATE_LIMIT_MAX_RETRIES} in {delay}ms");
                    await UniTask.Delay(delay);
                }
            }

            if (sessions == null || sessions.Count == 0) return null;

            foreach (var session in sessions)
            {
                // Skip if we somehow already hold a session with this id.
                if (_activeLobby != null && session.Id == _activeLobby.Id) continue;

                try
                {
                    var joined = await MultiplayerService.Instance.JoinSessionByIdAsync(
                        session.Id,
                        new JoinSessionOptions { PlayerProperties = BuildLocalPlayerProperties() });
                    Debug.Log($"[PresenceLobbyService] Joined existing presence lobby {joined.Id} (capacity {maxPlayers}).");
                    return joined;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[PresenceLobbyService] Failed to join session {session.Id}: {e.Message}");
                    if (IsRateLimitException(e))
                        await UniTask.Delay(RATE_LIMIT_BASE_DELAY_MS);
                }
            }

            return null;
        }

        /// <summary>
        /// Creates a new presence lobby and stores it in <see cref="_activeLobby"/>.
        /// Retries up to <see cref="RATE_LIMIT_MAX_RETRIES"/> times on HTTP 429.
        /// </summary>
        private async UniTask CreateAsync(int maxPlayers)
        {
            await UniTask.SwitchToMainThread();
            Debug.Log($"[PresenceLobbyService] Creating new presence lobby (maxPlayers={maxPlayers})...");
            try
            {
                var opts = new SessionOptions
                {
                    MaxPlayers        = maxPlayers,
                    IsLocked          = false,
                    IsPrivate         = false,
                    PlayerProperties  = BuildLocalPlayerProperties(),
                    SessionProperties = new Dictionary<string, SessionProperty>
                    {
                        {
                            "gameMode",
                            new SessionProperty(
                                PRESENCE_LOBBY_GAME_MODE,
                                VisibilityPropertyOptions.Public,
                                PropertyIndex.String1)
                        }
                    }
                };

                for (int attempt = 0; ; attempt++)
                {
                    try
                    {
                        _activeLobby = await MultiplayerService.Instance.CreateSessionAsync(opts);
                        Debug.Log($"[PresenceLobbyService] Created presence lobby {_activeLobby.Id}.");
                        return;
                    }
                    catch (Exception re) when (attempt < RATE_LIMIT_MAX_RETRIES && IsRateLimitException(re))
                    {
                        int delay = RATE_LIMIT_BASE_DELAY_MS * (1 << attempt);
                        Debug.LogWarning($"[PresenceLobbyService] Rate limited creating lobby — retry {attempt + 1}/{RATE_LIMIT_MAX_RETRIES} in {delay}ms");
                        await UniTask.Delay(delay);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[PresenceLobbyService] Could not create presence lobby: {e.Message}");
            }
        }

        /// <summary>
        /// Leaves or deletes the active lobby without throwing.  Used to release
        /// a lobby that lost a race condition (a rival lobby was created at the
        /// same moment and we are merging into theirs).
        /// </summary>
        private async UniTask DeleteOwnLobbyQuietlyAsync()
        {
            if (_activeLobby == null) return;
            await UniTask.SwitchToMainThread();
            Debug.Log($"[PresenceLobbyService] DeleteOwnLobbyQuietly — releasing race-lost lobby {_activeLobby.Id}.");
            try
            {
                if (_activeLobby.IsHost)
                    await _activeLobby.AsHost().DeleteAsync();
                else
                    await _activeLobby.LeaveAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PresenceLobbyService] DeleteOwnLobby error: {e.Message}");
            }
            finally
            {
                _activeLobby = null;
            }
        }

        /// <summary>
        /// True when the exception is a UGS HTTP 429 Too Many Requests response.
        /// Used in <c>catch ... when (...)</c> clauses to distinguish rate-limit
        /// errors (retry-able) from other errors (propagate).
        /// </summary>
        private static bool IsRateLimitException(Exception e) =>
            e.Message != null && e.Message.Contains("Too Many Requests");
    }
}
