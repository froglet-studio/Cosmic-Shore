namespace CosmicShore.Game
{
    /// <summary>
    /// Vessel-specific telemetry for the Dolphin.
    /// Adds on top of VesselTelemetry base (drift, boost, prisms damaged):
    ///   - Explosions triggered (crystal collisions that spawned AOE)
    ///   - Total hostile volume destroyed
    ///
    /// Dolphin's core loop: thread gaps to build charge → drift into crystal → AOE explosion → destroy structures.
    /// </summary>
    public class DolphinVesselTelemetry : VesselTelemetry
    {
        public int ExplosionsTriggered   { get; private set; }
        public float VolumeDestroyed     { get; private set; }

        protected override void ResetExtended()
        {
            ExplosionsTriggered = 0;
            VolumeDestroyed     = 0f;
        }

        public void RecordExplosion()
        {
            if (!IsTracking) return;
            ExplosionsTriggered++;
        }

        public void RecordVolumeDestroyed(float volume)
        {
            if (!IsTracking) return;
            VolumeDestroyed += volume;
        }
    }
}
