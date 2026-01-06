using CosmicShore.Soap;

namespace CosmicShore.Game.Arcade.Scoring
{
    /// <summary>
    /// Reminder - player elimination is a bad game mechanic. Redesign the game to not use this.
    /// </summary>
    public class TurnsPlayedScoring : BaseScoring
    {
        public TurnsPlayedScoring(GameDataSO data, float scoreMultiplier) : base(data, scoreMultiplier) { }

        /*public override void CalculateScore()
        {
            return turnsPlayed;
        }*/

        public override void Subscribe()
        {
            throw new System.NotImplementedException();
        }

        public override void Unsubscribe()
        {
            throw new System.NotImplementedException();
        }
    }
}
