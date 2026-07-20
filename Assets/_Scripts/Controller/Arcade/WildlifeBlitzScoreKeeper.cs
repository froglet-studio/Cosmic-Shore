using System.Collections.Generic;
using Obvious.Soap;
using Reflex.Attributes;
using Unity.Netcode;
using UnityEngine;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Wildlife Blitz score keeper - SERVER-AUTHORITATIVE. Every peer's simulation raises
    /// the local kill/crystal events, but only the server's counts (the NucleusRush
    /// precedent): the server attributes each event to its player by name and recomputes
    /// that player's Score absolutely as
    /// hostileVolume + kills*killPoints + crystals*crystalPoints (the legacy weights:
    /// volume x1, kills x5, crystals x20). Score replicates to every peer via n_Score,
    /// driving the centerline HUD; the TEAM sum is the objective the rule watches.
    /// UGS stats report on OnMiniGameEnd, AFTER the controller's rule-driven end
    /// finalized the Score (clear time / DNF), on every peer for its own local player.
    /// B15 (Docs/ScoringSystem/BUGS.md): per-stats subscriptions detach off the keeper's
    /// own record list, never gameData.RoundStatsList, with an OnDisable safety net.
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

        readonly Dictionary<string, int> _kills = new();
        readonly Dictionary<string, int> _crystals = new();
        readonly List<IRoundStats> _subscribedStats = new();
        bool _isTracking;
        bool _gameEventsSubscribed;

        /// <summary>Team total lifeforms killed this game (scoreboard stats + UGS).</summary>
        public int TotalLifeFormsKilled
        {
            get { int t = 0; foreach (var v in _kills.Values) t += v; return t; }
        }

        /// <summary>Team total elemental crystals collected this game (scoreboard stats + UGS).</summary>
        public int TotalCrystalsCollected
        {
            get { int t = 0; foreach (var v in _crystals.Values) t += v; return t; }
        }

        // Reflex populates [Inject] fields after Awake/OnEnable on scene load, so the
        // OnEnable attempt can see a null gameData - retry in Start with a dedup guard
        // (the Cell.cs deferred-subscription precedent).
        void OnEnable() => TrySubscribeGameEvents();
        void Start() => TrySubscribeGameEvents();

        void TrySubscribeGameEvents()
        {
            if (_gameEventsSubscribed || gameData == null) return;
            gameData.OnMiniGameEnd.OnRaised += ReportStats;
            gameData.OnResetForReplay.OnRaised += ResetScores;
            _gameEventsSubscribed = true;
        }

        void OnDisable()
        {
            if (_gameEventsSubscribed && gameData != null)
            {
                gameData.OnMiniGameEnd.OnRaised -= ReportStats;
                gameData.OnResetForReplay.OnRaised -= ResetScores;
            }
            _gameEventsSubscribed = false;
            StopTracking();
        }

        static bool IsServerPeer => NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

        public void StartTracking()
        {
            if (_isTracking) return;

            // Every peer counts its own simulation's kill/crystal events (display totals +
            // its own UGS report - the fidelity the solo version had). Only the SERVER
            // writes Score: a spawned RoundStats suppresses local raises anyway, so only
            // server writes reach the HUDs (via n_Score replication) and the objective.
            LifeForm.OnLifeFormDeath += OnLifeFormKilled;
            ElementalCrystalImpactor.OnCrystalCollected += OnCrystalCollected;

            if (IsServerPeer)
            {
                foreach (var stats in gameData.RoundStatsList)
                {
                    if (stats == null || _subscribedStats.Contains(stats)) continue;
                    stats.OnHostileVolumeDestroyedChanged += OnHostileVolumeChanged;
                    _subscribedStats.Add(stats);
                }
            }

            _isTracking = true;
            CSDebug.Log($"[WildlifeBlitzScoreKeeper] Started Tracking (server={IsServerPeer})");
        }

        public void StopTracking()
        {
            if (!_isTracking) return;

            LifeForm.OnLifeFormDeath -= OnLifeFormKilled;
            ElementalCrystalImpactor.OnCrystalCollected -= OnCrystalCollected;

            foreach (var stats in _subscribedStats)
            {
                if (stats == null) continue;
                stats.OnHostileVolumeDestroyedChanged -= OnHostileVolumeChanged;
            }
            _subscribedStats.Clear();

            _isTracking = false;
            CSDebug.Log("[WildlifeBlitzScoreKeeper] Stopped Tracking");
        }

        void OnLifeFormKilled(string playerName, int cellId)
        {
            _kills[playerName] = _kills.GetValueOrDefault(playerName) + 1;
            RecomputeScore(playerName);
        }

        void OnCrystalCollected(string playerName)
        {
            _crystals[playerName] = _crystals.GetValueOrDefault(playerName) + 1;
            RecomputeScore(playerName);
        }

        void OnHostileVolumeChanged(IRoundStats stats) => RecomputeScore(stats);

        void RecomputeScore(string playerName)
        {
            foreach (var stats in gameData.RoundStatsList)
            {
                if (stats != null && stats.Name == playerName)
                {
                    RecomputeScore(stats);
                    return;
                }
            }
        }

        /// <summary>
        /// Absolute per-player recompute - idempotent; the composite always reflects the
        /// player's current stats + attributed kills/crystals. Server-only write.
        /// </summary>
        void RecomputeScore(IRoundStats stats)
        {
            if (!IsServerPeer) return;
            stats.Score = stats.HostileVolumeDestroyed
                          + _kills.GetValueOrDefault(stats.Name) * killPoints
                          + _crystals.GetValueOrDefault(stats.Name) * crystalPoints;
            if (eventOnScoreChanged) eventOnScoreChanged.Raise();
        }

        public void ResetScores()
        {
            _kills.Clear();
            _crystals.Clear();
            if (IsServerPeer)
            {
                foreach (var stats in gameData.RoundStatsList)
                    if (stats != null) stats.Score = 0;
            }
            if (eventOnScoreChanged) eventOnScoreChanged.Raise();
        }

        /// <summary>
        /// UGS reporting on OnMiniGameEnd - the controller's rule-driven end has already
        /// finalized every Score (clear time / DNF) by the time this fires. Runs on every
        /// peer for its own local player (mirroring the Joust/CC reporter pattern).
        /// </summary>
        void ReportStats()
        {
            if (!ugsStatsManager || gameData.LocalRoundStats == null) return;

            ugsStatsManager.ReportBlitzStats(
                GameModes.WildlifeBlitz,
                gameData.SelectedIntensity.Value,
                TotalCrystalsCollected,
                TotalLifeFormsKilled,
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
