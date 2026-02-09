using CosmicShore.Game.Arcade.Scoring;
using CosmicShore.Soap;

namespace CosmicShore.Game.Arcade
{
    internal class HostilePrismsDestroyedScoring : BaseScoring
    {
        public HostilePrismsDestroyedScoring(
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