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
        public GameModes GameMode = GameModes.SkimRace;
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
        [Tooltip("Match size. The launcher launches max(2, this) players and autopilots the host seat.")]
        public int OpponentCount = 3;
        [Tooltip("If true, opponents use the best-known genome from the archive instead of vanilla AIPilot.")]
        public bool OpponentsUseTrainedGenome = false;

        [Header("Sensors")]
        public TargetSensor.TargetMode TargetMode = TargetSensor.TargetMode.ClosestCrystal;

        [Header("Fitness")]
        public FitnessProfileSO FitnessProfile;

        [Header("Reset")]
        [Tooltip("Ignored. The runner always calls MiniGameControllerBase.RequestReplay. HexRace reloads the scene.")]
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

        /// <summary>
        /// Intensity-4 Skim Race crystal target. A pilot who reaches it can
        /// close the episode once AssignScores has written the finish time.
        /// </summary>
        public const int HexRaceCrystalTarget = 54;

        /// <summary>
        /// Long enough for a domain to collect the intensity-4 target. The
        /// 120s default ended every evaluation before a finish time existed.
        /// </summary>
        public const float HexRaceMaxEpisodeSeconds = 240f;

        public string Key => $"{Vessel}_{GameMode}_I{Intensity}";

        /// <summary>
        /// Mode names a saved session key can still carry from before the
        /// GameModes rename. The numeric ids did not change. These are key
        /// tokens, not GameModes members.
        /// </summary>
        static readonly Dictionary<string, GameModes> LegacyModeNames = new(System.StringComparer.Ordinal)
        {
            { "HexRace", GameModes.SkimRace },
            { "MultiplayerCrystalCapture", GameModes.Scurry },
            { "MultiplayerJoust", GameModes.Joust },
            { "Ribcage", GameModes.Cleave },
            { "NucleusRush", GameModes.BroodRush },
            { "MultiplayerCellularDuel", GameModes.OnlineDuelForTheCell },
            { "CellularDuel", GameModes.DuelForTheCell },
            { "MultiplayerWildlifeBlitzGame", GameModes.CoOpWildlifeBlitz },
        };

        /// <summary>
        /// True when a saved session key names this scenario's vessel, mode id,
        /// and intensity. A key written before a mode rename still matches, so
        /// the saved population resumes instead of being reset.
        /// </summary>
        public bool MatchesSessionKey(string savedKey)
        {
            return TryParseKey(savedKey, out var vessel, out var mode, out int intensity)
                   && vessel == Vessel
                   && (int)mode == (int)GameMode
                   && intensity == Intensity;
        }

        /// <summary>Reads a "{Vessel}_{Mode}_I{Intensity}" key. The mode may be a current or a legacy name.</summary>
        public static bool TryParseKey(string key, out VesselClassType vessel, out GameModes mode, out int intensity)
        {
            vessel = default;
            mode = default;
            intensity = 0;
            if (string.IsNullOrEmpty(key)) return false;

            int first = key.IndexOf('_');
            int last = key.LastIndexOf("_I", System.StringComparison.Ordinal);
            if (first <= 0 || last <= first) return false;

            string vesselToken = key.Substring(0, first);
            string modeToken = key.Substring(first + 1, last - first - 1);
            string intensityToken = key.Substring(last + 2);

            if (!System.Enum.TryParse(vesselToken, false, out vessel) || !System.Enum.IsDefined(typeof(VesselClassType), vessel))
                return false;
            if (!int.TryParse(intensityToken, out intensity)) return false;
            if (LegacyModeNames.TryGetValue(modeToken, out mode)) return true;
            return System.Enum.TryParse(modeToken, false, out mode) && System.Enum.IsDefined(typeof(GameModes), mode);
        }

        /// <summary>
        /// Stamps the vessel, match size, and seek mode from the mode catalog.
        /// HexRace also stamps the finish window and a crystal early-exit at
        /// the intensity-4 target. Every other row clears early-exit gates and
        /// leaves the episode cap alone, so a golf match is not closed before
        /// AssignScores.
        /// </summary>
        public void ApplyCatalogDefaults(TrainingModeCatalog.Row row)
        {
            DisplayName = row.DisplayName;
            GameMode = row.GameMode;
            Vessel = row.Vessel;
            Intensity = 4;
            OpponentCount = row.PlayerCount;
            TargetMode = row.TargetMode;
            if (row.GameMode == GameModes.SkimRace)
            {
                MaxEpisodeSeconds = HexRaceMaxEpisodeSeconds;
                EarlyExitConditions = new List<EarlyExit>
                {
                    new()
                    {
                        Kind = TerminationKind.CrystalsAtLeast,
                        IntegerThreshold = HexRaceCrystalTarget,
                        FloatThreshold = 0f,
                    },
                };
            }
            else
            {
                EarlyExitConditions = new List<EarlyExit>();
            }
        }

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
            GameMode = GameModes.SkimRace;
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
            TargetMode = TargetSensor.TargetMode.ClosestCrystal;
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
