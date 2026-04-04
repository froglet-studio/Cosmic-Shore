using System.Linq;
using CosmicShore.Core;
using CosmicShore.Game.Analytics;
using CosmicShore.Soap;
using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Game.Arcade
{
    public class NeedleThreadStatsReporter : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameDataSO gameData;
        [SerializeField] private NeedleThreadController needleThreadController;

        [Header("Settings")]
        [SerializeField] private GameModes gameMode = GameModes.NeedleThread;

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
            if (!needleThreadController) return;

            var localName = gameData.LocalPlayer?.Name;
            var localStats = gameData.RoundStatsList.FirstOrDefault(s => s.Name == localName);
            if (localStats == null) return;

            bool isWinner = gameData.RoundStatsList.Count > 0 &&
                            gameData.RoundStatsList[0].Name == localName;

            if (!isWinner) return;

            float raceTime = localStats.Score;
            UGSStatsManager.Instance.ReportNeedleThreadStats(
                gameMode,
                gameData.SelectedIntensity.Value,
                localStats.HostileVolumeDestroyed,
                raceTime
            );

            if (gameData.LocalPlayer?.Vessel is Component vc
                && vc.TryGetComponent<VesselTelemetry>(out var vt))
            {
                UGSStatsManager.Instance.ReportVesselTelemetry(
                    vt, gameData.LocalPlayer.Vessel.VesselStatus.VesselType.ToString());
            }

            CSDebug.Log($"[NeedleThreadStats] Reported Win - Time: {raceTime:F2}s Volume: {localStats.HostileVolumeDestroyed:F1}");
        }
    }
}
