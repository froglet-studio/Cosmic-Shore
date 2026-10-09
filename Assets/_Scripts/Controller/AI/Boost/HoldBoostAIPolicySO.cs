using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// AI boost for a hull whose boost is a free HOLD (<see cref="BoostActionSO"/> - the Sparrow's
    /// indefinite afterburner): hold it down the straight, let go before the turn. The resource it
    /// conserves is turning circle, not fuel - a boosted hull turns wider, which is the whole cost
    /// of an unlimited boost.
    ///
    /// Replaces the blind 2 s-on / 10 s-off cycler entry the Sparrow shipped with (the cycler skips
    /// the boost while this drives it; a mode in disabledInModes keeps the old cycle).
    /// </summary>
    [CreateAssetMenu(fileName = "HoldBoostAIPolicy", menuName = "ScriptableObjects/AI/Boost Policy/Hold Boost")]
    public sealed class HoldBoostAIPolicySO : AIBoostPolicySO
    {
        [Header("Hold")]
        [Tooltip("Shortest boost once engaged, seconds - so a straight that briefly fails the test " +
                 "does not stutter the boost (each press plays the boost-activate sound).")]
        [SerializeField, Min(0f)] float minHoldSeconds = 0.6f;
        [Tooltip("Shortest gap between boosts, seconds.")]
        [SerializeField, Min(0f)] float minRestSeconds = 0.4f;

        public override AIBoostDriver CreateDriver(IVesselStatus status, ActionExecutorRegistry executors)
        {
            var handler = status?.ActionHandler;
            if (handler == null || !handler.TryGetBoundAction<BoostActionSO>(out var action, out _)) return null;
            return new Driver(this, action, status, executors);
        }

        sealed class Driver : AIBoostDriver
        {
            readonly HoldBoostAIPolicySO _policy;
            readonly BoostActionSO _action;
            readonly IVesselStatus _status;
            readonly ActionExecutorRegistry _executors;

            bool _held;
            float _changedAt = float.NegativeInfinity;

            public Driver(HoldBoostAIPolicySO policy, BoostActionSO action, IVesselStatus status,
                          ActionExecutorRegistry executors)
            {
                _policy = policy;
                _action = action;
                _status = status;
                _executors = executors;
            }

            public override bool Drives(ShipActionSO action) => action is BoostActionSO;

            public override void Tick(in AIBoostContext c)
            {
                float now = Time.time;
                if (_held)
                {
                    // A stance or a stop ends the boost at once - BoostActionSO.StartAction clears
                    // IsStationary, so holding it through a stance would un-plant the vessel.
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
                if (_held) Set(false, Time.time);
            }

            void Set(bool on, float now)
            {
                _held = on;
                _changedAt = now;
                if (on) _action.StartAction(_executors, _status);
                else _action.StopAction(_executors, _status);
            }
        }
    }
}
