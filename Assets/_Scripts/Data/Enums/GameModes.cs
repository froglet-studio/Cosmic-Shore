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
        DuelForTheCell = 8,        // single-player scene retired 2026-09; id kept (never reuse)
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
        WildlifeBlitz = 26,        // single-player scene retired 2026-09; still set by BenchmarkSceneLauncher
        ProtectMission = 27,
        // 28 was MultiplayerFreestyle, the prototype multiplayer sandbox (no rules, no clock,
        // no score) that grew into the Menu_Main lava lamp, freestyle, toybox and lobby, which
        // replaced it. Removed 2026-10 with its card, scene and controller; the mode is in git.
        // 28 IS RESERVED FOREVER, exactly like 7, 31 and 47 - saved selections still carry it.
        // 29 was OnlineDuelForTheCell, the two-human vessel-swap duel. Brood Rush (38) replaced
        // it. Removed 2026-10 with its card, scene and mode preview; OnlineDuelForTheCellController
        // stays because the CoOp Wildlife Blitz scene still runs on it. 29 IS RESERVED FOREVER.
        Multiplayer2v2CoOpVsAI = 30,
        CoOpWildlifeBlitz = 32,     // retired 2026-10 (Bug_Hunt BH-5.5/5.7); scene + controller deleted; enum kept
        SkimRace = 33,
        Joust = 34,
        Scurry = 35,
        // Maelstrom (36): session-level meta that chains the domain minigames
        // (SkimRace, Joust, Scurry) into one tournament. See
        // Docs/MaelstromSystem/ARCHITECTURE.md. (7, 28, 29 and 31 stay reserved.)
        Maelstrom = 36,
        // AstroLeague (37): hypersea soccer domain minigame. See
        // _Scripts/Controller/Arcade/ASTROLEAGUE.md.
        AstroLeague = 37,
        // BroodRush (38, display name "Brood Rush"): nucleus-control domain
        // minigame - every 30s fauna wave born under your domain's nucleus claim
        // scores a point; first domain to the wave target (default 3) wins. See
        // _Scripts/Controller/Arcade/BROODRUSH.md.
        BroodRush = 38,
        // Cleave (39): the Rhino-only SLICING race. Domains race to cut a per-INTENSITY
        // target of hostile prisms out of the arena (1200 / 1200 / 1500 / 1500 - a target is
        // a fraction of the arena, so it is re-priced whenever the arena is re-cut), and the
        // arena IS the score. Intensity picks WHICH PLACE you cut rather than how much of it
        // there is - four unrelated arenas, one CellConfigDataSO each: angled panes, wide
        // wavy roads, a three-rind cage, and interlocked one-sided Mobius ribbons. Every
        // arena is built from the same SMALL prisms (SliceArenaGeometry.PrismScaleI1..I4, all
        // 2): destroying lots of little prisms is the fun, one big prism reads as low
        // poly. Every prism is plain or danger; nothing is shielded,
        // because an AI never pulls the triggers that energize a blade and hardened mass
        // would be mass an all-AI domain could never score against. See
        // _Scripts/Controller/Arcade/CLEAVE.md.
        Cleave = 39,
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

        // Breakwater (50): the Sparrow-only station race. A polar START GATE plus a closed
        // fourteen-station CIRCUIT hangs on a generated walk through the cell - each station a
        // shallow dish of plates flaring back toward you, its throat welded shut by a weave of
        // DANGER bars around an 18-unit EYE. Closing on one you pick your way through: fire a
        // skyburst and vaporise a door, flip to turret stance and saw the weave open, or thread
        // the eye and take nothing but nerve. The walls you shoot ARE the ammunition (50 hostile
        // prisms buy a rocket), so opening one door roughly funds the next and a clean thread
        // banks a rocket for a station you cannot read. Two laps of the circuit = 29 crossings;
        // the first DOMAIN's LEAD RUNNER home wins, so a teammate does not shorten the race.
        // See _Scripts/Controller/Arcade/BREAKWATER.md.
        //
        // 50, not 48: Tollway took 48 and Headlong 49 on bleeding-edge while this was in flight.
        Breakwater = 50,

        // Skein (51): the Urchin-only cable race - the first mode built around the vessel's
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
        // 51, and it took THREE renumbers to get here: Tollway took 48 and Headlong 49 while
        // this branch was in flight, and Breakwater took 50 in the window between this
        // branch's review pass and its push. That is the trap DRUMFIRE.md records, hit a
        // third time - two parallel branches each take "the next free id", git merges two
        // additions to opposite ends of one enum without a conflict, and the duplicate
        // surfaces as CS0152 in whichever switch has to tell them apart. Never assume the
        // last row is the highest, and re-run check_switch_label_collisions.py after a merge.
        Skein = 51,

        // Bloomrush (52): the Manta-only party game - the delayed-detonation race, and the
        // vessel's accessibility thesis as a mode: nobody has to learn a button. Skim the reef
        // to arm bombs, graze wildlife and rival Mantas to plant them (silently - one bomb per
        // target, tagging is denial), then reach a crystal before the fuses burn down and set
        // the whole board off at once. 120-second round; score = hostile VOLUME destroyed
        // (crystal blooms are bigger than fuse fizzles, so beating the fuse pays by
        // construction); tiebreaker = fuses beaten. First DOMAIN sum wins. See
        // _Scripts/Controller/Arcade/BLOOMRUSH.md.
        //
        // 52, not 45: this branch took 45 while Switchback took it on bleeding-edge - the
        // parallel-branch collision the Skein note above records, hit a fourth time.
        Bloomrush = 52,

        // Redline (53): the Manta-only circuit race. A closed loop of switch rings is cut
        // through the cell and every pilot flies LAPS of it in order; the first DOMAIN whose
        // LEAD RUNNER threads the last gate of the last lap wins. Every corner is cut against
        // the Manta's FULL-BOOST turn radius - the 237u circle it holds with both triggers
        // flat - and since Soar is the OVERLAP of the triggers and Yastri their DIFFERENCE, a
        // corner is one question: how much Soar is it worth? Intensity is how many corners a
        // lap asks it at. The solver is Headlong's, the cut is the Manta's. See
        // _Scripts/Controller/Arcade/REDLINE.md.
        Redline = 53,

        // WreckingBall (54, display name "Wrecking Ball"): the Scarab-only demolition race, and
        // Rampage's analog for the hull whose weapons are a BALL and a PLATE. A sphere court is
        // grown full of Rampage's five breakable flora; every bright crystal you fly through
        // becomes your ball and every prism it plows through is yours, and the juke dash's
        // cavitation plate shreds whatever is beside you. First DOMAIN to the hostile-prism
        // target wins (ScoringMetric.PrismsDestroyed). Intensity is DENSITY and SUPPLY: more
        // forest and more crystals at 1, a sparse court and a scarce ball at 4. See
        // _Scripts/Controller/Arcade/WRECKING_BALL.md.
        WreckingBall = 54,

        // Undertow (55): the Scarab-only cavitation duel - The Bends for the hull whose blast is a
        // sideways PLATE rather than a cone. Fought in Wildlife Liberation's caged arena: dash
        // beside a rival to catch them in the plate (every element stripped for four seconds -
        // one BEND) and drag the wildlife through it (a creature caught in the plate dies -
        // one KILL). Points are bends and kills together; first DOMAIN to the target wins.
        // See _Scripts/Controller/Arcade/UNDERTOW.md.
        Undertow = 55,

        // Regatta (56): the ARENA race - every playable hull on the same closed circuit of
        // switch rings, three super-shielded rails (one per domain) braided along the racing
        // line so an Urchin grinds it and a Squirrel skims it while a Manta, a Rhino, a Scarab
        // or a Sparrow flies beside it; first DOMAIN whose LEAD RUNNER threads the last gate
        // of the last lap wins. The mixed fleet is balanced by the card's per-hull STARTING
        // ELEMENTS (SO_ArcadeGame.StartingElements) and the corner mix, never by a mode-local
        // speed dial. See _Scripts/Controller/Arcade/REGATTA.md.
        Regatta = 56,

        // Broadside (57): the ARENA brawl - Regatta's fighting twin. Seven hulls loose in Dog
        // Fight's Boneyard, each fighting with the weapon it actually has: a Sparrow's guns and
        // rockets, an Urchin's chain spikes, a Rhino's energised sword, a Squirrel's joust, a
        // Dolphin's cone, a Scarab's plate, a Manta's bloom. A hit is priced by the VERB that
        // landed it and never by the hull - a round is 1, a contact strike 8, an area debuff 12,
        // a rocket 10/20/30 by how close it got - and the first DOMAIN to the point target wins
        // on ScoringMetric.CombatPoints. See _Scripts/Controller/Arcade/BROADSIDE.md.
        Broadside = 57,

        // Waystation (58): the Butterfly-only migration race. The course is a chain of
        // CLUSTERS - tight knots of switch rings - laid far apart in the cell. Inside a cluster
        // you FLY, on the fleet's slowest hull and its widest turning circle; between clusters
        // you FOLD, and the Fold has exactly one degree of freedom, the heading you leave on. So
        // the last ring of a cluster is also the aiming device for the next jump, and threading
        // it on the right LINE is worth more than threading it fast. First DOMAIN whose LEAD
        // RUNNER threads the last ring wins, on ScoringMetric.SwitchesThreaded - the gate-race
        // platform, reused whole. See _Scripts/Controller/Arcade/WAYSTATION.md.
        Waystation = 58,

        // Dustup (59): the Butterfly's CHARGE game - a dust duel. The Butterfly carries no gun;
        // its one weapon is the Scale Dust capsule hanging BELOW the hull in Dust mode, so a
        // rival is hit by flying OVER them. Every rival the dust passes through takes the
        // Charge-scaled all-element bite and pays one DUSTING (a Strike-class combat hit); first
        // DOMAIN to the point target wins, on ScoringMetric.CombatPoints. Fought in Dog Fight's
        // Boneyard. See _Scripts/Controller/Arcade/DUSTUP.md.
        Dustup = 59,

        // Tapestry (60): the Butterfly's MASS game - a TIMED painting war. Mass mode lays a wake
        // 5x-20x wide; Dust mode raids a rival's painting (destroying, shrinking or stealing
        // what it touches). The score is the mass a domain has STANDING when the clock runs out
        // (ScoringMetric.VolumeRemaining) - a live stock, so every raid moves two scores at
        // once. Fought in the bare Barren cell, with no food web. See
        // _Scripts/Controller/Arcade/TAPESTRY.md.
        Tapestry = 60,

        // Sirocco (61): the Butterfly's SPACE game - an erosion race through Rampage's cactus
        // forest. The dust's LENGTH is Space, and on opposing mass it destroys, shrinks or
        // steals; first DOMAIN to destroy the hostile-prism target wins on
        // ScoringMetric.PrismsDestroyed (Rampage's metric and machinery). See
        // _Scripts/Controller/Arcade/SIROCCO.md.
        Sirocco = 61,


        // GrizzlyCharge (62): the Grizzly-only assault mode (in development). Authored as 42
        // on grizzly-v2, moved to 44 when Bends/ScarabScramble took 42/43, to 54 at the
        // 2026-09-12 merge after Salvo took 44 and Switchback 45, and to 62 at the 2026-10-02
        // merge after WreckingBall took 54 (bleeding-edge had run on to Sirocco = 61) - the
        // parallel-branch collision the Skein and Bloomrush notes above record, hit a SIXTH
        // time. The ArcadeGameGrizzlyCharge asset's serialized Mode moved with it each time.
        GrizzlyCharge = 62,

        // GrizzlyTime (63): the Grizzly-only circuit race - Redline's shape (a lapped gate
        // circuit on the shared HeadlongCircuit solver) cut against the Grizzly riding its own
        // trigger-bomb blasts rather than a boost: a corner asks how much launch it is worth. See
        // _Scripts/Controller/Arcade/GRIZZLYTIME.md.
        GrizzlyTime = 63,

        // Slingshot (64): the Stoat-only circuit race - Redline's shape (a lapped gate circuit on
        // the shared HeadlongCircuit solver) cut for a 60 u/s hull whose only speed is the
        // attractor-repulsor wormhole pair it slings on its triggers: a corner asks where to lay
        // the attractor. See _Scripts/Controller/Arcade/SLINGSHOT.md.
        Slingshot = 64,

        // ADDING A MODE? Bump EnumIntegrityTests.GameModes_HasExpectedMemberCount (currently
        // 60) in the same commit, and take the next free ID -- 7, 28, 29, 31 and 47 stay reserved
        // forever.
        // That test is a deliberate tripwire, not an obstacle: it exists so a new member can
        // never land without someone confirming the ID is safe for saved selections.
    }
}