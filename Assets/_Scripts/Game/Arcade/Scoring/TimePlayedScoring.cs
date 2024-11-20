using UnityEngine;

namespace CosmicShore.Game.Arcade.Scoring
{
    public class TimePlayedScoring : BaseScoringMode
    {
        private readonly float TimePlayedScoreMultiplier;

        public TimePlayedScoring(float timePlayedScoreMultiplier = 1000f, float scoreNormalizationQuotient = 145.65f) 
            : base(scoreNormalizationQuotient)
        {
            TimePlayedScoreMultiplier = timePlayedScoreMultiplier;
        }

        public override float CalculateScore(string playerName, float currentScore, float turnStartTime)
        {
            return currentScore + (Time.time - turnStartTime) * TimePlayedScoreMultiplier;
        }

        public override float EndTurnScore(string playerName, float currentScore, float turnStartTime)
        {
            return CalculateScore(playerName, currentScore, turnStartTime);
        }
    }
}
