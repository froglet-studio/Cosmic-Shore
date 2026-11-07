using CosmicShore.Data;

namespace CosmicShore.Core
{
    /// <summary>
    /// Repository for the account's unique pilots and artifacts (Docs/Pilots/INVENTORY.md §4).
    /// Cloud key: "ACCOUNT_INVENTORY". Writes go through <c>AccountInventoryStore</c>, which marks this dirty.
    /// </summary>
    public sealed class InventoryRepository : CloudDataRepository<InventoryCloudData>
    {
        public override string CloudKey => UGSKeys.Inventory;

        public InventoryRepository(ICloudSaveProvider provider) : base(provider) { }

        protected override void OnAfterLoad(InventoryCloudData data)
        {
            data.Inventory ??= new AccountInventory();
            data.Inventory.Normalize();
            data.SchemaVersion = InventoryCloudData.CurrentSchemaVersion;
        }
    }
}
