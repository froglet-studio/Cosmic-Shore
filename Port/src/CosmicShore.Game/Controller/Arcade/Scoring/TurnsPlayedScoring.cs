// Ported verbatim from Assets/_Scripts/Controller/Arcade/Scoring/TurnsPlayedScoring.cs (scoring family 2026-07-10).
// Mechanical substitutions only (README).
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Reminder - player elimination is a bad game mechanic. Redesign the game to not use this.
    /// </summary>
    public class TurnsPlayedScoring : BaseScoring
    {
        public TurnsPlayedScoring(IScoreTracker tracker, GameDataSO data, float scoreMultiplier) : base(tracker, data, scoreMultiplier) { }

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
