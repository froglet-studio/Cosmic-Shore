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
            if (scoreTracker == null)
                return new List<StatData>();

            var exposed = scoreTracker.GetExposedStats();
            if (exposed == null || exposed.Count == 0)
                return new List<StatData>();

            var stats = new List<StatData>();

            if (exposed.TryGetValue("Max Clean Streak", out var streak))
            {
                stats.Add(new StatData
                {
                    Label = "Best Streak",
                    Value = FormatInt(streak),
                    Icon = cleanStreakIcon
                });
            }

            if (exposed.TryGetValue("Longest Drift", out var drift))
            {
                stats.Add(new StatData
                {
                    Label = "Longest Drift",
                    Value = FormatFloat(drift),
                    Icon = driftIcon
                });
            }

            if (exposed.TryGetValue("Jousts Won", out var jousts))
            {
                stats.Add(new StatData
                {
                    Label = "Jousts Won",
                    Value = FormatInt(jousts),
                    Icon = joustIcon
                });
            }

            if (exposed.TryGetValue("Max Boost Time", out var boost))
            {
                stats.Add(new StatData
                {
                    Label = "Max Boost",
                    Value = FormatFloat(boost),
                    Icon = null
                });
            }

            return stats;
        }

        static string FormatInt(object val) => val is int i ? i.ToString() : val?.ToString() ?? "0";

        static string FormatFloat(object val)
        {
            if (val is float f) return $"{f:F1}s";
            return val?.ToString() ?? "0";
        }
    }
}
