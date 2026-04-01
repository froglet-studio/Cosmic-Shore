using CosmicShore.Soap;

namespace CosmicShore.Game.Arcade.Scoring
{
    public class HostileVolumeDestroyedScoring : BaseScoring
    {
        public HostileVolumeDestroyedScoring(
            IScoreTracker tracker,
            GameDataSO data,
            float scoreMultiplier) : base(tracker, data, scoreMultiplier) { }

        public override void Subscribe()
        {
            foreach (var playerScore in GameData.RoundStatsList)
            {
                if (!GameData.TryGetRoundStats(playerScore.Name, out var roundStats))
                    return;

                roundStats.OnHostileVolumeDestroyedChanged += UpdateScore;
            }
        }

        public override void Unsubscribe()
        {
            foreach (var playerScore in GameData.RoundStatsList)
            {
                if (!GameData.TryGetRoundStats(playerScore.Name, out var roundStats))
                    return;

                roundStats.OnHostileVolumeDestroyedChanged -= UpdateScore;
            }
        }

        void UpdateScore(IRoundStats roundStats)
        {
            // Reward destroying enemy volume
            Score = roundStats.HostileVolumeDestroyed * scoreMultiplier;
            ScoreTracker.CalculateTotalScore(roundStats.Name);
        }
    }
}