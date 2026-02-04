using CosmicShore.Game.Arcade.Scoring;
using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class SinglePlayerWildlifeBlitzScoreTracker : BaseScoreTracker
    {
        [Header("Blitz Settings")]
        [SerializeField] SingleplayerWildlifeBlitzTurnMonitor turnMonitor;
        [SerializeField] TimeBasedTurnMonitor timeMonitor;
        
        [Header("Events")]
        [SerializeField] ScriptableEventNoParam eventOnScoreChanged;

        private bool isTracking = false;

        void OnEnable()
        {
            // Only subscribe to generic Game Events (like Pause), NOT scoring yet
            SubscribeEvents();
        }

        void OnDisable()
        {
            UnsubscribeEvents();
            StopTracking(); // Safety catch
        }

        public void StartTracking()
        {
            if (isTracking) return; // Prevent double subscription
            
            LifeForm.OnLifeFormDeath += OnScoringEvent;
            ElementalCrystalImpactor.OnCrystalCollected += OnCrystalScoringEvent;
            isTracking = true;
            Debug.Log("[ScoreTracker] Started Tracking");
        }

        public void StopTracking()
        {
            if (!isTracking) return;

            LifeForm.OnLifeFormDeath -= OnScoringEvent;
            ElementalCrystalImpactor.OnCrystalCollected -= OnCrystalScoringEvent;
            isTracking = false;
            Debug.Log("[ScoreTracker] Stopped Tracking");
        }
        
        // Event Handlers
        void OnScoringEvent(string playerName, int cellId)
        {
            var lifeFormScoring = GetScoring<LifeFormsKilledScoring>();
            if (lifeFormScoring != null) AddScore(lifeFormScoring.ScorePerKill);
        }

        void OnCrystalScoringEvent(string playerName) 
        {
            var crystalsCollectedScoring = GetScoring<ElementalCrystalsCollectedBlitzScoring>();
            if (crystalsCollectedScoring != null) AddScore(crystalsCollectedScoring.GetScoreMultiplier());
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
        }
        protected override void CalculateWinnerAndInvokeEvent()
        {
             if (!turnMonitor || gameData.LocalRoundStats == null) return;
             
             bool didWin = turnMonitor.DidPlayerWin; 
             float winTime = timeMonitor ? timeMonitor.ElapsedTime : 0f;
             gameData.LocalRoundStats.Score = didWin ? winTime : 999f;
             
             SortAndInvokeResults();
        }
    }
}