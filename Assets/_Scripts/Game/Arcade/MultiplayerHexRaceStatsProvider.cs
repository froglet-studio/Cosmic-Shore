using System.Collections.Generic;
using CosmicShore.Game.Arcade;
using UnityEngine;

namespace CosmicShore.Game.UI
{
    public class MultiplayerHexRaceStatsProvider : ScoreboardStatsProvider
    {
        [Header("Dependencies")]
        [SerializeField] MultiplayerHexRaceScoreTracker scoreTracker;

        [Header("Icons")]
        [SerializeField] Sprite cleanStreakIcon;
        [SerializeField] Sprite driftIcon;
        [SerializeField] Sprite joustIcon;

        public override List<StatData> GetStats()
        {
            var list = new List<StatData>();

            if (!scoreTracker)
            {
                Debug.LogWarning("[MP Stats Provider] No ScoreTracker assigned!");
                return list;
            }
            
            list.Add(new StatData
            {
                Label = "Best Streak",
                Value = scoreTracker.MaxCleanStreak.ToString(),
                Icon = cleanStreakIcon
            });
            list.Add(new StatData
            {
                Label = "Longest Drift",
                Value = $"{scoreTracker.MaxDriftTimeRecord:F2}s",
                Icon = driftIcon
            });
            
            list.Add(new StatData
            {
                Label = "Jousts Won",
                Value = scoreTracker.JoustsWonSession.ToString(),
                Icon = joustIcon
            });

            return list;
        }
    }
}