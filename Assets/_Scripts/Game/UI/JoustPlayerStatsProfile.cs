using System;
using System.Collections.Generic;

namespace CosmicShore.Game.Analytics
{
    [Serializable]
    public class JoustPlayerStatsProfile
    {
        public int TotalJoustsWon;
        public int TotalWins;
        
        public Dictionary<string, float> BestRaceTimes = new();

        public bool TryUpdateBestTime(string levelKey, float newTime)
        {
            if (BestRaceTimes.TryGetValue(levelKey, out var currentBest))
            {
                if (newTime >= currentBest) return false;
                
                BestRaceTimes[levelKey] = newTime;
                return true;
            }
            
            BestRaceTimes.Add(levelKey, newTime);
            return true;
        }
    }
}