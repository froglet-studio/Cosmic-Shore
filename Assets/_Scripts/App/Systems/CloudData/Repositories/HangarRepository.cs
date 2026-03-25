using System.Collections.Generic;
using CosmicShore.App.Systems.CloudData.Models;

namespace CosmicShore.App.Systems.CloudData
{
    /// <summary>
    /// Repository for vessel unlock state and hangar preferences.
    /// Cloud key: "HANGAR_DATA"
    /// </summary>
    public sealed class HangarRepository : CloudDataRepository<HangarCloudData>
    {
        public override string CloudKey => UGSKeys.HangarData;

        public HangarRepository(ICloudSaveProvider provider) : base(provider) { }

        protected override void OnAfterLoad(HangarCloudData data)
        {
            data.UnlockedVessels ??= new List<string>();
            data.VesselPreferences ??= new Dictionary<string, VesselPreference>();
        }
    }
}
