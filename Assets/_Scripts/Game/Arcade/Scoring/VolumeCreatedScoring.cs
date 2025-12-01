using CosmicShore.SOAP;

namespace CosmicShore.Game.Arcade.Scoring
{
    public class VolumeCreatedScoring : BaseScoring
    {
        float lastVolumeCreated;
        
        public VolumeCreatedScoring(GameDataSO data, float scoreMultiplier) : base(data, scoreMultiplier) { }

        public override void Subscribe()
        {
            foreach (var playerScore in GameData.RoundStatsList)
            {
                if (!GameData.TryGetRoundStats(playerScore.Name, out var roundStats))
                    return;

                roundStats.OnVolumeCreatedChanged += UpdateScore;
            }
        }

        public override void Unsubscribe()
        {
            foreach (var playerScore in GameData.RoundStatsList)
            {
                if (!GameData.TryGetRoundStats(playerScore.Name, out var roundStats))
                    return;

                roundStats.OnVolumeCreatedChanged -= UpdateScore;
                lastVolumeCreated = 0;
            }
        }

        void UpdateScore(IRoundStats roundStats)
        {
            var volumeDelta = roundStats.VolumeCreated - lastVolumeCreated;
            roundStats.Score += volumeDelta * scoreMultiplier;
            lastVolumeCreated = roundStats.VolumeCreated;
        }
    }
}
