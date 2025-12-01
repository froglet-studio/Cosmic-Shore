using CosmicShore.SOAP;

namespace CosmicShore.Game.Arcade.Scoring
{
    public class FriendlyVolumeDestroyedScoring : BaseScoring
    {
        float lastVolumeDestroyed;
        
        public FriendlyVolumeDestroyedScoring(GameDataSO data, float scoreMultiplier) : base(data, scoreMultiplier) { }

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
                lastVolumeDestroyed = 0;
            }
        }

        void UpdateScore(IRoundStats roundStats)
        {
            var volumeDelta = roundStats.FriendlyVolumeDestroyed - lastVolumeDestroyed;
            roundStats.Score -= volumeDelta * scoreMultiplier;
            lastVolumeDestroyed = roundStats.FriendlyVolumeDestroyed;
        }
    }
}