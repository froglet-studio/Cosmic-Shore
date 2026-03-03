using System.Collections.Generic;
using CosmicShore.Game.Arcade;
using UnityEngine;

namespace CosmicShore.Game.UI
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