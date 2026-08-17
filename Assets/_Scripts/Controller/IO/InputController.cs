using UnityEngine;
using CosmicShore.Core;
using CosmicShore.Gameplay;
using CosmicShore.Gameplay.MultiMouse;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using CosmicShore.Utility;
using Reflex.Attributes;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// This class should only be initialized if user wants to control any ship.
    /// Don't initialize this for any AI / Multiplayer Non Owner Players
    /// </summary>
    public class InputController : MonoBehaviour
    {
        public IInputStatus InputStatus { get; private set; }

        [SerializeField] public bool Portrait;
        [Inject] GameSetting gameSetting;
        IVessel vessel;
        Player ownerPlayer;

        private IInputStrategy currentStrategy;
        private GamepadInputStrategy gamepadStrategy;
        private KeyboardInputStrategy keyboardStrategy;
        private TouchInputStrategy touchStrategy;
        private DualMouseInputStrategy dualMouseStrategy;
        private MultiMouseService multiMouseService;
        private DeviceOrientationHandler orientationHandler;

        // Dual-mouse opt-in: defaults to off so a normal mouse click on UI
        // doesn't drag the player back into flight. Engages on simultaneous
        // LMB press across both physical mice; disengages on escape.
        private bool dualMouseEngaged;
        private bool prevBothLeftButtonsHeld;

        private bool isInitialized;

        private void Awake()
        {
            InputStatus ??= TryAddInputStatus();
            InputStatus.InputController = this;
            ownerPlayer = GetComponent<Player>();
        }

        private void RegisterToEvents()
        {
            GameSetting.OnChangeInvertYEnabledStatus += OnToggleInvertY;
            GameSetting.OnChangeInvertThrottleEnabledStatus += OnToggleInvertThrottle;
            EnhancedTouchSupport.Enable();
        }

        private void OnDestroy()
        {
            if (!isInitialized)
                return;

            GameSetting.OnChangeInvertYEnabledStatus -= OnToggleInvertY;
            GameSetting.OnChangeInvertThrottleEnabledStatus -= OnToggleInvertThrottle;
            EnhancedTouchSupport.Disable();

            multiMouseService?.Shutdown();
            multiMouseService = null;
        }

        private void Update()
        {
            if (!isInitialized)
                return;
            
            // Toggle the fullscreen state if the Escape key was pressed this frame on windows
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (!Application.isFocused) return;

            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
                Screen.fullScreen = !Screen.fullScreen;
#endif
            
            // Flight input is local-human-pilot only. AI and remote Player replicas
            // still carry an InputController (OnNetworkSpawn initializes everyone)
            // but must not consume WASD / sticks on this machine.
            if (ownerPlayer != null && !ownerPlayer.IsLocalPilot)
                return;

            if (InputStatus.Paused)
                return;

            if (PauseSystem.Paused)
                return;

            // Tick once per frame here so engagement detection and the
            // strategy itself read the same per-frame snapshot.
            multiMouseService?.Tick();
            UpdateDualMouseEngagement();

            UpdateInputStrategy();
            currentStrategy?.ProcessInput();
            orientationHandler.Update();
        }

        private void UpdateDualMouseEngagement()
        {
            if (multiMouseService == null || !multiMouseService.HasTwoMice)
            {
                dualMouseEngaged = false;
                prevBothLeftButtonsHeld = false;
                return;
            }

            var left = multiMouseService.GetDevice(0);
            var right = multiMouseService.GetDevice(1);
            if (left == null || right == null) return;

            // Engage on the rising edge of both LMBs being held at the same
            // time. Once engaged, the user has to release at least one and
            // re-press to re-trigger this - releasing escape afterward leaves
            // the gesture state pre-armed but does not auto re-engage.
            bool bothHeld = left.LeftButton && right.LeftButton;
            if (bothHeld && !prevBothLeftButtonsHeld)
                dualMouseEngaged = true;
            prevBothLeftButtonsHeld = bothHeld;

            var kb = Keyboard.current;
            if (dualMouseEngaged && kb != null && kb.escapeKey.wasPressedThisFrame)
                dualMouseEngaged = false;
        }

        // public void Initialize(IVessel vessel, bool isOwner = true)
        public void Initialize()
        {
            RegisterToEvents();
            
            InitializeStrategies();
            SetInitialStrategy();
            
            // CRITICAL FIX: Initialize the invert settings from GameSetting's current state
            SyncInvertSettings();
            
            // TODO - Try remove IVessel reference from the method below.
            // InitializeOrientation();

            isInitialized = true;
        }

        /// <summary>
        /// Synchronizes the current invert settings from GameSetting to InputStatus
        /// This is critical because GameSetting reads from PlayerPrefs on Awake,
        /// but InputStatus doesn't get initialized until we call this.
        /// </summary>
        private void SyncInvertSettings()
        {
            // Fallback: InputController is often added dynamically via GetOrAdd,
            // which means Reflex DI never runs on it. Resolve manually if needed.
            if (gameSetting == null)
                gameSetting = FindFirstObjectByType<GameSetting>();

            if (gameSetting != null)
            {
                InputStatus.InvertYEnabled = gameSetting.InvertYEnabled;
                InputStatus.InvertThrottleEnabled = gameSetting.InvertThrottleEnabled;

                currentStrategy?.SetInvertY(gameSetting.InvertYEnabled);
                currentStrategy?.SetInvertThrottle(gameSetting.InvertThrottleEnabled);

                CSDebug.Log($"[InputController] Synced invert settings - Y: {gameSetting.InvertYEnabled}, Throttle: {gameSetting.InvertThrottleEnabled}");
            }
        }

        private void SetInitialStrategy()
        {
            currentStrategy = SelectStrategy();
            currentStrategy?.OnStrategyActivated();
        }

        private void InitializeStrategies()
        {
            multiMouseService = new MultiMouseService();

            touchStrategy = new TouchInputStrategy();
            gamepadStrategy = new GamepadInputStrategy();
            keyboardStrategy = new KeyboardInputStrategy();
            dualMouseStrategy = new DualMouseInputStrategy(multiMouseService);
            orientationHandler = new DeviceOrientationHandler();

            touchStrategy.Initialize(InputStatus);
            gamepadStrategy.Initialize(InputStatus);
            keyboardStrategy.Initialize(InputStatus);
            dualMouseStrategy.Initialize(InputStatus);
            orientationHandler.Initialize(InputStatus, this);
        }

        private IInputStrategy SelectStrategy()
        {
            if (Gamepad.current != null)
                return gamepadStrategy;
            if (SystemInfo.deviceType == DeviceType.Handheld)
                return touchStrategy;
            if (dualMouseEngaged && multiMouseService != null && multiMouseService.HasTwoMice)
                return dualMouseStrategy;
            // Desktop default is dual-WASD KeyboardInputStrategy, not the legacy
            // KeyboardMouseInputStrategy (that class remains in the project unused).
            return keyboardStrategy;
        }

        // TODO - Try remove IVessel reference from the method below
        
        /*
        private void InitializeOrientation()
        {
            if (IsPortrait)
            {
                vessel.SetShipUp(90);
            }
            IInputStatus.CurrentOrientation = Screen.orientation;
        }*/

        private void UpdateInputStrategy()
        {
            IInputStrategy newStrategy = SelectStrategy();

            if (newStrategy != null && newStrategy != currentStrategy)
            {
                currentStrategy?.OnStrategyDeactivated();
                currentStrategy = newStrategy;
                currentStrategy.OnStrategyActivated();

                // Re-sync settings when switching strategies
                SyncInvertSettings();
            }
        }

        public void OnToggleGyro(bool status)
        {
            InputStatus.IsGyroEnabled = status;
            orientationHandler.OnToggleGyro(status);
        }

        private void OnToggleInvertY(bool status)
        {
            CSDebug.Log($"[InputController] OnToggleInvertY called with status: {status}");
            InputStatus.InvertYEnabled = status;
            currentStrategy?.SetInvertY(status);
        }

        private void OnToggleInvertThrottle(bool status)
        {
            CSDebug.Log($"[InputController] OnToggleInvertThrottle called with status: {status}");
            InputStatus.InvertThrottleEnabled = status;
            currentStrategy?.SetInvertThrottle(status);
        }

        public void SetPortrait(bool portrait)
        {
            Portrait = portrait;
            currentStrategy?.SetPortrait(portrait);
        }
        
        public void SetIdle(bool idle) =>
            InputStatus.Idle = idle;

        public void SetPause(bool paused)
        {
            InputStatus.Paused = paused;
            if (paused)
                currentStrategy?.OnPaused();
            else
                currentStrategy?.OnResumed();
        }

        public Quaternion GetGyroRotation() =>
            orientationHandler.GetAttitudeRotation();
 
        public static bool UsingGamepad() =>
            Gamepad.current != null;

        IInputStatus TryAddInputStatus() =>
            gameObject.GetOrAdd<InputStatus>();

    }
}