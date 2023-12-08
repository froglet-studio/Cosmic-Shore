using System;
using System.Collections.Generic;

namespace CosmicShore.Integrations.Playfab.Economy
{
    [Serializable]
    public class Inventory
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


        // Vessel Knowledge
        // public List<VirtualItem> VesselKnowledge;
    }
}