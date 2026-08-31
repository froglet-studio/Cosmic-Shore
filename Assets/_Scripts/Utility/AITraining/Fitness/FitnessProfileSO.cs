using System;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Utility.AITraining
{
    /// <summary>
    /// Per-game fitness recipe. Each row picks one IFitnessComponent and a weight.
    /// The runner instantiates the chosen components once per episode and aggregates
    /// their evaluations into the TrainingFitness breakdown.
    ///
    /// Adding a new fitness component is a two-step process:
    ///   1. Implement IFitnessComponent.
    ///   2. Add its enum entry below.
    /// Game designers then pick + weight it from the inspector.
    /// </summary>
    [CreateAssetMenu(
        fileName = "FitnessProfile",
        menuName = "ScriptableObjects/AI Training/Fitness Profile",
        order = 200)]
    public class FitnessProfileSO : ScriptableObject
    {
        public enum ComponentKind
        {
            ObjectiveProgress = 0,
            CrystalCollection = 1,
            EnemyVesselCollisions = 2,
            JoustCollisions = 3,
            VolumeCreated = 4,
            VolumeRestored = 5,
            VolumeDestroyedHostile = 6,
            VolumeDestroyedFriendlyPenalty = 7,
            CollisionPenalty = 8,
            BoostUseBonus = 9,
            TimePenalty = 10,
            SurvivalBonus = 11,
            AbilityUseBonus = 12,
            ScoreFromRoundStats = 13,
            DistanceTravelled = 14,
            HighSpeedTime = 15,
            CombatPoints = 16,          // Dog Fight gunnery (bullets ×1, missiles ×50)
            LifeformsKilled = 17,       // Wildlife Liberation / Blitz ecology kills
            GoalsScored = 18,           // Astro League / Scarab Scramble goals
            HostilePrismsDestroyed = 19,// Rampage / Ribcage / Salvo demolition
        }

        [Serializable]
        public struct Entry
        {
            public ComponentKind Kind;
            public float Weight;
            [Tooltip("Optional label shown in fitness breakdowns. Defaults to the kind name.")]
            public string Label;
        }

        [Header("Description")]
        [TextArea] public string Description;

        [Header("Components")]
        public List<Entry> Entries = new();

        /// <summary>
        /// Called by Unity at asset creation. Pre-populates with a sensible
        /// "race + collect + don't crash" recipe so a fresh asset trains
        /// usefully out of the box for crystal-collection minigames.
        /// </summary>
        void Reset()
        {
            ApplyRacingDefaults();
        }

        public void ApplyRacingDefaults()
        {
            Description = "Default racing/collection recipe: rewards crystal pickup, score, and " +
                          "high-speed time; penalizes elapsed time and friendly-fire damage.";
            Entries = new List<Entry>
            {
                new() { Kind = ComponentKind.CrystalCollection,              Weight = 100f, Label = "Crystals" },
                new() { Kind = ComponentKind.ScoreFromRoundStats,            Weight = 0.1f, Label = "Score" },
                new() { Kind = ComponentKind.BoostUseBonus,                  Weight = 5f,   Label = "BoostTime" },
                new() { Kind = ComponentKind.TimePenalty,                    Weight = 1f,   Label = "TimePenalty" },
                new() { Kind = ComponentKind.VolumeDestroyedFriendlyPenalty, Weight = 50f,  Label = "FriendlyFire" },
            };
        }

        public void ApplyJoustDefaults()
        {
            Description = "Joust / vessel-combat recipe: rewards enemy collisions and survival, " +
                          "penalizes time so passive pilots lose to aggressive ones.";
            Entries = new List<Entry>
            {
                new() { Kind = ComponentKind.JoustCollisions,            Weight = 100f, Label = "JoustHits" },
                new() { Kind = ComponentKind.EnemyVesselCollisions,      Weight = 30f,  Label = "EnemyContact" },
                new() { Kind = ComponentKind.SurvivalBonus,              Weight = 2f,   Label = "Survived" },
                new() { Kind = ComponentKind.AbilityUseBonus,            Weight = 4f,   Label = "AbilityUse" },
                new() { Kind = ComponentKind.TimePenalty,                Weight = 0.5f, Label = "TimePenalty" },
            };
        }

        public void ApplyCellularCaptureDefaults()
        {
            Description = "Crystal Capture / cell-control recipe: rewards volume created and " +
                          "hostile-volume destroyed; heavily penalizes friendly-fire damage.";
            Entries = new List<Entry>
            {
                new() { Kind = ComponentKind.VolumeCreated,                  Weight = 50f,  Label = "VolumeBuilt" },
                new() { Kind = ComponentKind.VolumeDestroyedHostile,         Weight = 30f,  Label = "VolumeKilledEnemy" },
                new() { Kind = ComponentKind.VolumeRestored,                 Weight = 20f,  Label = "VolumeRestored" },
                new() { Kind = ComponentKind.VolumeDestroyedFriendlyPenalty, Weight = 80f,  Label = "FriendlyFire" },
                new() { Kind = ComponentKind.CrystalCollection,              Weight = 10f,  Label = "Crystals" },
                new() { Kind = ComponentKind.TimePenalty,                    Weight = 0.5f, Label = "TimePenalty" },
            };
        }

        public void ApplyFreestyleDefaults()
        {
            Description = "Freestyle / exploration recipe: rewards distance, ability use, and " +
                          "high-speed time so the AI flies expressively rather than sitting still.";
            Entries = new List<Entry>
            {
                new() { Kind = ComponentKind.DistanceTravelled, Weight = 1f,   Label = "Distance" },
                new() { Kind = ComponentKind.HighSpeedTime,     Weight = 5f,   Label = "FastTime" },
                new() { Kind = ComponentKind.BoostUseBonus,     Weight = 3f,   Label = "Boost" },
                new() { Kind = ComponentKind.AbilityUseBonus,   Weight = 2f,   Label = "Abilities" },
                new() { Kind = ComponentKind.SurvivalBonus,     Weight = 0.5f, Label = "Survived" },
            };
        }

        public void ApplyGunneryDefaults()
        {
            Description = "Dog Fight / Salvo recipe: rewards combat points and demolition, " +
                          "penalizes time so passive pilots lose to hunters.";
            Entries = new List<Entry>
            {
                new() { Kind = ComponentKind.CombatPoints,           Weight = 10f,  Label = "CombatPoints" },
                new() { Kind = ComponentKind.HostilePrismsDestroyed, Weight = 0.5f, Label = "Demolition" },
                new() { Kind = ComponentKind.SurvivalBonus,          Weight = 1f,   Label = "Survived" },
                new() { Kind = ComponentKind.TimePenalty,            Weight = 0.5f, Label = "TimePenalty" },
            };
        }

        public void ApplyHuntDefaults()
        {
            Description = "Wildlife Liberation / Blitz recipe: rewards ecology kills and crystal " +
                          "pickup from the hearts those kills drop.";
            Entries = new List<Entry>
            {
                new() { Kind = ComponentKind.LifeformsKilled,   Weight = 50f, Label = "Kills" },
                new() { Kind = ComponentKind.CrystalCollection, Weight = 10f, Label = "Hearts" },
                new() { Kind = ComponentKind.TimePenalty,       Weight = 1f,  Label = "TimePenalty" },
            };
        }

        public void ApplyCourtDefaults()
        {
            Description = "Astro League / Scarab Scramble recipe: goals above all, with a touch of " +
                          "pace so the AI contests the ball rather than waiting for it.";
            Entries = new List<Entry>
            {
                new() { Kind = ComponentKind.GoalsScored,   Weight = 100f, Label = "Goals" },
                new() { Kind = ComponentKind.HighSpeedTime, Weight = 2f,   Label = "Pace" },
                new() { Kind = ComponentKind.TimePenalty,   Weight = 0.5f, Label = "TimePenalty" },
            };
        }

        /// <summary>
        /// Co-op vs AI recipe (2v2 Co-op, WildlifeBlitz co-op): the AI teammate is judged on
        /// contributing to the shared objective WITHOUT hoarding it — crystals and volume score,
        /// friendly damage is punished hard, and survival matters because a dead teammate helps
        /// nobody. Deliberately no time penalty: a co-op partner that rushes the match ends the
        /// human's fun.
        /// </summary>
        public void ApplyCoOpTeammateDefaults()
        {
            Description = "Co-op teammate recipe: shared-objective contribution, zero tolerance for " +
                          "friendly fire, no rush incentive — a partner, not a competitor.";
            Entries = new List<Entry>
            {
                new() { Kind = ComponentKind.CrystalCollection,              Weight = 40f, Label = "Crystals" },
                new() { Kind = ComponentKind.VolumeCreated,                  Weight = 10f, Label = "VolumeBuilt" },
                new() { Kind = ComponentKind.VolumeRestored,                 Weight = 20f, Label = "Restored" },
                new() { Kind = ComponentKind.LifeformsKilled,                Weight = 20f, Label = "Kills" },
                new() { Kind = ComponentKind.VolumeDestroyedFriendlyPenalty, Weight = 120f, Label = "FriendlyFire" },
                new() { Kind = ComponentKind.SurvivalBonus,                  Weight = 2f,  Label = "Survived" },
            };
        }

        public List<IFitnessComponent> Build()
        {
            var built = new List<IFitnessComponent>(Entries.Count);
            foreach (var e in Entries)
            {
                var c = FitnessComponentFactory.Create(e.Kind, string.IsNullOrEmpty(e.Label) ? e.Kind.ToString() : e.Label);
                if (c != null) built.Add(c);
            }
            return built;
        }

        public IReadOnlyList<Entry> Build_Entries => Entries;
    }
}
