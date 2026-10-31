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
            // Added 2026-10-28 (Froglet Compute trains every card): every remaining multiplayer card with a
            // scoring rule. Their recipes are FitnessProfileSO.MetricRecipes; gate races seek TargetMode.NextGate.
            // Not here: Maelstrom (chains other modes) and Grizzly Charge (in development, no scoring rule).
            new(GameModes.Regatta, "Regatta", "Regatta · Manta · I4",
                VesselClassType.Manta, false, 4, TargetSensor.TargetMode.NextGate,
                "MinigameRegatta",
                "Arena gate race; every hull on the card. The scenario defaults to Manta. Fitness: gates threaded (SwitchesThreaded)."),
            new(GameModes.Headlong, "Headlong", "Headlong · Rhino · I4",
                VesselClassType.Rhino, true, 4, TargetSensor.TargetMode.NextGate,
                "MinigameHeadlong",
                "Rhino circuit gate race. Fitness: gates threaded."),
            new(GameModes.Redline, "Redline", "Redline · Manta · I4",
                VesselClassType.Manta, true, 4, TargetSensor.TargetMode.NextGate,
                "MinigameRedline",
                "Manta circuit gate race. Fitness: gates threaded."),
            new(GameModes.Switchback, "Switchback", "Switchback · Dolphin · I4",
                VesselClassType.Dolphin, true, 4, TargetSensor.TargetMode.NextGate,
                "MinigameSwitchback",
                "Dolphin gate race. Fitness: gates threaded."),
            new(GameModes.Breakwater, "Breakwater", "Breakwater · Sparrow · I4",
                VesselClassType.Sparrow, true, 4, TargetSensor.TargetMode.NextGate,
                "MinigameBreakwater",
                "Sparrow station race. Fitness: gates threaded."),
            new(GameModes.Skein, "Skein", "Skein · Urchin · I4",
                VesselClassType.Urchin, true, 4, TargetSensor.TargetMode.NextGate,
                "MinigameSkein",
                "Urchin cable race. Fitness: gates threaded."),
            new(GameModes.Waystation, "Waystation", "Waystation · Butterfly · I4",
                VesselClassType.Butterfly, true, 4, TargetSensor.TargetMode.NextGate,
                "MinigameWaystation",
                "Butterfly migration race. Fitness: gates threaded."),
            new(GameModes.GrizzlyTime, "GrizzlyTime", "Grizzly Time · Grizzly · I4",
                VesselClassType.Grizzly, true, 4, TargetSensor.TargetMode.NextGate,
                "MinigameGrizzlyTime",
                "Grizzly gate race. Fitness: gates threaded."),
            new(GameModes.Slingshot, "Slingshot", "Slingshot · Stoat · I4",
                VesselClassType.Stoat, true, 4, TargetSensor.TargetMode.NextGate,
                "MinigameSlingshot",
                "Stoat gate race. Fitness: gates threaded."),
            new(GameModes.Warpline, "Warpline", "Warpline · Meerkat · I4",
                VesselClassType.Meerkat, true, 4, TargetSensor.TargetMode.NextGate,
                "MinigameWarpline",
                "Meerkat gate race. Fitness: gates threaded."),
            new(GameModes.ThresherTime, "ThresherTime", "Thresher Time · Thresher · I4",
                VesselClassType.Thresher, true, 4, TargetSensor.TargetMode.NextGate,
                "MinigameThresherTime",
                "Thresher gate race. Fitness: gates threaded."),
            new(GameModes.WrasseRun, "WrasseRun", "Wrasse Run · Wrasse · I4",
                VesselClassType.Wrasse, true, 4, TargetSensor.TargetMode.NextGate,
                "MinigameWrasseRun",
                "Wrasse gate race (a crew hull: the trainer flies its pilot seat). Fitness: gates threaded."),
            new(GameModes.BurrowRun, "BurrowRun", "Burrow Run · Meerkat · I4",
                VesselClassType.Meerkat, true, 4, TargetSensor.TargetMode.NextGate,
                "MinigameBurrowRun",
                "Meerkat burrow race. Fitness: gates threaded."),
            new(GameModes.Broadside, "Broadside", "Broadside · Manta · I4",
                VesselClassType.Manta, false, 4, TargetSensor.TargetMode.BothPreferEnemy,
                "MinigameBroadside",
                "Arena brawl; every hull on the card. The scenario defaults to Manta. Fitness: CombatPoints."),
            new(GameModes.Dustup, "Dustup", "Dustup · Butterfly · I4",
                VesselClassType.Butterfly, true, 4, TargetSensor.TargetMode.ClosestEnemyVessel,
                "MinigameDustup",
                "Butterfly dust duel. Fitness: CombatPoints."),
            new(GameModes.Undertow, "Undertow", "Undertow · Scarab · I4",
                VesselClassType.Scarab, true, 4, TargetSensor.TargetMode.ClosestEnemyVessel,
                "MinigameUndertow",
                "Scarab cavitation duel. Fitness: CombatPoints."),
            new(GameModes.Sirocco, "Sirocco", "Sirocco · Butterfly · I4",
                VesselClassType.Butterfly, true, 4, TargetSensor.TargetMode.ClosestCrystal,
                "MinigameSirocco",
                "Butterfly erosion race. Fitness: HostilePrismsDestroyed."),
            new(GameModes.WreckingBall, "WreckingBall", "Wrecking Ball · Scarab · I4",
                VesselClassType.Scarab, true, 4, TargetSensor.TargetMode.ClosestCrystal,
                "MinigameWreckingBall",
                "Scarab demolition race. Fitness: HostilePrismsDestroyed."),
            new(GameModes.Bloomrush, "Bloomrush", "Bloomrush · Manta · I4",
                VesselClassType.Manta, true, 4, TargetSensor.TargetMode.ClosestCrystal,
                "MinigameBloomrush",
                "Manta delayed-detonation race. Fitness: hostile volume destroyed."),
            new(GameModes.Hijack, "Hijack", "Hijack · Urchin · I4",
                VesselClassType.Urchin, true, 4, TargetSensor.TargetMode.BothPreferEnemy,
                "MinigameHijack",
                "Urchin heist race. Fitness: PrismsStolen."),
            new(GameModes.Tapestry, "Tapestry", "Tapestry · Butterfly · I4",
                VesselClassType.Butterfly, true, 4, TargetSensor.TargetMode.ClosestCrystal,
                "MinigameTapestry",
                "Butterfly painting war. Fitness: standing volume (VolumeRemaining)."),
            new(GameModes.Tollway, "Tollway", "Tollway · Scarab · I4",
                VesselClassType.Scarab, true, 4, TargetSensor.TargetMode.ClosestCrystal,
                "MinigameTollway",
                "Scarab ring race. Fitness: GoalsScored."),
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
