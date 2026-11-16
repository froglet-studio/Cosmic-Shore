namespace CosmicShore.Data
{
    // Remember folks, only you can prevent Unity from arbitrarily swapping enum values in files.
    // Always assign a static numeric value to your enum types
    /// <summary>
    /// Every situation the in-game toast system can announce. A situation only produces a
    /// visible toast when the current game mode's <c>GameToastConfigSO</c> (or the shared
    /// config) authors a <c>GameToastDefinition</c> for it - unauthored situations are
    /// silently skipped, which is how a mode (e.g. Scurry) opts out of a toast entirely.
    /// </summary>
    public enum GameToastSituation
    {
        None = 0,

        // Shared across all modes (authored in the shared config)
        PlayerJoined = 1,
        PlayerReady = 2,
        PlayerDisconnected = 3,

        // Joust
        Joust = 10,
        JoustIdleHint = 11,

        // Race modes (Skim Race / SkimRace)
        Overtake = 20,
        NewRaceLeader = 21,

        // Any party game with the comeback system (shown only where authored)
        ComebackActivated = 30,

        // Brood Rush (BroodRush)
        BroodWaveScored = 40,

        // Cleave
        // {0}=leading domain, {1}=that domain's prisms destroyed, {2}=destruction target
        // Values 50/51 have now been renamed TWICE and both renames were free for the same
        // reason: no GameToastConfigSO authors these situations yet, so nothing serialized
        // points at any of the old names. They were CleaveBroodReleased/CleavePackReleased when
        // the mode ran a fauna ladder, then ...QuarterPeeled/...HalfPeeled while the mode was
        // called Peel the Cage. "Peeled" described ONE of the four arenas the mode now ships -
        // you do not peel a wave sheet - so the rungs are named for what they actually measure.
        CleaveQuarterCut = 50,      // leader is a quarter of the way to the target
        CleaveHalfCut = 51,         // leader is halfway
        CleaveLeaderChanged = 52,   // the lead changes hands after a milestone

        // Wildlife Liberation. 53-55: {0} = leading DOMAIN (the controller posts the domain, as
        // every domain race does - this comment used to say "player name"), {1} = that domain's
        // kills, {2} = target. 56: {0} = the pilot, {1} = their domain; posted once a match.
        WildlifeHuntQuarter = 53,    // the leading domain is a quarter of the way to the target
        WildlifeHuntHalf = 54,       // the leading domain is halfway
        WildlifeLeadChanged = 55,    // the lead changes hands after a milestone
        WildlifeCoreBreached = 56,   // the first pilot to fly into the innermost cage's room

        // Dog Fight. {0} = leading domain, {1} = that domain's points, {2} = point target.
        DogFightQuarterDown = 57,    // the leading domain is a quarter of the way to the target
        DogFightHalfDown = 58,       // the leading domain is halfway
        DogFightLeadChanged = 59,    // the lead changes hands after a milestone

        // The Bends. {0} = leading domain, {1} = that domain's points, {2} = point target.
        BendsQuarterBent = 60,       // the leading domain is a quarter of the way to the target
        BendsHalfBent = 61,          // the leading domain is halfway
        BendsLeadChanged = 62,       // the lead changes hands after a milestone
        // Scarab Scramble. Goal/bank: {0} = scorer name, {1} = their domain's goals, {2} = target
        // ({3} = wall bounces on the bank goal). Milestones/lead: {0} = leading domain,
        // {1} = that domain's goals, {2} = target. Overload takes no args.
        ScarabScrambleGoal = 63,          // a forged ball threaded a hoop
        ScarabScrambleMatchPoint = 64,    // the leading domain is one goal from winning
        ScarabScrambleLeadChanged = 65,   // the lead changes hands late in the match
        ScarabScrambleForgeHint = 66,     // idle hint: follow the marker to the bright crystal
        ScarabScrambleRollHint = 67,      // idle hint: roll your ball through any glowing ring
        ScarabScrambleBankGoal = 68,      // a goal off 2+ wall caroms — the signature screamer
        // A cell reached its ball limit, so EVERY loose ball in it detonated regardless of
        // domain (AstroLeagueBall.OnCellOverload). Court-wide and player-agnostic - it is
        // broadcast to every peer, so it names nobody and wears no domain colour.
        ScarabScrambleBallCap = 69,

        // Tollway. Toll/chain: {0} = the pilot who PLANTED the ring, {1} = their domain's tolls,
        // {2} = target ({3} = how many rings this one ball has paid, on the chain). Match
        // point / lead: {0} = leading domain, {1} = its tolls, {2} = target. The ring hint takes
        // no args.
        TollwayToll = 70,          // a ball threaded somebody's ring and paid its planter
        TollwayChain = 71,         // ONE ball paid 2+ tolls inside the chain window
        TollwayMatchPoint = 72,    // the leading domain is one toll from winning
        TollwayLeadChanged = 73,   // the lead changes hands
        TollwayRingHint = 74,      // idle hint: plant a ring - ANY ball through it pays you
        TollwayNoAnchor = 75,      // the press was refused: no free plant heart on this line

        // Per-player STAT toasts, produced by StatToastDriver from the replicated RoundStats on
        // every peer (nothing crosses the wire). {0} = player name, {1} = the player's new
        // total, {2} = this step's increase, {3} = the mode's objective target (0 when the
        // mode has none). A config entry's everyN says how often the total has to cross a
        // multiple before the toast fires (Skim Race announces every crystal, Scurry every 10).
        CrystalCollected = 80,          // CrystalsCollected rose
        RocketHit = 81,                 // MissileHitsLanded rose - a skyburst reached a pilot (Dog Fight)
        BendLanded = 82,                // DebuffHitsLanded rose - a blast debuffed a pilot (The Bends)
        PrismsDestroyedMilestone = 83,  // HostilePrismsDestroyed crossed a multiple of everyN
        LifeformKilled = 84,            // LifeformsKilled rose
        PrismsStolenMilestone = 85,     // PrismStolen crossed a multiple of everyN (Hijack)
        StrikeLanded = 86,              // StrikeHitsLanded rose - a ball, sword or plate reached a pilot (Jackstraw)

        // Bloomrush. The cash-out is the mode's whole payoff, so it is the one thing worth
        // announcing: {0} = the pilot, {1} = how many bombs the crystal just cashed. 90/91
        // rather than 70/71: Tollway took those on bleeding-edge while this was in flight.
        BloomrushKabloom = 90,
        // Idle hint: the loop is buttonless, so a new pilot has nothing to press and needs
        // telling what flying into things does.
        BloomrushStingHint = 91,

        // Wrecking Ball. Lead: {0} = leading domain, {1} = its prisms destroyed, {2} = target.
        // The two hints take no args.
        WreckingBallLeadChanged = 92,   // the lead changes hands past the first milestone
        WreckingBallForgeHint = 93,     // idle hint: fly through a bright crystal to forge a ball
        WreckingBallDashHint = 94,      // idle hint: flick the right stick beside the forest

        // Undertow. {0} = leading domain, {1} = that domain's points, {2} = point target. The
        // hint takes no args.
        UndertowQuarter = 95,           // the leading domain is a quarter of the way to the target
        UndertowHalf = 96,              // the leading domain is halfway
        UndertowLeadChanged = 97,       // the lead changes hands after a milestone
        UndertowDashHint = 98,          // idle hint: dash beside a rival to catch them in the plate

        // Regatta. Two idle hints, no args: the mode's whole tutorial is "the rail in your
        // colour is your speed" and which verb your hull uses on it. 110+ because the lobby
        // block below took 100 and the per-mode blocks under it are full.
        RegattaRailHint = 110,          // idle hint: the rail in your colour is the racing line
        RegattaLaneHint = 111,          // idle hint: ride it, skim it, or fly beside it - by hull

        // Broadside. {0} = leading domain, {1} = that domain's points, {2} = point target.
        // The hints take no args and are per-VERB rather than per-hull, because seven hulls
        // share four ways of landing a hit and a hint per hull would be seven hints nobody
        // reads.
        BroadsideQuarter = 112,         // the leading domain is a quarter of the way to the target
        BroadsideHalf = 113,            // the leading domain is halfway
        BroadsideLeadChanged = 114,     // the lead changes hands after a milestone
        BroadsideVerbHint = 115,        // idle hint: your hull already has a weapon - use it
        BroadsideCloseHint = 116,       // idle hint: a contact strike pays more than a round

        // Waystation. TWO IDLE HINTS AND NOTHING ELSE, and when it was authored the absence was
        // the decision: the gate-race platform had no gate-threaded hook, so a milestone or
        // lead-change situation here would have had no poster. It has one now -
        // GateRaceController runs DomainRaceToasts, so the DomainRace* beats below (128+) are
        // posted in every gate race, Waystation and Regatta included; their generator-owned
        // configs simply do not author them yet. An idle hint needs no poster at all (the toast
        // system fires it off idleSeconds), which is why Regatta authored only hints too. Both take no args, and they are the mode's two
        // verbs: a new pilot who never finds the Fold simply orbits the first cluster forever.
        WaystationRingHint = 117,       // idle hint: thread every ring around you
        WaystationFoldHint = 118,       // idle hint: hold the fold and aim at the next cluster

        // Dustup. {0} = leading domain, {1} = that domain's dustings, {2} = point target. The
        // hint takes no args: the mode's whole tutorial is "the dust hangs BELOW you".
        DustupQuarter = 119,            // the leading domain is a quarter of the way to the target
        DustupHalf = 120,               // the leading domain is halfway
        DustupLeadChanged = 121,        // the lead changes hands after a milestone
        DustupAboveHint = 122,          // idle hint: switch to Dust mode and fly OVER a rival

        // Tapestry. {0} = leading domain, {1} = that domain's standing volume. The two hints
        // take no args and are the mode's two verbs: paint wide, raid narrow.
        TapestryLeadChanged = 123,      // the lead changes hands (sampled, after the opening)
        TapestryPaintHint = 124,        // idle hint: Mass mode paints a wide wake - that is your score
        TapestryRaidHint = 125,         // idle hint: Dust mode over a rival's paint takes it away

        // Sirocco. {0} = leading domain, {1} = its prisms destroyed, {2} = target. The hint takes
        // no args.
        SiroccoLeadChanged = 126,       // the lead changes hands past the first milestone
        SiroccoDustHint = 127,          // idle hint: Dust mode, fly low over the forest

        // DOMAIN RACE BEATS - shared, posted by DomainRaceToasts from any controller that runs it
        // (the gate-race family through GateRaceController; Rampage, Salvo and Hijack directly).
        // A local poll over replicated RoundStats on every peer, so nothing crosses the wire.
        // Generic rather than per-mode because the beats ARE generic: each mode picks the ones it
        // wants by authoring them, and an unauthored one shows nothing. {0} = leading domain,
        // {1} = its score, {2} = target, {3} = its best single pilot (a gate race's lead runner).
        DomainRaceQuarter = 128,        // the leading domain is a quarter of the way to the target
        DomainRaceHalf = 129,           // the leading domain is halfway
        DomainRaceLeadChanged = 130,    // the lead changes hands after the quarter beat
        DomainRaceHomeStretch = 131,    // the leading domain is within the last few of the target
        DomainRaceFinalLap = 132,       // a lapped course: the leading domain starts its last lap

        // Astro League. Goal: {0} = scorer name, {1} = their domain's goals, {2} = goal limit.
        // Match point: {0} = leading domain, {1} = its goals, {2} = goal limit. Golden goal takes
        // no args. All three are posted from the controller's existing announcer ClientRpcs.
        AstroLeagueGoal = 133,          // a ball crossed a goal line and somebody was credited
        AstroLeagueMatchPoint = 134,    // a domain is one goal from the limit
        AstroLeagueGoldenGoal = 135,    // the clock ran out level: next goal wins

        // Salvo. {0} = the pilot whose omni crystal reloaded the wing, {1} = their domain. Posted
        // only when the domain fields a WINGMAN to reload - the mode's reason to play together.
        SalvoWingReload = 136,

        // Tandava - the cell's NARRATOR (authored as 128-138 on its branch, renumbered to 137-147 at the 2026-10-25
        // merge after the domain-race beats, Astro League and Salvo took 128-136). {0} = the line (authored in
        // TandavaSettings and sent as an index, so every
        // peer reads its own copy); the hint takes no args.
        TandavaMatchStart = 137,        // the hunt begins: {0} = TandavaSettings.StartLine
        TandavaFormTaken = 138,         // the swarm took a new form: {0} = that form's line, naming the variant drawn
        TandavaCompleted = 139,         // the Antlion ate its last feast - the swarm won: {0} = CompletedLine
        TandavaBroken = 140,            // the pilots won: {0} = WonLine, DanceBrokenLine or HeldOffLine
        TandavaFeeding = 141,           // its first meal - its guards are out, strike the body: {0} = FeedingLine
        TandavaDenyHint = 142,          // idle hint: break its meals - hit the body while it eats
        TandavaMealBroken = 143,        // the pilots hurt it at the table and it bolted: {0} = MealBrokenLine
        TandavaRising = 144,            // banked, it rises into the Lord of the Dance where it stands: {0} = RisingLine
        TandavaHaloLit = 145,           // the halo lit and the drum started: {0} = HaloLitLine
        TandavaHaloBroken = 146,        // the first halo ring, and the last-but-one, broken: {0} = the line
        TandavaLunge = 147,             // the creature turned on a pilot (its first lunges): {0} = LungeLine
        // Tandava: Severing (GameModes.TandavaSevering, Voyage 2) raises every Tandava toast above plus these five - the
        // ids PR #1046 claimed for them on claude/tandava-severing, kept so that branch and this fork agree
        TandavaSevered = 148,           // a cut parted the body and the piece crawled off: {0} = SeveredLine
        TandavaRejoined = 149,          // the severed piece got home and grafted back on: {0} = RejoinedLine
        TandavaSuccession = 150,        // the body was cut away and the piece took its form: {0} = SuccessionLine
        TandavaLearned = 151,           // a wound taught it something (once per wound): {0} = that wound's Learned line
        TandavaChimera = 152,           // on its way up it became the whale-jelly chimera: {0} = ChimeraLine

        // Serpent Time, the dirty race (Docs/Studios/SERPENT_TIME.md). Posted to the LOCAL pilot only. 153-155 (rounds,
        // no round, wall hit) were retired with the round economy and the wall cost on 2026-10-31: the mode never
        // changes the vessel's kit. Do not reuse them.
        SerpentTimeWallHint = 156,      // once, at the start: how the dirty race is played

        // Hood (SerpentHood, was Serpent Gulag), the sniper duel (_Scripts/Controller/Arcade/SERPENTHOOD.md). Its race
        // beats are the shared 128-130 (DomainRaceToasts); a snipe is 82 (DebuffHitsLanded rose). Posted to the LOCAL
        // pilot only.
        SerpentHoodScopeHint = 157,     // once, at the start: hold LT to scope, RT to fire; the heart reloads you

        // LanternRun, the Angler's Time race (_Scripts/Controller/Arcade/LANTERNRUN.md). Its race beats are the shared
        // 129-132 (DomainRaceToasts). Posted to the LOCAL pilot only.
        LanternRunPaintHint = 158,    // once, at the start: paint the two clusters, throw both groups, dash after them

        // Jackstraw, the Thresher's charge game (_Scripts/Controller/Arcade/JACKSTRAW.md). Its race beats are the shared
        // 128-130 (DomainRaceToasts); a strike is 86 (StrikeHitsLanded rose). Posted to the LOCAL pilot only.
        JackstrawHint = 160,            // once, at the start: strike a rival with the ball or chain; fuel is in the clearings

        // Flatline, the Brittle Star's charge game (_Scripts/Controller/Arcade/FLATLINE.md). Its race beats are the shared
        // 128-130 (DomainRaceToasts); a cord or saber Strike is 86 (StrikeLanded). Posted to the LOCAL pilot only.
        FlatlineHint = 159,           // once, at the start: panes stop rounds, cords cut through, get off a ring's plane
        // Kelpwood, the Angler's charge game (_Scripts/Controller/Arcade/KELPWOOD.md). Its race beats are the shared
        // 128-130 (DomainRaceToasts); a bite is 86 (StrikeHitsLanded rose). Posted to the LOCAL pilot only.
        KelpwoodHint = 161,             // once, at the start: lock a rival with the eraser; tame the herds in the kelp
        // Corona, the Manta's charge game (_Scripts/Controller/Arcade/CORONA.md). Its race beats are the shared
        // 128-130 (DomainRaceToasts); a bloom hit is 82 (DebuffHitsLanded rose). Posted to the LOCAL pilot only.
        CoronaHint = 162,               // once, at the start: skim a shell to arm, graze to plant, cash at the heart
        // Glasshouse, the Angler's Mass game (_Scripts/Controller/Arcade/GLASSHOUSE.md). Its lead change is the shared
        // 130 (DomainRaceLeadChanged), sampled by the controller because a timed mode has no target. Posted to the
        // LOCAL pilot only.
        GlasshouseHint = 164,             // once, at the start: take an omni crystal, let go of the lure to plant, light it to grow

        // Harvest, the Thresher's space game (_Scripts/Controller/Arcade/HARVEST.md). Its race beats are the shared
        // 128-130 (DomainRaceToasts); a prism milestone is 83 (HostilePrismsDestroyed). Posted to the LOCAL pilot only.
        HarvestHint = 163,              // once, at the start: press A to break; mow the stalks, smash the core
        // Shatterplane, the Brittle Star's space game (_Scripts/Controller/Arcade/SHATTERPLANE.md). Its race beats are the
        // shared 128-130 (DomainRaceToasts). Posted to the LOCAL pilot only. Took 163 first (Harvest landed with it);
        // 164 is GlasshouseHint's in flight (claude/peaceful-cerf-vpl3x8).
        ShatterplaneHint = 165,         // once, at the start: square up to a plate and the ring erases it whole
        // Chum, the Angler's space game (_Scripts/Controller/Arcade/CHUM.md). 166 is its row in
        // Docs/ElementalLoop/GAME_KIT_ROSTER.md (166-202 are reserved there, one per missing game). Posted to the LOCAL pilot only.
        ChumHint = 166,                 // once, at the start: tame a school, paint a shoal, let go to throw it
        // Starweave, the Brittle Star's mass game (_Scripts/Controller/Arcade/STARWEAVE.md). 167 is its row in
        // Docs/ElementalLoop/GAME_KIT_ROSTER.md. Posted to the LOCAL pilot only.
        StarweaveHint = 167,            // once, at the start: Creation Mode lays five trails; what stands at the whistle scores
        // Haul, the Thresher's mass game (_Scripts/Controller/Arcade/HAUL.md). 168 is its row in
        // Docs/ElementalLoop/GAME_KIT_ROSTER.md. Posted to the LOCAL pilot only.
        HaulHint = 168,                 // once, at the start: swing the ball through a bale to steal it into your colour

        // Hedgerow, the Serpent's mass game (_Scripts/Controller/Arcade/HEDGEROW.md). 169 is its row in
        // Docs/ElementalLoop/GAME_KIT_ROSTER.md. Posted to the LOCAL pilot only.
        HedgerowHint = 169,             // once, at the start: stop dead short of a hedge and your wall eats along it

        // Longshot, the Serpent's space game (_Scripts/Controller/Arcade/LONGSHOT.md). 170 is its row in
        // Docs/ElementalLoop/GAME_KIT_ROSTER.md. Posted to the LOCAL pilot only.
        LongshotHint = 170,             // once, at the start: line up along a lance and the round breaks the whole run
        // The Orca's three element games (PODHUNT.md, ICESHELF.md, DEPTHCHARGE.md). Ids 174-176 reserved by
        // Docs/ElementalLoop/GAME_KIT_ROSTER.md (rows 9-11). Race beats are the shared 128-130 (DomainRaceToasts).
        PodHuntHint = 174,              // once, at the start: fly your missiles into rival hulls; the bomb counts too
        IceShelfHint = 175,             // once, at the start: lay trail and drop shields late - only what stands counts
        DepthChargeHint = 176,          // once, at the start: burst missiles and the bomb in the canopy
        // The Swordfish's three games (SPEARLINE.md, RIPOSTE.md, PALISADE.md), ids reserved in
        // Docs/ElementalLoop/GAME_KIT_ROSTER.md. Posted to the LOCAL pilot only, once, at the start.
        SpearlineHint = 171,            // a sealed ring counts only through a hole: swing the bill on the way through
        RiposteHint = 172,              // a sword hit lands only while you are faster: cut a straight lane, ramp, strike
        PalisadeHint = 173,             // light the blade and let go to lay afterimages; cut the rivals' walls down
        // The Shrike's games (Docs/ElementalLoop/GAME_KIT_ROSTER.md rows 12-14 reserved 177-179). Posted to the LOCAL
        // pilot only. Larder's race beats are the shared 128-130 and a missile hit is 81 (MissileHitsLanded rose);
        // Thornbush's lead change is the shared 130, sampled by its controller (a timed mode has no target);
        // Lancefall's race beats are the shared 128-130.
        LarderHint = 177,               // once, at the start: paint a rival and every missile plant launches at them
        ThornbushHint = 178,            // once, at the start: dock on a stick, press Mass to leave a nest; hold your mass
        LancefallHint = 179,            // once, at the start: take a plate edge-on and the lance breaks a whole strip

        // Shockfront and Fulmen, the Stoat's charge and space games (_Scripts/Controller/Arcade/SHOCKFRONT.md, FULMEN.md).
        // Their race beats are the shared 128-130 (DomainRaceToasts); a ripple hit is 82 (DebuffHitsLanded rose), a
        // prism milestone 83. Ids reserved by Docs/ElementalLoop/GAME_KIT_ROSTER.md (rows 29-30). LOCAL pilot only.
        ShockfrontHint = 194,           // once, at the start: sling round and let the pair close beside a rival
        FulmenHint = 195,               // once, at the start: take the omni crystals in the heart; each throws a bolt
        // The Honeyguide's element games (FLITSTRIKE.md, HONEYCOMB.md, WOODPECKER.md), at the toast ids
        // Docs/ElementalLoop/GAME_KIT_ROSTER.md reserved (166-202 are that roster's). Flitstrike's and Woodpecker's race
        // beats are the shared 128-130 (DomainRaceToasts); Honeycomb's lead change is the shared 130, sampled by its
        // controller because a timed mode has no target. Posted to the LOCAL pilot only.
        FlitstrikeHint = 183,           // once, at the start: hold LT to see the dart, let go to Flit through a rival
        HoneycombHint = 184,            // once, at the start: an omni crystal is a hive; what stands in your colour scores
        WoodpeckerHint = 185,           // once, at the start: hold RT to aim, let go to peck the hostile forest
        // Stingherd and Trawl, the Jacks' Charge and Space games (STINGHERD.md, TRAWL.md). Race beats are the shared 128-130
        // (DomainRaceToasts); ids reserved by the game kit roster (Docs/ElementalLoop/GAME_KIT_ROSTER.md rows 27-28).
        StingherdHint = 192,            // once, at the start: herd your danger creatures into a rival hull to sting it
        TrawlHint = 193,                // once, at the start: drag prisms in the net and smash them into each other
        // Railrush, the Urchin's charge game (_Scripts/Controller/Arcade/RAILRUSH.md). Its race beats are the shared
        // 128-130 (DomainRaceToasts). Posted to the LOCAL pilot only. 199 is the id GAME_KIT_ROSTER.md reserved (row 34).
        RailrushHint = 199,             // once, at the start: grind your colour's rail, launch, spike a rival at speed
        // The Rhino's three games (GAME_KIT_ROSTER.md rows 31-33 reserved 196-198). Each is posted once, at the start, to
        // the LOCAL pilot only. Their race beats are the shared 128-130 (Bullrush: DomainRaceToasts; Windrow: the lead
        // change, sampled by its controller because a timed mode has no target), Landslide's are Cleave's 50-52.
        BullrushHint = 196,             // a gore is a ram on a rival from behind, faster than it: wind up the Ramp Spool
        WindrowHint = 197,              // your standing trail is your score; plough through rival rows to break them
        LandslideHint = 198,            // ploughed prisms break what they hit; a wider blade catches more ribs
        // Standoff, the Grizzly's charge game (_Scripts/Controller/Arcade/STANDOFF.md). Its race beats are the shared
        // 128-130 (DomainRaceToasts); a blast hit is 81 (MissileHitsLanded). Posted to the LOCAL pilot only. 200 is the
        // hint id the game kit roster reserved for it (Docs/ElementalLoop/GAME_KIT_ROSTER.md row 35).
        StandoffHint = 200,             // once, at the start: a bomb that touches a rival sticks; pull and release to blow it
        // The Wrasse and Grouper's reef games (_Scripts/Controller/Arcade/WRASSEREEF.md), ids reserved by
        // Docs/ElementalLoop/GAME_KIT_ROSTER.md (rows 21-23). Race beats are the shared 128-130 (DomainRaceToasts; Reefbuild,
        // timed, posts only 130 from its own lead sampler). Posted to the LOCAL pilot only.
        CleanerRamHint = 186,           // once, at the start: loop Charge motes, then ram a rival on the kite's board
        ReefbuildHint = 187,            // once, at the start: loop Mass motes to lay your reef; what stands at the whistle scores
        BlastlineHint = 188,            // once, at the start: loop Space motes, the blast clears hostile prisms where it closed
        // The Goby & Pistol's games (SNAP.md, DEN_BUILDER.md, RIPTIDE.md; ids reserved in GAME_KIT_ROSTER.md). Race beats
        // are the shared 128-130 (DomainRaceToasts; Den Builder samples its own lead change, 130). LOCAL pilot only.
        SnapHint = 189,                 // once, at the start: lock a rival from the Goby, fire rings from the Pistol when they are on screen
        DenBuilderHint = 190,           // once, at the start: jump the trail for energy, rest at a perch to build a den
        RiptideHint = 191,              // once, at the start: ride through the plates, the undertow pulls them in, the rings smash them
        // The Jewel Wasp's two games (GAME_KIT_ROSTER.md rows 36-37 reserved 201-202). Each is posted once, at the start,
        // to the LOCAL pilot only. Marionette's race beats are the shared 128-130 (DomainRaceToasts) and a hit is 81;
        // Gorge's lead change is the shared 130, sampled by its controller (a timed mode has no target).
        MarionetteHint = 201,           // once, at the start: sting a creature, ride it into a rival pilot and strike
        GorgeHint = 202,                // once, at the start: ride a creature to a shoal and hold RT to suck it in
        // Redline, the Manta's Time game (_Scripts/Controller/Arcade/REDLINE.md). Its race beats are the shared 129-132
        // (DomainRaceToasts). Posted to the LOCAL pilot only, from its own threads (RedlineController.Heat). 360-369 is
        // Redline's band (the 2026-11-11 round 5 sessions each hold their own); 367-369 are free.
        RedlineHint = 360,              // once, at the start: Soar is lit, bury both triggers, thread the hearts
        RedlineStreak = 361,            // a streak of clean threads is called (3, 8, 16, then every 16): {0} = the streak
        RedlineLapSplit = 362,          // a lap completed: {0} = the lap, {1} = its time in seconds
        RedlineBestLap = 363,           // a lap beat this race's best: {0} = the lap, {1} = its time in seconds
        RedlineCleanLap = 364,          // every gate of a lap threaded clean: {0} = the lap
        RedlineSoarDark = 365,          // the pilot's Time fell below Soar's relock (4): Soar is dark until a crystal
        RedlineRedline = 366,           // once a race: the pilot reached the redline: {0} = the speed in u/s

        // The Meerkat's games (Docs/ElementalLoop/GAME_KIT_ROSTER.md rows 15-17). Their race beats are the shared 128-130
        // (DomainRaceToasts; Warren samples its own lead change, 130, because a timed mode has no target). Posted to the
        // LOCAL pilot only.
        SentryHint = 180,               // once, at the start: Y for Sentinel Mode, form solitons where rivals pass
        WarrenHint = 181,               // once, at the start: every soliton rings its exit with your prisms; raid theirs
        CaveInHint = 182,               // once, at the start: form a soliton beside a plate and fire it into the next

        // END-OF-GAME LOBBY. Shared across every multiplayer mode, so 100+ rather than crowding
        // the per-mode blocks. {0} = player name, {1} = how many have asked so far, {2} = how many
        // humans are in the match.
        RematchRequested = 100,     // a client pressed Play Again - a VOTE the host can act on
        // A pilot left mid-match and the AI took their ship. {0} = the departed player's name.
        PilotHandedToAI = 101,
    }
}
