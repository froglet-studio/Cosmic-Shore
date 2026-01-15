using UnityEngine;
using UnityEngine.InputSystem;

namespace CosmicShore.Game.IO
{
    public class KeyboardInputStrategy : BaseInputStrategy
    {
        // Smoothing for analog-like feel
        private const float RAMP_SPEED = 8f;
        private const float DEAD_ZONE = 0.05f;

        // State tracking for effects
        private bool fullSpeedStraightEffectsStarted;
        private bool minimumSpeedStraightEffectsStarted;

        // Virtual stick positions (smoothed)
        private Vector2 leftStickCurrent;
        private Vector2 rightStickCurrent;

        // Target positions (raw key input)
        private Vector2 leftStickTarget;
        private Vector2 rightStickTarget;

        // Button state tracking for press/release detection
        private bool wasButton1Pressed;
        private bool wasButton2Pressed;
        private bool wasButton3Pressed;
        private bool wasLeftTriggerPressed;
        private bool wasRightTriggerPressed;
        private bool wasFlipPressed;

        public override void Initialize(IInputStatus inputStatus)
        {
            base.Initialize(inputStatus);
            ResetInput();
            ResetSmoothingState();
        }

        private void ResetSmoothingState()
        {
            leftStickCurrent = Vector2.zero;
            rightStickCurrent = Vector2.zero;
            leftStickTarget = Vector2.zero;
            rightStickTarget = Vector2.zero;

            wasButton1Pressed = false;
            wasButton2Pressed = false;
            wasButton3Pressed = false;
            wasLeftTriggerPressed = false;
            wasRightTriggerPressed = false;
            wasFlipPressed = false;

            fullSpeedStraightEffectsStarted = false;
            minimumSpeedStraightEffectsStarted = false;
        }

        public override void ProcessInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            ProcessStickInput(keyboard);
            ProcessButtonInput(keyboard);
            Reparameterize();
            PerformSpeedAndDirectionalEffects();
        }

        private void ProcessStickInput(Keyboard keyboard)
        {
            // Read target positions from keys (WASD for left stick)
            leftStickTarget = new Vector2(
                (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f),
                (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f)
            );

            // P;L' for right stick (shifted right from IJKL)
            rightStickTarget = new Vector2(
                (keyboard.quoteKey.isPressed ? 1f : 0f) - (keyboard.lKey.isPressed ? 1f : 0f),
                (keyboard.pKey.isPressed ? 1f : 0f) - (keyboard.semicolonKey.isPressed ? 1f : 0f)
            );

            // Normalize diagonal inputs to prevent faster diagonal movement
            if (leftStickTarget.magnitude > 1f)
                leftStickTarget.Normalize();
            if (rightStickTarget.magnitude > 1f)
                rightStickTarget.Normalize();

            // Smooth interpolation for analog-like feel
            float deltaTime = Time.deltaTime;
            leftStickCurrent = Vector2.Lerp(leftStickCurrent, leftStickTarget, RAMP_SPEED * deltaTime);
            rightStickCurrent = Vector2.Lerp(rightStickCurrent, rightStickTarget, RAMP_SPEED * deltaTime);

            // Apply dead zone
            if (leftStickCurrent.magnitude < DEAD_ZONE)
                leftStickCurrent = Vector2.zero;
            if (rightStickCurrent.magnitude < DEAD_ZONE)
                rightStickCurrent = Vector2.zero;

            // Throttle: E key (binary like gamepad shoulder button)
            inputStatus.Throttle = keyboard.eKey.isPressed ? 1f : 0f;
        }

        private void ProcessButtonInput(Keyboard keyboard)
        {
            // Button 1 - Spacebar (A/South on gamepad)
            bool isButton1Pressed = keyboard.spaceKey.isPressed;
            if (isButton1Pressed && !wasButton1Pressed)
                inputStatus.OnButtonPressed.Raise(InputEvents.Button1Action);
            if (!isButton1Pressed && wasButton1Pressed)
                inputStatus.OnButtonReleased.Raise(InputEvents.Button1Action);
            wasButton1Pressed = isButton1Pressed;

            // Button 2 - B key (B/East on gamepad)
            bool isButton2Pressed = keyboard.bKey.isPressed;
            if (isButton2Pressed && !wasButton2Pressed)
                inputStatus.OnButtonPressed.Raise(InputEvents.Button2Action);
            if (!isButton2Pressed && wasButton2Pressed)
                inputStatus.OnButtonReleased.Raise(InputEvents.Button2Action);
            wasButton2Pressed = isButton2Pressed;

            // Button 3 - N key (X/West on gamepad)
            bool isButton3Pressed = keyboard.nKey.isPressed;
            if (isButton3Pressed && !wasButton3Pressed)
                inputStatus.OnButtonPressed.Raise(InputEvents.Button3Action);
            if (!isButton3Pressed && wasButton3Pressed)
                inputStatus.OnButtonReleased.Raise(InputEvents.Button3Action);
            wasButton3Pressed = isButton3Pressed;

            // Flip Action - E key (Right Shoulder on gamepad)
            bool isFlipPressed = keyboard.eKey.isPressed;
            if (isFlipPressed && !wasFlipPressed)
                inputStatus.OnButtonPressed.Raise(InputEvents.FlipAction);
            if (!isFlipPressed && wasFlipPressed)
                inputStatus.OnButtonReleased.Raise(InputEvents.FlipAction);
            wasFlipPressed = isFlipPressed;


            // Left Trigger - Left Shift
            bool isLeftTriggerPressed = keyboard.leftShiftKey.isPressed;
            bool leftJustPressed = isLeftTriggerPressed && !wasLeftTriggerPressed;
            bool leftJustReleased = !isLeftTriggerPressed && wasLeftTriggerPressed;

            if (leftJustPressed)
                inputStatus.OnButtonPressed.Raise(InputEvents.LeftStickAction);
            if (leftJustReleased)
                inputStatus.OnButtonReleased.Raise(InputEvents.LeftStickAction);

            // Right Trigger - Right Shift
            bool isRightTriggerPressed = keyboard.rightShiftKey.isPressed;
            bool rightJustPressed = isRightTriggerPressed && !wasRightTriggerPressed;
            bool rightJustReleased = !isRightTriggerPressed && wasRightTriggerPressed;

            if (rightJustPressed)
                inputStatus.OnButtonPressed.Raise(InputEvents.RightStickAction);
            if (rightJustReleased)
                inputStatus.OnButtonReleased.Raise(InputEvents.RightStickAction);

            // BothSticksAction Released
            if ((leftJustReleased && rightJustReleased)
                || (leftJustReleased && isRightTriggerPressed)
                || (rightJustReleased && isLeftTriggerPressed))
                inputStatus.OnButtonReleased.Raise(InputEvents.BothSticksAction);

            // OnlyLeftStickAction Released
            if ((leftJustReleased && !isRightTriggerPressed)
                || (rightJustPressed && isLeftTriggerPressed))
                inputStatus.OnButtonReleased.Raise(InputEvents.OnlyLeftStickAction);

            // OnlyRightStickAction Released
            if ((rightJustReleased && !isLeftTriggerPressed)
                || (leftJustPressed && isRightTriggerPressed))
                inputStatus.OnButtonReleased.Raise(InputEvents.OnlyRightStickAction);

            // OnlyLeftStickAction Pressed
            if ((leftJustPressed && !isRightTriggerPressed)
                || (rightJustReleased && isLeftTriggerPressed))
                inputStatus.OnButtonPressed.Raise(InputEvents.OnlyLeftStickAction);

            // OnlyRightStickAction Pressed
            if ((rightJustPressed && !isLeftTriggerPressed)
                || (leftJustReleased && isRightTriggerPressed))
                inputStatus.OnButtonPressed.Raise(InputEvents.OnlyRightStickAction);

            // BothSticksAction Pressed
            if ((leftJustPressed && rightJustPressed)
                || (leftJustPressed && isRightTriggerPressed)
                || (rightJustPressed && isLeftTriggerPressed))
                inputStatus.OnButtonPressed.Raise(InputEvents.BothSticksAction);

            // Update previous state
            wasLeftTriggerPressed = isLeftTriggerPressed;
            wasRightTriggerPressed = isRightTriggerPressed;
        }

        private void Reparameterize()
        {
            // Calculate eased joystick positions
            inputStatus.EasedLeftJoystickPosition = new Vector2(
                Ease(2 * leftStickCurrent.x),
                Ease(2 * leftStickCurrent.y)
            );
            inputStatus.EasedRightJoystickPosition = new Vector2(
                Ease(2 * rightStickCurrent.x),
                Ease(2 * rightStickCurrent.y)
            );

            // Calculate sums and differences exactly as gamepad/touch input does
            inputStatus.XSum = Ease(rightStickCurrent.x + leftStickCurrent.x);
            inputStatus.YSum = -Ease(rightStickCurrent.y + leftStickCurrent.y);
            inputStatus.XDiff = (rightStickCurrent.x - leftStickCurrent.x + 2) / 4;
            inputStatus.YDiff = Ease(rightStickCurrent.y - leftStickCurrent.y);
        }

        private void PerformSpeedAndDirectionalEffects()
        {
            float threshold = 0.3f;
            float sumOfRotations = Mathf.Abs(inputStatus.YDiff) + Mathf.Abs(inputStatus.YSum) + Mathf.Abs(inputStatus.XSum);
            float deviationFromFullSpeedStraight = (1 - inputStatus.XDiff) + sumOfRotations;
            float deviationFromMinimumSpeedStraight = inputStatus.XDiff + sumOfRotations;

            if (deviationFromFullSpeedStraight < threshold && !fullSpeedStraightEffectsStarted)
            {
                fullSpeedStraightEffectsStarted = true;
                inputStatus.OnButtonPressed.Raise(InputEvents.FullSpeedStraightAction);
            }
            else if (deviationFromMinimumSpeedStraight < threshold && !minimumSpeedStraightEffectsStarted)
            {
                minimumSpeedStraightEffectsStarted = true;
                inputStatus.OnButtonPressed.Raise(InputEvents.MinimumSpeedStraightAction);
            }
            else
            {
                if (fullSpeedStraightEffectsStarted && deviationFromFullSpeedStraight > threshold)
                {
                    fullSpeedStraightEffectsStarted = false;
                    inputStatus.OnButtonReleased.Raise(InputEvents.FullSpeedStraightAction);
                }
                if (minimumSpeedStraightEffectsStarted && deviationFromMinimumSpeedStraight > threshold)
                {
                    minimumSpeedStraightEffectsStarted = false;
                    inputStatus.OnButtonReleased.Raise(InputEvents.MinimumSpeedStraightAction);
                }
            }
        }

        public override void OnStrategyActivated()
        {
            ResetSmoothingState();
        }

        public override void OnStrategyDeactivated()
        {
            // Stop any ongoing effects
            if (fullSpeedStraightEffectsStarted)
            {
                fullSpeedStraightEffectsStarted = false;
                inputStatus.OnButtonReleased.Raise(InputEvents.FullSpeedStraightAction);
            }
            if (minimumSpeedStraightEffectsStarted)
            {
                minimumSpeedStraightEffectsStarted = false;
                inputStatus.OnButtonReleased.Raise(InputEvents.MinimumSpeedStraightAction);
            }
        }

        public override void OnPaused()
        {
            // Reset stick positions when paused to prevent drift
            leftStickCurrent = Vector2.zero;
            rightStickCurrent = Vector2.zero;
            leftStickTarget = Vector2.zero;
            rightStickTarget = Vector2.zero;
        }

        public override void SetPortrait(bool portrait)
        {
            // Keyboard doesn't need to handle portrait mode changes
        }
    }
}