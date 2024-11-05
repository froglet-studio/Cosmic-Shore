using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using CosmicShore.Core;

namespace CosmicShore.Game.IO
{
    public class TouchInputStrategy : BaseInputStrategy
    {
        private float joystickRadius;
        private Vector2 leftJoystickValue, rightJoystickValue;
        private Vector2 leftJoystickStart, rightJoystickStart;
        private Vector2 leftClampedPosition, rightClampedPosition;
        private Vector2 leftNormalizedJoystickPosition, rightNormalizedJoystickPosition;
        private bool leftStickEffectsStarted, rightStickEffectsStarted;
        private int leftTouchIndex, rightTouchIndex;
        private bool fullSpeedStraightEffectsStarted;
        private bool minimumSpeedStraightEffectsStarted;

        public override void Initialize(Ship ship)
        {
            base.Initialize(ship);
            joystickRadius = Screen.dpi;
            leftJoystickValue = leftClampedPosition = new Vector2(joystickRadius, joystickRadius);
            rightJoystickValue = rightClampedPosition = new Vector2(Screen.currentResolution.width - joystickRadius, joystickRadius);
            EnhancedTouchSupport.Enable();
        }

        public override void ProcessInput(Ship ship)
        {
            var touchCount = Touch.activeTouches.Count;

            if (touchCount >= 3)
            {
                ProcessMultiTouch(true);
            }
            else if (touchCount == 2)
            {
                ProcessMultiTouch(false);
            }
            else if (touchCount == 1)
            {
                ProcessSingleTouch();
            }
            else
            {
                ResetInput();
                if (!idle)
                {
                    idle = true;
                    ship.PerformShipControllerActions(InputEvents.IdleAction);
                }
            }

            if (touchCount > 0)
            {
                Reparameterize();
                PerformSpeedAndDirectionalEffects();
                if (idle)
                {
                    idle = false;
                    ship.StopShipControllerActions(InputEvents.IdleAction);
                }
            }
        }

        private void ProcessMultiTouch(bool threeFingerFumble)
        {
            if (threeFingerFumble)
            {
                leftTouchIndex = GetClosestTouch(leftJoystickValue);
                rightTouchIndex = GetClosestTouch(rightJoystickValue);
            }
            else
            {
                if (Touch.activeTouches[0].screenPosition.x <= Touch.activeTouches[1].screenPosition.x)
                {
                    leftTouchIndex = 0;
                    rightTouchIndex = 1;
                }
                else
                {
                    leftTouchIndex = 1;
                    rightTouchIndex = 0;
                }
            }

            leftJoystickValue = Touch.activeTouches[leftTouchIndex].screenPosition;
            rightJoystickValue = Touch.activeTouches[rightTouchIndex].screenPosition;

            HandleJoystick(ref leftJoystickStart, leftTouchIndex, ref leftNormalizedJoystickPosition, ref leftClampedPosition);
            HandleJoystick(ref rightJoystickStart, rightTouchIndex, ref rightNormalizedJoystickPosition, ref rightClampedPosition);

            StopStickEffects();
        }

        private void ProcessSingleTouch()
        {
            var position = Touch.activeTouches[0].screenPosition;

            if (ship != null && ship.ShipStatus.CommandStickControls)
            {
                ProcessCommandStickControls(position);
            }

            if (Vector2.Distance(leftJoystickValue, position) < Vector2.Distance(rightJoystickValue, position))
            {
                HandleLeftStick(position);
            }
            else
            {
                HandleRightStick(position);
            }
        }

        private void ProcessCommandStickControls(Vector2 position)
        {
            SingleTouchValue = position;
            var tempThreeDPosition = new Vector3(
                (SingleTouchValue.x - Screen.width / 2) * 2f,
                (SingleTouchValue.y - Screen.height / 2) * 2f,
                0
            );

            if (tempThreeDPosition.sqrMagnitude < 10000 &&
                Touch.activeTouches[0].phase == UnityEngine.InputSystem.TouchPhase.Began)
            {
                ship.PerformShipControllerActions(InputEvents.NodeTapAction);
            }
            else if ((tempThreeDPosition - ship.transform.position).sqrMagnitude < 10000 &&
                     Touch.activeTouches[0].phase == UnityEngine.InputSystem.TouchPhase.Began)
            {
                ship.PerformShipControllerActions(InputEvents.SelfTapAction);
            }
            else
            {
                SingleTouchValue = tempThreeDPosition;
            }
        }

        private void HandleLeftStick(Vector2 position)
        {
            if (!leftStickEffectsStarted)
            {
                leftStickEffectsStarted = true;
                ship.PerformShipControllerActions(InputEvents.LeftStickAction);
            }
            leftJoystickValue = position;
            leftTouchIndex = 0;
            OneTouchLeft = true;
            HandleJoystick(ref leftJoystickStart, leftTouchIndex, ref leftNormalizedJoystickPosition, ref leftClampedPosition);
            rightNormalizedJoystickPosition = Vector3.Lerp(rightNormalizedJoystickPosition, Vector3.zero, 7 * Time.deltaTime);
        }

        private void HandleRightStick(Vector2 position)
        {
            if (!rightStickEffectsStarted)
            {
                rightStickEffectsStarted = true;
                ship.PerformShipControllerActions(InputEvents.RightStickAction);
            }
            rightJoystickValue = position;
            rightTouchIndex = 0;
            OneTouchLeft = false;
            HandleJoystick(ref rightJoystickStart, rightTouchIndex, ref rightNormalizedJoystickPosition, ref rightClampedPosition);
            leftNormalizedJoystickPosition = Vector3.Lerp(leftNormalizedJoystickPosition, Vector3.zero, 7 * Time.deltaTime);
        }

        private void HandleJoystick(ref Vector2 joystickStart, int touchIndex, ref Vector2 joystick, ref Vector2 clampedPosition)
        {
            Touch touch = Touch.activeTouches[touchIndex];

            if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began || joystickStart == Vector2.zero)
                joystickStart = touch.screenPosition;

            Vector2 offset = touch.screenPosition - joystickStart;
            Vector2 clampedOffset = Vector2.ClampMagnitude(offset, joystickRadius);
            clampedPosition = joystickStart + clampedOffset;
            Vector2 normalizedOffset = clampedOffset / joystickRadius;
            joystick = normalizedOffset;
        }

        private void StopStickEffects()
        {
            if (leftStickEffectsStarted)
            {
                leftStickEffectsStarted = false;
                ship.StopShipControllerActions(InputEvents.LeftStickAction);
            }
            if (rightStickEffectsStarted)
            {
                rightStickEffectsStarted = false;
                ship.StopShipControllerActions(InputEvents.RightStickAction);
            }
        }

        private void Reparameterize()
        {
            EasedRightJoystickPosition = new Vector2(
                Ease(2 * rightNormalizedJoystickPosition.x),
                Ease(2 * rightNormalizedJoystickPosition.y)
            );
            EasedLeftJoystickPosition = new Vector2(
                Ease(2 * leftNormalizedJoystickPosition.x),
                Ease(2 * leftNormalizedJoystickPosition.y)
            );

            XSum = Ease(rightNormalizedJoystickPosition.x + leftNormalizedJoystickPosition.x);
            YSum = -Ease(rightNormalizedJoystickPosition.y + leftNormalizedJoystickPosition.y);
            XDiff = (rightNormalizedJoystickPosition.x - leftNormalizedJoystickPosition.x + 2) / 4;
            YDiff = Ease(rightNormalizedJoystickPosition.y - leftNormalizedJoystickPosition.y);
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
            else if (DeviationFromMinimumSpeedStraight < threshold && !minimumSpeedStraightEffectsStarted)
            {
                minimumSpeedStraightEffectsStarted = true;
                ship.PerformShipControllerActions(InputEvents.MinimumSpeedStraightAction);
            }
            else
            {
                if (fullSpeedStraightEffectsStarted && DeviationFromFullSpeedStraight > threshold)
                {
                    fullSpeedStraightEffectsStarted = false;
                    ship.StopShipControllerActions(InputEvents.FullSpeedStraightAction);
                }
                if (minimumSpeedStraightEffectsStarted && DeviationFromMinimumSpeedStraight > threshold)
                {
                    minimumSpeedStraightEffectsStarted = false;
                    ship.StopShipControllerActions(InputEvents.MinimumSpeedStraightAction);
                }
            }
        }

        private int GetClosestTouch(Vector2 target)
        {
            int touchIndex = 0;
            float minDistance = float.MaxValue;

            for (int i = 0; i < Touch.activeTouches.Count; i++)
            {
                float distance = Vector2.Distance(target, Touch.activeTouches[i].screenPosition);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    touchIndex = i;
                }
            }
            return touchIndex;
        }
    }
}