using System;
using CosmicShore.Game.Arcade.Scoring;
using CosmicShore.SOAP;
using Obvious.Soap;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;

namespace CosmicShore.Game.Arcade
{
    public abstract class BaseScoreTracker : NetworkBehaviour, IScoreTracker
    {
        [SerializeField] protected ScriptableEventNoParam OnClickToMainMenu; 
        [SerializeField] protected GameDataSO gameData;
        [SerializeField] protected bool golfRules; // For primary scoring mode
        [SerializeField] protected ScoringConfig[] scoringConfigs;

        protected BaseScoring[] scoringArray;

        #region Event Subscriptions
        protected void SubscribeEvents()
        {
            if (gameData == null) return;

            gameData.OnInitializeGame += InitializeScoringMode;
            gameData.OnMiniGameTurnStarted.OnRaised += OnTurnStarted;
            gameData.OnMiniGameTurnEnd.OnRaised += OnTurnEnded;
            gameData.OnMiniGameEnd += CalculateWinnerAndInvokeEvent;
            OnClickToMainMenu.OnRaised += OnTurnEnded;
        }

        protected void UnsubscribeEvents()
        {
            if (gameData == null) return;

            gameData.OnInitializeGame -= InitializeScoringMode;
            gameData.OnMiniGameTurnStarted.OnRaised -= OnTurnStarted;
            gameData.OnMiniGameTurnEnd.OnRaised -= OnTurnEnded;
            gameData.OnMiniGameEnd -= CalculateWinnerAndInvokeEvent;
            OnClickToMainMenu.OnRaised -= OnTurnEnded;
        }
        #endregion

        #region Core Logic
        protected void OnTurnStarted()
        {
            if (scoringArray == null) return;
            foreach (var scoring in scoringArray)
                scoring.Subscribe();
        }

        protected void OnTurnEnded()
        {
            if (scoringArray == null) return;
            foreach (var scoring in scoringArray)
                scoring.Unsubscribe();
        }

        protected virtual void CalculateWinnerAndInvokeEvent()
        {
            SortAndInvokeResults();
        }

        protected void SortAndInvokeResults()
        {
            gameData.SortRoundStats(golfRules);
            gameData.InvokeWinnerCalculated();
        }

        protected void InitializeScoringMode()
        {
            if (scoringConfigs == null || scoringConfigs.Length == 0)
            {
                Debug.LogError("No Scoring Configs were provided.");
                return;
            }

            int arrayLength = scoringConfigs.Length;
            scoringArray = new BaseScoring[arrayLength];

            for (int i = 0; i < arrayLength; i++)
            {
                scoringArray[i] = CreateScoring(scoringConfigs[i].Mode, scoringConfigs[i].Multiplier);
            }
        }

        public void CalculateTotalScore(string playerName)
        {
            if (!gameData.TryGetRoundStats(playerName, out var roundStats))
                return;

            float totalScore = 0;
            foreach (var scoring in scoringArray)
                totalScore += scoring.Score;
            
            roundStats.Score = totalScore;
        }

        BaseScoring CreateScoring(ScoringModes mode, float multiplier)
        {
            return mode switch
            {
                ScoringModes.PrismsCreated => new PrismsCreatedScoring(this, gameData, multiplier),
                ScoringModes.HostilePrismsDestroyed => new HostilePrismsDestroyedScoring(this, gameData, multiplier),
                ScoringModes.FriendlyPrismsDestroyed => new FriendlyPrismsDestroyedScoring(this, gameData, multiplier),
                ScoringModes.HostileVolumeDestroyed => new HostileVolumeDestroyedScoring(this, gameData, multiplier),
                ScoringModes.FriendlyVolumeDestroyed => new FriendlyVolumeDestroyedScoring(this, gameData, multiplier), 
                ScoringModes.VolumeCreated => new VolumeCreatedScoring(this, gameData, multiplier),
                ScoringModes.TimePlayed => new TimePlayedScoring(gameData, multiplier),
                ScoringModes.TurnsPlayed => new TurnsPlayedScoring(gameData, multiplier),
                ScoringModes.VolumeStolen => new VolumeAndBlocksStolenScoring(gameData, multiplier),
                ScoringModes.BlocksStolen => new VolumeAndBlocksStolenScoring(gameData, multiplier, true),
                ScoringModes.TeamVolumeDifference => new TeamVolumeDifferenceScoring(gameData, multiplier),
                ScoringModes.CrystalsCollected => new CrystalsCollectedScoring(gameData, multiplier),
                ScoringModes.OmniCrystalsCollected => new CrystalsCollectedScoring(gameData, multiplier, CrystalsCollectedScoring.CrystalType.Omni),
                ScoringModes.ElementalCrystalsCollected => new CrystalsCollectedScoring(gameData, multiplier, CrystalsCollectedScoring.CrystalType.Elemental),
                ScoringModes.CrystalsCollectedScaleWithSize => new CrystalsCollectedScoring(gameData, multiplier, CrystalsCollectedScoring.CrystalType.Elemental, true),
                _ => throw new ArgumentException($"Unknown scoring mode: {mode}")
            };
        }
        #endregion
    }

    [Serializable]
    public struct ScoringConfig
    {
        public ScoringModes Mode;
        public float Multiplier; //0.00686f for volume
    }
}