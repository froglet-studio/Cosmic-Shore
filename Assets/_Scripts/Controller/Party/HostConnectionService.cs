using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
    /// Coordinates the user's presence in the global lobby and (lazily) their
    /// Relay-backed party session. Single source of truth for outgoing/incoming
    /// invites and party-member state; everything else flows through SOAP.
    ///
    /// Internally split into focused helpers (kept as nested types so the public
    /// MonoBehaviour surface stays a single, inspector-stable component):
    ///
    /// • <see cref="OutgoingInviteTracker"/> – owns the local outstanding-invite map
    ///   (timeout expiry + composite serialization).
    /// • <see cref="LobbyPatcherLogFilter"/> – swallows known harmless SDK noise.
    /// • A pair of <see cref="SemaphoreSlim"/>s – one serializes lobby reads/writes
    ///   (replacing the legacy busy-flag spinloop), one dedups Relay-session creation.
    /// • <see cref="RunLobbyPropertyWriteAsync"/> – DRYs the
    ///   acquire-mutex + refresh + set-property + save-with-retry pattern that
    ///   previously appeared in five near-identical methods.
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

        [Tooltip("How often (seconds) to refresh the online player list and check for invites. " +
                 "UGS lobby read rate limit is ~1/s per client, so 1.5s keeps us safely under " +
                 "while staying responsive enough that invite arrival and member joins feel instant.")]
        [SerializeField] private float refreshIntervalSeconds = 1.5f;

        [Inject] private PlayerDataService playerDataService;
        [Inject] private GameDataSO _gameData;

        // ─────────────────────────────────────────────────────────────────────
        // Static singleton access
        // ─────────────────────────────────────────────────────────────────────

        public static HostConnectionService Instance { get; private set; }

        // ─────────────────────────────────────────────────────────────────────
        // Constants — keys, sentinels, separators, tuning
        // (Names preserved verbatim; PartyInviteSystemTests reflects on them.)
        // ─────────────────────────────────────────────────────────────────────

        private const string PRESENCE_LOBBY_GAME_MODE = "PRESENCE_LOBBY";
        private const string DISPLAY_NAME_KEY        = "displayName";
        private const string AVATAR_ID_KEY           = "avatarId";
        private const string PARTY_COUNT_KEY         = "partyCount";
        private const string PARTY_MAX_KEY           = "partyMax";
        private const string MATCH_NAME_KEY          = "matchName";
        private const string INVITE_PAYLOADS_KEY     = "invite_payloads";
        private const string JOINED_PARTY_KEY        = "joined_party";
        private const string ACCEPTED_INVITE_KEY     = "accepted_invite";
        private const string PENDING_SESSION_ID      = "PENDING";

        private const char INVITE_LINE_SEPARATOR  = '\n';
        private const char INVITE_FIELD_SEPARATOR = '|';

        private const float OUTGOING_INVITE_TIMEOUT_SECONDS  = 30f;
        private const int   LOBBY_RACE_SETTLE_MS             = 1500;
        private const int   MAX_REFRESH_ERRORS_BEFORE_RECONNECT = 3;
        private const int   RATE_LIMIT_MAX_RETRIES           = 3;
        private const int   RATE_LIMIT_BASE_DELAY_MS         = 2000;
        private const float BOOSTED_REFRESH_INTERVAL_SECONDS = 0.75f;
        private const float BOOSTED_REFRESH_WINDOW_SECONDS   = 15f;
        private const float FORCE_REFRESH_COOLDOWN_SECONDS   = 0.5f;
        private const int   PROFILE_INIT_TIMEOUT_MS          = 5000;
        private const int   HOST_CONFLICT_MAX_RETRIES        = 2;

        /// <summary>
        /// After session creation, suppress <see cref="RefreshPartyMembersAsync"/>
        /// for this many seconds. A freshly-provisioned session can transiently
        /// fail RefreshAsync with non-429 errors; nulling <see cref="_partySession"/>
        /// in response would cause <see cref="ScanForAcceptanceSignalsAsync"/> to
        /// recreate it on the next tick, kicking any joining client.
        /// </summary>
        private const float SESSION_CREATION_GRACE_PERIOD_SECONDS = 4f;

        // ─────────────────────────────────────────────────────────────────────
        // Sessions
        // ─────────────────────────────────────────────────────────────────────

        private ISession _presenceLobby;
        private ISession _partySession;
        private float    _partySessionCreatedAt;

        // ─────────────────────────────────────────────────────────────────────
        // Synchronization
        //
        // The legacy `_lobbyBusy` boolean was a hand-rolled mutex with two
        // serious flaws:
        //   1. Callers spun on `while (_lobbyBusy) await Task.Yield()` — no
        //      FIFO ordering, no fairness guarantees, no cancellation support.
        //   2. Re-entrance from inside RefreshAsync was hacked via
        //      `bool ownsLobby = !_lobbyBusy` — easy to misread.
        //
        // SemaphoreSlim(1,1) gives us a real mutex. _insideRefreshCycle makes
        // the reentrant case explicit: helpers called from inside RefreshAsync
        // skip re-acquiring; helpers called from outside acquire normally.
        // ─────────────────────────────────────────────────────────────────────

        private readonly SemaphoreSlim _lobbyMutex            = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim _sessionCreationMutex  = new SemaphoreSlim(1, 1);
        private bool                   _insideRefreshCycle;

        // ─────────────────────────────────────────────────────────────────────
        // Lifecycle / state
        // ─────────────────────────────────────────────────────────────────────

        private bool   _initialized;
        private bool   _joining;
        private bool   _leaving;
        private bool   _profileSubscribed;
        private bool   _gameLaunchSubscribed;
        private float  _refreshTimer;
        private float  _rateLimitBackoffUntil;
        private float  _boostedRefreshUntil;
        private float  _nextForcedRefreshAllowed;
        private int    _consecutiveRefreshErrors;
        private int    _publishedPartyCount = -1;
        private string _publishedMatchName  = "<UNSET>";
        private ILogHandler _originalLogHandler;

        // ─────────────────────────────────────────────────────────────────────
        // Invite state
        // ─────────────────────────────────────────────────────────────────────

        private PartyInviteData? _lastFiredInvite;
        /// <summary>
        /// True after the local user has accept/decline/left for <see cref="_lastFiredInvite"/>.
        /// Kept alongside the cached invite so the SDK-side dedup guard still
        /// suppresses repeated SOAP raises while UI queries via <see cref="LastPendingInvite"/>
        /// correctly report "no pending invite".
        /// </summary>
        private bool _lastInviteResolved;

        private readonly OutgoingInviteTracker _outgoingInvites = new OutgoingInviteTracker();

        /// <summary>Raised when an outgoing invite is cleared (any reason).</summary>
        public event Action<string> OutgoingInviteCleared;
        public IReadOnlyCollection<string> OutgoingInviteTargets => _outgoingInvites.Targets;

        // ─────────────────────────────────────────────────────────────────────
        // Public read-only state
        // ─────────────────────────────────────────────────────────────────────

        public ISession PartySession => _partySession;

        /// <summary>
        /// Most recently detected incoming invite, or null once the user has
        /// resolved it (accept/decline/leave) or Menu_Main has reloaded.
        /// </summary>
        public PartyInviteData? LastPendingInvite =>
            _lastInviteResolved ? null : _lastFiredInvite;

        // ╔═══════════════════════════════════════════════════════════════════╗
        // ║  Unity Lifecycle                                                  ║
        // ╚═══════════════════════════════════════════════════════════════════╝

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

            await EnsureInitializedAsync();
        }

        void Update()
        {
            if (!_initialized || _presenceLobby == null) return;
            if (_lobbyMutex.CurrentCount == 0) return;                   // someone is already inside the mutex
            if (Time.unscaledTime < _rateLimitBackoffUntil) return;
            if (!IsOnMenuScene()) return;

            ExpireOutgoingInvites();

            _refreshTimer += Time.unscaledDeltaTime;
            float interval = Time.unscaledTime < _boostedRefreshUntil
                ? BOOSTED_REFRESH_INTERVAL_SECONDS
                : refreshIntervalSeconds;

            if (_refreshTimer >= interval)
            {
                _refreshTimer = 0f;
                RefreshAsync().Forget();
            }
        }

        async void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            UnsubscribeFromProfileChanges();
            UnsubscribeFromGameLaunch();
            UninstallLobbyLogFilter();
            await LeavePresenceLobbyAsync();

            _lobbyMutex.Dispose();
            _sessionCreationMutex.Dispose();

            if (Instance == this)
                Instance = null;
        }

        /// <summary>
        /// Resets per-session invite state on Menu_Main reload and rebuilds
        /// <see cref="HostConnectionDataSO.PartyMembers"/> from <see cref="_partySession"/>.
        /// SOAP <c>ScriptableList</c> wipes itself on every <c>LoadSceneMode.Single</c>
        /// load, so we must re-seed if a session was already active.
        /// </summary>
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "Menu_Main") return;

            _lastFiredInvite     = null;
            _lastInviteResolved  = false;
            PublishPresenceImmediateAsync().Forget();
            RepopulatePartyMembersFromSession();
        }

        // ╔═══════════════════════════════════════════════════════════════════╗
        // ║  Initialization (shared by Start + sign-in SOAP event)            ║
        // ╚═══════════════════════════════════════════════════════════════════╝

        public async void HandleSignedInEvent()
        {
            if (!IsAuthSignedInAndHasId()) return;
            await EnsureInitializedAsync();
        }

        public async void HandleSignedOutEvent()
        {
            _initialized = false;
            connectionData.ResetRuntimeData();
            await LeavePresenceLobbyAsync();
            connectionData.OnHostConnectionLost?.Raise();
        }

        /// <summary>
        /// Idempotent initialization. Safe to call from both <see cref="Start"/>
        /// (auth-already-signed-in path) and <see cref="HandleSignedInEvent"/>
        /// (auth-signed-in-after-Start path) — concurrent calls collapse to one.
        ///
        /// NOTE: party session is intentionally NOT created here. Eager creation
        /// would burn a Relay allocation per launch and would call
        /// <c>nm.Shutdown()</c> + <c>StartHost()</c> — destroying and respawning
        /// every menu vessel. The Relay session is created lazily on first
        /// invite acceptance via <see cref="ScanForAcceptanceSignalsAsync"/>.
        /// </summary>
        private async Task EnsureInitializedAsync()
        {
            if (_initialized || _joining) return;
            _joining = true;
            try
            {
                SubscribeToProfileChanges();
                SubscribeToGameLaunch();

                await WaitForProfileInitAsync(PROFILE_INIT_TIMEOUT_MS);
                SyncLocalIdentity();

                await JoinPresenceLobbyAsync();

                // Catch the case where the cloud profile resolved during
                // JoinPresenceLobbyAsync — HandleProfileChanged's republish
                // would have been a no-op (lobby was still null at that moment).
                SyncLocalIdentity();
                RepublishLocalIdentityAsync().Forget();

                _initialized = true;
                DebugExtensions.LogColored(
                    $"[HostConnectionService] Initialized — lobby: {_presenceLobby?.Id ?? "NULL"}, " +
                    $"partySession: {_partySession?.Id ?? "NULL"}, " +
                    $"localId: {connectionData.LocalPlayerId}",
                    Color.green);

                RefreshAsync().Forget();
            }
            finally { _joining = false; }
        }

        // ╔═══════════════════════════════════════════════════════════════════╗
        // ║  Public Invite API                                                ║
        // ╚═══════════════════════════════════════════════════════════════════╝

        public async Task SendInviteAsync(string targetPlayerId)
        {
            DebugExtensions.LogColored(
                $"[INVITE-SEND] SendInviteAsync called — target: {targetPlayerId}", Color.cyan);

            if (_presenceLobby == null)
            {
                DebugExtensions.LogErrorColored(
                    "[INVITE-SEND] ABORT — _presenceLobby is null", Color.red);
                throw new InvalidOperationException("Presence lobby unavailable.");
            }

            // Idempotent re-click: just refresh the timeout, no network roundtrip.
            if (_outgoingInvites.Contains(targetPlayerId))
            {
                DebugExtensions.LogColored(
                    $"[INVITE-SEND] {targetPlayerId} already pending — refreshing timeout",
                    Color.yellow);
                _outgoingInvites.RefreshTimeout(targetPlayerId,
                    Time.unscaledTime + OUTGOING_INVITE_TIMEOUT_SECONDS);
                return;
            }

            await _lobbyMutex.WaitAsync();
            bool inviteAdded = false;
            try
            {
                SyncLocalIdentity();
                DebugExtensions.LogColored(
                    $"[INVITE-SEND] LocalPlayerId: {connectionData.LocalPlayerId}, " +
                    $"DisplayName: {connectionData.LocalDisplayName}", Color.cyan);

                // No NetworkManager activity here — the Relay session is created
                // lazily when the first recipient accepts (see
                // ScanForAcceptanceSignalsAsync). Sending an invite must never
                // touch NM, otherwise the host's vessel respawns mid-menu.
                DebugExtensions.LogColored(
                    $"[INVITE-SEND] PartySession ID: {_partySession?.Id ?? PENDING_SESSION_ID} " +
                    $"(PENDING until first accept)", Color.cyan);

                string payload = BuildInvitePayload(targetPlayerId);
                _outgoingInvites.AddOrRefresh(targetPlayerId, payload,
                    Time.unscaledTime + OUTGOING_INVITE_TIMEOUT_SECONDS);
                inviteAdded = true;

                DebugExtensions.LogColored(
                    $"[INVITE-SEND] target='{targetPlayerId}', payload='{payload}'", Color.cyan);

                // Best-effort refresh to sync the SDK's player-index cache before
                // SaveCurrentPlayerDataAsync. Without it the save can fail silently.
                try { await _presenceLobby.RefreshAsync(); }
                catch { /* SaveWithRetryAsync handles stale state */ }

                PublishInvitePayloadsToCurrentPlayer();
                await SaveWithRetryAsync();

                DebugExtensions.LogColored(
                    "[INVITE-SEND] SaveCurrentPlayerDataAsync completed — properties persisted",
                    Color.green);

                foreach (var player in connectionData.OnlinePlayers.ToList())
                {
                    if (player.PlayerId != targetPlayerId) continue;
                    connectionData.OnInviteSent?.Raise(player);
                    DebugExtensions.LogColored(
                        $"[INVITE-SEND] OnInviteSent raised for {player.DisplayName}",
                        Color.green);
                    break;
                }
            }
            catch (Exception e)
            {
                DebugExtensions.LogErrorColored(
                    $"[INVITE-SEND] ERROR: {e.Message}\n{e.StackTrace}", Color.red);

                if (inviteAdded)
                {
                    _outgoingInvites.Remove(targetPlayerId);
                    OutgoingInviteCleared?.Invoke(targetPlayerId);
                }
                throw;
            }
            finally
            {
                _refreshTimer = 0f;
                _boostedRefreshUntil = Time.unscaledTime + BOOSTED_REFRESH_WINDOW_SECONDS;
                _lobbyMutex.Release();
            }
        }

        public async Task AcceptInviteAsync(PartyInviteData invite)
        {
            // Mark resolved up-front so a re-opened FriendsListPanel doesn't
            // re-spawn a row for the invite the user just accepted.
            _lastInviteResolved = true;
            connectionData.OnInviteResolved?.Raise();
            try
            {
                SyncLocalIdentity();

                // Three-phase accept:
                //   1. Tell the host we accepted (presence-lobby property write).
                //   2. Wait for the host to publish the real session id (poll).
                //   3. Join the now-real session via Relay.
                await PublishAcceptanceSignalAsync(invite.HostPlayerId);

                string realSessionId = invite.PartySessionId;
                if (string.IsNullOrEmpty(realSessionId) || realSessionId == PENDING_SESSION_ID)
                {
                    Debug.Log("[HostConnectionService] Invite has PENDING session — polling for real id...");
                    _boostedRefreshUntil = Time.unscaledTime + BOOSTED_REFRESH_WINDOW_SECONDS;
                    realSessionId = await WaitForRealSessionIdAsync(invite.HostPlayerId);
                }

                _partySession = await MultiplayerService.Instance.JoinSessionByIdAsync(
                    realSessionId,
                    new JoinSessionOptions { PlayerProperties = BuildLocalPlayerProperties() });

                connectionData.IsHost = false;

                connectionData.PartyMembers?.Clear();
                connectionData.PartyMembers?.Add(connectionData.LocalPlayerData);
                var hostData = new PartyPlayerData(invite.HostPlayerId, invite.HostDisplayName, invite.HostAvatarId);
                connectionData.PartyMembers?.Add(hostData);
                connectionData.OnPartyMemberJoined?.Raise(hostData);

                _refreshTimer = -refreshIntervalSeconds;
                Debug.Log($"[HostConnectionService] Joined party {_partySession.Id}");

                _boostedRefreshUntil = Time.unscaledTime + BOOSTED_REFRESH_WINDOW_SECONDS;

                // Advertise this join so the host's RefreshAsync picks us up
                // before their party-session Players list catches up.
                PublishJoinedPartyAsync(realSessionId).Forget();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[HostConnectionService] AcceptInvite error: {e.Message}");
            }
        }

        public Task DeclineInviteAsync()
        {
            // Sender's slot is freed by their own timeout — UGS doesn't expose
            // a decline signal back to the sender.
            _lastFiredInvite     = null;
            _lastInviteResolved  = true;
            connectionData.OnInviteResolved?.Raise();
            return Task.CompletedTask;
        }

        /// <summary>
        /// Client-side leave. Routes through <see cref="PartyInviteController"/>
        /// for the Netcode shutdown + fresh local-host restart.
        /// </summary>
        public async Task LeavePartyAsync()
        {
            var controller = PartyInviteController.Instance;
            if (controller == null)
            {
                Debug.LogWarning("[HostConnectionService] PartyInviteController not available.");
                return;
            }

            _lastFiredInvite    = null;
            _lastInviteResolved = true;
            connectionData.OnInviteResolved?.Raise();

            // Locally fire OnPartyMemberLeft so panels clear slots immediately
            // instead of waiting for the next refresh tick.
            if (connectionData.PartyMembers != null && connectionData.OnPartyMemberLeft != null)
            {
                foreach (var member in connectionData.PartyMembers.ToList())
                {
                    if (member.PlayerId == connectionData.LocalPlayerId) continue;
                    connectionData.OnPartyMemberLeft.Raise(member);
                }
            }

            ClearJoinedPartyAsync().Forget();
            await controller.LeavePartyAndReturnToMenuAsync();
        }

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

            if (_partySession != null)
            {
                try
                {
                    await _partySession.AsHost().RemovePlayerAsync(playerId);
                    Debug.Log($"[HostConnectionService] Kicked {playerId} from party session.");
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[HostConnectionService] KickPartyMember session error: {e.Message}");
                }
            }
        }

        // ╔═══════════════════════════════════════════════════════════════════╗
        // ║  Public misc API                                                  ║
        // ╚═══════════════════════════════════════════════════════════════════╝

        /// <summary>
        /// On-demand refresh trigger. Safe to call from any UI code; cooldown +
        /// mutex make repeated calls collapse to at most one lobby hit.
        /// </summary>
        public void ForceRefreshNow()
        {
            if (!_initialized || _presenceLobby == null) return;

            _boostedRefreshUntil = Time.unscaledTime + BOOSTED_REFRESH_WINDOW_SECONDS;

            if (Time.unscaledTime < _nextForcedRefreshAllowed) return;
            _nextForcedRefreshAllowed = Time.unscaledTime + FORCE_REFRESH_COOLDOWN_SECONDS;

            _refreshTimer = 0f;
            if (_lobbyMutex.CurrentCount == 0) return; // already running
            RefreshAsync().Forget();
        }

        /// <summary>
        /// Drops the cached party session reference so the next outgoing invite
        /// re-creates one fresh. Used by <see cref="Core.SceneLoader"/> on
        /// game→menu transitions.
        /// </summary>
        public void ClearStalePartySession()
        {
            if (_partySession == null) return;
            Debug.Log("[HostConnectionService] Clearing stale party session reference.");

            _partySession          = null;
            _partySessionCreatedAt = 0f;
            _lastFiredInvite       = null;
            connectionData.PartyMembers?.Clear();

            ClearJoinedPartyAsync().Forget();
        }

        /// <summary>
        /// Public wrapper for party session creation. Reserved; no current callers.
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

        // ╔═══════════════════════════════════════════════════════════════════╗
        // ║  Presence Lobby — Join / Create / Leave                           ║
        // ╚═══════════════════════════════════════════════════════════════════╝

        private async Task JoinPresenceLobbyAsync()
        {
            if (_presenceLobby != null) return;

            // Lobby-only session (no Relay) used purely for player discovery
            // and invite property exchange. Coexists safely with any active NM.
            try
            {
                _presenceLobby = await TryQueryAndJoinLobbyAsync();

                if (_presenceLobby == null)
                {
                    await CreatePresenceLobbyAsync();

                    // Re-query after a short settle; if a rival lobby was created
                    // simultaneously (MPPM, near-simultaneous launches), merge into it.
                    await Task.Delay(LOBBY_RACE_SETTLE_MS);
                    var rival = await TryQueryAndJoinLobbyAsync();
                    if (rival != null)
                    {
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
                connectionData.IsHost      = _presenceLobby.IsHost;

                connectionData.PartyMembers?.Clear();
                connectionData.PartyMembers?.Add(connectionData.LocalPlayerData);

                connectionData.OnHostConnectionEstablished?.Raise();
            }
        }

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

            if (sessions.Count == 0) return null;

            foreach (var session in sessions)
            {
                if (_presenceLobby != null && session.Id == _presenceLobby.Id) continue;

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
                var opts = new SessionOptions
                {
                    MaxPlayers       = presenceLobbyMaxPlayers,
                    IsLocked         = false,
                    IsPrivate        = false,
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

        // ╔═══════════════════════════════════════════════════════════════════╗
        // ║  Party Session — Lazy Relay creation                              ║
        // ╚═══════════════════════════════════════════════════════════════════╝

        private async Task CreatePartySessionAsync()
        {
            if (_partySession != null) return;

            // Dedicated mutex — replaces the legacy _creatingPartySessionTask
            // pattern (which had a finally-clear race window). Double-check
            // pattern: a second caller waits, then sees the session and bails.
            await _sessionCreationMutex.WaitAsync();
            try
            {
                if (_partySession != null) return;
                await CreatePartySessionCoreAsync();
            }
            finally
            {
                _sessionCreationMutex.Release();
            }
        }

        private async Task CreatePartySessionCoreAsync()
        {
            var opts = new SessionOptions
            {
                MaxPlayers       = connectionData.MaxPartySlots,
                IsLocked         = false,
                IsPrivate        = true,
                PlayerProperties = BuildLocalPlayerProperties()
            }.WithRelayNetwork();

            for (int attempt = 0; ; attempt++)
            {
                // The UGS Multiplayer SDK calls NetworkManager.StartHost()
                // internally for Relay-backed sessions. If a local host is
                // already running, that call fails — shut it down first.
                var nm = NetworkManager.Singleton;
                if (nm != null && nm.IsListening)
                {
                    Debug.Log("[HostConnectionService] Shutting down local host before Relay party session creation...");
                    nm.Shutdown();

                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    while (nm != null && nm.IsListening && sw.ElapsedMilliseconds < 5000)
                        await Task.Delay(100);

                    await Task.Delay(200); // transport cleanup settle
                }

                try
                {
                    _partySession          = await MultiplayerService.Instance.CreateSessionAsync(opts);
                    connectionData.IsHost  = true;
                    _partySessionCreatedAt = Time.unscaledTime;

                    // Give the new session breathing room before RefreshAsync
                    // touches it — avoids transient "stale" errors that would
                    // null the session and trigger another recreation.
                    _refreshTimer = -refreshIntervalSeconds;

                    Debug.Log($"[HostConnectionService] Created party session {_partySession.Id}");
                    return;
                }
                catch (Exception e) when (attempt < HOST_CONFLICT_MAX_RETRIES && IsHostConflictException(e))
                {
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

        // ╔═══════════════════════════════════════════════════════════════════╗
        // ║  Refresh Loop                                                     ║
        // ╚═══════════════════════════════════════════════════════════════════╝

        private async UniTaskVoid RefreshAsync()
        {
            if (_presenceLobby == null) return;

            // Quick non-blocking check — if someone else holds the mutex, skip
            // this tick rather than queuing up. The next tick will pick up.
            if (!await _lobbyMutex.WaitAsync(0))
                return;

            _insideRefreshCycle = true;
            bool shouldReconnect = false;
            try
            {
                await _presenceLobby.RefreshAsync();

                // Diff-based update — never Clear() + re-Add() (would flicker UI).
                if (connectionData.OnlinePlayers != null)
                    RefreshOnlinePlayersDiff();

                // Scan composite invite_payloads for lines targeting us.
                foreach (var p in _presenceLobby.Players)
                {
                    if (p.Id == connectionData.LocalPlayerId) continue;
                    if (TryFindIncomingInvite(p, out var invite))
                        TryRaiseIncomingInvite(invite);
                }

                // Acceptance-signal scan: lazy party-session creation. Must run
                // BEFORE the JOINED_PARTY_KEY scan because recipients won't set
                // joined_party until after they read the real session id.
                //
                // Gate on outgoing-invite count (NOT IsHost) — IsHost is set
                // only after CreatePartySessionAsync succeeds, and lazy creation
                // hasn't reached that point yet on the first acceptance.
                if (_outgoingInvites.Count > 0)
                    await ScanForAcceptanceSignalsAsync();

                if (_partySession != null && connectionData.IsHost)
                    ScanPresenceForJoinedPartyMembers();

                if (_partySession != null)
                    await RefreshPartyMembersAsync();

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
                        _presenceLobby            = null;
                        shouldReconnect           = true;
                    }
                }
            }
            finally
            {
                _insideRefreshCycle = false;
                _lobbyMutex.Release();
            }

            if (shouldReconnect)
                await JoinPresenceLobbyAsync();
        }

        private bool TryFindIncomingInvite(IReadOnlyPlayer sender, out PartyInviteData invite)
        {
            invite = default;
            if (!sender.Properties.TryGetValue(INVITE_PAYLOADS_KEY, out var payloadsProp))
                return false;
            if (payloadsProp == null || string.IsNullOrEmpty(payloadsProp.Value))
                return false;

            foreach (var line in payloadsProp.Value.Split(INVITE_LINE_SEPARATOR))
            {
                if (string.IsNullOrEmpty(line)) continue;

                var parsed = ParseInviteLine(line);
                if (!parsed.HasValue)
                {
                    DebugExtensions.LogErrorColored(
                        $"[INVITE-RECV] ParseInviteLine FAILED for line: '{line}'",
                        Color.red);
                    continue;
                }
                if (parsed.Value.targetId != connectionData.LocalPlayerId) continue;

                invite = parsed.Value.invite;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Parses one invite line in the format
        /// <c>targetPlayerId|hostPlayerId|sessionId|hostDisplayName|avatarId</c>.
        /// Kept on this class (not extracted) because PartyInviteSystemTests
        /// reflects on it directly.
        /// </summary>
        private static (string targetId, PartyInviteData invite)? ParseInviteLine(string line)
        {
            if (string.IsNullOrEmpty(line)) return null;
            var parts = line.Split(INVITE_FIELD_SEPARATOR);
            if (parts.Length < 5) return null;
            if (!int.TryParse(parts[4], out int avatarId)) return null;

            return (parts[0], new PartyInviteData(parts[1], parts[2], parts[3], avatarId));
        }

        private void TryRaiseIncomingInvite(PartyInviteData invite)
        {
            // Already a client in this session — suppress re-fire.
            if (_partySession != null &&
                !connectionData.IsHost &&
                _partySession.Id == invite.PartySessionId)
            {
                _lastFiredInvite    = invite;
                _lastInviteResolved = true;
                return;
            }

            // PENDING → real id transition for an already-resolved invite:
            // silently update the cached record so WaitForRealSessionId can
            // observe the change without re-popping the popup.
            if (_lastInviteResolved &&
                _lastFiredInvite.HasValue &&
                _lastFiredInvite.Value.HostPlayerId == invite.HostPlayerId)
            {
                _lastFiredInvite = invite;
                return;
            }

            bool isDuplicate = _lastFiredInvite.HasValue &&
                _lastFiredInvite.Value.PartySessionId == invite.PartySessionId &&
                _lastFiredInvite.Value.HostPlayerId == invite.HostPlayerId;
            if (isDuplicate) return;

            DebugExtensions.LogColored(
                $"[INVITE-RECV] New invite from '{invite.HostDisplayName}' " +
                $"(sessionId: {invite.PartySessionId})",
                Color.green);
            _lastFiredInvite    = invite;
            _lastInviteResolved = false;
            connectionData.OnInviteReceived?.Raise(invite);

            _boostedRefreshUntil = Time.unscaledTime + BOOSTED_REFRESH_WINDOW_SECONDS;
        }

        private void RefreshOnlinePlayersDiff()
        {
            var freshPlayerIds = new HashSet<string>();

            foreach (var p in _presenceLobby.Players)
            {
                if (p.Id == connectionData.LocalPlayerId) continue;
                freshPlayerIds.Add(p.Id);

                var playerData = ReadOnlinePlayerData(p);

                int existingIdx = -1;
                for (int i = 0; i < connectionData.OnlinePlayers.Count; i++)
                {
                    if (connectionData.OnlinePlayers[i].PlayerId == p.Id) { existingIdx = i; break; }
                }

                if (existingIdx < 0)
                {
                    connectionData.OnlinePlayers.Add(playerData);
                }
                else
                {
                    var existing = connectionData.OnlinePlayers[existingIdx];
                    bool changed =
                        existing.DisplayName       != playerData.DisplayName       ||
                        existing.AvatarId          != playerData.AvatarId          ||
                        existing.PartyMemberCount  != playerData.PartyMemberCount  ||
                        existing.PartyMaxSlots     != playerData.PartyMaxSlots     ||
                        existing.MatchName         != playerData.MatchName;

                    if (changed)
                    {
                        // Obvious.Soap's ScriptableList<T> indexer-set silently mutates
                        // without raising any event; RemoveAt + Insert at the same
                        // index fires OnItemRemoved/OnItemAdded so subscribers update.
                        connectionData.OnlinePlayers.RemoveAt(existingIdx);
                        connectionData.OnlinePlayers.Insert(existingIdx, playerData);
                    }
                }
            }

            for (int i = connectionData.OnlinePlayers.Count - 1; i >= 0; i--)
            {
                if (!freshPlayerIds.Contains(connectionData.OnlinePlayers[i].PlayerId))
                    connectionData.OnlinePlayers.RemoveAt(i);
            }

            // Departed players with outstanding invites — free the slot now.
            if (_outgoingInvites.Count == 0) return;

            List<string> departed = null;
            foreach (var targetId in _outgoingInvites.Targets)
            {
                if (!freshPlayerIds.Contains(targetId))
                {
                    departed ??= new List<string>();
                    departed.Add(targetId);
                }
            }
            if (departed != null)
            {
                foreach (var id in departed)
                    _ = ClearOutgoingInviteIfPresentAsync(id, "presence-leave");
            }
        }

        private PartyPlayerData ReadOnlinePlayerData(IReadOnlyPlayer p)
        {
            string displayName = "Unknown Pilot";
            int    avatarId    = 0;
            int    partyCount  = 0;
            int    partyMax    = 0;
            string matchName   = string.Empty;

            if (p.Properties.TryGetValue(DISPLAY_NAME_KEY, out var dn) &&
                !string.IsNullOrEmpty(dn.Value))
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

            return new PartyPlayerData(p.Id, displayName, avatarId, partyCount, partyMax, matchName);
        }

        private void ScanPresenceForJoinedPartyMembers()
        {
            if (_presenceLobby == null || _partySession == null) return;
            if (connectionData.PartyMembers == null) return;

            var joinedPlayerIds = new List<string>();

            foreach (var p in _presenceLobby.Players)
            {
                if (p.Id == connectionData.LocalPlayerId) continue;
                if (!p.Properties.TryGetValue(JOINED_PARTY_KEY, out var joinedProp)) continue;
                if (string.IsNullOrEmpty(joinedProp.Value)) continue;
                if (joinedProp.Value != _partySession.Id) continue;

                var memberData = ReadPartyMemberData(p);
                if (!connectionData.PartyMembers.Contains(memberData))
                {
                    connectionData.PartyMembers.Add(memberData);
                    connectionData.OnPartyMemberJoined?.Raise(memberData);
                    DebugExtensions.LogColored(
                        $"[INVITE-SEND] Presence scan detected joined member '{memberData.DisplayName}' ({p.Id})",
                        Color.green);
                    joinedPlayerIds.Add(p.Id);
                }
            }

            // Already inside RefreshAsync (mutex held) → fire-and-forget.
            foreach (var joinedId in joinedPlayerIds)
                _ = ClearOutgoingInviteIfPresentAsync(joinedId, "presence-join");
        }

        private async Task RefreshPartyMembersAsync()
        {
            if (_partySession == null) return;
            if (connectionData.PartyMembers == null) return;

            // Grace period: a freshly-provisioned session can transiently fail
            // RefreshAsync. Nulling _partySession here would cause
            // ScanForAcceptanceSignalsAsync to recreate it on the next tick,
            // kicking any joining client.
            if (Time.unscaledTime - _partySessionCreatedAt < SESSION_CREATION_GRACE_PERIOD_SECONDS)
                return;

            try { await _partySession.RefreshAsync(); }
            catch (Exception e)
            {
                Debug.LogWarning($"[HostConnectionService] Party session refresh error ({e.GetType().Name}): {e.Message}");

                if (IsRateLimitException(e))
                {
                    _rateLimitBackoffUntil = Time.unscaledTime + refreshIntervalSeconds * 2;
                    return;
                }

                // Pending invites prevent nulling — would cascade into duplicate
                // session creation that kicks any already-joined client.
                if (_outgoingInvites.Count > 0)
                {
                    Debug.LogWarning(
                        $"[HostConnectionService] Refresh failed but {_outgoingInvites.Count} outgoing invite(s) pending — keeping _partySession to avoid duplicate creation.");
                    return;
                }

                _partySession = null;
                connectionData.PartyMembers?.Clear();
                return;
            }

            var sessionPlayerIds = new HashSet<string>();
            foreach (var p in _partySession.Players) sessionPlayerIds.Add(p.Id);

            var joinedPlayerIds = new List<string>();
            foreach (var p in _partySession.Players)
            {
                if (p.Id == connectionData.LocalPlayerId) continue;

                var memberData = ReadPartyMemberData(p);
                if (!connectionData.PartyMembers.Contains(memberData))
                {
                    connectionData.PartyMembers.Add(memberData);
                    connectionData.OnPartyMemberJoined?.Raise(memberData);
                    joinedPlayerIds.Add(p.Id);
                }
            }

            foreach (var joinedId in joinedPlayerIds)
                await ClearOutgoingInviteIfPresentAsync(joinedId, "party-join");

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

        private PartyPlayerData ReadPartyMemberData(IReadOnlyPlayer p)
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

        private void RepopulatePartyMembersFromSession()
        {
            if (_partySession == null || connectionData == null || connectionData.PartyMembers == null)
                return;

            connectionData.PartyMembers.Clear();
            connectionData.PartyMembers.Add(connectionData.LocalPlayerData);

            foreach (var p in _partySession.Players)
            {
                if (p.Id == connectionData.LocalPlayerId) continue;
                var memberData = ReadPartyMemberData(p);
                connectionData.PartyMembers.Add(memberData);
                connectionData.OnPartyMemberJoined?.Raise(memberData);
            }
        }

        // ╔═══════════════════════════════════════════════════════════════════╗
        // ║  PENDING-Sentinel Acceptance Protocol                             ║
        // ║                                                                   ║
        // ║  Three-phase flow:                                                ║
        // ║   1. Sender publishes invite payloads with PENDING session id.   ║
        // ║   2. Recipient writes ACCEPTED_INVITE_KEY = senderId.            ║
        // ║   3. Sender's refresh detects the signal, lazily creates the     ║
        // ║      Relay session, republishes payloads with the real id.       ║
        // ║   4. Recipient polls and joins on real id.                        ║
        // ╚═══════════════════════════════════════════════════════════════════╝

        private async Task ScanForAcceptanceSignalsAsync()
        {
            foreach (var p in _presenceLobby.Players)
            {
                if (p.Id == connectionData.LocalPlayerId) continue;
                if (!_outgoingInvites.Contains(p.Id)) continue;
                if (!p.Properties.TryGetValue(ACCEPTED_INVITE_KEY, out var prop)) continue;

                string acceptedValue = prop?.Value ?? string.Empty;
                if (acceptedValue != connectionData.LocalPlayerId)
                {
                    if (!string.IsNullOrEmpty(acceptedValue))
                        Debug.Log($"[ACCEPT-SCAN] Player {p.Id} has accepted_invite='{acceptedValue}' but we are '{connectionData.LocalPlayerId}' — skipping.");
                    continue;
                }

                Debug.Log($"[ACCEPT-SCAN] Acceptance signal from {p.Id} — creating party session...");

                if (_partySession == null)
                    await CreatePartySessionAsync();

                Debug.Log($"[ACCEPT-SCAN] Party session ready (id={_partySession?.Id ?? "null"}) — republishing payloads.");
                await RepublishOutgoingInvitesWithRealSessionIdAsync();
                return;
            }
        }

        private async Task RepublishOutgoingInvitesWithRealSessionIdAsync()
        {
            if (_partySession == null || _outgoingInvites.Count == 0) return;

            _outgoingInvites.RebuildPayloads(BuildInvitePayload);

            // CreatePartySessionAsync can take 2-3s (NM shutdown + Relay alloc),
            // so the SDK's internal player index will be stale. Refresh before
            // saving — same pattern as SendInviteAsync / PublishJoinedPartyAsync.
            try { await _presenceLobby.RefreshAsync(); }
            catch (Exception e)
            {
                Debug.LogWarning($"[HostConnectionService] RepublishInvites pre-save refresh failed (non-fatal): {e.Message}");
            }

            PublishInvitePayloadsToCurrentPlayer();
            await SaveWithRetryAsync();
            Debug.Log($"[HostConnectionService] Republished {_outgoingInvites.Count} pending invite(s) with real session id {_partySession.Id}");
        }

        private async Task<string> WaitForRealSessionIdAsync(string hostPlayerId, float timeoutSeconds = 7f)
        {
            float deadline = Time.unscaledTime + timeoutSeconds;
            int   pollCount = 0;
            while (Time.unscaledTime < deadline)
            {
                await Task.Delay(400);
                pollCount++;

                if (_presenceLobby == null)
                {
                    Debug.LogWarning("[HostConnectionService] WaitForRealSessionId: _presenceLobby is null — lobby may have been reset.");
                    continue;
                }

                bool hostFound = false;
                foreach (var p in _presenceLobby.Players)
                {
                    if (p.Id != hostPlayerId) continue;
                    hostFound = true;

                    if (!p.Properties.TryGetValue(INVITE_PAYLOADS_KEY, out var prop))
                    {
                        if (pollCount % 5 == 1) Debug.Log($"[HostConnectionService] WaitForRealSessionId poll#{pollCount}: host found but no {INVITE_PAYLOADS_KEY} property yet.");
                        break;
                    }
                    if (string.IsNullOrEmpty(prop?.Value))
                    {
                        if (pollCount % 5 == 1) Debug.Log($"[HostConnectionService] WaitForRealSessionId poll#{pollCount}: {INVITE_PAYLOADS_KEY} is empty.");
                        break;
                    }

                    bool foundForUs = false;
                    foreach (var line in prop.Value.Split(INVITE_LINE_SEPARATOR))
                    {
                        var parsed = ParseInviteLine(line);
                        if (!parsed.HasValue) continue;
                        if (parsed.Value.targetId != connectionData.LocalPlayerId) continue;
                        foundForUs = true;

                        var sid = parsed.Value.invite.PartySessionId;
                        if (!string.IsNullOrEmpty(sid) && sid != PENDING_SESSION_ID)
                        {
                            Debug.Log($"[HostConnectionService] WaitForRealSessionId resolved after {pollCount} polls: {sid}");
                            return sid;
                        }
                        if (pollCount % 5 == 1) Debug.Log($"[HostConnectionService] WaitForRealSessionId poll#{pollCount}: session id still PENDING.");
                    }
                    if (!foundForUs && pollCount % 5 == 1)
                        Debug.Log($"[HostConnectionService] WaitForRealSessionId poll#{pollCount}: payloads key present but no line targeting us in '{prop.Value}'.");
                    break;
                }
                if (!hostFound && pollCount % 5 == 1)
                    Debug.Log($"[HostConnectionService] WaitForRealSessionId poll#{pollCount}: host {hostPlayerId} not in _presenceLobby.Players ({_presenceLobby.Players.Count} players total).");
            }
            throw new TimeoutException(
                $"[HostConnectionService] Host {hostPlayerId} did not publish a real session id within {timeoutSeconds}s (after {pollCount} polls).");
        }

        // ╔═══════════════════════════════════════════════════════════════════╗
        // ║  Outgoing invite serialization & expiry                           ║
        // ╚═══════════════════════════════════════════════════════════════════╝

        /// <summary>
        /// Builds one invite line:
        /// <c>targetPlayerId|hostPlayerId|sessionId|hostDisplayName|avatarId</c>.
        /// Uses <see cref="PENDING_SESSION_ID"/> when no Relay session exists yet.
        /// </summary>
        private string BuildInvitePayload(string targetPlayerId)
        {
            string sessionIdField = _partySession?.Id ?? PENDING_SESSION_ID;
            return $"{targetPlayerId}{INVITE_FIELD_SEPARATOR}" +
                   $"{connectionData.LocalPlayerId}{INVITE_FIELD_SEPARATOR}" +
                   $"{sessionIdField}{INVITE_FIELD_SEPARATOR}" +
                   $"{connectionData.LocalDisplayName}{INVITE_FIELD_SEPARATOR}" +
                   $"{connectionData.LocalAvatarId}";
        }

        private void PublishInvitePayloadsToCurrentPlayer()
        {
            string composite = _outgoingInvites.SerializeAll(INVITE_LINE_SEPARATOR);
            _presenceLobby.CurrentPlayer.SetProperty(INVITE_PAYLOADS_KEY,
                new PlayerProperty(composite, VisibilityPropertyOptions.Public));
        }

        private void ExpireOutgoingInvites()
        {
            var expired = _outgoingInvites.CollectExpired(Time.unscaledTime);
            if (expired == null) return;
            foreach (var id in expired)
                _ = ClearOutgoingInviteIfPresentAsync(id, "timeout");
        }

        /// <summary>
        /// Clears an outgoing invite locally and re-publishes the composite
        /// property. Reentrant: callers from inside <see cref="RefreshAsync"/>
        /// (mutex already held) skip re-acquiring; external callers acquire normally.
        /// </summary>
        private async Task ClearOutgoingInviteIfPresentAsync(string playerId, string reason)
        {
            if (_presenceLobby == null) return;
            if (string.IsNullOrEmpty(playerId)) return;
            if (!_outgoingInvites.Contains(playerId)) return;

            // Local + UI updates fire immediately so the row reverts before the
            // network round-trip confirms.
            _outgoingInvites.Remove(playerId);
            DebugExtensions.LogColored(
                $"[INVITE-SEND] Clearing invite for '{playerId}' (reason: {reason})",
                Color.green);
            OutgoingInviteCleared?.Invoke(playerId);

            bool needsLock = !_insideRefreshCycle;
            if (needsLock) await _lobbyMutex.WaitAsync();
            try
            {
                PublishInvitePayloadsToCurrentPlayer();
                await SaveWithRetryAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[HostConnectionService] ClearOutgoingInvite error: {e.Message}");
            }
            finally
            {
                if (needsLock) _lobbyMutex.Release();
            }
        }

        // ╔═══════════════════════════════════════════════════════════════════╗
        // ║  Property publishing — DRY helper for the WaitMutex+Refresh+      ║
        // ║  SetProperty+SaveWithRetry pattern                                ║
        // ╚═══════════════════════════════════════════════════════════════════╝

        /// <summary>
        /// Acquires <see cref="_lobbyMutex"/>, refreshes the lobby's player
        /// cache, runs <paramref name="setProperty"/>, then saves with retry.
        /// All callers from outside the refresh cycle should use this — it
        /// replaces the five near-identical methods that previously duplicated
        /// the lock+refresh+set+save dance.
        /// </summary>
        private async Task RunLobbyPropertyWriteAsync(Action setProperty, string operationName)
        {
            if (_presenceLobby == null) return;

            await _lobbyMutex.WaitAsync();
            try
            {
                await _presenceLobby.RefreshAsync();
                setProperty();
                await SaveWithRetryAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[HostConnectionService] {operationName} error: {e.Message}");
            }
            finally
            {
                _lobbyMutex.Release();
            }
        }

        private async UniTaskVoid PublishJoinedPartyAsync(string partySessionId)
        {
            if (string.IsNullOrEmpty(partySessionId)) return;
            await RunLobbyPropertyWriteAsync(
                () => _presenceLobby.CurrentPlayer.SetProperty(JOINED_PARTY_KEY,
                    new PlayerProperty(partySessionId, VisibilityPropertyOptions.Public)),
                "PublishJoinedParty");
        }

        private async UniTaskVoid ClearJoinedPartyAsync()
        {
            await RunLobbyPropertyWriteAsync(
                () => _presenceLobby.CurrentPlayer.SetProperty(JOINED_PARTY_KEY,
                    new PlayerProperty(string.Empty, VisibilityPropertyOptions.Public)),
                "ClearJoinedParty");
        }

        private async Task PublishAcceptanceSignalAsync(string hostPlayerId)
        {
            if (string.IsNullOrEmpty(hostPlayerId)) return;
            await RunLobbyPropertyWriteAsync(
                () =>
                {
                    _presenceLobby.CurrentPlayer.SetProperty(ACCEPTED_INVITE_KEY,
                        new PlayerProperty(hostPlayerId, VisibilityPropertyOptions.Public));
                    Debug.Log($"[HostConnectionService] Published acceptance signal to host {hostPlayerId}");
                },
                "PublishAcceptanceSignal");
        }

        private async UniTaskVoid RepublishLocalIdentityAsync()
        {
            await RunLobbyPropertyWriteAsync(
                () =>
                {
                    _presenceLobby.CurrentPlayer.SetProperty(DISPLAY_NAME_KEY,
                        new PlayerProperty(connectionData.LocalDisplayName ?? "Pilot",
                            VisibilityPropertyOptions.Public));
                    _presenceLobby.CurrentPlayer.SetProperty(AVATAR_ID_KEY,
                        new PlayerProperty(connectionData.LocalAvatarId.ToString(),
                            VisibilityPropertyOptions.Public));
                },
                "RepublishLocalIdentity");
        }

        private async UniTaskVoid PublishPresenceImmediateAsync()
        {
            if (_presenceLobby == null) return;

            await _lobbyMutex.WaitAsync();
            try
            {
                await PublishPartyStateIfChangedAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[HostConnectionService] PublishPresenceImmediate error: {e.Message}");
            }
            finally
            {
                _lobbyMutex.Release();
            }
        }

        private async Task PublishPartyStateIfChangedAsync()
        {
            if (_presenceLobby == null) return;

            int    currentCount = connectionData.PartyMembers != null ? connectionData.PartyMembers.Count : 0;
            string currentMatch = ResolveCurrentMatchName();

            if (currentCount == _publishedPartyCount && currentMatch == _publishedMatchName) return;

            try
            {
                _presenceLobby.CurrentPlayer.SetProperty(PARTY_COUNT_KEY,
                    new PlayerProperty(currentCount.ToString(), VisibilityPropertyOptions.Public));
                _presenceLobby.CurrentPlayer.SetProperty(PARTY_MAX_KEY,
                    new PlayerProperty(connectionData.MaxPartySlots.ToString(), VisibilityPropertyOptions.Public));
                _presenceLobby.CurrentPlayer.SetProperty(MATCH_NAME_KEY,
                    new PlayerProperty(currentMatch ?? string.Empty, VisibilityPropertyOptions.Public));

                await SaveWithRetryAsync();
                _publishedPartyCount = currentCount;
                _publishedMatchName  = currentMatch;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[HostConnectionService] PublishPartyState error: {e.Message}");
            }
        }

        private string ResolveCurrentMatchName()
        {
            if (_gameData == null) return string.Empty;
            if (IsOnMenuScene()) return string.Empty;
            if (!_gameData.IsMultiplayerMode) return string.Empty;
            return _gameData.GameMode.ToString();
        }

        private async Task SaveWithRetryAsync()
        {
            const int maxRetries = 3;
            const int retryDelayMs = 2000;

            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                try
                {
                    await _presenceLobby.SaveCurrentPlayerDataAsync();

                    // Post-save refresh: keeps the SDK's cached state in sync with
                    // the server, reducing the window where WebSocket deltas
                    // reference stale player indices (root cause of harmless
                    // ArgumentOutOfRangeException in LobbyPatcher).
                    try { await _presenceLobby.RefreshAsync(); }
                    catch { /* polling corrects on next cycle */ }

                    return;
                }
                catch (Exception e) when (attempt < maxRetries &&
                    (e.Message.Contains("Too Many Requests") ||
                     e.Message.Contains("Index was out of range")))
                {
                    Debug.LogWarning($"[HostConnectionService] SaveCurrentPlayerData failed ({e.Message}) — retry {attempt + 1}/{maxRetries} in {retryDelayMs}ms");
                    await Task.Delay(retryDelayMs);
                    try { await _presenceLobby.RefreshAsync(); } catch { /* best-effort */ }
                }
            }
        }

        private Dictionary<string, PlayerProperty> BuildLocalPlayerProperties()
        {
            int partyCount = connectionData.PartyMembers != null ? connectionData.PartyMembers.Count : 0;
            int partyMax   = connectionData.MaxPartySlots;

            // 8 properties total — UGS lobbies cap player.data at 10. The
            // composite INVITE_PAYLOADS_KEY holds an unbounded number of
            // outstanding invites in a single property.
            return new Dictionary<string, PlayerProperty>
            {
                { DISPLAY_NAME_KEY,    new PlayerProperty(connectionData.LocalDisplayName ?? "Pilot", VisibilityPropertyOptions.Public) },
                { AVATAR_ID_KEY,       new PlayerProperty(connectionData.LocalAvatarId.ToString(),    VisibilityPropertyOptions.Public) },
                { PARTY_COUNT_KEY,     new PlayerProperty(partyCount.ToString(), VisibilityPropertyOptions.Public) },
                { PARTY_MAX_KEY,       new PlayerProperty(partyMax.ToString(),   VisibilityPropertyOptions.Public) },
                { MATCH_NAME_KEY,      new PlayerProperty(string.Empty,          VisibilityPropertyOptions.Public) },
                { JOINED_PARTY_KEY,    new PlayerProperty(string.Empty,          VisibilityPropertyOptions.Public) },
                { INVITE_PAYLOADS_KEY, new PlayerProperty(string.Empty,          VisibilityPropertyOptions.Public) },
                { ACCEPTED_INVITE_KEY, new PlayerProperty(string.Empty,          VisibilityPropertyOptions.Public) },
            };
        }

        // ╔═══════════════════════════════════════════════════════════════════╗
        // ║  Identity sync (cloud profile + auth fallback chain)              ║
        // ╚═══════════════════════════════════════════════════════════════════╝

        private async Task WaitForProfileInitAsync(int timeoutMs)
        {
            if (playerDataService == null || playerDataService.IsInitialized) return;

            int elapsed = 0;
            const int stepMs = 100;
            while (!playerDataService.IsInitialized && elapsed < timeoutMs)
            {
                await Task.Delay(stepMs);
                elapsed += stepMs;
            }

            if (!playerDataService.IsInitialized)
                Debug.LogWarning(
                    $"[HostConnectionService] PlayerDataService.IsInitialized still false after {timeoutMs}ms — " +
                    "proceeding with local default identity; profile-change republish will correct it.");
        }

        private void SyncLocalIdentity()
        {
            connectionData.LocalPlayerId = AuthData.PlayerId;

            if (playerDataService?.CurrentProfile != null)
            {
                connectionData.LocalDisplayName = playerDataService.CurrentProfile.displayName;
                connectionData.LocalAvatarId    = playerDataService.CurrentProfile.avatarId;
            }

            // Fallback chain so LocalDisplayName is NEVER empty when used to
            // construct invite payloads or presence properties.
            if (string.IsNullOrEmpty(connectionData.LocalDisplayName))
            {
                try
                {
                    var ugsName = Unity.Services.Authentication.AuthenticationService.Instance?.PlayerName;
                    if (!string.IsNullOrEmpty(ugsName))
                    {
                        int hashIndex = ugsName.LastIndexOf('#');
                        connectionData.LocalDisplayName = hashIndex > 0
                            ? ugsName.Substring(0, hashIndex)
                            : ugsName;
                    }
                }
                catch { /* Authentication may not be initialized */ }
            }

            if (string.IsNullOrEmpty(connectionData.LocalDisplayName))
                connectionData.LocalDisplayName = "Pilot";
        }

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
            RepublishLocalIdentityAsync().Forget();
        }

        private void SubscribeToGameLaunch()
        {
            if (_gameLaunchSubscribed || _gameData == null || _gameData.OnLaunchGame == null) return;
            _gameData.OnLaunchGame.OnRaised += HandleGameLaunch;
            _gameLaunchSubscribed = true;
        }

        private void UnsubscribeFromGameLaunch()
        {
            if (!_gameLaunchSubscribed || _gameData == null || _gameData.OnLaunchGame == null) return;
            _gameData.OnLaunchGame.OnRaised -= HandleGameLaunch;
            _gameLaunchSubscribed = false;
        }

        private void HandleGameLaunch() => PublishPresenceImmediateAsync().Forget();

        // ╔═══════════════════════════════════════════════════════════════════╗
        // ║  Helpers                                                          ║
        // ╚═══════════════════════════════════════════════════════════════════╝

        private bool IsAuthSignedInAndHasId()
        {
            if (AuthData == null) return false;

            bool signedIn =
                AuthData.IsSignedIn ||
                AuthData.State == AuthenticationData.AuthState.SignedIn;
            return signedIn && !string.IsNullOrEmpty(AuthData.PlayerId);
        }

        private static bool IsOnMenuScene()
        {
            var sceneName = SceneManager.GetActiveScene().name;
            return sceneName == "Menu_Main" || sceneName == "Authentication";
        }

        private static bool IsRateLimitException(Exception e) =>
            e.Message != null && e.Message.Contains("Too Many Requests");

        private static bool IsHostConflictException(Exception e) =>
            e.Message != null &&
            e.Message.Contains("Failed to start NetworkManager component as host");

        // ╔═══════════════════════════════════════════════════════════════════╗
        // ║  Lobby SDK log filter                                             ║
        // ╚═══════════════════════════════════════════════════════════════════╝

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
        /// Suppresses the known harmless UGS SDK <see cref="ArgumentOutOfRangeException"/>
        /// thrown by <c>LobbyPatcher.ApplyPatchesToLobby</c> when a WebSocket
        /// lobby-change delta references a stale player index. The SDK self-corrects
        /// on the next refresh; the log noise is pure pollution.
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
                if (logType is LogType.Error or LogType.Exception or LogType.Warning)
                {
                    if (ContainsLobbyPatcherIndexError(format)) return;
                    if (args != null)
                    {
                        for (int i = 0; i < args.Length; i++)
                            if (ContainsLobbyPatcherIndexError(args[i]?.ToString())) return;
                    }
                }
                _inner.LogFormat(logType, context, format, args);
            }

            private static bool ContainsLobbyPatcherIndexError(string s) =>
                !string.IsNullOrEmpty(s) && s.Contains("LobbyPatcher") && s.Contains("Index was out of range");
        }

        // ╔═══════════════════════════════════════════════════════════════════╗
        // ║  OutgoingInviteTracker — encapsulates the local outgoing-invite   ║
        // ║  map. Owns timeout expiry and composite-payload serialization.    ║
        // ║                                                                   ║
        // ║  Pulled out as a dedicated type so the rest of the service no    ║
        // ║  longer juggles a Dictionary<string, struct OutgoingInvite> with  ║
        // ║  the awkward copy-modify-assign mutation pattern.                 ║
        // ╚═══════════════════════════════════════════════════════════════════╝

        private sealed class OutgoingInviteTracker
        {
            private sealed class Entry
            {
                public string Payload;
                public float  ExpiresAt;
            }

            private readonly Dictionary<string, Entry> _entries = new();

            public int                          Count   => _entries.Count;
            public IReadOnlyCollection<string>  Targets => _entries.Keys;

            public bool Contains(string targetId) => _entries.ContainsKey(targetId);

            public void AddOrRefresh(string targetId, string payload, float expiresAt)
            {
                if (_entries.TryGetValue(targetId, out var existing))
                {
                    existing.Payload   = payload;
                    existing.ExpiresAt = expiresAt;
                }
                else
                {
                    _entries[targetId] = new Entry { Payload = payload, ExpiresAt = expiresAt };
                }
            }

            public void RefreshTimeout(string targetId, float expiresAt)
            {
                if (_entries.TryGetValue(targetId, out var existing))
                    existing.ExpiresAt = expiresAt;
            }

            public bool Remove(string targetId) => _entries.Remove(targetId);

            /// <summary>
            /// Rebuilds every entry's payload via <paramref name="payloadFactory"/>.
            /// Used when the Relay session id transitions PENDING → real and all
            /// outstanding invite lines must be re-serialized.
            /// </summary>
            public void RebuildPayloads(Func<string, string> payloadFactory)
            {
                foreach (var key in new List<string>(_entries.Keys))
                    _entries[key].Payload = payloadFactory(key);
            }

            public string SerializeAll(char separator)
            {
                if (_entries.Count == 0) return string.Empty;
                var lines = new List<string>(_entries.Count);
                foreach (var entry in _entries.Values)
                    lines.Add(entry.Payload);
                return string.Join(separator.ToString(), lines);
            }

            /// <summary>
            /// Returns a snapshot list of target ids whose timeout has elapsed,
            /// or null if none. Caller is responsible for removing them.
            /// </summary>
            public List<string> CollectExpired(float currentTime)
            {
                List<string> expired = null;
                foreach (var kv in _entries)
                {
                    if (currentTime >= kv.Value.ExpiresAt)
                    {
                        expired ??= new List<string>();
                        expired.Add(kv.Key);
                    }
                }
                return expired;
            }
        }
    }
}
