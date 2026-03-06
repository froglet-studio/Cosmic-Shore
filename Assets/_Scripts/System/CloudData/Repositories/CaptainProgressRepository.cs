using System.Collections.Generic;
using CosmicShore.App.Systems.CloudData.Models;
using CosmicShore.Core;

namespace CosmicShore.App.Systems.CloudData
{
    /// <summary>
    /// Repository for captain XP, level, and unlock/encounter state.
    /// Cloud key: "CAPTAIN_PROGRESS"
    /// </summary>
    public sealed class CaptainProgressRepository : CloudDataRepository<CaptainProgressCloudData>
    {
        public override string CloudKey => UGSKeys.CaptainProgress;

        public CaptainProgressRepository(ICloudSaveProvider provider) : base(provider) { }

        protected override void OnAfterLoad(CaptainProgressCloudData data)
        {
            data.Captains ??= new Dictionary<string, CaptainState>();
        }
    }
}
