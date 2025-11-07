using System.Collections.Generic;
using CosmicShore.Core;
using UnityEngine;

/*
namespace CosmicShore.Game.Arcade
{
    public class ScoreData
    {
        public List<IRoundStats> RoundStatsList = new ();
        
        public float TurnStartTime;

        public IRoundStats Winner;

        public bool TryGetPlayerScore(string playerName, out IRoundStats roundStats)
        {
            roundStats = null;
            
            foreach (var Score in RoundStatsList)
            {
                if (Score.Name != playerName)
                    continue;
                roundStats = Score;
                return true;
            }

            Debug.LogError("This should never happen! Every Score data need to have a local player!");
            return false;
        }
    }
}*/