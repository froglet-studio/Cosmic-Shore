using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Scarab's movement model (design: R_VesselActions/SCARAB.md §3.2) — the fleet's first
    /// true analog throttle. Every other transformer computes a TARGET speed and eases toward it;
    /// the Scarab INTEGRATES a real world-space VELOCITY VECTOR: the trigger's depth is how hard
    /// you push, not how fast you end up.
    ///
    /// Thrust is applied along the NOSE, always — never along the current course. That single
    /// choice is what makes the drift read as driving rather than as ice: your momentum keeps
    /// carrying you down the old line while the engine pushes where you are POINTING, so a drift
    /// is something you steer out of by aiming and squeezing, not a direction lock that the
    /// throttle only makes worse. (The first pass accelerated along `Course`, which meant
    /// throttling mid-drift dug you deeper into the slide.)
    ///
    /// A scalar cannot express that, because during a drift the travel direction and the thrust
    /// direction are different vectors — hence the vector state. The inherited `speed` field is
    /// kept in sync every frame because the fleet's rotation math reads it
    /// (`speed * RotationThrottleScaler`), and an external write to it (a vessel swap's
    /// `SetInitialSpeed`, `ResetTransformer`) is detected and re-seeds the vector.
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
        [Tooltip("Speed gained per second at full trigger pull (world units/s²), applied along " +
                 "the NOSE. The trigger's analog depth scales this linearly — half pull " +
                 "accelerates at half rate. Sized against coastDragPerSecond: the brake is " +
                 "stronger than the engine, so the vessel is quicker to stop than to start.")]
        [SerializeField, Min(0f)] float accelerationPerSecond = 90f;

        [Tooltip("Speed shed per second when the trigger is released (world units/s²). The Scarab " +
                 "has no brake — releasing the throttle IS the brake, so this is deliberately " +
                 "strong: at 120 a full-speed vessel is stopped in about a second and a half. " +
                 "Linear rather than proportional so it actually reaches zero instead of " +
                 "asymptotically crawling. Speed never decays below MinimumSpeed (authored 0 on " +
                 "the Scarab, so releasing brings you to a genuine stop).")]
        [SerializeField, Min(0f)] float coastDragPerSecond = 120f;

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

        /// <summary>World-space momentum. The Scarab's authoritative movement state — see the
        /// class summary for why a scalar cannot express thrust-along-nose during a drift.</summary>
        Vector3 _velocity;

        /// <summary>Last value this class wrote into the inherited <c>speed</c> field. If they
        /// disagree at the top of a frame, something OUTSIDE wrote it (a menu vessel swap's
        /// <c>SetInitialSpeed</c>, a spawn's <c>ResetTransformer</c>) and the vector re-seeds from
        /// it — otherwise an inherited-speed swap would silently drop to a dead stop.</summary>
        float _lastPublishedSpeed;

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

        /// <summary>Re-seed the velocity vector if an external caller wrote the inherited
        /// <c>speed</c> field since our last publish (vessel swap / reset).</summary>
        void SyncExternalSpeedWrites()
        {
            if (Mathf.Approximately(speed, _lastPublishedSpeed)) return;
            Vector3 dir = _velocity.sqrMagnitude > 1e-6f ? _velocity.normalized : transform.forward;
            _velocity = dir * speed;
            _lastPublishedSpeed = speed;
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

        // Not used by this class's MoveShip (the integrator owns the velocity directly), but the
        // base contract expects a sane answer: the target IS the current speed.
        protected override float ComputeThrottleTarget() => speed;

        protected override void MoveShip()
        {
            if (VesselStatus == null || InputStatus == null) return;

            float dt = Time.deltaTime;
            SyncExternalSpeedWrites();

            float throttle01 = ReadThrottle01();
            DetectSnapDash(throttle01);

            // 1) THRUST ALONG THE NOSE — never along the current course. This is the whole
            //    handling model: mid-drift the engine pushes where you point, so aiming out of
            //    a slide and squeezing is how you recover.
            _velocity += transform.forward * (throttle01 * accelerationPerSecond * dt);

            // 2) COURSE. Not drifting, travel IS the nose (the fleet's normal behaviour, and it
            //    keeps the trail aligned). Drifting, momentum is preserved and converges toward
            //    the nose at DriftDamping — the base ApplyAnalogDrift still writes that value, so
            //    LT's analog depth controls how loose the back end feels.
            float speedNow = _velocity.magnitude;
            if (speedNow > 1e-4f)
            {
                float driftAmount = VesselStatus.IsDrifting ? DriftIntensity01() : 0f;
                // driftAmount 0 → snap to the nose outright; 1 → converge only as fast as
                // DriftDamping allows (0 damping = the course never converges at all).
                float convergence = Mathf.Clamp01(Mathf.Lerp(1f, DriftDamping * dt, driftAmount));
                _velocity = Vector3.Slerp(_velocity / speedNow, transform.forward, convergence) * speedNow;
            }
            else
            {
                _velocity = transform.forward * speedNow;
            }

            // 3) COAST. Releasing the trigger is the only brake, so the decay is linear and
            //    strong — linear so it genuinely reaches MinimumSpeed instead of crawling
            //    asymptotically toward it.
            speedNow = Mathf.Max(speedNow - coastDragPerSecond * dt, 0f);
            speedNow = Mathf.Clamp(speedNow, MinimumSpeed, ThrottleCeiling());
            _velocity = speedNow > 1e-4f ? _velocity.normalized * speedNow : Vector3.zero;

            // Publish. `speed` is the fleet's API (the rotation scalers read it), so it stays in
            // lockstep with the vector's magnitude.
            speed = speedNow;
            _lastPublishedSpeed = speedNow;

            // Modifier channel semantics unchanged from the base: scale this frame's OUTPUT only
            // — multiplying into the persistent state would compound per frame.
            float effectiveSpeed = speedNow * throttleMultiplier;
            VesselStatus.Speed = effectiveSpeed;
            VesselStatus.Course = speedNow > 1e-4f ? _velocity / speedNow : transform.forward;

            transform.position += (effectiveSpeed * VesselStatus.Course + velocityShift) * dt;
        }
    }
}
