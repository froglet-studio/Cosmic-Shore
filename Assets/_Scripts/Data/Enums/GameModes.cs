namespace CosmicShore.Data
{
    // Remember folks, only you can prevent Unity from arbitrarily swapping enum values in files.
    // Always assign a static numeric value to your enum types
    public enum GameModes
    {
        Random = 0,
        // ── RETIRED SOLO IDS — DO NOT REUSE ─────────────────────────────────
        // Solo modes were retired 2026-07-20 (solo = a multiplayer game whose
        // party is one host). IDs 1-6, 8-25, and 27 kept their enum members so
        // the serialized ints inside the kept-but-dormant training/mission
        // assets (SO_TrainingGame_*, SO_Mission_Protect) stay stable, but their
        // SO_ArcadeGame cards and scenes are deleted. WildlifeBlitz(26) is LIVE
        // (the networked single-host co-op blitz). Do not reuse any retired ID.
        Elimination = 1,
        Rampage = 2,
        Darts = 3,
        ShootingGallery = 4,
        BlockBandit = 5,
        RiskyDriftness = 6,
        // 7 (Freestyle) retired: the standalone arcade Freestyle game was removed.
        // Freestyle now refers to the Menu_Main lava-lamp experience (see CLAUDE.md,
        // "Lava-Lamp Mode"). Do not reuse ID 7.
        CellularDuel = 8,
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
        WildlifeBlitz = 26,
        ProtectMission = 27,
        // 28 retired 2026-07-21: the standalone MultiplayerFreestyle sandbox game
        // (scene + controller + card) was deleted - freestyle IS the Menu_Main
        // lava lamp now (see CLAUDE.md "Lava-Lamp Mode"). Member kept for
        // serialized-int stability; do not reuse.
        MultiplayerFreestyle = 28,
        // 29 retired 2026-07-21: Cellular Duel deleted outright (scene + controller +
        // rule + card). Member kept for serialized-int stability; do not reuse.
        MultiplayerCellularDuel = 29,
        Multiplayer2v2CoOpVsAI = 30,
        // 32 retired 2026-07-20: the separate co-op blitz stack (scene + card)
        // was deleted - WildlifeBlitz(26) IS the networked co-op blitz now.
        // Member kept for serialized-int stability; do not reuse. (31 stays
        // reserved - never assigned.)
        MultiplayerWildlifeBlitzGame = 32,
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
    }
}