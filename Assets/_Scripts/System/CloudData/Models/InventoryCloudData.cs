using System;
using CosmicShore.Data;

namespace CosmicShore.Core
{
    /// <summary>
    /// The account's unique pilots and artifacts (Docs/Pilots/INVENTORY.md §4). Cloud key: "ACCOUNT_INVENTORY".
    ///
    /// <para>Schema 1 (2026-11-07) is the first. Before it, pilots were callsigns with nothing to save and the device kept
    /// the Hangar seats, slots and artifact track levels in PlayerPrefs; <c>AccountInventoryStore</c> folds those in once
    /// per device (<see cref="AccountInventory.DeviceImportDone"/>).</para>
    /// </summary>
    [Serializable]
    public class InventoryCloudData
    {
        public const int CurrentSchemaVersion = 1;

        public int SchemaVersion = CurrentSchemaVersion;
        public AccountInventory Inventory = new();
    }
}
