using CosmicShore.Core;
using CosmicShore.Game.Analytics;
using CosmicShore.Game.Arcade.Scoring;
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

        // [Visual Note] Streak Stats
        public int MaxCleanStreak { get; private set; }
        private int _currentCleanStreak;

        // Other Stats
        public float MaxDriftTimeRecord { get; private set; }
        public float MaxHighBoostTimeRecord { get; private set; }

        private float _currentDriftTime;
        private float _currentBoostTime;
        private IVesselStatus _observedVessel;
        private bool _isTracking;
        private TimePlayedScoring _timeScoring;

        protected virtual void Start()
        {
            _timeScoring = new TimePlayedScoring(gameData, 1.0f);

            // Turn Monitor Event
            if (turnMonitor) turnMonitor.OnTurnFinished += HandleTurnFinished;

            // [Visual Note] SUBSCRIBE TO STREAK EVENTS
            // 1. Gain: When a crystal is collected
            ElementalCrystalImpactor.OnCrystalCollected += HandleCrystalCollected;
            
            // 2. Reset: When we hit a prism
            VesselResetBoostPrismEffectSO.OnPrismCollision += HandlePrismCollision;

            if (gameData.LocalPlayer?.Vessel != null)
                StartTracking(gameData.LocalPlayer.Vessel.VesselStatus);
        }

        protected virtual void OnDestroy()
        {
            if (turnMonitor) turnMonitor.OnTurnFinished -= HandleTurnFinished;
            
            // Unsubscribe to prevent memory leaks
            ElementalCrystalImpactor.OnCrystalCollected -= HandleCrystalCollected;
            VesselResetBoostPrismEffectSO.OnPrismCollision -= HandlePrismCollision;
        }

        #region Streak Logic (The Fix)

        void HandleCrystalCollected(string playerName)
        {
            if (!_isTracking) return;
            // Verify it is THIS player (important for split screen/multiplayer)
            if (_observedVessel != null && _observedVessel.PlayerName != playerName) return;

            _currentCleanStreak++;

            if (_currentCleanStreak > MaxCleanStreak)
            {
                MaxCleanStreak = _currentCleanStreak;
            }

            if (showDebugLogs) Debug.Log($"[HexRaceTracker] Streak: {_currentCleanStreak} (Best: {MaxCleanStreak})");
        }

        void HandlePrismCollision()
        {
            if (!_isTracking) return;

            if (showDebugLogs) Debug.Log($"<color=red>[HexRaceTracker] Prism Hit! Streak Reset (Was: {_currentCleanStreak})</color>");
            _currentCleanStreak = 0;
        }

        #endregion

        // ... [Rest of the Standard Tracking Code] ...
        
        void HandleTurnFinished() => CalculateWinnerAndInvokeEvent();

        public void StartTracking(IVesselStatus vessel)
        {
            if (_isTracking) return;
            _observedVessel = vessel;
            _isTracking = true;
            if (_timeScoring != null) _timeScoring.Subscribe();
        }

        public void StopTracking()
        {
            _isTracking = false;
            if (_timeScoring != null) _timeScoring.Unsubscribe();
        }

        void Update()
        {
            if (!_isTracking || _observedVessel == null) return;
            TrackDrift();
            TrackBoost();
        }

        void TrackDrift()
        {
            if (_observedVessel.IsDrifting)
                _currentDriftTime += Time.deltaTime;
            else
            {
                if (_currentDriftTime > MaxDriftTimeRecord) MaxDriftTimeRecord = _currentDriftTime;
                _currentDriftTime = 0;
            }
        }

        void TrackBoost()
        {
            if (_observedVessel.IsBoosting && _observedVessel.BoostMultiplier >= 4.0f)
                _currentBoostTime += Time.deltaTime;
            else
            {
                if (_currentBoostTime > MaxHighBoostTimeRecord) MaxHighBoostTimeRecord = _currentBoostTime;
                _currentBoostTime = 0;
            }
        }

        protected override void CalculateWinnerAndInvokeEvent()
        {
            if (!turnMonitor || gameData.LocalRoundStats == null) return;
            StopTracking();

            // Finalize time stats
            if (_currentDriftTime > MaxDriftTimeRecord) MaxDriftTimeRecord = _currentDriftTime;
            if (_currentBoostTime > MaxHighBoostTimeRecord) MaxHighBoostTimeRecord = _currentBoostTime;

            // Win/Loss Calculation
            int remaining = 0;
            int.TryParse(turnMonitor.GetRemainingCrystalsCountToCollect(), out remaining);
            bool isRaceFinished = remaining <= 0;
            
            float elapsedTime = gameData.LocalRoundStats.Score; 
            float finalScore = isRaceFinished ? elapsedTime : (penaltyScoreBase + remaining);
            gameData.LocalRoundStats.Score = finalScore;

            if (showDebugLogs) Debug.Log($"<color=yellow>[HexRaceTracker] FINAL -> Time: {elapsedTime} | Best Streak: {MaxCleanStreak}</color>");

            if (UGSStatsManager.Instance)
            {
                UGSStatsManager.Instance.ReportHexRaceStats(
                    MaxCleanStreak, // [Visual Note] Passing the STREAK here
                    MaxDriftTimeRecord, 
                    MaxHighBoostTimeRecord, 
                    finalScore
                );
            }
            
            SortAndInvokeResults();
        }
    }
}