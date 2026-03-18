using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Game.Analytics;
using CosmicShore.Game.UI;
using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Game.Arcade
{
    /// <summary>
    /// Handles scoring logic for Drag Scouting (multiplayer crystal race with Manta).
    /// Same end-game logic as HexRace — first to collect all crystals wins.
    /// </summary>
    public class DragScoutingScoreTracker : BaseScoreTracker, IStatExposable
    {
        [Header("Dependencies")]
        [SerializeField] CrystalCollisionTurnMonitor turnMonitor;
        [SerializeField] private DragScoutingController controller;

        [Header("Settings")]
        [SerializeField] float penaltyScoreBase = 10000f;
        [SerializeField] bool showDebugLogs = true;

        private float _elapsedRaceTime;
        private IVesselStatus _observedVessel;
        private VesselTelemetry _vesselTelemetry;
        private bool _isTracking;
        private bool _hasReported;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        void Start()
        {
            SubscribeEvents();
            gameData.OnMiniGameTurnEnd.OnRaised     += HandleGameEnd;
            gameData.OnMiniGameTurnStarted.OnRaised += HandleTurnStarted;
        }

        void OnDestroy()
        {
            UnsubscribeEvents();
            if (gameData == null) return;
            gameData.OnMiniGameTurnStarted.OnRaised -= HandleTurnStarted;
            if (gameData.OnMiniGameTurnEnd != null)
                gameData.OnMiniGameTurnEnd.OnRaised -= HandleGameEnd;
        }

        void OnDisable() => _isTracking = false;

        // ── Turn lifecycle ─────────────────────────────────────────────────────

        void HandleTurnStarted()
        {
            _hasReported     = false;
            _elapsedRaceTime = 0f;

            if (gameData.LocalPlayer?.Vessel == null) return;

            _observedVessel = gameData.LocalPlayer.Vessel.VesselStatus;

            if (gameData.LocalPlayer.Vessel is Component vesselComponent)
                _vesselTelemetry = vesselComponent.GetComponent<VesselTelemetry>();

            if (_vesselTelemetry == null)
                CSDebug.LogWarning("[DragScoutingScoreTracker] No VesselTelemetry found on local vessel.");

            _isTracking = true;
        }

        // ── Update ─────────────────────────────────────────────────────────────

        void Update()
        {
            if (!_isTracking || _observedVessel == null) return;
            _elapsedRaceTime += Time.deltaTime;
            if (gameData.LocalRoundStats != null)
                gameData.LocalRoundStats.Score = _elapsedRaceTime;
        }

        // ── Game end ───────────────────────────────────────────────────────────

        void HandleGameEnd()
        {
            if (_hasReported) return;
            _hasReported = true;
            _isTracking  = false;

            OnTurnEnded();

            int crystalsRemaining = 0;
            if (turnMonitor && int.TryParse(turnMonitor.GetRemainingCrystalsCountToCollect(), out int parsed))
                crystalsRemaining = parsed;

            bool  isWinner   = crystalsRemaining <= 0;
            float finalScore = isWinner ? _elapsedRaceTime : (penaltyScoreBase + crystalsRemaining);

            gameData.LocalRoundStats.Score = finalScore;

            if (showDebugLogs)
                CSDebug.Log($"<color=yellow>[DragScoutingTracker] GAME END. Score: {finalScore:F2} | Winner: {isWinner} | Crystals Remaining: {crystalsRemaining}</color>");

            if (isWinner)
            {
                if (UGSStatsManager.Instance && _vesselTelemetry != null)
                {
                    UGSStatsManager.Instance.ReportHexRaceStats(
                        GameModes.DragScouting,
                        gameData.SelectedIntensity.Value,
                        0, // No clean streak tracking for Manta
                        _vesselTelemetry.MaxDriftTime,
                        0, // No joust tracking for this mode
                        finalScore
                    );
                }

                // Report per-vessel telemetry to UGS
                if (UGSStatsManager.Instance && _vesselTelemetry != null && _observedVessel != null)
                    UGSStatsManager.Instance.ReportVesselTelemetry(_vesselTelemetry, _observedVessel.VesselType.ToString());

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
            if (_vesselTelemetry == null) return new Dictionary<string, object>();

            var stats = new Dictionary<string, object>
            {
                { "Longest Drift",  _vesselTelemetry.MaxDriftTime  },
                { "Max Boost Time", _vesselTelemetry.MaxBoostTime  }
            };

            foreach (var kvp in stats)
                CSDebug.Log($"[DragScoutingScoreTracker] {kvp.Key}: {kvp.Value}");

            return stats;
        }
    }
}
