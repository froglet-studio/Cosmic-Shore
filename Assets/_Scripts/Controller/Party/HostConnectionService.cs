using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CosmicShore.UI;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Obvious.Soap;
using Reflex.Attributes;
using Unity.Services.Multiplayer;
using UnityEngine;
using CosmicShore.ScriptableObjects;
namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Single-responsibility service that establishes and maintains the host
    /// connection (presence lobby) at the main-menu stage.
    ///
    /// Writes all state into <see cref="HostConnectionDataSO"/> so every UI
    /// consumer can react via SOAP events / lists without coupling to this class.
    /// </summary>
    public class HostConnectionService : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────────
        // Inspector
        // ─────────────────────────────────────────────────────────────────────

        [Header("Auth (Source of Truth)")]
        [SerializeField] private AuthenticationDataVariable authenticationDataVariable;
        private AuthenticationData AuthData => authenticationDataVariable.Value;

        [Header("SOAP Data Container")]
        [SerializeField] private HostConnectionDataSO connectionData;

        [Header("Presence Lobby")]
        [Tooltip("Max simultaneous players in the global presence lobby.")]
        [SerializeField] private int presenceLobbyMaxPlayers = 100;

        [Tooltip("How often (seconds) to refresh the online player list and check for invites.")]
        [SerializeField] private float refreshIntervalSeconds = 3f;

        [Inject] private PlayerDataService playerDataService;

        // ─────────────────────────────────────────────────────────────────────
        // Static access
        // ─────────────────────────────────────────────────────────────────────

        public static HostConnectionService Instance { get; private set; }

        // ─────────────────────────────────────────────────────────────────────
        // Internal state
        // ─────────────────────────────────────────────────────────────────────

        private ISession _presenceLobby;
        private ISession _partySession;
        private float _refreshTimer;
        private bool _initialized;
        private bool _joining;
        private bool _leaving;
        private PartyInviteData? _lastFiredInvite;
        private string _currentInviteTargetId;
        private bool _refreshSuspended;

        private const string PRESENCE_LOBBY_GAME_MODE = "PRESENCE_LOBBY";
        private const string DISPLAY_NAME_KEY = "displayName";
        private const string AVATAR_ID_KEY = "avatarId";
        private const string INVITE_TARGET_KEY = "invite_target";
        private const string INVITE_DATA_KEY = "invite_data";

        /// <summary>
        /// Milliseconds to wait after creating a lobby before re-querying,
        /// giving a near-simultaneous second instance time to also create.
        /// If a rival lobby is detected, we merge into it.
        /// </summary>
        private const int LOBBY_RACE_SETTLE_MS = 1500;

        // ─────────────────────────────────────────────────────────────────────
        // Unity Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        async void Start()
        {
            while (!IsAuthSignedInAndHasId())
                await Task.Delay(300);

            // HandleSignedInEvent may have already completed via SOAP event.
            if (_initialized) return;

            SyncLocalIdentity();
            await JoinPresenceLobbyAsync();

            // Create Relay-backed party session so the NetworkManager starts as a
            // Relay host. Wrapped in try-catch so a Relay failure does not block
            // _initialized — the presence lobby and refresh loop must always work.
            // SendInviteAsync has a lazy-creation fallback if _partySession is null.
            try
            {
                await CreatePartySessionAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[HostConnectionService] Party session creation failed (Relay may be unavailable). " +
                    $"Invite send will retry on demand. Error: {e.Message}");
            }

            _initialized = true;
            DebugExtensions.LogColored(
                $"[HostConnectionService] Initialized (Start) — lobby: {_presenceLobby?.Id ?? "NULL"}, " +
                $"partySession: {_partySession?.Id ?? "NULL"}, " +
                $"localId: {connectionData.LocalPlayerId}",
                Color.green);

            // Immediate first refresh so OnlinePlayers is populated
            // before the user opens the panel (don't wait 3 seconds).
            RefreshAsync().Forget();
        }

        void Update()
        {
            if (!_initialized || _presenceLobby == null || _refreshSuspended) return;

            _refreshTimer += Time.deltaTime;
            if (_refreshTimer >= refreshIntervalSeconds)
            {
                _refreshTimer = 0f;
                RefreshAsync().Forget();
            }
        }

        async void OnDestroy()
        {
            await LeavePresenceLobbyAsync();

            if (Instance == this)
                Instance = null;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Public: Auth hooks (wire via SOAP EventListenerNoParam in inspector)
        // ─────────────────────────────────────────────────────────────────────

        public async void HandleSignedInEvent()
        {
            if (_initialized || _joining) return;
            if (!IsAuthSignedInAndHasId()) return;

            SyncLocalIdentity();

            _joining = true;
            try
            {
                await JoinPresenceLobbyAsync();

                // Create Relay-backed party session so the NetworkManager starts as a
                // Relay host. Failure is non-fatal — the presence lobby and refresh loop
                // must always work. SendInviteAsync has a lazy-creation fallback.
                try
                {
                    await CreatePartySessionAsync();
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[HostConnectionService] Party session creation failed (Relay may be unavailable). " +
                        $"Invite send will retry on demand. Error: {e.Message}");
                }

                _initialized = true;
                DebugExtensions.LogColored(
                    $"[HostConnectionService] Initialized (HandleSignedInEvent) — lobby: {_presenceLobby?.Id ?? "NULL"}, " +
                    $"partySession: {_partySession?.Id ?? "NULL"}, " +
                    $"localId: {connectionData.LocalPlayerId}",
                    Color.green);

                // Immediate refresh so OnlinePlayers is populated right away.
                RefreshAsync().Forget();
            }
            finally { _joining = false; }
        }

        public async void HandleSignedOutEvent()
        {
            _initialized = false;
            connectionData.ResetRuntimeData();
            await LeavePresenceLobbyAsync();
            connectionData.OnHostConnectionLost?.Raise();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Public: Invite API
        // ─────────────────────────────────────────────────────────────────────

        public async Task SendInviteAsync(string targetPlayerId)
        {
            DebugExtensions.LogColored(
                $"[INVITE-SEND] SendInviteAsync called — target: {targetPlayerId}", Color.cyan);

            if (_presenceLobby == null)
            {
                DebugExtensions.LogErrorColored(
                    "[INVITE-SEND] ABORT — _presenceLobby is null", Color.red);
                return;
            }

            _refreshSuspended = true;
            try
            {
                SyncLocalIdentity();
                DebugExtensions.LogColored(
                    $"[INVITE-SEND] LocalPlayerId: {connectionData.LocalPlayerId}, " +
                    $"DisplayName: {connectionData.LocalDisplayName}", Color.cyan);

                if (_partySession == null)
                {
                    DebugExtensions.LogWarningColored(
                        "[INVITE-SEND] _partySession is null — creating on demand...", Color.yellow);
                    await CreatePartySessionAsync();
                }

                DebugExtensions.LogColored(
                    $"[INVITE-SEND] PartySession ID: {_partySession?.Id ?? "NULL"}", Color.cyan);

                string inviteData = $"{connectionData.LocalPlayerId}|{_partySession.Id}|{connectionData.LocalDisplayName}|{connectionData.LocalAvatarId}";

                DebugExtensions.LogColored(
                    $"[INVITE-SEND] Setting properties — invite_target: '{targetPlayerId}', " +
                    $"invite_data: '{inviteData}'", Color.cyan);

                _presenceLobby.CurrentPlayer.SetProperty(INVITE_TARGET_KEY,
                    new PlayerProperty(targetPlayerId, VisibilityPropertyOptions.Public));
                _presenceLobby.CurrentPlayer.SetProperty(INVITE_DATA_KEY,
                    new PlayerProperty(inviteData, VisibilityPropertyOptions.Public));
                await _presenceLobby.SaveCurrentPlayerDataAsync();

                _currentInviteTargetId = targetPlayerId;

                DebugExtensions.LogColored(
                    "[INVITE-SEND] SaveCurrentPlayerDataAsync completed — properties persisted",
                    Color.green);

                foreach (var player in connectionData.OnlinePlayers.ToList())
                {
                    if (player.PlayerId == targetPlayerId)
                    {
                        connectionData.OnInviteSent?.Raise(player);
                        DebugExtensions.LogColored(
                            $"[INVITE-SEND] OnInviteSent raised for {player.DisplayName}",
                            Color.green);
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                DebugExtensions.LogErrorColored(
                    $"[INVITE-SEND] ERROR: {e.Message}\n{e.StackTrace}", Color.red);
            }
            finally
            {
                _refreshSuspended = false;
            }
        }

        public async Task AcceptInviteAsync(PartyInviteData invite)
        {
            try
            {
                SyncLocalIdentity();

                _partySession = await MultiplayerService.Instance.JoinSessionByIdAsync(
                    invite.PartySessionId,
                    new JoinSessionOptions { PlayerProperties = BuildLocalPlayerProperties() });

                connectionData.IsHost = false;

                // Add self + host to party members
                connectionData.PartyMembers?.Clear();
                connectionData.PartyMembers?.Add(connectionData.LocalPlayerData);
                var hostData = new PartyPlayerData(invite.HostPlayerId, invite.HostDisplayName, invite.HostAvatarId);
                connectionData.PartyMembers?.Add(hostData);
                connectionData.OnPartyMemberJoined?.Raise(hostData);

                // Keep _lastFiredInvite set so the dedup guard prevents
                // re-triggering if the host is slow to clear their properties.
                Debug.Log($"[HostConnectionService] Joined party {_partySession.Id}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[HostConnectionService] AcceptInvite error: {e.Message}");
            }
        }

        public async Task DeclineInviteAsync()
        {
            _lastFiredInvite = null;
            await RequestClearInviteAsync();
        }

        /// <summary>
        /// Kicks a remote player from the party. Host-only.
        /// Removes from the local PartyMembers list and fires OnPartyMemberKicked.
        /// </summary>
        public async Task KickPartyMemberAsync(string playerId)
        {
            if (!connectionData.IsHost)
            {
                Debug.LogWarning("[HostConnectionService] Only the host can kick party members.");
                return;
            }

            if (playerId == connectionData.LocalPlayerId)
            {
                Debug.LogWarning("[HostConnectionService] Cannot kick yourself from the party.");
                return;
            }

            connectionData.RemovePartyMember(playerId);

            // If we have a party session, attempt to remove the player from it
            if (_partySession != null)
            {
                try
                {
                    await _partySession.AsHost().RemovePlayerAsync(playerId);
                    Debug.Log($"[HostConnectionService] Kicked {playerId} from party session.");
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[HostConnectionService] KickPartyMember session error: {e.Message}");
                }
            }
        }

        public ISession PartySession => _partySession;

        /// <summary>
        /// Public wrapper for party session creation.
        /// Used by <see cref="PartyInviteController"/> after shutting down the local
        /// NetworkManager, so the Relay session can start a fresh host.
        /// </summary>
        public async Task CreatePartySessionPublicAsync()
        {
            if (_partySession != null)
            {
                Debug.Log("[HostConnectionService] Party session already exists.");
                return;
            }

            SyncLocalIdentity();
            await CreatePartySessionAsync();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Presence Lobby
        // ─────────────────────────────────────────────────────────────────────

        private async Task JoinPresenceLobbyAsync()
        {
            if (_presenceLobby != null) return;

            // The presence lobby is a lobby-only session (no Relay) used purely
            // for player discovery and invite property exchange. It coexists
            // safely with an active NetworkManager host/client.

            try
            {
                _presenceLobby = await TryQueryAndJoinLobbyAsync();

                if (_presenceLobby == null)
                {
                    // No existing lobby found — create one, then re-query.
                    // Another instance may have created one at the same time
                    // (race condition with MPPM or near-simultaneous launches).
                    await CreatePresenceLobbyAsync();

                    // Re-query after a short settle to detect a race.
                    await Task.Delay(LOBBY_RACE_SETTLE_MS);
                    var rival = await TryQueryAndJoinLobbyAsync();

                    if (rival != null)
                    {
                        // Another lobby appeared — abandon ours and join theirs.
                        Debug.Log("[HostConnectionService] Race detected — merging into existing lobby.");
                        await DeleteOwnLobbyQuietly();
                        _presenceLobby = rival;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[HostConnectionService] Join failed, creating new lobby: {e.Message}");
                if (_presenceLobby == null)
                    await CreatePresenceLobbyAsync();
            }

            if (_presenceLobby != null)
            {
                connectionData.IsConnected = true;
                connectionData.IsHost = _presenceLobby.IsHost;

                // Seed party members with self
                connectionData.PartyMembers?.Clear();
                connectionData.PartyMembers?.Add(connectionData.LocalPlayerData);

                connectionData.OnHostConnectionEstablished?.Raise();
            }
        }

        /// <summary>
        /// Queries for an existing PRESENCE_LOBBY session and joins the first one found.
        /// Returns the joined session, or null if none exist.
        /// </summary>
        private async Task<ISession> TryQueryAndJoinLobbyAsync()
        {
            var queryOptions = new QuerySessionsOptions();
            queryOptions.FilterOptions.Add(
                new FilterOption(FilterField.StringIndex1, PRESENCE_LOBBY_GAME_MODE, FilterOperation.Equal));

            var results = await MultiplayerService.Instance.QuerySessionsAsync(queryOptions);

            if (results.Sessions.Count == 0)
                return null;

            // Try each session — the first one may be our own (skip it).
            foreach (var session in results.Sessions)
            {
                // Skip sessions we already own.
                if (_presenceLobby != null && session.Id == _presenceLobby.Id)
                    continue;

                try
                {
                    var joined = await MultiplayerService.Instance.JoinSessionByIdAsync(
                        session.Id,
                        new JoinSessionOptions { PlayerProperties = BuildLocalPlayerProperties() });

                    Debug.Log($"[HostConnectionService] Joined presence lobby {joined.Id}");
                    return joined;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[HostConnectionService] Join session {session.Id} failed: {e.Message}");
                }
            }

            return null;
        }

        /// <summary>
        /// Deletes the locally-created presence lobby without throwing.
        /// Used when a race condition is detected and we need to merge into another lobby.
        /// </summary>
        private async Task DeleteOwnLobbyQuietly()
        {
            if (_presenceLobby == null) return;
            try
            {
                if (_presenceLobby.IsHost)
                    await _presenceLobby.AsHost().DeleteAsync();
                else
                    await _presenceLobby.LeaveAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[HostConnectionService] DeleteOwnLobby error: {e.Message}");
            }
        }

        private async Task CreatePresenceLobbyAsync()
        {
            try
            {
                // Lobby-only session: no WithRelayNetwork() because this session
                // is used purely for player discovery and invite property exchange.
                // Relay is only needed on the party session (actual gameplay).
                var opts = new SessionOptions
                {
                    MaxPlayers = presenceLobbyMaxPlayers,
                    IsLocked = false,
                    IsPrivate = false,
                    PlayerProperties = BuildLocalPlayerProperties(),
                    SessionProperties = new Dictionary<string, SessionProperty>
                    {
                        {
                            "gameMode",
                            new SessionProperty(PRESENCE_LOBBY_GAME_MODE,
                                VisibilityPropertyOptions.Public,
                                PropertyIndex.String1)
                        }
                    }
                };

                _presenceLobby = await MultiplayerService.Instance.CreateSessionAsync(opts);
                connectionData.IsHost = true;
                Debug.Log($"[HostConnectionService] Created presence lobby {_presenceLobby.Id}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[HostConnectionService] Could not create presence lobby: {e.Message}");
            }
        }

        private async Task LeavePresenceLobbyAsync()
        {
            if (_presenceLobby == null || _leaving) return;
            _leaving = true;
            try
            {
                if (_presenceLobby.IsHost)
                    await _presenceLobby.AsHost().DeleteAsync();
                else
                    await _presenceLobby.LeaveAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[HostConnectionService] Leave error: {e.Message}");
            }
            finally
            {
                _presenceLobby = null;
                _leaving = false;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Refresh
        // ─────────────────────────────────────────────────────────────────────

        private async UniTaskVoid RefreshAsync()
        {
            if (_presenceLobby == null) return;

            try
            {
                await _presenceLobby.RefreshAsync();

                // ── Online player list (diff-based) ─────────────────────────
                // Build the fresh set, then add/remove only what changed.
                // This avoids the Clear() + re-Add() pattern that causes
                // the OnlinePlayersPanel to flicker and rebuild every cycle.
                if (connectionData.OnlinePlayers != null)
                    RefreshOnlinePlayersDiff();

                // ── Invite check (scan player properties) ──────────────────
                // Each player stores invite_target (who they're inviting) and
                // invite_data (party session info) in their own player properties.
                // Any player whose invite_target matches our ID is inviting us.
                foreach (var p in _presenceLobby.Players)
                {
                    if (p.Id == connectionData.LocalPlayerId) continue;

                    bool hasTarget = p.Properties.TryGetValue(INVITE_TARGET_KEY, out var targetProp);
                    bool hasData = p.Properties.TryGetValue(INVITE_DATA_KEY, out var dataProp);

                    if (hasTarget &&
                        targetProp.Value == connectionData.LocalPlayerId &&
                        hasData)
                    {
                        var invite = ParseInvite(dataProp.Value);
                        if (invite.HasValue)
                        {
                            bool isDuplicate = _lastFiredInvite.HasValue &&
                                _lastFiredInvite.Value.PartySessionId == invite.Value.PartySessionId;

                            // Only log on first detection — skip dedup repeats to avoid spam
                            if (!isDuplicate)
                            {
                                DebugExtensions.LogColored(
                                    $"[INVITE-RECV] New invite from '{invite.Value.HostDisplayName}' " +
                                    $"(sessionId: {invite.Value.PartySessionId})",
                                    Color.green);
                                _lastFiredInvite = invite;
                                connectionData.OnInviteReceived?.Raise(invite.Value);
                            }
                        }
                        else
                        {
                            DebugExtensions.LogErrorColored(
                                $"[INVITE-RECV] ParseInvite FAILED for data: '{dataProp.Value}'",
                                Color.red);
                        }
                    }
                }

                // ── Party session member tracking ───────────────────────────
                if (_partySession != null)
                    await RefreshPartyMembersAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[HostConnectionService] Refresh error: {e.Message}");
            }
        }

        /// <summary>
        /// Diff-based update of the OnlinePlayers list.
        /// Adds new players, removes stale ones, without clearing the whole list.
        /// This prevents SOAP list events from firing OnCleared → OnItemAdded
        /// every cycle, which would cause UI to flicker.
        /// </summary>
        private void RefreshOnlinePlayersDiff()
        {
            // Build the fresh set from the lobby.
            var freshPlayerIds = new HashSet<string>();

            foreach (var p in _presenceLobby.Players)
            {
                if (p.Id == connectionData.LocalPlayerId) continue;
                freshPlayerIds.Add(p.Id);

                string displayName = "Unknown Pilot";
                int avatarId = 0;

                if (p.Properties.TryGetValue(DISPLAY_NAME_KEY, out var dn))
                    displayName = dn.Value;
                if (p.Properties.TryGetValue(AVATAR_ID_KEY, out var av) &&
                    int.TryParse(av.Value, out int parsed))
                    avatarId = parsed;

                var playerData = new PartyPlayerData(p.Id, displayName, avatarId);

                // Add if not already present (PartyPlayerData equality is by PlayerId).
                if (!connectionData.OnlinePlayers.Contains(playerData))
                    connectionData.OnlinePlayers.Add(playerData);
            }

            // Remove players no longer in the lobby.
            for (int i = connectionData.OnlinePlayers.Count - 1; i >= 0; i--)
            {
                if (!freshPlayerIds.Contains(connectionData.OnlinePlayers[i].PlayerId))
                    connectionData.OnlinePlayers.RemoveAt(i);
            }
        }

        private async Task RefreshPartyMembersAsync()
        {
            if (_partySession == null) return;
            if (connectionData.PartyMembers == null) return;

            // Refresh the party session so Players list is up-to-date.
            try { await _partySession.RefreshAsync(); }
            catch (Exception e)
            {
                Debug.LogWarning($"[HostConnectionService] Party session refresh error: {e.Message}");
                return;
            }

            // Build a set of player IDs currently in the session
            var sessionPlayerIds = new HashSet<string>();
            foreach (var p in _partySession.Players)
                sessionPlayerIds.Add(p.Id);

            // Detect and add new members
            bool inviteTargetJoined = false;
            foreach (var p in _partySession.Players)
            {
                if (p.Id == connectionData.LocalPlayerId) continue;

                string displayName = "Unknown Pilot";
                int avatarId = 0;

                if (p.Properties.TryGetValue(DISPLAY_NAME_KEY, out var dn))
                    displayName = dn.Value;
                if (p.Properties.TryGetValue(AVATAR_ID_KEY, out var av) &&
                    int.TryParse(av.Value, out int parsed))
                    avatarId = parsed;

                var memberData = new PartyPlayerData(p.Id, displayName, avatarId);

                if (!connectionData.PartyMembers.Contains(memberData))
                {
                    connectionData.PartyMembers.Add(memberData);
                    connectionData.OnPartyMemberJoined?.Raise(memberData);

                    if (p.Id == _currentInviteTargetId)
                        inviteTargetJoined = true;
                }
            }

            // Invited player joined — clear sender's invite properties so receivers
            // stop seeing the stale invite on every refresh cycle.
            if (inviteTargetJoined)
            {
                DebugExtensions.LogColored(
                    $"[INVITE-SEND] Invited player '{_currentInviteTargetId}' joined party — clearing invite properties",
                    Color.green);
                _currentInviteTargetId = null;
                await ClearSentInvitePropertiesAsync();
            }

            // Detect and remove members who left the session
            for (int i = connectionData.PartyMembers.Count - 1; i >= 0; i--)
            {
                var member = connectionData.PartyMembers[i];
                if (member.PlayerId == connectionData.LocalPlayerId) continue;

                if (!sessionPlayerIds.Contains(member.PlayerId))
                {
                    connectionData.PartyMembers.RemoveAt(i);
                    connectionData.OnPartyMemberLeft?.Raise(member);
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Party Session
        // ─────────────────────────────────────────────────────────────────────

        private async Task CreatePartySessionAsync()
        {
            if (_partySession != null) return;

            var opts = new SessionOptions
            {
                MaxPlayers = connectionData.MaxPartySlots,
                IsLocked = false,
                IsPrivate = true,
                PlayerProperties = BuildLocalPlayerProperties()
            }.WithRelayNetwork();

            _partySession = await MultiplayerService.Instance.CreateSessionAsync(opts);
            connectionData.IsHost = true;
            Debug.Log($"[HostConnectionService] Created party session {_partySession.Id}");
        }

        /// <summary>
        /// Clears the SENDER's own invite properties after the invited player
        /// joins the party. This stops receivers from seeing stale invite data.
        /// </summary>
        private async Task ClearSentInvitePropertiesAsync()
        {
            if (_presenceLobby == null) return;

            try
            {
                _presenceLobby.CurrentPlayer.SetProperty(INVITE_TARGET_KEY,
                    new PlayerProperty(string.Empty, VisibilityPropertyOptions.Public));
                _presenceLobby.CurrentPlayer.SetProperty(INVITE_DATA_KEY,
                    new PlayerProperty(string.Empty, VisibilityPropertyOptions.Public));
                await _presenceLobby.SaveCurrentPlayerDataAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[HostConnectionService] ClearSentInvite error: {e.Message}");
            }
        }

        /// <summary>
        /// Clears this player's own invite properties and resets dedup state.
        /// Used by the RECIPIENT when declining an invite.
        /// </summary>
        private async Task RequestClearInviteAsync()
        {
            if (_presenceLobby == null) return;

            try
            {
                // Any player can clear their own player properties.
                _presenceLobby.CurrentPlayer.SetProperty(INVITE_TARGET_KEY,
                    new PlayerProperty(string.Empty, VisibilityPropertyOptions.Public));
                _presenceLobby.CurrentPlayer.SetProperty(INVITE_DATA_KEY,
                    new PlayerProperty(string.Empty, VisibilityPropertyOptions.Public));
                await _presenceLobby.SaveCurrentPlayerDataAsync();

                _lastFiredInvite = null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[HostConnectionService] ClearInvite error: {e.Message}");
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────

        private void SyncLocalIdentity()
        {
            connectionData.LocalPlayerId = AuthData.PlayerId;

            if (playerDataService?.CurrentProfile != null)
            {
                connectionData.LocalDisplayName = playerDataService.CurrentProfile.displayName;
                connectionData.LocalAvatarId = playerDataService.CurrentProfile.avatarId;
            }
        }

        private Dictionary<string, PlayerProperty> BuildLocalPlayerProperties()
        {
            return new Dictionary<string, PlayerProperty>
            {
                { DISPLAY_NAME_KEY, new PlayerProperty(connectionData.LocalDisplayName ?? "Pilot", VisibilityPropertyOptions.Public) },
                { AVATAR_ID_KEY,    new PlayerProperty(connectionData.LocalAvatarId.ToString(),    VisibilityPropertyOptions.Public) }
            };
        }

        private static PartyInviteData? ParseInvite(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return null;
            var parts = raw.Split('|');
            if (parts.Length < 4) return null;
            if (!int.TryParse(parts[3], out int avatarId)) return null;

            return new PartyInviteData(parts[0], parts[1], parts[2], avatarId);
        }

        private bool IsAuthSignedInAndHasId()
        {
            if (AuthData == null) return false;

            bool signedIn =
                AuthData.IsSignedIn ||
                AuthData.State == AuthenticationData.AuthState.SignedIn;

            return signedIn && !string.IsNullOrEmpty(AuthData.PlayerId);
        }
    }
}
