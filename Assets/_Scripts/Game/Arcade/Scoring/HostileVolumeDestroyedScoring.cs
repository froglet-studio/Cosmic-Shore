using UnityEngine;

namespace CosmicShore.Game.Arcade.Scoring
{
    public class HostileVolumeDestroyedScoring : BaseScoringMode
    {
        public HostileVolumeDestroyedScoring(float scoreNormalizationQuotient) 
            : base(scoreNormalizationQuotient) { }

        public override float CalculateScore(string playerName, float currentScore, float turnStartTime)
        {
            if (StatsManager.Instance.PlayerStats.TryGetValue(playerName, out var roundStats))
            {
                float score = roundStats.HostileVolumeDestroyed * ScoreMultiplier;
                return currentScore + score;
            }
            return currentScore;
        }

        public override float EndTurnScore(string playerName, float currentScore, float turnStartTime)
        {
            var score = CalculateScore(playerName, currentScore, turnStartTime);
            StatsManager.Instance.ResetStats();
            return score;
        }
    }
}
