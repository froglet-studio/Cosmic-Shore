using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Utility.AITraining
{
    /// <summary>
    /// All built-in IFitnessComponent implementations. Most read straight off
    /// IRoundStats — the platform's own ledger of what a pilot did — because a
    /// stat the game already counts is a stat the scoring rules already care
    /// about, which keeps fitness aligned with what actually wins matches.
    /// The handful of frame-sampled components read the trainee's
    /// EpisodeObservation instead.
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
                FitnessProfileSO.ComponentKind.CombatPoints => new CombatPointsFitness(label),
                FitnessProfileSO.ComponentKind.LifeformsKilled => new LifeformsKilledFitness(label),
                FitnessProfileSO.ComponentKind.GoalsScored => new GoalsScoredFitness(label),
                FitnessProfileSO.ComponentKind.HostilePrismsDestroyed => new HostilePrismsDestroyedFitness(label),
                _ => null
            };
        }
    }

    abstract class FitnessBase : IFitnessComponent
    {
        public string Label { get; }
        protected FitnessBase(string label) { Label = label; }
        public virtual void OnEpisodeStart(EpisodeObservation obs) { }
        public virtual void OnFrame(EpisodeObservation obs) { }
        public abstract float Evaluate(EpisodeObservation obs, IRoundStats roundStats);
    }

    class ObjectiveProgressFitness : FitnessBase
    {
        public ObjectiveProgressFitness(string l) : base(l) { }
        public override float Evaluate(EpisodeObservation obs, IRoundStats r)
        {
            if (r == null) return 0f;
            return r.CrystalsCollected + r.Score * 0.1f;
        }
    }

    class CrystalCollectionFitness : FitnessBase
    {
        public CrystalCollectionFitness(string l) : base(l) { }
        public override float Evaluate(EpisodeObservation obs, IRoundStats r) => r?.CrystalsCollected ?? 0f;
    }

    class EnemyVesselCollisionsFitness : FitnessBase
    {
        public EnemyVesselCollisionsFitness(string l) : base(l) { }
        public override float Evaluate(EpisodeObservation obs, IRoundStats r) => r?.SkimmerShipCollisions ?? 0f;
    }

    class JoustCollisionsFitness : FitnessBase
    {
        public JoustCollisionsFitness(string l) : base(l) { }
        public override float Evaluate(EpisodeObservation obs, IRoundStats r) => r?.JoustCollisions ?? 0f;
    }

    class VolumeCreatedFitness : FitnessBase
    {
        public VolumeCreatedFitness(string l) : base(l) { }
        public override float Evaluate(EpisodeObservation obs, IRoundStats r) => r?.VolumeCreated ?? 0f;
    }

    class VolumeRestoredFitness : FitnessBase
    {
        public VolumeRestoredFitness(string l) : base(l) { }
        public override float Evaluate(EpisodeObservation obs, IRoundStats r) => r?.VolumeRestored ?? 0f;
    }

    class VolumeDestroyedHostileFitness : FitnessBase
    {
        public VolumeDestroyedHostileFitness(string l) : base(l) { }
        public override float Evaluate(EpisodeObservation obs, IRoundStats r) => r?.HostileVolumeDestroyed ?? 0f;
    }

    class VolumeDestroyedFriendlyPenaltyFitness : FitnessBase
    {
        public VolumeDestroyedFriendlyPenaltyFitness(string l) : base(l) { }
        public override float Evaluate(EpisodeObservation obs, IRoundStats r)
            => -(r?.FriendlyVolumeDestroyed ?? 0f);
    }

    class CollisionPenaltyFitness : FitnessBase
    {
        public CollisionPenaltyFitness(string l) : base(l) { }
        public override float Evaluate(EpisodeObservation obs, IRoundStats r)
            => -(r?.SkimmerShipCollisions ?? 0);
    }

    class BoostUseBonusFitness : FitnessBase
    {
        float _boostSeconds;
        public BoostUseBonusFitness(string l) : base(l) { }
        public override void OnEpisodeStart(EpisodeObservation obs) { _boostSeconds = 0f; }
        public override void OnFrame(EpisodeObservation obs)
        {
            if (obs.IsBoosting) _boostSeconds += Time.deltaTime;
        }
        public override float Evaluate(EpisodeObservation obs, IRoundStats r) => _boostSeconds;
    }

    class TimePenaltyFitness : FitnessBase
    {
        public TimePenaltyFitness(string l) : base(l) { }
        public override float Evaluate(EpisodeObservation obs, IRoundStats r) => -(obs?.EpisodeTime ?? 0f);
    }

    class SurvivalBonusFitness : FitnessBase
    {
        public SurvivalBonusFitness(string l) : base(l) { }
        public override float Evaluate(EpisodeObservation obs, IRoundStats r) => obs?.EpisodeTime ?? 0f;
    }

    class AbilityUseBonusFitness : FitnessBase
    {
        public AbilityUseBonusFitness(string l) : base(l) { }
        public override float Evaluate(EpisodeObservation obs, IRoundStats r)
        {
            if (r == null) return 0f;
            return (r.Button1AbilityActiveTime + r.Button2AbilityActiveTime + r.Button3AbilityActiveTime) * 0.5f;
        }
    }

    class ScoreFromRoundStatsFitness : FitnessBase
    {
        public ScoreFromRoundStatsFitness(string l) : base(l) { }
        public override float Evaluate(EpisodeObservation obs, IRoundStats r) => r?.Score ?? 0f;
    }

    class DistanceTravelledFitness : FitnessBase
    {
        Vector3 _last;
        float _accum;
        bool _seeded;
        public DistanceTravelledFitness(string l) : base(l) { }
        public override void OnEpisodeStart(EpisodeObservation obs)
        {
            _last = obs?.Position ?? Vector3.zero;
            _accum = 0f;
            _seeded = obs != null;
        }
        public override void OnFrame(EpisodeObservation obs)
        {
            if (!_seeded) { _last = obs.Position; _seeded = true; return; }
            _accum += Vector3.Distance(_last, obs.Position);
            _last = obs.Position;
        }
        public override float Evaluate(EpisodeObservation obs, IRoundStats r) => _accum;
    }

    class HighSpeedTimeFitness : FitnessBase
    {
        const float HighSpeedThreshold = 30f;
        float _accum;
        public HighSpeedTimeFitness(string l) : base(l) { }
        public override void OnEpisodeStart(EpisodeObservation obs) { _accum = 0f; }
        public override void OnFrame(EpisodeObservation obs)
        {
            if (obs.Speed >= HighSpeedThreshold) _accum += Time.deltaTime;
        }
        public override float Evaluate(EpisodeObservation obs, IRoundStats r) => _accum;
    }

    // ── New-era stats (Dog Fight, Wildlife Liberation, Astro League / Scramble,
    //    Rampage / Ribcage / Salvo) ────────────────────────────────────

    class CombatPointsFitness : FitnessBase
    {
        public CombatPointsFitness(string l) : base(l) { }
        public override float Evaluate(EpisodeObservation obs, IRoundStats r) => r?.CombatPoints ?? 0f;
    }

    class LifeformsKilledFitness : FitnessBase
    {
        public LifeformsKilledFitness(string l) : base(l) { }
        public override float Evaluate(EpisodeObservation obs, IRoundStats r) => r?.LifeformsKilled ?? 0f;
    }

    class GoalsScoredFitness : FitnessBase
    {
        public GoalsScoredFitness(string l) : base(l) { }
        public override float Evaluate(EpisodeObservation obs, IRoundStats r) => r?.GoalsScored ?? 0f;
    }

    class HostilePrismsDestroyedFitness : FitnessBase
    {
        public HostilePrismsDestroyedFitness(string l) : base(l) { }
        public override float Evaluate(EpisodeObservation obs, IRoundStats r) => r?.HostilePrismsDestroyed ?? 0f;
    }
}
