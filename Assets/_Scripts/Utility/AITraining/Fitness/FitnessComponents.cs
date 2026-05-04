using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Utility.AITraining
{
    /// <summary>
    /// All built-in IFitnessComponent implementations. Each is small enough to live
    /// in a single file; if any grows past ~80 lines split it out.
    /// </summary>
    public static class FitnessComponentFactory
    {
        public static IFitnessComponent Create(FitnessProfileSO.ComponentKind kind, string label)
        {
            return kind switch
            {
                FitnessProfileSO.ComponentKind.ObjectiveProgress => new ObjectiveProgressFitness(label),
                FitnessProfileSO.ComponentKind.CrystalCollection => new CrystalCollectionFitness(label),
                FitnessProfileSO.ComponentKind.EnemyVesselCollisions => new EnemyVesselCollisionsFitness(label),
                FitnessProfileSO.ComponentKind.JoustCollisions => new JoustCollisionsFitness(label),
                FitnessProfileSO.ComponentKind.VolumeCreated => new VolumeCreatedFitness(label),
                FitnessProfileSO.ComponentKind.VolumeRestored => new VolumeRestoredFitness(label),
                FitnessProfileSO.ComponentKind.VolumeDestroyedHostile => new VolumeDestroyedHostileFitness(label),
                FitnessProfileSO.ComponentKind.VolumeDestroyedFriendlyPenalty => new VolumeDestroyedFriendlyPenaltyFitness(label),
                FitnessProfileSO.ComponentKind.CollisionPenalty => new CollisionPenaltyFitness(label),
                FitnessProfileSO.ComponentKind.BoostUseBonus => new BoostUseBonusFitness(label),
                FitnessProfileSO.ComponentKind.TimePenalty => new TimePenaltyFitness(label),
                FitnessProfileSO.ComponentKind.SurvivalBonus => new SurvivalBonusFitness(label),
                FitnessProfileSO.ComponentKind.AbilityUseBonus => new AbilityUseBonusFitness(label),
                FitnessProfileSO.ComponentKind.ScoreFromRoundStats => new ScoreFromRoundStatsFitness(label),
                FitnessProfileSO.ComponentKind.DistanceTravelled => new DistanceTravelledFitness(label),
                FitnessProfileSO.ComponentKind.HighSpeedTime => new HighSpeedTimeFitness(label),
                _ => null
            };
        }
    }

    abstract class FitnessBase : IFitnessComponent
    {
        public string Label { get; }
        protected FitnessBase(string label) { Label = label; }
        public virtual void OnEpisodeStart(DecisionContext ctx) { }
        public virtual void OnFrame(DecisionContext ctx) { }
        public abstract float Evaluate(DecisionContext ctx, IRoundStats roundStats);
    }

    class ObjectiveProgressFitness : FitnessBase
    {
        // Crystals + score, normalized so weight ~1.0 produces values in [0,~50].
        public ObjectiveProgressFitness(string l) : base(l) { }
        public override float Evaluate(DecisionContext ctx, IRoundStats r)
        {
            if (r == null) return 0f;
            return r.CrystalsCollected + r.Score * 0.1f;
        }
    }

    class CrystalCollectionFitness : FitnessBase
    {
        public CrystalCollectionFitness(string l) : base(l) { }
        public override float Evaluate(DecisionContext ctx, IRoundStats r) => r?.CrystalsCollected ?? 0f;
    }

    class EnemyVesselCollisionsFitness : FitnessBase
    {
        public EnemyVesselCollisionsFitness(string l) : base(l) { }
        public override float Evaluate(DecisionContext ctx, IRoundStats r) => r?.SkimmerShipCollisions ?? 0f;
    }

    class JoustCollisionsFitness : FitnessBase
    {
        public JoustCollisionsFitness(string l) : base(l) { }
        public override float Evaluate(DecisionContext ctx, IRoundStats r) => r?.JoustCollisions ?? 0f;
    }

    class VolumeCreatedFitness : FitnessBase
    {
        public VolumeCreatedFitness(string l) : base(l) { }
        public override float Evaluate(DecisionContext ctx, IRoundStats r) => r?.VolumeCreated ?? 0f;
    }

    class VolumeRestoredFitness : FitnessBase
    {
        public VolumeRestoredFitness(string l) : base(l) { }
        public override float Evaluate(DecisionContext ctx, IRoundStats r) => r?.VolumeRestored ?? 0f;
    }

    class VolumeDestroyedHostileFitness : FitnessBase
    {
        public VolumeDestroyedHostileFitness(string l) : base(l) { }
        public override float Evaluate(DecisionContext ctx, IRoundStats r) => r?.HostileVolumeDestroyed ?? 0f;
    }

    class VolumeDestroyedFriendlyPenaltyFitness : FitnessBase
    {
        public VolumeDestroyedFriendlyPenaltyFitness(string l) : base(l) { }
        public override float Evaluate(DecisionContext ctx, IRoundStats r)
            => -(r?.FriendlyVolumeDestroyed ?? 0f);
    }

    class CollisionPenaltyFitness : FitnessBase
    {
        // Tracks vessel-to-vessel collisions only at the moment; prism collisions are
        // not exposed via IRoundStats so we approximate by reading skimmer collisions
        // and treating them as proximity events the AI should avoid by default.
        int _baseline;
        public CollisionPenaltyFitness(string l) : base(l) { }
        public override void OnEpisodeStart(DecisionContext ctx) { _baseline = 0; }
        public override float Evaluate(DecisionContext ctx, IRoundStats r)
        {
            if (r == null) return 0f;
            return -(r.SkimmerShipCollisions - _baseline);
        }
    }

    class BoostUseBonusFitness : FitnessBase
    {
        float _highBoostSeconds;
        public BoostUseBonusFitness(string l) : base(l) { }
        public override void OnEpisodeStart(DecisionContext ctx) { _highBoostSeconds = 0f; }
        public override void OnFrame(DecisionContext ctx)
        {
            if (ctx.IsBoosting) _highBoostSeconds += Time.deltaTime;
        }
        public override float Evaluate(DecisionContext ctx, IRoundStats r) => _highBoostSeconds;
    }

    class TimePenaltyFitness : FitnessBase
    {
        public TimePenaltyFitness(string l) : base(l) { }
        public override float Evaluate(DecisionContext ctx, IRoundStats r) => -ctx.EpisodeTime;
    }

    class SurvivalBonusFitness : FitnessBase
    {
        public SurvivalBonusFitness(string l) : base(l) { }
        public override float Evaluate(DecisionContext ctx, IRoundStats r) => ctx.EpisodeTime;
    }

    class AbilityUseBonusFitness : FitnessBase
    {
        public AbilityUseBonusFitness(string l) : base(l) { }
        public override float Evaluate(DecisionContext ctx, IRoundStats r)
        {
            if (r == null) return 0f;
            return (r.Button1AbilityActiveTime + r.Button2AbilityActiveTime + r.Button3AbilityActiveTime) * 0.5f;
        }
    }

    class ScoreFromRoundStatsFitness : FitnessBase
    {
        public ScoreFromRoundStatsFitness(string l) : base(l) { }
        public override float Evaluate(DecisionContext ctx, IRoundStats r) => r?.Score ?? 0f;
    }

    class DistanceTravelledFitness : FitnessBase
    {
        Vector3 _last;
        float _accum;
        public DistanceTravelledFitness(string l) : base(l) { }
        public override void OnEpisodeStart(DecisionContext ctx) { _last = ctx.Position; _accum = 0f; }
        public override void OnFrame(DecisionContext ctx)
        {
            _accum += Vector3.Distance(_last, ctx.Position);
            _last = ctx.Position;
        }
        public override float Evaluate(DecisionContext ctx, IRoundStats r) => _accum;
    }

    class HighSpeedTimeFitness : FitnessBase
    {
        const float HighSpeedThreshold = 30f;
        float _accum;
        public HighSpeedTimeFitness(string l) : base(l) { }
        public override void OnEpisodeStart(DecisionContext ctx) { _accum = 0f; }
        public override void OnFrame(DecisionContext ctx)
        {
            if (ctx.Speed >= HighSpeedThreshold) _accum += Time.deltaTime;
        }
        public override float Evaluate(DecisionContext ctx, IRoundStats r) => _accum;
    }
}
