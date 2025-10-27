using CosmicShore.SOAP;
using UnityEngine;

namespace CosmicShore.Game.Arcade.Scoring
{
    public class TimePlayedScoring : BaseScoring
    {
        public TimePlayedScoring(GameDataSO data, float scoreMultiplier) : base(data, scoreMultiplier) { }

        /*public override void CalculateScore()
        {
            foreach (var playerScore in GameData.RoundStatsList)
            {
                playerScore.Score += (Time.time - GameData.TurnStartTime) * scoreMultiplier;
            }
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
