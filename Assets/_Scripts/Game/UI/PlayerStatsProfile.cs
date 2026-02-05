using System;
using System.Collections.Generic;

namespace CosmicShore.Game.Analytics
{
    [Serializable]
    public class PlayerStatsProfile
    {
        // [Visual Note] Global accumulation stats (SP + MP combined)
        public int TotalGamesPlayed;
        public int TotalPlayAgainPressed;
        
        public int LifetimeCrystalsCollected;
        public int LifetimeLifeFormsKilled;
        
        // [Visual Note] Key = "GameMode_Intensity_MP" or "GameMode_Intensity_SP"
        public Dictionary<string, int> HighScores = new();

        /// <summary>
        /// Returns true if the new score is a personal best
        /// </summary>
        public bool TryUpdateHighScore(string modeKey, int newScore)
        {
            if (HighScores.TryGetValue(modeKey, out int currentBest))
            {
                if (newScore <= currentBest) return false;
                
                HighScores[modeKey] = newScore;
                return true;
            }
            
            HighScores.Add(modeKey, newScore);
            return true;
        }
    }
}