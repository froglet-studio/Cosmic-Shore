using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
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
    ///
    /// Only the telemetry on the local pilot's own vessel counts, and every value it raises is also reported to that
    /// pilot's <see cref="RoundStats"/>, which the server shares with every peer - so each scoreboard shows every
    /// pilot's numbers, not only its own (Docs/ScoringSystem/BUGS.md B21).
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
            FillMissingBaseStats();
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

        /// <summary>
        /// A telemetry added at runtime (<see cref="VesselTelemetryBootstrapper"/>: the Dolphin, Manta, Rhino, Serpent and
        /// Swordfish) has no serialized links, so it counted and never raised - those pilots read 0 on every scoreboard.
        /// The three base stats are the same assets for every hull; take any missing one from the shared profile.
        /// </summary>
        void FillMissingBaseStats()
        {
            if (longestDriftStat && maxBoostTimeStat && prismsDamagedStat) return;
            var profile = GameModeStatsProfileSO.Load();
            if (!profile) return;
            if (!longestDriftStat)  longestDriftStat  = profile.VesselLongestDrift;
            if (!maxBoostTimeStat)  maxBoostTimeStat  = profile.VesselMaxBoost;
            if (!prismsDamagedStat) prismsDamagedStat = profile.VesselPrismsDamaged;
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
            if (_unreported.Count > 0 && Time.unscaledTime >= _nextReportTime)
                ReportToRoundStats();
        }

        // ── Reporting (every pilot's numbers on every scoreboard) ──────────────

        /// <summary>How often a changed value goes to the server: a ram raises Prisms Damaged many times a second.</summary>
        const float ReportIntervalSeconds = 0.5f;

        readonly Dictionary<VesselStatEventSO, float> _unreported = new();
        float _nextReportTime;

        /// <summary>
        /// Raises a stat for this machine's listeners and queues it for the pilot's <see cref="RoundStats"/>.
        /// Use this, not <c>stat.Raise</c>, for every value a telemetry tracks.
        /// </summary>
        protected void Publish(VesselStatEventSO stat, float value)
        {
            if (stat == null) return;
            stat.Raise(value);
            _unreported[stat] = value;
        }

        void ReportToRoundStats()
        {
            _nextReportTime = Time.unscaledTime + ReportIntervalSeconds;
            if (_unreported.Count == 0) return;
            if (gameData?.LocalPlayer?.RoundStats is RoundStats roundStats)
                foreach (var kv in _unreported)
                    roundStats.ReportVesselStat(kv.Key.name, kv.Value);
            _unreported.Clear();
        }

        // ── Turn lifecycle ─────────────────────────────────────────────────────

        private void HandleTurnStarted()
        {
            DetachTurnHooks(); // the previous turn never ended: never subscribe twice
            ResetAll();

            Vessel = gameData.LocalPlayer?.Vessel?.VesselStatus;

            // Every hull carries a telemetry, and each one used to track the LOCAL pilot - so with four ships in the
            // arena the local pilot was counted four times over. Only the one on that pilot's own ship counts now.
            if (Vessel != null && !ReferenceEquals(Vessel, GetComponentInParent<IVesselStatus>()))
            {
                Vessel = null;
                IsTracking = false;
                return;
            }

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
            // The turn's last values go now, not at the next half second: the scoreboard follows the turn's end.
            if (IsTracking) ReportToRoundStats();
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
            Publish(prismsDamagedStat, PrismsDamaged);
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
                Publish(longestDriftStat, MaxDriftTime);
            }
            _currentDriftTime = 0f;
        }

        private void FinalizeInProgressBoost()
        {
            if (_currentBoostTime <= 0f) return;
            if (_currentBoostTime > MaxBoostTime)
            {
                MaxBoostTime = _currentBoostTime;
                Publish(maxBoostTimeStat, MaxBoostTime);
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
            _unreported.Clear();

            longestDriftStat?.Reset();
            maxBoostTimeStat?.Reset();
            prismsDamagedStat?.Reset();

            ResetExtended();
        }
    }
}