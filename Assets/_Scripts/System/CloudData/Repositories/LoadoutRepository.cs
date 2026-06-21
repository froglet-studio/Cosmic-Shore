using System.Collections.Generic;

namespace CosmicShore.Core
{
    /// <summary>
    /// Repository for the player's loadout slots and per-game configs.
    /// Cloud key: "LOADOUT_DATA"
    /// </summary>
    public sealed class LoadoutRepository : CloudDataRepository<LoadoutCloudData>
    {
        public override string CloudKey => UGSKeys.Loadout;

        public LoadoutRepository(ICloudSaveProvider provider) : base(provider) { }

        protected override void OnAfterLoad(LoadoutCloudData data)
        {
            data.PlayerLoadouts ??= new List<LoadoutEntry>();
            data.GameLoadouts ??= new List<GameLoadoutEntry>();
        }
    }
}
