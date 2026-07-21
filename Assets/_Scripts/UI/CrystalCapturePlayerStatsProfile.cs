using System;
using System.Collections.Generic;

namespace CosmicShore.UI
{
    [Serializable]
    public class CrystalCapturePlayerStatsProfile
    {
        // Key = "Mode_Intensity", Value = capture time in whole seconds (LOWER is better
        // since the finish-time scoring change; CrystalCaptureStatsReporter only reports
        // winners, so values are always real times). NOTE: values recorded before the
        // change were crystal counts (~20) and shadow real times until the cloud bucket
        // is cleared server-side.
        public Dictionary<string, int> HighScores = new();

        public bool TryUpdateHighScore(string levelKey, int newScore)
        {
            if (HighScores.TryGetValue(levelKey, out var currentBest))
            {
                if (newScore >= currentBest) return false;

                HighScores[levelKey] = newScore;
                return true;
            }

            HighScores.Add(levelKey, newScore);
            return true;
        }
    }
}
