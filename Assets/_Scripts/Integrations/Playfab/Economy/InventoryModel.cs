using System;
using System.Collections.Generic;

namespace CosmicShore._Core.Playfab.Economy
{
    [Serializable]
    public class InventoryModel
    {
        // Vessels
        public List<VirtualItemModel> Vessels;
        // Vessel Upgrades
        public List<VirtualItemModel> VesselUpgrades;
        // Ships
        public List<VirtualItemModel> Ships;
        // MiniGames
        public List<VirtualItemModel> MiniGames;
        // Shards already has Quantity, 
        public List<VirtualItemModel> VesselShards;
    }
}