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
        DolphinDarts = 3,
        ShootingGallery = 4,
        BlockBandit = 5,
        RiskyDriftness = 6,
        // 7 (Freestyle) retired: the standalone arcade Freestyle game was removed.
        // Freestyle now refers to the Menu_Main lava-lamp experience (see CLAUDE.md,
        // "Lava-Lamp Mode"). Do not reuse ID 7.
        DuelForTheCell = 8,
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
        MazeRun = 25,
        WildlifeBlitz = 26,
        ProtectMission = 27,
        MultiplayerFreestyle = 28,
        OnlineDuelForTheCell = 29,
        Multiplayer2v2CoOpVsAI = 30,
        CoOpWildlifeBlitz = 32,
        SkimRace = 33,
        Joust = 34,
        Scurry = 35,
        // Maelstrom (36): session-level meta that chains the domain minigames
        // (SkimRace, Joust, Scurry) into one tournament. See
        // Docs/MaelstromSystem/ARCHITECTURE.md. (7 and 31 stay reserved.)
        Maelstrom = 36,
        // AstroLeague (37): hypersea soccer domain minigame. See
        // _Scripts/Controller/Arcade/ASTROLEAGUE.md.
        AstroLeague = 37,
        // BroodRush (38, display name "Brood Rush"): nucleus-control domain
        // minigame - every 30s fauna wave born under your domain's nucleus claim
        // scores a point; first domain to the wave target (default 3) wins. See
        // _Scripts/Controller/Arcade/BROODRUSH.md.
        BroodRush = 38,
        // PeelTheCage (39): Rhino-only cage-breaking race. A hollow SHIELDED prism sphere
        // pens the cell's brood; domains race to smash the destruction target, and the
        // leader IS the cell's controlling domain - so the fauna wave hatches in the
        // leader's colour and the legacy herbivore diet (eat opposing-domain mass) turns
        // the swarm loose on every trailing team's trails. See
        // _Scripts/Controller/Arcade/PEEL_THE_CAGE.md.
        PeelTheCage = 39,
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
        // Switchback (45): the Dolphin-only gate race. A course of randomly placed and randomly
        // ORIENTED switch rings is scattered through the cell, and every pilot flies the same
        // course in order - thread your next gate, or go back for it. The first DOMAIN whose
        // LEAD RUNNER threads the last gate wins, so a teammate does not shorten the course;
        // what they can do is put the Dolphin's blast cone on a rival. Intensity is the COURSE
        // (tighter mouths, sharper corners, gates twisted further off the line you arrive on),
        // never the arena. See _Scripts/Controller/Arcade/SWITCHBACK.md.
        Switchback = 45,
        // Hijack (46): the Urchin-only heist race. Three great-circle RAILS ring a hollow core,
        // meeting at spiny BURRS of raw prism where the rings cross. Every rail is painted in
        // three domain thirds and every burr wears one colour, so the yard belongs to nobody
        // for long: you latch onto a rail and grind it fast where it wears your colour and at a
        // crawl where it does not, spike the road ahead to convert it, fly off the open end -
        // aimed at the next burr by the geometry, not by a bonus - and rake the cluster with a
        // chain cascade. NOTHING here is ever destroyed: mass only changes hands. First DOMAIN
        // to steal the prism target wins (ScoringMetric.PrismsStolen). See
        // _Scripts/Controller/Arcade/HIJACK.md.
        Hijack = 46,

        // 47 was Drumfire, the Dolphin-only rhythm range: a prism DRUM at the cell centre
        // and a firing lane of crystals per pilot, clock-ended and scored on volume. Removed
        // 2026-09 - it read as Rampage (same hull, same weapon, same 'aim the cone at a lot of
        // mass') without offering enough of its own to earn a second slot. Its lane geometry
        // survives as a platform capability (ApproachLaneGeometry,
        // CrystalManager.CrystalPlacementMode.ApproachLanes) and the mode itself is in git.
        // 47 IS RESERVED FOREVER, exactly like 7 and 31 - saved selections still carry it.

        // Tollway (48): the Scarab-only ring race, built on the one Scarab idea no mode had
        // used - a switch pays its PLACER when ANY ball threads it, friend or enemy. Plant
        // rings in the court's own TOLL POSTS (unconstrained placement made the mode one move
        // long); every ball that threads one pays the pilot who planted it and raises
        // a 255-prism scarab-wing monument on the spot, so the arena is built by the scoring.
        // Rings are consumed when they pay and must be replanted. First DOMAIN to the toll
        // target wins. See _Scripts/Controller/Arcade/TOLLWAY.md.
        Tollway = 48,

        // Headlong (49): the Rhino-only circuit race. A closed loop of switch rings is cut
        // through the cell and every pilot flies LAPS of it in order; the first domain whose
        // LEAD RUNNER threads the last gate of the last lap wins. Every corner is cut against
        // the Rhino's FLAT-OUT turn radius - the tightest circle it can fly without dropping
        // the ramp boost - so a corner is a decision rather than a chore: thread it and keep
        // 910 u/s, or turn properly and pay six seconds winding the ramp back up. Intensity is
        // how many corners let you choose. See _Scripts/Controller/Arcade/HEADLONG.md.
        Headlong = 49,

        // Skein (50): the Urchin-only cable race - the first mode built around the vessel's
        // GRIND rather than around what the grind can steal. A trefoil-knot cable hangs in the
        // cell, wrapped in one family of rails whose radii BREATHE: each strand oscillates
        // between 45 and 135 units with its own phase, so at every station the strands cover
        // the whole band, and each spends part of the lap as the direct inner path and part
        // spiralling out. Ride an outward-bound strand and it carries you out; the inward
        // phase is 1.315x shorter, so the fast line means CHANGING STRANDS. Rails END, and
        // every end is AIMED - run one off its tip and its own tangent throws you onto a live
        // rail somewhere else, so "hold the throttle and the arena navigates for you" is a
        // property of the geometry rather than a bonus anyone authored. Ordered rings are
        // threaded in sequence; two are wide collars that swallow the whole cable (the start
        // and the finish, so nobody wins or loses for the lane they were in) and the rest sit
        // on ONE specific rail, so the question is never "can you thread it" but "can you be
        // on that rail when you get there". First DOMAIN whose LEAD RUNNER threads the last
        // ring wins - the gate-race fold, reusing metric 9 outright.
        // See _Scripts/Controller/Arcade/SKEIN.md.
        //
        // 50 and not 48: Tollway took 48 and Headlong 49 while this branch was in flight. The
        // enum collision is the trap DRUMFIRE.md records - two parallel branches claiming one
        // ID, merged cleanly by git into a file carrying the number twice.
        Skein = 50,


        // ADDING A MODE? Bump EnumIntegrityTests.GameModes_HasExpectedMemberCount (currently
        // 47) in the same commit, and take the next free ID -- 7, 31 and 47 stay reserved
        // forever.
        // That test is a deliberate tripwire, not an obstacle: it exists so a new member can
        // never land without someone confirming the ID is safe for saved selections.
    }
}