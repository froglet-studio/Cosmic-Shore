using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CosmicShore.UI;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Obvious.Soap;
using Reflex.Attributes;
using Unity.Netcode;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;
using CosmicShore.Core;
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
        [Inject] private SceneTransitionManager _sceneTransitionManager;
        [Inject] private GameDataSO _gameData;

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
        /// <summary>
        /// Set when the user has answered <see cref="_lastFiredInvite"/> (accept or decline).
        /// Kept alongside _lastFiredInvite so the SDK-side dedup guard (which compares
        /// PartySessionId) still suppresses repeated SOAP raises during the window where
        /// the sender's lobby properties are still stale, while UI queries via
        /// <see cref="LastPendingInvite"/> correctly report "no pending invite".
        /// </summary>
        private bool _lastInviteResolved;
        private string _currentInviteTargetId;
        private ILogHandler _originalLogHandler;
        private Task _creatingPartySessionTask;
        /// <summary>
        /// Mutex flag preventing concurrent lobby operations.
        /// RefreshAsync skips if busy; SendInviteAsync waits then claims.
        /// Prevents the SDK's internal player index from going stale when
        /// a refresh and a save race each other.
        /// </summary>
        private bool _lobbyBusy;

        /// <summary>
        /// Set after the first Relay party session creation triggers a Menu_Main
        /// network-scene reload. Prevents subsequent recreations (e.g. after
        /// returning from a game) from reloading the scene in an infinite loop.
        /// Reset when ClearStalePartySession is called so the next fresh
        /// session creation can reload once if needed.
        /// </summary>
        private bool _hasReloadedMenuForRelay;

        private const string PRESENCE_LOBBY_GAME_MODE = "PRESENCE_LOBBY";
        private const string DISPLAY_NAME_KEY = "displayName";
        private const string AVATAR_ID_KEY = "avatarId";
        private const string INVITE_TARGET_KEY = "invite_target";
        private const string INVITE_DATA_KEY = "invite_data";
        private const string PARTY_COUNT_KEY = "partyCount";
        private const string PARTY_MAX_KEY = "partyMax";
        private const string MATCH_NAME_KEY = "matchName";

        /// <summary>
        /// Milliseconds to wait after creating a lobby before re-querying,
        /// giving a near-simultaneous second instance time to also create.
        /// If a rival lobby is detected, we merge into it.
        /// </summary>
        private const int LOBBY_RACE_SETTLE_MS = 1500;

        /// <summary>
        /// After this many consecutive RefreshAsync failures, abandon the
        /// stale lobby reference and attempt to rejoin.
        /// </summary>
        private const int MAX_REFRESH_ERRORS_BEFORE_RECONNECT = 3;
        private int _consecutiveRefreshErrors;

        private const int RATE_LIMIT_MAX_RETRIES = 3;
        private const int RATE_LIMIT_BASE_DELAY_MS = 2000;
        private float _rateLimitBackoffUntil;

        /// <summary>Last published party-size value so we only push on change.</summary>
        private int _publishedPartyCount = -1;

        private static bool IsRateLimitException(Exception e)
        {
            return e.Message != null && e.Message.Contains("Too Many Requests");
        }

        // ─────────────────────────────────────────────────────────────────────
        // Unity Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InstallLobbyLogFilter();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        async void Start()
        {
            while (!IsAuthSignedInAndHasId())
                await Task.Delay(300);

            // Subscribe to profile changes so a name/avatar update mid-session
            // is republished to the presence lobby. Without this, players who
            // join before the cloud profile loads would advertise the default
            // "Pilot" name forever.
            SubscribeToProfileChanges();

            // HandleSignedInEvent may have already completed via SOAP event,
            // or may currently be running (_joining). Both paths call
            // CreatePartySessionAsync — concurrent calls cause the second
            // to find a running host, shut it down, and reload Menu_Main.
            if (_initialized || _joining) return;

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
            if (!_initialized || _presenceLobby == null || _lobbyBusy) return;
            if (Time.unscaledTime < _rateLimitBackoffUntil) return;

            // Only run party session recreation and lobby refresh while on
            // Menu_Main. During game scenes the party session is irrelevant
            // and recreation would shut down / restart the NetworkManager,
            // destroying in-flight network objects and flashing stale UI.
            if (!IsOnMenuScene()) return;

            // If the party session was cleared (e.g. after returning from a game),
            // recreate it so Menu_Main gets a live Relay host for vessel spawning.
            if (_partySession == null && _creatingPartySessionTask == null
                && Time.unscaledTime >= _nextRecreationAttemptTime)
            {
                RecreatePartySessionAsync().Forget();
            }

            _refreshTimer += Time.unscaledDeltaTime;
            if (_refreshTimer >= refreshIntervalSeconds)
            {
                _refreshTimer = 0f;
                RefreshAsync().Forget();
            }
        }

        /// <summary>
        /// Returns true when the active scene is Menu_Main (or Authentication,
        /// where initial setup runs). False during game scenes so the refresh
        /// loop and party-session recreation are suspended.
        /// </summary>
        private static bool IsOnMenuScene()
        {
            var sceneName = SceneManager.GetActiveScene().name;
            return sceneName == "Menu_Main" || sceneName == "Authentication";
        }

        /// <summary>Time before next recreation attempt after a failure.</summary>
        private float _nextRecreationAttemptTime;

        private async UniTaskVoid RecreatePartySessionAsync()
        {
            try
            {
                Debug.Log("[HostConnectionService] Party session is null — recreating...");
                await CreatePartySessionAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[HostConnectionService] Party session recreation failed: {e.Message}");
                // Back off to avoid tight retry loops if Relay is unavailable.
                _nextRecreationAttemptTime = Time.unscaledTime + refreshIntervalSeconds * 2;
            }
        }

        async void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            UnsubscribeFromProfileChanges();
            UninstallLobbyLogFilter();
            await LeavePresenceLobbyAsync();

            if (Instance == this)
                Instance = null;
        }

        /// <summary>
        /// Resets invite dedup state when Menu_Main loads so stale
        /// <see cref="_lastFiredInvite"/> from a prior session doesn't
        /// block new invite detection.
        /// </summary>
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "Menu_Main")
            {
                _lastFiredInvite = null;
                _lastInviteResolved = false;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Public: Auth hooks (wire via SOAP EventListenerNoParam in inspector)
        // ─────────────────────────────────────────────────────────────────────

        public async void HandleSignedInEvent()
        {
            if (_initialized || _joining) return;
            if (!IsAuthSignedInAndHasId()) return;

            SubscribeToProfileChanges();
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

            // Wait for any in-flight RefreshAsync to finish so the SDK's
            // internal player index is stable before we call SaveCurrentPlayerDataAsync.
            while (_lobbyBusy)
                await Task.Yield();
            _lobbyBusy = true;
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

                // Best-effort refresh to sync SDK player list cache before setting
                // properties. Without this, SaveCurrentPlayerDataAsync can fail
                // silently if the SDK's internal player index is stale.
                try { await _presenceLobby.RefreshAsync(); }
                catch { /* non-fatal — SaveWithRetryAsync handles stale state */ }

                _presenceLobby.CurrentPlayer.SetProperty(INVITE_TARGET_KEY,
                    new PlayerProperty(targetPlayerId, VisibilityPropertyOptions.Public));
                _presenceLobby.CurrentPlayer.SetProperty(INVITE_DATA_KEY,
                    new PlayerProperty(inviteData, VisibilityPropertyOptions.Public));

                await SaveWithRetryAsync();

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
                // Push the next polling refresh a full interval out so we don't
                // immediately hit the rate limit with a back-to-back request.
                _refreshTimer = 0f;
                _lobbyBusy = false;
            }
        }

        public async Task AcceptInviteAsync(PartyInviteData invite)
        {
            // Mark resolved up-front so a re-opened FriendsListPanel doesn't
            // re-spawn a row for the invite the user just accepted, even if
            // _lastFiredInvite lingers for SDK-side dedup.
            _lastInviteResolved = true;
            connectionData.OnInviteResolved?.Raise();
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

                // Give the new session a grace period before the refresh loop
                // hits it — the join itself already consumed API quota and an
                // immediate RefreshAsync would trigger HTTP 429 Too Many Requests.
                _refreshTimer = -(refreshIntervalSeconds);

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
            _lastInviteResolved = true;
            connectionData.OnInviteResolved?.Raise();
            await RequestClearInviteAsync();
        }

        /// <summary>
        /// Leaves the current party and returns the local player to Menu_Main.
        /// Delegates to <see cref="PartyInviteController"/> which handles the
        /// Netcode shutdown + fresh host restart. Intended for UI leave-party
        /// buttons (e.g. <c>ArcadeLobbyList</c>).
        /// </summary>
        public async Task LeavePartyAsync()
        {
            var controller = PartyInviteController.Instance;
            if (controller == null)
            {
                Debug.LogWarning("[HostConnectionService] PartyInviteController not available.");
                return;
            }

            await controller.LeavePartyAndReturnToMenuAsync();
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
        /// Most recently detected incoming party invite, or null if no invite is
        /// currently pending. Cleared when the user accepts/declines (via
        /// <see cref="AcceptInviteAsync"/> or <see cref="DeclineInviteAsync"/>)
        /// or when Menu_Main reloads.
        ///
        /// UI panels that subscribe to <see cref="HostConnectionDataSO.OnInviteReceived"/>
        /// via OnEnable should also read this on show, so invites that arrived while
        /// the panel was hidden are not missed.
        /// </summary>
        public PartyInviteData? LastPendingInvite =>
            _lastInviteResolved ? null : _lastFiredInvite;

        /// <summary>
        /// Clears the stale party session reference so the next refresh cycle
        /// (or explicit call to <see cref="CreatePartySessionPublicAsync"/>)
        /// will create a fresh Relay-backed session.
        /// Called by <see cref="Core.SceneLoader"/> when returning to Menu_Main
        /// after a game, since the game's LeaveSession() deleted the UGS game
        /// session and shut down the NetworkManager.
        /// </summary>
        public void ClearStalePartySession()
        {
            if (_partySession == null) return;
            Debug.Log("[HostConnectionService] Clearing stale party session reference.");
            _partySession = null;
            _lastFiredInvite = null;
            _hasReloadedMenuForRelay = false;
            connectionData.PartyMembers?.Clear();
        }

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
                    Debug.LogWarning($"[HostConnectionService] Rate limited querying lobby — retry {attempt + 1}/{RATE_LIMIT_MAX_RETRIES} in {delay}ms");
                    await Task.Delay(delay);
                }
            }

            if (sessions.Count == 0)
                return null;

            // Try each session — the first one may be our own (skip it).
            foreach (var session in sessions)
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
                    if (IsRateLimitException(e))
                        await Task.Delay(RATE_LIMIT_BASE_DELAY_MS);
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

                for (int attempt = 0; ; attempt++)
                {
                    try
                    {
                        _presenceLobby = await MultiplayerService.Instance.CreateSessionAsync(opts);
                        connectionData.IsHost = true;
                        Debug.Log($"[HostConnectionService] Created presence lobby {_presenceLobby.Id}");
                        return;
                    }
                    catch (Exception re) when (attempt < RATE_LIMIT_MAX_RETRIES && IsRateLimitException(re))
                    {
                        int delay = RATE_LIMIT_BASE_DELAY_MS * (1 << attempt);
                        Debug.LogWarning($"[HostConnectionService] Rate limited creating presence lobby — retry {attempt + 1}/{RATE_LIMIT_MAX_RETRIES} in {delay}ms");
                        await Task.Delay(delay);
                    }
                }
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
            if (_presenceLobby == null || _lobbyBusy) return;

            _lobbyBusy = true;
            bool shouldReconnect = false;
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
                                // Reset resolved flag: this is a fresh invite, not the one we last answered.
                                _lastInviteResolved = false;
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

                // ── Publish local party state to presence lobby if changed ──
                await PublishPartyStateIfChangedAsync();

                _consecutiveRefreshErrors = 0;
            }
            catch (Exception e)
            {
                if (IsRateLimitException(e))
                {
                    _rateLimitBackoffUntil = Time.unscaledTime + refreshIntervalSeconds * 2;
                    Debug.LogWarning("[HostConnectionService] Rate limited during refresh — backing off");
                }
                else
                {
                    Debug.LogWarning($"[HostConnectionService] Refresh error: {e.Message}");
                    _consecutiveRefreshErrors++;
                    if (_consecutiveRefreshErrors >= MAX_REFRESH_ERRORS_BEFORE_RECONNECT)
                    {
                        Debug.LogWarning($"[HostConnectionService] {_consecutiveRefreshErrors} consecutive refresh errors — reconnecting to presence lobby");
                        _consecutiveRefreshErrors = 0;
                        _presenceLobby = null;
                        shouldReconnect = true;
                    }
                }
            }
            finally
            {
                _lobbyBusy = false;
            }

            // Reconnect outside the try/finally so _lobbyBusy is released first.
            if (shouldReconnect)
                await JoinPresenceLobbyAsync();
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
                int partyCount = 0;
                int partyMax = 0;
                string matchName = string.Empty;

                if (p.Properties.TryGetValue(DISPLAY_NAME_KEY, out var dn))
                    displayName = dn.Value;
                if (p.Properties.TryGetValue(AVATAR_ID_KEY, out var av) &&
                    int.TryParse(av.Value, out int parsedAv))
                    avatarId = parsedAv;
                if (p.Properties.TryGetValue(PARTY_COUNT_KEY, out var pc) &&
                    int.TryParse(pc.Value, out int parsedPc))
                    partyCount = parsedPc;
                if (p.Properties.TryGetValue(PARTY_MAX_KEY, out var pm) &&
                    int.TryParse(pm.Value, out int parsedPm))
                    partyMax = parsedPm;
                if (p.Properties.TryGetValue(MATCH_NAME_KEY, out var mn))
                    matchName = mn.Value ?? string.Empty;

                var playerData = new PartyPlayerData(
                    p.Id, displayName, avatarId, partyCount, partyMax, matchName);

                // Upsert: equality is by PlayerId, so we must replace to pick up
                // updated party-state fields even if the player was already present.
                int existingIdx = -1;
                for (int i = 0; i < connectionData.OnlinePlayers.Count; i++)
                {
                    if (connectionData.OnlinePlayers[i].PlayerId == p.Id)
                    {
                        existingIdx = i;
                        break;
                    }
                }

                if (existingIdx < 0)
                {
                    connectionData.OnlinePlayers.Add(playerData);
                }
                else
                {
                    var existing = connectionData.OnlinePlayers[existingIdx];
                    if (existing.DisplayName != playerData.DisplayName ||
                        existing.AvatarId != playerData.AvatarId ||
                        existing.PartyMemberCount != playerData.PartyMemberCount ||
                        existing.PartyMaxSlots != playerData.PartyMaxSlots ||
                        existing.MatchName != playerData.MatchName)
                    {
                        connectionData.OnlinePlayers[existingIdx] = playerData;
                    }
                }
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

                // Rate limits are transient — back off and retry next cycle.
                // Only null the session for non-transient errors (session deleted,
                // auth failure, etc.) so the Update() loop can recreate it.
                if (IsRateLimitException(e))
                {
                    _rateLimitBackoffUntil = Time.unscaledTime + refreshIntervalSeconds * 2;
                    return;
                }

                _partySession = null;
                connectionData.PartyMembers?.Clear();
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

        private const int HOST_CONFLICT_MAX_RETRIES = 2;

        private static bool IsHostConflictException(Exception e)
        {
            return e.Message != null &&
                   e.Message.Contains("Failed to start NetworkManager component as host");
        }

        private async Task CreatePartySessionAsync()
        {
            if (_partySession != null) return;

            // If another caller is already creating the session, await that
            // operation instead of returning with _partySession still null.
            if (_creatingPartySessionTask != null)
            {
                await _creatingPartySessionTask;
                return;
            }

            _creatingPartySessionTask = CreatePartySessionCoreAsync();
            try
            {
                await _creatingPartySessionTask;
            }
            finally
            {
                _creatingPartySessionTask = null;
            }
        }

        private async Task CreatePartySessionCoreAsync()
        {
            var opts = new SessionOptions
            {
                MaxPlayers = connectionData.MaxPartySlots,
                IsLocked = false,
                IsPrivate = true,
                PlayerProperties = BuildLocalPlayerProperties()
            }.WithRelayNetwork();

            for (int attempt = 0; ; attempt++)
            {
                // The UGS Multiplayer SDK calls NetworkManager.StartHost()
                // internally when creating a Relay-backed session. If a local
                // host is already running (started by AuthenticationSceneController
                // as a fallback), that call fails. Shut it down before each attempt.
                var nm = NetworkManager.Singleton;
                if (nm != null && nm.IsListening)
                {
                    Debug.Log("[HostConnectionService] Shutting down local host before Relay party session creation...");
                    nm.Shutdown();

                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    while (nm != null && nm.IsListening && sw.ElapsedMilliseconds < 5000)
                        await Task.Delay(100);

                    // Allow transport cleanup to settle.
                    await Task.Delay(200);
                }

                try
                {
                    _partySession = await MultiplayerService.Instance.CreateSessionAsync(opts);
                    connectionData.IsHost = true;

                    // Give the new session a grace period before the refresh loop
                    // tries RefreshAsync() on it — avoids immediate "stale" errors
                    // that would null the session and trigger another recreation.
                    _refreshTimer = -(refreshIntervalSeconds);

                    Debug.Log($"[HostConnectionService] Created party session {_partySession.Id}");

                    // Reload Menu_Main as a network scene so the new Relay host
                    // serves it properly — but only on the first local→Relay
                    // transition. Subsequent recreations (e.g. after returning
                    // from a game) must NOT reload or we enter an infinite loop:
                    // reload → Start() → recreate → reload → ...
                    if (!_hasReloadedMenuForRelay)
                    {
                        _hasReloadedMenuForRelay = true;
                        ReloadMenuSceneIfActive();
                    }

                    return;
                }
                catch (Exception e) when (attempt < HOST_CONFLICT_MAX_RETRIES && IsHostConflictException(e))
                {
                    // A local host was started by another system (e.g.
                    // AuthenticationSceneController) during Relay allocation.
                    // The next iteration's pre-check will shut it down.
                    Debug.LogWarning($"[HostConnectionService] Host conflict during Relay session creation — retry {attempt + 1}/{HOST_CONFLICT_MAX_RETRIES}");
                }
                catch (Exception e) when (attempt < RATE_LIMIT_MAX_RETRIES && IsRateLimitException(e))
                {
                    int delay = RATE_LIMIT_BASE_DELAY_MS * (1 << attempt);
                    Debug.LogWarning($"[HostConnectionService] Rate limited creating party session — retry {attempt + 1}/{RATE_LIMIT_MAX_RETRIES} in {delay}ms");
                    await Task.Delay(delay);
                }
            }
        }

        /// <summary>
        /// After transitioning from local host to Relay host, reloads Menu_Main
        /// via Netcode scene management so it is properly served to joining clients.
        /// </summary>
        private void ReloadMenuSceneIfActive()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening || nm.SceneManager == null) return;

            var activeScene = SceneManager.GetActiveScene();
            if (activeScene.name == "Menu_Main")
            {
                Debug.Log("[HostConnectionService] Reloading Menu_Main as network scene (Relay host)...");
                _sceneTransitionManager?.SetFadeImmediate(1f);

                if (_gameData != null)
                    _gameData.OnClientReady.OnRaised += FadeFromSplashOnReady;

                nm.SceneManager.LoadScene("Menu_Main", LoadSceneMode.Single);
            }
        }

        private void FadeFromSplashOnReady()
        {
            if (_gameData != null)
                _gameData.OnClientReady.OnRaised -= FadeFromSplashOnReady;
            _sceneTransitionManager?.FadeFromBlack().Forget();
        }

        /// <summary>
        /// Pushes the local player's current party count/max to the presence lobby
        /// player properties, so remote clients can render this player's row with
        /// the right status text ("IN LOBBY X/4", "LOBBY FULL", etc).
        /// Called from within <see cref="RefreshAsync"/> while _lobbyBusy is held —
        /// safe to call SaveCurrentPlayerData from here.
        /// No-op when nothing has changed since the last publish.
        /// </summary>
        private async Task PublishPartyStateIfChangedAsync()
        {
            if (_presenceLobby == null) return;

            int currentCount = connectionData.PartyMembers != null ? connectionData.PartyMembers.Count : 0;

            if (currentCount == _publishedPartyCount) return;

            try
            {
                _presenceLobby.CurrentPlayer.SetProperty(PARTY_COUNT_KEY,
                    new PlayerProperty(currentCount.ToString(), VisibilityPropertyOptions.Public));
                _presenceLobby.CurrentPlayer.SetProperty(PARTY_MAX_KEY,
                    new PlayerProperty(connectionData.MaxPartySlots.ToString(), VisibilityPropertyOptions.Public));

                await SaveWithRetryAsync();
                _publishedPartyCount = currentCount;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[HostConnectionService] PublishPartyState error: {e.Message}");
            }
        }

        /// <summary>
        /// Clears the SENDER's own invite properties after the invited player
        /// joins the party. This stops receivers from seeing stale invite data.
        /// Always called from within RefreshAsync() which already holds _lobbyBusy —
        /// no additional mutex needed here.
        /// </summary>
        private async Task ClearSentInvitePropertiesAsync()
        {
            if (_presenceLobby == null) return;

            try
            {
                await _presenceLobby.RefreshAsync();
                _presenceLobby.CurrentPlayer.SetProperty(INVITE_TARGET_KEY,
                    new PlayerProperty(string.Empty, VisibilityPropertyOptions.Public));
                _presenceLobby.CurrentPlayer.SetProperty(INVITE_DATA_KEY,
                    new PlayerProperty(string.Empty, VisibilityPropertyOptions.Public));
                await SaveWithRetryAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[HostConnectionService] ClearSentInvite error: {e.Message}");
            }
        }

        /// <summary>
        /// Saves current player data with retry on UGS rate-limit (HTTP 429).
        /// The Lobby service rate-limits at ~1 request per 1.5s. If a refresh
        /// just ran, the save can hit 429. Retries up to 3 times with 2s backoff.
        /// </summary>
        private async Task SaveWithRetryAsync()
        {
            const int maxRetries = 3;
            const int retryDelayMs = 2000;

            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                try
                {
                    await _presenceLobby.SaveCurrentPlayerDataAsync();

                    // Post-save refresh: sync the SDK's cached lobby state with the
                    // server immediately after the save. This reduces the window where
                    // incoming WebSocket lobby-change deltas reference stale player
                    // indices, which triggers ArgumentOutOfRangeException in the SDK's
                    // internal LobbyPatcher.
                    try { await _presenceLobby.RefreshAsync(); }
                    catch { /* best-effort — polling corrects state on next cycle */ }

                    return;
                }
                catch (Exception e) when (attempt < maxRetries &&
                    (e.Message.Contains("Too Many Requests") ||
                     e.Message.Contains("Index was out of range")))
                {
                    Debug.LogWarning($"[HostConnectionService] SaveCurrentPlayerData failed ({e.Message}) — retry {attempt + 1}/{maxRetries} in {retryDelayMs}ms");
                    await Task.Delay(retryDelayMs);

                    // Re-sync the SDK's cached player list before retrying.
                    try { await _presenceLobby.RefreshAsync(); }
                    catch { /* best-effort refresh */ }
                }
            }
        }

        /// <summary>
        /// Clears this player's own invite properties and resets dedup state.
        /// Used by the RECIPIENT when declining an invite.
        /// </summary>
        private async Task RequestClearInviteAsync()
        {
            if (_presenceLobby == null) return;

            while (_lobbyBusy)
                await Task.Yield();
            _lobbyBusy = true;
            try
            {
                // Refresh first to sync the SDK's cached player index,
                // then set properties and save.
                await _presenceLobby.RefreshAsync();
                _presenceLobby.CurrentPlayer.SetProperty(INVITE_TARGET_KEY,
                    new PlayerProperty(string.Empty, VisibilityPropertyOptions.Public));
                _presenceLobby.CurrentPlayer.SetProperty(INVITE_DATA_KEY,
                    new PlayerProperty(string.Empty, VisibilityPropertyOptions.Public));
                await SaveWithRetryAsync();

                _lastFiredInvite = null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[HostConnectionService] ClearInvite error: {e.Message}");
            }
            finally
            {
                _lobbyBusy = false;
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

        /// <summary>
        /// Tracks whether we've already wired the profile-change subscription so
        /// repeated calls (Start + HandleSignedInEvent) don't stack handlers.
        /// </summary>
        private bool _profileSubscribed;

        /// <summary>
        /// Subscribes to <see cref="PlayerDataService.OnProfileChanged"/> once.
        /// Called from Start and HandleSignedInEvent. Ensures that when the
        /// cloud profile resolves (or the user edits their name/avatar), the
        /// presence lobby and party session both see the updated values.
        /// </summary>
        private void SubscribeToProfileChanges()
        {
            if (_profileSubscribed || playerDataService == null) return;
            playerDataService.OnProfileChanged += HandleProfileChanged;
            _profileSubscribed = true;
        }

        private void UnsubscribeFromProfileChanges()
        {
            if (!_profileSubscribed || playerDataService == null) return;
            playerDataService.OnProfileChanged -= HandleProfileChanged;
            _profileSubscribed = false;
        }

        private void HandleProfileChanged(PlayerProfileData profile)
        {
            if (profile == null) return;

            SyncLocalIdentity();

            // Republish identity to the presence lobby so remote players see
            // the correct display name and avatar instead of the bootstrap
            // "Pilot" fallback. Fire-and-forget — the refresh loop will recover
            // from any transient error.
            RepublishLocalIdentityAsync().Forget();
        }

        /// <summary>
        /// Writes the current LocalDisplayName + LocalAvatarId into the presence
        /// lobby's player properties so remote clients see the up-to-date values.
        /// Wrapped with the <see cref="_lobbyBusy"/> mutex to avoid racing the
        /// refresh loop or in-flight invite sends.
        /// </summary>
        private async UniTaskVoid RepublishLocalIdentityAsync()
        {
            if (_presenceLobby == null) return;

            while (_lobbyBusy)
                await Task.Yield();
            _lobbyBusy = true;
            try
            {
                await _presenceLobby.RefreshAsync();
                _presenceLobby.CurrentPlayer.SetProperty(DISPLAY_NAME_KEY,
                    new PlayerProperty(connectionData.LocalDisplayName ?? "Pilot",
                        VisibilityPropertyOptions.Public));
                _presenceLobby.CurrentPlayer.SetProperty(AVATAR_ID_KEY,
                    new PlayerProperty(connectionData.LocalAvatarId.ToString(),
                        VisibilityPropertyOptions.Public));
                await SaveWithRetryAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[HostConnectionService] RepublishLocalIdentity error: {e.Message}");
            }
            finally
            {
                _lobbyBusy = false;
            }
        }

        private Dictionary<string, PlayerProperty> BuildLocalPlayerProperties()
        {
            int partyCount = connectionData.PartyMembers != null ? connectionData.PartyMembers.Count : 0;
            int partyMax = connectionData.MaxPartySlots;

            return new Dictionary<string, PlayerProperty>
            {
                { DISPLAY_NAME_KEY, new PlayerProperty(connectionData.LocalDisplayName ?? "Pilot", VisibilityPropertyOptions.Public) },
                { AVATAR_ID_KEY,    new PlayerProperty(connectionData.LocalAvatarId.ToString(),    VisibilityPropertyOptions.Public) },
                { PARTY_COUNT_KEY,  new PlayerProperty(partyCount.ToString(), VisibilityPropertyOptions.Public) },
                { PARTY_MAX_KEY,    new PlayerProperty(partyMax.ToString(),   VisibilityPropertyOptions.Public) },
                { MATCH_NAME_KEY,   new PlayerProperty(string.Empty,          VisibilityPropertyOptions.Public) },
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

        // ─────────────────────────────────────────────────────────────────────
        // Lobby SDK Log Filter
        // ─────────────────────────────────────────────────────────────────────

        private void InstallLobbyLogFilter()
        {
            _originalLogHandler = Debug.unityLogger.logHandler;
            Debug.unityLogger.logHandler = new LobbyPatcherLogFilter(_originalLogHandler);
        }

        private void UninstallLobbyLogFilter()
        {
            if (_originalLogHandler != null && Debug.unityLogger.logHandler is LobbyPatcherLogFilter)
                Debug.unityLogger.logHandler = _originalLogHandler;
            _originalLogHandler = null;
        }

        /// <summary>
        /// Suppresses the known UGS SDK <see cref="ArgumentOutOfRangeException"/> thrown
        /// by <c>LobbyPatcher.ApplyPatchesToLobby</c> when a WebSocket lobby-change delta
        /// references a player index that is stale in the local cache.
        ///
        /// This is a race condition in the SDK (com.unity.services.multiplayer): the server
        /// computes a change delta against the current lobby state, but by the time the
        /// client receives it, a player may have joined or left, shifting indices. The SDK
        /// catches this internally at <c>LobbyChannel.HandleLobbyChanges</c> and logs it,
        /// but the lobby self-corrects on the next <see cref="ISession.RefreshAsync"/> poll.
        ///
        /// This filter only suppresses that specific harmless error — all other log messages
        /// pass through unmodified.
        /// </summary>
        private class LobbyPatcherLogFilter : ILogHandler
        {
            private readonly ILogHandler _inner;

            public LobbyPatcherLogFilter(ILogHandler inner) => _inner = inner;

            public void LogException(Exception exception, UnityEngine.Object context)
            {
                if (exception is ArgumentOutOfRangeException &&
                    exception.StackTrace?.Contains("LobbyPatcher") == true)
                    return;

                _inner.LogException(exception, context);
            }

            public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args)
            {
                if (logType == LogType.Error && args is { Length: > 0 })
                {
                    string msg = args[0]?.ToString() ?? string.Empty;
                    if (msg.Contains("LobbyPatcher") && msg.Contains("Index was out of range"))
                        return;
                }

                _inner.LogFormat(logType, context, format, args);
            }
        }
    }
}
