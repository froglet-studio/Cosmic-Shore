using System.Collections.Generic;
using CosmicShore.UI;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    public class HexRaceStatsProvider : ScoreboardStatsProvider
    {
        [Header("Dependencies")]
        [SerializeField] HexRaceScoreTracker scoreTracker;

        [Header("Icons")]
        [SerializeField] Sprite cleanStreakIcon;
        [SerializeField] Sprite driftIcon;
        [SerializeField] Sprite joustIcon;
        
        public override List<StatData> GetStats()
        {
    
            return null;
        }
    }
}