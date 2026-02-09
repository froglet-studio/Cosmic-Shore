using CosmicShore.Game.Arcade.Scoring;
using CosmicShore.Soap;

namespace CosmicShore.Game.Arcade
{
    internal class FriendlyPrismsDestroyedScoring : BaseScoring
    {
        public FriendlyPrismsDestroyedScoring(
            IScoreTracker tracker,
            GameDataSO gameData,
            float multiplier)
            : base(tracker, gameData, multiplier) { }

        public override void Subscribe()
        {
            foreach (var playerScore in GameData.RoundStatsList)
            {
                if (!GameData.TryGetRoundStats(playerScore.Name, out var roundStats))
                    return;

                roundStats.OnFriendlyPrismsDestroyedChanged += UpdateScore;
            }
        }

        public override void Unsubscribe()
        {
            foreach (var playerScore in GameData.RoundStatsList)
            {
                if (!GameData.TryGetRoundStats(playerScore.Name, out var roundStats))
                    return;

                roundStats.OnFriendlyPrismsDestroyedChanged -= UpdateScore;
            }
        }

        void UpdateScore(IRoundStats roundStats)
        {
            // Score for this scoring rule = friendly prisms destroyed * multiplier
            Score = roundStats.FriendlyPrismsDestroyed * scoreMultiplier;

            // Recompute total across all scoring rules for this player
            ScoreTracker.CalculateTotalScore(roundStats.Name);
        }
    }
}