using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// AI boost for a hull whose speed is a RAMP (<see cref="RampBoostActionSO"/> - the Rhino): on a straight at the
    /// objective with runway ahead, press the ramp, hold the throttle full and keep the steering inside the ramp's
    /// full-power plateau, so small corrections toward the ring keep the whole ramp; let go before the turn, and the
    /// ramp winds itself down as the pilot steers again (<see cref="RampBoostActionExecutor"/> grades by the pilot's
    /// own straight-line deviation and ends past its grace band).
    ///
    /// <para><b>Why it exists</b> (2026-10-14 Regatta audit): the ramp starts only on <c>FullSpeedStraightAction</c>,
    /// which only a human input strategy raises, and the action handler drops presses while autopilot flies - so an
    /// AI Rhino never ramped at all and cruised at its AI throttle (about 19-50 u/s against a human's 840).
    /// REGATTA.md and AI_BOOST.md said "the ramp engages off a straight stick an AI naturally holds"; it does not.</para>
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

        public override AIBoostDriver CreateDriver(IVesselStatus status, ActionExecutorRegistry executors)
        {
            var handler = status?.ActionHandler;
            if (handler == null || !handler.TryGetBoundAction<RampBoostActionSO>(out var action, out _)) return null;
            return new Driver(this, action, status, executors);
        }

        sealed class Driver : AIBoostDriver
        {
            readonly RampBoostAIPolicySO _policy;
            readonly RampBoostActionSO _action;
            readonly IVesselStatus _status;
            readonly ActionExecutorRegistry _executors;

            bool _ramping;
            float _changedAt = float.NegativeInfinity;

            public Driver(RampBoostAIPolicySO policy, RampBoostActionSO action, IVesselStatus status,
                          ActionExecutorRegistry executors)
            {
                _policy = policy;
                _action = action;
                _status = status;
                _executors = executors;
            }

            public override bool Drives(ShipActionSO action) => action is RampBoostActionSO;

            public override bool HoldsFullThrottle => _ramping;

            public override float RotationLimit =>
                _ramping ? StraightLineGesture.EngageThreshold * _policy.steeringShareOfPlateau : -1f;

            public override string Status => _ramping
                ? $"ramping on the straight ({_status.Speed:F0} u/s, x{_status.BoostMultiplier:F1})"
                : "cruising - waiting for a straight with runway";

            public override void Tick(in AIBoostContext c)
            {
                float now = Time.time;
                if (_ramping)
                {
                    if (c.Restricted) { Set(false, now); return; }
                    if (now - _changedAt < _policy.minHoldSeconds) return;
                    if (!_policy.StillStraight(c) || _policy.TurnAhead(c)) Set(false, now);
                    return;
                }

                if (now - _changedAt < _policy.minRestSeconds) return;
                if (_policy.IsStraight(c) && _policy.HasRunway(c)) Set(true, now);
            }

            public override void Release()
            {
                if (_ramping) Set(false, Time.time);
            }

            void Set(bool on, float now)
            {
                _ramping = on;
                _changedAt = now;
                // StopAction is the gesture's lapse, not an end: the executor ends the ramp itself once the pilot
                // steers past its grace band, which it does as soon as this driver stops holding the line.
                if (on) _action.StartAction(_executors, _status);
                else _action.StopAction(_executors, _status);
            }
        }
    }
}
