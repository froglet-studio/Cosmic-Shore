using CosmicShore.Game.UI;
using UnityEngine;

namespace CosmicShore.Game
{
    /// <summary>
    /// Vessel-specific telemetry for the Sparrow.
    /// Adds on top of VesselTelemetry base (drift, boost, prisms damaged):
    ///   - Prism blocks shot (volleys fired via FullAutoAction)
    ///   - Skyburst missiles shot (fired via FireGunAction)
    ///   - Danger blocks spawned (produced at top boost)
    /// </summary>
    public class SparrowVesselTelemetry : VesselTelemetry
    {
        [Header("Stat Events — Sparrow")]
        [SerializeField] private VesselStatEventSO prismBlocksShotStat;
        [SerializeField] private VesselStatEventSO skyburstMissilesShotStat;
        [SerializeField] private VesselStatEventSO dangerBlocksSpawnedStat;

        // ── Public records ─────────────────────────────────────────────────────

        public int PrismBlocksShot       { get; private set; }
        public int SkyburstMissilesShot  { get; private set; }
        public int DangerBlocksSpawned   { get; private set; }

        // ── Registration ───────────────────────────────────────────────────────

        protected override void RegisterStatsExtended()
        {
            RegisterStat(prismBlocksShotStat);
            RegisterStat(skyburstMissilesShotStat);
            RegisterStat(dangerBlocksSpawnedStat);
        }

        // ── Turn lifecycle ─────────────────────────────────────────────────────

        protected override void OnTurnStartedExtended()
        {
            FullAutoAction.OnVolleyFired += HandleVolleyFired;
            FireGunAction.OnShotFired    += HandleSkyburstFired;
            VesselDangerBlockFormationBySkimmerEffectSO.OnDangerBlockSpawned += HandleDangerBlockSpawned;
        }

        protected override void OnTurnEndedExtended()
        {
            FullAutoAction.OnVolleyFired -= HandleVolleyFired;
            FireGunAction.OnShotFired    -= HandleSkyburstFired;
            VesselDangerBlockFormationBySkimmerEffectSO.OnDangerBlockSpawned -= HandleDangerBlockSpawned;
        }

        protected override void ResetExtended()
        {
            PrismBlocksShot      = 0;
            SkyburstMissilesShot = 0;
            DangerBlocksSpawned  = 0;

            prismBlocksShotStat?.Reset();
            skyburstMissilesShotStat?.Reset();
            dangerBlocksSpawnedStat?.Reset();
        }

        // ── Event handlers ─────────────────────────────────────────────────────

        private void HandleVolleyFired(string playerName)
        {
            if (!IsTracking || Vessel?.PlayerName != playerName) return;
            PrismBlocksShot++;
            prismBlocksShotStat?.Raise(PrismBlocksShot);
        }

        private void HandleSkyburstFired(string playerName)
        {
            if (!IsTracking || Vessel?.PlayerName != playerName) return;
            SkyburstMissilesShot++;
            skyburstMissilesShotStat?.Raise(SkyburstMissilesShot);
        }

        private void HandleDangerBlockSpawned(string playerName)
        {
            if (!IsTracking || Vessel?.PlayerName != playerName) return;
            DangerBlocksSpawned++;
            dangerBlocksSpawnedStat?.Raise(DangerBlocksSpawned);
        }
    }
}
