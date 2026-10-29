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
    ///
    /// <para><b>Clear the way</b> (optional, 2026-10-26). With <see cref="clearWayRocket"/> set, this driver also fires
    /// that rocket itself, at the objective, instead of AIPilot's blind timer (2 s every 5 s, wherever the hull points):
    /// once per objective, when it is straight ahead of the nose and inside the rocket's reach, with the ammunition to
    /// pay for it. Yash, 2026-10-26: "the sparrow can use its shooting ability to clear up space in front of you for it
    /// to race down the track" - in Breakwater a rocket blows a 50 u hole in the station's danger weave around the 18 u
    /// eye. Empty in the base asset, so every mode keeps the timer; Breakwater's variant
    /// (<c>Resources/AIBoostPolicies/SparrowAIBoostPolicy_Breakwater</c>) turns it on.</para>
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

        [Header("Clear the way (optional)")]
        [Tooltip("A forward rocket this driver fires at the objective (the Sparrow's SkyBurstGunAction), taking it off " +
                 "AIPilot's ability timer. Empty: the rocket stays on the timer, as before.")]
        [SerializeField] FireGunActionSO clearWayRocket;
        [Tooltip("Fire only when the objective is at most this far, units: inside the rocket's reach (speed x 2T/pi, " +
                 "229 u for SkyBurst's 120 u/s for 3 s), so it bursts in the station rather than short of it.")]
        [SerializeField, Min(1f)] float rocketMaxRange = 220f;
        [Tooltip("...and at least this far, units: a rocket fired from closer bursts in the hull's own face.")]
        [SerializeField, Min(0f)] float rocketMinRange = 60f;
        [Tooltip("Fire only with the objective within this many degrees of the nose (the round leaves along it).")]
        [SerializeField, Range(0f, 45f)] float rocketAimDegrees = 4f;
        [Tooltip("Shortest gap between two rockets, seconds.")]
        [SerializeField, Min(0.1f)] float rocketCooldownSeconds = 1f;

        public FireGunActionSO ClearWayRocket => clearWayRocket;

        /// <summary>A rocket at the objective now: in reach, on the nose, paid for. Pure, for the tests (the driver
        /// adds once-per-objective and the cooldown).</summary>
        public bool RocketShot(in AIBoostContext c, float ammo, float ammoCost) =>
            clearWayRocket && !c.Restricted && !c.BreakingOrbit
            && c.Distance <= rocketMaxRange && c.Distance >= rocketMinRange
            && Vector3.Angle(c.Forward, c.ToTarget) <= rocketAimDegrees
            && ammo >= ammoCost;

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
            float _rocketAt = float.NegativeInfinity;
            Vector3 _rocketObjective = new(float.NaN, float.NaN, float.NaN);

            public Driver(HoldBoostAIPolicySO policy, BoostActionSO action, IVesselStatus status,
                          ActionExecutorRegistry executors)
            {
                _policy = policy;
                _action = action;
                _status = status;
                _executors = executors;
            }

            public override bool Drives(ShipActionSO action) =>
                action is BoostActionSO || (_policy.clearWayRocket && action == _policy.clearWayRocket);

            public override void Tick(in AIBoostContext c)
            {
                float now = Time.time;
                TickRocket(c, now);
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

            /// <summary>Clear the way: one rocket per objective (a station), when <see cref="RocketShot"/> says so.</summary>
            void TickRocket(in AIBoostContext c, float now)
            {
                var rocket = _policy.clearWayRocket;
                if (!rocket || now - _rocketAt < _policy.rocketCooldownSeconds) return;
                Vector3 objective = _status.Transform.position + c.ToTarget;
                if ((objective - _rocketObjective).sqrMagnitude < 1f) return;   // this station already has its hole
                if (!_policy.RocketShot(c, Ammo(rocket.AmmoIndex), rocket.AmmoCost)) return;
                VesselActionTape.Start(rocket, _executors, _status);
                _rocketAt = now;
                _rocketObjective = objective;
            }

            float Ammo(int index)
            {
                var resources = _status.ResourceSystem;
                if (!resources || (uint)index >= (uint)resources.Resources.Count) return 0f;
                var res = resources.Resources[index];
                return res != null ? res.CurrentAmount : 0f;
            }

            void Set(bool on, float now)
            {
                _held = on;
                _changedAt = now;
                if (on) VesselActionTape.Start(_action, _executors, _status);
                else VesselActionTape.Stop(_action, _executors, _status);
            }
        }
    }
}
