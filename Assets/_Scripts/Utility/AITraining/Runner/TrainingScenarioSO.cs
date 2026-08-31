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

        // NOTE: the old TargetMode field is gone with the parallel policy pilot.
        // What the AI targets (crystals vs players) is decided by the mode's own
        // spawn pipeline — ServerPlayerVesselInitializerWithAI.ConfigureAIPilot
        // sets seekPlayers per game mode — and training tunes THAT pilot.

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

        /// <summary>
        /// Called by Unity at asset creation. Field initializers already pick sensible
        /// numeric defaults; this just gives the asset a human-readable display name and
        /// adds a single early-exit condition that ends races as soon as a player wins,
        /// which is the difference between an asset that trains usefully out of the box
        /// and one that always runs to the timeout.
        /// </summary>
        void Reset()
        {
            DisplayName = "HexRace · Manta · Flawless";
            GameMode = GameModes.HexRace;
            Vessel = VesselClassType.Manta;
            Intensity = 4;
            PopulationSize = 24;
            EliteCount = 4;
            NumericMutationRate = 0.3f;
            NumericMutationStrength = 0.18f;
            StructuralMutationRate = 0.04f;
            NoveltyWeight = 0.15f;
            MaxEpisodeSeconds = 120f;
            MinEpisodeSeconds = 5f;
            OpponentCount = 3;
            OpponentsUseTrainedGenome = false;
            UseResetForReplay = true;
            DelayBetweenEpisodes = 1f;
            EarlyExitConditions = new List<EarlyExit>
            {
                // HexRace ends at 39 crystals by default; this lets a winning rollout
                // close out cleanly instead of waiting for the watchdog.
                new() { Kind = TerminationKind.CrystalsAtLeast, IntegerThreshold = 39, FloatThreshold = 0f }
            };
        }
    }
}
