// JoustStatsReporter.cs
using System.Linq;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using Reflex.Attributes;
using UnityEngine;
using CosmicShore.Data;
using CosmicShore.UI;
namespace CosmicShore.Gameplay
{
    public class JoustStatsReporter : MonoBehaviour
    {
        [Header("References")]
        [Inject] private GameDataSO gameData;
        [SerializeField] private MultiplayerJoustController joustController;

        [Header("Settings")]
        [SerializeField] private GameModes gameMode = GameModes.MultiplayerJoust;

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
            if (!joustController || !joustController.joustTurnMonitor) return;

            var localName = gameData.LocalPlayer?.Name;
            var localStats = gameData.RoundStatsList.FirstOrDefault(s => s.Name == localName);
            if (localStats == null) return;

            // Winner = index 0 after ascending sort
            bool isWinner = gameData.RoundStatsList.Count > 0 &&
                            gameData.RoundStatsList[0].Name == localName;

            if (!isWinner) return;
            float raceTime = localStats.Score;
            ugsStatsManager.ReportJoustStats(
                gameMode,
                gameData.SelectedIntensity.Value,
                localStats.JoustCollisions,
                raceTime
            );

            // Report per-vessel telemetry
            if (gameData.LocalPlayer?.Vessel is Component vc
                && vc.TryGetComponent<VesselTelemetry>(out var vt))
            {
                ugsStatsManager.ReportVesselTelemetry(
                    vt, gameData.LocalPlayer.Vessel.VesselStatus.VesselType.ToString());
            }

            CSDebug.Log($"[JoustStats] Reported Win - Time: {raceTime:F2}s Jousts: {localStats.JoustCollisions}");
        }
    }
}