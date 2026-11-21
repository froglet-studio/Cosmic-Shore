using System.Collections.Generic;

namespace CosmicShore.Core
{
    /// <summary>
    /// Repository for the microgame drill's account facts.
    /// Cloud key: "DRILL_PROGRESS".
    /// </summary>
    public sealed class DrillProgressRepository : CloudDataRepository<DrillProgressCloudData>
    {
        public override string CloudKey => UGSKeys.DrillProgress;

        public DrillProgressRepository(ICloudSaveProvider provider) : base(provider) { }

        protected override void OnAfterLoad(DrillProgressCloudData data)
        {
            data.SeenTipIds ??= new List<string>();
            data.BestLaps ??= new List<DrillBestLap>();
        }
    }
}
