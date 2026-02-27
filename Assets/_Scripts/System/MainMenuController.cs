using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using Reflex.Attributes;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Scene controller and state machine for the Menu_Main scene.
    ///
    /// Attached to the Game GameObject in Menu_Main. Owns the menu's internal
    /// lifecycle as a sub-state machine while <see cref="ApplicationStateMachine"/>
    /// stays in <see cref="ApplicationState.MainMenu"/>.
    ///
    /// Responsibilities:
    ///   1. Configure game data for the autopilot vessel display.
    ///   2. Track menu readiness via <see cref="MainMenuState"/> transitions.
    ///   3. Signal state changes so other menu systems can react.
    ///
    /// Single-writer: only this class transitions the menu state.
    ///
    /// Flow:
    ///   None → Initializing  : Start() — configures game data, fires InitializeGame
    ///   Initializing → Ready : OnMenuReady SOAP event (autopilot vessel spawned and active)
    ///   Ready → LaunchingGame: OnLaunchGame SOAP event (player selected a game mode)
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [Header("Menu Autopilot Configuration")]
        [SerializeField, Tooltip("Vessel class displayed as the autopilot in the menu background.")]
        VesselClassType menuVesselClass = VesselClassType.Squirrel;

        [SerializeField, Tooltip("Number of AI players for the menu background scene.")]
        int menuPlayerCount = 3;

        [SerializeField, Tooltip("Game intensity for the menu background scene.")]
        int menuIntensity = 1;

        [Inject] GameDataSO _gameData;

        MainMenuState _state = MainMenuState.None;

        /// <summary>Current menu sub-state (read-only for external systems).</summary>
        public MainMenuState CurrentState => _state;

        /// <summary>
        /// Fired on every valid menu state transition. Passes the new state.
        /// Use for UI systems that need to react to menu readiness changes.
        /// </summary>
        public event Action<MainMenuState> OnStateChanged;

        // ── Transition table ────────────────────────────────────────────
        static readonly Dictionary<MainMenuState, HashSet<MainMenuState>> ValidTransitions = new()
        {
            [MainMenuState.None] = new HashSet<MainMenuState>
            {
                MainMenuState.Initializing,
            },
            [MainMenuState.Initializing] = new HashSet<MainMenuState>
            {
                MainMenuState.Ready,
            },
            [MainMenuState.Ready] = new HashSet<MainMenuState>
            {
                MainMenuState.LaunchingGame,
                MainMenuState.Initializing, // re-enter from scene reload
            },
            [MainMenuState.LaunchingGame] = new HashSet<MainMenuState>
            {
                MainMenuState.Ready, // cancelled launch returns to ready
            },
        };

        // ── Unity Lifecycle ─────────────────────────────────────────────

        void Start()
        {
            SubscribeEvents();
            TransitionTo(MainMenuState.Initializing);
            ConfigureMenuGameData();
            _gameData.InitializeGame();
        }

        void OnDestroy()
        {
            UnsubscribeEvents();
        }

        // ── Event Wiring ────────────────────────────────────────────────

        void SubscribeEvents()
        {
            if (_gameData?.OnMenuReady != null)
                _gameData.OnMenuReady.OnRaised += HandleMenuReady;

            if (_gameData?.OnLaunchGame != null)
                _gameData.OnLaunchGame.OnRaised += HandleLaunchGame;
        }

        void UnsubscribeEvents()
        {
            if (_gameData?.OnMenuReady != null)
                _gameData.OnMenuReady.OnRaised -= HandleMenuReady;

            if (_gameData?.OnLaunchGame != null)
                _gameData.OnLaunchGame.OnRaised -= HandleLaunchGame;
        }

        // ── Game Data Configuration ─────────────────────────────────────

        void ConfigureMenuGameData()
        {
            _gameData.selectedVesselClass.Value = menuVesselClass;
            _gameData.SelectedPlayerCount.Value = menuPlayerCount;
            _gameData.SelectedIntensity.Value = menuIntensity;
        }

        // ── Event Handlers ──────────────────────────────────────────────

        void HandleMenuReady()
        {
            TransitionTo(MainMenuState.Ready);
        }

        void HandleLaunchGame()
        {
            TransitionTo(MainMenuState.LaunchingGame);
        }

        // ── State Machine ───────────────────────────────────────────────

        bool TransitionTo(MainMenuState newState)
        {
            if (_state == newState)
                return true;

            if (!ValidTransitions.TryGetValue(_state, out var allowed) || !allowed.Contains(newState))
            {
                CSDebug.LogWarning($"[MainMenuController] Invalid transition: {_state} → {newState}");
                return false;
            }

            var previous = _state;
            _state = newState;
            CSDebug.Log($"[MainMenuController] {previous} → {newState}");
            OnStateChanged?.Invoke(newState);
            return true;
        }
    }
}
