using System;
using CosmicShore.Game.Arcade.Scoring;
using CosmicShore.Soap;
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
        [SerializeField] protected bool golfRules;
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
            gameData.CalculateDomainStats(golfRules);
            gameData.InvokeWinnerCalculated();
        }

        protected void InitializeScoringMode()
        {
            ForceUnsubscribeAll();

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
                if(gameData.IsTurnRunning) 
                    scoringArray[i].Subscribe();
            }
        }
        
        void ForceUnsubscribeAll()
        {
            if (scoringArray == null) return;
            foreach (var scoring in scoringArray)
            {
                scoring?.Unsubscribe();
            }
        }

        public void CalculateTotalScore(string playerName)
        {
            if (!gameData.TryGetRoundStats(playerName, out var roundStats))
                return;

            float totalScore = scoringArray.Sum(scoring => scoring.Score);

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
                ScoringModes.TimePlayed => new TimePlayedScoring(this, gameData, multiplier),
                ScoringModes.TurnsPlayed => new TurnsPlayedScoring(this, gameData, multiplier),
                ScoringModes.VolumeStolen => new VolumeAndBlocksStolenScoring(this, gameData, multiplier),
                ScoringModes.BlocksStolen => new VolumeAndBlocksStolenScoring(this, gameData, multiplier, true),
                ScoringModes.TeamVolumeDifference => new TeamVolumeDifferenceScoring(this, gameData, multiplier),
                ScoringModes.CrystalsCollected => new CrystalsCollectedScoring(this, gameData, multiplier),
                ScoringModes.OmniCrystalsCollected => new CrystalsCollectedScoring(this, gameData, multiplier, CrystalsCollectedScoring.CrystalType.Omni),
                ScoringModes.ElementalCrystalsCollected => new CrystalsCollectedScoring(this, gameData, multiplier, CrystalsCollectedScoring.CrystalType.Elemental),
                ScoringModes.CrystalsCollectedScaleWithSize => new CrystalsCollectedScoring(this, gameData, multiplier, CrystalsCollectedScoring.CrystalType.Elemental, true),
                _ => throw new ArgumentException($"Unknown scoring mode: {mode}")
            };
        }

        /// <summary>
        /// Get a specific scoring instance by type (useful for getting stats)
        /// </summary>
        public T GetScoring<T>() where T : BaseScoring
        {
            if (scoringArray == null) return null;

            foreach (var scoring in scoringArray)
            {
                if (scoring is T typedScoring)
                    return typedScoring;
            }

            return null;
        }

        #endregion
    }

    [Serializable]
    public struct ScoringConfig
    {
        public ScoringModes Mode;
        public float Multiplier;
    }
}