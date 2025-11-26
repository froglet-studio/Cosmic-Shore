using CosmicShore.Core;
using CosmicShore.SOAP;
using UnityEngine;

namespace CosmicShore.Game.Arcade.Scoring
{
    public class VolumeAndBlocksStolenScoring : BaseScoring
    {
        private readonly bool trackBlocks;

        public VolumeAndBlocksStolenScoring(MiniGameDataSO data, float scoreNormalizationQuotient,
            bool trackBlocks = false)
            : base(data, scoreNormalizationQuotient)
        {
            this.trackBlocks = trackBlocks;
        }

        public override void CalculateScore()
        {
            foreach (var playerScore in miniGameData.RoundStatsList)
            {
                if (!TryGetRoundStats(playerScore.Name, out IRoundStats roundStats))
                    return;

                playerScore.Score +=
                    (trackBlocks ? roundStats.BlocksStolen : roundStats.VolumeStolen) * scoreMultiplier;
            }
        }

        /*public override float CalculateScore(string playerName, float currentScore, float turnStartTime)
        {
            if (StatsManager.Instance.PlayerStats.TryGetValue(playerName, out var roundStats))
                return currentScore + (trackBlocks ? roundStats.BlocksStolen : roundStats.VolumeStolen) * ScoreMultiplier;
            return currentScore;
        }

        public override float EndTurnScore(string playerName, float currentScore, float turnStartTime)
        {
            var score = CalculateScore(playerName, currentScore, turnStartTime);
            StatsManager.Instance.ResetStats();
            return score;
        }*/
    }
}