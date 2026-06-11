using System;
using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
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

        readonly AuthenticationDataVariable _authVariable;
        readonly NetworkMonitorDataVariable _networkVariable;
        readonly GameDataSO _gameData;
        readonly ApplicationLifecycleEventsContainerSO _lifecycleEvents;
        readonly ApplicationStateDataVariable _appStateVariable;
        readonly bool _allowLog;

        bool _collecting;
        bool _isConnected = true;
        bool _signedIn;
        bool _uiActionsWired;
        bool _gameInProgress;
        float _gameStartTime;
        bool _sessionEndRecorded;
        string _lastUiAction = string.Empty;

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
            bool allowLog)
        {
            _authVariable = authVariable;
            _networkVariable = networkVariable;
            _gameData = gameData;
            _lifecycleEvents = lifecycleEvents;
            _appStateVariable = appStateVariable;
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
            RecordEvent(UGSKeys.EventGameCompleted, parameters);
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

        void Log(string message)
        {
            if (_allowLog)
                CSDebug.Log($"[Analytics] {message}");
        }
    }
}
