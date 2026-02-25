// CrystalCaptureStatsReporter.cs
using System.Linq;
using CosmicShore.Game.Analytics;
using CosmicShore.Soap;
using Reflex.Attributes;
using UnityEngine;
using CosmicShore.Models.Enums;

namespace CosmicShore.Game.Arcade
{
    public class CrystalCaptureStatsReporter : MonoBehaviour
    {
        [SerializeField] private GameDataSO gameData;
        [SerializeField] private GameModes gameMode = GameModes.MultiplayerCrystalCapture;

        [Inject] UGSStatsManager ugsStatsManager;

        void OnEnable()
        {
            if (gameData != null) gameData.OnMiniGameEnd.OnRaised += ReportStats;
        }

        void OnDisable()
        {
            if (gameData != null) gameData.OnMiniGameEnd.OnRaised -= ReportStats;
        }

        void ReportStats()
        {
            if (!ugsStatsManager) return;

            var localName = gameData.LocalPlayer?.Name;
            var localStats = gameData.RoundStatsList.FirstOrDefault(s => s.Name == localName);
            if (localStats == null) return;

            // Winner = index 0 after descending sort (highest crystals first)
            bool isWinner = gameData.RoundStatsList.Count > 0 &&
                            gameData.RoundStatsList[0].Name == localName;

            if (isWinner)
            {
                ugsStatsManager.ReportCrystalCaptureStats(
                    gameMode,
                    gameData.SelectedIntensity.Value,
                    (int)localStats.Score
                );

                // Report per-vessel telemetry
                if (gameData.LocalPlayer?.Vessel is Component vc
                    && vc.TryGetComponent<VesselTelemetry>(out var vt))
                {
                    ugsStatsManager.ReportVesselTelemetry(
                        vt, gameData.LocalPlayer.Vessel.VesselStatus.VesselType.ToString());
                }
            }
        }
    }
}