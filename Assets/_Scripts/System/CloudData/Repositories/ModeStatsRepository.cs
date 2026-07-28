using System.Collections.Generic;

namespace CosmicShore.Core
{
    /// <summary>
    /// Repository for per-game-mode, per-intensity play records.
    /// Cloud key: "MODE_STATS"
    /// </summary>
    public sealed class ModeStatsRepository : CloudDataRepository<ModeStatsCloudData>
    {
        public override string CloudKey => UGSKeys.ModeStats;

        public ModeStatsRepository(ICloudSaveProvider provider) : base(provider, 2f) { }

        protected override void OnAfterLoad(ModeStatsCloudData data)
        {
            data.Modes ??= new Dictionary<string, ModeRecord>();
        }
    }
}
