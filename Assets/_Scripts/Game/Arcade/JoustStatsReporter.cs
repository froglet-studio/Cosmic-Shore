using System.Linq;
using CosmicShore.Game.Analytics;
using CosmicShore.Soap;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    /// <summary>
    /// Listens to joust game end and reports stats to UGS
    /// Attach this to the same GameObject as MultiplayerJoustController
    /// </summary>
    public class JoustStatsReporter : MonoBehaviour
    {
        [SerializeField] private GameDataSO gameData;
        [SerializeField] private GameModes gameMode = GameModes.MultiplayerJoust;

        void OnEnable()
        {
            if (gameData != null)
            {
                gameData.OnMiniGameEnd += ReportStats;
            }
        }

        void OnDisable()
        {
            if (gameData != null)
            {
                gameData.OnMiniGameEnd -= ReportStats;
            }
        }

        void ReportStats()
        {
            if (!UGSStatsManager.Instance) return;
            
            var localPlayerName = gameData.LocalPlayer?.Vessel?.VesselStatus?.PlayerName;
            if (string.IsNullOrEmpty(localPlayerName)) return;

            var localStats = gameData.RoundStatsList.FirstOrDefault(s => s.Name == localPlayerName);
            if (localStats == null) return;

            var score = localStats.Score;
            var joustsWon = localStats.JoustCollisions;
            var raceTime = score < 10000f ? score : 0f;
            var intensity =  gameData.SelectedIntensity.Value;

            UGSStatsManager.Instance.ReportJoustStats(
                gameMode,
                intensity,
                joustsWon,
                raceTime
            );

            var didWin = score < 10000f;
            Debug.Log($"[JoustStats] Reported - Win: {didWin}, Jousts: {joustsWon}, Time: {raceTime:F2}s");
        }
    }
}