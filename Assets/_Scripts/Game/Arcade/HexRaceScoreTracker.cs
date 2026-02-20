using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Game.Analytics;
using CosmicShore.Game.UI;
using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class HexRaceScoreTracker : BaseScoreTracker, IStatExposable
    {
        [Header("Dependencies")]
        [SerializeField] CrystalCollisionTurnMonitor turnMonitor;
        [SerializeField] private HexRaceController controller;

        [Header("Settings")]
        [SerializeField] float penaltyScoreBase = 10000f;
        [SerializeField] bool showDebugLogs = true;

        [Header("Joust")]
        [SerializeField] ScriptableEventString OnJoustCollisionEvent;

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

        private int _joustsWonSession;
        public int JoustsWonSession => _joustsWonSession;

        void Start()
        {
            SubscribeEvents();

            gameData.OnMiniGameTurnEnd.OnRaised += HandleGameEnd;

            ElementalCrystalImpactor.OnCrystalCollected += HandleCrystalCollected;
            VesselResetBoostPrismEffectSO.OnPrismCollision += HandlePrismCollision;
            gameData.OnMiniGameTurnStarted.OnRaised += HandleTurnStarted;

            if (OnJoustCollisionEvent) OnJoustCollisionEvent.OnRaised += HandleJoustEvent;
        }

        void OnDestroy()
        {
            UnsubscribeEvents();

            ElementalCrystalImpactor.OnCrystalCollected -= HandleCrystalCollected;
            VesselResetBoostPrismEffectSO.OnPrismCollision -= HandlePrismCollision;

            if (OnJoustCollisionEvent) OnJoustCollisionEvent.OnRaised -= HandleJoustEvent;

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

        void HandleJoustEvent(string winner)
        {
            if (gameData.LocalPlayer?.Name == winner) _joustsWonSession++;
        }

        void HandleGameEnd()
        {
            if (_hasReported) return;
            _hasReported = true;
            StopTracking();
            OnTurnEnded();
            if (_currentDriftTime > MaxDriftTimeRecord) MaxDriftTimeRecord = _currentDriftTime;
            if (_currentBoostTime > MaxHighBoostTimeRecord) MaxHighBoostTimeRecord = _currentBoostTime;

            int crystalsRemaining = 0;
            if (turnMonitor && int.TryParse(turnMonitor.GetRemainingCrystalsCountToCollect(), out int parsed))
                crystalsRemaining = parsed;

            bool isWinner = crystalsRemaining <= 0;

            float finalScore = isWinner ? _elapsedRaceTime : (penaltyScoreBase + crystalsRemaining);

            gameData.LocalRoundStats.Score = finalScore;

            if (showDebugLogs)
                Debug.Log($"<color=yellow>[HexRaceTracker] GAME END. Score: {finalScore:F2} | Winner: {isWinner} | Crystals Remaining: {crystalsRemaining}</color>");

            if (isWinner)
            {
                if (UGSStatsManager.Instance)
                {
                    UGSStatsManager.Instance.ReportHexRaceStats(
                        GameModes.HexRace,
                        gameData.SelectedIntensity.Value,
                        MaxCleanStreak,
                        MaxDriftTimeRecord,
                        _joustsWonSession,
                        finalScore
                    );
                }

                if (controller) controller.ReportLocalPlayerFinished(finalScore);
            }

            if (controller == null)
                SortAndInvokeResults();
        }

        protected override void CalculateWinnerAndInvokeEvent()
        {
            // Not used — HandleGameEnd drives the flow
        }

        public Dictionary<string, object> GetExposedStats()
        {
            return new Dictionary<string, object>
            {
                { "Max Clean Streak", MaxCleanStreak },
                { "Longest Drift", MaxDriftTimeRecord },
                { "Max Boost Time", MaxHighBoostTimeRecord },
                { "Jousts Won", JoustsWonSession }
            };
        }
    }
}
