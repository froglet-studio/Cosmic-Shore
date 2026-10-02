using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Utility.AITraining
{
    /// <summary>
    /// Discharges charged boost when the path ahead is clear and the vessel is
    /// pointed at the objective. Triggers FullSpeedStraightAction.
    /// </summary>
    public class BoostManagementPolicy : IDecisionPolicy
    {
        public string ModuleName => "BoostManagement";

        const string GeneMinCharge = "boost.min_charge";
        const string GeneMinClear = "boost.min_clear_distance";
        const string GeneLockDot = "boost.lock_dot";
        const string GeneHold = "boost.hold_seconds";
        const string GeneCooldown = "boost.cooldown";

        float _minCharge;
        float _minClear;
        float _lockDot;
        float _hold;
        float _cooldown;

        bool _engaged;
        float _engagedSince;
        float _nextEngageTime;

        public void RegisterGenes()
        {
            GeneRegistry.Register(ModuleName, new GeneSpec(GeneMinCharge, 0f, 1f, 0.5f));
            GeneRegistry.Register(ModuleName, new GeneSpec(GeneMinClear, 5f, 80f, 30f));
            GeneRegistry.Register(ModuleName, new GeneSpec(GeneLockDot, 0.85f, 0.999f, 0.95f));
            GeneRegistry.Register(ModuleName, new GeneSpec(GeneHold, 0.2f, 3f, 1f));
            GeneRegistry.Register(ModuleName, new GeneSpec(GeneCooldown, 0.5f, 6f, 2f));
        }

        public void OnEpisodeStart(TrainingGenome genome)
        {
            _minCharge = genome.Get(GeneMinCharge);
            _minClear = genome.Get(GeneMinClear);
            _lockDot = genome.Get(GeneLockDot);
            _hold = genome.Get(GeneHold);
            _cooldown = genome.Get(GeneCooldown);
            _engaged = false;
            _engagedSince = 0f;
            _nextEngageTime = 0f;
        }

        public DecisionOutput Decide(DecisionContext ctx)
        {
            DecisionOutput output = DecisionOutput.Zero;

            if (_engaged)
            {
                if (ctx.EpisodeTime - _engagedSince >= _hold)
                {
                    output = output.RequestStop(InputEvents.FullSpeedStraightAction);
                    _engaged = false;
                    _nextEngageTime = ctx.EpisodeTime + _cooldown;
                }
                return output;
            }

            if (ctx.EpisodeTime < _nextEngageTime) return output;
            if (ctx.ChargedBoostCharge < _minCharge) return output;
            if (!ctx.HasTarget) return output;
            if (ctx.DotForwardObjective < _lockDot) return output;

            // Path clearance check: any prism within minClear in our forward arc kills the boost.
            foreach (var p in ctx.NearbyPrisms)
            {
                if (p.Range > _minClear) continue;
                Vector3 dir = (p.Position - ctx.Position).normalized;
                if (Vector3.Dot(dir, ctx.Forward) > 0.6f) return output;
            }

            output = output.RequestStart(InputEvents.FullSpeedStraightAction);
            _engaged = true;
            _engagedSince = ctx.EpisodeTime;
            return output;
        }
    }
}
