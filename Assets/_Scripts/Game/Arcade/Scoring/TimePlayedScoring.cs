using UnityEngine;

namespace CosmicShore.Game.Arcade.Scoring
{
    public class TimePlayedScoring : BaseScoringMode
    {

        public TimePlayedScoring(float scoreNormalizationQuotient)
            : base(scoreNormalizationQuotient) { }


        public override float CalculateScore(string playerName, float currentScore, float turnStartTime)
        {
            return currentScore + (Time.time - turnStartTime) * ScoreMultiplier;
        }

        public override float EndTurnScore(string playerName, float currentScore, float turnStartTime)
        {
            return CalculateScore(playerName, currentScore, turnStartTime);
        }
    }
}
