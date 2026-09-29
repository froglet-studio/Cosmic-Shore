using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using CosmicShore.Gameplay;
using CosmicShore.Data;
using CosmicShore.Utility;

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
        // enters the moment a thumb is LIFTED to trigger an ability (a lift is a 2+ -> 1 touch
        // transition, see HandleDriftTransitions: the Squirrel's boost ring, the Butterfly's mode
        // switch and Fold). While it lasts, the live thumb is mirrored onto BOTH virtual sticks
        // in Reparameterize. On the stripped branch the Squirrel's DRIFT is no longer a lift - it
        // is the stick overdrive (see UpdateOverdriveDrift).
        private bool oneThumbActive;
        private Vector2 oneThumbStick;

        /// <summary>
        /// Turn authority for the MIX while one thumb flies and NO ability was triggered to get
        /// there (a single finger down from the start). 1 = the mirror alone, which is exactly
        /// the ceiling a two-thumb pilot already has and never exceeds it: the mix eases
        /// (right + left), so one stick alone feeds Ease(s) - 0.4625 of full authority at max
        /// deflection on THIS class's curve, not the 0.2926 the gamepad cosine in
        /// <see cref="BaseInputStrategy"/> gives - while a mirrored thumb feeds Ease(2s) = 1.0.
        /// That 2.162x IS the "faster turning" this mode is for; it just lets one thumb reach
        /// the two-thumb maximum.
        /// </summary>
        private const float OneThumbTurnGain = 1f;

        /// <summary>
        /// Turn authority for the MIX while one thumb flies BECAUSE the other was lifted to fire
        /// an ability (the boost ring on the Squirrel - drift too, off the stripped branch;
        /// Yawstery / analog turn boost on the Manta). Lower
        /// than <see cref="OneThumbTurnGain"/> because those abilities MULTIPLY the vessel's own
        /// rotation scalers - <c>VesselTransformer.ApplyAnalogDrift</c> writes
        /// <c>Pitch/Yaw/RollScaler = base x Mult</c> - so the mirror's 2.162x and the ability's
        /// multiplier STACK, and each was calibrated as if it were the only one.
        ///
        /// On the Squirrel that stack is what made the drift feel wrong. Commanded yaw ran
        /// <c>YawScaler 120 x Mult x Ease(2s)</c> = 216 deg/s at full deflection, against a grip
        /// that only closes the slip angle by <c>1 - e^(-Grip.dt)</c> per frame. Course cannot
        /// follow, slip passes 90 degrees - and past 90 the nose-ward thrust in
        /// <c>VesselTransformer.ComputeNoseAcceleration</c> is SUBTRACTING from the velocity's
        /// magnitude, because it always adds along +forward while the velocity's forward
        /// component has gone negative. The racing drift scrubs speed instead of carrying it.
        ///
        /// 0.70 lands the mirrored thumb on <c>Ease(1.4) = 0.6643</c> -> 111.6 deg/s, still
        /// faster than the 99.9 deg/s one thumb produced before the mirror existed, with slip
        /// staying inside 90 degrees through a normal corner.
        ///
        /// This is a CALIBRATION, not a derived constant. Lower it toward 0.5 if a held drift
        /// still washes speed off; raise it toward 0.8 if the drift reads sluggish.
        /// </summary>
        private const float OneThumbDriftTurnGain = 0.70f;

        /// <summary>
        /// True while the single live thumb is the result of LIFTING one to fire an ability,
        /// rather than plain one-finger flight. It replaces a write-only "isDrifting" flag that
        /// was set on BOTH single-thumb transitions and so never meant "a drift is running" -
        /// only "one touch remains". On the Squirrel the two really do differ: a lifted RIGHT
        /// thumb raises OnlyLeftStickAction (InputEvents 12), which that vessel binds to the
        /// drift, while a lifted LEFT thumb raises OnlyRightStickAction (11), bound to the tube -
        /// so the old flag pinned the throttle for an ability that is not a drift at all.
        /// Whichever ability it is, THIS is the state the gain cut and the throttle hold are
        /// about: an ability is engaged, so the vessel is scaling rotation underneath us.
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

        // ── Drift by OVERDRIVE (stripped-performance branch) ────────────────────
        // A drift used to be "lift your right thumb": you gave up half your steering to hold it,
        // the lift itself yanked the vessel, and its depth was a fixed number. On glass the
        // natural way to ask for MORE turn than full lock is to keep pushing: so a drift is
        // both thumbs hard over to one side (full yaw on this mix) and then past the rim of the
        // sticks. How far past is the drift's depth - analog, like the pad's trigger - and both
        // thumbs stay down the whole time. Raised as BothSticksAction, which the Squirrel binds
        // its drift to on touch; the drift's authored ceiling is VesselTransformer.touchDriftDepth.

        /// <summary>
        /// Combined push past full yaw, in stick radii, that reads as a full-depth drift. Two
        /// thumbs hard over is 2 radii of yaw; 2 + this is a full drift. One thumb counts double
        /// (it is mirrored onto both sticks), so it reaches full depth half a radius past its rim.
        /// </summary>
        const float DriftOverdriveRadii = 1f;

        /// <summary>Depth at which an overdrive ENGAGES; it releases only once the push falls
        /// back inside full yaw - a small hysteresis so a thumb resting on the rim cannot
        /// chatter the drift on and off.</summary>
        const float DriftEngageDepth = 0.1f;

        /// <summary>
        /// True while the single live thumb is alone because the other was LIFTED (a 2 -&gt; 1
        /// transition), false when it is alone because it was the FIRST thumb down. On the strip
        /// only the former raises Left/RightStickAction: those are the Butterfly's Fold and mode
        /// switch, and off the strip they also fired on a first touch - so putting the right thumb
        /// down first toggled the Butterfly's mode, and left-thumb-first started a Fold that
        /// teleported the vessel the moment the second thumb landed. Which thumb a pilot happens
        /// to touch with first is not a decision; lifting one is.
        /// </summary>
        private bool singleThumbFromLift;

        private bool StickEventsAllowed => singleThumbFromLift || !PerfStrip.TouchStickEventsOnLiftOnly;

        private bool overdriveDriftEngaged;
        private Vector2 leftRawStick, rightRawStick;
        private Vector2 oneThumbRawStick;

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
                throttleCarry = 0f;
                UpdateOverdriveDrift(0);
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

                UpdateOverdriveDrift(touchCount);

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
            // Any change between one and two thumbs re-zeroes the sticks where the thumbs are
            // (see rebaseSticks). A three-finger fumble is not a deliberate lift, so it is left
            // alone.
            if ((prevTouchCount == 2 && touchCount == 1) || (prevTouchCount == 1 && touchCount == 2))
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

            HandleJoystick(ref leftJoystickStart, leftTouchIndex, ref leftNormalizedJoystickPosition, ref leftClampedPosition, out leftRawStick);
            HandleJoystick(ref rightJoystickStart, rightTouchIndex, ref rightNormalizedJoystickPosition, ref rightClampedPosition, out rightRawStick);

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
            oneThumbRawStick = useLeft ? leftRawStick : rightRawStick;
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
            if (!leftStickEffectsStarted && StickEventsAllowed)
            {
                leftStickEffectsStarted = true;
                inputStatus.OnButtonPressed.Raise(InputEvents.LeftStickAction);
            }
            leftJoystickValue = position;
            leftTouchIndex = 0;
            inputStatus.OneTouchLeft = true;
            HandleJoystick(ref leftJoystickStart, leftTouchIndex, ref leftNormalizedJoystickPosition, ref leftClampedPosition, out leftRawStick);
            rightNormalizedJoystickPosition = Vector3.Lerp(rightNormalizedJoystickPosition, Vector3.zero, 7 * Time.deltaTime);
        }

        private void HandleRightStick(Vector2 position)
        {
            if (!rightStickEffectsStarted && StickEventsAllowed)
            {
                rightStickEffectsStarted = true;
                inputStatus.OnButtonPressed.Raise(InputEvents.RightStickAction);
            }
            rightJoystickValue = position;
            rightTouchIndex = 0;
            inputStatus.OneTouchLeft = false;
            HandleJoystick(ref rightJoystickStart, rightTouchIndex, ref rightNormalizedJoystickPosition, ref rightClampedPosition, out rightRawStick);
            leftNormalizedJoystickPosition = Vector3.Lerp(leftNormalizedJoystickPosition, Vector3.zero, 7 * Time.deltaTime);
        }

        private void HandleJoystick(ref Vector2 joystickStart, int touchIndex, ref Vector2 joystick, ref Vector2 clampedPosition, out Vector2 raw)
        {
            Touch touch = Touch.activeTouches[touchIndex];

            if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began || joystickStart == Vector2.zero)
                joystickStart = touch.screenPosition;

            Vector2 offset = touch.screenPosition - joystickStart;

            // Unclamped, in stick radii: the drift overdrive reads how far PAST the rim a thumb is,
            // which the clamped value below cannot see.
            raw = offset / joystickRadius;
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

            // The MIX (XSum/YSum/XDiff/YDiff) and the FAN-OUT (the eased pair + the normalized
            // pair) are built from SEPARATE copies. The fan-out is what a single-stick hull
            // steers from and what every "stick at the rim" ability perimeter (|stick| >= 1) is
            // measured against, so the one-thumb gain below must never reach it - a reduced
            // mirrored stick would silently move those perimeters inward.
            var mixLeft = left;
            var mixRight = right;

            // ONE-THUMB FLIGHT. Mirror the live thumb onto both sticks. The mix is
            // XSum = yaw, YSum = pitch, XDiff = throttle, YDiff = roll over (right +/- left), so
            // mirroring is not a special case bolted on - it falls out of the existing mix as
            // exactly the mode we want:
            //   XDiff = (s.x - s.x + 2)/4 = 0.5  -> throttle neutral (then held, see heldXDiff)
            //   YDiff = Ease(s.y - s.y)   = 0    -> no roll, i.e. pitch and yaw ONLY
            //   XSum/YSum = Ease(2s)             -> pitch + yaw at the two-thumb ceiling
            // Flying one-thumbed previously did the opposite of all three: the idle stick decays
            // toward zero, so XDiff drifted with sideways thumb travel (a turn silently changed
            // SPEED), YDiff picked up roll from vertical travel, and pitch/yaw ran at Ease(s) =
            // 0.4625 of full authority.
            if (oneThumbActive)
            {
                left = oneThumbStick;
                right = left;

                mixLeft = oneThumbStick
                          * (OneThumbAbilityActive ? OneThumbDriftTurnGain : OneThumbTurnGain);
                mixRight = mixLeft;
            }

            inputStatus.EasedRightJoystickPosition = new Vector2(Ease(2 * right.x), Ease(2 * right.y));
            inputStatus.EasedLeftJoystickPosition = new Vector2(Ease(2 * left.x), Ease(2 * left.y));

            inputStatus.RightNormalizedJoystickPosition = right;
            inputStatus.LeftNormalizedJoystickPosition = left;

            inputStatus.XSum = Ease(mixRight.x + mixLeft.x);
            inputStatus.YSum = -Ease(mixRight.y + mixLeft.y);
            inputStatus.XDiff = (mixRight.x - mixLeft.x + 2) / 4;
            inputStatus.YDiff = Ease(mixRight.y - mixLeft.y);
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

        /// <summary>
        /// The drift overdrive (see <see cref="DriftOverdriveRadii"/>). Publishes the depth on
        /// <c>LeftTriggerAnalog</c> - the channel the pad's drift trigger already uses, which
        /// VesselTransformer.GetTriggerSum and DriftAudioController read - and raises
        /// BothSticksAction on engage/release. While engaged the depth is always &gt; 0; zero means
        /// "no measured depth", which is how a thumb-LIFT drift on another hull still reads as a
        /// full pull there.
        /// </summary>
        private void UpdateOverdriveDrift(int touchCount)
        {
            float push = 0f;
            if (touchCount >= 2) push = Mathf.Abs(leftRawStick.x + rightRawStick.x);
            else if (touchCount == 1 && oneThumbActive) push = 2f * Mathf.Abs(oneThumbRawStick.x);

            float depth = Mathf.Clamp01((push - 2f) / DriftOverdriveRadii);
            bool gestureOn = PerfStrip.TouchOverdriveDrift;

            if (gestureOn && !overdriveDriftEngaged && depth >= DriftEngageDepth)
            {
                overdriveDriftEngaged = true;
                inputStatus.OnButtonPressed.Raise(InputEvents.BothSticksAction);
            }
            else if (overdriveDriftEngaged && (depth <= 0f || !gestureOn))
            {
                overdriveDriftEngaged = false;
                inputStatus.OnButtonReleased.Raise(InputEvents.BothSticksAction);
            }

            inputStatus.LeftTriggerAnalog = overdriveDriftEngaged ? depth : 0f;
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
