using System;
using CosmicShore.Gameplay;
using CosmicShore.Data;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    internal class PrismsCreatedScoring : BaseScoring
    {
        public PrismsCreatedScoring(IScoreTracker tracker, GameDataSO gameData, float multiplier) : base(tracker, gameData, multiplier)
        {
        }

        public override void Subscribe()
        {
            foreach (var playerScore in GameData.RoundStatsList)
            {
                if (!GameData.TryGetRoundStats(playerScore.Name, out var roundStats))
                    return;

                roundStats.OnBlocksCreatedChanged += UpdateScore;
            }
        }

        public override void Unsubscribe()
        {
            foreach (var playerScore in GameData.RoundStatsList)
            {
                if (!GameData.TryGetRoundStats(playerScore.Name, out var roundStats))
                    return;

                roundStats.OnBlocksCreatedChanged -= UpdateScore;
            }
        }

        void UpdateScore(IRoundStats roundStats)
        {
            SetScore(roundStats, roundStats.BlocksCreated * scoreMultiplier);
            ScoreTracker.CalculateTotalScore(roundStats.Name);
        }
    }
}