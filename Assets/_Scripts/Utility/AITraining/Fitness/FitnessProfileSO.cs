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
            // ScoringMetric.SwitchesThreaded: gates this pilot threaded in order (every gate race).
            SwitchesThreaded = 20,
            // ScoringMetric.PrismsStolen (Hijack).
            PrismsStolen = 21,
            // ScoringMetric.VolumeRemaining: standing volume, a live stock (Tapestry).
            VolumeRemaining = 22,
            // Where people fly: minus the mean distance (units) from the pilot's path to the people's recorded lines
            // of this pair (PeopleLine, loaded by Froglet Compute's seed); 0 when no line is loaded.
            PeopleLine = 23,
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
                case GameModes.SkimRace:
                case GameModes.Scurry:
                case GameModes.Joust:
                case GameModes.Rampage:
                case GameModes.Cleave:
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
                case GameModes.SkimRace: ApplyHexRaceDefaults(); break;
                case GameModes.Scurry: ApplyCrystalCaptureDefaults(); break;
                case GameModes.Joust: ApplyJoustDefaults(); break;
                case GameModes.Rampage: ApplyRampageDefaults(); break;
                case GameModes.Cleave: ApplyRibcageDefaults(); break;
                case GameModes.WildlifeLiberation: ApplyWildlifeLiberationDefaults(); break;
                case GameModes.DogFight: ApplyDogFightDefaults(); break;
                case GameModes.Bends: ApplyBendsDefaults(); break;
                case GameModes.ScarabScramble: ApplyScarabScrambleDefaults(); break;
                case GameModes.Salvo: ApplySalvoDefaults(); break;
                case GameModes.BroodRush: ApplyNucleusRushDefaults(); break;
                case GameModes.AstroLeague: ApplyAstroLeagueDefaults(); break;
                default:
                    if (!TryApplyMetricRecipe(mode)) ApplyRacingDefaults();
                    break;
            }
        }

        /// <summary>One mode's metric recipe: its ScoringMetric's component, weighted, plus the time penalty.</summary>
        public readonly struct MetricRecipe
        {
            public readonly GameModes Mode;
            public readonly ComponentKind Kind;
            public readonly float Weight;
            public readonly string Label;
            public readonly string Description;

            public MetricRecipe(GameModes mode, ComponentKind kind, float weight, string label, string description)
            {
                Mode = mode; Kind = kind; Weight = weight; Label = label; Description = description;
            }
        }

        /// <summary>
        /// The modes added 2026-10-28 so Froglet Compute can train every card (Docs/Compute/README.md section 16).
        /// The heavy row is the ScoringMetric each mode's ScoringRule asset scores; Score is not weighted
        /// (the sign of a golf Score differs by mode, and the per-pilot metric already separates pilots).
        /// Tools/Build/author_training_scenarios.py reads this table to write each mode's assets: one row per
        /// line. Weights are first passes, scaled to the metric's points per event.
        /// </summary>
        public static readonly MetricRecipe[] MetricRecipes =
        {
            new(GameModes.Regatta, ComponentKind.SwitchesThreaded, 100f, "Gates", "Regatta: gates this pilot threaded in order (SwitchesThreaded). The time penalty prefers the faster match."),
            new(GameModes.Headlong, ComponentKind.SwitchesThreaded, 100f, "Gates", "Headlong: gates this pilot threaded in order (SwitchesThreaded). The time penalty prefers the faster match."),
            new(GameModes.Redline, ComponentKind.SwitchesThreaded, 100f, "Gates", "Redline: gates this pilot threaded in order (SwitchesThreaded). The time penalty prefers the faster match."),
            new(GameModes.Switchback, ComponentKind.SwitchesThreaded, 100f, "Gates", "Switchback: gates this pilot threaded in order (SwitchesThreaded). The time penalty prefers the faster match."),
            new(GameModes.Breakwater, ComponentKind.SwitchesThreaded, 100f, "Gates", "Breakwater: gates this pilot threaded in order (SwitchesThreaded). The time penalty prefers the faster match."),
            new(GameModes.Skein, ComponentKind.SwitchesThreaded, 100f, "Gates", "Skein: gates this pilot threaded in order (SwitchesThreaded). The time penalty prefers the faster match."),
            new(GameModes.Waystation, ComponentKind.SwitchesThreaded, 100f, "Gates", "Waystation: gates this pilot threaded in order (SwitchesThreaded). The time penalty prefers the faster match."),
            new(GameModes.GrizzlyTime, ComponentKind.SwitchesThreaded, 100f, "Gates", "Grizzly Time: gates this pilot threaded in order (SwitchesThreaded). The time penalty prefers the faster match."),
            new(GameModes.Slingshot, ComponentKind.SwitchesThreaded, 100f, "Gates", "Slingshot: gates this pilot threaded in order (SwitchesThreaded). The time penalty prefers the faster match."),
            new(GameModes.Warpline, ComponentKind.SwitchesThreaded, 100f, "Gates", "Warpline: gates this pilot threaded in order (SwitchesThreaded). The time penalty prefers the faster match."),
            new(GameModes.ThresherTime, ComponentKind.SwitchesThreaded, 100f, "Gates", "Thresher Time: gates this pilot threaded in order (SwitchesThreaded). The time penalty prefers the faster match."),
            new(GameModes.WrasseRun, ComponentKind.SwitchesThreaded, 100f, "Gates", "Wrasse Run: gates this pilot threaded in order (SwitchesThreaded). The time penalty prefers the faster match."),
            new(GameModes.BurrowRun, ComponentKind.SwitchesThreaded, 100f, "Gates", "Burrow Run: gates this pilot threaded in order (SwitchesThreaded). The time penalty prefers the faster match."),
            new(GameModes.SerpentTime, ComponentKind.SwitchesThreaded, 100f, "Gates", "Serpentine (Serpent Time): rings this pilot threaded in order (SwitchesThreaded). The time penalty prefers the faster match."),
            new(GameModes.ScarabRally, ComponentKind.SwitchesThreaded, 100f, "Gates", "Scarab Rally: gates this pilot threaded in order (SwitchesThreaded). The time penalty prefers the faster match."),
            new(GameModes.BrittleStarTime, ComponentKind.SwitchesThreaded, 100f, "Gates", "Brittle Star Time: gates this pilot threaded in order (SwitchesThreaded). The time penalty prefers the faster match."),
            new(GameModes.Broadside, ComponentKind.CombatPoints, 1f, "CombatPoints", "Broadside: this pilot's CombatPoints (hits already weighted by the mode's ScoringRule: a bullet 1, a missile blast 20)."),
            new(GameModes.Dustup, ComponentKind.CombatPoints, 10f, "DustHits", "Dustup: this pilot's CombatPoints (one dust hit is one point)."),
            new(GameModes.Undertow, ComponentKind.CombatPoints, 30f, "CombatPoints", "Undertow: this pilot's CombatPoints (a bend is 3 points, a kill 1)."),
            new(GameModes.Sirocco, ComponentKind.HostilePrismsDestroyed, 1f, "HostilePrisms", "Sirocco: this pilot's HostilePrismsDestroyed (ScoringMetric.PrismsDestroyed)."),
            new(GameModes.WreckingBall, ComponentKind.HostilePrismsDestroyed, 1f, "HostilePrisms", "Wrecking Ball: this pilot's HostilePrismsDestroyed (ScoringMetric.PrismsDestroyed)."),
            new(GameModes.Bloomrush, ComponentKind.VolumeDestroyedHostile, 1f, "HostileVolume", "Bloomrush: hostile volume this pilot destroyed (ScoringMetric.VolumeDestroyed)."),
            new(GameModes.Hijack, ComponentKind.PrismsStolen, 10f, "PrismsStolen", "Hijack: prisms this pilot stole (ScoringMetric.PrismsStolen)."),
            new(GameModes.Tapestry, ComponentKind.VolumeRemaining, 1f, "VolumeStanding", "Tapestry: this pilot's standing volume at the end (ScoringMetric.VolumeRemaining, a live stock)."),
            new(GameModes.Tollway, ComponentKind.GoalsScored, 100f, "Goals", "Tollway: this pilot's GoalsScored (ScoringMetric.Goals)."),
        };

        public bool TryApplyMetricRecipe(GameModes mode)
        {
            for (int i = 0; i < MetricRecipes.Length; i++)
            {
                var r = MetricRecipes[i];
                if (r.Mode != mode) continue;
                SetPersonalMetricRecipe(r.Description, r.Kind, r.Weight, r.Label);
                return true;
            }
            return false;
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
