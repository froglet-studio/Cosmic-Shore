using UnityEngine;
using CosmicShore.Core;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using CosmicShore.Game.UI;
using CosmicShore.App.Systems;
using CosmicShore.Utility.ClassExtensions;
using Unity.Netcode;


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

        private IInputStatus _inputStatus;
        public IInputStatus InputStatus => _inputStatus ??= TryAddInputStatus();

        [SerializeField] public bool Portrait;

        IShip ship;
        public IShip Ship
        {
            get => ship;
            set
            {
                ship = value;
                if (ship != null)
                {
                    //touchStrategy.Initialize(ship);
                    //keyboardMouseStrategy.Initialize(ship);
                    gamepadStrategy.Initialize(ship);
                    orientationHandler.Initialize(ship, this);
                }
            }   
        }

        [HideInInspector] public bool AutoPilotEnabled;
        [HideInInspector] public static ScreenOrientation currentOrientation;

        private IInputStrategy currentStrategy;
        //private TouchInputStrategy touchStrategy;
        //private KeyboardMouseInputStrategy keyboardMouseStrategy;
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

        private void Awake()
        {
            InitializeStrategies();
            SetInitialStrategy();
            InitializeOrientation();
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
            // Toggle the fullscreen state if the Escape key was pressed this frame on windows
            #if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                
                Screen.fullScreen = !Screen.fullScreen;
            }
            #endif

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
                currentStrategy?.SetAutoPilotValues(new Vector2(ship.AIPilot.X, ship.AIPilot.Y));
            }
            else
            {
                currentStrategy?.SetAutoPilotValues(
                    ship.AIPilot.XSum,
                    ship.AIPilot.YSum,
                    ship.AIPilot.XDiff,
                    ship.AIPilot.YDiff
                );
            }
        }

        private void UpdateInputStrategy()
        {
            IInputStrategy newStrategy = null;

            if (Gamepad.current != null)
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

        IInputStatus TryAddInputStatus()
        {
            bool found = TryGetComponent(out NetworkObject _);
            if (found)
                return gameObject.GetOrAdd<NetworkInputStatus>();
            else
                return gameObject.GetOrAdd<InputStatus>();
        }

    }
}
