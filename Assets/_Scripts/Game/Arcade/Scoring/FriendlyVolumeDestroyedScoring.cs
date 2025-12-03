using CosmicShore.SOAP;

namespace CosmicShore.Game.Arcade.Scoring
{
    public class FriendlyVolumeDestroyedScoring : BaseScoring
    {
        protected IScoreTracker ScoreTracker;

        public FriendlyVolumeDestroyedScoring(
            IScoreTracker tracker,
            GameDataSO data,
            float scoreMultiplier) : base(data, scoreMultiplier)
        {
            ScoreTracker = tracker;
        }

        public override void Subscribe()
        {
            foreach (var playerScore in GameData.RoundStatsList)
            {
                if (!GameData.TryGetRoundStats(playerScore.Name, out var roundStats))
                    return;

                roundStats.OnFriendlyVolumeDestroyedChanged += UpdateScore;
            }
        }

        public override void Unsubscribe()
        {
            foreach (var playerScore in GameData.RoundStatsList)
            {
                if (!GameData.TryGetRoundStats(playerScore.Name, out var roundStats))
                    return;

                roundStats.OnFriendlyVolumeDestroyedChanged -= UpdateScore;
            }
        }

        void UpdateScore(IRoundStats roundStats)
        {
            // Penalty: destroying your own / friendly volume
            Score = -roundStats.FriendlyVolumeDestroyed * scoreMultiplier;
            ScoreTracker.CalculateTotalScore(roundStats.Name);
        }
    }
}