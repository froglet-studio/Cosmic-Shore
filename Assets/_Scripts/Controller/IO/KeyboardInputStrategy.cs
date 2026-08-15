using UnityEngine;
using UnityEngine.InputSystem;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Keyboard-only dual-WASD flight. Two digital sticks mix with the same
    /// XSum / YSum / XDiff / YDiff formulas as <see cref="GamepadInputStrategy"/>.
    /// No mouse. No vessel-class special cases — drift and tube bind through
    /// the existing stick/trigger events.
    ///
    /// Left stick WASD:  W +Y, S -Y, A -X, D +X
    /// Right stick P L ; ':  P +Y, ; -Y, L -X, ' +X
    /// Mix: yaw = XSum, pitch = YSum (W+P stacks), speed = XDiff (A+' fast, L+D slow),
    ///      roll = YDiff (P+S left, W+; right). Neutral horizontals → XDiff 0.5 cruise.
    /// Left Shift hold = left trigger (drift). Right Shift press = right trigger (tube).
    /// </summary>
    public class KeyboardInputStrategy : BaseInputStrategy
    {
        private const float TriggerDeadzone = 0.05f;

        private bool fullSpeedStraightEffectsStarted;
        private bool minimumSpeedStraightEffectsStarted;

        private Vector2 leftStickRaw;
        private Vector2 rightStickRaw;

        private bool prevLeftTriggerActive;
        private bool prevRightTriggerActive;

        public override void Initialize(IInputStatus inputStatus)
        {
            base.Initialize(inputStatus);
            ResetInput();
            ResetStrategyState();
        }

        public override void OnStrategyActivated()
        {
            base.OnStrategyActivated();
            ResetStrategyState();
            inputStatus.ActiveInputDevice = InputDeviceType.Keyboard;
        }

        public override void OnStrategyDeactivated()
        {
            ReleaseHeldTriggers();
            ReleaseSpeedEffects();
            ResetInput();
            ResetStrategyState();
        }

        public override void OnPaused()
        {
            ReleaseHeldTriggers();
            leftStickRaw = Vector2.zero;
            rightStickRaw = Vector2.zero;
            inputStatus.LeftTriggerAnalog = 0f;
            inputStatus.RightTriggerAnalog = 0f;
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
            leftStickRaw = new Vector2(
                Axis(keyboard.dKey.isPressed, keyboard.aKey.isPressed),
                Axis(keyboard.wKey.isPressed, keyboard.sKey.isPressed));

            rightStickRaw = new Vector2(
                Axis(keyboard.quoteKey.isPressed, keyboard.lKey.isPressed),
                Axis(keyboard.pKey.isPressed, keyboard.semicolonKey.isPressed));
        }

        private static float Axis(bool positive, bool negative)
        {
            float value = 0f;
            if (positive) value += 1f;
            if (negative) value -= 1f;
            return value;
        }

        private void ProcessButtonInput(Keyboard keyboard)
        {
            if (keyboard.spaceKey.wasPressedThisFrame)
                inputStatus.OnButtonPressed.Raise(InputEvents.Button1Action);
            if (keyboard.spaceKey.wasReleasedThisFrame)
                inputStatus.OnButtonReleased.Raise(InputEvents.Button1Action);

            if (keyboard.bKey.wasPressedThisFrame)
                inputStatus.OnButtonPressed.Raise(InputEvents.Button2Action);
            if (keyboard.bKey.wasReleasedThisFrame)
                inputStatus.OnButtonReleased.Raise(InputEvents.Button2Action);

            if (keyboard.nKey.wasPressedThisFrame)
                inputStatus.OnButtonPressed.Raise(InputEvents.Button3Action);
            if (keyboard.nKey.wasReleasedThisFrame)
                inputStatus.OnButtonReleased.Raise(InputEvents.Button3Action);

            if (keyboard.eKey.wasPressedThisFrame)
                inputStatus.OnButtonPressed.Raise(InputEvents.FlipAction);
            if (keyboard.eKey.wasReleasedThisFrame)
                inputStatus.OnButtonReleased.Raise(InputEvents.FlipAction);

            inputStatus.Throttle = keyboard.eKey.isPressed ? 1f : 0f;

            float leftTriggerValue = keyboard.leftShiftKey.isPressed ? 1f : 0f;
            float rightTriggerValue = keyboard.rightShiftKey.isPressed ? 1f : 0f;
            DispatchTriggers(leftTriggerValue, rightTriggerValue);
        }

        private void DispatchTriggers(float leftTriggerValue, float rightTriggerValue)
        {
            inputStatus.LeftTriggerAnalog = leftTriggerValue;
            inputStatus.RightTriggerAnalog = rightTriggerValue;

            bool leftActive = leftTriggerValue > TriggerDeadzone;
            bool rightActive = rightTriggerValue > TriggerDeadzone;

            bool leftJustPressed = leftActive && !prevLeftTriggerActive;
            bool leftJustReleased = !leftActive && prevLeftTriggerActive;
            bool leftHeld = leftActive;

            bool rightJustPressed = rightActive && !prevRightTriggerActive;
            bool rightJustReleased = !rightActive && prevRightTriggerActive;
            bool rightHeld = rightActive;

            prevLeftTriggerActive = leftActive;
            prevRightTriggerActive = rightActive;

            if (leftJustPressed)
                inputStatus.OnButtonPressed.Raise(InputEvents.LeftStickAction);
            if (leftJustReleased)
                inputStatus.OnButtonReleased.Raise(InputEvents.LeftStickAction);
            if (rightJustPressed)
                inputStatus.OnButtonPressed.Raise(InputEvents.RightStickAction);
            if (rightJustReleased)
                inputStatus.OnButtonReleased.Raise(InputEvents.RightStickAction);

            if ((leftJustReleased && rightJustReleased)
                || (leftJustReleased && rightHeld)
                || (rightJustReleased && leftHeld))
                inputStatus.OnButtonReleased.Raise(InputEvents.BothSticksAction);

            if ((leftJustReleased && !rightHeld)
                || (rightJustPressed && leftHeld))
                inputStatus.OnButtonReleased.Raise(InputEvents.OnlyLeftStickAction);

            if ((rightJustReleased && !leftHeld)
                || (leftJustPressed && rightHeld))
                inputStatus.OnButtonReleased.Raise(InputEvents.OnlyRightStickAction);

            if ((leftJustPressed && !rightHeld)
                || (rightJustReleased && leftHeld))
                inputStatus.OnButtonPressed.Raise(InputEvents.OnlyLeftStickAction);

            if ((rightJustPressed && !leftHeld)
                || (leftJustReleased && rightHeld))
                inputStatus.OnButtonPressed.Raise(InputEvents.OnlyRightStickAction);

            if ((leftJustPressed && rightJustPressed)
                || (leftJustPressed && rightHeld)
                || (rightJustPressed && leftHeld))
                inputStatus.OnButtonPressed.Raise(InputEvents.BothSticksAction);
        }

        private void Reparameterize()
        {
            inputStatus.EasedLeftJoystickPosition = new Vector2(
                Ease(2 * leftStickRaw.x),
                Ease(2 * leftStickRaw.y)
            );
            inputStatus.EasedRightJoystickPosition = new Vector2(
                Ease(2 * rightStickRaw.x),
                Ease(2 * rightStickRaw.y)
            );

            inputStatus.RightNormalizedJoystickPosition = rightStickRaw;
            inputStatus.LeftNormalizedJoystickPosition = leftStickRaw;

            inputStatus.XSum = Ease(rightStickRaw.x + leftStickRaw.x);
            inputStatus.YSum = -Ease(rightStickRaw.y + leftStickRaw.y);
            inputStatus.XDiff = (rightStickRaw.x - leftStickRaw.x + 2) / 4;
            inputStatus.YDiff = Ease(rightStickRaw.y - leftStickRaw.y);

            if (inputStatus.InvertYEnabled)
            {
                inputStatus.YSum *= -1f;
                inputStatus.YDiff *= -1f;
            }

            if (inputStatus.InvertThrottleEnabled)
            {
                inputStatus.XDiff = 1f - inputStatus.XDiff;
            }
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
            }
            else if (DeviationFromMinimumSpeedStraight < threshold && !minimumSpeedStraightEffectsStarted)
            {
                minimumSpeedStraightEffectsStarted = true;
                inputStatus.OnButtonPressed.Raise(InputEvents.MinimumSpeedStraightAction);
            }
            else
            {
                if (fullSpeedStraightEffectsStarted && DeviationFromFullSpeedStraight > threshold)
                {
                    fullSpeedStraightEffectsStarted = false;
                    inputStatus.OnButtonReleased.Raise(InputEvents.FullSpeedStraightAction);
                }
                if (minimumSpeedStraightEffectsStarted && DeviationFromMinimumSpeedStraight > threshold)
                {
                    minimumSpeedStraightEffectsStarted = false;
                    inputStatus.OnButtonReleased.Raise(InputEvents.MinimumSpeedStraightAction);
                }
            }
        }

        private void ReleaseHeldTriggers()
        {
            if (prevLeftTriggerActive || prevRightTriggerActive)
                DispatchTriggers(0f, 0f);
        }

        private void ReleaseSpeedEffects()
        {
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

        private void ResetStrategyState()
        {
            leftStickRaw = Vector2.zero;
            rightStickRaw = Vector2.zero;
            prevLeftTriggerActive = false;
            prevRightTriggerActive = false;
            fullSpeedStraightEffectsStarted = false;
            minimumSpeedStraightEffectsStarted = false;
        }

        public override void SetPortrait(bool portrait)
        {
        }
    }
}
