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
    /// Thin facade that coordinates the party lifecycle: presence lobby, party
    /// session, invite payloads, and member list.  Single source of truth for
    /// outgoing/incoming invites and party-member state; everything flows out via
    /// SOAP events through <see cref="HostConnectionDataSO"/>.
    ///
    /// Internal helpers extracted progressively (Phases 3-11):
    /// • <see cref="LobbyPropertyWriter"/>  – mutex + refresh + save-with-retry pattern (Phase 3)
    /// • <see cref="SoapPartyEventBus"/>    – all SOAP event Raise calls (Phase 4)
    /// • <see cref="InviteService"/>        – payload build/track/serialize/parse (Phase 5)
    /// • <see cref="LobbyPatcherLogFilter"/> – suppresses known harmless SDK noise
    /// • <see cref="PartyStateMachine"/>    – explicit lifecycle state (Phase 1)
    ///
    /// Lifetime: DontDestroyOnLoad MonoBehaviour (same GO as PartyInviteController).
    /// Thread-safety: main-thread only.
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
        // Both mutexes live in LobbyPropertyWriter (extracted in Phase 3).
        // Shortcuts for readability — these reference the same SemaphoreSlim
        // objects owned by the service:
        //   _propertyWriter.LobbyMutex           serialises lobby reads/writes
        //   _propertyWriter.SessionCreationMutex  deduplicates session creation
        //
        // _insideRefreshCycle makes the re-entrant case explicit: helpers called
        // from inside RefreshAsync (which holds LobbyMutex) skip re-acquiring;
        // helpers called from outside acquire normally.
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Owns both mutexes and the mutex+refresh+save-with-retry write pattern.
        /// Direct instantiation here; Phase 12 moves this to Reflex DI.
        /// </summary>
        private readonly LobbyPropertyWriter _propertyWriter = new LobbyPropertyWriter();

        /// <summary>
        /// Centralises all SOAP event raises for the party system.
        /// Initialised in Awake() once connectionData (SerializeField) is ready.
        /// Direct instantiation here; Phase 12 moves this to Reflex DI.
        /// </summary>
        private SoapPartyEventBus _eventBus;

        // Shortcut properties to keep call sites readable.
        private SemaphoreSlim _lobbyMutex           => _propertyWriter.LobbyMutex;
        private SemaphoreSlim _sessionCreationMutex => _propertyWriter.SessionCreationMutex;

        private bool _insideRefreshCycle;

        // ─────────────────────────────────────────────────────────────────────
        // Lifecycle / state
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Tracks which phase of the party lifecycle we are in.
        /// Single source of truth — replaces the scatter of boolean flags
        /// (_isHost, _joining, _inviteSent…) that previously drifted out of sync.
        /// Read via CurrentState; change via TryTransition; react via OnStateChanged.
        /// </summary>
        private readonly PartyStateMachine _stateMachine = new();

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

        /// <summary>
        /// Owns the in-memory map of pending outgoing invites and the serialised
        /// payload string.  Direct instantiation here; Phase 12 moves to Reflex DI.
        /// </summary>
        private readonly InviteService _inviteService = new InviteService();

        /// <summary>Raised when an outgoing invite is cleared (any reason).</summary>
        public event Action<string> OutgoingInviteCleared;
        public IReadOnlyCollection<string> OutgoingInviteTargets => _inviteService.OutgoingTargets;

        // ─────────────────────────────────────────────────────────────────────
        // Public read-only state
        // ─────────────────────────────────────────────────────────────────────

        public ISession PartySession => _partySession;

        /// <summary>
        /// Read-only view of the party state machine.
        /// Use <c>StateMachine.CurrentState</c> to check what phase we are in.
        /// Do NOT call TryTransition from outside HostConnectionService — only this
        /// class is the single writer of party state.
        /// </summary>
        public PartyStateMachine StateMachine => _stateMachine;

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
            // connectionData (SerializeField) is populated before Awake — safe to use here.
            _eventBus = new SoapPartyEventBus(connectionData);
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
            // Sign-out is the "emergency exit" — always allowed regardless of current state.
            _stateMachine.TryTransition(PartyState.Disconnected);
            connectionData.ResetRuntimeData();
            await LeavePresenceLobbyAsync();
            _eventBus.RaiseHostConnectionLost();
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
                // Now in the presence lobby — can browse players and send/receive invites.
                _stateMachine.TryTransition(PartyState.InPresenceLobby);
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
            if (_inviteService.Contains(targetPlayerId))
            {
                DebugExtensions.LogColored(
                    $"[INVITE-SEND] {targetPlayerId} already pending — refreshing timeout",
                    Color.yellow);
                _inviteService.RefreshTimeout(targetPlayerId,
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

                _inviteService.AddOrRefresh(
                    targetPlayerId,
                    _partySession?.Id ?? PENDING_SESSION_ID,
                    connectionData.LocalPlayerId,
                    connectionData.LocalDisplayName,
                    connectionData.LocalAvatarId,
                    Time.unscaledTime + OUTGOING_INVITE_TIMEOUT_SECONDS);
                inviteAdded = true;

                // First invite transitions us to Inviting. Subsequent invites to additional
                // players are no-ops for the state machine (already Inviting).
                if (_stateMachine.CurrentState == PartyState.InPresenceLobby)
                    _stateMachine.TryTransition(PartyState.Inviting);

                DebugExtensions.LogColored(
                    $"[INVITE-SEND] target='{targetPlayerId}', outgoing total={_inviteService.OutgoingCount}", Color.cyan);

                // Best-effort refresh to sync the SDK's player-index cache before
                // SaveCurrentPlayerDataAsync. Without it the save can fail silently.
                try { await _presenceLobby.RefreshAsync(); }
                catch { /* SaveWithRetryAsync handles stale state via its own retry */ }

                PublishInvitePayloadsToCurrentPlayer();
                await _propertyWriter.SaveWithRetryAsync(_presenceLobby);

                DebugExtensions.LogColored(
                    "[INVITE-SEND] SaveCurrentPlayerDataAsync completed — properties persisted",
                    Color.green);

                foreach (var player in connectionData.OnlinePlayers.ToList())
                {
                    if (player.PlayerId != targetPlayerId) continue;
                    _eventBus.RaiseInviteSent(player);
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
                    _inviteService.Remove(targetPlayerId);
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
            _eventBus.RaiseInviteResolved();
            try
            {
                SyncLocalIdentity();
                // Accepting moves us from browsing to actively connecting.
                _stateMachine.TryTransition(PartyState.JoiningParty);

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
                _eventBus.RaisePartyMemberJoined(hostData);

                _refreshTimer = -refreshIntervalSeconds;
                Debug.Log($"[HostConnectionService] Joined party {_partySession.Id}");
                // Relay session join succeeded — we are now fully inside the party.
                _stateMachine.TryTransition(PartyState.InParty);

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
            _eventBus.RaiseInviteResolved();
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
            _eventBus.RaiseInviteResolved();

            // Locally fire OnPartyMemberLeft so panels clear slots immediately
            // instead of waiting for the next refresh tick.
            if (connectionData.PartyMembers != null)
            {
                foreach (var member in connectionData.PartyMembers.ToList())
                {
                    if (member.PlayerId == connectionData.LocalPlayerId) continue;
                    _eventBus.RaisePartyMemberLeft(member);
                }
            }

            // Party leave returns us to browsing — back to the presence lobby.
            _stateMachine.TryTransition(PartyState.InPresenceLobby);
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

            // Session cleared (e.g. game→menu transition) — return to browsing state.
            // Guard: may already be InPresenceLobby if the session was cleared without
            // us having entered HostingParty/InParty (e.g. failed Relay creation).
            if (_stateMachine.CurrentState != PartyState.InPresenceLobby &&
                _stateMachine.CurrentState != PartyState.Disconnected)
            {
                _stateMachine.TryTransition(PartyState.InPresenceLobby);
            }

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

                _eventBus.RaiseHostConnectionEstablished();
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
                if (_inviteService.OutgoingCount > 0)
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
                        // Connection was lost — enter Reconnecting so callers and UI
                        // can show a "reconnecting…" indicator.
                        _stateMachine.TryTransition(PartyState.Reconnecting);
                    }
                }
            }
            finally
            {
                _insideRefreshCycle = false;
                _lobbyMutex.Release();
            }

            if (shouldReconnect)
            {
                await JoinPresenceLobbyAsync();
                // Reconnect succeeded (or fell back) — return to browsing state.
                if (_presenceLobby != null)
                    _stateMachine.TryTransition(PartyState.InPresenceLobby);
            }
        }

        private bool TryFindIncomingInvite(IReadOnlyPlayer sender, out PartyInviteData invite)
        {
            invite = default;
            if (!sender.Properties.TryGetValue(INVITE_PAYLOADS_KEY, out var payloadsProp))
                return false;
            if (payloadsProp == null || string.IsNullOrEmpty(payloadsProp.Value))
                return false;

            foreach (var line in payloadsProp.Value.Split(InviteService.LINE_SEPARATOR))
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
        // ParseInviteLine must remain private static on this class — tests reflect on it.
        // Delegates to InviteService.ParseLine so the format is defined in one place.
        private static (string targetId, PartyInviteData invite)? ParseInviteLine(string line)
            => InviteService.ParseLine(line);

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
            _eventBus.RaiseInviteReceived(invite);

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
            if (_inviteService.OutgoingCount == 0) return;

            List<string> departed = null;
            foreach (var targetId in _inviteService.OutgoingTargets)
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
                    _eventBus.RaisePartyMemberJoined(memberData);
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
                if (_inviteService.OutgoingCount > 0)
                {
                    Debug.LogWarning(
                        $"[HostConnectionService] Refresh failed but {_inviteService.OutgoingCount} outgoing invite(s) pending — keeping _partySession to avoid duplicate creation.");
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
                    _eventBus.RaisePartyMemberJoined(memberData);
                    joinedPlayerIds.Add(p.Id);
                }
            }

            foreach (var joinedId in joinedPlayerIds)
                await ClearOutgoingInviteIfPresentAsync(joinedId, "party-join");

            // A new party member appeared in the Relay session — the party is live.
            // Transition HostingParty → InParty (no-op if already InParty for a second joiner).
            if (joinedPlayerIds.Count > 0 && _stateMachine.CurrentState == PartyState.HostingParty)
                _stateMachine.TryTransition(PartyState.InParty);

            for (int i = connectionData.PartyMembers.Count - 1; i >= 0; i--)
            {
                var member = connectionData.PartyMembers[i];
                if (member.PlayerId == connectionData.LocalPlayerId) continue;

                if (!sessionPlayerIds.Contains(member.PlayerId))
                {
                    connectionData.PartyMembers.RemoveAt(i);
                    _eventBus.RaisePartyMemberLeft(member);
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
                _eventBus.RaisePartyMemberJoined(memberData);
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
                if (!_inviteService.Contains(p.Id)) continue;
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

                // Session is live — we are now the host, waiting for the recipient's NM to connect.
                // Transition: Inviting → HostingParty.  The final InParty transition happens
                // in RefreshPartyMembersAsync once both players show in the session.
                _stateMachine.TryTransition(PartyState.HostingParty);

                Debug.Log($"[ACCEPT-SCAN] Party session ready (id={_partySession?.Id ?? "null"}) — republishing payloads.");
                await RepublishOutgoingInvitesWithRealSessionIdAsync();
                return;
            }
        }

        private async Task RepublishOutgoingInvitesWithRealSessionIdAsync()
        {
            if (_partySession == null || _inviteService.OutgoingCount == 0) return;

            _inviteService.UpdatePayloadsWithRealSessionId(_partySession.Id);

            // CreatePartySessionAsync can take 2-3s (NM shutdown + Relay alloc),
            // so the SDK's internal player index will be stale. Refresh before
            // saving — same pattern as SendInviteAsync / PublishJoinedPartyAsync.
            try { await _presenceLobby.RefreshAsync(); }
            catch (Exception e)
            {
                Debug.LogWarning($"[HostConnectionService] RepublishInvites pre-save refresh failed (non-fatal): {e.Message}");
            }

            PublishInvitePayloadsToCurrentPlayer();
            await _propertyWriter.SaveWithRetryAsync(_presenceLobby);
            Debug.Log($"[HostConnectionService] Republished {_inviteService.OutgoingCount} pending invite(s) with real session id {_partySession.Id}");
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
                    foreach (var line in prop.Value.Split(InviteService.LINE_SEPARATOR))
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

        private void PublishInvitePayloadsToCurrentPlayer()
        {
            string composite = _inviteService.SerializeAll();
            _presenceLobby.CurrentPlayer.SetProperty(INVITE_PAYLOADS_KEY,
                new PlayerProperty(composite, VisibilityPropertyOptions.Public));
        }

        private void ExpireOutgoingInvites()
        {
            // InviteService.RemoveExpired removes entries from the tracker and returns
            // their IDs.  HandleInviteClearedAsync fires the UI event and saves the
            // updated (shorter) composite property to the lobby.
            var expired = _inviteService.RemoveExpired();
            foreach (var id in expired)
                HandleInviteClearedAsync(id, "timeout").Forget();
        }

        /// <summary>
        /// Clears an outgoing invite from the tracker, fires the UI-cleared event,
        /// and republishes the composite property to the lobby.
        /// Reentrant: callers from inside <see cref="RefreshAsync"/> (mutex already
        /// held) skip re-acquiring; external callers acquire normally.
        /// </summary>
        private async Task ClearOutgoingInviteIfPresentAsync(string playerId, string reason)
        {
            if (_presenceLobby == null || string.IsNullOrEmpty(playerId)) return;
            if (!_inviteService.Contains(playerId)) return;

            _inviteService.Remove(playerId);
            await HandleInviteClearedAsync(playerId, reason);
        }

        /// <summary>
        /// Fires the <see cref="OutgoingInviteCleared"/> event and saves the updated
        /// (post-removal) composite invite property to the lobby.
        ///
        /// Called by both the timeout path (<see cref="ExpireOutgoingInvites"/>, after
        /// <see cref="InviteService.RemoveExpired"/> already removed entries) and the
        /// presence-leave path (<see cref="ClearOutgoingInviteIfPresentAsync"/>, after
        /// <see cref="InviteService.Remove"/>).
        /// </summary>
        private async Task HandleInviteClearedAsync(string playerId, string reason)
        {
            DebugExtensions.LogColored(
                $"[INVITE-SEND] Clearing invite for '{playerId}' (reason: {reason})",
                Color.green);
            OutgoingInviteCleared?.Invoke(playerId);

            if (_presenceLobby == null) return;
            bool needsLock = !_insideRefreshCycle;
            if (needsLock) await _lobbyMutex.WaitAsync();
            try
            {
                PublishInvitePayloadsToCurrentPlayer();
                await _propertyWriter.SaveWithRetryAsync(_presenceLobby);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[HostConnectionService] HandleInviteCleared error: {e.Message}");
            }
            finally
            {
                if (needsLock) _lobbyMutex.Release();
            }
        }

        // ╔═══════════════════════════════════════════════════════════════════╗
        // ║  Property publishing                                              ║
        // ║  Delegates to _propertyWriter (LobbyPropertyWriter, Phase 3).    ║
        // ╚═══════════════════════════════════════════════════════════════════╝

        private async UniTaskVoid PublishJoinedPartyAsync(string partySessionId)
        {
            if (string.IsNullOrEmpty(partySessionId)) return;
            await _propertyWriter.WriteAsync(
                _presenceLobby,
                () => _presenceLobby.CurrentPlayer.SetProperty(JOINED_PARTY_KEY,
                    new PlayerProperty(partySessionId, VisibilityPropertyOptions.Public)),
                "PublishJoinedParty");
        }

        private async UniTaskVoid ClearJoinedPartyAsync()
        {
            await _propertyWriter.WriteAsync(
                _presenceLobby,
                () => _presenceLobby.CurrentPlayer.SetProperty(JOINED_PARTY_KEY,
                    new PlayerProperty(string.Empty, VisibilityPropertyOptions.Public)),
                "ClearJoinedParty");
        }

        private async Task PublishAcceptanceSignalAsync(string hostPlayerId)
        {
            if (string.IsNullOrEmpty(hostPlayerId)) return;
            await _propertyWriter.WriteAsync(
                _presenceLobby,
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
            await _propertyWriter.WriteAsync(
                _presenceLobby,
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

                await _propertyWriter.SaveWithRetryAsync(_presenceLobby);
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

    }
}
