using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CosmicShore.Core
{
    /// <summary>
    /// Single writer for all UGS Analytics custom events (UGS-only pipeline -
    /// the Firebase analytics path is retired).
    ///
    /// Owns the collection lifecycle: starts data collection after UGS sign-in
    /// (gated by consent and network availability), records events through one
    /// choke point, and flushes only on app pause/quit - the SDK batches
    /// everything else automatically.
    ///
    /// Custom events and their parameters must also be declared in the UGS
    /// dashboard Event Manager or the backend silently discards them.
    /// </summary>
    public class AnalyticsServiceFacade
    {
        /// <summary>
        /// PlayerPrefs key for analytics consent. Tri-state: key ABSENT = undecided
        /// (opt-in: do not collect), 1 = granted, 0 = denied. Written by the consent
        /// dialog and the Settings opt-out toggle via <see cref="SetConsent"/>.
        /// </summary>
        public const string ConsentPrefKey = "AnalyticsConsent";

        /// <summary>
        /// PlayerPrefs key for the COPPA age gate. Tri-state: ABSENT = not asked,
        /// 1 = age-eligible (13+), 0 = under-13 (never collect). Written by the age
        /// gate via <see cref="SetAgeEligible"/> / <see cref="SubmitBirthYear"/>.
        /// </summary>
        public const string AgeGatePrefKey = "AnalyticsAgeEligible";

        /// <summary>Minimum age (years) to be eligible for analytics collection (COPPA).</summary>
        const int MinimumAge = 13;

        /// <summary>PlayerPrefs key (device-local) guarding the once-ever first-launch event.</summary>
        const string FirstLaunchPrefKey = "AnalyticsFirstLaunchRecorded";

        /// <summary>Consecutive losses before <c>repeated_game_fail</c> fires.</summary>
        const int RepeatedFailThreshold = 3;

        /// <summary>
        /// Version of the event contract carried on every event. Bump when a field's meaning
        /// changes, so downstream queries can tell old rows from new ones instead of silently
        /// mixing them. Must match Docs/Analytics/EVENT_SCHEMA.json.
        /// </summary>
        const int EventSchemaVersion = 1;

        readonly AuthenticationDataVariable _authVariable;
        readonly NetworkMonitorDataVariable _networkVariable;
        readonly GameDataSO _gameData;
        readonly ApplicationLifecycleEventsContainerSO _lifecycleEvents;
        readonly ApplicationStateDataVariable _appStateVariable;
        readonly MenuFreestyleEventsContainerSO _menuFreestyleEvents;
        readonly FriendsDataSO _friendsData;
        readonly HostConnectionDataSO _hostConnectionData;
        readonly bool _allowLog;

        /// <summary>
        /// Every destination. One payload is built per event and fanned out unchanged, so the
        /// destinations cannot drift apart. Consent and the age gate are enforced here, above
        /// the sinks, so adding a destination can never widen collection. The one exception is
        /// narrowing: the UGS sink drops what the schema marks Froglet-only
        /// (<see cref="UgsAnalyticsSink.FrogletOnlyFields"/>).
        /// </summary>
        readonly List<IAnalyticsSink> _sinks = new();

        readonly FrogletAnalyticsSink _frogletSink;

        /// <summary>
        /// The one instance currently subscribed to every persistent SOAP/static event below.
        /// This is a Resolution.Lazy Reflex singleton, but Reflex has no disposal hook that runs
        /// when a container is rebuilt - and with this project's Enter Play Mode settings
        /// (Domain Reload + Scene Reload both off, see ProjectSettings/EditorSettings.asset),
        /// re-entering Bootstrap constructs a SECOND facade without ever tearing down the first
        /// one's subscriptions (same class of risk ReconnectService's own doc comment calls out:
        /// "re-loading that scene would spawn a duplicate of every other [persistent root]").
        /// Confirmed in production PostHog data: every subscribed handler below fired at ~2x the
        /// rate of every directly-called Record* method, with distinct event uuids and identical
        /// match_id/distinct_id pairs a few milliseconds apart.
        ///
        /// Tearing down whatever instance came before guarantees exactly one live subscriber
        /// regardless of how many times this gets constructed in one process.
        /// </summary>
        static AnalyticsServiceFacade _liveInstance;

        bool _collecting;
        bool _isConnected = true;
        bool _signedIn;
        bool _uiActionsWired;
        bool _profileRefreshWired;
        string _lastIdentifySignature;
        bool _gameInProgress;
        float _gameStartTime;
        bool _sessionEndRecorded;
        string _lastUiAction = string.Empty;
        bool _menuReadyThisSession;
        bool _freestyleEnteredThisSession;
        int _consecutiveLosses;
        int _droppedEvents;
        bool _warnedNotCollecting;

        /// <summary>The vessels flown in the current Freestyle (Toybox) stretch - see <see cref="FreestyleVesselLog"/>.</summary>
        readonly FreestyleVesselLog _freestyleVessels = new();

        // ── Sessions (session_started / session_ended) ──
        readonly AnalyticsSessionClock _session = new(DateTime.UtcNow);

        // ── The running match, for match_abandoned ──
        /// <summary>game_started's payload for the match in progress: match_abandoned repeats its fields.</summary>
        Dictionary<string, object> _matchStartParameters;
        /// <summary>The active scene when the match started; leaving it while the match runs is an abandon.</summary>
        string _matchSceneName;
        DateTime _matchStartUtc;
        bool _networkLostDuringMatch;

        // ── Invite attribution (party_invite_* / party_joined) ──
        readonly InviteAttribution _invites = new();

        // ── client_exception ──
        /// <summary>
        /// Seconds a new fault is held before it is sent, so an error repeating every frame folds into
        /// one event with its count instead of being sent on its first sighting with a count of 1.
        /// Pause and quit send whatever is held at once.
        /// </summary>
        const float ExceptionFoldWindowSeconds = 5f;

        readonly ClientExceptionLog _exceptions = new();

        /// <summary>The main thread's context, captured at construction; faults seen off it are sent from it.</summary>
        readonly SynchronizationContext _mainContext;

        /// <summary>1 while a fold window is running (Interlocked: set from any thread).</summary>
        int _exceptionFlushScheduled;

        /// <summary>Mirrors _collecting for the log callback, which runs on any thread.</summary>
        volatile bool _captureExceptions;

        /// <summary>The active scene's name, kept on the main thread for the log callback to read.</summary>
        volatile string _activeSceneName = string.Empty;

        /// <summary>
        /// True on a thread while it is inside analytics (a sink recording, the exception send). An error
        /// logged there is NOT captured: client_exception must never produce another client_exception.
        /// </summary>
        [ThreadStatic] static bool t_inAnalytics;

        AuthenticationData AuthData => _authVariable.Value;
        NetworkMonitorData NetworkData => _networkVariable.Value;

        // ── Consent / age gate (opt-in) ──
        /// <summary>True once the player has answered the consent dialog (accept or decline).</summary>
        public bool ConsentDecided => PlayerPrefs.HasKey(ConsentPrefKey);
        /// <summary>True only when the player has explicitly granted consent. Undecided reads as false (opt-in).</summary>
        public bool ConsentGranted => PlayerPrefs.GetInt(ConsentPrefKey, 0) == 1;
        /// <summary>True once the age gate has been answered.</summary>
        public bool AgeChecked => PlayerPrefs.HasKey(AgeGatePrefKey);
        /// <summary>True only when the player is confirmed 13+.</summary>
        public bool AgeEligible => PlayerPrefs.GetInt(AgeGatePrefKey, 0) == 1;
        /// <summary>
        /// True while the first-run privacy flow still needs to run: the age gate
        /// hasn't been answered, or the player is age-eligible but hasn't decided consent.
        /// The consent UI shows itself while this is true.
        /// </summary>
        public bool NeedsPrivacyFlow => !AgeChecked || (AgeEligible && !ConsentDecided);
        public bool IsCollecting => _collecting;

        public AnalyticsServiceFacade(
            AuthenticationDataVariable authVariable,
            NetworkMonitorDataVariable networkVariable,
            GameDataSO gameData,
            ApplicationLifecycleEventsContainerSO lifecycleEvents,
            ApplicationStateDataVariable appStateVariable,
            MenuFreestyleEventsContainerSO menuFreestyleEvents,
            FriendsDataSO friendsData,
            HostConnectionDataSO hostConnectionData,
            bool allowLog)
        {
            _authVariable = authVariable;
            _networkVariable = networkVariable;
            _gameData = gameData;
            _lifecycleEvents = lifecycleEvents;
            _appStateVariable = appStateVariable;
            _menuFreestyleEvents = menuFreestyleEvents;
            _friendsData = friendsData;
            _hostConnectionData = hostConnectionData;
            _allowLog = allowLog;

            // Must run before a single subscription is wired - see _liveInstance.
            _liveInstance?.Unsubscribe();
            _liveInstance = this;

            _sinks.Add(new UgsAnalyticsSink(Log));

            // Our own analytics server (Tools/Analytics/server/), which replaced PostHog on
            // 2026-11-06. The asset ships pointed at the server; nothing is filled in by hand.
            var frogletConfig = Resources.Load<FrogletAnalyticsConfigSO>("FrogletAnalyticsConfig");
            if (frogletConfig != null && frogletConfig.Enabled)
            {
                _frogletSink = new FrogletAnalyticsSink(frogletConfig, Log, BuildSinkEnvelope);
                _sinks.Add(_frogletSink);
            }

            AuthData.OnSignedIn.OnRaised += HandleSignedIn;
            NetworkData.OnNetworkFound.OnRaised += HandleNetworkFound;
            NetworkData.OnNetworkLost.OnRaised += HandleNetworkLost;
            _gameData.OnMiniGameTurnStarted.OnRaised += HandleTurnStarted;
            _gameData.OnMiniGameEnd.OnRaised += HandleMiniGameEnd;
            _gameData.OnPlayerPairInitialized.OnRaised += HandlePlayerPairInitialized;
            _gameData.OnSceneTransition.OnRaised += HandleSceneTransition;
            _lifecycleEvents.OnAppPaused.OnRaised += HandleAppPaused;
            _lifecycleEvents.OnAppQuitting.OnRaised += HandleAppQuitting;
            TryWireUiActions();
            TryWireProfileRefresh();

            // Crash capture follows the same opt-in gate as analytics. Applied here so a returning
            // player's stored decision re-arms it immediately after the BeforeSceneLoad disarm.
            ApplyCrashReportingGate();

            // ── Phase 2: subscribe to SO-asset SOAP events (safe at construction) ──
            _menuFreestyleEvents.OnGameStateTransitionStart.OnRaised += HandleFreestyleEntered;
            _friendsData.OnFriendRequestSent.OnRaised += HandleFriendRequestSent;
            _friendsData.OnFriendRequestReceived.OnRaised += HandleFriendRequestReceived;
            _friendsData.OnFriendAdded.OnRaised += HandleFriendAdded;
            _hostConnectionData.OnInviteSent.OnRaised += HandlePartyInviteSent;
            _hostConnectionData.OnInviteReceived.OnRaised += HandlePartyInviteReceived;
            _hostConnectionData.OnPartyJoinCompleted.OnRaised += HandlePartyJoined;

            // ── Phase 2: static C# events (exist regardless of scene) ──
            // Named handlers rather than inline lambdas: Unsubscribe() below needs the exact
            // same delegate it subscribed with, and a freshly-written lambda expression at the
            // unsubscribe site is not guaranteed to compare equal to the one used at subscribe
            // time (relying on compiler delegate-caching for correctness is fragile).
            GameSetting.OnChangeMusicEnabledStatus      += HandleMusicEnabledChanged;
            GameSetting.OnChangeSFXEnabledStatus        += HandleSfxEnabledChanged;
            GameSetting.OnChangeHapticsEnabledStatus    += HandleHapticsEnabledChanged;
            GameSetting.OnChangeInvertYEnabledStatus    += HandleInvertYChanged;
            GameSetting.OnChangeInvertThrottleEnabledStatus += HandleInvertThrottleChanged;
            GameSetting.OnChangeJoystickVisualsStatus   += HandleJoystickVisualsChanged;
            GameSetting.OnChangeMusicLevel              += HandleMusicLevelChanged;
            GameSetting.OnChangeSFXLevel                += HandleSfxLevelChanged;
            GameSetting.OnChangeHapticsLevel            += HandleHapticsLevelChanged;
            FavoriteSystem.OnFavoriteChanged            += HandleFavoriteChanged;
            UGSCloudSaveProvider.OnSaveFailed           += HandleCloudSaveFailed;
            FTUEEventManager.OnQuestNodeCompleted       += HandleFtueStepCompleted;
            FTUEEventManager.OnQuestPhaseCompleted      += HandleFtuePhaseCompleted;
            FTUEEventManager.OnQuestCompleted           += HandleFtueCompleted;
            SubscribeFreestyleClock();

            // match_abandoned's backstop and client_exception's scene: Unity's own scene event.
            _activeSceneName = SceneManager.GetActiveScene().name ?? string.Empty;
            SceneManager.activeSceneChanged += HandleActiveSceneChanged;

            // client_exception. The facade is built on the main thread, so this is its context; the
            // threaded log callback posts every send back onto it.
            _mainContext = SynchronizationContext.Current;
            Application.logMessageReceivedThreaded += HandleLogMessageThreaded;

            // Adopt what the one-shot events have ALREADY announced. This facade is a LAZY DI
            // singleton, so it is constructed whenever something first injects it - which is
            // routinely AFTER sign-in and after the network probe have completed. Subscribing
            // is then not enough: OnSignedIn / OnNetworkFound have already fired, nothing will
            // raise them again this session, and _signedIn / _isConnected stay false forever -
            // so StartCollectionIfReady never starts and EVERY event is dropped, to UGS and
            // PostHog alike. Observed in the field: a session that was demonstrably signed in
            // (it was in a presence lobby) still logged "DROPPING EVENTS - UGS sign-in has not
            // completed" at quit, leaving that whole play session with no telemetry at all.
            //
            // Both flags mirror state that is independently readable, so the correct fix is to
            // reconcile with it rather than to hope for the raise. Idempotent by construction:
            // the handlers only set a flag and re-run the guarded StartCollectionIfReady, so a
            // genuine event arriving afterwards is a no-op.
            ReconcileWithAlreadyAnnouncedState();
        }

        /// <summary>
        /// Mirror image of the constructor's subscription block - every <c>+=</c> above has its
        /// matching <c>-=</c> here. Called on the OUTGOING instance by the next constructor,
        /// never by this one; see <see cref="_liveInstance"/>.
        /// </summary>
        void Unsubscribe()
        {
            var auth = AuthData;
            if (auth != null) auth.OnSignedIn.OnRaised -= HandleSignedIn;

            var network = NetworkData;
            if (network != null)
            {
                network.OnNetworkFound.OnRaised -= HandleNetworkFound;
                network.OnNetworkLost.OnRaised -= HandleNetworkLost;
            }

            if (_gameData != null)
            {
                _gameData.OnMiniGameTurnStarted.OnRaised -= HandleTurnStarted;
                _gameData.OnMiniGameEnd.OnRaised -= HandleMiniGameEnd;
                _gameData.OnPlayerPairInitialized.OnRaised -= HandlePlayerPairInitialized;
                _gameData.OnSceneTransition.OnRaised -= HandleSceneTransition;
            }

            if (_lifecycleEvents != null)
            {
                _lifecycleEvents.OnAppPaused.OnRaised -= HandleAppPaused;
                _lifecycleEvents.OnAppQuitting.OnRaised -= HandleAppQuitting;
            }

            if (_menuFreestyleEvents != null)
                _menuFreestyleEvents.OnGameStateTransitionStart.OnRaised -= HandleFreestyleEntered;

            if (_friendsData != null)
            {
                _friendsData.OnFriendRequestSent.OnRaised -= HandleFriendRequestSent;
                _friendsData.OnFriendRequestReceived.OnRaised -= HandleFriendRequestReceived;
                _friendsData.OnFriendAdded.OnRaised -= HandleFriendAdded;
            }

            if (_hostConnectionData != null)
            {
                _hostConnectionData.OnInviteSent.OnRaised -= HandlePartyInviteSent;
                _hostConnectionData.OnInviteReceived.OnRaised -= HandlePartyInviteReceived;
                _hostConnectionData.OnPartyJoinCompleted.OnRaised -= HandlePartyJoined;
            }

            GameSetting.OnChangeMusicEnabledStatus      -= HandleMusicEnabledChanged;
            GameSetting.OnChangeSFXEnabledStatus        -= HandleSfxEnabledChanged;
            GameSetting.OnChangeHapticsEnabledStatus    -= HandleHapticsEnabledChanged;
            GameSetting.OnChangeInvertYEnabledStatus    -= HandleInvertYChanged;
            GameSetting.OnChangeInvertThrottleEnabledStatus -= HandleInvertThrottleChanged;
            GameSetting.OnChangeJoystickVisualsStatus   -= HandleJoystickVisualsChanged;
            GameSetting.OnChangeMusicLevel               -= HandleMusicLevelChanged;
            GameSetting.OnChangeSFXLevel                 -= HandleSfxLevelChanged;
            GameSetting.OnChangeHapticsLevel             -= HandleHapticsLevelChanged;
            FavoriteSystem.OnFavoriteChanged            -= HandleFavoriteChanged;
            UGSCloudSaveProvider.OnSaveFailed           -= HandleCloudSaveFailed;
            FTUEEventManager.OnQuestNodeCompleted       -= HandleFtueStepCompleted;
            FTUEEventManager.OnQuestPhaseCompleted      -= HandleFtuePhaseCompleted;
            FTUEEventManager.OnQuestCompleted           -= HandleFtueCompleted;
            FlightClock.OnFreestyleSegmentCompleted     -= HandleFreestyleSegmentCompleted;
            SceneManager.activeSceneChanged             -= HandleActiveSceneChanged;
            Application.logMessageReceivedThreaded      -= HandleLogMessageThreaded;
            _captureExceptions = false;

            if (_uiActionsWired && UserActionSystem.Instance != null)
                UserActionSystem.Instance.OnUserActionCompleted -= HandleUserAction;

            if (_profileRefreshWired && PlayerDataService.Instance != null)
                PlayerDataService.Instance.OnProfileChanged -= HandleProfileChanged;
        }

        /// <summary>
        /// Catch up on <see cref="HandleSignedIn"/> / <see cref="HandleNetworkFound"/> when the
        /// events that would have called them fired before this facade existed.
        /// </summary>
        void ReconcileWithAlreadyAnnouncedState()
        {
            var auth = AuthData;
            if (auth != null && auth.IsSignedIn && !_signedIn)
            {
                Log("Sign-in had already completed before this facade was constructed - adopting it.");
                HandleSignedIn();
            }

            var network = NetworkData;
            if (network != null && network.IsOnline && !_isConnected)
                HandleNetworkFound();
        }

        #region Consent & collection lifecycle

        /// <summary>
        /// Persists the player's analytics consent and starts/stops collection
        /// accordingly. Called by the consent dialog and the Settings opt-out toggle.
        /// Consent only takes effect for age-eligible players (under-13 never collects).
        /// </summary>
        public void SetConsent(bool granted)
        {
            PlayerPrefs.SetInt(ConsentPrefKey, granted ? 1 : 0);
            PlayerPrefs.Save();

            if (granted)
                StartCollectionIfReady();
            else
                StopCollection();

            ApplyCrashReportingGate();
        }

        /// <summary>
        /// Records the COPPA age gate result. Under-13 forces consent denied and
        /// stops collection - there is no path to collect from an ineligible player.
        /// </summary>
        public void SetAgeEligible(bool eligible)
        {
            PlayerPrefs.SetInt(AgeGatePrefKey, eligible ? 1 : 0);

            if (!eligible)
            {
                // Under-13: never collect, regardless of any later consent answer.
                PlayerPrefs.SetInt(ConsentPrefKey, 0);
                PlayerPrefs.Save();
                StopCollection();
                ApplyCrashReportingGate();
                return;
            }

            PlayerPrefs.Save();
            ApplyCrashReportingGate();
        }

        /// <summary>
        /// Mirrors the analytics opt-in decision onto crash capture. Deliberately keyed off consent
        /// and age eligibility ONLY - not sign-in or connectivity - because a crash that happens
        /// offline or before sign-in is exactly the one worth capturing.
        /// </summary>
        void ApplyCrashReportingGate() => CrashReportingService.ApplyConsent(AgeEligible && ConsentGranted);

        /// <summary>
        /// COPPA-friendly neutral age gate: pass the player's birth year; eligibility
        /// is derived (>= <see cref="MinimumAge"/>). Prefer this over a yes/no age
        /// question so the screen does not nudge the player toward a "13+" answer.
        /// </summary>
        public void SubmitBirthYear(int birthYear)
        {
            int age = DateTime.UtcNow.Year - birthYear;
            SetAgeEligible(age >= MinimumAge);
            // Fair play keeps the year itself, so the server can tell a minor (R13, FairPlayAge).
            FairPlayAge.Record(birthYear);
        }

        /// <summary>
        /// Honors a right-to-erasure request: stops collection, records a denial so
        /// we don't immediately re-collect, and asks UGS to delete the player's data.
        /// Wire to a "Delete my data" button in Settings/Privacy.
        /// </summary>
        public void RequestDataDeletion()
        {
            // Delete BEFORE revoking consent: StopCollection drops anything still queued.
            string distinctId = ResolveDistinctId();
            foreach (var sink in _sinks)
                sink.RequestDataDeletion(distinctId);

            Log("Data deletion requested from all sinks.");
            SetConsent(false);
        }

        void HandleSignedIn()
        {
            _signedIn = true;
            TryWireUiActions();
            TryWireProfileRefresh();
            StartCollectionIfReady();
        }

        void HandleNetworkFound()
        {
            _isConnected = true;
            StartCollectionIfReady();
        }

        // The SDK caches events (up to 5MB memory, persisted on shutdown) while
        // offline, so collection stays on - we only track the flag so a
        // first-time StartDataCollection waits for connectivity.
        void HandleNetworkLost()
        {
            _isConnected = false;
            if (_gameInProgress)
                _networkLostDuringMatch = true;
        }

        void StartCollectionIfReady()
        {
            if (_collecting || !_signedIn || !_isConnected)
                return;
            // Opt-in: collect only for an age-eligible player who explicitly granted.
            // Undecided / under-13 / declined all read false here.
            if (!AgeEligible || !ConsentGranted)
                return;
            if (UnityServices.State != ServicesInitializationState.Initialized)
                return;

            foreach (var sink in _sinks)
                sink.StartCollection();

            _collecting = true;
            _captureExceptions = true;
            Log("Data collection started.");

            TryWireProfileRefresh();
            IdentifyPlayer();
            // After the identify: the Froglet sink drops events until it knows who the player is.
            MaybeRecordSessionStarted();
            MaybeRecordFirstLaunch();
        }

        void StopCollection()
        {
            if (!_collecting)
                return;

            foreach (var sink in _sinks)
                sink.StopCollection();

            _collecting = false;
            _captureExceptions = false;

            // Forget what we last identified. StopCollection DISCARDS the sink's pending queue,
            // so an identify sent before a revoke may never have uploaded - and if consent is
            // granted again with the profile unchanged, the dedup above would skip the re-identify
            // and the person would carry no properties (no invite_wave, so no cohort) for the rest
            // of the session. Dropping the signature makes the next start always re-identify.
            _lastIdentifySignature = null;

            Log("Data collection stopped (consent revoked).");
        }

        #endregion

        #region Recording

        /// <summary>
        /// Records a custom event. Parameter values must be string, int, long,
        /// float, double, or bool, and every event/parameter must be declared in
        /// the UGS dashboard Event Manager.
        /// </summary>
        public void RecordEvent(string eventName, IDictionary<string, object> parameters = null)
        {
            if (!_collecting)
            {
                _droppedEvents++;
                WarnNotCollectingOnce(eventName);
                return;
            }

            var payload = parameters != null
                ? new Dictionary<string, object>(parameters)
                : new Dictionary<string, object>();

            // Inside the sinks an error must not come back as a client_exception (t_inAnalytics).
            bool outermost = !t_inAnalytics;
            t_inAnalytics = true;
            try
            {
                foreach (var sink in _sinks)
                    sink.RecordEvent(eventName, payload);
            }
            finally
            {
                if (outermost)
                    t_inAnalytics = false;
            }

            Log($"Recorded '{eventName}'.");
        }

        /// <summary>
        /// Says out loud, once, why events are going nowhere.
        ///
        /// Collection is opt-in: both the COPPA age gate and consent default to denied, so an
        /// unanswered privacy flow silently drops every event before it reaches any sink - both
        /// UGS and Froglet Analytics go quiet at once, which looks like a backend or key problem and is
        /// not. That failure has already cost this project one debugging session, so it warns
        /// rather than whispering behind a verbose-logging flag.
        /// </summary>
        void WarnNotCollectingOnce(string eventName)
        {
            if (_warnedNotCollecting)
                return;

            _warnedNotCollecting = true;

            string why = !AgeChecked ? "the age gate has not been answered"
                : !AgeEligible ? "the player is not age-eligible (under 13)"
                : !ConsentDecided ? "consent has not been answered"
                : !ConsentGranted ? "consent was declined"
                : !_signedIn ? "UGS sign-in has not completed"
                : !_isConnected ? "there is no network connection"
                : "collection has not started yet";

            Debug.LogWarning(
                $"[Analytics] DROPPING EVENTS - {why}. Nothing will reach UGS or Froglet Analytics until this " +
                $"is resolved. First dropped event: '{eventName}'. " +
                (NeedsPrivacyFlow
                    ? "No privacy consent UI is reachable in this scene; in the editor use " +
                      "FrogletTools > Services > Analytics Consent (Dev) to grant it."
                    : "Check AnalyticsServiceFacade.StartCollectionIfReady."));
        }

        /// <summary>Events dropped because collection was not active. 0 is the healthy value.</summary>
        public int DroppedEventCount => _droppedEvents;

        /// <summary>
        /// Identity + build context that Froglet Analytics has no way to collect on its own.
        ///
        /// This is deliberately NOT applied to every event in the facade. UGS Analytics
        /// already auto-collects the player id, platform and app version as core data, and it
        /// validates every custom parameter against a schema hand-created in the dashboard
        /// Event Manager - where events and parameters, once created, can never be deleted and
        /// count against a 1,500-per-environment cap. Stamping six redundant fields onto 28
        /// events would have cost 168 permanent, unnecessary schema rows for data UGS already
        /// has. So the envelope is a Froglet Analytics concern and lives in the Froglet Analytics sink.
        /// See Docs/Analytics/DATA_ARCHITECTURE.md §7.1.
        /// </summary>
        public IDictionary<string, object> BuildSinkEnvelope() => new Dictionary<string, object>
        {
            ["player_id"] = ResolveDistinctId(),
            ["app_version"] = Application.version,
            ["platform"] = Application.platform.ToString(),
            ["schema_version"] = EventSchemaVersion,
            // The Web build and the per-device views (AnalyticsDevice): Froglet Analytics only, like the rest.
            ["build_kind"] = AnalyticsDevice.Current.BuildKind,
            ["device"] = AnalyticsDevice.Current.Device,
            ["os"] = AnalyticsDevice.Current.Os,
            ["device_model"] = AnalyticsDevice.Current.DeviceModel
        };

        /// <summary>
        /// The canonical identity: the UGS player id. Immutable, and the same key as Cloud
        /// Save and Leaderboards, so Froglet Analytics joins to both with no mapping table. Display name
        /// is a person PROPERTY (see <see cref="IdentifyPlayer"/>), never the identity - it is
        /// mutable and two players can choose the same one.
        /// </summary>
        string ResolveDistinctId()
        {
            try
            {
                if (AuthData != null && !string.IsNullOrEmpty(AuthData.PlayerId))
                    return AuthData.PlayerId;
            }
            catch { /* auth not ready */ }

            return string.Empty;
        }

        /// <summary>
        /// The invite-wave cohort label for a player, derived from when they were first seen:
        /// the <c>"yyyy-MM-dd"</c> key of the UTC week (its Monday) that their first session
        /// fell in. This is the dimension the paid-EA gate splits retention and stability by.
        ///
        /// <para><b>It is a PROXY for a Steam grant batch, not the grant batch itself.</b> Steam
        /// hands the client no wave number - there is no Steam SDK in this build at all, and
        /// grants are issued in Steamworks - so the wave has to be derived. Waves go out in
        /// weekly batches, which makes week-of-first-play a faithful stand-in, but a tester who
        /// requested access in week 1 and installed in week 3 lands in wave 3. Anyone reading
        /// the gate needs to know that; see DATA_ARCHITECTURE.md 7.3.3.</para>
        ///
        /// <para>The week boundary is <see cref="WeeklyChallengeCatalogSO.WeekStartUtc"/>, the
        /// project's one definition of "which week" - reused rather than reimplemented, because
        /// two definitions of a week boundary in one project is a reporting bug nobody finds.
        /// Note this is the REAL week key (<c>WeekKeyFor</c>), never the catalog's instance
        /// <c>PeriodKeyFor</c>, whose test mode shortens a "week" to minutes - a shrunken test
        /// period must never reach a cohort the release gate is read off.</para>
        ///
        /// <para>Returns empty for an unknown first-seen. 0 is "never stamped", and
        /// <c>FromUnixTimeMilliseconds(0)</c> would bucket that player into the week of
        /// 1969-12-29 - a cohort that looks real and is not.</para>
        /// </summary>
        public static string InviteWaveFor(long firstSeenUtcMs)
        {
            if (firstSeenUtcMs <= 0)
                return string.Empty;

            return WeeklyChallengeCatalogSO.WeekKeyFor(
                DateTimeOffset.FromUnixTimeMilliseconds(firstSeenUtcMs).UtcDateTime);
        }

        /// <summary>
        /// Mirrors cloud-save state onto the Froglet Analytics person, so events can be segmented by
        /// player state without a join. Grouped exactly like PLAYER_PROFILE, which is what
        /// makes this mapping mechanical rather than a hand-maintained list.
        ///
        /// display_name is included (approved as P2 personal data). That makes the privacy
        /// policy + consent disclosure a release blocker - see DATA_ARCHITECTURE.md §8.
        /// </summary>
        public void IdentifyPlayer()
        {
            if (!_collecting)
                return;

            string distinctId = ResolveDistinctId();
            if (string.IsNullOrEmpty(distinctId))
                return;

            var properties = new Dictionary<string, object>();

            var profile = PlayerDataService.Instance?.CurrentProfile;
            if (profile != null)
            {
                properties["display_name"] = profile.Identity.DisplayName ?? string.Empty;
                properties["avatar_id"] = profile.Identity.AvatarId;
                properties["crystal_balance"] = profile.Economy.CrystalBalance;
                properties["lifetime_crystals_earned"] = profile.Economy.LifetimeCrystalsEarned;
                properties["lifetime_crystals_spent"] = profile.Economy.LifetimeCrystalsSpent;

                // First-seen, and the wave derived from it, are written ONLY when known.
                // PlayerDataService builds a local-default profile in Awake, so an identify
                // that lands before the cloud profile arrives would otherwise $set
                // first_seen_utc_ms = 0 and invite_wave = "" OVER the real values - and PostHog
                // person properties are last-write-wins. An omitted key changes nothing on the
                // person; a written empty one destroys the cohort the release gate reads.
                long firstSeenUtcMs = profile.Lifecycle.FirstSeenUtcMs;
                if (firstSeenUtcMs > 0)
                {
                    properties["first_seen_utc_ms"] = firstSeenUtcMs;
                    properties["invite_wave"] = InviteWaveFor(firstSeenUtcMs);
                }

                properties["session_count"] = profile.Lifecycle.SessionCount;
                properties["games_completed"] = profile.Lifecycle.GamesCompleted;
                properties["total_flight_time_seconds"] = profile.Lifecycle.TotalFlightTimeSeconds;
            }

            var dataService = UGSDataService.Instance;
            var hangar = dataService?.Hangar?.Data;
            if (hangar != null)
            {
                properties["preferred_vessel"] = hangar.PreferredVessel ?? string.Empty;
                properties["selected_vessel"] = hangar.SelectedVessel ?? string.Empty;
                properties["unlocked_vessel_count"] = hangar.UnlockedVesselCount();
            }

            var progression = dataService?.Progression?.Data;
            if (progression?.UnlockedModes != null)
                properties["unlocked_mode_count"] = progression.UnlockedModes.Count;

            // Send only when something actually CHANGED. Every identify is a billable event on
            // the Froglet Analytics side, and OnProfileChanged is raised by things that do not always move
            // a person property - RefreshProfileVisuals raises it for a pure UI repaint. Before
            // the profile-change hook existed every caller was a segment boundary (collection
            // start, pause, quit, freestyle exit) and this could not matter; now it can.
            //
            // A real change (crystals awarded, a rename, a game completed) still sends, which is
            // the point - this drops the no-ops, not the updates. distinctId is part of the
            // signature so a change of identity always re-identifies.
            string signature = BuildIdentifySignature(distinctId, properties);
            if (signature == _lastIdentifySignature)
                return;

            _lastIdentifySignature = signature;

            foreach (var sink in _sinks)
                sink.Identify(distinctId, properties);
        }

        /// <summary>
        /// Order-independent fingerprint of one identify payload. Sorted because a dictionary
        /// makes no ordering promise across runtimes, and an ordering-sensitive signature would
        /// re-send an identical payload rather than dropping it - failing open, which is the safe
        /// direction but defeats the purpose.
        ///
        /// <para>Public because its failure mode is SILENT: a signature that is not sensitive
        /// enough suppresses an identify that needed to go out, and nothing anywhere reports a
        /// person who quietly stopped being updated. <c>AnalyticsWaveCohortTests</c> holds it.</para>
        /// </summary>
        public static string BuildIdentifySignature(string distinctId, Dictionary<string, object> properties)
        {
            var keys = new List<string>(properties.Keys);
            keys.Sort(StringComparer.Ordinal);

            var sb = new System.Text.StringBuilder(distinctId);
            foreach (var key in keys)
            {
                sb.Append('\u001f').Append(key).Append('=');
                // Invariant so a locale cannot make an unchanged float look changed (or, worse,
                // two different values look identical under a low-precision format).
                sb.Append(Convert.ToString(properties[key], CultureInfo.InvariantCulture));
            }

            return sb.ToString();
        }

        public void RecordPlayAgain() => RecordEvent(UGSKeys.EventPlayAgain);

        /// <summary>
        /// Uploads the SDK's event batch immediately. Reserved for moments the
        /// process may die (pause/quit) - everywhere else the SDK's own batching
        /// is cheaper and loses nothing.
        /// </summary>
        public void Flush()
        {
            if (!_collecting)
                return;

            foreach (var sink in _sinks)
                sink.Flush();
        }

        #endregion

        #region Game lifecycle (game_started / game_completed)

        // OnMiniGameTurnStarted fires once per turn; the flag collapses that to
        // one game_started per game, for both single-player and multiplayer
        // controllers. Menu freestyle never raises it, so the lava lamp stays out.
        void HandleTurnStarted()
        {
            if (_gameInProgress)
                return;

            _gameInProgress = true;
            _gameStartTime = Time.realtimeSinceStartup;

            var parameters = BuildGameParameters();
            AddMatchEnvelope(parameters);

            // What match_abandoned repeats if this match is left before its end.
            _matchStartParameters = new Dictionary<string, object>(parameters);
            _matchSceneName = SceneManager.GetActiveScene().name ?? string.Empty;
            _matchStartUtc = DateTime.UtcNow;
            _networkLostDuringMatch = false;

            RecordEvent(UGSKeys.EventGameStarted, parameters);
        }

        void HandleMiniGameEnd()
        {
            if (!_gameInProgress)
                return;

            _gameInProgress = false;
            _matchStartParameters = null;
            var parameters = BuildGameParameters();

            // Wall-clock, pause included. Kept alongside flight_time_seconds because the
            // DELTA between the two is the pause + AFK + between-round time - a churn signal
            // we would otherwise have to instrument separately.
            parameters["duration_seconds"] = Mathf.RoundToInt(Time.realtimeSinceStartup - _gameStartTime);

            // Time actually at the stick: control granted -> game end, pause and background
            // excluded. GameDataSO.InvokeMiniGameEnd settles the clock before raising, so this
            // is final by the time we read it.
            parameters["flight_time_seconds"] = FlightClock.LastGameSeconds;

            // Echo the grouping keys so completion joins back to game_started.
            parameters["match_id"] = _gameData.MatchId ?? string.Empty;
            parameters["party_id"] = _gameData.PartyId ?? string.Empty;

            AddCompletionTimestamp(parameters);

            bool resolved = TryResolveLocalWin(out bool won);

            AddBalanceFields(parameters, resolved ? won : (bool?)null);

            if (resolved)
            {
                parameters["player_won"] = won;
                if (won)
                {
                    _consecutiveLosses = 0;
                }
                else
                {
                    _consecutiveLosses++;
                    if (_consecutiveLosses >= RepeatedFailThreshold)
                        RecordEvent(UGSKeys.EventRepeatedGameFail, new Dictionary<string, object>
                        {
                            { "game_mode", _gameData.GameMode.ToString() },
                            { "fail_count", _consecutiveLosses }
                        });
                }
            }

            RecordEvent(UGSKeys.EventGameCompleted, parameters);
        }

        /// <summary>
        /// Best-effort local win/loss at game end. Team modes resolve by domain;
        /// otherwise by winner name vs the local player. Defensive - never throws
        /// or logs, returns false when the result can't be determined.
        /// </summary>
        bool TryResolveLocalWin(out bool won)
        {
            won = false;
            try
            {
                IPlayer local = _gameData.LocalPlayer;
                if (local == null)
                    return false;

                // Domain (team) modes set WinnerDomain to a playable domain; Blue is the neutral sentinel.
                if (_gameData.WinnerDomain != Domains.Blue)
                {
                    won = local.Domain == _gameData.WinnerDomain;
                    return true;
                }

                if (!string.IsNullOrEmpty(_gameData.WinnerName))
                {
                    won = _gameData.WinnerName == local.Name;
                    return true;
                }
            }
            catch
            {
                // Result not populated for this mode - omit player_won.
            }

            return false;
        }

        /// <summary>
        /// Balance (Froglet Analytics only): where the player finished and against which AI level. Each
        /// field is left out when the game cannot say, never guessed. Reading a roster that is being torn
        /// down must never cost the match its game_completed, so a failure here drops only these fields,
        /// the same way <see cref="TryResolveLocalWin"/> drops player_won.
        /// </summary>
        void AddBalanceFields(IDictionary<string, object> parameters, bool? localWon)
        {
            try
            {
                if (TryCollectScores(out var scores, out int localIndex))
                {
                    parameters[UGSKeys.ParamPlayersInMatch] = scores.Count;
                    if (MatchStanding.TryResolve(scores, localIndex, localWon, out var standing))
                        standing.AddTo(parameters);
                }

                string aiDifficulty = ResolveOpponentAiDifficulty();
                if (aiDifficulty != null)
                    parameters[UGSKeys.ParamAiDifficulty] = aiDifficulty;
            }
            catch (Exception)
            {
                // Roster not readable at this moment - send the match without its balance fields.
            }
        }

        /// <summary>
        /// Every pilot's final score, best first, and the local player's row in it: the mode's ranked
        /// <see cref="GameDataSO.Results"/> when it produced them, else the round stats, which every base
        /// controller sorts best-first just before it ends the game. False when there are no rows.
        /// <paramref name="localIndex"/> is -1 when the local player is not among them.
        /// </summary>
        bool TryCollectScores(out List<float> scores, out int localIndex)
        {
            scores = null;
            localIndex = -1;
            var local = _gameData.LocalPlayer;

            var results = _gameData.Results;
            if (results is { Count: > 0 })
            {
                scores = new List<float>(results.Count);
                for (int i = 0; i < results.Count; i++)
                {
                    if (localIndex < 0 && local != null && results[i].Name == local.Name)
                        localIndex = i;
                    scores.Add(results[i].Score);
                }
                return true;
            }

            var stats = _gameData.RoundStatsList;
            if (stats is not { Count: > 0 })
                return false;

            var localStats = _gameData.LocalRoundStats;
            scores = new List<float>(stats.Count);
            foreach (var row in stats)
            {
                if (!UnityLiveness.Alive(row))
                    continue;
                if (localIndex < 0 && (ReferenceEquals(row, localStats) || (local != null && row.Name == local.Name)))
                    localIndex = scores.Count;
                scores.Add(row.Score);
            }

            return scores.Count > 0;
        }

        /// <summary>
        /// <c>ai_difficulty</c>: the level the AI OPPONENTS (AI on another domain than the local player's)
        /// flew, or null when none opposed the player. Only the machine that seated the bots knows it - the
        /// server stamps each pilot's level (<see cref="GameDataSO.AIDifficultyFor"/>), and the launch
        /// panel writes the match's level on the launching machine only - so a guest in someone else's
        /// match sends nothing rather than its own stale default.
        /// </summary>
        string ResolveOpponentAiDifficulty()
        {
            if (!IsMatchAuthority())
                return null;

            var local = _gameData.LocalPlayer;
            var players = _gameData.Players;
            if (players == null)
                return null;

            var levels = new List<AIDifficulty>();
            foreach (var player in players)
            {
                if (!UnityLiveness.Alive(player) || !player.IsInitializedAsAI)
                    continue;
                if (local != null && player.Domain == local.Domain)
                    continue;
                levels.Add(_gameData.AIDifficultyFor(player));
            }

            return MatchStanding.AiDifficultyLabel(levels);
        }

        /// <summary>True on the host or server, and in a game with no network session at all.</summary>
        static bool IsMatchAuthority()
        {
            var nm = NetworkManager.Singleton;
            return nm == null || !nm.IsListening || nm.IsServer;
        }

        // ── match_abandoned ──

        /// <summary>
        /// A scene change is starting (SceneLoader raises false when it leaves a scene). Raised before the
        /// loader resets the round data, so the score and flight time are still there to read.
        /// </summary>
        void HandleSceneTransition(bool entered)
        {
            if (!entered)
                MaybeRecordMatchAbandoned(SceneExitReason());
        }

        /// <summary>
        /// The backstop for routes that leave a match without the loader's transition (a guest's bounce
        /// when its link to the host breaks, the host taking the party to the menu): the active scene
        /// moved off the match's scene while the match was running.
        /// </summary>
        void HandleActiveSceneChanged(Scene previous, Scene next)
        {
            _activeSceneName = next.name ?? string.Empty;
            if (_gameInProgress && !string.Equals(next.name, _matchSceneName, StringComparison.Ordinal))
                MaybeRecordMatchAbandoned(SceneExitReason());
        }

        string SceneExitReason() =>
            MatchAbandonReason.ForSceneExit(LinkDroppedThisMatch(), _networkLostDuringMatch);

        /// <summary>The guest's bounce marks the remembered match dropped (MatchRejoin.MarkDropped) before it leaves.</summary>
        bool LinkDroppedThisMatch() =>
            MatchRejoin.TryLoad(out var record) && record.Dropped && record.droppedAtUtcTicks >= _matchStartUtc.Ticks;

        /// <summary>
        /// One match_abandoned for a match left before its end. It ends the match for analytics, so it
        /// fires once at most and never together with game_completed (both clear _gameInProgress).
        /// </summary>
        void MaybeRecordMatchAbandoned(string reason)
        {
            if (!_gameInProgress)
                return;

            _gameInProgress = false;

            var start = _matchStartParameters;
            _matchStartParameters = null;
            if (start == null)
            {
                start = BuildGameParameters();
                AddMatchEnvelope(start);
            }

            float? flightSeconds = FlightClock.IsGameActive ? FlightClock.CurrentGameSeconds : (float?)null;

            float? scoreAtQuit = null;
            int? placementAtQuit = null;
            string aiDifficulty = null;
            try
            {
                var localStats = _gameData.LocalRoundStats;
                if (localStats != null)
                {
                    scoreAtQuit = localStats.Score;
                    placementAtQuit = PlacementAtQuit(localStats);
                }

                aiDifficulty = ResolveOpponentAiDifficulty();
            }
            catch (Exception)
            {
                // A roster mid-teardown: the abandon is still recorded, without these fields.
            }

            RecordEvent(UGSKeys.EventMatchAbandoned, BuildMatchAbandonedParameters(start,
                Mathf.RoundToInt(Time.realtimeSinceStartup - _gameStartTime), flightSeconds, reason,
                aiDifficulty, scoreAtQuit, placementAtQuit));
        }

        /// <summary>
        /// The local player's rank by score at the moment of the quit, only where that means something:
        /// a mode whose scoring rule ranks higher scores first, with some score already on the board.
        /// A golf mode (finish times) scores nobody until they finish, so it sends nothing.
        /// </summary>
        int? PlacementAtQuit(IRoundStats localStats)
        {
            var rule = _gameData.ScoringRule;
            if (rule == null || rule.GolfRules)
                return null;

            var scores = new List<float>();
            foreach (var row in _gameData.RoundStatsList)
                if (UnityLiveness.Alive(row))
                    scores.Add(row.Score);

            return MatchStanding.PlacementAmong(scores, localStats.Score);
        }

        /// <summary>
        /// The <c>match_abandoned</c> payload (Docs/Analytics/EVENT_SCHEMA.json): game_started's fields
        /// for the match (without player_ids, which only game_started carries), then how far it got.
        /// </summary>
        public static Dictionary<string, object> BuildMatchAbandonedParameters(
            IDictionary<string, object> gameStartedParameters, int elapsedSeconds, float? flightTimeSeconds,
            string reason, string aiDifficulty, float? scoreAtQuit, int? placementAtQuit)
        {
            var p = gameStartedParameters != null
                ? new Dictionary<string, object>(gameStartedParameters)
                : new Dictionary<string, object>();
            p.Remove("player_ids");

            p[UGSKeys.ParamElapsedSeconds] = Math.Max(0, elapsedSeconds);
            if (flightTimeSeconds.HasValue)
                p[UGSKeys.ParamFlightTimeSeconds] = flightTimeSeconds.Value;
            p[UGSKeys.ParamReason] = string.IsNullOrEmpty(reason) ? MatchAbandonReason.QuitToMenu : reason;
            if (aiDifficulty != null)
                p[UGSKeys.ParamAiDifficulty] = aiDifficulty;
            if (scoreAtQuit.HasValue)
                p[UGSKeys.ParamScoreAtQuit] = scoreAtQuit.Value;
            if (placementAtQuit.HasValue)
                p[UGSKeys.ParamPlacementAtQuit] = placementAtQuit.Value;
            return p;
        }

        /// <summary>
        /// Adds the identifiers that let us group players who played together, and separate
        /// organic rematches from re-invited ones.
        ///
        /// match_id / party_id / invite_triggered are host-stamped and replicated
        /// (MultiplayerMiniGameControllerBase), so every client reports identical values.
        ///
        /// player_ids is derived here from replicated Player NetworkObjects and SORTED. Deriving
        /// it from the local party roster would disagree between clients (join order, a peer
        /// that dropped during scene load); deriving it from replicated state and sorting is
        /// deterministic on every peer. AI is excluded and counted separately.
        /// See Docs/Analytics/DATA_ARCHITECTURE.md §6.
        ///
        /// The same loop counts the match setup per domain from the local player's side
        /// (<see cref="DomainComposition"/>); game_started only, game_completed joins back on match_id.
        /// </summary>
        void AddMatchEnvelope(IDictionary<string, object> parameters)
        {
            parameters["match_id"] = _gameData.MatchId ?? string.Empty;
            parameters["party_id"] = _gameData.PartyId ?? string.Empty;
            parameters["invite_triggered"] = _gameData.InviteTriggered;

            var humanIds = new List<string>();
            int aiCount = 0;

            // The match setup from the LOCAL player's side: every pilot counted below (each AI, each
            // human in player_ids) with its domain and whether an AI flies it. Only collected when
            // there is a local player - see DomainComposition.
            var local = _gameData.LocalPlayer;
            var seats = local != null ? new List<(Domains Domain, bool IsAI)>() : null;

            var players = _gameData.Players;
            if (players != null)
            {
                for (int i = 0; i < players.Count; i++)
                {
                    var player = players[i];
                    if (player == null) continue;

                    if (player.IsInitializedAsAI)
                    {
                        aiCount++;
                        seats?.Add((player.Domain, true));
                        continue;
                    }

                    string id = player.UgsPlayerId;
                    if (string.IsNullOrEmpty(id) || humanIds.Contains(id))
                        continue;

                    humanIds.Add(id);
                    seats?.Add((player.Domain, false));
                }
            }

            humanIds.Sort(StringComparer.Ordinal);

            if (local != null)
                DomainComposition.Count(local.Domain, seats).AddTo(parameters, local.Domain);

            // Comma-joined, not an array: UGS Analytics parameters accept only scalar values.
            // The Froglet Analytics sink splits this back into a real array (commit 4).
            parameters["player_ids"] = string.Join(",", humanIds);
            parameters["player_count_human"] = humanIds.Count;
            parameters["player_count_ai"] = aiCount;
        }

        /// <summary>
        /// Stamps the client's completion time in both forms: epoch milliseconds for
        /// arithmetic, ISO-8601 for day/week/month bucketing without a per-query conversion.
        /// UGS and Froglet Analytics both stamp their own ingest time; this is the CLIENT time, which is
        /// what "timestamp at completion" means.
        /// </summary>
        static void AddCompletionTimestamp(IDictionary<string, object> parameters)
        {
            var nowUtc = DateTime.UtcNow;
            parameters["timestamp_utc_ms"] = new DateTimeOffset(nowUtc).ToUnixTimeMilliseconds();
            parameters["timestamp_utc_iso"] = nowUtc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
        }

        Dictionary<string, object> BuildGameParameters()
        {
            var parameters = new Dictionary<string, object>
            {
                { "game_mode", _gameData.GameMode.ToString() },
                { "intensity", _gameData.SelectedIntensity.Value },
                { "vessel_class", _gameData.selectedVesselClass.Value.ToString() },
                { "player_count", _gameData.SelectedPlayerCount.Value },
                { "ai_count", _gameData.RequestedAIBackfillCount },
                { "is_multiplayer", _gameData.IsMultiplayerMode }
            };

            // The match's element: the same answer the card's genre flower draws (the mode's row,
            // else its preview's scoring metric). Froglet Analytics only - the UGS sink strips it.
            if (ArcadeGridOrder.TryGenres(_gameData.GameMode, out var primary, out var secondary))
                AddGenreElements(parameters, primary, secondary);

            return parameters;
        }

        /// <summary>
        /// Adds <c>element</c> and, for a two-element genre, <c>element_secondary</c>. A mode with
        /// no genre (<see cref="Element.None"/>) gets neither key - absent, never an empty string or
        /// "None", so a dashboard's element split has no fake bucket.
        /// </summary>
        public static void AddGenreElements(IDictionary<string, object> parameters, Element primary, Element secondary)
        {
            if (primary == Element.None)
                return;

            parameters[UGSKeys.ParamElement] = primary.ToString();
            if (secondary != Element.None)
                parameters[UGSKeys.ParamElementSecondary] = secondary.ToString();
        }

        #endregion

        #region Session lifecycle (session_ended + flush)

        void HandleAppPaused(bool paused)
        {
            if (paused)
            {
                _session.EnterBackground(DateTime.UtcNow);
                FlushHeldExceptions();
                RecordSessionEnded("pause");
                IdentifyPlayer();
                Flush();
                _frogletSink?.SaveQueue();
            }
            else
            {
                // Resumed: the next pause/quit counts as a fresh session end.
                _sessionEndRecorded = false;

                // Back after more than 30 minutes away: a new session, and every fault may be sent again.
                if (_session.ReturnToForeground(DateTime.UtcNow))
                {
                    _exceptions.Reset();
                    MaybeRecordSessionStarted();
                }
            }
        }

        void HandleAppQuitting()
        {
            // A match still running when the app closes was left before its end.
            MaybeRecordMatchAbandoned(MatchAbandonReason.AppQuit);
            _session.EnterBackground(DateTime.UtcNow);
            FlushHeldExceptions();
            RecordSessionEnded("quit");
            IdentifyPlayer();
            Flush();
            _frogletSink?.SaveQueue();
        }

        /// <summary>One session_started per session, once collection runs (consent and sign-in can come late).</summary>
        void MaybeRecordSessionStarted()
        {
            if (!_collecting || _session.StartRecorded)
                return;

            _session.StartRecorded = true;
            RecordEvent(UGSKeys.EventSessionStarted,
                BuildSessionStartedParameters(_session.SessionId, _session.LaunchKind));
        }

        /// <summary>The <c>session_started</c> payload (Docs/Analytics/EVENT_SCHEMA.json).</summary>
        public static Dictionary<string, object> BuildSessionStartedParameters(string sessionId, string launchKind) => new()
        {
            { UGSKeys.ParamSessionId, sessionId ?? string.Empty },
            { UGSKeys.ParamLaunchKind, string.IsNullOrEmpty(launchKind) ? AnalyticsSessionClock.LaunchCold : launchKind }
        };

        /// <summary>
        /// The <c>session_ended</c> payload. session_id and session_seconds (foreground seconds in the
        /// session so far) are Froglet Analytics only: the UGS sink strips them.
        /// </summary>
        public static Dictionary<string, object> BuildSessionEndedParameters(string reason, string lastUiAction,
            string appState, string sessionId, double sessionSeconds) => new()
        {
            { "reason", reason },
            { "last_ui_action", lastUiAction ?? string.Empty },
            { "app_state", appState ?? string.Empty },
            { UGSKeys.ParamSessionId, sessionId ?? string.Empty },
            { UGSKeys.ParamSessionSeconds, (int)Math.Round(Math.Max(0d, sessionSeconds)) }
        };

        void RecordSessionEnded(string reason)
        {
            if (_sessionEndRecorded)
                return;

            _sessionEndRecorded = true;
            RecordEvent(UGSKeys.EventSessionEnded, BuildSessionEndedParameters(reason, _lastUiAction,
                _appStateVariable.Value.State.ToString(), _session.SessionId,
                _session.ForegroundSeconds(DateTime.UtcNow)));

            // Crystal balance at session end (hoarding vs. spending). PlayerDataService
            // is the sole holder of the balance; read it defensively.
            var profile = PlayerDataService.Instance;
            if (profile != null)
                RecordEvent(UGSKeys.EventCrystalBalanceSnapshot, new Dictionary<string, object>
                {
                    { "balance", profile.GetCrystalBalance() }
                });
        }

        #endregion

        #region UI actions & ads

        // UserActionSystem is a scene singleton (Bootstrap, DailyChallengeSystem
        // prefab). It exists by the time DI constructs this facade, but the
        // sign-in retry covers any load-order drift.
        void TryWireUiActions()
        {
            if (_uiActionsWired || UserActionSystem.Instance == null)
                return;

            UserActionSystem.Instance.OnUserActionCompleted += HandleUserAction;
            _uiActionsWired = true;
        }

        /// <summary>
        /// Re-identify whenever the cloud profile changes, so person properties are refreshed
        /// the moment the real values exist rather than only at pause/quit.
        ///
        /// <para>This is what DATA_ARCHITECTURE.md 7.3.2 always claimed ("refreshed on profile
        /// change and at session end") and what was actually missing. The gap matters most for
        /// exactly the thing the paid-EA gate measures: the first identify of a session fires
        /// from StartCollectionIfReady, which races the cloud-save load, so a session that ended
        /// in a CRASH could reach pause/quit never and leave the person with no invite_wave -
        /// systematically under-attributing the crashiest cohort.</para>
        ///
        /// <para>Deferred and duplicate-guarded like <see cref="TryWireUiActions"/>: this facade
        /// is a lazy DI singleton and may be constructed before PlayerDataService exists.</para>
        /// </summary>
        void TryWireProfileRefresh()
        {
            if (_profileRefreshWired || PlayerDataService.Instance == null)
                return;

            PlayerDataService.Instance.OnProfileChanged += HandleProfileChanged;
            _profileRefreshWired = true;
        }

        void HandleProfileChanged(PlayerProfileData _) => IdentifyPlayer();

        void HandleUserAction(UserAction action)
        {
            _lastUiAction = action.Label;
            RecordEvent(UGSKeys.EventUiAction, new Dictionary<string, object>
            {
                { "action_type", action.ActionType.ToString() },
                { "action_label", action.Label },
                { "action_value", action.Value }
            });
        }


        #endregion

        #region Phase 2 - typed events (called by injected systems)

        /// <summary>Menu became fully interactive. Call from MainMenuController on ready.</summary>
        public void RecordMenuReady()
        {
            bool isFirst = !_menuReadyThisSession;
            _menuReadyThisSession = true;
            RecordEvent(UGSKeys.EventMenuReady, new Dictionary<string, object>
            {
                { "is_first", isFirst },
                { "seconds_since_launch", SecondsSinceLaunch }
            });
        }

        public void RecordModeUnlocked(GameModes mode) =>
            RecordEvent(UGSKeys.EventModeUnlocked, new Dictionary<string, object>
            {
                { "game_mode", mode.ToString() }
            });

        public void RecordIntensityUnlocked(GameModes mode, int intensity) =>
            RecordEvent(UGSKeys.EventIntensityUnlocked, new Dictionary<string, object>
            {
                { "game_mode", mode.ToString() },
                { "intensity", intensity }
            });

        public void RecordCrystalsEarned(int amount, string source, int balance) =>
            RecordEvent(UGSKeys.EventCrystalsEarned, new Dictionary<string, object>
            {
                { "amount", amount },
                { "source", string.IsNullOrEmpty(source) ? "unknown" : source },
                { "balance", balance }
            });

        public void RecordCrystalsSpent(int amount, string source, int balance) =>
            RecordEvent(UGSKeys.EventCrystalsSpent, new Dictionary<string, object>
            {
                { "amount", amount },
                { "source", string.IsNullOrEmpty(source) ? "unknown" : source },
                { "balance", balance }
            });

        public void RecordCrystalSpendBlocked(int amount, string item, int balance) =>
            RecordEvent(UGSKeys.EventCrystalSpendBlocked, new Dictionary<string, object>
            {
                { "amount", amount },
                { "item", string.IsNullOrEmpty(item) ? "unknown" : item },
                { "balance", balance }
            });

        public void RecordVesselUnlocked(string vessel, int cost, int balance) =>
            RecordEvent(UGSKeys.EventVesselUnlocked, new Dictionary<string, object>
            {
                { "vessel", string.IsNullOrEmpty(vessel) ? "unknown" : vessel },
                { "cost", cost },
                { "balance", balance }
            });

        public void RecordQuestCompleted(string questTitle, int shardValue) =>
            RecordEvent(UGSKeys.EventQuestCompleted, new Dictionary<string, object>
            {
                { "quest", string.IsNullOrEmpty(questTitle) ? "unknown" : questTitle },
                { "shard_value", shardValue }
            });

        public void RecordShareTriggered(string gameMode) =>
            RecordEvent(UGSKeys.EventShareTriggered, new Dictionary<string, object>
            {
                { "game_mode", string.IsNullOrEmpty(gameMode) ? "unknown" : gameMode }
            });

        // ── Handlers for subscribed events ──

        void HandleFreestyleEntered()
        {
            bool isFirst = !_freestyleEnteredThisSession;
            _freestyleEnteredThisSession = true;

            // A new stretch starts in the vessel the player just took. Re-subscribing here as well
            // as in the constructor keeps the clock's handler alive whatever order the
            // BeforeSceneLoad reset (FlightClock.Initialize clears its event) and this lazy
            // singleton's construction ran in; a stretch can only end after it has begun.
            SubscribeFreestyleClock();
            string vessel = LocalVesselClassName() ?? UnknownVessel;
            _freestyleVessels.Begin(vessel);

            RecordEvent(UGSKeys.EventFreestyleEntered, new Dictionary<string, object>
            {
                { "is_first", isFirst },
                { UGSKeys.ParamSecondsSinceLaunch, SecondsSinceLaunch },
                { UGSKeys.ParamVesselClass, vessel }
            });
        }

        void SubscribeFreestyleClock()
        {
            FlightClock.OnFreestyleSegmentCompleted -= HandleFreestyleSegmentCompleted;
            FlightClock.OnFreestyleSegmentCompleted += HandleFreestyleSegmentCompleted;
        }

        /// <summary>
        /// One <c>freestyle_ended</c> per stretch: FlightClock closes a freestyle segment when the
        /// player leaves freestyle (back to the menu, into a game, out of Menu_Main) and on pause or
        /// backgrounding. The next stretch, if the player keeps flying, starts in the vessel flown now.
        /// </summary>
        void HandleFreestyleSegmentCompleted(float seconds)
        {
            // The vessel can already be gone (Menu_Main tearing down banks its segment from
            // OnDisable), so the stretch's own record of what was flown answers then.
            string vessel = LocalVesselClassName() ?? _freestyleVessels.Current ?? UnknownVessel;
            _freestyleVessels.Observe(vessel);

            RecordEvent(UGSKeys.EventFreestyleEnded, BuildFreestyleEndedParameters(
                seconds, vessel, _freestyleVessels.VesselsFlown, SecondsSinceLaunch));

            _freestyleVessels.Begin(vessel);
        }

        /// <summary>
        /// A vessel swap in Freestyle. The Toybox's Vessel Changer raises nothing of its own; every
        /// swap re-initialises the local player's pair (<c>ClientPlayerVesselInitializer.ReInitializePair</c>
        /// raises <see cref="GameDataSO.OnPlayerPairInitialized"/> on the host and on a client), so this
        /// observes that and reports a change of the local hull's class while freestyle is flying.
        /// </summary>
        void HandlePlayerPairInitialized(ulong playerNetId)
        {
            if (!FlightClock.IsFreestyleActive)
                return;

            var local = _gameData.LocalPlayer;
            if (local == null || local.PlayerNetId != playerNetId)
                return;

            string vessel = LocalVesselClassName();
            if (vessel == null || !_freestyleVessels.TryChange(vessel, out string from))
                return;

            RecordEvent(UGSKeys.EventFreestyleVesselChanged, BuildFreestyleVesselChangedParameters(
                vessel, from, FlightClock.CurrentFreestyleSeconds));
        }

        /// <summary>The <c>freestyle_ended</c> payload (Docs/Analytics/EVENT_SCHEMA.json).</summary>
        public static Dictionary<string, object> BuildFreestyleEndedParameters(
            float seconds, string vesselClass, int vesselsFlown, int secondsSinceLaunch) => new()
        {
            { UGSKeys.ParamSeconds, seconds },
            { UGSKeys.ParamVesselClass, string.IsNullOrEmpty(vesselClass) ? UnknownVessel : vesselClass },
            { UGSKeys.ParamVesselsFlown, Math.Max(1, vesselsFlown) },
            { UGSKeys.ParamSecondsSinceLaunch, secondsSinceLaunch }
        };

        /// <summary>The <c>freestyle_vessel_changed</c> payload (Docs/Analytics/EVENT_SCHEMA.json).</summary>
        public static Dictionary<string, object> BuildFreestyleVesselChangedParameters(
            string vesselClass, string fromVesselClass, float secondsInFreestyle) => new()
        {
            { UGSKeys.ParamVesselClass, string.IsNullOrEmpty(vesselClass) ? UnknownVessel : vesselClass },
            { UGSKeys.ParamFromVesselClass, string.IsNullOrEmpty(fromVesselClass) ? UnknownVessel : fromVesselClass },
            { UGSKeys.ParamSecondsInFreestyle, secondsInFreestyle }
        };

        /// <summary>The local player's live hull class as its VesselClassType name, or null when there is none.</summary>
        string LocalVesselClassName()
        {
            var vessel = _gameData.LocalPlayer?.Vessel;
            if (!vessel.IsAlive())
                return null;

            var status = vessel.VesselStatus;
            return status.IsAlive() ? status.VesselType.ToString() : null;
        }

        /// <summary>What a vessel field says when no hull can be read, like the other "unknown" fields.</summary>
        const string UnknownVessel = "unknown";

        void HandleSettingChanged(string setting, object value) =>
            RecordEvent(UGSKeys.EventSettingChanged, new Dictionary<string, object>
            {
                { "setting", setting },
                { "value", value?.ToString() ?? string.Empty },
                { "seconds_since_launch", SecondsSinceLaunch }
            });

        // One named handler per GameSetting static event - see the subscription comment above.
        void HandleMusicEnabledChanged(bool v) => HandleSettingChanged("music_enabled", v);
        void HandleSfxEnabledChanged(bool v) => HandleSettingChanged("sfx_enabled", v);
        void HandleHapticsEnabledChanged(bool v) => HandleSettingChanged("haptics_enabled", v);
        void HandleInvertYChanged(bool v) => HandleSettingChanged("invert_y", v);
        void HandleInvertThrottleChanged(bool v) => HandleSettingChanged("invert_throttle", v);
        void HandleJoystickVisualsChanged(bool v) => HandleSettingChanged("joystick_visuals", v);
        void HandleMusicLevelChanged(float v) => HandleSettingChanged("music_level", v);
        void HandleSfxLevelChanged(float v) => HandleSettingChanged("sfx_level", v);
        void HandleHapticsLevelChanged(float v) => HandleSettingChanged("haptics_level", v);

        void HandleFriendRequestSent(FriendData f) =>
            RecordEvent(UGSKeys.EventFriendRequestSent, new Dictionary<string, object>
            {
                { "target_id", f.PlayerId ?? string.Empty }
            });

        void HandleFriendRequestReceived(FriendData f) =>
            RecordEvent(UGSKeys.EventFriendRequestReceived, new Dictionary<string, object>
            {
                { "from_id", f.PlayerId ?? string.Empty }
            });

        void HandleFriendAdded(FriendData f) =>
            RecordEvent(UGSKeys.EventFriendAdded, new Dictionary<string, object>
            {
                { "friend_id", f.PlayerId ?? string.Empty }
            });

        // Invite attribution: both ends derive the same invite_id from sender, target and party session
        // (InviteAttribution - the invite's wire line carries no id of its own).
        void HandlePartyInviteSent(PartyPlayerData p) =>
            RecordEvent(UGSKeys.EventPartyInviteSent, BuildPartyInviteParameters("target_id", p.PlayerId,
                InviteAttribution.InviteIdFor(LocalPartyPlayerId(), p.PlayerId, ActiveSessionId())));

        void HandlePartyInviteReceived(PartyInviteData p)
        {
            string inviteId = InviteAttribution.InviteIdFor(p.HostPlayerId, LocalPartyPlayerId(), p.PartySessionId);
            _invites.RememberReceived(p.PartySessionId, inviteId, p.HostPlayerId);
            RecordEvent(UGSKeys.EventPartyInviteReceived, BuildPartyInviteParameters("host_id", p.HostPlayerId, inviteId));
        }

        /// <summary>
        /// party_joined names the invite that led to it when the party joined is one an invite named; a
        /// join no remembered invite led to (a presence join, a rejoin) carries nothing, as before.
        /// </summary>
        void HandlePartyJoined()
        {
            var parameters = BuildPartyJoinedParameters(
                _invites.TryTakeForJoin(ActiveSessionId(), out string inviteId, out string inviterId) ? inviteId : null,
                inviterId);
            RecordEvent(UGSKeys.EventPartyJoined, parameters.Count > 0 ? parameters : null);
        }

        /// <summary>party_invite_sent / party_invite_received: the other player's id, and the invite id when known.</summary>
        public static Dictionary<string, object> BuildPartyInviteParameters(string idKey, string otherPlayerId, string inviteId)
        {
            var p = new Dictionary<string, object> { { idKey, otherPlayerId ?? string.Empty } };
            if (!string.IsNullOrEmpty(inviteId))
                p[UGSKeys.ParamInviteId] = inviteId;
            return p;
        }

        /// <summary>party_joined: invite_id and inviter_id when an invite led to the join, else empty.</summary>
        public static Dictionary<string, object> BuildPartyJoinedParameters(string inviteId, string inviterId)
        {
            var p = new Dictionary<string, object>();
            if (string.IsNullOrEmpty(inviteId))
                return p;

            p[UGSKeys.ParamInviteId] = inviteId;
            if (!string.IsNullOrEmpty(inviterId))
                p[UGSKeys.ParamInviterId] = inviterId;
            return p;
        }

        /// <summary>This player's id as the party layer writes it into an invite line (its UGS id).</summary>
        string LocalPartyPlayerId() =>
            _hostConnectionData != null && !string.IsNullOrEmpty(_hostConnectionData.LocalPlayerId)
                ? _hostConnectionData.LocalPlayerId
                : ResolveDistinctId();

        /// <summary>The party session this machine is in (GameDataSO.ActiveSession is the party session's one field).</summary>
        string ActiveSessionId()
        {
            try
            {
                return _gameData.ActiveSession?.Id;
            }
            catch (Exception)
            {
                return null; // a torn-down session object
            }
        }

        // ── client_exception ──

        /// <summary>
        /// Application.logMessageReceivedThreaded: any thread. Reads the line (pure string work), keeps
        /// its type, first project frame and message hash, and asks the main thread to send it after
        /// the fold window. Never logs, never throws, never touches a UnityEngine.Object.
        /// </summary>
        void HandleLogMessageThreaded(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Exception && type != LogType.Error)
                return;
            if (!_captureExceptions || t_inAnalytics)
                return;

            t_inAnalytics = true;
            try
            {
                bool isNew = _exceptions.Observe(
                    ClientExceptionLog.ExceptionTypeOf(condition, type == LogType.Exception),
                    ClientExceptionLog.TopFrameOf(stackTrace),
                    _activeSceneName,
                    ClientExceptionLog.MessageHashOf(condition));

                if (isNew && _mainContext != null && Interlocked.CompareExchange(ref _exceptionFlushScheduled, 1, 0) == 0)
                    _mainContext.Post(_ => FlushExceptionsAfterWindowAsync().Forget(), null);
            }
            catch (Exception)
            {
                // A fault while reading a fault is dropped: logging it would come straight back here.
            }
            finally
            {
                t_inAnalytics = false;
            }
        }

        /// <summary>Main thread: waits out the fold window (real time, so a paused game still sends), then sends.</summary>
        async UniTaskVoid FlushExceptionsAfterWindowAsync()
        {
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(ExceptionFoldWindowSeconds), DelayType.Realtime);
            }
            finally
            {
                FlushHeldExceptions();
            }
        }

        /// <summary>Main thread: sends every held fault with its count so far. Pause and quit call it too.</summary>
        void FlushHeldExceptions()
        {
            // Re-arm first: a fault seen from here on starts a window of its own.
            Interlocked.Exchange(ref _exceptionFlushScheduled, 0);

            var held = _exceptions.TakeHeld();
            foreach (var entry in held)
                RecordEvent(UGSKeys.EventClientException, BuildClientExceptionParameters(entry));
        }

        /// <summary>The <c>client_exception</c> payload. No message text: only its hash.</summary>
        public static Dictionary<string, object> BuildClientExceptionParameters(ClientExceptionLog.Entry entry) => new()
        {
            { UGSKeys.ParamExceptionType, string.IsNullOrEmpty(entry.ExceptionType) ? ClientExceptionLog.Unknown : entry.ExceptionType },
            { UGSKeys.ParamTopFrame, string.IsNullOrEmpty(entry.TopFrame) ? ClientExceptionLog.Unknown : entry.TopFrame },
            { UGSKeys.ParamScene, string.IsNullOrEmpty(entry.Scene) ? ClientExceptionLog.Unknown : entry.Scene },
            { UGSKeys.ParamMessageHash, entry.MessageHash ?? string.Empty },
            { UGSKeys.ParamCount, Math.Max(1, entry.Count) }
        };

        void HandleFavoriteChanged(GameModes mode, bool favorited) =>
            RecordEvent(UGSKeys.EventMinigameFavorited, new Dictionary<string, object>
            {
                { "game_mode", mode.ToString() },
                { "favorited", favorited }
            });

        // The quest graph (Assets/FTUE) - the top of the activation funnel. step_id is the node's
        // stable id, so re-wording a node's display name never forks the funnel; step_name rides
        // along so a dashboard row is readable without a lookup table.
        void HandleFtueStepCompleted(string questId, int phaseIndex, string nodeId, string nodeName) =>
            RecordEvent(UGSKeys.EventFtueStepCompleted, BuildFtueStepParameters(
                questId, phaseIndex, nodeId, nodeName, SecondsSinceLaunch));

        void HandleFtuePhaseCompleted(string questId, int phaseIndex) =>
            RecordEvent(UGSKeys.EventFtuePhaseCompleted, new Dictionary<string, object>
            {
                { "quest_id", string.IsNullOrEmpty(questId) ? "unknown" : questId },
                { "phase_index", phaseIndex },
                { "seconds_since_launch", SecondsSinceLaunch }
            });

        void HandleFtueCompleted(string questId) =>
            RecordEvent(UGSKeys.EventFtueCompleted, new Dictionary<string, object>
            {
                { "quest_id", string.IsNullOrEmpty(questId) ? "unknown" : questId },
                { "seconds_since_launch", SecondsSinceLaunch }
            });

        /// <summary>
        /// The <c>ftue_step_completed</c> payload. A node with no authored display name reports
        /// its id as its name rather than an empty string, which a dashboard would group as one
        /// anonymous step.
        /// </summary>
        internal static Dictionary<string, object> BuildFtueStepParameters(
            string questId, int phaseIndex, string nodeId, string nodeName, int secondsSinceLaunch)
        {
            string stepId = string.IsNullOrEmpty(nodeId) ? "unknown" : nodeId;
            return new Dictionary<string, object>
            {
                { "quest_id", string.IsNullOrEmpty(questId) ? "unknown" : questId },
                { "phase_index", phaseIndex },
                { "step_id", stepId },
                { "step_name", string.IsNullOrWhiteSpace(nodeName) ? stepId : nodeName },
                { "seconds_since_launch", secondsSinceLaunch }
            };
        }

        void HandleCloudSaveFailed(string key) =>
            RecordEvent(UGSKeys.EventCloudSaveFailed, new Dictionary<string, object>
            {
                { "key", key }
            });

        void MaybeRecordFirstLaunch()
        {
            if (PlayerPrefs.GetInt(FirstLaunchPrefKey, 0) == 1)
                return;

            PlayerPrefs.SetInt(FirstLaunchPrefKey, 1);
            PlayerPrefs.Save();
            RecordEvent(UGSKeys.EventGameFirstLaunched);
        }

        static int SecondsSinceLaunch => Mathf.RoundToInt(Time.realtimeSinceStartup);

        #endregion

        void Log(string message)
        {
            if (_allowLog)
                CSDebug.LogVerbose(CSLogChannel.CloudData, $"[Analytics] {message}");
        }

        /// <summary>
        /// A match's setup from the LOCAL player's side, for game_started's <c>domain</c>,
        /// <c>humans_on_my_domain</c>, <c>humans_on_other_domains</c>, <c>ai_on_my_domain</c> and
        /// <c>ai_on_other_domains</c> (Shombith 2026-11-07: the Overview's split of solo, co-op,
        /// competitive and teams vs AI, in multiplayer and solo alike). Froglet Analytics only - the
        /// UGS sink strips all five.
        ///
        /// <para>The seats are the pilots <see cref="AddMatchEnvelope"/> already counts: every AI
        /// (as in <c>player_count_ai</c>) and every human once by UGS id (as in <c>player_ids</c>), so
        /// humans_on_my_domain + humans_on_other_domains = player_count_human and the two AI counts sum
        /// to player_count_ai. The local player is one of those humans, so it is in humans_on_my_domain.
        /// The fields are omitted when there is no local player: with no side to stand on, every
        /// pilot would read as an opponent.</para>
        ///
        /// Pure counting on domains; no Unity calls, so the edit-mode tests drive it directly.
        /// </summary>
        public readonly struct DomainComposition
        {
            public readonly int HumansOnMyDomain;
            public readonly int HumansOnOtherDomains;
            public readonly int AiOnMyDomain;
            public readonly int AiOnOtherDomains;

            public DomainComposition(int humansOnMyDomain, int humansOnOtherDomains, int aiOnMyDomain, int aiOnOtherDomains)
            {
                HumansOnMyDomain = humansOnMyDomain;
                HumansOnOtherDomains = humansOnOtherDomains;
                AiOnMyDomain = aiOnMyDomain;
                AiOnOtherDomains = aiOnOtherDomains;
            }

            /// <summary>Counts <paramref name="seats"/> (the local player's own seat included) against <paramref name="localDomain"/>.</summary>
            public static DomainComposition Count(Domains localDomain, IEnumerable<(Domains Domain, bool IsAI)> seats)
            {
                int humansMine = 0, humansOther = 0, aiMine = 0, aiOther = 0;
                if (seats != null)
                {
                    foreach (var (domain, isAI) in seats)
                    {
                        bool mine = domain == localDomain;
                        if (isAI)
                        {
                            if (mine) aiMine++;
                            else aiOther++;
                        }
                        else
                        {
                            if (mine) humansMine++;
                            else humansOther++;
                        }
                    }
                }

                return new DomainComposition(humansMine, humansOther, aiMine, aiOther);
            }

            /// <summary>Writes the five game_started fields; <c>domain</c> is the Domains enum name.</summary>
            public void AddTo(IDictionary<string, object> parameters, Domains localDomain)
            {
                parameters[UGSKeys.ParamDomain] = localDomain.ToString();
                parameters[UGSKeys.ParamHumansOnMyDomain] = HumansOnMyDomain;
                parameters[UGSKeys.ParamHumansOnOtherDomains] = HumansOnOtherDomains;
                parameters[UGSKeys.ParamAiOnMyDomain] = AiOnMyDomain;
                parameters[UGSKeys.ParamAiOnOtherDomains] = AiOnOtherDomains;
            }
        }

        /// <summary>
        /// The distinct vessels flown in one Freestyle (Toybox) stretch, for <c>freestyle_ended</c>'s
        /// <c>vessels_flown</c> and <c>freestyle_vessel_changed</c>'s <c>from_vessel_class</c>.
        /// Pure bookkeeping on vessel names; no Unity calls, so the edit-mode tests drive it directly.
        /// </summary>
        public sealed class FreestyleVesselLog
        {
            readonly HashSet<string> _flown = new(StringComparer.Ordinal);

            /// <summary>The vessel flown now in this stretch, or null before the first one is seen.</summary>
            public string Current { get; private set; }

            /// <summary>Distinct vessels flown in this stretch; 1 when the player never switched.</summary>
            public int VesselsFlown => Math.Max(1, _flown.Count);

            /// <summary>Starts a stretch in <paramref name="vessel"/>, forgetting the last one.</summary>
            public void Begin(string vessel)
            {
                _flown.Clear();
                Current = null;
                Observe(vessel);
            }

            /// <summary>Records <paramref name="vessel"/> as flown now, without calling it a change.</summary>
            public void Observe(string vessel)
            {
                if (string.IsNullOrEmpty(vessel))
                    return;

                Current = vessel;
                _flown.Add(vessel);
            }

            /// <summary>
            /// True when <paramref name="vessel"/> differs from the vessel flown until now, with that
            /// one in <paramref name="from"/>. A first sighting (nothing flown yet) and the same hull
            /// again are not changes; both are still recorded.
            /// </summary>
            public bool TryChange(string vessel, out string from)
            {
                from = Current;
                if (string.IsNullOrEmpty(vessel) || vessel == Current)
                    return false;

                bool changed = Current != null;
                Observe(vessel);
                return changed;
            }
        }
    }
}
