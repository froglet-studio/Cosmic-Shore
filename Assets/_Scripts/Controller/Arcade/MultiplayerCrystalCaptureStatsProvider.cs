using System.Collections.Generic;
using System.Linq;
using CosmicShore.UI;
using CosmicShore.Utility;
using Reflex.Attributes;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// End-game stats provider for the Crystal Capture scoreboard.
    /// Pulls crystals collected and vessel telemetry for the local player.
    /// </summary>
    public class MultiplayerCrystalCaptureStatsProvider : ScoreboardStatsProvider
    {
        [Header("Icons")]
        [SerializeField] private Sprite crystalIcon;
        [SerializeField] private Sprite omniCrystalIcon;
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
                Label = "Crystals Collected",
                Value = ((int)localStats.Score).ToString(),
                Icon = crystalIcon
            });

            if (localStats.OmniCrystalsCollected > 0)
            {
                stats.Add(new StatData
                {
                    Label = "Omni Crystals",
                    Value = localStats.OmniCrystalsCollected.ToString(),
                    Icon = omniCrystalIcon
                });
            }

            // Vessel telemetry
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
