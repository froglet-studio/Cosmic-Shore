using CosmicShore.Data;
using CosmicShore.Gameplay.MultiMouse;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Two-mouse symmetric flight controls - the desktop-with-two-mice
    /// counterpart of touch (two thumbs), gamepad (two thumbsticks) and
    /// keyboard (two hands).
    ///
    /// Each physical mouse acts as a virtual stick:
    ///   movement deflects the stick (accumulated delta, clamped to a unit
    ///   circle), and the stick eases back to center when the mouse is
    ///   still. Buttons mirror the gamepad mapping:
    ///     LMB on left mouse  → Button1Action
    ///     LMB on right mouse → Button2Action
    ///     MMB on either      → Button3Action
    ///     RMB on left mouse  → LeftStickAction  (drift trigger)
    ///     RMB on right mouse → RightStickAction (drift trigger)
    /// Plus the OnlyLeft / OnlyRight / Both composites the other strategies
    /// raise from these two trigger states.
    /// </summary>
    public sealed class DualMouseInputStrategy : BaseInputStrategy
    {
        private const float MOUSE_TO_STICK = 0.013f;   // pixels of delta → stick units
        private const float STICK_RECENTER_RATE = .1f;  // exponential pull toward 0 when idle
        private const float DEAD_ZONE = 0.01f;

        private readonly MultiMouseService mice;

        private Vector2 leftStick;
        private Vector2 rightStick;

        private bool prevLeftLmb, prevLeftRmb, prevAnyMmb;
        private bool prevRightLmb, prevRightRmb;

        private bool fullSpeedStraightEffectsStarted;
        private bool minimumSpeedStraightEffectsStarted;

        public DualMouseInputStrategy(MultiMouseService mice)
        {
            this.mice = mice;
        }

        public override void Initialize(IInputStatus inputStatus)
        {
            base.Initialize(inputStatus);
            ResetInput();
        }

        public override void OnStrategyActivated()
        {
            base.OnStrategyActivated();
            inputStatus.ActiveInputDevice = InputDeviceType.DualMouse;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            ResetInput();

            // Snapshot current button state into prev* so the engagement
            // gesture itself (both LMB held) doesn't fire a phantom press
            // edge on the first frame after activation. Also drain any
            // pre-engagement cursor delta so the first frame doesn't snap
            // the virtual sticks.
            if (mice != null && mice.DeviceCount >= 2)
            {
                var l = mice.GetDevice(0);
                var r = mice.GetDevice(1);
                if (l != null && r != null)
                {
                    prevLeftLmb = l.LeftButton;
                    prevRightLmb = r.LeftButton;
                    prevLeftRmb = l.RightButton;
                    prevRightRmb = r.RightButton;
                    prevAnyMmb = l.MiddleButton || r.MiddleButton;

                    l.ConsumeDelta();
                    r.ConsumeDelta();
                }
            }
        }

        public override void OnStrategyDeactivated()
        {
            base.OnStrategyDeactivated();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            ReleaseTriggersOnExit();
        }

        public override void OnPaused()
        {
            base.OnPaused();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            leftStick = Vector2.zero;
            rightStick = Vector2.zero;
        }

        public override void OnResumed()
        {
            base.OnResumed();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public override void ProcessInput()
        {
            // The InputController ticks MultiMouseService once per frame in
            // SelectStrategy() - don't tick again here or we'd double-count
            // the Unity Input System frame delta.
            if (mice == null || mice.DeviceCount < 2) return;

            var left = mice.GetDevice(0);
            var right = mice.GetDevice(1);
            if (left == null || right == null) return;

            UpdateVirtualStick(ref leftStick, left.ConsumeDelta());
            UpdateVirtualStick(ref rightStick, right.ConsumeDelta());

            ProcessButtonInput(left, right);
            Reparameterize();
            PerformSpeedAndDirectionalEffects();
        }

        private static void UpdateVirtualStick(ref Vector2 stick, Vector2 delta)
        {
            stick += delta * MOUSE_TO_STICK;

            // ease back toward center when no input - exponential decay so
            // letting go of the mouse returns to neutral
            if (delta.sqrMagnitude < 0.0001f)
                stick = Vector2.Lerp(stick, Vector2.zero, STICK_RECENTER_RATE * Time.deltaTime);

            stick = Vector2.ClampMagnitude(stick, 1f);

            if (stick.sqrMagnitude < DEAD_ZONE * DEAD_ZONE)
                stick = Vector2.zero;
        }

        private void ProcessButtonInput(IMultiMouseDevice left, IMultiMouseDevice right)
        {
            // Action buttons (LMB on each mouse + middle on either)
            EdgeFire(left.LeftButton, ref prevLeftLmb, InputEvents.Button1Action);
            EdgeFire(right.LeftButton, ref prevRightLmb, InputEvents.Button2Action);
            EdgeFire(left.MiddleButton || right.MiddleButton,
                     ref prevAnyMmb, InputEvents.Button3Action);

            // Stick triggers (RMB on each mouse) - mirror gamepad shoulder semantics
            bool leftActive = left.RightButton;
            bool rightActive = right.RightButton;

            inputStatus.LeftTriggerAnalog = leftActive ? 1f : 0f;
            inputStatus.RightTriggerAnalog = rightActive ? 1f : 0f;

            bool leftJustPressed = leftActive && !prevLeftRmb;
            bool leftJustReleased = !leftActive && prevLeftRmb;
            bool rightJustPressed = rightActive && !prevRightRmb;
            bool rightJustReleased = !rightActive && prevRightRmb;

            if (leftJustPressed) inputStatus.OnButtonPressed.Raise(InputEvents.LeftStickAction);
            if (leftJustReleased) inputStatus.OnButtonReleased.Raise(InputEvents.LeftStickAction);
            if (rightJustPressed) inputStatus.OnButtonPressed.Raise(InputEvents.RightStickAction);
            if (rightJustReleased) inputStatus.OnButtonReleased.Raise(InputEvents.RightStickAction);

            // Composite events (mirrors gamepad/keyboard)
            if ((leftJustReleased && rightJustReleased)
                || (leftJustReleased && rightActive)
                || (rightJustReleased && leftActive))
                inputStatus.OnButtonReleased.Raise(InputEvents.BothSticksAction);

            if ((leftJustReleased && !rightActive)
                || (rightJustPressed && leftActive))
                inputStatus.OnButtonReleased.Raise(InputEvents.OnlyLeftStickAction);

            if ((rightJustReleased && !leftActive)
                || (leftJustPressed && rightActive))
                inputStatus.OnButtonReleased.Raise(InputEvents.OnlyRightStickAction);

            if ((leftJustPressed && !rightActive)
                || (rightJustReleased && leftActive))
                inputStatus.OnButtonPressed.Raise(InputEvents.OnlyLeftStickAction);

            if ((rightJustPressed && !leftActive)
                || (leftJustReleased && rightActive))
                inputStatus.OnButtonPressed.Raise(InputEvents.OnlyRightStickAction);

            if ((leftJustPressed && rightJustPressed)
                || (leftJustPressed && rightActive)
                || (rightJustPressed && leftActive))
                inputStatus.OnButtonPressed.Raise(InputEvents.BothSticksAction);

            prevLeftRmb = leftActive;
            prevRightRmb = rightActive;
        }

        private void EdgeFire(bool isPressed, ref bool prev, InputEvents evt)
        {
            if (isPressed && !prev) inputStatus.OnButtonPressed.Raise(evt);
            if (!isPressed && prev) inputStatus.OnButtonReleased.Raise(evt);
            prev = isPressed;
        }

        private void Reparameterize()
        {
            inputStatus.EasedLeftJoystickPosition = new Vector2(
                Ease(2 * leftStick.x),
                Ease(2 * leftStick.y));
            inputStatus.EasedRightJoystickPosition = new Vector2(
                Ease(2 * rightStick.x),
                Ease(2 * rightStick.y));

            // Publish the NORMALIZED sticks too, exactly as the other three strategies do
            // (GamepadInputStrategy / KeyboardInputStrategy / TouchInputStrategy all set these
            // beside the eased pair). This strategy set only the eased pair, so every consumer of
            // the radially-clamped stick read a permanent (0,0) on dual mouse: the Scarab's dash —
            // and therefore its cavitation blast — could never fire, the Sparrow's gun aim
            // (GunTransformer) never tracked, and the thumb-perimeter UI never moved. UpdateVirtualStick
            // already ClampMagnitude's to 1, so these are the same contract the perimeter test
            // (|stick| >= 1 - epsilon) is written against; nothing here is newly computed, it was
            // simply never handed over.
            inputStatus.RightNormalizedJoystickPosition = rightStick;
            inputStatus.LeftNormalizedJoystickPosition = leftStick;

            // Match gamepad / keyboard reparameterization so the downstream
            // vessel controllers see identical XSum/YSum/XDiff/YDiff shapes.
            // For dual mouse, pitch and roll are swapped relative to gamepad:
            // both mice fore/aft together = roll, differential = pitch.
            inputStatus.XSum = Ease(rightStick.x + leftStick.x);
            inputStatus.YSum = Ease(rightStick.y + leftStick.y);
            inputStatus.XDiff = (leftStick.x - rightStick.x + 2) / 4;
            inputStatus.YDiff = Ease(rightStick.y - leftStick.y);

            if (inputStatus.InvertYEnabled)
            {
                inputStatus.YSum *= -1f;
                inputStatus.YDiff *= -1f;
            }
            if (inputStatus.InvertThrottleEnabled)
                inputStatus.XDiff = 1f - inputStatus.XDiff;
        }

        private void PerformSpeedAndDirectionalEffects()
        {
            const float threshold = 0.3f;
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

        private void ReleaseTriggersOnExit()
        {
            if (prevLeftRmb)
            {
                inputStatus.OnButtonReleased.Raise(InputEvents.LeftStickAction);
                prevLeftRmb = false;
            }
            if (prevRightRmb)
            {
                inputStatus.OnButtonReleased.Raise(InputEvents.RightStickAction);
                prevRightRmb = false;
            }
            if (fullSpeedStraightEffectsStarted)
            {
                inputStatus.OnButtonReleased.Raise(InputEvents.FullSpeedStraightAction);
                fullSpeedStraightEffectsStarted = false;
            }
            if (minimumSpeedStraightEffectsStarted)
            {
                inputStatus.OnButtonReleased.Raise(InputEvents.MinimumSpeedStraightAction);
                minimumSpeedStraightEffectsStarted = false;
            }
        }

        protected override void ResetInput()
        {
            base.ResetInput();
            leftStick = Vector2.zero;
            rightStick = Vector2.zero;
            prevLeftLmb = prevLeftRmb = prevAnyMmb = false;
            prevRightLmb = prevRightRmb = false;
            fullSpeedStraightEffectsStarted = false;
            minimumSpeedStraightEffectsStarted = false;
        }
    }
}
