using UnityEngine;
using UnityEngine.InputSystem;


namespace CosmicShore.Game.IO
{
    public class GamepadInputStrategy : BaseInputStrategy
    {
        private bool leftStickEffectsStarted;
        private bool rightStickEffectsStarted;
        private bool fullSpeedStraightEffectsStarted;
        private bool minimumSpeedStraightEffectsStarted;

        private Vector2 leftStickRaw;
        private Vector2 rightStickRaw;

        public override void Initialize(IInputStatus inputStatus)
        {
            base.Initialize(inputStatus);
            ResetInput();
        }

        public override void ProcessInput()
        {
            if (Gamepad.current == null) return;

            ProcessStickInput();
            ProcessButtonInput();
            Reparameterize();
            PerformSpeedAndDirectionalEffects();
        }

        private void ProcessStickInput()
        {
            inputStatus.Throttle = Gamepad.current.rightShoulder.ReadValue();
            leftStickRaw = Gamepad.current.leftStick.ReadValue();
            rightStickRaw = Gamepad.current.rightStick.ReadValue();
        }

        private void ProcessButtonInput()
        {
            // Primary action buttons
            if (Gamepad.current.buttonSouth.wasPressedThisFrame)
                inputStatus.OnButtonPressed.Raise(InputEvents.Button1Action);
                //vessel.PerformShipControllerActions(InputEvents.Button1Action);
            if (Gamepad.current.buttonSouth.wasReleasedThisFrame)
                inputStatus.OnButtonReleased.Raise(InputEvents.Button1Action);
                // vessel.StopShipControllerActions(InputEvents.Button1Action);

            if (Gamepad.current.buttonEast.wasPressedThisFrame)
                inputStatus.OnButtonPressed.Raise(InputEvents.Button2Action);
                // vessel.PerformShipControllerActions(InputEvents.Button2Action);
            if (Gamepad.current.buttonEast.wasReleasedThisFrame)
                inputStatus.OnButtonReleased.Raise(InputEvents.Button2Action);
                // vessel.StopShipControllerActions(InputEvents.Button2Action);

            if (Gamepad.current.buttonWest.wasPressedThisFrame)
                inputStatus.OnButtonPressed.Raise(InputEvents.Button3Action);
                // vessel.PerformShipControllerActions(InputEvents.Button3Action);
            if (Gamepad.current.buttonWest.wasReleasedThisFrame)
                inputStatus.OnButtonReleased.Raise(InputEvents.Button3Action);
                // vessel.StopShipControllerActions(InputEvents.Button3Action);

            // Shoulder buttons and triggers
            if (Gamepad.current.leftShoulder.wasPressedThisFrame)
            {
                //inputStatus.Idle = true;
                //inputStatus.OnButtonPressed.Raise(InputEvents.IdleAction);;
                // vessel.PerformShipControllerActions(InputEvents.IdleAction);
            }
            if (Gamepad.current.leftShoulder.wasReleasedThisFrame)
            {
                //inputStatus.Idle = false;
                //inputStatus.OnButtonReleased.Raise(InputEvents.IdleAction);;
                // vessel.StopShipControllerActions(InputEvents.IdleAction);
            }

            // Right shoulder for flip action
            if (Gamepad.current.rightShoulder.wasPressedThisFrame)
                inputStatus.OnButtonPressed.Raise(InputEvents.FlipAction);
                // vessel.PerformShipControllerActions(InputEvents.FlipAction);
            if (Gamepad.current.rightShoulder.wasReleasedThisFrame)
                inputStatus.OnButtonReleased.Raise(InputEvents.FlipAction);
            // vessel.StopShipControllerActions(InputEvents.FlipAction);

            // Triggers for stick actions
            var leftTrigger = Gamepad.current.leftTrigger;
            var rightTrigger = Gamepad.current.rightTrigger;

            bool leftJustPressed = leftTrigger.wasPressedThisFrame;
            bool leftJustReleased = leftTrigger.wasReleasedThisFrame;
            bool leftHeld = leftTrigger.isPressed;

            bool rightJustPressed = rightTrigger.wasPressedThisFrame;
            bool rightJustReleased = rightTrigger.wasReleasedThisFrame;
            bool rightHeld = rightTrigger.isPressed;

            // Individual trigger events
            if (leftJustPressed)
                inputStatus.OnButtonPressed.Raise(InputEvents.LeftStickAction);
            if (leftJustReleased)
                inputStatus.OnButtonReleased.Raise(InputEvents.LeftStickAction);
            if (rightJustPressed)
                inputStatus.OnButtonPressed.Raise(InputEvents.RightStickAction);
            if (rightJustReleased)
                inputStatus.OnButtonReleased.Raise(InputEvents.RightStickAction);

            // BothSticksAction Released
            if ((leftJustReleased && rightJustReleased)
                || (leftJustReleased && rightHeld)
                || (rightJustReleased && leftHeld))
                inputStatus.OnButtonReleased.Raise(InputEvents.BothSticksAction);

            // OnlyLeftStickAction Released
            if ((leftJustReleased && !rightHeld)
                || (rightJustPressed && leftHeld))
                inputStatus.OnButtonReleased.Raise(InputEvents.OnlyLeftStickAction);

            // OnlyRightStickAction Released
            if ((rightJustReleased && !leftHeld)
                || (leftJustPressed && rightHeld))
                inputStatus.OnButtonReleased.Raise(InputEvents.OnlyRightStickAction);

            // OnlyLeftStickAction Pressed
            if ((leftJustPressed && !rightHeld)
                || (rightJustReleased && leftHeld))
                inputStatus.OnButtonPressed.Raise(InputEvents.OnlyLeftStickAction);

            // OnlyRightStickAction Pressed
            if ((rightJustPressed && !leftHeld)
                || (leftJustReleased && rightHeld))
                inputStatus.OnButtonPressed.Raise(InputEvents.OnlyRightStickAction);

            // BothSticksAction Pressed
            if ((leftJustPressed && rightJustPressed)
                || (leftJustPressed && rightHeld)
                || (rightJustPressed && leftHeld))
                inputStatus.OnButtonPressed.Raise(InputEvents.BothSticksAction);



            // vessel.StopShipControllerActions(InputEvents.OnlyLeftStickAction);


            // vessel.PerformShipControllerActions(InputEvents.LeftStickAction);
        }

        private void Reparameterize()
        {
            // Calculate eased joystick positions
            inputStatus.EasedLeftJoystickPosition = new Vector2(
                Ease(2 * leftStickRaw.x),
                Ease(2 * leftStickRaw.y)
            );
            inputStatus.EasedRightJoystickPosition = new Vector2(
                Ease(2 * rightStickRaw.x),
                Ease(2 * rightStickRaw.y)
            );

            // Calculate sums and differences exactly as touch input does
            inputStatus.XSum = Ease(rightStickRaw.x + leftStickRaw.x);
            inputStatus.YSum = -Ease(rightStickRaw.y + leftStickRaw.y);
            inputStatus.XDiff = (rightStickRaw.x - leftStickRaw.x + 2) / 4;
            inputStatus.YDiff = Ease(rightStickRaw.y - leftStickRaw.y);
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
            else if (DeviationFromMinimumSpeedStraight < threshold && !minimumSpeedStraightEffectsStarted)
            {
                minimumSpeedStraightEffectsStarted = true;
                inputStatus.OnButtonPressed.Raise(InputEvents.MinimumSpeedStraightAction);
                // vessel.PerformShipControllerActions(InputEvents.MinimumSpeedStraightAction);
            }
            else
            {
                if (fullSpeedStraightEffectsStarted && DeviationFromFullSpeedStraight > threshold)
                {
                    fullSpeedStraightEffectsStarted = false;
                    inputStatus.OnButtonReleased.Raise(InputEvents.FullSpeedStraightAction);
                    // vessel.StopShipControllerActions(InputEvents.FullSpeedStraightAction);
                }
                if (minimumSpeedStraightEffectsStarted && DeviationFromMinimumSpeedStraight > threshold)
                {
                    minimumSpeedStraightEffectsStarted = false;
                    inputStatus.OnButtonReleased.Raise(InputEvents.MinimumSpeedStraightAction);
                    // vessel.StopShipControllerActions(InputEvents.MinimumSpeedStraightAction);
                }
            }
        }

        public override void SetPortrait(bool portrait)
        {
            // Gamepad doesn't need to handle portrait mode changes
        }
    }
}
