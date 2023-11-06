using System;
using System.Collections.Generic;

namespace _Scripts._Core.Playfab_Models.Economy
{
    [Serializable]
    public class InventoryModel
    {
        // Vessels
        public List<VirtualItemModel> Vessels;
        // Ships
        public List<VirtualItemModel> Ships;
        // MiniGames
        public List<VirtualItemModel> MiniGames;
        // Shards already has Quantity, 
        public List<VirtualItemModel> VesselShards;
    }
}