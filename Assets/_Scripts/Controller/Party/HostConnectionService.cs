using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CosmicShore.UI;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Obvious.Soap;
using Reflex.Attributes;
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
    /// • <see cref="LobbyPropertyWriter"/>    – mutex + refresh + save-with-retry pattern (Phase 3)
    /// • <see cref="SoapPartyEventBus"/>      – all SOAP event Raise calls (Phase 4)
    /// • <see cref="InviteService"/>          – payload build/track/serialize/parse (Phase 5)
    /// • <see cref="LobbyRefreshScheduler"/>     – refresh timer + boost window (Phase 6)
    /// • <see cref="PresenceLobbyService"/>      – presence lobby join/leave/refresh (Phase 7)
    /// • <see cref="AcceptanceSignalService"/>   – PENDING-sentinel acceptance handshake (Phase 8)
    /// • <see cref="PartySessionService"/>       – Relay party session create/join/leave (Phase 9)
    /// • <see cref="PartyMemberService"/>        – PartyMembers SOAP list diff + events (Phase 10)
    /// • <see cref="NetworkTransitionService"/>  – NM shutdown for party session creation (Phase 11)
    /// • <see cref="PartyStateMachine"/>         – explicit lifecycle state (Phase 1)
    ///
    /// Lifetime: DontDestroyOnLoad MonoBehaviour (same GO as PartyInviteController).
    /// Thread-safety: main-thread only.
    /// </summary>
    public class HostConnectionService : MonoBehaviour, IPartyStateQuery
    {
        // ─────────────────────────────────────────────────────────────────────
        // Inspector
        // ─────────────────────────────────────────────────────────────────────

        [Header("Auth (Source of Truth)")]
        [SerializeField] private AuthenticationDataVariable authenticationDataVariable;
        private AuthenticationData AuthData => authenticationDataVariable.Value;

        [Header("SOAP Data Container")]
        [SerializeField] private HostConnectionDataSO connectionData;

        [Header("Boot Status SOAP")]
        [Tooltip("Raised by BootStatusPanel when the user taps the retry button after the auto-retry loop exhausts. Triggers RetryCreateOwnPartySessionAsync.")]
        [SerializeField] private ScriptableEventNoParam bootStatusRetryRequestedEvent;

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
        private const int   MAX_REFRESH_ERRORS_BEFORE_RECONNECT = 3;
        private const float FORCE_REFRESH_COOLDOWN_SECONDS   = 0.5f;
        private const int   PROFILE_INIT_TIMEOUT_MS          = 5000;

        /// <summary>
        /// After session creation, suppress <see cref="RefreshPartyMembersAsync"/>
        /// for this many seconds.  A freshly-provisioned session can transiently
        /// fail RefreshAsync; nulling the session in response would cause
        /// <see cref="AcceptanceSignalService.ScanForSignals"/> to recreate it on
        /// the next tick, kicking any joining client.
        /// </summary>
        private const float SESSION_CREATION_GRACE_PERIOD_SECONDS = 4f;

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

        /// <summary>Owns both mutexes and the mutex+refresh+save-with-retry write pattern.</summary>
        [Inject] private LobbyPropertyWriter _propertyWriter;

        /// <summary>Centralises all SOAP event raises for the party system.</summary>
        [Inject] private SoapPartyEventBus _eventBus;

        /// <summary>
        /// Owns the elapsed-time accumulator and boosted-refresh window for the
        /// presence-lobby poll cycle.
        /// </summary>
        [Inject] private LobbyRefreshScheduler _scheduler;

        /// <summary>Manages the UGS lobby-only presence session: join/create/leave/refresh.</summary>
        [Inject] private IPresenceLobbyService _lobbyService;

        /// <summary>
        /// Orchestrates the PENDING-sentinel three-phase acceptance handshake:
        /// scan for signals, publish acceptance, wait for real id, republish.
        /// </summary>
        [Inject] private AcceptanceSignalService _acceptanceService;

        /// <summary>Manages the UGS Relay-backed party session lifecycle.</summary>
        [Inject] private IPartySessionService _partySessionService;

        /// <summary>
        /// Owns the PartyMembers SOAP list: diffs against live session, seeds,
        /// repopulates on scene reload, and fires member-change SOAP events.
        /// </summary>
        [Inject] private IPartyMemberService _memberService;

        /// <summary>Manages NetworkManager lifecycle during party session creation.</summary>
        [Inject] private INetworkTransitionService _networkTransition;

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
        private bool   _profileSubscribed;
        private bool   _gameLaunchSubscribed;
        private float  _rateLimitBackoffUntil;
        private float  _nextForcedRefreshAllowed;
        private int    _consecutiveRefreshErrors;
        private int    _publishedPartyCount = -1;
        private string _publishedMatchName  = "<UNSET>";

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
        /// payload string.
        /// </summary>
        [Inject] private InviteService _inviteService;

        /// <summary>Raised when an outgoing invite is cleared (any reason).</summary>
        public event Action<string> OutgoingInviteCleared;
        public IReadOnlyCollection<string> OutgoingInviteTargets => _inviteService.OutgoingTargets;

        // ─────────────────────────────────────────────────────────────────────
        // Public read-only state
        // ─────────────────────────────────────────────────────────────────────

        public ISession PartySession => _partySessionService?.ActiveSession;

        /// <summary>
        /// Read-only view of the party state machine.
        /// Use <c>StateMachine.CurrentState</c> to check what phase we are in.
        /// Do NOT call TryTransition from outside HostConnectionService — only this
        /// class is the single writer of party state.
        /// </summary>
        public PartyStateMachine StateMachine => _stateMachine;

        // ─────────────────────────────────────────────────────────────────────
        // IPartyStateQuery (read-only view used by FriendsInitializer and UI)
        // ─────────────────────────────────────────────────────────────────────

        PartyState IPartyStateQuery.CurrentState         => _stateMachine.CurrentState;
        string     IPartyStateQuery.ActivePartySessionId => _partySessionService?.ActiveSession?.Id ?? "";
        int        IPartyStateQuery.PartyMemberCount     => connectionData?.PartyMembers?.Count ?? 0;

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
            // [Inject] fields are populated by Reflex between Awake and Start.
            // Do not access service fields here — use Start() instead.
            SceneManager.sceneLoaded += OnSceneLoaded;

            if (bootStatusRetryRequestedEvent != null)
                bootStatusRetryRequestedEvent.OnRaised += HandleBootStatusRetryRequested;
        }

        async void Start()
        {
            // All [Inject] fields (services + gameData) are populated before Start.
            while (!IsAuthSignedInAndHasId())
                await UniTask.Delay(300);

            await EnsureInitializedAsync();
        }

        void Update()
        {
            if (!_initialized || _lobbyService.ActiveLobby == null) return;
            if (_lobbyMutex.CurrentCount == 0) return;                   // someone is already inside the mutex
            if (Time.unscaledTime < _rateLimitBackoffUntil) return;
            if (!IsOnMenuScene()) return;

            ExpireOutgoingInvites();

            if (_scheduler.ShouldFireNow(Time.unscaledDeltaTime))
                RefreshAsync().Forget();
        }

        async void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            UnsubscribeFromProfileChanges();
            UnsubscribeFromGameLaunch();

            if (bootStatusRetryRequestedEvent != null)
                bootStatusRetryRequestedEvent.OnRaised -= HandleBootStatusRetryRequested;

            await _lobbyService.LeaveAsync();

            _lobbyMutex.Dispose();
            _sessionCreationMutex.Dispose();

            if (Instance == this)
                Instance = null;
        }

        private void HandleBootStatusRetryRequested() => RetryCreateOwnPartySessionAsync().Forget();

        /// <summary>
        /// Resets per-session invite state on Menu_Main reload and rebuilds
        /// <see cref="HostConnectionDataSO.PartyMembers"/> from the active party session.
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
            await _lobbyService.LeaveAsync();
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
        /// invite acceptance via <see cref="AcceptanceSignalService.ScanForSignals"/>.
        /// </summary>
        private async UniTask EnsureInitializedAsync()
        {
            if (_initialized || _joining) return;
            _joining = true;
            try
            {
                SubscribeToProfileChanges();
                SubscribeToGameLaunch();

                await WaitForProfileInitAsync(PROFILE_INIT_TIMEOUT_MS);
                SyncLocalIdentity();

                await _lobbyService.JoinOrCreateAsync(presenceLobbyMaxPlayers);

                // Apply post-join state now that the lobby reference is live.
                ApplyPostLobbyJoinState();

                // Catch the case where the cloud profile resolved during
                // JoinOrCreateAsync — HandleProfileChanged's republish
                // would have been a no-op (lobby was still null at that moment).
                SyncLocalIdentity();
                RepublishLocalIdentityAsync().Forget();

                _initialized = true;
                // Presence lobby joined — transient state, immediately creates solo Relay session.
                _stateMachine.TryTransition(PartyState.InPresenceLobby);
                DebugExtensions.LogColored(
                    $"[HostConnectionService] Presence lobby joined — lobby: {_lobbyService.ActiveLobby?.Id ?? "NULL"}, " +
                    $"localId: {connectionData.LocalPlayerId}",
                    Color.green);

                // Every player always hosts their own solo Relay party session from menu entry.
                // Creates Relay session and starts NM — vessel spawns when NM is up.
                await CreateOwnPartySessionAsync();
            }
            finally { _joining = false; }
        }

        // ╔═══════════════════════════════════════════════════════════════════╗
        // ║  Public Invite API                                                ║
        // ╚═══════════════════════════════════════════════════════════════════╝

        public async UniTask SendInviteAsync(string targetPlayerId)
        {
            DebugExtensions.LogColored(
                $"[INVITE-SEND] SendInviteAsync called — target: {targetPlayerId}", Color.cyan);

            if (_lobbyService.ActiveLobby == null)
            {
                DebugExtensions.LogErrorColored(
                    "[INVITE-SEND] ABORT — presence lobby is null", Color.red);
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

            // Ensure our own Relay session is live before writing the invite.
            // CreateOwnPartySessionAsync is mutex-guarded and idempotent — if it is
            // already running from the startup path this call simply waits for it
            // to finish; if it already succeeded the double-check guard returns early.
            if (_partySessionService.ActiveSession == null)
            {
                DebugExtensions.LogColored(
                    "[INVITE-SEND] Relay session not yet ready — awaiting CreateOwnPartySessionAsync...",
                    Color.yellow);
                await CreateOwnPartySessionAsync();
            }

            await _lobbyMutex.WaitAsync();
            bool inviteAdded = false;
            try
            {
                SyncLocalIdentity();
                DebugExtensions.LogColored(
                    $"[INVITE-SEND] LocalPlayerId: {connectionData.LocalPlayerId}, " +
                    $"DisplayName: {connectionData.LocalDisplayName}", Color.cyan);

                // The Relay session was created at startup (or just above).
                // Use the real session ID directly — no PENDING placeholder.
                if (_partySessionService.ActiveSession?.Id is not { Length: > 0 } sessionId)
                {
                    Debug.LogError("[INVITE-SEND] ABORT — party session creation failed; cannot send invite.");
                    return;
                }

                DebugExtensions.LogColored(
                    $"[INVITE-SEND] PartySession ID: {sessionId}", Color.cyan);

                _inviteService.AddOrRefresh(
                    targetPlayerId,
                    sessionId,
                    connectionData.LocalPlayerId,
                    connectionData.LocalDisplayName,
                    connectionData.LocalAvatarId,
                    Time.unscaledTime + OUTGOING_INVITE_TIMEOUT_SECONDS);
                inviteAdded = true;

                // First invite transitions us from InParty to Inviting.
                // Subsequent invites to additional players are no-ops (already Inviting).
                if (_stateMachine.CurrentState == PartyState.InParty)
                    _stateMachine.TryTransition(PartyState.Inviting);

                DebugExtensions.LogColored(
                    $"[INVITE-SEND] target='{targetPlayerId}', outgoing total={_inviteService.OutgoingCount}", Color.cyan);

                // Best-effort refresh to sync the SDK's player-index cache before
                // SaveCurrentPlayerDataAsync. Without it the save can fail silently.
                try { await _lobbyService.RefreshAsync(); }
                catch { /* SaveWithRetryAsync handles stale state via its own retry */ }

                PublishInvitePayloadsToCurrentPlayer();
                await _propertyWriter.SaveWithRetryAsync(_lobbyService.ActiveLobby);

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
                _scheduler.Reset();
                _scheduler.Boost();
                _lobbyMutex.Release();
            }
        }

        public async UniTask AcceptInviteAsync(PartyInviteData invite)
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
                await _acceptanceService.PublishSignalAsync(
                    _lobbyService.ActiveLobby, invite.HostPlayerId, _propertyWriter);

                string realSessionId = invite.PartySessionId;
                if (string.IsNullOrEmpty(realSessionId))
                {
                    Debug.LogError("[HostConnectionService] AcceptInvite ABORT — invite has no session ID. The host may not have a Relay session.");
                    await CreateOwnPartySessionAsync(); // JoiningParty → HostingParty → InParty
                    return;
                }

                await _partySessionService.JoinByIdAsync(realSessionId);

                connectionData.IsPartyHost = false;

                _memberService.SeedLocalPlayer(clearFirst: true);
                var hostData = new PartyPlayerData(invite.HostPlayerId, invite.HostDisplayName, invite.HostAvatarId);
                connectionData.PartyMembers?.Add(hostData);
                _eventBus.RaisePartyMemberJoined(hostData);

                // Give the freshly-joined session a settling period before the
                // first member-sync refresh fires — avoids stale-session 404s.
                _scheduler.ResetDeferred(refreshIntervalSeconds);
                Debug.Log($"[HostConnectionService] Joined party {_partySessionService.ActiveSession?.Id}");
                // Relay session join succeeded — we are now fully inside the party.
                _stateMachine.TryTransition(PartyState.InParty);

                _scheduler.Boost();

                // Advertise this join so the host's RefreshAsync picks us up
                // before their party-session Players list catches up.
                PublishJoinedPartyAsync(realSessionId).Forget();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[HostConnectionService] AcceptInvite error: {e.Message}");
            }
        }

        public UniTask DeclineInviteAsync()
        {
            // Sender's slot is freed by their own timeout — UGS doesn't expose
            // a decline signal back to the sender.
            _lastFiredInvite     = null;
            _lastInviteResolved  = true;
            _eventBus.RaiseInviteResolved();
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// Client-side leave. Routes through <see cref="PartyInviteController"/>
        /// for the Netcode shutdown + fresh local-host restart.
        /// </summary>
        public async UniTask LeavePartyAsync()
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
            _memberService.ClearWithEvents(connectionData.LocalPlayerId);

            ClearJoinedPartyAsync().Forget();
            await controller.LeavePartyAndReturnToMenuAsync();
            // After leaving the party, recreate our own solo Relay party session.
            await CreateOwnPartySessionAsync();
        }

        public async UniTask KickPartyMemberAsync(string playerId)
        {
            if (!connectionData.IsPartyHost)
            {
                Debug.LogWarning("[HostConnectionService] Only the party host can kick party members.");
                return;
            }
            if (playerId == connectionData.LocalPlayerId)
            {
                Debug.LogWarning("[HostConnectionService] Cannot kick yourself from the party.");
                return;
            }

            connectionData.RemovePartyMember(playerId);

            if (_partySessionService.ActiveSession != null)
            {
                try
                {
                    await _partySessionService.ActiveSession.AsHost().RemovePlayerAsync(playerId).AsMainThread();
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
            if (!_initialized || _lobbyService.ActiveLobby == null) return;

            _scheduler.Boost();

            if (Time.unscaledTime < _nextForcedRefreshAllowed) return;
            _nextForcedRefreshAllowed = Time.unscaledTime + FORCE_REFRESH_COOLDOWN_SECONDS;

            _scheduler.Reset();
            if (_lobbyMutex.CurrentCount == 0) return; // already running
            RefreshAsync().Forget();
        }

        /// <summary>
        /// Creates the local player's solo Relay-backed party session and starts
        /// NetworkManager as a Relay host.  Called automatically after presence lobby
        /// join and after any event that requires a fresh solo session (leave, reconnect,
        /// failed join).
        ///
        /// Transitions: current → HostingParty (transient, session creating) → InParty
        /// (session live, NM listening, vessel will spawn).
        ///
        /// Thread-safety: serialised by <see cref="_sessionCreationMutex"/>; concurrent
        /// callers wait and then bail out if the first caller already succeeded.
        /// </summary>
        private async UniTask CreateOwnPartySessionAsync()
        {
            await _sessionCreationMutex.WaitAsync();
            try
            {
                // Double-check: a concurrent call may have already reached InParty.
                if (_stateMachine.CurrentState == PartyState.InParty &&
                    _partySessionService.ActiveSession != null)
                    return;

                if (_stateMachine.CurrentState != PartyState.HostingParty)
                    _stateMachine.TryTransition(PartyState.HostingParty);

                using var shutdownCts = new System.Threading.CancellationTokenSource();
                // .AsMainThread() guarantees the continuation (and the SOAP raise
                // further down) runs on Unity's main thread.
                await _networkTransition.ShutdownAsync(timeoutSeconds: 5f, shutdownCts.Token).AsMainThread();

                await _partySessionService.CreateAsync(connectionData.MaxPartySlots).AsMainThread();

                connectionData.IsPartyHost = true;
                _memberService.SeedLocalPlayer(clearFirst: true);

                // Give the new session breathing room before RefreshAsync touches it.
                _scheduler.ResetDeferred(refreshIntervalSeconds);

                // HostingParty → InParty: session is live, NM is listening.
                // Meaning shifts: InParty now means "I have a live Relay session"
                // (solo or multi), not "at least one remote member has connected".
                _stateMachine.TryTransition(PartyState.InParty);
                _eventBus.RaiseHostConnectionEstablished();
                RefreshAsync().Forget();

                DebugExtensions.LogColored(
                    $"[HostConnectionService] Solo party session ready: {_partySessionService.ActiveSession?.Id} — InParty, vessel will spawn.",
                    Color.green);
            }
            finally
            {
                _sessionCreationMutex.Release();
            }
        }

        /// <summary>
        /// Public re-entry point for <see cref="Core.AuthenticationSceneController"/> to
        /// retry Relay session creation without breaking the single-owner rule.
        /// Clears any stale session reference before retrying.
        /// </summary>
        public async UniTask RetryCreateOwnPartySessionAsync(CancellationToken ct = default)
        {
            _partySessionService.ClearSession();
            await CreateOwnPartySessionAsync();
        }

        // ╔═══════════════════════════════════════════════════════════════════╗
        // ║  Refresh Loop                                                     ║
        // ╚═══════════════════════════════════════════════════════════════════╝

        private async UniTaskVoid RefreshAsync()
        {
            if (_lobbyService.ActiveLobby == null) return;

            // Quick non-blocking check — if someone else holds the mutex, skip
            // this tick rather than queuing up. The next tick will pick up.
            if (!await _lobbyMutex.WaitAsync(0))
                return;

            _insideRefreshCycle = true;
            bool shouldReconnect = false;
            try
            {
                await _lobbyService.RefreshAsync();

                // Diff-based update — never Clear() + re-Add() (would flicker UI).
                if (connectionData.OnlinePlayers != null)
                    RefreshOnlinePlayersDiff();

                // Scan composite invite_payloads for lines targeting us.
                foreach (var p in _lobbyService.ActiveLobby.Players)
                {
                    if (p.Id == connectionData.LocalPlayerId) continue;
                    if (TryFindIncomingInvite(p, out var invite))
                        TryRaiseIncomingInvite(invite);
                }

                // Acceptance-signal scan. Must run BEFORE the JOINED_PARTY_KEY scan
                // because recipients won't set joined_party until after they read the
                // real session id. Gated on outgoing-invite count — no work to do if
                // we haven't sent any invites.
                if (_inviteService.OutgoingCount > 0)
                {
                    string acceptingId = _acceptanceService.ScanForSignals(
                        _lobbyService.ActiveLobby,
                        connectionData.LocalPlayerId,
                        _inviteService.OutgoingTargets);

                    if (acceptingId != null)
                    {
                        // In the "Always InParty" model, the Relay session already exists
                        // before the invite was sent — no session creation needed here.
                        string activeSessionId = _partySessionService.ActiveSession?.Id;
                        if (string.IsNullOrEmpty(activeSessionId))
                        {
                            Debug.LogError($"[HostConnectionService] Acceptance signal from {acceptingId} but no active party session — joiner cannot connect.");
                        }
                        else
                        {
                            Debug.Log($"[HostConnectionService] Acceptance signal from {acceptingId} — joiner will connect to existing session {activeSessionId}.");
                            await _acceptanceService.RepublishWithRealIdAsync(
                                _lobbyService, activeSessionId, _inviteService, _propertyWriter);
                        }
                    }
                }

                // ── Presence-lobby party-join scan (host only) ──────────────
                // Clients advertise their party join via JOINED_PARTY_KEY so we
                // can detect them even when the party-session Players list is
                // still stale. This is the authoritative fast path for the
                // sender's arcade lobby list.
                if (_partySessionService.ActiveSession != null && connectionData.IsPartyHost)
                    ScanPresenceForJoinedPartyMembers();

                if (_partySessionService.ActiveSession != null)
                    await RefreshPartyMembersAsync();

                await PublishPartyStateIfChangedAsync();

                _consecutiveRefreshErrors = 0;
            }
            catch (Exception e)
            {
                // UGS SDK self-corrects on the next refresh tick. Treat as a
                // no-op so the consecutive-error counter doesn't roll into
                // the reconnect path on harmless SDK noise.
                if (IsBenignLobbyPatcherError(e))
                {
                    // intentional: no log, no counter increment, no state change
                }
                else if (IsRateLimitException(e))
                {
                    _rateLimitBackoffUntil = Time.unscaledTime + refreshIntervalSeconds * 2;
                    Debug.LogWarning("[HostConnectionService] Rate limited during refresh — backing off");
                }
                else
                {
                    Debug.LogWarning($"[HostConnectionService] Refresh error ({e.GetType().Name}): {e}");
                    _consecutiveRefreshErrors++;
                    if (_consecutiveRefreshErrors >= MAX_REFRESH_ERRORS_BEFORE_RECONNECT)
                    {
                        Debug.LogWarning($"[HostConnectionService] {_consecutiveRefreshErrors} consecutive refresh errors — reconnecting to presence lobby");
                        _consecutiveRefreshErrors = 0;
                        // Clear the internal session reference so JoinOrCreateAsync will proceed.
                        _lobbyService.ForceReset();
                        shouldReconnect = true;
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
                // Surface the failure so any subscribed UI (boot status panel,
                // in-menu reconnect banner) can show "Connection lost".  We do
                // NOT call CreateOwnPartySessionAsync from this background
                // loop — that path would shut down NetworkManager and respawn
                // every menu vessel.  Relay re-creation is driven by an
                // explicit user action (retry button) via
                // RetryCreateOwnPartySessionAsync, which keeps the user-visible
                // recovery in one place.
                _eventBus.RaiseHostConnectionLost();

                await _lobbyService.JoinOrCreateAsync(presenceLobbyMaxPlayers);
                ApplyPostLobbyJoinState();
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
            if (_partySessionService.ActiveSession != null &&
                !connectionData.IsPartyHost &&
                _partySessionService.ActiveSession.Id == invite.PartySessionId)
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

            _scheduler.Boost();
        }

        private void RefreshOnlinePlayersDiff()
        {
            var freshPlayerIds = new HashSet<string>();

            foreach (var p in _lobbyService.ActiveLobby.Players)
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
            if (_lobbyService.ActiveLobby == null || _partySessionService.ActiveSession == null) return;
            if (connectionData.PartyMembers == null) return;

            var joinedPlayerIds = new List<string>();
            var sessionId       = _partySessionService.ActiveSession.Id;

            foreach (var p in _lobbyService.ActiveLobby.Players)
            {
                if (p.Id == connectionData.LocalPlayerId) continue;
                if (!p.Properties.TryGetValue(JOINED_PARTY_KEY, out var joinedProp)) continue;
                if (string.IsNullOrEmpty(joinedProp.Value)) continue;
                if (joinedProp.Value != sessionId) continue;

                var memberData = _memberService.ReadMemberData(p);
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

        private async UniTask RefreshPartyMembersAsync()
        {
            if (_partySessionService.ActiveSession == null) return;
            if (connectionData.PartyMembers == null) return;

            // Grace period: a freshly-provisioned session can transiently fail
            // RefreshAsync.  Clearing the session here would cause
            // AcceptanceSignalService.ScanForSignals to recreate it on the next tick,
            // kicking any joining client.
            if (Time.unscaledTime - _partySessionService.CreatedAtUnscaledTime < SESSION_CREATION_GRACE_PERIOD_SECONDS)
                return;

            try { await _partySessionService.RefreshAsync(); }
            catch (Exception e)
            {
                // Error-handling matrix — see Docs/PARTY_SYSTEM_REFACTOR.md.
                //
                // [benign] LobbyPatcher stale-index ArgumentOutOfRangeException —
                // known harmless SDK noise, self-corrects on the next tick.
                if (IsBenignLobbyPatcherError(e))
                    return;

                // [rate-limit] UGS throttled us — back off, keep ActiveSession.
                if (IsRateLimitException(e))
                {
                    Debug.LogWarning($"[HostConnectionService] Party session refresh rate-limited — backing off");
                    _rateLimitBackoffUntil = Time.unscaledTime + refreshIntervalSeconds * 2;
                    return;
                }

                // [transient] Everything else: log and retry next tick WITHOUT
                // clearing the session. Clearing here cascades into host-vessel
                // despawn via SendInviteAsync → CreateOwnPartySessionAsync →
                // NetworkTransitionService.ShutdownAsync → NetworkManager.Shutdown.
                // Session lifetime is owned by explicit user paths (LeavePartyAsync,
                // kick, NM shutdown, RetryCreateOwnPartySessionAsync) — not by
                // background refresh ticks.
                //
                // TODO (Commits 11/12): split this branch into [transient] vs
                // [definite session-gone] (HTTP 404 / SessionNotFound) and auto-
                // recover via LeavePartyKeepHostAsync so the UI never shows stale
                // "in party" when the server has dropped the session.
                Debug.LogWarning(
                    $"[HostConnectionService] Party session refresh error ({e.GetType().Name}): {e.Message} — keeping session, will retry next tick");
                return;
            }

            var joinedPlayerIds = _memberService.SyncFromSession(
                _partySessionService.ActiveSession, connectionData.LocalPlayerId);

            foreach (var joinedId in joinedPlayerIds)
                await ClearOutgoingInviteIfPresentAsync(joinedId, "party-join");

            // A new party member appeared in the Relay session.
            // If we're Inviting (sent an invite and they connected), transition to InParty.
            if (joinedPlayerIds.Count > 0 && _stateMachine.CurrentState == PartyState.Inviting)
                _stateMachine.TryTransition(PartyState.InParty);
        }

        private void RepopulatePartyMembersFromSession()
        {
            if (_partySessionService.ActiveSession == null) return;
            _memberService.RepopulateFromSession(
                _partySessionService.ActiveSession, connectionData.LocalPlayerId);
        }

        // ╔═══════════════════════════════════════════════════════════════════╗
        // ║  Outgoing invite serialization & expiry                           ║
        // ╚═══════════════════════════════════════════════════════════════════╝

        private void PublishInvitePayloadsToCurrentPlayer()
        {
            string composite = _inviteService.SerializeAll();
            _lobbyService.ActiveLobby.CurrentPlayer.SetProperty(INVITE_PAYLOADS_KEY,
                new PlayerProperty(composite, VisibilityPropertyOptions.Public));
        }

        private void ExpireOutgoingInvites()
        {
            // InviteService.RemoveExpired removes entries from the tracker and returns
            // their IDs.  HandleInviteClearedAsync fires the UI event and saves the
            // updated (shorter) composite property to the lobby.
            var expired = _inviteService.RemoveExpired();
            foreach (var id in expired)
                _ = HandleInviteClearedAsync(id, "timeout");
        }

        /// <summary>
        /// Clears an outgoing invite from the tracker, fires the UI-cleared event,
        /// and republishes the composite property to the lobby.
        /// Reentrant: callers from inside <see cref="RefreshAsync"/> (mutex already
        /// held) skip re-acquiring; external callers acquire normally.
        /// </summary>
        private async UniTask ClearOutgoingInviteIfPresentAsync(string playerId, string reason)
        {
            if (_lobbyService.ActiveLobby == null || string.IsNullOrEmpty(playerId)) return;
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
        private async UniTask HandleInviteClearedAsync(string playerId, string reason)
        {
            DebugExtensions.LogColored(
                $"[INVITE-SEND] Clearing invite for '{playerId}' (reason: {reason})",
                Color.green);
            OutgoingInviteCleared?.Invoke(playerId);

            if (_lobbyService.ActiveLobby == null) return;
            bool needsLock = !_insideRefreshCycle;
            if (needsLock) await _lobbyMutex.WaitAsync();
            try
            {
                PublishInvitePayloadsToCurrentPlayer();
                await _propertyWriter.SaveWithRetryAsync(_lobbyService.ActiveLobby);
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
        // ║  Post-join state application                                      ║
        // ╚═══════════════════════════════════════════════════════════════════╝

        /// <summary>
        /// Applies lobby join side-effects to <see cref="connectionData"/> and raises SOAP events.
        /// Called after <see cref="IPresenceLobbyService.JoinOrCreateAsync"/> succeeds — both from
        /// the initial <see cref="EnsureInitializedAsync"/> path and from the reconnect path in
        /// <see cref="RefreshAsync"/>.
        /// </summary>
        /// <remarks>
        /// Safe to call when <see cref="IPresenceLobbyService.ActiveLobby"/> is null (returns
        /// immediately); callers need not null-check before calling.
        /// </remarks>
        private void ApplyPostLobbyJoinState()
        {
            var lobby = _lobbyService.ActiveLobby;
            if (lobby == null) return;

            connectionData.IsConnected         = true;
            connectionData.IsPresenceLobbyHost = lobby.IsHost;

            _memberService.SeedLocalPlayer(clearFirst: true);

            _eventBus.RaiseHostConnectionEstablished();
        }

        // ╔═══════════════════════════════════════════════════════════════════╗
        // ║  Property publishing                                              ║
        // ║  Delegates to _propertyWriter (LobbyPropertyWriter, Phase 3).    ║
        // ╚═══════════════════════════════════════════════════════════════════╝

        private async UniTaskVoid PublishJoinedPartyAsync(string partySessionId)
        {
            if (string.IsNullOrEmpty(partySessionId)) return;
            var lobby = _lobbyService.ActiveLobby;
            if (lobby == null) return;
            await _propertyWriter.WriteAsync(
                lobby,
                () => lobby.CurrentPlayer.SetProperty(JOINED_PARTY_KEY,
                    new PlayerProperty(partySessionId, VisibilityPropertyOptions.Public)),
                "PublishJoinedParty");
        }

        private async UniTaskVoid ClearJoinedPartyAsync()
        {
            var lobby = _lobbyService.ActiveLobby;
            if (lobby == null) return;
            await _propertyWriter.WriteAsync(
                lobby,
                () => lobby.CurrentPlayer.SetProperty(JOINED_PARTY_KEY,
                    new PlayerProperty(string.Empty, VisibilityPropertyOptions.Public)),
                "ClearJoinedParty");
        }

        private async UniTaskVoid RepublishLocalIdentityAsync()
        {
            var lobby = _lobbyService.ActiveLobby;
            if (lobby == null) return;
            await _propertyWriter.WriteAsync(
                lobby,
                () =>
                {
                    lobby.CurrentPlayer.SetProperty(DISPLAY_NAME_KEY,
                        new PlayerProperty(connectionData.LocalDisplayName ?? "Pilot",
                            VisibilityPropertyOptions.Public));
                    lobby.CurrentPlayer.SetProperty(AVATAR_ID_KEY,
                        new PlayerProperty(connectionData.LocalAvatarId.ToString(),
                            VisibilityPropertyOptions.Public));
                },
                "RepublishLocalIdentity");
        }

        private async UniTaskVoid PublishPresenceImmediateAsync()
        {
            if (_lobbyService.ActiveLobby == null) return;

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

        private async UniTask PublishPartyStateIfChangedAsync()
        {
            var lobby = _lobbyService.ActiveLobby;
            if (lobby == null) return;

            int    currentCount = connectionData.PartyMembers != null ? connectionData.PartyMembers.Count : 0;
            string currentMatch = ResolveCurrentMatchName();

            if (currentCount == _publishedPartyCount && currentMatch == _publishedMatchName) return;

            try
            {
                lobby.CurrentPlayer.SetProperty(PARTY_COUNT_KEY,
                    new PlayerProperty(currentCount.ToString(), VisibilityPropertyOptions.Public));
                lobby.CurrentPlayer.SetProperty(PARTY_MAX_KEY,
                    new PlayerProperty(connectionData.MaxPartySlots.ToString(), VisibilityPropertyOptions.Public));
                lobby.CurrentPlayer.SetProperty(MATCH_NAME_KEY,
                    new PlayerProperty(currentMatch ?? string.Empty, VisibilityPropertyOptions.Public));

                await _propertyWriter.SaveWithRetryAsync(lobby);
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

        // ╔═══════════════════════════════════════════════════════════════════╗
        // ║  Identity sync (cloud profile + auth fallback chain)              ║
        // ╚═══════════════════════════════════════════════════════════════════╝

        private async UniTask WaitForProfileInitAsync(int timeoutMs)
        {
            if (playerDataService == null || playerDataService.IsInitialized) return;

            int elapsed = 0;
            const int stepMs = 100;
            while (!playerDataService.IsInitialized && elapsed < timeoutMs)
            {
                await UniTask.Delay(stepMs);
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

        /// <summary>
        /// Detects the harmless <see cref="ArgumentOutOfRangeException"/> the UGS
        /// Lobby SDK throws from <c>LobbyPatcher.ApplyPatchesToLobby</c> when a
        /// WebSocket delta references a stale player index. Surfaces both as the
        /// direct exception and as an <c>AggregateException</c>/inner-wrapped
        /// exception forwarded by <c>await</c>. <see cref="RefreshPartyMembersAsync"/>
        /// swallows these on the next-tick path so the refresh loop stays clean.
        /// </summary>
        private static bool IsBenignLobbyPatcherError(Exception e)
        {
            for (var current = e; current != null; current = current.InnerException)
            {
                if (current is ArgumentOutOfRangeException
                    && (current.StackTrace?.Contains("LobbyPatcher") ?? false))
                    return true;
            }
            return false;
        }

    }
}
