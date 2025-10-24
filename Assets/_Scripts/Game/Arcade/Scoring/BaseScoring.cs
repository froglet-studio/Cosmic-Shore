using System;
using CosmicShore.Core;
using CosmicShore.SOAP;
using UnityEngine;

namespace CosmicShore.Game.Arcade.Scoring
{
    [System.Serializable]
    public abstract class BaseScoring
    {
        protected float scoreMultiplier;

        protected GameDataSO GameData;
        
        protected BaseScoring(GameDataSO data, float scoreMultiplier = 145.65f)
        {
            GameData = data;
            this.scoreMultiplier = scoreMultiplier;
        }

        /*// TODO - Remove float turnStartTime as it's not needed
        public abstract float CalculateScore(string playerName, float currentScore, float turnStartTime);
        public abstract float EndTurnScore(string playerName, float currentScore, float turnStartTime);
        */

        
        /// <summary>
        /// Calculate the score based on specific scoring logic, and add it to the previous score.
        /// </summary>
        public abstract void CalculateScore();

        public abstract void Subscribe();
        public abstract void Unsubscribe();
        
        protected bool TryGetRoundStats(string playerName, out IRoundStats roundStats)
        {
            roundStats = null;
            if (GameData.TryGetRoundStats(playerName, out roundStats)) 
                return true;
            
            Debug.LogError($"Didn't find RoundStats for player: {playerName}");
            return false;
        }
    }
}
