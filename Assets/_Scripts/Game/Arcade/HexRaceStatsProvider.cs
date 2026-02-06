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
        [SerializeField] Sprite cleanCrystalIcon;
        [SerializeField] Sprite driftIcon;
        [SerializeField] Sprite boostIcon;

        public override List<StatData> GetStats()
        {
            var list = new List<StatData>();
            if (!scoreTracker) return list;

            list.Add(new StatData
            {
                Label = "Clean Crystals",
                Value = scoreTracker.MaxCleanStreak.ToString(),
                Icon = cleanCrystalIcon
            });

            list.Add(new StatData
            {
                Label = "Longest Drift",
                Value = $"{scoreTracker.MaxDriftTimeRecord:F2}s",
                Icon = driftIcon
            });

            list.Add(new StatData
            {
                Label = "Max Boost",
                Value = $"{scoreTracker.MaxHighBoostTimeRecord:F2}s",
                Icon = boostIcon
            });

            return list;
        }
    }
}