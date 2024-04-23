using System;
using System.Collections.Generic;

namespace CosmicShore.Integrations.Playfab.Economy
{
    [Serializable]
    public class Inventory
    {
        // Crystals - including Omni Crystals and Elemental Crystals
        public List<VirtualItem> crystals = new();

        // Vessels
        public List<VirtualItem> vessels = new();
        
        // Vessel Upgrades
        public List<VirtualItem> vesselUpgrades = new();
        
        // Ships
        public List<VirtualItem> ships = new();
        
        // MiniGames
        public List<VirtualItem> miniGames = new();
    }
}