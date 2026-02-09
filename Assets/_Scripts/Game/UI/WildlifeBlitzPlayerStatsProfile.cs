using System;
using System.Collections.Generic;

namespace CosmicShore.Game.Analytics
{
    [Serializable]
    public class WildlifeBlitzPlayerStatsProfile
    {
        public int LifetimeCrystalsCollected;
        public int LifetimeLifeFormsKilled;
        
        // [Visual Note] Key = "Mode_Intensity", Value = Score (Higher is better)
        public Dictionary<string, int> HighScores = new();

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