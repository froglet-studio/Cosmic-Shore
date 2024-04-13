using System;
using System.Collections.Generic;

namespace CosmicShore.Integrations.Playfab.Economy
{
    [Serializable]
    public class StoreShelve
    {
        public List<VirtualItem> crystals = new();
        public List<VirtualItem> miniGames = new();
        public List<VirtualItem> ships = new();
        public List<VirtualItem> dailyRewards = new();
    }
}