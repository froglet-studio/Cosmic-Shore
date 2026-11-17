// FrictionScoreTracker.cs
using CosmicShore.Gameplay;
using Reflex.Attributes;
using UnityEngine;
using CosmicShore.Utility;
using CosmicShore.Core;
using CosmicShore.UI;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Ticks local elapsed race time into gameData.LocalRoundStats.Score while a Friction
    /// turn is active — the running clock FrictionController.HandleWin reads as the
    /// winner's finish time. Modeled on HexRaceScoreTracker, minus winner/loss
    /// determination: that stays server-side in FrictionController.OnTurnEndedCustom, so
    /// this tracker only supplies the clock and reports mode-agnostic vessel telemetry.
    /// </summary>
    public class FrictionScoreTracker : BaseScoreTracker
    {
        [Inject] UGSStatsManager ugsStatsManager;

        float _elapsedRaceTime;
        IVesselStatus _observedVessel;
        VesselTelemetry _vesselTelemetry;
        bool _isTracking;
        bool _hasReported;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        void Start()
        {
            SubscribeEvents();
            gameData.OnMiniGameTurnEnd.OnRaised     += HandleTurnEnded;
            gameData.OnMiniGameTurnStarted.OnRaised += HandleTurnStarted;
        }

        public override void OnDestroy()
        {
            UnsubscribeEvents();
            if (gameData != null)
            {
                gameData.OnMiniGameTurnStarted.OnRaised -= HandleTurnStarted;
                if (gameData.OnMiniGameTurnEnd != null)
                    gameData.OnMiniGameTurnEnd.OnRaised -= HandleTurnEnded;
            }
            base.OnDestroy();
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
                CSDebug.LogWarning("[FrictionScoreTracker] No VesselTelemetry found on local vessel.");

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

        // ── Turn end ───────────────────────────────────────────────────────────

        void HandleTurnEnded()
        {
            if (_hasReported) return;
            _hasReported = true;
            _isTracking  = false;

            OnTurnEnded();

            // Winner/loser scoring is finalized server-side by
            // FrictionController.OnTurnEndedCustom → SyncFinalResults_ClientRpc. This
            // tracker's job ends at supplying the elapsed-time clock above. Vessel
            // telemetry reporting is mode-agnostic (writes per-vessel-type stats, not a
            // per-mode leaderboard bucket), so it's safe to submit unconditionally here —
            // unlike UGSStatsManager.ReportHexRaceStats, which would write Friction results
            // into HexRace's MultiHexStats bucket regardless of the GameModes value passed.
            if (ugsStatsManager && _vesselTelemetry != null && _observedVessel != null)
                ugsStatsManager.ReportVesselTelemetry(_vesselTelemetry, _observedVessel.VesselType.ToString());
        }

        protected override void CalculateWinnerAndInvokeEvent()
        {
            // Not used — FrictionController drives final scoring server-side.
        }
    }
}
