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

            // Records are keyed "{mode}:{intensity}" by the enum member NAME, so a mode rename
            // orphans them. Runs on every load: an old save can arrive at any time.
            GameModeRenameMigration.Migrate(data);
        }
    }
}
