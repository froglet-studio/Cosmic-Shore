using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Game.Analytics;
using CosmicShore.Game.UI;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    /// <summary>
    /// Handles scoring logic for HexRace (single and multiplayer).
    /// Vessel telemetry (drift, boost, streak, jousts) is owned by the vessel's
    /// VesselTelemetry subclass — this tracker only reads final values for UGS reporting.
    /// </summary>
    public class HexRaceScoreTracker : BaseScoreTracker, IStatExposable
    {
        [Header("Dependencies")]
        [SerializeField] CrystalCollisionTurnMonitor turnMonitor;
        [SerializeField] private HexRaceController controller;

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
                Debug.LogWarning("[HexRaceScoreTracker] No VesselTelemetry found on local vessel.");

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
                Debug.Log($"<color=yellow>[HexRaceTracker] GAME END. Score: {finalScore:F2} | Winner: {isWinner} | Crystals Remaining: {crystalsRemaining}</color>");

            if (isWinner)
            {
                // Cast to SquirrelVesselTelemetry to access vessel-specific stats for UGS.
                // If the vessel type doesn't have those stats, they'll just be 0.
                var squirrelTelemetry = _vesselTelemetry as SquirrelVesselTelemetry;

                if (UGSStatsManager.Instance && _vesselTelemetry != null)
                {
                    UGSStatsManager.Instance.ReportHexRaceStats(
                        GameModes.HexRace,
                        gameData.SelectedIntensity.Value,
                        squirrelTelemetry?.MaxCleanStreak ?? 0,
                        _vesselTelemetry.MaxDriftTime,
                        squirrelTelemetry?.JoustsWon ?? 0,
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

        // Temporary — logs telemetry values for verification.
        // Remove once EventDrivenStatsProvider is confirmed working.
        public Dictionary<string, object> GetExposedStats()
        {
            if (_vesselTelemetry == null) return new Dictionary<string, object>();

            var squirrel = _vesselTelemetry as SquirrelVesselTelemetry;
            var stats = new Dictionary<string, object>
            {
                { "Longest Drift",    _vesselTelemetry.MaxDriftTime           },
                { "Max Boost Time",   _vesselTelemetry.MaxBoostTime           },
                { "Max Clean Streak", squirrel?.MaxCleanStreak ?? 0           },
                { "Jousts Won",       squirrel?.JoustsWon      ?? 0           }
            };

            foreach (var kvp in stats)
                Debug.Log($"[HexRaceScoreTracker] {kvp.Key}: {kvp.Value}");

            return stats;
        }
    }
}