using System.Linq;
using CosmicShore.Game.Analytics;
using CosmicShore.Soap;
using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Game.Arcade
{
    public class DogFightStatsReporter : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameDataSO gameData;
        [SerializeField] private DogFightController dogFightController;

        [Header("Settings")]
        [SerializeField] private GameModes gameMode = GameModes.MultiplayerDogFight;

        void OnEnable()
        {
            if (gameData != null) gameData.OnMiniGameEnd += ReportStats;
        }

        void OnDisable()
        {
            if (gameData != null) gameData.OnMiniGameEnd -= ReportStats;
        }

        void ReportStats()
        {
            if (!UGSStatsManager.Instance) return;
            if (!dogFightController || !dogFightController.dogFightTurnMonitor) return;

            var localName = gameData.LocalPlayer?.Name;
            var localStats = gameData.RoundStatsList.FirstOrDefault(s => s.Name == localName);
            if (localStats == null) return;

            bool isWinner = dogFightController.ResultsReady &&
                            dogFightController.WinnerName == localName;

            if (!isWinner) return;
            float finishTime = localStats.Score;
            UGSStatsManager.Instance.ReportJoustStats(
                gameMode,
                gameData.SelectedIntensity.Value,
                localStats.DogFightHits,
                finishTime
            );

            if (gameData.LocalPlayer?.Vessel is Component vc
                && vc.TryGetComponent<VesselTelemetry>(out var vt))
            {
                UGSStatsManager.Instance.ReportVesselTelemetry(
                    vt, gameData.LocalPlayer.Vessel.VesselStatus.VesselType.ToString());
            }

            CSDebug.Log($"[DogFightStats] Reported Win - Time: {finishTime:F2}s Hits: {localStats.DogFightHits}");
        }
    }
}
