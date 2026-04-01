using CosmicShore.Core;
using CosmicShore.Game.UI;
using CosmicShore.Utilities;
using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.Game
{
    /// <summary>
    /// Vessel-specific telemetry for the Squirrel.
    /// Adds on top of VesselTelemetry base (drift, boost, prisms damaged):
    ///   - Max clean crystal streak
    ///   - Jousts won
    ///   - Prisms stolen (via SkimmerStealPrismEffectSO)
    ///
    /// Crystal collection now listens to OmniCrystalImpactor.OnCrystalCollected
    /// (the SOAP ScriptableEventCrystalStats) rather than the old ElementalCrystalImpactor event.
    /// </summary>
    public class SquirrelVesselTelemetry : VesselTelemetry
    {
        [Header("Stat Events — Squirrel")]
        [SerializeField] private VesselStatEventSO maxCleanStreakStat;
        [SerializeField] private VesselStatEventSO joustsWonStat;
        [SerializeField] private VesselStatEventSO prismsStolen;

        [Header("SOAP Events")]
        [SerializeField] private ScriptableEventCrystalStats onCrystalCollected;
        [SerializeField] private ScriptableEventString onJoustCollisionEvent;

        // ── Public records ─────────────────────────────────────────────────────

        public int MaxCleanStreak { get; private set; }
        public int JoustsWon      { get; private set; }
        public int PrismsStolen   { get; private set; }

        private int _currentCleanStreak;

        // ── Registration ───────────────────────────────────────────────────────

        protected override void RegisterStatsExtended()
        {
            RegisterStat(maxCleanStreakStat);
            RegisterStat(joustsWonStat);
            RegisterStat(prismsStolen);
        }

        // ── Turn lifecycle ─────────────────────────────────────────────────────

        protected override void OnTurnStartedExtended()
        {
            if (onCrystalCollected) onCrystalCollected.OnRaised     += HandleCrystalCollected;
            if (onJoustCollisionEvent) onJoustCollisionEvent.OnRaised += HandleJoustEvent;

            SkimmerStealPrismEffectSO.OnSkimmerStolenPrism += HandlePrismStolen;
        }

        protected override void OnTurnEndedExtended()
        {
            if (onCrystalCollected) onCrystalCollected.OnRaised     -= HandleCrystalCollected;
            if (onJoustCollisionEvent) onJoustCollisionEvent.OnRaised -= HandleJoustEvent;

            SkimmerStealPrismEffectSO.OnSkimmerStolenPrism -= HandlePrismStolen;
        }

        protected override void ResetExtended()
        {
            MaxCleanStreak      = 0;
            JoustsWon           = 0;
            PrismsStolen        = 0;
            _currentCleanStreak = 0;

            maxCleanStreakStat?.Reset();
            joustsWonStat?.Reset();
            prismsStolen?.Reset();
        }

        // ── Event handlers ─────────────────────────────────────────────────────

        private void HandleCrystalCollected(CrystalStats stats)
        {
            if (!IsTracking || Vessel?.PlayerName != stats.PlayerName) return;

            _currentCleanStreak++;
            if (_currentCleanStreak > MaxCleanStreak)
            {
                MaxCleanStreak = _currentCleanStreak;
                maxCleanStreakStat?.Raise(MaxCleanStreak);
            }
        }

        private void HandlePrismStolen(string playerName)
        {
            if (!IsTracking || Vessel?.PlayerName != playerName) return;
            PrismsStolen++;
            prismsStolen?.Raise(PrismsStolen);
        }

        private void HandleJoustEvent(string winner)
        {
            if (!IsTracking || Vessel?.PlayerName != winner) return;
            JoustsWon++;
            joustsWonStat?.Raise(JoustsWon);
        }
    }
}