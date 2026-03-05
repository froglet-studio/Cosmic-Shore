using CosmicShore.Gameplay;

namespace CosmicShore.App.Systems.CloudData
{
    /// <summary>
    /// Repository for per-vessel lifetime telemetry stats.
    /// Cloud key: "VESSEL_STATS"
    /// </summary>
    public sealed class VesselStatsRepository : CloudDataRepository<VesselStatsCloudData>
    {
        public override string CloudKey => UGSKeys.VesselStats;

        public VesselStatsRepository(ICloudSaveProvider provider) : base(provider, 2f) { }
    }
}
