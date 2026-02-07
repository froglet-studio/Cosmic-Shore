using CosmicShore.Core;
using CosmicShore.Game.Analytics;
using CosmicShore.Game.UI;
using Cysharp.Threading.Tasks; 
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class HexRaceScoreTracker : BaseScoreTracker
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
        private bool _hasFinished; // NEW: Track if we've already finished

        protected virtual void Start()
        {
            // Subscribe to BaseScoreTracker events so TimePlayedScoring works!
            SubscribeEvents();
            
            // Events
            if (turnMonitor) turnMonitor.OnTurnFinished += HandleTurnFinished;
            ElementalCrystalImpactor.OnCrystalCollected += HandleCrystalCollected;
            VesselResetBoostPrismEffectSO.OnPrismCollision += HandlePrismCollision;

            gameData.OnMiniGameTurnStarted.OnRaised += HandleTurnStarted;
            gameData.OnMiniGameTurnEnd.OnRaised += HandleGlobalTurnEnd;
        }

        protected virtual void OnDestroy()
        {
            // Unsubscribe from BaseScoreTracker events
            UnsubscribeEvents();
            
            if (turnMonitor) turnMonitor.OnTurnFinished -= HandleTurnFinished;
            ElementalCrystalImpactor.OnCrystalCollected -= HandleCrystalCollected;
            VesselResetBoostPrismEffectSO.OnPrismCollision -= HandlePrismCollision;

            if (gameData == null) return;
            gameData.OnMiniGameTurnStarted.OnRaised -= HandleTurnStarted;
            if (gameData.OnMiniGameTurnEnd != null)
                gameData.OnMiniGameTurnEnd.OnRaised -= HandleGlobalTurnEnd;
        }

        // Triggered by Controller after Countdown
        void HandleTurnStarted()
        {
            _hasFinished = false; // Reset finish flag
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

            // Local Timer runs only when tracking
            _elapsedRaceTime += Time.deltaTime;
            
            // Sync to local display immediately
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

        void HandleGlobalTurnEnd()
        {
            // This fires when the race ends (first player finished)
            // If we already finished and reported, do nothing
            if (_hasFinished)
            {
                if (showDebugLogs) Debug.Log($"<color=cyan>[HexRaceTracker] HandleGlobalTurnEnd - Already finished, ignoring</color>");
                return;
            }

            // If we're still tracking, we didn't finish - force loss and report current score
            if (_isTracking) 
            {
                if (showDebugLogs) Debug.Log($"<color=red>[HexRaceTracker] HandleGlobalTurnEnd - Race ended, didn't finish. Reporting loss.</color>");
                CalculateWinnerAndInvokeEvent(forcedLoss: true);
            }
        }

        void HandleTurnFinished()
        {
            // We finished naturally by collecting all crystals
            if (_hasFinished) return; // Prevent double finish
            
            if (showDebugLogs) Debug.Log($"<color=green>[HexRaceTracker] HandleTurnFinished - Natural finish</color>");
            CalculateWinnerAndInvokeEvent(forcedLoss: false);
        }

        protected override void CalculateWinnerAndInvokeEvent() => CalculateWinnerAndInvokeEvent(false);

        protected virtual void CalculateWinnerAndInvokeEvent(bool forcedLoss)
        {
            if (!turnMonitor || gameData.LocalRoundStats == null) return;
            if (_hasFinished) return; // Prevent double finish
            
            _hasFinished = true; // Mark as finished
            StopTracking(); // Stop the timer immediately
            
            // CRITICAL: Stop all scoring systems (especially TimePlayedScoring!)
            OnTurnEnded(); // This calls Unsubscribe on all scoring systems

            // Stats Finalization
            if (_currentDriftTime > MaxDriftTimeRecord) MaxDriftTimeRecord = _currentDriftTime;
            if (_currentBoostTime > MaxHighBoostTimeRecord) MaxHighBoostTimeRecord = _currentBoostTime;

            int remaining = 0;
            if (int.TryParse(turnMonitor.GetRemainingCrystalsCountToCollect(), out int parsed)) remaining = parsed;

            // Win Logic
            bool isWin = (remaining <= 0) && !forcedLoss;
            float finalScore = isWin ? _elapsedRaceTime : (penaltyScoreBase + remaining);
            
            // Set Local Score and FREEZE it
            gameData.LocalRoundStats.Score = finalScore;

            if (showDebugLogs) Debug.Log($"<color=yellow>[HexRaceTracker] FINISH. Score: {finalScore} (Win: {isWin}, Forced Loss: {forcedLoss})</color>");

            if (UGSStatsManager.Instance)
            {
                UGSStatsManager.Instance.ReportHexRaceStats(MaxCleanStreak, MaxDriftTimeRecord, MaxHighBoostTimeRecord, finalScore);
            }
            
            // Report to multiplayer controller (will wait for other players)
            ReportToMultiplayerController(finalScore);

            // DON'T call SortAndInvokeResults in multiplayer - let controller handle after sync
            if (!gameData.IsMultiplayerMode)
            {
                SortAndInvokeResults();
            }
        }

        protected virtual void ReportToMultiplayerController(float finalScore)
        {
            // Base implementation does nothing (singleplayer)
        }
    }
}