using System.Collections.Generic;
using CosmicShore.Game.UI;
using CosmicShore.Soap;
using UnityEngine;

namespace CosmicShore.Game
{
    /// <summary>
    /// Abstract base for all vessel telemetry.
    /// Tracks stats universal to every vessel and game mode:
    ///   - Longest drift
    ///   - Max boost time
    ///   - Prisms damaged (via VesselDamagePrismEffectSO)
    ///
    /// Subclass per vessel type to add vessel-specific stats.
    /// </summary>
    public abstract class VesselTelemetry : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] protected GameDataSO gameData;

        [Header("Stat Events — Flight (all vessels)")]
        [SerializeField] private VesselStatEventSO longestDriftStat;
        [SerializeField] private VesselStatEventSO maxBoostTimeStat;

        [Header("Stat Events — Combat (all vessels)")]
        [SerializeField] private VesselStatEventSO prismsDamagedStat;

        // ── Public records ─────────────────────────────────────────────────────

        public float MaxDriftTime    { get; private set; }
        public float MaxBoostTime    { get; private set; }
        public int   PrismsDamaged   { get; private set; }

        // ── Protected access for subclasses ───────────────────────────────────

        protected IVesselStatus Vessel     { get; private set; }
        protected bool          IsTracking { get; private set; }

        // ── Stat registry ──────────────────────────────────────────────────────

        private readonly List<VesselStatEventSO> _allStats = new();

        public IReadOnlyList<VesselStatEventSO> GetAllStats() => _allStats;

        protected void RegisterStat(VesselStatEventSO stat)
        {
            if (stat != null) _allStats.Add(stat);
        }

        // ── Private accumulators ───────────────────────────────────────────────

        private float _currentDriftTime;
        private float _currentBoostTime;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            RegisterStat(longestDriftStat);
            RegisterStat(maxBoostTimeStat);
            RegisterStat(prismsDamagedStat);
            RegisterStatsExtended();
        }

        protected virtual void OnEnable()
        {
            if (gameData == null) return;
            gameData.OnMiniGameTurnStarted.OnRaised += HandleTurnStarted;
            gameData.OnMiniGameTurnEnd.OnRaised     += HandleTurnEnded;

            VesselDamagePrismEffectSO.OnVesselDamagedPrism += HandlePrismDamaged;
        }

        protected virtual void OnDisable()
        {
            if (gameData == null) return;
            gameData.OnMiniGameTurnStarted.OnRaised -= HandleTurnStarted;
            gameData.OnMiniGameTurnEnd.OnRaised     -= HandleTurnEnded;

            VesselDamagePrismEffectSO.OnVesselDamagedPrism -= HandlePrismDamaged;
        }

        private void Update()
        {
            if (!IsTracking || Vessel == null) return;
            TrackDrift();
            TrackBoost();
            OnUpdateExtended();
        }

        // ── Turn lifecycle ─────────────────────────────────────────────────────

        private void HandleTurnStarted()
        {
            ResetAll();

            Vessel = gameData.LocalPlayer?.Vessel?.VesselStatus;

            if (Vessel == null || !Vessel.IsLocalUser)
            {
                IsTracking = false;
                return;
            }

            IsTracking = true;
            OnTurnStartedExtended();
        }

        private void HandleTurnEnded()
        {
            FinalizeInProgressDrift();
            FinalizeInProgressBoost();
            IsTracking = false;
            OnTurnEndedExtended();
        }

        // ── Extension points ───────────────────────────────────────────────────

        protected virtual void RegisterStatsExtended() { }
        protected virtual void OnTurnStartedExtended() { }
        protected virtual void OnTurnEndedExtended()   { }
        protected virtual void OnUpdateExtended()      { }
        protected virtual void ResetExtended()         { }

        // ── Event handlers ─────────────────────────────────────────────────────

        private void HandlePrismDamaged(string playerName)
        {
            if (!IsTracking || Vessel?.PlayerName != playerName) return;
            PrismsDamaged++;
            prismsDamagedStat?.Raise(PrismsDamaged);
        }

        // ── Frame tracking ─────────────────────────────────────────────────────

        private void TrackDrift()
        {
            if (Vessel.IsDrifting)
                _currentDriftTime += Time.deltaTime;
            else
                FinalizeInProgressDrift();
        }

        private void TrackBoost()
        {
            bool isHighBoost = Vessel.IsBoosting && Vessel.BoostMultiplier >= 4.0f;
            if (isHighBoost)
                _currentBoostTime += Time.deltaTime;
            else
                FinalizeInProgressBoost();
        }

        private void FinalizeInProgressDrift()
        {
            if (_currentDriftTime <= 0f) return;
            if (_currentDriftTime > MaxDriftTime)
            {
                MaxDriftTime = _currentDriftTime;
                longestDriftStat?.Raise(MaxDriftTime);
            }
            _currentDriftTime = 0f;
        }

        private void FinalizeInProgressBoost()
        {
            if (_currentBoostTime <= 0f) return;
            if (_currentBoostTime > MaxBoostTime)
            {
                MaxBoostTime = _currentBoostTime;
                maxBoostTimeStat?.Raise(MaxBoostTime);
            }
            _currentBoostTime = 0f;
        }

        // ── Reset ──────────────────────────────────────────────────────────────

        private void ResetAll()
        {
            MaxDriftTime      = 0f;
            MaxBoostTime      = 0f;
            PrismsDamaged     = 0;
            _currentDriftTime = 0f;
            _currentBoostTime = 0f;
            IsTracking        = false;
            Vessel            = null;

            longestDriftStat?.Reset();
            maxBoostTimeStat?.Reset();
            prismsDamagedStat?.Reset();

            ResetExtended();
        }
    }
}