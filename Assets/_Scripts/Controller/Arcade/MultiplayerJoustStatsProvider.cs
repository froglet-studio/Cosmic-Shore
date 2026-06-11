using System.Collections.Generic;
using System.Linq;
using CosmicShore.UI;
using CosmicShore.Utility;
using Reflex.Attributes;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// End-game stats provider for the Joust scoreboard.
    /// Pulls jousts won, fastest joust time, and vessel telemetry for the local player.
    /// </summary>
    public class MultiplayerJoustStatsProvider : ScoreboardStatsProvider
    {
        [Header("Dependencies")]
        [SerializeField] private JoustCollisionTurnMonitor joustTurnMonitor;

        [Header("Icons")]
        [SerializeField] private Sprite joustIcon;
        [SerializeField] private Sprite timeIcon;
        [SerializeField] private Sprite driftIcon;
        [SerializeField] private Sprite boostIcon;

        [Inject] private GameDataSO gameData;

        public override List<StatData> GetStats()
        {
            var stats = new List<StatData>();
            if (gameData == null) return stats;

            var localName = gameData.LocalPlayer?.Name;
            var localStats = gameData.RoundStatsList?.FirstOrDefault(s => s.Name == localName);
            if (localStats == null) return stats;

            stats.Add(new StatData
            {
                Label = "Jousts Won",
                Value = localStats.JoustCollisions.ToString(),
                Icon = joustIcon
            });

            // Race time only meaningful if won
            int needed = joustTurnMonitor != null ? joustTurnMonitor.CollisionsNeeded : 0;
            bool didWin = needed > 0 && localStats.JoustCollisions >= needed;

            if (didWin)
            {
                var ts = System.TimeSpan.FromSeconds(localStats.Score);
                stats.Add(new StatData
                {
                    Label = "Race Time",
                    Value = $"{ts.Minutes:D2}:{ts.Seconds:D2}.{ts.Milliseconds / 10:D2}",
                    Icon = timeIcon
                });
            }

            // Vessel telemetry (drift, boost) for the local vessel
            if (gameData.LocalPlayer?.Vessel is Component vc
                && vc.TryGetComponent<VesselTelemetry>(out var telemetry))
            {
                stats.Add(new StatData
                {
                    Label = "Longest Drift",
                    Value = $"{telemetry.MaxDriftTime:F1}s",
                    Icon = driftIcon
                });

                stats.Add(new StatData
                {
                    Label = "Max Boost",
                    Value = $"{telemetry.MaxBoostTime:F1}s",
                    Icon = boostIcon
                });
            }

            return stats;
        }
    }
}
