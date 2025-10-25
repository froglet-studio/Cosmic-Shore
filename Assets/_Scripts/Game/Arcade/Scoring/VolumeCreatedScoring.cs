using CosmicShore.SOAP;

namespace CosmicShore.Game.Arcade.Scoring
{
    public class VolumeCreatedScoring : BaseScoring
    {
        public VolumeCreatedScoring(GameDataSO data, float scoreMultiplier) : base(data, scoreMultiplier) { }

        float lastVolumeCreated;
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
            var newVolumeCreated = roundStats.VolumeCreated - lastVolumeCreated;
            roundStats.Score += newVolumeCreated * scoreMultiplier;
        }
        
        public override void CalculateScore()
        {
            /*foreach (var playerScore in GameData.RoundStatsList)
            {
                if (!GameData.TryGetRoundStats(playerScore.Name, out var roundStats))
                    return;

                playerScore.Score += roundStats.VolumeCreated * scoreMultiplier;
            }*/
        }
    }
}
