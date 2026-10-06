using System.Collections.Generic;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using Reflex.Attributes;
using UnityEngine;
using CosmicShore.UI;
namespace CosmicShore.Gameplay
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
        [Inject] protected GameDataSO gameData;

        [Header("Stat Events - Flight (all vessels)")]
        [SerializeField] private VesselStatEventSO longestDriftStat;
        [SerializeField] private VesselStatEventSO maxBoostTimeStat;

        [Header("Stat Events - Combat (all vessels)")]
        [SerializeField] private VesselStatEventSO prismsDamagedStat;

        [Header("Tracking Thresholds")]
        [Tooltip("Minimum BoostMultiplier required while IsBoosting for boost time to count toward Max Boost. " +
                 "Tune per vessel - e.g. Squirrel's ChargeBoost peaks at 2x, so a 4x threshold would never fire.")]
        [SerializeField] private float boostMultiplierThreshold = 1.5f;

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

        // ── Runtime injection (used by VesselTelemetryBootstrapper) ────────────

        /// <summary>
        /// Sets gameData when the component is added at runtime via AddComponent.
        /// Must be called before OnEnable fires.
        /// </summary>
        public void InjectGameData(GameDataSO data) => gameData = data;

        // ── Private accumulators ───────────────────────────────────────────────

        private float _currentDriftTime;
        private float _currentBoostTime;
        private bool  _subscribed;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            RegisterStat(longestDriftStat);
            RegisterStat(maxBoostTimeStat);
            RegisterStat(prismsDamagedStat);
            RegisterStatsExtended();

            if (CSDebug.IsVerbose(CSLogChannel.VesselTelemetry))
                CSDebug.LogVerbose(CSLogChannel.VesselTelemetry, $"[VesselTelemetry] {GetType().Name} Awake - " +
                $"registered {_allStats.Count} stat(s), " +
                $"gameData={(gameData != null ? "OK" : "NULL")}, " +
                $"drift={(longestDriftStat != null ? "OK" : "NULL")}, " +
                $"boost={(maxBoostTimeStat != null ? "OK" : "NULL")}, " +
                $"prismsDmg={(prismsDamagedStat != null ? "OK" : "NULL")}");
        }

        protected virtual void OnEnable()
        {
            TrySubscribe();
            // Re-enabled mid-turn (deactivated and reactivated while tracking): the turn's
            // hooks were released at OnDisable, so take them again.
            if (IsTracking) AttachTurnHooks();
        }

        private void Start() => TrySubscribe();

        private void TrySubscribe()
        {
            // Vessels are instantiated at runtime, so OnEnable can fire before Reflex
            // injection populates gameData. Retry in Start, which always runs after injection.
            if (_subscribed || gameData == null) return;

            gameData.OnMiniGameTurnStarted.OnRaised += HandleTurnStarted;
            gameData.OnMiniGameTurnEnd.OnRaised     += HandleTurnEnded;
            VesselDamagePrismEffectSO.OnVesselDamagedPrism += HandlePrismDamaged;
            _subscribed = true;
        }

        protected virtual void OnDisable()
        {
            // A turn that never ends (a scene reload mid-match, a return to the menu, the AI
            // training watchdog) never raises OnMiniGameTurnEnd, and the turn's hooks are
            // static events and persistent SOAP assets: left attached they keep this
            // destroyed component — and through Vessel its vessel and whole trail — alive for
            // the rest of the session. Release them here as well as at turn end.
            DetachTurnHooks();

            if (!_subscribed) return;

            if (gameData != null)
            {
                gameData.OnMiniGameTurnStarted.OnRaised -= HandleTurnStarted;
                gameData.OnMiniGameTurnEnd.OnRaised     -= HandleTurnEnded;
            }
            VesselDamagePrismEffectSO.OnVesselDamagedPrism -= HandlePrismDamaged;
            _subscribed = false;
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
            DetachTurnHooks(); // the previous turn never ended: never subscribe twice
            ResetAll();

            Vessel = gameData.LocalPlayer?.Vessel?.VesselStatus;

            if (Vessel == null || !Vessel.IsLocalUser)
            {
                Debug.LogWarning($"[VesselTelemetry] {GetType().Name} HandleTurnStarted - " +
                    $"NOT tracking (Vessel={(Vessel != null ? Vessel.VesselType.ToString() : "NULL")}, " +
                    $"IsLocal={Vessel?.IsLocalUser})");
                IsTracking = false;
                return;
            }

            IsTracking = true;
            if (CSDebug.IsVerbose(CSLogChannel.VesselTelemetry))
                CSDebug.LogVerbose(CSLogChannel.VesselTelemetry, $"[VesselTelemetry] {GetType().Name} HandleTurnStarted - " +
                $"tracking {Vessel.VesselType} for player '{Vessel.PlayerName}', " +
                $"{_allStats.Count} stat(s) registered");
            AttachTurnHooks();
        }

        private void HandleTurnEnded()
        {
            FinalizeInProgressDrift();
            FinalizeInProgressBoost();
            IsTracking = false;
            DetachTurnHooks();
            if (CSDebug.IsVerbose(CSLogChannel.VesselTelemetry))
                CSDebug.LogVerbose(CSLogChannel.VesselTelemetry, $"[VesselTelemetry] {GetType().Name} HandleTurnEnded - " +
                $"drift={MaxDriftTime:F2}s, boost={MaxBoostTime:F2}s, prismsDmg={PrismsDamaged}");
        }

        // Whether OnTurnStartedExtended's subscriptions are live. The base owns the pairing so
        // a subclass can neither leak them (turn end never raised) nor double them (turn start
        // raised twice).
        bool _turnHooksAttached;

        void AttachTurnHooks()
        {
            if (_turnHooksAttached) return;
            _turnHooksAttached = true;
            OnTurnStartedExtended();
        }

        void DetachTurnHooks()
        {
            if (!_turnHooksAttached) return;
            _turnHooksAttached = false;
            OnTurnEndedExtended();
        }

        // ── Extension points ───────────────────────────────────────────────────
        // OnTurnStartedExtended subscribes to the turn's events; OnTurnEndedExtended must ONLY
        // undo exactly that. It also runs when the component is disabled or destroyed mid-turn,
        // so it must not finalize or raise stats. The base guarantees the two alternate.

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
            bool isHighBoost = Vessel.IsBoosting && Vessel.BoostMultiplier >= boostMultiplierThreshold;
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