using UnityEngine;
using UnityEngine.InputSystem;
using CosmicShore.Core;

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
            LeftJoystickHome = new Vector2(Screen.dpi, Screen.dpi);
            RightJoystickHome = new Vector2(Screen.currentResolution.width - Screen.dpi, Screen.dpi);
        }

        public override void ProcessInput(IShip ship)
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
                    ship.PerformShipControllerActions(InputEvents.LeftStickAction);
                }
            }
            else if (Keyboard.current.sKey.isPressed)
            {
                currentThrottle -= THROTTLE_SPEED * Time.deltaTime;
                if (!leftStickEffectsStarted)
                {
                    leftStickEffectsStarted = true;
                    ship.PerformShipControllerActions(InputEvents.LeftStickAction);
                }
            }
            else if (leftStickEffectsStarted)
            {
                leftStickEffectsStarted = false;
                ship.StopShipControllerActions(InputEvents.LeftStickAction);
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
                    ship.PerformShipControllerActions(InputEvents.RightStickAction);
                }
            }
            else if (Keyboard.current.dKey.isPressed)
            {
                currentRoll += ROLL_SPEED * Time.deltaTime;
                if (!rightStickEffectsStarted)
                {
                    rightStickEffectsStarted = true;
                    ship.PerformShipControllerActions(InputEvents.RightStickAction);
                }
            }
            else if (rightStickEffectsStarted)
            {
                rightStickEffectsStarted = false;
                ship.StopShipControllerActions(InputEvents.RightStickAction);
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
                ship.PerformShipControllerActions(InputEvents.Button1Action);
            if (Mouse.current.leftButton.wasReleasedThisFrame)
                ship.StopShipControllerActions(InputEvents.Button1Action);

            if (Mouse.current.middleButton.wasPressedThisFrame)
                ship.PerformShipControllerActions(InputEvents.Button2Action);
            if (Mouse.current.middleButton.wasReleasedThisFrame)
                ship.StopShipControllerActions(InputEvents.Button2Action);

            if (Mouse.current.rightButton.wasPressedThisFrame)
                ship.PerformShipControllerActions(InputEvents.Button3Action);
            if (Mouse.current.rightButton.wasReleasedThisFrame)
                ship.StopShipControllerActions(InputEvents.Button3Action);

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
            LeftClampedPosition = LeftJoystickHome + rawLeftStick * Screen.dpi;
            RightClampedPosition = RightJoystickHome + rawRightStick * Screen.dpi;
        }

        private void Reparameterize()
        {
            // Calculate eased joystick positions (matching touch/gamepad behavior)
            EasedLeftJoystickPosition = new Vector2(
                Ease(2 * rawLeftStick.x),
                Ease(2 * rawLeftStick.y)
            );
            EasedRightJoystickPosition = new Vector2(
                Ease(2 * rawRightStick.x),
                Ease(2 * rawRightStick.y)
            );

            // Match the exact calculations from the original input controller
            XSum = Ease(mouseMovement.x);  // Yaw from mouse X
            YSum = Ease(-mouseMovement.y);  // Pitch from mouse Y (inverted)
            XDiff = currentThrottle;  // Throttle from W/S
            YDiff = Ease(currentRoll);  // Roll from A/D

            if (invertYEnabled)
                YSum *= -1;
            if (invertThrottleEnabled)
                XDiff = 1 - XDiff;
        }

        private void PerformSpeedAndDirectionalEffects()
        {
            float threshold = .3f;
            float sumOfRotations = Mathf.Abs(YDiff) + Mathf.Abs(YSum) + Mathf.Abs(XSum);
            float DeviationFromFullSpeedStraight = (1 - XDiff) + sumOfRotations;
            float DeviationFromMinimumSpeedStraight = XDiff + sumOfRotations;

            if (DeviationFromFullSpeedStraight < threshold && !fullSpeedStraightEffectsStarted)
            {
                fullSpeedStraightEffectsStarted = true;
                ship.PerformShipControllerActions(InputEvents.FullSpeedStraightAction);
            }
            else if (fullSpeedStraightEffectsStarted && DeviationFromFullSpeedStraight > threshold)
            {
                fullSpeedStraightEffectsStarted = false;
                ship.StopShipControllerActions(InputEvents.FullSpeedStraightAction);
            }

            if (DeviationFromMinimumSpeedStraight < threshold && !minimumSpeedStraightEffectsStarted)
            {
                minimumSpeedStraightEffectsStarted = true;
                ship.PerformShipControllerActions(InputEvents.MinimumSpeedStraightAction);
            }
            else if (minimumSpeedStraightEffectsStarted && DeviationFromMinimumSpeedStraight > threshold)
            {
                minimumSpeedStraightEffectsStarted = false;
                ship.StopShipControllerActions(InputEvents.MinimumSpeedStraightAction);
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
