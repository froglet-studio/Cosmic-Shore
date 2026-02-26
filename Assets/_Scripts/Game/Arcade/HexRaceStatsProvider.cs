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
            if (!scoreTracker) return new List<StatData>();

            var exposed = scoreTracker.GetExposedStats();
            if (exposed.Count == 0) return new List<StatData>();

            var stats = new List<StatData>();

            if (exposed.TryGetValue("Longest Drift", out var drift))
            {
                float driftVal = drift is float f ? f : 0f;
                stats.Add(new StatData
                {
                    Label = "Longest Drift",
                    Value = $"{driftVal:F1}s",
                    Icon = driftIcon
                });
            }

            if (exposed.TryGetValue("Max Clean Streak", out var streak))
            {
                stats.Add(new StatData
                {
                    Label = "Clean Streak",
                    Value = $"{streak}",
                    Icon = cleanStreakIcon
                });
            }

            if (exposed.TryGetValue("Jousts Won", out var jousts))
            {
                stats.Add(new StatData
                {
                    Label = "Jousts Won",
                    Value = $"{jousts}",
                    Icon = joustIcon
                });
            }

            return stats;
        }
    }
}
