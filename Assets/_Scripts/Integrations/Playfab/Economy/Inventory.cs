using System;
using System.Collections.Generic;

namespace CosmicShore.Integrations.Playfab.Economy
{
    [Serializable]
    public class Inventory
    {
        // Crystals - including Omni Crystals and Elemental Crystals
        public List<VirtualItem> Crystals = new();

        // Vessels
        public List<VirtualItem> Vessels = new();
        
        // Vessel Upgrades
        public List<VirtualItem> VesselUpgrades = new();
        
        // Ships
        public List<VirtualItem> Ships = new();
        
        // MiniGames
        public List<VirtualItem> MiniGames = new();
    }
}