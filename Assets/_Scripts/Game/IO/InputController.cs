using UnityEngine;
using CosmicShore.Core;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using CosmicShore.Systems;
using CosmicShore.Utility.ClassExtensions;


namespace CosmicShore.Game.IO
{
    /// <summary>
    /// This class should only be initialized if user wants to control any ship.
    /// Don't initialize this for any AI / Multiplayer Non Owner Players
    /// </summary>
    public class InputController : MonoBehaviour
    {
        public IInputStatus InputStatus { get; private set; }

        [SerializeField] public bool Portrait;
        IVessel vessel;

        private IInputStrategy currentStrategy;
        private GamepadInputStrategy gamepadStrategy;
        private KeyboardInputStrategy keyboardStrategy;
        private DeviceOrientationHandler orientationHandler;

        private bool isInitialized;

        private void Awake()
        {
            InputStatus ??= TryAddInputStatus();
            InputStatus.InputController = this;
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
            
            if (InputStatus.Paused)
                return;

            if (PauseSystem.Paused)
            {
                // Debug.Log("InputController.Update: PauseSystem.Paused -> blocking input");
                return;
            }

            // 4) Otherwise, handle normal player input:
            UpdateInputStrategy();
            currentStrategy?.ProcessInput();
            orientationHandler.Update();
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
            var gameSetting = GameSetting.Instance;
            if (gameSetting != null)
            {
                // Initialize InputStatus with current settings
                InputStatus.InvertYEnabled = gameSetting.InvertYEnabled;
                InputStatus.InvertThrottleEnabled = gameSetting.InvertThrottleEnabled;
                
                // Also apply to current strategy
                currentStrategy?.SetInvertY(gameSetting.InvertYEnabled);
                currentStrategy?.SetInvertThrottle(gameSetting.InvertThrottleEnabled);
                
                Debug.Log($"[InputController] Synced invert settings - Y: {gameSetting.InvertYEnabled}, Throttle: {gameSetting.InvertThrottleEnabled}");
            }
            else
            {
                Debug.LogWarning("[InputController] GameSetting.Instance is null, cannot sync invert settings!");
            }
        }

        private void SetInitialStrategy()
        {
            if (Gamepad.current != null)
                currentStrategy = gamepadStrategy;
            //else if (SystemInfo.deviceType == DeviceType.Handheld)
            //    currentStrategy = touchStrategy;
            else
                currentStrategy = keyboardStrategy;

            currentStrategy?.OnStrategyActivated();
        }

        private void InitializeStrategies()
        {
            //touchStrategy = new TouchInputStrategy();
            //keyboardMouseStrategy = new KeyboardMouseInputStrategy();
            gamepadStrategy = new GamepadInputStrategy();
            keyboardStrategy = new KeyboardInputStrategy();
            orientationHandler = new DeviceOrientationHandler();

            //touchStrategy.Initialize(vessel);
            //keyboardMouseStrategy.Initialize(vessel);
            gamepadStrategy.Initialize(InputStatus);
            keyboardStrategy.Initialize(InputStatus);
            orientationHandler.Initialize(InputStatus, this);
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
            IInputStrategy newStrategy = null;

            if (Gamepad.current != null && newStrategy != gamepadStrategy)
                newStrategy = gamepadStrategy;
            //else if (Mouse.current.rightButton.isPressed)
            //    newStrategy = keyboardMouseStrategy;
            else 
                newStrategy = keyboardStrategy;

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
            Debug.Log($"[InputController] OnToggleInvertY called with status: {status}");
            InputStatus.InvertYEnabled = status;
            currentStrategy?.SetInvertY(status);
        }

        private void OnToggleInvertThrottle(bool status)
        {
            Debug.Log($"[InputController] OnToggleInvertThrottle called with status: {status}");
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