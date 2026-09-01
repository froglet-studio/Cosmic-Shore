using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
        [Tooltip("Raised by BootStatusPanel when the user taps the retry button after the auto-retry loop exhausts. Triggers EnsurePartySessionAsync.")]
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
        // Constants - keys, sentinels, separators, tuning
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

        // The HOST's clock starts at SEND, while the recipient's starts when their lobby poll
        // OBSERVES the invite - a refresh interval plus RTT plus any 429 backoff later. At 10s
        // the host could expire an invite (and stop accepting the acceptance signal, see
        // ScanForAcceptances' OutgoingCount gate) before a distant player had finished reading
        // it. Kept in step with FriendsListPanel.partyInviteExpirationSeconds.
        private const float OUTGOING_INVITE_TIMEOUT_SECONDS  = 60f;
        private const int   MAX_REFRESH_ERRORS_BEFORE_RECONNECT = 3;
        private const float FORCE_REFRESH_COOLDOWN_SECONDS   = 0.5f;
        private const int   PROFILE_INIT_TIMEOUT_MS          = 5000;
        // B8 fix 2: cap how long a client leave waits for the joined_party
        // clear-property write before proceeding anyway. WriteAsync is normally
        // ~1-3s; under B1 stale-index churn its retries can stretch longer, and a
        // clean leave must not hang on a flaky write (fix 1 already protects the
        // host from the stale property).
        private const float CLEAR_JOINED_PARTY_TIMEOUT_SECONDS = 3f;

        /// <summary>
        /// Cadence (seconds) for the periodic presence-lobby convergence check
        /// (<see cref="IPresenceLobbyService.ConvergeToCanonicalAsync"/>).  Heals a
        /// simultaneous-create split within a few seconds while staying well under
        /// the UGS QuerySessions rate limit.  Decoupled from
        /// <see cref="refreshIntervalSeconds"/> so the cheap per-tick refresh stays
        /// responsive without firing an extra query every tick.
        /// </summary>
        private const float PRESENCE_CONVERGE_INTERVAL_SECONDS = 4f;

        /// <summary>
        /// After session creation, suppress <see cref="RefreshPartyMembersAsync"/>
        /// for this many seconds.  A freshly-provisioned session can transiently
        /// fail RefreshAsync; nulling the session in response would cause
        /// <see cref="AcceptanceSignalService.ScanForSignals"/> to recreate it on
        /// the next tick, kicking any joining client.
        /// </summary>
        private const float SESSION_CREATION_GRACE_PERIOD_SECONDS = 4f;

        /// <summary>
        /// How long an IDLE party session (hosting, nobody connected, no invite outstanding) may
        /// sit before it is recycled.
        ///
        /// <para><b>Why this exists.</b> Under the locked eager-Relay design every player creates
        /// a Relay-backed party session on entering Menu_Main, and it then sits with ZERO peers
        /// until somebody accepts an invite. Unity Relay reclaims an allocation that carries no
        /// traffic, and when it does the UGS SESSION stays perfectly valid - so every
        /// session-level self-heal in this class keeps passing while the transport underneath is
        /// dead. Observed in the field (host log):
        ///   "Received error message from Relay: player timed out due to inactivity."
        ///   "Relay allocation is invalid. See ... RelayConnectionStatus.AllocationInvalid"
        /// After that the advertised session id points at nothing: a guest connects, NGO
        /// synchronisation never completes, their ClientRpcs are deferred and dropped, and they
        /// bounce - which is why the only known workaround was restarting the game (a restart
        /// mints a fresh allocation).
        ///
        /// <para>Comfortably under Relay's reclaim window, so the allocation is replaced before
        /// it can go stale rather than after.</para>
        /// </summary>
        private const float IDLE_SESSION_RECYCLE_SECONDS = 240f;

        /// <summary>
        /// <see cref="ReconcilePartyMembersNow"/> retries the refresh+sync this many
        /// times to absorb UGS leave-propagation lag, stopping early once the roster
        /// shrinks.
        /// </summary>
        private const int RECONCILE_MAX_ATTEMPTS = 3;

        /// <summary>Delay between <see cref="ReconcilePartyMembersNow"/> retry attempts.</summary>
        private const int RECONCILE_RETRY_DELAY_MS = 500;

        // ─────────────────────────────────────────────────────────────────────
        // Synchronization
        //
        // Both mutexes live in LobbyPropertyWriter (extracted in Phase 3).
        // Shortcuts for readability - these reference the same SemaphoreSlim
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
        /// Single source of truth - replaces the scatter of boolean flags
        /// (_isHost, _joining, _inviteSent…) that previously drifted out of sync.
        /// Read via CurrentState; change via TryTransition; react via OnStateChanged.
        /// </summary>
        private readonly PartyStateMachine _stateMachine = new();

        private bool   _joining;
        private bool   _profileSubscribed;
        private bool   _gameLaunchSubscribed;
        private bool   _partyLeaveSubscribed;
        private bool   _handlingDefiniteSessionGone;
        private float  _rateLimitBackoffUntil;
        private float  _nextForcedRefreshAllowed;
        private float  _nextConvergeAllowed;
        private int    _consecutiveRefreshErrors;
        private int    _publishedPartyCount = -1;
        private string _publishedMatchName  = "<UNSET>";
        // Identity (displayName/avatarId) rides the same change-gated per-tick
        // publish so a rename is GUARANTEED to reach the lobby even when the
        // event-driven RepublishLocalIdentityAsync no-ops (lobby ref null during
        // a reconnect/converge window) or its save fails - the tick reconciles.
        private string _publishedDisplayName = "<UNSET>";
        private int    _publishedAvatarId    = int.MinValue;

        // ─────────────────────────────────────────────────────────────────────
        // Invite state
        // ─────────────────────────────────────────────────────────────────────

        private PartyInviteData? _lastFiredInvite;

        /// <summary>
        /// Consecutive refresh ticks on which the sender of <see cref="_lastFiredInvite"/> had no
        /// invite line for us. Two in a row are required before the record is dropped, so a single
        /// stale lobby snapshot (a presence-lobby converge mid-tick) cannot flicker a live invite.
        /// </summary>
        private int _inviteMissTicks;
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
        /// Do NOT call TryTransition from outside HostConnectionService - only this
        /// class is the single writer of party state.
        /// </summary>
        public PartyStateMachine StateMachine => _stateMachine;

        // ─────────────────────────────────────────────────────────────────────
        // Guard predicates - derive from authoritative state (state machine,
        // lobby service ref, NetworkManager) rather than a separate boolean.
        // See Docs/PartySystem/ARCHITECTURE.md (Investigation answers Q2).
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// True after <see cref="EnsureInitializedAsync"/> has completed at least
        /// once and before <see cref="HandleSignedOutEvent"/> transitions us back
        /// to Disconnected. Equivalent to the old <c>_initialized</c> boolean.
        /// </summary>
        private bool IsInitialized => _stateMachine.CurrentState != PartyState.Disconnected;

        /// <summary>
        /// True when we're initialized AND the presence lobby reference is live.
        /// Read by <see cref="Update"/>, <see cref="ForceRefreshNow"/>, and the
        /// <see cref="EnsureInitializedAsync"/> re-entry guard.
        /// </summary>
        private bool IsInPresenceLobby => IsInitialized && _lobbyService.ActiveLobby != null;

        /// <summary>
        /// True when NetworkManager is actively hosting a Relay-backed party
        /// session. The canonical "am I a live party host?" predicate - checks
        /// both Netcode reality (<c>IsListening</c>, <c>IsServer</c>) and the
        /// presence of an <see cref="ISession"/> reference. Used as the
        /// idempotent guard for party-session creation (Commit 4 onwards).
        /// </summary>
        private bool IsHostingParty
        {
            get
            {
                var nm = NetworkManager.Singleton;
                return nm != null && nm.IsListening && nm.IsServer && PartySession != null;
            }
        }

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
            // Do not access service fields here - use Start() instead.
            SceneManager.sceneLoaded += OnSceneLoaded;

            if (bootStatusRetryRequestedEvent != null)
                bootStatusRetryRequestedEvent.OnRaised += HandleBootStatusRetryRequested;
        }

        void Start()
        {
            // All [Inject] fields (services + gameData) are populated before Start.
            // UGS auth completes asynchronously AFTER Start in the normal flow, so
            // OnSignedIn is the PRIMARY init trigger - subscribe in code, the same
            // pattern used by MultiplayerSetup / UGSDataService / AnalyticsServiceFacade.
            // There is no inspector EventListenerNoParam for this handler.
            // HandleSignedInEvent is idempotent - the immediate call (for the
            // already-signed-in case) and the event collapse through
            // EnsureInitializedAsync's IsInPresenceLobby || _joining guard.
            if (authenticationDataVariable == null)
            {
                Debug.LogError(
                    "[HostConnectionService] authenticationDataVariable not wired - " +
                    "party init cannot start (presence lobby will never be created).");
                return;
            }

            authenticationDataVariable.Value.OnSignedIn.OnRaised += HandleSignedInEvent;

            // State-preserving lobby rejoin (B4): every lobby (re)join - initial,
            // reconnect, and the periodic converge migration - publishes the LIVE
            // stateful property values instead of wiping them to empty. HCS stays
            // the single writer of these values; the lobby service only carries
            // them across the rejoin.
            _lobbyService.LivePropertySource = BuildLivePresenceProperties;

            HandleSignedInEvent();
        }

        /// <summary>
        /// Live values for the stateful presence-lobby player properties, used by
        /// the lobby service when (re)joining a lobby so migration/reconnect
        /// preserves state instead of resetting it (Docs/PresenceSystem/BUGS.md B4):
        /// outgoing invite lines (a member's pending invite survives a converge
        /// migration), a guest's joined_party advertisement (the host's admit scan
        /// doesn't lose them mid-migration), and the current match name. The
        /// accepted_invite signal is deliberately NOT preserved - it is a fast-path
        /// hint the inviter also gets from the session member sync, and carrying it
        /// across rejoins would make stale signals permanent.
        /// </summary>
        private IReadOnlyDictionary<string, string> BuildLivePresenceProperties()
        {
            var live = new Dictionary<string, string>();

            string inviteLines = _inviteService?.SerializeAll();
            if (!string.IsNullOrEmpty(inviteLines))
                live[INVITE_PAYLOADS_KEY] = inviteLines;

            if (!connectionData.IsPartyHost &&
                _partySessionService?.ActiveSession?.Id is { Length: > 0 } joinedSessionId)
                live[JOINED_PARTY_KEY] = joinedSessionId;

            string matchName = ResolveCurrentMatchName();
            if (!string.IsNullOrEmpty(matchName))
                live[MATCH_NAME_KEY] = matchName;

            return live;
        }

        void Update()
        {
            if (!IsInPresenceLobby) return;
            if (_lobbyMutex.CurrentCount == 0) return;                   // someone is already inside the mutex
            if (Time.unscaledTime < _rateLimitBackoffUntil) return;
            if (!IsOnMenuScene()) return;

            ExpireOutgoingInvites();

            if (_scheduler.ShouldFireNow(Time.unscaledDeltaTime))
                RefreshAsync().Forget();
        }

        async void OnDestroy()
        {
            // Duplicate instance (Awake's singleton guard already Destroy()'d this
            // gameObject) or we've been replaced - do NO cleanup. The DI-injected
            // _lobbyService / _propertyWriter are SHARED singletons; tearing them
            // down from a duplicate would corrupt the live instance. A duplicate
            // may also have un-injected (null) fields, so this also avoids
            // spurious null-guard logs.
            if (Instance != this) return;

            // Unsubscribes are no-ops if we never subscribed.
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (authenticationDataVariable != null)
                authenticationDataVariable.Value.OnSignedIn.OnRaised -= HandleSignedInEvent;
            UnsubscribeFromProfileChanges();
            UnsubscribeFromGameLaunch();
            UnsubscribeFromPartySessionEvents();

            if (bootStatusRetryRequestedEvent != null)
                bootStatusRetryRequestedEvent.OnRaised -= HandleBootStatusRetryRequested;
            else
                Debug.LogError(
                    "[HostConnectionService] OnDestroy: bootStatusRetryRequestedEvent is null - " +
                    "SOAP event asset not wired on the prefab. Boot-status retry would not have functioned.");

            if (_lobbyService != null)
                await _lobbyService.LeaveAsync();
            else
                Debug.LogError(
                    "[HostConnectionService] OnDestroy: _lobbyService is null - Reflex DI never populated it. " +
                    "Skipping presence-lobby leave; other users may see this player online for ~30s until UGS reaps the entry.");

            // Deliberately NOT disposing the two SemaphoreSlims: this OnDestroy is async, so
            // the service's other in-flight flows (every `await *Mutex.WaitAsync()`) can still
            // resume AFTER the awaits above — a disposed semaphore turned every play-exit into
            // "ObjectDisposedException: The semaphore has been disposed" spam (crash-detector
            // journal, 2026-08-20). A SemaphoreSlim that never touches AvailableWaitHandle
            // holds no OS handle, so there is nothing to leak by letting the GC collect it.

            Instance = null;
        }

        private void HandleBootStatusRetryRequested() => EnsurePartySessionAsync().Forget();

        /// <summary>
        /// Resets per-session invite state on Menu_Main reload.
        /// <see cref="HostConnectionDataSO.PartyMembers"/> / <c>OnlinePlayers</c> use
        /// <c>ResetType.ApplicationStarts</c>, so they persist across scene loads:
        /// the local player is seeded once at init (<see cref="ApplyPostLobbyJoinState"/>)
        /// and remote members are reconciled by the refresh loop
        /// (<see cref="RefreshPartyMembersAsync"/>). No per-scene rebuild is needed.
        /// </summary>
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "Menu_Main") return;

            _lastFiredInvite     = null;
            _lastInviteResolved  = false;
            PublishPresenceImmediateAsync().Forget();
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
            // Sign-out is the "emergency exit" - always allowed regardless of current state.
            // Transition flips IsInitialized to false (replaces the old _initialized boolean).
            _stateMachine.TryTransition(PartyState.Disconnected);
            connectionData.ResetRuntimeData();
            await _lobbyService.LeaveAsync();
            _eventBus.RaiseHostConnectionLost();
        }

        /// <summary>
        /// Idempotent initialization. Safe to call from both <see cref="Start"/>
        /// (auth-already-signed-in path) and <see cref="HandleSignedInEvent"/>
        /// (auth-signed-in-after-Start path) - concurrent calls collapse to one.
        ///
        /// NOTE: party session is intentionally NOT created here. Eager creation
        /// would burn a Relay allocation per launch and would call
        /// <c>nm.Shutdown()</c> + <c>StartHost()</c> - destroying and respawning
        /// every menu vessel. The Relay session is created lazily on first
        /// invite acceptance via <see cref="AcceptanceSignalService.ScanForSignals"/>.
        /// </summary>
        private async UniTask EnsureInitializedAsync()
        {
            if (IsInPresenceLobby || _joining) return;
            _joining = true;
            try
            {
                SubscribeToProfileChanges();
                SubscribeToGameLaunch();
                SubscribeToPartySessionEvents();

                await WaitForProfileInitAsync(PROFILE_INIT_TIMEOUT_MS);
                SyncLocalIdentity();

                await _lobbyService.JoinOrCreateAsync(presenceLobbyMaxPlayers);

                // Apply post-join state now that the lobby reference is live.
                ApplyPostLobbyJoinState();

                // Catch the case where the cloud profile resolved during
                // JoinOrCreateAsync - HandleProfileChanged's republish
                // would have been a no-op (lobby was still null at that moment).
                SyncLocalIdentity();
                RepublishLocalIdentityAsync().Forget();

                // Presence lobby joined - transient state, immediately creates solo Relay session.
                // Transition flips IsInitialized to true (replaces the old _initialized boolean).
                _stateMachine.TryTransition(PartyState.InPresenceLobby);
                DebugExtensions.LogColored(
                    $"[HostConnectionService] Presence lobby joined - lobby: {_lobbyService.ActiveLobby?.Id ?? "NULL"}, " +
                    $"localId: {connectionData.LocalPlayerId}",
                    Color.green);

                // Every player always hosts their own solo Relay party session from menu entry.
                // Creates Relay session and starts NM - vessel spawns when NM is up.
                await EnsurePartySessionAsync();
            }
            finally { _joining = false; }
        }

        // ╔═══════════════════════════════════════════════════════════════════╗
        // ║  Public Invite API                                                ║
        // ╚═══════════════════════════════════════════════════════════════════╝

        public async UniTask SendInviteAsync(string targetPlayerId)
        {
            DebugExtensions.LogColored(
                $"[INVITE-SEND] SendInviteAsync called - target: {targetPlayerId}", Color.cyan);

            // OFFLINE session: there is no presence lobby and no Relay session to invite
            // anyone into. The party UI should be gated (OfflineUIGate), but a screen that
            // was never wired must still not be able to fire a doomed request - and without
            // this the call would fall through to EnsurePartySessionAsync, which no-ops
            // offline, leaving a null session ref to dereference below.
            if (_gameData != null && _gameData.IsOfflineSession)
            {
                CSDebug.Log("[HostConnectionService] Offline session - invites are unavailable.");
                return;
            }

            if (_lobbyService.ActiveLobby == null)
            {
                DebugExtensions.LogErrorColored(
                    "[INVITE-SEND] ABORT - presence lobby is null", Color.red);
                throw new InvalidOperationException("Presence lobby unavailable.");
            }

            // Capacity guard: a full party can't take another member - refuse
            // before any network write instead of letting the acceptor discover
            // it as a JoinByIdAsync failure + bounce. Throwing (not returning)
            // lets the UI catch reset the optimistic "PENDING REQUEST" row.
            if (!connectionData.HasOpenSlots)
            {
                DebugExtensions.LogErrorColored(
                    $"[INVITE-SEND] ABORT - party is full " +
                    $"({connectionData.PartyMembers?.Count ?? 0}/{connectionData.MaxPartySlots})", Color.red);
                throw new InvalidOperationException("Party is full.");
            }

            // Idempotent re-click: just refresh the timeout, no network roundtrip.
            if (_inviteService.Contains(targetPlayerId))
            {
                DebugExtensions.LogColored(
                    $"[INVITE-SEND] {targetPlayerId} already pending - refreshing timeout",
                    Color.yellow);
                _inviteService.RefreshTimeout(targetPlayerId,
                    Time.unscaledTime + OUTGOING_INVITE_TIMEOUT_SECONDS);
                return;
            }

            // Ensure our own Relay session is live before writing the invite.
            // EnsurePartySessionAsync is idempotent - fast-paths if IsHostingParty,
            // serialises concurrent callers via the mutex, and post-checks again.
            if (_partySessionService.ActiveSession == null)
            {
                // Role-aware guard (invite chain): a GUEST with a null session
                // ref is broken party state. EnsurePartySessionAsync on a guest
                // is guest-destructive - its IsHostingParty fast-path requires
                // nm.IsServer, so it would shut down the NM client connection,
                // mint a solo Relay session, flip IsPartyHost, and stamp this
                // invite with the WRONG session id - silently ejecting the
                // sender from the party they're in. Recovery of broken guest
                // state belongs to the refresh watchdog / bounce paths, not a
                // send. See Docs/PartySystem/INVITE_ENHANCEMENTS.md Task 4 (2a).
                if (!connectionData.IsPartyHost && connectionData.RemotePartyMemberCount > 0)
                {
                    DebugExtensions.LogErrorColored(
                        "[INVITE-SEND] ABORT - guest has no ActiveSession (broken party state); " +
                        "refusing to self-eject via EnsurePartySessionAsync", Color.red);
                    CSDebug.Log($"[INVITE-SEND] NetDiag: {NetworkDiagnostics.GetSnapshot()}");
                    throw new InvalidOperationException("Party session unavailable.");
                }

                DebugExtensions.LogColored(
                    "[INVITE-SEND] Relay session not yet ready - awaiting EnsurePartySessionAsync...",
                    Color.yellow);
                await EnsurePartySessionAsync();
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
                // Use the real session ID directly - no PENDING placeholder.
                // NOTE (invite chain): for a party MEMBER this is the session
                // they are IN - i.e. the actual host's session - so a member's
                // invite lands the acceptor in the member's current party.
                // Throw (not return) so the UI catch resets the optimistic
                // pending row instead of leaving it stuck until the timeout.
                if (_partySessionService.ActiveSession?.Id is not { Length: > 0 } sessionId)
                {
                    Debug.LogError("[INVITE-SEND] ABORT - party session creation failed; cannot send invite.");
                    throw new InvalidOperationException("Party session unavailable.");
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
                    "[INVITE-SEND] SaveCurrentPlayerDataAsync completed - properties persisted",
                    Color.green);

                // This party is now invite-formed for analytics purposes (host side).
                // Cleared by HostConnectionDataSO.ResetRuntimeData on party teardown.
                connectionData.PartyFormedByInvite = true;

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

        /// <summary>
        /// Host-initiated cancel of an outgoing invite (the ✕ on a "Pending Invite" row). Reuses
        /// the same clear path as the auto-timeout / join-detected clears: removes the invite from
        /// the tracker, re-publishes <c>invite_payloads</c> WITHOUT that line (so the recipient's
        /// invite / popup / Requests row disappear), and fires <see cref="OutgoingInviteCleared"/>
        /// so the host's row reverts to the invitee's online status (re-invitable). No-op if no
        /// outgoing invite is pending for the target.
        /// </summary>
        public UniTask CancelInviteAsync(string targetPlayerId)
            => ClearOutgoingInviteIfPresentAsync(targetPlayerId, "user-cancel");

        public async UniTask AcceptInviteAsync(PartyInviteData invite)
        {
            // Mark resolved up-front so a re-opened FriendsListPanel doesn't
            // re-spawn a row for the invite the user just accepted.
            _lastInviteResolved = true;
            _eventBus.RaiseInviteResolved();

            // This party is invite-formed for analytics purposes (joiner side).
            connectionData.PartyFormedByInvite = true;

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
                    Debug.LogError("[HostConnectionService] AcceptInvite ABORT - invite has no session ID. The host may not have a Relay session.");
                    await EnsurePartySessionAsync(); // JoiningParty → HostingParty → InParty
                    return;
                }

                // Eager "Always InParty" model: this client hosts its OWN Relay party
                // session (created on menu entry). Leave it through the SDK BEFORE
                // joining the inviter's so the SDK releases its host network handler /
                // session binding from the shared NetworkManager. Skipping this leaves a
                // stale host binding that races the client-start inside JoinByIdAsync -
                // the intermittent "Netcode client never connected" bounce. The NM was
                // already shut down by PartyInviteController.ShutdownAsync, so this is a
                // server-side delete + binding release. See Docs/PartySystem/ARCHITECTURE.md.
                Debug.Log($"[HostConnectionService][diag] before leave-own - ActiveSession={_partySessionService.ActiveSession?.Id ?? "null"}");
                await _partySessionService.LeaveAsync();
                Debug.Log("[HostConnectionService][diag] left own session - joining inviter's session...");

                await _partySessionService.JoinByIdAsync(realSessionId);
                Debug.Log($"[HostConnectionService][diag] JoinByIdAsync returned - ActiveSession={_partySessionService.ActiveSession?.Id ?? "null"}");

                connectionData.IsPartyHost = false;

                _memberService.SeedLocalPlayer(clearFirst: true);
                var hostData = new PartyPlayerData(invite.HostPlayerId, invite.HostDisplayName, invite.HostAvatarId);
                connectionData.PartyMembers?.Add(hostData);
                _eventBus.RaisePartyMemberJoined(hostData);

                // Give the freshly-joined session a settling period before the
                // first member-sync refresh fires - avoids stale-session 404s.
                _scheduler.ResetDeferred(refreshIntervalSeconds);
                Debug.Log($"[HostConnectionService] Joined party {_partySessionService.ActiveSession?.Id}");
                // Relay session join succeeded - we are now fully inside the party.
                _stateMachine.TryTransition(PartyState.InParty);

                _scheduler.Boost();

                // Advertise this join so the host's RefreshAsync picks us up
                // before their party-session Players list catches up.
                PublishJoinedPartyAsync(realSessionId).Forget();
            }
            catch (Exception e)
            {
                // Do NOT swallow. A throw here (most often a transient JoinByIdAsync
                // failure - see PartySessionService's retry) used to return normally, so
                // PartyInviteController logged a false "joined" and then waited the full
                // 8s connect timeout on a NetworkManager that was never started as a
                // client. Log the full exception and rethrow so PIC's catch recovers
                // immediately (fail fast) and the real cause is visible.
                // See Docs/PartySystem/ARCHITECTURE.md (Error-handling matrix).
                Debug.LogError(
                    $"[HostConnectionService] AcceptInvite error ({e.GetType().Name}): {e}" +
                    (e.InnerException != null
                        ? $" - inner ({e.InnerException.GetType().Name}): {e.InnerException}"
                        : string.Empty));
                CosmicShore.Utility.CSDebug.Log($"[HostConnectionService] NetDiag: class={CosmicShore.Utility.NetworkDiagnostics.ClassifyException(e)} | {CosmicShore.Utility.NetworkDiagnostics.GetSnapshot()}");
                throw;
            }
        }

        public UniTask DeclineInviteAsync()
        {
            // Sender's slot is freed by their own timeout - UGS doesn't expose
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

            // B8 fix 2: wait for the clear so the stale `joined_party` presence
            // property is actually removed on the wire BEFORE leave teardown
            // disrupts the lobby reference - otherwise the host keeps seeing the
            // stale "I'm in your party" claim (B8 fix 1 makes the host ignore it,
            // this removes it). Bounded by a timeout: WriteAsync is normally
            // ~1-3s (mutex + refresh + save-with-retry) but its retries can
            // stretch longer under B1 stale-index churn, and a clean leave must
            // not stall on a flaky property write. WriteAsync swallows its own
            // exceptions, so the clear can only be slow, never throw; if the
            // timeout wins we proceed (fix 1 already protects the host). Uses
            // WhenAny + Delay rather than UniTask.Timeout() to stick to core
            // UniTask primitives this version is known to support.
            var clearTask = ClearJoinedPartyAsync();
            int winner = await UniTask.WhenAny(
                clearTask,
                UniTask.Delay(TimeSpan.FromSeconds(CLEAR_JOINED_PARTY_TIMEOUT_SECONDS)));
            if (winner != 0)
                Debug.LogWarning(
                    "[HostConnectionService] ClearJoinedParty did not complete within " +
                    $"{CLEAR_JOINED_PARTY_TIMEOUT_SECONDS}s - proceeding with leave " +
                    "(host ignores stale joined_party via the session cross-check).");

            // LeavePartyAndReturnToMenuAsync owns the full leave sequence:
            // session-leave → NM shutdown → Menu_Main reload → EnsurePartySessionAsync.
            // No trailing call needed.
            await controller.LeavePartyAndReturnToMenuAsync();
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
                    // Per Docs/PartySystem/ARCHITECTURE.md error-handling matrix:
                    // log, state unchanged. The local SOAP removal above already
                    // updated the UI; if the UGS-side kick fails the target will
                    // reappear on the next refresh tick and the host can retry.
                    Debug.LogWarning(
                        $"[HostConnectionService] Kick of '{playerId}' failed " +
                        $"({e.GetType().Name}): {e.Message} - local view updated, host can retry.");
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
            if (!IsInPresenceLobby) return;

            _scheduler.Boost();

            if (Time.unscaledTime < _nextForcedRefreshAllowed) return;
            _nextForcedRefreshAllowed = Time.unscaledTime + FORCE_REFRESH_COOLDOWN_SECONDS;

            _scheduler.Reset();
            if (_lobbyMutex.CurrentCount == 0) return; // already running
            RefreshAsync().Forget();
        }

        /// <summary>
        /// Immediately reconcile the party roster against the authoritative UGS
        /// session player list, bypassing the post-creation grace gate that the
        /// periodic poll applies.  Fired on member-departure signals
        /// (<c>ISession.PlayerLeaving</c>, host-side Netcode <c>OnClientDisconnect</c>)
        /// so a departed/bounced member is removed without waiting for the poll
        /// cadence.  Host-only, idempotent, and serialised with the poll via the
        /// shared lobby mutex.
        /// </summary>
        public void ReconcilePartyMembersNow()
        {
            if (!connectionData.IsPartyHost) return;
            if (_partySessionService.ActiveSession == null) return;
            ReconcilePartyMembersNowAsync().Forget();
        }

        private async UniTaskVoid ReconcilePartyMembersNowAsync()
        {
            // Non-blocking acquire: if the poll already holds the mutex it is
            // mid-refresh and will observe the departure itself - one authoritative
            // pass is enough, so we skip rather than queue.
            if (!await _lobbyMutex.WaitAsync(0)) return;
            _insideRefreshCycle = true;
            try
            {
                int before = connectionData.PartyMembers?.Count ?? 0;
                for (int i = 0; i < RECONCILE_MAX_ATTEMPTS; i++)
                {
                    // RefreshPartyMembersAsync owns the error matrix + SyncFromSession
                    // (which removes departed members and raises OnPartyMemberLeft).
                    // RefreshAsync() is internally .AsMainThread() - do not double-wrap.
                    await RefreshPartyMembersAsync(bypassGraceGate: true);

                    if ((connectionData.PartyMembers?.Count ?? 0) < before) break;
                    if (i < RECONCILE_MAX_ATTEMPTS - 1)
                        await UniTask.Delay(RECONCILE_RETRY_DELAY_MS);
                }
            }
            finally
            {
                _insideRefreshCycle = false;
                _lobbyMutex.Release();
            }
        }

        /// <summary>
        /// Idempotent: ensures the local player owns a live solo Relay-backed
        /// party session and that NetworkManager is up as a Relay host.
        ///
        /// • No-op fast-path when <see cref="IsHostingParty"/> is already true.
        /// • Otherwise creates the session and starts NM, transitioning
        ///   current → HostingParty (transient) → InParty.
        ///
        /// Thread-safety: serialised by <see cref="_sessionCreationMutex"/>; the
        /// post-mutex <see cref="IsHostingParty"/> double-check makes concurrent
        /// callers safely collapse to one creation.
        ///
        /// This is the canonical create-or-no-op surface - see
        /// <c>Docs/PartySystem/ARCHITECTURE.md</c> locked design. Callers that
        /// need to drop a stale session reference first must call
        /// <see cref="ClearPartySessionRef"/> explicitly (only the recovery
        /// path in <c>PartyInviteController.RecoverFromFailedTransitionAsync</c>
        /// does this).
        /// </summary>
        public async UniTask EnsurePartySessionAsync()
        {
            // OFFLINE session (OfflineModeService, Docs/OFFLINE_MODE.md): the loopback host
            // IS the session. A late Relay success here would ShutdownAsync that host out
            // from under a live offline game (auth can succeed while Relay keeps failing,
            // and this method retries with backoff long after the boot flow has already
            // fallen back). Re-entering online is a deliberate re-boot, never an in-place
            // promotion - so party session creation stands down for the whole session.
            if (_gameData != null && _gameData.IsOfflineSession)
            {
                CSDebug.Log("[HostConnectionService] Offline session active - skipping party session creation.");
                return;
            }

            // Fast path - already hosting, no work to do.
            if (IsHostingParty) return;

            await _sessionCreationMutex.WaitAsync();
            try
            {
                // Post-mutex double-check: catches the race where a concurrent
                // caller reached IsHostingParty == true while we were waiting.
                if (IsHostingParty) return;

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
                    $"[HostConnectionService] Solo party session ready: {_partySessionService.ActiveSession?.Id} - InParty, vessel will spawn.",
                    Color.green);
            }
            finally
            {
                _sessionCreationMutex.Release();
            }
        }

        /// <summary>
        /// Drops the cached party session reference. The single explicit escape
        /// hatch from a stale <see cref="ISession"/> ref.
        ///
        /// <para>
        /// <b>Currently unused.</b> The leave/recovery flows now decompose into
        /// <see cref="LeavePartySessionAsync"/> + <see cref="EnsurePartySessionAsync"/>
        /// with explicit NM-shutdown and Menu_Main reload between them, which
        /// performs a proper UGS leave (<c>DeleteAsync</c>/<c>LeaveAsync</c>) and
        /// recreates the session in the right scene. Retained for now as a
        /// defensive escape hatch; may be deleted in a follow-up if no caller
        /// emerges.
        /// </para>
        /// </summary>
        public void ClearPartySessionRef() => _partySessionService.ClearSession();

        /// <summary>
        /// Leaves the current party session via <see cref="PartySessionService.LeaveAsync"/>
        /// - calls <c>DeleteAsync</c> (host) or <c>session.LeaveAsync</c> (client)
        /// on the UGS session and clears the shared session ref. Safe to call
        /// when no session is active.
        ///
        /// <para>
        /// Bare leave primitive: does NOT touch NetworkManager, does NOT recreate
        /// a solo session. The caller (currently <c>PartyInviteController</c>'s
        /// leave + failed-transition recovery flows) is responsible for sequencing
        /// the subsequent NM shutdown, Menu_Main reload, and
        /// <see cref="EnsurePartySessionAsync"/> calls in the right order - see
        /// <c>Docs/PartySystem/ARCHITECTURE.md</c> (Investigation answers Q6) for
        /// the rationale. The decomposed primitives ensure
        /// <see cref="EnsurePartySessionAsync"/> only ever runs against a
        /// freshly-loaded Menu_Main scene, so vessel spawn fires exactly once
        /// via the scene-placed initializer's catch.
        /// </para>
        ///
        /// <para>
        /// <see cref="PartySessionService.LeaveAsync"/> currently swallows its
        /// own exceptions, so the outer try/catch here is defensive - kept for
        /// future-proofing.
        /// </para>
        /// </summary>
        /// <summary>
        /// Tears the party layer down to a clean slate. Two callers, one need:
        /// <c>ReconnectService</c> before the boot chain re-runs, and
        /// <c>OfflineModeService</c> when an offline session starts.
        ///
        /// <para>
        /// Leaves the Relay party session AND the presence lobby, and returns the state machine
        /// to <see cref="PartyState.Disconnected"/>. Leaving the presence lobby is the part that
        /// is easy to miss and fatal to skip: UGS membership is SERVER-side, so a re-join while
        /// still a member is refused with "player is already a member of the lobby", HCS never
        /// finishes initialising, and no Relay session is ever created - the auth scene then
        /// waits out three attempts against a session nobody was going to make.
        /// </para>
        ///
        /// <para>
        /// Deliberately does NOT raise <c>HostConnectionLost</c>. That event drives the boot
        /// status panel's "tap retry" surface, and this teardown is a step INSIDE a transition
        /// that is already covered by the loading veil - announcing a loss here would render a
        /// retry button over a flow that is progressing normally (the same suppression
        /// <c>BootStatusBroadcaster</c> already applies to launch and party transitions).
        /// </para>
        ///
        /// <para>
        /// Entering OFFLINE needs exactly the same teardown: an offline session has no lobby and
        /// no Relay, and a presence lobby left running keeps its refresh/converge loop hammering
        /// UGS for the whole offline session - errors on a screen the player was told is offline.
        /// </para>
        ///
        /// <para>Fail-soft throughout: a teardown that throws must not strand the caller, and
        /// every step is already idempotent / safe when nothing is active (a cold offline boot
        /// never joined a lobby at all).</para>
        /// </summary>
        public async UniTask ResetPartyLayerAsync()
        {
            DebugExtensions.LogColored("[HostConnectionService] Resetting party layer...", Color.cyan);

            // Emergency exit - legal from any state, and it stops the refresh loop from
            // fighting the teardown.
            _stateMachine.TryTransition(PartyState.Disconnected);

            try { await LeavePartySessionAsync(); }
            catch (Exception e)
            {
                CSDebug.LogWarning($"[HostConnectionService] Party layer reset: session leave failed ({e.Message}) - continuing.");
            }

            try { await _lobbyService.LeaveAsync(); }
            catch (Exception e)
            {
                CSDebug.LogWarning($"[HostConnectionService] Party layer reset: lobby leave failed ({e.Message}) - continuing.");
            }

            // Drop any local reference the leave calls could not clear (a leave that threw still
            // has to leave us re-joinable), then wipe the roster/invite state the next init
            // rebuilds from scratch.
            _lobbyService.ForceReset();
            connectionData.ResetRuntimeData();

            DebugExtensions.LogColored("[HostConnectionService] Party layer reset - ready to re-init.", Color.green);
        }

        public async UniTask LeavePartySessionAsync()
        {
            try
            {
                if (_partySessionService.ActiveSession != null)
                    await _partySessionService.LeaveAsync();
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    $"[HostConnectionService] LeavePartySessionAsync: " +
                    $"LeaveAsync threw ({ex.GetType().Name}): {ex.Message}.");
            }
        }

        // ╔═══════════════════════════════════════════════════════════════════╗
        // ║  Refresh Loop                                                     ║
        // ╚═══════════════════════════════════════════════════════════════════╝

        private async UniTaskVoid RefreshAsync()
        {
            if (_lobbyService.ActiveLobby == null) return;

            // Mid party-transition (accept/leave) the host→client transport swap
            // briefly disrupts presence refreshes. Counting those toward
            // MAX_REFRESH_ERRORS_BEFORE_RECONNECT would falsely escalate to
            // ForceReset → Reconnecting → throwaway presence lobby - even on a
            // successful join. Skip the tick and clear the counter. Host is
            // never IsTransitioning, so its scan loop keeps running.
            if (PartyInviteController.Instance != null && PartyInviteController.Instance.IsTransitioning)
            {
                _consecutiveRefreshErrors = 0;
                return;
            }

            // Quick non-blocking check - if someone else holds the mutex, skip
            // this tick rather than queuing up. The next tick will pick up.
            if (!await _lobbyMutex.WaitAsync(0))
                return;

            _insideRefreshCycle = true;
            bool shouldReconnect = false;
            try
            {
                await _lobbyService.RefreshAsync();

                // Periodic self-heal: if a simultaneous-create split left us in our
                // own presence lobby while a peer sits in theirs, converge everyone
                // onto the canonical (smallest-id) lobby so discovery and invites
                // work regardless of who started when.  Throttled well under the UGS
                // query rate limit.  Presence lobby is lobby-only - this never
                // touches NetworkManager / Relay / vessels.  Runs inside the lobby
                // mutex (held for this refresh cycle) so the rejoin can't race a
                // concurrent lobby write.
                //
                // Runs even while an invite is outstanding or a party has formed:
                // the rejoin is state-preserving (BuildLivePresenceProperties via
                // LivePropertySource re-publishes invite_payloads / joined_party /
                // matchName), so migrating mid-handshake no longer drops in-flight
                // invites. The old pause froze lobby splits exactly when a 3rd
                // player was being invited into an existing party - the B4 failure
                // (invite never delivered, partied rows vanish). See
                // Docs/PresenceSystem/BUGS.md B4 and INVITE_ENHANCEMENTS.md Task 4.
                if (Time.unscaledTime >= _nextConvergeAllowed)
                {
                    _nextConvergeAllowed = Time.unscaledTime + PRESENCE_CONVERGE_INTERVAL_SECONDS;
                    await _lobbyService.ConvergeToCanonicalAsync(presenceLobbyMaxPlayers);
                }

                // Diff-based update - never Clear() + re-Add() (would flicker UI).
                if (connectionData.OnlinePlayers != null)
                    RefreshOnlinePlayersDiff();

                // Scan composite invite_payloads for lines targeting us - and notice when the
                // line behind the invite we last surfaced is GONE (see ForgetWithdrawnInvite).
                bool lastHostStillInviting = false;
                foreach (var p in _lobbyService.ActiveLobby.Players)
                {
                    if (p.Id == connectionData.LocalPlayerId) continue;
                    if (!TryFindIncomingInvite(p, out var invite)) continue;
                    if (_lastFiredInvite.HasValue && _lastFiredInvite.Value.HostPlayerId == invite.HostPlayerId)
                        lastHostStillInviting = true;
                    TryRaiseIncomingInvite(invite);
                }
                ForgetWithdrawnInvite(lastHostStillInviting);

                // Keep the idle Relay allocation from going stale under us (see
                // IDLE_SESSION_RECYCLE_SECONDS). Before the acceptance scan, so a recycle can
                // never land between a guest reading the session id and joining it.
                await RecycleIdlePartySessionIfStaleAsync();

                // Acceptance-signal scan. Must run BEFORE the JOINED_PARTY_KEY scan
                // because recipients won't set joined_party until after they read the
                // real session id. Gated on outgoing-invite count - no work to do if
                // we haven't sent any invites.
                if (_inviteService.OutgoingCount > 0)
                {
                    var accepters = _acceptanceService.ScanForSignals(
                        _lobbyService.ActiveLobby,
                        connectionData.LocalPlayerId,
                        _inviteService.OutgoingTargets);

                    if (accepters.Count > 0)
                    {
                        // Every player hosts their own Relay session from menu entry
                        // (eager creation), so the session already exists before the
                        // invite was sent - no session creation needed here.
                        // See Docs/PartySystem/ARCHITECTURE.md (Locked design).
                        string activeSessionId = _partySessionService.ActiveSession?.Id;
                        string who = string.Join(", ", accepters);
                        if (string.IsNullOrEmpty(activeSessionId))
                        {
                            Debug.LogError($"[HostConnectionService] Acceptance signal from {who} but no active party session - joiner cannot connect.");
                        }
                        else
                        {
                            Debug.Log($"[HostConnectionService] Acceptance signal from {who} - joiner will connect to existing session {activeSessionId}.");
                            // One republish covers every accepter: it patches the whole outgoing
                            // set, and it is a no-op write when nothing was PENDING.
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
                else if (IsBenignSdkStaleIndexError(e))
                {
                    // Same SDK stale-index defect, read-path surface. Silence to
                    // match the IsBenignLobbyPatcherError treatment above.
                }
                else if (IsRateLimitException(e))
                {
                    _rateLimitBackoffUntil = Time.unscaledTime + refreshIntervalSeconds * 2;
                    Debug.LogWarning("[HostConnectionService] Rate limited during refresh - backing off");
                }
                else
                {
                    Debug.LogWarning($"[HostConnectionService] Refresh error ({e.GetType().Name}): {e}");
                    CosmicShore.Utility.CSDebug.Log($"[HostConnectionService] NetDiag: class={CosmicShore.Utility.NetworkDiagnostics.ClassifyException(e)} | {CosmicShore.Utility.NetworkDiagnostics.GetSnapshot()}");

                    // Companion to the entry guard at the top of RefreshAsync - this branch
                    // catches an in-flight tick that was already past the entry guard (holding
                    // the lobby mutex, awaiting _lobbyService.RefreshAsync) when _transitioning
                    // flipped to true. Counting that transport-teardown failure would combine
                    // with any pre-transition jitter (LobbyRefreshScheduler enters its boost
                    // window on invite-receive, so the counter can already be at 1-2) and
                    // falsely escalate to ForceReset + Reconnecting + a throwaway lobby on a
                    // *successful* join. Reset wipes any stale accumulation too.  The finally
                    // block below still releases _lobbyMutex / clears _insideRefreshCycle.
                    if (PartyInviteController.Instance != null && PartyInviteController.Instance.IsTransitioning)
                    {
                        _consecutiveRefreshErrors = 0;
                        return;
                    }

                    _consecutiveRefreshErrors++;
                    if (_consecutiveRefreshErrors >= MAX_REFRESH_ERRORS_BEFORE_RECONNECT)
                    {
                        Debug.LogWarning($"[HostConnectionService] {_consecutiveRefreshErrors} consecutive refresh errors - reconnecting to presence lobby");
                        _consecutiveRefreshErrors = 0;
                        // Clear the internal session reference so JoinOrCreateAsync will proceed.
                        _lobbyService.ForceReset();
                        shouldReconnect = true;
                        // Connection was lost - enter Reconnecting so callers and UI
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
                // NOT call EnsurePartySessionAsync from this background
                // loop - that path would shut down NetworkManager and respawn
                // every menu vessel.  Relay re-creation is driven by an
                // explicit user action (retry button) via the boot-status
                // SOAP event → HandleBootStatusRetryRequested → EnsurePartySessionAsync,
                // which keeps the user-visible recovery in one place.
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
        // ParseInviteLine must remain private static on this class - tests reflect on it.
        // Delegates to InviteService.ParseLine so the format is defined in one place.
        private static (string targetId, PartyInviteData invite)? ParseInviteLine(string line)
            => InviteService.ParseLine(line);

        /// <summary>
        /// Drops the incoming-invite record once the sender no longer has a line for us.
        ///
        /// <para>
        /// The record is keyed on the SENDER and it used to be permanent: after this player
        /// accepted (the flag is set at the top of <see cref="AcceptInviteAsync"/>, before the
        /// join has been attempted) or declined one invite from a host, <see cref="TryRaiseIncomingInvite"/>
        /// swallowed EVERY later invite from that host as a "PENDING → real id transition" of the
        /// old one - same host, same session id, so it also read as a duplicate. So a guest whose
        /// join bounced could never be re-invited by that host, a declined invite could never be
        /// re-sent, and the only thing that ever cleared it was the host restarting the game
        /// (which mints a new session id). That is the "3rd player can never get in" and "restart
        /// the game to invite again" report.
        /// </para>
        ///
        /// <para>
        /// The host's line for us disappears exactly when the invite is over - cleared on our
        /// corroborated join, cancelled by the host, or timed out after 60s - so its absence is
        /// the signal that the NEXT line from that host is a NEW invite. Left alone while a join
        /// is in flight (the line is cleared as the corroboration lands) and while we are inside
        /// that host's party (the in-session guard in <see cref="TryRaiseIncomingInvite"/> answers
        /// any stale re-appearance there). An UNRESOLVED invite whose line vanished was withdrawn
        /// by the host, so the popup is told to go too.
        /// </para>
        /// </summary>
        private void ForgetWithdrawnInvite(bool lastHostStillInviting)
        {
            if (!_lastFiredInvite.HasValue) { _inviteMissTicks = 0; return; }
            if (lastHostStillInviting)      { _inviteMissTicks = 0; return; }

            if (PartyInviteController.Instance != null && PartyInviteController.Instance.IsTransitioning)
                return;

            var last = _lastFiredInvite.Value;
            if (_partySessionService.ActiveSession != null &&
                !connectionData.IsPartyHost &&
                _partySessionService.ActiveSession.Id == last.PartySessionId)
                return;

            if (++_inviteMissTicks < 2) return;
            _inviteMissTicks = 0;

            bool wasUnresolved = !_lastInviteResolved;
            _lastFiredInvite    = null;
            _lastInviteResolved = false;

            if (wasUnresolved)
            {
                DebugExtensions.LogColored(
                    $"[INVITE-RECV] Invite from '{last.HostDisplayName}' was withdrawn (line gone) - dismissing.",
                    Color.yellow);
                _eventBus.RaiseInviteResolved();
            }
            else
            {
                CSDebug.Log($"[INVITE-RECV] Invite from '{last.HostDisplayName}' is over - a later invite from them will surface as new.");
            }
        }

        private void TryRaiseIncomingInvite(PartyInviteData invite)
        {
            // Already a client in this session - suppress re-fire.
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

            // Departed players with outstanding invites - free the slot now.
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

            // Authoritative party membership is the party SESSION's player list,
            // not the presence-lobby property. Build the session-id set once so
            // the presence scan can only ADD a player who is genuinely in the
            // session. Without this cross-check a client that has left the party
            // but whose stale `joined_party` presence property still points at
            // this session would be re-added every refresh tick - fighting
            // PartyMemberService.SyncFromSession (which correctly removes them),
            // producing an endless MemberLeft/MemberJoined flicker on the host.
            // See Docs/PartySystem/BUGS.md B8.
            var sessionPlayerIds = new HashSet<string>();
            foreach (var sp in _partySessionService.ActiveSession.Players)
                if (!string.IsNullOrEmpty(sp.Id))
                    sessionPlayerIds.Add(sp.Id);

            foreach (var p in _lobbyService.ActiveLobby.Players)
            {
                if (p.Id == connectionData.LocalPlayerId) continue;
                if (!p.Properties.TryGetValue(JOINED_PARTY_KEY, out var joinedProp)) continue;
                if (string.IsNullOrEmpty(joinedProp.Value)) continue;
                if (joinedProp.Value != sessionId) continue;

                // Cross-check against the authoritative session. A presence-lobby
                // "I joined your party" claim that the session does not corroborate
                // is stale data (departed client whose clear-property write didn't
                // land) - never trust it over the session. This is the B8 fix.
                if (!sessionPlayerIds.Contains(p.Id)) continue;

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

        private async UniTask RefreshPartyMembersAsync(bool bypassGraceGate = false)
        {
            var tickSession = _partySessionService.ActiveSession;
            if (tickSession == null) return;
            if (connectionData.PartyMembers == null) return;

            // Grace period: a freshly-provisioned session can transiently fail
            // RefreshAsync.  Clearing the session here would cause
            // AcceptanceSignalService.ScanForSignals to recreate it on the next tick,
            // kicking any joining client.  Bypassed for leave-driven reconcile
            // (ReconcilePartyMembersNow): the goal there is to remove a departed
            // member immediately, not to protect a joining one.
            if (!bypassGraceGate &&
                Time.unscaledTime - _partySessionService.CreatedAtUnscaledTime < SESSION_CREATION_GRACE_PERIOD_SECONDS)
                return;

            try { await _partySessionService.RefreshAsync(); }
            catch (Exception e)
            {
                // [stale-tick] The session this tick was polling is no longer the
                // active session - AcceptInviteAsync left it and joined the
                // inviter's (or a recovery swapped it) while we were awaiting.
                // Its errors - typically SessionDeleted / NotInLobby from our own
                // deliberate leave - describe the OLD session, not the current
                // one. Falling through to the [definite] branch would
                // ClearSession() the just-joined ref, raise a spurious
                // OnHostConnectionLost (the boot-status panel then shows
                // "Connection lost. Tap retry." over the join splash), and start
                // a solo-session recreation that races the join.
                if (!ReferenceEquals(tickSession, _partySessionService.ActiveSession))
                    return;

                // [transition] Companion to the RefreshAsync outer-catch guard:
                // while PartyInviteController is mid-transition the session refs
                // are owned by the accept/leave flow, so any error landing here
                // is teardown noise. Covers the interleaving the stale-tick check
                // misses - the error lands a beat before ActiveSession is
                // reassigned, so the refs still compare equal.
                if (PartyInviteController.Instance != null && PartyInviteController.Instance.IsTransitioning)
                    return;

                // Error-handling matrix - see Docs/PartySystem/ARCHITECTURE.md.
                //
                // [benign] LobbyPatcher stale-index ArgumentOutOfRangeException -
                // known harmless SDK noise, self-corrects on the next tick.
                if (IsBenignLobbyPatcherError(e))
                    return;

                // [benign] WrappedLobbyService NRE on lobby refresh - same SDK
                // stale-index family as the LobbyPatcher case above, surfacing on
                // the read path. Same recovery (retry next tick); silence to match.
                // See Docs/PresenceSystem/BUGS.md B6 + Docs/PartySystem/MPPM_SESSION_LOG.md
                // Session 1 finding #2.
                if (IsBenignSdkStaleIndexError(e))
                    return;

                // [rate-limit] UGS throttled us - back off, keep ActiveSession.
                if (IsRateLimitException(e))
                {
                    Debug.LogWarning($"[HostConnectionService] Party session refresh rate-limited - backing off");
                    _rateLimitBackoffUntil = Time.unscaledTime + refreshIntervalSeconds * 2;
                    return;
                }

                // [definite] Session is gone server-side (404 / SessionNotFound /
                // SessionDeleted / NotInLobby). Retrying forever would leave the UI
                // showing a stale "in party" state. Auto-recover into a fresh solo
                // session so the user is back in a functional menu with no manual
                // action. See HandleDefiniteSessionGoneAsync.
                if (IsDefiniteSessionGoneException(e))
                {
                    Debug.LogWarning(
                        $"[HostConnectionService] Party session gone server-side " +
                        $"({e.GetType().Name}): {e.Message} - auto-recovering to solo session.");
                    CosmicShore.Utility.CSDebug.Log($"[HostConnectionService] NetDiag: class={CosmicShore.Utility.NetworkDiagnostics.ClassifyException(e)} | {CosmicShore.Utility.NetworkDiagnostics.GetSnapshot()}");
                    HandleDefiniteSessionGoneAsync().Forget();
                    return;
                }

                // [transient] Everything else: log and retry next tick WITHOUT
                // clearing the session. Clearing here cascades into host-vessel
                // despawn via SendInviteAsync → EnsurePartySessionAsync →
                // NetworkTransitionService.ShutdownAsync → NetworkManager.Shutdown.
                // Session lifetime is owned by explicit user paths (LeavePartyAsync,
                // kick, NM shutdown, user-tapped boot-status retry) and the
                // [definite] auto-recovery above - not by background refresh ticks.
                Debug.LogWarning(
                    $"[HostConnectionService] Party session refresh error ({e.GetType().Name}): {e.Message} - keeping session, will retry next tick");
                CosmicShore.Utility.CSDebug.Log($"[HostConnectionService] NetDiag: class={CosmicShore.Utility.NetworkDiagnostics.ClassifyException(e)} | {CosmicShore.Utility.NetworkDiagnostics.GetSnapshot()}");
                return;
            }

            // Resolve an authoritative local id; if we can't (signed out), skip this
            // reconcile tick rather than risk adding our own session player as a phantom.
            string localId = ResolveLocalPlayerId();
            if (string.IsNullOrEmpty(localId))
                return;

            var joinedPlayerIds = _memberService.SyncFromSession(
                _partySessionService.ActiveSession, localId);

            foreach (var joinedId in joinedPlayerIds)
                await ClearOutgoingInviteIfPresentAsync(joinedId, "party-join");

            // A new party member appeared in the Relay session.
            // If we're Inviting (sent an invite and they connected), transition to InParty.
            if (joinedPlayerIds.Count > 0 && _stateMachine.CurrentState == PartyState.Inviting)
                _stateMachine.TryTransition(PartyState.InParty);
        }

        /// <summary>
        /// Recovery action for a definite server-side session loss (see
        /// <see cref="IsDefiniteSessionGoneException"/>). Leaves the dead session
        /// and recreates a fresh solo Relay so the user returns to a functional
        /// menu with no manual action.
        ///
        /// <para>
        /// Re-entrancy guarded: the refresh loop fires every ~1.5s while recovery
        /// (leave + recreate) takes a couple of seconds, so without the guard a
        /// second definite-gone tick could start an overlapping recovery.
        /// </para>
        ///
        /// <para>
        /// Recovery sequence:
        /// <list type="number">
        ///   <item>Snapshot whether we had remote members (before clearing).</item>
        ///   <item><see cref="PartySessionService.ClearSession"/> - drop the dead
        ///         ref directly. We KNOW the session is gone, so we skip the doomed
        ///         UGS <c>DeleteAsync</c> that <see cref="LeavePartySessionAsync"/>
        ///         would attempt.</item>
        ///   <item><see cref="PartyMemberService.ClearWithEvents"/> - clears member
        ///         slots and raises <c>OnPartyMemberLeft</c> per non-local member so
        ///         party panels update immediately, not on the next sync.</item>
        ///   <item>Raise <c>OnHostConnectionLost</c> - but only if a real party
        ///         dropped; a solo player whose solo session was reaped recovers
        ///         invisibly (no spurious toast).</item>
        ///   <item><see cref="EnsurePartySessionAsync"/> - fresh solo Relay so the
        ///         user is back in a functional menu with no manual action.</item>
        /// </list>
        /// </para>
        /// </summary>
        private async UniTask HandleDefiniteSessionGoneAsync()
        {
            if (_handlingDefiniteSessionGone) return;
            _handlingDefiniteSessionGone = true;
            try
            {
                bool hadRemoteMembers = connectionData.PartyMembers != null &&
                    connectionData.PartyMembers.Any(m => m.PlayerId != connectionData.LocalPlayerId);

                // Known-gone: drop the ref directly (no doomed UGS DeleteAsync).
                _partySessionService.ClearSession();

                // Clear party-member UI slots + raise OnPartyMemberLeft per member.
                _memberService.ClearWithEvents(connectionData.LocalPlayerId);

                // Only toast when a real party dropped - solo recovery is silent.
                if (hadRemoteMembers)
                    _eventBus.RaiseHostConnectionLost();

                // Recreate a fresh solo Relay session.
                await EnsurePartySessionAsync();
            }
            finally
            {
                _handlingDefiniteSessionGone = false;
            }
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
        /// <summary>
        /// Replace an IDLE party session before Unity Relay reclaims its allocation.
        ///
        /// <para>Runs only when this player is hosting, NOBODY is connected, and no invite is
        /// outstanding - so it can never disturb a live party, and can never race a guest who is
        /// mid-join off an advertised session id. Those two conditions are what make recycling
        /// safe rather than disruptive; without them this would be a reconnect storm.</para>
        ///
        /// <para>Recreating changes the session id, so the new one is republished to the presence
        /// lobby immediately (the same republish the acceptance handshake uses). With no invites
        /// outstanding there is nothing else holding the old id.</para>
        /// </summary>
        private async UniTask RecycleIdlePartySessionIfStaleAsync()
        {
            var session = _partySessionService.ActiveSession;
            if (session == null) return;

            // Hosting only: a GUEST's "session" is the host's, and leaving it here would eject
            // this player from the party they are in.
            if (!connectionData.IsPartyHost) return;

            // Idle only. A connected peer keeps the allocation alive by definition, and an
            // outstanding invite means a guest may be reading this id right now.
            if (connectionData.RemotePartyMemberCount > 0) return;
            if (_inviteService.OutgoingCount > 0) return;

            // Netcode must also be quiet - ConnectedClients covers a peer whose UGS membership
            // has not been reconciled into PartyMembers yet.
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsServer && nm.ConnectedClientsIds.Count > 1) return;

            if (Time.unscaledTime - _partySessionService.CreatedAtUnscaledTime < IDLE_SESSION_RECYCLE_SECONDS)
                return;

            if (PartyInviteController.Instance != null && PartyInviteController.Instance.IsTransitioning)
                return;

            try
            {
                CSDebug.Log(
                    $"[HostConnectionService] Recycling idle party session {session.Id} after " +
                    $"{IDLE_SESSION_RECYCLE_SECONDS}s with no peers - Relay reclaims idle " +
                    "allocations, and a dead allocation is invisible at the session layer.");

                await LeavePartySessionAsync();
                await EnsurePartySessionAsync();

                // Republish so the presence lobby advertises the NEW id. Nothing else can be
                // holding the old one (no outstanding invites - guarded above), but the local
                // player's own published state must not keep pointing at a session that is gone.
                await PublishPartyStateIfChangedAsync();
            }
            catch (Exception e)
            {
                // Never fatal: the next tick retries, and the pre-invite
                // EnsurePartySessionAsync remains the backstop.
                CSDebug.LogWarning(
                    $"[HostConnectionService] Idle session recycle failed ({e.GetType().Name}): " +
                    $"{e.Message} - will retry on a later tick.");
            }
        }

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
        /// Called after <see cref="IPresenceLobbyService.JoinOrCreateAsync"/> succeeds - both from
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

        /// <summary>
        /// Clears our own <c>joined_party</c> presence property (sets it empty) - called when
        /// departing a party: by the deliberate <see cref="LeavePartyAsync"/> (awaited, bounded)
        /// and by host-loss recovery (<c>PartyInviteController.HandleHostLossAsync</c>,
        /// fire-and-forget). Hygiene so no future host sees a dangling "I'm in your party"
        /// claim; B8 fix 1 already makes a stale value inert, so it is best-effort. The presence
        /// lobby is independent of the party session / NM, so this write still lands during a
        /// host-loss teardown. <see cref="LobbyPropertyWriter.WriteAsync"/> swallows its own
        /// exceptions (can only be slow, never throw).
        /// </summary>
        public async UniTask ClearJoinedPartyAsync()
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

            string name   = connectionData.LocalDisplayName ?? "Pilot";
            int    avatar = connectionData.LocalAvatarId;

            await _propertyWriter.WriteAsync(
                lobby,
                () =>
                {
                    lobby.CurrentPlayer.SetProperty(DISPLAY_NAME_KEY,
                        new PlayerProperty(name, VisibilityPropertyOptions.Public));
                    lobby.CurrentPlayer.SetProperty(AVATAR_ID_KEY,
                        new PlayerProperty(avatar.ToString(),
                            VisibilityPropertyOptions.Public));
                },
                "RepublishLocalIdentity");

            // Deliberately do NOT mark _publishedDisplayName/_publishedAvatarId
            // here: WriteAsync swallows terminal save failures (logs + returns),
            // so success can't be observed from this side. Only the per-tick
            // reconciler (PublishPartyStateIfChangedAsync) updates the trackers,
            // inside its own success-gated try - worst case the tick re-saves
            // the same values once after a successful push, which is cheap and
            // keeps the "rename always reaches the lobby" guarantee honest.
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

            int    currentCount  = connectionData.PartyMembers != null ? connectionData.PartyMembers.Count : 0;
            string currentMatch  = ResolveCurrentMatchName();
            string currentName   = connectionData.LocalDisplayName ?? "Pilot";
            int    currentAvatar = connectionData.LocalAvatarId;

            if (currentCount  == _publishedPartyCount &&
                currentMatch  == _publishedMatchName &&
                currentName   == _publishedDisplayName &&
                currentAvatar == _publishedAvatarId) return;

            try
            {
                lobby.CurrentPlayer.SetProperty(PARTY_COUNT_KEY,
                    new PlayerProperty(currentCount.ToString(), VisibilityPropertyOptions.Public));
                // Displayed party size (4), not transport capacity (6) - publishing the
                // capacity is what made every remote row read "1/6".
                lobby.CurrentPlayer.SetProperty(PARTY_MAX_KEY,
                    new PlayerProperty(connectionData.PartyDisplaySlots.ToString(), VisibilityPropertyOptions.Public));
                lobby.CurrentPlayer.SetProperty(MATCH_NAME_KEY,
                    new PlayerProperty(currentMatch ?? string.Empty, VisibilityPropertyOptions.Public));
                // Identity reconciliation: rides the same single save so a rename
                // missed by the event push is guaranteed out within one tick.
                lobby.CurrentPlayer.SetProperty(DISPLAY_NAME_KEY,
                    new PlayerProperty(currentName, VisibilityPropertyOptions.Public));
                lobby.CurrentPlayer.SetProperty(AVATAR_ID_KEY,
                    new PlayerProperty(currentAvatar.ToString(), VisibilityPropertyOptions.Public));

                await _propertyWriter.SaveWithRetryAsync(lobby);
                _publishedPartyCount  = currentCount;
                _publishedMatchName   = currentMatch;
                _publishedDisplayName = currentName;
                _publishedAvatarId    = currentAvatar;
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

            // Event-driven: subscribe to OnProfileChanged and complete the TCS the
            // first time the event fires with IsInitialized == true. PlayerDataService
            // flips IsInitialized = true IMMEDIATELY before raising OnProfileChanged
            // (see PlayerDataService.HandleDataServiceReady), so this is race-free.
            // Timeout via linked CTS - no polling.
            using var cts = new CancellationTokenSource(timeoutMs);
            var tcs = new UniTaskCompletionSource();

            void OnProfileChanged(PlayerProfileData _)
            {
                if (playerDataService.IsInitialized)
                    tcs.TrySetResult();
            }

            playerDataService.OnProfileChanged += OnProfileChanged;
            try
            {
                // Re-check inside the subscribe window: the profile may have
                // resolved between the early-return check and the subscription.
                if (playerDataService.IsInitialized) return;

                await tcs.Task.AttachExternalCancellation(cts.Token);
            }
            catch (OperationCanceledException)
            {
                Debug.LogWarning(
                    $"[HostConnectionService] PlayerDataService.IsInitialized still false after {timeoutMs}ms - " +
                    "proceeding with local default identity; profile-change republish will correct it.");
            }
            finally
            {
                playerDataService.OnProfileChanged -= OnProfileChanged;
            }
        }

        private void SyncLocalIdentity()
        {
            connectionData.LocalPlayerId = AuthData.PlayerId;

            if (playerDataService?.CurrentProfile != null)
            {
                connectionData.LocalDisplayName = playerDataService.CurrentProfile.Identity.DisplayName;
                connectionData.LocalAvatarId    = playerDataService.CurrentProfile.Identity.AvatarId;
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

        /// <summary>
        /// Authoritative local player id for party-member self-identification.
        /// <see cref="HostConnectionDataSO.LocalPlayerId"/> can be transiently empty
        /// (ResetRuntimeData on a sign-out, followed by a re-init that early-returned
        /// at the IsInPresenceLobby/_joining guard before SyncLocalIdentity re-ran).
        /// <c>AuthenticationService.Instance.PlayerId</c> is the same id UGS stamps on
        /// session players, so recovering from it keeps the self-skip correct and stops
        /// the local player being re-added to the member list as an "Unknown Pilot".
        /// Returns empty only when genuinely signed out - callers must skip syncing then.
        /// </summary>
        private string ResolveLocalPlayerId()
        {
            if (!string.IsNullOrEmpty(connectionData.LocalPlayerId))
                return connectionData.LocalPlayerId;

            var auth = Unity.Services.Authentication.AuthenticationService.Instance;
            if (auth != null && auth.IsSignedIn && !string.IsNullOrEmpty(auth.PlayerId))
            {
                SyncLocalIdentity();
                return connectionData.LocalPlayerId;
            }

            return string.Empty;
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
            // Presence lobby (online lists on every peer) - immediate push; the
            // per-tick reconciler in PublishPartyStateIfChangedAsync is the
            // guaranteed fallback if this no-ops or its save fails.
            RepublishLocalIdentityAsync().Forget();
            // Party session player record (party slot names on every peer) -
            // session properties are otherwise only written at create/join.
            _partySessionService.UpdateLocalPlayerPropertiesAsync(
                connectionData.LocalDisplayName, connectionData.LocalAvatarId).Forget();
            // Own row in the local PartyMembers list (slot UI repaints via the
            // list's item events; remote peers pick the rename up from the
            // session player record via SyncFromSession's identity refresh).
            RefreshLocalPartyMemberEntry();
        }

        /// <summary>
        /// Replaces the local player's own entry in <see cref="HostConnectionDataSO.PartyMembers"/>
        /// after a profile change. The entry was seeded as a snapshot
        /// (<see cref="PartyMemberService.SeedLocalPlayer"/>), so a rename would
        /// otherwise leave the local party slot showing the stale name.
        /// RemoveAt + Insert (not indexer-set) so the list's item events fire and
        /// the slot UI repaints; the SOAP member-joined/left events are NOT raised
        /// - this is an identity refresh, not a membership change.
        /// </summary>
        private void RefreshLocalPartyMemberEntry()
        {
            var members = connectionData.PartyMembers;
            if (members == null) return;

            for (int i = 0; i < members.Count; i++)
            {
                if (members[i].PlayerId != connectionData.LocalPlayerId) continue;

                var updated = connectionData.LocalPlayerData;
                if (members[i].DisplayName != updated.DisplayName ||
                    members[i].AvatarId    != updated.AvatarId)
                {
                    members.RemoveAt(i);
                    members.Insert(i, updated);
                }
                return;
            }
        }

        private void SubscribeToPartySessionEvents()
        {
            if (_partyLeaveSubscribed || _partySessionService == null) return;
            _partySessionService.PlayerLeaving += OnPartySessionPlayerLeaving;
            _partyLeaveSubscribed = true;
        }

        private void UnsubscribeFromPartySessionEvents()
        {
            if (!_partyLeaveSubscribed || _partySessionService == null) return;
            _partySessionService.PlayerLeaving -= OnPartySessionPlayerLeaving;
            _partyLeaveSubscribed = false;
        }

        /// <summary>
        /// A player left the host's party session.  Clear any outgoing invite still
        /// aimed at them and reconcile the roster immediately so the departed/bounced
        /// member's slot clears without waiting for the poll.  Host-only: only the
        /// host owns the authoritative roster and outgoing invites; non-host clients
        /// keep getting reconciled by the periodic poll.  Runs on the main thread
        /// (UGS session callbacks are already marshaled).
        /// </summary>
        private void OnPartySessionPlayerLeaving(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return;
            if (!connectionData.IsPartyHost) return;
            _ = ClearOutgoingInviteIfPresentAsync(playerId, "party-leave");
            ReconcilePartyMembersNow();
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
        /// Detects a "session is definitely gone server-side" error - as opposed
        /// to a transient refresh failure that the SDK self-corrects on the next
        /// tick. A definite-gone error means our cached <see cref="ISession"/> no
        /// longer maps to a live UGS session (host deleted it, server reaped it,
        /// or we were removed). The <see cref="RefreshPartyMembersAsync"/> catch
        /// auto-recovers into a fresh solo session on this signal instead of
        /// retrying forever.
        ///
        /// <para>
        /// Structured-first: matches <see cref="SessionError.SessionNotFound"/>,
        /// <see cref="SessionError.SessionDeleted"/>, and
        /// <see cref="SessionError.NotInLobby"/> on a <see cref="SessionException"/>,
        /// plus an HTTP-404 <c>RequestFailedException</c>. Falls back to a narrow
        /// message match (requires the word "session" to co-occur with a
        /// gone-flavored phrase) for SDK paths that surface as plain text. Walks
        /// the <see cref="Exception.InnerException"/> chain because UGS / UniTask
        /// wrap exceptions.
        /// </para>
        /// </summary>
        private static bool IsDefiniteSessionGoneException(Exception e)
        {
            for (var current = e; current != null; current = current.InnerException)
            {
                if (current is SessionException se &&
                    se.Error is SessionError.SessionNotFound
                             or SessionError.SessionDeleted
                             or SessionError.NotInLobby)
                    return true;

                if (current is Unity.Services.Core.RequestFailedException rfe && rfe.ErrorCode == 404)
                    return true;

                var msg = current.Message;
                if (!string.IsNullOrEmpty(msg) &&
                    msg.IndexOf("session", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    (msg.IndexOf("not found",      StringComparison.OrdinalIgnoreCase) >= 0 ||
                     msg.IndexOf("deleted",        StringComparison.OrdinalIgnoreCase) >= 0 ||
                     msg.IndexOf("does not exist", StringComparison.OrdinalIgnoreCase) >= 0))
                    return true;
            }
            return false;
        }

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

        /// <summary>
        /// Detects the harmless <c>SessionException</c> family the UGS SDK throws
        /// from <c>WrappedLobbyService.GetLobbyAsync</c> when a lobby read
        /// deserialises against a stale local cache. Same root cause as
        /// <see cref="IsBenignLobbyPatcherError"/>, surfacing on the read path
        /// instead of the WebSocket-delta path: the HTTP GET succeeds, then the
        /// SDK throws while parsing the response. Self-corrects on the next
        /// refresh tick once the cache reconciles.
        ///
        /// <para>
        /// <b>Discriminator: <see cref="SessionException.Error"/> ==
        /// <see cref="SessionError.Unknown"/></b> - NOT the message string. The
        /// SDK surfaces this single defect through a moving set of inner-exception
        /// messages ("Object reference not set…", "Index was out of range…",
        /// "Index must be within the bounds of the List…", and likely more), all
        /// wrapped in a <c>SessionException</c> whose structured
        /// <c>Error</c> is <c>Unknown</c> (visible as <c>[Error: Unknown]</c> in
        /// the log). Chasing message strings was whack-a-mole - three variants
        /// appeared across three MPPM restarts. The structured <c>Error</c> is the
        /// stable signal: a genuinely actionable <c>SessionException</c> carries a
        /// specific reason (<c>SessionNotFound</c>, <c>RateLimited</c>, …), which
        /// the <c>[definite]</c> / rate-limit branches handle *before* this check
        /// runs; only the unclassifiable SDK-internal failures land on
        /// <c>Unknown</c>, and for those "log-silent, retry next tick" is already
        /// the correct (and only) recovery.
        /// </para>
        ///
        /// <para>
        /// Stack is deliberately NOT used: <see cref="Exception.StackTrace"/> is
        /// unreliable after the exception crosses several async <c>SetException</c>
        /// boundaries (UniTask + Task continuations) before our catch - the call
        /// stack in the Unity console is Unity's *captured* stack, not the
        /// exception object's own string. An earlier stack-substring match
        /// silently failed for exactly this reason.
        /// </para>
        ///
        /// <para>
        /// <see cref="LobbyPropertyWriter.SaveWithRetryAsync"/> handles the same
        /// defect on the write path via a message filter (it does not have a
        /// structured <c>Error</c> to inspect at that callsite).
        /// See <c>Docs/PresenceSystem/BUGS.md</c> B1 (write/delta-path symptoms)
        /// and B6 (read-path symptom) for the full SDK-defect characterization,
        /// and <c>Docs/PartySystem/MPPM_SESSION_LOG.md</c> Session 1 finding #2
        /// for the discovery + the message→structured-Error pivot.
        /// </para>
        /// </summary>
        private static bool IsBenignSdkStaleIndexError(Exception e)
        {
            for (var current = e; current != null; current = current.InnerException)
            {
                // Structured match: SessionException with Error == Unknown.
                // ToString() compare avoids pinning the exact enum member spelling
                // across SDK versions; SessionError.Unknown is the documented
                // "unclassified" reason and the common factor across every observed
                // stale-index message variant.
                if (current is SessionException se &&
                    string.Equals(se.Error.ToString(), "Unknown", StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

    }
}
