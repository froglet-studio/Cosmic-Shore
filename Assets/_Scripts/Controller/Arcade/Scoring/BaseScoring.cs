using System;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    [System.Serializable]
    public abstract class BaseScoring
    {
        /// <summary>
        /// The scorer-wide value. Only the deliberately SHARED accumulators use it (co-op totals:
        /// LifeFormsKilledScoring, ElementalCrystalsCollectedBlitzScoring); a per-player metric
        /// writes through <see cref="SetScore"/> instead.
        /// </summary>
        public float Score { get; protected set; }
        protected float scoreMultiplier;

        // Per-player values. Every metric scorer used to write ONE Score for whichever player's
        // stat changed last, and BaseScoreTracker.CalculateTotalScore summed those into the player
        // it was recomputing - so in Cellular Duel a player's total carried the OTHER player's
        // destruction numbers, and the winner was decided partly on the opponent's play.
        readonly System.Collections.Generic.Dictionary<string, float> _playerScores = new();

        protected void SetScore(IRoundStats roundStats, float value) => _playerScores[roundStats.Name] = value;

        /// <summary>This scorer's contribution to <paramref name="playerName"/>'s total: their own
        /// value when the scorer is per-player, otherwise the shared <see cref="Score"/>.</summary>
        public float ScoreFor(string playerName) =>
            _playerScores.TryGetValue(playerName, out var value) ? value : Score;

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
            
            CSDebug.LogError($"Didn't find RoundStats for player: {playerName}");
            return false;
        }
    }
}