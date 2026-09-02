using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using CosmicShore.Gameplay;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    public class TouchInputStrategy : BaseInputStrategy
    {
        // ── Touch feel tuning ────────────────────────────────────────────────
        // Informed by touch-vs-thumbstick input research: response curves exist on physical
        // sticks to compensate for SPRING TENSION, and the raised edge lets players feel full
        // deflection. Glass has neither — long travel + heavy curves read as pure lag, and with
        // no felt edge players routinely sit at partial deflection believing they're at full
        // tilt. So: short travel, near-linear response, and an explicit small dead zone for
        // finger tremor (the job curves were doing on sticks).

        /// <summary>Full-deflection thumb travel, in inches (was 1.0 — a whole inch of drag to
        /// reach 100%). 0.6" keeps full deflection inside a comfortable thumb arc.</summary>
        const float JoystickRadiusInches = 0.6f;

        /// <summary>Tremor filter around the touch origin, in pixels (~10-15px is standard on
        /// phones). Output rescales from the dead zone's EDGE to the rim (scaled radial dead
        /// zone), so there is no output cliff when leaving it.</summary>
        const float DeadZonePixels = 12f;

        /// <summary>Fallback when Screen.dpi reports 0 (some Android devices).</summary>
        const float FallbackDpi = 160f;

        private float joystickRadius;
        private Vector2 leftJoystickValue, rightJoystickValue;
        private Vector2 leftJoystickStart, rightJoystickStart;
        private Vector2 leftClampedPosition, rightClampedPosition;
        private Vector2 leftNormalizedJoystickPosition, rightNormalizedJoystickPosition;

        // ── One-thumb flight ────────────────────────────────────────────────
        // A two-stick hull flown with a SINGLE thumb - which is exactly the state the vessel
        // enters the moment a thumb is LIFTED to trigger an ability (drift is a 2+ -> 1 touch
        // transition, see HandleDriftTransitions). While it lasts, the live thumb is mirrored
        // onto BOTH virtual sticks in Reparameterize.
        private bool oneThumbActive;
        private Vector2 oneThumbStick;

        // Extra gain on the mirrored thumb. 1 = none, because the mirror ALREADY speeds the
        // turn up sharply on its own: the mix eases (right + left), so one stick alone feeds
        // Ease(s) - about 0.29 of full authority at max deflection - while a mirrored thumb
        // feeds Ease(2s), the whole curve. That is the "faster turning" this mode wants; raise
        // this only if it still reads sluggish, and expect it to get twitchy fast.
        private const float OneThumbTurnBoost = 1f;
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
            float dpi = Screen.dpi > 0f ? Screen.dpi : FallbackDpi;
            joystickRadius = dpi * JoystickRadiusInches;
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
        /// Touch-tuned easing: 90% linear with a whisper of cubic. The gamepad cosine curve
        /// crushes mid-range to ~15% output - that compensates for stick resistance but feels
        /// sluggish on glass where there is no friction. Center noise is handled by the explicit
        /// dead zone in <see cref="HandleJoystick"/>, so the curve no longer needs to do that job.
        ///
        /// Input [-2, 2] → Output [-1, 1] (same domain/range as gamepad Ease).
        /// </summary>
        protected override float Ease(float input)
        {
            float t = Mathf.Clamp(input * 0.5f, -1f, 1f);
            float cubic = t * t * t;
            return cubic * 0.1f + t * 0.9f;
        }

        public override void ProcessInput()
        {
            var touchCount = Touch.activeTouches.Count;

            // Detect transitions that start or stop drift
            HandleDriftTransitions(touchCount);

            if (touchCount >= 3)
            {
                oneThumbActive = false;
                ProcessMultiTouch(true);
            }
            else if (touchCount == 2)
            {
                oneThumbActive = false;
                ProcessMultiTouch(false);
            }
            else if (touchCount == 1)
            {
                ProcessSingleTouch();
            }
            else
            {
                oneThumbActive = false;
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

            // Edge case: 2+ → 0 (both lifted same frame) - no drift, just idle
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

            bool useLeft = (leftJoystickValue - position).sqrMagnitude
                           < (rightJoystickValue - position).sqrMagnitude;
            if (useLeft)
            {
                HandleLeftStick(position);
            }
            else
            {
                HandleRightStick(position);
            }

            // Capture the thumb that is actually down. The OTHER stick is being lerped toward
            // zero by the handler above, so it must not be read as a real input - that decaying
            // value is what used to leak into throttle and roll (see Reparameterize).
            oneThumbActive = true;
            oneThumbStick = useLeft ? leftNormalizedJoystickPosition : rightNormalizedJoystickPosition;
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

            // Scaled radial dead zone: inside DeadZonePixels output is zero (tremor filter);
            // outside, output rescales from the dead zone's edge to the rim so leaving the zone
            // ramps smoothly from 0 instead of jumping.
            float magnitude = clampedOffset.magnitude;
            if (magnitude <= DeadZonePixels)
            {
                joystick = Vector2.zero;
                return;
            }
            joystick = (clampedOffset / magnitude)
                       * ((magnitude - DeadZonePixels) / (joystickRadius - DeadZonePixels));
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
            var left = leftNormalizedJoystickPosition;
            var right = rightNormalizedJoystickPosition;

            // ONE-THUMB FLIGHT. Mirror the live thumb onto both sticks. The mix is
            // XSum = yaw, YSum = pitch, XDiff = throttle, YDiff = roll over (right +/- left), so
            // mirroring is not a special case bolted on - it falls out of the existing mix as
            // exactly the mode we want:
            //   XDiff = (s.x - s.x + 2)/4 = 0.5  -> throttle pinned neutral
            //   YDiff = Ease(s.y - s.y)   = 0    -> no roll
            //   XSum/YSum = Ease(2s)             -> pitch + yaw at FULL authority
            // Flying one-thumbed previously did the opposite of all three: the idle stick decays
            // toward zero, so XDiff drifted with sideways thumb travel (a turn silently changed
            // SPEED), YDiff picked up roll from vertical travel, and pitch/yaw ran at Ease(s) -
            // roughly 0.29 of full authority. Hence "faster turning, pitch and yaw only".
            if (oneThumbActive)
            {
                left = oneThumbStick * OneThumbTurnBoost;
                right = left;
            }

            inputStatus.EasedRightJoystickPosition = new Vector2(Ease(2 * right.x), Ease(2 * right.y));
            inputStatus.EasedLeftJoystickPosition = new Vector2(Ease(2 * left.x), Ease(2 * left.y));

            inputStatus.RightNormalizedJoystickPosition = right;
            inputStatus.LeftNormalizedJoystickPosition = left;

            inputStatus.XSum = Ease(right.x + left.x);
            inputStatus.YSum = -Ease(right.y + left.y);
            inputStatus.XDiff = (right.x - left.x + 2) / 4;
            inputStatus.YDiff = Ease(right.y - left.y);
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
            float minSqrDistance = float.MaxValue;

            for (int i = 0; i < Touch.activeTouches.Count; i++)
            {
                // argmin over distance == argmin over squared distance - no sqrt needed.
                float sqrDistance = (target - Touch.activeTouches[i].screenPosition).sqrMagnitude;
                if (sqrDistance < minSqrDistance)
                {
                    minSqrDistance = sqrDistance;
                    touchIndex = i;
                }
            }
            return touchIndex;
        }
    }
}
