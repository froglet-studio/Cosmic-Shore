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

        [SerializeField, Tooltip("Domain (team color) to force on the menu autopilot vessel. " +
                 "Jade by default so the Squirrel renders in green and the cell's flora/fauna " +
                 "have a non-local domain to react to. Without this override, NetDomain stays " +
                 "at its serialized default and can drift to whatever Unity loads from the " +
                 "prefab — fauna then read the wrong color and avoid the autopilot's trail.")]
        Domains menuVesselDomain = Domains.Jade;

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

            // The host's Player NetworkObject was spawned in the Auth scene, where
            // gameData.selectedVesselClass was Squirrel (set by AppManager.ConfigureGameData).
            // That value got locked into NetDefaultVesselType in Player.OnNetworkSpawn before
            // Menu_Main loaded. ServerPlayerVesselInitializer's retry loop only overwrites
            // NetDefaultVesselType when the current value is INVALID (Random/Any) — Squirrel
            // is valid, so the menu's selection (e.g. Serpent) never reaches the spawn chain.
            // Explicitly push the menu's selection onto the host's NetDefaultVesselType here
            // so the spawn chain spawns whatever the inspector dropdown says.
            ApplyMenuVesselClassToHost();
        }

        void ApplyMenuVesselClassToHost()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening) return;

            // The host owns its own player object. NetDefaultVesselType is owner-writable,
            // so the host can rewrite it directly. Remote clients in a party session manage
            // their own vessel type via the team/vessel selection panel.
            var localClient = nm.LocalClient;
            if (localClient == null || localClient.PlayerObject == null) return;
            if (!localClient.PlayerObject.TryGetComponent<Player>(out var hostPlayer)) return;
            if (!hostPlayer.IsOwner) return;

            if (hostPlayer.NetDefaultVesselType.Value != menuVesselClass)
                hostPlayer.NetDefaultVesselType.Value = menuVesselClass;
        }

        // ── Event Handlers ──────────────────────────────────────────────

        void HandleMenuReady()
        {
            TransitionTo(MainMenuState.Ready);
            ActivateLocalPlayerAutopilot();
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

            // Force the menu vessel into the configured domain. NetDomain is owner-writable
            // and the host owns its own player, so this propagates to all subsystems via
            // OnNetDomainChanged → Player.SetDomain. Also write Player.Domain and
            // RoundStats.Domain directly because OnNetDomainChanged only updates Player.Domain
            // (RoundStats.Domain is set once at InitializeForMultiplayerMode and stays stale
            // through subsequent NetDomain writes). All three writes together keep the
            // ship material, fauna controlling-color reads, and gameData.LocalRoundStats
            // consistent without waiting for replication.
            ApplyMenuDomain(player);

            player.StartPlayer();
            player.Vessel.ToggleAIPilot(true);
            player.InputController?.SetPause(true);
        }

        void ApplyMenuDomain(IPlayer player)
        {
            if (player is not Player p) return;

            if (p.IsOwner && p.NetDomain != null && p.NetDomain.Value != menuVesselDomain)
                p.NetDomain.Value = menuVesselDomain;

            p.SetDomain(menuVesselDomain);
            if (p.RoundStats != null)
                p.RoundStats.Domain = menuVesselDomain;

            // ShipHelper.SetShipProperties was already called once during
            // VesselController.Initialize (using whatever domain was set at that point).
            // Re-run it now so the ship material, skimmer material, AOE materials, and
            // silhouette prefab references all match the menu domain.
            //
            // Critically, SetShipProperties only updates the ShipMaterial *reference*
            // on VesselStatus. The actual mesh paint happens in
            // VesselCustomization.Initialize (which is one-shot). RefreshShipMaterial()
            // re-paints the mesh renderers — without it the vessel mesh keeps its
            // original material even though VesselStatus.ShipMaterial now points to
            // the new domain's set. This was the root cause of the squirrel still
            // rendering Blue even after the trail prisms switched to Jade.
            if (p.Vessel != null && _gameData != null && _gameData.ThemeManagerData != null)
            {
                ShipHelper.SetShipProperties(_gameData.ThemeManagerData, p.Vessel);
                p.Vessel.VesselStatus?.Customization?.RefreshShipMaterial();
            }
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
