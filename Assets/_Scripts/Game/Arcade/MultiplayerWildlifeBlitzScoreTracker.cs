using CosmicShore.Core;
using CosmicShore.Game.Arcade.Scoring;
using CosmicShore.Soap;
using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    /// <summary>
    /// Networked score tracker for multiplayer co-op Wildlife Blitz.
    /// Tracks kills and crystals locally, reports kills to the controller for server sync.
    /// All players contribute to a shared team score.
    /// </summary>
    public class MultiplayerWildlifeBlitzScoreTracker : BaseScoreTracker
    {
        [Header("Blitz Settings")]
        [SerializeField] private MultiplayerWildlifeBlitzMiniGame controller;

        [Header("Events")]
        [SerializeField] ScriptableEventNoParam eventOnScoreChanged;

        private bool _isTracking;

        void OnEnable() { SubscribeEvents(); }
        void OnDisable()
        {
            UnsubscribeEvents();
            StopTracking();
        }

        public void StartTracking()
        {
            if (_isTracking) return;

            LifeForm.OnLifeFormDeath += OnLifeFormKilled;
            ElementalCrystalImpactor.OnCrystalCollected += OnCrystalCollected;
            _isTracking = true;
            Debug.Log("[MPBlitzScoreTracker] Started Tracking");
        }

        public void StopTracking()
        {
            if (!_isTracking) return;

            LifeForm.OnLifeFormDeath -= OnLifeFormKilled;
            ElementalCrystalImpactor.OnCrystalCollected -= OnCrystalCollected;
            _isTracking = false;
            Debug.Log("[MPBlitzScoreTracker] Stopped Tracking");
        }

        void OnLifeFormKilled(string playerName, int cellId)
        {
            // Only process kills by the local player
            if (gameData.LocalPlayer == null || playerName != gameData.LocalPlayer.Name)
                return;

            var lifeFormScoring = GetScoring<LifeFormsKilledScoring>();
            if (lifeFormScoring != null)
                AddScore(lifeFormScoring.ScorePerKill);

            // Report kill to controller for server-side per-player tracking
            if (controller != null)
                controller.ReportLifeFormKill(playerName);
        }

        void OnCrystalCollected(string playerName)
        {
            if (gameData.LocalPlayer == null || playerName != gameData.LocalPlayer.Name)
                return;

            var crystalScoring = GetScoring<ElementalCrystalsCollectedBlitzScoring>();
            if (crystalScoring != null)
                AddScore(crystalScoring.GetScoreMultiplier());
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
            // In multiplayer co-op, the controller handles final score calculation
            // and syncs results to all clients via RPCs. Nothing to do here.
        }
    }
}
