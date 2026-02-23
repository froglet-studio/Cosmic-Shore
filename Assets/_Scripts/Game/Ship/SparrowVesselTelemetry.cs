using CosmicShore.Game.UI;
using UnityEngine;

namespace CosmicShore.Game
{
    /// <summary>
    /// Vessel-specific telemetry for the Sparrow.
    /// Adds on top of VesselTelemetry base (drift, boost, prisms damaged):
    ///   - Prism blocks shot (block prisms spawned via FullAutoBlockShootActionExecutor)
    ///   - Skyburst missiles shot (fired via FireGunActionExecutor)
    ///   - Danger blocks spawned (trail blocks created during overheat danger mode)
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
            Debug.Log($"[SparrowTelemetry] RegisterStats — " +
                $"prismBlocks={(prismBlocksShotStat != null ? "OK" : "NULL")}, " +
                $"skyburst={(skyburstMissilesShotStat != null ? "OK" : "NULL")}, " +
                $"dangerBlocks={(dangerBlocksSpawnedStat != null ? "OK" : "NULL")}");
            RegisterStat(prismBlocksShotStat);
            RegisterStat(skyburstMissilesShotStat);
            RegisterStat(dangerBlocksSpawnedStat);
        }

        // ── Turn lifecycle ─────────────────────────────────────────────────────

        protected override void OnTurnStartedExtended()
        {
            FullAutoBlockShootActionExecutor.OnBlockShot += HandleBlockShot;
            FireGunActionExecutor.OnShotFired            += HandleSkyburstFired;
            VesselPrismController.OnDangerBlockCreated   += HandleDangerBlockSpawned;
            Debug.Log("[SparrowTelemetry] Turn started — subscribed to BlockShot, ShotFired, DangerBlockCreated");
        }

        protected override void OnTurnEndedExtended()
        {
            FullAutoBlockShootActionExecutor.OnBlockShot -= HandleBlockShot;
            FireGunActionExecutor.OnShotFired            -= HandleSkyburstFired;
            VesselPrismController.OnDangerBlockCreated   -= HandleDangerBlockSpawned;
            Debug.Log($"[SparrowTelemetry] Turn ended — prismBlocks={PrismBlocksShot}, " +
                $"skyburst={SkyburstMissilesShot}, dangerBlocks={DangerBlocksSpawned}");
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

        private void HandleBlockShot(string playerName)
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
