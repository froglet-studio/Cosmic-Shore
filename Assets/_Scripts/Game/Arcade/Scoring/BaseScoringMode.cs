using System;
using UnityEngine;

namespace CosmicShore.Game.Arcade.Scoring
{
    [System.Serializable]
    public abstract class BaseScoringMode
    {
        [SerializeField]
        protected float ScoreNormalizationQuotient;

        [SerializeField]
        public bool UseGolfRules;
        
        protected BaseScoringMode(float scoreNormalizationQuotient = 145.65f, bool useGolfRules = false)
        {
            ScoreNormalizationQuotient = scoreNormalizationQuotient;
            UseGolfRules = useGolfRules;
        }

        public abstract float CalculateScore(string playerName, float currentScore, float turnStartTime);
        public abstract float EndTurnScore(string playerName, float currentScore, float turnStartTime);

        protected float ApplyGolfRules(float score)
        {
            return UseGolfRules ? -score : score;
        }
    }
}
