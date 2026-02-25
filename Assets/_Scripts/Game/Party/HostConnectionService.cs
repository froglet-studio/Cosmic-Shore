using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CosmicShore.App.Profile;
using CosmicShore.Soap;
using CosmicShore.Utilities;
using Cysharp.Threading.Tasks;
using Obvious.Soap;
using Reflex.Attributes;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace CosmicShore.Game.Party
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

        private const string PRESENCE_LOBBY_GAME_MODE = "PRESENCE_LOBBY";
        private const string DISPLAY_NAME_KEY = "displayName";
        private const string AVATAR_ID_KEY = "avatarId";
        private const string INVITE_PREFIX = "invite_";

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

            SyncLocalIdentity();
            await JoinPresenceLobbyAsync();
            _initialized = true;
        }

        void Update()
        {
            if (!_initialized || _presenceLobby == null) return;

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
                _initialized = true;
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
            if (_presenceLobby == null || !_presenceLobby.IsHost)
            {
                Debug.LogWarning("[HostConnectionService] Cannot send invite — not lobby host.");
                return;
            }

            try
            {
                SyncLocalIdentity();

                if (_partySession == null)
                    await CreatePartySessionAsync();

                string inviteKey = $"{INVITE_PREFIX}{targetPlayerId}";
                string inviteValue = $"{connectionData.LocalPlayerId}|{_partySession.Id}|{connectionData.LocalDisplayName}|{connectionData.LocalAvatarId}";

                var hostSession = _presenceLobby.AsHost();
                hostSession.SetProperty(inviteKey, new SessionProperty(inviteValue, VisibilityPropertyOptions.Public));
                await hostSession.SavePropertiesAsync();

                // Find the target in online players and raise OnInviteSent
                foreach (var player in connectionData.OnlinePlayers)
                {
                    if (player.PlayerId == targetPlayerId)
                    {
                        connectionData.OnInviteSent?.Raise(player);
                        break;
                    }
                }

                Debug.Log($"[HostConnectionService] Invite sent to {targetPlayerId}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[HostConnectionService] SendInvite error: {e.Message}");
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

                Debug.Log($"[HostConnectionService] Joined party {_partySession.Id}");
                await RequestClearInviteAsync();
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
        /// Hands the active party session off to GameDataSO for game launch.
        /// </summary>
        public void HandOffToMultiplayerSetup(GameDataSO gameData)
        {
            if (_partySession != null)
                gameData.ActiveSession = _partySession;
        }

        public ISession PartySession => _partySession;

        // ─────────────────────────────────────────────────────────────────────
        // Presence Lobby
        // ─────────────────────────────────────────────────────────────────────

        private async Task JoinPresenceLobbyAsync()
        {
            if (_presenceLobby != null) return;

            try
            {
                var queryOptions = new QuerySessionsOptions();
                queryOptions.FilterOptions.Add(
                    new FilterOption(FilterField.StringIndex1, PRESENCE_LOBBY_GAME_MODE, FilterOperation.Equal));

                var results = await MultiplayerService.Instance.QuerySessionsAsync(queryOptions);

                if (results.Sessions.Count > 0)
                {
                    _presenceLobby = await MultiplayerService.Instance.JoinSessionByIdAsync(
                        results.Sessions[0].Id,
                        new JoinSessionOptions { PlayerProperties = BuildLocalPlayerProperties() });

                    connectionData.IsHost = false;
                    Debug.Log($"[HostConnectionService] Joined presence lobby {_presenceLobby.Id}");
                }
                else
                {
                    await CreatePresenceLobbyAsync();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[HostConnectionService] Join failed, creating new lobby: {e.Message}");
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

        private async Task CreatePresenceLobbyAsync()
        {
            try
            {
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
                }.WithRelayNetwork();

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

                // ── Online player list ──────────────────────────────────────
                connectionData.OnlinePlayers?.Clear();

                foreach (var p in _presenceLobby.Players)
                {
                    if (p.Id == connectionData.LocalPlayerId) continue;

                    string displayName = "Unknown Pilot";
                    int avatarId = 0;

                    if (p.Properties.TryGetValue(DISPLAY_NAME_KEY, out var dn))
                        displayName = dn.Value;
                    if (p.Properties.TryGetValue(AVATAR_ID_KEY, out var av) &&
                        int.TryParse(av.Value, out int parsed))
                        avatarId = parsed;

                    connectionData.OnlinePlayers?.Add(
                        new PartyPlayerData(p.Id, displayName, avatarId));
                }

                // ── Invite check ────────────────────────────────────────────
                string inviteKey = $"{INVITE_PREFIX}{connectionData.LocalPlayerId}";
                if (_presenceLobby.Properties.TryGetValue(inviteKey, out var inviteProp))
                {
                    var invite = ParseInvite(inviteProp.Value);
                    if (invite.HasValue)
                    {
                        if (!_lastFiredInvite.HasValue ||
                            _lastFiredInvite.Value.PartySessionId != invite.Value.PartySessionId)
                        {
                            _lastFiredInvite = invite;
                            connectionData.OnInviteReceived?.Raise(invite.Value);
                        }
                    }
                }

                // ── Party session member tracking ───────────────────────────
                if (_partySession != null)
                    RefreshPartyMembers();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[HostConnectionService] Refresh error: {e.Message}");
            }
        }

        private void RefreshPartyMembers()
        {
            if (_partySession == null) return;

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

                if (connectionData.PartyMembers != null && !connectionData.PartyMembers.Contains(memberData))
                {
                    connectionData.PartyMembers.Add(memberData);
                    connectionData.OnPartyMemberJoined?.Raise(memberData);
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Party Session
        // ─────────────────────────────────────────────────────────────────────

        private async Task CreatePartySessionAsync()
        {
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

        private async Task RequestClearInviteAsync()
        {
            if (_presenceLobby == null) return;

            try
            {
                _lastFiredInvite = null;

                if (_presenceLobby.IsHost)
                {
                    string inviteKey = $"{INVITE_PREFIX}{connectionData.LocalPlayerId}";
                    var hostSession = _presenceLobby.AsHost();
                    hostSession.SetProperty(inviteKey, new SessionProperty(string.Empty, VisibilityPropertyOptions.Public));
                    await hostSession.SavePropertiesAsync();
                }
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
