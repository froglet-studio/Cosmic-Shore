using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CosmicShore.App.Profile;
using Reflex.Attributes;
using Unity.Services.Multiplayer;
using UnityEngine;
using Cysharp.Threading.Tasks;

namespace CosmicShore.Game.Party
{
    /// <summary>
    /// Manages party/lobby lifecycle and invites.
    /// Registered as a value in the Reflex root container via AppManager.
    /// </summary>
    public class PartyManager : MonoBehaviour
    {
        [Serializable]
        public struct OnlinePlayerInfo
        {
            public string PlayerId;
            public string DisplayName;
            public int AvatarId;
        }

        [Serializable]
        public struct PartyInvite
        {
            public string HostPlayerId;
            public string PartySessionId;
            public string HostDisplayName;
            public int HostAvatarId;
        }

        // -----------------------------------------------------------------------------------------
        // DI

        [Inject] PlayerDataService playerDataService;

        // -----------------------------------------------------------------------------------------
        // Events

        /// <summary>Fired whenever the online player list refreshes.</summary>
        public event Action<IReadOnlyList<OnlinePlayerInfo>> OnOnlinePlayersUpdated;

        /// <summary>Fired when this local player receives an invite.</summary>
        public event Action<PartyInvite> OnInviteReceived;

        /// <summary>Fired when local player successfully joins a party (as client).</summary>
        public event Action<string> OnJoinedParty;

        /// <summary>Fired when a remote player accepts the local player's invite (host view).</summary>
        public event Action<string> OnPartyMemberJoined;

        // -----------------------------------------------------------------------------------------
        // Inspector

        [Header("Auth (Source of Truth)")]
        [SerializeField] AuthenticationDataVariable authenticationDataVariable;
        AuthenticationData authenticationData => authenticationDataVariable.Value;

        [Header("Presence Lobby")]
        [Tooltip("Max simultaneous players in the global presence lobby.")]
        [SerializeField] private int presenceLobbyMaxPlayers = 100;

        [Tooltip("How often (seconds) to refresh the online player list and check for invites.")]
        [SerializeField] private float refreshIntervalSeconds = 3f;

        // -----------------------------------------------------------------------------------------
        // Public state

        public bool IsInPresenceLobby => _presenceLobby != null;
        public bool IsHost { get; private set; }
        public ISession PartySession { get; private set; }
        public ISession PresenceLobby => _presenceLobby;

        public string LocalPlayerId { get; private set; }
        public string LocalDisplayName { get; private set; }
        public int LocalAvatarId { get; private set; }

        // -----------------------------------------------------------------------------------------
        // Private state

        private ISession _presenceLobby;
        private float _refreshTimer;
        private bool _initialized;
        private bool _leaving;
        private bool _joining;
        private PartyInvite? _lastFiredInvite; // prevents re-firing the same invite every tick

        // -----------------------------------------------------------------------------------------
        // Constants

        private const string PRESENCE_LOBBY_GAME_MODE = "PRESENCE_LOBBY";
        private const string DISPLAY_NAME_KEY = "displayName";
        private const string AVATAR_ID_KEY = "avatarId";
        private const string INVITE_PREFIX = "invite_"; // session property key prefix: invite_{targetPlayerId}

        // -----------------------------------------------------------------------------------------
        // Unity Lifecycle

        void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        async void Start()
        {
            // If you wire HandleSignedInEvent() via Soap listener, this Start loop is still safe.
            // It just ensures PartyManager works even if it starts before auth completes.
            while (!IsAuthSignedInAndHasId())
                await Task.Delay(300);

            LocalPlayerId = authenticationData.PlayerId;

            // Profile might load a bit after auth; we try now and keep using SyncProfileFromPlayerDataService() later.
            SyncProfileFromPlayerDataService();

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
        }

        // -----------------------------------------------------------------------------------------
        // Optional: Hook these to AuthenticationData.OnSignedIn / OnSignedOut via Soap listeners

        public async void HandleSignedInEvent()
        {
            // If Start already initialized, don't double-join.
            if (_initialized || _joining) return;

            if (!IsAuthSignedInAndHasId())
                return;

            LocalPlayerId = authenticationData.PlayerId;
            SyncProfileFromPlayerDataService();

            _joining = true;
            try
            {
                await JoinPresenceLobbyAsync();
                _initialized = true;
            }
            finally
            {
                _joining = false;
            }
        }

        public async void HandleSignedOutEvent()
        {
            _initialized = false;
            LocalPlayerId = string.Empty;
            LocalDisplayName = string.Empty;
            LocalAvatarId = 0;

            await LeavePresenceLobbyAsync();
        }

        // -----------------------------------------------------------------------------------------
        // Profile sync

        /// <summary>Pull display name and avatar from PlayerDataService into local cache.</summary>
        public void SyncProfileFromPlayerDataService()
        {
            if (playerDataService?.CurrentProfile == null) return;
            LocalDisplayName = playerDataService.CurrentProfile.displayName;
            LocalAvatarId = playerDataService.CurrentProfile.avatarId;
        }

        // -----------------------------------------------------------------------------------------
        // Presence Lobby

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

                    IsHost = false;
                    Debug.Log($"[PartyManager] Joined presence lobby {_presenceLobby.Id}");
                }
                else
                {
                    await CreatePresenceLobbyAsync();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PartyManager] Join failed, creating new lobby: {e.Message}");
                await CreatePresenceLobbyAsync();
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
                IsHost = true;
                Debug.Log($"[PartyManager] Created presence lobby {_presenceLobby.Id}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[PartyManager] Could not create presence lobby: {e.Message}");
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
                Debug.LogWarning($"[PartyManager] Leave error: {e.Message}");
            }
            finally
            {
                _presenceLobby = null;
                _leaving = false;
            }
        }

        // -----------------------------------------------------------------------------------------
        // Refresh — player list + incoming invite check

        private async UniTaskVoid RefreshAsync()
        {
            if (_presenceLobby == null) return;

            try
            {
                await _presenceLobby.RefreshAsync();

                // ── Online player list ──────────────────────────────────────────────
                var onlinePlayers = new List<OnlinePlayerInfo>();

                foreach (var p in _presenceLobby.Players)
                {
                    if (p.Id == LocalPlayerId) continue;

                    string displayName = "Unknown Pilot";
                    int avatarId = 0;

                    if (p.Properties.TryGetValue(DISPLAY_NAME_KEY, out var dn))
                        displayName = dn.Value;

                    if (p.Properties.TryGetValue(AVATAR_ID_KEY, out var av) &&
                        int.TryParse(av.Value, out int parsed))
                        avatarId = parsed;

                    onlinePlayers.Add(new OnlinePlayerInfo
                    {
                        PlayerId = p.Id,
                        DisplayName = displayName,
                        AvatarId = avatarId
                    });
                }

                OnOnlinePlayersUpdated?.Invoke(onlinePlayers);

                // ── Invite check — read session property keyed to this player's ID ──
                string inviteKey = $"{INVITE_PREFIX}{LocalPlayerId}";
                if (_presenceLobby.Properties.TryGetValue(inviteKey, out var inviteProp))
                {
                    var invite = ParseInvite(inviteProp.Value);
                    if (invite.HasValue)
                    {
                        // Don't re-fire the same invite every tick
                        if (!_lastFiredInvite.HasValue ||
                            _lastFiredInvite.Value.PartySessionId != invite.Value.PartySessionId)
                        {
                            _lastFiredInvite = invite;
                            OnInviteReceived?.Invoke(invite.Value);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PartyManager] Refresh error: {e.Message}");
            }
        }

        // -----------------------------------------------------------------------------------------
        // Invite — Send (host of presence lobby only)

        public async Task SendInviteAsync(string targetPlayerId)
        {
            if (_presenceLobby == null)
            {
                Debug.LogWarning("[PartyManager] Not in presence lobby.");
                return;
            }

            if (!_presenceLobby.IsHost)
            {
                Debug.LogWarning("[PartyManager] Only the presence lobby host can send invites.");
                return;
            }

            try
            {
                // Ensure latest profile values before sending
                SyncProfileFromPlayerDataService();

                if (PartySession == null)
                    await CreatePartySessionAsync();

                string inviteKey = $"{INVITE_PREFIX}{targetPlayerId}";
                string inviteValue = $"{LocalPlayerId}|{PartySession.Id}|{LocalDisplayName}|{LocalAvatarId}";

                var hostSession = _presenceLobby.AsHost();
                hostSession.SetProperty(inviteKey, new SessionProperty(inviteValue, VisibilityPropertyOptions.Public));
                await hostSession.SavePropertiesAsync();

                Debug.Log($"[PartyManager] Invite sent to {targetPlayerId}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PartyManager] SendInvite error: {e.Message}");
            }
        }

        // -----------------------------------------------------------------------------------------
        // Invite — Accept / Decline

        public async Task AcceptInviteAsync(PartyInvite invite)
        {
            try
            {
                // Ensure latest profile values before joining
                SyncProfileFromPlayerDataService();

                PartySession = await MultiplayerService.Instance.JoinSessionByIdAsync(
                    invite.PartySessionId,
                    new JoinSessionOptions { PlayerProperties = BuildLocalPlayerProperties() });

                IsHost = false;
                Debug.Log($"[PartyManager] Joined party {PartySession.Id}");
                OnJoinedParty?.Invoke(invite.HostDisplayName);

                await RequestClearInviteAsync(invite.HostPlayerId);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PartyManager] AcceptInvite error: {e.Message}");
            }
        }

        public async Task DeclineInviteAsync()
        {
            _lastFiredInvite = null;
            await RequestClearInviteAsync(LocalPlayerId);
        }

        private async Task RequestClearInviteAsync(string hostPlayerId)
        {
            if (_presenceLobby == null) return;

            try
            {
                _lastFiredInvite = null;

                if (_presenceLobby.IsHost)
                {
                    string inviteKey = $"{INVITE_PREFIX}{LocalPlayerId}";
                    var hostSession = _presenceLobby.AsHost();
                    hostSession.SetProperty(inviteKey, new SessionProperty(string.Empty, VisibilityPropertyOptions.Public));
                    await hostSession.SavePropertiesAsync();
                }
                else
                {
                    Debug.Log("[PartyManager] Invite cleared locally (host will expire it naturally).");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PartyManager] ClearInvite error: {e.Message}");
            }
        }

        // -----------------------------------------------------------------------------------------
        // Party Session (host side)

        private async Task CreatePartySessionAsync()
        {
            var opts = new SessionOptions
            {
                MaxPlayers = 8,
                IsLocked = false,
                IsPrivate = true,
                PlayerProperties = BuildLocalPlayerProperties()
            }.WithRelayNetwork();

            PartySession = await MultiplayerService.Instance.CreateSessionAsync(opts);
            IsHost = true;
            Debug.Log($"[PartyManager] Created party session {PartySession.Id}");
        }

        public void HandOffToMultiplayerSetup(CosmicShore.Soap.GameDataSO gameData)
        {
            if (PartySession != null)
                gameData.ActiveSession = PartySession;
        }

        // -----------------------------------------------------------------------------------------
        // Helpers

        private Dictionary<string, PlayerProperty> BuildLocalPlayerProperties()
        {
            return new Dictionary<string, PlayerProperty>
            {
                { DISPLAY_NAME_KEY, new PlayerProperty(LocalDisplayName ?? "Pilot", VisibilityPropertyOptions.Public) },
                { AVATAR_ID_KEY,    new PlayerProperty(LocalAvatarId.ToString(),    VisibilityPropertyOptions.Public) }
            };
        }

        private static PartyInvite? ParseInvite(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return null;
            var parts = raw.Split('|');
            if (parts.Length < 4) return null;
            if (!int.TryParse(parts[3], out int avatarId)) return null;

            return new PartyInvite
            {
                HostPlayerId = parts[0],
                PartySessionId = parts[1],
                HostDisplayName = parts[2],
                HostAvatarId = avatarId
            };
        }

        private bool IsAuthSignedInAndHasId()
        {
            if (authenticationData == null)
                return false;

            bool signedIn =
                authenticationData.IsSignedIn ||
                authenticationData.State == AuthenticationData.AuthState.SignedIn;

            return signedIn && !string.IsNullOrEmpty(authenticationData.PlayerId);
        }
    }
}