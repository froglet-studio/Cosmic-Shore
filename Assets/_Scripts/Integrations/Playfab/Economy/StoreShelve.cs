using System;
using System.Collections.Generic;

namespace CosmicShore.Integrations.PlayFab.Economy
{
    [Serializable]
    public class StoreShelve
    {
        public Dictionary<string, VirtualItem> allItems = new();
        public Dictionary<string, VirtualItem> crystals = new();
        public Dictionary<string, VirtualItem> games = new();
        public Dictionary<string, VirtualItem> classes = new();
        public Dictionary<string, VirtualItem> captains = new();
        public Dictionary<string, VirtualItem> captainUpgrades = new();
        public Dictionary<string, VirtualItem> tickets = new();
        public VirtualItem FactionMissionTicket;
        public VirtualItem DailyChallengeTicket;
    }
}