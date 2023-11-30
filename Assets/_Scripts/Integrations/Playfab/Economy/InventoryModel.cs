using System;
using System.Collections.Generic;

namespace CosmicShore.Integrations.Playfab.Economy
{
    [Serializable]
    public class InventoryModel
    {
        // Crystals - including Omni Crystals and Elemental Crystals
        public List<VirtualItem> Crystals;

        // Vessels
        public List<VirtualItem> Vessels;


        // Vessel Upgrades
        public List<VirtualItem> VesselUpgrades;


        // Ships
        public List<VirtualItem> Ships;


        // MiniGames
        public List<VirtualItem> MiniGames;


        // Vessel Knowledge goes to player data 
        public List<VirtualItem> VesselKnowledge;
    }
}