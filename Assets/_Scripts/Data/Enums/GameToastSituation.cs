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

        // Tandava - the cell's NARRATOR (authored as 128-138; moved to 137-147 at the bleeding-edge merge, where
        // the domain race beats, Astro League and Salvo had taken 128-136). {0} = the line (authored in TandavaSettings and sent as an index, so every
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
        TandavaSevered = 148,           // a cut parted the body and the piece crawled off: {0} = SeveredLine
        TandavaRejoined = 149,          // the severed piece got home and grafted back on: {0} = RejoinedLine
        TandavaSuccession = 150,        // the body was cut away and the piece took its form: {0} = SuccessionLine
        TandavaLearned = 151,           // a wound taught it something (once per wound): {0} = that wound's Learned line

        // END-OF-GAME LOBBY. Shared across every multiplayer mode, so 100+ rather than crowding
        // the per-mode blocks. {0} = player name, {1} = how many have asked so far, {2} = how many
        // humans are in the match.
        RematchRequested = 100,     // a client pressed Play Again - a VOTE the host can act on
        // A pilot left mid-match and the AI took their ship. {0} = the departed player's name.
        PilotHandedToAI = 101,
    }
}
