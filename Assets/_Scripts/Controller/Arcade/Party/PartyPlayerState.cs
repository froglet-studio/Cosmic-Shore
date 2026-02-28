namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Tracks cumulative party-level state for a single player across all rounds.
    /// </summary>
    [System.Serializable]
    public class PartyPlayerState
    {
        public string PlayerName;
        public Domains Domain;
        public int GamesWon;
        public bool IsAIReplacement;
        public bool IsReady;
    }
}
