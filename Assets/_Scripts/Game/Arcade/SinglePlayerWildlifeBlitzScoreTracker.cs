using CosmicShore.Game.Ship;
using CosmicShore.Game.Arcade.Scoring;
using Obvious.Soap;
using Reflex.Attributes;
using UnityEngine;
using CosmicShore.Utility.Recording;
using CosmicShore.Models.Enums;
using CosmicShore.Game.UI;
using CosmicShore.Game.Arcade.TurnMonitors;
namespace CosmicShore.Game.Arcade
{
    public class SinglePlayerWildlifeBlitzScoreTracker : BaseScoreTracker
    {
        [Header("Blitz Settings")]
        [SerializeField] SingleplayerWildlifeBlitzTurnMonitor turnMonitor;
        [SerializeField] TimeBasedTurnMonitor timeMonitor;
        
        [Header("Events")]
        [SerializeField] ScriptableEventNoParam eventOnScoreChanged;

        [Inject] UGSStatsManager ugsStatsManager;

        private bool isTracking = false;

        void OnEnable() { SubscribeEvents(); }

        void OnDisable()
        {
            UnsubscribeEvents();
            StopTracking(); 
        }

        public void StartTracking()
        {
            if (isTracking) return; 
            
            LifeForm.OnLifeFormDeath += OnScoringEvent;
            ElementalCrystalImpactor.OnCrystalCollected += OnCrystalScoringEvent;
            isTracking = true;
            CSDebug.Log("[ScoreTracker] Started Tracking");
        }

        public void StopTracking()
        {
            if (!isTracking) return;

            LifeForm.OnLifeFormDeath -= OnScoringEvent;
            ElementalCrystalImpactor.OnCrystalCollected -= OnCrystalScoringEvent;
            isTracking = false;
            CSDebug.Log("[ScoreTracker] Stopped Tracking");
        }
        
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
            if (gameData?.LocalRoundStats != null) gameData.LocalRoundStats.Score = 0;
            if (eventOnScoreChanged) eventOnScoreChanged.Raise();
        }

        protected override void CalculateWinnerAndInvokeEvent()
        {
             if (!turnMonitor || gameData.LocalRoundStats == null) return;
             
             bool didWin = turnMonitor.DidPlayerWin; 
             float winTime = timeMonitor ? timeMonitor.ElapsedTime : 0f;
             gameData.LocalRoundStats.Score = didWin ? winTime : 999f;

             if (ugsStatsManager)
             {
                 var lifeScoring = GetScoring<LifeFormsKilledScoring>();
                 var crystalScoring = GetScoring<ElementalCrystalsCollectedBlitzScoring>();

                 int kills = lifeScoring?.GetTotalLifeFormsKilled() ?? 0;
                 int crystals = crystalScoring?.GetTotalCrystalsCollected() ?? 0;
                 int finalScore = (int)gameData.LocalRoundStats.Score;

                 ugsStatsManager.ReportBlitzStats(
                     GameModes.WildlifeBlitz,
                     gameData.SelectedIntensity.Value,
                     crystals,
                     kills,
                     finalScore
                 );

                 // Report per-vessel telemetry
                 if (gameData.LocalPlayer?.Vessel is Component vc
                     && vc.TryGetComponent<VesselTelemetry>(out var vt))
                 {
                     ugsStatsManager.ReportVesselTelemetry(
                         vt, gameData.LocalPlayer.Vessel.VesselStatus.VesselType.ToString());
                 }
             }
             
             SortAndInvokeResults();
        }
    }
}