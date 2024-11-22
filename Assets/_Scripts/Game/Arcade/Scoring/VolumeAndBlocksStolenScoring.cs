using UnityEngine;

namespace CosmicShore.Game.Arcade.Scoring
{
    public class VolumeAndBlocksStolenScoring : BaseScoringMode
    {
        private readonly bool trackBlocks;

        public VolumeAndBlocksStolenScoring(bool trackBlocks = false, float scoreNormalizationQuotient = 145.65f) 
            : base(scoreNormalizationQuotient)
        {
            this.trackBlocks = trackBlocks;
        }

        public override float CalculateScore(string playerName, float currentScore, float turnStartTime)
        {
            if (StatsManager.Instance.PlayerStats.TryGetValue(playerName, out var roundStats))
                return currentScore + (trackBlocks ? roundStats.BlocksStolen : roundStats.VolumeStolen);
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
