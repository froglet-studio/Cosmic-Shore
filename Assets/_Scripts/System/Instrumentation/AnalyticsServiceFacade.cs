using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;
using Unity.Services.Core;
using UnityEngine;

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
        /// the sinks, so adding a destination can never widen collection.
        /// </summary>
        readonly List<IAnalyticsSink> _sinks = new();

        readonly PostHogAnalyticsSink _postHogSink;

        bool _collecting;
        bool _isConnected = true;
        bool _signedIn;
        bool _uiActionsWired;
        bool _gameInProgress;
        float _gameStartTime;
        bool _sessionEndRecorded;
        string _lastUiAction = string.Empty;
        bool _menuReadyThisSession;
        bool _freestyleEnteredThisSession;
        int _consecutiveLosses;
        int _droppedEvents;
        bool _warnedNotCollecting;

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

            _sinks.Add(new UgsAnalyticsSink(Log));

            var postHogConfig = Resources.Load<PostHogConfigSO>("PostHogConfig");
            if (postHogConfig != null && postHogConfig.Enabled)
            {
                _postHogSink = new PostHogAnalyticsSink(postHogConfig, Log, BuildSinkEnvelope);
                _sinks.Add(_postHogSink);
            }

            AuthData.OnSignedIn.OnRaised += HandleSignedIn;
            NetworkData.OnNetworkFound.OnRaised += HandleNetworkFound;
            NetworkData.OnNetworkLost.OnRaised += HandleNetworkLost;
            _gameData.OnMiniGameTurnStarted.OnRaised += HandleTurnStarted;
            _gameData.OnMiniGameEnd.OnRaised += HandleMiniGameEnd;
            _lifecycleEvents.OnAppPaused.OnRaised += HandleAppPaused;
            _lifecycleEvents.OnAppQuitting.OnRaised += HandleAppQuitting;
            TryWireUiActions();

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
            GameSetting.OnChangeMusicEnabledStatus      += v => HandleSettingChanged("music_enabled", v);
            GameSetting.OnChangeSFXEnabledStatus        += v => HandleSettingChanged("sfx_enabled", v);
            GameSetting.OnChangeHapticsEnabledStatus    += v => HandleSettingChanged("haptics_enabled", v);
            GameSetting.OnChangeInvertYEnabledStatus    += v => HandleSettingChanged("invert_y", v);
            GameSetting.OnChangeInvertThrottleEnabledStatus += v => HandleSettingChanged("invert_throttle", v);
            GameSetting.OnChangeJoystickVisualsStatus   += v => HandleSettingChanged("joystick_visuals", v);
            GameSetting.OnChangeMusicLevel              += v => HandleSettingChanged("music_level", v);
            GameSetting.OnChangeSFXLevel                += v => HandleSettingChanged("sfx_level", v);
            GameSetting.OnChangeHapticsLevel            += v => HandleSettingChanged("haptics_level", v);
            FavoriteSystem.OnFavoriteChanged            += HandleFavoriteChanged;
            UGSCloudSaveProvider.OnSaveFailed           += HandleCloudSaveFailed;
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
        void HandleNetworkLost() => _isConnected = false;

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
            Log("Data collection started.");

            IdentifyPlayer();
            MaybeRecordFirstLaunch();
        }

        void StopCollection()
        {
            if (!_collecting)
                return;

            foreach (var sink in _sinks)
                sink.StopCollection();

            _collecting = false;
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

            foreach (var sink in _sinks)
                sink.RecordEvent(eventName, payload);

            Log($"Recorded '{eventName}'.");
        }

        /// <summary>
        /// Says out loud, once, why events are going nowhere.
        ///
        /// Collection is opt-in: both the COPPA age gate and consent default to denied, so an
        /// unanswered privacy flow silently drops every event before it reaches any sink - both
        /// UGS and PostHog go quiet at once, which looks like a backend or key problem and is
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
                $"[Analytics] DROPPING EVENTS - {why}. Nothing will reach UGS or PostHog until this " +
                $"is resolved. First dropped event: '{eventName}'. " +
                (NeedsPrivacyFlow
                    ? "No privacy consent UI is reachable in this scene; in the editor use " +
                      "FrogletTools > Services > Analytics Consent (Dev) to grant it."
                    : "Check AnalyticsServiceFacade.StartCollectionIfReady."));
        }

        /// <summary>Events dropped because collection was not active. 0 is the healthy value.</summary>
        public int DroppedEventCount => _droppedEvents;

        /// <summary>
        /// Identity + build context that PostHog has no way to collect on its own.
        ///
        /// This is deliberately NOT applied to every event in the facade. UGS Analytics
        /// already auto-collects the player id, platform and app version as core data, and it
        /// validates every custom parameter against a schema hand-created in the dashboard
        /// Event Manager - where events and parameters, once created, can never be deleted and
        /// count against a 1,500-per-environment cap. Stamping six redundant fields onto 28
        /// events would have cost 168 permanent, unnecessary schema rows for data UGS already
        /// has. So the envelope is a PostHog concern and lives in the PostHog sink.
        /// See Docs/Analytics/DATA_ARCHITECTURE.md §7.1.
        /// </summary>
        public IDictionary<string, object> BuildSinkEnvelope() => new Dictionary<string, object>
        {
            ["player_id"] = ResolveDistinctId(),
            ["app_version"] = Application.version,
            ["platform"] = Application.platform.ToString(),
            ["schema_version"] = EventSchemaVersion
        };

        /// <summary>
        /// The canonical identity: the UGS player id. Immutable, and the same key as Cloud
        /// Save and Leaderboards, so PostHog joins to both with no mapping table. Display name
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
        /// Mirrors cloud-save state onto the PostHog person, so events can be segmented by
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
                properties["first_seen_utc_ms"] = profile.Lifecycle.FirstSeenUtcMs;
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

            foreach (var sink in _sinks)
                sink.Identify(distinctId, properties);
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
            RecordEvent(UGSKeys.EventGameStarted, parameters);
        }

        void HandleMiniGameEnd()
        {
            if (!_gameInProgress)
                return;

            _gameInProgress = false;
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

            if (TryResolveLocalWin(out bool won))
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
        /// </summary>
        void AddMatchEnvelope(IDictionary<string, object> parameters)
        {
            parameters["match_id"] = _gameData.MatchId ?? string.Empty;
            parameters["party_id"] = _gameData.PartyId ?? string.Empty;
            parameters["invite_triggered"] = _gameData.InviteTriggered;

            var humanIds = new List<string>();
            int aiCount = 0;

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
                        continue;
                    }

                    string id = player.UgsPlayerId;
                    if (!string.IsNullOrEmpty(id) && !humanIds.Contains(id))
                        humanIds.Add(id);
                }
            }

            humanIds.Sort(StringComparer.Ordinal);

            // Comma-joined, not an array: UGS Analytics parameters accept only scalar values.
            // The PostHog sink splits this back into a real array (commit 4).
            parameters["player_ids"] = string.Join(",", humanIds);
            parameters["player_count_human"] = humanIds.Count;
            parameters["player_count_ai"] = aiCount;
        }

        /// <summary>
        /// Stamps the client's completion time in both forms: epoch milliseconds for
        /// arithmetic, ISO-8601 for day/week/month bucketing without a per-query conversion.
        /// UGS and PostHog both stamp their own ingest time; this is the CLIENT time, which is
        /// what "timestamp at completion" means.
        /// </summary>
        static void AddCompletionTimestamp(IDictionary<string, object> parameters)
        {
            var nowUtc = DateTime.UtcNow;
            parameters["timestamp_utc_ms"] = new DateTimeOffset(nowUtc).ToUnixTimeMilliseconds();
            parameters["timestamp_utc_iso"] = nowUtc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        }

        Dictionary<string, object> BuildGameParameters() => new()
        {
            { "game_mode", _gameData.GameMode.ToString() },
            { "intensity", _gameData.SelectedIntensity.Value },
            { "vessel_class", _gameData.selectedVesselClass.Value.ToString() },
            { "player_count", _gameData.SelectedPlayerCount.Value },
            { "ai_count", _gameData.RequestedAIBackfillCount },
            // More than one connected human this session (AI backfill doesn't count).
            // Metric meaning changed 2026-07-20: was the retired IsMultiplayerMode config
            // flag; every game now runs the networked single-host model.
            { "is_multiplayer", Unity.Netcode.NetworkManager.Singleton != null
                && Unity.Netcode.NetworkManager.Singleton.ConnectedClientsIds.Count > 1 }
        };

        #endregion

        #region Session lifecycle (session_ended + flush)

        void HandleAppPaused(bool paused)
        {
            if (paused)
            {
                RecordSessionEnded("pause");
                IdentifyPlayer();
                Flush();
                _postHogSink?.SaveQueue();
            }
            else
            {
                // Resumed: the next pause/quit counts as a fresh session end.
                _sessionEndRecorded = false;
            }
        }

        void HandleAppQuitting()
        {
            RecordSessionEnded("quit");
            IdentifyPlayer();
            Flush();
            _postHogSink?.SaveQueue();
        }

        void RecordSessionEnded(string reason)
        {
            if (_sessionEndRecorded)
                return;

            _sessionEndRecorded = true;
            RecordEvent(UGSKeys.EventSessionEnded, new Dictionary<string, object>
            {
                { "reason", reason },
                { "last_ui_action", _lastUiAction },
                { "app_state", _appStateVariable.Value.State.ToString() }
            });

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
            RecordEvent(UGSKeys.EventFreestyleEntered, new Dictionary<string, object>
            {
                { "is_first", isFirst },
                { "seconds_since_launch", SecondsSinceLaunch }
            });
        }

        void HandleSettingChanged(string setting, object value) =>
            RecordEvent(UGSKeys.EventSettingChanged, new Dictionary<string, object>
            {
                { "setting", setting },
                { "value", value?.ToString() ?? string.Empty },
                { "seconds_since_launch", SecondsSinceLaunch }
            });

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

        void HandlePartyInviteSent(PartyPlayerData p) =>
            RecordEvent(UGSKeys.EventPartyInviteSent, new Dictionary<string, object>
            {
                { "target_id", p.PlayerId ?? string.Empty }
            });

        void HandlePartyInviteReceived(PartyInviteData p) =>
            RecordEvent(UGSKeys.EventPartyInviteReceived, new Dictionary<string, object>
            {
                { "host_id", p.HostPlayerId ?? string.Empty }
            });

        void HandlePartyJoined() => RecordEvent(UGSKeys.EventPartyJoined);

        void HandleFavoriteChanged(GameModes mode, bool favorited) =>
            RecordEvent(UGSKeys.EventMinigameFavorited, new Dictionary<string, object>
            {
                { "game_mode", mode.ToString() },
                { "favorited", favorited }
            });

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
                CSDebug.Log($"[Analytics] {message}");
        }
    }
}
