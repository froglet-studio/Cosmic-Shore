using UnityEngine;
using CosmicShore.Core;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using CosmicShore.App.Systems;
using CosmicShore.Utility.ClassExtensions;


namespace CosmicShore.Game.IO
{
    public class InputController : MonoBehaviour
    {
        struct JoystickData
        {
            public Vector2 joystickStart;
            public int touchIndex;
            public Vector2 joystickNormalizedOffset;
            public Vector2 clampedPosition;
        }

        public IInputStatus InputStatus { get; private set; }

        [SerializeField] public bool Portrait;
        IShip _ship;

        private IInputStrategy currentStrategy;
        private GamepadInputStrategy gamepadStrategy;
        private DeviceOrientationHandler orientationHandler;

        private void Awake()
        {
            InputStatus ??= TryAddInputStatus();
            InputStatus.InputController = this;
        }

        private void OnEnable()
        {
            GameSetting.OnChangeInvertYEnabledStatus += OnToggleInvertY;
            GameSetting.OnChangeInvertThrottleEnabledStatus += OnToggleInvertThrottle;
            EnhancedTouchSupport.Enable();
        }

        private void OnDisable()
        {
            GameSetting.OnChangeInvertYEnabledStatus -= OnToggleInvertY;
            GameSetting.OnChangeInvertThrottleEnabledStatus -= OnToggleInvertThrottle;
            EnhancedTouchSupport.Disable();
        }

        private void Update()
        {
            // Toggle the fullscreen state if the Escape key was pressed this frame on windows
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (Keyboard.current.escapeKey.wasPressedThisFrame)
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

        public void Initialize(IShip ship)
        {
            _ship = ship;
            InitializeStrategies();
            SetInitialStrategy();
            InitializeOrientation();
        }

        private void SetInitialStrategy()
        {
            //if (Gamepad.current != null)
                currentStrategy = gamepadStrategy;
            //else if (SystemInfo.deviceType == DeviceType.Handheld)
            //    currentStrategy = touchStrategy;
            //else
            //    currentStrategy = keyboardMouseStrategy;

            currentStrategy?.OnStrategyActivated();
        }

        private void InitializeStrategies()
        {
            //touchStrategy = new TouchInputStrategy();
            //keyboardMouseStrategy = new KeyboardMouseInputStrategy();
            gamepadStrategy = new GamepadInputStrategy();
            orientationHandler = new DeviceOrientationHandler();

            //touchStrategy.Initialize(ship);
            //keyboardMouseStrategy.Initialize(ship);
            gamepadStrategy.Initialize(InputStatus);
            orientationHandler.Initialize(_ship, this);
        }

        private void InitializeOrientation()
        {
            if (Portrait)
            {
                _ship.SetShipUp(90);
            }
            IInputStatus.CurrentOrientation = Screen.orientation;
        }

        private void UpdateInputStrategy()
        {
            IInputStrategy newStrategy = null;

            if (Gamepad.current != null && newStrategy != gamepadStrategy)
                newStrategy = gamepadStrategy;
            //else if (Mouse.current.rightButton.isPressed)
            //    newStrategy = keyboardMouseStrategy;
            //else 
                //newStrategy = touchStrategy;

            if (newStrategy != null && newStrategy != currentStrategy)
            {
                currentStrategy?.OnStrategyDeactivated();
                currentStrategy = newStrategy;
                currentStrategy.OnStrategyActivated();
            }
        }

        public void OnToggleGyro(bool status)
        {
            InputStatus.IsGyroEnabled = status;
            orientationHandler.OnToggleGyro(status);
        }

        private void OnToggleInvertY(bool status)
        {
            currentStrategy?.SetInvertY(status);
        }

        private void OnToggleInvertThrottle(bool status)
        {
            currentStrategy?.SetInvertThrottle(status);
        }

        public void SetPortrait(bool portrait)
        {
            Portrait = portrait;
            currentStrategy?.SetPortrait(portrait);
        }

        public void SetPaused(bool paused)
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
            gameObject.GetOrAdd<NetworkInputStatus>();

    }
}
