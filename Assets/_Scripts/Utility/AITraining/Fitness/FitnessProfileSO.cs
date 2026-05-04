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
