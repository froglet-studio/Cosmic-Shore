using System;
using System.Collections.Generic;
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
            // Do NOT null _lastFiredInvite here — keeping it set prevents the
            // refresh loop from re-firing the same invite.  Non-host players
            // cannot clear the lobby property, so the invite key remains until
            // the host overwrites it.  The dedup check in RefreshAsync (comparing
            // PartySessionId) will suppress repeated notifications for this invite.
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

        /// <summary>
        /// Hands the active party session off to GameDataSO for game launch.
        /// </summary>
        public void HandOffToMultiplayerSetup(GameDataSO gameData)
        {
            if (_partySession != null)
                gameData.ActiveSession = _partySession;
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

            // Skip if NetworkManager is already running as host (e.g. menu
            // autopilot started by MultiplayerSetup). The presence lobby uses
            // WithRelayNetwork() which reconfigures the transport and attempts
            // to restart the host, corrupting the existing local session.
            if (Unity.Netcode.NetworkManager.Singleton != null &&
                Unity.Netcode.NetworkManager.Singleton.IsListening)
            {
                Debug.Log("[HostConnectionService] Deferring presence lobby — NetworkManager already hosting.");
                return;
            }

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
            if (connectionData.PartyMembers == null) return;

            // Build a set of player IDs currently in the session
            var sessionPlayerIds = new HashSet<string>();
            foreach (var p in _partySession.Players)
                sessionPlayerIds.Add(p.Id);

            // Detect and add new members
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
                }
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
                if (_presenceLobby.IsHost)
                {
                    string inviteKey = $"{INVITE_PREFIX}{connectionData.LocalPlayerId}";
                    var hostSession = _presenceLobby.AsHost();
                    hostSession.SetProperty(inviteKey, new SessionProperty(string.Empty, VisibilityPropertyOptions.Public));
                    await hostSession.SavePropertiesAsync();

                    // Only clear the dedup guard after the property is actually
                    // removed from the lobby so the refresh loop won't re-fire.
                    _lastFiredInvite = null;
                }
                // Non-host players cannot clear lobby properties.  The dedup
                // guard (_lastFiredInvite) stays set so RefreshAsync suppresses
                // repeated notifications for the same invite.
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
