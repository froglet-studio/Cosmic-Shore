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

        private const string DISPLAY_NAME_KEY        = PartyLobbyKeys.DisplayName;
        private const string AVATAR_ID_KEY           = PartyLobbyKeys.AvatarId;
        private const string PARTY_COUNT_KEY         = PartyLobbyKeys.PartyCount;
        private const string PARTY_MAX_KEY           = PartyLobbyKeys.PartyMax;
        private const string MATCH_NAME_KEY          = PartyLobbyKeys.MatchName;
        private const string INVITE_PAYLOADS_KEY     = PartyLobbyKeys.InvitePayloads;
        private const string JOINED_PARTY_KEY        = PartyLobbyKeys.JoinedParty;
        private const string ACCEPTED_INVITE_KEY     = PartyLobbyKeys.AcceptedInvite;
        private const string PRESENCE_STATE_KEY      = PartyLobbyKeys.PresenceState;
        private const string PENDING_SESSION_ID      = PartyLobbyKeys.PendingSessionId;

        private const float OUTGOING_INVITE_TIMEOUT_SECONDS  = 10f;
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
        /// How long the whole <see cref="Update"/> loop stays parked after UGS
        /// returns a 429.
        ///
        /// <para>
        /// Previously computed as <c>refreshIntervalSeconds * 2</c>, which
        /// coupled two unrelated things: how often we poll and how long we
        /// retreat when told to stop. The value here (6 s) is exactly what that
        /// expression produced with the shipped prefab value
        /// (<c>refreshIntervalSeconds: 3</c>), so this is a rename of a number,
        /// not a retune - but it means wiring the cadence field to the scheduler
        /// can no longer silently halve the backoff as a side effect.
        /// </para>
        /// </summary>
        private const float RATE_LIMIT_BACKOFF_SECONDS = 6f;

        /// <summary>
        /// Extra settling time granted to a freshly created or joined party
        /// session before the first member-sync refresh touches it, ON TOP of the
        /// normal refresh interval (see <see cref="LobbyRefreshScheduler.ResetDeferred"/>).
        /// Guards against stale-session 404s against a session UGS has only just
        /// provisioned.
        ///
        /// <para>
        /// Also previously passed as <c>refreshIntervalSeconds</c>; 3 s preserves
        /// the shipped prefab value. Same rationale as
        /// <see cref="RATE_LIMIT_BACKOFF_SECONDS"/> - the settle exists because
        /// UGS needs a beat, which has nothing to do with our poll rate.
        /// </para>
        /// </summary>
        private const float POST_SESSION_SETTLE_SECONDS = 3f;

        /// <summary>
        /// Minimum seconds between benign-SDK-skip diagnostic lines. The whole
        /// point of classifying these faults as benign was to stop the console
        /// spam (<c>Docs/PresenceSystem/BUGS.md</c> B1 fired ~every 3 s in solo
        /// Menu_Main), so the accounting must stay quiet enough not to reintroduce
        /// it while still making a sustained stall visible. One line per 10 s
        /// carries the running counts, so a burst is legible from a single line.
        /// </summary>
        private const float BENIGN_SKIP_LOG_INTERVAL_SECONDS = 10f;

        /// <summary>
        /// After session creation, suppress <see cref="RefreshPartyMembersAsync"/>
        /// for this many seconds.  A freshly-provisioned session can transiently
        /// fail RefreshAsync; nulling the session in response would cause
        /// <see cref="AcceptanceSignalService.ScanForSignals"/> to recreate it on
        /// the next tick, kicking any joining client.
        /// </summary>
        private const float SESSION_CREATION_GRACE_PERIOD_SECONDS = 4f;

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

        /// <summary>
        /// Tracks what this player is DOING, as far as every other client is
        /// concerned - orthogonal to <see cref="_stateMachine"/>, which tracks
        /// which Relay session we are in. You are simultaneously
        /// <c>PartyState.InParty</c> and <c>PresenceState.InMatch</c>.
        ///
        /// <para>
        /// Its <c>Announced → Present</c> transition, driven by
        /// <c>GameDataSO.OnClientReady</c>, is the "this player is in the world"
        /// broadcast: it publishes a <c>presenceState</c> property that every
        /// peer reads, so a player is only rendered as a real, interactable row
        /// once their lava-lamp vessel actually exists.
        /// </para>
        /// </summary>
        private readonly PresenceStateMachine _presence = new();

        /// <summary>
        /// Read-only view of the presence lifecycle for UI and other subsystems.
        /// Subscribe to <c>OnStateChanged</c> to react to this player entering
        /// the world, entering a match, or starting to reconnect.
        /// </summary>
        public PresenceStateMachine Presence => _presence;

        private bool   _joining;
        private bool   _profileSubscribed;
        private bool   _gameLaunchSubscribed;
        private bool   _clientReadySubscribed;
        /// <summary>
        /// Latched true once the local Menu_Main vessel is known to exist -
        /// either from the OnClientReady signal or from ReconcilePresenceState's
        /// direct probe. Separate from the state machine so a signal that arrives
        /// in a state which cannot accept it is remembered rather than discarded.
        /// </summary>
        private bool   _localVesselReady;
        private bool   _partyLeaveSubscribed;
        private bool   _handlingDefiniteSessionGone;
        /// <summary>
        /// True between an app-pause presence leave and the matching resume
        /// rejoin. Tracked separately from the state machine because the pause
        /// leave deliberately does NOT mark us Disconnected - see HandleAppPaused.
        /// </summary>
        private bool   _leftPresenceForBackground;

        /// <summary>
        /// Consecutive lobby reads a player must be absent from before we drop
        /// their online row. Mirrors
        /// <c>PartyMemberService.MISSED_READS_BEFORE_REMOVAL</c>; see the comment
        /// at the removal loop in <see cref="RefreshOnlinePlayersDiff"/>.
        /// </summary>
        private const int ONLINE_MISSED_READS_BEFORE_REMOVAL = 2;

        /// <summary>Absent-read strike counts keyed by PlayerId, cleared on sight.</summary>
        private readonly Dictionary<string, int> _onlineMissedReads = new();

        /// <summary>Reused buffer for draining departed ids - avoids a per-tick allocation.</summary>
        private readonly List<string> _departedScratch = new();
        private float  _rateLimitBackoffUntil;
        private float  _nextForcedRefreshAllowed;
        private float  _nextConvergeAllowed;
        private int    _consecutiveRefreshErrors;

        // Benign-SDK-fault accounting. A benign-classified throw is silenced by
        // design (the SDK self-corrects next tick), but it is NOT free: it aborts
        // the whole refresh cycle, so the online-player diff, invite scan, member
        // sync and presence publish for that tick never run. Counting them is the
        // difference between "the roster is converging" and "the roster has been
        // frozen for 40 s and nothing said so". See Docs/PresenceSystem/BUGS.md
        // B1/B6 and Docs/PresenceSystem/PRESENCE_SYNC_PLAN.md RC-2.
        private int    _benignPresenceSkips;
        private int    _benignPartySessionSkips;
        private float  _nextBenignSkipLogAllowed;

        /// <summary>
        /// Set by <see cref="HandleRosterChangedPublish"/> when the local party
        /// roster moves; drained by <see cref="Update"/> into a presence
        /// re-publish. See that handler for why this cannot be a direct call.
        /// </summary>
        private bool   _rosterPublishRequested;

        private int    _publishedPartyCount = -1;
        private string _publishedMatchName  = "<UNSET>";
        // Identity (displayName/avatarId) rides the same change-gated per-tick
        // publish so a rename is GUARANTEED to reach the lobby even when the
        // event-driven RepublishLocalIdentityAsync no-ops (lobby ref null during
        // a reconnect/converge window) or its save fails - the tick reconciles.
        private string _publishedDisplayName = "<UNSET>";
        private int    _publishedAvatarId    = int.MinValue;
        private int    _publishedPresenceState = int.MinValue;
        /// <summary>Lobby id the trackers above describe; a change invalidates them all.</summary>
        private string _publishedToLobbyId;

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

        /// <summary>
        /// Refresh ticks aborted by a benign-classified SDK fault on the PRESENCE
        /// lobby read. Each one is a lost online-player diff, invite scan and
        /// presence publish. Read by the NetDiag overlay; a value that climbs
        /// while the roster looks stale is the confirmation that the SDK
        /// stale-index defect (<c>Docs/PresenceSystem/BUGS.md</c> B1/B6), not our
        /// own logic, is holding the list back.
        /// </summary>
        public int BenignPresenceSkips => _benignPresenceSkips;

        /// <summary>
        /// Refresh ticks aborted by a benign-classified SDK fault on the PARTY
        /// SESSION read. Each one is a lost <c>PartyMemberService.SyncFromSession</c>,
        /// i.e. a party roster that did not converge this tick.
        /// </summary>
        public int BenignPartySessionSkips => _benignPartySessionSkips;

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

            // The scheduler is DI-constructed by a factory with no access to this
            // component's serialized config, so it starts on the factory's
            // placeholder interval. Hand it the real inspector value here - until
            // this line existed, refreshIntervalSeconds did not affect the refresh
            // rate at all (the factory's hardcoded 1.5f won, while the shipped
            // prefab said 3).
            _scheduler.DefaultInterval = refreshIntervalSeconds;

            // Leave the presence lobby explicitly on the way out. Until this
            // existed the only leave was an async void OnDestroy awaiting after
            // teardown had begun - best-effort at best, usually incomplete - so a
            // quitting or backgrounded player stayed in every peer's online list
            // until UGS reaped them. See RC-5 in Docs/PresenceSystem/PRESENCE_SYNC_PLAN.md.
            ApplicationLifecycleManager.OnAppQuitRequested += HandleAppQuitRequested;
            ApplicationLifecycleManager.OnAppPaused        += HandleAppPaused;

            // State-preserving lobby rejoin (B4): every lobby (re)join - initial,
            // reconnect, and the periodic converge migration - publishes the LIVE
            // stateful property values instead of wiping them to empty. HCS stays
            // the single writer of these values; the lobby service only carries
            // them across the rejoin.
            _lobbyService.LivePropertySource = BuildLivePresenceProperties;

            // Our published partyCount is a function of the roster, so the roster
            // moving is exactly when it needs re-publishing. Before this, the new
            // value reached the wire only on the next SUCCESSFUL poll tick - and
            // the presence read that gates that tick is voided ~12% of the time
            // by the SDK stale-index defect, so peers could render a stale party
            // size for many seconds after the party had actually changed.
            if (connectionData.OnPartyRosterChanged != null)
            {
                connectionData.OnPartyRosterChanged.OnRaised += HandleRosterChangedPublish;
            }
            else
            {
                // Loud, per the locked "every null guard logs Debug.LogError with
                // field name and suspected cause" rule - and this one earns it.
                // OnPartyRosterChanged is the ONLY repaint signal FriendsListPanel
                // and ArcadeLobbyList still listen to for party changes; the
                // per-member subscriptions they used to repaint from were removed
                // when this channel replaced them. So an unwired asset does not
                // degrade the party UI, it freezes it - and silently, because a
                // SOAP raise on a null reference is a no-op.
                Debug.LogError(
                    "[HostConnectionService] connectionData.OnPartyRosterChanged is NOT WIRED on " +
                    $"'{connectionData.name}'. Party slot counts and the friends-list party rows " +
                    "will never repaint, and this player's partyCount will never be republished " +
                    "on a roster change. Assign Event_PartyRosterChanged.asset to the " +
                    "OnPartyRosterChanged field on the HostConnectionData asset.");
            }

            HandleSignedInEvent();
        }

        /// <summary>
        /// Requests a presence re-publish because the local party roster moved.
        ///
        /// <para>
        /// Sets a flag rather than publishing inline, and that is not incidental:
        /// the roster raise fires from inside <see cref="RefreshAsync"/> and
        /// <see cref="ScanPresenceForJoinedPartyMembers"/>, both of which are
        /// already holding <c>_lobbyMutex</c>.
        /// <see cref="PublishPresenceImmediateAsync"/> waits on that same mutex,
        /// so calling it here would deadlock the refresh cycle against itself.
        /// <see cref="Update"/> drains the flag once the mutex is free.
        /// </para>
        ///
        /// <para>
        /// The flag also debounces: several roster changes landing in one frame
        /// collapse into a single publish, and
        /// <see cref="PublishPartyStateIfChangedAsync"/>'s own change-gate makes
        /// a publish whose values did not actually move free.
        /// </para>
        /// </summary>
        private void HandleRosterChangedPublish() => _rosterPublishRequested = true;

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

            // presenceState is stateful and MUST survive a rejoin, exactly like
            // the three keys above. Omitting it meant a converge migration
            // rebuilt our player record from BuildLocalPlayerProperties - which
            // does not know about presence - and silently dropped the key, while
            // the change-gate in PublishPartyStateIfChangedAsync still believed
            // the published value matched and never re-sent it.
            live[PRESENCE_STATE_KEY] = ((int)_presence.CurrentState).ToString();

            return live;
        }

        void Update()
        {
            // Accumulate FIRST, unconditionally, before any eligibility gate.
            // The gates below describe when a refresh may be ISSUED, not whether
            // time passed. Accumulating after them made the scheduler measure
            // eligible time instead of wall time, so any stretch spent holding
            // the lobby mutex - i.e. any in-flight property write, which is
            // exactly when party state is changing - froze the clock and pushed
            // the next poll out by a further full interval past the write. See
            // LobbyRefreshScheduler.Accumulate.
            _scheduler.Accumulate(Time.unscaledDeltaTime);

            // Raise any pending roster-changed signal, on the main thread, before
            // EVERY gate below - including the menu-scene check.
            //
            // The raise is deferred rather than issued at the mutation site because
            // this event's listeners touch UnityEngine.Object (FriendsListPanel
            // Instantiates rows; FriendsInitializer does an op_Equality null check
            // on a persistent GameObject) while its raise sites are NOT all
            // main-thread - PartyMemberService.SeedLocalPlayer runs from
            // ApplyPostLobbyJoinState, immediately after an await of
            // JoinOrCreateAsync whose fallback path gives no thread guarantee.
            // Raising inline there threw EnsureRunningOnMainThread. See
            // SoapPartyEventBus.RequestPartyRosterChanged.
            //
            // Unconditional and first: a roster change must never be stranded
            // unraised by a gate, and draining here means the publish request it
            // produces is still picked up later in this same frame.
            _eventBus?.FlushPartyRosterChanged();

            if (!IsOnMenuScene()) return;

            // ── Party-session push drain ─────────────────────────────────────
            // Deliberately ABOVE the presence gates below, and it is not an
            // ordering accident:
            //
            //   * The party session and the presence lobby are independent
            //     layers (the locked two-level design). Gating a party-roster
            //     update on presence-lobby membership would let a discovery-layer
            //     problem freeze the party roster.
            //   * It makes NO UGS call, so neither the lobby mutex nor the
            //     rate-limit backoff applies. Gating it on the mutex would stall
            //     the roster during an in-flight property write - i.e. exactly
            //     when party state is changing, the same trap that made the
            //     scheduler measure eligible time instead of wall time.
            //
            // Fires on the frame UGS pushes the event, so a join is visible
            // locally in one frame instead of waiting on a poll read that is
            // voided ~32% of the time.
            if (_partySessionService != null &&
                _partySessionService.ActiveSession != null &&
                _partySessionService.TryConsumeRosterDirty())
                DrainPartySessionPushAsync().Forget();

            if (!IsInPresenceLobby) return;
            if (_lobbyMutex.CurrentCount == 0) return;                   // someone is already inside the mutex
            if (Time.unscaledTime < _rateLimitBackoffUntil) return;

            ExpireOutgoingInvites();
            ReconcilePresenceState();

            // UGS told us we are no longer in the lobby (RemovedFromSession /
            // Deleted). Unlike the consecutive-error watchdog below in
            // RefreshAsync, this is not a heuristic - it is the service stating
            // the fact - so recover immediately instead of waiting for three
            // refreshes to fail against a lobby that no longer exists.
            if (_lobbyService.ConsumeMembershipLost())
            {
                HandlePresenceMembershipLostAsync().Forget();
                return;
            }

            // The roster moved since the last frame - push our new partyCount out
            // now rather than waiting for a poll tick that may be voided. Drained
            // here because the raise fires with the lobby mutex held (see
            // HandleRosterChangedPublish); by this line the gates above have
            // already established the mutex is free.
            if (_rosterPublishRequested)
            {
                _rosterPublishRequested = false;
                PublishPresenceImmediateAsync().Forget();
                return;
            }

            // Push: a roster-affecting event arrived since the last frame. Turns
            // "discovered on the next tick" into "delivered when it happens".
            bool pushed = _lobbyService.ConsumeRosterDirty();

            // Consume only once eligible, so a tick blocked by the gates keeps its
            // accumulated time and fires as soon as it can rather than restarting
            // the interval. Cannot burst - the accumulator zeroes on consume.
            bool polled = _scheduler.TryConsumeFire();

            // A push tick reads nothing from the network - the SDK has already
            // patched the roster in memory - so it costs one diff and no UGS
            // call. Only a poll tick fetches.
            //
            // The scheduler is deliberately NOT reset on a push. An earlier
            // revision did that to avoid "a redundant read right behind the
            // push", but with the push tick no longer reading there is nothing
            // redundant to avoid - and suppressing the safety poll because push
            // fired is backwards: the poll exists precisely to catch what push
            // misses, so a steady stream of pushes must not be able to starve it.
            if (pushed || polled)
                RefreshAsync(fetchFromServer: polled).Forget();
        }

        /// <summary>
        /// Recovers from a definite presence-lobby membership loss pushed by UGS
        /// (<c>RemovedFromSession</c> or <c>Deleted</c>).
        ///
        /// <para>
        /// Deliberately does NOT touch the party session, NetworkManager or any
        /// vessel: the presence lobby is the discovery layer and is independent
        /// of the Relay-backed party. Losing it means peers stop appearing in the
        /// online list, not that the party broke. This is the same reason
        /// <see cref="ApplyPostLobbyJoinState"/> must not clear a live party
        /// roster on rejoin.
        /// </para>
        /// </summary>
        private async UniTaskVoid HandlePresenceMembershipLostAsync()
        {
            Debug.LogWarning("[HostConnectionService] Presence lobby membership lost (UGS push) - rejoining.");
            CSDebug.Log($"[HostConnectionService] NetDiag: {NetworkDiagnostics.GetSnapshot()}");

            // Reconnecting is only legal from Inviting / JoiningParty / InParty,
            // so capture whether we actually entered it - this is the first call
            // site in the codebase to READ TryTransition's return value, and it is
            // what stops the recovery below from firing an illegal
            // <whatever> → InParty when we never left the original state.
            bool enteredReconnecting = _stateMachine.TryTransition(PartyState.Reconnecting);
            _presence.TryTransition(PresenceState.Recovering);

            _lobbyService.ForceReset();
            await _lobbyService.JoinOrCreateAsync(presenceLobbyMaxPlayers);
            ApplyPostLobbyJoinState();

            // Leave Reconnecting rather than stranding the machine there (it has
            // no other exit today). The presence lobby is independent of the
            // Relay party, so if the session is still live we are fully
            // recovered. If it is not, surface the loss and let the explicit
            // user-driven retry recreate it - recreating a session from this
            // background path would Shutdown() NetworkManager and respawn every
            // menu vessel, which is exactly why the consecutive-error reconnect
            // path in RefreshAsync does not do it either.
            if (!enteredReconnecting) return;

            if (_partySessionService.ActiveSession != null)
                _stateMachine.TryTransition(PartyState.InParty);
            else
                _eventBus.RaiseHostConnectionLost();
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
            ApplicationLifecycleManager.OnAppQuitRequested -= HandleAppQuitRequested;
            ApplicationLifecycleManager.OnAppPaused        -= HandleAppPaused;
            if (authenticationDataVariable != null)
                authenticationDataVariable.Value.OnSignedIn.OnRaised -= HandleSignedInEvent;
            UnsubscribeFromProfileChanges();
            UnsubscribeFromGameLaunch();
            UnsubscribeFromVesselReady();
            UnsubscribeFromPartySessionEvents();

            // HostConnectionDataSO is a project asset: it outlives every scene and
            // every instance of this service, so a leaked handler here would keep
            // being invoked on a destroyed component for the rest of the session.
            if (connectionData != null && connectionData.OnPartyRosterChanged != null)
                connectionData.OnPartyRosterChanged.OnRaised -= HandleRosterChangedPublish;

            if (bootStatusRetryRequestedEvent != null)
                bootStatusRetryRequestedEvent.OnRaised -= HandleBootStatusRetryRequested;
            else
                Debug.LogError(
                    "[HostConnectionService] OnDestroy: bootStatusRetryRequestedEvent is null - " +
                    "SOAP event asset not wired on the prefab. Boot-status retry would not have functioned.");

            // Backstop only. The real leave happens on OnAppQuitRequested /
            // OnAppPaused, while the app is still alive and can actually await a
            // UGS round-trip. This await is not reliable during teardown - which
            // is exactly why it was never sufficient on its own - so it is kept
            // for the paths that skip the lifecycle hooks entirely (a scene-level
            // Destroy, a duplicate-instance teardown) and skipped when the
            // departure path already ran, so a normal quit does not double-leave.
            if (_lobbyService == null)
                Debug.LogError(
                    "[HostConnectionService] OnDestroy: _lobbyService is null - Reflex DI never populated it. " +
                    "Skipping presence-lobby leave; other users may see this player online for ~30s until UGS reaps the entry.");
            else if (_lobbyService.ActiveLobby != null)
                await _lobbyService.LeaveAsync();

            if (_propertyWriter != null)
            {
                _propertyWriter.LobbyMutex?.Dispose();
                _propertyWriter.SessionCreationMutex?.Dispose();
            }
            else
            {
                Debug.LogError(
                    "[HostConnectionService] OnDestroy: _propertyWriter is null - Reflex DI never populated it. " +
                    "Skipping mutex disposal.");
            }

            Instance = null;
        }

        private void HandleBootStatusRetryRequested() => EnsurePartySessionAsync().Forget();

        // ╔═══════════════════════════════════════════════════════════════════╗
        // ║  Departure - explicit leave on quit / background                  ║
        // ╚═══════════════════════════════════════════════════════════════════╝

        /// <summary>
        /// Hard cap on how long a departure leave may take. Must stay comfortably
        /// under <see cref="ApplicationLifecycleManager"/>'s quit-drain window,
        /// or the app quits mid-leave and we are back to the ghost-row behaviour
        /// this exists to prevent.
        /// </summary>
        private const int DEPARTURE_LEAVE_TIMEOUT_MS = 1200;

        /// <summary>
        /// The user asked to quit and the quit has been deferred for a short
        /// window. Leave BOTH UGS sessions: we are not coming back, so the
        /// presence lobby row and the Relay party allocation should both go now
        /// rather than waiting on the service's reap timer.
        ///
        /// <para>
        /// This is the one case where a sub-second removal on every peer is
        /// actually achievable. UGS fans an explicit leave out over the WebSocket
        /// immediately, so peers see the row disappear on their next drained push
        /// - as opposed to a hard kill, where nothing can beat the service-side
        /// reap. See PRESENCE_SYNC_PLAN.md § 6.
        /// </para>
        /// </summary>
        private void HandleAppQuitRequested() =>
            LeaveForDepartureAsync(leaveParty: true, markDisconnected: true).Forget();

        /// <summary>
        /// Mobile background / foreground.
        ///
        /// <para>
        /// On pause we leave the PRESENCE LOBBY ONLY, and deliberately keep the
        /// Relay party session. A backgrounded app may never be resumed - the OS
        /// can kill it silently, which produces exactly the ghost row this fixes
        /// - but a three-second app switch must not eject the player from their
        /// party. Netcode/Relay has its own disconnect handling for that case,
        /// with a real transport underneath it; the presence lobby has nothing
        /// but the reap timer, which is why only it needs the explicit leave.
        /// </para>
        ///
        /// <para>
        /// On resume we rejoin. <c>JoinOrCreateAsync</c> is idempotent and
        /// returns early when a lobby is already held, so a spurious resume is
        /// harmless.
        /// </para>
        /// </summary>
        private void HandleAppPaused(bool paused)
        {
            if (paused)
            {
                if (!IsInPresenceLobby) return;
                _leftPresenceForBackground = true;

                // markDisconnected:false is load-bearing. IsInitialized is derived
                // from the state machine (CurrentState != Disconnected), so marking
                // Disconnected here would make the resume branch below decide we
                // were never initialized and silently never rejoin - leaving the
                // player invisible to everyone for the rest of the session.
                LeaveForDepartureAsync(leaveParty: false, markDisconnected: false).Forget();
                return;
            }

            if (!_leftPresenceForBackground) return;
            _leftPresenceForBackground = false;
            RejoinPresenceAfterResumeAsync().Forget();
        }

        private async UniTaskVoid LeaveForDepartureAsync(bool leaveParty, bool markDisconnected)
        {
            if (markDisconnected)
                _stateMachine.TryTransition(PartyState.Disconnected);

            _presence.TryTransition(PresenceState.Departing);

            var leave = leaveParty
                ? UniTask.WhenAll(_lobbyService.LeaveAsync(), _partySessionService.LeaveAsync())
                : _lobbyService.LeaveAsync();

            // Bounded: a hung UGS request must never hold the quit open past the
            // drain window, and on mobile the OS gives no guarantee we get any
            // time at all. Best-effort by design.
            await UniTask.WhenAny(leave, UniTask.Delay(DEPARTURE_LEAVE_TIMEOUT_MS, DelayType.UnscaledDeltaTime));

            _presence.TryTransition(PresenceState.Offline);
            Debug.Log($"[HostConnectionService] Departure leave complete (leaveParty={leaveParty}).");
        }

        private async UniTaskVoid RejoinPresenceAfterResumeAsync()
        {
            Debug.Log("[HostConnectionService] Resumed from background - rejoining presence lobby.");
            await _lobbyService.JoinOrCreateAsync(presenceLobbyMaxPlayers);
            ApplyPostLobbyJoinState();
        }

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
            _presence.TryTransition(PresenceState.Offline);
            connectionData.ResetRuntimeData();
            await _lobbyService.LeaveAsync();
            _eventBus.RaiseHostConnectionLost();
        }

        /// <summary>
        /// Idempotent initialization. Safe to call from both <see cref="Start"/>
        /// (auth-already-signed-in path) and <see cref="HandleSignedInEvent"/>
        /// (auth-signed-in-after-Start path) - concurrent calls collapse to one.
        ///
        /// <para>
        /// <b>The party session IS created here, eagerly.</b> Every authenticated
        /// player hosts their own solo Relay session from menu entry - the locked
        /// design in <c>Docs/PartySystem/ARCHITECTURE.md</c>. (This comment
        /// previously described the retired LAZY / on-first-invite model, eight
        /// lines above the code doing the opposite. Do not reintroduce lazy
        /// creation: the shutdown-and-recreate cascade it caused is the root of
        /// every recurring party-invite bug.)
        /// </para>
        /// </summary>
        private async UniTask EnsureInitializedAsync()
        {
            if (IsInPresenceLobby || _joining) return;
            _joining = true;
            try
            {
                SubscribeToProfileChanges();
                SubscribeToGameLaunch();
                SubscribeToVesselReady();
                SubscribeToPartySessionEvents();

                await WaitForProfileInitAsync(PROFILE_INIT_TIMEOUT_MS);
                SyncLocalIdentity();

                _presence.TryTransition(PresenceState.Joining);
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
            catch (Exception e)
            {
                // Backstop. EnsurePartySessionAsync now handles its own failures,
                // so this covers everything BEFORE it - profile wait, identity
                // sync, lobby join. Nothing on the boot path may throw into the
                // async void caller (HandleSignedInEvent): that reads as an
                // unhandled exception and leaves the player on the loading splash
                // with no error surface and no way forward.
                Debug.LogError(
                    $"[HostConnectionService] Initialization FAILED ({e.GetType().Name}): {e.Message} - " +
                    "surfacing the retry surface.");
                CSDebug.Log($"[HostConnectionService] NetDiag: class={NetworkDiagnostics.ClassifyException(e)} | {NetworkDiagnostics.GetSnapshot()}");

                _eventBus.RaiseHostConnectionLost();
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
                _scheduler.ResetDeferred(POST_SESSION_SETTLE_SECONDS);
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
                _scheduler.ResetDeferred(POST_SESSION_SETTLE_SECONDS);

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
            catch (Exception e)
            {
                // Session creation failed after PartySessionService exhausted its
                // retries. This MUST NOT propagate.
                //
                // The call chain above is EnsureInitializedAsync ->
                // HandleSignedInEvent, and both have finally-but-no-catch, so an
                // escaping exception surfaced as an unhandled async void throw and
                // simply stopped the boot: OnHostConnectionEstablished never fired,
                // the auth scene's wait for the Relay-ready signal never completed,
                // and the player sat on the loading splash indefinitely with no
                // error and no way forward. Observed with a Relay 500
                // ("Failed to create allocation").
                //
                // Raising HostConnectionLost is the recovery: BootStatusBroadcaster
                // listens for it and swaps the splash to the Retry surface, whose
                // button routes back here via bootStatusRetryRequestedEvent ->
                // HandleBootStatusRetryRequested -> EnsurePartySessionAsync.
                //
                // The state machine is deliberately LEFT in HostingParty. That is
                // exactly what makes the retry clean: IsHostingParty is false (no
                // session), so the fast-path guard lets us back in, and the
                // CurrentState check skips a redundant transition. Rolling back to
                // Disconnected would instead make IsInPresenceLobby false and send
                // a re-fired sign-in through the whole init again.
                Debug.LogError(
                    $"[HostConnectionService] Party session creation FAILED ({e.GetType().Name}): {e.Message} - " +
                    "surfacing the retry surface. The player is in the presence lobby but has no Relay session, " +
                    "so no vessel will spawn until this succeeds.");
                CSDebug.Log($"[HostConnectionService] NetDiag: class={NetworkDiagnostics.ClassifyException(e)} | {NetworkDiagnostics.GetSnapshot()}");

                _eventBus.RaiseHostConnectionLost();
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

        /// <summary>
        /// One presence reconcile cycle.
        /// </summary>
        /// <param name="fetchFromServer">
        /// <c>true</c> for a safety-poll tick: issue the network reads
        /// (<c>GetLobby</c>, and the periodic converge <c>QuerySessions</c>) before
        /// diffing. <c>false</c> for a PUSH tick: diff against the roster the SDK
        /// has already patched in memory, with no network call at all.
        ///
        /// <para>
        /// A push tick needs no fetch. Unity documents <c>PlayerJoined</c> /
        /// <c>PlayerHasLeft</c> as firing "right after the session gets updated",
        /// and <c>PlayerPropertiesChanged</c> as not firing at all "if the
        /// properties are already up to date locally" - a statement that only
        /// means anything if the SDK maintains the local copy. This repo carries
        /// its own proof: <see cref="IsBenignLobbyPatcherError"/> exists because
        /// <c>LobbyPatcher.ApplyPatchesToLobby</c> throws while patching the local
        /// lobby from a WebSocket delta. So by the time we drain the flag, the
        /// data is already here.
        /// </para>
        ///
        /// <para>
        /// Fetching anyway cost a <c>GetLobby</c> per inbound delta per client -
        /// the dominant read cost at any lobby size, since every member's property
        /// write fans out to every other member - and put a network round-trip
        /// between "the delta arrived" and "the UI updated", which defeats the
        /// point of having a push channel. Introduced in <c>8a146795</c> by routing
        /// push through the existing poll-shaped path because it was the smallest
        /// diff: correct, but not cheap.
        /// </para>
        ///
        /// <para>
        /// Safe by construction: the safety poll still fetches on its own cadence,
        /// so if the SDK ever fails to keep the local roster current the worst case
        /// degrades to poll latency rather than a stale list.
        /// </para>
        /// </param>
        private async UniTaskVoid RefreshAsync(bool fetchFromServer = true)
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
                // ── READ + DIFF ──────────────────────────────────────────────
                // Wrapped in its own try so a benign SDK fault is absorbed HERE,
                // at the read, instead of unwinding past the publish below.
                //
                // The lobby read is the first await in this block, and the
                // publish used to sit downstream of it inside ONE try - so a
                // fault the SDK self-corrects on the next tick also silently
                // dropped our own partyCount write. That read is voided ~12% of
                // poll ticks (Docs/PresenceSystem/BUGS.md, MEASURED run 2), which
                // made "my party size reaches my peers" depend on a coin flip we
                // do not control. Reading and publishing are independent
                // operations and now fail independently.
                //
                // Only the two BENIGN classifications are absorbed here.
                // Everything else - rate limit, definite, transient - propagates
                // to the outer catch with its recovery matrix untouched: those
                // mean the lobby itself is in trouble, and publishing into it is
                // pointless.
                try
                {
                    if (fetchFromServer)
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
                    // Fetch ticks only - this is a QuerySessions network call, and a
                    // burst of pushes must not turn into a burst of queries.
                    if (fetchFromServer && Time.unscaledTime >= _nextConvergeAllowed)
                    {
                        _nextConvergeAllowed = Time.unscaledTime + PRESENCE_CONVERGE_INTERVAL_SECONDS;
                        await _lobbyService.ConvergeToCanonicalAsync(presenceLobbyMaxPlayers);
                    }

                    // Diff-based update - never Clear() + re-Add() (would flicker UI).
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
                    // real session id. Gated on outgoing-invite count - no work to do if
                    // we haven't sent any invites.
                    if (_inviteService.OutgoingCount > 0)
                    {
                        string acceptingId = _acceptanceService.ScanForSignals(
                            _lobbyService.ActiveLobby,
                            connectionData.LocalPlayerId,
                            _inviteService.OutgoingTargets);

                        if (acceptingId != null)
                        {
                            // Every player hosts their own Relay session from menu entry
                            // (eager creation), so the session already exists before the
                            // invite was sent - no session creation needed here.
                            // See Docs/PartySystem/ARCHITECTURE.md (Locked design).
                            string activeSessionId = _partySessionService.ActiveSession?.Id;
                            if (string.IsNullOrEmpty(activeSessionId))
                            {
                                Debug.LogError($"[HostConnectionService] Acceptance signal from {acceptingId} but no active party session - joiner cannot connect.");
                            }
                            else
                            {
                                Debug.Log($"[HostConnectionService] Acceptance signal from {acceptingId} - joiner will connect to existing session {activeSessionId}.");
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

                    // Fetch ticks only. This refreshes the PARTY session - a
                    // different session entirely - so a presence-lobby delta is no
                    // reason to hit it. Its own push (PartySessionService subscribes
                    // PlayerLeaving) and the safety poll cover it.
                    if (fetchFromServer && _partySessionService.ActiveSession != null)
                        await RefreshPartyMembersAsync();

                    // Only a tick that actually talked to the server is evidence
                    // the connection is healthy. Clearing the counter on a push
                    // tick - which does no network I/O - would let pushes mask a
                    // genuinely failing fetch and suppress the reconnect watchdog
                    // entirely. Inside the inner try, so a voided read does not
                    // count as evidence of health either.
                    if (fetchFromServer)
                        _consecutiveRefreshErrors = 0;
                }
                catch (Exception e) when (IsBenignLobbyPatcherError(e))
                {
                    // No state change and no console warning (B1 noise suppression),
                    // but the READ is lost - account for it. The publish below still
                    // runs. See RecordBenignRefreshSkip.
                    RecordBenignRefreshSkip(ref _benignPresenceSkips, e, "presence", "LobbyPatcher");
                }
                catch (Exception e) when (!IsRateLimitException(e) && IsBenignSdkStaleIndexError(e))
                {
                    // Same SDK stale-index defect, read-path surface.
                    //
                    // The !IsRateLimitException guard preserves the documented
                    // branch ordering now that these run as filters rather than
                    // in an if/else chain: IsBenignSdkStaleIndexError matches ANY
                    // SessionException whose Error is Unknown, and a wrapped 429
                    // can present in exactly that shape. Absorbing one here would
                    // swallow a throttle without arming the backoff - the bug the
                    // original ordering comment exists to prevent. Excluded, it
                    // falls through to the outer catch's rate-limit branch.
                    RecordBenignRefreshSkip(ref _benignPresenceSkips, e, "presence", "SdkStaleIndex");
                }

                // ── PUBLISH ──────────────────────────────────────────────────
                // Deliberately OUTSIDE the read's try. Change-gated, so free on a
                // tick where nothing moved - and reached even on a tick whose read
                // was voided, which is the entire point of the split above.
                await PublishPartyStateIfChangedAsync();
            }
            catch (Exception e)
            {
                // [rate-limit] MUST be tested before the benign branches.
                // IsBenignSdkStaleIndexError matches ANY SessionException whose
                // Error is Unknown, and a 429 can arrive wrapped in exactly that
                // shape - so with benign first, being throttled was silently
                // classified as harmless SDK noise and no backoff was ever armed,
                // leaving us hammering the same endpoint that just refused us.
                // The two are otherwise disjoint (a stale-index fault carries no
                // 429 and no "Too Many Requests"), so this reorder cannot steal
                // anything from the benign branches.
                if (IsRateLimitException(e))
                {
                    _rateLimitBackoffUntil = Time.unscaledTime + RATE_LIMIT_BACKOFF_SECONDS;
                    Debug.LogWarning("[HostConnectionService] Rate limited during refresh - backing off");
                }
                // Benign SDK noise reaching the OUTER catch. The read path no
                // longer gets here - its own inner try absorbs both benign
                // classifications so they cannot void the publish - so these
                // branches now cover the publish itself and anything else that
                // is ever added to the outer scope. Kept rather than deleted
                // because PublishPartyStateIfChangedAsync swallowing its own
                // exceptions is an implementation detail, and losing the
                // classification if that changes would put SDK noise back into
                // the reconnect watchdog.
                else if (IsBenignLobbyPatcherError(e))
                {
                    RecordBenignRefreshSkip(ref _benignPresenceSkips, e, "presence", "LobbyPatcher");
                }
                else if (IsBenignSdkStaleIndexError(e))
                {
                    RecordBenignRefreshSkip(ref _benignPresenceSkips, e, "presence", "SdkStaleIndex");
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

                    // The watchdog measures CONNECTION health, so only a tick that
                    // actually talked to the server may feed it. A push tick does
                    // no network read; letting it increment while only fetch ticks
                    // clear the counter would make this a one-way ratchet, and a
                    // burst of pushes during an invite handshake could trip a
                    // reconnect on a perfectly healthy connection.
                    if (!fetchFromServer) return;

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

        /// <summary>
        /// Records a refresh tick that was aborted by a benign-classified SDK
        /// fault, and emits at most one diagnostic line per
        /// <see cref="BENIGN_SKIP_LOG_INTERVAL_SECONDS"/>.
        ///
        /// <para>
        /// <b>Why this exists.</b> Both benign branches used to be literally
        /// empty - no log, no counter, no state change - which made a voided tick
        /// byte-for-byte indistinguishable from a healthy one. Because the lobby
        /// read is the FIRST await in <see cref="RefreshAsync"/>, a benign throw
        /// there skipped the online-player diff, the invite scan, the acceptance
        /// scan, the joined-member scan, the party-member sync AND the presence
        /// publish for that entire tick. A persistent SDK fault therefore froze
        /// every roster in the game while the console stayed clean. The counters
        /// are the signal that tells <c>LobbyMembershipMonitor</c> (see
        /// <c>Docs/PresenceSystem/REFACTOR.md</c>) whether the reconnect watchdog
        /// is escalating on real membership loss or on SDK noise - the data its
        /// "wait for NetDiag data" prerequisite was blocked on.
        /// </para>
        ///
        /// <para>
        /// <b>The presence publish is no longer among the casualties.</b> It sits
        /// outside the read's try, so a voided presence READ costs the reads and
        /// nothing else - our own <c>partyCount</c> still reaches the lobby. The
        /// log line still names the whole set because a voided read genuinely
        /// does lose all of them; only the publish was pulled out.
        /// </para>
        ///
        /// <para>
        /// Deliberately <see cref="CSDebug"/>, not <see cref="Debug.LogWarning"/>:
        /// CSDebug.Log is compiled out entirely in release builds, and B1's whole
        /// point was noise suppression. This restores observability without
        /// restoring the spam.
        /// </para>
        /// </summary>
        /// <param name="counter">The per-path counter to increment.</param>
        /// <param name="e">The classified exception (logged via NetDiag only).</param>
        /// <param name="readPath">Which read aborted - "presence" or "party-session".</param>
        /// <param name="defect">Which benign classifier matched.</param>
        private void RecordBenignRefreshSkip(ref int counter, Exception e, string readPath, string defect)
        {
            counter++;

            if (Time.unscaledTime < _nextBenignSkipLogAllowed) return;
            _nextBenignSkipLogAllowed = Time.unscaledTime + BENIGN_SKIP_LOG_INTERVAL_SECONDS;

            // Stackless: this fires on a timer from deep inside an async chain, so
            // Unity's attached stack is ~100 lines of UGS/UniTask plumbing per
            // occurrence - identical every time, and it buries the counters that
            // are the actual signal.
            CSDebug.LogNoStack(
                $"[HostConnectionService] Benign SDK fault on the {readPath} read - READS VOIDED this tick " +
                $"(roster diff / invite scan / member sync skipped; the presence publish still ran). " +
                $"defect={defect} | skips: presence={_benignPresenceSkips}, partySession={_benignPartySessionSkips} | " +
                $"NetDiag: class={NetworkDiagnostics.ClassifyException(e)} | {NetworkDiagnostics.GetSnapshot()}");
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
            // Authoritative departures first. UGS named these players in a
            // PlayerLeaving / PlayerHasLeft push, so there is nothing to
            // corroborate - evict on the spot rather than making an explicit,
            // instant leave wait out the two-strike rule meant for AMBIGUOUS
            // absences. This is what makes a quitting player disappear from
            // every peer in well under a second instead of after the UGS reap.
            _departedScratch.Clear();
            if (_lobbyService.TryConsumeDepartedPlayerIds(_departedScratch))
            {
                foreach (var departedId in _departedScratch)
                {
                    _onlineMissedReads.Remove(departedId);

                    for (int i = connectionData.OnlinePlayers.Count - 1; i >= 0; i--)
                    {
                        if (connectionData.OnlinePlayers[i].PlayerId != departedId) continue;
                        Debug.Log($"[HostConnectionService] Player {departedId} left (UGS push) - removing immediately.");
                        connectionData.OnlinePlayers.RemoveAt(i);
                    }

                    // Their invite, if any, is dead with them.
                    if (_inviteService.OutgoingCount > 0)
                        _ = ClearOutgoingInviteIfPresentAsync(departedId, "presence-leave-push");
                }
            }

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
                    // Delegated to the struct so the field list lives next to the
                    // fields. Inlined here, it silently went stale the moment
                    // PresenceState was added: a peer's row was created while they
                    // were still Announced, they published Present a beat later,
                    // this comparison did not know about the field, reported
                    // "unchanged", and the row was never replaced - so they
                    // rendered as CONNECTING… on every peer forever.
                    bool changed = !existing.HasSameDisplayDataAs(playerData);

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

            // Removal needs corroboration. A lobby read can come back short
            // without anyone having left - the UGS stale-cache defect
            // (Docs/PresenceSystem/BUGS.md B1/B6), or a ConvergeToCanonicalAsync
            // migration swapping _activeLobby mid-cycle so we diff against a
            // lobby we have only just joined. Acting on a single short read
            // emptied the whole online panel and then refilled it on the next
            // tick, which is precisely the "rows flicker / wrong info" symptom.
            // A genuine graceful leave is not delayed by this: it arrives as an
            // ISession.PlayerLeaving push, which drives an immediate re-read.
            for (int i = connectionData.OnlinePlayers.Count - 1; i >= 0; i--)
            {
                string id = connectionData.OnlinePlayers[i].PlayerId;
                if (freshPlayerIds.Contains(id))
                {
                    _onlineMissedReads.Remove(id);
                    continue;
                }

                _onlineMissedReads.TryGetValue(id, out int misses);
                misses++;
                _onlineMissedReads[id] = misses;

                if (misses < ONLINE_MISSED_READS_BEFORE_REMOVAL) continue;

                _onlineMissedReads.Remove(id);
                connectionData.OnlinePlayers.RemoveAt(i);
            }

            // Departed players with outstanding invites - free the slot now.
            if (_inviteService.OutgoingCount == 0) return;

            List<string> departed = null;
            foreach (var targetId in _inviteService.OutgoingTargets)
            {
                if (freshPlayerIds.Contains(targetId)) continue;

                // Same corroboration rule, expressed via the roster rather than
                // the strike counter: the loop above DELETES a player's counter
                // entry at the moment it evicts them, so reading the counter here
                // would find nothing precisely when the player has genuinely
                // gone. "Absent from this read AND no longer in OnlinePlayers"
                // means the strikes were served.
                if (ContainsOnlinePlayer(targetId)) continue;

                departed ??= new List<string>();
                departed.Add(targetId);
            }
            if (departed != null)
            {
                foreach (var id in departed)
                    _ = ClearOutgoingInviteIfPresentAsync(id, "presence-leave");
            }
        }

        private bool ContainsOnlinePlayer(string playerId)
        {
            if (connectionData.OnlinePlayers == null) return false;
            foreach (var p in connectionData.OnlinePlayers)
                if (p.PlayerId == playerId) return true;
            return false;
        }

        private PartyPlayerData ReadOnlinePlayerData(IReadOnlyPlayer p)
        {
            string displayName = "Unknown Pilot";
            int    avatarId    = 0;
            int    partyCount  = 0;
            int    partyMax    = 0;
            string matchName   = string.Empty;
            // Absent means "unknown, assume in-world" - a peer on a build from
            // before this property existed publishes nothing, and defaulting to 0
            // (Offline) would make every such player invisible.
            int    presenceState = PartyPlayerData.PRESENCE_PRESENT;

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
            if (p.Properties.TryGetValue(PRESENCE_STATE_KEY, out var ps) &&
                !string.IsNullOrEmpty(ps.Value) &&
                int.TryParse(ps.Value, out int parsedPs))
                presenceState = parsedPs;

            return new PartyPlayerData(
                p.Id, displayName, avatarId, partyCount, partyMax, matchName, presenceState);
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

            // One coalesced repaint for the whole scan, after the roster has
            // settled - this is the second of the two mutation sites that bypass
            // PartyMemberService (the other is HostConnectionDataSO.RemovePartyMember),
            // so the raise has to happen here or a presence-detected join would
            // be the one roster change that never updated a count.
            if (joinedPlayerIds.Count > 0)
                _eventBus.RequestPartyRosterChanged();

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
                // [rate-limit] UGS throttled us - back off, keep ActiveSession.
                // Tested BEFORE the benign branches: IsBenignSdkStaleIndexError
                // matches any SessionException with Error == Unknown, which a
                // wrapped 429 can present as, and swallowing it there armed no
                // backoff. The two are disjoint in practice, so nothing is stolen
                // from benign by running this first.
                if (IsRateLimitException(e))
                {
                    Debug.LogWarning($"[HostConnectionService] Party session refresh rate-limited - backing off");
                    _rateLimitBackoffUntil = Time.unscaledTime + RATE_LIMIT_BACKOFF_SECONDS;
                    return;
                }

                // [benign] LobbyPatcher stale-index ArgumentOutOfRangeException -
                // known harmless SDK noise, self-corrects on the next tick.
                if (IsBenignLobbyPatcherError(e))
                {
                    RecordBenignRefreshSkip(ref _benignPartySessionSkips, e, "party-session", "LobbyPatcher");
                    return;
                }

                // [benign] WrappedLobbyService NRE on lobby refresh - same SDK
                // stale-index family as the LobbyPatcher case above, surfacing on
                // the read path. Same recovery (retry next tick); silence to match.
                // See Docs/PresenceSystem/BUGS.md B6 + Docs/PartySystem/MPPM_SESSION_LOG.md
                // Session 1 finding #2.
                //
                // NOTE: [definite] deliberately stays BELOW these, unlike the
                // ordering the class doc for IsBenignSdkStaleIndexError asserts.
                // Structured definite errors (SessionNotFound / SessionDeleted /
                // NotInLobby) carry a specific Error and so are never matched by
                // the Unknown-discriminator above - they already reach [definite]
                // correctly. The only case the current order sends to benign
                // instead is a SessionException the SDK ITSELF could not classify
                // (Error == Unknown) whose message happens to read like
                // "session ... not found". Promoting [definite] would make that
                // ambiguous input trigger HandleDefiniteSessionGoneAsync, which
                // recreates the solo session and kicks any client mid-join (see
                // SESSION_CREATION_GRACE_PERIOD_SECONDS). "Retry next tick" is the
                // safe reading of an error the SDK could not name.
                if (IsBenignSdkStaleIndexError(e))
                {
                    RecordBenignRefreshSkip(ref _benignPartySessionSkips, e, "party-session", "SdkStaleIndex");
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

            await SyncPartyMembersFromCacheAsync();
        }

        /// <summary>
        /// Diffs <c>HostConnectionDataSO.PartyMembers</c> against the SDK's
        /// IN-MEMORY session roster. Issues no UGS call whatsoever.
        ///
        /// <para>
        /// <b>This is the push path, and its cost profile is the point.</b> UGS
        /// patches <c>ISession.Players</c> before it raises
        /// <c>PlayerJoined</c> / <c>PlayerHasLeft</c> / <c>Changed</c>, so by the
        /// time we drain the flag the roster is already correct locally. A path
        /// with no read cannot be voided by a read fault - and the party-session
        /// read is voided ~32% of poll ticks by the SDK stale-index defect
        /// (<c>Docs/PresenceSystem/BUGS.md</c>, MEASURED run 2). Discovering a
        /// join used to depend entirely on that read; now the read is only the
        /// backstop.
        /// </para>
        ///
        /// <para>
        /// Also called by <see cref="RefreshPartyMembersAsync"/> as the tail of
        /// the poll path, after its read has succeeded - one diff implementation
        /// for both, so push and poll can never drift apart.
        /// </para>
        /// </summary>
        private async UniTask SyncPartyMembersFromCacheAsync()
        {
            var session = _partySessionService.ActiveSession;
            if (session == null) return;
            if (connectionData.PartyMembers == null) return;

            // Resolve an authoritative local id; if we can't (signed out), skip this
            // reconcile tick rather than risk adding our own session player as a phantom.
            string localId = ResolveLocalPlayerId();
            if (string.IsNullOrEmpty(localId))
                return;

            var joinedPlayerIds = _memberService.SyncFromSession(session, localId);

            foreach (var joinedId in joinedPlayerIds)
                await ClearOutgoingInviteIfPresentAsync(joinedId, "party-join");

            // A new party member appeared in the Relay session.
            // If we're Inviting (sent an invite and they connected), transition to InParty.
            if (joinedPlayerIds.Count > 0 && _stateMachine.CurrentState == PartyState.Inviting)
                _stateMachine.TryTransition(PartyState.InParty);
        }

        /// <summary>
        /// Drains the party session's push flag and re-diffs the roster from the
        /// SDK's in-memory player list. Runs outside the lobby mutex because it
        /// makes no UGS call and touches only the party session and the SOAP list.
        ///
        /// <para>
        /// Deliberately NOT gated on <c>SESSION_CREATION_GRACE_PERIOD_SECONDS</c>.
        /// That gate exists to stop a freshly-provisioned session's READ from
        /// 404ing and triggering recovery that kicks a joining client - there is
        /// no read here to fail, and suppressing the push during exactly the
        /// window when someone is joining would forfeit the whole benefit.
        /// </para>
        /// </summary>
        private async UniTaskVoid DrainPartySessionPushAsync()
        {
            try
            {
                await SyncPartyMembersFromCacheAsync();
            }
            catch (Exception e)
            {
                // Cannot be an SDK read fault - there is no read. Anything landing
                // here is a local bug in the diff, so it stays loud.
                Debug.LogWarning(
                    $"[HostConnectionService] Party-session push sync failed ({e.GetType().Name}): {e.Message}");
            }
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

            // Every lobby-join path converges here - initial join, reconnect,
            // resume-from-background, and the pushed membership-loss recovery -
            // so this is the one place Announced needs to be asserted.
            // Announced, not Present: the vessel-spawn signal (OnClientReady)
            // promotes us, and on a mid-session rejoin it will already have
            // fired, so HandleLocalVesselReady's catch-up probe in Start covers
            // the case where it never fires again.
            _presence.TryTransition(PresenceState.Announced);

            // Catch-up probe for an already-fired signal. OnClientReady is a
            // one-shot ScriptableEventNoParam with no replay, and on a RE-join
            // (reconnect, resume-from-background, pushed membership loss) the
            // vessel already exists and will not spawn again - so waiting for the
            // event would strand us in Announced and render this player as
            // "CONNECTING…" on every peer for the rest of the session. Same
            // already-fired pattern as ToyboxController's vessel probe.
            if (_gameData?.LocalPlayer?.Vessel != null)
            {
                _localVesselReady = true;
                _presence.TryTransition(PresenceState.Present);
            }

            // Clear the party roster ONLY when there is no live party session.
            // The presence lobby is the discovery layer; the Relay-backed party
            // is independent of it. A presence rejoin - reconnect, converge
            // migration, or a UGS-pushed membership loss - left NetworkManager,
            // the session and every remote member untouched, yet the
            // unconditional clearFirst:true wiped every remote row anyway.
            // ScriptableList.Clear() raises only OnCleared (never per-item
            // OnItemRemoved), so ArcadeLobbyList blanked slots 1-3 and disabled
            // the Leave button while the party was still perfectly alive.
            // On the INITIAL join ActiveSession is still null (EnsureInitialized
            // creates the session after this runs), so first-boot behaviour is
            // unchanged. SeedLocalPlayer is idempotent, so skipping the clear
            // cannot duplicate our own row.
            _memberService.SeedLocalPlayer(clearFirst: _partySessionService.ActiveSession == null);

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

            int    currentCount    = connectionData.PartyMembers != null ? connectionData.PartyMembers.Count : 0;
            string currentMatch    = ResolveCurrentMatchName();
            string currentName     = connectionData.LocalDisplayName ?? "Pilot";
            int    currentAvatar   = connectionData.LocalAvatarId;
            int    currentPresence = (int)_presence.CurrentState;

            // A lobby change invalidates every tracker. The trackers describe
            // what WE last wrote to a SPECIFIC lobby; after a converge migration
            // or a reconnect we are writing to a different one, whose copy of our
            // player record was rebuilt from scratch. Without this the change-gate
            // below compares against the old lobby's values, concludes nothing
            // changed, and never re-publishes - so any key the rebuild dropped
            // stays dropped for the rest of the session. This is the general
            // guard; carrying presenceState in LivePropertySource is the specific
            // one, and both are wanted (belt and braces on a path that has
            // already produced this class of bug twice - see BUGS.md B4).
            string currentLobbyId = lobby.Id;
            if (currentLobbyId != _publishedToLobbyId)
            {
                _publishedPartyCount    = -1;
                _publishedMatchName     = "<UNSET>";
                _publishedDisplayName   = "<UNSET>";
                _publishedAvatarId      = int.MinValue;
                _publishedPresenceState = int.MinValue;
                _publishedToLobbyId     = currentLobbyId;
            }

            if (currentCount    == _publishedPartyCount &&
                currentMatch    == _publishedMatchName &&
                currentName     == _publishedDisplayName &&
                currentAvatar   == _publishedAvatarId &&
                currentPresence == _publishedPresenceState) return;

            try
            {
                lobby.CurrentPlayer.SetProperty(PARTY_COUNT_KEY,
                    new PlayerProperty(currentCount.ToString(), VisibilityPropertyOptions.Public));
                lobby.CurrentPlayer.SetProperty(PARTY_MAX_KEY,
                    new PlayerProperty(connectionData.MaxPartySlots.ToString(), VisibilityPropertyOptions.Public));
                lobby.CurrentPlayer.SetProperty(MATCH_NAME_KEY,
                    new PlayerProperty(currentMatch ?? string.Empty, VisibilityPropertyOptions.Public));
                // Identity reconciliation: rides the same single save so a rename
                // missed by the event push is guaranteed out within one tick.
                lobby.CurrentPlayer.SetProperty(DISPLAY_NAME_KEY,
                    new PlayerProperty(currentName, VisibilityPropertyOptions.Public));
                lobby.CurrentPlayer.SetProperty(AVATAR_ID_KEY,
                    new PlayerProperty(currentAvatar.ToString(), VisibilityPropertyOptions.Public));
                // Rides the SAME single save as everything else - one UpdatePlayer
                // call, not one per field. This is the property peers read to
                // know whether this player is actually in the world yet.
                lobby.CurrentPlayer.SetProperty(PRESENCE_STATE_KEY,
                    new PlayerProperty(currentPresence.ToString(), VisibilityPropertyOptions.Public));

                await _propertyWriter.SaveWithRetryAsync(lobby);
                _publishedPartyCount    = currentCount;
                _publishedMatchName     = currentMatch;
                _publishedDisplayName   = currentName;
                _publishedAvatarId      = currentAvatar;
                _publishedPresenceState = currentPresence;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[HostConnectionService] PublishPartyState error: {e.Message}");
            }
        }

        /// <summary>
        /// The match this player is in, or empty when they are in the menu.
        ///
        /// <para>
        /// Keyed off <see cref="PresenceState.InMatch"/>, NOT the active scene
        /// name. The previous <c>if (IsOnMenuScene()) return string.Empty;</c>
        /// made this structurally impossible to publish: the only trigger,
        /// <c>HandleGameLaunch</c>, fires on <c>GameDataSO.OnLaunchGame</c> -
        /// which <c>SceneLoader.LaunchGame</c> also handles - so it ran while
        /// Menu_Main was still the active scene. With the lobby mutex
        /// uncontended, <c>SemaphoreSlim.WaitAsync()</c> completes synchronously,
        /// so the publish executed inline, still on the menu, returned empty,
        /// matched <c>_publishedMatchName</c>, and the change-gate skipped the
        /// write entirely. Then <see cref="Update"/>'s <c>IsOnMenuScene</c> gate
        /// killed the loop for the rest of the match.
        /// </para>
        ///
        /// <para>
        /// Net effect: <c>matchName</c> was never published, so
        /// <c>OnlineInfoEntry.Status.InMatch</c> was dead code and players in a
        /// match rendered as idle and invitable - while their own refresh loop
        /// was dead, so the invite was never even scanned for.
        /// </para>
        /// </summary>
        private string ResolveCurrentMatchName()
        {
            if (_gameData == null) return string.Empty;
            if (_presence.CurrentState != PresenceState.InMatch) return string.Empty;
            // Every in-game scene advertises its match name - solo is just a party of one.
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

        private void HandleGameLaunch()
        {
            // Enter InMatch BEFORE publishing - ResolveCurrentMatchName now reads
            // the state machine, so the order is what makes the write contain a
            // match name at all.
            _presence.TryTransition(PresenceState.InMatch);
            PublishPresenceImmediateAsync().Forget();
        }

        // ╔═══════════════════════════════════════════════════════════════════╗
        // ║  Presence lifecycle drivers                                       ║
        // ╚═══════════════════════════════════════════════════════════════════╝

        private void SubscribeToVesselReady()
        {
            if (_clientReadySubscribed || _gameData == null || _gameData.OnClientReady == null) return;
            _gameData.OnClientReady.OnRaised += HandleLocalVesselReady;
            _clientReadySubscribed = true;
        }

        private void UnsubscribeFromVesselReady()
        {
            if (!_clientReadySubscribed || _gameData == null || _gameData.OnClientReady == null) return;
            _gameData.OnClientReady.OnRaised -= HandleLocalVesselReady;
            _clientReadySubscribed = false;
        }

        /// <summary>
        /// <b>The "I am here" broadcast.</b> <c>GameDataSO.OnClientReady</c> is
        /// raised by <c>ClientPlayerVesselInitializer.InitializePair</c> for the
        /// LOCAL user once its vessel exists - i.e. the moment the Menu_Main
        /// lava-lamp vessel has spawned. Moving to
        /// <see cref="PresenceState.Present"/> here publishes a
        /// <c>presenceState</c> property change, which the push channel delivers
        /// to every peer on their next drained tick.
        ///
        /// <para>
        /// Also covers the return leg: coming back from an arcade game reloads
        /// Menu_Main and respawns the menu vessel, raising this again, which
        /// moves us <c>InMatch → Present</c> and clears <c>matchName</c>.
        /// </para>
        /// </summary>
        private void HandleLocalVesselReady()
        {
            // Latch FIRST, unconditionally. OnClientReady is a one-shot with no
            // replay, and the transition below can legitimately be illegal at the
            // moment it arrives (e.g. still Joining, because the lobby join is
            // slower than the vessel spawn on this machine). Before the latch,
            // that rejection DISCARDED the only signal we would ever get and the
            // player stayed Announced - rendering as CONNECTING… on every peer
            // for the rest of the session. ReconcilePresenceState re-tries from
            // the latch on every tick, so ordering no longer matters.
            _localVesselReady = true;

            if (_presence.CurrentState == PresenceState.InMatch)
            {
                // Back in the menu: drop the match advertisement.
                _presence.TryTransition(PresenceState.Present);
                PublishPresenceImmediateAsync().Forget();
                return;
            }

            if (_presence.TryTransition(PresenceState.Present))
                PublishPresenceImmediateAsync().Forget();
        }

        /// <summary>
        /// Per-tick convergence check for the presence state.
        ///
        /// <para>
        /// The presence machine must never depend on having CAUGHT a one-shot
        /// event, because there is no ordering guarantee between the presence
        /// lobby join and the menu vessel spawn - and the two race differently on
        /// the main editor instance versus an MPPM virtual player, which is
        /// exactly how three of four instances ended up stuck at
        /// <see cref="PresenceState.Announced"/> while the fourth was correct.
        /// This re-derives the state from the observable CONDITION (does the
        /// local vessel exist?) once per refresh tick, so a missed, early or
        /// out-of-order signal self-heals within one interval instead of
        /// persisting for the whole session.
        /// </para>
        /// </summary>
        private void ReconcilePresenceState()
        {
            // The vessel may have spawned before we were in a state that could
            // accept the transition, or before we subscribed at all.
            if (!_localVesselReady && _gameData?.LocalPlayer?.Vessel != null)
                _localVesselReady = true;

            if (!_localVesselReady) return;
            if (_presence.CurrentState != PresenceState.Announced) return;

            if (_presence.TryTransition(PresenceState.Present))
            {
                Debug.Log("[HostConnectionService] Presence reconciled to Present " +
                          "(vessel exists but the spawn signal did not land in a state that could accept it).");
                PublishPresenceImmediateAsync().Forget();
            }
        }

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

        /// <summary>
        /// True when the exception is a UGS rate-limit (429) response.
        /// Delegates to <see cref="UgsErrorClassifier.IsRateLimit"/> so all three
        /// party-layer services agree.
        ///
        /// <para>
        /// The previous local version tested only
        /// <c>e.Message.Contains("Too Many Requests")</c> on the OUTER exception,
        /// while its two siblings in this file
        /// (<see cref="IsDefiniteSessionGoneException"/>,
        /// <see cref="IsBenignLobbyPatcherError"/>) both walk the chain. A wrapped
        /// 429 therefore missed the rate-limit branch and fell through to the
        /// generic one, which increments
        /// <see cref="MAX_REFRESH_ERRORS_BEFORE_RECONNECT"/> - so being throttled
        /// could escalate into <c>ForceReset</c> plus a throwaway presence lobby
        /// instead of backing off.
        /// </para>
        /// </summary>
        private static bool IsRateLimitException(Exception e) =>
            UgsErrorClassifier.IsRateLimit(e);

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
        /// specific reason (<c>SessionNotFound</c>, <c>RateLimited</c>, …), so it
        /// never matches the <c>Unknown</c> discriminator here; only the
        /// unclassifiable SDK-internal failures land on <c>Unknown</c>, and for
        /// those "log-silent, retry next tick" is already the correct (and only)
        /// recovery.
        /// </para>
        ///
        /// <para>
        /// <b>Branch ordering.</b> This doc previously claimed the
        /// <c>[definite]</c> and rate-limit branches ran *before* this check.
        /// They did not - at both catch sites this ran first, which is how a
        /// wrapped 429 got silently classified as harmless. The rate-limit branch
        /// has since been moved above this one. <c>[definite]</c> deliberately
        /// still runs after: the structured discriminator already keeps definite
        /// errors out of here, and promoting it would route
        /// SDK-couldn't-classify errors into a session recreation. See the note
        /// at the <c>[benign]</c> branch in <see cref="RefreshPartyMembersAsync"/>.
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
