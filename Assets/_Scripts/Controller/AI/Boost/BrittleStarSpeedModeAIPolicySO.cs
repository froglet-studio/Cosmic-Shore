using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// AI boost for the Brittle Star, whose boost is a MODE: Speed Mode (<c>R_VesselActions/BRITTLE_STAR.md</c> §3.1).
    /// The fleet's shared law decides WHEN — a straight at the objective with runway left, let go before the turn (the
    /// base class's <see cref="AIBoostPolicySO.IsStraight"/> / <see cref="AIBoostPolicySO.HasRunway"/>) — and the
    /// executor's autopilot does the PRESSING, through the same replicated control a human presses, because the mode is
    /// one of three and only the executor knows the other two (<see cref="BrittleStarExecutor.SetAutopilotSpeedWish"/>).
    /// So this driver presses nothing: it states a wish each frame and the executor acts on it.
    /// </summary>
    [CreateAssetMenu(fileName = "BrittleStarAIBoostPolicy", menuName = "ScriptableObjects/AI/Boost Policy/Brittle Star Speed Mode")]
    public sealed class BrittleStarSpeedModeAIPolicySO : AIBoostPolicySO
    {
        [Header("Speed Mode")]
        [Tooltip("Shortest time a Speed Mode wish holds once raised, seconds — every switch plays the mode's cue and " +
                 "re-shapes the trail, so a straight that briefly fails the test must not flicker it.")]
        [SerializeField, Min(0f)] float minHoldSeconds = 1.2f;
        [Tooltip("Shortest time between wishes, seconds.")]
        [SerializeField, Min(0f)] float minRestSeconds = 0.8f;

        public override AIBoostDriver CreateDriver(IVesselStatus status, ActionExecutorRegistry executors)
        {
            var star = executors ? executors.Get<BrittleStarExecutor>() : null;
            return star ? new Driver(this, star) : null;
        }

        sealed class Driver : AIBoostDriver
        {
            readonly BrittleStarSpeedModeAIPolicySO _policy;
            readonly BrittleStarExecutor _star;
            bool _wish;
            float _changedAt = float.NegativeInfinity;

            public Driver(BrittleStarSpeedModeAIPolicySO policy, BrittleStarExecutor star)
            {
                _policy = policy;
                _star = star;
            }

            public override void Tick(in AIBoostContext c)
            {
                float now = Time.time;
                if (_wish)
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
                _wish = false;
                if (_star) _star.SetAutopilotSpeedWish(null);
            }

            void Set(bool on, float now)
            {
                _wish = on;
                _changedAt = now;
                if (_star) _star.SetAutopilotSpeedWish(on);
            }
        }
    }
}
