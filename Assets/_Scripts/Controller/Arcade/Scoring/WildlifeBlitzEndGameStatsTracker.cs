using CosmicShore.Gameplay;
using CosmicShore.Utility;
using Reflex.Attributes;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    public class WildlifeBlitzEndGameStatsTracker : MonoBehaviour
    {
        [Header("References")]
        [Inject] GameDataSO gameData;
        [SerializeField] SingleplayerWildlifeBlitzTurnMonitor blitzMonitor;
        [SerializeField] TimeBasedTurnMonitor timeMonitor;
        [SerializeField] SinglePlayerWildlifeBlitzScoreTracker scoreTracker;
        
        void OnEnable()
        {
            if (gameData)
                gameData.OnMiniGameEnd.OnRaised += CompileFinalStats;
        }

        void OnDisable()
        {
            if (gameData)
                gameData.OnMiniGameEnd.OnRaised -= CompileFinalStats;
        }

        void CompileFinalStats()
        {
            if (!blitzMonitor || !timeMonitor || !scoreTracker)
            {
                CSDebug.LogError("[BlitzStats] Missing references!");
                return;
            }

            var stats = gameData.LocalRoundStats;
            bool didWin = blitzMonitor.DidPlayerWin;
            
            float elapsedTime = timeMonitor.ElapsedTime;
            float finalScore = didWin ? elapsedTime : 999f;

            stats.Score = finalScore;

            LogFinalStats(didWin, elapsedTime, finalScore);
        }

        void LogFinalStats(bool didWin, float elapsedTime, float finalScore)
        {
            if (!CSDebug.IsVerbose(CSLogChannel.ArcadeMatch)) return;
            CSDebug.LogVerbose(CSLogChannel.ArcadeMatch,
                "[WildlifeBlitzEndGameStats] Final stats" +
                $"\n  result={(didWin ? "victory" : "defeat")}" +
                $"\n  time={FormatTime(elapsedTime)}" +
                $"\n  rankedScore={finalScore}");
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
                timeMonitor.ElapsedTime,
                blitzMonitor.DidPlayerWin
            );
        }
    }
}