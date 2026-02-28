using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Reflex.Attributes;
using Unity.Cinemachine;
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
        [Header("Spawn Origins")]
        [SerializeField] protected Transform[] _playerOrigins;
        
        [Header("Menu Autopilot Configuration")]
        [SerializeField, Tooltip("Vessel class displayed as the autopilot in the menu background.")]
        VesselClassType menuVesselClass = VesselClassType.Squirrel;

        [SerializeField, Tooltip("Number of AI players for the menu background scene.")]
        int menuPlayerCount = 3;

        [SerializeField, Tooltip("Game intensity for the menu background scene.")]
        int menuIntensity = 1;

        [Header("Camera Switching")]
        [SerializeField, Tooltip("Duration of camera blend when switching between menu and gameplay cameras.")]
        float _cameraBlendDuration = 1.5f;

        [SerializeField, Tooltip("SOAP events for entering/exiting freestyle mode.")]
        MenuFreestyleEventsContainerSO _freestyleEvents;

        [Inject] GameDataSO _gameData;

        CinemachineCamera _menuVCam;

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
            CacheMenuVCam();
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

            _freestyleEvents.OnEnterFreestyle.OnRaised += HandleEnterFreestyle;
            _freestyleEvents.OnExitFreestyle.OnRaised += HandleExitFreestyle;
        }

        void UnsubscribeEvents()
        {
            if (_gameData?.OnClientReady != null)
                _gameData.OnClientReady.OnRaised -= HandleMenuReady;

            if (_gameData?.OnLaunchGame != null)
                _gameData.OnLaunchGame.OnRaised -= HandleLaunchGame;

            _freestyleEvents.OnEnterFreestyle.OnRaised -= HandleEnterFreestyle;
            _freestyleEvents.OnExitFreestyle.OnRaised -= HandleExitFreestyle;
        }

        // ── Game Data Configuration ─────────────────────────────────────

        void ConfigureMenuGameData()
        {
            _gameData.SetSpawnPositions(_playerOrigins);
            _gameData.selectedVesselClass.Value = menuVesselClass;
            _gameData.SelectedPlayerCount.Value = menuPlayerCount;
            _gameData.SelectedIntensity.Value = menuIntensity;
        }

        // ── Event Handlers ──────────────────────────────────────────────

        void HandleMenuReady()
        {
            TransitionTo(MainMenuState.Ready);
            ActivateMenuCamera();
            _gameData.InitializeGame();
        }

        void HandleLaunchGame()
        {
            TransitionTo(MainMenuState.LaunchingGame);
        }

        // ── Camera Switching ───────────────────────────────────────

        void CacheMenuVCam()
        {
            if (!CameraManager.Instance) return;
            var cmTransform = CameraManager.Instance.transform.Find("CM Main Menu");
            if (cmTransform)
                _menuVCam = cmTransform.GetComponent<CinemachineCamera>();
        }

        /// <summary>
        /// Activates the CM Main Menu Cinemachine camera for menu state.
        /// Deactivates all CameraManager gameplay cameras. Used for the initial
        /// menu setup where no blend is needed.
        /// </summary>
        void ActivateMenuCamera()
        {
            if (!CameraManager.Instance) return;
            CameraManager.Instance.DeactivateAllCameras();
            if (_menuVCam) _menuVCam.gameObject.SetActive(true);
        }

        /// <summary>
        /// Smoothly transitions from the gameplay camera to the menu camera.
        /// Positions Camera.main at the gameplay camera's pose so the CinemachineBrain
        /// blends from there to the menu vCam target.
        /// </summary>
        void HandleExitFreestyle()
        {
            if (!CameraManager.Instance) return;

            // Capture gameplay camera pose before deactivating
            var playerCamTransform = CameraManager.Instance.GetCloseCamera();

            CameraManager.Instance.DeactivateAllCameras();

            // Position the brain camera at the gameplay camera's last location so
            // CinemachineBrain blends from here to the menu vCam target.
            var mainCam = Camera.main;
            if (mainCam && playerCamTransform)
                mainCam.transform.SetPositionAndRotation(
                    playerCamTransform.position, playerCamTransform.rotation);

            if (_menuVCam) _menuVCam.gameObject.SetActive(true);
        }

        /// <summary>
        /// Smoothly transitions from the menu camera to the gameplay camera.
        /// Captures Camera.main's pose and blends the player camera from that
        /// position to the vessel's follow target.
        /// </summary>
        void HandleEnterFreestyle()
        {
            if (!CameraManager.Instance) return;

            var player = _gameData.LocalPlayer;
            if (player?.Vessel == null) return;

            // Capture menu camera pose before switching
            var mainCam = Camera.main;
            var fromPos = mainCam ? mainCam.transform.position : Vector3.zero;
            var fromRot = mainCam ? mainCam.transform.rotation : Quaternion.identity;

            if (_menuVCam) _menuVCam.gameObject.SetActive(false);

            var followTarget = player.Vessel.VesselStatus.CameraFollowTarget;
            CameraManager.Instance.SetupGamePlayCameras(followTarget);

            // Override the snap with a smooth blend from the menu camera pose
            CameraManager.Instance.BlendPlayerCameraFrom(fromPos, fromRot, _cameraBlendDuration);
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
