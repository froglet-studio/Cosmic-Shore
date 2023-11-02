using System;
using System.Collections.Generic;

namespace _Scripts._Core.Playfab_Models.Economy
{
    [Serializable]
    public class InventoryModel
    {
        public List<VirtualItemModel> Vessels;
        public List<VirtualItemModel> Ships;
        public List<VirtualItemModel> Games;
        public List<VirtualItemModel> Shards;
    }
}