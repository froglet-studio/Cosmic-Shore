using System;
using System.Collections.Generic;

namespace CosmicShore.Integrations.Playfab.Economy
{
    [Serializable]
    public class StoreShelve
    {
        public List<VirtualItem> Crystals;
        public List<VirtualItem> MiniGames;
        public List<VirtualItem> Ships;
    }
}