using System;
using System.Collections.Generic;

[Serializable]
public class HexRacePlayerStatsProfile
{
    public int TotalCleanCrystalsCollected;
    public float TotalDriftTime; 
    public int TotalJoustsWon;
    public int TotalWins;
    
    public float LongestSingleDrift;

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