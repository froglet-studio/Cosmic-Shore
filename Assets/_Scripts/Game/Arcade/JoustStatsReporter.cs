using System.Linq;
using CosmicShore.Game.Analytics;
using CosmicShore.Soap;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    /// <summary>
    /// Listens to joust game end and reports stats to UGS.
    /// Only reports if the local player actually won (reached collision goal).
    /// </summary>
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

            var localPlayerName = gameData.LocalPlayer?.Vessel?.VesselStatus?.PlayerName;
            var localStats = gameData.RoundStatsList.FirstOrDefault(s => s.Name == localPlayerName);

            if (localStats == null) return;

            // --- LOGIC ---
            // 1. Determine Win based on Joust Count vs Needed
            int needed = joustController.joustTurnMonitor.CollisionsNeeded;
            bool isWinner = localStats.JoustCollisions >= needed;

            // 2. Only report to Leaderboard if we WON
            if (isWinner)
            {
                // Score corresponds to Race Time in seconds
                float raceTime = localStats.Score; 

                UGSStatsManager.Instance.ReportJoustStats(
                    gameMode,
                    gameData.SelectedIntensity.Value,
                    localStats.JoustCollisions,
                    raceTime
                );
                
                Debug.Log($"[JoustStats] Reported Win - Time: {raceTime:F2}s");
            }
        }
    }
}