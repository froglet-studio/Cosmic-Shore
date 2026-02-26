using CosmicShore.Utility.DataContainers;

namespace CosmicShore.Game.Arcade
{
    public class FriendlyVolumeDestroyedScoring : BaseScoring
    {
        public FriendlyVolumeDestroyedScoring(
            IScoreTracker tracker,
            GameDataSO data,
            float scoreMultiplier) : base(tracker, data, scoreMultiplier) { }

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