using CosmicShore.SOAP;

namespace CosmicShore.Game.Arcade.Scoring
{
    public class VolumeCreatedScoring : BaseScoring
    {
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

                roundStats.OnVolumeCreatedChanged += UpdateScore;
            }
        }

        void UpdateScore(IRoundStats roundStats) =>
            roundStats.Score = roundStats.VolumeCreated * scoreMultiplier;
        
        public override void CalculateScore()
        {
            foreach (var playerScore in GameData.RoundStatsList)
            {
                if (!GameData.TryGetRoundStats(playerScore.Name, out var roundStats))
                    return;

                playerScore.Score += roundStats.VolumeCreated * scoreMultiplier;
            }
        }
    }
}
