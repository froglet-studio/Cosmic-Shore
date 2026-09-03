using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Scarab's movement policy (design: R_VesselActions/SCARAB.md §3.2) — the fleet's first
    /// true analog throttle. Every other vessel computes a TARGET speed and eases toward it; the
    /// Scarab INTEGRATES: the trigger's depth is how hard you push, not how fast you end up.
    ///
    /// This class is now a POLICY, not a flight model. The velocity vector, grip, the
    /// Course/Speed publish, the modifier channels, position integration and the external-write
    /// re-seed all live in <see cref="VesselTransformer"/>'s shared vector model (authored on this
    /// vessel with <c>vectorFlightModel</c>); the Scarab supplies only the two questions a vessel
    /// can answer differently — <see cref="ComputeNoseAcceleration"/> ("how hard am I pushing
    /// along the nose this frame?") and <see cref="ShapeSpeed"/> ("what bounds my speed?"). The
    /// first version of this class carried a whole fourth copy of <c>MoveShip</c>; lifting the
    /// model to the base is what let the Squirrel and Dolphin have it too.
    ///
    /// Steering is single-stick (left stick pitch/yaw, Sparrow-style) but this class deliberately
    /// extends VesselTransformer rather than SingleStickVesselTransformer: that subclass's
    /// RotateShip writes <c>Course = forward</c> every frame and its MoveShip drops the drift
    /// machinery, so a single-stick vessel cannot drift through it. The Scarab needs BOTH
    /// single-stick steering AND analog heading/course decoupling (LT drift, singleTriggerDrift on
    /// the prefab), so it takes the base and re-implements the small rotation overrides here.
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
                 "accelerates at half rate, so depth controls how HARD you push, and the ceiling " +
                 "controls where you end up. Independent of coastDragPerSecond: the two never " +
                 "apply on the same frame (drag is release-only), so the brake being stronger " +
                 "than the engine is a feel choice, not a tug of war.")]
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

        /// <summary>This frame's throttle depth, sampled in <see cref="ComputeNoseAcceleration"/>
        /// and re-read in <see cref="ShapeSpeed"/>. The base model calls those two in that order
        /// within one MoveShip, so the value is always fresh — sampling the input twice would
        /// risk reading a different frame's trigger between them.</summary>
        float _throttle01;

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
            if (InputStatus == null || BankIntoTurnSuppressed) return;
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

        /// <summary>The LIVE top speed, Time scaling included — what anything normalizing
        /// against "how fast can this vessel go right now" must read (ScarabAnimation's leg
        /// tuck: normalized against the authored base, a Time-10 Scarab rides pinned 'tucked'
        /// from two-thirds throttle up and the fleet's best throttle read carries nothing).</summary>
        public float CurrentTopSpeed => ThrottleCeiling();

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

        // The Scarab's speed is state, not a tracked target, so there is no meaningful "target"
        // to report — the current speed IS it. Nothing on this vessel's path consumes it (both
        // base methods that would are overridden below); it exists so the base contract has a
        // sane answer rather than a lie.
        protected override float ComputeThrottleTarget() => speed;

        /// <summary>Thrust is the trigger's depth, full stop — no target tracking, and no drift
        /// exception: the Scarab's throttle is deliberately LIVE while drifting, because "aim out
        /// of the slide and squeeze" is the vessel's whole handling identity. (The base's
        /// DriftThrottlePolicy is therefore not consulted here.) Also the once-per-frame home for
        /// the Snap Dash edge detector, which needs the same throttle sample.</summary>
        protected override float ComputeNoseAcceleration(float dt)
        {
            _throttle01 = ReadThrottle01();
            DetectSnapDash(_throttle01);
            return _throttle01 * accelerationPerSecond * dt;
        }

        /// <summary>
        /// Replaces the base drift-overshoot ceiling entirely: the Scarab already has a hard
        /// speed ceiling of its own, so a second bound keyed off a throttle target it does not
        /// have would be meaningless.
        ///
        /// COAST IS RELEASE-ONLY. Holding the throttle must never be fought by the brake: the drag
        /// is deliberately STRONGER than the engine (it has to be, to stop you faster than it
        /// starts you), so applying it unconditionally made net acceleration NEGATIVE at full
        /// throttle and the vessel decelerated to a standstill while the trigger was buried —
        /// movement was only possible by tapping. Gating on the same deadzone the input strategy
        /// uses makes that whole class of bug impossible by construction rather than a balance to
        /// maintain between two numbers.
        /// </summary>
        protected override float ShapeSpeed(float speedNow, float speedBeforeThrust, float dt)
        {
            if (_throttle01 <= TriggerDeadzone)
                speedNow = Mathf.Max(speedNow - coastDragPerSecond * dt, 0f);
            return Mathf.Clamp(speedNow, MinimumSpeed, ThrottleCeiling());
        }
    }
}
