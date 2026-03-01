using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Reflex.Attributes;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
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
    /// Single-writer: only this class transitions the menu state.
    ///
    /// Flow:
    ///   None → Initializing   : Start() — configures game data, fires InitializeGame
    ///   Initializing → Ready  : OnClientReady SOAP event (autopilot vessel spawned and active)
    ///   Ready → Freestyle     : OnEnterFreestyle SOAP event (local player takes vessel control)
    ///   Freestyle → Ready     : OnExitFreestyle SOAP event (local player returns to autopilot)
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

        [Header("Camera Switching")]
        [SerializeField, Tooltip("SOAP events for entering/exiting freestyle mode.")]
        MenuFreestyleEventsContainerSO _freestyleEvents;

        [SerializeField, Tooltip("Cell runtime data used to find the crystal tracking target at runtime.")]
        CellRuntimeDataSO _cellData;

        [Header("Menu Camera Orbit")]
        [SerializeField, Tooltip("Orbit radius from crystal center.")]
        float _orbitRadius = 80f;

        [SerializeField, Tooltip("Camera height offset above the crystal.")]
        float _orbitHeight = 30f;

        [SerializeField, Tooltip("Orbit speed in degrees per second.")]
        float _orbitSpeed = 5f;

        [Header("Freestyle Camera")]
        [SerializeField, Tooltip("Follow offset for the freestyle Cinemachine vCam.")]
        Vector3 _gameplayFollowOffset = new(0, 5, -25);

        [Inject] GameDataSO _gameData;

        CinemachineCamera _menuVCam;
        CinemachineFollow _menuFollow;
        Transform _menuFollowTarget;
        RotateAroundOrigin _followTargetRotator;
        Transform _crystalTarget;
        CinemachineCamera _gameplayVCam;

        const int HighPriority = 20;
        const int LowPriority = 0;

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
            CacheMenuVCam();
            CreateGameplayVCam();
            ConfigureMenuGameData();
            SubscribeEvents();
            TransitionTo(MainMenuState.Initializing);
            DomainAssigner.Initialize();
            _gameData.InitializeGame();
        }

        void OnDestroy()
        {
            UnsubscribeEvents();

            // Re-enable RotateAroundOrigin in case the CameraManager object is reused
            if (_followTargetRotator) _followTargetRotator.enabled = true;

            // Ensure menu vCam doesn't interfere with subsequent scenes
            if (_menuVCam)
            {
                SetVCamPriority(_menuVCam, LowPriority);
                _menuVCam.gameObject.SetActive(false);
            }
        }

        void Update()
        {
            UpdateMenuOrbit();
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

            _cellData.OnCrystalSpawned.OnRaised += HandleCrystalSpawned;
        }

        void UnsubscribeEvents()
        {
            if (_gameData?.OnClientReady != null)
                _gameData.OnClientReady.OnRaised -= HandleMenuReady;

            if (_gameData?.OnLaunchGame != null)
                _gameData.OnLaunchGame.OnRaised -= HandleLaunchGame;

            _freestyleEvents.OnEnterFreestyle.OnRaised -= HandleEnterFreestyle;
            _freestyleEvents.OnExitFreestyle.OnRaised -= HandleExitFreestyle;

            _cellData.OnCrystalSpawned.OnRaised -= HandleCrystalSpawned;
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
            ActivateLocalPlayerAutopilot();

            // Activate non-owner players so their vessels render on this client.
            // Ported from MultiplayerFreestyleController.OnClientReady — ensures
            // joining clients see existing players' vessels as active.
            _gameData.SetNonOwnerPlayersActiveInNewClient();

            _gameData.InitializeGame();
        }

        void HandleLaunchGame()
        {
            TransitionTo(MainMenuState.LaunchingGame);
        }

        void HandleEnterFreestyle()
        {
            TransitionTo(MainMenuState.Freestyle);
            ActivateGameplayCamera();
        }

        void HandleExitFreestyle()
        {
            TransitionTo(MainMenuState.Ready);
            ActivateMenuCamera();
        }

        void HandleCrystalSpawned()
        {
            SetMenuVCamTarget();
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

        // ── Camera Setup ────────────────────────────────────────────

        void CacheMenuVCam()
        {
            if (!CameraManager.Instance) return;

            var cmTransform = CameraManager.Instance.transform.Find("CM Main Menu");
            if (!cmTransform) return;

            _menuVCam = cmTransform.GetComponent<CinemachineCamera>();
            _menuFollow = cmTransform.GetComponent<CinemachineFollow>();

            var followTransform = CameraManager.Instance.transform.Find("Main Menu Follow Target");
            if (followTransform)
            {
                _menuFollowTarget = followTransform;
                _followTargetRotator = followTransform.GetComponent<RotateAroundOrigin>();
            }
        }

        void CreateGameplayVCam()
        {
            var go = new GameObject("CM Menu Gameplay");
            go.transform.SetParent(transform);

            // Add pipeline components before CinemachineCamera so it discovers them on enable
            var follow = go.AddComponent<CinemachineFollow>();
            follow.FollowOffset = _gameplayFollowOffset;
            var tracker = follow.TrackerSettings;
            tracker.BindingMode = BindingMode.LockToTargetWithWorldUp;
            tracker.PositionDamping = new Vector3(1, 1, 1);
            follow.TrackerSettings = tracker;

            go.AddComponent<CinemachineRotationComposer>();

            _gameplayVCam = go.AddComponent<CinemachineCamera>();
            SetVCamPriority(_gameplayVCam, LowPriority);

            var lens = _gameplayVCam.Lens;
            lens.FieldOfView = 60;
            lens.NearClipPlane = 0.3f;
            lens.FarClipPlane = 12000;
            _gameplayVCam.Lens = lens;
        }

        // ── Menu Camera Orbit ───────────────────────────────────────

        void SetMenuVCamTarget()
        {
            if (!_menuVCam) return;

            var crystalTransform = _cellData.CrystalTransform;
            if (!crystalTransform) return;

            _crystalTarget = crystalTransform;

            // Position follow target at orbit radius from crystal
            if (_menuFollowTarget)
            {
                _menuFollowTarget.position = crystalTransform.position + Vector3.back * _orbitRadius;

                // Disable default RotateAroundOrigin — it orbits world origin, not the crystal
                if (_followTargetRotator) _followTargetRotator.enabled = false;
            }

            // TrackingTarget = orbiting follow target (for camera positioning)
            // LookAtTarget = crystal (for camera aiming via CinemachineRotationComposer)
            var target = _menuVCam.Target;
            target.TrackingTarget = _menuFollowTarget ? _menuFollowTarget : crystalTransform;
            target.LookAtTarget = crystalTransform;
            target.CustomLookAtTarget = true;
            _menuVCam.Target = target;

            // CinemachineFollow offset provides height above the orbit path
            if (_menuFollow)
                _menuFollow.FollowOffset = new Vector3(0, _orbitHeight, 0);
        }

        void UpdateMenuOrbit()
        {
            if (!_crystalTarget || !_menuFollowTarget) return;

            var pivot = _crystalTarget.position;
            var offset = _menuFollowTarget.position - pivot;
            offset = Quaternion.Euler(0, _orbitSpeed * Time.deltaTime, 0) * offset;
            _menuFollowTarget.position = pivot + offset;
        }

        // ── Camera Switching ────────────────────────────────────────

        /// <summary>
        /// Activates the CM Main Menu Cinemachine camera for menu state.
        /// Deactivates all CameraManager gameplay cameras and raises the menu
        /// vCam's priority so the CinemachineBrain blends to it smoothly.
        /// </summary>
        void ActivateMenuCamera()
        {
            if (!CameraManager.Instance) return;
            CameraManager.Instance.DeactivateAllCameras();

            if (_menuVCam)
            {
                SetMenuVCamTarget();
                _menuVCam.gameObject.SetActive(true);
                SetVCamPriority(_menuVCam, HighPriority);
            }

            if (_gameplayVCam)
                SetVCamPriority(_gameplayVCam, LowPriority);
        }

        /// <summary>
        /// Activates the gameplay Cinemachine vCam for freestyle mode.
        /// Raises the gameplay vCam's priority so the CinemachineBrain blends
        /// from the menu orbit camera to the vessel follow camera smoothly.
        /// </summary>
        void ActivateGameplayCamera()
        {
            var player = _gameData.LocalPlayer;
            if (player?.Vessel == null) return;

            var followTarget = player.Vessel.VesselStatus.CameraFollowTarget;

            if (_gameplayVCam)
            {
                var target = _gameplayVCam.Target;
                target.TrackingTarget = followTarget;
                target.CustomLookAtTarget = false;
                _gameplayVCam.Target = target;

                SetVCamPriority(_gameplayVCam, HighPriority);
            }

            if (_menuVCam)
                SetVCamPriority(_menuVCam, LowPriority);
        }

        static void SetVCamPriority(CinemachineCamera cam, int value)
        {
            var p = cam.Priority;
            p.Enabled = true;
            p.Value = value;
            cam.Priority = p;
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
