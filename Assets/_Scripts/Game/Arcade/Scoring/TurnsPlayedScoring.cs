namespace CosmicShore.Game.Arcade.Scoring
{
    public class TurnsPlayedScoring : BaseScoringMode
    {
        private int turnsPlayed;

        public TurnsPlayedScoring(float scoreNormalizationQuotient) : base(scoreNormalizationQuotient) { }

        public override float CalculateScore(string playerName, float currentScore, float turnStartTime)
        {
            return turnsPlayed;
        }

        public override float EndTurnScore(string playerName, float currentScore, float turnStartTime)
        {
            turnsPlayed++;
            return turnsPlayed;
        }
    }
}
