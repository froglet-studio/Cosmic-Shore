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

        // Race modes (Skim Race / HexRace)
        Overtake = 20,
        NewRaceLeader = 21,

        // Any party game with the comeback system (shown only where authored)
        ComebackActivated = 30,

        // Brood Rush (NucleusRush)
        BroodWaveScored = 40,

        // Ribcage ("Peel the Cage")
        // {0}=leading domain, {1}=that domain's prisms destroyed, {2}=destruction target
        // Values 50/51 were RibcageBroodReleased/RibcagePackReleased when the mode ran a fauna
        // ladder; the fauna were removed from the level and the same two rungs now mark pure
        // race progress. Renamed rather than retired because no GameToastConfigSO authors them
        // yet, so nothing serialized points at the old names.
        RibcageQuarterPeeled = 50,   // leader is a quarter of the way to the target
        RibcageHalfPeeled = 51,      // leader is halfway
        RibcageLeaderChanged = 52,   // the lead changes hands after a milestone

        // Wildlife Liberation. {0} = player name, {1} = kills, {2} = target.
        WildlifeHuntQuarter = 53,    // the leading hunter is a quarter of the way to the target
        WildlifeHuntHalf = 54,       // the leading hunter is halfway
        WildlifeLeadChanged = 55,    // the lead changes hands after a milestone
        WildlifeCoreBreached = 56,   // a hunter has reached the innermost room ({0} = player name)

        // Dog Fight. {0} = leading domain, {1} = that domain's points, {2} = point target.
        DogFightQuarterDown = 57,    // the leading domain is a quarter of the way to the target
        DogFightHalfDown = 58,       // the leading domain is halfway
        DogFightLeadChanged = 59,    // the lead changes hands after a milestone

        // Scarab Scramble. Goal/bank: {0} = scorer name, {1} = their domain's goals, {2} = target
        // ({3} = wall bounces on the bank goal). Milestones/lead: {0} = leading domain,
        // {1} = that domain's goals, {2} = target. Cap: {0} = the capped player's name.
        ScarabScrambleGoal = 60,          // a forged ball threaded a hoop
        ScarabScrambleMatchPoint = 61,    // the leading domain is one goal from winning
        ScarabScrambleLeadChanged = 62,   // the lead changes hands late in the match
        ScarabScrambleForgeHint = 63,     // idle hint: follow the marker to the bright crystal
        ScarabScrambleRollHint = 64,      // idle hint: roll your ball through any glowing ring
        ScarabScrambleBankGoal = 65,      // a goal off 2+ wall caroms — the signature screamer
        ScarabScrambleBallCap = 66,       // forge refused at the live-ball cap (targeted at the pilot)
    }
}
