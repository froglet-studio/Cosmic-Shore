// ScurryStatsReporter.cs
using System.Linq;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using Reflex.Attributes;
using UnityEngine;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.UI;
namespace CosmicShore.Gameplay
{
    public class ScurryStatsReporter : MonoBehaviour
    {
        [Inject] private GameDataSO gameData;
        [SerializeField] private GameModes gameMode = GameModes.Scurry;

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

            // Winner = my Score is a real finish time (winning-domain players all carry the
            // match time; losers carry the DnfThreshold+remaining sentinel). Every winning
            // teammate reports - per-player best-time tracking, same shape as SkimRace.
            bool isWinner = GolfScoreSentinels.IsFinishTime(localStats.Score);

            if (isWinner)
            {
                ugsStatsManager.ReportScurryStats(
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