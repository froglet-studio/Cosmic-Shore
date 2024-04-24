using System;
using System.Collections.Generic;

namespace CosmicShore.Integrations.Playfab.Economy
{
    [Serializable]
    public class Inventory
    {
        // Crystals - including Omni Crystals and Elemental Crystals
        public List<VirtualItem> crystals = new();

        // Guides
        public List<VirtualItem> guides = new();
        
        // Guide Upgrades
        public List<VirtualItem> guideUpgrades = new();
        
        // Ships
        public List<VirtualItem> ships = new();
        
        // MiniGames
        public List<VirtualItem> miniGames = new();
    }
}