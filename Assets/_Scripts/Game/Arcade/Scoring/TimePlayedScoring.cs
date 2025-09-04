using CosmicShore.SOAP;
using UnityEngine;

namespace CosmicShore.Game.Arcade.Scoring
{
    public class TimePlayedScoring : BaseScoring
    {
        public TimePlayedScoring(MiniGameDataSO data, float scoreMultiplier) : base(data, scoreMultiplier) { }

        public override void CalculateScore()
        {
            foreach (var playerScore in miniGameData.RoundStatsList)
            {
                playerScore.Score += (Time.time - miniGameData.TurnStartTime) * scoreMultiplier;
            }
        }
        

        /*public override float CalculateScore(string playerName, float currentScore, float turnStartTime)
        {
            return currentScore + (Time.time - turnStartTime) * scoreMultiplier;
        }

        public override float EndTurnScore(string playerName, float currentScore, float turnStartTime)
        {
            return CalculateScore(playerName, currentScore, turnStartTime);
        }*/
    }
}
