namespace CosmicShore.Game.Arcade.Party
{
    /// <summary>
    /// Phases of a party game session, synced across the network.
    /// </summary>
    public enum PartyPhase
    {
        /// <summary>Players are in the lobby, flying freely and waiting for others.</summary>
        Lobby = 0,

        /// <summary>All players/AI are present. Waiting for everyone to ready up.</summary>
        WaitingForReady = 1,

        /// <summary>Game is being randomized and announced.</summary>
        Randomizing = 2,

        /// <summary>Pre-round countdown is running.</summary>
        Countdown = 3,

        /// <summary>A mini-game round is actively being played.</summary>
        Playing = 4,

        /// <summary>A round just ended; showing results in the party panel.</summary>
        RoundResults = 5,

        /// <summary>All rounds are complete; showing final standings.</summary>
        FinalResults = 6,

        /// <summary>Mini-game environment is active; waiting for ready to start gameplay.</summary>
        MiniGameReady = 7,
    }
}
