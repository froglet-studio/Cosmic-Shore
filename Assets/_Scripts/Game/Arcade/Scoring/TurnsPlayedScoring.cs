using CosmicShore.Soap;

namespace CosmicShore.Game.Arcade.Scoring
{
    /// <summary>
    /// Reminder - player elimination is a bad game mechanic. Redesign the game to not use this.
    /// </summary>
    public class TurnsPlayedScoring : BaseScoring
    {
        public TurnsPlayedScoring(IScoreTracker tracker, GameDataSO data, float scoreMultiplier) : base(tracker, data, scoreMultiplier) { }

        public override void Subscribe() { }

        public override void Unsubscribe() { }
    }
}
