using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Game.Analytics;
using CosmicShore.Game.UI;
using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Game.Arcade
{
    public class NeedleThreadScoreTracker : BaseScoreTracker, IStatExposable
    {
        [Header("Dependencies")]
        [SerializeField] VolumeDestructionTurnMonitor turnMonitor;
        [SerializeField] private NeedleThreadController controller;

        [Header("Settings")]
        [SerializeField] float penaltyScoreBase = 10000f;
        [SerializeField] bool showDebugLogs = true;

        private float _elapsedRaceTime;
        private IVesselStatus _observedVessel;
        private VesselTelemetry _vesselTelemetry;
        private bool _isTracking;
        private bool _hasReported;

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

        void HandleTurnStarted()
        {
            _hasReported     = false;
            _elapsedRaceTime = 0f;

            if (gameData.LocalPlayer?.Vessel == null) return;

            _observedVessel = gameData.LocalPlayer.Vessel.VesselStatus;

            if (gameData.LocalPlayer.Vessel is Component vesselComponent)
                _vesselTelemetry = vesselComponent.GetComponent<VesselTelemetry>();

            if (_vesselTelemetry == null)
                CSDebug.LogWarning("[NeedleThreadScoreTracker] No VesselTelemetry found on local vessel.");

            _isTracking = true;
        }

        void Update()
        {
            if (!_isTracking || _observedVessel == null) return;
            _elapsedRaceTime += Time.deltaTime;
            if (gameData.LocalRoundStats != null)
                gameData.LocalRoundStats.Score = _elapsedRaceTime;
        }

        void HandleGameEnd()
        {
            if (_hasReported) return;
            _hasReported = true;
            _isTracking  = false;

            OnTurnEnded();

            float volumeRemaining = 0f;
            if (turnMonitor)
            {
                if (float.TryParse(turnMonitor.GetRemainingVolumeToDestroy(), out float parsed))
                    volumeRemaining = parsed;
            }

            bool  isWinner   = volumeRemaining <= 0f;
            float finalScore = isWinner ? _elapsedRaceTime : (penaltyScoreBase + Mathf.CeilToInt(volumeRemaining));

            gameData.LocalRoundStats.Score = finalScore;

            if (showDebugLogs)
                CSDebug.Log($"<color=cyan>[NeedleThreadTracker] GAME END. Score: {finalScore:F2} | Winner: {isWinner} | Volume Remaining: {volumeRemaining:F1}</color>");

            if (isWinner)
            {
                if (controller) controller.ReportLocalPlayerFinished(finalScore);

                try
                {
                    if (UGSStatsManager.Instance && _vesselTelemetry != null)
                    {
                        UGSStatsManager.Instance.ReportNeedleThreadStats(
                            GameModes.NeedleThread,
                            gameData.SelectedIntensity.Value,
                            gameData.LocalRoundStats.HostileVolumeDestroyed,
                            finalScore
                        );
                    }

                    if (UGSStatsManager.Instance && _vesselTelemetry != null && _observedVessel != null)
                        UGSStatsManager.Instance.ReportVesselTelemetry(_vesselTelemetry, _observedVessel.VesselType.ToString());
                }
                catch (System.Exception ex)
                {
                    CSDebug.LogError($"[NeedleThreadTracker] Telemetry reporting failed: {ex.Message}");
                }
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
                { "Longest Drift",       _vesselTelemetry.MaxDriftTime  },
                { "Max Boost Time",      _vesselTelemetry.MaxBoostTime  },
                { "Volume Destroyed",    gameData.LocalRoundStats?.HostileVolumeDestroyed ?? 0f },
            };

            foreach (var kvp in stats)
                CSDebug.Log($"[NeedleThreadScoreTracker] {kvp.Key}: {kvp.Value}");

            return stats;
        }
    }
}
