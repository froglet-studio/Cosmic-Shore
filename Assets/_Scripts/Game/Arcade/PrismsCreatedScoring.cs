using System;
using CosmicShore.Game.Arcade;
using CosmicShore.Models.Enums;
using CosmicShore.Utility.DataContainers;

namespace CosmicShore.Game.Arcade
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
            Score = roundStats.BlocksCreated * scoreMultiplier;
            ScoreTracker.CalculateTotalScore(roundStats.Name);
        }
    }
}