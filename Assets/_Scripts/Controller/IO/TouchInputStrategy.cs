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

        /// <summary>Centre zone around the touch origin, in inches - 8% of the stick's travel at
        /// any screen density. Bleeding-edge had no zone at all, and the Android strip's first
        /// one was a flat 12 px - 0.075" on a 160 dpi screen and 0.03" (under a millimetre) on the
        /// 400+ dpi phones the game actually runs on: a centre nobody could find by feel, so a
        /// thumb walked back to "straight" kept a small turn alive and the pilot corrected it
        /// into an overcorrection. Output rescales from the
        /// zone's EDGE to the rim (scaled radial dead zone), so there is no cliff leaving it.</summary>
        const float DeadZoneInches = 0.05f;

        /// <summary>Floor for <see cref="DeadZoneInches"/> on a low-density or dpi-less screen -
        /// the old flat value, so no device gets a smaller centre than it had.</summary>
        const float DeadZoneMinPixels = 12f;

        /// <summary>Fallback when Screen.dpi reports 0 (some Android devices).</summary>
        const float FallbackDpi = 160f;

        private float joystickRadius;
        private float deadZone;
        private Vector2 leftJoystickValue, rightJoystickValue;
        private Vector2 leftJoystickStart, rightJoystickStart;
        private Vector2 leftClampedPosition, rightClampedPosition;
        private Vector2 leftNormalizedJoystickPosition, rightNormalizedJoystickPosition;

        // ── One-thumb flight ────────────────────────────────────────────────
        // A two-stick hull flown with a SINGLE thumb - which is exactly the state the vessel
        // enters the moment a thumb is LIFTED to trigger an ability (a lift is a 2+ -> 1 touch
        // transition, see HandleDriftTransitions: the Squirrel's boost ring, the Butterfly's mode
        // switch and Fold). While it lasts, the live thumb is mirrored onto BOTH virtual sticks
        // in Reparameterize. The Squirrel's drift is one of these lifts (the RIGHT thumb), and a
        // lift is a full trigger pull (see the section below).
        private bool oneThumbActive;
        private Vector2 oneThumbStick;

        // FULL authority, in a drift too. The mirror feeds the mix Ease(2s): one thumb at the rim
        // commands exactly what two full sticks command on a pad - the two-thumb ceiling, never
        // more. A drift then turns SHARPER the way it does on a pad: the drift action multiplies
        // the vessel's rotation scalers (VesselTransformer.ApplyAnalogDrift, Mult), and a lift is
        // a full trigger pull. The Android strip branch once cut the mirror to 0.70 while an
        // ability was lifted, to stop a full-lock drift scrubbing speed; that was a touch-only
        // steering cut a pad pilot never had, and on a pad the same full lock at full trigger
        // scrubs the same 7%. Parity is the rule now - Tools/Build/touch_drift_slip.py --check
        // holds it.

        /// <summary>
        /// True while the single live thumb is the result of LIFTING one to fire an ability,
        /// rather than plain one-finger flight. It replaces a write-only "isDrifting" flag that
        /// was set on BOTH single-thumb transitions and so never meant "a drift is running" -
        /// only "one touch remains". On the Squirrel the two really do differ: a lifted RIGHT
        /// thumb raises OnlyLeftStickAction (InputEvents 12), which that vessel binds to the
        /// drift, while a lifted LEFT thumb raises OnlyRightStickAction (11), bound to the tube -
        /// so the old flag pinned the throttle for an ability that is not a drift at all.
        /// Whichever ability it is, THIS is the state the throttle hold is about: the pilot's
        /// speed is kept while one thumb is up, as a pad keeps it while a trigger is held.
        /// </summary>
        private bool OneThumbAbilityActive => oneThumbActive && (onlyLeftActive || onlyRightActive);

        /// <summary>
        /// Throttle carried into the one-thumb ability state. Mirroring makes XDiff structurally
        /// 0.5 - <c>(s.x - s.x + 2)/4</c> - so without this a pilot's throttle silently halves
        /// the instant they lift a thumb. Replaying the value they actually had is also why this
        /// is not simply pinned to 1: an unasked-for full-throttle lurch on drift entry is its
        /// own kind of "doesn't feel right".
        /// </summary>
        private float heldXDiff = 0.5f;

        // ── No pull on a thumb lift or a thumb replace ───────────────────────
        // Throttle on this mix is the horizontal SPREAD of the thumbs (XDiff), so a pilot at
        // cruise holds both thumbs pushed outward. Lift one and the other is still deflected
        // outward - which the one-thumb mirror read as a hard yaw toward that side: the vessel was
        // yanked off its line exactly when the pilot lifted a thumb to fire an ability. The same
        // happened in reverse on the way back down. So every change between one and two thumbs
        // RE-ZEROES the sticks where the thumbs are: the vessel keeps flying straight through the
        // transition, and steering resumes from wherever the thumbs now rest. Lift, fire the
        // boost ring, put the thumb back - and you are still on the line the ring was laid on.

        /// <summary>Set on a 1&lt;-&gt;2 thumb transition; consumed by the next stick sample.</summary>
        private bool rebaseSticks;

        /// <summary>
        /// Throttle carried back into two-thumb flight after an ability lift. Re-zeroing the
        /// sticks makes neutral thumbs read as HALF throttle, so a pilot who lifted a thumb at
        /// full speed would have slowed to half the moment it came back down. With the carry,
        /// neutral thumbs after a re-place mean the speed you had; spreading or squeezing still
        /// reaches full and zero, because the carry fades out as the live spread moves toward
        /// either end (see <see cref="CarryThrottle"/>). Cleared when both thumbs lift.
        /// </summary>
        private float throttleCarry;

        // ── A thumb lift is a FULL trigger pull ─────────────────────────────────────────────
        // Glass cannot measure trigger travel, so a lift that fires a drift is the binary
        // fallback every non-analog input gets (VesselTransformer.GetTriggerSum): the drift is on
        // at full pull for as long as the thumb is up, and the remaining thumb flies the vessel
        // alone - mirrored onto both sticks, pitch and yaw only (see Reparameterize). Deriving a
        // depth from the steering thumb's deflection was tried and retired: it made the slide
        // change under the pilot as they steered. Nothing here writes the trigger
        // channel: zero travel IS how the vessel knows to take the binary path.

        /// <summary>
        /// True while the single live thumb is alone because the other was LIFTED (a 2 -&gt; 1
        /// transition), false when it is alone because it was the FIRST thumb down. Only the
        /// former raises Left/RightStickAction: those are the Butterfly's Fold and mode switch,
        /// and they used to fire on a first touch too - so putting the right thumb down first
        /// toggled the Butterfly's mode, and left-thumb-first started a Fold that teleported the
        /// vessel the moment the second thumb landed. Which thumb a pilot happens to touch with
        /// first is not a decision; lifting one is.
        /// </summary>
        private bool singleThumbFromLift;

        private bool leftStickEffectsStarted, rightStickEffectsStarted;
        private int leftTouchIndex, rightTouchIndex;
        private bool fullSpeedStraightEffectsStarted;
        private bool minimumSpeedStraightEffectsStarted;

        // Drift state: tracks finger-lift transitions for OnlyLeft/OnlyRight events
        private int prevTouchCount;
        private bool onlyLeftActive;
        private bool onlyRightActive;

        public override void Initialize(IInputStatus inputStatus)
        {
            base.Initialize(inputStatus);
            float dpi = Screen.dpi > 0f ? Screen.dpi : FallbackDpi;
            joystickRadius = dpi * JoystickRadiusInches;
            // Never more than half the travel: a bogus near-zero Screen.dpi would otherwise put the
            // 12 px floor past the rim, and the rescale below would divide by zero or less.
            deadZone = Mathf.Min(Mathf.Max(DeadZoneMinPixels, dpi * DeadZoneInches), joystickRadius * 0.5f);
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
        /// Touch-tuned easing: 75% linear + 25% cubic - between the near-linear 90/10 the Android
        /// strip branch once ran and the gamepad's cosine (<see cref="BaseInputStrategy"/>),
        /// which is flat at the centre. Output at quarter / half / three-quarter deflection: 0.191 / 0.406 / 0.668,
        /// against the pad's 0.076 / 0.293 / 0.617 and the strip's 90/10 0.227 / 0.463 / 0.717. Full
        /// deflection is 1 on every curve, so the turn-rate ceiling (and the drift) is unchanged.
        ///
        /// The 90/10 pass answered "touch feels
        /// less responsive than a pad" by steepening the curve, but the unresponsiveness was the
        /// hull's 0.67 s nose lag (VesselTransformer.touchNoseResponse fixes it), and a steeper
        /// curve on top of a lagging nose is a bigger turn to overshoot. With the lag gone, the
        /// softer centre is precision rather than sluggishness. Tremor is still the dead zone's job.
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
                throttleCarry = 0f;
                // No thumb on the glass is a let-go of everything. The drift already ends on
                // 1 -> 0 (HandleDriftTransitions); the stick abilities a thumb LIFT started and
                // the straight-line speed gestures used to stay held until two thumbs landed again
                // (StopStickEffects ran only from ProcessMultiTouch), so a Dolphin kept drifting
                // and charging, or a Sparrow kept firing, with the pilot's hands off the screen.
                StopStickEffects();
                ReleaseSpeedEffects();
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

                // Hold the throttle the pilot had when they lifted the thumb (see heldXDiff), and
                // carry it back into two-thumb flight when the thumb returns (see throttleCarry).
                if (OneThumbAbilityActive) inputStatus.XDiff = heldXDiff;
                else
                {
                    if (touchCount >= 2 && throttleCarry != 0f)
                        inputStatus.XDiff = CarryThrottle(inputStatus.XDiff, throttleCarry);
                    heldXDiff = inputStatus.XDiff;
                }


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
            // Every change between ONE thumb and several re-zeroes the sticks where the thumbs are
            // (see rebaseSticks) - the same edges the ability below fires on, so a three-finger
            // lift that fires it also re-zeroes, and the mirrored one-thumb flight never starts
            // from wherever the lifted thumbs left the stick.
            if ((prevTouchCount >= 2 && touchCount == 1) || (prevTouchCount == 1 && touchCount >= 2))
                rebaseSticks = true;

            // Coming back from an ABILITY lift: carry the held throttle into two-thumb flight.
            // Read before StopDrift clears the flags that say it was an ability lift.
            if (prevTouchCount == 1 && touchCount >= 2 && (onlyLeftActive || onlyRightActive))
                throttleCarry = heldXDiff - 0.5f;

            if (prevTouchCount == 0 && touchCount == 1) singleThumbFromLift = false;

            // 2+ → 1: finger lifted, start drifting
            if (prevTouchCount >= 2 && touchCount == 1)
            {
                singleThumbFromLift = true;
                // The held throttle already embodies any earlier carry.
                throttleCarry = 0f;

                var remainingPosition = Touch.activeTouches[0].screenPosition;
                bool remainingIsLeft = remainingPosition.x < Screen.width * 0.5f;

                if (remainingIsLeft)
                {
                    // Right thumb was lifted, only left remains → OnlyLeftStickAction
                    onlyLeftActive = true;
                    inputStatus.OnButtonPressed.Raise(InputEvents.OnlyLeftStickAction);
                }
                else
                {
                    // Left thumb was lifted, only right remains → OnlyRightStickAction
                    onlyRightActive = true;
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

        // The strategy can stop running with thumbs down (a pad or keyboard takes over, or the game
        // pauses), and then never sees the lift. Release everything a held touch started.
        public override void OnStrategyDeactivated()
        {
            StopDrift();
            StopStickEffects();
            ReleaseSpeedEffects();
        }

        public override void OnPaused()
        {
            StopDrift();
            StopStickEffects();
            ReleaseSpeedEffects();
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

            if (rebaseSticks)
            {
                rebaseSticks = false;
                leftJoystickStart = leftJoystickValue;
                rightJoystickStart = rightJoystickValue;
            }

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

            if (rebaseSticks)
            {
                rebaseSticks = false;
                if (useLeft) leftJoystickStart = position;
                else rightJoystickStart = position;
            }

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
            if (!leftStickEffectsStarted && singleThumbFromLift)
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
            if (!rightStickEffectsStarted && singleThumbFromLift)
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

            // Scaled radial dead zone: inside deadZone output is zero (tremor filter);
            // outside, output rescales from the dead zone's edge to the rim so leaving the zone
            // ramps smoothly from 0 instead of jumping.
            float magnitude = clampedOffset.magnitude;
            if (magnitude <= deadZone)
            {
                joystick = Vector2.zero;
                return;
            }
            joystick = (clampedOffset / magnitude)
                       * ((magnitude - deadZone) / (joystickRadius - deadZone));
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
            //   XDiff = (s.x - s.x + 2)/4 = 0.5  -> throttle neutral (then held, see heldXDiff)
            //   YDiff = Ease(s.y - s.y)   = 0    -> no roll, i.e. pitch and yaw ONLY
            //   XSum/YSum = Ease(2s)             -> pitch + yaw at the two-thumb ceiling
            // No gain is applied, in a drift or out of one, so the mix and the fan-out (what a
            // single-stick hull steers from, and what every "stick at the rim" ability perimeter
            // is measured against) are the same mirrored stick.
            // Flying one-thumbed previously did the opposite of all three: the idle stick decays
            // toward zero, so XDiff drifted with sideways thumb travel (a turn silently changed
            // SPEED), YDiff picked up roll from vertical travel, and pitch/yaw ran at Ease(s) =
            // 0.4625 of full authority.
            if (oneThumbActive)
            {
                left = oneThumbStick;
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

        /// <summary>
        /// Blend the carried throttle into the live spread: at neutral thumbs the output IS the
        /// carried throttle; toward full spread or full squeeze the carry fades out, so both
        /// ends stay reachable. <paramref name="live"/> and the result are XDiff (0..1, 0.5 neutral).
        /// </summary>
        private static float CarryThrottle(float live, float carry)
        {
            float fromNeutral = Mathf.Min(1f, Mathf.Abs(live - 0.5f) * 2f);
            return Mathf.Clamp01(live + carry * (1f - fromNeutral));
        }

        private void PerformSpeedAndDirectionalEffects()
        {
            const float threshold = StraightLineGesture.EngageThreshold;
            float DeviationFromFullSpeedStraight = StraightLineGesture.DeviationFromFullSpeedStraight(inputStatus);
            float DeviationFromMinimumSpeedStraight = StraightLineGesture.DeviationFromMinimumSpeedStraight(inputStatus);

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
