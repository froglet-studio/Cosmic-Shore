using UnityEngine;

namespace CosmicShore.Game.Arcade.Scoring
{
    public class VolumeCreatedScoring : BaseScoringMode
    {
        public VolumeCreatedScoring(float scoreNormalizationQuotient = 145.65f) : base(scoreNormalizationQuotient) { }

        public override float CalculateScore(string playerName, float currentScore, float turnStartTime)
        {
            if (StatsManager.Instance.PlayerStats.TryGetValue(playerName, out var roundStats))
                return currentScore + roundStats.VolumeCreated;
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
