using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Single-writer state machine for the top-level application phase.
    ///
    /// Pure C# class registered as a Reflex DI lazy singleton in <see cref="AppManager"/>.
    /// Writes to <see cref="ApplicationStateDataVariable"/> (SOAP) so any system can
    /// read the current state or subscribe to <see cref="ApplicationStateData.OnStateChanged"/>.
    ///
    /// Transition callers:
    ///   None → Bootstrapping        : AppManager.Awake()
    ///   Bootstrapping → Authenticating : AppManager.RunBootstrapAsync() after OnBootstrapComplete
    ///   Authenticating → MainMenu    : AuthenticationSceneController on successful auth + scene load
    ///   MainMenu → LoadingGame       : SceneLoader.LaunchGame()
    ///   LoadingGame → InGame         : MiniGame controller (via GameDataSO.OnSessionStarted)
    ///   InGame → GameOver            : Scoring / turn system (via GameDataSO.OnMiniGameEnd)
    ///   GameOver → MainMenu          : SceneLoader.ReturnToMainMenu()
    ///   * → Paused                   : ApplicationLifecycleManager.OnAppPaused(true)
    ///   Paused → (previous)          : ApplicationLifecycleManager.OnAppPaused(false)
    ///   * → Disconnected             : NetworkMonitor.OnNetworkLost
    ///   * → ShuttingDown             : ApplicationLifecycleManager.OnAppQuitting
    /// </summary>
    public class ApplicationStateMachine
    {
        readonly ApplicationStateDataVariable _stateVariable;
        readonly GameDataSO _gameData;
        readonly NetworkMonitorDataVariable _networkMonitorData;
        readonly bool _allowLog;

        ApplicationStateData StateData => _stateVariable.Value;

        /// <summary>Current application state (convenience accessor).</summary>
        public ApplicationState Current => StateData.State;

        // Valid transition table. Key = from-state, Value = set of allowed to-states.
        // ShuttingDown and Paused are special: allowed from any state (handled in code).
        static readonly Dictionary<ApplicationState, HashSet<ApplicationState>> ValidTransitions = new()
        {
            [ApplicationState.None] = new HashSet<ApplicationState>
            {
                ApplicationState.Bootstrapping,
            },
            [ApplicationState.Bootstrapping] = new HashSet<ApplicationState>
            {
                ApplicationState.Authenticating,
            },
            [ApplicationState.Authenticating] = new HashSet<ApplicationState>
            {
                ApplicationState.MainMenu,
            },
            [ApplicationState.MainMenu] = new HashSet<ApplicationState>
            {
                ApplicationState.LoadingGame,
            },
            [ApplicationState.LoadingGame] = new HashSet<ApplicationState>
            {
                ApplicationState.InGame,
                ApplicationState.MainMenu, // cancelled / failed load returns to menu
            },
            [ApplicationState.InGame] = new HashSet<ApplicationState>
            {
                ApplicationState.GameOver,
                ApplicationState.MainMenu, // early exit
            },
            [ApplicationState.GameOver] = new HashSet<ApplicationState>
            {
                ApplicationState.MainMenu,
                ApplicationState.LoadingGame, // replay
                ApplicationState.InGame,      // restart (same scene)
            },
            [ApplicationState.Paused] = new HashSet<ApplicationState>
            {
                // Restored to the state that was active before pausing.
                // Validated dynamically in TransitionTo.
            },
            [ApplicationState.Disconnected] = new HashSet<ApplicationState>
            {
                ApplicationState.MainMenu,
                ApplicationState.Authenticating,
            },
        };

        /// <summary>
        /// The state that was active before entering <see cref="ApplicationState.Paused"/>.
        /// Used to restore state on un-pause.
        /// </summary>
        ApplicationState _stateBeforePause = ApplicationState.None;

        public ApplicationStateMachine(
            ApplicationStateDataVariable stateVariable,
            GameDataSO gameData,
            NetworkMonitorDataVariable networkMonitorData,
            bool allowLog)
        {
            _stateVariable = stateVariable;
            _gameData = gameData;
            _networkMonitorData = networkMonitorData;
            _allowLog = allowLog;

            SubscribeToSOAPEvents();
        }

        void SubscribeToSOAPEvents()
        {
            // Gameplay lifecycle: LoadingGame → InGame when session starts.
            if (_gameData?.OnSessionStarted != null)
                _gameData.OnSessionStarted.OnRaised += HandleSessionStarted;

            // Gameplay lifecycle: InGame → GameOver when mini-game ends.
            if (_gameData?.OnMiniGameEnd != null)
                _gameData.OnMiniGameEnd.OnRaised += HandleMiniGameEnd;

            // Pause/resume from OS-level lifecycle events.
            ApplicationLifecycleManager.OnAppPaused += HandleAppPaused;

            // ShuttingDown from OS quit event.
            ApplicationLifecycleManager.OnAppQuitting += HandleAppQuitting;

            // Network disconnection.
            if (_networkMonitorData?.Value?.OnNetworkLost != null)
                _networkMonitorData.Value.OnNetworkLost.OnRaised += HandleNetworkLost;
        }

        void HandleSessionStarted() => TransitionTo(ApplicationState.InGame);
        void HandleMiniGameEnd() => TransitionTo(ApplicationState.GameOver);
        void HandleAppQuitting() => TransitionTo(ApplicationState.ShuttingDown);
        void HandleNetworkLost() => TransitionTo(ApplicationState.Disconnected);

        /// <summary>
        /// Attempt a state transition. Returns true if the transition was valid and applied.
        /// </summary>
        public bool TransitionTo(ApplicationState newState)
        {
            var current = StateData.State;

            if (current == newState)
                return true;

            // ShuttingDown is always allowed (terminal state).
            if (newState == ApplicationState.ShuttingDown)
                return ApplyTransition(current, newState);

            // Paused is allowed from any non-terminal state.
            if (newState == ApplicationState.Paused)
            {
                if (current == ApplicationState.ShuttingDown)
                {
                    LogWarning($"Cannot pause during ShuttingDown.");
                    return false;
                }

                _stateBeforePause = current;
                return ApplyTransition(current, newState);
            }

            // Un-pausing: the only valid exit from Paused is back to _stateBeforePause.
            if (current == ApplicationState.Paused)
            {
                if (newState != _stateBeforePause)
                {
                    LogWarning($"Paused can only transition back to {_stateBeforePause}, not {newState}.");
                    return false;
                }

                return ApplyTransition(current, newState);
            }

            // Disconnected can be entered from any active state.
            if (newState == ApplicationState.Disconnected)
            {
                if (current == ApplicationState.None || current == ApplicationState.ShuttingDown)
                {
                    LogWarning($"Cannot enter Disconnected from {current}.");
                    return false;
                }

                return ApplyTransition(current, newState);
            }

            // Standard table-driven validation.
            if (!ValidTransitions.TryGetValue(current, out var allowed) || !allowed.Contains(newState))
            {
                LogWarning($"Invalid transition: {current} → {newState}");
                return false;
            }

            return ApplyTransition(current, newState);
        }

        /// <summary>
        /// Convenience for the pause/resume cycle driven by
        /// <see cref="ApplicationLifecycleManager.OnAppPaused"/>.
        /// </summary>
        public void HandleAppPaused(bool isPaused)
        {
            if (isPaused)
                TransitionTo(ApplicationState.Paused);
            else if (Current == ApplicationState.Paused)
                TransitionTo(_stateBeforePause);
        }

        bool ApplyTransition(ApplicationState from, ApplicationState to)
        {
            StateData.PreviousState = from;
            StateData.State = to;

            Log($"{from} → {to}");
            StateData.OnStateChanged?.Raise(to);
            return true;
        }

        void Log(string msg)
        {
            if (_allowLog)
                CSDebug.Log($"[AppState] {msg}");
        }

        void LogWarning(string msg)
        {
            Debug.LogWarning($"[AppState] {msg}");
        }
    }
}
