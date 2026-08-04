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

        // Ribcage
        // {0}=leading domain, {1}=that domain's bars smashed, {2}=cage target
        RibcageBroodReleased = 50,   // 25% - the grazer swarm hatches for the leader
        RibcagePackReleased = 51,    // 50% - the predator joins and the cell goes Frenzy
        RibcageLeaderChanged = 52,   // the swarm changes hands (and therefore targets)
    }
}
