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

        // Drift state: tracks finger-lift transitions for OnlyLeft/OnlyRight events
        private int prevTouchCount;
        private bool onlyLeftActive;
        private bool onlyRightActive;
        private bool isDrifting;

        public override void Initialize(IInputStatus inputStatus)
        {
            base.Initialize(inputStatus);
            joystickRadius = Screen.dpi;
            leftJoystickValue = leftClampedPosition = new Vector2(joystickRadius, joystickRadius);
            rightJoystickValue = rightClampedPosition = new Vector2(Screen.currentResolution.width - joystickRadius, joystickRadius);
            EnhancedTouchSupport.Enable();
        }

        public override void OnStrategyActivated()
        {
            base.OnStrategyActivated();
            inputStatus.ActiveInputDevice = InputDeviceType.Touch;
        }

        /// <summary>
        /// Touch-tuned easing: mostly linear with a subtle cubic dead zone.
        /// The gamepad cosine curve crushes mid-range to ~15% output — that
        /// compensates for stick resistance but feels sluggish on glass where
        /// there is no friction. This blend keeps a gentle noise filter near
        /// center while staying responsive through mid-range.
        ///
        /// Input [-2, 2] → Output [-1, 1] (same domain/range as gamepad Ease).
        /// </summary>
        protected override float Ease(float input)
        {
            float t = Mathf.Clamp(input * 0.5f, -1f, 1f);
            float cubic = t * t * t;
            return cubic * 0.25f + t * 0.75f;
        }

        public override void ProcessInput()
        {
            var touchCount = Touch.activeTouches.Count;

            // Detect transitions that start or stop drift
            HandleDriftTransitions(touchCount);

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
                if (!inputStatus.Idle)
                {
                    inputStatus.Idle = true;
                    inputStatus.OnButtonPressed.Raise(InputEvents.IdleAction);
                }
            }

            if (touchCount > 0)
            {
                Reparameterize();

                // Maintain full throttle while drifting with one thumb
                if (isDrifting)
                    inputStatus.XDiff = 1.0f;

                PerformSpeedAndDirectionalEffects();
                if (inputStatus.Idle)
                {
                    inputStatus.Idle = false;
                    inputStatus.OnButtonReleased.Raise(InputEvents.IdleAction);
                }
            }

            prevTouchCount = touchCount;
        }

        /// <summary>
        /// Detects touch-count transitions that map to drift actions.
        /// 2+ → 1: a finger was lifted → start drift (single or double based on which thumb lifted)
        /// 1 → 2+ or 1 → 0: drift ends
        /// </summary>
        private void HandleDriftTransitions(int touchCount)
        {
            // 2+ → 1: finger lifted, start drifting
            if (prevTouchCount >= 2 && touchCount == 1)
            {
                var remainingPosition = Touch.activeTouches[0].screenPosition;
                bool remainingIsLeft = remainingPosition.x < Screen.width * 0.5f;

                if (remainingIsLeft)
                {
                    // Right thumb was lifted, only left remains → OnlyLeftStickAction
                    onlyLeftActive = true;
                    isDrifting = true;
                    inputStatus.OnButtonPressed.Raise(InputEvents.OnlyLeftStickAction);
                }
                else
                {
                    // Left thumb was lifted, only right remains → OnlyRightStickAction
                    onlyRightActive = true;
                    isDrifting = true;
                    inputStatus.OnButtonPressed.Raise(InputEvents.OnlyRightStickAction);
                }
            }

            // Drift ends: 1 → 2+ (finger put back down) or 1 → 0 (remaining finger lifted)
            if ((prevTouchCount == 1 && touchCount >= 2) ||
                (prevTouchCount == 1 && touchCount == 0))
            {
                StopDrift();
            }

            // Edge case: 2+ → 0 (both lifted same frame) — no drift, just idle
            if (prevTouchCount >= 2 && touchCount == 0)
            {
                StopDrift();
            }
        }

        private void StopDrift()
        {
            if (onlyLeftActive)
            {
                onlyLeftActive = false;
                inputStatus.OnButtonReleased.Raise(InputEvents.OnlyLeftStickAction);
            }
            if (onlyRightActive)
            {
                onlyRightActive = false;
                inputStatus.OnButtonReleased.Raise(InputEvents.OnlyRightStickAction);
            }
            isDrifting = false;
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

            if (inputStatus.CommandStickControls)
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
            inputStatus.SingleTouchValue = position;
            var tempThreeDPosition = new Vector3(
                (inputStatus.SingleTouchValue.x - Screen.width / 2) * 2f,
                (inputStatus.SingleTouchValue.y - Screen.height / 2) * 2f,
                0
            );

            if (tempThreeDPosition.sqrMagnitude < 10000 &&
                Touch.activeTouches[0].phase == UnityEngine.InputSystem.TouchPhase.Began)
            {
                inputStatus.OnButtonPressed.Raise(InputEvents.NodeTapAction);
            }
        }

        private void HandleLeftStick(Vector2 position)
        {
            if (!leftStickEffectsStarted)
            {
                leftStickEffectsStarted = true;
                inputStatus.OnButtonPressed.Raise(InputEvents.LeftStickAction);
            }
            leftJoystickValue = position;
            leftTouchIndex = 0;
            inputStatus.OneTouchLeft = true;
            HandleJoystick(ref leftJoystickStart, leftTouchIndex, ref leftNormalizedJoystickPosition, ref leftClampedPosition);
            rightNormalizedJoystickPosition = Vector3.Lerp(rightNormalizedJoystickPosition, Vector3.zero, 7 * Time.deltaTime);
        }

        private void HandleRightStick(Vector2 position)
        {
            if (!rightStickEffectsStarted)
            {
                rightStickEffectsStarted = true;
                inputStatus.OnButtonPressed.Raise(InputEvents.RightStickAction);
            }
            rightJoystickValue = position;
            rightTouchIndex = 0;
            inputStatus.OneTouchLeft = false;
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
                inputStatus.OnButtonReleased.Raise(InputEvents.LeftStickAction);
            }
            if (rightStickEffectsStarted)
            {
                rightStickEffectsStarted = false;
                inputStatus.OnButtonReleased.Raise(InputEvents.RightStickAction);
            }
        }

        private void Reparameterize()
        {
            inputStatus.EasedRightJoystickPosition = new Vector2(
                Ease(2 * rightNormalizedJoystickPosition.x),
                Ease(2 * rightNormalizedJoystickPosition.y)
            );
            inputStatus.EasedLeftJoystickPosition = new Vector2(
                Ease(2 * leftNormalizedJoystickPosition.x),
                Ease(2 * leftNormalizedJoystickPosition.y)
            );

            inputStatus.XSum = Ease(rightNormalizedJoystickPosition.x + leftNormalizedJoystickPosition.x);
            inputStatus.YSum = -Ease(rightNormalizedJoystickPosition.y + leftNormalizedJoystickPosition.y);
            inputStatus.XDiff = (rightNormalizedJoystickPosition.x - leftNormalizedJoystickPosition.x + 2) / 4;
            inputStatus.YDiff = Ease(rightNormalizedJoystickPosition.y - leftNormalizedJoystickPosition.y);
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
