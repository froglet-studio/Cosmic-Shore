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

        // Crystal-chase engage window. Archive gen-2 keeps lock_dot≈0.92 /
        // hold≈0.35 / cooldown≈2.59 — too tight and too brief for ≤70s collection.
        // GeneRegistry is first-register-wins, so Decide overrides are the reliable
        // path (RegisterGenes floors alone only help a fresh Clear).
        const float CrystalLockDotCeiling = 0.5f;
        const float CrystalHoldFloor = 2.0f;
        const float CrystalCooldownCeiling = 0.25f;

        public void RegisterGenes()
        {
            // Archive gen-2 shipped lock_dot ≈ 0.989 and min_clear = 80 — boost almost never
            // engaged. Cap those so Get() clamps the chromosome onto a usable window.
            // Crystal chase still overrides lock/hold/cooldown in Decide (below).
            GeneRegistry.Register(ModuleName, new GeneSpec(GeneMinCharge, 0f, 1f, 0.4f));
            GeneRegistry.Register(ModuleName, new GeneSpec(GeneMinClear, 5f, 45f, 25f));
            GeneRegistry.Register(ModuleName, new GeneSpec(GeneLockDot, 0.4f, 0.96f, 0.5f));
            GeneRegistry.Register(ModuleName, new GeneSpec(GeneHold, 0.5f, 4f, 2f));
            GeneRegistry.Register(ModuleName, new GeneSpec(GeneCooldown, 0.15f, 6f, 0.25f));
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
            bool chasingCrystal = ctx.TargetKind == TargetKind.Crystal;
            float hold = chasingCrystal ? Mathf.Max(_hold, CrystalHoldFloor) : _hold;
            float cooldown = chasingCrystal ? Mathf.Min(_cooldown, CrystalCooldownCeiling) : _cooldown;
            float lockDot = chasingCrystal ? Mathf.Min(_lockDot, CrystalLockDotCeiling) : _lockDot;

            if (_engaged)
            {
                if (ctx.EpisodeTime - _engagedSince >= hold)
                {
                    output = output.RequestStop(InputEvents.FullSpeedStraightAction);
                    _engaged = false;
                    _nextEngageTime = ctx.EpisodeTime + cooldown;
                }
                return output;
            }

            if (ctx.EpisodeTime < _nextEngageTime) return output;
            if (ctx.ChargedBoostCharge < _minCharge) return output;
            if (!ctx.HasTarget) return output;
            if (ctx.DotForwardObjective < lockDot) return output;

            // Crystal chase skips path clearance — the same rule as ThrottleControl /
            // ObstacleAvoidance. Own trail and arena mass sit in the forward arc on
            // every skim-race line, so the NearbyPrisms loop would kill FullSpeedStraight
            // for the whole match and leave collection at cruise.
            if (!chasingCrystal)
            {
                foreach (var p in ctx.NearbyPrisms)
                {
                    if (p.Range > _minClear) continue;
                    Vector3 dir = (p.Position - ctx.Position).normalized;
                    if (Vector3.Dot(dir, ctx.Forward) > 0.6f) return output;
                }
            }

            output = output.RequestStart(InputEvents.FullSpeedStraightAction);
            _engaged = true;
            _engagedSince = ctx.EpisodeTime;
            return output;
        }
    }
}
