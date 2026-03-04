using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Reflex.Attributes;
using Unity.Netcode;
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
    ///   4. Activate non-local player vessels for multiplayer party sessions.
    ///
    /// Camera management is handled by <see cref="MainMenuCameraController"/>
    /// which subscribes to the same SOAP events independently.
    ///
    /// Single-writer: only this class transitions the menu state.
    ///
    /// Flow:
    ///   None → Initializing   : Start() — configures game data, fires InitializeGame
    ///   Initializing → Ready  : OnClientReady SOAP event (autopilot vessel spawned and active)
    ///   Ready → Freestyle     : OnGameStateTransitionStart SOAP event (local player takes vessel control)
    ///   Freestyle → Ready     : OnMenuStateTransitionStart SOAP event (local player returns to autopilot)
    ///   Ready → LaunchingGame : OnLaunchGame SOAP event (player selected a game mode)
    ///   Freestyle → LaunchingGame : can launch directly from freestyle
    ///
    /// In multiplayer party sessions, each client independently toggles between
    /// Ready and Freestyle for their own vessel. The state is local to each client.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [Header("Spawn Origins")]
        [SerializeField] protected Transform[] _playerOrigins;

        [Header("Menu Autopilot Configuration")]
        [SerializeField, Tooltip("Vessel class displayed as the autopilot in the menu background.")]
        VesselClassType menuVesselClass = VesselClassType.Squirrel;

        [SerializeField, Tooltip("Number of AI players for the menu background scene.")]
        int menuPlayerCount = 3;

        [SerializeField, Tooltip("Game intensity for the menu background scene.")]
        int menuIntensity = 1;

        [Inject] MenuFreestyleEventsContainerSO _freestyleEvents;
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
                MainMenuState.Freestyle,
                MainMenuState.Initializing, // re-enter from scene reload
            },
            [MainMenuState.Freestyle] = new HashSet<MainMenuState>
            {
                MainMenuState.Ready,
                MainMenuState.LaunchingGame, // can launch game from freestyle
            },
            [MainMenuState.LaunchingGame] = new HashSet<MainMenuState>
            {
                MainMenuState.Ready, // cancelled launch returns to ready
            },
        };

        // ── Unity Lifecycle ─────────────────────────────────────────────

        void Start()
        {
            ConfigureMenuGameData();
            SubscribeEvents();
            TransitionTo(MainMenuState.Initializing);
            DomainAssigner.Initialize();
            _gameData.InitializeGame();
        }

        void OnDestroy()
        {
            UnsubscribeEvents();
        }

        // ── Event Wiring ────────────────────────────────────────────────

        void SubscribeEvents()
        {
            if (_gameData?.OnClientReady != null)
                _gameData.OnClientReady.OnRaised += HandleMenuReady;

            if (_gameData?.OnLaunchGame != null)
                _gameData.OnLaunchGame.OnRaised += HandleLaunchGame;

            if (_gameData?.OnPlayerPairInitialized != null)
                _gameData.OnPlayerPairInitialized.OnRaised += HandlePlayerPairInitialized;

            _freestyleEvents.OnGameStateTransitionStart.OnRaised += HandleEnterFreestyle;
            _freestyleEvents.OnMenuStateTransitionStart.OnRaised += HandleExitFreestyle;
        }

        void UnsubscribeEvents()
        {
            if (_gameData?.OnClientReady != null)
                _gameData.OnClientReady.OnRaised -= HandleMenuReady;

            if (_gameData?.OnLaunchGame != null)
                _gameData.OnLaunchGame.OnRaised -= HandleLaunchGame;

            if (_gameData?.OnPlayerPairInitialized != null)
                _gameData.OnPlayerPairInitialized.OnRaised -= HandlePlayerPairInitialized;

            _freestyleEvents.OnGameStateTransitionStart.OnRaised -= HandleEnterFreestyle;
            _freestyleEvents.OnMenuStateTransitionStart.OnRaised -= HandleExitFreestyle;
        }

        // ── Game Data Configuration ─────────────────────────────────────

        void ConfigureMenuGameData()
        {
            _gameData.SetSpawnPositions(_playerOrigins);
            _gameData.selectedVesselClass.Value = menuVesselClass;
            // SelectedPlayerCount is NOT set here — menu autopilot spawns exactly 1 Player
            // via the Netcode pipeline. The game-launch path sets it via ConfigurePlayerCounts().
            _gameData.SelectedIntensity.Value = menuIntensity;
        }

        // ── Event Handlers ──────────────────────────────────────────────

        void HandleMenuReady()
        {
            TransitionTo(MainMenuState.Ready);
            ActivateLocalPlayerAutopilot();
            _gameData.InitializeGame();
        }

        /// <summary>
        /// Activates a non-local player's vessel with autopilot when their
        /// player-vessel pair finishes initialization on this client.
        /// Replaces the old batch activation in HandleMenuReady which raced
        /// against pairs that hadn't resolved yet.
        /// Host skips this — <see cref="MenuServerPlayerVesselInitializer"/>
        /// already activates every player via ActivateAutopilot().
        /// </summary>
        void HandlePlayerPairInitialized(ulong playerNetObjId)
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
                return;

            var player = _gameData.Players.FirstOrDefault(p => p.PlayerNetId == playerNetObjId);
            if (player == null || player.IsLocalUser || player.Vessel == null)
                return;

            player.StartPlayer();
            player.Vessel.ToggleAIPilot(true);
            player.InputController?.SetPause(true);
        }

        void HandleLaunchGame()
        {
            TransitionTo(MainMenuState.LaunchingGame);
        }

        void HandleEnterFreestyle()
        {
            TransitionTo(MainMenuState.Freestyle);
        }

        void HandleExitFreestyle()
        {
            TransitionTo(MainMenuState.Ready);
        }

        // ── Autopilot ─────────────────────────────────────────────

        /// <summary>
        /// Activates autopilot on the local player's vessel (client-side).
        /// For the host this is redundant with <see cref="MenuServerPlayerVesselInitializer"/>,
        /// but for remote clients joining via party invite this ensures their vessel
        /// starts in autopilot mode.
        /// </summary>
        void ActivateLocalPlayerAutopilot()
        {
            var player = _gameData.LocalPlayer;
            if (player?.Vessel == null) return;

            player.StartPlayer();
            player.Vessel.ToggleAIPilot(true);
            player.InputController?.SetPause(true);
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
