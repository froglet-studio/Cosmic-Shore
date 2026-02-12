using System;
using System.Collections.Generic;

namespace CosmicShore.Game.Analytics
{
    [Serializable]
    public class CrystalCapturePlayerStatsProfile
    {
        public int LifetimeCrystalsCollected;
        public int TotalWins;
        
        public Dictionary<string, int> HighScores = new();

        public bool TryUpdateHighScore(string levelKey, int newScore)
        {
            if (HighScores.TryGetValue(levelKey, out var currentBest))
            {
                if (newScore <= currentBest) return false;
                
                HighScores[levelKey] = newScore;
                return true;
            }
            
            HighScores.Add(levelKey, newScore);
            return true;
        }
    }
}