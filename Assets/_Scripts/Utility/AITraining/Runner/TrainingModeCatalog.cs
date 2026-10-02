using System.Collections.Generic;
using CosmicShore.Data;

namespace CosmicShore.Utility.AITraining
{
    /// <summary>
    /// Live multiplayer modes the trainer can score from existing RoundStats.
    /// A row exists only when the mode has a scene and a ScoringRule on
    /// bleeding-edge. Vessel is the card's lock when the card lists one class,
    /// otherwise the default hull for an unlocked roster.
    /// PlayerCount is the match size. The launcher reads OpponentCount as that
    /// size (minimum 2).
    /// </summary>
    public static class TrainingModeCatalog
    {
        public const string Root = "Assets/_SO_Assets/AI Training";

        public readonly struct Row
        {
            public readonly GameModes GameMode;
            public readonly string Token;
            public readonly string DisplayName;
            public readonly VesselClassType Vessel;
            public readonly bool VesselLocked;
            public readonly int PlayerCount;
            public readonly TargetSensor.TargetMode TargetMode;
            public readonly string SceneName;
            public readonly string Attribution;

            public Row(
                GameModes gameMode,
                string token,
                string displayName,
                VesselClassType vessel,
                bool vesselLocked,
                int playerCount,
                TargetSensor.TargetMode targetMode,
                string sceneName,
                string attribution)
            {
                GameMode = gameMode;
                Token = token;
                DisplayName = displayName;
                Vessel = vessel;
                VesselLocked = vesselLocked;
                PlayerCount = playerCount;
                TargetMode = targetMode;
                SceneName = sceneName;
                Attribution = attribution;
            }

            public string ScenarioPath => $"{Root}/Scenarios/Scenario_{Token}.asset";
            public string ProfilePath => $"{Root}/Profiles/FitnessProfile_{Token}.asset";
        }

        public static IReadOnlyList<Row> Live { get; } = new Row[]
        {
            new(GameModes.SkimRace, "HexRace", "HexRace · Squirrel · I4",
                VesselClassType.Squirrel, true, 3, TargetSensor.TargetMode.ClosestCrystal,
                "MinigameSkimRace",
                "Domain sum of crystals ends the race. Fitness uses this pilot's CrystalsCollected. Golf Score is shared by the domain and negated."),
            new(GameModes.Scurry, "CrystalCapture", "Crystal Capture · Squirrel · I4",
                VesselClassType.Squirrel, false, 4, TargetSensor.TargetMode.ClosestCrystal,
                "MinigameScurryMultiplayer_Gameplay",
                "Domain sum of crystals ends the match. Card allows Squirrel, Manta, and Sparrow; the scenario defaults to Squirrel. Golf Score is shared and negated."),
            new(GameModes.Joust, "Joust", "Joust · Squirrel · I4",
                VesselClassType.Squirrel, true, 4, TargetSensor.TargetMode.ClosestEnemyVessel,
                "MinigameJoust_Gameplay",
                "Domain sum of jousts ends the match. Fitness uses this pilot's JoustCollisions. Golf Score is shared and negated."),
            new(GameModes.Rampage, "Rampage", "Rampage · Dolphin · I4",
                VesselClassType.Dolphin, true, 4, TargetSensor.TargetMode.ClosestCrystal,
                "MinigameRampage",
                "Domain sum of HostilePrismsDestroyed ends the match. Golf Score is shared and negated."),
            new(GameModes.Cleave, "Ribcage", "Peel the Cage · Rhino · I4",
                VesselClassType.Rhino, true, 4, TargetSensor.TargetMode.ClosestCrystal,
                "MinigameCleave",
                "Domain sum of HostilePrismsDestroyed ends the match. Golf Score is shared and negated."),
            new(GameModes.WildlifeLiberation, "WildlifeLiberation", "Wildlife Liberation · Sparrow · I4",
                VesselClassType.Sparrow, true, 4, TargetSensor.TargetMode.ClosestCrystal,
                "MinigameWildlifeLiberation",
                "Domain sum of LifeformsKilled ends the match. Golf Score is shared and negated."),
            new(GameModes.DogFight, "DogFight", "Dog Fight · Sparrow · I4",
                VesselClassType.Sparrow, true, 4, TargetSensor.TargetMode.ClosestEnemyVessel,
                "MinigameDogFight",
                "Domain sum of CombatPoints ends the match. Golf Score is shared and negated."),
            new(GameModes.Bends, "Bends", "The Bends · Dolphin · I4",
                VesselClassType.Dolphin, true, 4, TargetSensor.TargetMode.BothPreferEnemy,
                "MinigameBends",
                "Domain sum of CombatPoints (bends) ends the match. Golf Score is shared and negated."),
            new(GameModes.ScarabScramble, "ScarabScramble", "Scarab Scramble · Scarab · I4",
                VesselClassType.Scarab, true, 4, TargetSensor.TargetMode.ClosestCrystal,
                "MinigameScarabScramble",
                "Domain sum of goals ends the match. Fitness uses this pilot's GoalsScored. Score copies that tally and is not golf."),
            new(GameModes.Salvo, "Salvo", "Salvo · Sparrow · I4",
                VesselClassType.Sparrow, true, 4, TargetSensor.TargetMode.BothPreferCrystal,
                "MinigameSalvo",
                "Domain sum of HostilePrismsDestroyed ends the match. Golf Score is shared and negated."),
            new(GameModes.BroodRush, "NucleusRush", "Brood Rush · Squirrel · I4",
                VesselClassType.Squirrel, false, 4, TargetSensor.TargetMode.ClosestCrystal,
                "MinigameBroodRush",
                "Domain sum of GoalsScored ends the match. Each wave is stamped on one representative player, so a teammate who did not receive the stamp scores zero brood."),
            new(GameModes.AstroLeague, "AstroLeague", "Astro League · Rhino · I4",
                VesselClassType.Rhino, false, 4, TargetSensor.TargetMode.BothPreferCrystal,
                "MinigameAstroLeague",
                "Domain sum of goals ends the match. Card also allows Scarab; the scenario defaults to Rhino. Fitness uses this pilot's GoalsScored."),
        };

        public static bool TryGet(GameModes mode, out Row row)
        {
            for (int i = 0; i < Live.Count; i++)
            {
                if (Live[i].GameMode == mode)
                {
                    row = Live[i];
                    return true;
                }
            }
            row = default;
            return false;
        }
    }
}
