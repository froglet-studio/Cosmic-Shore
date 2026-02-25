using System.Collections.Generic;
using CosmicShore.Game.UI;
using UnityEngine;

namespace CosmicShore.Game.Arcade
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