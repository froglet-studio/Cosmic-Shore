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
        // Ribcage (39): Rhino-only cage-breaking race. A hollow SHIELDED prism sphere
        // pens the cell's brood; domains race to smash the destruction target, and the
        // leader IS the cell's controlling domain - so the fauna wave hatches in the
        // leader's colour and the legacy herbivore diet (eat opposing-domain mass) turns
        // the swarm loose on every trailing team's trails. See
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
        // Bends (42, display name "The Bends"): the Dolphin-only debuff duel. Two to four pilots
        // fight in a cactus forest with no guns at all - the only weapon is the Dolphin's crystal
        // blast, and the only thing that scores is catching an OPPOSING pilot in it. A caught
        // pilot takes the all-element decaying debuff (the blast's elemental expression), which is
        // one "bend"; first DOMAIN to the bend target wins. See
        // _Scripts/Controller/Arcade/BENDS.md.
        Bends = 42,
        // ScarabScramble (43): the Scarab-only party game - the accessible sibling of Astro
        // League. Every white (omni) crystal you fly through becomes YOUR ball, permanently
        // your colour; roll it through any of the arena's glowing hoops and your DOMAIN scores.
        // Goals stop nothing (continuous play, no kickoffs), there are no own goals, and the
        // first domain to the goal target wins. See _Scripts/Controller/Arcade/SCARABSCRAMBLE.md.
        ScarabScramble = 43,
        // Salvo (44): the Sparrow-only demolition race, and Dog Fight's inverse in the same
        // Boneyard - here tearing the wreck apart IS the score. Guns chip, missiles level whole
        // hulks, and the arena is stocked with omni crystals: every one collected reloads the
        // missile bays of EVERY pilot on the collector's domain, so a wingman running crystals
        // keeps the strikers firing. First DOMAIN to the prism target wins. See
        // _Scripts/Controller/Arcade/SALVO.md.
        Salvo = 44,

        // ADDING A MODE? Bump EnumIntegrityTests.GameModes_HasExpectedMemberCount (currently
        // 43) in the same commit, and take the next free ID -- 7 and 31 stay reserved forever.
        // That test is a deliberate tripwire, not an obstacle: it exists so a new member can
        // never land without someone confirming the ID is safe for saved selections.
    }
}