using System.Collections.Generic;

namespace CosmicShore.Game.Arcade.Party
{
    /// <summary>
    /// Stores the result of a single party round.
    /// </summary>
    [System.Serializable]
    public class PartyRoundResult
    {
        public int RoundIndex;
        public GameModes MiniGameMode;
        public string WinnerName;
        public Domains WinnerDomain;
        public List<PartyRoundPlayerScore> PlayerScores = new();

        public bool IsCompleted => !string.IsNullOrEmpty(WinnerName);
    }

    /// <summary>
    /// A single player's score for a single round.
    /// </summary>
    [System.Serializable]
    public class PartyRoundPlayerScore
    {
        public string PlayerName;
        public Domains Domain;
        public float Score;
        public bool IsAIReplacement;
    }
}
