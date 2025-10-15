using CosmicShore.SOAP;

namespace CosmicShore.Game.Arcade.Scoring
{
    public class VolumeCreatedScoring : BaseScoring
    {
        public VolumeCreatedScoring(GameDataSO data, float scoreMultiplier) : base(data, scoreMultiplier) { }

        public override void CalculateScore()
        {
            foreach (var playerScore in GameData.RoundStatsList)
            {
                if (!GameData.TryGetRoundStats(playerScore.Name, out var roundStats))
                    return;

                playerScore.Score += roundStats.VolumeCreated * scoreMultiplier;
            }
        }

        /*public override float CalculateScore(string playerName, float currentScore, float turnStartTime)
        {
            if (StatsManager.Instance.PlayerStats.TryGetValue(playerName, out var roundStats))
                return currentScore + roundStats.VolumeCreated * ScoreMultiplier;
            return currentScore;
        }

        public override float EndTurnScore(string playerName, float currentScore, float turnStartTime)
        {
            // var score = CalculateScore(playerName, currentScore, turnStartTime);
            var score = CalculateScore(playerName, currentScore);
            StatsManager.Instance.ResetStats();
            return score;
        }*/
    }
}
