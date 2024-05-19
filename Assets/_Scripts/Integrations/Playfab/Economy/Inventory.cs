using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CosmicShore.Integrations.PlayFab.Economy
{
    [Serializable]
    public class Inventory
    {
        // Crystals - including Omni Crystals and Elemental Crystals
        public List<VirtualItem> crystals = new();

        // Guides
        public List<VirtualItem> captains = new();
        
        // Guide Upgrades
        public List<VirtualItem> captainUpgrades = new();
        
        // Ships
        public List<VirtualItem> shipClasses = new();
        
        // Games
        public List<VirtualItem> games = new();

        public void SaveToDisk()
        {
            DataAccessor.Save("inventory.data", this);
        }

        public void LoadFromDisk()
        {
            var tempInventory = DataAccessor.Load<Inventory>("inventory.data");

            crystals = tempInventory.crystals;
            captains = tempInventory.captains;
            captainUpgrades = tempInventory.captainUpgrades;
            shipClasses = tempInventory.shipClasses;
            games = tempInventory.games;
        }

        public bool ContainsCaptain(string captainName)
        {
            return captains.Where(item => item.Name == captainName).Count() > 0;
        }

        public bool ContainsShipClass(string shipName)
        {
            foreach (var item in shipClasses)
            {
                Debug.LogWarning($"Ship Class Item {item.Name}");
            }
            var count = shipClasses.Where(item => item.Name == shipName).Count();
            Debug.LogWarning($"ContainsShipClass {shipName}, Count: {count}");
            return count > 0;
        }
    }
}