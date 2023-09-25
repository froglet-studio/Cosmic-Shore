using System.Collections.Generic;
using PlayFab.EconomyModels;

namespace _Scripts._Core.Playfab_Models
{
    /// <summary>
    /// Player Inventory
    /// Player inventory wrapper
    /// Has a dictionary of catalog items and inventory items
    /// </summary>
    public class PlayerInventory
    {
        // A dictionary of catalog items
        public List<CatalogItem> CatalogItems { get; set; }
        // A dictionary of inventory items and their quantity
        public List<InventoryItem> InventoryItems { get; set; }
    }
}