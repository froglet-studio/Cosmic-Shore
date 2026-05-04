using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Utility.AITraining
{
    /// <summary>
    /// One row of work for the training runner: "in this scene, with this vessel,
    /// against this many AI opponents, optimize against this fitness profile."
    ///
    /// Multiple scenarios can target the same scene — they differ only in vessel,
    /// difficulty, and which fitness components they reward. The editor window
    /// schedules them in sequence (or in parallel if multiple scenes are open).
    /// </summary>
    [CreateAssetMenu(
        fileName = "TrainingScenario",
        menuName = "ScriptableObjects/AI Training/Scenario",
        order = 201)]
    public class TrainingScenarioSO : ScriptableObject
    {
        [Header("Identification")]
        public string DisplayName;
        public GameModes GameMode = GameModes.HexRace;
        public VesselClassType Vessel = VesselClassType.Manta;
        [Range(1, 4)] public int Intensity = 4;

        [Header("Population Defaults")]
        public int PopulationSize = 24;
        public int EliteCount = 4;
        [Range(0f, 1f)] public float NumericMutationRate = 0.3f;
        [Range(0f, 1f)] public float NumericMutationStrength = 0.18f;
        [Range(0f, 1f)] public float StructuralMutationRate = 0.04f;
        [Range(0f, 1f)] public float NoveltyWeight = 0.15f;

        [Header("Episode")]
        public float MaxEpisodeSeconds = 120f;
        public float MinEpisodeSeconds = 5f;
        [Tooltip("Players in the episode in addition to the trainee. Use 0 for solo training.")]
        public int OpponentCount = 3;
        [Tooltip("If true, opponents use the best-known genome from the archive instead of vanilla AIPilot.")]
        public bool OpponentsUseTrainedGenome = false;

        [Header("Sensors")]
        public TargetSensor.TargetMode TargetMode = TargetSensor.TargetMode.ClosestCrystal;

        [Header("Fitness")]
        public FitnessProfileSO FitnessProfile;

        [Header("Reset")]
        [Tooltip("If true, the runner calls gameData.ResetForReplay between episodes instead of reloading the scene.")]
        public bool UseResetForReplay = true;

        [Tooltip("Seconds to wait between episodes for cleanup before starting the next one.")]
        public float DelayBetweenEpisodes = 1f;

        [Header("Termination Hints")]
        [Tooltip("Optional list of stat-based early termination conditions. Empty = run for full duration.")]
        public List<EarlyExit> EarlyExitConditions = new();

        [System.Serializable]
        public struct EarlyExit
        {
            public TerminationKind Kind;
            public int IntegerThreshold;
            public float FloatThreshold;
        }

        public enum TerminationKind
        {
            None = 0,
            CrystalsAtLeast = 1,            // ctx.RoundStats.CrystalsCollected >= IntegerThreshold
            ScoreAtLeast = 2,               // ctx.RoundStats.Score >= FloatThreshold
            EnemyCollisionsAtLeast = 3,     // SkimmerShipCollisions >= IntegerThreshold
            VolumeCreatedAtLeast = 4,
            DistanceAtLeast = 5,
        }

        public string Key => $"{Vessel}_{GameMode}_I{Intensity}";
    }
}
