namespace CosmicShore.Data
{
    // Remember folks, only you can prevent Unity from arbitrarily swapping enum values in files.
    // Always assign a static numeric value to your enum types
    public enum GameModes
    {
        Random = 0,
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
        MultiplayerFreestyle = 28,
        MultiplayerCellularDuel = 29,
        Multiplayer2v2CoOpVsAI = 30,
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