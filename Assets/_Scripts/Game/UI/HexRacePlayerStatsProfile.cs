using System;
using System.Collections.Generic;

namespace CosmicShore.Game.Analytics
{
    [Serializable]
    public class HexRacePlayerStatsProfile
    {
        // Key = "Mode_Intensity", Value = RaceTime (Lower is better)
        public Dictionary<string, float> BestMultiplayerRaceTimes = new();

        public bool TryUpdateBestTime(string levelKey, float newTime)
        {
            if (BestMultiplayerRaceTimes.TryGetValue(levelKey, out float currentBest))
            {
                if (newTime >= currentBest) return false;
                BestMultiplayerRaceTimes[levelKey] = newTime;
                return true;
            }
            BestMultiplayerRaceTimes.Add(levelKey, newTime);
            return true;
        }
    }
}
