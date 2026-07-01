namespace CosmicShore.Core
{
    /// <summary>
    /// Repository for First-Time-User-Experience completion/resume state.
    /// Cloud key: "FTUE_PROGRESS".
    /// </summary>
    public sealed class FTUEProgressRepository : CloudDataRepository<FTUECloudData>
    {
        public override string CloudKey => UGSKeys.Ftue;

        public FTUEProgressRepository(ICloudSaveProvider provider) : base(provider) { }
    }
}
