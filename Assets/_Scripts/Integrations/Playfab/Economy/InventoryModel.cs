using System;
using System.Collections.Generic;

namespace CosmicShore.Integrations.Playfab.Economy
{
    [Serializable]
    public class InventoryModel
    {
        // Vessels
        public List<VirtualItem> Vessels;
        // Vessel Upgrades
        public List<VirtualItem> VesselUpgrades;
        // Ships
        public List<VirtualItem> Ships;
        // MiniGames
        public List<VirtualItem> MiniGames;
        // Shards already has Quantity, 
        public List<VirtualItem> VesselShards;
    }
}