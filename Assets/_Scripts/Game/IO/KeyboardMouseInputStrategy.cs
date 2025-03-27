using UnityEngine;
using UnityEngine.InputSystem;


namespace CosmicShore.Game.IO
{
    public class KeyboardMouseInputStrategy : BaseInputStrategy
    {
        private const float MOUSE_SENSITIVITY = 2.0f;
        private const float THROTTLE_SPEED = 0.5f;
        private const float ROLL_SPEED = 2.0f;
        private const float THROTTLE_DECAY = 0.1f;

        private bool leftStickEffectsStarted;
        private bool rightStickEffectsStarted;
        private bool fullSpeedStraightEffectsStarted;
        private bool minimumSpeedStraightEffectsStarted;

        private Vector2 mouseMovement;
        private float currentThrottle;
        private float currentRoll;
        private Vector2 rawLeftStick;  // For consistent parameterization
        private Vector2 rawRightStick; // For consistent parameterization

        public override void Initialize(IShip ship)
        {
            base.Initialize(ship);
            ResetInput();
            InitializeJoystickPositions();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void InitializeJoystickPositions()
        {
            // Match the touch input joystick home positions for consistency
            InputStatus.LeftJoystickHome = new Vector2(Screen.dpi, Screen.dpi);
            InputStatus.RightJoystickHome = new Vector2(Screen.currentResolution.width - Screen.dpi, Screen.dpi);
        }

        public override void ProcessInput()
        {
            if (Keyboard.current == null || Mouse.current == null) return;

            ProcessKeyboardInput();
            ProcessMouseInput();
            ProcessActionButtons();

            // Convert keyboard/mouse input to stick-equivalent values
            UpdateStickValues();
            Reparameterize();
            PerformSpeedAndDirectionalEffects();
        }

        private void ProcessKeyboardInput()
        {
            // Throttle control (WS) - XDiff equivalent
            if (Keyboard.current.wKey.isPressed)
            {
                currentThrottle += THROTTLE_SPEED * Time.deltaTime;
                if (!leftStickEffectsStarted)
                {
                    leftStickEffectsStarted = true;
                    _ship.PerformShipControllerActions(InputEvents.LeftStickAction);
                }
            }
            else if (Keyboard.current.sKey.isPressed)
            {
                currentThrottle -= THROTTLE_SPEED * Time.deltaTime;
                if (!leftStickEffectsStarted)
                {
                    leftStickEffectsStarted = true;
                    _ship.PerformShipControllerActions(InputEvents.LeftStickAction);
                }
            }
            else if (leftStickEffectsStarted)
            {
                leftStickEffectsStarted = false;
                _ship.StopShipControllerActions(InputEvents.LeftStickAction);
                currentThrottle = Mathf.MoveTowards(currentThrottle, 0, THROTTLE_DECAY * Time.deltaTime);
            }

            currentThrottle = Mathf.Clamp01(currentThrottle);

            // Roll control (AD) - YDiff equivalent
            currentRoll = 0f;
            if (Keyboard.current.aKey.isPressed)
            {
                currentRoll -= ROLL_SPEED * Time.deltaTime;
                if (!rightStickEffectsStarted)
                {
                    rightStickEffectsStarted = true;
                    _ship.PerformShipControllerActions(InputEvents.RightStickAction);
                }
            }
            else if (Keyboard.current.dKey.isPressed)
            {
                currentRoll += ROLL_SPEED * Time.deltaTime;
                if (!rightStickEffectsStarted)
                {
                    rightStickEffectsStarted = true;
                    _ship.PerformShipControllerActions(InputEvents.RightStickAction);
                }
            }
            else if (rightStickEffectsStarted)
            {
                rightStickEffectsStarted = false;
                _ship.StopShipControllerActions(InputEvents.RightStickAction);
            }
        }

        private void ProcessMouseInput()
        {
            // Scale mouse movement by sensitivity and deltaTime
            mouseMovement = Mouse.current.delta.ReadValue() * MOUSE_SENSITIVITY * Time.deltaTime;

            // Handle right mouse button for potential additional controls
            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else if (Mouse.current.rightButton.wasReleasedThisFrame)
            {
                mouseMovement = Vector2.zero;
            }
        }

        private void ProcessActionButtons()
        {
            if (Mouse.current.leftButton.wasPressedThisFrame)
                _ship.PerformShipControllerActions(InputEvents.Button1Action);
            if (Mouse.current.leftButton.wasReleasedThisFrame)
                _ship.StopShipControllerActions(InputEvents.Button1Action);

            if (Mouse.current.middleButton.wasPressedThisFrame)
                _ship.PerformShipControllerActions(InputEvents.Button2Action);
            if (Mouse.current.middleButton.wasReleasedThisFrame)
                _ship.StopShipControllerActions(InputEvents.Button2Action);

            if (Mouse.current.rightButton.wasPressedThisFrame)
                _ship.PerformShipControllerActions(InputEvents.Button3Action);
            if (Mouse.current.rightButton.wasReleasedThisFrame)
                _ship.StopShipControllerActions(InputEvents.Button3Action);

            // Handle escape for cursor lock toggle
            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = Cursor.lockState == CursorLockMode.Locked ?
                    CursorLockMode.None : CursorLockMode.Locked;
                Cursor.visible = Cursor.lockState == CursorLockMode.None;
            }
        }

        private void UpdateStickValues()
        {
            // Convert mouse/keyboard input to equivalent stick values
            rawLeftStick = new Vector2(0, currentThrottle * 2 - 1);  // Map throttle to Y axis
            rawRightStick = new Vector2(mouseMovement.x, -mouseMovement.y);  // Mouse X/Y for yaw/pitch

            // Update clamped positions for visual feedback
            InputStatus.LeftClampedPosition = InputStatus.LeftJoystickHome + rawLeftStick * Screen.dpi;
            InputStatus.RightClampedPosition = InputStatus.RightJoystickHome + rawRightStick * Screen.dpi;
        }

        private void Reparameterize()
        {
            // Calculate eased joystick positions (matching touch/gamepad behavior)
            InputStatus.EasedLeftJoystickPosition = new Vector2(
                Ease(2 * rawLeftStick.x),
                Ease(2 * rawLeftStick.y)
            );
            InputStatus.EasedRightJoystickPosition = new Vector2(
                Ease(2 * rawRightStick.x),
                Ease(2 * rawRightStick.y)
            );

            // Match the exact calculations from the original input controller
            InputStatus.XSum = Ease(mouseMovement.x);  // Yaw from mouse X
            InputStatus.YSum = Ease(-mouseMovement.y);  // Pitch from mouse Y (inverted)
            InputStatus.XDiff = currentThrottle;  // Throttle from W/S
            InputStatus.YDiff = Ease(currentRoll);  // Roll from A/D

            if (InputStatus.InvertYEnabled)
                InputStatus.YSum *= -1;
            if (InputStatus.InvertThrottleEnabled)
                InputStatus.XDiff = 1 - InputStatus.XDiff;
        }

        private void PerformSpeedAndDirectionalEffects()
        {
            float threshold = .3f;
            float sumOfRotations = Mathf.Abs(InputStatus.YDiff) + Mathf.Abs(InputStatus.YSum) + Mathf.Abs(InputStatus.XSum);
            float DeviationFromFullSpeedStraight = (1 - InputStatus.XDiff) + sumOfRotations;
            float DeviationFromMinimumSpeedStraight = InputStatus.XDiff + sumOfRotations;

            if (DeviationFromFullSpeedStraight < threshold && !fullSpeedStraightEffectsStarted)
            {
                fullSpeedStraightEffectsStarted = true;
                _ship.PerformShipControllerActions(InputEvents.FullSpeedStraightAction);
            }
            else if (fullSpeedStraightEffectsStarted && DeviationFromFullSpeedStraight > threshold)
            {
                fullSpeedStraightEffectsStarted = false;
                _ship.StopShipControllerActions(InputEvents.FullSpeedStraightAction);
            }

            if (DeviationFromMinimumSpeedStraight < threshold && !minimumSpeedStraightEffectsStarted)
            {
                minimumSpeedStraightEffectsStarted = true;
                _ship.PerformShipControllerActions(InputEvents.MinimumSpeedStraightAction);
            }
            else if (minimumSpeedStraightEffectsStarted && DeviationFromMinimumSpeedStraight > threshold)
            {
                minimumSpeedStraightEffectsStarted = false;
                _ship.StopShipControllerActions(InputEvents.MinimumSpeedStraightAction);
            }
        }

        public override void OnStrategyActivated()
        {
            base.OnStrategyActivated();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public override void OnStrategyDeactivated()
        {
            base.OnStrategyDeactivated();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            ResetInput();
        }

        public override void OnPaused()
        {
            base.OnPaused();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public override void OnResumed()
        {
            base.OnResumed();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        protected override void ResetInput()
        {
            base.ResetInput();
            mouseMovement = Vector2.zero;
            currentThrottle = 0f;
            currentRoll = 0f;
            rawLeftStick = Vector2.zero;
            rawRightStick = Vector2.zero;
            leftStickEffectsStarted = false;
            rightStickEffectsStarted = false;
            fullSpeedStraightEffectsStarted = false;
            minimumSpeedStraightEffectsStarted = false;
        }
    }
}
