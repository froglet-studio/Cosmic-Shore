using UnityEngine;
using CosmicShore.Core;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
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
        public IShip Ship { get; private set; }

        // public bool AutoPilotEnabled => Ship.ShipStatus.AutoPilotEnabled;

        [HideInInspector] public static ScreenOrientation currentOrientation;

        private IInputStrategy currentStrategy;
        //private TouchInputStrategy touchStrategy;
        //private KeyboardMouseInputStrategy keyboardMouseStrategy;
        private GamepadInputStrategy gamepadStrategy;
        private DeviceOrientationHandler orientationHandler;

        private void Awake()
        {
            enabled = false;
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
            {
                Screen.fullScreen = !Screen.fullScreen;
            }
#endif

            if (InputStatus.Paused)
            {
                if (Ship != null && Ship.ShipStatus.AutoPilotEnabled)
                {
                    Debug.Log($"InputController.Update: {InputStatus.Paused} && {Ship.ShipStatus.AutoPilotEnabled} AutoPilot -> calling ProcessAutoPilot()");
                    //Debug.Log("Input Status")
                    ProcessAutoPilot();
                }
                return;
            }

            if (Ship != null && Ship.ShipStatus.AutoPilotEnabled)
            {
                // Debug.Log("InputController.Update: AutoPilotEnabled -> calling ProcessAutoPilot()");
                ProcessAutoPilot();
                return;
            }

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
            Ship = ship;
            InitializeStrategies();
            SetInitialStrategy();
            InitializeOrientation();

            enabled = true;
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
            gamepadStrategy.Initialize(Ship);
            orientationHandler.Initialize(Ship, this);
        }

        private void InitializeOrientation()
        {
            if (Portrait)
            {
                Ship.SetShipUp(90);
            }
            currentOrientation = Screen.orientation;
        }


        private void ProcessAutoPilot()
        {
            if (currentStrategy == null)
            {
                Debug.LogWarning("ProcessAutoPilot: currentStrategy is NULL. Creating GamepadInputStrategy now.");
                gamepadStrategy = new GamepadInputStrategy();
                gamepadStrategy.Initialize(Ship);
                currentStrategy = gamepadStrategy;
                currentStrategy.OnStrategyActivated();
            }

            if (Ship.ShipStatus.SingleStickControls)
            {
                currentStrategy?.SetAutoPilotValues(new Vector2(Ship.ShipStatus.AIPilot.X, Ship.ShipStatus.AIPilot.Y));
            }
            else
            {
                currentStrategy?.SetAutoPilotValues(
                    Ship.ShipStatus.AIPilot.XSum,
                    Ship.ShipStatus.AIPilot.YSum,
                    Ship.ShipStatus.AIPilot.XDiff,
                    Ship.ShipStatus.AIPilot.YDiff
                );
            }
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
