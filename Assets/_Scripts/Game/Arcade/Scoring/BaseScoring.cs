using System;
using CosmicShore.Core;
using CosmicShore.Soap;
using UnityEngine;

namespace CosmicShore.Game.Arcade.Scoring
{
    [System.Serializable]
    public abstract class BaseScoring
    {
        public float Score { get; protected set; }
        protected float scoreMultiplier;

        protected GameDataSO GameData;
        protected IScoreTracker ScoreTracker;
        
        protected BaseScoring(IScoreTracker tracker, GameDataSO data, float scoreMultiplier = 145.65f)
        {
            ScoreTracker = tracker;
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