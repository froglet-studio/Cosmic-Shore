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

        // Captains
        public List<VirtualItem> captains = new();

        // Captain Upgrades
        public List<VirtualItem> captainUpgrades = new();
        
        // TODO: remove - ship classes can no longer be bought directly
        // Ships
        public List<VirtualItem> shipClasses = new();
        
        // Games
        public List<VirtualItem> games = new();

        public List<VirtualItem> tickets = new();

        public List<VirtualItem> allItems = new();

        public void SaveToDisk()
        {
            DataAccessor.Save("inventory.data", this);
        }

        public void LoadFromDisk()
        {
            Debug.Log("Inventory.LoadFromDisk");
            var tempInventory = DataAccessor.Load<Inventory>("inventory.data");

            crystals = tempInventory.crystals;
            captains = tempInventory.captains;
            captainUpgrades = tempInventory.captainUpgrades;
            shipClasses = tempInventory.shipClasses;
            games = tempInventory.games;
            tickets = tempInventory.tickets;

            allItems = tempInventory.allItems;
        }

        public bool ContainsCaptain(string captainName)
        {
            return captains.Where(item => item.Name == captainName).Count() > 0;
        }

        public bool ContainsShipClass(string shipName)
        {
            return shipClasses.Where(item => item.Name == shipName).Count() > 0;
        }

        public bool ContainsGame(string gameName)
        {
            return games.Where(item => item.Name == gameName).Count() > 0;
        }
    }
}