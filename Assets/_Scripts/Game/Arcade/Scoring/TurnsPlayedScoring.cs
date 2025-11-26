using CosmicShore.SOAP;

namespace CosmicShore.Game.Arcade.Scoring
{
    /// <summary>
    /// Reminder - player elimination is a bad game mechanic. Redesign the game to not use this.
    /// </summary>
    public class TurnsPlayedScoring : BaseScoring
    {
        public TurnsPlayedScoring(MiniGameDataSO data, float scoreMultiplier) : base(data, scoreMultiplier)
        {
        }

        public override void CalculateScore()
        {
        }

        /*public override float CalculateScore(string playerName, float currentScore, float turnStartTime)
        {
            return turnsPlayed;
        }

        public override float EndTurnScore(string playerName, float currentScore, float turnStartTime)
        {
            turnsPlayed++;
            return turnsPlayed;
        }*/
    }
}