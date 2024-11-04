using UnityEngine;
using CosmicShore.Core;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using CosmicShore.Game.UI;
using CosmicShore.App.Systems;


namespace CosmicShore.Game.IO
{
    public class InputController : MonoBehaviour
    {
        [SerializeField] private GameCanvas gameCanvas;
        [SerializeField] public bool Portrait;

        Ship ship;
        public Ship Ship
        {
            get => ship;
            set
            {
                ship = value;
                if (ship != null)
                {
                    touchStrategy.Initialize(ship);
                    keyboardMouseStrategy.Initialize(ship);
                    gamepadStrategy.Initialize(ship);
                    orientationHandler.Initialize(ship, this);
                }
            }   
        }

        [HideInInspector] public bool AutoPilotEnabled;
        [HideInInspector] public static ScreenOrientation currentOrientation;

        private IInputStrategy currentStrategy;
        private TouchInputStrategy touchStrategy;
        private KeyboardMouseInputStrategy keyboardMouseStrategy;
        private GamepadInputStrategy gamepadStrategy;
        private DeviceOrientationHandler orientationHandler;

        // Properties matching original InputController
        public float XSum => currentStrategy?.XSum ?? 0f;
        public float YSum => currentStrategy?.YSum ?? 0f;
        public float XDiff => currentStrategy?.XDiff ?? 0f;
        public float YDiff => currentStrategy?.YDiff ?? 0f;
        public Vector2 EasedLeftJoystickPosition => currentStrategy?.EasedLeftJoystickPosition ?? Vector2.zero;
        public Vector2 EasedRightJoystickPosition => currentStrategy?.EasedRightJoystickPosition ?? Vector2.zero;
        public Vector2 RightJoystickHome => currentStrategy?.RightJoystickHome ?? Vector2.zero;
        public Vector2 LeftJoystickHome => currentStrategy?.LeftJoystickHome ?? Vector2.zero;
        public Vector2 RightClampedPosition => currentStrategy?.RightClampedPosition ?? Vector2.zero;
        public Vector2 LeftClampedPosition => currentStrategy?.LeftClampedPosition ?? Vector2.zero;
        public Vector2 RightJoystickStart => currentStrategy?.RightJoystickStart ?? Vector2.zero;
        public Vector2 LeftJoystickStart => currentStrategy?.LeftJoystickStart ?? Vector2.zero;
        public Vector2 RightNormalizedJoystickPosition => currentStrategy?.RightNormalizedJoystickPosition ?? Vector2.zero;
        public Vector2 LeftNormalizedJoystickPosition => currentStrategy?.LeftNormalizedJoystickPosition ?? Vector2.zero;
        public bool OneTouchLeft => currentStrategy?.OneTouchLeft ?? false;
        public Vector2 SingleTouchValue => currentStrategy?.SingleTouchValue ?? Vector2.zero;
        public Vector3 ThreeDPosition => currentStrategy?.ThreeDPosition ?? Vector3.zero;
        public bool Idle => currentStrategy?.IsIdle ?? true;
        public bool Paused { get; private set; }
        public bool IsGyroEnabled { get; private set; }

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

        private void Awake()
        {
            InitializeStrategies();
            SetInitialStrategy();
            InitializeOrientation();
        }

        private void SetInitialStrategy()
        {
            if (Gamepad.current != null)
                currentStrategy = gamepadStrategy;
            else if (SystemInfo.deviceType == DeviceType.Handheld)
                currentStrategy = touchStrategy;
            else
                currentStrategy = keyboardMouseStrategy;

            currentStrategy?.OnStrategyActivated();
        }

        private void InitializeStrategies()
        {
            touchStrategy = new TouchInputStrategy();
            keyboardMouseStrategy = new KeyboardMouseInputStrategy();
            gamepadStrategy = new GamepadInputStrategy();
            orientationHandler = new DeviceOrientationHandler();
        }

        private void InitializeOrientation()
        {
            if (Portrait)
            {
                ship.SetShipUp(90);
            }
            currentOrientation = Screen.orientation;
        }

        private void Update()
        {
            if (PauseSystem.Paused || Paused) return;

            if (AutoPilotEnabled && ship != null)
            {
                ProcessAutoPilot();
                return;
            }

            UpdateInputStrategy();
            currentStrategy?.ProcessInput(ship);
            orientationHandler.Update();
        }

        private void ProcessAutoPilot()
        {
            if (ship.ShipStatus.SingleStickControls)
            {
                currentStrategy?.SetAutoPilotValues(new Vector2(ship.AutoPilot.X, ship.AutoPilot.Y));
            }
            else
            {
                currentStrategy?.SetAutoPilotValues(
                    ship.AutoPilot.XSum,
                    ship.AutoPilot.YSum,
                    ship.AutoPilot.XDiff,
                    ship.AutoPilot.YDiff
                );
            }
        }

        private void UpdateInputStrategy()
        {
            IInputStrategy newStrategy = null;

            if (Gamepad.current != null)
                newStrategy = gamepadStrategy;
            else if (Mouse.current.rightButton.isPressed)
                newStrategy = keyboardMouseStrategy;
            else 
                newStrategy = touchStrategy;

            if (newStrategy != null && newStrategy != currentStrategy)
            {
                currentStrategy?.OnStrategyDeactivated();
                currentStrategy = newStrategy;
                currentStrategy.OnStrategyActivated();
            }
        }

        public void OnToggleGyro(bool status)
        {
            IsGyroEnabled = status;
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
            Paused = paused;
            if (paused)
                currentStrategy?.OnPaused();
            else
                currentStrategy?.OnResumed();
        }

        public Quaternion GetGyroRotation()
        {
            return orientationHandler.GetAttitudeRotation();
        }

        public static bool UsingGamepad()
        {
            return Gamepad.current != null;
        }
    }
}
