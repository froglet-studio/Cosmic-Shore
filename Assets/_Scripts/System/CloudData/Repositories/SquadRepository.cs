namespace CosmicShore.Core
{
    /// <summary>
    /// Repository for the player's squad configuration.
    /// Cloud key: "SQUAD_DATA"
    /// </summary>
    public sealed class SquadRepository : CloudDataRepository<SquadCloudData>
    {
        public override string CloudKey => UGSKeys.Squad;

        public SquadRepository(ICloudSaveProvider provider) : base(provider) { }
    }
}
