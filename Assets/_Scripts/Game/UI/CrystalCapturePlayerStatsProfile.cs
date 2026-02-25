using System;
using System.Collections.Generic;

namespace CosmicShore.Game.UI
{
    [Serializable]
    public class CrystalCapturePlayerStatsProfile
    {
        // Key = "Mode_Intensity", Value = Score (Higher is better)
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
