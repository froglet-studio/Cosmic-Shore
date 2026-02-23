// JoustStatsReporter.cs
using System.Linq;
using CosmicShore.Game.Analytics;
using CosmicShore.Soap;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class JoustStatsReporter : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameDataSO gameData;
        [SerializeField] private MultiplayerJoustController joustController;

        [Header("Settings")]
        [SerializeField] private GameModes gameMode = GameModes.MultiplayerJoust;

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
            if (!joustController || !joustController.joustTurnMonitor) return;

            var localName = gameData.LocalPlayer?.Name;
            var localStats = gameData.RoundStatsList.FirstOrDefault(s => s.Name == localName);
            if (localStats == null) return;

            // Winner = index 0 after ascending sort
            bool isWinner = gameData.RoundStatsList.Count > 0 &&
                            gameData.RoundStatsList[0].Name == localName;

            if (!isWinner) return;
            float raceTime = localStats.Score;
            UGSStatsManager.Instance.ReportJoustStats(
                gameMode,
                gameData.SelectedIntensity.Value,
                localStats.JoustCollisions,
                raceTime
            );
            Debug.Log($"[JoustStats] Reported Win - Time: {raceTime:F2}s Jousts: {localStats.JoustCollisions}");
        }
    }
}