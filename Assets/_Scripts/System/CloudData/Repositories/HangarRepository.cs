using System.Collections.Generic;

namespace CosmicShore.Core
{
    /// <summary>
    /// Repository for vessel ownership, selection, and lifetime per-vessel stats.
    /// Cloud key: "HANGAR_DATA"
    ///
    /// Debounce is 2s (was 1.5s) because this key now also absorbs the per-game-end vessel
    /// telemetry writes that used to land on VESSEL_STATS.
    /// </summary>
    public sealed class HangarRepository : CloudDataRepository<HangarCloudData>
    {
        public override string CloudKey => UGSKeys.HangarData;

        public HangarRepository(ICloudSaveProvider provider) : base(provider, 2f) { }

        protected override void OnAfterLoad(HangarCloudData data)
        {
            data.Vessels ??= new Dictionary<string, VesselRecord>();

            foreach (var record in data.Vessels.Values)
                if (record != null)
                    record.Counters ??= new Dictionary<string, int>();
        }
    }
}
