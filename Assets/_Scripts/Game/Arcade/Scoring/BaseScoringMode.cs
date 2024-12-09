using System;
using UnityEngine;

namespace CosmicShore.Game.Arcade.Scoring
{
    [System.Serializable]
    public abstract class BaseScoringMode
    {
        [SerializeField]
        protected float ScoreMultiplier;
        
        protected BaseScoringMode(float scoreNormalizationQuotient = 145.65f)
        {
            ScoreMultiplier = scoreNormalizationQuotient;
        }

        public abstract float CalculateScore(string playerName, float currentScore, float turnStartTime);
        public abstract float EndTurnScore(string playerName, float currentScore, float turnStartTime);
    }
}
