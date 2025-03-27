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

        public override void Initialize(IShip ship)
        {
            base.Initialize(ship);
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
            InputStatus.Throttle = Gamepad.current.rightShoulder.ReadValue();
            leftStickRaw = Gamepad.current.leftStick.ReadValue();
            rightStickRaw = Gamepad.current.rightStick.ReadValue();
        }

        private void ProcessButtonInput()
        {
            // Primary action buttons
            if (Gamepad.current.buttonSouth.wasPressedThisFrame)
                _ship.PerformShipControllerActions(InputEvents.Button1Action);
            if (Gamepad.current.buttonSouth.wasReleasedThisFrame)
                _ship.StopShipControllerActions(InputEvents.Button1Action);

            if (Gamepad.current.buttonEast.wasPressedThisFrame)
                _ship.PerformShipControllerActions(InputEvents.Button2Action);
            if (Gamepad.current.buttonEast.wasReleasedThisFrame)
                _ship.StopShipControllerActions(InputEvents.Button2Action);

            if (Gamepad.current.buttonWest.wasPressedThisFrame)
                _ship.PerformShipControllerActions(InputEvents.Button3Action);
            if (Gamepad.current.buttonWest.wasReleasedThisFrame)
                _ship.StopShipControllerActions(InputEvents.Button3Action);

            // Shoulder buttons and triggers
            if (Gamepad.current.leftShoulder.wasPressedThisFrame)
            {
                InputStatus.Idle = true;
                _ship.PerformShipControllerActions(InputEvents.IdleAction);
            }
            if (Gamepad.current.leftShoulder.wasReleasedThisFrame)
            {
                InputStatus.Idle = false;
                _ship.StopShipControllerActions(InputEvents.IdleAction);
            }

            // Right shoulder for flip action
            if (Gamepad.current.rightShoulder.wasPressedThisFrame)
                _ship.PerformShipControllerActions(InputEvents.FlipAction);
            if (Gamepad.current.rightShoulder.wasReleasedThisFrame)
                _ship.StopShipControllerActions(InputEvents.FlipAction);

            // Triggers for stick actions
            if (Gamepad.current.leftTrigger.wasPressedThisFrame)
                _ship.PerformShipControllerActions(InputEvents.LeftStickAction);
            if (Gamepad.current.leftTrigger.wasReleasedThisFrame)
                _ship.StopShipControllerActions(InputEvents.LeftStickAction);

            if (Gamepad.current.rightTrigger.wasPressedThisFrame)
                _ship.PerformShipControllerActions(InputEvents.RightStickAction);
            if (Gamepad.current.rightTrigger.wasReleasedThisFrame)
                _ship.StopShipControllerActions(InputEvents.RightStickAction);
        }

        private void Reparameterize()
        {
            // Calculate eased joystick positions
            InputStatus.EasedLeftJoystickPosition = new Vector2(
                Ease(2 * leftStickRaw.x),
                Ease(2 * leftStickRaw.y)
            );
            InputStatus.EasedRightJoystickPosition = new Vector2(
                Ease(2 * rightStickRaw.x),
                Ease(2 * rightStickRaw.y)
            );

            // Calculate sums and differences exactly as touch input does
            InputStatus.XSum = Ease(rightStickRaw.x + leftStickRaw.x);
            InputStatus.YSum = -Ease(rightStickRaw.y + leftStickRaw.y);
            InputStatus.XDiff = (rightStickRaw.x - leftStickRaw.x + 2) / 4;
            InputStatus.YDiff = Ease(rightStickRaw.y - leftStickRaw.y);
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
            else if (DeviationFromMinimumSpeedStraight < threshold && !minimumSpeedStraightEffectsStarted)
            {
                minimumSpeedStraightEffectsStarted = true;
                _ship.PerformShipControllerActions(InputEvents.MinimumSpeedStraightAction);
            }
            else
            {
                if (fullSpeedStraightEffectsStarted && DeviationFromFullSpeedStraight > threshold)
                {
                    fullSpeedStraightEffectsStarted = false;
                    _ship.StopShipControllerActions(InputEvents.FullSpeedStraightAction);
                }
                if (minimumSpeedStraightEffectsStarted && DeviationFromMinimumSpeedStraight > threshold)
                {
                    minimumSpeedStraightEffectsStarted = false;
                    _ship.StopShipControllerActions(InputEvents.MinimumSpeedStraightAction);
                }
            }
        }

        public override void SetPortrait(bool portrait)
        {
            // Gamepad doesn't need to handle portrait mode changes
        }
    }
}
