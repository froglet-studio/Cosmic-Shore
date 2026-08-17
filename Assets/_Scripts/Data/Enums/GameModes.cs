namespace CosmicShore.Data
{
    // Remember folks, only you can prevent Unity from arbitrarily swapping enum values in files.
    // Always assign a static numeric value to your enum types
    public enum GameModes
    {
        Random = 0,
        // ── RETIRED SOLO IDS — DO NOT REUSE ─────────────────────────────────
        // Solo modes were retired 2026-07-20 (solo = a multiplayer game whose
        // party is one host). IDs 1, 3-6, 8-25 and 27 kept their enum members so
        // the serialized ints inside the kept-but-dormant training/mission
        // assets (SO_TrainingGame_*, SO_Mission_Protect) stay stable, but their
        // SO_ArcadeGame cards and scenes are deleted. Do not reuse any retired ID.
        // EXCEPTION: Rampage(2) is NOT retired - its legacy solo ID was deliberately
        // repurposed for the live multiplayer destruction race (see below).
        Elimination = 1,
        // Rampage (2): multiplayer destruction race - the destructive analog of
        // Crystal Capture/"Scurry". Race to destroy the hostile-prism target first.
        // (Repurposed from the legacy single-player arcade entry, whose scene never
        // shipped.) See _Scripts/Controller/Arcade/RAMPAGE.md.
        Rampage = 2,
        Darts = 3,
        ShootingGallery = 4,
        BlockBandit = 5,
        RiskyDriftness = 6,
        // 7 (Freestyle) retired: the standalone arcade Freestyle game was removed.
        // Freestyle now refers to the Menu_Main lava-lamp experience (see CLAUDE.md,
        // "Lava-Lamp Mode"). Do not reuse ID 7.
        // 8 (CellularDuel) retired 2026-07-21 with the Cellular Duel deletion - do not reuse.
        DashNGrab = 9,
        CellularBrawl = 10,
        Denial = 11,
        CatNMouse = 12,
        SlipNStride = 13,
        PumpNDump = 14,
        MasterExploder = 15,
        Soar = 16,
        ObstacleCourse = 17,
        Distraction = 18,
        RhinoRun = 19,
        KickinMass = 20,
        Sidewinder = 21,
        Multipass = 22,
        BotDuel = 23,
        Curvatious = 24,
        MazeRunner = 25,
        // 26 (WildlifeBlitz) retired 2026-07-21 with the Wildlife Blitz deletion - do not reuse.
        ProtectMission = 27,
        // 28 (MultiplayerFreestyle) retired 2026-07-21 - freestyle IS the Menu_Main lava lamp. Do not reuse.
        // 29 (MultiplayerCellularDuel) retired 2026-07-21 with the Cellular Duel deletion - do not reuse.
        // 30 (Multiplayer2v2CoOpVsAI) retired 2026-07-21 - unreachable content deleted. Do not reuse.
        // 31 stays reserved - never assigned.
        // 32 (MultiplayerWildlifeBlitzGame) retired 2026-07-20 with the co-op blitz stack - do not reuse.
        HexRace = 33,
        MultiplayerJoust = 34,
        MultiplayerCrystalCapture = 35,
        // Tournament (36): session-level meta that chains the domain minigames
        // (HexRace, Joust, CrystalCapture) into one tournament. See
        // Docs/TournamentSystem/ARCHITECTURE.md. (7 and 31 stay reserved.)
        Tournament = 36,
        // AstroLeague (37): hypersea soccer domain minigame. See
        // _Scripts/Controller/Arcade/ASTROLEAGUE.md.
        AstroLeague = 37,
        // NucleusRush (38, display name "Brood Rush"): nucleus-control domain
        // minigame - every 30s fauna wave born under your domain's nucleus claim
        // scores a point; first domain to the wave target (default 3) wins. See
        // _Scripts/Controller/Arcade/NUCLEUSRUSH.md.
        NucleusRush = 38,
        // Ribcage (39, display name "Peel the Cage"): Rhino-only cage-breaking race.
        // A layered orange of hollow prism-bone shells pens the cell's core; domains
        // race to DESTROY the hostile-prism target (2000, the same metric and target as
        // Rampage). Intensity is how many rinds you peel. See
        // _Scripts/Controller/Arcade/RIBCAGE.md.
        Ribcage = 39,
        // WildlifeLiberation (40): the Sparrow-only hunt. Three concentric cages at 1050 / 600
        // / 200 pen three tiers of wildlife - a huge swarm of small creatures in the outer
        // room, much bigger ones in the middle, the biggest and toughest in the core. Break in
        // and shoot; first PLAYER (not domain - this one is a free-for-all) to the kill target
        // wins. See _Scripts/Controller/Arcade/WILDLIFE_LIBERATION.md.
        WildlifeLiberation = 40,
        // DogFight (41): the Sparrow-only gun duel. Two to four pilots hunt each other through
        // the Boneyard - a wrecked world of hollow hulks and rubble canyons built for close
        // encounters and hiding places. A bullet hit scores 1, a missile hit (direct strike or
        // caught in the blast) scores 50, and the first DOMAIN to the point target wins. The
        // only mode whose score comes from vessel-vs-vessel gunnery. See
        // _Scripts/Controller/Arcade/DOGFIGHT.md.
        DogFight = 41,
        // Benchmark (42): the Settings > Run Benchmark stress-test context - not an
        // arcade mode (no card, no scoring, endless). Set by BenchmarkSceneLauncher so
        // mode-keyed consumers (presence/connecting-panel display, comeback default,
        // HUD objective default) resolve honestly instead of borrowing a retired id.
        // (Authored as 39 on Ys-bleeding-edge, then 40; landed at 42 on the merge with
        // bleeding-edge, which had already shipped Ribcage at 39, WildlifeLiberation at
        // 40 and DogFight at 41. Benchmark is set in code only - no serialized asset
        // carries the id - so the renumber is safe.)
        Benchmark = 42,
    }
}