using System;
using System.Collections.Generic;

namespace CosmicShore.UI
{
    [Serializable]
    public class WildlifeBlitzPlayerStatsProfile
    {
        // Key = "Mode_Intensity", Value = Score (Higher is better)
        public Dictionary<string, int> HighScores = new();

        // Backwards-compat: older cloud save data may contain this field.
        // Kept so deserialization does not fail on existing player profiles.
        public int LifetimeCrystalsCollected;

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
