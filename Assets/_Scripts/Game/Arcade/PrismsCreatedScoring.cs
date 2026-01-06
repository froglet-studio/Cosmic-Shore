using System;
using CosmicShore.Game.Arcade.Scoring;
using CosmicShore.Soap;

namespace CosmicShore.Game.Arcade
{
    internal class PrismsCreatedScoring : BaseScoring
    {
        protected IScoreTracker ScoreTracker;

        public PrismsCreatedScoring(IScoreTracker tracker, GameDataSO gameData, float multiplier) : base(gameData,
            multiplier)
        {
            ScoreTracker = tracker;
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
            Score = roundStats.BlocksCreated * scoreMultiplier;
            ScoreTracker.CalculateTotalScore(roundStats.Name);
        }
    }
}