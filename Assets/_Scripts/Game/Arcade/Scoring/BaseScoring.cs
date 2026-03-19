using System;
using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Soap;
using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Game.Arcade.Scoring
{
    [System.Serializable]
    public abstract class BaseScoring
    {
        /// <summary>
        /// Global score for scoring modes that don't track per-player
        /// (e.g. LifeFormsKilled, ElementalCrystalsCollectedBlitz).
        /// </summary>
        public float Score { get; protected set; }

        readonly Dictionary<string, float> _playerScores = new();

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

        /// <summary>
        /// Get the score contribution for a specific player.
        /// Falls back to the global Score if no per-player value is stored.
        /// </summary>
        public float GetScoreForPlayer(string playerName)
        {
            return _playerScores.TryGetValue(playerName, out var score) ? score : Score;
        }

        /// <summary>
        /// Set the score contribution for a specific player.
        /// </summary>
        protected void SetScoreForPlayer(string playerName, float value)
        {
            _playerScores[playerName] = value;
        }

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