using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// AI boost for a hull whose speed is a RAMP (<see cref="RampBoostActionSO"/> - the Rhino). Three states:
    /// <list type="bullet">
    /// <item><b>Ramp</b> - on a straight at the objective, press the ramp, hold the throttle full and keep the steering
    /// inside the ramp's full-power plateau, so small corrections toward the ring keep the whole ramp.</item>
    /// <item><b>Brake</b> - from the point where the ramp's own bleed (<see cref="RampBoostActionSO.BleedPerSecond"/>,
    /// 300 u/s²) just reaches the CORNER SPEED by the objective, stay on the ramp and lift the throttle: the graded ramp
    /// then tracks down at its bleed rate, 2.5× faster than the coast a released ramp falls back at
    /// (<see cref="RampBoostActionSO.ReturnPerSecond"/>, 120 u/s²). The lift is a governor read off the transformer's
    /// own target, so it never re-derives the throttle formula, and it never lifts so far that the ramp falls off.</item>
    /// <item><b>Off</b> - a sharp corner: let go of the ramp (full steering), and press it again on the next straight
    /// that pays the wind-up back.</item>
    /// </list>
    ///
    /// <para><b>The corner speed</b> comes from the corner itself when the mode says what comes after the objective
    /// (<see cref="AIBoostContext.CornerDegrees"/>; the gate races do): <see cref="cornerSpeed"/> is the speed for a 90°
    /// corner, and a turn of θ needs a lead of R·tan(θ/2), so the speed scales by 1/tan(θ/2) - a gentle bend costs no
    /// braking at all, a hairpin brakes to <see cref="minCornerSpeed"/>. With no corner known it is a 90° one.</para>
    ///
    /// <para><b>The wind-up is paid back</b> only when the leg is long enough to climb at the ramp's acceleration AND
    /// brake back to the corner speed in time: the peak speed that fits (<see cref="PeakSpeed"/>) must beat the
    /// current speed by <see cref="minSpeedGain"/> (REGATTA_AI_ROADMAP.md step 4).</para>
    ///
    /// <para><b>Why it exists</b> (2026-10-14 Regatta audit): the ramp starts only on <c>FullSpeedStraightAction</c>,
    /// which only a human input strategy raises, and the action handler drops presses while autopilot flies - so an
    /// AI Rhino never ramped at all. Its first cut (2026-10-15) ramped but let go 0.5 s from every objective, after
    /// which the released ramp coasted down from 840 u/s at 120 u/s² and the Rhino missed the mouths it overshot
    /// (REGATTA_AI_ROADMAP.md: 67.9 s at I1 against a 17.8 s model). The re-arm and the explicit end are the
    /// <c>cece/relaxed-hopper-r62hbd</c> cut's (Garrett's Rhino branch).</para>
    /// </summary>
    [CreateAssetMenu(fileName = "RampBoostAIPolicy", menuName = "ScriptableObjects/AI/Boost Policy/Ramp Boost")]
    public sealed class RampBoostAIPolicySO : AIBoostPolicySO
    {
        [Header("Ramp")]
        [Tooltip("While ramping, the AI keeps its total steering under this share of the ramp's full-power plateau " +
                 "(StraightLineGesture.EngageThreshold, 0.3), so it still corrects toward the ring at full ramp power.")]
        [SerializeField, Range(0f, 1f)] float steeringShareOfPlateau = 0.9f;
        [Tooltip("Shortest ramp once engaged, seconds - a straight that briefly fails the test does not stutter it.")]
        [SerializeField, Min(0f)] float minHoldSeconds = 0.5f;
        [Tooltip("Shortest gap between ramps, seconds.")]
        [SerializeField, Min(0f)] float minRestSeconds = 0.3f;
        [Tooltip("Shortest gap between two arms while the AI still wants the ramp, seconds - each arm plays the engage " +
                 "sound. Re-arms a ramp the grading let fall off.")]
        [SerializeField, Min(0f)] float minRearmSeconds = 0.5f;
        [Tooltip("Press the ramp only when the leg lets it climb at least this many u/s above the current speed and " +
                 "still brake back to the corner speed in time - a short leg buys a wide turn, not time.")]
        [SerializeField, Min(0f)] float minSpeedGain = 50f;

        [Header("Corners")]
        [Tooltip("Speed to arrive at a 90-degree corner with, u/s. A turn of θ scales it by 1/tan(θ/2) (a 45° bend " +
                 "allows 2.4×, a 135° one 0.41×). Learned from people's Regatta flights: their speed at each ring. " +
                 "The default is the best of an offline sweep (AI_BOOST.md, the Rhino ramp section).")]
        [SerializeField, Min(1f)] float cornerSpeed = 600f;
        [Tooltip("The least corner speed, u/s - the hairpin floor.")]
        [SerializeField, Min(1f)] float minCornerSpeed = 160f;
        [Tooltip("Finish braking this many units BEFORE the objective - the gate races switch from the approach point " +
                 "to the mouth before the pilot reaches it.")]
        [SerializeField, Min(0f)] float brakeMarginUnits = 120f;
        [Tooltip("Start braking this many seconds early - the governor needs a few frames to find the lift.")]
        [SerializeField, Min(0f)] float brakeLeadSeconds = 0.25f;
        [Tooltip("While braking, the steering plus the throttle lift stays under this share of the ramp's grace band, " +
                 "so braking never lets the ramp fall off (and coast).")]
        [SerializeField, Range(0.5f, 1f)] float graceShare = 0.9f;
        [Tooltip("Steering the brake always leaves the pilot, as a total stick (the throttle lift never takes it).")]
        [SerializeField, Range(0f, 1f)] float brakeSteerFloor = 0.3f;
        [Tooltip("How fast the governor moves the throttle, per second.")]
        [SerializeField, Min(0.1f)] float throttleSlewPerSecond = 4f;
        [Tooltip("Let go of the ramp (full steering, the slow coast) once the course is this far off the objective " +
                 "and the speed is down to the corner speed.")]
        [SerializeField, Range(0f, 180f)] float cornerReleaseDegrees = 30f;

        public float CornerSpeed => cornerSpeed;
        public float MinCornerSpeed => minCornerSpeed;

        public override AIBoostDriver CreateDriver(IVesselStatus status, ActionExecutorRegistry executors)
        {
            var handler = status?.ActionHandler;
            if (handler == null || !handler.TryGetBoundAction<RampBoostActionSO>(out var action, out _)) return null;
            var executor = executors ? executors.Get<RampBoostActionExecutor>() : null;
            return new Driver(this, action, status, executors, executor);
        }

        /// <summary>
        /// The speed to arrive at a corner of <paramref name="cornerDegrees"/> with (negative = unknown, read as 90°):
        /// <see cref="cornerSpeed"/> / tan(θ/2), floored at <see cref="minCornerSpeed"/>. A turn of θ at radius R needs
        /// a lead of R·tan(θ/2), and R grows with speed.
        /// </summary>
        public float CornerSpeedFor(float cornerDegrees)
        {
            float theta = cornerDegrees < 0f ? 90f : Mathf.Clamp(cornerDegrees, 1f, 179f);
            return Mathf.Max(minCornerSpeed, cornerSpeed / Mathf.Tan(0.5f * theta * Mathf.Deg2Rad));
        }

        /// <summary>Units needed to slow from <paramref name="speed"/> to <paramref name="target"/> at
        /// <paramref name="decel"/> u/s² (0 when already at or under it).</summary>
        public static float BrakeDistance(float speed, float target, float decel) =>
            speed <= target ? 0f : (speed * speed - target * target) / (2f * Mathf.Max(decel, 1f));

        /// <summary>
        /// The highest speed a pilot at <paramref name="speed"/> reaches on <paramref name="distance"/> units when it
        /// climbs at <paramref name="accel"/> and must be back down to <paramref name="exitSpeed"/> at the end, braking
        /// at <paramref name="decel"/>: v² (1/2a + 1/2b) = d + v0²/2a + ve²/2b. Never below the current speed.
        /// </summary>
        public static float PeakSpeed(float speed, float exitSpeed, float accel, float decel, float distance)
        {
            float a = Mathf.Max(accel, 1f), b = Mathf.Max(decel, 1f);
            // A leg too short to climb past the exit speed needs no brake at all: it climbs the whole way.
            float climbOnly = Mathf.Sqrt(speed * speed + 2f * a * Mathf.Max(0f, distance));
            if (climbOnly <= exitSpeed) return Mathf.Max(speed, climbOnly);
            float v2 =(Mathf.Max(0f, distance) + speed * speed / (2f * a) + exitSpeed * exitSpeed / (2f * b))
                       / (1f / (2f * a) + 1f / (2f * b));
            return Mathf.Max(speed, Mathf.Sqrt(Mathf.Max(0f, v2)));
        }

        /// <summary>The leg ahead pays the wind-up back (see <see cref="minSpeedGain"/>).</summary>
        public bool PaysBack(in AIBoostContext c, float accel, float decel)
        {
            float exit = CornerSpeedFor(c.CornerDegrees);
            float room = c.Distance - brakeMarginUnits - c.Speed * brakeLeadSeconds;
            return PeakSpeed(c.Speed, exit, accel, decel, room) - c.Speed >= minSpeedGain;
        }

        /// <summary>Close enough to the objective that the ramp's bleed only just reaches the corner speed.</summary>
        public bool BrakePoint(in AIBoostContext c, float decel) =>
            c.Distance - brakeMarginUnits - c.Speed * brakeLeadSeconds
            <= BrakeDistance(c.Speed, CornerSpeedFor(c.CornerDegrees), decel);

        /// <summary>
        /// The governor's next throttle: down while the ramp is tracking above <paramref name="wanted"/>, up while it is
        /// under, never under <paramref name="floor"/>. Pure, so the tests can drive it.
        /// </summary>
        public static float GovernThrottle(float throttle, float trackedTarget, float wanted, float slew, float dt, float floor) =>
            Mathf.Clamp(throttle + (trackedTarget > wanted ? -1f : 1f) * slew * dt, Mathf.Clamp01(floor), 1f);

        enum Mode { Off = 0, Ramp = 1, Brake = 2 }

        sealed class Driver : AIBoostDriver
        {
            readonly RampBoostAIPolicySO _policy;
            readonly RampBoostActionSO _action;
            readonly IVesselStatus _status;
            readonly ActionExecutorRegistry _executors;
            readonly RampBoostActionExecutor _executor;

            Mode _mode;
            float _changedAt = float.NegativeInfinity;
            float _armedAt = float.NegativeInfinity;
            float _throttle = 1f;
            float _wanted;

            public Driver(RampBoostAIPolicySO policy, RampBoostActionSO action, IVesselStatus status,
                          ActionExecutorRegistry executors, RampBoostActionExecutor executor)
            {
                _policy = policy;
                _action = action;
                _status = status;
                _executors = executors;
                _executor = executor;
            }

            public override bool Drives(ShipActionSO action) => action is RampBoostActionSO;

            public override bool HoldsFullThrottle => _mode == Mode.Ramp;

            public override float Throttle => _mode == Mode.Brake ? _throttle : -1f;

            float Grace => _action.StraightnessGraceBand * _policy.graceShare;

            public override float RotationLimit => _mode switch
            {
                Mode.Ramp => StraightLineGesture.EngageThreshold * _policy.steeringShareOfPlateau,
                // What the throttle lift leaves of the grace band: the ramp stays on while braking.
                Mode.Brake => Mathf.Max(0f, Grace - (1f - _throttle)),
                _ => -1f,
            };

            public override string Status => _mode switch
            {
                Mode.Ramp => $"ramping on the straight ({_status.Speed:F0} u/s, x{_status.BoostMultiplier:F1})",
                Mode.Brake => $"braking on the ramp to {_wanted:F0} u/s for the corner ({_status.Speed:F0} u/s, throttle {_throttle:F2})",
                _ => "cruising - waiting for a straight that pays the wind-up back",
            };

            public override void Tick(in AIBoostContext c)
            {
                float now = Time.time;
                float decel = _action.BleedPerSecond;
                if (c.Restricted || c.BreakingOrbit)
                {
                    if (_mode != Mode.Off) Set(Mode.Off, now);
                    return;
                }

                switch (_mode)
                {
                    case Mode.Off:
                        if (now - _changedAt >= _policy.minRestSeconds && _policy.IsStraight(c) && _policy.HasRunway(c)
                            && _policy.PaysBack(c, _action.AccelerationPerSecond, decel))
                            Set(Mode.Ramp, now);
                        return;

                    case Mode.Ramp:
                        Rearm(now);
                        if (now - _changedAt < _policy.minHoldSeconds) return;
                        if (!_policy.StillStraight(c) || _policy.TurnAhead(c) || _policy.BrakePoint(c, decel))
                            Set(Mode.Brake, now);
                        return;

                    case Mode.Brake:
                        _wanted = _policy.CornerSpeedFor(c.CornerDegrees);
                        if (c.AngleToTarget >= _policy.cornerReleaseDegrees && c.Speed <= _wanted * 1.1f)
                        {
                            Set(Mode.Off, now);   // the corner itself: all the steering, and the ramp again after it
                            return;
                        }
                        if (now - _changedAt >= _policy.minHoldSeconds && _policy.IsStraight(c) && _policy.HasRunway(c)
                            && !_policy.BrakePoint(c, decel) && _policy.PaysBack(c, _action.AccelerationPerSecond, decel))
                        {
                            Set(Mode.Ramp, now);   // round the corner on the ramp: the next straight is long enough
                            return;
                        }
                        Rearm(now);
                        var transformer = _status.VesselTransformer;
                        // The speed the ramp is tracking toward, as the transformer will use it (never re-derived):
                        // any target under the speed bleeds at the full BleedPerSecond, so holding the target AT the
                        // corner speed brakes at full rate and stops there.
                        float tracked = transformer != null ? transformer.CurrentThrottleTarget : c.Speed;
                        float floor = 1f - Mathf.Max(0f, Grace - _policy.brakeSteerFloor);
                        _throttle = GovernThrottle(_throttle, tracked, _wanted,
                                                   _policy.throttleSlewPerSecond, Time.deltaTime, floor);
                        return;
                }
            }

            public override void Release()
            {
                if (_mode != Mode.Off) Set(Mode.Off, Time.time);
            }

            void Set(Mode mode, float now)
            {
                var was = _mode;
                _mode = mode;
                _changedAt = now;
                if (mode == Mode.Brake) _throttle = 1f;
                if (mode == Mode.Off) End();
                else if (was == Mode.Off) Arm(now);
            }

            // The grading lets the ramp fall off past its grace band; while this driver still wants it, press it again.
            void Rearm(float now)
            {
                if (_executor && !_executor.IsEngaged && now - _armedAt >= _policy.minRearmSeconds) Arm(now);
            }

            void Arm(float now)
            {
                _armedAt = now;
                _action.StartAction(_executors, _status);
            }

            // An explicit end: a graded ramp's own release is the top of its slope, not an end
            // (RampBoostActionExecutor.ReleaseGesture), so letting go of the gesture would bleed nothing.
            void End()
            {
                if (_executor) _executor.End();
                else _action.StopAction(_executors, _status);
            }
        }
    }
}
