using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;
using Unity.Services.Analytics;
using Unity.Services.Core;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Single writer for all UGS Analytics custom events (UGS-only pipeline —
    /// the Firebase analytics path is retired).
    ///
    /// Owns the collection lifecycle: starts data collection after UGS sign-in
    /// (gated by consent and network availability), records events through one
    /// choke point, and flushes only on app pause/quit — the SDK batches
    /// everything else automatically.
    ///
    /// Custom events and their parameters must also be declared in the UGS
    /// dashboard Event Manager or the backend silently discards them.
    /// </summary>
    public class AnalyticsServiceFacade
    {
        /// <summary>
        /// PlayerPrefs key for analytics consent. Defaults to granted (current
        /// shipping behavior) until the consent dialog ships; the dialog and the
        /// settings opt-out toggle both write through <see cref="SetConsent"/>.
        /// </summary>
        const string ConsentPrefKey = "AnalyticsConsent";

        /// <summary>PlayerPrefs key (device-local) guarding the once-ever first-launch event.</summary>
        const string FirstLaunchPrefKey = "AnalyticsFirstLaunchRecorded";

        /// <summary>Consecutive losses before <c>repeated_game_fail</c> fires.</summary>
        const int RepeatedFailThreshold = 3;

        readonly AuthenticationDataVariable _authVariable;
        readonly NetworkMonitorDataVariable _networkVariable;
        readonly GameDataSO _gameData;
        readonly ApplicationLifecycleEventsContainerSO _lifecycleEvents;
        readonly ApplicationStateDataVariable _appStateVariable;
        readonly MenuFreestyleEventsContainerSO _menuFreestyleEvents;
        readonly FriendsDataSO _friendsData;
        readonly HostConnectionDataSO _hostConnectionData;
        readonly bool _allowLog;

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

        AuthenticationData AuthData => _authVariable.Value;
        NetworkMonitorData NetworkData => _networkVariable.Value;

        public bool ConsentGranted => PlayerPrefs.GetInt(ConsentPrefKey, 1) == 1;
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

            AuthData.OnSignedIn.OnRaised += HandleSignedIn;
            NetworkData.OnNetworkFound.OnRaised += HandleNetworkFound;
            NetworkData.OnNetworkLost.OnRaised += HandleNetworkLost;
            _gameData.OnMiniGameTurnStarted.OnRaised += HandleTurnStarted;
            _gameData.OnMiniGameEnd.OnRaised += HandleMiniGameEnd;
            _lifecycleEvents.OnAppPaused.OnRaised += HandleAppPaused;
            _lifecycleEvents.OnAppQuitting.OnRaised += HandleAppQuitting;
            AdsSystem.AdLoaded += HandleAdLoaded;
            TryWireUiActions();

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
        }

        #region Consent & collection lifecycle

        /// <summary>
        /// Persists the player's analytics consent and starts/stops collection
        /// accordingly. The future consent dialog and the settings opt-out
        /// toggle are the intended callers.
        /// </summary>
        public void SetConsent(bool granted)
        {
            PlayerPrefs.SetInt(ConsentPrefKey, granted ? 1 : 0);
            PlayerPrefs.Save();

            if (granted)
                StartCollectionIfReady();
            else
                StopCollection();
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
        // offline, so collection stays on — we only track the flag so a
        // first-time StartDataCollection waits for connectivity.
        void HandleNetworkLost() => _isConnected = false;

        void StartCollectionIfReady()
        {
            if (_collecting || !_signedIn || !_isConnected || !ConsentGranted)
                return;
            if (UnityServices.State != ServicesInitializationState.Initialized)
                return;

            try
            {
                // No ExternalUserId override: with UGS auth active, events carry
                // the UGS player id — the same key as Cloud Save and Leaderboards.
                AnalyticsService.Instance.StartDataCollection();
                _collecting = true;
                Log("Data collection started.");
                MaybeRecordFirstLaunch();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Analytics] StartDataCollection failed: {e.Message}");
            }
        }

        void StopCollection()
        {
            if (!_collecting)
                return;

            try
            {
                AnalyticsService.Instance.StopDataCollection();
                _collecting = false;
                Log("Data collection stopped (consent revoked).");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Analytics] StopDataCollection failed: {e.Message}");
            }
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
                Log($"Dropped '{eventName}' — collection not active.");
                return;
            }

            try
            {
                var evt = new CustomEvent(eventName);
                if (parameters != null)
                    foreach (var kvp in parameters)
                        evt.Add(kvp.Key, kvp.Value);

                AnalyticsService.Instance.RecordEvent(evt);
                Log($"Recorded '{eventName}'.");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Analytics] Failed to record '{eventName}': {e.Message}");
            }
        }

        public void RecordPlayAgain() => RecordEvent(UGSKeys.EventPlayAgain);

        /// <summary>
        /// Uploads the SDK's event batch immediately. Reserved for moments the
        /// process may die (pause/quit) — everywhere else the SDK's own batching
        /// is cheaper and loses nothing.
        /// </summary>
        public void Flush()
        {
            if (!_collecting)
                return;

            try
            {
                AnalyticsService.Instance.Flush();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Analytics] Flush failed: {e.Message}");
            }
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
            RecordEvent(UGSKeys.EventGameStarted, BuildGameParameters());
        }

        void HandleMiniGameEnd()
        {
            if (!_gameInProgress)
                return;

            _gameInProgress = false;
            var parameters = BuildGameParameters();
            parameters["duration_seconds"] = Mathf.RoundToInt(Time.realtimeSinceStartup - _gameStartTime);

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
        /// otherwise by winner name vs the local player. Defensive — never throws
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
                // Result not populated for this mode — omit player_won.
            }

            return false;
        }

        Dictionary<string, object> BuildGameParameters() => new()
        {
            { "game_mode", _gameData.GameMode.ToString() },
            { "intensity", _gameData.SelectedIntensity.Value },
            { "vessel_class", _gameData.selectedVesselClass.Value.ToString() },
            { "player_count", _gameData.SelectedPlayerCount.Value },
            { "ai_count", _gameData.RequestedAIBackfillCount },
            { "is_multiplayer", _gameData.IsMultiplayerMode }
        };

        #endregion

        #region Session lifecycle (session_ended + flush)

        void HandleAppPaused(bool paused)
        {
            if (paused)
            {
                RecordSessionEnded("pause");
                Flush();
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
            Flush();
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

        void HandleAdLoaded() => RecordEvent(UGSKeys.EventAdImpression);

        #endregion

        #region Phase 2 — typed events (called by injected systems)

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
