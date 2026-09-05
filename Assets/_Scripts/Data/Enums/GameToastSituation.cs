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

        // PeelTheCage ("Peel the Cage")
        // {0}=leading domain, {1}=that domain's prisms destroyed, {2}=destruction target
        // Values 50/51 were PeelTheCageBroodReleased/PeelTheCagePackReleased when the mode ran a fauna
        // ladder; the fauna were removed from the level and the same two rungs now mark pure
        // race progress. Renamed rather than retired because no GameToastConfigSO authors them
        // yet, so nothing serialized points at the old names.
        PeelTheCageQuarterPeeled = 50,   // leader is a quarter of the way to the target
        PeelTheCageHalfPeeled = 51,      // leader is halfway
        PeelTheCageLeaderChanged = 52,   // the lead changes hands after a milestone

        // Wildlife Liberation. {0} = player name, {1} = kills, {2} = target.
        WildlifeHuntQuarter = 53,    // the leading hunter is a quarter of the way to the target
        WildlifeHuntHalf = 54,       // the leading hunter is halfway
        WildlifeLeadChanged = 55,    // the lead changes hands after a milestone
        WildlifeCoreBreached = 56,   // a hunter has reached the innermost room ({0} = player name)

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
    }
}
