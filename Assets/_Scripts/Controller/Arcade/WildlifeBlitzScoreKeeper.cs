using Obvious.Soap;
using Reflex.Attributes;
using UnityEngine;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Wildlife Blitz score keeper (single-player scene) - the standalone successor of
    /// SinglePlayerWildlifeBlitzScoreTracker, off the legacy BaseScoreTracker family.
    /// Owns the blitz composite: the local player's Score is recomputed absolutely on
    /// every scoring event as hostileVolume + kills*killPoints + crystals*crystalPoints
    /// (the same weights the legacy scoringConfigs carried: volume x1, kills x5,
    /// crystals x20). The SingleplayerWildlifeBlitzTurnMonitor races this Score against
    /// the cell's CellEndGameScore. UGS stats report on OnMiniGameEnd, AFTER the
    /// controller's rule-driven EndGame finalized the Score (win time / DNF).
    /// </summary>
    public class WildlifeBlitzScoreKeeper : MonoBehaviour
    {
        [Header("Blitz Scoring")]
        [Tooltip("Points per lifeform killed (legacy Mode 15 multiplier).")]
        [SerializeField] float killPoints = 5f;
        [Tooltip("Points per elemental crystal collected (legacy Mode 16 multiplier).")]
        [SerializeField] float crystalPoints = 20f;

        [Header("Events")]
        [SerializeField] ScriptableEventNoParam eventOnScoreChanged;

        [Inject] GameDataSO gameData;
        [Inject] UGSStatsManager ugsStatsManager;

        int _kills;
        int _crystals;
        bool _isTracking;

        /// <summary>Total lifeforms killed this game (scoreboard stats + UGS).</summary>
        public int TotalLifeFormsKilled => _kills;
        /// <summary>Total elemental crystals collected this game (scoreboard stats + UGS).</summary>
        public int TotalCrystalsCollected => _crystals;
        // B15 discipline: remember the one stats record we subscribed to and detach from
        // THAT record - never by re-reading gameData.LocalRoundStats at teardown time.
        IRoundStats _subscribedStats;

        void OnEnable()
        {
            if (gameData != null)
            {
                gameData.OnMiniGameEnd.OnRaised += ReportStats;
                gameData.OnResetForReplay.OnRaised += ResetScores;
            }
        }

        void OnDisable()
        {
            if (gameData != null)
            {
                gameData.OnMiniGameEnd.OnRaised -= ReportStats;
                gameData.OnResetForReplay.OnRaised -= ResetScores;
            }
            StopTracking();
        }

        public void StartTracking()
        {
            if (_isTracking) return;

            LifeForm.OnLifeFormDeath += OnLifeFormKilled;
            ElementalCrystalImpactor.OnCrystalCollected += OnCrystalCollected;

            if (_subscribedStats == null && gameData.LocalRoundStats != null)
            {
                _subscribedStats = gameData.LocalRoundStats;
                _subscribedStats.OnHostileVolumeDestroyedChanged += OnHostileVolumeChanged;
            }

            _isTracking = true;
            CSDebug.Log("[WildlifeBlitzScoreKeeper] Started Tracking");
        }

        public void StopTracking()
        {
            if (!_isTracking) return;

            LifeForm.OnLifeFormDeath -= OnLifeFormKilled;
            ElementalCrystalImpactor.OnCrystalCollected -= OnCrystalCollected;

            if (_subscribedStats != null)
            {
                _subscribedStats.OnHostileVolumeDestroyedChanged -= OnHostileVolumeChanged;
                _subscribedStats = null;
            }

            _isTracking = false;
            CSDebug.Log("[WildlifeBlitzScoreKeeper] Stopped Tracking");
        }

        void OnLifeFormKilled(string playerName, int cellId)
        {
            _kills++;
            RecomputeScore();
        }

        void OnCrystalCollected(string playerName)
        {
            _crystals++;
            RecomputeScore();
        }

        void OnHostileVolumeChanged(IRoundStats stats) => RecomputeScore();

        /// <summary>
        /// Absolute recompute - idempotent, replacing the legacy mix of incremental
        /// AddScore writes and strategy-sum overwrites that raced each other.
        /// </summary>
        void RecomputeScore()
        {
            var local = gameData.LocalRoundStats;
            if (local == null) return;
            local.Score = local.HostileVolumeDestroyed + _kills * killPoints + _crystals * crystalPoints;
            if (eventOnScoreChanged) eventOnScoreChanged.Raise();
        }

        public void ResetScores()
        {
            _kills = 0;
            _crystals = 0;
            if (gameData?.LocalRoundStats != null) gameData.LocalRoundStats.Score = 0;
            if (eventOnScoreChanged) eventOnScoreChanged.Raise();
        }

        /// <summary>
        /// UGS reporting on OnMiniGameEnd - the controller's rule-driven EndGame has
        /// already finalized the local Score (win time / DNF sentinel) by the time this
        /// fires, mirroring the Joust/CrystalCapture reporter pattern.
        /// </summary>
        void ReportStats()
        {
            if (!ugsStatsManager || gameData.LocalRoundStats == null) return;

            ugsStatsManager.ReportBlitzStats(
                GameModes.WildlifeBlitz,
                gameData.SelectedIntensity.Value,
                _crystals,
                _kills,
                (int)gameData.LocalRoundStats.Score
            );

            if (gameData.LocalPlayer?.Vessel is Component vc
                && vc.TryGetComponent<VesselTelemetry>(out var vt))
            {
                ugsStatsManager.ReportVesselTelemetry(
                    vt, gameData.LocalPlayer.Vessel.VesselStatus.VesselType.ToString());
            }
        }
    }
}
