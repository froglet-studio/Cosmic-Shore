using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Scarab's movement model (design: R_VesselActions/SCARAB.md §3.2) — the fleet's first
    /// true analog throttle. Every other transformer computes a TARGET speed and eases toward it;
    /// the Scarab INTEGRATES: hold the right trigger and speed climbs at a constant rate toward a
    /// Time-scaled ceiling, release it and the vessel coasts down slowly toward MinimumSpeed. The
    /// trigger's depth is how hard you push, not how fast you end up — momentum is the feel.
    ///
    /// Steering is single-stick (left stick pitch/yaw, Sparrow-style) but this class deliberately
    /// extends VesselTransformer rather than SingleStickVesselTransformer: that subclass's
    /// RotateShip writes Course = forward every frame and its MoveShip drops the base drift-course
    /// blend, so a single-stick vessel cannot drift through it. The Scarab needs BOTH single-stick
    /// steering AND the Squirrel's analog heading/course decoupling (LT drift,
    /// singleTriggerDrift = true on the prefab), so it takes the base transformer and re-implements
    /// the small single-stick rotation overrides here, leaving the base drift machinery intact.
    ///
    /// TIME 5 — "Snap Dash": double-tap the throttle trigger for a burst along the current course
    /// (the once-per-window edge detector below; gated on IsUpgradeActive(Element.Time) at the
    /// moment of the second tap, per the per-use snapshot law). Below Time 5 the second tap is
    /// just more throttle.
    ///
    /// AI: AIPilot has no trigger synthesis, so under autopilot the throttle reads as fully held —
    /// the single-stick "full throttle is implicit" semantic. Without this an AI Scarab would idle
    /// at MinimumSpeed forever.
    /// </summary>
    public class ScarabVesselTransformer : VesselTransformer
    {
        const float TriggerDeadzone = 0.05f; // matches GamepadInputStrategy's edge threshold

        [Header("Scarab Throttle (integrator)")]
        [Tooltip("Speed gained per second at full trigger pull (world units/s²). The trigger's " +
                 "analog depth scales this linearly — half pull accelerates at half rate.")]
        [SerializeField, Min(0f)] float accelerationPerSecond = 70f;

        [Tooltip("Speed shed per second while coasting (world units/s²). Deliberately low — the " +
                 "long coast is what makes the vessel read as a thing with mass. Speed never " +
                 "decays below MinimumSpeed.")]
        [SerializeField, Min(0f)] float coastDragPerSecond = 12f;

        [Tooltip("The throttle ceiling at element level 0. The EFFECTIVE ceiling is this × " +
                 "ThrottleScalerMultiplier.EvaluateLive — author that ElementalFloat on the " +
                 "prefab as the TIME element, 1 → 1.5 (SCARAB.md §7: Time = top speed of the " +
                 "throttle ramp; the map's generic Time multiplier stays pinned to 1).")]
        [SerializeField, Min(0f)] float baseTopSpeed = 180f;

        [Header("Snap Dash (TIME 5)")]
        [Tooltip("Two throttle-trigger presses inside this window fire the dash (seconds).")]
        [SerializeField, Min(0.05f)] float doubleTapWindowSeconds = 0.3f;
        [Tooltip("Peak dash speed injected along the current course via ModifyVelocity " +
                 "(world u/s; the channel clamps at 100 — see VesselTransformer).")]
        [SerializeField, Min(0f)] float dashSpeed = 100f;
        [SerializeField, Min(0.05f)] float dashDurationSeconds = 0.4f;

        bool _triggerHeld;
        float _lastTapTime = float.NegativeInfinity;

        public override void Initialize(IVessel vessel)
        {
            base.Initialize(vessel);
            Vessel.VesselStatus.IsSingleStickControls = true;
        }

        // ------------------- Single-stick steering (left stick only) -------------------
        // The SingleStickVesselTransformer pattern, minus its Course write: pitch/yaw carry
        // TurnScalar (the restricted-stance multiplier), roll is the bank into the turn and
        // deliberately does not.
        protected override void Pitch()
        {
            if (InputStatus == null) return;
            accumulatedRotation = Quaternion.AngleAxis(
                -InputStatus.EasedLeftJoystickPosition.y * (speed * RotationThrottleScaler + PitchScaler) * TurnScalar * Time.deltaTime,
                transform.right) * accumulatedRotation;
        }

        protected override void Yaw()
        {
            if (InputStatus == null) return;
            accumulatedRotation = Quaternion.AngleAxis(
                InputStatus.EasedLeftJoystickPosition.x * (speed * RotationThrottleScaler + YawScaler) * TurnScalar * Time.deltaTime,
                transform.up) * accumulatedRotation;
        }

        protected override void Roll()
        {
            if (InputStatus == null) return;
            accumulatedRotation = Quaternion.AngleAxis(
                -InputStatus.EasedLeftJoystickPosition.x * (speed * RotationThrottleScaler + RollScaler) * Time.deltaTime,
                transform.forward) * accumulatedRotation;
        }

        // ------------------- The integrator -------------------
        /// <summary>Live throttle 0..1: the right trigger's analog depth (the keyboard strategy
        /// writes RightTriggerAnalog binary from RShift, so desktop works unchanged). Autopilot
        /// reads as fully held — AIPilot produces no trigger input.</summary>
        float ReadThrottle01()
        {
            if (VesselStatus != null && VesselStatus.AutoPilotEnabled) return 1f;
            return InputStatus != null ? Mathf.Clamp01(InputStatus.RightTriggerAnalog) : 0f;
        }

        /// <summary>The Time-scaled throttle ceiling. ThrottleScalerMultiplier is the base
        /// class's existing ElementalFloat (dormant fleet-wide until now) — authored on the
        /// Scarab prefab as element Time, 1 → 1.5.</summary>
        float ThrottleCeiling()
            => baseTopSpeed * ThrottleScalerMultiplier.EvaluateLive(VesselStatus);

        void IntegrateThrottle()
        {
            float throttle01 = ReadThrottle01();
            speed += throttle01 * accelerationPerSecond * Time.deltaTime;
            speed -= coastDragPerSecond * Time.deltaTime;
            speed = Mathf.Clamp(speed, MinimumSpeed, ThrottleCeiling());

            DetectSnapDash(throttle01);
        }

        /// <summary>Double-tap detector for the TIME-5 dash. A rising edge is the analog value
        /// crossing the same deadzone the input strategy uses for its own trigger edges; two
        /// rising edges inside the window fire the dash. The upgrade gate is read at the moment
        /// of the second tap (per-use snapshot), never cached.</summary>
        void DetectSnapDash(float throttle01)
        {
            bool held = throttle01 > TriggerDeadzone;
            bool risingEdge = held && !_triggerHeld;
            _triggerHeld = held;
            if (!risingEdge) return;

            // Autopilot holds the trigger permanently — no edges, no dash. (Defensive: held
            // stays true under AI, so risingEdge can never fire there anyway.)
            float now = Time.time;
            bool withinWindow = now - _lastTapTime <= doubleTapWindowSeconds;
            _lastTapTime = now;
            if (!withinWindow) return;

            _lastTapTime = float.NegativeInfinity; // consume — a triple tap is not two dashes

            if (VesselStatus == null) return;
            if (!VesselStatus.ElementalAbilityHandler.IsUpgradeActive(Element.Time)) return;

            Vector3 course = VesselStatus.Course.sqrMagnitude > 1e-4f
                ? VesselStatus.Course
                : transform.forward;
            ModifyVelocity(course.normalized * dashSpeed, dashDurationSeconds);
        }

        /// <summary>Mirror of the base class's private drift trigger sum, needed because MoveShip
        /// is overridden here: on gamepad the Scarab authors singleTriggerDrift, so LT's 0-1
        /// travel spans the whole 0-2 drift range; elsewhere drift is binary while active.</summary>
        float DriftIntensity01()
        {
            if (InputStatus == null || VesselStatus == null) return 0f;
            if (InputStatus.ActiveInputDevice == InputDeviceType.Gamepad)
                return Mathf.Clamp01(InputStatus.LeftTriggerAnalog * 2f);
            return VesselStatus.IsDrifting ? 1f : 0f;
        }

        // Not used by this class's MoveShip (the integrator owns `speed` directly), but the
        // base contract expects a sane answer: the target IS the current integrated speed.
        protected override float ComputeThrottleTarget() => speed;

        protected override void MoveShip()
        {
            if (VesselStatus == null || InputStatus == null) return;

            IntegrateThrottle();

            // Modifier channel semantics unchanged from the base: scale this frame's OUTPUT
            // only — multiplying into the persistent `speed` would compound per frame.
            float effectiveSpeed = speed * throttleMultiplier;
            VesselStatus.Speed = effectiveSpeed;

            // The base transformer's drift-course blend, re-stated here because this override
            // replaces base.MoveShip: while drifting, the course decouples from the nose by the
            // analog drift intensity; DriftDamping (written by the base ApplyAnalogDrift, which
            // still runs in the base Update) is the convergence rate — higher = less drift.
            if (VesselStatus.IsDrifting)
            {
                float driftAmount = DriftIntensity01();
                Vector3 driftedCourse = DriftDamping > 0.001f
                    ? Vector3.Slerp(VesselStatus.Course, transform.forward, DriftDamping * Time.deltaTime).normalized
                    : VesselStatus.Course;
                VesselStatus.Course = Vector3.Slerp(transform.forward, driftedCourse, driftAmount);
            }
            else
            {
                VesselStatus.Course = transform.forward;
            }

            transform.position += (effectiveSpeed * VesselStatus.Course + velocityShift) * Time.deltaTime;
        }
    }
}
