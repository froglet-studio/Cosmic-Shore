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
