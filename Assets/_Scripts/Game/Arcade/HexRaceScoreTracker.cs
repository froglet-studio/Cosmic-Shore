using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Game.Analytics;
using CosmicShore.Game.UI;
using Cysharp.Threading.Tasks; 
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class HexRaceScoreTracker : BaseScoreTracker, IStatExposable
    {
        [Header("Dependencies")]
        [SerializeField] CrystalCollisionTurnMonitor turnMonitor;

        [Header("Settings")]
        [SerializeField] float penaltyScoreBase = 10000f;
        [SerializeField] bool showDebugLogs = true;

        public float MaxDriftTimeRecord { get; private set; }
        public float MaxHighBoostTimeRecord { get; private set; }
        public int MaxCleanStreak { get; private set; }

        private float _currentDriftTime;
        private float _currentBoostTime;
        private int _currentCleanStreak;
        private float _elapsedRaceTime;
        
        private IVesselStatus _observedVessel;
        private bool _isTracking;
        private bool _hasReported;
        private IStatExposable _statExposableImplementation;

        protected virtual void Start()
        {
            SubscribeEvents();
            
            // ONLY ONE EVENT: OnMiniGameTurnEnd
            gameData.OnMiniGameTurnEnd.OnRaised += HandleGameEnd;
            
            ElementalCrystalImpactor.OnCrystalCollected += HandleCrystalCollected;
            VesselResetBoostPrismEffectSO.OnPrismCollision += HandlePrismCollision;
            gameData.OnMiniGameTurnStarted.OnRaised += HandleTurnStarted;
        }

        protected virtual void OnDestroy()
        {
            UnsubscribeEvents();
            
            ElementalCrystalImpactor.OnCrystalCollected -= HandleCrystalCollected;
            VesselResetBoostPrismEffectSO.OnPrismCollision -= HandlePrismCollision;

            if (gameData == null) return;
            gameData.OnMiniGameTurnStarted.OnRaised -= HandleTurnStarted;
            if (gameData.OnMiniGameTurnEnd != null)
                gameData.OnMiniGameTurnEnd.OnRaised -= HandleGameEnd;
        }

        void HandleTurnStarted()
        {
            _hasReported = false;
            if (gameData.LocalPlayer?.Vessel != null)
            {
                StartTracking(gameData.LocalPlayer.Vessel.VesselStatus);
            }
        }

        public void StartTracking(IVesselStatus vessel)
        {
            if (_isTracking) return;
            _observedVessel = vessel;
            _isTracking = true;
            _elapsedRaceTime = 0f;
            if (showDebugLogs) Debug.Log($"<color=green>[HexRaceTracker] GO! Timer Started.</color>");
        }

        public void StopTracking() => _isTracking = false;
        void OnDisable() => StopTracking();

        void Update()
        {
            if (!_isTracking || _observedVessel == null) return;

            _elapsedRaceTime += Time.deltaTime;
            
            if (gameData.LocalRoundStats != null)
                gameData.LocalRoundStats.Score = _elapsedRaceTime;

            TrackDrift();
            TrackBoost();
        }

        void TrackDrift()
        {
            if (_observedVessel.IsDrifting) _currentDriftTime += Time.deltaTime;
            else { if (_currentDriftTime > 0 && _currentDriftTime > MaxDriftTimeRecord) MaxDriftTimeRecord = _currentDriftTime; _currentDriftTime = 0; }
        }
        
        void TrackBoost()
        {
            if (_observedVessel.IsBoosting && _observedVessel.BoostMultiplier >= 4.0f) _currentBoostTime += Time.deltaTime;
            else { if (_currentBoostTime > 0 && _currentBoostTime > MaxHighBoostTimeRecord) MaxHighBoostTimeRecord = _currentBoostTime; _currentBoostTime = 0; }
        }
        
        void HandleCrystalCollected(string p) 
        { 
            if (_isTracking && _observedVessel.PlayerName == p) 
            { 
                _currentCleanStreak++; 
                if(_currentCleanStreak > MaxCleanStreak) MaxCleanStreak = _currentCleanStreak; 
            } 
        }
        
        void HandlePrismCollision() 
        { 
            if (_isTracking) _currentCleanStreak = 0; 
        }
        void HandleGameEnd()
        {
            if (_hasReported) return;
            _hasReported = true;
            StopTracking();
            OnTurnEnded();
            if (_currentDriftTime > MaxDriftTimeRecord) MaxDriftTimeRecord = _currentDriftTime;
            if (_currentBoostTime > MaxHighBoostTimeRecord) MaxHighBoostTimeRecord = _currentBoostTime;

            // Get crystals remaining
            int crystalsRemaining = 0;
            if (turnMonitor && int.TryParse(turnMonitor.GetRemainingCrystalsCountToCollect(), out int parsed))
                crystalsRemaining = parsed;

            bool isWinner = DetermineIfWinner(crystalsRemaining);

            float finalScore = isWinner ? _elapsedRaceTime : (penaltyScoreBase + crystalsRemaining);

            gameData.LocalRoundStats.Score = finalScore;

            if (showDebugLogs) 
                Debug.Log($"<color=yellow>[HexRaceTracker] GAME END. Score: {finalScore:F2} | Winner: {isWinner} | Crystals Remaining: {crystalsRemaining}</color>");

            if (UGSStatsManager.Instance && !gameData.IsMultiplayerMode && isWinner)
            {
                UGSStatsManager.Instance.ReportHexRaceStats(
                    gameData.GameMode, 
                    gameData.SelectedIntensity.Value, 
                    MaxCleanStreak, 
                    MaxDriftTimeRecord, 
                    MaxHighBoostTimeRecord, 
                    finalScore
                );
            }
            ReportToMultiplayerController(finalScore, isWinner);

            if (!gameData.IsMultiplayerMode)
            {
                SortAndInvokeResults();
            }
        }

        protected virtual bool DetermineIfWinner(int localCrystalsRemaining)
        {
            // Singleplayer: Always a winner if crystals remaining = 0
            if (!gameData.IsMultiplayerMode)
                return localCrystalsRemaining == 0;

            // Multiplayer: Determined by child class
            return false;
        }

        protected override void CalculateWinnerAndInvokeEvent()
        {
            // Not used anymore
        }

        protected virtual void ReportToMultiplayerController(float finalScore, bool isWinner)
        {
            // Overridden in multiplayer
        }

        public Dictionary<string, object> GetExposedStats()
        {
            return new Dictionary<string, object>
            {
                { "Max Clean Streak", MaxCleanStreak },
                { "Longest Drift", MaxDriftTimeRecord },
                { "Max Boost Time", MaxHighBoostTimeRecord }
            };
        }
    }
}