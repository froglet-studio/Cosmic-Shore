using CosmicShore.Game.Arcade.Scoring;
using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class WildlifeBlitzScoreTracker : BaseScoreTracker
    {
        [Header("Blitz Settings")]
        [SerializeField] SingleplayerWildlifeBlitzTurnMonitor turnMonitor;
        [SerializeField] TimeBasedTurnMonitor timeMonitor;
        
        [Header("Events")]
        [SerializeField] ScriptableEventNoParam eventOnScoreChanged;

        void OnEnable()
        {
            SubscribeEvents();
            SubscribeToScoringEvents();
        }

        void OnDisable()
        {
            UnsubscribeEvents();
            UnsubscribeFromScoringEvents();
        }

        void SubscribeToScoringEvents()
        {
            LifeForm.OnLifeFormDeath -= OnScoringEvent;
            LifeForm.OnLifeFormDeath += OnScoringEvent;
            
            ElementalCrystalImpactor.OnCrystalCollected -= OnCrystalScoringEvent;
            ElementalCrystalImpactor.OnCrystalCollected += OnCrystalScoringEvent;
        }

        void UnsubscribeFromScoringEvents()
        {
            LifeForm.OnLifeFormDeath -= OnScoringEvent;
            ElementalCrystalImpactor.OnCrystalCollected -= OnCrystalScoringEvent;
        }
        
        void OnScoringEvent(string playerName, int cellId)
        {
            var lifeFormScoring = GetScoring<LifeFormsKilledScoring>();
            AddScore(lifeFormScoring.ScorePerKill);
        }

        void OnCrystalScoringEvent(string playerName) 
        {
            var crystalsCollectedScoring = GetScoring<ElementalCrystalsCollectedBlitzScoring>();
             AddScore(crystalsCollectedScoring.Score);
        }

        void AddScore(float amount)
        {
            if (gameData.LocalRoundStats == null) return;
            gameData.LocalRoundStats.Score += amount;
            if (eventOnScoreChanged) eventOnScoreChanged.Raise();
        }
        
        public void ResetScores()
        {
            if (gameData?.LocalRoundStats != null) 
                gameData.LocalRoundStats.Score = 0;
            
            if (eventOnScoreChanged) 
                eventOnScoreChanged.Raise();
            SubscribeToScoringEvents();
        }

        protected override void CalculateWinnerAndInvokeEvent()
        {
             if (!turnMonitor || !timeMonitor || gameData.LocalRoundStats == null) return;
             
             bool didWin = turnMonitor.DidPlayerWin; 
             float winTime = timeMonitor.ElapsedTime;
             gameData.LocalRoundStats.Score = didWin ? winTime : 999f;
             
             SortAndInvokeResults();
        }
    }
}