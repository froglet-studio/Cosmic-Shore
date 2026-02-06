using System;
using System.Collections.Generic;

namespace CosmicShore.Game.Analytics
{
    [Serializable]
    public class HexRacePlayerStatsProfile
    {
        // [Visual Note] Global Accumulators
        public int TotalCleanCrystalsCollected;
        public float TotalDriftTime;
        
        // [Visual Note] Records (Higher is Better)
        public float LongestSingleDrift;
        public float MaxTimeAtHighBoost;

        // [Visual Note] Best Times per Level (Key = "LevelName_Intensity")
        // NOTE: For Race times, LOWER is better.
        public Dictionary<string, float> BestRaceTimes = new();

        public bool TryUpdateBestTime(string levelKey, float newTime)
        {
            if (BestRaceTimes.TryGetValue(levelKey, out float currentBest))
            {
                // [Visual Note] Lower time is better for racing
                if (newTime >= currentBest) return false;
                
                BestRaceTimes[levelKey] = newTime;
                return true;
            }
            
            BestRaceTimes.Add(levelKey, newTime);
            return true;
        }

        public void UpdateSkillStats(float driftTime, float boostTime)
        {
            if (driftTime > LongestSingleDrift) LongestSingleDrift = driftTime;
            if (boostTime > MaxTimeAtHighBoost) MaxTimeAtHighBoost = boostTime;
            
            TotalDriftTime += driftTime;
        }
    }
}