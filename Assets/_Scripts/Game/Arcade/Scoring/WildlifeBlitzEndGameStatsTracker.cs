using CosmicShore.Game.Arcade.Scoring;
using CosmicShore.Soap;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class WildlifeBlitzEndGameStatsTracker : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] GameDataSO gameData;
        [SerializeField] WildlifeBlitzController blitzController;
        [SerializeField] TimeBasedTurnMonitor timeMonitor;
        [SerializeField] WildlifeBlitzScoreTracker scoreTracker;

        void OnEnable()
        {
            if (gameData)
                gameData.OnMiniGameEnd += CompileFinalStats;
        }

        void OnDisable()
        {
            if (gameData)
                gameData.OnMiniGameEnd -= CompileFinalStats;
        }

        void CompileFinalStats()
        {
            if (!blitzController || !timeMonitor || !scoreTracker)
            {
                Debug.LogError("[BlitzStats] Missing references!");
                return;
            }

            var stats = gameData.LocalRoundStats;
            bool didWin = blitzController.ResultsReady && blitzController.DidWin;

            float elapsedTime = blitzController.FinishTime;
            float finalScore = didWin ? elapsedTime : 999f;

            stats.Score = finalScore;

            LogFinalStats(didWin, elapsedTime, finalScore);
        }

        void LogFinalStats(bool didWin, float elapsedTime, float finalScore)
        {
            Debug.Log("========================================");
            Debug.Log($"<color=cyan>WILDLIFE BLITZ - FINAL STATS</color>");
            Debug.Log($"<color=yellow>Time Taken:</color> {FormatTime(elapsedTime)}");
            Debug.Log(didWin ? $"<color=green>VICTORY!</color>" : $"<color=red>DEFEAT</color>");
            Debug.Log($"<color=white>Final Ranked Score:</color> {finalScore}");
            Debug.Log("========================================");
        }

        public static string FormatTime(float seconds)
        {
            int minutes = (int)(seconds / 60);
            int secs = (int)(seconds % 60);
            return $"{minutes:00}:{secs:00}";
        }

        public (int kills, float time, bool win) GetDisplayStats()
        {
            var lifeFormScoring = scoreTracker.GetScoring<LifeFormsKilledScoring>();
            return (
                lifeFormScoring?.GetTotalLifeFormsKilled() ?? 0,
                blitzController.FinishTime,
                blitzController.ResultsReady && blitzController.DidWin
            );
        }
    }
}
