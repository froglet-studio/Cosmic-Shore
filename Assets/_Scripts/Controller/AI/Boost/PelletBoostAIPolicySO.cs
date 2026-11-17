using UnityEngine;
using CosmicShore.UI;

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

        [Header("Refuel (the wall)")]
        [Tooltip("The tank refills only while the Serpent's wall eats its own trail " +
                 "(R_VesselActions/SERPENT_FUEL_PELLETS.md). At or under this many pellets the pilot " +
                 "presses the cloak itself to lay a wall and refuel, instead of the ability cycler " +
                 "pressing it on a clock (often with a full tank, where the refill is lost). " +
                 "Negative: off, the cycler keeps the cloak (the behaviour before this field).")]
        [SerializeField, Range(-1f, 5f)] float refuelBelowPellets = -1f;
        [Tooltip("Seconds after a refuel cloak with no pellet lit: the pilot cruises along the wall's " +
                 "edge so its trail lands in the wall, rather than boosting away from it.")]
        [SerializeField, Min(0f)] float refuelCoastSeconds;
        [Tooltip("Pacing: the gap between two lights while the tank is at or under the refuel line, " +
                 "seconds. 0 uses Min Seconds Between Pellets everywhere.")]
        [SerializeField, Min(0f)] float lowTankSecondsBetweenPellets;

        bool Refuels => refuelBelowPellets >= 0f;

        public override AIBoostDriver CreateDriver(IVesselStatus status, ActionExecutorRegistry executors)
        {
            var handler = status?.ActionHandler;
            if (handler == null || !handler.TryGetBoundAction<ConsumeBoostActionSO>(out var action, out _)) return null;
            var executor = executors ? executors.Get<ConsumeBoostActionExecutor>() : null;
            if (!executor) return null;
            CloakSeedWallActionSO cloak = null;
            if (Refuels) handler.TryGetBoundAction(out cloak, out _);
            return new Driver(this, action, executor, status, executors, cloak);
        }

        sealed class Driver : AIBoostDriver
        {
            readonly PelletBoostAIPolicySO _policy;
            readonly ConsumeBoostActionSO _action;
            readonly ConsumeBoostActionExecutor _executor;
            readonly IVesselStatus _status;
            readonly ActionExecutorRegistry _executors;
            readonly CloakSeedWallActionSO _cloak;

            float _nextLightAt;
            float _nextCloakAt;
            float _coastUntil;

            public Driver(PelletBoostAIPolicySO policy, ConsumeBoostActionSO action,
                          ConsumeBoostActionExecutor executor, IVesselStatus status,
                          ActionExecutorRegistry executors, CloakSeedWallActionSO cloak)
            {
                _policy = policy;
                _action = action;
                _executor = executor;
                _status = status;
                _executors = executors;
                _cloak = cloak;
            }

            // With refuel on, the cloak is this driver's too: the cycler would otherwise press it on its own clock.
            public override bool Drives(ShipActionSO action) =>
                action is ConsumeBoostActionSO || (_cloak && action is CloakSeedWallActionSO);

            bool LowTank => _executor.PelletsHeld <= _policy.refuelBelowPellets;

            public override void Tick(in AIBoostContext c)
            {
                float now = Time.time;
                if (!_executor) return;
                TickRefuel(now, c);
                if (now < _nextLightAt || now < _coastUntil) return;
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
                VesselActionTape.Start(_action, _executors, _status);
                VesselActionTape.Stop(_action, _executors, _status);
                float gap = _cloak && _policy.lowTankSecondsBetweenPellets > 0f && LowTank
                    ? _policy.lowTankSecondsBetweenPellets
                    : _policy.minSecondsBetweenPellets;
                _nextLightAt = now + gap;
            }

            /// <summary>Lay a wall when the tank is low, the way a person refuels: press the cloak (its
            /// executor ignores a press while it runs or cools down), then hold the pellets for
            /// <see cref="refuelCoastSeconds"/> so the trail streams into the wall.</summary>
            void TickRefuel(float now, in AIBoostContext c)
            {
                if (!_cloak || now < _nextCloakAt || c.Restricted || !LowTank) return;
                VesselActionTape.Start(_cloak, _executors, _status);
                VesselActionTape.Stop(_cloak, _executors, _status);
                _nextCloakAt = now + _cloak.CooldownSeconds + 0.1f;
                _coastUntil = now + _policy.refuelCoastSeconds;
            }

            // Nothing is held: a pellet already lit burns out exactly as a human's does.
            public override void Release() { }
        }
    }
}
