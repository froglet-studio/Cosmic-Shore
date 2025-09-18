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

        public override void Initialize(IInputStatus inputStatus)
        {
            base.Initialize(inputStatus);
            ResetInput();
            InitializeJoystickPositions();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void InitializeJoystickPositions()
        {
            // Match the touch input joystick home positions for consistency
            inputStatus.LeftJoystickHome = new Vector2(Screen.dpi, Screen.dpi);
            inputStatus.RightJoystickHome = new Vector2(Screen.currentResolution.width - Screen.dpi, Screen.dpi);
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
                    inputStatus.OnButtonPressed.Raise(InputEvents.LeftStickAction);
                    // vessel.PerformShipControllerActions(InputEvents.LeftStickAction);
                }
            }
            else if (Keyboard.current.sKey.isPressed)
            {
                currentThrottle -= THROTTLE_SPEED * Time.deltaTime;
                if (!leftStickEffectsStarted)
                {
                    leftStickEffectsStarted = true;
                    inputStatus.OnButtonPressed.Raise(InputEvents.LeftStickAction);
                    // vessel.PerformShipControllerActions(InputEvents.LeftStickAction);
                }
            }
            else if (leftStickEffectsStarted)
            {
                leftStickEffectsStarted = false;
                inputStatus.OnButtonReleased.Raise(InputEvents.LeftStickAction);
                // vessel.StopShipControllerActions(InputEvents.LeftStickAction);
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
                    inputStatus.OnButtonPressed.Raise(InputEvents.RightStickAction);
                    // vessel.PerformShipControllerActions(InputEvents.RightStickAction);
                }
            }
            else if (Keyboard.current.dKey.isPressed)
            {
                currentRoll += ROLL_SPEED * Time.deltaTime;
                if (!rightStickEffectsStarted)
                {
                    rightStickEffectsStarted = true;
                    inputStatus.OnButtonPressed.Raise(InputEvents.RightStickAction);
                    // vessel.PerformShipControllerActions(InputEvents.RightStickAction);
                }
            }
            else if (rightStickEffectsStarted)
            {
                rightStickEffectsStarted = false;
                inputStatus.OnButtonReleased.Raise(InputEvents.RightStickAction);
                // vessel.StopShipControllerActions(InputEvents.RightStickAction);
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
                inputStatus.OnButtonPressed.Raise(InputEvents.Button1Action);
                // vessel.PerformShipControllerActions(InputEvents.Button1Action);
            if (Mouse.current.leftButton.wasReleasedThisFrame)
                inputStatus.OnButtonReleased.Raise(InputEvents.Button1Action);
                // vessel.StopShipControllerActions(InputEvents.Button1Action);

            if (Mouse.current.middleButton.wasPressedThisFrame)
                inputStatus.OnButtonPressed.Raise(InputEvents.Button2Action);
                // vessel.PerformShipControllerActions(InputEvents.Button2Action);
            if (Mouse.current.middleButton.wasReleasedThisFrame)
                inputStatus.OnButtonReleased.Raise(InputEvents.Button2Action);
                // vessel.StopShipControllerActions(InputEvents.Button2Action);

            if (Mouse.current.rightButton.wasPressedThisFrame)
                inputStatus.OnButtonPressed.Raise(InputEvents.Button3Action);
                // vessel.PerformShipControllerActions(InputEvents.Button3Action);
            if (Mouse.current.rightButton.wasReleasedThisFrame)
                inputStatus.OnButtonReleased.Raise(InputEvents.Button3Action);
                // vessel.StopShipControllerActions(InputEvents.Button3Action);

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
            inputStatus.LeftClampedPosition = inputStatus.LeftJoystickHome + rawLeftStick * Screen.dpi;
            inputStatus.RightClampedPosition = inputStatus.RightJoystickHome + rawRightStick * Screen.dpi;
        }

        private void Reparameterize()
        {
            // Calculate eased joystick positions (matching touch/gamepad behavior)
            inputStatus.EasedLeftJoystickPosition = new Vector2(
                Ease(2 * rawLeftStick.x),
                Ease(2 * rawLeftStick.y)
            );
            inputStatus.EasedRightJoystickPosition = new Vector2(
                Ease(2 * rawRightStick.x),
                Ease(2 * rawRightStick.y)
            );

            // Match the exact calculations from the original input controller
            inputStatus.XSum = Ease(mouseMovement.x);  // Yaw from mouse X
            inputStatus.YSum = Ease(-mouseMovement.y);  // Pitch from mouse Y (inverted)
            inputStatus.XDiff = currentThrottle;  // Throttle from W/S
            inputStatus.YDiff = Ease(currentRoll);  // Roll from A/D

            if (inputStatus.InvertYEnabled)
                inputStatus.YSum *= -1;
            if (inputStatus.InvertThrottleEnabled)
                inputStatus.XDiff = 1 - inputStatus.XDiff;
        }

        private void PerformSpeedAndDirectionalEffects()
        {
            float threshold = .3f;
            float sumOfRotations = Mathf.Abs(inputStatus.YDiff) + Mathf.Abs(inputStatus.YSum) + Mathf.Abs(inputStatus.XSum);
            float DeviationFromFullSpeedStraight = (1 - inputStatus.XDiff) + sumOfRotations;
            float DeviationFromMinimumSpeedStraight = inputStatus.XDiff + sumOfRotations;

            if (DeviationFromFullSpeedStraight < threshold && !fullSpeedStraightEffectsStarted)
            {
                fullSpeedStraightEffectsStarted = true;
                inputStatus.OnButtonPressed.Raise(InputEvents.FullSpeedStraightAction);
                // vessel.PerformShipControllerActions(InputEvents.FullSpeedStraightAction);
            }
            else if (fullSpeedStraightEffectsStarted && DeviationFromFullSpeedStraight > threshold)
            {
                fullSpeedStraightEffectsStarted = false;
                inputStatus.OnButtonReleased.Raise(InputEvents.FullSpeedStraightAction);
                // vessel.StopShipControllerActions(InputEvents.FullSpeedStraightAction);
            }

            if (DeviationFromMinimumSpeedStraight < threshold && !minimumSpeedStraightEffectsStarted)
            {
                minimumSpeedStraightEffectsStarted = true;
                inputStatus.OnButtonPressed.Raise(InputEvents.MinimumSpeedStraightAction);
                // vessel.PerformShipControllerActions(InputEvents.MinimumSpeedStraightAction);
            }
            else if (minimumSpeedStraightEffectsStarted && DeviationFromMinimumSpeedStraight > threshold)
            {
                minimumSpeedStraightEffectsStarted = false;
                inputStatus.OnButtonReleased.Raise(InputEvents.MinimumSpeedStraightAction);
                // vessel.StopShipControllerActions(InputEvents.MinimumSpeedStraightAction);
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
