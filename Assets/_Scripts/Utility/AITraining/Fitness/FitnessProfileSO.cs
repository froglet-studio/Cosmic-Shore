using System;
using System.Collections.Generic;
using CosmicShore.Data;
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
            // ScoringMetric.PrismsDestroyed reads HostilePrismsDestroyed, not volume.
            HostilePrismsDestroyed = 16,
            // ScoringMetric.LifeformsKilled.
            LifeformsKilled = 17,
            // ScoringMetric.CombatPoints (Dog Fight weights and Bends bends are already in the stat).
            CombatPoints = 18,
            // ScoringMetric.Goals.
            GoalsScored = 19,
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

        /// <summary>
        /// HexRace scores golf: the winner's Score is finish time, a loser's is
        /// 10000 plus the domain's crystal deficit. A positive weight would
        /// select for losing. Crystal count stays the per-pilot term; domain
        /// placement is the negated score, not a shared team bonus.
        /// </summary>
        public void ApplyHexRaceDefaults()
        {
            Description = "HexRace: crystals collected by this pilot, golf score negated " +
                          "(lower finish time is better), elapsed-time penalty. No team bonus.";
            Entries = new List<Entry>
            {
                new() { Kind = ComponentKind.CrystalCollection, Weight = 100f, Label = "Crystals" },
                new() { Kind = ComponentKind.ScoreFromRoundStats, Weight = 0.1f, Label = "GolfScore" },
                new() { Kind = ComponentKind.TimePenalty, Weight = 1f, Label = "TimePenalty" },
            };
        }

        /// <summary>
        /// Modes whose AssignScores writes a shared golf value onto every pilot
        /// of a domain: finish time for the winning domain, a loser sentinel
        /// otherwise. Lower is better. Nucleus Rush, Astro League, and Scarab
        /// Scramble copy personal GoalsScored into Score and are not in this set.
        /// </summary>
        public static bool ScoreIsGolf(GameModes mode)
        {
            switch (mode)
            {
                case GameModes.HexRace:
                case GameModes.MultiplayerCrystalCapture:
                case GameModes.MultiplayerJoust:
                case GameModes.Rampage:
                case GameModes.Ribcage:
                case GameModes.WildlifeLiberation:
                case GameModes.DogFight:
                case GameModes.Bends:
                case GameModes.Salvo:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Sign correction applied at harvest. Golf Score is negated so a
        /// positive weight selects for finishing. The per-pilot metric is
        /// left as authored. Personal-goal modes keep Score positive.
        /// </summary>
        public static float SignedRaw(GameModes mode, ComponentKind kind, float raw)
        {
            if (kind == ComponentKind.ScoreFromRoundStats && ScoreIsGolf(mode))
                return -raw;
            return raw;
        }

        /// <summary>
        /// Recipe for one live arcade mode. The heavy row is the ScoringMetric
        /// the mode already writes on this pilot's RoundStats. Golf modes add
        /// the shared Score at a small weight; harvest negates it.
        /// </summary>
        public void ApplyFor(GameModes mode)
        {
            switch (mode)
            {
                case GameModes.HexRace: ApplyHexRaceDefaults(); break;
                case GameModes.MultiplayerCrystalCapture: ApplyCrystalCaptureDefaults(); break;
                case GameModes.MultiplayerJoust: ApplyJoustDefaults(); break;
                case GameModes.Rampage: ApplyRampageDefaults(); break;
                case GameModes.Ribcage: ApplyRibcageDefaults(); break;
                case GameModes.WildlifeLiberation: ApplyWildlifeLiberationDefaults(); break;
                case GameModes.DogFight: ApplyDogFightDefaults(); break;
                case GameModes.Bends: ApplyBendsDefaults(); break;
                case GameModes.ScarabScramble: ApplyScarabScrambleDefaults(); break;
                case GameModes.Salvo: ApplySalvoDefaults(); break;
                case GameModes.NucleusRush: ApplyNucleusRushDefaults(); break;
                case GameModes.AstroLeague: ApplyAstroLeagueDefaults(); break;
                case GameModes.MultiplayerFreestyle: ApplyFreestyleDefaults(); break;
                default: ApplyRacingDefaults(); break;
            }
        }

        public void ApplyCrystalCaptureDefaults()
        {
            SetGolfRecipe(
                "Crystal Capture: this pilot's CrystalsCollected. The match ends on the " +
                "domain sum. Score is the shared golf finish time (negated at harvest), " +
                "not a bonus every teammate receives for the domain winning.",
                ComponentKind.CrystalCollection, 100f, "Crystals");
        }

        public void ApplyRampageDefaults()
        {
            SetGolfRecipe(
                "Rampage: this pilot's HostilePrismsDestroyed (ScoringMetric.PrismsDestroyed). " +
                "The match ends on the domain sum. Score is the shared golf finish time, negated at harvest.",
                ComponentKind.HostilePrismsDestroyed, 1f, "HostilePrisms");
        }

        public void ApplyRibcageDefaults()
        {
            SetGolfRecipe(
                "Peel the Cage: this pilot's HostilePrismsDestroyed. The match ends on the " +
                "domain sum. Score is the shared golf finish time, negated at harvest.",
                ComponentKind.HostilePrismsDestroyed, 1f, "HostilePrisms");
        }

        public void ApplyWildlifeLiberationDefaults()
        {
            SetGolfRecipe(
                "Wildlife Liberation: this pilot's LifeformsKilled. The match ends on the " +
                "domain sum. Score is the shared golf finish time, negated at harvest.",
                ComponentKind.LifeformsKilled, 10f, "LifeformsKilled");
        }

        public void ApplyDogFightDefaults()
        {
            SetGolfRecipe(
                "Dog Fight: this pilot's CombatPoints (bullets and missiles already weighted " +
                "by the mode's ScoringRule). The match ends on the domain sum. Score is the " +
                "shared golf finish time, negated at harvest.",
                ComponentKind.CombatPoints, 1f, "CombatPoints");
        }

        public void ApplyBendsDefaults()
        {
            SetGolfRecipe(
                "The Bends: this pilot's CombatPoints (one bend is one point). The match ends " +
                "on the domain sum. Score is the shared golf finish time, negated at harvest.",
                ComponentKind.CombatPoints, 100f, "Bends");
        }

        public void ApplySalvoDefaults()
        {
            SetGolfRecipe(
                "Salvo: this pilot's HostilePrismsDestroyed. The match ends on the domain sum. " +
                "Score is the shared golf finish time, negated at harvest.",
                ComponentKind.HostilePrismsDestroyed, 1f, "HostilePrisms");
        }

        public void ApplyScarabScrambleDefaults()
        {
            SetPersonalMetricRecipe(
                "Scarab Scramble: this pilot's GoalsScored. The match ends on the domain sum. " +
                "AssignScores copies that personal tally into Score (higher is better), so the " +
                "recipe does not also weight Score.",
                ComponentKind.GoalsScored, 100f, "Goals");
        }

        public void ApplyAstroLeagueDefaults()
        {
            SetPersonalMetricRecipe(
                "Astro League: this pilot's GoalsScored. The match ends on the domain sum. " +
                "AssignScores copies that personal tally into Score (higher is better), so the " +
                "recipe does not also weight Score.",
                ComponentKind.GoalsScored, 100f, "Goals");
        }

        public void ApplyNucleusRushDefaults()
        {
            SetPersonalMetricRecipe(
                "Brood Rush: GoalsScored. Each fauna wave is stamped on one representative " +
                "player of the controlling domain, then summed by domain. Fitness follows that " +
                "stamp. Score copies GoalsScored (higher is better) and is not negated.",
                ComponentKind.GoalsScored, 100f, "Brood");
        }

        void SetGolfRecipe(string description, ComponentKind metric, float metricWeight, string metricLabel)
        {
            Description = description;
            Entries = new List<Entry>
            {
                new() { Kind = metric, Weight = metricWeight, Label = metricLabel },
                new() { Kind = ComponentKind.ScoreFromRoundStats, Weight = 0.1f, Label = "GolfScore" },
                new() { Kind = ComponentKind.TimePenalty, Weight = 1f, Label = "TimePenalty" },
            };
        }

        void SetPersonalMetricRecipe(string description, ComponentKind metric, float metricWeight, string metricLabel)
        {
            Description = description;
            Entries = new List<Entry>
            {
                new() { Kind = metric, Weight = metricWeight, Label = metricLabel },
                new() { Kind = ComponentKind.TimePenalty, Weight = 1f, Label = "TimePenalty" },
            };
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
            SetGolfRecipe(
                "Joust: this pilot's JoustCollisions. The match ends on the domain sum. " +
                "Score is the shared golf finish time, or one flat loser sentinel for the " +
                "whole losing domain, negated at harvest. Teammates separate on their own joust count.",
                ComponentKind.JoustCollisions, 100f, "Jousts");
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
