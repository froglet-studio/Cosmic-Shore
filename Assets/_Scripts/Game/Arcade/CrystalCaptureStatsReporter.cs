using System.Linq;
using CosmicShore.Game.Analytics;
using CosmicShore.Soap;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class CrystalCaptureStatsReporter : MonoBehaviour
    {
        [SerializeField] private GameDataSO gameData;
        [SerializeField] private GameModes gameMode = GameModes.MultiplayerCrystalCapture;

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

            var localPlayerName = gameData.LocalPlayer?.Vessel?.VesselStatus?.PlayerName;
            var localStats = gameData.RoundStatsList.FirstOrDefault(s => s.Name == localPlayerName);

            if (localStats == null) return;

            bool isWinner = gameData.IsLocalDomainWinner(out _);

            if (isWinner)
            {
                // [Visual Note] Background payload transmission to UGS. No UI blocking. 
                UGSStatsManager.Instance.ReportCrystalCaptureStats(
                    gameMode,
                    gameData.SelectedIntensity.Value,
                    (int)localStats.Score
                );
            }
        }
    }
}