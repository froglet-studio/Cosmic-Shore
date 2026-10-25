using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// AI boost for a hull that burns FUEL PELLETS (<see cref="ConsumeBoostActionSO"/> - the
    /// Serpent): light a pellet on a straight, stack further pellets only while the straight is
    /// long enough to carry them, and never light one into a turn - a lit pellet cannot be put out,
    /// so a pellet spent before a corner is a pellet the next straight does not have.
    ///
    /// Reads the tank and the burn count off the hull's own <see cref="ConsumeBoostActionExecutor"/>
    /// (the HUD's source), so pellet capacity, cost and regen stay authored in one place.
    /// Replaces the blind one-pellet-per-8.1 s cycler entry the Serpent shipped with.
    /// </summary>
    [CreateAssetMenu(fileName = "PelletBoostAIPolicy", menuName = "ScriptableObjects/AI/Boost Policy/Pellet Boost")]
    public sealed class PelletBoostAIPolicySO : AIBoostPolicySO
    {
        [Header("Pellets")]
        [Tooltip("Most pellets this pilot lets burn at once. Each one adds the same increment of " +
                 "speed, so stacking is how a long straight is won - and how a short one is overshot.")]
        [SerializeField, Min(1)] int maxConcurrentBurns = 3;
        [Tooltip("Shortest gap between two lights, seconds.")]
        [SerializeField, Min(0f)] float minSecondsBetweenPellets = 0.75f;
        [Tooltip("Whole pellets kept in the tank, never burned by the AI - a reserve for the start " +
                 "of the next straight. 0 spends the tank.")]
        [SerializeField, Min(0)] int reservePellets;

        public override AIBoostDriver CreateDriver(IVesselStatus status, ActionExecutorRegistry executors)
        {
            var handler = status?.ActionHandler;
            if (handler == null || !handler.TryGetBoundAction<ConsumeBoostActionSO>(out var action, out _)) return null;
            var executor = executors ? executors.Get<ConsumeBoostActionExecutor>() : null;
            if (!executor) return null;
            return new Driver(this, action, executor, status, executors);
        }

        sealed class Driver : AIBoostDriver
        {
            readonly PelletBoostAIPolicySO _policy;
            readonly ConsumeBoostActionSO _action;
            readonly ConsumeBoostActionExecutor _executor;
            readonly IVesselStatus _status;
            readonly ActionExecutorRegistry _executors;

            float _nextLightAt;

            public Driver(PelletBoostAIPolicySO policy, ConsumeBoostActionSO action,
                          ConsumeBoostActionExecutor executor, IVesselStatus status,
                          ActionExecutorRegistry executors)
            {
                _policy = policy;
                _action = action;
                _executor = executor;
                _status = status;
                _executors = executors;
            }

            public override bool Drives(ShipActionSO action) => action is ConsumeBoostActionSO;

            public override void Tick(in AIBoostContext c)
            {
                float now = Time.time;
                if (now < _nextLightAt || !_executor) return;
                if (!_policy.IsStraight(c)) return;

                int burning = _executor.BurningCount;
                if (burning >= _policy.maxConcurrentBurns) return;

                // The n-th concurrent pellet needs n engage-runways of straight. Runway is measured
                // at the CURRENT speed, which the burning pellets have already raised, so stacking
                // tapers off by itself as the objective (and the turn after it) approaches.
                if (!_policy.HasRunway(c, burning + 1)) return;

                if (_executor.PelletsHeld < 1f + _policy.reservePellets) return;

                // One press is one pellet; the release is a no-op on this ability (a lit pellet
                // burns out on its own clock), sent anyway so the press/release pair is honest.
                _action.StartAction(_executors, _status);
                _action.StopAction(_executors, _status);
                _nextLightAt = now + _policy.minSecondsBetweenPellets;
            }

            // Nothing is held: a pellet already lit burns out exactly as a human's does.
            public override void Release() { }
        }
    }
}
