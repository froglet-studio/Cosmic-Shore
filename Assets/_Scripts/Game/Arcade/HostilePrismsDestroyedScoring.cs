using CosmicShore.Game.Arcade.Scoring;
using CosmicShore.SOAP;

namespace CosmicShore.Game.Arcade
{
    internal class HostilePrismsDestroyedScoring : BaseScoring
    {
        protected IScoreTracker ScoreTracker;

        public HostilePrismsDestroyedScoring(
            IScoreTracker tracker,
            GameDataSO gameData,
            float multiplier)
            : base(gameData, multiplier)
        {
            ScoreTracker = tracker;
        }

        public override void Subscribe()
        {
            foreach (var playerScore in GameData.RoundStatsList)
            {
                if (!GameData.TryGetRoundStats(playerScore.Name, out var roundStats))
                    return;

                roundStats.OnHostilePrismsDestroyedChanged += UpdateScore;
            }
        }

        public override void Unsubscribe()
        {
            foreach (var playerScore in GameData.RoundStatsList)
            {
                if (!GameData.TryGetRoundStats(playerScore.Name, out var roundStats))
                    return;

                roundStats.OnHostilePrismsDestroyedChanged -= UpdateScore;
            }
        }

        void UpdateScore(IRoundStats roundStats)
        {
            // Score for this scoring rule = hostile prisms destroyed * multiplier
            Score = roundStats.HostilePrismsDestroyed * scoreMultiplier;

            // Recompute total across all scoring rules for this player
            ScoreTracker.CalculateTotalScore(roundStats.Name);
        }
    }
}