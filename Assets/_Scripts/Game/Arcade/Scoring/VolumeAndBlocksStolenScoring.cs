using CosmicShore.Core;
using CosmicShore.Soap;

namespace CosmicShore.Game.Arcade.Scoring
{
    public class VolumeAndBlocksStolenScoring : BaseScoring
    {
        private readonly bool trackBlocks;

        public VolumeAndBlocksStolenScoring(IScoreTracker tracker, GameDataSO data, float scoreNormalizationQuotient, bool trackBlocks = false)
            : base(tracker, data, scoreNormalizationQuotient)
        {
            this.trackBlocks = trackBlocks;
        }

        public override void Subscribe()
        {
            foreach (var playerScore in GameData.RoundStatsList)
            {
                if (!GameData.TryGetRoundStats(playerScore.Name, out var roundStats))
                    return;

                if (trackBlocks)
                    roundStats.OnPrismsStolenChanged += UpdateScore;
                else
                    roundStats.OnVolumeStolenChanged += UpdateScore;
            }
        }

        public override void Unsubscribe()
        {
            foreach (var playerScore in GameData.RoundStatsList)
            {
                if (!GameData.TryGetRoundStats(playerScore.Name, out var roundStats))
                    return;

                if (trackBlocks)
                    roundStats.OnPrismsStolenChanged -= UpdateScore;
                else
                    roundStats.OnVolumeStolenChanged -= UpdateScore;
            }
        }

        void UpdateScore(IRoundStats roundStats)
        {
            Score = (trackBlocks ? roundStats.PrismStolen : roundStats.VolumeStolen) * scoreMultiplier;
            ScoreTracker.CalculateTotalScore(roundStats.Name);
        }
    }
}
