using CosmicShore.Data;

namespace CosmicShore.Core
{
    /// <summary>
    /// Repository for the player's saved squads.
    /// Cloud key: "SQUAD_DATA"
    /// </summary>
    public sealed class SquadRepository : CloudDataRepository<SquadCloudData>
    {
        public override string CloudKey => UGSKeys.Squad;

        public SquadRepository(ICloudSaveProvider provider) : base(provider) { }

        protected override void OnAfterLoad(SquadCloudData data)
        {
            data.Book ??= new SquadBook();
            data.Book.Normalize();
            data.SchemaVersion = SquadCloudData.CurrentSchemaVersion;
        }
    }
}
